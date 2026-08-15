using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public class SpellEquippedItemsEntry
    {
        [Index(true)]
        public uint ID;
        public int SpellID;
        public sbyte EquippedItemClass;
        public int EquippedItemInvTypes;
        public int EquippedItemSubclass;
    }
}
