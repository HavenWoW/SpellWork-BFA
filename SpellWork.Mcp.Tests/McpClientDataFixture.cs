using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // DBC.DBC is process-wide static state -- see
    // SpellWork.Core.Tests/ClientDataFixture.cs for the same hazard in that
    // suite. Two loads in this process both mutate the same static
    // Storage<T> properties on DBC.DBC and the derived SpellInfoStore.
    //
    // This suite needs two different outcomes exercised: a load that never
    // finds usable data (an empty DbcPath, proving SpellDataService's
    // no_data_loaded backstop is reachable -- see DBC.cs's null guard) and
    // a load that succeeds. Order matters between them, not just count: if
    // the successful load ran first, DBC.DBC's Storage<T> properties would
    // already be non-null real data by the time the no-data load runs,
    // so its per-property DirectoryNotFoundException catch would leave the
    // *previous* load's data in place instead of the nulls a first-ever
    // failed load actually produces -- silently turning the regression
    // scenario into a no-op rather than a race. Doing both loads once, in a
    // fixed order, inside a single locked constructor sidesteps both
    // hazards regardless of how xunit schedules or orders the tests that
    // consume this fixture.
    public sealed class McpClientDataFixture
    {
        public const string SkipReason =
            "Set SPELLWORK_DBC_PATH (and optionally SPELLWORK_DB2_PATH and SPELLWORK_LOCALE) to the extracted CoA 3.3.5a client data directories.";

        // The one modelled store deliberately absent from the partial-load
        // temp directory, and two that are present in it. All three are
        // modelled Storage<T> properties on DBC.DBC; none is read by
        // BuildSpellInfoStore, so omitting the first cannot turn the partial
        // load into a NullReferenceException instead of the per-store
        // degradation it is meant to demonstrate.
        public const string MissingStoreName = "ScreenEffect";
        // Spell is included because LoadAsync clears SpellInfoStore on every
        // load: without it the partial load would have no spells and report
        // no_data_loaded instead of per-store degradation.
        public static readonly string[] PresentStoreNames = { "SpellDifficulty", "SpellDuration", "Spell" };

        private static readonly object SyncRoot = new object();
        private static bool _attempted;
        private static bool _available;
        private static SpellData _noDataResult;
        private static SpellData _loadedResult;
        private static SpellDataService _loadedService;
        private static SpellTools _spellTools;
        private static DecoderTools _decoderTools;
        private static Db2Tools _db2Tools;
        private static SpellData _partialResult;
        private static string _partialListDb2Json;
        private static string _partialServerInfoJson;
        private static string _partialMissingStoreRowJson;
        private static string _partialPresentStoreQueryJson;
        private static IReadOnlyList<string> _partialUnloadedStores;

        public bool Available { get; }
        public SpellData NoDataResult { get; }
        public SpellData LoadedResult { get; }
        public SpellDataService LoadedService { get; }
        public SpellTools SpellTools { get; }
        public DecoderTools DecoderTools { get; }
        public Db2Tools Db2Tools { get; }

        /// <summary>Outcome of a load against a locale directory that exists but
        /// holds only some of the <c>.db2</c> files.</summary>
        public SpellData PartialResult { get; }

        /// <summary><c>list_db2</c>, captured while the partial load's stores were
        /// installed on DBC.DBC.</summary>
        /// <summary><c>server_info</c>, captured in the same state.</summary>
        public string PartialServerInfoJson { get; }

        public string PartialListDb2Json { get; }

        /// <summary><c>get_db2_row</c> against <see cref="MissingStoreName"/>,
        /// captured in the same state.</summary>
        public string PartialMissingStoreRowJson { get; }

        /// <summary><c>query_db2</c> against a store whose file <em>was</em>
        /// present, captured in the same state.</summary>
        public string PartialPresentStoreQueryJson { get; }

        /// <summary><c>DBC.DBC.UnloadedStores</c>, captured in the same state --
        /// the list the WinForms host refuses to start on.</summary>
        public IReadOnlyList<string> PartialUnloadedStores { get; }

        public McpClientDataFixture()
        {
            lock (SyncRoot)
            {
                if (!_attempted)
                {
                    _attempted = true;

                    var dbcPath = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH");
                    var db2Path = Environment.GetEnvironmentVariable("SPELLWORK_DB2_PATH");
                    var locale = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS";

                    if (!string.IsNullOrEmpty(dbcPath))
                    {
                        _available = true;

                        // No-data scenario FIRST, deliberately, before any
                        // real data has ever populated DBC.DBC's static
                        // Storage<T> properties in this process -- see the
                        // class comment above for why the order is load-
                        // bearing. A missing locale folder no longer yields
                        // "no data" (the resolver falls back to the root), so
                        // the scenario is a DbcPath that exists but holds no
                        // client files.
                        var emptyRoot = Path.Combine(Path.GetTempPath(),
                            "spellwork-mcp-empty-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(emptyRoot);
                        try
                        {
                            var noDataService = new SpellDataService(
                                McpServerConfig.FromEnvironment(new Dictionary<string, string>
                                {
                                    ["SPELLWORK_DBC_PATH"] = emptyRoot,
                                    ["SPELLWORK_LOCALE"] = locale,
                                }),
                                NullLoggerFactoryStub.Logger);
                            _noDataResult = noDataService.GetAsync().GetAwaiter().GetResult();
                        }
                        finally
                        {
                            try { Directory.Delete(emptyRoot, true); } catch (IOException) { }
                        }

                        var loadedService = new SpellDataService(
                            McpServerConfig.FromEnvironment(new Dictionary<string, string>
                            {
                                ["SPELLWORK_DBC_PATH"] = dbcPath,
                                ["SPELLWORK_DB2_PATH"] = db2Path,
                                ["SPELLWORK_LOCALE"] = locale,
                            }),
                            NullLoggerFactoryStub.Logger);
                        _loadedResult = loadedService.GetAsync().GetAwaiter().GetResult();
                        _loadedService = loadedService;
                        _spellTools = new SpellTools(loadedService);
                        _decoderTools = new DecoderTools(loadedService);
                        _db2Tools = new Db2Tools(loadedService);

                        // Partial-load scenario LAST, and only after every
                        // property above has been captured -- see
                        // RunPartialLoad for why it must run here rather than
                        // inside a test.
                        RunPartialLoad(dbcPath, db2Path, locale);
                    }
                }

                Available = _available;
                NoDataResult = _noDataResult;
                LoadedResult = _loadedResult;
                LoadedService = _loadedService;
                SpellTools = _spellTools;
                DecoderTools = _decoderTools;
                Db2Tools = _db2Tools;
                PartialResult = _partialResult;
                PartialListDb2Json = _partialListDb2Json;
                PartialServerInfoJson = _partialServerInfoJson;
                PartialMissingStoreRowJson = _partialMissingStoreRowJson;
                PartialPresentStoreQueryJson = _partialPresentStoreQueryJson;
                PartialUnloadedStores = _partialUnloadedStores;
            }
        }

        /// <summary>
        /// Loads a client-data directory that exists but holds only two of the
        /// forty-six modelled client files, and captures what the server
        /// reports while that partial state is installed.
        /// <para>
        /// Why here and not in a test. DBC.DBC's Storage&lt;T&gt; properties
        /// are process-wide statics that every other client-data test reads,
        /// and xunit runs test classes concurrently -- a test that installed a
        /// partial store set would race every one of them. Doing it inside
        /// this constructor, under the same lock that already serialises the
        /// two loads above and before any test body runs, removes the race
        /// entirely: the tests only ever assert on the JSON captured here.
        /// </para>
        /// <para>
        /// Why no nulling step. DBC.LoadAsync now nulls every store it does not
        /// open, so the partial load starts from the same state a first load in a
        /// fresh process does, with no help from here.
        /// </para>
        /// <para>
        /// Why the real data survives. The partial directory holds Spell
        /// (LoadAsync clears SpellInfoStore on entry, so spells must be
        /// rebuilt from it). The finally
        /// then runs the full load again, which restores the stores and also
        /// OpenedFiles, LayoutMismatches and LocaleFallback.
        /// </para>
        /// <para>
        /// SpellInfoStore being rebuilt from Spell.dbc is what makes the
        /// partial SpellDataService report Loaded == true. That is what lets the Db2Tools calls
        /// below get past their "if (!data.Loaded)" guard and reach the
        /// per-store answers under test -- which is the point, since a missing
        /// individual store is precisely the case where the rest of the data
        /// is fine.
        /// </para>
        /// </summary>
        private static void RunPartialLoad(string dbcPath, string db2Path, string locale)
        {
            var root = Path.Combine(Path.GetTempPath(),
                "spellwork-mcp-partial-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var sourceConfig = new SpellWorkConfig { DbcPath = dbcPath, Db2Path = db2Path, Locale = locale };
            try
            {
                // Copied from wherever the real load found them (.db2 or
                // .dbc, locale folder or root) into the partial root.
                foreach (var store in PresentStoreNames)
                {
                    var source = SpellWork.DBC.ClientFileResolver.Resolve(store, sourceConfig);
                    File.Copy(source.Path, Path.Combine(root, Path.GetFileName(source.Path)));
                }

                var partialService = new SpellDataService(
                    McpServerConfig.FromEnvironment(new Dictionary<string, string>
                    {
                        ["SPELLWORK_DBC_PATH"] = root,
                        ["SPELLWORK_LOCALE"] = locale,
                    }),
                    NullLoggerFactoryStub.Logger);

                _partialResult = partialService.GetAsync().GetAwaiter().GetResult();

                // Captured here rather than read from a test, for the same
                // reason as the JSON below: it is a live view of the static
                // state, so outside this lock it would report whatever the
                // rest of the suite happens to have loaded.
                _partialUnloadedStores = SpellWork.DBC.DBC.UnloadedStores;

                var partialDb2Tools = new Db2Tools(partialService);
                _partialServerInfoJson = new DiagnosticTools(partialService).ServerInfo().GetAwaiter().GetResult();
                _partialListDb2Json = partialDb2Tools.ListDb2().GetAwaiter().GetResult();
                _partialMissingStoreRowJson =
                    partialDb2Tools.GetDb2Row(MissingStoreName, 1).GetAwaiter().GetResult();
                _partialPresentStoreQueryJson =
                    partialDb2Tools.QueryDb2(PresentStoreNames[0], limit: 5).GetAwaiter().GetResult();
            }
            finally
            {
                // Put the real client data back by loading it again. The partial
                // load nulled every store it did not open, and replaced
                // OpenedFiles, LayoutMismatches and LocaleFallback; a full load
                // restores all of them together, which no per-store snapshot
                // could (those three are not settable from here).
                SpellWork.DBC.DBC.LoadAsync(sourceConfig, SpellWork.NullSpellWorkLogger.Instance).GetAwaiter().GetResult();

                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                    // A leftover temp directory is not worth failing a suite
                    // over; the OS cleans %TEMP%.
                }
            }
        }

        /// <summary>
        /// The same reflection scan DBC.LoadAsync, SpellDataService.CountStores
        /// and Db2Tools.ModelledStores all use -- same BindingFlags on purpose,
        /// so this snapshot cannot cover fewer properties than the load it is
        /// undoing.
        /// </summary>
        private static IEnumerable<PropertyInfo> StorageProperties()
        {
            foreach (var property in typeof(SpellWork.DBC.DBC).GetProperties(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition() ==
                        typeof(DBFileReaderLib.Storage<>))
                {
                    yield return property;
                }
            }
        }

        // Convenience wrapper matching SpellWork.Core.Tests/
        // ClientDataFixture.cs's SkipIfNoClientData, so both suites share one
        // calling convention at test call sites.
        public void SkipIfNoClientData() => Skip.IfNot(Available, SkipReason);
    }
}
