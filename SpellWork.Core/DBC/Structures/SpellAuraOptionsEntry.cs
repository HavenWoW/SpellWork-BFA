// 5.4.8 layout: WoWDBDefs SpellAuraOptions.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellAuraOptionsEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        public int CumulativeAura;
        public int ProcChance;
        public int ProcCharges;
        public int ProcTypeMask;
        public int ProcCategoryRecovery;
        public int SpellProcsPerMinuteID;
    }
}
