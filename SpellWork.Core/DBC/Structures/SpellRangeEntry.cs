using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellRangeEntry
    {
        [Index(true)]
        public int ID;
        public string DisplayName;
        public string DisplayNameShort;
        public int Flags;
        [Cardinality(2)]
        public float[] RangeMin = new float[2];
        [Cardinality(2)]
        public float[] RangeMax = new float[2];
    }
}
