// 5.4.8 layout: WoWDBDefs SpellScaling.dbd BUILD 5.4.0.17266-5.4.8.18414 (keyed by Spell.ScalingID, no SpellID column)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellScalingEntry
    {
        [Index(true)] public int ID;
        public int CastTimeMin;
        public int CastTimeMax;
        public int CastTimeMaxLevel;
        public int Class;
        public float NerfFactor;       // SkyFire: CoefBase
        public int NerfMaxLevel;       // SkyFire: CoefLevelBase
        public int MaxScalingLevel;    // SkyFire: MaxLevel
        public int ScalesFromItemLevel;
    }
}
