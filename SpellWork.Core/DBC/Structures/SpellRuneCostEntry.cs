// CoA 3.3.5a layout: CoA DBCfmt.h SpellRuneCostfmt "niiii" (5 fields, 20 bytes); names from WoWDBDefs SpellRuneCost.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellRuneCostEntry
    {
        [Index(true)] public int ID;
        /// <summary>Blood, Unholy, Frost.</summary>
        [Cardinality(3)] public int[] RuneCost = new int[3];
        public int RunicPower;
    }
}
