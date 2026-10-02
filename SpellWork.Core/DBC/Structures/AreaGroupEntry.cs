// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/AreaGroupEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class AreaGroupEntry
    {
        [Index(true)] public int ID;
        [Cardinality(6)] public uint[] AreaId = new uint[6];
        public uint NextGroup;
    }
}
