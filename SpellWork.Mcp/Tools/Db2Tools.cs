using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DBFileReaderLib;
using ModelContextProtocol.Server;

namespace SpellWork.Mcp.Tools
{
    /// <summary>
    /// Raw store access, layered directly over the <c>Storage&lt;T&gt;</c>
    /// properties SpellWork.DBC.DBC loads (spec section 6.1). The client
    /// files SpellWork does not model are visible to <see cref="ListDb2"/> as
    /// a directory listing only; the counts are discovered by reflection and
    /// by directory enumeration, so they are described, never enforced.
    /// <see cref="GetDb2Row"/> and <see cref="QueryDb2"/> refuse to read them
    /// positionally -- an empty <c>field_0</c>-style row presented as data
    /// is worse than a clear refusal naming the limitation.
    /// </summary>
    [McpServerToolType]
    public sealed class Db2Tools
    {
        // Same BindingFlags as SpellWork.DBC.DBC.LoadAsync's own reflection
        // scan over its Storage<T> properties (DBC.cs) and
        // SpellDataService.CountStores's copy of it -- deliberately, not
        // Static | Public alone. A prior review found two reflection sites
        // in this codebase using different flag sets and silently
        // under-reporting stores; matching this exactly is what keeps a
        // third site from becoming a fourth.
        private const BindingFlags DbcPropertyFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly SpellDataService _data;

