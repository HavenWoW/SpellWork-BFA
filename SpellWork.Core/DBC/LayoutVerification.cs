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
            "{0}: {1} fields / {2}-byte records (expected {3} / {4}). The structure was written against the CoA 3.3.5a (12340) layout; " +
            "a different file (stale extraction, other build) decodes into plausible but wrong values.",
            File, ActualFieldsCount, ActualRecordSize, ExpectedFieldsCount, ExpectedRecordSize);
    }

    public static class LayoutVerification
    {
        public static readonly IReadOnlyDictionary<string, (int FieldsCount, int RecordSize)> Expected =
            new Dictionary<string, (int, int)>
            {
                // Spec section 4.1: read from the CoA 3.3.5a (12340) reference
                // extraction's WDBC headers; the field lists follow the CoA
                // DBCfmt.h/DBCStructure.h and WoWDBDefs 3.3.5.12340. Keyed by file name.
                ["Spell"] = (234, 936),
                ["SpellCastTimes"] = (4, 16),
                ["SpellDuration"] = (4, 16),
                ["SpellRadius"] = (4, 16),
                ["SpellRange"] = (40, 160),
                ["SpellRuneCost"] = (5, 20),
                ["SpellDifficulty"] = (5, 20),
                ["SpellCategory"] = (2, 8),
                ["SpellDescriptionVariables"] = (2, 8),
                ["SpellIcon"] = (2, 8),
                ["SpellAffect"] = (3, 12),
                ["SpellItemEnchantment"] = (38, 152),
                ["SpellShapeshiftForm"] = (35, 140),
                ["SpellFocusObject"] = (18, 72),
                ["TotemCategory"] = (20, 80),
                ["SkillLine"] = (56, 224),
                ["SkillLineAbility"] = (14, 56),
                ["AreaGroup"] = (8, 32),
                ["AreaTable"] = (36, 144),
                ["OverrideSpellData"] = (12, 48),
                ["ScreenEffect"] = (10, 40),
                ["SpellVisual"] = (32, 128),
                ["SpellVisualKit"] = (38, 152),
                ["SpellVisualEffectName"] = (7, 28),
                ["SpellVisualKitModelAttach"] = (10, 40),
                ["SpellVisualKitAreaModel"] = (3, 12),
                ["SpellMissile"] = (15, 60),
                ["SpellMissileMotion"] = (5, 20),
                ["SpellChainEffects"] = (48, 177),
                ["Item"] = (8, 32),
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
