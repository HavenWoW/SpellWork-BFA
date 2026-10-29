// CoA 3.3.5a layout: CoA DBCfmt.h SpellEntryfmt (234 fields, 936 bytes);
// names from WoWDBDefs Spell.dbd BUILD 3.3.5.12340. Every column the CoA
// server skips ('x') is still a field here: WDBC rows are read positionally.
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellEntry
    {
        [Index(true)] public int ID;                                                    // 0
        public int Category;                                                            // 1
        public int DispelType;                                                          // 2
        public int Mechanic;                                                            // 3
        /// <summary>Attributes, AttributesEx .. AttributesExG (columns 4-11), word 0-7.</summary>
        [Cardinality(8)] public uint[] Attributes = new uint[8];                        // 4-11
        [Cardinality(2)] public uint[] ShapeshiftMask = new uint[2];                    // 12-13
        [Cardinality(2)] public uint[] ShapeshiftExclude = new uint[2];                 // 14-15
        public uint Targets;                                                            // 16
        public uint TargetCreatureType;                                                 // 17
        public int RequiresSpellFocus;                                                  // 18
        public uint FacingCasterFlags;                                                  // 19
        public int CasterAuraState;                                                     // 20
        public int TargetAuraState;                                                     // 21
        public int ExcludeCasterAuraState;                                              // 22
        public int ExcludeTargetAuraState;                                              // 23
        public int CasterAuraSpell;                                                     // 24
        public int TargetAuraSpell;                                                     // 25
        public int ExcludeCasterAuraSpell;                                              // 26
        public int ExcludeTargetAuraSpell;                                              // 27
        public int CastingTimeIndex;                                                    // 28
        public int RecoveryTime;                                                        // 29
        public int CategoryRecoveryTime;                                                // 30
        public uint InterruptFlags;                                                     // 31
        public uint AuraInterruptFlags;                                                 // 32
        public uint ChannelInterruptFlags;                                              // 33
        public uint ProcTypeMask;                                                       // 34
        public int ProcChance;                                                          // 35
        public int ProcCharges;                                                         // 36
        public int MaxLevel;                                                            // 37
        public int BaseLevel;                                                           // 38
        public int SpellLevel;                                                          // 39
        public int DurationIndex;                                                       // 40
        public int PowerType;                                                           // 41
        public int ManaCost;                                                            // 42
        public int ManaCostPerLevel;                                                    // 43
        public int ManaPerSecond;                                                       // 44
        public int ManaPerSecondPerLevel;                                               // 45
        public int RangeIndex;                                                          // 46
        public float Speed;                                                             // 47
        public int ModalNextSpell;                                                      // 48
        public int CumulativeAura;                                                      // 49
        [Cardinality(2)] public int[] Totem = new int[2];                               // 50-51
        [Cardinality(8)] public int[] Reagent = new int[8];                             // 52-59
        [Cardinality(8)] public int[] ReagentCount = new int[8];                        // 60-67
        public int EquippedItemClass;                                                   // 68
        public int EquippedItemSubclass;                                                // 69
        public int EquippedItemInvTypes;                                                // 70
        [Cardinality(3)] public int[] Effect = new int[3];                              // 71-73
        [Cardinality(3)] public int[] EffectDieSides = new int[3];                      // 74-76
        [Cardinality(3)] public float[] EffectRealPointsPerLevel = new float[3];        // 77-79
        [Cardinality(3)] public int[] EffectBasePoints = new int[3];                    // 80-82
        [Cardinality(3)] public int[] EffectMechanic = new int[3];                      // 83-85
        [Cardinality(3)] public int[] ImplicitTargetA = new int[3];                     // 86-88
        [Cardinality(3)] public int[] ImplicitTargetB = new int[3];                     // 89-91
        [Cardinality(3)] public int[] EffectRadiusIndex = new int[3];                   // 92-94
        [Cardinality(3)] public int[] EffectAura = new int[3];                          // 95-97
        [Cardinality(3)] public int[] EffectAuraPeriod = new int[3];                    // 98-100
        [Cardinality(3)] public float[] EffectAmplitude = new float[3];                 // 101-103 (server: EffectValueMultiplier)
        [Cardinality(3)] public int[] EffectChainTargets = new int[3];                  // 104-106
        [Cardinality(3)] public int[] EffectItemType = new int[3];                      // 107-109
        [Cardinality(3)] public int[] EffectMiscValue = new int[3];                     // 110-112
        [Cardinality(3)] public int[] EffectMiscValueB = new int[3];                    // 113-115
        [Cardinality(3)] public int[] EffectTriggerSpell = new int[3];                  // 116-118
        [Cardinality(3)] public float[] EffectPointsPerCombo = new float[3];            // 119-121
        // Columns 122-130 under WoWDBDefs' names. The CoA server reads them as
        // flag96 EffectSpellClassMask[3] (effect-major): effect i's three mask
        // words are columns 122+3i .. 124+3i, which here is A for effect 0, B
        // for effect 1, C for effect 2 -- see SpellEntry.EffectClassMask. The
        // reference data agrees (5328 single-effect rows carry mask words only
        // in columns 122-124; WoWDBDefs' A[i]/B[i]/C[i] reading would leave
        // those as masks of effects that do not exist).
        [Cardinality(3)] public uint[] EffectSpellClassMaskA = new uint[3];             // 122-124
        [Cardinality(3)] public uint[] EffectSpellClassMaskB = new uint[3];             // 125-127
        [Cardinality(3)] public uint[] EffectSpellClassMaskC = new uint[3];             // 128-130
        [Cardinality(2)] public int[] SpellVisualID = new int[2];                       // 131-132
        public int SpellIconID;                                                         // 133
        public int ActiveIconID;                                                        // 134
        public int SpellPriority;                                                       // 135
        // Each localized column is 16 string slots + a flags word. Slots 0-8
        // are the nine 3.3.5 locales (enUS .. ruRU) and are read as strings.
        // Slots 9-15 are unused by the 3.3.5 client and kept raw: on the CoA
        // reference data ~500 rows (e.g. 16488) hold non-offset values there
        // (16712190, the flags word), which the reader cannot resolve as
        // strings. Field count and record size are unchanged.
        [Cardinality(9)] public string[] NameLang = new string[9];                      // 136-144
        [Cardinality(7)] public uint[] NameLangUnusedSlots = new uint[7];               // 145-151
        public uint NameLangFlags;                                                      // 152
        [Cardinality(9)] public string[] NameSubtextLang = new string[9];               // 153-161
        [Cardinality(7)] public uint[] NameSubtextLangUnusedSlots = new uint[7];        // 162-168
        public uint NameSubtextLangFlags;                                               // 169
        [Cardinality(9)] public string[] DescriptionLang = new string[9];               // 170-178
        [Cardinality(7)] public uint[] DescriptionLangUnusedSlots = new uint[7];        // 179-185
        public uint DescriptionLangFlags;                                               // 186
        [Cardinality(9)] public string[] AuraDescriptionLang = new string[9];           // 187-195
        [Cardinality(7)] public uint[] AuraDescriptionLangUnusedSlots = new uint[7];    // 196-202
        public uint AuraDescriptionLangFlags;                                           // 203
        public int ManaCostPct;                                                         // 204
        public int StartRecoveryCategory;                                               // 205
        public int StartRecoveryTime;                                                   // 206
        public int MaxTargetLevel;                                                      // 207
        public int SpellClassSet;                                                       // 208
        [Cardinality(3)] public uint[] SpellClassMask = new uint[3];                    // 209-211
        public int MaxTargets;                                                          // 212
        public int DefenseType;                                                         // 213
        public int PreventionType;                                                      // 214
        public int StanceBarOrder;                                                      // 215
        [Cardinality(3)] public float[] EffectChainAmplitude = new float[3];            // 216-218 (server: EffectDamageMultiplier)
        public int MinFactionID;                                                        // 219
        public int MinReputation;                                                       // 220
        public int RequiredAuraVision;                                                  // 221
        [Cardinality(2)] public int[] RequiredTotemCategoryID = new int[2];             // 222-223
        public int RequiredAreasID;                                                     // 224
        public int SchoolMask;                                                          // 225
        public int RuneCostID;                                                          // 226
        public int SpellMissileID;                                                      // 227
        public int PowerDisplayID;                                                      // 228
        [Cardinality(3)] public float[] EffectBonusCoefficient = new float[3];          // 229-231 (server: EffectBonusMultiplier)
        public int SpellDescriptionVariableID;                                          // 232
        public int SpellDifficultyID;                                                   // 233

        // Strings of the configured locale slot (DBC.LocaleSlot). Properties,
        // not fields: the reader fills fields only.
        public string Name => LocaleSlots.Pick(NameLang);
        public string NameSubtext => LocaleSlots.Pick(NameSubtextLang);
        public string Description => LocaleSlots.Pick(DescriptionLang);
        public string AuraDescription => LocaleSlots.Pick(AuraDescriptionLang);

        /// <summary>The three SpellClassMask words of effect <paramref name="effectIndex"/> (0-2), as the CoA server reads them.</summary>
        public uint[] EffectClassMask(int effectIndex)
        {
            switch (effectIndex)
            {
                case 0: return EffectSpellClassMaskA;
                case 1: return EffectSpellClassMaskB;
                case 2: return EffectSpellClassMaskC;
                default: throw new System.ArgumentOutOfRangeException(nameof(effectIndex));
            }
        }
    }
}
