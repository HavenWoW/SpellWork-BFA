// CoA 3.3.5a layout: WoWDBDefs SpellIcon.dbd 3.3.5.12340 (2 fields, 8 bytes; not loaded by the CoA server)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellIconEntry
    {
        [Index(true)] public int ID;
        public string TextureFilename;
    }
}
