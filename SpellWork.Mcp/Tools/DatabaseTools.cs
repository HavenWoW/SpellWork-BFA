using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using MySql.Data.MySqlClient;
using SpellWork.Database;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class DatabaseTools
    {
        /// <summary>MySQL's ER_NO_SUCH_TABLE. The one failure that means
        /// "this schema does not have that table" rather than "something went
        /// wrong".</summary>
        internal const int MySqlErrorNoSuchTable = 1146;

        // Every table get_spell_db_overrides collects by spell id, with the
        // column that holds the spell id in each (AzerothCore/CoA db_world
        // base SQL). The names are inconsistent, which is why the column
        // travels with the table name rather than being assumed.
        //
        // A table the connected schema does not have is still reported as
        // absent (missingTables), not as a failure, so the tool stays usable
        // against other schemas.
        internal static readonly (string Table, string IdColumn)[] OverrideTables =
        {
            ("spell_proc", "SpellId"),
            ("spell_proc_event", "entry"),
            ("spell_script_names", "spell_id"),
            ("spell_custom_attr", "spell_id"),
            ("spell_bonus_data", "entry"),
            ("spell_threat", "entry"),
            ("spell_ranks", "spell_id"),
            ("spell_linked_spell", "spell_trigger"),
            ("spell_dbc", "ID"),
            ("spell_cooldown_overrides", "Id"),
            ("spell_area", "spell"),
            ("spell_group", "spell_id"),
            ("spell_required", "spell_id"),
        };

        /// <summary>The key a row can carry for the queried spell, relative
        /// to the spell id and its first rank.</summary>
        internal enum KeyForm { Id, NegId, FirstRank, NegFirstRank }

        // Tables whose loaders do not match on the plain spell id alone. Each
        // entry lists the stored key forms that apply to a spell and why.
        // Tables absent here are exact-id only.
        //  - spell_proc: negative SpellId = every rank of the spell whose
        //    first rank is |id| (SpellMgr.cpp LoadSpellProcs).
        //  - spell_proc_event: legacy, not loaded by the CoA server (only
        //    spell_proc is); matched like spell_proc for continuity.
        //  - spell_script_names: spell_id <= 0 = all ranks of first rank
        //    -spell_id (ObjectMgr.cpp:6472-6520 LoadSpellScriptNames).
        //  - spell_group: only first ranks are listed and membership covers
        //    every rank (SpellMgr.cpp LoadSpellGroups, GetFirstSpellInChain).
        //  - spell_linked_spell: negative spell_trigger = fires when the aura
        //    is removed (SpellMgr.cpp LoadSpellLinked).
        //  - spell_bonus_data, spell_threat: the exact row, else the first
        //    rank's row (SpellMgr.cpp:990-1019 GetSpellBonusData /
        //    GetSpellThreatEntry); see FallbackTables.
        internal static readonly Dictionary<string, (KeyForm Form, string MatchedBy)[]> MatchRules =
            new Dictionary<string, (KeyForm, string)[]>
            {
                ["spell_proc"] = new[] { (KeyForm.Id, "exact"), (KeyForm.NegFirstRank, "allRanks") },
                ["spell_proc_event"] = new[] { (KeyForm.Id, "exact"), (KeyForm.NegFirstRank, "allRanks") },
                ["spell_script_names"] = new[] { (KeyForm.Id, "exact"), (KeyForm.NegFirstRank, "allRanks") },
                ["spell_group"] = new[] { (KeyForm.Id, "exact"), (KeyForm.FirstRank, "firstRank") },
                ["spell_linked_spell"] = new[] { (KeyForm.Id, "exact"), (KeyForm.NegId, "onRemoval") },
                ["spell_bonus_data"] = new[] { (KeyForm.Id, "exact"), (KeyForm.FirstRank, "firstRank") },
                ["spell_threat"] = new[] { (KeyForm.Id, "exact"), (KeyForm.FirstRank, "firstRank") },
            };

        /// <summary>
        /// Tables the server reads either/or: the exact row wins, and the
        /// first rank's row applies only when there is no exact row. The
        /// first-rank rows are dropped from the result whenever an exact row
        /// came back, so the rows reported are the ones the server uses.
        /// </summary>
        internal static readonly HashSet<string> FallbackTables =
            new HashSet<string> { "spell_bonus_data", "spell_threat" };

        /// <summary>Applies <see cref="FallbackTables"/>' either/or to
        /// rows already labelled with matchedBy.</summary>
        internal static List<Dictionary<string, object>> ApplyFallback(string table, List<Dictionary<string, object>> rows)
        {
            if (!FallbackTables.Contains(table) || !rows.Any(r => Equals(r["matchedBy"], "exact")))
                return rows;
            return rows.Where(r => Equals(r["matchedBy"], "exact")).ToList();
        }

        private static long KeyValue(KeyForm form, int id, int first) => form switch
        {
            KeyForm.Id => id,
            KeyForm.NegId => -(long)id,
            KeyForm.FirstRank => first,
            _ => -(long)first,
        };

        /// <summary>The distinct key values to match for a table, in rule
        /// order, each with the matchedBy label of its first rule.</summary>
        internal static List<(long Value, string MatchedBy)> KeysFor(string table, int id, int first)
        {
            var rules = MatchRules.TryGetValue(table, out var r) ? r : new[] { (KeyForm.Id, "exact") };
            var keys = new List<(long, string)>();
            foreach (var (form, label) in rules)
            {
                var v = KeyValue(form, id, first);
                if (!keys.Any(k => k.Item1 == v))
                    keys.Add((v, label));
            }
            return keys;
        }

        private readonly SpellDataService _data;

        public DatabaseTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "query_world_db")]
        [Description("Run one read-only SELECT against the configured world database. Multiple statements, DDL and DML are rejected. database is optional: omitted, empty or \"world\" means the configured world database (SPELLWORK_MYSQL_WORLD_DB); any other name must equal it. The server should be configured with a read-only MySQL user.")]
        public Task<string> QueryWorldDb(string sql, string database = null, int limit = 100)
        {
            // Validate first, unconditionally: a malformed query is a caller
            // error whether or not a database happens to be reachable, and
            // rejecting before any connection is attempted is what makes this
            // testable with no server present.
            if (!SqlGuard.TryValidate(sql, out var rejection))
                return Task.FromResult(ToolResponse.Error("sql_rejected", rejection));

            var config = _data.Config;
            if (!config.MySqlConfigured)
                return Task.FromResult(NotConfigured());

            // "world" is the generic name (5.4.8's default); it and an omitted
            // value both mean the configured world database, whatever it is
            // called (acore_world, ...).
            if (!IsWorldDatabaseAlias(database) &&
                !string.Equals(database, config.WorldDatabase, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ToolResponse.Error("unknown_database",
                    $"This server is configured for database '{config.WorldDatabase}'. " +
                    "Set SPELLWORK_MYSQL_WORLD_DB to query a different one.",
                    new Dictionary<string, object> { ["configuredDatabase"] = config.WorldDatabase }));
            }

            var effectiveLimit = EffectiveLimit(limit);

            try
            {
                var worldDatabase = new WorldDatabase(config.ToSpellWorkConfig());
                var result = worldDatabase.SelectRows(sql, effectiveLimit);

                return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
                {
                    ["database"] = config.WorldDatabase,
                    ["columns"] = result.Columns,
                    ["rows"] = result.Rows.Cast<object>().ToList(),
                    ["count"] = result.Rows.Count,
                    ["limit"] = effectiveLimit,
                    ["truncated"] = result.Truncated,
                    ["maxRows"] = ToolResponse.MaxRows,
                }));
            }
            catch (Exception ex)
            {
                // The driver's message names the syntax error or the missing
                // table, which is what the agent needs to fix its query.
                return Task.FromResult(ToolResponse.Error("query_failed", ex.Message));
            }
        }

        [McpServerTool(Name = "get_spell_db_overrides")]
        [Description("Rows for one spell id from whichever AzerothCore/CoA spell override tables this world database has: spell_proc, spell_proc_event, spell_script_names, spell_custom_attr, spell_bonus_data, spell_threat, spell_ranks, spell_linked_spell, spell_dbc, spell_cooldown_overrides, spell_area, spell_group and spell_required. Rows carry matchedBy: exact, allRanks (negative key naming the first rank), firstRank (spell_group lists first ranks; spell_bonus_data and spell_threat fall back to the first rank's row only when the spell has no row of its own, as the server does) or onRemoval (negative spell_linked_spell trigger). spell_proc_event is legacy: the CoA server does not load it (only spell_proc), so its rows have no effect. Tables the schema does not have are listed under missingTables, which is expected and not an error.")]
        public Task<string> GetSpellDbOverrides(int id)
        {
            var config = _data.Config;
            if (!config.MySqlConfigured)
                return Task.FromResult(NotConfigured());

            var worldDatabase = new WorldDatabase(config.ToSpellWorkConfig());

            // Ask once whether the server is reachable at all. Without this a
            // server that is simply down reports the same connection error
            // once per table, and the caller has to read all
            // of them to work out that none of them is about a table.
            if (!worldDatabase.TestConnection(out var connectionError))
            {
                return Task.FromResult(ToolResponse.Error("connection_failed",
                    $"Could not connect to '{config.WorldDatabase}' on {config.MySqlHost}: {connectionError}"));
            }

            var tables = new Dictionary<string, object>();
            var missing = new List<string>();
            var truncated = new List<string>();
            var errors = new Dictionary<string, object>();

            // First rank of the spell's chain, for the rank-aware tables.
            var first = id;
            try
            {
                var rank = worldDatabase.SelectRows(
                    "SELECT `first_spell_id` FROM `spell_ranks` WHERE `spell_id` = @id", 1, ("@id", id));
                if (rank.Rows.Count > 0 && rank.Rows[0]["first_spell_id"] != null)
                    first = Convert.ToInt32(rank.Rows[0]["first_spell_id"]);
            }
            catch (Exception ex) when (IsMissingTable(ex)) { /* no chains: the id is its own first rank */ }
            catch (Exception) { /* reported by the spell_ranks query below */ }

            foreach (var (table, idColumn) in OverrideTables)
            {
                // Key values are bound (@k0, @k1, ...). Table and column
                // names are compile-time constants from OverrideTables.
                var keys = KeysFor(table, id, first);
                var args = new List<(string, object)>();
                var names = new List<string>();
                foreach (var key in keys)
                {
                    names.Add("@k" + names.Count);
                    args.Add((names[names.Count - 1], key.Value));
                }
                var sql = $"SELECT * FROM `{table}` WHERE `{idColumn}` IN ({string.Join(", ", names)})";

                try
                {
                    var result = worldDatabase.SelectRows(sql, ToolResponse.MaxRows, args.ToArray());
                    foreach (var row in result.Rows)
                    {
                        var stored = Convert.ToInt64(row[idColumn]);
                        row["matchedBy"] = keys.First(k => k.Value == stored).MatchedBy;
                    }
                    tables[table] = ApplyFallback(table, result.Rows.ToList()).Cast<object>().ToList();
                    if (result.Truncated)
                        truncated.Add(table);
                }
                catch (Exception ex) when (IsMissingTable(ex))
                {
                    // Expected, not a failure: some of these tables are
                    // absent from a given world schema (installs differ), so this
                    // branch can fire on every call against it. Reporting an
                    // expected absence in the same shape as a real fault --
                    // a syntax error, a permission denial, a wrong id column
                    // -- would train the caller to ignore both.
                    missing.Add(table);
                }
                catch (Exception ex)
                {
                    errors[table] = ex.Message;
                }
            }

            var payload = new Dictionary<string, object>
            {
                ["id"] = id,
                ["database"] = config.WorldDatabase,
                ["tables"] = tables,
            };

            if (missing.Count > 0)
            {
                payload["missingTables"] = missing;
                payload["missingTablesNote"] =
                    "These tables do not exist in this schema (MySQL error 1146). Installs differ in which " +
                    "override tables they keep; this is not a failure.";
            }

            if (truncated.Count > 0)
                payload["truncatedTables"] = truncated;

            if (errors.Count > 0)
                payload["unavailableTables"] = errors;

            return Task.FromResult(ToolResponse.Ok(payload));
        }

        /// <summary>
        /// The row ceiling this call actually applied, which is also the value
        /// the response echoes as "limit".
        /// <para>
        /// No lower floor on the rows returned: ToolResponse.Paged is the
        /// authoritative clamp and its contract says a non-positive limit
        /// yields zero rows, never one and never "unbounded". A second,
        /// disagreeing clamp here is exactly the drift Paged was made
        /// authoritative to prevent. What the negative branch fixes is the
        /// *echo*: Math.Min(-5, MaxRows) reports -5 back to a caller that got
        /// zero rows, while Paged -- which the other twelve paging tools go
        /// through -- sanitises the same input to 0 and documents that the
        /// reported limit is the effective applied ceiling. Thirteen tools
        /// must not disagree about what "limit" means. Passing 0 rather than
        /// -5 to SelectRows returns the same zero rows either way, so this
        /// changes no result set.
        /// </para>
        /// </summary>
        internal static bool IsWorldDatabaseAlias(string database) =>
            string.IsNullOrWhiteSpace(database) ||
            string.Equals(database, "world", StringComparison.OrdinalIgnoreCase);

        internal static int EffectiveLimit(int limit) =>
            Math.Min(limit < 0 ? 0 : limit, ToolResponse.MaxRows);

        /// <summary>
        /// True only for MySQL's "table does not exist". Deliberately keyed on
        /// the driver's error number rather than on the message text: the
        /// message is localised and reworded between server versions, and a
        /// text match that drifted would start filing real faults -- a
        /// permission denial, a syntax error -- as "this table is simply not
        /// here", which is the one mistake this split exists to prevent.
        /// </summary>
        internal static bool IsMissingTable(Exception ex) =>
            ex is MySqlException mysql && mysql.Number == MySqlErrorNoSuchTable;

        private static string NotConfigured() =>
            ToolResponse.Error("mysql_not_configured",
                "No MySQL connection is configured. Set SPELLWORK_MYSQL_HOST, SPELLWORK_MYSQL_USER and " +
                "SPELLWORK_MYSQL_WORLD_DB (and SPELLWORK_MYSQL_PASS / SPELLWORK_MYSQL_PORT if needed).",
                new Dictionary<string, object>
                {
                    ["variables"] = new[]
                    {
                        "SPELLWORK_MYSQL_HOST", "SPELLWORK_MYSQL_PORT", "SPELLWORK_MYSQL_USER",
                        "SPELLWORK_MYSQL_PASS", "SPELLWORK_MYSQL_WORLD_DB",
                    },
                });
    }
}
