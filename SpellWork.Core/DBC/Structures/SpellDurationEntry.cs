// CoA 3.3.5a layout: CoA DBCfmt.h SpellDurationfmt "niii" (4 fields, 16 bytes); names from WoWDBDefs SpellDuration.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellDurationEntry
    {
        [Index(true)] public int ID;
        public int Duration;
        public int DurationPerLevel;
        public int MaxDuration;
    }
}
