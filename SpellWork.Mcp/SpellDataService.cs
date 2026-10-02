using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DBFileReaderLib;
// SpellWork.Mcp is nested under SpellWork, which makes the sibling
// namespace SpellWork.DBC visible unqualified as "DBC" -- colliding with,
// and taking priority over, the class SpellWork.DBC.DBC of the same name
// (confirmed: neither "using SpellWork.DBC;" nor a "using DBC = ...;"
// alias wins against that enclosing-namespace visibility). SpellWork's own
// WinForms entry point (SpellWork/Program.cs) hits the identical collision
// and resolves it the same way: write out "DBC.DBC" -- first DBC is the
// namespace (visible via the enclosing SpellWork namespace), second DBC is
// the class.

namespace SpellWork.Mcp
{
    /// <summary>The outcome of the one client-data load this process performs.</summary>
    public sealed class SpellData
    {
        public bool Loaded { get; internal set; }
        public string ErrorCode { get; internal set; }
        public string ErrorMessage { get; internal set; }
        public string ErrorVariable { get; internal set; }
        public TimeSpan LoadDuration { get; internal set; }
        public IReadOnlyDictionary<string, int> StoreCounts { get; internal set; } =
            new Dictionary<string, int>();
        public int SpellCount { get; internal set; }
    }

    /// <summary>
    /// Loads DBC and GameTable data exactly once, on first use.
    /// ExecutionAndPublication rather than a plain lock: DBC.LoadAsync has no
    /// load guard and GameTable&lt;T&gt;.Open populates with _records.Add, so a
    /// second load in the same process throws a duplicate-key
    /// ArgumentException. The Lazy is what makes that unreachable.
    /// </summary>
    public sealed class SpellDataService
    {
        private readonly McpServerConfig _config;
        private readonly ILogger<SpellDataService> _logger;
        private readonly Lazy<Task<SpellData>> _data;

        public SpellDataService(McpServerConfig config, ILogger<SpellDataService> logger)
        {
            _config = config;
            _logger = logger;
            _data = new Lazy<Task<SpellData>>(LoadAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public McpServerConfig Config => _config;

        public Task<SpellData> GetAsync() => _data.Value;

        private async Task<SpellData> LoadAsync()
        {
            var missing = ValidatePaths();
            if (missing != null)
                return missing;

            var stopwatch = Stopwatch.StartNew();
            var result = new SpellData();

            try
            {
                // DBC.LoadAsync already opens the one GameTable SpellWork uses
                // (GtSpellScalingEntry, from SpellScaling.txt) as its last
                // step -- see DBC.cs. There is no separate GameTable.LoadAll
                // API; SpellWork\Program.cs (the WinForms entry point) calls
                // nothing beyond DBC.LoadAsync either, so this mirrors it.
                await DBC.DBC.LoadAsync(_config.ToSpellWorkConfig(), new LoggerAdapter(_logger));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client data load failed.");
                result.ErrorCode = "load_failed";
                result.ErrorMessage = ex.Message;
                result.LoadDuration = stopwatch.Elapsed;
                return result;
            }

            stopwatch.Stop();
            result.LoadDuration = stopwatch.Elapsed;
            result.StoreCounts = CountStores();
            result.SpellCount = DBC.DBC.SpellInfoStore.Count;

            // DBC.LoadAsync swallows DirectoryNotFoundException per store, so
            // a path that exists but holds no .db2 files "succeeds" with every
            // store empty. Zero spells is that case, and the caller needs to
            // hear about it rather than get empty results forever.
            if (result.SpellCount == 0)
            {
                result.ErrorCode = "no_data_loaded";
                result.ErrorMessage =
                    $"No spells were loaded from '{_config.DbcPath}\\{_config.Locale}'. " +
                    "Check that the path holds the .db2 files for the configured locale.";
                result.ErrorVariable = "SPELLWORK_DBC_PATH";
                return result;
            }

            result.Loaded = true;
            _logger.LogInformation("Loaded {SpellCount} spells in {Duration}.", result.SpellCount, result.LoadDuration);
            return result;
        }

        private SpellData ValidatePaths()
        {
            if (string.IsNullOrWhiteSpace(_config.DbcPath))
                return Failure("dbc_path_missing", "SPELLWORK_DBC_PATH is not set.", "SPELLWORK_DBC_PATH");

            if (!Directory.Exists(_config.DbcPath))
                return Failure("dbc_path_not_found",
                    $"SPELLWORK_DBC_PATH points at '{_config.DbcPath}', which does not exist.", "SPELLWORK_DBC_PATH");

            return null;
        }

        private static SpellData Failure(string code, string message, string variable) => new SpellData
        {
            Loaded = false,
            ErrorCode = code,
            ErrorMessage = message,
            ErrorVariable = variable,
        };

        private static IReadOnlyDictionary<string, int> CountStores()
        {
            // Same BindingFlags as DBC.LoadAsync's own reflection scan
            // (DBC.cs). Every Storage<T> property on DBC.DBC happens to be
            // public today, so this and Static | Public alone currently
            // agree -- but keeping them identical means a future non-public
            // store can't silently vanish from storeCounts while
            // DBC.LoadAsync still loads it.
            var counts = new Dictionary<string, int>();
            foreach (var property in typeof(DBC.DBC).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!property.PropertyType.IsGenericType ||
                    property.PropertyType.GetGenericTypeDefinition() != typeof(Storage<>))
                    continue;

                var storage = property.GetValue(null) as System.Collections.ICollection;
                counts[property.Name] = storage?.Count ?? 0;
            }

            return counts;
        }

        private sealed class LoggerAdapter : ISpellWorkLogger
        {
            private readonly ILogger _logger;
            public LoggerAdapter(ILogger logger) => _logger = logger;
            public void Warn(string message) => _logger.LogWarning("{Message}", message);
        }
    }
}
