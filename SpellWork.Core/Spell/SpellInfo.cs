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
        public float MinValue;
        public float MaxValue;
        public float ValuePerResource;
        public float RealPointsPerLevel;
        public float SpellPowerCoefficient;
        public bool Scaled;
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
            MinValue = breakdown.MinValue;
            MaxValue = breakdown.MaxValue;
            ValuePerResource = breakdown.ValuePerResource;
            RealPointsPerLevel = breakdown.RealPointsPerLevel;
            SpellPowerCoefficient = breakdown.SpellPowerCoefficient;
            Scaled = breakdown.Scaled;
        }

        public float BaseValue { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
        public float ValuePerResource { get; }
        public float RealPointsPerLevel { get; }
        public float SpellPowerCoefficient { get; }
        public bool Scaled { get; }
    }

    /// <summary>
    /// One effect slot of the spell's Spell row. 3.3.5 has no per-difficulty
    /// effect rows (a difficulty is a different Spell row, see
    /// <see cref="DBC.SpellDifficultyResolver"/>), so <see cref="ResolvedDifficulty"/>
    /// is always 0; it stays for the consumers that report it.
    /// </summary>
    public sealed class ResolvedEffect
    {
        public SpellEffectEntry Entry { get; set; }
        public int ResolvedDifficulty { get; set; }
    }

    /// <summary>
    /// A spell as built from its one 3.3.5 <see cref="SpellEntry"/> row (spec
    /// section 4.2) plus the small lookup rows it names by id.
    /// </summary>
    public class SpellInfo
    {
        public SpellEntry Spell { get; }
        public SpellRuneCostEntry RuneCost { get; }
        public SpellDescriptionVariablesEntry DescriptionVariables { get; }
        public SpellDurationEntry DurationEntry { get; }
        public SpellRangeEntry Range { get; }
        public SpellCastTimesEntry CastTimeEntry { get; }
        public SpellIconEntry Icon { get; }
        /// <summary>The SpellCategory.dbc row of <see cref="CategoryID"/> (category flags), or null.</summary>
        public SpellCategoryEntry Category { get; }

        /// <summary>The non-zero effect slots (Effect[i] != 0), in index order.</summary>
        public IReadOnlyList<ResolvedEffect> Effects { get; }

        #region SpellDuration
        public int Duration => DurationEntry?.Duration ?? 0;
        public int DurationPerLevel => DurationEntry?.DurationPerLevel ?? 0;
        public int MaxDuration => DurationEntry?.MaxDuration ?? 0;
        #endregion

        #region Spell
        public int ID => Spell.ID;
        public string Name => Spell.Name;
        public string Description => Spell?.Description ?? string.Empty;
        public string Tooltip => Spell?.AuraDescription ?? string.Empty;

        /// <summary>The 8 attribute words (Attributes, AttributesEx .. AttributesEx7).</summary>
        public uint[] Attributes => Spell.Attributes;
        public uint AttributesEx => Attributes[1];
        public uint AttributesEx2 => Attributes[2];
        public uint AttributesEx3 => Attributes[3];
        public uint AttributesEx4 => Attributes[4];
        public uint AttributesEx5 => Attributes[5];
        public uint AttributesEx6 => Attributes[6];
        public uint AttributesEx7 => Attributes[7];

        public float Speed => Spell.Speed;
        public int CastingTimeIndex => Spell.CastingTimeIndex;
        public int ActiveIconID => Spell.ActiveIconID;
        public int SpellIconID => Spell.SpellIconID;
        public int SpellVisualID => Spell.SpellVisualID[0];
        public int RangeIndex => Spell.RangeIndex;
        public int SchoolMask => Spell.SchoolMask;
        public int ModalNextSpell => Spell.ModalNextSpell;
        public uint SpellFamilyName => (uint)Spell.SpellClassSet;
        /// <summary>The spell's three SpellClassMask words (family flags).</summary>
        public uint[] SpellClassMask => Spell.SpellClassMask;
        public int DamageClass => Spell.DefenseType;
        public int PreventionType => Spell.PreventionType;
        public int CategoryID => Spell.Category;
        public int Dispel => Spell.DispelType;
        public int Mechanic => Spell.Mechanic;

        // Two 32-bit words per mask, low word first.
        public ulong Stances => ((ulong)Spell.ShapeshiftMask[1] << 32) | Spell.ShapeshiftMask[0];
        public ulong StancesNot => ((ulong)Spell.ShapeshiftExclude[1] << 32) | Spell.ShapeshiftExclude[0];

        public int CategoryRecoveryTime => Spell.CategoryRecoveryTime;
        public int RecoveryTime => Spell.RecoveryTime;
        public int StartRecoveryTime => Spell.StartRecoveryTime;
        public int StartRecoveryCategory => Spell.StartRecoveryCategory;

        public int CasterAuraState => Spell.CasterAuraState;
        public int TargetAuraState => Spell.TargetAuraState;
        public int CasterAuraStateNot => Spell.ExcludeCasterAuraState;
        public int TargetAuraStateNot => Spell.ExcludeTargetAuraState;
        public int CasterAuraSpell => Spell.CasterAuraSpell;
        public int TargetAuraSpell => Spell.TargetAuraSpell;
        public int ExcludeCasterAuraSpell => Spell.ExcludeCasterAuraSpell;
        public int ExcludeTargetAuraSpell => Spell.ExcludeTargetAuraSpell;

        public int ProcCharges => Spell.ProcCharges;
        public int ProcChance => Spell.ProcChance;
        public uint ProcFlags => Spell.ProcTypeMask;
        public int CumulativeAura => Spell.CumulativeAura;

        public int BaseLevel => Spell.BaseLevel;
        public int MaxLevel => Spell.MaxLevel;
        public int SpellLevel => Spell.SpellLevel;

        public int EquippedItemClass => Spell.EquippedItemClass;
        public uint EquippedItemInvTypes => (uint)Spell.EquippedItemInvTypes;
        public uint EquippedItemSubclass => (uint)Spell.EquippedItemSubclass;

        public int RequiredAreasId => Spell.RequiredAreasID;
        public uint FacingCasterFlags => Spell.FacingCasterFlags;
        public int MinFactionID => Spell.MinFactionID;
        public int MinReputation => Spell.MinReputation;
        public int RequiredAuraVision => Spell.RequiredAuraVision;
        public int RequiresSpellFocus => Spell.RequiresSpellFocus;

        public uint AuraInterruptFlags => Spell.AuraInterruptFlags;
        public uint ChannelInterruptFlags => Spell.ChannelInterruptFlags;
        public uint InterruptFlags => Spell.InterruptFlags;

        /// <summary>Reagent item ids with their counts; slots with a zero item or count are skipped.</summary>
        public (int Item, int Count)[] Reagents =>
            Enumerable.Range(0, Spell.Reagent.Length)
                .Where(i => Spell.Reagent[i] != 0 && Spell.ReagentCount[i] != 0)
                .Select(i => (Spell.Reagent[i], Spell.ReagentCount[i]))
                .ToArray();

        /// <summary>The two Totem (item id) columns, raw.</summary>
        public int[] Totems => Spell.Totem;

        /// <summary>The two RequiredTotemCategoryID columns, raw.</summary>
        public int[] TotemCategories => Spell.RequiredTotemCategoryID;
        #endregion

        private const string Separator = "=================================================";

        public SpellInfo(SpellEntry spell)
        {
            Spell = spell ?? throw new ArgumentNullException(nameof(spell));

            RuneCost = Lookup(DBC.DBC.SpellRuneCost, spell.RuneCostID);
            DescriptionVariables = Lookup(DBC.DBC.SpellDescriptionVariables, spell.SpellDescriptionVariableID);
            DurationEntry = Lookup(DBC.DBC.SpellDuration, spell.DurationIndex);
            Range = Lookup(DBC.DBC.SpellRange, spell.RangeIndex);
            CastTimeEntry = Lookup(DBC.DBC.SpellCastTimes, spell.CastingTimeIndex);
            Icon = Lookup(DBC.DBC.SpellIcon, spell.SpellIconID);
            Category = Lookup(DBC.DBC.SpellCategory, spell.Category);

            var effects = new List<ResolvedEffect>();
            for (var i = 0; i < DBC.DBC.MaxEffectIndex; ++i)
                if (spell.Effect[i] != 0)
                    effects.Add(new ResolvedEffect { Entry = SpellEffectEntry.FromSpell(spell, i), ResolvedDifficulty = 0 });
            Effects = effects;
        }

        // Id 0 means "no row"; a missing store or row leaves the member null.
        private static T Lookup<T>(IDictionary<int, T> store, int id) where T : class =>
            id != 0 && store != null && store.TryGetValue(id, out var row) ? row : null;

        public void Write(ISpellInfoWriter rtb, RenderContext context)
        {
            // 3.3.5: everything comes from the one Spell row; a difficulty is
            // a different spell id (resolved by the caller), not other rows.
            var attributes = Attributes;
            var durationEntry = DurationEntry;
            var range = Range;
            var schoolMask = SchoolMask;
            var category = CategoryID;
            var procFlags = ProcFlags;
            var procChance = ProcChance;
            var procCharges = ProcCharges;

            rtb.Clear();

            rtb.SetBold();
            rtb.AppendFormatLine("ID - {0} {1}{2}",
                ID, Name, string.IsNullOrEmpty(Spell.NameSubtext) ? string.Empty : $" ({Spell.NameSubtext})");
            rtb.SetDefaultStyle();

            rtb.AppendFormatLine(Separator);

            WriteDifficulty(rtb, SpellDifficultyView.For(ID, context.Difficulty));

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
                foreach (var procSpellId in DBC.DBC.SpellTriggerStore[ID].OrderBy(id => id))
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

            if (RuneCost != null)
                rtb.AppendFormatLine("Rune cost: Blood {0}, Unholy {1}, Frost {2}, RunicPower {3}",
                    RuneCost.RuneCost[0], RuneCost.RuneCost[1], RuneCost.RuneCost[2], RuneCost.RunicPower);

            rtb.AppendFormatLine($"Category = { category }, SpellIconID = { SpellIconID }, ActiveIconID = { ActiveIconID }, SpellVisualID = { Spell.SpellVisualID[0] }, { Spell.SpellVisualID[1] }");

            rtb.AppendFormatLine("Family {0} ({1}), flag [0] 0x{2:X8} [1] 0x{3:X8} [2] 0x{4:X8}",
                    (SpellFamilyNames)SpellFamilyName, SpellFamilyName,
                    SpellClassMask[0], SpellClassMask[1], SpellClassMask[2]);

            #region Modified by ...
            foreach (var eff in
                    from s in DBC.DBC.SpellInfoStore.Values.OrderBy(x => x.ID)
                    where s.SpellFamilyName == SpellFamilyName
                    from eff in s.Effects
                    where eff != null && ((eff.Entry.EffectSpellClassMask[0] & SpellClassMask[0]) != 0 ||
                          (eff.Entry.EffectSpellClassMask[1] & SpellClassMask[1]) != 0 ||
                          (eff.Entry.EffectSpellClassMask[2] & SpellClassMask[2]) != 0)
                    select eff.Entry)
            {
                rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
                rtb.AppendFormatLine("Modified by {0} ({1})",
                    DBC.DBC.SpellInfoStore[eff.SpellID].Name, eff.SpellID);
            }
            #endregion

            rtb.AppendLine();

            var damageClass = DamageClass;
            var preventionType = PreventionType;
            rtb.AppendFormatLine("SpellSchoolMask = {0} ({1})", (SpellSchoolMask)schoolMask, schoolMask);
            rtb.AppendFormatLine("DamageClass = {0} ({1})", damageClass, (SpellDmgClass)damageClass);
            rtb.AppendFormatLine("PreventionType = {0} ({1})", preventionType, (SpellPreventionType)preventionType);

            #region Attributes
            if (!attributes.All(a => a == 0))
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

            rtb.AppendLine(Separator);
            #endregion

            if (Spell.Targets != 0)
                rtb.AppendFormatLine("Targets Mask = 0x{0:X8} ({1})", Spell.Targets, (SpellCastTargetFlags)Spell.Targets);

            if (Spell.TargetCreatureType != 0)
                rtb.AppendFormatLine("Creature Type Mask = 0x{0:X8} ({1})",
                    Spell.TargetCreatureType, (CreatureTypeMask)Spell.TargetCreatureType);

            rtb.AppendFormatLineIfNotNull("MaxTargets: {0}", Spell.MaxTargets);
            rtb.AppendFormatLineIfNotNull("MaxTargetLevel: {0}", Spell.MaxTargetLevel);

            if (Stances != 0)
                rtb.AppendFormatLine("Stances: {0}", (ShapeshiftFormMask)Stances);

            if (StancesNot != 0)
                rtb.AppendFormatLine("Stances Not: {0}", (ShapeshiftFormMask)StancesNot);

            // Skills
            if (DBC.DBC.SkillLineAbility != null)
            {
                var query = DBC.DBC.SkillLineAbility.Where(skl => skl.Value.Spell == ID).ToArray();
                if (query.Length != 0)
                {
                    var skill = query.First().Value;
                    var line = DBC.DBC.SkillLine != null && DBC.DBC.SkillLine.TryGetValue(skill.SkillLine, out var l) ? l : null;

                    rtb.AppendFormatLine(@"Skill (Id {0}) ""{1}""", skill.SkillLine, line?.DisplayName);
                    rtb.AppendFormat("    ReqSkillValue {0}", skill.MinSkillLineRank);

                    rtb.AppendFormatLine(", Forward spell = {0}, MinMaxValue ({1}, {2})", skill.SupercededBySpell,
                        skill.TrivialSkillLineRankLow, skill.TrivialSkillLineRankHigh);
                }
            }

            if (TotemCategories.Any(c => c != 0) || Totems.Any(t => t != 0))
                rtb.AppendFormatLine("Totems: TotemCategory [{0}, {1}], Totem (item) [{2}, {3}]",
                    TotemCategories[0], TotemCategories[1], Totems[0], Totems[1]);

            var reagents = Reagents;
            if (reagents.Length != 0)
            {
                rtb.AppendLine();
                rtb.Append("Reagents:");
                foreach (var (item, count) in reagents)
                    rtb.AppendFormat("  {0} x{1}", item, count);
                rtb.AppendLine();
            }

            rtb.AppendFormatLine("Spell Level = {0}, base {1}, max {2}", SpellLevel, BaseLevel, MaxLevel);

            if (EquippedItemClass >= 0)
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
            var dispel = Dispel;
            var mechanic = Mechanic;
            if (Category != null)
                rtb.AppendFormatLine("Category = {0}, flags 0x{1:X2} ({2})", category, Category.Flags, (SpellCategoryFlags)Category.Flags);
            else
                rtb.AppendFormatLine("Category = {0}", category);
            rtb.AppendFormatLine("DispelType = {0} ({1})", dispel, (DispelType)dispel);
            rtb.AppendFormatLine("Mechanic = {0} ({1})", mechanic, (Mechanics)mechanic);

            if (range != null)
            {
                rtb.AppendFormatLine("SpellRange: (Id {0}) \"{1}\":", RangeIndex, range.DisplayName);
                rtb.AppendFormatLine("    MinRange = {0} (hostile), {1} (friendly)", range.RangeMin[0], range.RangeMin[1]);
                rtb.AppendFormatLine("    MaxRange = {0} (hostile), {1} (friendly)", range.RangeMax[0], range.RangeMax[1]);
            }

            rtb.AppendFormatLineIfNotNull("Speed {0:F}", Speed);

            rtb.AppendFormatLineIfNotNull("Stackable up to {0}", CumulativeAura);

            if (CastTimeEntry != null)
            {
                // 3.3.5 SpellInfo::CalcCastTime: Base + PerLevel * SpellLevel-relative level, floored at Minimum.
                var level = (int)context.Level - SpellLevel;
                var castTime = CastTimeEntry.Base + CastTimeEntry.PerLevel * level;
                if (castTime < CastTimeEntry.Minimum)
                    castTime = CastTimeEntry.Minimum;

                rtb.AppendFormatLine("Cast Time: (ID {0}): {1}", CastingTimeIndex, castTime);
            }

            if (RecoveryTime != 0 || CategoryRecoveryTime != 0 || StartRecoveryCategory != 0)
            {
                rtb.AppendFormatLine("RecoveryTime: {0} ms, CategoryRecoveryTime: {1} ms", RecoveryTime, CategoryRecoveryTime);
                rtb.AppendFormatLine("StartRecoveryCategory = {0}, StartRecoveryTime = {1:F} ms", StartRecoveryCategory, StartRecoveryTime);
            }

            if (durationEntry != null)
                rtb.AppendFormatLine("Duration {0}, {1}, {2}", durationEntry.Duration, durationEntry.DurationPerLevel, durationEntry.MaxDuration);

            if (Spell.ManaCost != 0 || Spell.ManaCostPct != 0 || Spell.ManaCostPerLevel != 0 || Spell.ManaPerSecond != 0)
            {
                rtb.AppendFormat("Power {0}", (Powers)Spell.PowerType);
                rtb.AppendFormatIfNotNull(", Base {0}", Spell.ManaCost);
                rtb.AppendFormatIfNotNull(", {0}%", Spell.ManaCostPct);
                rtb.AppendFormatIfNotNull(" + lvl * {0}", Spell.ManaCostPerLevel);
                rtb.AppendFormatIfNotNull(" + {0} Per Second", Spell.ManaPerSecond);
                rtb.AppendFormatIfNotNull(" + lvl * {0} Per Second", Spell.ManaPerSecondPerLevel);
                rtb.AppendLine();
            }

            rtb.AppendFormatLine("Interrupt Flags: 0x{0:X8} ({1})", InterruptFlags, (SpellInterruptFlags)InterruptFlags);
            rtb.AppendFormatLine("AuraInterrupt Flags: 0x{0:X8} ({1})", AuraInterruptFlags, (SpellAuraInterruptFlags)AuraInterruptFlags);
            rtb.AppendFormatLine("ChannelInterrupt Flags: 0x{0:X8} ({1})", ChannelInterruptFlags, (SpellChannelInterruptFlags)ChannelInterruptFlags);

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
                    areas.AddRange(group.AreaID.Where(a => a != 0));
                    groupId = group.NextAreaID;
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

            if (procFlags != 0)
            {
                rtb.SetBold();
                rtb.AppendFormatLine("Proc flag 0x{0:X8}, chance: {1}%, charges: {2}",
                    procFlags, procChance, procCharges);
                rtb.SetDefaultStyle();
                rtb.AppendFormatLine(Separator);
                rtb.AppendText(ProcInfo.DescribeProcFlags(procFlags));
            }
            else
                rtb.AppendFormatLine("Chance = {0}, charges - {1}", procChance, procCharges);

            rtb.AppendLine(Separator);
            foreach (var effect in Effects)
                AppendEffectInfo(rtb, effect, context);


            WriteVisualChain(rtb, SpellVisualChainResolver.ResolveSection(ID));
        }

        /// <summary>
        /// The SpellDifficulty line(s): printed only when the spell is in a
        /// SpellDifficulty row or a mode was requested.
        /// </summary>
        private static void WriteDifficulty(ISpellInfoWriter rtb, SpellDifficultyView difficulty)
        {
            if (difficulty.Variants.Count != 0)
                rtb.AppendFormatLine("SpellDifficulty variants: {0}", string.Join(", ", difficulty.Variants));

            if (difficulty.Requested.HasValue)
                rtb.AppendFormatLine("Difficulty mode {0} ({1}) resolves to spell {2}",
                    difficulty.Requested.Value, DifficultyModeNames[difficulty.Requested.Value], difficulty.ResolvedSpellId);
        }

        private static readonly string[] DifficultyModeNames = { "10N", "25N", "10H", "25H" };

        /// <summary>
        /// The text rendering of the visual chain. Branches on the same
        /// <see cref="SpellVisualSection"/> get_spell's JSON and
        /// get_spell_visual_chain use, so it cannot print an empty block when a
        /// store is missing (spec §6).
        /// </summary>
        internal static void WriteVisualChain(ISpellInfoWriter rtb, SpellVisualSection section)
        {
            if (section.Unavailable)
            {
                AppendVisualHeader(rtb);
                rtb.AppendFormatLine("Visual chain unavailable -- these stores did not load: {0}.",
                    string.Join(", ", section.MissingStores));
                return;
            }

            var chain = section.Chain;
            if (chain.Variants.Count == 0)
                return;

            AppendVisualHeader(rtb);
            var kitsById = chain.Kits.ToDictionary(k => k.KitID);

            foreach (var variant in chain.Variants)
            {
                if (!variant.VisualResolved)
                {
                    rtb.AppendFormatLine("Visual (slot {0}): SpellVisual #{1} -- no SpellVisual row", variant.Slot, variant.SpellVisualID);
                    continue;
                }

                rtb.AppendFormatLine("Visual (slot {0}): SpellVisual #{1}", variant.Slot, variant.SpellVisualID);

                foreach (var slot in variant.KitSlots)
                {
                    if (!kitsById.TryGetValue(slot.KitID, out var kit))
                    {
                        // Either no SpellVisualKit row (listed under Unresolved
                        // below) or withheld by the kit cap (Truncated).
                        rtb.AppendFormatLine("  {0} #{1}: not shown", slot.Slot, slot.KitID);
                        continue;
                    }

                    var effects = kit.Effects.Count == 0
                        ? "(no effects)"
                        : string.Join(", ", kit.Effects.Select(e => $"{e.Name ?? "#" + e.EffectNameID + " unresolved"} ({e.Slot})"));
                    rtb.AppendFormatLine("  {0} #{1}: {2}", slot.Slot, slot.KitID, effects);

                    foreach (var attach in kit.ModelAttachments)
                        rtb.AppendFormatLine("    ModelAttach #{0} (attachment {1}): {2}", attach.ID, attach.AttachmentID,
                            attach.Name ?? "#" + attach.EffectNameID + " unresolved");
                }

                if (variant.Unk14MissileMotion != 0)
                    rtb.AppendFormatLine("  Unk14MissileMotion: {0} (unverified column, raw value)", variant.Unk14MissileMotion);

                foreach (var missile in variant.Missiles)
                {
                    var motion = missile.MotionID == 0
                        ? string.Empty
                        : $" (motion #{missile.MotionID} {missile.MotionName ?? "unresolved"})";
                    rtb.AppendFormatLine("  Missile #{0}: {1}{2}", missile.ID,
                        missile.Name ?? (missile.EffectNameID == 0 ? "no model" : "#" + missile.EffectNameID + " unresolved"), motion);
                }
            }

            if (chain.Truncated)
                rtb.AppendFormatLine("(more kits are reachable than the {0}-kit cap shows)", SpellVisualChainResolver.DefaultMaxKits);

            foreach (var reference in chain.UnresolvedReferences)
                rtb.AppendFormatLine("Unresolved: {0}", reference);
        }

        private static void AppendVisualHeader(ISpellInfoWriter rtb)
        {
            rtb.AppendLine(Separator);
            rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
            rtb.AppendLine("Spell visual:");
            rtb.SetDefaultStyle();
        }

        // 3.3.5 SpellEffectInfo::CalcValue (see SpellScalingCalculator).
        internal (float Base, float Min, float Max, float PerComboPoint) CalculateEffectRange(ResolvedEffect effect, RenderContext context)
        {
            var entry = effect.Entry;
            return SpellScalingCalculator.Value(entry.EffectBasePoints, entry.EffectDieSides,
                entry.EffectRealPointsPerLevel, entry.EffectPointsPerCombo,
                SpellLevel, BaseLevel, MaxLevel, (int)context.Level);
        }

        internal float CalculateBaseEffectValue(ResolvedEffect effect, RenderContext context) =>
            CalculateEffectRange(effect, context).Base;

        // Extracted from AppendEffectInfo so the text path and
        // SpellInfoJsonSerializer render the same numbers instead of the
        // JSON side re-deriving them -- duplicated arithmetic is exactly the
        // drift this extraction exists to stop.
        internal EffectValueBreakdown CalculateEffectValues(ResolvedEffect resolved, RenderContext context)
        {
            var effect = resolved.Entry;
            var range = CalculateEffectRange(resolved, context);
            var breakdown = new EffectValueBreakdown
            {
                BaseValue = range.Base,
                MinValue = range.Min,
                MaxValue = range.Max,
                ValuePerResource = range.PerComboPoint,
            };

            if (Math.Abs(effect.EffectRealPointsPerLevel) > 1.0E-5f)
                breakdown.RealPointsPerLevel = effect.EffectRealPointsPerLevel;

            // CalcValue applies the level term only for a non-zero RealPointsPerLevel.
            breakdown.Scaled = effect.EffectRealPointsPerLevel != 0.0f;

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
        public EffectValues GetEffectValues(ResolvedEffect effect, RenderContext context) =>
            new EffectValues(CalculateEffectValues(effect, context));

        private void AppendEffectInfo(ISpellInfoWriter rtb, ResolvedEffect resolved, RenderContext context)
        {
            var effect = resolved.Entry;
            rtb.SetBold();
            rtb.AppendFormatLine($"Effect { effect.EffectIndex }: Id { effect.Effect } ({ (SpellEffects)effect.Effect })");
            rtb.SetDefaultStyle();

            var values = CalculateEffectValues(resolved, context);
            var baseValue = values.BaseValue;
            if (values.MinValue != values.MaxValue)
                rtb.AppendFormat("BasePoints = {0:F} to {1:F}", values.MinValue, values.MaxValue);
            else
                rtb.AppendFormat("BasePoints = {0:F}", baseValue);

            if (Math.Abs(values.RealPointsPerLevel) > 1.0E-5f)
                rtb.AppendFormat(" (includes {0:F} per level)", values.RealPointsPerLevel);

            rtb.AppendFormatIfNotNull(" + combo * {0:F}", values.ValuePerResource);
            rtb.AppendLine();

            rtb.AppendFormatLine("Targets ({0}, {1}) ({2}, {3})",
                effect.ImplicitTarget[0], effect.ImplicitTarget[1],
                (Targets)effect.ImplicitTarget[0], (Targets)effect.ImplicitTarget[1]);

            AuraModTypeName(rtb, effect, baseValue);

            var classMask = effect.EffectSpellClassMask;

            if (classMask[0] != 0 || classMask[1] != 0 || classMask[2] != 0)
            {
                rtb.AppendFormatLine("SpellClassMask = {0:X8} {1:X8} {2:X8}", classMask[0], classMask[1], classMask[2]);

                var skillLineAbilities = DBC.DBC.SkillLineAbility?.Values ?? Enumerable.Empty<SkillLineAbilityEntry>();
                var query = from spell in DBC.DBC.SpellInfoStore.Values.OrderBy(x => x.ID)
                            where spell.SpellFamilyName == SpellFamilyName && spell.SpellClassMask.ContainsElement(classMask)
                            join sk in skillLineAbilities on spell.ID equals sk.Spell into temp
                            from skill in temp.DefaultIfEmpty(new SkillLineAbilityEntry())
                            select new
                            {
                                SpellID = spell.ID,
                                SpellName = spell.Name,
                                SkillId = skill.SkillLine
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

            AppendRadius(rtb, "", effect.EffectRadiusIndex);

            // append trigger spell
            var trigger = effect.EffectTriggerSpell;
            if (trigger != 0)
            {
                if (DBC.DBC.SpellInfoStore.ContainsKey(trigger))
                {
                    var triggerSpell = DBC.DBC.SpellInfoStore[trigger];
                    rtb.SetStyle(TextColor.Blue, TextStyle.Bold);
                    rtb.AppendFormatLine("   Trigger spell ({0}) {1}. Chance = {2}", trigger, triggerSpell.Name, ProcChance);
                    rtb.AppendFormatLineIfNotNull("   Description: {0}", triggerSpell.Spell.Description);
                    rtb.AppendFormatLineIfNotNull("   ToolTip: {0}", triggerSpell.Spell.AuraDescription);
                    rtb.SetDefaultStyle();
                    if (triggerSpell.ProcFlags != 0)
                    {
                        rtb.AppendFormatLine("Charges - {0}", triggerSpell.ProcCharges);
                        rtb.AppendLine(Separator);
                        rtb.AppendLine(ProcInfo.DescribeProcFlags(triggerSpell.ProcFlags));
                        rtb.AppendLine(Separator);
                    }
                }
                else
                    rtb.AppendFormatLine("Trigger spell ({0}) Not found, Chance = {1}", trigger, ProcChance);
            }

            rtb.AppendFormatLineIfNotNull("EffectChainTargets = {0}", effect.EffectChainTargets);
            rtb.AppendFormatLineIfNotNull("EffectItemType = {0}", effect.EffectItemType);

            if ((Mechanics)effect.EffectMechanic != Mechanics.MECHANIC_NONE)
                rtb.AppendFormatLine("Effect Mechanic = {0} ({1})", effect.EffectMechanic, (Mechanics)effect.EffectMechanic);

            rtb.AppendFormatLineIfNotNull("DamageMultiplier = {0}", effect.EffectChainAmplitude);
            rtb.AppendFormatLineIfNotNull("BonusMultiplier = {0}", effect.EffectBonusCoefficient);
            rtb.AppendLine();
        }

        private static void AppendRadius(ISpellInfoWriter rtb, string name, int radiusIndex)
        {
            if (DBC.DBC.SpellRadius == null || !DBC.DBC.SpellRadius.ContainsKey(radiusIndex))
                return;

            var radius = DBC.DBC.SpellRadius[radiusIndex];
            rtb.AppendFormatLine("{0}Radius (Id {1}) {2:F} (Max: {3:F})",
                name, radiusIndex, radius.Radius, radius.RadiusMax);
        }

        private static void AuraModTypeName(ISpellInfoWriter rtb, SpellEffectEntry effect, float value)
        {
            var aura = (AuraType)effect.EffectAura;
            var misc = effect.EffectMiscValue[0];

            if (effect.EffectAura == 0)
            {
                rtb.AppendFormatLineIfNotNull("EffectMiscValueA = {0}", effect.EffectMiscValue[0]);
                rtb.AppendFormatLineIfNotNull("EffectMiscValueB = {0}", effect.EffectMiscValue[1]);
                rtb.AppendFormatLineIfNotNull("EffectAuraPeriod = {0}", effect.EffectAuraPeriod);
                rtb.AppendFormatLineIfNotNull("EffectAmplitude = {0}", effect.EffectAmplitude);

                return;
            }

            rtb.AppendFormat("Aura Id {0:D} ({0})", aura);
            rtb.AppendFormat(", value = {0}", value);
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
                                DBC.DBC.SpellInfoStore.ContainsKey(@override.Spells[i]) ? DBC.DBC.SpellInfoStore[@override.Spells[i]].Name : "?????");
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
