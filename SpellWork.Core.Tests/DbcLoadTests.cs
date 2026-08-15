using System.Linq;
using System.Reflection;
using Xunit;

namespace SpellWork.Core.Tests
{
    /// <summary>
    /// DBC.UnloadedStores against a real, complete ClientData directory.
    /// <para>
    /// This lives here rather than in SpellWork.Mcp.Tests on purpose. That
    /// suite's fixture deliberately installs a partial store set on DBC.DBC's
    /// statics and restores it afterwards, so an assertion there about the
    /// live value of UnloadedStores would be asserting on whatever phase of
    /// that fixture happened to be in effect. This suite never mutates them
    /// after ClientDataFixture's single load, so the live value is meaningful.
    /// </para>
    /// </summary>
    public class DbcLoadTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public DbcLoadTests(ClientDataFixture fixture) => _fixture = fixture;

        // The tripwire for the WinForms host's new refuse-to-start guard
        // (Program.StoppedOnIncompleteData). That guard is only as good as
        // this being empty for data that is actually fine: if a modelled
        // Storage<T> property were added for a .db2 the client does not ship,
        // or renamed so the "{name}.db2" convention LoadAsync depends on no
        // longer resolves, UnloadedStores would report it forever and the GUI
        // would refuse to start on a perfectly good extraction. Nothing else
        // would catch that -- the MCP server tolerates missing stores by
        // design, so its whole suite would stay green.
        [SkippableFact]
        public void RealClientData_LoadsEveryModelledStore()
        {
            _fixture.SkipIfNoClientData();

            var unloaded = DBC.DBC.UnloadedStores;

            Assert.True(unloaded.Count == 0,
                "Every modelled Storage<T> should have loaded from a complete " +
                "ClientData directory, but these did not: " +
                string.Join(", ", unloaded.Select(name => name + ".db2")) +
                ". SpellWork (WinForms) refuses to start while this list is " +
                "non-empty, so either the file really is absent from " +
                "SPELLWORK_DBC_PATH or a property was added/renamed that no " +
                ".db2 of that name backs.");
        }

        // The other direction: empty must mean "all of them loaded", not
        // "there was nothing to scan". A bug that stopped the scan matching
        // Storage<T> -- a changed BindingFlags, a moved generic type -- would
        // satisfy the assertion above vacuously and disarm the GUI guard
        // without failing anything.
        //
        // Checked by counting the properties rather than by nulling one and
        // reading it back: ClientDataFixture's contract is that nothing
        // mutates DBC.DBC's statics after its single load, because xunit runs
        // these test classes concurrently and the rendering suites read those
        // stores throughout.
        [SkippableFact]
        public void UnloadedStores_IsEmptyBecauseStoresLoaded_NotBecauseNoneWereScanned()
        {
            _fixture.SkipIfNoClientData();

            var storages = typeof(DBC.DBC)
                .GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() ==
                                typeof(DBFileReaderLib.Storage<>))
                .ToList();

            Assert.True(storages.Count > 40,
                $"Expected DBC to model dozens of Storage<T> stores, found {storages.Count} -- " +
                "UnloadedStores being empty proves nothing if it scans nothing.");

            Assert.NotNull(DBC.DBC.SpellName);
            Assert.True(DBC.DBC.SpellInfoStore.Count > 0);
        }
    }
}
