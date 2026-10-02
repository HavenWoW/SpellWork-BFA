using SpellWork.DBC;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class RenderContextTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public RenderContextTests(ClientDataFixture fixture) => _fixture = fixture;

        // FromGlobals must snapshot exactly what the statics hold, or a caller
        // relying on the process-wide selection renders at the wrong level.
        [SkippableFact]
        public void FromGlobals_SnapshotsTheStatics()
        {
            _fixture.SkipIfNoClientData();

            var context = RenderContext.FromGlobals();

            Assert.Equal(DBC.DBC.SelectedLevel, context.Level);
            Assert.Equal(DBC.DBC.SelectedItemLevel, context.ItemLevel);
            Assert.Equal(DBC.DBC.SelectedDifficulty, context.Difficulty);
        }

        // Contexts carry their own level; two threads rendering at different
        // levels must never see each other's value.
        [SkippableFact]
        public void ConcurrentContexts_DoNotInterfere()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[133];
            var effect = spell.EffectsFor(0)[0];
            var low = new RenderContext(10, 0, 0);
            var high = new RenderContext(90, 0, 0);
            var expectedLow = spell.GetEffectValues(effect, low).BaseValue;
            var expectedHigh = spell.GetEffectValues(effect, high).BaseValue;
            Assert.NotEqual(expectedLow, expectedHigh);

            System.Threading.Tasks.Parallel.For(0, 200, i =>
            {
                var isHigh = i % 2 == 0;
                var actual = spell.GetEffectValues(effect, isHigh ? high : low).BaseValue;
                Assert.Equal(isHigh ? expectedHigh : expectedLow, actual);
            });
        }
    }
}
