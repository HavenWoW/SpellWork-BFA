// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellCastTimesEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellCastTimesEntry
    {
        [Index(true)] public int ID;
        public int CastTime;
        public int CastTimePerLevel;
        public int MinCastTime;
    }
}
