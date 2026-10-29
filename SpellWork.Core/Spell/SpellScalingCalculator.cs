using System;

namespace SpellWork.Spell
{
    /// <summary>
    /// The CoA (AzerothCore 3.3.5a) SpellEffectInfo::CalcValue rule
    /// (src/server/game/Spells/SpellInfo.cpp), minus the caster-only terms
    /// (script hooks, ApplyEffectModifiers, creature-level scaling) that need a
    /// live Unit. The caster level is the level argument.
    /// </summary>
    public static class SpellScalingCalculator
    {
        /// <summary>
        /// CoA CalcValue, recorded from the source:
        ///   1. Only when RealPointsPerLevel != 0: level = casterLevel;
        ///      if (level &gt; MaxLevel &amp;&amp; MaxLevel &gt; 0) level = MaxLevel;
        ///      else if (level &lt; BaseLevel) level = BaseLevel (the two clamps are
        ///      an if / else-if, never both);
        ///      level -= max(BaseLevel, SpellLevel);
        ///      basePoints += (int)(level * RealPointsPerLevel)  (float product
        ///      truncated toward zero).
        ///   2. Die: DieSides 0 adds nothing; 1 adds 1; otherwise irand(1, d) for
        ///      d &gt;= 1 and irand(d, 1) for d &lt; 0, so the range is 1..d or d..1.
        ///   3. PointsPerComboPoint is added per combo point on top; it is
        ///      reported separately here, not folded into the value.
        /// Base is the roll of 1 (basePoints + 1 whenever DieSides != 0), which
        /// is the low end for a positive die and the high end for a negative
        /// one; Min and Max are the extremes of the roll.
        /// </summary>
        public static (float Base, float Min, float Max, float PerComboPoint) Value(
            int basePoints, int dieSides, float realPointsPerLevel, float pointsPerComboPoint,
            int spellLevel, int baseLevel, int maxLevel, int casterLevel)
        {
            if (realPointsPerLevel != 0.0f)
            {
                var level = casterLevel;
                if (level > maxLevel && maxLevel > 0)
                    level = maxLevel;
                else if (level < baseLevel)
                    level = baseLevel;

                level -= Math.Max(baseLevel, spellLevel);
                basePoints += (int)(level * realPointsPerLevel);
            }

            if (dieSides == 0)
                return (basePoints, basePoints, basePoints, pointsPerComboPoint);

            var roll1 = basePoints + 1;
            var rollDie = basePoints + dieSides;
            return (roll1, Math.Min(roll1, rollDie), Math.Max(roll1, rollDie), pointsPerComboPoint);
        }
    }
}
