using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class DifficultyEntry
    {
        [Index(true)]
        public int ID;
        public string Name;
        public byte InstanceType;
        public byte OrderIndex;
        public sbyte OldEnumValue;
        public short FallbackDifficultyID;
        public byte MinPlayers;
        public byte MaxPlayers;
        public int Flags;
        public byte ItemContext;
        public short ToggleDifficultyID;
        public ushort GroupSizeHealthCurveID;
        public ushort GroupSizeDmgCurveID;
        public ushort GroupSizeSpellPointsCurveID;
        public int Field_1_15_4_56400_013;
    }
}
