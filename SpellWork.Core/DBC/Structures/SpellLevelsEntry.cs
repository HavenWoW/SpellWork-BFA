// 5.4.8 layout: WoWDBDefs SpellLevels.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellLevelsEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        public int BaseLevel;
        public int MaxLevel;
        public int SpellLevel;
    }
}
