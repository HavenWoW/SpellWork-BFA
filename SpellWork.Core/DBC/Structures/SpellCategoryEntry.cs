// CoA 3.3.5a layout: CoA DBCfmt.h SpellCategoryfmt "ni" (2 fields, 8 bytes); names from WoWDBDefs SpellCategory.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellCategoryEntry
    {
        [Index(true)] public int ID;
        public int Flags;
    }
}
