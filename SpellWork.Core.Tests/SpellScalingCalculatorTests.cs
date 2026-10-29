using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class SpellScalingCalculatorTests
    {
        // The 5.4.8 gt formula's Multiplier/Value tests went with the
        // SpellScaling/SpellEffectScaling structures (3.3.5a has neither);
        // Task 5 replaces this suite with the CoA CalcValue tests.
        [Theory(Skip = "coa: Task 5")]
        [InlineData(8, 90, 0, 7 * 100 + 89)]      // mage, level 90
        [InlineData(8, 90, 85, 7 * 100 + 84)]     // capped by MaxScalingLevel
        [InlineData(-1, 90, 0, 32 * 100 + 89)]    // MAX_CLASSES - 2 - (-1) = 32 (CoA MAX_CLASSES = 33)
        [InlineData(-2, 90, 0, 33 * 100 + 89)]    // Mana Gem style
        public void GtKey(int cls, int level, int maxLevel, int expected) =>
            Assert.Equal(expected, SpellScalingCalculator.GtKey(cls, level, maxLevel));
    }
}
