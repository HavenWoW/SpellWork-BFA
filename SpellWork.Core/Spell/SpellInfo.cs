using SpellWork.DBC.Structures;
using SpellWork.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpellWork.Spell
{
    // Holds every term AppendEffectInfo prints about an effect's magnitude,
    // computed once by CalculateEffectValues and rendered from by both the
    // text writer and SpellInfoJsonSerializer. Each field is zeroed out
    // exactly when the text path's own condition would have suppressed
    // printing it, so a caller can treat "zero" and "omit" as the same
    // signal the text path already uses.
    internal sealed class EffectValueBreakdown
    {
        public float BaseValue;
        public float Variance;
        public float ValuePerResource;
        public float PerLevelValue;
        public float SpellPowerCoefficient;
    }

    /// <summary>
    /// Read-only public view of <see cref="EffectValueBreakdown"/>. The
    /// breakdown itself stays internal: it is the shared arithmetic seam that
    /// stops the text render and the MCP server reporting different damage,
    /// and exposing its mutable fields would invite a caller to write to it.
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
        }

        public float BaseValue { get; }
        public float Variance { get; }
        public float ValuePerResource { get; }
        public float PerLevelValue { get; }
        public float SpellPowerCoefficient { get; }
    }

    /// <summary>
    /// One SpellEffect row as resolved for a difficulty: the row itself, the
    /// difficulty it actually came from (the requested one, or 0 when the
    /// spell has no row of its own at that difficulty for this effect index),
    /// and its SpellEffectScaling row if any.
    /// </summary>
    public sealed class ResolvedEffect
    {
        public SpellEffectEntry Entry { get; set; }
        public int ResolvedDifficulty { get; set; }
        public SpellEffectScalingEntry Scaling { get; set; }
    }

    public class SpellInfo
    {
        public SpellEntry Spell { get; }
        public SpellMiscEntry Misc { get; }
        public SpellScalingEntry Scaling { get; }
        public SpellRuneCostEntry RuneCost { get; }
        public SpellAuraRestrictionsEntry AuraRestrictions { get; }
        public SpellCastingRequirementsEntry CastingRequirements { get; }
        public SpellClassOptionsEntry ClassOptions { get; }
        public SpellEquippedItemsEntry EquippedItems { get; }
        public SpellReagentsEntry Reagents { get; }
        public SpellShapeshiftEntry Shapeshift { get; }
        public SpellTotemsEntry Totems { get; }
        public SpellDescriptionVariablesEntry DescriptionVariables { get; }
        public SpellDurationEntry DurationEntry { get; }
        public SpellRangeEntry Range { get; }

        private readonly List<SpellPowerEntry> _powers = new List<SpellPowerEntry>();

        /// <summary>SpellPower rows, ordered by OrderIndex.</summary>
        public IReadOnlyList<SpellPowerEntry> Powers => _powers;

        #region Difficulty-aware rows
        public SpellAuraOptionsEntry AuraOptionsFor(int d) => DBC.DBC.AuraOptionsIndex?.Get(ID, d, out _);
        public SpellCategoriesEntry CategoriesFor(int d) => DBC.DBC.CategoriesIndex?.Get(ID, d, out _);
        public SpellCooldownsEntry CooldownsFor(int d) => DBC.DBC.CooldownsIndex?.Get(ID, d, out _);
        public SpellInterruptsEntry InterruptsFor(int d) => DBC.DBC.InterruptsIndex?.Get(ID, d, out _);
        public SpellLevelsEntry LevelsFor(int d) => DBC.DBC.LevelsIndex?.Get(ID, d, out _);
        public SpellTargetRestrictionsEntry TargetRestrictionsFor(int d) => DBC.DBC.TargetRestrictionsIndex?.Get(ID, d, out _);
        public SpellMiscEntry MiscFor(int d) => DBC.DBC.MiscIndex?.Get(ID, d, out _) ?? Misc;

        // Difficulty 0.
        public SpellAuraOptionsEntry AuraOptions => AuraOptionsFor(0);
        public SpellCategoriesEntry Categories => CategoriesFor(0);
        public SpellCooldownsEntry Cooldowns => CooldownsFor(0);
        public SpellInterruptsEntry Interrupts => InterruptsFor(0);
        public SpellLevelsEntry Levels => LevelsFor(0);
        public SpellTargetRestrictionsEntry TargetRestrictions => TargetRestrictionsFor(0);

        public SpellProcsPerMinuteEntry ProcsPerMinute => ProcsPerMinuteFor(0);

        // Rows reached through a difficulty-resolved row: the SpellMisc row at a
        // difficulty can name its own duration and range, and the SpellAuraOptions
        // row its own procs-per-minute entry.
        public SpellDurationEntry DurationFor(int d)
        {
            var misc = MiscFor(d);
            return misc == null ? null : Lookup(DBC.DBC.SpellDuration, misc.DurationIndex);
        }

        public SpellRangeEntry RangeFor(int d)
        {
            var misc = MiscFor(d);
            return misc == null ? null : Lookup(DBC.DBC.SpellRange, misc.RangeIndex);
        }

        public SpellProcsPerMinuteEntry ProcsPerMinuteFor(int d) =>
            Lookup(DBC.DBC.SpellProcsPerMinute, AuraOptionsFor(d)?.SpellProcsPerMinuteID ?? 0);

        /// <summary>The 14 attribute words of <see cref="MiscFor"/>(d).</summary>
        public uint[] AttributesFor(int d) => MiscFor(d)?.Attributes ?? new uint[14];
        #endregion

        #region Effects
        private readonly Dictionary<int, SortedDictionary<int, ResolvedEffect>> _effectsByDifficulty =
            new Dictionary<int, SortedDictionary<int, ResolvedEffect>>();

        internal void AddEffect(SpellEffectEntry e, SpellEffectScalingEntry scaling)
        {
            lock (_effectsByDifficulty)
            {
                if (!_effectsByDifficulty.TryGetValue(e.DifficultyID, out var byIndex))
                    _effectsByDifficulty[e.DifficultyID] = byIndex = new SortedDictionary<int, ResolvedEffect>();
                byIndex[e.EffectIndex] = new ResolvedEffect { Entry = e, ResolvedDifficulty = e.DifficultyID, Scaling = scaling };
            }
        }

        internal void AddPower(SpellPowerEntry power)
        {
            lock (_powers)
                _powers.Add(power);
        }

        public IReadOnlyList<int> EffectDifficulties => _effectsByDifficulty.Keys.OrderBy(k => k).ToList();

        /// <summary>
        /// Per effect index: the row at <paramref name="difficulty"/> if one
        /// exists, else the base row. A spell can have an effect index only at
        /// a non-zero difficulty, so both key sets are unioned.
        /// </summary>
        public IReadOnlyList<ResolvedEffect> EffectsFor(int difficulty)
        {
            _effectsByDifficulty.TryGetValue(0, out var baseRows);
            _effectsByDifficulty.TryGetValue(difficulty, out var exact);
            var indices = new SortedSet<int>();
            if (baseRows != null) indices.UnionWith(baseRows.Keys);
            if (exact != null) indices.UnionWith(exact.Keys);
            return indices.Select(i => exact != null && exact.TryGetValue(i, out var x) ? x : baseRows[i]).ToList();
        }

        public IReadOnlyList<ResolvedEffect> Effects => EffectsFor(0);
        #endregion

        #region SpellDuration
        public int Duration => DurationEntry?.Duration ?? 0;
        public int DurationPerLevel => DurationEntry?.Unknown2 ?? 0;
        public int MaxDuration => DurationEntry?.MaxDuration ?? 0;
        #endregion

        #region Spell
        public int ID => Spell.ID;
        public string Name => Spell.Name;
        public string Description => Spell?.Description ?? string.Empty;
        public string Tooltip => Spell?.AuraDescription ?? string.Empty;
        #endregion

        #region SpellMisc
        public uint[] Attributes => Misc?.Attributes ?? new uint[14];
        public uint AttributesEx => Attributes[1];
        public uint AttributesEx2 => Attributes[2];
        public uint AttributesEx3 => Attributes[3];
        public uint AttributesEx4 => Attributes[4];
        public uint AttributesEx5 => Attributes[5];
        public uint AttributesEx6 => Attributes[6];
        public uint AttributesEx7 => Attributes[7];
        public uint AttributesEx8 => Attributes[8];
        public uint AttributesEx9 => Attributes[9];
        public uint AttributesEx10 => Attributes[10];
        public uint AttributesEx11 => Attributes[11];
        public uint AttributesEx12 => Attributes[12];
        public uint AttributesEx13 => Attributes[13];
        public float Speed => Misc?.Speed ?? 0;
        public int CastingTimeIndex => Misc?.CastingTimeIndex ?? 0;
        public int ActiveIconID => Misc?.ActiveIconID ?? 0;
        public int SpellIconID => Misc?.SpellIconID ?? 0;
        public int SpellVisualID => Misc?.SpellVisualID[0] ?? 0;
        public int RangeIndex => Misc?.RangeIndex ?? 0;
        public int SchoolMask => Misc?.SchoolMask ?? 0;
        #endregion

        #region SpellClassOptions
        public uint ModalNextSpell => ClassOptions?.ModalNextSpell ?? 0;
        public uint SpellFamilyName => ClassOptions?.SpellFamilyName ?? 0;
        public uint[] SpellClassMask => ClassOptions?.SpellFamilyFlags ?? new uint[4];
        #endregion

        #region SpellCategories
        public int DamageClass => Categories?.DefenseType ?? 0;
        public int PreventionType => Categories?.PreventionType ?? 0;
        public int Category => Categories?.Category ?? 0;
        public int Dispel => Categories?.DispelType ?? 0;
        public int Mechanic => Categories?.Mechanic ?? 0;
        #endregion

        #region SpellShapeshift
        // Two 32-bit words per mask, low word first.
        public ulong Stances => Shapeshift == null ? 0 : ((ulong)Shapeshift.ShapeshiftMask[1] << 32) | Shapeshift.ShapeshiftMask[0];
        public ulong StancesNot => Shapeshift == null ? 0 : ((ulong)Shapeshift.ShapeshiftExclude[1] << 32) | Shapeshift.ShapeshiftExclude[0];
        #endregion

        #region SpellCooldowns
        public int CategoryRecoveryTime => Cooldowns?.CategoryRecoveryTime ?? 0;
        public int RecoveryTime => Cooldowns?.RecoveryTime ?? 0;
        public int StartRecoveryTime => Cooldowns?.StartRecoveryTime ?? 0;
        public int StartRecoveryCategory => Categories?.StartRecoveryCategory ?? 0;
        #endregion

        #region SpellAuraRestrictions
        public int CasterAuraState => (int)(AuraRestrictions?.CasterAuraState ?? 0);
        public int TargetAuraState => (int)(AuraRestrictions?.TargetAuraState ?? 0);
        public int CasterAuraStateNot => (int)(AuraRestrictions?.CasterAuraStateNot ?? 0);
        public int TargetAuraStateNot => (int)(AuraRestrictions?.TargetAuraStateNot ?? 0);
        public int CasterAuraSpell => (int)(AuraRestrictions?.CasterAuraSpell ?? 0);
        public int TargetAuraSpell => (int)(AuraRestrictions?.TargetAuraSpell ?? 0);
        public int ExcludeCasterAuraSpell => (int)(AuraRestrictions?.ExcludeCasterAuraSpell ?? 0);
        public int ExcludeTargetAuraSpell => (int)(AuraRestrictions?.ExcludeTargetAuraSpell ?? 0);
        #endregion

        #region SpellAuraOptions
        public int ProcCharges => AuraOptions?.ProcCharges ?? 0;
        public int ProcChance => AuraOptions?.ProcChance ?? 0;
        public uint ProcFlags => (uint)(AuraOptions?.ProcTypeMask ?? 0);
        public int CumulativeAura => AuraOptions?.CumulativeAura ?? 0;
        public int ProcCooldown => AuraOptions?.ProcCategoryRecovery ?? 0;
        #endregion

        #region SpellLevels
        public int BaseLevel => Levels?.BaseLevel ?? 0;
        public int MaxLevel => Levels?.MaxLevel ?? 0;
        public int SpellLevel => Levels?.SpellLevel ?? 0;
        #endregion

        #region EquippedItems
        public int EquippedItemClass => EquippedItems?.EquippedItemClass ?? 0;
        public uint EquippedItemInvTypes => EquippedItems?.EquippedItemInventoryTypeMask ?? 0;
        public uint EquippedItemSubclass => EquippedItems?.EquippedItemSubClassMask ?? 0;
        #endregion

        #region CastingRequirements
        public int RequiredAreasId => CastingRequirements?.AreaGroupId ?? 0;
        public uint FacingCasterFlags => CastingRequirements?.FacingCasterFlags ?? 0;
        public uint MinFactionID => CastingRequirements?.MinFactionId ?? 0;
        public uint MinReputation => CastingRequirements?.MinReputation ?? 0;
        public uint RequiredAuraVision => CastingRequirements?.RequiredAuraVision ?? 0;
        public uint RequiresSpellFocus => CastingRequirements?.RequiresSpellFocus ?? 0;
        #endregion

        #region SpellProcsPerMinute
        public float BaseProcRate => ProcsPerMinute?.BaseProcRate ?? 0;
        public int ProcsPerMinuteFlags => ProcsPerMinute?.Flags ?? 0;
        #endregion

        #region SpellInterrupts
        public int AuraInterruptFlags => Interrupts?.AuraInterruptFlags[0] ?? 0;
        public int AuraInterruptFlags2 => Interrupts?.AuraInterruptFlags[1] ?? 0;
        public int ChannelInterruptFlags => Interrupts?.ChannelInterruptFlags[0] ?? 0;
        public int ChannelInterruptFlags2 => Interrupts?.ChannelInterruptFlags[1] ?? 0;
        public int InterruptFlags => Interrupts?.InterruptFlags ?? 0;
        #endregion

        public string ProcInfo => DescribeProcFlags(ProcFlags);

        internal static string DescribeProcFlags(uint procFlags)
        {
            var i = 0;
            var sb = new StringBuilder();
            var proc = procFlags;
            while (proc != 0)
            {
                if ((proc & 1) != 0)
                    sb.AppendFormatLine("  {0}", SpellEnums.ProcFlagDesc[i]);
                ++i;
                proc >>= 1;
            }

            return sb.ToString();
        }

        private const string Separator = "=================================================";

        public SpellInfo(SpellEntry spell)
        {
            Spell = spell;

            Misc = Lookup(DBC.DBC.SpellMisc, spell.MiscID);
            Scaling = Lookup(DBC.DBC.SpellScaling, spell.ScalingID);
            RuneCost = Lookup(DBC.DBC.SpellRuneCost, spell.RuneCostID);
            AuraRestrictions = Lookup(DBC.DBC.SpellAuraRestrictions, spell.AuraRestrictionsID);
            CastingRequirements = Lookup(DBC.DBC.SpellCastingRequirements, spell.CastingRequirementsID);
            ClassOptions = Lookup(DBC.DBC.SpellClassOptions, spell.ClassOptionsID);
            EquippedItems = Lookup(DBC.DBC.SpellEquippedItems, spell.EquippedItemsID);
            Reagents = Lookup(DBC.DBC.SpellReagents, spell.ReagentsID);
            Shapeshift = Lookup(DBC.DBC.SpellShapeshift, spell.ShapeshiftID);
            Totems = Lookup(DBC.DBC.SpellTotems, spell.TotemsID);
            DescriptionVariables = Lookup(DBC.DBC.SpellDescriptionVariables, spell.DescriptionVariablesID);
            if (Misc != null)
            {
                DurationEntry = Lookup(DBC.DBC.SpellDuration, Misc.DurationIndex);
                Range = Lookup(DBC.DBC.SpellRange, Misc.RangeIndex);
            }
        }

        // Id 0 means "no row"; a missing store or row leaves the member null.
        private static T Lookup<T>(IDictionary<int, T> store, int id) where T : class =>
            id != 0 && store != null && store.TryGetValue(id, out var row) ? row : null;

        public void Write(ISpellInfoWriter rtb, RenderContext context)
        {
            // Every difficulty-keyed table is read at the context's difficulty
            // (falling back to the base row), never through the difficulty-0
            // convenience properties.
            var difficulty = context.Difficulty;
            var misc = MiscFor(difficulty);
            var attributes = AttributesFor(difficulty);
            var categories = CategoriesFor(difficulty);
            var auraOptions = AuraOptionsFor(difficulty);
            var cooldowns = CooldownsFor(difficulty);
            var levels = LevelsFor(difficulty);
            var interrupts = InterruptsFor(difficulty);
            var durationEntry = DurationFor(difficulty);
            var range = RangeFor(difficulty);
            var procsPerMinute = ProcsPerMinuteFor(difficulty);
            var schoolMask = misc?.SchoolMask ?? 0;
            var category = categories?.Category ?? 0;
            var procFlags = (uint)(auraOptions?.ProcTypeMask ?? 0);
            var procChance = auraOptions?.ProcChance ?? 0;
            var procCharges = auraOptions?.ProcCharges ?? 0;

            rtb.Clear();

            rtb.SetBold();
            rtb.AppendFormatLine("ID - {0} {1}{2}",
                ID, Name, Scaling != null ? $" (Level {context.Level})" : string.Empty);
            rtb.SetDefaultStyle();

            rtb.AppendFormatLine(Separator);

            rtb.AppendLine(Description);
            rtb.AppendFormatLineIfNotNull("ToolTip: {0}", Tooltip);
            if (DescriptionVariables != null)
            {
                rtb.AppendLine("Description variables:");
                rtb.AppendLine(DescriptionVariables.Variables);
            }

            rtb.AppendFormatLineIfNotNull("Modal Next Spell: {0}", ModalNextSpell);
            if (!string.IsNullOrEmpty(Spell.Description) && !string.IsNullOrEmpty(Spell.AuraDescription) && ModalNextSpell != 0)
                rtb.AppendFormatLine(Separator);

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

            rtb.AppendFormatLine($"Category = { category }, SpellIconID = { misc?.SpellIconID ?? 0 }, ActiveIconID = { misc?.ActiveIconID ?? 0 }, SpellVisualID = { misc?.SpellVisualID[0] ?? 0 }");

            rtb.AppendFormatLine("Family {0} ({1}), flag [0] 0x{2:X8} [1] 0x{3:X8} [2] 0x{4:X8} [3] 0x{5:X8}",
                    (SpellFamilyNames)SpellFamilyName, SpellFamilyName,
                    SpellClassMask[0], SpellClassMask[1], SpellClassMask[2], SpellClassMask[3]);

            #region Modified by ...
            foreach (var eff in
                    from s in DBC.DBC.SpellInfoStore.Values
                    where s.SpellFamilyName == SpellFamilyName
                    from eff in s.Effects
                    where eff != null && ((eff.Entry.EffectSpellClassMask[0] & SpellClassMask[0]) != 0 ||
                          (eff.Entry.EffectSpellClassMask[1] & SpellClassMask[1]) != 0 ||
                          (eff.Entry.EffectSpellClassMask[2] & SpellClassMask[2]) != 0 ||
                          (eff.Entry.EffectSpellClassMask[3] & SpellClassMask[3]) != 0)
                    select eff.Entry)
            {
                rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
                rtb.AppendFormatLine("Modified by {0} ({1})",
                    DBC.DBC.SpellInfoStore[eff.SpellID].Name, eff.SpellID);
            }
            #endregion

            rtb.AppendLine();

            var damageClass = categories?.DefenseType ?? 0;
            var preventionType = categories?.PreventionType ?? 0;
            rtb.AppendFormatLine("SpellSchoolMask = {0} ({1})", (SpellSchoolMask)schoolMask, schoolMask);
            rtb.AppendFormatLine("DamageClass = {0} ({1})", damageClass, (SpellDmgClass)damageClass);
            rtb.AppendFormatLine("PreventionType = {0} ({1})", preventionType, (SpellPreventionType)preventionType);

            #region Attributes
            if (misc != null && !attributes.All(a => a == 0))
                rtb.AppendLine(Separator);

            if (attributes[0] != 0)
                rtb.AppendFormatLine("Attributes: 0x{0:X8} ({1})", attributes[0], (SpellAtribute)attributes[0]);
            if (attributes[1] != 0)
                rtb.AppendFormatLine("AttributesEx1: 0x{0:X8} ({1})", attributes[1], (SpellAtributeEx)attributes[1]);
            if (attributes[2] != 0)
                rtb.AppendFormatLine("AttributesEx2: 0x{0:X8} ({1})", attributes[2], (SpellAtributeEx2)attributes[2]);
            if (attributes[3] != 0)
                rtb.AppendFormatLine("AttributesEx3: 0x{0:X8} ({1})", attributes[3], (SpellAtributeEx3)attributes[3]);
            if (attributes[4] != 0)
                rtb.AppendFormatLine("AttributesEx4: 0x{0:X8} ({1})", attributes[4], (SpellAtributeEx4)attributes[4]);
            if (attributes[5] != 0)
                rtb.AppendFormatLine("AttributesEx5: 0x{0:X8} ({1})", attributes[5], (SpellAtributeEx5)attributes[5]);
            if (attributes[6] != 0)
                rtb.AppendFormatLine("AttributesEx6: 0x{0:X8} ({1})", attributes[6], (SpellAtributeEx6)attributes[6]);
            if (attributes[7] != 0)
                rtb.AppendFormatLine("AttributesEx7: 0x{0:X8} ({1})", attributes[7], (SpellAtributeEx7)attributes[7]);
            if (attributes[8] != 0)
                rtb.AppendFormatLine("AttributesEx8: 0x{0:X8} ({1})", attributes[8], (SpellAtributeEx8)attributes[8]);
            if (attributes[9] != 0)
                rtb.AppendFormatLine("AttributesEx9: 0x{0:X8} ({1})", attributes[9], (SpellAtributeEx9)attributes[9]);
            if (attributes[10] != 0)
                rtb.AppendFormatLine("AttributesEx10: 0x{0:X8} ({1})", attributes[10], (SpellAtributeEx10)attributes[10]);
            if (attributes[11] != 0)
                rtb.AppendFormatLine("AttributesEx11: 0x{0:X8} ({1})", attributes[11], (SpellAtributeEx11)attributes[11]);
            if (attributes[12] != 0)
                rtb.AppendFormatLine("AttributesEx12: 0x{0:X8} ({1})", attributes[12], (SpellAtributeEx12)attributes[12]);
            if (attributes[13] != 0)
                rtb.AppendFormatLine("AttributesEx13: 0x{0:X8} ({1})", attributes[13], (SpellAtributeEx13)attributes[13]);

            rtb.AppendLine(Separator);
            #endregion

            var targetRestriction = TargetRestrictionsFor(context.Difficulty);
            if (targetRestriction != null)
            {
                if (targetRestriction.Targets != 0)
                    rtb.AppendFormatLine("Targets Mask = 0x{0:X8} ({1})", targetRestriction.Targets, (SpellCastTargetFlags)targetRestriction.Targets);

                if (targetRestriction.TargetCreatureType != 0)
                    rtb.AppendFormatLine("Creature Type Mask = 0x{0:X8} ({1})",
                        targetRestriction.TargetCreatureType, (CreatureTypeMask)targetRestriction.TargetCreatureType);

                rtb.AppendFormatLineIfNotNull("MaxTargets: {0}", targetRestriction.MaxTargets);
                rtb.AppendFormatLineIfNotNull("ConeDegrees: {0}", targetRestriction.ConeAngle);
                rtb.AppendFormatLineIfNotNull("Width (line): {0}", targetRestriction.Width);
            }

            if (Stances != 0)
                rtb.AppendFormatLine("Stances: {0}", (ShapeshiftFormMask)Stances);

            if (StancesNot != 0)
                rtb.AppendFormatLine("Stances Not: {0}", (ShapeshiftFormMask)StancesNot);

            // Skills
            if (DBC.DBC.SkillLineAbility != null)
            {
                var query = DBC.DBC.SkillLineAbility.Where(skl => skl.Value.SpellId == ID).ToArray();
                if (query.Length != 0)
                {
                    var skill = query.First().Value;
                    var line = DBC.DBC.SkillLine != null && DBC.DBC.SkillLine.TryGetValue((int)skill.SkillId, out var l) ? l : null;

                    rtb.AppendFormatLine(@"Skill (Id {0}) ""{1}""", skill.SkillId, line?.Name);
                    rtb.AppendFormat("    ReqSkillValue {0}", skill.ReqSkillValue);

                    rtb.AppendFormat(", Forward spell = {0}, MinMaxValue ({1}, {2})", skill.ForwardSpellid,
                        skill.MinValue, skill.MaxValue);
                }
            }

            // SpellReagents
            {
                var printedHeader = false;
                for (var i = 0; Reagents != null && i < Reagents.Count.Length; ++i)
                {
                    if (Reagents.Count[i] == 0 || Reagents.ItemId[i] == 0)
                        continue;

                    if (!printedHeader)
                    {
                        rtb.AppendLine();
                        rtb.Append("Reagents:");
                        printedHeader = true;
                    }

                    rtb.AppendFormat("  {0} x{1}", Reagents.ItemId[i], Reagents.Count[i]);
                }

                if (printedHeader)
                    rtb.AppendLine();
            }

            rtb.AppendFormatLine("Spell Level = {0}, base {1}, max {2}",
                levels?.SpellLevel ?? 0, levels?.BaseLevel ?? 0, levels?.MaxLevel ?? 0);

            if (EquippedItemClass != 0)
            {
                rtb.AppendFormatLine("EquippedItemClass = {0} ({1})", EquippedItemClass, (ItemClass)EquippedItemClass);

                if (EquippedItemSubclass != 0)
                {
                    switch ((ItemClass)EquippedItemClass)
                    {
                        case ItemClass.WEAPON:
                            rtb.AppendFormatLine("    SubClass mask 0x{0:X8} ({1})", EquippedItemSubclass, (ItemSubClassWeaponMask)EquippedItemSubclass);
                            break;
                        case ItemClass.ARMOR:
                            rtb.AppendFormatLine("    SubClass mask 0x{0:X8} ({1})", EquippedItemSubclass, (ItemSubClassArmorMask)EquippedItemSubclass);
                            break;
                        case ItemClass.MISC:
                            rtb.AppendFormatLine("    SubClass mask 0x{0:X8} ({1})", EquippedItemSubclass, (ItemSubClassMiscMask)EquippedItemSubclass);
                            break;
                    }
                }

                if (EquippedItemInvTypes != 0)
                    rtb.AppendFormatLine("    InventoryType mask = 0x{0:X8} ({1})", EquippedItemInvTypes, (InventoryTypeMask)EquippedItemInvTypes);
            }

            rtb.AppendLine();
            var dispel = categories?.DispelType ?? 0;
            var mechanic = categories?.Mechanic ?? 0;
            rtb.AppendFormatLine("Category = {0}", category);
            rtb.AppendFormatLine("DispelType = {0} ({1})", dispel, (DispelType)dispel);
            rtb.AppendFormatLine("Mechanic = {0} ({1})", mechanic, (Mechanics)mechanic);

            if (range != null)
            {
                rtb.AppendFormatLine("SpellRange: (Id {0}) \"{1}\":", misc.RangeIndex, range.Name);
                rtb.AppendFormatLine("    MinRangeNegative = {0}, MinRangePositive = {1}", range.MinRangeNegative, range.MinRangePositive);
                rtb.AppendFormatLine("    MaxRangeNegative = {0}, MaxRangePositive = {1}", range.MaxRangeNegative, range.MaxRangePositive);
            }

            if (misc != null)
                rtb.AppendFormatLineIfNotNull("Speed {0:F}", misc.Speed);

            rtb.AppendFormatLineIfNotNull("Stackable up to {0}", auraOptions?.CumulativeAura ?? 0);

            var castingTimeIndex = misc?.CastingTimeIndex ?? 0;
            if (castingTimeIndex != 0 && DBC.DBC.SpellCastTimes != null &&
                DBC.DBC.SpellCastTimes.TryGetValue(castingTimeIndex, out var castTimeEntry))
            {
                var level = (int)context.Level;
                if (Scaling != null && Scaling.MaxScalingLevel != 0 && level > Scaling.MaxScalingLevel)
                    level = Scaling.MaxScalingLevel;

                if (levels != null && levels.BaseLevel != 0)
                    level -= levels.BaseLevel;

                var castTime = castTimeEntry.CastTime + castTimeEntry.CastTimePerLevel * level;
                if (castTime < castTimeEntry.MinCastTime)
                    castTime = castTimeEntry.MinCastTime;

                rtb.AppendFormatLine("Cast Time: (ID {0}): {1}", castingTimeIndex, castTime);
            }

            var recoveryTime = cooldowns?.RecoveryTime ?? 0;
            var categoryRecoveryTime = cooldowns?.CategoryRecoveryTime ?? 0;
            var startRecoveryCategory = categories?.StartRecoveryCategory ?? 0;
            if (recoveryTime != 0 || categoryRecoveryTime != 0 || startRecoveryCategory != 0)
            {
                rtb.AppendFormatLine("RecoveryTime: {0} ms, CategoryRecoveryTime: {1} ms", recoveryTime, categoryRecoveryTime);
                rtb.AppendFormatLine("StartRecoveryCategory = {0}, StartRecoveryTime = {1:F} ms", startRecoveryCategory, cooldowns?.StartRecoveryTime ?? 0);
            }

            if (durationEntry != null)
                rtb.AppendFormatLine("Duration {0}, {1}, {2}", durationEntry.Duration, durationEntry.Unknown2, durationEntry.MaxDuration);

            foreach (var spellPower in Powers)
            {
                rtb.AppendFormat("Power {0}", (Powers)spellPower.PowerType);
                rtb.AppendFormatIfNotNull(", Base {0}", spellPower.ManaCost);
                rtb.AppendFormatIfNotNull(", {0}%", spellPower.PowerCostPct);
                rtb.AppendFormatIfNotNull(" + lvl * {0}", spellPower.ManaCostPerLevel);
                rtb.AppendFormatIfNotNull(" + {0} Per Second", spellPower.ManaPerSecond);
                rtb.AppendFormatIfNotNull(" + {0}% Per Second", spellPower.PowerPctPerSecond);
                rtb.AppendLine();
            }

            if (interrupts != null)
            {
                rtb.AppendFormatLine("Interrupt Flags: 0x{0:X8} ({1})", interrupts.InterruptFlags, (SpellInterruptFlags)interrupts.InterruptFlags);
                rtb.AppendFormatLine("AuraInterrupt Flags: 0x{0:X8} ({1}), 0x{2:X8} ({3})", interrupts.AuraInterruptFlags[0], (SpellAuraInterruptFlags)interrupts.AuraInterruptFlags[0], interrupts.AuraInterruptFlags[1], (SpellAuraInterruptFlags)interrupts.AuraInterruptFlags[1]);
                rtb.AppendFormatLine("ChannelInterrupt Flags: 0x{0:X8} ({1}), 0x{2:X8} ({3})", interrupts.ChannelInterruptFlags[0], (SpellChannelInterruptFlags)interrupts.ChannelInterruptFlags[0], interrupts.ChannelInterruptFlags[1], (SpellChannelInterruptFlags)interrupts.ChannelInterruptFlags[1]);
            }

            if (CasterAuraState != 0)
                rtb.AppendFormatLine("CasterAuraState = {0} ({1})", CasterAuraState, (AuraState)CasterAuraState);

            if (TargetAuraState != 0)
                rtb.AppendFormatLine("TargetAuraState = {0} ({1})", TargetAuraState, (AuraState)TargetAuraState);

            if (CasterAuraStateNot != 0)
                rtb.AppendFormatLine("CasterAuraStateNot = {0} ({1})", CasterAuraStateNot, (AuraState)CasterAuraStateNot);

            if (TargetAuraStateNot != 0)
                rtb.AppendFormatLine("TargetAuraStateNot = {0} ({1})", TargetAuraStateNot, (AuraState)TargetAuraStateNot);

            if (CasterAuraSpell != 0)
            {
                if (DBC.DBC.SpellInfoStore.ContainsKey(CasterAuraSpell))
                    rtb.AppendFormatLine("  Caster Aura Spell ({0}) {1}", CasterAuraSpell, DBC.DBC.SpellInfoStore[CasterAuraSpell].Name);
                else
                    rtb.AppendFormatLine("  Caster Aura Spell ({0}) ?????", CasterAuraSpell);
            }

            if (TargetAuraSpell != 0)
            {
                if (DBC.DBC.SpellInfoStore.ContainsKey(TargetAuraSpell))
                    rtb.AppendFormatLine("  Target Aura Spell ({0}) {1}", TargetAuraSpell, DBC.DBC.SpellInfoStore[TargetAuraSpell].Name);
                else
                    rtb.AppendFormatLine("  Target Aura Spell ({0}) ?????", TargetAuraSpell);
            }

            if (ExcludeCasterAuraSpell != 0)
            {
                if (DBC.DBC.SpellInfoStore.ContainsKey(ExcludeCasterAuraSpell))
                    rtb.AppendFormatLine("  Ex Caster Aura Spell ({0}) {1}", ExcludeCasterAuraSpell, DBC.DBC.SpellInfoStore[ExcludeCasterAuraSpell].Name);
                else
                    rtb.AppendFormatLine("  Ex Caster Aura Spell ({0}) ?????", ExcludeCasterAuraSpell);
            }

            if (ExcludeTargetAuraSpell != 0)
            {
                if (DBC.DBC.SpellInfoStore.ContainsKey(ExcludeTargetAuraSpell))
                    rtb.AppendFormatLine("  Ex Target Aura Spell ({0}) {1}", ExcludeTargetAuraSpell, DBC.DBC.SpellInfoStore[ExcludeTargetAuraSpell].Name);
                else
                    rtb.AppendFormatLine("  Ex Target Aura Spell ({0}) ?????", ExcludeTargetAuraSpell);
            }

            if (RequiredAreasId > 0)
            {
                var areas = new List<int>();
                // AreaGroup rows hold up to six areas and chain on through NextGroup.
                var groupId = RequiredAreasId;
                var seen = new HashSet<int>();
                while (groupId != 0 && seen.Add(groupId) && DBC.DBC.AreaGroup != null &&
                       DBC.DBC.AreaGroup.TryGetValue(groupId, out var group))
                {
                    areas.AddRange(group.AreaId.Where(a => a != 0).Select(a => (int)a));
                    groupId = (int)group.NextGroup;
                }

                if (areas.Count == 0)
                    rtb.AppendFormatLine("Cannot find area group id {0} in AreaGroup.dbc!", RequiredAreasId);
                else
                {
                    rtb.AppendLine();
                    rtb.SetBold();
                    rtb.AppendLine("Allowed areas:");
                    foreach (var areaId in areas)
                    {
                        if (DBC.DBC.AreaTable == null || !DBC.DBC.AreaTable.ContainsKey(areaId))
                            continue;

                        var areaEntry = DBC.DBC.AreaTable[areaId];
                        rtb.AppendFormatLine("{0} - {1} (Map: {2})", areaId, areaEntry.AreaName, areaEntry.ContinentID);
                    }

                    rtb.AppendLine();
                }
            }

            rtb.AppendFormatLineIfNotNull("Requires Spell Focus {0}", RequiresSpellFocus);

            var baseProcRate = procsPerMinute?.BaseProcRate ?? 0;
            if (Math.Abs(baseProcRate) > 1.0E-5f)
            {
                rtb.SetBold();
                rtb.AppendFormatLine("PPM flag 0x{0:X2} BaseRate {1}", procsPerMinute.Flags, baseProcRate);
                rtb.SetDefaultStyle();
            }

            if (procFlags != 0)
            {
                rtb.SetBold();
                rtb.AppendFormatLine("Proc flag 0x{0:X8}, chance: {1}%, charges: {2}, cooldown: {3}",
                    procFlags, procChance, procCharges, auraOptions?.ProcCategoryRecovery ?? 0);
                rtb.SetDefaultStyle();
                rtb.AppendFormatLine(Separator);
                rtb.AppendText(DescribeProcFlags(procFlags));
            }
            else
                rtb.AppendFormatLine("Chance = {0}, charges - {1}", procChance, procCharges);

            rtb.AppendLine(Separator);
            foreach (var effect in EffectsFor(context.Difficulty))
                AppendEffectInfo(rtb, effect.Entry, context);

            AppendItemInfo(rtb);
        }

        internal float CalculateBaseEffectValue(SpellEffectEntry effect, RenderContext context)
        {
            // Non-scaling value only; the SpellScaling/gtSpellScaling path is
            // ported separately (SkyFire_548 CalcValue).
            float value = effect.EffectBasePoints;
            return (float)Math.Round(value);
        }

        // Extracted from AppendEffectInfo so the text path and
        // SpellInfoJsonSerializer render the same numbers instead of the
        // JSON side re-deriving them -- duplicated arithmetic is exactly the
        // drift this extraction exists to stop.
        internal EffectValueBreakdown CalculateEffectValues(SpellEffectEntry effect, RenderContext context)
        {
            var baseValue = CalculateBaseEffectValue(effect, context);
            var breakdown = new EffectValueBreakdown
            {
                BaseValue = baseValue,
            };

            breakdown.ValuePerResource = effect.EffectPointsPerResource;
            if (Math.Abs(effect.EffectRealPointsPerLevel) > 1.0E-5f)
                breakdown.PerLevelValue = effect.EffectRealPointsPerLevel;

            if (effect.EffectBonusCoefficient > 1.0E-5f)
                breakdown.SpellPowerCoefficient = effect.EffectBonusCoefficient;

            return breakdown;
        }

        /// <summary>
        /// Public projection of <see cref="CalculateEffectValues"/> for
        /// callers outside SpellWork.Core (the MCP server's
        /// calculate_spell_scaling tool) that need the same numbers
        /// get_spell renders without gaining write access to the internal
        /// breakdown.
        /// </summary>
        public EffectValues GetEffectValues(SpellEffectEntry effect, RenderContext context) =>
            new EffectValues(CalculateEffectValues(effect, context));

        private void AppendEffectInfo(ISpellInfoWriter rtb, SpellEffectEntry effect, RenderContext context)
        {
            rtb.SetBold();
            rtb.AppendFormatLine($"Effect { effect.EffectIndex }: Id { effect.Effect } ({ (SpellEffects)effect.Effect })");
            rtb.SetBold();
            rtb.AppendFormatLine($"Difficulty: Id { effect.DifficultyID } ({ (Difficulty)effect.DifficultyID })");
            rtb.SetDefaultStyle();

            var values = CalculateEffectValues(effect, context);
            var baseValue = values.BaseValue;
            if (Math.Abs(values.Variance) > 1.0E-5f)
            {
                var delta = Math.Abs(baseValue * values.Variance * 0.5f);
                rtb.AppendFormat("BasePoints = {0:F} to {1:F}", baseValue - delta, baseValue + delta);
            }
            else
                rtb.AppendFormat("BasePoints = {0:F}", baseValue);

            if (Math.Abs(values.PerLevelValue) > 1.0E-5f)
                rtb.AppendFormat(" + Level * {0:F}", values.PerLevelValue);

            rtb.AppendFormatIfNotNull(" + resource * {0:F}", values.ValuePerResource);

            if (values.SpellPowerCoefficient > 1.0E-5f)
                rtb.AppendFormat(" + spellPower * {0}", values.SpellPowerCoefficient);

            rtb.AppendLine();

            rtb.AppendFormatLine("Targets ({0}, {1}) ({2}, {3})",
                effect.ImplicitTarget[0], effect.ImplicitTarget[1],
                (Targets)effect.ImplicitTarget[0], (Targets)effect.ImplicitTarget[1]);

            AuraModTypeName(rtb, effect);

            var classMask = effect.EffectSpellClassMask;

            if (classMask[0] != 0 || classMask[1] != 0 || classMask[2] != 0 || classMask[3] != 0)
            {
                rtb.AppendFormatLine("SpellClassMask = {0:X8} {1:X8} {2:X8} {3:X8}", classMask[0], classMask[1], classMask[2], classMask[3]);

                var skillLineAbilities = DBC.DBC.SkillLineAbility?.Values ?? Enumerable.Empty<SkillLineAbilityEntry>();
                var query = from spell in DBC.DBC.SpellInfoStore.Values
                            where spell.SpellFamilyName == SpellFamilyName && spell.SpellClassMask.ContainsElement(classMask)
                            join sk in skillLineAbilities on spell.ID equals (int)sk.SpellId into temp
                            from skill in temp.DefaultIfEmpty(new SkillLineAbilityEntry())
                            select new
                            {
                                SpellID = spell.ID,
                                SpellName = spell.Name,
                                SkillId = skill.SkillId
                            };

                foreach (var row in query)
                {
                    if (row.SkillId > 0)
                    {
                        rtb.SetColor(TextColor.Blue);
                        rtb.AppendFormatLine("\t+ {0} - {1}", row.SpellID, row.SpellName);
                    }
                    else
                    {
                        rtb.SetColor(TextColor.Red);
                        rtb.AppendFormatLine("\t- {0} - {1}", row.SpellID, row.SpellName);
                    }
                    rtb.SetColor(TextColor.Black);
                }
            }

            AppendRadius(rtb, "", effect.EffectRadiusIndex[0]);
            AppendRadius(rtb, "Max ", effect.EffectRadiusIndex[1]);

            // append trigger spell
            var trigger = effect.EffectTriggerSpell;
            if (trigger != 0)
            {
                if (DBC.DBC.SpellInfoStore.ContainsKey(trigger))
                {
                    var triggerSpell = DBC.DBC.SpellInfoStore[trigger];
                    rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
                    rtb.AppendFormatLine("   Trigger spell ({0}) {1}. Chance = {2}", trigger, triggerSpell.Name, AuraOptionsFor(context.Difficulty)?.ProcChance ?? 0);
                    rtb.AppendFormatLineIfNotNull("   Description: {0}", triggerSpell.Spell.Description);
                    rtb.AppendFormatLineIfNotNull("   ToolTip: {0}", triggerSpell.Spell.AuraDescription);
                    rtb.SetDefaultStyle();
                    if (triggerSpell.ProcFlags != 0)
                    {
                        rtb.AppendFormatLine("Charges - {0}", triggerSpell.ProcCharges);
                        rtb.AppendLine(Separator);
                        rtb.AppendLine(triggerSpell.ProcInfo);
                        rtb.AppendLine(Separator);
                    }
                }
                else
                    rtb.AppendFormatLine("Trigger spell ({0}) Not found, Chance = {1}", trigger, AuraOptionsFor(context.Difficulty)?.ProcChance ?? 0);
            }

            rtb.AppendFormatLineIfNotNull("EffectChainTargets = {0}", effect.EffectChainTargets);
            rtb.AppendFormatLineIfNotNull("EffectItemType = {0}", effect.EffectItemType);

            if ((Mechanics)effect.EffectMechanic != Mechanics.MECHANIC_NONE)
                rtb.AppendFormatLine("Effect Mechanic = {0} ({1})", effect.EffectMechanic, (Mechanics)effect.EffectMechanic);

            rtb.AppendFormatLineIfNotNull("Attributes {0:X8} ({0})", effect.EffectAttributes);
            rtb.AppendLine();
        }

        private static void AppendRadius(ISpellInfoWriter rtb, string name, int radiusIndex)
        {
            if (DBC.DBC.SpellRadius == null || !DBC.DBC.SpellRadius.ContainsKey(radiusIndex))
                return;

            var radius = DBC.DBC.SpellRadius[radiusIndex];
            rtb.AppendFormatLine("{0}Radius (Id {1}) {2:F} (Max: {3:F})",
                name, radiusIndex, radius.Radius, radius.MaxRadius);
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
                    rtb.Append((CombatRating)misc);
                    break;
                case AuraType.SPELL_AURA_ADD_FLAT_MODIFIER:
                case AuraType.SPELL_AURA_ADD_PCT_MODIFIER:
                    rtb.Append((SpellModOp)misc);
                    break;
                // TODO: more case
                default:
                    rtb.Append(misc);
                    break;
            }

            rtb.AppendFormat("), miscB = {0}", effect.EffectMiscValue[1]);
            rtb.AppendFormatLine(", amplitude = {0}, periodic = {1}", effect.EffectAmplitude, effect.EffectAuraPeriod);

            switch (aura)
            {
                case AuraType.SPELL_AURA_OVERRIDE_SPELLS:
                    if (DBC.DBC.OverrideSpellData == null || !DBC.DBC.OverrideSpellData.ContainsKey(misc))
                    {
                        rtb.SetStyle(TextColor.Red, TextStyle.Bold);
                        rtb.AppendFormatLine("Cannot find key {0} in OverrideSpellData.dbc", (uint)misc);
                    }
                    else
                    {
                        rtb.AppendLine();
                        rtb.SetStyle(TextColor.DarkRed, TextStyle.Bold);
                        var @override = DBC.DBC.OverrideSpellData[misc];
                        for (var i = 0; i < 10; ++i)
                        {
                            if (@override.Spells[i] == 0)
                                continue;

                            rtb.SetStyle(TextColor.DarkBlue, TextStyle.Regular);
                            rtb.AppendFormatLine("\t - #{0} ({1}) {2}", i + 1, @override.Spells[i],
                                DBC.DBC.SpellInfoStore.ContainsKey((int)@override.Spells[i]) ? DBC.DBC.SpellInfoStore[(int)@override.Spells[i]].Name : "?????");
                        }
                        rtb.AppendLine();
                    }
                    break;
                case AuraType.SPELL_AURA_SCREEN_EFFECT:
                    rtb.SetStyle(TextColor.DarkBlue, TextStyle.Bold);
                    rtb.AppendFormatLine("ScreenEffect: {0}",
                        DBC.DBC.ScreenEffect != null && DBC.DBC.ScreenEffect.ContainsKey(misc) ? DBC.DBC.ScreenEffect[misc].Name : "?????");
                    break;
            }
        }

        // 5.4.8 has no ItemEffect table: an item's on-use/equip spells are the
        // five SpellId columns of Item-sparse.db2.
        private void AppendItemInfo(ISpellInfoWriter rtb)
        {
            if (DBC.DBC.ItemSparse == null)
                return;

            var items = DBC.DBC.ItemSparse.Values.Where(item => item.SpellId.Contains(ID)).ToArray();
            if (!items.Any())
                return;

            rtb.AppendLine(Separator);
            rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
            rtb.AppendLine("Items using this spell:");
            rtb.SetDefaultStyle();

            foreach (var item in items)
            {
                var name = item.Name;
                var description = item.Description;

                description = string.IsNullOrEmpty(description) ? string.Empty : $" - \"{ description }\"";

                rtb.AppendFormatLine($@"   { item.ID }: { name } { description }");
            }
        }

        public bool HasEffect(SpellEffects effect)
        {
            return Effects.Any(eff => eff != null && eff.Entry.Effect == (int)effect);
        }

        public bool HasAura(AuraType aura)
        {
            return Effects.Any(eff => eff != null && eff.Entry.EffectAura == (int)aura);
        }

        public bool HasTargetA(Targets target)
        {
            return Effects.Any(eff => eff != null && eff.Entry.ImplicitTarget[0] == (int)target);
        }

        public bool HasTargetB(Targets target)
        {
            return Effects.Any(eff => eff != null && eff.Entry.ImplicitTarget[1] == (int)target);
        }
    }
}
