// 5.4.8 layout: WoWDBDefs SpellEffectScaling.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellEffectScalingEntry
    {
        [Index(true)] public int ID;
        public float Coefficient;          // SkyFire: Multiplier
        public float Variance;             // SkyFire: RandomPointsMultiplier
        public float ResourceCoefficient;  // SkyFire: OtherMultiplier (combo)
        public float Unk4;
        public int SpellEffectID;
    }
}
