// 5.4.8 layout: WoWDBDefs Difficulty.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class DifficultyEntry
    {
        [Index(true)] public int ID;
        public int FallbackDifficultyID;
        public int InstanceType;
        public int MinPlayers;
        public int MaxPlayers;
        public int OldEnumValue;
        public int Flags;
        public int ToggleDifficultyID;
        public int GroupSizeHealthCurveID;
        public int GroupSizeDmgCurveID;
        public int GroupSizeSpellPointsCurveID;
        public string Name;
    }
}
