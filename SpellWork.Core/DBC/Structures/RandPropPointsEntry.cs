// 5.4.8 layout: WoWDBDefs RandPropPoints.dbd BUILD 5.0.1.15464-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class RandPropPointsEntry
    {
        [Index(true)] public int ID;
        [Cardinality(5)] public uint[] Epic = new uint[5];
        [Cardinality(5)] public uint[] Superior = new uint[5];
        [Cardinality(5)] public uint[] Good = new uint[5];
    }
}
