using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellProcsPerMinuteEntry
    {
        [Index(true)]
        public uint ID;
        public float BaseProcRate;
        public byte Flags;
    }
}
