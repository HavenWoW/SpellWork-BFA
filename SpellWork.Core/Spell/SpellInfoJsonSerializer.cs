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
        // single "spell attributes" enum: SpellEnums.cs declares eight of
        // them (SpellAtribute, SpellAtributeEx .. SpellAtributeEx7), so the
        // group-to-enum mapping is a dispatch table, not a single cast.
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
        };

        // Section names the `sections` filter accepts; the single source of
        // truth for both validating `sections` and echoing back which of them
        // were applied. "context" is the render context (level, itemLevel and
        // the difficulty block) basePoints is computed against. "general" holds
        // the scalar spell-row fields 5.4.8 kept under "misc" (spec 4.2: there
        // is no SpellMisc in 3.3.5, so there is no "misc" object either).
        private static readonly string[] ValidSections =
        {
            "general", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs",
            "context", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems",
            "triggers", "totems", "visual",
        };

        /// <param name="difficulty">
        /// The difficulty block. A tool that resolved a requested spell id to a
        /// different variant passes the view it resolved with; when null the
        /// block is resolved for <paramref name="spell"/> at
        /// <see cref="RenderContext.Difficulty"/>.
        /// </param>
        /// <param name="itemNames">
        /// item_template names by entry, when the world database is configured;
        /// a reagent's <c>name</c> is emitted only when its item is in here.
        /// </param>
        public static string Serialize(SpellInfo spell, RenderContext context, IReadOnlyCollection<string> sections = null,
            SpellDifficultyView difficulty = null, IReadOnlyDictionary<int, string> itemNames = null)
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
                // 3.3.5 NameSubtext ("Rank 1"); empty for unranked spells.
                ["rank"] = spell.Spell.NameSubtext ?? string.Empty,
            };

            // Echoed only when the caller actually filtered. Its presence
            // lets a consumer tell "effects is missing because it was
            // filtered out" (absent from this list) apart from "effects is
            // missing because this spell genuinely has none" (present in
            // this list, but the effects key itself still absent below).
            if (include != null)
                root["sections"] = ValidSections.Where(Wants).ToList();

            // basePoints (in "effects") is computed against the supplied
            // RenderContext, so the document records which level produced it,
            // and which SpellDifficulty mode (if any) was asked for. Recorded
            // at the root because it describes how the *document* was
            // rendered. Emitted directly: a level of 0 is a real render
            // context and must not be indistinguishable from "omitted".
            if (Wants("context"))
            {
                root["level"] = context.Level;
                root["itemLevel"] = context.ItemLevel;
                root["difficulty"] = BuildDifficulty(difficulty ?? SpellDifficultyView.For(spell.ID, context.Difficulty));
            }

            AddIfNotEmpty(root, "description", spell.Description);
            AddIfNotEmpty(root, "auraDescription", spell.Spell.AuraDescription);

            if (Wants("general"))
                BuildGeneral(root, spell);

            if (Wants("attributes"))
                root["attributes"] = BuildAttributes(spell);

            // ProcTypeMask/ProcChance/ProcCharges of the Spell row (3.3.5 has
            // no SpellAuraOptions). Emitted always: chance 0 and no charges
            // are values, not absences.
            if (Wants("procs"))
            {
                root["procFlags"] = BuildRawAndFlags(spell.ProcFlags, typeof(ProcFlags));
                root["procChance"] = spell.ProcChance;
                root["procCharges"] = spell.ProcCharges;
            }

            if (Wants("effects"))
                AddIfNotEmpty(root, "effects", BuildEffects(spell, context));

            if (Wants("powers"))
            {
                AddIfNotEmpty(root, "powers", BuildPowers(spell));

                var rune = spell.RuneCost;
                if (rune != null)
                {
                    root["runeCost"] = new Dictionary<string, object>
                    {
                        ["id"] = spell.Spell.RuneCostID,
                        ["blood"] = rune.RuneCost[0],
                        ["unholy"] = rune.RuneCost[1],
                        ["frost"] = rune.RuneCost[2],
                        ["runicPower"] = rune.RunicPower,
                    };
                }
            }

            if (Wants("targets"))
                AddIfNotEmpty(root, "targets", BuildTargets(spell));

            if (Wants("reagents"))
                AddIfNotEmpty(root, "reagents", BuildReagents(spell, itemNames));

            if (Wants("aura"))
                AddIfNotEmpty(root, "aura", BuildAura(spell));

            if (Wants("cooldowns"))
                AddIfNotEmpty(root, "cooldowns", BuildCooldowns(spell));

            if (Wants("levels"))
                AddIfNotEmpty(root, "levels", BuildLevels(spell));

            if (Wants("interrupts"))
                AddIfNotEmpty(root, "interrupts", BuildInterrupts(spell));

            if (Wants("shapeshift"))
                AddIfNotEmpty(root, "shapeshift", BuildShapeshift(spell));

            if (Wants("totems"))
            {
                var totems = new Dictionary<string, object>();
                if (spell.TotemCategories.Any(c => c != 0))
                    totems["totemCategory"] = spell.TotemCategories;
                if (spell.Totems.Any(t => t != 0))
                    totems["totem"] = spell.Totems;
                AddIfNotEmpty(root, "totems", totems);
            }

            if (Wants("equippedItems"))
                AddIfNotEmpty(root, "equippedItems", BuildEquippedItems(spell));

            if (Wants("triggers"))
                root["triggers"] = BuildTriggers(spell);

            // Emitted unconditionally: {"storeUnloaded": true, ...} when a
            // gated store is missing, otherwise the chain (spec §6). The same
            // single ResolveSection call the text block and
            // get_spell_visual_chain branch on.
            if (Wants("visual"))
                root["visual"] = SpellVisualChainJson.Describe(SpellVisualChainResolver.ResolveSection(spell.ID));

            return JsonSerializer.Serialize(root, SerializerOptions);
        }

        private static Dictionary<string, object> BuildDifficulty(SpellDifficultyView difficulty) =>
            new Dictionary<string, object>
            {
                ["requested"] = difficulty.Requested,
                ["resolvedSpellId"] = difficulty.ResolvedSpellId,
                ["variants"] = difficulty.Variants,
            };

        // The scalar Spell-row fields. Root keys rather than a nested object:
        // search_spells filters on family, and burying it one level deep
        // makes every caller destructure to reach it.
        private static void BuildGeneral(IDictionary<string, object> root, SpellInfo spell)
        {
            if (spell.SchoolMask != 0)
                root["schoolMask"] = BuildRawAndFlags(spell.SchoolMask, typeof(SpellSchoolMask));

            // SPELLFAMILY_GENERIC is 0 and is a real family: emitted always.
            root["spellFamilyName"] = BuildRawAndName(spell.SpellFamilyName, typeof(SpellFamilyNames));
            root["spellClassMask"] = spell.SpellClassMask;

            // The raw Spell.Category id, plus the SpellCategory.dbc row's
            // flags when that row exists.
            if (spell.CategoryID != 0)
            {
                var category = new Dictionary<string, object> { ["id"] = spell.CategoryID };
                if (spell.Category != null)
                    category["flags"] = BuildRawAndFlags(spell.Category.Flags, typeof(SpellCategoryFlags));
                root["category"] = category;
            }

            AddIfNonZero(root, "speed", spell.Speed);

            // CastingTimeIndex/RangeIndex are indices, not values: resolved
            // to an object with the id plus the row's values, or omitted.
            AddIfNotEmpty(root, "castTime", BuildCastTime(spell.CastingTimeIndex));
            AddIfNotEmpty(root, "range", BuildRange(spell));

            AddIfNonZero(root, "spellIconID", spell.SpellIconID);
            AddIfNonZero(root, "activeIconID", spell.ActiveIconID);
            AddIfNonZero(root, "startRecoveryCategory", spell.StartRecoveryCategory);

            // MECHANIC_NONE, SPELL_DAMAGE_CLASS_NONE, DISPEL_NONE and
            // SPELL_PREVENTION_TYPE_NONE are real values: emitted even at 0.
            root["mechanic"] = BuildRawAndName(spell.Mechanic, typeof(Mechanics));
            root["damageClass"] = BuildRawAndName(spell.DamageClass, typeof(SpellDmgClass));
            root["dispel"] = BuildRawAndName(spell.Dispel, typeof(DispelType));
            root["preventionType"] = BuildRawAndFlags(spell.PreventionType, typeof(SpellPreventionType));
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
                ["base"] = castTime.Base,
                ["perLevel"] = castTime.PerLevel,
                ["minimum"] = castTime.Minimum,
            };
        }

        // RangeIndex is likewise an index, resolved through SpellInfo.Range.
        private static Dictionary<string, object> BuildRange(SpellInfo spell)
        {
            var range = spell.Range;
            if (range == null)
                return null;

            return new Dictionary<string, object>
            {
                ["id"] = spell.RangeIndex,
                ["displayName"] = range.DisplayName,
                ["minRangeHostile"] = range.RangeMin[0],
                ["minRangeFriendly"] = range.RangeMin[1],
                ["maxRangeHostile"] = range.RangeMax[0],
                ["maxRangeFriendly"] = range.RangeMax[1],
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
        // alone) because attributes spans eight different enums sharing
        // one array; a bare bit position would be ambiguous about which
        // group's enum it failed to match against.
        private static List<object> BuildAttributes(SpellInfo spell)
        {
            // The 8 attribute words of the Spell row (CoA words 0-7).
            var groups = spell.Attributes;

            var result = new List<object>();
            for (var group = 0; group < Math.Min(groups.Length, AttributeEnumTypes.Length); group++)
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

        // One entry per non-zero Effect[i] slot of the Spell row (spec 4.2).
        // Every column of the effect's group is emitted, zero or not, so the
        // shape of an effect never depends on its values. 3.3.5 has no
        // per-difficulty effect rows, so there is no per-effect difficulty.
        //
        // basePoints is the CalcValue base at the context level (BasePoints
        // plus the level term, plus 1 when DieSides != 0 -- see
        // SpellScalingCalculator); basePointsMin/Max appear when DieSides
        // gives a range. The raw columns sit beside it.
        private static List<object> BuildEffects(SpellInfo spell, RenderContext context)
        {
            var result = new List<object>();
            foreach (var resolved in spell.Effects)
            {
                var effect = resolved?.Entry;
                if (effect == null)
                    continue;

                var values = spell.CalculateEffectValues(resolved, context);
                var entry = new Dictionary<string, object>
                {
                    ["index"] = effect.EffectIndex,
                    ["effect"] = BuildRawAndName(effect.Effect, typeof(SpellEffects)),
                    ["aura"] = BuildRawAndName(effect.EffectAura, typeof(AuraType)),
                    ["basePoints"] = values.BaseValue,
                };

                if (values.MinValue != values.MaxValue)
                {
                    entry["basePointsMin"] = values.MinValue;
                    entry["basePointsMax"] = values.MaxValue;
                }

                entry["dieSides"] = effect.EffectDieSides;
                entry["realPointsPerLevel"] = effect.EffectRealPointsPerLevel;
                entry["pointsPerComboPoint"] = effect.EffectPointsPerCombo;
                // WoWDBDefs EffectAmplitude (server: EffectValueMultiplier);
                // the aura tick period is auraPeriod.
                entry["amplitude"] = effect.EffectAmplitude;
                entry["auraPeriod"] = effect.EffectAuraPeriod;
                entry["chainTargets"] = effect.EffectChainTargets;
                entry["itemType"] = effect.EffectItemType;
                entry["mechanic"] = BuildRawAndName(effect.EffectMechanic, typeof(Mechanics));
                entry["implicitTargetA"] = BuildRawAndName(effect.ImplicitTarget[0], typeof(Targets));
                entry["implicitTargetB"] = BuildRawAndName(effect.ImplicitTarget[1], typeof(Targets));

                // The SpellRadius row for the index, or null when the index is
                // 0 or names no row.
                entry["radius"] = BuildRadius(effect.EffectRadiusIndex);

                entry["miscValue"] = effect.EffectMiscValue[0];
                entry["miscValueB"] = effect.EffectMiscValue[1];

                // get_spell_triggers reads this. The name is resolved here: an
                // id that resolves to nothing is a data problem the caller
                // should see. Null when the effect triggers nothing.
                entry["triggerSpell"] = effect.EffectTriggerSpell == 0
                    ? null
                    : new Dictionary<string, object>
                    {
                        ["id"] = effect.EffectTriggerSpell,
                        ["name"] = DBC.DBC.SpellInfoStore.TryGetValue(effect.EffectTriggerSpell, out var triggered)
                            ? triggered.Name
                            : null,
                    };

                // The effect's own three mask words (CoA flag96 per effect).
                entry["spellClassMask"] = effect.EffectSpellClassMask;

                // WoWDBDefs EffectChainAmplitude (server: EffectDamageMultiplier)
                // and EffectBonusCoefficient (server: EffectBonusMultiplier).
                entry["damageMultiplier"] = effect.EffectChainAmplitude;
                entry["bonusMultiplier"] = effect.EffectBonusCoefficient;

                result.Add(entry);
            }

            return result;
        }

        // EffectRadiusIndex is an index, not a radius. Mirrors AppendRadius:
        // resolve against DBC.DBC.SpellRadius; null when there is no such row.
        private static Dictionary<string, object> BuildRadius(int radiusIndex)
        {
            if (DBC.DBC.SpellRadius == null || !DBC.DBC.SpellRadius.ContainsKey(radiusIndex))
                return null;

            var radius = DBC.DBC.SpellRadius[radiusIndex];
            return new Dictionary<string, object>
            {
                ["id"] = radiusIndex,
                ["radius"] = radius.Radius,
                ["maxRadius"] = radius.RadiusMax,
            };
        }

        // 3.3.5 has one power cost per spell, on the Spell row.
        private static List<object> BuildPowers(SpellInfo spell)
        {
            var result = new List<object>();
            var row = spell.Spell;
            if (row.ManaCost == 0 && row.ManaCostPct == 0 && row.ManaCostPerLevel == 0 && row.ManaPerSecond == 0 && row.ManaPerSecondPerLevel == 0)
                return result;

            var entry = new Dictionary<string, object>
            {
                ["powerType"] = BuildRawAndName(unchecked((uint)row.PowerType), typeof(Powers)),
            };
            AddIfNonZero(entry, "manaCost", row.ManaCost);
            AddIfNonZero(entry, "manaCostPerLevel", row.ManaCostPerLevel);
            AddIfNonZero(entry, "manaPerSecond", row.ManaPerSecond);
            AddIfNonZero(entry, "manaPerSecondPerLevel", row.ManaPerSecondPerLevel);
            AddIfNonZero(entry, "powerCostPct", row.ManaCostPct);
            result.Add(entry);
            return result;
        }

        private static Dictionary<string, object> BuildTargets(SpellInfo spell)
        {
            var row = spell.Spell;
            var entry = new Dictionary<string, object>();
            if (row.Targets != 0)
                entry["targets"] = BuildRawAndFlags(row.Targets, typeof(SpellCastTargetFlags));

            if (row.TargetCreatureType != 0)
                entry["targetCreatureType"] = BuildRawAndFlags((int)row.TargetCreatureType, typeof(CreatureTypeMask));

            AddIfNonZero(entry, "maxTargets", row.MaxTargets);
            AddIfNonZero(entry, "maxTargetLevel", row.MaxTargetLevel);
            return entry;
        }

        // [{item, count, name?}] per non-empty reagent slot. 3.3.5 Item.dbc
        // has no names; name comes from item_template when the caller
        // supplies it (world database configured), and is omitted otherwise.
        private static List<object> BuildReagents(SpellInfo spell, IReadOnlyDictionary<int, string> itemNames)
        {
            var result = new List<object>();
            foreach (var (item, count) in spell.Reagents)
            {
                var entry = new Dictionary<string, object> { ["item"] = item, ["count"] = count };
                if (itemNames != null && itemNames.TryGetValue(item, out var name) && name != null)
                    entry["name"] = name;
                result.Add(entry);
            }

            return result;
        }

        // CumulativeAura and the duration scalars; proc data is at the root.
        private static Dictionary<string, object> BuildAura(SpellInfo spell)
        {
            var aura = new Dictionary<string, object>();
            AddIfNonZero(aura, "cumulativeAura", spell.CumulativeAura);
            AddIfNonZero(aura, "duration", spell.Duration);
            AddIfNonZero(aura, "durationPerLevel", spell.DurationPerLevel);
            AddIfNonZero(aura, "maxDuration", spell.MaxDuration);
            return aura;
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

        private static Dictionary<string, object> BuildCooldowns(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            AddIfNonZero(result, "recoveryTime", spell.RecoveryTime);
            AddIfNonZero(result, "categoryRecoveryTime", spell.CategoryRecoveryTime);
            AddIfNonZero(result, "startRecoveryTime", spell.StartRecoveryTime);
            return result;
        }

        private static Dictionary<string, object> BuildLevels(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            AddIfNonZero(result, "baseLevel", spell.BaseLevel);
            AddIfNonZero(result, "spellLevel", spell.SpellLevel);
            AddIfNonZero(result, "maxLevel", spell.MaxLevel);
            return result;
        }

        private static Dictionary<string, object> BuildInterrupts(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            if (spell.InterruptFlags != 0)
                result["interruptFlags"] = BuildRawAndFlags(spell.InterruptFlags, typeof(SpellInterruptFlags));
            if (spell.AuraInterruptFlags != 0)
                result["auraInterruptFlags"] = BuildRawAndFlags(spell.AuraInterruptFlags, typeof(SpellAuraInterruptFlags));

            // Channel interrupt flags decode through their own enum, not
            // SpellAuraInterruptFlags -- SpellInfo.cs's text path casts
            // ChannelInterruptFlags to SpellChannelInterruptFlags; reusing
            // SpellAuraInterruptFlags here would decode the same bits against
            // the wrong bit-to-name table.
            if (spell.ChannelInterruptFlags != 0)
                result["channelInterruptFlags"] = BuildRawAndFlags(spell.ChannelInterruptFlags, typeof(SpellChannelInterruptFlags));
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
            AddIfNonZero(result, "equippedItemSubClassMask", spell.EquippedItemSubclass);
            AddIfNonZero(result, "equippedItemInventoryTypeMask", spell.EquippedItemInvTypes);
            if (spell.EquippedItemClass >= 0)
                result["equippedItemClass"] = BuildRawAndName((uint)spell.EquippedItemClass, typeof(ItemClass));
            return result;
        }

        // Forward direction from the spell's own effects; reverse direction
        // from SpellTriggerStore, which DBC.LoadAsync builds during load.
        // Emitted unconditionally when requested, including when both arrays
        // are empty: "this spell triggers nothing" is an answer, and an
        // absent key would be indistinguishable from "the section was
        // filtered out".
        private static Dictionary<string, object> BuildTriggers(SpellInfo spell)
        {
            var triggers = spell.Effects
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
        // inside it) since each of these fields has its own wrapping object.
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
        // for attributes (each word carries its own value). BuildRawAndFlags's
        // callers are different -- preventionType and procFlags are emitted
        // unconditionally, even at zero, precisely
        // so SPELL_PREVENTION_TYPE_NONE stays distinguishable from "this
        // field never got serialised" -- so an empty flags array there would
        // erase that distinction again. When the enum declares a zero member,
        // synthesise it as the sole flags entry instead of leaving the array
        // empty; schoolMask/targets/targetCreatureType keep the caller-side
        // `!= 0` gate that already prevents them from reaching this path at
        // zero, so this fires for preventionType, procFlags and category flags -- and
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
