// CoA 3.3.5a layout: CoA DBCfmt.h OverrideSpellDatafmt (12 fields, 48 bytes); names from WoWDBDefs OverrideSpellData.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class OverrideSpellDataEntry
    {
        [Index(true)] public int ID;
        [Cardinality(10)] public int[] Spells = new int[10];
        public int Flags;
    }
}
