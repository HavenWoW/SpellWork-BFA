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

        // The nine section names the interface line documents, plus
        // "context" (final review fixes item 2): the render context
        // (level/itemLevel/difficulty) that basePoints is computed
        // against but that used to leave no trace in the document. Kept as
        // the single source of truth for both validating `sections` and
        // echoing back which of them were applied.
        private static readonly string[] ValidSections =
        {
            "misc", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs", "scaling",
            "context", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems",
            "items", "triggers", "totems", "visual",
        };

        public static string Serialize(SpellInfo spell, RenderContext context, IReadOnlyCollection<string> sections = null)
        {
            if (spell == null)
                throw new ArgumentNullException(nameof(spell));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Ordinal*Ignore*Case: a caller (frequently a model) capitalising
            // "Effects" must reach the same filter as "effects", not silently
            // fall through to a near-empty document. Anything that still
            // does not match one of the section names in ValidSections is a
            // mistake the caller needs to see, not a document that quietly
            // omits data -- see the ArgumentException below.
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

            // Echoed only when the caller actually filtered. Its presence
            // lets a consumer tell "effects is missing because it was
            // filtered out" (absent from this list) apart from "effects is
            // missing because this spell genuinely has none" (present in
            // this list, but the effects key itself still absent below).
            if (include != null)
                root["sections"] = ValidSections.Where(Wants).ToList();

            // basePoints (in "effects") is computed against the supplied
            // RenderContext -- spell 606 serialises basePoints 5714 at level
            // 120 and 57 at level 20 -- and the document said nothing about
            // which level produced it. Recorded at the root, not inside
            // "misc", because it describes how the *document* was rendered,
            // not a property of the spell itself. Emitted directly (not
            // through AddIfNonZero/AddIfNotEmpty): a level of 0 is a real
            // render context and must not be indistinguishable from "omitted".
            if (Wants("context"))
            {
                root["level"] = context.Level;
                root["itemLevel"] = context.ItemLevel;

                // A Difficulty.dbc id; 0 is the base difficulty. Emitted
                // directly: 0 is a real render context.
                root["difficulty"] = context.Difficulty;
            }

            AddIfNotEmpty(root, "description", spell.Description);
            AddIfNotEmpty(root, "nameSubtext", spell.Spell?.NameSubtext);
            AddIfNotEmpty(root, "auraDescription", spell.Spell?.AuraDescription);
            if (spell.EffectDifficulties.Count > 0)
                root["effectDifficulties"] = spell.EffectDifficulties.ToList();

            if (Wants("misc"))
                AddIfNotEmpty(root, "misc", BuildMisc(spell, context.Difficulty));

            // Emitted at the root rather than inside "misc": search_spells
            // filters on family, and burying the filter key one level deep
            // makes every caller destructure to reach it. Both are emitted
            // unconditionally -- SPELLFAMILY_GENERIC is 0 and is a real
            // family, not an absent one.
            if (Wants("misc"))
            {
                root["spellFamilyName"] = BuildRawAndName(spell.SpellFamilyName, typeof(SpellFamilyNames));
                root["spellClassMask"] = spell.SpellClassMask;
            }

            if (Wants("misc"))
            {
                var rootSchoolMask = spell.MiscFor(context.Difficulty)?.SchoolMask ?? 0;
                if (rootSchoolMask != 0)
                    root["schoolMask"] = BuildRawAndFlags(rootSchoolMask, typeof(SpellSchoolMask));

                var rune = spell.RuneCost;
                if (rune != null)
                {
                    root["runeCost"] = new Dictionary<string, object>
                    {
                        ["blood"] = rune.Blood,
                        ["unholy"] = rune.Unholy,
                        ["frost"] = rune.Frost,
                        ["chromatic"] = rune.Chromatic,
                        ["runicPower"] = rune.RunicPower,
                    };
                }
            }

            if (Wants("attributes"))
            {
                root["attributes"] = BuildAttributes(spell, context.Difficulty);
            }

            if (Wants("effects"))
                AddIfNotEmpty(root, "effects", BuildEffects(spell, context));

            if (Wants("powers") && spell.Powers.Count > 0)
                root["powers"] = BuildPowers(spell);

            if (Wants("targets") && spell.TargetRestrictionsFor(context.Difficulty) != null)
                root["targets"] = BuildTargets(spell, context);

            if (Wants("reagents"))
                AddIfNotEmpty(root, "reagents", BuildReagents(spell));

            if (Wants("aura"))
                AddIfNotEmpty(root, "aura", BuildAura(spell, context.Difficulty));

            if (Wants("procs"))
                AddIfNotEmpty(root, "procs", BuildProcs(spell, context.Difficulty));

            if (Wants("scaling") && spell.Scaling != null)
                AddIfNotEmpty(root, "scaling", BuildScaling(spell.Scaling));

            if (Wants("cooldowns"))
                AddIfNotEmpty(root, "cooldowns", BuildCooldowns(spell, context.Difficulty));

            if (Wants("levels"))
                AddIfNotEmpty(root, "levels", BuildLevels(spell, context.Difficulty));

            if (Wants("interrupts"))
                AddIfNotEmpty(root, "interrupts", BuildInterrupts(spell, context.Difficulty));

            if (Wants("shapeshift"))
                AddIfNotEmpty(root, "shapeshift", BuildShapeshift(spell));

            if (Wants("totems") && spell.Totems != null)
            {
                var totems = new Dictionary<string, object>();
                if (spell.Totems.TotemCategory.Any(c => c != 0))
                    totems["totemCategory"] = spell.Totems.TotemCategory;
                if (spell.Totems.Totem.Any(t => t != 0))
                    totems["totem"] = spell.Totems.Totem;
                AddIfNotEmpty(root, "totems", totems);
            }

            if (Wants("equippedItems"))
                AddIfNotEmpty(root, "equippedItems", BuildEquippedItems(spell));

            if (Wants("triggers"))
                root["triggers"] = BuildTriggers(spell, context);

            if (Wants("items"))
                AddIfNotEmpty(root, "items", BuildItems(spell));

            // Emitted unconditionally: {"storeUnloaded": true, ...} when a
            // gated store is missing, otherwise the chain (spec §6). The same
            // single ResolveSection call the text block and
            // get_spell_visual_chain branch on.
            if (Wants("visual"))
                root["visual"] = SpellVisualChainJson.Describe(SpellVisualChainResolver.ResolveSection(spell.ID));

            return JsonSerializer.Serialize(root, SerializerOptions);
        }

        // SpellMisc and SpellCategories are both keyed by (spell, difficulty):
        // read the rows at the requested difficulty, falling back to the base
        // row, never the difficulty-0 convenience properties.
        private static Dictionary<string, object> BuildMisc(SpellInfo spell, int difficulty)
        {
            var row = spell.MiscFor(difficulty);
            var categories = spell.CategoriesFor(difficulty);
            var misc = new Dictionary<string, object>();
            AddIfNonZero(misc, "speed", row?.Speed ?? 0f);


            // CastingTimeIndex/RangeIndex are indices, not values -- the same
            // trap note (e) already flagged for the radius fields. Resolve
            // them the way BuildRadius resolves EffectRadiusIndex, and keep
            // its shape: an object with the id plus the resolved values, or
            // the key omitted when there is no such row.
            AddIfNotEmpty(misc, "castingTimeIndex", BuildCastTime(row?.CastingTimeIndex ?? 0));
            AddIfNotEmpty(misc, "rangeIndex", BuildRange(spell, difficulty));

            AddIfNonZero(misc, "spellIconID", row?.SpellIconID ?? 0);
            AddIfNonZero(misc, "activeIconID", row?.ActiveIconID ?? 0);
            AddIfNonZero(misc, "category", categories?.Category ?? 0);
            AddIfNonZero(misc, "startRecoveryCategory", categories?.StartRecoveryCategory ?? 0);

            // MECHANIC_NONE is 0 and meaningful -- same reasoning as
            // damageClass/dispel above, so this is emitted unconditionally
            // rather than through AddIfNonZero.
            misc["mechanic"] = BuildRawAndName(categories?.Mechanic ?? 0, typeof(Mechanics));

            // damageClass, dispel and preventionType are emitted even when
            // zero: SPELL_DAMAGE_CLASS_NONE, DISPEL_NONE and
            // SPELL_PREVENTION_TYPE_NONE are real values the text render
            // prints unconditionally, and omitting them at zero would make
            // "none" indistinguishable from "this field never got
            // serialised". Every other field here keeps the omit-zero rule.
            misc["damageClass"] = BuildRawAndName(categories?.DefenseType ?? 0, typeof(SpellDmgClass));
            misc["dispel"] = BuildRawAndName(categories?.DispelType ?? 0, typeof(DispelType));
            misc["preventionType"] = BuildRawAndFlags(categories?.PreventionType ?? 0, typeof(SpellPreventionType));

            return misc;
        }

        // CastingTimeIndex is an index into DBC.DBC.SpellCastTimes, not a
        // cast time in milliseconds. Mirrors BuildRadius: resolve against the
        // store and return null (causing the caller to omit the key) when
        // there is no such row. Deliberately does not reproduce
        // SpellInfo.cs:443-468's level/scaling-adjusted effective cast time --
        // that calculation depends on the selected level and attribute
        // state, which is presentation logic, not data this store row holds.
        private static Dictionary<string, object> BuildCastTime(int castingTimeIndex)
        {
            if (DBC.DBC.SpellCastTimes == null || !DBC.DBC.SpellCastTimes.ContainsKey(castingTimeIndex))
                return null;

            var castTime = DBC.DBC.SpellCastTimes[castingTimeIndex];
            return new Dictionary<string, object>
            {
                ["id"] = castingTimeIndex,
                ["base"] = castTime.CastTime,
                ["perLevel"] = castTime.CastTimePerLevel,
                ["minimum"] = castTime.MinCastTime,
            };
        }

        // RangeIndex is likewise an index, read off the SpellMisc row at the
        // requested difficulty and resolved through SpellInfo.RangeFor.
        private static Dictionary<string, object> BuildRange(SpellInfo spell, int difficulty)
        {
            var range = spell.RangeFor(difficulty);
            if (range == null)
                return null;

            return new Dictionary<string, object>
            {
                ["id"] = spell.MiscFor(difficulty).RangeIndex,
                ["displayName"] = range.Name,
                ["minRangeNegative"] = range.MinRangeNegative,
                ["minRangePositive"] = range.MinRangePositive,
                ["maxRangeNegative"] = range.MaxRangeNegative,
                ["maxRangePositive"] = range.MaxRangePositive,
            };
        }

        // One entry per set bit, deliberately -- the text path renders a whole
        // group through a single cast and shows a combined name when several
        // bits are set. That is the right shape for a human reading a report;
        // per-bit is the right shape for a JSON consumer inspecting one flag
        // at a time. The difference is intentional, not a bug to reconcile.
        //
        // A set bit with no declared member in its group's enum is reported
        // through unmatchedBits instead of being appended to the matched
        // array with a stringified-decimal "name" -- same rule BuildFlagList
        // follows, via the same FindDeclaredFlagName lookup, so the two
        // per-bit decomposition loops in this file cannot drift apart on what
        // "unmatched" means. Each entry carries "group" as well as "bit"
        // (unlike BuildFlagList's flat unmatchedBits list of bit positions
        // alone) because attributes spans fourteen different enums sharing
        // one array; a bare bit position would be ambiguous about which
        // group's enum it failed to match against.
        private static List<object> BuildAttributes(SpellInfo spell, int difficulty)
        {
            // The SpellMisc row at the requested difficulty (base fallback).
            // Always 14 words, so a consumer can index by word number.
            var groups = spell.AttributesFor(difficulty);

            var result = new List<object>();
            for (var group = 0; group < groups.Length; group++)
            {
                var flags = BuildFlagList(AttributeEnumTypes[group], groups[group], out var unmatched);
                var word = new Dictionary<string, object>
                {
                    ["word"] = group,
                    ["value"] = groups[group],
                    ["flags"] = flags,
                };
                if (unmatched.Count > 0)
                    word["unmatchedBits"] = unmatched;
                result.Add(word);
            }

            return result;
        }

        // One entry per effect index, resolved at the context's difficulty:
        // the row at that difficulty if the spell has one for the index, else
        // the base (difficulty 0) row. resolvedDifficulty says which one it was.
        private static List<object> BuildEffects(SpellInfo spell, RenderContext context)
        {
            var result = new List<object>();
            foreach (var resolved in spell.EffectsFor(context.Difficulty))
            {
                var effect = resolved?.Entry;
                if (effect == null)
                    continue;

                var entry = new Dictionary<string, object>
                {
                    ["index"] = effect.EffectIndex,
                    ["resolvedDifficulty"] = resolved.ResolvedDifficulty,
                    ["effect"] = new Dictionary<string, object>
                    {
                        ["raw"] = effect.Effect,
                        ["name"] = GetEnumName(typeof(SpellEffects), unchecked((uint)effect.Effect)),
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

                // CalculateEffectValues yields the base value plus the extra
                // terms AppendEffectInfo renders alongside it (a per-resource
                // value, a per-level coefficient (already included in basePoints) and a spell-power coefficient).
                // Emitting the full breakdown keeps the text render and this
                // document from disagreeing about the same effect.
                var values = spell.CalculateEffectValues(resolved, context);
                var baseValue = values.BaseValue;
                entry["basePoints"] = baseValue;
                if (Math.Abs(values.Variance) > 1.0E-5f)
                {
                    var delta = Math.Abs(baseValue * values.Variance * 0.5f);
                    entry["basePointsMin"] = baseValue - delta;
                    entry["basePointsMax"] = baseValue + delta;
                }

                if (resolved.Scaling != null)
                {
                    entry["scaling"] = new Dictionary<string, object>
                    {
                        ["coefficient"] = resolved.Scaling.Coefficient,
                        ["variance"] = resolved.Scaling.Variance,
                        ["resourceCoefficient"] = resolved.Scaling.ResourceCoefficient,
                    };
                }

                // Each omitted on exactly the condition the text path uses to
                // suppress it -- see CalculateEffectValues, which zeroes a
                // field out whenever AppendEffectInfo would not have printed it.
                AddIfNonZero(entry, "valuePerResource", values.ValuePerResource);
                AddIfNonZero(entry, "realPointsPerLevel", values.RealPointsPerLevel);
                AddIfNonZero(entry, "bonusMultiplier", effect.EffectBonusCoefficient);
                AddIfNonZero(entry, "spellPowerCoefficient", values.SpellPowerCoefficient);

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

                var radius = BuildRadius(effect.EffectRadiusIndex[0]);
                if (radius != null)
                    entry["radius"] = radius;

                var maxRadius = BuildRadius(effect.EffectRadiusIndex[1]);
                if (maxRadius != null)
                    entry["maxRadius"] = maxRadius;

                AddIfNonZero(entry, "chainTargets", effect.EffectChainTargets);

                // get_spell_triggers reads this. Resolve the name here rather
                // than leaving the caller a bare id: an id that resolves to
                // nothing is a data problem the caller should see, and it is
                // only visible if we tried.
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

                var effectClassMask = effect.EffectSpellClassMask;
                if (effectClassMask[0] != 0 || effectClassMask[1] != 0 || effectClassMask[2] != 0 || effectClassMask[3] != 0)
                    entry["spellClassMask"] = effectClassMask;

                result.Add(entry);
            }

            return result;
        }

        // EffectRadiusIndex is an int[2] of *indices*, not radii. Mirrors
        // AppendRadius: resolve against DBC.DBC.SpellRadius and omit entirely
        // when the store has no such key, for both the radius and max-radius
        // slots.
        private static Dictionary<string, object> BuildRadius(int radiusIndex)
        {
            if (DBC.DBC.SpellRadius == null || !DBC.DBC.SpellRadius.ContainsKey(radiusIndex))
                return null;

            var radius = DBC.DBC.SpellRadius[radiusIndex];
            return new Dictionary<string, object>
            {
                ["id"] = radiusIndex,
                ["radius"] = radius.Radius,
                ["maxRadius"] = radius.MaxRadius,
            };
        }

        private static List<object> BuildPowers(SpellInfo spell)
        {
            var result = new List<object>();
            foreach (var power in spell.Powers)
            {
                var entry = new Dictionary<string, object>
                {
                    ["orderIndex"] = power.OrderIndex,
                    ["powerType"] = BuildRawAndName(power.PowerType, typeof(Powers)),
                };
                AddIfNonZero(entry, "manaCost", power.ManaCost);
                AddIfNonZero(entry, "manaCostPerLevel", power.ManaCostPerLevel);
                AddIfNonZero(entry, "manaPerSecond", power.ManaPerSecond);
                AddIfNonZero(entry, "powerCostPct", power.PowerCostPct);
                AddIfNonZero(entry, "powerPctPerSecond", power.PowerPctPerSecond);
                result.Add(entry);
            }

            return result;
        }

        private static List<object> BuildTargets(SpellInfo spell, RenderContext context)
        {
            var result = new List<object>();
            var target = spell.TargetRestrictionsFor(context.Difficulty);
            if (target != null)
            {
                var entry = new Dictionary<string, object>
                {
                    ["difficultyId"] = target.DifficultyID,
                };

                if (target.Targets != 0)
                    entry["targets"] = BuildRawAndFlags(target.Targets, typeof(SpellCastTargetFlags));

                if (target.TargetCreatureType != 0)
                    entry["targetCreatureType"] = BuildRawAndFlags((int)target.TargetCreatureType, typeof(CreatureTypeMask));

                AddIfNonZero(entry, "maxTargets", target.MaxTargets);
                AddIfNonZero(entry, "maxTargetLevel", target.MaxTargetLevel);
                AddIfNonZero(entry, "coneDegrees", target.ConeAngle);
                AddIfNonZero(entry, "width", target.Width);
                result.Add(entry);
            }

            return result;
        }

        // 5.4.8 has no SpellReagentsCurrency table; items are the only reagents.
        private static Dictionary<string, object> BuildReagents(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();

            if (spell.Reagents != null)
            {
                var items = new List<object>();
                for (var i = 0; i < spell.Reagents.Count.Length; i++)
                {
                    if (spell.Reagents.Count[i] == 0 || spell.Reagents.ItemId[i] == 0)
                        continue;

                    items.Add(new Dictionary<string, object>
                    {
                        ["itemId"] = spell.Reagents.ItemId[i],
                        ["count"] = spell.Reagents.Count[i],
                    });
                }

                if (items.Count > 0)
                    result["items"] = items;
            }

            return result;
        }

        // ProcCategoryRecovery, ProcChance, ProcCharges and the proc type mask
        // all live on the AuraOptions-backed SpellInfo properties but belong to
        // "procs", not "aura" -- see the mapping table in the task brief. That
        // leaves CumulativeAura and the duration scalars as the only members of
        // "aura" itself.
        private static Dictionary<string, object> BuildAura(SpellInfo spell, int difficulty)
        {
            var aura = new Dictionary<string, object>();
            var duration = spell.DurationFor(difficulty);
            AddIfNonZero(aura, "cumulativeAura", spell.AuraOptionsFor(difficulty)?.CumulativeAura ?? 0);
            AddIfNonZero(aura, "duration", duration?.Duration ?? 0);
            AddIfNonZero(aura, "durationPerLevel", duration?.Unknown2 ?? 0);
            AddIfNonZero(aura, "maxDuration", duration?.MaxDuration ?? 0);
            return aura;
        }

        private static Dictionary<string, object> BuildProcs(SpellInfo spell, int difficulty)
        {
            var procs = new Dictionary<string, object>();
            var options = spell.AuraOptionsFor(difficulty);
            var ppm = spell.ProcsPerMinuteFor(difficulty);

            AddFlagsWithUnmatched(procs, "procFlags", typeof(ProcFlags), (uint)(options?.ProcTypeMask ?? 0));

            AddIfNonZero(procs, "procChance", options?.ProcChance ?? 0);
            AddIfNonZero(procs, "procCharges", options?.ProcCharges ?? 0);
            AddIfNonZero(procs, "procCooldown", options?.ProcCategoryRecovery ?? 0);
            AddIfNonZero(procs, "baseProcRate", ppm?.BaseProcRate ?? 0f);
            AddIfNonZero(procs, "procsPerMinuteFlags", ppm?.Flags ?? 0);

            return procs;
        }

        // procFlags/procFlagsEx have no wrapping {raw, flags} object of their
        // own (unlike schoolMask/targetCreatureType/etc., built through
        // BuildRawAndFlags below) -- "<key>" is the matched-bit array
        // directly, same as before this fix. A "<key>UnmatchedBits" sibling
        // is added alongside it, only when at least one set bit has no
        // declared member, using the key prefix to disambiguate procFlags's
        // list from procFlagsEx's list sharing the same "procs" dictionary.
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

        private static Dictionary<string, object> BuildScaling(SpellScalingEntry scaling) =>
            new Dictionary<string, object>
            {
                ["class"] = scaling.Class,
                ["castTimeMin"] = scaling.CastTimeMin,
                ["castTimeMax"] = scaling.CastTimeMax,
                ["castTimeMaxLevel"] = scaling.CastTimeMaxLevel,
                ["nerfFactor"] = scaling.NerfFactor,
                ["nerfMaxLevel"] = scaling.NerfMaxLevel,
                ["maxScalingLevel"] = scaling.MaxScalingLevel,
                ["scalesFromItemLevel"] = scaling.ScalesFromItemLevel,
            };

        private static Dictionary<string, object> BuildCooldowns(SpellInfo spell, int difficulty)
        {
            var result = new Dictionary<string, object>();
            var cooldowns = spell.CooldownsFor(difficulty);
            AddIfNonZero(result, "recoveryTime", cooldowns?.RecoveryTime ?? 0);
            AddIfNonZero(result, "categoryRecoveryTime", cooldowns?.CategoryRecoveryTime ?? 0);
            AddIfNonZero(result, "startRecoveryTime", cooldowns?.StartRecoveryTime ?? 0);
            return result;
        }

        private static Dictionary<string, object> BuildLevels(SpellInfo spell, int difficulty)
        {
            var result = new Dictionary<string, object>();
            var levels = spell.LevelsFor(difficulty);
            if (levels == null)
                return result;

            AddIfNonZero(result, "baseLevel", levels.BaseLevel);
            AddIfNonZero(result, "spellLevel", levels.SpellLevel);
            AddIfNonZero(result, "maxLevel", levels.MaxLevel);
            return result;
        }

        private static Dictionary<string, object> BuildInterrupts(SpellInfo spell, int difficulty)
        {
            var result = new Dictionary<string, object>();
            var interrupts = spell.InterruptsFor(difficulty);
            if (interrupts == null)
                return result;

            if (interrupts.InterruptFlags != 0)
                result["interruptFlags"] = BuildRawAndFlags((uint)interrupts.InterruptFlags, typeof(SpellInterruptFlags));
            if (interrupts.AuraInterruptFlags[0] != 0)
                result["auraInterruptFlags"] = BuildRawAndFlags((uint)interrupts.AuraInterruptFlags[0], typeof(SpellAuraInterruptFlags));

            // Channel interrupt flags decode through their own enum, not
            // SpellAuraInterruptFlags -- SpellInfo.cs's text path casts
            // ChannelInterruptFlags to SpellChannelInterruptFlags; reusing
            // SpellAuraInterruptFlags here would decode the same bits against
            // the wrong bit-to-name table.
            if (interrupts.ChannelInterruptFlags[0] != 0)
                result["channelInterruptFlags"] = BuildRawAndFlags((uint)interrupts.ChannelInterruptFlags[0], typeof(SpellChannelInterruptFlags));
            return result;
        }

        private static Dictionary<string, object> BuildShapeshift(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            AddIfNonZero(result, "stances", spell.Stances);
            AddIfNonZero(result, "stancesNot", spell.StancesNot);
            return result;
        }

        private static Dictionary<string, object> BuildEquippedItems(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            if (spell.EquippedItems == null)
                return result;

            AddIfNonZero(result, "equippedItemSubClassMask", spell.EquippedItems.EquippedItemSubClassMask);
            AddIfNonZero(result, "equippedItemInventoryTypeMask", spell.EquippedItems.EquippedItemInventoryTypeMask);
            if (spell.EquippedItems.EquippedItemClass >= 0)
                result["equippedItemClass"] = BuildRawAndName((uint)spell.EquippedItems.EquippedItemClass, typeof(ItemClass));
            return result;
        }

        // Forward direction from the spell's own effects; reverse direction
        // from SpellTriggerStore, which DBC.LoadAsync builds during load.
        // Emitted unconditionally when requested, including when both arrays
        // are empty: "this spell triggers nothing" is an answer, and an
        // absent key would be indistinguishable from "the section was
        // filtered out".
        private static Dictionary<string, object> BuildTriggers(SpellInfo spell, RenderContext context)
        {
            var triggers = spell.EffectsFor(context.Difficulty)
                .Where(effect => effect != null && effect.Entry.EffectTriggerSpell != 0)
                .Select(effect => effect.Entry.EffectTriggerSpell)
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

        // Mirrors SpellInfo.AppendItemInfo: 5.4.8 has no ItemEffect table, so
        // an item uses a spell when one of Item-sparse.db2's five SpellId
        // columns names it.
        private static List<object> BuildItems(SpellInfo spell)
        {
            var result = new List<object>();

            // ItemSparse is a Storage<T> property that stays null until
            // DBC.LoadAsync runs; section validation needs no data, so guard
            // rather than crash on a spell built without a load.
            if (DBC.DBC.ItemSparse == null)
                return result;

            foreach (var item in DBC.DBC.ItemSparse.Values.Where(i => i.SpellId.Contains(spell.ID)))
            {
                result.Add(new Dictionary<string, object>
                {
                    ["id"] = item.ID,
                    ["name"] = item.Name,
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

        // Stances/StancesNot are the only 64-bit fields in this file
        // (SpellShapeshift.dbc stores each as one 64-bit mask). Mirrors every
        // other overload's exact-zero test; do not introduce an epsilon here.
        private static void AddIfNonZero(IDictionary<string, object> dict, string key, ulong value)
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
