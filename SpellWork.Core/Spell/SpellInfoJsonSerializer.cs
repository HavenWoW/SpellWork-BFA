using SpellWork.DBC.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SpellWork.Spell
{
    /// <summary>
    /// Serialises <see cref="SpellInfo"/> to JSON by reading its public
    /// properties directly, rather than going through <see cref="ISpellInfoWriter"/>.
    /// <see cref="SpellInfo.Write"/> emits formatted text lines; the properties
    /// already are the structured model the text is rendered from, so this type
    /// re-reads that same model instead of re-parsing formatted output.
    /// </summary>
    public static class SpellInfoJsonSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions =
            new JsonSerializerOptions { WriteIndented = false };

        // Group index -> the enum that decodes that group's bits. There is no
        // single "spell attributes" enum: SpellEnums.cs declares fourteen of
        // them (SpellAtribute, SpellAtributeEx .. SpellAtributeEx13), so the
        // group-to-enum mapping is a dispatch table, not a single cast.
        // SpellAtributeEx11/12/13 are declared `internal`; that is fine here
        // because this table never leaves this internal-to-Core dispatch and
        // is never exposed through a public signature.
        private static readonly Type[] AttributeEnumTypes =
        {
            typeof(SpellAtribute),
            typeof(SpellAtributeEx),
            typeof(SpellAtributeEx2),
            typeof(SpellAtributeEx3),
            typeof(SpellAtributeEx4),
            typeof(SpellAtributeEx5),
            typeof(SpellAtributeEx6),
            typeof(SpellAtributeEx7),
            typeof(SpellAtributeEx8),
            typeof(SpellAtributeEx9),
            typeof(SpellAtributeEx10),
            typeof(SpellAtributeEx11),
            typeof(SpellAtributeEx12),
            typeof(SpellAtributeEx13),
        };

        // Bring-up (5.5.4): only the sections backed by loaded stores.
        private static readonly string[] ValidSections =
        {
            "misc", "attributes", "effects", "context", "triggers",
        };

        public static string Serialize(SpellInfo spell, RenderContext context, IReadOnlyCollection<string> sections = null)
        {
            if (spell == null)
                throw new ArgumentNullException(nameof(spell));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            HashSet<string> include = null;
            if (sections != null)
            {
                include = new HashSet<string>(sections, StringComparer.OrdinalIgnoreCase);
                foreach (var section in include)
                {
                    if (!ValidSections.Contains(section, StringComparer.OrdinalIgnoreCase))
                        throw new ArgumentException(
                            $"Unknown section '{section}'. Valid sections are: {string.Join(", ", ValidSections)}.",
                            nameof(sections));
                }
            }

            bool Wants(string section) => include == null || include.Contains(section);

            var root = new Dictionary<string, object>
            {
                ["id"] = spell.ID,
                ["name"] = spell.Name,
            };

            if (include != null)
                root["sections"] = ValidSections.Where(Wants).ToList();

            if (Wants("context"))
            {
                root["level"] = context.Level;
                root["itemLevel"] = context.ItemLevel;
                root["difficultyId"] = context.DifficultyId;
            }

            AddIfNotEmpty(root, "description", spell.Description);
            AddIfNotEmpty(root, "tooltip", spell.Tooltip);

            if (Wants("misc"))
            {
                AddIfNotEmpty(root, "misc", BuildMisc(spell));
                root["spellFamilyName"] = BuildRawAndName(spell.SpellFamilyName, typeof(SpellFamilyNames));
                root["spellClassMask"] = spell.SpellClassMask;
            }

            if (Wants("attributes"))
            {
                var attributes = BuildAttributes(spell, out var attributesUnmatchedBits);
                AddIfNotEmpty(root, "attributes", attributes);
                AddIfNotEmpty(root, "attributesUnmatchedBits", attributesUnmatchedBits);
            }

            if (Wants("effects"))
                AddIfNotEmpty(root, "effects", BuildEffects(spell, context));

            if (Wants("triggers"))
                root["triggers"] = BuildTriggers(spell);

            return JsonSerializer.Serialize(root, SerializerOptions);
        }

        private static Dictionary<string, object> BuildMisc(SpellInfo spell)
        {
            var misc = new Dictionary<string, object>();
            AddIfNonZero(misc, "speed", spell.Speed);

            if (spell.SchoolMask != 0)
                misc["schoolMask"] = BuildRawAndFlags(spell.SchoolMask, typeof(SpellSchoolMask));

            AddIfNonZero(misc, "castingTimeIndex", spell.CastingTimeIndex);
            AddIfNotEmpty(misc, "rangeIndex", BuildRange(spell));
            AddIfNotEmpty(misc, "duration", BuildDuration(spell));

            AddIfNonZero(misc, "spellIconFileDataID", spell.SpellIconFileDataID);
            AddIfNonZero(misc, "activeIconFileDataID", spell.ActiveIconFileDataID);

            return misc;
        }

        private static Dictionary<string, object> BuildDuration(SpellInfo spell)
        {
            if (spell.DurationEntry == null)
                return null;

            return new Dictionary<string, object>
            {
                ["duration"] = spell.Duration,
                ["durationPerLevel"] = spell.DurationPerLevel,
                ["maxDuration"] = spell.MaxDuration,
            };
        }

        private static Dictionary<string, object> BuildRange(SpellInfo spell)
        {
            if (spell.Range == null)
                return null;

            return new Dictionary<string, object>
            {
                ["id"] = spell.RangeIndex,
                ["displayName"] = spell.Range.DisplayName,
                ["minRangeNegative"] = spell.Range.RangeMin[0],
                ["minRangePositive"] = spell.Range.RangeMin[1],
                ["maxRangeNegative"] = spell.Range.RangeMax[0],
                ["maxRangePositive"] = spell.Range.RangeMax[1],
            };
        }

        // One entry per set bit across all 17 attribute words. Words 14..16 have
        // no named enum, so their set bits are reported through unmatchedBits.
        private static List<object> BuildAttributes(SpellInfo spell, out List<object> unmatchedBits)
        {
            var groups = spell.AttributeWords;

            var result = new List<object>();
            unmatchedBits = new List<object>();
            for (var group = 0; group < groups.Length; group++)
            {
                var raw = groups[group];
                if (raw == 0)
                    continue;

                for (var bit = 0; bit < 32; bit++)
                {
                    var mask = 1u << bit;
                    if ((raw & mask) == 0)
                        continue;

                    var name = group < AttributeEnumTypes.Length
                        ? FindDeclaredFlagName(AttributeEnumTypes[group], mask)
                        : null;
                    if (name == null)
                    {
                        unmatchedBits.Add(new Dictionary<string, object> { ["group"] = group, ["bit"] = bit });
                        continue;
                    }

                    result.Add(new Dictionary<string, object>
                    {
                        ["group"] = group,
                        ["raw"] = mask,
                        ["name"] = name,
                    });
                }
            }

            return result;
        }

        // Effects holds one entry per (effect index, difficulty) pair.
        private static List<object> BuildEffects(SpellInfo spell, RenderContext context)
        {
            var result = new List<object>();
            foreach (var effect in spell.Effects)
            {
                if (effect == null)
                    continue;

                var entry = new Dictionary<string, object>
                {
                    ["index"] = effect.EffectIndex,
                    ["difficultyId"] = effect.DifficultyID,
                    ["effect"] = new Dictionary<string, object>
                    {
                        ["raw"] = effect.Effect,
                        ["name"] = GetEnumName(typeof(SpellEffects), effect.Effect),
                    },
                };

                if (effect.EffectAura != 0)
                {
                    entry["auraType"] = new Dictionary<string, object>
                    {
                        ["raw"] = effect.EffectAura,
                        ["name"] = GetEnumName(typeof(AuraType), unchecked((uint)effect.EffectAura)),
                    };
                }

                var values = spell.CalculateEffectValues(effect, context);
                var baseValue = values.BaseValue;
                entry["basePoints"] = baseValue;
                if (Math.Abs(effect.Variance) > 1.0E-5f)
                {
                    var delta = Math.Abs(baseValue * effect.Variance * 0.5f);
                    entry["basePointsMin"] = baseValue - delta;
                    entry["basePointsMax"] = baseValue + delta;
                }

                AddIfNonZero(entry, "valuePerResource", values.ValuePerResource);
                AddIfNonZero(entry, "perLevelValue", values.PerLevelValue);
                AddIfNonZero(entry, "spellPowerCoefficient", values.SpellPowerCoefficient);
                AddIfNonZero(entry, "attackPowerCoefficient", values.AttackPowerCoefficient);

                entry["targetA"] = new Dictionary<string, object>
                {
                    ["raw"] = effect.ImplicitTarget[0],
                    ["name"] = GetEnumName(typeof(Targets), unchecked((uint)effect.ImplicitTarget[0])),
                };
                entry["targetB"] = new Dictionary<string, object>
                {
                    ["raw"] = effect.ImplicitTarget[1],
                    ["name"] = GetEnumName(typeof(Targets), unchecked((uint)effect.ImplicitTarget[1])),
                };

                AddIfNonZero(entry, "radiusIndex", effect.EffectRadiusIndex[0]);
                AddIfNonZero(entry, "maxRadiusIndex", effect.EffectRadiusIndex[1]);
                AddIfNonZero(entry, "chainTargets", effect.EffectChainTargets);

                if (effect.EffectTriggerSpell != 0)
                {
                    entry["triggerSpell"] = new Dictionary<string, object>
                    {
                        ["id"] = effect.EffectTriggerSpell,
                        ["name"] = DBC.DBC.SpellInfoStore.TryGetValue(effect.EffectTriggerSpell, out var triggered)
                            ? triggered.Name
                            : null,
                    };
                }

                AddIfNonZero(entry, "miscValue", effect.EffectMiscValue[0]);
                AddIfNonZero(entry, "miscValueB", effect.EffectMiscValue[1]);
                AddIfNonZero(entry, "amplitude", effect.EffectAmplitude);

                if (effect.EffectMechanic != 0)
                    entry["mechanic"] = BuildRawAndName((uint)effect.EffectMechanic, typeof(Mechanics));

                var effectClassMask = Array.ConvertAll(effect.EffectSpellClassMask, i => (uint)i);
                if (effectClassMask[0] != 0 || effectClassMask[1] != 0 || effectClassMask[2] != 0 || effectClassMask[3] != 0)
                    entry["spellClassMask"] = effectClassMask;

                result.Add(entry);
            }

            return result;
        }

        private static Dictionary<string, object> BuildTriggers(SpellInfo spell)
        {
            var triggers = spell.Effects
                .Where(effect => effect != null && effect.EffectTriggerSpell != 0)
                .Select(effect => effect.EffectTriggerSpell)
                .Distinct()
                .OrderBy(id => id)
                .Cast<object>()
                .ToList();

            var triggeredBy = new List<object>();
            if (DBC.DBC.SpellTriggerStore.TryGetValue(spell.ID, out var sources))
                triggeredBy.AddRange(sources.OrderBy(id => id).Cast<object>());

            return new Dictionary<string, object>
            {
                ["triggers"] = triggers,
                ["triggeredBy"] = triggeredBy,
            };
        }

        private static void AddFlagsWithUnmatched(IDictionary<string, object> dict, string key, Type enumType, uint raw)
        {
            var flags = BuildFlagList(enumType, raw, out var unmatchedBits);
            AddIfNotEmpty(dict, key, flags);
            AddIfNotEmpty(dict, key + "UnmatchedBits", unmatchedBits);
        }

        // The shared per-bit decomposition every flag-list call site in this
        // file goes through (directly here, or via BuildFlagsList below).
        // unmatchedBits collects the bit *position* (0-31) of every set bit
        // with no declared enum member -- deliberately not paired with a
        // "name" anywhere, matching decode_flags's own unmatchedBits shape
        // (SpellWork.Mcp/Tools/DecoderTools.cs) so the two tools describing
        // the same mask cannot disagree about what "no name" means.
        // Internal rather than private so SpellWork.Core.Tests
        // (InternalsVisibleTo) can pin this directly without needing a
        // loaded spell to reach it.
        internal static List<object> BuildFlagList(Type enumType, uint raw, out List<object> unmatchedBits)
        {
            var result = new List<object>();
            unmatchedBits = new List<object>();
            if (raw == 0)
                return result;

            for (var bit = 0; bit < 32; bit++)
            {
                var mask = 1u << bit;
                if ((raw & mask) == 0)
                    continue;

                var name = FindDeclaredFlagName(enumType, mask);
                if (name == null)
                {
                    unmatchedBits.Add(bit);
                    continue;
                }

                result.Add(new Dictionary<string, object>
                {
                    ["raw"] = mask,
                    ["name"] = name,
                });
            }

            return result;
        }

        // Re-boxes a raw value as whichever integral type actually backs the
        // enum, regardless of its underlying integral type --
        // SpellAtribute..SpellAtributeEx13 are uint-backed, ProcFlags/
        // ProcFlagsEx/SpellEffects/AuraType/Targets/SpellSchoolMask/
        // SpellDmgClass/DispelType/SpellPreventionType/SpellCastTargetFlags/
        // CreatureTypeMask are the default int, and Powers is sbyte.
        // Enum.GetName requires the boxed value's runtime type to match the
        // enum's underlying type exactly (a mismatched boxed type makes it
        // return null even for a valid member), so both callers below need
        // this same re-boxing regardless of what they do with a non-match.
        private static object Rebox(Type enumType, uint value)
        {
            var underlying = Enum.GetUnderlyingType(enumType);
            if (underlying == typeof(uint))
                return value;
            if (underlying == typeof(short))
                return unchecked((short)value);
            if (underlying == typeof(ushort))
                return unchecked((ushort)value);
            if (underlying == typeof(sbyte))
                return unchecked((sbyte)value);
            if (underlying == typeof(byte))
                return unchecked((byte)value);
            if (underlying == typeof(long))
                return unchecked((long)value);
            if (underlying == typeof(ulong))
                return unchecked((ulong)value);
            return unchecked((int)value);
        }

        // Resolves a raw value against whichever enum decodes it. Falls back
        // to the plain decimal value -- formatted through that same
        // underlying type, so a negative member like Powers.POWER_HEALTH
        // (-2) reads "-2" rather than its unsigned bit pattern -- when the
        // value has no declared enum member. That fallback is correct here:
        // every caller of this overload (BuildRawAndName's three overloads)
        // resolves one genuinely scalar value (an effect type, an aura type,
        // a power type...), where "no declared member" means the id itself
        // is unrecognised, not that a flag bit was missed.
        private static string GetEnumName(Type enumType, uint value)
        {
            var boxed = Rebox(enumType, value);
            return Enum.GetName(enumType, boxed) ?? boxed.ToString();
        }

        // Task 8 review, finding "Task 2": per-bit flag decomposition (below)
        // must never fall through to GetEnumName's decimal-string fallback --
        // that is the exact bug the review flagged: a set bit with no
        // declared member came back from this JSON path as a bare decimal in
        // the name position ("targetCreatureType": {..., "name": "8385"}-style
        // for the *unmatched* bit specifically), while
        // SpellWork.Mcp/Tools/DecoderTools.cs's decode_flags reports the same
        // situation as an unmatchedBits entry instead. This resolves the
        // disagreement by making the JSON path behave like decode_flags:
        // returns null instead of a stringified fallback so the caller can
        // report the bit separately, never disguise it as a name.
        private static string FindDeclaredFlagName(Type enumType, uint bitValue) =>
            Enum.GetName(enumType, Rebox(enumType, bitValue));

        // The `{ "raw": …, "name": … }` shape used throughout this file for a
        // single decoded value (as opposed to BuildFlagList's one-entry-per-bit
        // shape, used where the text path itself decodes several set bits at
        // once through a combined ToString()).
        private static Dictionary<string, object> BuildRawAndName(int value, Type enumType)
        {
            return new Dictionary<string, object>
            {
                ["raw"] = value,
                ["name"] = GetEnumName(enumType, unchecked((uint)value)),
            };
        }

        private static Dictionary<string, object> BuildRawAndName(uint value, Type enumType)
        {
            return new Dictionary<string, object>
            {
                ["raw"] = value,
                ["name"] = GetEnumName(enumType, value),
            };
        }

        private static Dictionary<string, object> BuildRawAndName(sbyte value, Type enumType)
        {
            return new Dictionary<string, object>
            {
                ["raw"] = value,
                ["name"] = GetEnumName(enumType, unchecked((uint)(byte)value)),
            };
        }

        // The `{ "raw": …, "flags": […] }` shape for a [Flags] enum, now
        // `{ "raw": …, "flags": […], "unmatchedBits": […] }` when at least
        // one set bit has no declared member. Enum.GetName only matches a
        // single declared member exactly, so for a [Flags] enum it returns
        // null on every combined value and GetEnumName's fallback stringifies
        // the boxed integer -- schoolMask 125 became "125" where the GUI
        // (rendering through the enum's own ToString()) shows the decomposed
        // names. schoolMask, targets, targetCreatureType and preventionType
        // are all [Flags]; decompose them per-bit through BuildFlagList
        // instead of naming them as a single scalar. powerType/damageClass/
        // dispel are genuinely scalar (no [Flags]) and stay on
        // BuildRawAndName. "unmatchedBits" sits alongside "flags" (not nested
        // inside it) since each of these fields already has its own
        // dedicated wrapping object -- unlike procFlags/procFlagsEx above,
        // there is no risk of two flag lists colliding on one bare key here.
        private static Dictionary<string, object> BuildRawAndFlags(uint value, Type enumType)
        {
            var flags = BuildFlagsList(value, enumType, out var unmatchedBits);
            var result = new Dictionary<string, object>
            {
                ["raw"] = value,
                ["flags"] = flags,
            };
            if (unmatchedBits.Count > 0)
                result["unmatchedBits"] = unmatchedBits;
            return result;
        }

        private static Dictionary<string, object> BuildRawAndFlags(int value, Type enumType)
        {
            var flags = BuildFlagsList(unchecked((uint)value), enumType, out var unmatchedBits);
            var result = new Dictionary<string, object>
            {
                ["raw"] = value,
                ["flags"] = flags,
            };
            if (unmatchedBits.Count > 0)
                result["unmatchedBits"] = unmatchedBits;
            return result;
        }

        // BuildFlagList returns an empty list for raw == 0, which is correct
        // for its existing callers (procFlags/attributes): they only reach it
        // through AddIfNotEmpty, which already omits the key entirely at
        // zero, so "no bits" and "not serialised" never need to be told apart
        // inside the list. BuildRawAndFlags's callers are different --
        // preventionType is emitted unconditionally, even at zero, precisely
        // so SPELL_PREVENTION_TYPE_NONE stays distinguishable from "this
        // field never got serialised" -- so an empty flags array there would
        // erase that distinction again. When the enum declares a zero member,
        // synthesise it as the sole flags entry instead of leaving the array
        // empty; schoolMask/targets/targetCreatureType keep the caller-side
        // `!= 0` gate that already prevents them from reaching this path at
        // zero, so this only actually fires for preventionType today -- and
        // a synthesised zero entry is never an "unmatched bit" (there is no
        // bit position for value 0), so unmatchedBits stays empty on that
        // branch.
        private static List<object> BuildFlagsList(uint value, Type enumType, out List<object> unmatchedBits)
        {
            if (value != 0)
                return BuildFlagList(enumType, value, out unmatchedBits);

            unmatchedBits = new List<object>();
            var zeroName = GetEnumName(enumType, 0u);
            if (zeroName == "0")
                return new List<object>();

            return new List<object>
            {
                new Dictionary<string, object> { ["raw"] = 0u, ["name"] = zeroName },
            };
        }

        private static void AddIfNonZero(IDictionary<string, object> dict, string key, int value)
        {
            if (value != 0)
                dict[key] = value;
        }

        private static void AddIfNonZero(IDictionary<string, object> dict, string key, uint value)
        {
            if (value != 0)
                dict[key] = value;
        }

        private static void AddIfNonZero(IDictionary<string, object> dict, string key, short value)
        {
            if (value != 0)
                dict[key] = value;
        }

        private static void AddIfNonZero(IDictionary<string, object> dict, string key, byte value)
        {
            if (value != 0)
                dict[key] = value;
        }

        private static void AddIfNonZero(IDictionary<string, object> dict, string key, float value)
        {
            if (value != 0f)
                dict[key] = value;
        }

        // Stances/StancesNot are the only long-backed fields in this file --
        // they pack two 32-bit ShapeshiftMask/ShapeshiftExclude halves into
        // one 64-bit value (SpellInfo.cs:117-118). Mirrors every other
        // overload's exact-zero test; do not introduce an epsilon here.
        private static void AddIfNonZero(IDictionary<string, object> dict, string key, long value)
        {
            if (value != 0)
                dict[key] = value;
        }

        private static void AddIfNotEmpty(IDictionary<string, object> dict, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                dict[key] = value;
        }

        private static void AddIfNotEmpty(IDictionary<string, object> dict, string key, List<object> value)
        {
            if (value != null && value.Count > 0)
                dict[key] = value;
        }

        private static void AddIfNotEmpty(IDictionary<string, object> dict, string key, Dictionary<string, object> value)
        {
            if (value != null && value.Count > 0)
                dict[key] = value;
        }
    }
}
