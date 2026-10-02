// 5.4.8 layout: WoWDBDefs SpellTargetRestrictions.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellTargetRestrictionsEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        public float ConeAngle;
        public float Width;
        public int MaxTargets;
        public int MaxTargetLevel;
        public int TargetCreatureType;
        public int Targets;
    }
}
