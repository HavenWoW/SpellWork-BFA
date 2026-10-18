using DBCD;
using DBCD.Providers;
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
        public const string Version = "SpellWork 5.5.4 (70449)";
        public const uint MaxLevel = 90;
        public const uint MaxItemLevel = 1300;

        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable CollectionNeverUpdated.Global
        public static Storage<SpellNameEntry>         SpellName { get; set; }
        public static Storage<SpellEntry>             Spell { get; set; }
        public static Storage<SpellMiscEntry>         SpellMisc { get; set; }
        public static Storage<SpellEffectEntry>       SpellEffect { get; set; }
        public static Storage<SpellClassOptionsEntry> SpellClassOptions { get; set; }
        public static Storage<SpellDurationEntry>     SpellDuration { get; set; }
        public static Storage<SpellRangeEntry>        SpellRange { get; set; }
        public static Storage<DifficultyEntry>        Difficulty { get; set; }
        // ReSharper restore MemberCanBePrivate.Global
        // ReSharper restore CollectionNeverUpdated.Global

        public static readonly IDictionary<int, SpellInfo> SpellInfoStore = new ConcurrentDictionary<int, SpellInfo>();
        public static readonly IDictionary<int, ISet<int>> SpellTriggerStore = new Dictionary<int, ISet<int>>();

        /// <summary>
        /// Every modelled <see cref="Storage{T}"/> property that holds no data,
        /// by name, sorted. After <see cref="LoadAsync"/> has run this is the
        /// list of stores whose .db2 (or .dbd definition) could not be opened.
        /// Derived from the properties themselves (a store is null exactly when
        /// it did not load), so it needs no bookkeeping during the (sequential)
        /// load loop, no reset between loads, and is readable even if LoadAsync
        /// throws. Before any load it reports every store.
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
        /// class was written against, sorted by table name. Empty on a matching
        /// client build. Recorded rather than thrown: a mismatch is a
        /// diagnosis, and throwing would abort the whole load over a table the
        /// caller may not be asking about.
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
        /// Keys of the Difficulty store plus 0 (the base difficulty), deduped
        /// and sorted. Just { 0 } when Difficulty did not load.
        /// </summary>
        public static IReadOnlyCollection<int> ValidDifficultyIds { get; private set; } = new[] { 0 };

        /// <summary>True when the locale subdirectory was absent and DbcPath itself was used.</summary>
        public static bool LocaleFallback { get; private set; }

        /// <summary>
        /// The one reflection scan over this class's <see cref="Storage{T}"/>
        /// properties, shared by LoadAsync and UnloadedStores so the set that
        /// gets loaded and the set that gets reported can never differ.
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

        // The 8 stores are their own file basenames (SpellName -> SpellName.db2).
        private static string FileNameFor(PropertyInfo property) => property.Name;

        public static async Task LoadAsync(SpellWorkConfig config, ISpellWorkLogger logger)
        {
            // Cleared so a second load in one process does not double-report.
            while (_layoutMismatches.TryTake(out _)) { }

            var resolver = new ClientFileResolver(config.DbcPath, config.Locale);
            LocaleFallback = resolver.LocaleFallback;

            var dbcd = new DBCD.DBCD(
                new FilesystemDBCProvider(resolver.EffectiveDbcDirectory),
                new FilesystemDBDProvider(config.DefsPath));

            var mapMethod = typeof(DbcdStorageMapper).GetMethod(nameof(DbcdStorageMapper.Map));

            // Sequential on purpose: DBCD caches .dbd definitions in a non-thread-safe
            // Dictionary, so concurrent Load calls intermittently throw.
            foreach (var property in StorageProperties())
            {
                var name = FileNameFor(property);
                try
                {
                    IDBCDStorage raw = dbcd.Load(name, config.Build);

                    var mismatch = LayoutVerification.Check(name, raw.AvailableColumns);
                    if (mismatch != null)
                        _layoutMismatches.Add(mismatch);

                    var entryType = property.PropertyType.GetGenericArguments()[0];
                    property.SetValue(null, mapMethod.MakeGenericMethod(entryType).Invoke(null, new object[] { raw }));
                }
                // The .db2 or its .dbd definition is absent, or the directory
                // itself is missing: leave this one store null. UnloadedStores
                // reports it and each host decides its own policy.
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    throw new InvalidOperationException(
                        $"Failed to map {name}: {tie.InnerException.Message}", tie.InnerException);
                }
            }

            // Bring-up: only base rows (DifficultyID == 0) are loaded and no
            // difficulty overrides are applied, so 0 is the only valid id even
            // though the Difficulty store itself holds every row.
            ValidDifficultyIds = new[] { 0 };

            // A missing locale directory (or SpellName) leaves SpellName null;
            // skip post-processing so callers see an empty SpellInfoStore and
            // can report "no data loaded" rather than a NullReferenceException.
            if (SpellName != null)
                await BuildSpellInfoStore(logger);
        }

        private static Task BuildSpellInfoStore(ISpellWorkLogger logger)
        {
            foreach (var spell in SpellName)
            {
                SpellEntry entry = null;
                Spell?.TryGetValue(spell.Value.ID, out entry);
                SpellInfoStore[spell.Value.ID] = new SpellInfo(spell.Value, entry);
            }

            return Task.WhenAll(Task.Run(() =>
            {
                if (SpellMisc == null)
                    return;

                foreach (var spellMisc in SpellMisc.Values.Where(misc => SpellInfoStore.ContainsKey(misc.SpellID)))
                {
                    if (spellMisc.DifficultyID != 0)
                        continue;

                    var spell = SpellInfoStore[spellMisc.SpellID];

                    spell.Misc = spellMisc;

                    if (SpellDuration != null && SpellDuration.TryGetValue(spellMisc.DurationIndex, out var duration))
                        spell.DurationEntry = duration;

                    if (SpellRange != null && SpellRange.TryGetValue(spellMisc.RangeIndex, out var range))
                        spell.Range = range;
                }
            }), Task.Run(() =>
            {
                if (SpellEffect == null)
                    return;

                foreach (var effect in SpellEffect.Values)
                {
                    if (effect.DifficultyID != 0)
                        continue;

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
                if (SpellClassOptions == null)
                    return;

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
            }));
        }

        public static uint SelectedLevel = MaxLevel;
        public static uint SelectedItemLevel = 475;
    }
}
