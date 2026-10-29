using DBFileReaderLib;
using SpellWork.DBC.Structures;
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
        public const string Version = "SpellWork CoA 3.3.5a (12340)";
        public const uint MaxLevel = 100;
        public const uint MaxItemLevel = 600;
        public const int MaxClasses = 33;
        public const int MaxEffectIndex = 3;
        public const int AttributeWords = 8;

        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable CollectionNeverUpdated.Global
        public static Storage<SpellEntry>                      Spell { get; set; }
        public static Storage<SpellCastTimesEntry>             SpellCastTimes { get; set; }
        public static Storage<SpellDurationEntry>              SpellDuration { get; set; }
        public static Storage<SpellRadiusEntry>                SpellRadius { get; set; }
        public static Storage<SpellRangeEntry>                 SpellRange { get; set; }
        public static Storage<SpellRuneCostEntry>              SpellRuneCost { get; set; }
        public static Storage<SpellDifficultyEntry>            SpellDifficulty { get; set; }
        public static Storage<SpellCategoryEntry>              SpellCategory { get; set; }
        public static Storage<SpellDescriptionVariablesEntry>  SpellDescriptionVariables { get; set; }
        public static Storage<SpellIconEntry>                  SpellIcon { get; set; }
        public static Storage<SpellAffectEntry>                SpellAffect { get; set; }
        public static Storage<SpellItemEnchantmentEntry>       SpellItemEnchantment { get; set; }
        public static Storage<SpellShapeshiftFormEntry>        SpellShapeshiftForm { get; set; }
        public static Storage<SpellFocusObjectEntry>           SpellFocusObject { get; set; }
        public static Storage<TotemCategoryEntry>              TotemCategory { get; set; }
        public static Storage<SkillLineEntry>                  SkillLine { get; set; }
        public static Storage<SkillLineAbilityEntry>           SkillLineAbility { get; set; }
        public static Storage<AreaGroupEntry>                  AreaGroup { get; set; }
        public static Storage<AreaTableEntry>                  AreaTable { get; set; }
        public static Storage<OverrideSpellDataEntry>          OverrideSpellData { get; set; }
        public static Storage<ScreenEffectEntry>               ScreenEffect { get; set; }
        public static Storage<SpellVisualEntry>                SpellVisual { get; set; }
        public static Storage<SpellVisualKitEntry>             SpellVisualKit { get; set; }
        public static Storage<SpellVisualEffectNameEntry>      SpellVisualEffectName { get; set; }
        public static Storage<SpellVisualKitModelAttachEntry>  SpellVisualKitModelAttach { get; set; }
        public static Storage<SpellVisualKitAreaModelEntry>    SpellVisualKitAreaModel { get; set; }
        public static Storage<SpellMissileEntry>               SpellMissile { get; set; }
        public static Storage<SpellMissileMotionEntry>         SpellMissileMotion { get; set; }
        public static Storage<SpellChainEffectsEntry>          SpellChainEffects { get; set; }
        public static Storage<ItemEntry>                       Item { get; set; }
        // ReSharper restore MemberCanBePrivate.Global
        // ReSharper restore CollectionNeverUpdated.Global

        public static readonly IDictionary<int, SpellInfo> SpellInfoStore = new ConcurrentDictionary<int, SpellInfo>();
        public static readonly IDictionary<int, ISet<int>> SpellTriggerStore = new Dictionary<int, ISet<int>>();

        /// <summary>
        /// The client file (without <c>.dbc</c>) a store property loads from.
        /// Every 3.3.5a store property is named after its file.
        /// </summary>
        public static string FileNameFor(PropertyInfo p) => p.Name;

        public static IEnumerable<string> StoreFileNames() => StorageProperties().Select(FileNameFor);
        public static IEnumerable<Type> StoreEntryTypes() => StorageProperties().Select(p => p.PropertyType.GetGenericArguments()[0]);

        private static readonly ConcurrentDictionary<string, ResolvedClientFile> _openedFiles = new ConcurrentDictionary<string, ResolvedClientFile>();

        /// <summary>
        /// The file each loaded store was read from, keyed by store property
        /// name. A store that did not load has no entry.
        /// </summary>
        public static IReadOnlyDictionary<string, ResolvedClientFile> OpenedFiles => _openedFiles;

        /// <summary>
        /// The localized-string slot (0-15) every localized column is read
        /// at, from <see cref="SpellWorkConfig.Locale"/> (spec D6). 0 = enUS.
        /// </summary>
        public static int LocaleSlot { get; private set; }

        /// <summary>
        /// True when the configured locale is not a known 3.3.5 locale and
        /// slot 0 (enUS) is used instead.
        /// </summary>
        public static bool LocaleSlotFallback { get; private set; }

        /// <summary>
        /// SpellDifficulty resolution (spec D4), built by <see cref="LoadAsync"/>;
        /// resolves every spell to itself until a load has run.
        /// </summary>
        public static SpellDifficultyResolver DifficultyResolver { get; private set; } =
            new SpellDifficultyResolver(null);

        /// <summary>
        /// Every modelled <see cref="Storage{T}"/> property that holds no data,
        /// by name, sorted. After <see cref="LoadAsync"/> has run this is the
        /// list of stores whose client file (<c>.dbc</c>) could
        /// not be found or opened -- the per-store failures the loader below
        /// tolerates so the rest of the load can finish.
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
        /// Tables whose on-disk layout (fields count, record size) differs from
        /// the CoA 3.3.5a (12340) layout their structure class was written against, sorted
        /// by table name. Empty on a matching client build.
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
            // never mattered in practice); these two are cleared explicitly so
            // a second load in one process does not double-report mismatches
            // or keep a file the second load no longer opens.
            while (_layoutMismatches.TryTake(out _)) { }
            _openedFiles.Clear();

            // Reload hygiene: stale stores from a previous load must not survive.
            SpellInfoStore.Clear();
            SpellTriggerStore.Clear();
            DifficultyResolver = new SpellDifficultyResolver(null);

            LocaleSlot = LocaleSlots.Resolve(config.Locale, out var slotFallback);
            LocaleSlotFallback = slotFallback;

            // A store this load does not open must not keep the previous load's
            // data: UnloadedStores reads the properties and OpenedFiles reads the
            // files this load opened, and after a reload they have to agree.
            foreach (var property in StorageProperties())
                property.SetValue(null, null);

            Parallel.ForEach(StorageProperties(), dbc =>
               {
                   var name = FileNameFor(dbc);

                   try
                   {
                       var file = ClientFileResolver.Resolve(FileNameFor(dbc), config);
                       if (file == null)
                           return; // store stays null -> reported by UnloadedStores, exactly like the old FileNotFoundException path
                       var reader = new DBReader(file.Path);

                       // Check the layout before building the Storage<T>, not
                       // after: a mismatched layout still constructs happily
                       // and the point is to know before anyone reads a field.
                       var mismatch = LayoutVerification.Check(FileNameFor(dbc), file.Path, reader.FieldsCount, reader.RecordSize);
                       object storage;
                       if (mismatch == null)
                           storage = Activator.CreateInstance(dbc.PropertyType, reader);
                       else
                       {
                           _layoutMismatches.Add(mismatch);
                           // A structure that disagrees with the file may not
                           // even decode (fields past the record end): that is
                           // a reported mismatch, never a failed load (Review
                           // Focus 5). The store stays null and is listed by
                           // UnloadedStores as well.
                           try
                           {
                               storage = Activator.CreateInstance(dbc.PropertyType, reader);
                           }
                           catch (Exception)
                           {
                               return;
                           }
                       }
                       dbc.SetValue(null, storage);
                       _openedFiles[dbc.Name] = file; // only once the store exists
                   }
                   // The resolver only returns paths that existed a moment
                   // ago, so these two now only fire when the directory or
                   // file disappears between the existence check and the open
                   // (a share dropping, a file deleted mid-load).
                   catch (DirectoryNotFoundException)
                   {
                   }
                   // One client file is unreadable while the rest are fine --
                   // a partial extraction, a store a fork does not ship, a
                   // file deleted by hand. That used to escape the
                   // Parallel.ForEach, propagate out of LoadAsync and abort
                   // the entire load: forty-five stores that opened perfectly
                   // well were discarded because a forty-sixth was missing,
                   // and the host could answer nothing at all from client
                   // data. Treated exactly as a file the resolver did not
                   // find: leave this one property null and let the rest
                   // load, which is what makes SpellWork.Mcp's per-store
                   // "loaded" flag (list_db2), its zero entry in server_info's
                   // storeCounts, and its store_not_loaded error reachable.
                   //
                   // SpellWork.Mcp tolerates missing stores and reports them
                   // per store, through list_db2's "loaded" flag and
                   // server_info's storeCounts. A headless server that answers
                   // nothing at all because one file of forty-six is absent is
                   // worse than one that answers forty-five stores' worth and
                   // says which one it cannot.
                   catch (FileNotFoundException)
                   {
                   }
                   catch (TargetInvocationException tie)
                   {
                       if (tie.InnerException is ArgumentException)
                           throw new ArgumentException($"Failed to load {name}: {tie.InnerException.Message}");
                       throw;
                   }
               });

            // A DbcPath that holds no client files at all (wrong path, or
            // pointed one level too high) leaves every Storage<T> property
            // null rather than populated-but-empty. Without this guard, the
            // unconditional "foreach (var spell in Spell.Values)" below threw
            // NullReferenceException, which reached callers as a generic CLR
            // message instead of letting a SpellCount == 0 check downstream
            // (see SpellWork.Mcp.SpellDataService) fire with an actionable one.
            // Skipping post-processing here leaves SpellInfoStore empty,
            // exactly the state the loaded path already treats as "nothing
            // to report" for any row it can't resolve.
            if (Spell != null)
            {
                await BuildSpellInfoStore(logger);
            }
        }

        // Split out of LoadAsync so the null-store guard above can skip this
        // whole block with a single check. 3.3.5 has one fat Spell row per
        // spell, so this is a single pass building the SpellInfo and the
        // reverse trigger index from EffectTriggerSpell[0..2] (non-zero
        // effect slots only), then the spell -> SpellDifficulty row index.
        private static Task BuildSpellInfoStore(ISpellWorkLogger logger)
        {
            foreach (var spell in Spell.Values)
            {
                SpellInfoStore[spell.ID] = new SpellInfo(spell);

                for (var i = 0; i < MaxEffectIndex; ++i)
                {
                    var trigger = spell.EffectTriggerSpell[i];
                    if (spell.Effect[i] == 0 || trigger == 0)
                        continue;
                    if (!SpellTriggerStore.TryGetValue(trigger, out var set))
                        SpellTriggerStore[trigger] = set = new SortedSet<int>();
                    set.Add(spell.ID);
                }
            }

            DifficultyResolver = new SpellDifficultyResolver(SpellDifficulty);
            return Task.CompletedTask;
        }

        /// <summary>Item level used when a caller gives none (the 5.4.8 goldens and the MCP default).</summary>
        public const uint DefaultItemLevel = 463;
    }
}
