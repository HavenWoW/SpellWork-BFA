// 5.4.8 layout: WoWDBDefs SpellMisc.dbd BUILD 5.4.0.17266-5.4.8.18414 (the 5.4.8 file has 14 attribute words, 26 fields; the WoWDBDefs 5.4.7 block has 13)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellMiscEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        [Cardinality(14)] public uint[] Attributes = new uint[14];
        public int CastingTimeIndex;
        public int DurationIndex;
        public int RangeIndex;
        public float Speed;
        [Cardinality(2)] public int[] SpellVisualID = new int[2];
        public int SpellIconID;
        public int ActiveIconID;
        public int SchoolMask;
    }
}