        public Db2Tools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "list_db2")]
        [Description("Every .dbc file in SPELLWORK_DBC_PATH (3.3.5a has no .db2 files), one row per file: name (the file name without extension), extension (\"dbc\"), modelled (SpellWork has a structure for it, so real field names are available; store is then the store name), loaded (the store was read from this file) and rowCount. CoA-only files SpellWork does not model, such as SpellAddon, are listed with modelled false. A modelled store with no file is listed with extension null. Only modelled stores can be read with get_db2_row and query_db2, by store name in any case.")]
        public async Task<string> ListDb2()
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            var modelled = ModelledStores();
            var propertyByFile = StorePropertyByFileName();
            var opened = SpellWork.DBC.DBC.OpenedFiles;
            var stores = new List<(string Name, string Extension, Dictionary<string, object> Row)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in PresentFiles())
            {
                propertyByFile.TryGetValue(file.Name, out var property);
                seen.Add(file.Name);

                var isModelled = property != null;
                var isLoaded = isModelled && opened.TryGetValue(property, out var openedFile) &&
                    string.Equals(openedFile.Extension, file.Extension, StringComparison.OrdinalIgnoreCase);

                var row = new Dictionary<string, object>
                {
                    ["name"] = file.Name,
                    ["extension"] = file.Extension,
                    ["modelled"] = isModelled,
                    ["loaded"] = isLoaded,
                    ["rowCount"] = isLoaded ? (modelled[property] as ICollection)?.Count ?? 0 : 0,
                };
                if (isModelled)
                    row["store"] = property;
                stores.Add((file.Name, file.Extension, row));
            }

            foreach (var pair in propertyByFile)
            {
                if (seen.Contains(pair.Key))
                    continue;

                stores.Add((pair.Key, string.Empty, new Dictionary<string, object>
                {
                    ["name"] = pair.Key,
                    ["extension"] = null,
                    ["modelled"] = true,
                    ["loaded"] = false,
                    ["rowCount"] = 0,
                    ["store"] = pair.Value,
                }));
            }

            var ordered = stores
                .OrderBy(s => s.Name, StringComparer.Ordinal)
                .ThenBy(s => s.Extension, StringComparer.Ordinal)
                .Select(s => (object)s.Row)
                .ToList();

            return ToolResponse.Ok(new Dictionary<string, object>
            {
                ["stores"] = ordered,
                ["modelledCount"] = modelled.Count,
                ["presentCount"] = ordered.Count,
                ["note"] = "Unmodelled stores expose fields only positionally and are not readable by get_db2_row or query_db2 in this version.",
            });
        }

        [McpServerTool(Name = "get_db2_row")]
        [Description("One row from a modelled .dbc store (e.g. Spell, SpellDifficulty, SpellAffect), by id, with real field names. Call list_db2 for the available stores.")]
        public async Task<string> GetDb2Row(string store, int id)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            if (!TryGetStore(store, out var name, out var storage, out var error))
                return error;

            var dictionary = (IDictionary)storage;
            if (!dictionary.Contains(id))
            {
                return ToolResponse.Error("row_not_found", $"Store '{name}' has no row with id {id}.",
                    new Dictionary<string, object> { ["store"] = name, ["rowCount"] = dictionary.Count });
            }

            return ToolResponse.Ok(new Dictionary<string, object>
            {
                ["store"] = name,
                ["id"] = id,
                ["modelled"] = true,
                ["fieldNamesAvailable"] = true,
                ["row"] = ToRow(dictionary[id]),
            });
        }

        [McpServerTool(Name = "query_db2")]
        [Description("Rows from a modelled .dbc store. filter is comma-separated field=value pairs, e.g. \"ModifierSpellID=12345,AffectedSpellID=133\" on SpellAffect; values are compared as case-insensitive strings (an array field as its comma-joined values). No operators or expressions.")]
        public async Task<string> QueryDb2(string store, string filter = null, int limit = 50, int offset = 0)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            if (!TryGetStore(store, out var name, out var storage, out var error))
                return error;

            var dictionary = (IDictionary)storage;
            var recordType = storage.GetType().GetGenericArguments()[0];
            var fields = RowMembers(recordType).ToList();

            var predicates = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                foreach (var clause in filter.Split(','))
                {
                    var parts = clause.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                    {
                        return ToolResponse.Error("invalid_filter",
                            $"Filter clause '{clause}' is not of the form field=value.");
                    }

                    var field = parts[0].Trim();
                    if (!fields.Any(member => string.Equals(member, field, StringComparison.OrdinalIgnoreCase)))
                    {
                        return ToolResponse.Error("unknown_field",
                            $"Store '{name}' has no field '{field}'.",
                            new Dictionary<string, object> { ["availableFields"] = fields });
                    }

                    predicates.Add(new KeyValuePair<string, string>(field, parts[1].Trim()));
                }
            }

            // No lower floor: ToolResponse.Paged is the authoritative clamp and
            // its contract is that a non-positive limit yields zero rows, never
            // one and never unbounded. A second, disagreeing clamp here is
            // exactly the drift Paged was made authoritative to prevent.
            var effectiveLimit = Math.Min(limit, ToolResponse.MaxRows);
            var effectiveOffset = Math.Max(0, offset);

            var matched = new List<object>();
            var total = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                var row = ToRow(entry.Value);
                if (!predicates.All(predicate => Matches(row, predicate)))
                    continue;

                total++;
                if (total <= effectiveOffset || matched.Count >= effectiveLimit)
                    continue;

                matched.Add(row);
            }

            var payload = ToolResponse.Paged(matched, total, effectiveOffset, effectiveLimit);
            payload["store"] = name;
            payload["modelled"] = true;
            payload["fieldNamesAvailable"] = true;
            return ToolResponse.Ok(payload);
        }

        private static bool Matches(Dictionary<string, object> row, KeyValuePair<string, string> predicate)
        {
            var key = row.Keys.FirstOrDefault(k => string.Equals(k, predicate.Key, StringComparison.OrdinalIgnoreCase));
            if (key == null)
                return false;

            var value = row[key];
            return string.Equals(FormatValue(value), predicate.Value, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;

            // An array field compares against its comma-joined form so a
            // filter can still address it, rather than matching on the CLR
            // type name that ToString() would produce.
            if (value is IEnumerable enumerable && !(value is string))
                return string.Join(",", enumerable.Cast<object>().Select(FormatValue));

            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> ToRow(object record)
        {
            var row = new Dictionary<string, object>();
            if (record == null)
                return row;

            foreach (var field in record.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                row[field.Name] = field.GetValue(record);

            foreach (var property in record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length == 0)
                    row[property.Name] = property.GetValue(record);
            }

            return row;
        }

        private static IEnumerable<string> RowMembers(Type recordType) =>
            recordType.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name)
                .Concat(recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0).Select(p => p.Name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal);
        private sealed class PresentFile
        {
            public string Name { get; set; }
            public string Extension { get; set; }
        }

        /// <summary>
        /// Every <c>.dbc</c> file actually present in the effective DBC
        /// directory, one entry per name (3.3.5a has no .db2 files, and
        /// SPELLWORK_DB2_PATH is ignored). This is the one place either the listing in
        /// <see cref="ListDb2"/> or the <c>store_not_modelled</c> decision
        /// in <see cref="TryGetStore"/> ever asks "what files exist" --
        /// deliberately, so the answer can only ever be a real directory
        /// enumeration and never a path built out of caller-supplied text.
        /// A store name that is itself a rooted path (or contains `..`)
        /// simply will not appear in this list, however it's spelled, so
        /// there is no path string to validate or blacklist.
        /// </summary>
        private IEnumerable<PresentFile> PresentFiles()
        {
            const string extension = "dbc";
            var directory = SpellWork.DBC.ClientFileResolver.EffectiveDbcDirectory(_data.Config.ToSpellWorkConfig());
            if (directory == null || !Directory.Exists(directory))
                yield break;

            // "*.dbc" also matches longer extensions on Windows ("x.dbcx");
            // the extension is compared exactly below.
            foreach (var file in Directory.GetFiles(directory, "*." + extension))
            {
                if (!string.Equals(Path.GetExtension(file), "." + extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return new PresentFile { Name = Path.GetFileNameWithoutExtension(file), Extension = extension };
            }
        }

        /// <summary>Client file name to store property name (the same name in 3.3.5a; see DBC.FileNameFor).</summary>
        private static Dictionary<string, string> StorePropertyByFileName()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in typeof(SpellWork.DBC.DBC).GetProperties(DbcPropertyFlags))
            {
                if (property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition() == typeof(Storage<>))
                    map[SpellWork.DBC.DBC.FileNameFor(property)] = property.Name;
            }

            return map;
        }

        private static Dictionary<string, object> ModelledStores()
        {
            var stores = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in typeof(SpellWork.DBC.DBC).GetProperties(DbcPropertyFlags))
            {
                if (!property.PropertyType.IsGenericType ||
                    property.PropertyType.GetGenericTypeDefinition() != typeof(Storage<>))
                    continue;

                stores[property.Name] = property.GetValue(null);
            }

            return stores;
        }

        private bool TryGetStore(string requested, out string name, out object storage, out string error)
        {
            name = null;
            storage = null;
            error = null;

            var modelled = ModelledStores();
            // A store property name ("ItemSparse") or its client file name
            // ("Item-sparse") both select the store.
            var match = modelled.Keys.FirstOrDefault(key =>
                string.Equals(key, requested, StringComparison.OrdinalIgnoreCase));
            if (match == null && requested != null &&
                StorePropertyByFileName().TryGetValue(requested, out var byFile))
                match = byFile;

            if (match == null)
            {
                // Distinguish "no such file" from "present but not modelled":
                // the second is a known limitation with a documented reason,
                // and telling the caller "unknown store" for it sends them
                // looking for a typo that is not there. Decided from the
                // same directory enumeration list_db2 reports (see
                // PresentFiles), never from a path built out of
                // "requested" -- a raw File.Exists probe on
                // Path.Combine(DbcPath, Locale, requested + ".db2") let a
                // rooted "requested" (Path.Combine's "a later rooted
                // segment wins" rule) escape the configured directory
                // entirely and answer for whatever happened to exist there
                // instead. Comparing against real file names present on
                // disk has no such escape: a path string simply cannot
                // equal a bare file-name-without-extension, however it's
                // spelled.
                var present = PresentFiles().Any(file =>
                    string.Equals(file.Name, requested, StringComparison.OrdinalIgnoreCase));

                error = present
                    ? ToolResponse.Error("store_not_modelled",
                        $"'{requested}' is present as a client file but SpellWork does not model it, so its fields are only " +
                        "available positionally. Reading unmodelled stores is not supported in this version.",
                        new Dictionary<string, object> { ["store"] = requested })
                    : ToolResponse.Error("unknown_store", $"No store named '{requested}'.",
                        new Dictionary<string, object>
                        {
                            ["modelledStores"] = modelled.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                        });
                return false;
            }

            if (modelled[match] == null)
            {
                error = ToolResponse.Error("store_not_loaded",
                    $"Store '{match}' is modelled but was not loaded -- its .dbc file is missing from " +
                    $"'{_data.Config.DbcPath}'.",
                    new Dictionary<string, object> { ["variable"] = "SPELLWORK_DBC_PATH" });
                return false;
            }

            name = match;
            storage = modelled[match];
            return true;
        }
    }
}
