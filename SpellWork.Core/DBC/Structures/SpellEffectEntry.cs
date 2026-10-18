using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellEffectEntry
    {
        [Index(true)]
        public int ID;
        public short DifficultyID;
        public int EffectIndex;
        public uint Effect;
        public float EffectAmplitude;
        public int EffectAttributes;
        public short EffectAura;
        public int EffectAuraPeriod;
        public int EffectBasePoints;
        public float EffectBonusCoefficient;
        public float EffectChainAmplitude;
        public int EffectChainTargets;
        public int EffectDieSides;
        public int EffectItemType;
        public int EffectMechanic;
        public float EffectPointsPerResource;
        [Column("EffectPos_facing")]
        public float EffectPosFacing;
        public float EffectRealPointsPerLevel;
        public int EffectTriggerSpell;
        public float BonusCoefficientFromAP;
        public float PvpMultiplier;
        public float Coefficient;
        public float Variance;
        public float ResourceCoefficient;
        public float GroupSizeBasePointsCoefficient;
        public float EffectBasePointsF;
        public int Field_5_5_4_67090_025;
        [Cardinality(2)]
        public int[] EffectMiscValue = new int[2];
        [Cardinality(2)]
        public uint[] EffectRadiusIndex = new uint[2];
        [Cardinality(4)]
        public int[] EffectSpellClassMask = new int[4];
        [Cardinality(2)]
        public short[] ImplicitTarget = new short[2];
        public int SpellID;
    }
}
