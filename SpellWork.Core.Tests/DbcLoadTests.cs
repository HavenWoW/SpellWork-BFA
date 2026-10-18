using System.Linq;
using Xunit;

namespace SpellWork.Core.Tests
{
    /// <summary>
    /// 5.5.4 (70449) bring-up load against a real client data directory.
    /// Skipped when SPELLWORK_DBC_PATH is unset. Exact-value pinning of
    /// individual spells happens at the Task 6 green gate against live data.
    /// </summary>
    public class DbcLoadTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public DbcLoadTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void RealClientData_LoadsEveryModelledStore()
        {
            _fixture.SkipIfNoClientData();

            var unloaded = DBC.DBC.UnloadedStores;

            Assert.True(unloaded.Count == 0,
                "Every modelled Storage<T> should have loaded, but these did not: " +
                string.Join(", ", unloaded.Select(name => name + ".db2")));
        }

        [SkippableFact]
        public void RealClientData_StoreCountsMatch70449()
        {
            _fixture.SkipIfNoClientData();

            Assert.Equal(98183, DBC.DBC.SpellName.Count);
            Assert.Equal(103075, DBC.DBC.SpellMisc.Count);
            Assert.Equal(142837, DBC.DBC.SpellEffect.Count);
            Assert.Equal(20, DBC.DBC.Difficulty.Count);
        }

        [SkippableFact]
        public void RealClientData_BuildsSpellInfoStore()
        {
            _fixture.SkipIfNoClientData();

            Assert.Equal(DBC.DBC.SpellName.Count, DBC.DBC.SpellInfoStore.Count);
            Assert.Contains(0, DBC.DBC.ValidDifficultyIds);
            Assert.False(string.IsNullOrEmpty(DBC.DBC.SpellInfoStore.First().Value.Name));
        }
    }
}
