// 5.4.8 layout: WoWDBDefs SpellShapeshift.dbd BUILD 5.0.1.15464-5.4.8.18414 (upstream 5.4.8 declares two ulong masks; DBFileReaderLib's WDBC reader has no ulong, so each is two 32-bit words, low word first)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellShapeshiftEntry
    {
        [Index(true)] public int ID;
        [Cardinality(2)] public uint[] ShapeshiftExclude = new uint[2];
        [Cardinality(2)] public uint[] ShapeshiftMask = new uint[2];
        public int StanceBarOrder;
    }
}
