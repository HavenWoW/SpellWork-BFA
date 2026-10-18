using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellDurationEntry
    {
        [Index(true)]
        public int ID;
        public int Duration;
        public uint DurationPerLevel;
        public int MaxDuration;
    }
}
