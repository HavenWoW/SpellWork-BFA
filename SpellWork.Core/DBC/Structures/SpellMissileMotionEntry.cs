using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellMissileMotionEntry
    {
        // LAYOUT 6B78A45B, A61A5983, A76EF553 (WoWDBDefs SpellMissileMotion.dbd)
        // marks ID "$noninline,id$" -- Index(true). FieldsCount=4: Name,
        // ScriptBody, Flags, MissileCount, in that order.
        [Index(true)]
        public int ID;
        public string Name;
        public string ScriptBody;
        public byte Flags;
        public byte MissileCount;
    }
}
