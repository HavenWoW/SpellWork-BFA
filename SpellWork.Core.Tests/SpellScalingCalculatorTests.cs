using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class SpellScalingCalculatorTests
    {
        [Theory]
        [InlineData(8, 90, 0, 7 * 100 + 89)]      // mage, level 90
        [InlineData(8, 90, 85, 7 * 100 + 84)]     // capped by MaxScalingLevel
        [InlineData(-1, 90, 0, 11 * 100 + 89)]    // MAX_CLASSES - 2 - (-1) = 11
        [InlineData(-2, 90, 0, 12 * 100 + 89)]    // Mana Gem style
        public void GtKey(int cls, int level, int maxLevel, int expected) =>
            Assert.Equal(expected, SpellScalingCalculator.GtKey(cls, level, maxLevel));

        [Fact]
        public void CastTimeAndNerfFactorsApply()
        {
            var s = new SpellScalingEntry { Class = 8, CastTimeMin = 1000, CastTimeMax = 3000, CastTimeMaxLevel = 21, NerfFactor = 0.5f, NerfMaxLevel = 11 };
            // level 11: cast = 1000 + 10*2000/20 = 2000 -> *2000/3000; nerf: (1-0.5)*10/10 + 0.5 = 1.0
            var m = SpellScalingCalculator.Multiplier(s, 11, key => 300f);
            Assert.Equal(300f * 2000f / 3000f, m, 3);
        }

        [Fact]
        public void ValueRangeUsesVariance()
        {
            var s = new SpellScalingEntry { Class = 8 };
            var e = new SpellEffectScalingEntry { Coefficient = 1.5f, Variance = 0.24f, ResourceCoefficient = 0f };
            var v = SpellScalingCalculator.Value(s, e, 90, key => 1000f);
            Assert.Equal(1500f, v.Base, 3);
            Assert.Equal(1500f - 180f, v.Min, 3);
            Assert.Equal(1500f + 180f, v.Max, 3);
        }

        [Fact]
        public void MissingGtRowIsZeroNotAThrow() =>
            Assert.Equal(0f, SpellScalingCalculator.Multiplier(new SpellScalingEntry { Class = 8 }, 90, key => null));
    }
}
