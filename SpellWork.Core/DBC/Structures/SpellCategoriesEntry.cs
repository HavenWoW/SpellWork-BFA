// 5.4.8 layout: WoWDBDefs SpellCategories.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellCategoriesEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        public int Category;
        public int DefenseType;
        public int DispelType;
        public int Mechanic;
        public int PreventionType;
        public int StartRecoveryCategory;
        public int ChargeCategory;
    }
}
