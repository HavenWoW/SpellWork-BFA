// 5.4.8 layout: WoWDBDefs SpellPower.dbd BUILD 5.4.0.17266-5.4.8.18414 (no DifficultyID in 5.4.8; several rows per spell ordered by OrderIndex)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellPowerEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int OrderIndex;
        public int PowerType;
        public int ManaCost;
        public int ManaCostPerLevel;
        public int ManaPerSecond;
        public int PowerDisplayID;
        public int AltPowerBarID;
        public float PowerCostPct;
        public float PowerPctPerSecond;
        public int RequiredAuraSpellID;
        public float Unk12;
    }
}
