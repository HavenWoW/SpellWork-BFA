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

        // Section names the `sections` filter accepts -- 5.4.8's names, kept
        // so every spellwork server is called the same way (spec 6), minus
        // "scaling" (3.3.5 has no scaling data). The single source of truth
        // for both validating `sections` and echoing back which were applied.
        // "context" is the render context (level, itemLevel) basePoints is
        // computed against. "misc" selects the "misc" object plus the root
        // family/class-mask/school-mask/rune-cost keys, as in 5.4.8.
        internal static readonly string[] ValidSections =
        {
            "misc", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs",
            "context", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems",
            "items", "triggers", "totems", "visual",
        };

        /// <summary>The <c>itemsNote</c> emitted when no world database supplied the items.</summary>
        public const string ItemsWithoutWorldDatabaseNote =
            "World database not configured: items using this spell come from item_template (spellid_1..spellid_5).";

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
        /// <param name="itemsUsingSpell">
        /// The item_template slots naming this spell, when the world database
        /// is configured; null means it is not, and "items" is then an empty
        /// array with an "itemsNote" saying so.
        /// </param>
        public static string Serialize(SpellInfo spell, RenderContext context, IReadOnlyCollection<string> sections = null,
            SpellDifficultyView difficulty = null, IReadOnlyDictionary<int, string> itemNames = null,
            IReadOnlyList<Database.ItemSpellUse> itemsUsingSpell = null)
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

            var view = difficulty ?? SpellDifficultyView.For(spell.ID, context.Difficulty);
            var root = new Dictionary<string, object>
            {
                // The spell this document describes (the resolved variant).
                ["id"] = spell.ID,
                // The id the caller asked for; equals id when no difficulty
                // resolution changed it.
                ["requestedId"] = view.RequestedSpellId,
                ["name"] = spell.Name,
                // 3.3.5 NameSubtext ("Rank 1"); empty for unranked spells.
                // Added for CoA; 5.4.8's nameSubtext is emitted below too.
                ["rank"] = spell.Spell.NameSubtext ?? string.Empty,
                // Always present, whatever the sections filter: which spell
                // this document is and which SpellDifficulty mode (if any)
                // led to it (spec D4).
                ["difficulty"] = BuildDifficulty(view),
            };

            // Echoed only when the caller actually filtered. Its presence
            // lets a consumer tell "effects is missing because it was
            // filtered out" (absent from this list) apart from "effects is
            // missing because this spell genuinely has none" (present in
            // this list, but the effects key itself still absent below).
            if (include != null)
                root["sections"] = ValidSections.Where(Wants).ToList();

            // basePoints (in "effects") is computed against the supplied
            // RenderContext, so the document records which level produced it.
            // Recorded at the root because it describes how the *document* was
            // rendered. Emitted directly: a level of 0 is a real render
            // context and must not be indistinguishable from "omitted".
            if (Wants("context"))
            {
                root["level"] = context.Level;
                root["itemLevel"] = context.ItemLevel;
            }

            AddIfNotEmpty(root, "description", spell.Description);
            AddIfNotEmpty(root, "nameSubtext", spell.Spell.NameSubtext);
            AddIfNotEmpty(root, "auraDescription", spell.Spell.AuraDescription);

            // The 5.4.8 envelope (spec 6): the scalar Spell-row fields live in
            // a "misc" object; family, class mask, school mask and rune cost
            // sit at the root under the same section, as in 5.4.8.
            if (Wants("misc"))
            {
                AddIfNotEmpty(root, "misc", BuildMisc(spell));

                // SPELLFAMILY_GENERIC is 0 and is a real family: emitted always.
                root["spellFamilyName"] = BuildRawAndName(spell.SpellFamilyName, typeof(SpellFamilyNames));
                root["spellClassMask"] = spell.SpellClassMask;

                if (spell.SchoolMask != 0)
                    root["schoolMask"] = BuildRawAndFlags(spell.SchoolMask, typeof(SpellSchoolMask));

                var rune = spell.RuneCost;
                if (rune != null)
                {
                    // 5.4.8 keys; "id" is added (the SpellRuneCost row id).
                    // 3.3.5 has no chromatic rune.
                    root["runeCost"] = new Dictionary<string, object>
                    {
                        ["blood"] = rune.RuneCost[0],
                        ["unholy"] = rune.RuneCost[1],
                        ["frost"] = rune.RuneCost[2],
                        ["runicPower"] = rune.RunicPower,
                        ["id"] = spell.Spell.RuneCostID,
                    };
                }
            }

            if (Wants("attributes"))
                root["attributes"] = BuildAttributes(spell);

            if (Wants("effects"))
                AddIfNotEmpty(root, "effects", BuildEffects(spell, context));

            if (Wants("powers"))
                AddIfNotEmpty(root, "powers", BuildPowers(spell));

            if (Wants("targets"))
                AddIfNotEmpty(root, "targets", BuildTargets(spell));

            if (Wants("reagents"))
                AddIfNotEmpty(root, "reagents", BuildReagents(spell, itemNames));

            if (Wants("aura"))
                AddIfNotEmpty(root, "aura", BuildAura(spell));

            // ProcTypeMask/ProcChance/ProcCharges of the Spell row (3.3.5 has
            // no SpellAuraOptions), in 5.4.8's "procs" object.
            if (Wants("procs"))
                AddIfNotEmpty(root, "procs", BuildProcs(spell));

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

            // Items whose item_template spell slots name this spell (5.4.8
            // read Item-sparse; 3.3.5 Item.dbc has no spell columns). Emitted
            // always when requested: an empty array is an answer, and the
            // note says when it is empty only because there is no world DB.
            if (Wants("items"))
            {
                root["items"] = BuildItems(itemsUsingSpell);
                if (itemsUsingSpell == null)
                    root["itemsNote"] = ItemsWithoutWorldDatabaseNote;
            }

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

        private static List<object> BuildItems(IReadOnlyList<Database.ItemSpellUse> uses) =>
            (uses ?? Array.Empty<Database.ItemSpellUse>())
                .Select(u => (object)new Dictionary<string, object>
                {
                    ["id"] = u.Entry,
                    ["name"] = u.Name,
                    ["slot"] = u.Slot,
                    ["trigger"] = BuildRawAndName(u.Trigger, typeof(ItemSpelltriggerType)),
                })
                .ToList();

        // The scalar Spell-row fields: 5.4.8's "misc" object, same keys and
        // order. categoryFlags (the SpellCategory.dbc row's flags, when that
        // row exists) is a CoA addition beside category.
        private static Dictionary<string, object> BuildMisc(SpellInfo spell)
        {
            var misc = new Dictionary<string, object>();
            AddIfNonZero(misc, "speed", spell.Speed);

            // CastingTimeIndex/RangeIndex are indices, not values: resolved
            // to an object with the id plus the row's values, or omitted.
            AddIfNotEmpty(misc, "castingTimeIndex", BuildCastTime(spell.CastingTimeIndex));
            AddIfNotEmpty(misc, "rangeIndex", BuildRange(spell));

            AddIfNonZero(misc, "spellIconID", spell.SpellIconID);
            AddIfNonZero(misc, "activeIconID", spell.ActiveIconID);
            AddIfNonZero(misc, "category", spell.CategoryID);
            if (spell.Category != null)
                misc["categoryFlags"] = BuildRawAndFlags(spell.Category.Flags, typeof(SpellCategoryFlags));
            AddIfNonZero(misc, "startRecoveryCategory", spell.StartRecoveryCategory);

            // MECHANIC_NONE, SPELL_DAMAGE_CLASS_NONE, DISPEL_NONE and
            // SPELL_PREVENTION_TYPE_NONE are real values: emitted even at 0.
            misc["mechanic"] = BuildRawAndName(spell.Mechanic, typeof(Mechanics));
            misc["damageClass"] = BuildRawAndName(spell.DamageClass, typeof(SpellDmgClass));
            misc["dispel"] = BuildRawAndName(spell.Dispel, typeof(DispelType));
            misc["preventionType"] = BuildRawAndFlags(spell.PreventionType, typeof(SpellPreventionType));
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
                ["base"] = castTime.Base,
                ["perLevel"] = castTime.PerLevel,
                ["minimum"] = castTime.Minimum,
            };
        }

        // RangeIndex is likewise an index, resolved through SpellInfo.Range.
        // 5.4.8 key names: Negative is the hostile range (SpellRange.dbc
        // slot 0), Positive the friendly one (slot 1).
        private static Dictionary<string, object> BuildRange(SpellInfo spell)
        {
            var range = spell.Range;
            if (range == null)
                return null;

            return new Dictionary<string, object>
            {
                ["id"] = spell.RangeIndex,
                ["displayName"] = range.DisplayName,
                ["minRangeNegative"] = range.RangeMin[0],
                ["minRangePositive"] = range.RangeMin[1],
                ["maxRangeNegative"] = range.RangeMax[0],
                ["maxRangePositive"] = range.RangeMax[1],
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
        // 5.4.8's keys follow 5.4.8's emission rules (each omitted when zero,
        // exactly as there). The 3.3.5-only columns -- dieSides, auraPeriod,
        // itemType, damageMultiplier -- are additions, emitted always. 3.3.5
        // has no per-difficulty effect rows and no SpellEffectScaling, so
        // 5.4.8's resolvedDifficulty and scaling keys have no source here.
        //
        // basePoints is the CalcValue base at the context level (BasePoints
        // plus the level term, plus 1 when DieSides != 0 -- see
        // SpellScalingCalculator); basePointsMin/Max appear when DieSides
        // gives a range.
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
                };

                if (effect.EffectAura != 0)
                    entry["auraType"] = BuildRawAndName(effect.EffectAura, typeof(AuraType));

                entry["basePoints"] = values.BaseValue;
                if (values.MinValue != values.MaxValue)
                {
                    entry["basePointsMin"] = values.MinValue;
                    entry["basePointsMax"] = values.MaxValue;
                }

                // CalculateEffectValues zeroes each of these exactly when the
                // text path would not print it. valuePerResource is the
                // 3.3.5 PointsPerComboPoint; spellPowerCoefficient and
                // bonusMultiplier both come from EffectBonusCoefficient
                // (server: EffectBonusMultiplier), as the text path shows it.
                AddIfNonZero(entry, "valuePerResource", values.ValuePerResource);
                AddIfNonZero(entry, "realPointsPerLevel", values.RealPointsPerLevel);
                AddIfNonZero(entry, "bonusMultiplier", effect.EffectBonusCoefficient);
                AddIfNonZero(entry, "spellPowerCoefficient", values.SpellPowerCoefficient);

                entry["targetA"] = BuildRawAndName(effect.ImplicitTarget[0], typeof(Targets));
                entry["targetB"] = BuildRawAndName(effect.ImplicitTarget[1], typeof(Targets));

                // The SpellRadius row for the index; omitted when the index is
                // 0 or names no row. 3.3.5 has one radius index per effect, so
                // 5.4.8's second slot (maxRadius) never appears.
                var radius = BuildRadius(effect.EffectRadiusIndex);
                if (radius != null)
                    entry["radius"] = radius;

                AddIfNonZero(entry, "chainTargets", effect.EffectChainTargets);

                // get_spell_triggers reads this. The name is resolved here: an
                // id that resolves to nothing is a data problem the caller
                // should see.
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
                // WoWDBDefs EffectAmplitude (server: EffectValueMultiplier);
                // the aura tick period is auraPeriod.
                AddIfNonZero(entry, "amplitude", effect.EffectAmplitude);

                if (effect.EffectMechanic != 0)
                    entry["mechanic"] = BuildRawAndName(effect.EffectMechanic, typeof(Mechanics));

                // The effect's own three mask words (CoA flag96 per effect).
                var classMask = effect.EffectSpellClassMask;
                if (classMask.Any(word => word != 0))
                    entry["spellClassMask"] = classMask;

                // CoA additions (3.3.5-only columns), emitted always.
                entry["dieSides"] = effect.EffectDieSides;
                entry["auraPeriod"] = effect.EffectAuraPeriod;
                entry["itemType"] = effect.EffectItemType;
                // WoWDBDefs EffectChainAmplitude (server: EffectDamageMultiplier).
                entry["damageMultiplier"] = effect.EffectChainAmplitude;

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

        // 5.4.8's targets array of SpellTargetRestrictions rows. 3.3.5 keeps
        // these columns on the Spell row and has no per-difficulty rows, so
        // the array holds at most one entry, with difficultyId 0 (the base
        // row), and is omitted when every column is zero (5.4.8: no row).
        private static List<object> BuildTargets(SpellInfo spell)
        {
            var row = spell.Spell;
            var entry = new Dictionary<string, object>();
            if (row.Targets != 0)
                entry["targets"] = BuildRawAndFlags(row.Targets, typeof(SpellCastTargetFlags));

            if (row.TargetCreatureType != 0)
                entry["targetCreatureType"] = BuildRawAndFlags((int)row.TargetCreatureType, typeof(CreatureTypeMask));

            AddIfNonZero(entry, "maxTargets", row.MaxTargets);
            AddIfNonZero(entry, "maxTargetLevel", row.MaxTargetLevel);

            var result = new List<object>();
            if (entry.Count > 0)
            {
                var withDifficulty = new Dictionary<string, object> { ["difficultyId"] = 0 };
                foreach (var pair in entry)
                    withDifficulty[pair.Key] = pair.Value;
                result.Add(withDifficulty);
            }

            return result;
        }

        // 5.4.8's {items: [{itemId, count}]} per non-empty reagent slot.
        // 3.3.5 Item.dbc has no names; name (a CoA addition) comes from
        // item_template when the caller supplies it (world database
        // configured), and is omitted otherwise.
        private static Dictionary<string, object> BuildReagents(SpellInfo spell, IReadOnlyDictionary<int, string> itemNames)
        {
            var result = new Dictionary<string, object>();
            var items = new List<object>();
            foreach (var (item, count) in spell.Reagents)
            {
                var entry = new Dictionary<string, object> { ["itemId"] = item, ["count"] = count };
                if (itemNames != null && itemNames.TryGetValue(item, out var name) && name != null)
                    entry["name"] = name;
                items.Add(entry);
            }

            if (items.Count > 0)
                result["items"] = items;
            return result;
        }

        // 5.4.8's "procs" object. procFlags is the matched-bit array itself
        // (no {raw, flags} wrapper), with procFlagsUnmatchedBits beside it
        // when a set bit has no declared member; chance and charges are
        // omitted at zero, as in 5.4.8.
        private static Dictionary<string, object> BuildProcs(SpellInfo spell)
        {
            var procs = new Dictionary<string, object>();
            var flags = BuildFlagList(typeof(ProcFlags), spell.ProcFlags, out var unmatchedBits);
            AddIfNotEmpty(procs, "procFlags", flags);
            AddIfNotEmpty(procs, "procFlagsUnmatchedBits", unmatchedBits);
            AddIfNonZero(procs, "procChance", spell.ProcChance);
            AddIfNonZero(procs, "procCharges", spell.ProcCharges);
            return procs;
        }

        // CumulativeAura and the duration scalars; proc data is in "procs".
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
        // callers are different -- preventionType is emitted
        // unconditionally, even at zero, precisely
        // so SPELL_PREVENTION_TYPE_NONE stays distinguishable from "this
        // field never got serialised" -- so an empty flags array there would
        // erase that distinction again. When the enum declares a zero member,
        // synthesise it as the sole flags entry instead of leaving the array
        // empty; schoolMask/targets/targetCreatureType keep the caller-side
        // `!= 0` gate that already prevents them from reaching this path at
        // zero, so this fires for preventionType and category flags -- and
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
