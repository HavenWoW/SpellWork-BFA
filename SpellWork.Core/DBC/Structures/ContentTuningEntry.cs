using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class ContentTuningEntry
    {
        [Index(false)]
        public uint ID;
        public int MinLevel;
        public int MaxLevel;
        public int Flags;
        public int ExpansionID;
    }
}
