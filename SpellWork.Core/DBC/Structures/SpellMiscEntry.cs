using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellMiscEntry
    {
        [Index(true)]
        public int ID;
        [Cardinality(17)]
        public int[] Attributes = new int[17];
        public short DifficultyID;
        public ushort CastingTimeIndex;
        public ushort DurationIndex;
        public ushort RangeIndex;
        public byte SchoolMask;
        public float Speed;
        public float LaunchDelay;
        public float MinDuration;
        public int SpellIconFileDataID;
        public int ActiveIconFileDataID;
        public int ContentTuningID;
        public int ShowFutureSpellPlayerConditionID;
        public int SpellID;
    }
}
