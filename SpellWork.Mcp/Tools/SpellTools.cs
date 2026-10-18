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
        [Description("The decoded view of one spell. format is \"json\" or \"text\". sections filters (json only) to any of: misc, attributes, effects, context, triggers. difficulty is a Difficulty id (0 = base); unknown ids are rejected with the valid list.")]
        public async Task<string> GetSpell(
            int id,
            string format = "json",
            string[] sections = null,
            uint difficulty = 0)
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

            if (!SpellWork.DBC.DBC.ValidDifficultyIds.Contains((int)difficulty))
            {
                return ToolResponse.Error("unknown_difficulty",
                    $"Unknown difficulty {difficulty}. Valid difficulty ids are: {string.Join(", ", SpellWork.DBC.DBC.ValidDifficultyIds)}.",
                    new Dictionary<string, object>
                    {
                        ["validDifficultyIds"] = SpellWork.DBC.DBC.ValidDifficultyIds.ToList(),
                    });
            }

            var context = ResolveContext(difficulty);

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
                    $"Use a number or a member name of {enumType.Name}.";
            return false;
        }

        [McpServerTool(Name = "search_spells")]
        [Description("Find spells by any combination of name substring, family, aura type, effect type, target type, attribute bit and school mask. Enum arguments accept either a number or an enum member name. Returns id, name and a one-line summary per hit.")]
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

            long? familyValue = null, auraValue = null, effectValue = null, targetValue = null, attributeValue = null;

            if (!TryResolve(family, typeof(SpellFamilyNames), ref familyValue, out var error) ||
                !TryResolve(auraType, typeof(AuraType), ref auraValue, out error) ||
                !TryResolve(effectType, typeof(SpellEffects), ref effectValue, out error) ||
                !TryResolve(targetType, typeof(Targets), ref targetValue, out error) ||
                !TryResolve(attribute, typeof(SpellAtribute), ref attributeValue, out error))
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
                .Where(spell => Matches(spell, name, familyValue, auraValue, effectValue, targetValue, attributeValue, schoolMask))
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
            long? effect, long? target, long? attribute, uint? schoolMask)
        {
            if (name != null && (spell.Name == null ||
                spell.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0))
                return false;

            if (family.HasValue && spell.SpellFamilyName != family.Value)
                return false;

            if (schoolMask.HasValue && (spell.SchoolMask & schoolMask.Value) == 0)
                return false;

            // An attribute filter names a single bit, and the bit's position
            // determines which of the fourteen Attributes words holds it. The
            // enum values in SpellAtribute are already the shifted masks, so
            // this only ever tests word 0; the remaining words are not filterable here.
            if (attribute.HasValue && (spell.Attributes & (uint)attribute.Value) == 0)
                return false;

            if (aura.HasValue && !spell.Effects.Any(e => e != null && e.EffectAura == aura.Value))
                return false;

            if (effect.HasValue && !spell.Effects.Any(e => e != null && e.Effect == effect.Value))
                return false;

            if (target.HasValue && !spell.Effects.Any(e => e != null &&
                (e.ImplicitTarget[0] == target.Value || e.ImplicitTarget[1] == target.Value)))
                return false;

            return true;
        }

        // One line, cheap to compute: the first effect's type and aura, which
        // is what distinguishes near-identically-named spells in a hit list.
        private static string Summarise(SpellInfo spell)
        {
            var effect = spell.Effects.FirstOrDefault(e => e != null);
            if (effect == null)
                return "no effects";

            var description = $"{(SpellEffects)effect.Effect}";
            if (effect.EffectAura != 0)
                description += $" / {(AuraType)effect.EffectAura}";

            return description;
        }

        internal static RenderContext ResolveContext(uint difficulty) =>
            new RenderContext(SpellWork.DBC.DBC.MaxLevel, SpellWork.DBC.DBC.MaxItemLevel, (int)difficulty);

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
