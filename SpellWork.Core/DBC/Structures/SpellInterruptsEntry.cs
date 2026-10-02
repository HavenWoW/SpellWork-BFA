// 5.4.8 layout: WoWDBDefs SpellInterrupts.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellInterruptsEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        [Cardinality(2)] public int[] AuraInterruptFlags = new int[2];
        [Cardinality(2)] public int[] ChannelInterruptFlags = new int[2];
        public int InterruptFlags;
    }
}
