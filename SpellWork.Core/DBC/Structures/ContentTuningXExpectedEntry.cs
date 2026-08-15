using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public class ContentTuningXExpectedEntry
    {
        [Index(true)]
        public uint ID;
        public int ExpectedStatModID;
        public int MythicPlusSeasonID;
        public uint ContentTuningID;
    }
}
