// 5.4.8 layout: WoWDBDefs SpellVisualEffectName.dbd BUILD 5.4.0.17266-5.4.8.18414 (WDB2 packs Type into one byte + 3 padding bytes: 9 fields, 36 bytes)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEffectNameEntry
    {
        [Index(true)] public int ID;
        public string Name;
        public float AreaEffectSize;
        public float Scale;
        public float MinAllowedScale;
        public float MaxAllowedScale;
        public byte Type;
        [Cardinality(3)] public byte[] Padding = new byte[3];
        public float Alpha;
        public int Flags;
    }
}
