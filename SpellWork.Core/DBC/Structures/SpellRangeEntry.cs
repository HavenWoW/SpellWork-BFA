// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellRangeEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellRangeEntry
    {
        [Index(true)] public int ID;
        public float MinRangeNegative;
        public float MinRangePositive;
        public float MaxRangeNegative;
        public float MaxRangePositive;
        public uint Flags;
        public string Name;
        public string Name2;
    }
}
