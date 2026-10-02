// 5.4.8 layout: WoWDBDefs gtSpellScaling.dbd BUILD 5.4.0.17266-5.4.8.18414 (row key = (class column) * 100 + level - 1)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class GtSpellScalingEntry
    {
        [Index(true)] public int ID;
        public float Value;
    }
}
