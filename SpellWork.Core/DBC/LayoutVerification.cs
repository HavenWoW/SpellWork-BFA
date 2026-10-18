using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SpellWork.DBC
{
    /// <summary>
    /// One table's on-disk columns disagreeing with the columns its structure
    /// class was written against.
    /// </summary>
    public sealed class LayoutMismatch
    {
        public string Table;
        public string[] MissingColumns;
        public int ExpectedFieldsCount;
        public int ActualFieldsCount;

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0}.db2 is missing column(s) [{1}]; {2} columns (expected {3}). " +
            "The structure class was written against a different client build; " +
            "check WoWDBDefs for this build before trusting any field of it.",
            Table, string.Join(", ", MissingColumns ?? new string[0]), ActualFieldsCount, ExpectedFieldsCount);
    }

    /// <summary>
    /// Verifies a store's DBCD column names (IDBCDStorage.AvailableColumns)
    /// against the expected set for the 70449 WDC5 layouts. Unlisted tables
    /// return null.
    /// </summary>
    public static class LayoutVerification
    {
        public static readonly IReadOnlyDictionary<string, (string[] Columns, int FieldsCount)> Expected =
            new Dictionary<string, (string[], int)>
            {
                ["SpellName"] = (new[] { "ID", "Name_lang" }, 2),
                ["Spell"] = (new[] { "ID", "NameSubtext_lang", "Description_lang", "AuraDescription_lang" }, 4),
                ["SpellMisc"] = (new[]
                {
                    "ID", "Attributes", "DifficultyID", "CastingTimeIndex", "DurationIndex", "RangeIndex",
                    "SchoolMask", "Speed", "LaunchDelay", "MinDuration", "SpellIconFileDataID",
                    "ActiveIconFileDataID", "ContentTuningID", "ShowFutureSpellPlayerConditionID", "SpellID",
                }, 15),
                ["SpellEffect"] = (new[]
                {
                    "ID", "DifficultyID", "EffectIndex", "Effect", "EffectAmplitude", "EffectAttributes",
                    "EffectAura", "EffectAuraPeriod", "EffectBasePoints", "EffectBonusCoefficient",
                    "EffectChainAmplitude", "EffectChainTargets", "EffectDieSides", "EffectItemType",
                    "EffectMechanic", "EffectPointsPerResource", "EffectPos_facing", "EffectRealPointsPerLevel",
                    "EffectTriggerSpell", "BonusCoefficientFromAP", "PvpMultiplier", "Coefficient", "Variance",
                    "ResourceCoefficient", "GroupSizeBasePointsCoefficient", "EffectBasePointsF",
                    "Field_5_5_4_67090_025", "EffectMiscValue", "EffectRadiusIndex", "EffectSpellClassMask",
                    "ImplicitTarget", "SpellID",
                }, 32),
                ["SpellClassOptions"] = (new[] { "ID", "SpellID", "ModalNextSpell", "SpellClassSet", "SpellClassMask" }, 5),
                ["SpellDuration"] = (new[] { "ID", "Duration", "DurationPerLevel", "MaxDuration" }, 4),
                ["SpellRange"] = (new[] { "ID", "DisplayName_lang", "DisplayNameShort_lang", "Flags", "RangeMin", "RangeMax" }, 6),
                ["Difficulty"] = (new[]
                {
                    "ID", "Name_lang", "InstanceType", "OrderIndex", "OldEnumValue", "FallbackDifficultyID",
                    "MinPlayers", "MaxPlayers", "Flags", "ItemContext", "ToggleDifficultyID",
                    "GroupSizeHealthCurveID", "GroupSizeDmgCurveID", "GroupSizeSpellPointsCurveID",
                    "Field_1_15_4_56400_013",
                }, 15),
            };

        public static LayoutMismatch Check(string name, IReadOnlyCollection<string> availableColumns)
        {
            if (!Expected.TryGetValue(name, out var expected))
                return null;

            var missing = expected.Columns.Where(c => !availableColumns.Contains(c)).ToArray();
            if (missing.Length == 0 && expected.FieldsCount == availableColumns.Count)
                return null;

            return new LayoutMismatch
            {
                Table = name,
                MissingColumns = missing,
                ExpectedFieldsCount = expected.FieldsCount,
                ActualFieldsCount = availableColumns.Count,
            };
        }
    }
}
