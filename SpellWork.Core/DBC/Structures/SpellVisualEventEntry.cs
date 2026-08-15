using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEventEntry
    {
        // LAYOUT 8352EE58, A85282AD (WoWDBDefs SpellVisualEvent.dbd) marks ID
        // "$noninline,id$" -- Index(true), same reasoning as SpellVisualEntry.
        [Index(true)]
        public int ID;
        public int StartEvent;
        public int EndEvent;
        public int StartMinOffsetMs;
        public int StartMaxOffsetMs;
        public int EndMinOffsetMs;
        public int EndMaxOffsetMs;
        public int TargetType;
        public int SpellVisualKitID;
        // "$noninline,relation$SpellVisualID<32>" -- the parent SpellVisual
        // id. Stored in the file's relationship section, not the row's
        // inline bytes, but still a plain field with no attribute: compare
        // SpellXSpellVisualEntry.SpellID, the same pattern for that table's
        // relation column.
        public int SpellVisualID;
    }
}
