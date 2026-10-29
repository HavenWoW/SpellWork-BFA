using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class DecoderTools
    {
        private readonly SpellDataService _data;

        public DecoderTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "calculate_spell_scaling")]
        [Description("Per-effect computed values at a specific level, using the CoA 3.3.5 CalcValue rule (base points plus RealPointsPerLevel for each level above the spell level, level clamped to BaseLevel..MaxLevel, plus the DieSides roll range). Use this to see how a spell's numbers change with level rather than reading get_spell repeatedly. level clamps to 1-100. id, resolvedSpellId and name describe the spell computed; requestedId is the id asked for. difficulty is optional: when omitted the id is used exactly as given (id equals requestedId); when given it is a SpellDifficulty mode 0-3 and the id is resolved through SpellDifficulty.dbc first, using the variant spell's own row (mode 0 maps a 25N variant id to its 10N id). itemLevel is echoed and never applied (itemLevelApplied is false). mapId is accepted for compatibility and ignored.")]
        public async Task<string> CalculateSpellScaling(
            int id,
            uint level,
            uint itemLevel,
            uint? mapId = null,
            uint? difficulty = null)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(id))
                return SpellTools.NotFound(id);

            var difficultyError = difficulty.HasValue ? SpellTools.ValidateDifficulty(difficulty.Value) : null;
            if (difficultyError != null)
                return difficultyError;

            // The difficulty picks a different Spell row, not a different
            // effect row: resolve the id first, then read that spell.
            var resolvedId = difficulty.HasValue
                ? SpellWork.DBC.DBC.DifficultyResolver.Resolve(id, (int)difficulty.Value).ResolvedSpellId
                : id;
            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(resolvedId, out var spell))
                return SpellTools.NotFound(resolvedId);

            // Clamp to the client's level range.
            var clampedLevel = Math.Min(Math.Max(level, 1u), (uint)SpellWork.DBC.DBC.MaxLevel);
            var context = new SpellWork.Spell.RenderContext(clampedLevel, itemLevel, (int?)difficulty);

            var effects = new List<object>();
            foreach (var resolved in spell.Effects)
            {
                var effect = resolved?.Entry;
                if (effect == null)
                    continue;

                var values = spell.GetEffectValues(resolved, context);
                effects.Add(new Dictionary<string, object>
                {
                    ["index"] = effect.EffectIndex,
                    ["effect"] = new Dictionary<string, object>
                    {
                        ["raw"] = effect.Effect,
                        ["name"] = Enum.GetName(typeof(SpellWork.Spell.SpellEffects), effect.Effect),
                    },
                    ["base"] = values.BaseValue,
                    ["min"] = values.MinValue,
                    ["max"] = values.MaxValue,
                    ["perComboPoint"] = values.ValuePerResource,
                    ["spellPowerCoefficient"] = values.SpellPowerCoefficient,
                    ["scaled"] = values.Scaled,
                });
            }

            var payload = new Dictionary<string, object>
            {
                // One rule across the spell tools: id (and name) describe the
                // resolved spell, requestedId is the id asked for.
                ["id"] = spell.ID,
                ["requestedId"] = id,
                ["resolvedSpellId"] = spell.ID,
                ["name"] = spell.Name,
                ["level"] = clampedLevel,
                ["itemLevel"] = itemLevel,
                ["itemLevelApplied"] = false,
                ["effects"] = effects,
            };

            // The SpellDifficulty mode the spell id was resolved at.
            payload["difficulty"] = difficulty.HasValue ? (int)difficulty.Value : (int?)null;

            return ToolResponse.Ok(payload);
        }

        [McpServerTool(Name = "decode_flags")]
        [Description("Split a bitmask into its named flags. kind is an enum name such as SpellSchoolMask, SpellAtribute or ProcFlags — call list_enum with no name to see every available kind. Set bits with no declared member are reported in unmatchedBits.")]
        public Task<string> DecodeFlags(string kind, ulong value)
        {
            if (!EnumCatalog.TryResolve(kind, out var enumType))
                return Task.FromResult(UnknownEnum(kind));

            var flags = new List<object>();
            var unmatched = new List<object>();

            for (var bit = 0; bit < 64; bit++)
            {
                var mask = 1UL << bit;
                if ((value & mask) == 0)
                    continue;

                // Per bit, never Enum.GetName on the combined value: on a
                // [Flags] enum that returns null for anything that is not
                // exactly one declared member, and the usual ToString()
                // fallback then prints a bare decimal that reads like a name.
                var name = FindMemberName(enumType, mask);
                if (name == null)
                {
                    unmatched.Add(bit);
                    continue;
                }

                flags.Add(new Dictionary<string, object>
                {
                    ["bit"] = bit,
                    ["value"] = mask,
                    ["name"] = name,
                });
            }

            return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
            {
                ["kind"] = enumType.Name,
                ["value"] = value,
                ["flags"] = flags,
                ["unmatchedBits"] = unmatched,
            }));
        }

        [McpServerTool(Name = "list_enum")]
        [Description("The id-to-name map for one enum, so names can be resolved without guessing. Call with no name to list every available enum; that catalogue is uncapped and comes back as enums/count only -- limit, offset and the paging fields do not apply to it. With a name, filter is a case-insensitive substring match on member names and members are returned in the paged rows/count/totalMatches/offset/limit envelope the other list-shaped tools use: count is how many rows this response carries, totalMatches how many the filter matched in total, and offset pages through the rest.")]
        public Task<string> ListEnum(
            string name = null,
            string filter = null,
            int limit = ToolResponse.MaxRows,
            int offset = 0)
        {
            // The catalogue listing, deliberately left outside the page
            // envelope below: it is the discovery entry point, it is
            // uncapped, and limit/offset do not apply to it. Its "count" is
            // still "rows in this response" -- there is exactly one page and
            // it holds every enum -- so the convention holds here too.
            if (string.IsNullOrWhiteSpace(name))
            {
                return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
                {
                    ["enums"] = EnumCatalog.Names,
                    ["count"] = EnumCatalog.Names.Count,
                }));
            }

            if (!EnumCatalog.TryResolve(name, out var enumType))
                return Task.FromResult(UnknownEnum(name));

            // ShapeshiftFormMask is ulong-backed and its FORM_ALL member is
            // 0xFFFFFFFFFFFFFFFF -- above long.MaxValue. Convert.ToInt64
            // throws OverflowException converting that, so a ulong-backed
            // enum's raw value is read through Convert.ToUInt64 instead and
            // reported as an unsigned JSON number rather than wrapped into a
            // negative long -- System.Text.Json serialises ulong natively,
            // so there is no precision loss and no sign-wrap ambiguity for a
            // caller reading the value back.
            var isUlongBacked = Enum.GetUnderlyingType(enumType) == typeof(ulong);

            var members = Enum.GetNames(enumType)
                .Where(member => filter == null ||
                    member.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(member => new
                {
                    Name = member,
                    Value = isUlongBacked
                        ? (object)Convert.ToUInt64(Enum.Parse(enumType, member))
                        : (object)Convert.ToInt64(Enum.Parse(enumType, member)),
                })
                .OrderBy(member => member.Value)
                .ToList();

            // Routed through ToolResponse.Paged rather than Take(MaxRows) plus
            // a hand-rolled "count"/"truncated" pair. The old shape set
            // "count" to the number of members that *matched* while every
            // other tool on this server uses "count" for the number of rows
            // *in this response* -- so an agent that learned count ==
            // rows.Length from search_spells read "count: 495" from AuraType
            // and believed it had received 495 members. It also had no
            // offset, which meant a truncated enum's withheld members were
            // simply unreachable: AuraType is 495 members today, five under
            // the cap, and these enums grow every patch. Paged supplies
            // offset/limit/count/totalMatches/truncated with one set of
            // meanings shared across every list-shaped tool, so the
            // divergence is removed rather than documented.
            //
            // DatabaseTools.EffectiveLimit rather than a local
            // Math.Min(limit, MaxRows): that helper is already the one
            // expression in this server that agrees with Paged's clamp in
            // every direction (non-positive yields zero rows, never one and
            // never unbounded), and it is pinned to Paged by
            // EffectiveLimit_AgreesWithPaged. Re-deriving the same arithmetic
            // here would be a second definition free to drift from both.
            // limit defaults to MaxRows so a caller that passes neither
            // parameter gets exactly what this tool returned before.
            var effectiveOffset = Math.Max(0, offset);
            var page = members
                .Skip(effectiveOffset)
                .Take(DatabaseTools.EffectiveLimit(limit))
                .Select(member => (object)new Dictionary<string, object>
                {
                    ["name"] = member.Name,
                    ["value"] = member.Value,
                })
                .ToList();

            var payload = ToolResponse.Paged(page, members.Count, effectiveOffset, limit);

            // Enum-specific facts, outside the page envelope: they describe
            // the enum, not the page of members this call happens to return.
            payload["name"] = enumType.Name;
            payload["isFlags"] = enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;

            return Task.FromResult(ToolResponse.Ok(payload));
        }

        private static string FindMemberName(Type enumType, ulong mask)
        {
            // ShapeshiftFormMask is ulong-backed; its FORM_ALL member
            // (0xFFFFFFFFFFFFFFFF) is above long.MaxValue and
            // Convert.ToInt64 throws OverflowException converting it, even
            // though this loop is only looking for a single-bit match. Read
            // a ulong-backed enum's members through Convert.ToUInt64
            // instead, which never overflows for a value that is already
            // 64-bit unsigned.
            var isUlongBacked = Enum.GetUnderlyingType(enumType) == typeof(ulong);

            foreach (var member in Enum.GetNames(enumType))
            {
                var boxed = Enum.Parse(enumType, member);

                // Convert.ToInt64 then reinterpret: an enum with a negative
                // underlying value (a signed backing type with the sign bit
                // set) must still match the unsigned mask for that bit.
                var memberValue = isUlongBacked
                    ? Convert.ToUInt64(boxed)
                    : unchecked((ulong)Convert.ToInt64(boxed));

                if (memberValue == mask)
                    return member;
            }

            return null;
        }

        private static string UnknownEnum(string requested) =>
            ToolResponse.Error("unknown_enum", $"No enum named '{requested}'.",
                new Dictionary<string, object>
                {
                    ["availableKinds"] = EnumCatalog.Names,
                });
    }
}
