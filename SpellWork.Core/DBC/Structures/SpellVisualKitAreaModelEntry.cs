// CoA 3.3.5a layout: WoWDBDefs SpellVisualKitAreaModel.dbd 3.3.5.12340 (3 fields, 12 bytes; not loaded by the CoA server; no parent-kit column, so not chained)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualKitAreaModelEntry
    {
        [Index(true)] public int ID;
        /// <summary>Model path (the reference data holds .mdx/.m2 paths).</summary>
        public string Name;
        public int EnumID;
    }
}
