using System.Collections.Generic;
using System.Globalization;

namespace SpellWork.DBC
{
    public sealed class LayoutMismatch
    {
        public string Table { get; set; }
        public string File { get; set; }
        public int ExpectedFieldsCount { get; set; }
        public int ActualFieldsCount { get; set; }
        public int ExpectedRecordSize { get; set; }
        public int ActualRecordSize { get; set; }

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0}: {1} fields / {2}-byte records (expected {3} / {4}). The structure was written against the 5.4.8 (18414) layout; " +
            "a different file (stale extraction, other build) decodes into plausible but wrong values.",
            File, ActualFieldsCount, ActualRecordSize, ExpectedFieldsCount, ExpectedRecordSize);
    }

    public static class LayoutVerification
    {
        public static readonly IReadOnlyDictionary<string, (int FieldsCount, int RecordSize)> Expected =
            new Dictionary<string, (int, int)>
            {
                // Read from the 5.4.8 (18414) reference extraction's headers; the
                // field lists were checked against WoWDBDefs. Keyed by file name.
                ["Spell"] = (25, 100),
                ["SpellMisc"] = (26, 104),
                ["SpellEffect"] = (30, 120),
                ["SpellEffectScaling"] = (6, 24),
                ["SpellPower"] = (13, 52),
                ["SpellTargetRestrictions"] = (9, 36),
                ["SpellAuraOptions"] = (9, 36),
                ["SpellCategories"] = (10, 40),
                ["SpellCooldowns"] = (6, 24),
                ["SpellInterrupts"] = (8, 32),
                ["SpellLevels"] = (6, 24),
                ["SpellScaling"] = (9, 36),
                ["SpellRuneCost"] = (6, 24),
                ["gtSpellScaling"] = (2, 8),
                ["Difficulty"] = (12, 48),
                ["SpellVisual"] = (30, 120),
                ["SpellVisualKit"] = (39, 156),
                ["SpellVisualMissile"] = (17, 68),
                ["SpellVisualEffectName"] = (9, 36),
                ["SpellVisualKitModelAttach"] = (15, 60),
                ["SpellVisualKitAreaModel"] = (7, 28),
                ["SpellAuraRestrictions"] = (11, 44),
                ["SpellCastingRequirements"] = (7, 28),
                ["SpellCastTimes"] = (4, 16),
                ["SpellClassOptions"] = (7, 28),
                ["SpellDescriptionVariables"] = (2, 8),
                ["SpellDuration"] = (4, 16),
                ["SpellEquippedItems"] = (6, 24),
                ["SpellRadius"] = (5, 20),
                ["SpellRange"] = (8, 32),
                ["SpellShapeshift"] = (6, 24),
                ["SpellTotems"] = (5, 20),
                ["SpellReagents"] = (19, 76),
                ["SpellMissile"] = (15, 60),
                ["SpellMissileMotion"] = (5, 20),
                ["AreaGroup"] = (8, 32),
                ["AreaTable"] = (30, 120),
                ["OverrideSpellData"] = (13, 52),
                ["ScreenEffect"] = (14, 56),
                ["SkillLine"] = (9, 36),
                ["SkillLineAbility"] = (13, 52),
                ["Item"] = (8, 32),
                ["Item-sparse"] = (134, 536),
                ["SpellProcsPerMinute"] = (3, 12),
                ["RandPropPoints"] = (16, 64),
                ["SpellChainEffects"] = (58, 220),
            };

        public static LayoutMismatch Check(string fileName, string openedPath, int fieldsCount, int recordSize)
        {
            if (!Expected.TryGetValue(fileName, out var e))
                return null;
            if (e.FieldsCount == fieldsCount && e.RecordSize == recordSize)
                return null;
            return new LayoutMismatch
            {
                Table = fileName, File = System.IO.Path.GetFileName(openedPath),
                ExpectedFieldsCount = e.FieldsCount, ActualFieldsCount = fieldsCount,
                ExpectedRecordSize = e.RecordSize, ActualRecordSize = recordSize,
            };
        }
    }
}
