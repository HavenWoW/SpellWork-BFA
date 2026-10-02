using System.Linq;
using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class SpellInfoAssemblyTests
    {
        private readonly ClientDataFixture _fixture;
        public SpellInfoAssemblyTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void FireballAssembles()
        {
            _fixture.SkipIfNoClientData();
            var s = DBC.DBC.SpellInfoStore[133];
            Assert.Equal("Fireball", s.Name);
            Assert.Equal(4, s.SchoolMask);
            var e = Assert.Single(s.EffectsFor(0));
            Assert.Equal(91, e.Entry.ID);
            Assert.Equal(2, e.Entry.Effect);                 // SCHOOL_DAMAGE
            Assert.Equal(1.5f, e.Entry.EffectBonusCoefficient);
            Assert.NotNull(e.Scaling);
            Assert.Equal(1.5f, e.Scaling.Coefficient);
            Assert.Equal(0.24f, e.Scaling.Variance, 3);
        }

        [SkippableFact]
        public void DifficultyWithoutRowsFallsBackToBase()
        {
            _fixture.SkipIfNoClientData();
            var s = DBC.DBC.SpellInfoStore[43093]; // Grievous Throw: rows at 0, 2, 4
            var effects = s.EffectsFor(1);
            Assert.Equal(new[] { 0, 1 }, effects.Select(x => x.Entry.EffectIndex));
            Assert.All(effects, x => Assert.Equal(0, x.ResolvedDifficulty));
            Assert.Equal(new[] { 5000, 28499 }, effects.Select(x => x.Entry.EffectBasePoints));
        }

        [SkippableFact]
        public void DifficultyWithRowsUsesThem()
        {
            _fixture.SkipIfNoClientData();
            var effects = DBC.DBC.SpellInfoStore[43093].EffectsFor(2);
            Assert.All(effects, x => Assert.Equal(2, x.ResolvedDifficulty));
            Assert.Equal(new[] { 7500, 45124 }, effects.Select(x => x.Entry.EffectBasePoints));
        }

        [SkippableFact]
        public void EffectDifficultiesListed()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal(new[] { 0, 2, 4 }, DBC.DBC.SpellInfoStore[43093].EffectDifficulties);
        }

        [SkippableFact]
        public void TriggerStoreIsBuilt()
        {
            _fixture.SkipIfNoClientData();
            Assert.NotEmpty(DBC.DBC.SpellTriggerStore);
        }

        [SkippableFact]
        public void FireballScalesFromGtAtLevel90()
        {
            _fixture.SkipIfNoClientData();
            var s = DBC.DBC.SpellInfoStore[133];
            var key = SpellWork.Spell.SpellScalingCalculator.GtKey(s.Scaling.Class, 90, s.Scaling.MaxScalingLevel);
            var gt = DBC.DBC.GtSpellScaling[key].Value;
            var v = s.GetEffectValues(s.EffectsFor(0)[0], new SpellWork.Spell.RenderContext(90, 463, 0));
            Assert.Equal((float)System.Math.Round(1.5f * SpellWork.Spell.SpellScalingCalculator.Multiplier(s.Scaling, 90, k => DBC.DBC.GtSpellScaling.TryGetValue(k, out var r) ? r.Value : (float?)null)), v.BaseValue);
            Assert.True(gt > 0);
        }
    }
}
