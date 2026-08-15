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
        // (level/itemLevel/mapDifficulty) that basePoints is computed
        // against but that used to leave no trace in the document. Kept as
        // the single source of truth for both validating `sections` and
        // echoing back which of them were applied.
        //
        // "items" and "triggers" are declared here but populated starting in
        // Task 5 -- reserved now so the section validator and get_spell's
        // documented `sections` list agree from the start. "visual" carries
        // the resolved spell-visual chain (BuildVisual below): every
        // SpellXSpellVisual variant this spell has, each variant's missile
        // info, and the deduplicated SpellVisualKit rows it reaches with their
        // raw effects. It was previously excluded on the grounds that a
        // section which validates but never produces data is worse than a
        // clear "unknown section" error; it now produces data, so that reason
        // no longer applies.
        private static readonly string[] ValidSections =
        {
            "misc", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs", "scaling",
            "context", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems",
            "items", "triggers", "visual",
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

                // MapDifficulty is the one piece of render context that can
                // genuinely be absent (nothing selected) rather than merely
                // zero, so it alone is omitted when null. When present,
                // ContentTuningID is the field that actually reaches the
                // arithmetic at SpellInfo.cs:678-679, so it travels alongside
                // the id rather than requiring a second lookup.
                var mapDifficulty = context.MapDifficulty;
                if (mapDifficulty != null)
                {
                    root["mapDifficulty"] = new Dictionary<string, object>
                    {
                        ["id"] = mapDifficulty.ID,
                        ["contentTuningId"] = mapDifficulty.ContentTuningID,
                    };
                }
            }

            AddIfNotEmpty(root, "description", spell.Description);
            AddIfNotEmpty(root, "tooltip", spell.Tooltip);

            if (Wants("misc"))
                AddIfNotEmpty(root, "misc", BuildMisc(spell));

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

            if (Wants("attributes"))
            {
                var attributes = BuildAttributes(spell, out var attributesUnmatchedBits);
                AddIfNotEmpty(root, "attributes", attributes);
                AddIfNotEmpty(root, "attributesUnmatchedBits", attributesUnmatchedBits);
            }

            if (Wants("effects"))
                AddIfNotEmpty(root, "effects", BuildEffects(spell, context));

            if (Wants("powers") && spell.Powers.Count > 0)
                root["powers"] = BuildPowers(spell);

            if (Wants("targets") && spell.TargetRestrictions.Count > 0)
                root["targets"] = BuildTargets(spell);

            if (Wants("reagents"))
                AddIfNotEmpty(root, "reagents", BuildReagents(spell));

            if (Wants("aura"))
                AddIfNotEmpty(root, "aura", BuildAura(spell));

            if (Wants("procs"))
                AddIfNotEmpty(root, "procs", BuildProcs(spell));

            if (Wants("scaling") && spell.Scaling != null)
                AddIfNotEmpty(root, "scaling", BuildScaling(spell.Scaling));

            if (Wants("cooldowns"))
                AddIfNotEmpty(root, "cooldowns", BuildCooldowns(spell));

            if (Wants("levels"))
                AddIfNotEmpty(root, "levels", BuildLevels(spell));

            if (Wants("interrupts"))
                AddIfNotEmpty(root, "interrupts", BuildInterrupts(spell));

            if (Wants("shapeshift"))
                AddIfNotEmpty(root, "shapeshift", BuildShapeshift(spell));

            if (Wants("equippedItems"))
                AddIfNotEmpty(root, "equippedItems", BuildEquippedItems(spell));

            if (Wants("triggers"))
                root["triggers"] = BuildTriggers(spell);

            if (Wants("items"))
                AddIfNotEmpty(root, "items", BuildItems(spell));

            // Assigned unconditionally, deliberately not through
            // AddIfNotEmpty: BuildVisual's whole job is to be able to say
            // "the store is not loaded", and a helper that drops the key when
            // the payload is small would erase exactly that distinction --
            // leaving an unloaded SpellVisual.db2 indistinguishable from a
            // spell that genuinely has no visual.
            if (Wants("visual"))
                root["visual"] = BuildVisual(spell, context);

            return JsonSerializer.Serialize(root, SerializerOptions);
        }

        private static Dictionary<string, object> BuildMisc(SpellInfo spell)
        {
            var misc = new Dictionary<string, object>();
            AddIfNonZero(misc, "speed", spell.Speed);

            if (spell.SchoolMask != 0)
                misc["schoolMask"] = BuildRawAndFlags(spell.SchoolMask, typeof(SpellSchoolMask));

            // CastingTimeIndex/RangeIndex are indices, not values -- the same
            // trap note (e) already flagged for the radius fields. Resolve
            // them the way BuildRadius resolves EffectRadiusIndex, and keep
            // its shape: an object with the id plus the resolved values, or
            // the key omitted when there is no such row.
            AddIfNotEmpty(misc, "castingTimeIndex", BuildCastTime(spell.CastingTimeIndex));
            AddIfNotEmpty(misc, "rangeIndex", BuildRange(spell));

            AddIfNonZero(misc, "spellIconFileDataID", spell.SpellIconFileDataID);
            AddIfNonZero(misc, "activeIconFileDataID", spell.ActiveIconFileDataID);
            AddIfNonZero(misc, "category", spell.Category);
            AddIfNonZero(misc, "startRecoveryCategory", spell.StartRecoveryCategory);

            // MECHANIC_NONE is 0 and meaningful -- same reasoning as
            // damageClass/dispel above, so this is emitted unconditionally
            // rather than through AddIfNonZero.
            misc["mechanic"] = BuildRawAndName(spell.Mechanic, typeof(Mechanics));

            // damageClass, dispel and preventionType are emitted even when
            // zero: SPELL_DAMAGE_CLASS_NONE, DISPEL_NONE and
            // SPELL_PREVENTION_TYPE_NONE are real values the GUI prints
            // unconditionally (SpellInfo.cs:279-280,428), and omitting them
            // at zero would make "none" indistinguishable from "this field
            // never got serialised". Every other field here keeps the
            // omit-zero rule.
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
            if (!DBC.DBC.SpellCastTimes.ContainsKey(castingTimeIndex))
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

        // RangeIndex is likewise an index, but SpellInfo already resolves it
        // into `Range` at load time (DBC.cs:144-145) -- there is no store
        // lookup left to repeat here, just the same BuildRadius-style shape
        // read off the entry SpellInfo already holds.
        private static Dictionary<string, object> BuildRange(SpellInfo spell)
        {
            if (spell.Range == null)
                return null;

            return new Dictionary<string, object>
            {
                ["id"] = spell.RangeIndex,
                ["displayName"] = spell.Range.DisplayName,
                ["minRangeNegative"] = spell.Range.MinRange[0],
                ["minRangePositive"] = spell.Range.MinRange[1],
                ["maxRangeNegative"] = spell.Range.MaxRange[0],
                ["maxRangePositive"] = spell.Range.MaxRange[1],
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
        private static List<object> BuildAttributes(SpellInfo spell, out List<object> unmatchedBits)
        {
            var groups = new[]
            {
                spell.Attributes, spell.AttributesEx, spell.AttributesEx2, spell.AttributesEx3,
                spell.AttributesEx4, spell.AttributesEx5, spell.AttributesEx6, spell.AttributesEx7,
                spell.AttributesEx8, spell.AttributesEx9, spell.AttributesEx10, spell.AttributesEx11,
                spell.AttributesEx12, spell.AttributesEx13,
            };

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

                    var name = FindDeclaredFlagName(AttributeEnumTypes[group], mask);
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

        // Effects holds one entry per (effect index, difficulty) pair, not one
        // per effect -- SpellEffectInfoStore is keyed on EffectIndex alone and
        // would silently drop rows for later difficulties, so it is not used
        // here. HasEffect/HasAura/HasTargetA/HasTargetB all guard against null
        // entries; this does the same.
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

                // CalculateEffectValues folds in Coefficient, Scaling and the
                // spell's level data for the base value, plus the four extra
                // terms AppendEffectInfo renders alongside it
                // (SpellInfo.cs:694-716: a per-resource value, a per-level
                // value, a spell-power coefficient and an attack-power
                // coefficient). Reading only CalculateBaseEffectValue and
                // stopping there was the Critical this extraction exists to
                // fix: Fireball's whole 0.59 spell-power scaling lived in
                // that dropped half, so its basePoints alone reads as "deals
                // 0 damage". Emitting the full breakdown is the one
                // divergence between the GUI and MCP this whole extraction
                // exists to prevent.
                var values = spell.CalculateEffectValues(effect, context);
                var baseValue = values.BaseValue;
                entry["basePoints"] = baseValue;
                if (Math.Abs(effect.Variance) > 1.0E-5f)
                {
                    var delta = Math.Abs(baseValue * effect.Variance * 0.5f);
                    entry["basePointsMin"] = baseValue - delta;
                    entry["basePointsMax"] = baseValue + delta;
                }

                // Each omitted on exactly the condition the text path uses to
                // suppress it -- see CalculateEffectValues, which zeroes a
                // field out (or leaves UsesExpectedStat false) whenever
                // AppendEffectInfo would not have printed it.
                AddIfNonZero(entry, "valuePerResource", values.ValuePerResource);
                AddIfNonZero(entry, "perLevelValue", values.PerLevelValue);
                AddIfNonZero(entry, "spellPowerCoefficient", values.SpellPowerCoefficient);
                AddIfNonZero(entry, "attackPowerCoefficient", values.AttackPowerCoefficient);
                if (values.UsesExpectedStat)
                    entry["usesExpectedStat"] = true;

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

                var effectClassMask = Array.ConvertAll(effect.EffectSpellClassMask, i => (uint)i);
                if (effectClassMask[0] != 0 || effectClassMask[1] != 0 || effectClassMask[2] != 0 || effectClassMask[3] != 0)
                    entry["spellClassMask"] = effectClassMask;

                result.Add(entry);
            }

            return result;
        }

        // EffectRadiusIndex is a uint[2] of *indices*, not radii. Mirrors
        // AppendRadius: resolve against DBC.DBC.SpellRadius and omit entirely
        // when the store has no such key, for both the radius and max-radius
        // slots.
        private static Dictionary<string, object> BuildRadius(uint radiusIndex)
        {
            if (!DBC.DBC.SpellRadius.ContainsKey((int)radiusIndex))
                return null;

            var radius = DBC.DBC.SpellRadius[(int)radiusIndex];
            return new Dictionary<string, object>
            {
                ["id"] = (int)radiusIndex,
                ["radius"] = radius.Radius,
                ["radiusMin"] = radius.RadiusMin,
                ["maxRadius"] = radius.MaxRadius,
            };
        }

        private static List<object> BuildPowers(SpellInfo spell)
        {
            var result = new List<object>();
            foreach (var power in spell.Powers.OrderBy(p => p.OrderIndex))
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
                AddIfNonZero(entry, "powerCostMaxPct", power.PowerCostMaxPct);
                AddIfNonZero(entry, "powerPctPerSecond", power.PowerPctPerSecond);
                AddIfNonZero(entry, "optionalCost", power.OptionalCost);
                result.Add(entry);
            }

            return result;
        }

        private static List<object> BuildTargets(SpellInfo spell)
        {
            var result = new List<object>();
            foreach (var target in spell.TargetRestrictions)
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
                AddIfNonZero(entry, "coneDegrees", target.ConeDegrees);
                AddIfNonZero(entry, "width", target.Width);
                result.Add(entry);
            }

            return result;
        }

        // Reagents and ReagentsCurrency are two different DBC sources feeding
        // one JSON section.
        private static Dictionary<string, object> BuildReagents(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();

            if (spell.Reagents != null)
            {
                var items = new List<object>();
                for (var i = 0; i < spell.Reagents.ReagentCount.Length; i++)
                {
                    if (spell.Reagents.ReagentCount[i] == 0 || spell.Reagents.Reagent[i] == 0)
                        continue;

                    items.Add(new Dictionary<string, object>
                    {
                        ["itemId"] = spell.Reagents.Reagent[i],
                        ["count"] = spell.Reagents.ReagentCount[i],
                    });
                }

                if (items.Count > 0)
                    result["items"] = items;
            }

            if (spell.ReagentsCurrency.Count > 0)
            {
                result["currency"] = spell.ReagentsCurrency.Select(rc => (object)new Dictionary<string, object>
                {
                    ["currencyTypeId"] = rc.CurrencyTypesID,
                    ["count"] = rc.CurrencyCount,
                }).ToList();
            }

            return result;
        }

        // ProcCategoryRecovery, ProcChance, ProcCharges and the proc type mask
        // all live on the AuraOptions-backed SpellInfo properties but belong to
        // "procs", not "aura" -- see the mapping table in the task brief. That
        // leaves CumulativeAura and the duration scalars as the only members of
        // "aura" itself.
        private static Dictionary<string, object> BuildAura(SpellInfo spell)
        {
            var aura = new Dictionary<string, object>();
            AddIfNonZero(aura, "cumulativeAura", spell.CumulativeAura);
            AddIfNonZero(aura, "duration", spell.Duration);
            AddIfNonZero(aura, "durationPerLevel", spell.DurationPerLevel);
            AddIfNonZero(aura, "maxDuration", spell.MaxDuration);
            return aura;
        }

        private static Dictionary<string, object> BuildProcs(SpellInfo spell)
        {
            var procs = new Dictionary<string, object>();

            AddFlagsWithUnmatched(procs, "procFlags", typeof(ProcFlags), spell.ProcFlags);
            AddFlagsWithUnmatched(procs, "procFlagsEx", typeof(ProcFlagsEx), spell.ProcFlagsEx);

            AddIfNonZero(procs, "procChance", spell.ProcChance);
            AddIfNonZero(procs, "procCharges", spell.ProcCharges);
            AddIfNonZero(procs, "procCooldown", spell.ProcCooldown);
            AddIfNonZero(procs, "baseProcRate", spell.BaseProcRate);
            AddIfNonZero(procs, "procsPerMinuteFlags", spell.ProcsPerMinuteFlags);

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

        private static Dictionary<string, object> BuildScaling(SpellScalingEntry scaling)
        {
            var result = new Dictionary<string, object>();
            AddIfNonZero(result, "class", scaling.Class);
            AddIfNonZero(result, "minScalingLevel", scaling.MinScalingLevel);
            AddIfNonZero(result, "maxScalingLevel", scaling.MaxScalingLevel);
            AddIfNonZero(result, "scalesFromItemLevel", scaling.ScalesFromItemLevel);
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
            if (spell.Levels == null)
                return result;

            AddIfNonZero(result, "baseLevel", spell.Levels.BaseLevel);
            AddIfNonZero(result, "spellLevel", spell.Levels.SpellLevel);
            AddIfNonZero(result, "maxLevel", spell.Levels.MaxLevel);
            AddIfNonZero(result, "maxPassiveAuraLevel", spell.Levels.MaxPassiveAuraLevel);
            return result;
        }

        private static Dictionary<string, object> BuildInterrupts(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            if (spell.Interrupts == null)
                return result;

            if (spell.InterruptFlags != 0)
                result["interruptFlags"] = BuildRawAndFlags((uint)spell.InterruptFlags, typeof(SpellInterruptFlags));
            if (spell.AuraInterruptFlags != 0)
                result["auraInterruptFlags"] = BuildRawAndFlags((uint)spell.AuraInterruptFlags, typeof(SpellAuraInterruptFlags));

            // Channel interrupt flags decode through their own enum, not
            // SpellAuraInterruptFlags -- SpellInfo.cs's text path (AppendMisc)
            // casts Interrupts.ChannelInterruptFlags to SpellChannelInterruptFlags;
            // reusing SpellAuraInterruptFlags here would decode the same bits
            // against the wrong bit-to-name table.
            if (spell.ChannelInterruptFlags != 0)
                result["channelInterruptFlags"] = BuildRawAndFlags((uint)spell.ChannelInterruptFlags, typeof(SpellChannelInterruptFlags));
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

            AddIfNonZero(result, "equippedItemSubClassMask", spell.EquippedItems.EquippedItemSubclass);
            AddIfNonZero(result, "equippedItemInventoryTypeMask", spell.EquippedItems.EquippedItemInvTypes);
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

        // Mirrors SpellInfo.AppendItemInfo's join exactly (ItemEffect.SpellID
        // -> ItemEffect.ParentItemID -> ItemSparse), including which field it
        // reads the display name from. A row whose ParentItemID has no
        // ItemSparse entry is skipped rather than emitting a placeholder --
        // AppendItemInfo prints "Non-existing Item-sparse.db2 entry N" for a
        // human reading a report; a JSON consumer gets the rows that resolve.
        private static List<object> BuildItems(SpellInfo spell)
        {
            var result = new List<object>();

            // Unlike SpellTriggerStore (a plain field, always initialised),
            // ItemEffect/ItemSparse are Storage<T> properties that stay null
            // until DBC.LoadAsync runs. Serialize_AcceptsNewSections_
            // AndStillRejectsUnknown calls this section on a MinimalSpell
            // with no load performed at all -- guard rather than let a
            // caller-mistake test (section validation needs no data) crash
            // on an unrelated null reference.
            if (DBC.DBC.ItemEffect == null || DBC.DBC.ItemSparse == null)
                return result;

            foreach (var itemEffect in DBC.DBC.ItemEffect.Values.Where(e => e.SpellID == spell.ID))
            {
                if (!DBC.DBC.ItemSparse.TryGetValue(itemEffect.ParentItemID, out var item))
                    continue;

                result.Add(new Dictionary<string, object>
                {
                    ["id"] = itemEffect.ParentItemID,
                    ["name"] = item.Display,
                });
            }

            return result;
        }

        /// <summary>
        /// The resolved visual chain, projected through
        /// <see cref="SpellVisualChainJson"/> -- the same per-node shape the
        /// <c>get_spell_visual_chain</c> MCP tool emits, so the two views of
        /// this graph cannot drift.
        /// <para>
        /// <b>Filtered to <paramref name="context"/>'s difficulty, exactly as
        /// the text render is.</b> The rest of this document is already a
        /// rendering *at a context* -- base points are computed against it and
        /// the <c>context</c> section echoes it back -- so a visual section that
        /// ignored it would make <c>get_spell(id, format:"json", difficulty:N)</c>
        /// and <c>get_spell(id, format:"text", difficulty:N)</c> report
        /// different visual sets for identical arguments. The two id spaces
        /// involved do differ, but they are not unbridgeable: the
        /// <c>difficulty</c> argument names a <c>MapDifficulty</c> row, and
        /// <see cref="RenderContext.MapDifficulty"/> has already resolved it, so
        /// <c>MapDifficultyEntry.DifficultyID</c> is the value
        /// <c>SpellXSpellVisual.DifficultyID</c> is expressed in. That
        /// translation is the same one <c>SpellInfo.AppendSpellVisualInfo</c>
        /// has always performed. To ask for every difficulty at once, call
        /// <c>get_spell_visual_chain(difficulty: -1)</c>.
        /// </para>
        /// <para>
        /// <b>Filtering to nothing must never read as "this spell has no
        /// visuals".</b> When the filter empties the list but the spell does
        /// have variants elsewhere, <c>variantsAtOtherDifficulties</c> names
        /// those difficulties, so an empty <c>variants</c> plus no such key
        /// means exactly one thing: this spell has no <c>SpellXSpellVisual</c>
        /// rows at all. That is the same failure mode <c>storeUnloaded</c>
        /// exists to prevent, reached by a different route.
        /// </para>
        /// </summary>
        private static Dictionary<string, object> BuildVisual(SpellInfo spell, RenderContext context)
        {
            // Reported, never inferred. Storage<T> properties stay null until
            // DBC.LoadAsync runs -- and it swallows FileNotFoundException per
            // store, so a partial extraction is a supported state, not a
            // theoretical one. SpellVisualChainResolver.Resolve degrades to an
            // empty chain rather than throwing, which is right for the resolver
            // and wrong to pass on verbatim here: "no variants" would then mean
            // either "this spell has no visual" or "some store never loaded".
            //
            // Neither the store list nor the difficulty fallback is decided
            // here: ResolveSection decides both, once, for all three hosts.
            // This section and VisualTools once null-checked three stores each
            // -- different threes -- and disagreed about states DBC.LoadAsync
            // can actually produce; the text render did not check at all.
            // Which store is missing is server_info's question to answer, so
            // this reports the condition, not the inventory.
            return DescribeChain(SpellVisualChainResolver.ResolveSection(
                spell.ID, context?.MapDifficulty?.DifficultyID ?? 0));
        }

        /// <summary>
        /// The <c>visual</c> section for a resolved section: the
        /// store-unavailable branch, then the chain itself. The text render
        /// branches on the same <see cref="SpellVisualSection.Unavailable"/>
        /// (<c>SpellInfo.WriteVisualChain</c>), which is what stops the two
        /// formats from disagreeing about a state only one of them noticed.
        /// </summary>
        internal static Dictionary<string, object> DescribeChain(SpellVisualSection section)
        {
            if (section.Unavailable)
                return new Dictionary<string, object> { ["storeUnloaded"] = true };

            return DescribeChain(section.Chain, section.Difficulty, section.DifficultiesWithVariants);
        }

        /// <summary>
        /// The <c>visual</c> section's envelope around an already-resolved
        /// chain. Split out from <see cref="BuildVisual(SpellInfo, RenderContext)"/>
        /// so the shapes that this build's client data cannot produce -- a
        /// truncated chain, an unresolved reference -- are reachable from a test
        /// with a synthetic chain instead of shipping unexercised.
        /// </summary>
        /// <param name="difficultiesWithVariants">
        /// Null when <paramref name="chain"/> has variants. Otherwise every
        /// difficulty the spell does have variants at, so an empty list can be
        /// told apart from a filtered-away one.
        /// </param>
        internal static Dictionary<string, object> DescribeChain(
            SpellVisualChain chain, int difficulty, IReadOnlyList<int> difficultiesWithVariants)
        {
            var visual = new Dictionary<string, object>
            {
                ["difficulty"] = difficulty,
                ["variants"] = chain.Variants.Select(SpellVisualChainJson.Describe).ToList(),
                ["truncated"] = chain.Truncated,
            };

            if (difficultiesWithVariants != null && difficultiesWithVariants.Count > 0)
                visual["variantsAtOtherDifficulties"] = difficultiesWithVariants.Cast<object>().ToList();

            AddIfNotEmpty(visual, "unresolvedReferences", chain.UnresolvedReferences.Cast<object>().ToList());

            return visual;
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
