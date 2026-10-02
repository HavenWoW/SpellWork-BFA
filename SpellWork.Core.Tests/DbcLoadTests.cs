using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class DbcLoadTests
    {
        private readonly ClientDataFixture _fixture;
        public DbcLoadTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void SpellIsKeyedById()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal("Fireball", DBC.DBC.Spell[133].Name);
            Assert.Equal("Shadow Word: Pain", DBC.DBC.Spell[589].Name);
        }

        [SkippableFact]
        public void ReferenceRowCounts()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal(94842, DBC.DBC.Spell.Count);
            Assert.Equal(134760, DBC.DBC.SpellEffect.Count);
            Assert.Equal(111077, DBC.DBC.SpellMisc.Count);
            Assert.Equal(28214, DBC.DBC.SpellVisual.Count);
            Assert.Equal(15409, DBC.DBC.SpellVisualEffectName.Count);
            Assert.Equal(1700, DBC.DBC.GtSpellScaling.Count);
        }

        [SkippableFact]
        public void FireballMiscRow()
        {
            _fixture.SkipIfNoClientData();
            var misc = DBC.DBC.SpellMisc[DBC.DBC.Spell[133].MiscID];
            Assert.Equal(68, misc.ID);
            Assert.Equal(133, misc.SpellID);
            Assert.Equal(4, misc.SchoolMask);
            Assert.Equal(28f, misc.Speed);
            Assert.Equal(67, misc.SpellVisualID[0]);
        }

        [SkippableFact]
        public void LocaleFallbackIsReported()
        {
            _fixture.SkipIfNoClientData();
            Assert.True(DBC.DBC.LocaleFallback); // reference data has no enUS folder
        }
    }
}
