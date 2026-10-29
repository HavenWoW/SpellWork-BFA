// CoA 3.3.5a layout: CoA DBCfmt.h SpellCastTimefmt "nixx" (4 fields, 16 bytes); names from WoWDBDefs SpellCastTimes.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellCastTimesEntry
    {
        [Index(true)] public int ID;
        public int Base;
        public int PerLevel;
        public int Minimum;
    }
}
