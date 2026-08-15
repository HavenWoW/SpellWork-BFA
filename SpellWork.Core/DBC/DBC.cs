using DBFileReaderLib;
using SpellWork.DBC.Structures;
using SpellWork.Extensions;
using SpellWork.GameTables;
using SpellWork.GameTables.Structures;
using SpellWork.Spell;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SpellWork.DBC
{
    public static class DBC
    {
        public const string Version = "SpellWork 8.3.0 (34220)";
        public const uint MaxLevel = 120;
        public const uint MaxItemLevel = 1300;

        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable CollectionNeverUpdated.Global
        public static Storage<AreaGroupMemberEntry>             AreaGroupMember { get; set; }
        public static Storage<AreaTableEntry>                   AreaTable { get; set; }
        public static Storage<ContentTuningEntry>               ContentTuning { get; set; }
        public static Storage<ContentTuningXExpectedEntry>      ContentTuningXExpected { get; set; }
        public static Storage<DifficultyEntry>                  Difficulty { get; set; }
        public static Storage<ExpectedStatEntry>                ExpectedStat { get; set; }
        public static Storage<ExpectedStatModEntry>             ExpectedStatMod { get; set; }
        public static Storage<MapEntry>                         Map { get; set; }
        public static Storage<MapDifficultyEntry>               MapDifficulty { get; set; }
        public static Storage<OverrideSpellDataEntry>           OverrideSpellData { get; set; }
        public static Storage<ScreenEffectEntry>                ScreenEffect { get; set; }
        public static Storage<SpellEntry>                       Spell { get; set; }
        public static Storage<SpellNameEntry>                   SpellName { get; set; }
        public static Storage<SpellAuraOptionsEntry>            SpellAuraOptions { get; set; }
        public static Storage<SpellAuraRestrictionsEntry>       SpellAuraRestrictions { get; set; }
        public static Storage<SpellCastingRequirementsEntry>    SpellCastingRequirements { get; set; }
        public static Storage<SpellCastTimesEntry>              SpellCastTimes { get; set; }
        public static Storage<SpellCategoriesEntry>             SpellCategories { get; set; }
        public static Storage<SpellClassOptionsEntry>           SpellClassOptions { get; set; }
        public static Storage<SpellCooldownsEntry>              SpellCooldowns { get; set; }
        public static Storage<SpellDescriptionVariablesEntry>   SpellDescriptionVariables { get; set; }
        public static Storage<SpellDurationEntry>               SpellDuration { get; set; }
        public static Storage<SpellEffectEntry>                 SpellEffect { get; set; }
        public static Storage<SpellMiscEntry>                   SpellMisc { get; set; }
        public static Storage<SpellEquippedItemsEntry>          SpellEquippedItems { get; set; }
        public static Storage<SpellInterruptsEntry>             SpellInterrupts { get; set; }
        public static Storage<SpellLevelsEntry>                 SpellLevels { get; set; }
        public static Storage<SpellPowerEntry>                  SpellPower { get; set; }
        public static Storage<SpellRadiusEntry>                 SpellRadius { get; set; }
        public static Storage<SpellRangeEntry>                  SpellRange { get; set; }
        public static Storage<SpellScalingEntry>                SpellScaling { get; set; }
        public static Storage<SpellShapeshiftEntry>             SpellShapeshift { get; set; }
        public static Storage<SpellTargetRestrictionsEntry>     SpellTargetRestrictions { get; set; }
        public static Storage<SpellTotemsEntry>                 SpellTotems { get; set; }
        public static Storage<SpellXDescriptionVariablesEntry>  SpellXDescriptionVariables { get; set; }
        public static Storage<SpellXSpellVisualEntry>           SpellXSpellVisual { get; set; }
        public static Storage<RandPropPointsEntry>              RandPropPoints { get; set; }
        public static Storage<SpellProcsPerMinuteEntry>         SpellProcsPerMinute { get; set; }

        public static Storage<SkillLineAbilityEntry>            SkillLineAbility { get; set; }
        public static Storage<SkillLineEntry>                   SkillLine { get; set; }

        public static Storage<ItemEntry>                        Item { get; set; }
        public static Storage<ItemEffectEntry>                  ItemEffect { get; set; }
        public static Storage<ItemSparseEntry>                  ItemSparse { get; set; }

        public static Storage<SpellReagentsEntry>               SpellReagents { get; set; }
        public static Storage<SpellReagentsCurrencyEntry>       SpellReagentsCurrency { get; set; }
        public static Storage<SpellMissileEntry>                SpellMissile { get; set; }
        public static Storage<SpellVisualEntry>                 SpellVisual { get; set; }
        public static Storage<SpellVisualEventEntry>            SpellVisualEvent { get; set; }
        public static Storage<SpellVisualMissileEntry>          SpellVisualMissile { get; set; }
        public static Storage<SpellVisualKitEntry>              SpellVisualKit { get; set; }
        public static Storage<SpellVisualKitEffectEntry>        SpellVisualKitEffect { get; set; }
        public static Storage<SpellVisualEffectNameEntry>       SpellVisualEffectName { get; set; }
        public static Storage<SpellVisualKitModelAttachEntry>   SpellVisualKitModelAttach { get; set; }
        public static Storage<SpellChainEffectsEntry>           SpellChainEffects { get; set; }
        public static Storage<SpellMissileMotionEntry>          SpellMissileMotion { get; set; }
        // ReSharper restore MemberCanBePrivate.Global
        // ReSharper restore CollectionNeverUpdated.Global

        public static readonly IDictionary<int, SpellInfo> SpellInfoStore = new ConcurrentDictionary<int, SpellInfo>();
        public static readonly IDictionary<int, ISet<int>> SpellTriggerStore = new Dictionary<int, ISet<int>>();

        /// <summary>
        /// Every modelled <see cref="Storage{T}"/> property that holds no data,
        /// by name, sorted. After <see cref="LoadAsync"/> has run this is the
        /// list of stores whose <c>.db2</c> file could not be opened -- the
        /// per-store failures the catches below swallow so the rest of the load
        /// can finish.
        /// <para>
        /// Derived from the properties themselves rather than recorded by those
        /// catches on the way past. A store is null exactly when it did not
        /// load, so reading the state directly cannot disagree with the state,
        /// which a parallel tally kept alongside it eventually would. It also
        /// needs no synchronisation inside the parallel loop, no reset between
        /// loads, and -- the reason it matters here -- it is readable even when
        /// <see cref="LoadAsync"/> throws before returning, which is precisely
        /// the case (a missing store that <c>BuildSpellInfoStore</c> reads) a
        /// caller most needs it for.
        /// </para>
        /// <para>
        /// Before any load this reports every store, since they all start null.
        /// It answers "what is missing now", not "what did the last load fail
        /// to open"; call it after a load.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> UnloadedStores
        {
            get
            {
                var unloaded = new List<string>();
                foreach (var property in StorageProperties())
                {
                    if (property.GetValue(null) == null)
                        unloaded.Add(property.Name);
                }

                unloaded.Sort(StringComparer.Ordinal);
                return unloaded;
            }
        }

        private static readonly ConcurrentBag<LayoutMismatch> _layoutMismatches =
            new ConcurrentBag<LayoutMismatch>();

        /// <summary>
        /// Tables whose on-disk layout differs from the one their structure
        /// class was written against, worst case first is not meaningful here
        /// so they are sorted by table name. Empty on a matching client build.
        /// <para>
        /// Recorded rather than thrown. A mismatch means this client build
        /// differs from the reference one, which is a diagnosis; throwing
        /// would abort the whole load over a table the caller may not be
        /// asking about, which is the mistake the FileNotFoundException catch
        /// below already exists to avoid repeating.
        /// </para>
        /// </summary>
        public static IReadOnlyList<LayoutMismatch> LayoutMismatches
        {
            get
            {
                var mismatches = new List<LayoutMismatch>(_layoutMismatches);
                mismatches.Sort((a, b) => string.CompareOrdinal(a.Table, b.Table));
                return mismatches;
            }
        }

        /// <summary>
        /// The one reflection scan over this class's <see cref="Storage{T}"/>
        /// properties. Four separate copies of it already exist across the two
        /// hosts (SpellDataService.CountStores, Db2Tools.ModelledStores, and
        /// the test fixtures), each carrying a comment begging the next author
        /// to keep the BindingFlags identical because a prior review found two
        /// that had silently drifted apart. LoadAsync and UnloadedStores at
        /// least share this one, so the set of stores that gets loaded and the
        /// set that gets reported can never be different sets.
        /// </summary>
        private static IEnumerable<PropertyInfo> StorageProperties()
        {
            foreach (var property in typeof(DBC).GetProperties(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition() == typeof(Storage<>))
                {
                    yield return property;
                }
            }
        }

        public static async Task LoadAsync(SpellWorkConfig config, ISpellWorkLogger logger)
        {
            // No other per-load state in this method is reset on re-entry
            // (see ClientDataFixture's own single-load guard for why that has
            // never mattered in practice); this bag is cleared explicitly so
            // a second load in one process does not double-report mismatches
            // the first load already recorded.
            while (_layoutMismatches.TryTake(out _)) { }

            HotfixReader hotfixReader = null;
            try
            {
                hotfixReader = new HotfixReader(config.HotfixCachePath);
            }
            catch (Exception)
            {
                logger.Warn(
                    $"Hotfix cache {config.HotfixCachePath} cannot be loaded, ignoring!");
            }

            Parallel.ForEach(StorageProperties(), dbc =>
               {
                   var name = dbc.Name;

                   try
                   {
                       var db2Reader = new DBReader($@"{config.DbcPath}\{config.Locale}\{name}.db2");

                       // Check the layout before building the Storage<T>, not
                       // after: a mismatched layout still constructs happily
                       // and the point is to know before anyone reads a field.
                       var mismatch = LayoutVerification.Check(
                           name, db2Reader.LayoutHash, db2Reader.FieldsCount);
                       if (mismatch != null)
                           _layoutMismatches.Add(mismatch);

                       dynamic storage = Activator.CreateInstance(dbc.PropertyType, db2Reader);

                       if (hotfixReader != null)
                           hotfixReader.ApplyHotfixes(storage, db2Reader);

                       dbc.SetValue(dbc.GetValue(null), storage);
                   }
                   // The locale directory is missing entirely (wrong path,
                   // wrong locale, DbcPath one level too high). Every store
                   // fails the same way, so the guard below sees SpellName ==
                   // null and the caller gets an actionable "no data loaded"
                   // rather than a NullReferenceException.
                   catch (DirectoryNotFoundException)
                   {
                   }
                   // One .db2 is absent while the locale directory itself
                   // exists -- a partial extraction, a store a fork does not
                   // ship, a file deleted by hand. DBReader throws
                   // FileNotFoundException for that, which used to escape the
                   // Parallel.ForEach, propagate out of LoadAsync and abort
                   // the entire load: forty-five stores that opened perfectly
                   // well were discarded because a forty-sixth was missing,
                   // and the host could answer nothing at all from client
                   // data. Treated exactly as the DirectoryNotFoundException
                   // above: leave this one property null and let the rest
                   // load, which is what makes SpellWork.Mcp's per-store
                   // "loaded" flag (list_db2), its zero entry in server_info's
                   // storeCounts, and its store_not_loaded error reachable at
                   // all -- until now they were correct but dead code behind
                   // an invariant that was accidental rather than guaranteed.
                   //
                   // Swallowing here makes the two hosts differ, deliberately,
                   // and each one decides its own policy from UnloadedStores
                   // above rather than having it decided for them here:
                   //   - SpellWork.Mcp tolerates missing stores and reports
                   //     them per store, through list_db2's "loaded" flag and
                   //     server_info's storeCounts. A headless server that
                   //     answers nothing at all because one file of forty-six
                   //     is absent is worse than one that answers forty-five
                   //     stores' worth and says which one it cannot.
                   //   - SpellWork (WinForms) refuses to start and names the
                   //     files, in Program.cs. For a GUI the opposite is true:
                   //     an operator handed a silently incomplete spell list
                   //     has no way to tell the data is wrong, and before this
                   //     catch existed the load threw and the app did not open.
                   //     That policy is preserved there, not here.
                   catch (FileNotFoundException)
                   {
                   }
                   catch (TargetInvocationException tie)
                   {
                       if (tie.InnerException is ArgumentException)
                           throw new ArgumentException($"Failed to load {name}.db2: {tie.InnerException.Message}");
                       throw;
                   }
               });

            // A locale directory that does not exist under DbcPath (wrong
            // path, wrong SPELLWORK_LOCALE, or DbcPath pointed one level too
            // high) fails every single per-store DBReader open above with
            // DirectoryNotFoundException, which the Parallel.ForEach body
            // swallows silently -- so SpellName (and every other Storage<T>
            // property, since they all resolve from the same missing
            // directory) is left null rather than populated-but-empty.
            // Without this guard, the unconditional "foreach (var spell in
            // SpellName)" that used to sit here threw NullReferenceException,
            // which reached callers as a generic CLR message instead of
            // letting a SpellCount == 0 check downstream (see
            // SpellWork.Mcp.SpellDataService) fire with an actionable one.
            // Skipping post-processing here leaves SpellInfoStore empty,
            // exactly the state the loaded path already treats as "nothing
            // to report" for any row it can't resolve via ContainsKey below.
            //
            // GameTable<GtSpellScalingEntry>.Open moved inside this guard
            // too (it used to run unconditionally): nothing reads it unless
            // a SpellInfo exists to attach it to (see SpellInfo.cs), and
            // every SpellInfo comes from the SpellName loop above, so it is
            // never reachable when SpellName is null anyway. Skipping it in
            // that case also means the "no data" path never touches
            // GameTable<T>'s static _records dictionary, which only
            // supports Add and would throw a duplicate-key exception if two
            // loads in the same process both tried to populate it.
            if (SpellName != null)
            {
                await BuildSpellInfoStore(logger);
                GameTable<GtSpellScalingEntry>.Open($@"{config.GtPath}\SpellScaling.txt");
            }
        }

        // Split out of LoadAsync so the null-store guard above can skip this
        // whole block with a single check rather than threading a null check
        // through every individual loop below. Body is unchanged from before
        // the split -- same statements, same order, same indentation depth
        // (still a direct member of the DBC class) -- so the loaded path's
        // behaviour is identical to before this method existed.
        private static async Task BuildSpellInfoStore(ISpellWorkLogger logger)
        {
            foreach (var spell in SpellName)
                SpellInfoStore[(int)spell.Value.ID] = new SpellInfo(spell.Value, Spell.GetValue((int)spell.Value.ID));

            await Task.WhenAll(Task.Run(() =>
            {
                foreach (var spellMisc in SpellMisc.Values.Where(misc => SpellInfoStore.ContainsKey(misc.SpellID)))
                {
                    if (spellMisc.DifficultyID != 0)
                        continue;

                    var spell = SpellInfoStore[spellMisc.SpellID];

                    spell.Misc = spellMisc;

                    if (SpellDuration.ContainsKey(spellMisc.DurationIndex))
                        spell.DurationEntry = SpellDuration[spellMisc.DurationIndex];

                    if (SpellRange.ContainsKey(spellMisc.RangeIndex))
                        spell.Range = SpellRange[spellMisc.RangeIndex];
                }
            }), Task.Run(() =>
            {
                foreach (var effect in SpellEffect.Values)
                {
                    if (!SpellInfoStore.ContainsKey(effect.SpellID))
                    {
                        logger.Warn(
                            $"Spell effect {effect.ID} is referencing unknown spell {effect.SpellID}, ignoring!");
                        continue;
                    }

                    SpellInfoStore[effect.SpellID].Effects.Add(effect);
                    SpellInfoStore[effect.SpellID].SpellEffectInfoStore[effect.EffectIndex] = new SpellEffectInfo(effect); // Helper

                    var triggerId = effect.EffectTriggerSpell;
                    if (triggerId != 0)
                    {
                        if (SpellTriggerStore.ContainsKey(triggerId))
                            SpellTriggerStore[triggerId].Add(effect.SpellID);
                        else
                            SpellTriggerStore.Add(triggerId, new SortedSet<int> { effect.SpellID });
                    }
                }
            }), Task.Run(() =>
            {
                foreach (var spellTargetRestrictions in SpellTargetRestrictions.Values)
                {
                    if (spellTargetRestrictions.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(spellTargetRestrictions.SpellID))
                    {
                        logger.Warn(
                            $"SpellTargetRestrictions: Unknown spell {spellTargetRestrictions.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellTargetRestrictions.SpellID].TargetRestrictions.Add(spellTargetRestrictions);
                }
            }), Task.Run(() =>
            {
                foreach (var spellXSpellVisual in SpellXSpellVisual.Values.Where(effect => effect.CasterPlayerConditionID == 0))
                {
                    if (spellXSpellVisual.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(spellXSpellVisual.SpellID))
                    {
                        logger.Warn(
                            $"SpellXSpellVisual: Unknown spell {spellXSpellVisual.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellXSpellVisual.SpellID].SpellXSpellVisual = spellXSpellVisual;
                }
            }), Task.Run(() =>
            {
                foreach (var spellScaling in SpellScaling.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellScaling.SpellID))
                    {
                        logger.Warn(
                            $"SpellScaling: Unknown spell {spellScaling.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellScaling.SpellID].Scaling = spellScaling;
                }
            }), Task.Run(() =>
            {
                foreach (var spellAuraOptions in SpellAuraOptions.Values)
                {
                    if (spellAuraOptions.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(spellAuraOptions.SpellID))
                    {
                        logger.Warn(
                            $"SpellAuraOptions: Unknown spell {spellAuraOptions.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellAuraOptions.SpellID].AuraOptions = spellAuraOptions;
                    if (spellAuraOptions.SpellProcsPerMinuteID != 0)
                        SpellInfoStore[spellAuraOptions.SpellID].ProcsPerMinute = SpellProcsPerMinute[spellAuraOptions.SpellProcsPerMinuteID];
                }
            }), Task.Run(() =>
            {
                foreach (var spellAuraRestrictions in SpellAuraRestrictions.Values)
                {
                    if (spellAuraRestrictions.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(spellAuraRestrictions.SpellID))
                    {
                        logger.Warn(
                            $"SpellAuraRestrictions: Unknown spell {spellAuraRestrictions.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellAuraRestrictions.SpellID].AuraRestrictions = spellAuraRestrictions;
                }
            }), Task.Run(() =>
            {
                foreach (var spellCategories in SpellCategories.Values)
                {
                    if (spellCategories.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(spellCategories.SpellID))
                    {
                        logger.Warn(
                            $"SpellCategories: Unknown spell {spellCategories.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellCategories.SpellID].Categories = spellCategories;
                }
            }), Task.Run(() =>
            {
                foreach (var spellCastingRequirements in SpellCastingRequirements.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellCastingRequirements.SpellID))
                    {
                        logger.Warn(
                            $"SpellCastingRequirements: Unknown spell {spellCastingRequirements.SpellID} referenced, ignoring!");
                        return;
                    }

                    SpellInfoStore[spellCastingRequirements.SpellID].CastingRequirements = spellCastingRequirements;
                }
            }), Task.Run(() =>
            {
                foreach (var spellClassOptions in SpellClassOptions.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellClassOptions.SpellID))
                    {
                        logger.Warn(
                            $"SpellClassOptions: Unknown spell {spellClassOptions.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellClassOptions.SpellID].ClassOptions = spellClassOptions;
                }
            }), Task.Run(() =>
            {
                foreach (var spellCooldowns in SpellCooldowns.Values)
                {
                    if (spellCooldowns.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(spellCooldowns.SpellID))
                    {
                        logger.Warn(
                            $"SpellCooldowns: Unknown spell {spellCooldowns.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellCooldowns.SpellID].Cooldowns = spellCooldowns;
                }
            }), Task.Run(() =>
            {
                foreach (var effect in SpellInterrupts)
                {
                    if (effect.Value.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(effect.Value.SpellID))
                    {
                        logger.Warn(
                            $"SpellInterrupts: Unknown spell {effect.Value.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[effect.Value.SpellID].Interrupts = effect.Value;
                }
            }), Task.Run(() =>
            {
                foreach (var spellEquippedItems in SpellEquippedItems.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellEquippedItems.SpellID))
                    {
                        logger.Warn(
                            $"SpellEquippedItems: Unknown spell {spellEquippedItems.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellEquippedItems.SpellID].EquippedItems = spellEquippedItems;
                }
            }), Task.Run(() =>
            {
                foreach (var spellLevels in SpellLevels.Values)
                {
                    if (spellLevels.DifficultyID != 0)
                        continue;

                    if (!SpellInfoStore.ContainsKey(spellLevels.SpellID))
                    {
                        logger.Warn($"SpellLevels: Unknown spell {spellLevels.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellLevels.SpellID].Levels = spellLevels;
                }
            }), Task.Run(() =>
            {
                foreach (var spellPower in SpellPower.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellPower.SpellID))
                    {
                        logger.Warn(
                            $"SpellPower: Unknown spell {spellPower.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellPower.SpellID].Powers.Add(spellPower);
                }
            }), Task.Run(() =>
            {
                foreach (var spellReagents in SpellReagents.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellReagents.SpellID))
                    {
                        logger.Warn(
                            $"SpellReagents: Unknown spell {spellReagents.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellReagents.SpellID].Reagents = spellReagents;
                }
            }), Task.Run(() =>
            {
                foreach (var spellReagentsCurrency in SpellReagentsCurrency.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellReagentsCurrency.SpellID))
                    {
                        logger.Warn(
                            $"SpellReagentsCurrency: Unknown spell {spellReagentsCurrency.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellReagentsCurrency.SpellID].ReagentsCurrency.Add(spellReagentsCurrency);
                }
            }), Task.Run(() =>
            {
                foreach (var spellShapeshift in SpellShapeshift.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellShapeshift.SpellID))
                    {
                        logger.Warn(
                            $"SpellShapeshift: Unknown spell {spellShapeshift.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellShapeshift.SpellID].Shapeshift = spellShapeshift;
                }
            }), Task.Run(() =>
            {
                foreach (var spellTotems in SpellTotems.Values)
                {
                    if (!SpellInfoStore.ContainsKey(spellTotems.SpellID))
                    {
                        logger.Warn($"SpellTotems: Unknown spell {spellTotems.SpellID} referenced, ignoring!");
                        continue;
                    }

                    SpellInfoStore[spellTotems.SpellID].Totems = spellTotems;
                }
            }), Task.Run(() =>
            {
                foreach (var descriptionVariable in SpellXDescriptionVariables.Values)
                {
                    if (!SpellInfoStore.ContainsKey(descriptionVariable.SpellID))
                    {
                        logger.Warn($"SpellXDescriptionVariables: Unknown spell {descriptionVariable.SpellID} referenced, ignoring!");
                        continue;
                    }
                    SpellInfoStore[descriptionVariable.SpellID].DescriptionVariables = SpellDescriptionVariables.GetValue(descriptionVariable.SpellDescriptionVariablesID);
                }
            }));
        }

        public static uint SelectedLevel = MaxLevel;
        public static uint SelectedItemLevel = 475;
        public static MapDifficultyEntry SelectedMapDifficulty;
    }
}
