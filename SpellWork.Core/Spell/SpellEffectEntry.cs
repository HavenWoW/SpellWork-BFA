namespace SpellWork.Spell
{
    /// <summary>
    /// One effect slot (0-2) of a 3.3.5 <see cref="DBC.Structures.SpellEntry"/>:
    /// the indexed column groups read at one index. Not a client table and
    /// not a store -- 3.3.5 has no SpellEffect file; <see cref="SpellInfo"/>
    /// builds one of these per non-zero <c>Effect[i]</c>.
    /// </summary>
    public sealed class SpellEffectEntry
    {
        public int SpellID;
        public int EffectIndex;
        public int Effect;
        public int EffectDieSides;
        public float EffectRealPointsPerLevel;
        public int EffectBasePoints;
        public int EffectMechanic;
        /// <summary>[0] ImplicitTargetA, [1] ImplicitTargetB.</summary>
        public int[] ImplicitTarget = new int[2];
        public int EffectRadiusIndex;
        public int EffectAura;
        public int EffectAuraPeriod;
        /// <summary>WoWDBDefs EffectAmplitude (server: EffectValueMultiplier).</summary>
        public float EffectAmplitude;
        public int EffectChainTargets;
        public int EffectItemType;
        /// <summary>[0] EffectMiscValue, [1] EffectMiscValueB.</summary>
        public int[] EffectMiscValue = new int[2];
        public int EffectTriggerSpell;
        public float EffectPointsPerCombo;
        /// <summary>The effect's three SpellClassMask words (CoA server flag96 layout).</summary>
        public uint[] EffectSpellClassMask = new uint[3];
        /// <summary>WoWDBDefs EffectChainAmplitude (server: EffectDamageMultiplier).</summary>
        public float EffectChainAmplitude;
        /// <summary>WoWDBDefs EffectBonusCoefficient (server: EffectBonusMultiplier).</summary>
        public float EffectBonusCoefficient;

        /// <summary>Effect slot <paramref name="index"/> of <paramref name="row"/>.</summary>
        public static SpellEffectEntry FromSpell(DBC.Structures.SpellEntry row, int index) => new SpellEffectEntry
        {
            SpellID = row.ID,
            EffectIndex = index,
            Effect = row.Effect[index],
            EffectDieSides = row.EffectDieSides[index],
            EffectRealPointsPerLevel = row.EffectRealPointsPerLevel[index],
            EffectBasePoints = row.EffectBasePoints[index],
            EffectMechanic = row.EffectMechanic[index],
            ImplicitTarget = new[] { row.ImplicitTargetA[index], row.ImplicitTargetB[index] },
            EffectRadiusIndex = row.EffectRadiusIndex[index],
            EffectAura = row.EffectAura[index],
            EffectAuraPeriod = row.EffectAuraPeriod[index],
            EffectAmplitude = row.EffectAmplitude[index],
            EffectChainTargets = row.EffectChainTargets[index],
            EffectItemType = row.EffectItemType[index],
            EffectMiscValue = new[] { row.EffectMiscValue[index], row.EffectMiscValueB[index] },
            EffectTriggerSpell = row.EffectTriggerSpell[index],
            EffectPointsPerCombo = row.EffectPointsPerCombo[index],
            EffectSpellClassMask = (uint[])row.EffectClassMask(index).Clone(),
            EffectChainAmplitude = row.EffectChainAmplitude[index],
            EffectBonusCoefficient = row.EffectBonusCoefficient[index],
        };
    }
}
