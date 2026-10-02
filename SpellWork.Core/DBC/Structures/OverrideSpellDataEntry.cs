// 5.4.8 layout: WoWDBDefs OverrideSpellData.dbd BUILD 5.0.1.15464-5.4.8.18414 (upstream 5.4.8 declares a string in the last column; the file has none)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class OverrideSpellDataEntry
    {
        [Index(true)] public int ID;
        [Cardinality(10)] public uint[] Spells = new uint[10];
        public int Flags;
        public int PlayerActionbarFileDataID;
    }
}
