// CoA 3.3.5a layout: CoA DBCfmt.h SpellFocusObjectfmt (18 fields, 72 bytes); names from WoWDBDefs SpellFocusObject.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellFocusObjectEntry
    {
        [Index(true)] public int ID;
        [Cardinality(16)] public string[] NameLang = new string[16];
        public uint NameLangFlags;

        public string Name => LocaleSlots.Pick(NameLang);
    }
}
