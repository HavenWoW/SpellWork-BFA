// CoA 3.3.5a layout: CoA DBCfmt.h Itemfmt "niiiiiii" (8 fields, 32 bytes); names from WoWDBDefs Item.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class ItemEntry
    {
        [Index(true)] public int ID;
        public int ClassID;
        public int SubclassID;
        public int SoundOverrideSubclassID;
        public int Material;
        public int DisplayInfoID;
        public int InventoryType;
        public int SheatheType;
    }
}
