using System;
using SpellWork.DBC;
using SpellWork.DBC.Structures;

namespace SpellWork.Spell
{
    /// <summary>
    /// Port of SkyFire_548 SpellEffectInfo::CalcValue (SpellInfo.cpp): the
    /// gtSpellScaling-based effect scaling used by the 5.4.8 client data.
    /// Pure functions over the scaling rows and a gt lookup so the formula is
    /// testable without client data.
    /// </summary>
    public static class SpellScalingCalculator
    {
        /// <summary>
        /// gtSpellScaling row key. Class &lt;= -1 selects the generic columns
        /// (MAX_CLASSES - 2 - class); MaxScalingLevel caps the level.
        /// </summary>
        public static int GtKey(int scalingClass, int level, int maxScalingLevel)
        {
            var gtKeyBase = scalingClass <= -1 ? DBC.DBC.MaxClasses - 2 - scalingClass : scalingClass - 1;
            var levelForKey = maxScalingLevel != 0 ? Math.Min(maxScalingLevel, level) : level;
            return gtKeyBase * 100 + levelForKey - 1;
        }

        /// <summary>The level multiplier, or 0 when the gt row is missing.</summary>
        public static float Multiplier(SpellScalingEntry scaling, int level, Func<int, float?> gtLookup)
        {
            var gt = gtLookup(GtKey(scaling.Class, level, scaling.MaxScalingLevel));
            if (gt == null)
                return 0f;

            var multiplier = gt.Value;
            if (scaling.CastTimeMax > 0 && scaling.CastTimeMaxLevel > level)
            {
                // Integer arithmetic in the numerator, as in the C++.
                var castTime = scaling.CastTimeMin + (level - 1) * (scaling.CastTimeMax - scaling.CastTimeMin) / (scaling.CastTimeMaxLevel - 1);
                multiplier *= (float)castTime / scaling.CastTimeMax;
            }

            if (scaling.NerfMaxLevel > level)
                multiplier *= (1.0f - scaling.NerfFactor) * (level - 1) / (scaling.NerfMaxLevel - 1) + scaling.NerfFactor;

            return multiplier;
        }

        public static (float Base, float Min, float Max, float PerComboPoint) Value(
            SpellScalingEntry scaling, SpellEffectScalingEntry effectScaling, int level, Func<int, float?> gtLookup)
        {
            var multiplier = Multiplier(scaling, level, gtLookup);
            var baseValue = effectScaling.Coefficient * multiplier;
            var delta = effectScaling.Variance * effectScaling.Coefficient * multiplier * 0.5f;
            return (baseValue, baseValue - delta, baseValue + delta, effectScaling.ResourceCoefficient * multiplier);
        }
    }
}
