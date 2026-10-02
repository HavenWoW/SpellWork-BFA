// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellMissileMotionEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellMissileMotionEntry
    {
        [Index(true)] public int ID;
        public string Name;
        public string Script;
        public uint Flags;
        public uint MissileCount;
    }
}
