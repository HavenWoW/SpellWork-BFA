// CoA 3.3.5a layout: CoA DBCfmt.h SpellRangefmt (40 fields, 160 bytes); names from WoWDBDefs SpellRange.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellRangeEntry
    {
        [Index(true)] public int ID;
        /// <summary>[0] hostile, [1] friendly.</summary>
        [Cardinality(2)] public float[] RangeMin = new float[2];
        /// <summary>[0] hostile, [1] friendly.</summary>
        [Cardinality(2)] public float[] RangeMax = new float[2];
        public int Flags;
        [Cardinality(16)] public string[] DisplayNameLang = new string[16];
        public uint DisplayNameLangFlags;
        [Cardinality(16)] public string[] DisplayNameShortLang = new string[16];
        public uint DisplayNameShortLangFlags;

        public string DisplayName => LocaleSlots.Pick(DisplayNameLang);
        public string DisplayNameShort => LocaleSlots.Pick(DisplayNameShortLang);
    }
}
