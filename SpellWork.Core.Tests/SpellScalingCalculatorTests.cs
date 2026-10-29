using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // Hand-computed from CoA SpellEffectInfo::CalcValue (SpellInfo.cpp); the
    // arithmetic is in each case's comment.
    public sealed class SpellScalingCalculatorTests
    {
        [Fact]
        public void DieOfOne_AddsOne_AndSkipsLevelTermWithoutRealPointsPerLevel()
        {
            // rppl 0: level term skipped. die 1: basePoints + 1 = 10.
            var v = SpellScalingCalculator.Value(9, 1, 0f, 0f, 0, 0, 0, 80);
            Assert.Equal((10f, 10f, 10f, 0f), v);
        }

        [Fact]
        public void LevelClampedToMaxLevel_ThenDieRange()
        {
            // level 80 > MaxLevel 30 -> 30; 30 - max(BaseLevel 0, SpellLevel 20) = 10;
            // 99 + (int)(10 * 2.5) = 124; die 21 rolls 1..21 -> 125..145, Base = 125.
            var v = SpellScalingCalculator.Value(99, 21, 2.5f, 0f, 20, 0, 30, 80);
            Assert.Equal((125f, 125f, 145f, 0f), v);
        }

        [Fact]
        public void LevelRaisedToBaseLevel_WhenBelowIt()
        {
            // level 5 < BaseLevel 10 -> 10; 10 - max(10, 4) = 0; no level contribution.
            var v = SpellScalingCalculator.Value(50, 0, 3f, 0f, 4, 10, 0, 5);
            Assert.Equal((50f, 50f, 50f, 0f), v);
        }

        [Fact]
        public void ClampIsElseIf_BaseLevelAboveMaxLevelStillUsesMaxLevel()
        {
            // level 50 > MaxLevel 20 -> 20 (the BaseLevel clamp is not applied after);
            // 20 - max(30, 10) = -10; 100 + (int)(-10 * 2) = 80.
            var v = SpellScalingCalculator.Value(100, 0, 2f, 0f, 10, 30, 20, 50);
            Assert.Equal(80f, v.Base);
        }

        [Fact]
        public void MaxLevelZero_MeansNoUpperClamp()
        {
            // 100 - 20 = 80 levels * 0.5 = 40 -> 10 + 40 = 50.
            var v = SpellScalingCalculator.Value(10, 0, 0.5f, 0f, 20, 0, 0, 100);
            Assert.Equal(50f, v.Base);
        }

        [Fact]
        public void LevelProductTruncatesTowardZero()
        {
            // (int)(3 * 0.6f) = 1 ; (int)(-3 * 0.6f) = -1.
            Assert.Equal(11f, SpellScalingCalculator.Value(10, 0, 0.6f, 0f, 0, 0, 0, 3).Base);
            Assert.Equal(9f, SpellScalingCalculator.Value(10, 0, 0.6f, 0f, 5, 0, 0, 2).Base);
        }

        [Fact]
        public void NegativeDie_RollsDieToOne()
        {
            // die -5: irand(-5, 1) -> 95..101; Base is the roll of 1 = 101.
            var v = SpellScalingCalculator.Value(100, -5, 0f, 0f, 0, 0, 0, 80);
            Assert.Equal((101f, 95f, 101f, 0f), v);
        }

        [Fact]
        public void ComboPointsReportedSeparately()
        {
            var v = SpellScalingCalculator.Value(10, 0, 0f, 7.5f, 0, 0, 0, 80);
            Assert.Equal((10f, 10f, 10f, 7.5f), v);
        }
    }
}
