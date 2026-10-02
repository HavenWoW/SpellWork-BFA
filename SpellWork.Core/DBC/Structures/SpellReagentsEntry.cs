// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellReagentsEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellReagentsEntry
    {
        [Index(true)] public int ID;
        [Cardinality(9)] public int[] ItemId = new int[9];
        [Cardinality(9)] public uint[] Count = new uint[9];
    }
}
