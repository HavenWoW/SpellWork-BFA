// CoA 3.3.5a layout: CoA DBCfmt.h SpellShapeshiftFormEntryfmt (35 fields, 140 bytes); names from WoWDBDefs SpellShapeshiftForm.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellShapeshiftFormEntry
    {
        [Index(true)] public int ID;
        public int BonusActionBar;
        [Cardinality(16)] public string[] NameLang = new string[16];
        public uint NameLangFlags;
        public int Flags;
        public int CreatureType;
        public int AttackIconID;
        public int CombatRoundTime;
        [Cardinality(4)] public int[] CreatureDisplayID = new int[4];
        [Cardinality(8)] public int[] PresetSpellID = new int[8];

        public string Name => LocaleSlots.Pick(NameLang);
    }
}
