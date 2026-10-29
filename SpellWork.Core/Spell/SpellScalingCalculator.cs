using System;
using SpellWork.DBC;

namespace SpellWork.Spell
{
    /// <summary>
    /// 5.4.8 gtSpellScaling key arithmetic, left from the 5.4.8 port. The
    /// 3.3.5a client has no SpellScaling/gtSpellScaling tables (their
    /// structures are gone); Task 5 replaces this class with the CoA
    /// SpellEffectInfo::CalcValue rule (spec section 4.3).
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
    }
}
