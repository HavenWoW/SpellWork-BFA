// 5.4.8 layout: WoWDBDefs SpellProcsPerMinute.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellProcsPerMinuteEntry
    {
        [Index(true)] public int ID;
        public float BaseProcRate;
        public int Flags;
    }
}
