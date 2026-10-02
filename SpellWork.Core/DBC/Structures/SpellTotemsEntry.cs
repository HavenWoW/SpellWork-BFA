// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellTotemsEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellTotemsEntry
    {
        [Index(true)] public int ID;
        [Cardinality(2)] public uint[] TotemCategory = new uint[2];
        [Cardinality(2)] public uint[] Totem = new uint[2];
    }
}
