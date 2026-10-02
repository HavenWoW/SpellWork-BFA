// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellEquippedItemsEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellEquippedItemsEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        public int EquippedItemClass;
        public uint EquippedItemInventoryTypeMask;
        public uint EquippedItemSubClassMask;
    }
}
