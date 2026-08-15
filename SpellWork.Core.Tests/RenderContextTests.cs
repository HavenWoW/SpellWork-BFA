using SpellWork.DBC;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class RenderContextTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public RenderContextTests(ClientDataFixture fixture) => _fixture = fixture;

        // Spell 606 "Mind Rot" takes the ExpectedStat branch of
        // CalculateBaseEffectValue, so its base value is a direct function of
        // the render context's level. If the context is not actually reaching
        // the arithmetic, both calls return the same number.
        [SkippableFact]
        public void CalculateBaseEffectValue_UsesContextLevel_NotGlobals()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[606];
            var effect = spell.Effects[0];

            var atLevel120 = spell.CalculateBaseEffectValue(effect, new RenderContext(120, 475, null));
            var atLevel20 = spell.CalculateBaseEffectValue(effect, new RenderContext(20, 475, null));

            Assert.NotEqual(atLevel120, atLevel20);
        }

        // The point of the whole task: two contexts alive at once must not
        // interfere. Interleave them and assert each still produces the value
        // it produces alone.
        [SkippableFact]
        public void ConcurrentContexts_DoNotInterfere()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[606];
            var effect = spell.Effects[0];
            var low = new RenderContext(20, 475, null);
            var high = new RenderContext(120, 475, null);

            var lowAlone = spell.CalculateBaseEffectValue(effect, low);
            var highAlone = spell.CalculateBaseEffectValue(effect, high);

            System.Threading.Tasks.Parallel.For(0, 200, i =>
            {
                var context = (i % 2 == 0) ? low : high;
                var expected = (i % 2 == 0) ? lowAlone : highAlone;
                Assert.Equal(expected, spell.CalculateBaseEffectValue(effect, context));
            });
        }

        // Guards the GUI: FromGlobals must snapshot exactly what the statics
        // hold, or the WinForms app renders at the wrong level.
        [SkippableFact]
        public void FromGlobals_SnapshotsTheStatics()
        {
            _fixture.SkipIfNoClientData();

            var context = RenderContext.FromGlobals();

            Assert.Equal(DBC.DBC.SelectedLevel, context.Level);
            Assert.Equal(DBC.DBC.SelectedItemLevel, context.ItemLevel);
            Assert.Same(DBC.DBC.SelectedMapDifficulty, context.MapDifficulty);
        }
    }
}
