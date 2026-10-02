using System;
using Xunit;

namespace SpellWork.Core.Tests
{
    // The data-backed suites share one fixture instance through this
    // collection (no other collection definition existed in this project).
    [CollectionDefinition("ClientData")]
    public sealed class ClientDataCollection : ICollectionFixture<ClientDataFixture> { }

    public sealed class ClientDataFixture
    {
        public const string SkipReason =
            "Set SPELLWORK_DBC_PATH (and optionally SPELLWORK_DB2_PATH) to the extracted 5.4.8 client data directories.";

        // DBC.DBC is process-wide static state. xunit constructs a fresh
        // ClientDataFixture per test class (IClassFixture is per-class, not
        // shared), and by default runs different test classes' collections
        // concurrently. Without this guard, two fixtures racing DBC.LoadAsync
        // both add entries to the same static dictionaries and throw
        // "duplicate key" — serialize on a lock and load at most once. The
        // test classes still run concurrently against DBC.DBC's statics after
        // this constructor returns; that is safe only because nothing
        // mutates them post-load (ProcInfo.SpellProc and ProcInfo.Update are
        // mutable statics that now live in Core too, but only GUI code
        // writes them today — a future test that writes them would break
        // this assumption).
        private static readonly object SyncRoot = new object();
        private static bool _attempted;
        private static bool _loaded;
        private static Exception _loadError;

        public bool Loaded { get; }

        public ClientDataFixture()
        {
            lock (SyncRoot)
            {
                if (!_attempted)
                {
                    _attempted = true;

                    var dbcPath = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH");

                    if (!string.IsNullOrEmpty(dbcPath))
                    {
                        var config = new SpellWorkConfig
                        {
                            DbcPath = dbcPath,
                            Db2Path = Environment.GetEnvironmentVariable("SPELLWORK_DB2_PATH"),
                            Locale = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS"
                        };

                        try
                        {
                            DBC.DBC.LoadAsync(config, NullSpellWorkLogger.Instance).GetAwaiter().GetResult();
                            _loaded = true;
                        }
                        catch (Exception ex)
                        {
                            // Store rather than let it escape here: if this
                            // instance lost the race, _attempted is already
                            // true, so a raw throw here would only fail the
                            // class that happened to run first while every
                            // other class silently skips with a SkipReason
                            // that is false (the env vars were set fine).
                            _loadError = ex;
                        }
                    }
                }

                if (_loadError != null)
                    throw new InvalidOperationException("ClientData load failed", _loadError);

                Loaded = _loaded;
            }
        }

        // Convenience wrapper over Skip.IfNot(Loaded, SkipReason) so callers
        // don't need to repeat both halves at every call site.
        public void SkipIfNoClientData() => Skip.IfNot(Loaded, SkipReason);
    }
}
