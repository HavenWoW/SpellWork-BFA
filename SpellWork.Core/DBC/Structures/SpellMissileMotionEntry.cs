// CoA 3.3.5a layout: WoWDBDefs SpellMissileMotion.dbd 3.3.5.12340 (5 fields, 20 bytes; not loaded by the CoA server)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellMissileMotionEntry
    {
        [Index(true)] public int ID;
        public string Name;
        public string ScriptBody;
        public uint Flags;
        public int MissileCount;
    }
}
