using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualKitEntry
    {
        // LAYOUT 6D87523A (WoWDBDefs SpellVisualKit.dbd) marks ID
        // "$noninline,id$" -- Index(true), same reasoning as SpellVisualEntry.
        [Index(true)]
        public int ID;
        /// <summary>
        /// WoWDBDefs calls this ClutterLevel; TrinityCore calls the same byte
        /// FallbackPriority (DB2Structure.h:3438). Same width, same slot --
        /// a naming disagreement, not a layout one. WoWDBDefs wins here.
        /// </summary>
        public sbyte ClutterLevel;
        public int FallbackSpellVisualKitID;
        public ushort DelayMin;
        public ushort DelayMax;
        [Cardinality(2)]
        public int[] Flags = new int[2];
    }
}
