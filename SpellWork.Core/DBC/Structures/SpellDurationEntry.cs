// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellDurationEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellDurationEntry
    {
        [Index(true)] public int ID;
        public int Duration;
        public int Unknown2;
        public int MaxDuration;
    }
}
