using System;
using System.Collections.Generic;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // DBC.DBC is process-wide static state: a second successful load in one
    // process would re-populate the same statics. This suite needs two
    // outcomes -- a load that finds no usable data (wrong locale, proving
    // SpellDataService's no_data_loaded backstop) and a successful one -- and
    // order matters: the failed load must run first, before real data has
    // populated the statics. Both loads happen once, in that order, inside a
    // single locked constructor.
    public sealed class McpClientDataFixture
    {
        public const string SkipReason =
            "Set SPELLWORK_DBC_PATH (and optionally SPELLWORK_DEFS_PATH, SPELLWORK_BUILD, SPELLWORK_LOCALE) to the extracted 70449 client data.";

        private const string DefaultDefsPath =
            @"F:\WorkDir\trinitycore-mop-classic\tools\mop-re\refs\wowdbdefs\definitions";

        private static readonly object SyncRoot = new object();
        private static bool _attempted;
        private static bool _available;
        private static SpellData _noDataResult;
        private static SpellData _loadedResult;
        private static SpellDataService _loadedService;
        private static SpellTools _spellTools;

        public bool Available { get; }
        public SpellData NoDataResult { get; }
        public SpellData LoadedResult { get; }
        public SpellDataService LoadedService { get; }
        public SpellTools SpellTools { get; }

        public McpClientDataFixture()
        {
            lock (SyncRoot)
            {
                if (!_attempted)
                {
                    _attempted = true;

                    var dbcPath = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH");
                    var locale = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS";
                    var defsPath = Environment.GetEnvironmentVariable("SPELLWORK_DEFS_PATH") ?? DefaultDefsPath;
                    var build = Environment.GetEnvironmentVariable("SPELLWORK_BUILD") ?? "5.5.4.70449";

                    if (!string.IsNullOrEmpty(dbcPath))
                    {
                        _available = true;

                        Dictionary<string, string> Env(string loc) => new Dictionary<string, string>
                        {
                            ["SPELLWORK_DBC_PATH"] = dbcPath,
                            ["SPELLWORK_DEFS_PATH"] = defsPath,
                            ["SPELLWORK_BUILD"] = build,
                            ["SPELLWORK_LOCALE"] = loc,
                        };

                        var noDataService = new SpellDataService(
                            McpServerConfig.FromEnvironment(Env("xx-not-a-real-locale")),
                            NullLoggerFactoryStub.Logger);
                        _noDataResult = noDataService.GetAsync().GetAwaiter().GetResult();

                        var loadedService = new SpellDataService(
                            McpServerConfig.FromEnvironment(Env(locale)),
                            NullLoggerFactoryStub.Logger);
                        _loadedResult = loadedService.GetAsync().GetAwaiter().GetResult();
                        _loadedService = loadedService;
                        _spellTools = new SpellTools(loadedService);
                    }
                }

                Available = _available;
                NoDataResult = _noDataResult;
                LoadedResult = _loadedResult;
                LoadedService = _loadedService;
                SpellTools = _spellTools;
            }
        }

        public void SkipIfNoClientData() => Skip.IfNot(Available, SkipReason);
    }
}
