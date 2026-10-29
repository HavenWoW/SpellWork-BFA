// CoA 3.3.5a layout: CoA DBCfmt.h SkillLinefmt (56 fields, 224 bytes); names from WoWDBDefs SkillLine.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SkillLineEntry
    {
        [Index(true)] public int ID;
        public int CategoryID;
        public int SkillCostsID;
        [Cardinality(16)] public string[] DisplayNameLang = new string[16];
        public uint DisplayNameLangFlags;
        [Cardinality(16)] public string[] DescriptionLang = new string[16];
        public uint DescriptionLangFlags;
        public int SpellIconID;
        [Cardinality(16)] public string[] AlternateVerbLang = new string[16];
        public uint AlternateVerbLangFlags;
        public int CanLink;

        public string DisplayName => LocaleSlots.Pick(DisplayNameLang);
    }
}
