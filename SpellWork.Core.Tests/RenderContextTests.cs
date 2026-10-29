using System.Linq;
using SpellWork.DBC;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class RenderContextTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public RenderContextTests(ClientDataFixture fixture) => _fixture = fixture;

        // Default is the documented fallback context: max level, default item level, no difficulty.
        [Fact]
        public void Default_IsMaxLevelDefaultItemLevelNoDifficulty()
        {
            var context = RenderContext.Default;

            Assert.Equal(DBC.DBC.MaxLevel, context.Level);
            Assert.Equal(DBC.DBC.DefaultItemLevel, context.ItemLevel);
            Assert.Null(context.Difficulty);
        }

        // Difficulty is a SpellDifficulty mode (0-3) or null (spec D4).
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void Difficulty_AcceptsModes0Through3(int mode) =>
            Assert.Equal(mode, new RenderContext(80, 0, mode).Difficulty);

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void Difficulty_RejectsOtherModes(int mode) =>
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RenderContext(80, 0, mode));

        // Contexts carry their own level; two threads rendering at different
        // levels must never see each other's value.
        [SkippableFact]
        public void ConcurrentContexts_DoNotInterfere()
        {
            _fixture.SkipIfNoClientData();

            // Any spell whose first effect grows per level across 10..90
            // (3.3.5 values scale through RealPointsPerLevel only).
            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Count > 0 && s.Effects[0].Entry.EffectRealPointsPerLevel >= 1f &&
                s.BaseLevel <= 10 && s.SpellLevel <= 10 && (s.MaxLevel == 0 || s.MaxLevel >= 90));
            var effect = spell.Effects[0];
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
