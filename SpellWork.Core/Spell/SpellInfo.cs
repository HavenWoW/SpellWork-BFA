using SpellWork.DBC.Structures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SpellWork.Spell
{
    // Holds every term AppendEffectInfo prints about an effect's magnitude,
    // computed once by CalculateEffectValues and rendered from by both the
    // text writer and SpellInfoJsonSerializer. Bring-up (5.5.4): the value is
    // the raw base points; no scaling stores are loaded.
    internal sealed class EffectValueBreakdown
    {
        public float BaseValue;
        public float Variance;
        public float ValuePerResource;
        public float PerLevelValue;
        public float SpellPowerCoefficient;
        public float AttackPowerCoefficient;
        public bool UsesExpectedStat;
    }

    /// <summary>
    /// Read-only public view of <see cref="EffectValueBreakdown"/>.
    /// </summary>
    public sealed class EffectValues
    {
        internal EffectValues(EffectValueBreakdown breakdown)
        {
            BaseValue = breakdown.BaseValue;
            Variance = breakdown.Variance;
            ValuePerResource = breakdown.ValuePerResource;
            PerLevelValue = breakdown.PerLevelValue;
            SpellPowerCoefficient = breakdown.SpellPowerCoefficient;
            AttackPowerCoefficient = breakdown.AttackPowerCoefficient;
            UsesExpectedStat = breakdown.UsesExpectedStat;
        }

        public float BaseValue { get; }
        public float Variance { get; }
        public float ValuePerResource { get; }
        public float PerLevelValue { get; }
        public float SpellPowerCoefficient { get; }
        public float AttackPowerCoefficient { get; }
        public bool UsesExpectedStat { get; }
    }

    public class SpellInfo
    {
        public SpellEntry Spell { get; set; }
        public SpellNameEntry SpellName { get; set; }
        public SpellClassOptionsEntry ClassOptions { get; set; }
        public SpellMiscEntry Misc { get; set; }
        public List<SpellEffectEntry> Effects { get; } = new List<SpellEffectEntry>(32);
        public SpellDurationEntry DurationEntry { get; set; }
        public SpellRangeEntry Range { get; set; }

        // Helper
        public readonly IDictionary<int, SpellEffectInfo> SpellEffectInfoStore = new ConcurrentDictionary<int, SpellEffectInfo>();

        #region SpellDuration
        public int Duration => DurationEntry?.Duration ?? 0;
        public uint DurationPerLevel => DurationEntry?.DurationPerLevel ?? 0;
        public int MaxDuration => DurationEntry?.MaxDuration ?? 0;
        #endregion

        #region Spell
        public int ID => (int)SpellName.ID;
        public string Name => SpellName.Name;
        public string Description => Spell?.Description ?? string.Empty;
        public string Tooltip => Spell?.AuraDescription ?? string.Empty;
        #endregion

        #region SpellMisc
        // All 17 attribute words (Misc.Attributes[0..16]).
        public uint[] AttributeWords
        {
            get
            {
                var words = new uint[17];
                if (Misc?.Attributes != null)
                    for (var i = 0; i < words.Length && i < Misc.Attributes.Length; i++)
                        words[i] = unchecked((uint)Misc.Attributes[i]);
                return words;
            }
        }

        private uint Word(int i) => unchecked((uint)(Misc?.Attributes != null && Misc.Attributes.Length > i ? Misc.Attributes[i] : 0));

        public uint Attributes => Word(0);
        public uint AttributesEx => Word(1);
        public uint AttributesEx2 => Word(2);
        public uint AttributesEx3 => Word(3);
        public uint AttributesEx4 => Word(4);
        public uint AttributesEx5 => Word(5);
        public uint AttributesEx6 => Word(6);
        public uint AttributesEx7 => Word(7);
        public uint AttributesEx8 => Word(8);
        public uint AttributesEx9 => Word(9);
        public uint AttributesEx10 => Word(10);
        public uint AttributesEx11 => Word(11);
        public uint AttributesEx12 => Word(12);
        public uint AttributesEx13 => Word(13);
        public uint AttributesEx14 => Word(14);
        public uint AttributesEx15 => Word(15);
        public uint AttributesEx16 => Word(16);
        public float Speed => Misc?.Speed ?? 0;
        public int CastingTimeIndex => Misc?.CastingTimeIndex ?? 0;
        public int ActiveIconFileDataID => Misc?.ActiveIconFileDataID ?? 0;
        public int SpellIconFileDataID => Misc?.SpellIconFileDataID ?? 0;
        public int RangeIndex => Misc?.RangeIndex ?? 0;
        public uint SchoolMask => (Misc?.SchoolMask ?? 0);
        #endregion

        #region SpellClassOptions
        public uint ModalNextSpell => ClassOptions?.ModalNextSpell ?? 0;
        public uint SpellFamilyName => ClassOptions?.SpellClassSet ?? 0;
        public uint[] SpellClassMask => Array.ConvertAll(ClassOptions?.SpellClassMask ?? new int[4], i => (uint)i);
        #endregion

        private const string Separator = "=================================================";

        public SpellInfo(SpellNameEntry spellName, SpellEntry spellEntry)
        {
            Spell = spellEntry;
            SpellName = spellName;
        }

        public void Write(ISpellInfoWriter rtb, RenderContext context)
        {
            rtb.Clear();

            rtb.SetBold();
            rtb.AppendFormatLine("ID - {0} {1}", ID, Name);
            rtb.SetDefaultStyle();

            rtb.AppendFormatLine(Separator);

            rtb.AppendLine(Description);
            rtb.AppendFormatLineIfNotNull("ToolTip: {0}", Tooltip);
            rtb.AppendFormatLineIfNotNull("Modal Next Spell: {0}", ModalNextSpell);

            #region Triggered by ...
            var addline = false;
            if (DBC.DBC.SpellTriggerStore.ContainsKey(ID))
            {
                foreach (var procSpellId in DBC.DBC.SpellTriggerStore[ID])
                {
                    var procname = "Spell Not Found";
                    if (DBC.DBC.SpellInfoStore.ContainsKey(procSpellId))
                        procname = DBC.DBC.SpellInfoStore[procSpellId].Name;
                    rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
                    rtb.AppendFormatLine("Triggered by spell: ({0}) {1}", procSpellId, procname);
                    rtb.SetDefaultStyle();
                    addline = true;
                }
            }
            if (addline)
                rtb.AppendFormatLine(Separator);
            #endregion

            rtb.AppendFormatLine("SpellIconFileDataID = {0}, ActiveIconFileDataID = {1}", SpellIconFileDataID, ActiveIconFileDataID);

            var mask = SpellClassMask;
            rtb.AppendFormatLine("Family {0} ({1}), flag [0] 0x{2:X8} [1] 0x{3:X8} [2] 0x{4:X8} [3] 0x{5:X8}",
                (SpellFamilyNames)SpellFamilyName, SpellFamilyName, mask[0], mask[1], mask[2], mask[3]);

            rtb.AppendLine();

            rtb.AppendFormatLine("SpellSchoolMask = {0} ({1})", (SpellSchoolMask)SchoolMask, SchoolMask);
            rtb.AppendFormatLine("Speed = {0}, CastingTimeIndex = {1}, RangeIndex = {2}", Speed, CastingTimeIndex, RangeIndex);
            if (DurationEntry != null)
                rtb.AppendFormatLine("Duration: {0} (per level {1}, max {2})", Duration, DurationPerLevel, MaxDuration);

            #region Attributes
            var words = AttributeWords;
            if (words.Any(w => w != 0))
                rtb.AppendLine(Separator);

            if (words[0] != 0)
                rtb.AppendFormatLine("Attributes: 0x{0:X8} ({1})", words[0], (SpellAtribute)words[0]);
            if (words[1] != 0)
                rtb.AppendFormatLine("AttributesEx1: 0x{0:X8} ({1})", words[1], (SpellAtributeEx)words[1]);
            if (words[2] != 0)
                rtb.AppendFormatLine("AttributesEx2: 0x{0:X8} ({1})", words[2], (SpellAtributeEx2)words[2]);
            if (words[3] != 0)
                rtb.AppendFormatLine("AttributesEx3: 0x{0:X8} ({1})", words[3], (SpellAtributeEx3)words[3]);
            if (words[4] != 0)
                rtb.AppendFormatLine("AttributesEx4: 0x{0:X8} ({1})", words[4], (SpellAtributeEx4)words[4]);
            if (words[5] != 0)
                rtb.AppendFormatLine("AttributesEx5: 0x{0:X8} ({1})", words[5], (SpellAtributeEx5)words[5]);
            if (words[6] != 0)
                rtb.AppendFormatLine("AttributesEx6: 0x{0:X8} ({1})", words[6], (SpellAtributeEx6)words[6]);
            if (words[7] != 0)
                rtb.AppendFormatLine("AttributesEx7: 0x{0:X8} ({1})", words[7], (SpellAtributeEx7)words[7]);
            if (words[8] != 0)
                rtb.AppendFormatLine("AttributesEx8: 0x{0:X8} ({1})", words[8], (SpellAtributeEx8)words[8]);
            if (words[9] != 0)
                rtb.AppendFormatLine("AttributesEx9: 0x{0:X8} ({1})", words[9], (SpellAtributeEx9)words[9]);
            if (words[10] != 0)
                rtb.AppendFormatLine("AttributesEx10: 0x{0:X8} ({1})", words[10], (SpellAtributeEx10)words[10]);
            if (words[11] != 0)
                rtb.AppendFormatLine("AttributesEx11: 0x{0:X8} ({1})", words[11], (SpellAtributeEx11)words[11]);
            if (words[12] != 0)
                rtb.AppendFormatLine("AttributesEx12: 0x{0:X8} ({1})", words[12], (SpellAtributeEx12)words[12]);
            if (words[13] != 0)
                rtb.AppendFormatLine("AttributesEx13: 0x{0:X8} ({1})", words[13], (SpellAtributeEx13)words[13]);
            for (var i = 14; i < words.Length; i++)
                if (words[i] != 0)
                    rtb.AppendFormatLine("AttributesEx{0}: 0x{1:X8}", i, words[i]);

            if (words.Any(w => w != 0))
                rtb.AppendLine(Separator);
            #endregion

            #region Effects
            foreach (var effect in Effects.Where(e => e != null).OrderBy(e => e.EffectIndex))
                AppendEffectInfo(rtb, effect, context);
            #endregion
        }

        internal float CalculateBaseEffectValue(SpellEffectEntry effect, RenderContext context)
        {
            // Bring-up: no scaling stores are loaded, the displayed value is the raw base points.
            return Math.Abs(effect.EffectBasePointsF) > 1.0E-5f ? effect.EffectBasePointsF : effect.EffectBasePoints;
        }

        internal EffectValueBreakdown CalculateEffectValues(SpellEffectEntry effect, RenderContext context)
        {
            return new EffectValueBreakdown
            {
                BaseValue = CalculateBaseEffectValue(effect, context),
                Variance = effect.Variance,
                ValuePerResource = effect.EffectPointsPerResource,
                PerLevelValue = effect.EffectRealPointsPerLevel,
                SpellPowerCoefficient = effect.EffectBonusCoefficient,
                AttackPowerCoefficient = effect.BonusCoefficientFromAP,
                UsesExpectedStat = false,
            };
        }

        /// <summary>
        /// Public entry point to the same arithmetic <see cref="Write"/> uses.
        /// </summary>
        public EffectValues GetEffectValues(SpellEffectEntry effect, RenderContext context) =>
            new EffectValues(CalculateEffectValues(effect, context));

        private void AppendEffectInfo(ISpellInfoWriter rtb, SpellEffectEntry effect, RenderContext context)
        {
            rtb.SetBold();
            rtb.AppendFormatLine($"Effect {effect.EffectIndex}: Id {effect.Effect} ({(SpellEffects)effect.Effect})");
            rtb.SetDefaultStyle();

            var values = CalculateEffectValues(effect, context);
            var baseValue = values.BaseValue;
            if (Math.Abs(effect.Variance) > 1.0E-5f)
            {
                var delta = Math.Abs(baseValue * effect.Variance * 0.5f);
                rtb.AppendFormat("BasePoints = {0:F} to {1:F}", baseValue - delta, baseValue + delta);
            }
            else
                rtb.AppendFormat("BasePoints = {0:F}", baseValue);

            if (Math.Abs(values.PerLevelValue) > 1.0E-5f)
                rtb.AppendFormat(" + Level * {0:F}", values.PerLevelValue);

            rtb.AppendFormatIfNotNull(" + resource * {0:F}", values.ValuePerResource);

            if (values.SpellPowerCoefficient > 1.0E-5f)
                rtb.AppendFormat(" + spellPower * {0}", values.SpellPowerCoefficient);

            if (values.AttackPowerCoefficient > 1.0E-5)
                rtb.AppendFormat(" + AP * {0}", values.AttackPowerCoefficient);

            rtb.AppendLine();

            rtb.AppendFormatLine("Targets ({0}, {1}) ({2}, {3})",
                effect.ImplicitTarget[0], effect.ImplicitTarget[1],
                (Targets)effect.ImplicitTarget[0], (Targets)effect.ImplicitTarget[1]);

            AuraModTypeName(rtb, effect);

            var classMask = Array.ConvertAll(effect.EffectSpellClassMask, i => (uint)i);
            if (classMask[0] != 0 || classMask[1] != 0 || classMask[2] != 0 || classMask[3] != 0)
                rtb.AppendFormatLine("SpellClassMask = {0:X8} {1:X8} {2:X8} {3:X8}", classMask[0], classMask[1], classMask[2], classMask[3]);

            var trigger = effect.EffectTriggerSpell;
            if (trigger != 0)
            {
                if (DBC.DBC.SpellInfoStore.TryGetValue(trigger, out var triggerSpell))
                {
                    rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
                    rtb.AppendFormatLine("   Trigger spell ({0}) {1}.", trigger, triggerSpell.SpellName.Name);
                    rtb.AppendFormatLineIfNotNull("   Description: {0}", triggerSpell.Description);
                    rtb.AppendFormatLineIfNotNull("   ToolTip: {0}", triggerSpell.Tooltip);
                    rtb.SetDefaultStyle();
                }
                else
                    rtb.AppendFormatLine("Trigger spell ({0}) Not found", trigger);
            }

            rtb.AppendFormatLineIfNotNull("EffectChainTargets = {0}", effect.EffectChainTargets);
            rtb.AppendFormatLineIfNotNull("EffectItemType = {0}", effect.EffectItemType);

            if ((Mechanics)effect.EffectMechanic != Mechanics.MECHANIC_NONE)
                rtb.AppendFormatLine("Effect Mechanic = {0} ({1})", effect.EffectMechanic, (Mechanics)effect.EffectMechanic);

            rtb.AppendFormatLineIfNotNull("Attributes {0:X8} ({0})", effect.EffectAttributes);
            rtb.AppendLine();
        }

        private static void AuraModTypeName(ISpellInfoWriter rtb, SpellEffectEntry effect)
        {
            var aura = (AuraType)effect.EffectAura;
            var misc = effect.EffectMiscValue[0];

            if (effect.EffectAura == 0)
            {
                rtb.AppendFormatLineIfNotNull("EffectMiscValueA = {0}", effect.EffectMiscValue[0]);
                rtb.AppendFormatLineIfNotNull("EffectMiscValueB = {0}", effect.EffectMiscValue[1]);
                rtb.AppendFormatLineIfNotNull("EffectAmplitude = {0}", effect.EffectAmplitude);

                return;
            }

            rtb.AppendFormat("Aura Id {0:D} ({0})", aura);
            rtb.AppendFormat(", value = {0}", effect.EffectBasePoints);
            rtb.AppendFormat(", misc = {0} (", misc);

            switch (aura)
            {
                case AuraType.SPELL_AURA_MOD_STAT:
                    rtb.Append((UnitMods)misc);
                    break;
                case AuraType.SPELL_AURA_MOD_RATING:
                case AuraType.SPELL_AURA_MOD_RATING_PCT:
                    rtb.Append((CombatRating)misc);
                    break;
                case AuraType.SPELL_AURA_ADD_FLAT_MODIFIER:
                case AuraType.SPELL_AURA_ADD_PCT_MODIFIER:
                    rtb.Append((SpellModOp)misc);
                    break;
                default:
                    rtb.Append(misc);
                    break;
            }

            rtb.AppendFormat("), miscB = {0}", effect.EffectMiscValue[1]);
            rtb.AppendFormatLine(", amplitude = {0}, periodic = {1}", effect.EffectAmplitude, effect.EffectAuraPeriod);
        }

        public bool HasEffect(SpellEffects effect)
        {
            return Effects.Any(eff => eff != null && eff.Effect == (uint)effect);
        }

        public bool HasAura(AuraType aura)
        {
            return Effects.Any(eff => eff != null && eff.EffectAura == (uint)aura);
        }

        public bool HasTargetA(Targets target)
        {
            return Effects.Any(eff => eff != null && eff.ImplicitTarget[0] == (uint)target);
        }

        public bool HasTargetB(Targets target)
        {
            return Effects.Any(eff => eff != null && eff.ImplicitTarget[1] == (uint)target);
        }
    }

    public class SpellEffectInfo
    {
        public SpellEffectEntry SpellEffect { get; set; }

        public uint ID => (uint)SpellEffect.ID;

        public int SpellID => SpellEffect.SpellID;

        public int DifficultyID => SpellEffect.DifficultyID;

        public uint Effect => SpellEffect.Effect;
        public int EffectIndex => SpellEffect.EffectIndex;
        public int EffectAttributes => SpellEffect.EffectAttributes;

        public short EffectAura => SpellEffect.EffectAura;
        public int EffectAuraPeriod => SpellEffect.EffectAuraPeriod;

        // Raw base points; base value from EffectBasePointsF when present.
        public float EffectBasePoints =>
            Math.Abs(SpellEffect.EffectBasePointsF) > 1.0E-5f ? SpellEffect.EffectBasePointsF : SpellEffect.EffectBasePoints;
        public float Variance => SpellEffect.Variance;
        public float Coefficient => SpellEffect.Coefficient;

        public int EffectMiscValueA => SpellEffect.EffectMiscValue[0];
        public int EffectMiscValueB => SpellEffect.EffectMiscValue[1];

        public int EffectSpellClassMaskA => SpellEffect.EffectSpellClassMask[0];
        public int EffectSpellClassMaskB => SpellEffect.EffectSpellClassMask[1];
        public int EffectSpellClassMaskC => SpellEffect.EffectSpellClassMask[2];
        public int EffectSpellClassMaskD => SpellEffect.EffectSpellClassMask[3];

        public int EffectTriggerSpell => SpellEffect.EffectTriggerSpell;

        public short TargetA => SpellEffect.ImplicitTarget[0];
        public short TargetB => SpellEffect.ImplicitTarget[1];

        public uint EffectRadiusIndex => SpellEffect.EffectRadiusIndex[0];
        public uint EffectRadiusMaxIndex => SpellEffect.EffectRadiusIndex[1];

        public int EffectChainTargets => SpellEffect.EffectChainTargets;
        public int EffectItemType => SpellEffect.EffectItemType;
        public int EffectMechanic => SpellEffect.EffectMechanic;

        public float EffectAmplitude => SpellEffect.EffectAmplitude;
        public float EffectBonusCoefficient => SpellEffect.EffectBonusCoefficient;
        public float EffectChainAmplitude => SpellEffect.EffectChainAmplitude;
        public float EffectPointsPerResource => SpellEffect.EffectPointsPerResource;
        public float EffectRealPointsPerLevel => SpellEffect.EffectRealPointsPerLevel;

        public float EffectPosFacing => SpellEffect.EffectPosFacing;
        public float BonusCoefficientFromAP => SpellEffect.BonusCoefficientFromAP;

        public SpellEffectInfo(SpellEffectEntry spellEffectEntry)
        {
            SpellEffect = spellEffectEntry;
        }
    }
}
