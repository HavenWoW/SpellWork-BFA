// CoA 3.3.5a layout: WoWDBDefs SpellVisualEffectName.dbd 3.3.5.12340 (7 fields, 28 bytes; not loaded by the CoA server)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEffectNameEntry
    {
        [Index(true)] public int ID;
        public string Name;
        public string FileName;
        public float AreaEffectSize;
        public float Scale;
        public float MinAllowedScale;
        public float MaxAllowedScale;
    }
}
