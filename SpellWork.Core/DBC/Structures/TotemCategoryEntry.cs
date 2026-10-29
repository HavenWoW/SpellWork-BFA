// CoA 3.3.5a layout: CoA DBCfmt.h TotemCategoryEntryfmt (20 fields, 80 bytes); names from WoWDBDefs TotemCategory.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class TotemCategoryEntry
    {
        [Index(true)] public int ID;
        [Cardinality(16)] public string[] NameLang = new string[16];
        public uint NameLangFlags;
        public int TotemCategoryType;
        public uint TotemCategoryMask;

        public string Name => LocaleSlots.Pick(NameLang);
    }
}
