// 5.4.8 layout: WoWDBDefs SpellEffect.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellEffectEntry
    {
        [Index(true)] public int ID;
        public int DifficultyID;
        public int Effect;
        public float EffectAmplitude;
        public int EffectAura;
        public int EffectAuraPeriod;
        public int EffectBasePoints;
        public float EffectBonusCoefficient;
        public float EffectChainAmplitude;
        public int EffectChainTargets;
        public int EffectDieSides;
        public int EffectItemType;
        public int EffectMechanic;
        [Cardinality(2)] public int[] EffectMiscValue = new int[2];
        public float EffectPointsPerResource;
        [Cardinality(2)] public int[] EffectRadiusIndex = new int[2];
        public float EffectRealPointsPerLevel;
        [Cardinality(4)] public uint[] EffectSpellClassMask = new uint[4];
        public int EffectTriggerSpell;
        public float EffectPosFacing;
        [Cardinality(2)] public int[] ImplicitTarget = new int[2];
        public int SpellID;
        public int EffectIndex;
        public int EffectAttributes;
    }
}
