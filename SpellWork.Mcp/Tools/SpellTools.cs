using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Spell;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class SpellTools
    {
        private readonly SpellDataService _data;

        public SpellTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "get_spell")]
        [Description("The full decoded view of one spell. format is \"json\" or \"text\". sections filters to any of: misc, attributes, effects, powers, targets, reagents, aura, procs, scaling, context, cooldowns, levels, interrupts, shapeshift, equippedItems, totems, items, triggers, visual (the 5.4.8 visual chain, same shape as get_spell_visual_chain; storeUnloaded true and missingStores naming the stores that did not load when a visual store is missing). Displayed numeric values are rounded (half away from zero) server formula results; the 5.4.8 server truncates, so a client tooltip may differ by 1. difficulty is a Difficulty.dbc id (0 = base, the default); every difficulty-keyed table (SpellMisc, SpellEffect, SpellCategories, SpellAuraOptions, SpellCooldowns, SpellLevels, SpellInterrupts, SpellTargetRestrictions) is read at that difficulty, and a spell with no row of its own there (per effect index for SpellEffect) uses its base row. An unknown difficulty is an error that lists the valid ids.")]
        public async Task<string> GetSpell(
            int id,
            string format = "json",
            string[] sections = null,
            uint difficulty = 0,
            uint? level = null,
            uint? itemLevel = null)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(id, out var spell))
                return NotFound(id);

            if (!string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
            {
                return ToolResponse.Error("invalid_format",
                    $"Unknown format '{format}'. Valid formats are: json, text.");
            }

            var difficultyError = ValidateDifficulty(difficulty);
            if (difficultyError != null)
                return difficultyError;

            var context = ResolveContext(level, itemLevel, difficulty);

            if (string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
            {
                var writer = new PlainTextSpellInfoWriter();
                spell.Write(writer, context);
                return ToolResponse.Ok(new Dictionary<string, object>
                {
                    ["id"] = spell.ID,
                    ["name"] = spell.Name,
                    ["text"] = writer.ToString(),
                });
            }

            try
            {
                // Serialize returns a complete JSON document, so it is spliced
                // in as raw JSON rather than re-serialised as a string value.
                var document = SpellInfoJsonSerializer.Serialize(spell, context, sections);
                return "{\"ok\":true,\"result\":" + document + "}";
            }
            catch (ArgumentException ex)
            {
                // The serializer throws for an unknown section name. That is a
                // caller mistake, not a server fault: it must come back as a
                // structured error naming the valid sections, not as an
                // exception the transport turns into a protocol-level failure.
                return ToolResponse.Error("invalid_section", ex.Message);
            }
        }

        /// <summary>
        /// Accepts "3" or "SPELLFAMILY_MAGE" for the same filter. An agent
        /// that has just read list_enum output holds the name, not the number,
        /// and making it convert first is a round trip for nothing.
        /// Returns false and sets <paramref name="error"/> when the text
        /// matches neither.
        /// </summary>
        internal static bool TryParseEnumArgument(string text, Type enumType, out long value, out string error)
        {
            value = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (long.TryParse(text, out value))
                return true;

            foreach (var name in Enum.GetNames(enumType))
            {
                if (!string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = Convert.ToInt64(Enum.Parse(enumType, name));
                return true;
            }

            error = $"'{text}' is neither a number nor a member of {enumType.Name}. " +
                    $"Call list_enum(\"{enumType.Name}\") for the valid names.";
            return false;
        }

        [McpServerTool(Name = "search_spells")]
        [Description("Find spells by any combination of name substring, family, aura type, effect type, target type, attribute bit and school mask. Enum arguments accept either a number or an enum member name. attribute is a mask or SpellAtribute member name tested against Attributes word 0, or \"word:value\" (word 0-13, value a number, 0x-hex mask or that word's enum member), e.g. \"13:0x1\". Returns id, name and a one-line summary per hit.")]
        public async Task<string> SearchSpells(
            string name = null,
            string family = null,
            string auraType = null,
            string effectType = null,
            string targetType = null,
            string attribute = null,
            uint? schoolMask = null,
            int limit = 50,
            int offset = 0)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            long? familyValue = null, auraValue = null, effectValue = null, targetValue = null;
            var attributeWord = 0;
            long? attributeValue = null;

            if (!TryResolve(family, typeof(SpellFamilyNames), ref familyValue, out var error) ||
                !TryResolve(auraType, typeof(AuraType), ref auraValue, out error) ||
                !TryResolve(effectType, typeof(SpellEffects), ref effectValue, out error) ||
                !TryResolve(targetType, typeof(Targets), ref targetValue, out error) ||
                !TryResolveAttribute(attribute, out attributeWord, out attributeValue, out error))
            {
                return ToolResponse.Error("invalid_enum_value", error);
            }

            // No Math.Max(1, ...) floor here: ToolResponse.Paged is the
            // single authoritative place that decides paging semantics, and
            // its documented contract is that a non-positive limit means
            // zero rows, never "at least one" and never "unbounded". Take
            // already returns empty for a non-positive count, so clamping
            // only the upper bound here and letting Paged sanitise the rest
            // is sufficient -- a second, disagreeing clamp upstream would
            // just be the drift that contract exists to prevent.
            var effectiveLimit = Math.Min(limit, ToolResponse.MaxRows);
            var effectiveOffset = Math.Max(0, offset);

            var matches = SpellWork.DBC.DBC.SpellInfoStore.Values
                .Where(spell => Matches(spell, name, familyValue, auraValue, effectValue, targetValue, attributeWord, attributeValue, schoolMask))
                .OrderBy(spell => spell.ID)
                .ToList();

            var rows = matches
                .Skip(effectiveOffset)
                .Take(effectiveLimit)
                .Select(spell => (object)new Dictionary<string, object>
                {
                    ["id"] = spell.ID,
                    ["name"] = spell.Name,
                    ["summary"] = Summarise(spell),
                })
                .ToList();

            return ToolResponse.Ok(ToolResponse.Paged(rows, matches.Count, effectiveOffset, effectiveLimit));
        }

        private static readonly Type[] AttributeWordEnums =
        {
            typeof(SpellAtribute), typeof(SpellAtributeEx), typeof(SpellAtributeEx2), typeof(SpellAtributeEx3),
            typeof(SpellAtributeEx4), typeof(SpellAtributeEx5), typeof(SpellAtributeEx6), typeof(SpellAtributeEx7),
            typeof(SpellAtributeEx8), typeof(SpellAtributeEx9), typeof(SpellAtributeEx10), typeof(SpellAtributeEx11),
            typeof(SpellAtributeEx12), typeof(SpellAtributeEx13),
        };

        /// <summary>
        /// "SPELL_ATTR0_PASSIVE" or "64" test word 0, as before. "13:0x1" or
        /// "7:SPELL_ATTR7_UNK0" test the named word (0-13); the value is a
        /// decimal or 0x-hex mask, or a member of that word's enum.
        /// </summary>
        private static bool TryResolveAttribute(string text, out int word, out long? mask, out string error)
        {
            word = 0;
            mask = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            var value = text.Trim();
            var colon = value.IndexOf(':');
            if (colon >= 0)
            {
                if (!int.TryParse(value.Substring(0, colon).Trim(), out word) || word < 0 || word >= AttributeWordEnums.Length)
                {
                    error = $"'{text}': the attribute word must be a number from 0 to {AttributeWordEnums.Length - 1}, as in \"13:0x1\".";
                    return false;
                }

                value = value.Substring(colon + 1).Trim();
            }

            var enumType = AttributeWordEnums[word];
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var hex))
                {
                    error = $"'{text}' is not a valid hexadecimal mask.";
                    return false;
                }

                mask = hex;
                return true;
            }

            if (!TryParseEnumArgument(value, enumType, out var parsed, out error))
                return false;

            mask = parsed;
            return true;
        }

        private static bool TryResolve(string text, Type enumType, ref long? slot, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (!TryParseEnumArgument(text, enumType, out var value, out error))
                return false;

            slot = value;
            return true;
        }

        private static bool Matches(SpellInfo spell, string name, long? family, long? aura,
            long? effect, long? target, int attributeWord, long? attribute, uint? schoolMask)
        {
            if (name != null && (spell.Name == null ||
                spell.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0))
                return false;

            if (family.HasValue && spell.SpellFamilyName != family.Value)
                return false;

            if (schoolMask.HasValue && (spell.SchoolMask & schoolMask.Value) == 0)
                return false;

            // An attribute filter is a mask tested against one of the fourteen
            // Attributes words (0-13); "word:value" picks the word, and a bare
            // value means word 0.
            if (attribute.HasValue && (spell.Attributes[attributeWord] & (uint)attribute.Value) == 0)
                return false;

            if (aura.HasValue && !spell.Effects.Select(r => r.Entry).Any(e => e != null && e.EffectAura == aura.Value))
                return false;

            if (effect.HasValue && !spell.Effects.Select(r => r.Entry).Any(e => e != null && e.Effect == effect.Value))
                return false;

            if (target.HasValue && !spell.Effects.Select(r => r.Entry).Any(e => e != null &&
                (e.ImplicitTarget[0] == target.Value || e.ImplicitTarget[1] == target.Value)))
                return false;

            return true;
        }

        // One line, cheap to compute: the first effect's type and aura, which
        // is what distinguishes near-identically-named spells in a hit list.
        private static string Summarise(SpellInfo spell)
        {
            var effect = spell.Effects.Select(r => r.Entry).FirstOrDefault(e => e != null);
            if (effect == null)
                return "no effects";

            var description = $"{(SpellEffects)effect.Effect}";
            if (effect.EffectAura != 0)
                description += $" / {(AuraType)effect.EffectAura}";

            return description;
        }

        [McpServerTool(Name = "get_spell_triggers")]
        [Description("Which spells this spell triggers (from its own effects, with the effect index) and which spells trigger it.")]
        public async Task<string> GetSpellTriggers(int id)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(id, out var spell))
                return NotFound(id);

            var triggers = spell.Effects.Select(r => r.Entry)
                .Where(effect => effect != null && effect.EffectTriggerSpell != 0)
                .Select(effect => (object)new Dictionary<string, object>
                {
                    ["id"] = effect.EffectTriggerSpell,
                    ["name"] = SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(effect.EffectTriggerSpell, out var target)
                        ? target.Name
                        : null,
                    ["effectIndex"] = effect.EffectIndex,
                })
                .ToList();

            var triggeredBy = new List<object>();
            if (SpellWork.DBC.DBC.SpellTriggerStore.TryGetValue(id, out var sources))
            {
                triggeredBy.AddRange(sources.OrderBy(sourceId => sourceId)
                    .Select(sourceId => (object)new Dictionary<string, object>
                    {
                        ["id"] = sourceId,
                        ["name"] = SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(sourceId, out var source)
                            ? source.Name
                            : null,
                    }));
            }

            return ToolResponse.Ok(new Dictionary<string, object>
            {
                ["id"] = spell.ID,
                ["name"] = spell.Name,
                ["triggers"] = triggers.Take(ToolResponse.MaxRows).ToList(),
                ["triggeredBy"] = triggeredBy.Take(ToolResponse.MaxRows).ToList(),
                ["truncated"] = triggers.Count > ToolResponse.MaxRows || triggeredBy.Count > ToolResponse.MaxRows,
            });
        }

        [McpServerTool(Name = "compare_spells")]
        [Description("Structured field-by-field diff of two spells. Returns each differing JSON path with both values.")]
        public async Task<string> CompareSpells(int idA, int idB)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(idA, out var spellA))
                return NotFound(idA);
            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(idB, out var spellB))
                return NotFound(idB);

            var context = ResolveContext(null, null, 0);

            // Diff the serialized documents rather than comparing fields by
            // hand: this covers every field get_spell emits, including ones
            // added later, and cannot drift away from what get_spell returns.
            // Four of the eighteen sections are left out, each for its own
            // reason: "context" renders identically on both sides by
            // construction (it echoes the RenderContext this call itself
            // chose, not anything about either spell); "items" and "triggers"
            // describe how each spell relates to the surrounding tables
            // (equippable items, spells that trigger or are triggered by it)
            // rather than the spell's own definition -- diffing them would
            // make two near-identical spells look different for reasons that
            // have nothing to do with either spell; and "visual" is a
            // deliberate omission rather than an oversight, because a visual
            // chain diffs as a wall of raw SpellVisualKit and
            // SpellVisualKitEffect ids whose meaning is unresolved in this
            // build (only effectType 2 has a confirmed target), so two spells
            // that look alike in every way a caller cares about still differ
            // in dozens of opaque numbers. get_spell_visual_chain compares
            // them properly, one spell at a time.
            var sections = new[]
            {
                "misc", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs",
                "scaling", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems", "totems",
            };

            // Identity stripped before the diff, not filtered out of the
            // result afterwards. SpellInfoJsonSerializer.Serialize writes "id"
            // and "name" into the root unconditionally, outside every
            // Wants(...) gate, so any comparison of two different spells
            // carried at least those two guaranteed differences -- making
            // "identical" true only when idA == idB, an advertised field that
            // was permanently false for the only comparison anyone runs. Both
            // values are already reported in the "a" and "b" blocks below, so
            // nothing is lost by removing them here.
            //
            // Removing them from the documents rather than from the collected
            // differences keeps every other number honest: they never consume
            // two of the MaxRows slots, never shift which difference the
            // truncation probe lands on, and never inflate totalDifferences --
            // all three of which a post-hoc filter would get subtly wrong,
            // because the recursion visits root keys in alphabetical order and
            // may or may not have reached "id"/"name" before hitting the cap.
            using var documentA = ParseWithoutIdentity(
                SpellInfoJsonSerializer.Serialize(spellA, context, sections));
            using var documentB = ParseWithoutIdentity(
                SpellInfoJsonSerializer.Serialize(spellB, context, sections));

            var differences = new List<object>();
            // Collect one past the cap, not exactly the cap: MaxRows
            // differences could mean "that is genuinely every difference" or
            // "the recursion stopped there and more were never inspected",
            // and those two situations must not report the same way. The
            // (MaxRows + 1)th difference, if the recursion ever reaches it,
            // is the probe that tells them apart -- it is never shown to the
            // caller, only used to answer "was anything withheld".
            Diff("", documentA.RootElement, documentB.RootElement, differences, ToolResponse.MaxRows + 1);

            // Exact, not "at least": collecting the probe element means a
            // count of MaxRows + 1 can only happen when the traversal found
            // a real (MaxRows + 1)th difference, never as an artefact of the
            // cap itself, so "> MaxRows" now means what it says.
            var truncated = differences.Count > ToolResponse.MaxRows;

            var result = new Dictionary<string, object>
            {
                ["a"] = new Dictionary<string, object> { ["id"] = spellA.ID, ["name"] = spellA.Name },
                ["b"] = new Dictionary<string, object> { ["id"] = spellB.ID, ["name"] = spellB.Name },
                ["identical"] = differences.Count == 0,
                ["differences"] = differences.Take(ToolResponse.MaxRows).ToList(),
                ["truncated"] = truncated,
            };

            // Only meaningful as a true total when nothing was withheld: once
            // truncated is true, differences.Count is capped at MaxRows + 1
            // by the probe above, an artefact of how far the recursion was
            // allowed to run, not the real number of differences between the
            // two documents (which could be far larger and was never
            // counted, since Diff stops as soon as the probe is collected).
            // Reporting that capped count under a name that says "total"
            // would be actively misleading, so the field is omitted rather
            // than mislabelled -- it appears only when its value is exact.
            if (!truncated)
                result["totalDifferences"] = differences.Count;

            return ToolResponse.Ok(result);
        }

        /// <summary>
        /// The root properties that identify <em>which</em> spell a document
        /// describes rather than anything about its definition. Named here as
        /// data so the reason they are dropped is stated once; see
        /// <see cref="CompareSpells"/> for that reason.
        /// </summary>
        internal static readonly string[] IdentityProperties = { "id", "name" };

        /// <summary>
        /// Parses a serialized spell document with <see cref="IdentityProperties"/>
        /// removed from its root. Every other property is copied through
        /// verbatim -- <c>JsonProperty.WriteTo</c> reproduces the original
        /// token exactly -- so the diff paths, values and ordering are
        /// identical to those of the unfiltered document.
        /// </summary>
        private static System.Text.Json.JsonDocument ParseWithoutIdentity(string json)
        {
            using var source = System.Text.Json.JsonDocument.Parse(json);

            var buffer = new System.IO.MemoryStream();
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (var property in source.RootElement.EnumerateObject())
                {
                    if (Array.IndexOf(IdentityProperties, property.Name) >= 0)
                        continue;

                    property.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            return System.Text.Json.JsonDocument.Parse(buffer.ToArray());
        }

        private static void Diff(string path, System.Text.Json.JsonElement a, System.Text.Json.JsonElement b,
            List<object> differences, int cap)
        {
            // Stop descending once the cap is reached: a deep recursion that
            // collects 50,000 differences to then discard all but 500 wastes
            // the time of the caller waiting on it. The caller decides what
            // "reached" means -- CompareSpells passes MaxRows + 1 so the one
            // extra difference it allows through can serve as the truncation
            // probe described where Diff is called.
            if (differences.Count >= cap)
                return;

            if (a.ValueKind != b.ValueKind)
            {
                differences.Add(Difference(path, a, b));
                return;
            }

            switch (a.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    var keys = a.EnumerateObject().Select(p => p.Name)
                        .Union(b.EnumerateObject().Select(p => p.Name))
                        .OrderBy(name => name, StringComparer.Ordinal);
                    foreach (var key in keys)
                    {
                        // The cap check at the top of this method only runs
                        // once per call. Without also checking it here, a
                        // single object whose keys are mostly missing on one
                        // side (a caller-controlled shape, not bounded by
                        // anything about spells) can add every one of those
                        // keys in this same loop, unboundedly, before the
                        // check is ever reconsidered -- the recursive
                        // Diff(...) call below is not reached for a missing
                        // key, so its own entry check never gets a chance to
                        // stop this branch.
                        if (differences.Count >= cap)
                            break;

                        var childPath = path.Length == 0 ? key : path + "." + key;
                        var hasA = a.TryGetProperty(key, out var childA);
                        var hasB = b.TryGetProperty(key, out var childB);

                        if (!hasA || !hasB)
                        {
                            differences.Add(new Dictionary<string, object>
                            {
                                ["path"] = childPath,
                                ["a"] = hasA ? childA.ToString() : null,
                                ["b"] = hasB ? childB.ToString() : null,
                            });
                            continue;
                        }

                        Diff(childPath, childA, childB, differences, cap);
                    }
                    break;

                case System.Text.Json.JsonValueKind.Array:
                    var arrayA = a.EnumerateArray().ToList();
                    var arrayB = b.EnumerateArray().ToList();
                    for (var i = 0; i < Math.Max(arrayA.Count, arrayB.Count); i++)
                    {
                        // Same reasoning as the object case above: a mismatched
                        // array length can add one difference per extra index
                        // in this loop without ever reaching the recursive
                        // call that would otherwise re-check the cap.
                        if (differences.Count >= cap)
                            break;

                        var childPath = $"{path}[{i}]";
                        if (i >= arrayA.Count || i >= arrayB.Count)
                        {
                            differences.Add(new Dictionary<string, object>
                            {
                                ["path"] = childPath,
                                ["a"] = i < arrayA.Count ? arrayA[i].ToString() : null,
                                ["b"] = i < arrayB.Count ? arrayB[i].ToString() : null,
                            });
                            continue;
                        }

                        Diff(childPath, arrayA[i], arrayB[i], differences, cap);
                    }
                    break;

                default:
                    if (!string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal))
                        differences.Add(Difference(path, a, b));
                    break;
            }
        }

        private static Dictionary<string, object> Difference(string path,
            System.Text.Json.JsonElement a, System.Text.Json.JsonElement b) =>
            new Dictionary<string, object>
            {
                ["path"] = path,
                ["a"] = a.ToString(),
                ["b"] = b.ToString(),
            };

        internal static RenderContext ResolveContext(uint? level, uint? itemLevel, uint difficulty) =>
            new RenderContext(
                level ?? SpellWork.DBC.DBC.MaxLevel,
                itemLevel ?? 475,
                (int)difficulty);

        // Shared by get_spell and calculate_spell_scaling: null when the
        // difficulty is 0 or a Difficulty.dbc row, else the error response.
        internal static string ValidateDifficulty(uint difficulty)
        {
            if (difficulty == 0 || (SpellWork.DBC.DBC.Difficulty != null && SpellWork.DBC.DBC.Difficulty.ContainsKey((int)difficulty)))
                return null;

            return ToolResponse.Error("unknown_difficulty", $"No Difficulty.dbc row {difficulty}.", new Dictionary<string, object>
            {
                ["valid"] = (SpellWork.DBC.DBC.Difficulty?.Keys ?? Enumerable.Empty<int>()).OrderBy(k => k).Prepend(0).ToList()
            });
        }

        internal static string DataError(SpellData data) =>
            ToolResponse.Error(data.ErrorCode ?? "client_data_unavailable",
                data.ErrorMessage ?? "Client data is not loaded.",
                data.ErrorVariable == null
                    ? null
                    : new Dictionary<string, object> { ["variable"] = data.ErrorVariable });

        // Spec section 9: an unknown id gets nearest-name suggestions from
        // SpellName, so the agent can recover from a typo or a stale id
        // without a second round trip.
        internal static string NotFound(int id) =>
            ToolResponse.Error("spell_not_found", $"No spell with id {id}.",
                new Dictionary<string, object>
                {
                    ["nearestIds"] = SpellWork.DBC.DBC.SpellInfoStore.Keys
                        .OrderBy(candidate => Math.Abs((long)candidate - id))
                        .Take(5)
                        .Select(candidate => new Dictionary<string, object>
                        {
                            ["id"] = candidate,
                            ["name"] = SpellWork.DBC.DBC.SpellInfoStore[candidate].Name,
                        })
                        .Cast<object>()
                        .ToList(),
                });
    }
}
