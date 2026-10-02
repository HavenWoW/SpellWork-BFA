// 5.4.8 layout: WoWDBDefs Spell.dbd BUILD 5.4.0.17266-5.4.8.18414 (strings unlocalized in WDBC)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellEntry
    {
        [Index(true)] public int ID;
        public string Name;
        public string NameSubtext;
        public string Description;
        public string AuraDescription;
        public int RuneCostID;
        public int SpellMissileID;
        public int DescriptionVariablesID;
        public int DifficultyID;          // WoWDBDefs: BonusCoefficient float; upstream: SpellDifficultyId. Read as int; unused.
        public int ScalingID;
        public int AuraOptionsID;
        public int AuraRestrictionsID;
        public int CastingRequirementsID;
        public int CategoriesID;
        public int ClassOptionsID;
        public int CooldownsID;
        public int EquippedItemsID;
        public int InterruptsID;
        public int LevelsID;
        public int ReagentsID;
        public int ShapeshiftID;
        public int TargetRestrictionsID;
        public int TotemsID;
        public int RequiredProjectID;
        public int MiscID;
    }
}
