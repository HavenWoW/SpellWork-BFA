// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/ItemEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class ItemEntry
    {
        [Index(true)] public int ID;
        public uint Class;
        public uint SubClass;
        public int SoundOverrideSubclass;
        public int Material;
        public uint DisplayId;
        public uint InventoryType;
        public uint Sheath;
    }
}
