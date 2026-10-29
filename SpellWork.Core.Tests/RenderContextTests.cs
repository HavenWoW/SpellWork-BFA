using SpellWork.DBC;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class RenderContextTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public RenderContextTests(ClientDataFixture fixture) => _fixture = fixture;

        // Default is the documented fallback context (spec D5: level 90, item level 463, base difficulty).
        
        [Fact]
        public void Default_IsMaxLevelDefaultItemLevelBase()
        {
            var context = RenderContext.Default;

            Assert.Equal(DBC.DBC.MaxLevel, context.Level);
            Assert.Equal(DBC.DBC.DefaultItemLevel, context.ItemLevel);
            Assert.Equal(0, context.Difficulty);
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

        [Theory]
        [InlineData(0u, 1u)]
        [InlineData(1u, 1u)]
        [InlineData(90u, 90u)]
        [InlineData(100u, 100u)]
        [InlineData(150u, 100u)]
        public void Level_ClampsTo1Through100(uint given, uint expected) =>
            Assert.Equal(expected, new RenderContext(given, 0, 0).Level);
    }
}
