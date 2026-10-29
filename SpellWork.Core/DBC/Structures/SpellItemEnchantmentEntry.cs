// CoA 3.3.5a layout: CoA DBCfmt.h SpellItemEnchantmentfmt (38 fields, 152 bytes); names from WoWDBDefs SpellItemEnchantment.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellItemEnchantmentEntry
    {
        [Index(true)] public int ID;
        public int Charges;
        [Cardinality(3)] public int[] Effect = new int[3];
        [Cardinality(3)] public int[] EffectPointsMin = new int[3];
        [Cardinality(3)] public int[] EffectPointsMax = new int[3];
        [Cardinality(3)] public int[] EffectArg = new int[3];
        [Cardinality(16)] public string[] NameLang = new string[16];
        public uint NameLangFlags;
        public int ItemVisual;
        public int Flags;
        public int SrcItemID;
        public int ConditionID;
        public int RequiredSkillID;
        public int RequiredSkillRank;
        public int MinLevel;

        public string Name => LocaleSlots.Pick(NameLang);
    }
}
