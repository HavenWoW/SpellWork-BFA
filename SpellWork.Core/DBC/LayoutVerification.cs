using System.Collections.Generic;
using System.Globalization;

namespace SpellWork.DBC
{
    /// <summary>
    /// One table's on-disk layout disagreeing with the layout its structure
    /// class was written against.
    /// </summary>
    public sealed class LayoutMismatch
    {
        public string Table { get; set; }
        public uint ExpectedLayoutHash { get; set; }
        public uint ActualLayoutHash { get; set; }
        public int ExpectedFieldsCount { get; set; }
        public int ActualFieldsCount { get; set; }

        /// <summary>
        /// Both values, both named, in the form the reader needs to search
        /// WoWDBDefs with. A message that says only "wrong layout" sends them
        /// there with nothing to look for.
        /// </summary>
        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0}.db2 layout hash 0x{1:X8} (expected 0x{2:X8}), {3} fields (expected {4}). " +
            "The structure class was written against a different client build; " +
            "look up LAYOUT {1:X8} in WoWDBDefs before trusting any field of it.",
            Table, ActualLayoutHash, ExpectedLayoutHash, ActualFieldsCount, ExpectedFieldsCount);
    }

    /// <summary>
    /// Control 1 of the three in the design's section 7.1, and the strongest.
    /// <para>
    /// DBFileReaderLib maps fields by declaration order, so a structure whose
    /// fields are the right count but the wrong order decodes to silent
    /// garbage rather than an error. A field-count mismatch already fails
    /// loudly (DBC.cs catches the ArgumentException). Field *order* among
    /// same-width types is what fails quietly, and the WDC3 layout hash is
    /// computed over the field layout, so it catches exactly that.
    /// </para>
    /// <para>
    /// Only tables listed here are checked. An unlisted table returns null
    /// rather than a mismatch: the 46 structures that predate this mechanism
    /// have no recorded expected layout, and reporting all of them would bury
    /// the signal this exists to produce.
    /// </para>
    /// </summary>
    public static class LayoutVerification
    {
        /// <summary>
        /// Measured against the 8.3.7 client data at
        /// BFA-HavenCore-Corruption/build/bin/Debug/ClientData/dbc/enUS.
        /// <para>
        /// FieldsCount excludes columns marked $noninline,...$ (including both
        /// $noninline,id$ and $noninline,relation$), counts an array as one
        /// field, and counts a plain $id$ (not marked $noninline,...$) as an
        /// inline field. Examples: SpellVisualMissile's inline $id$ is counted
        /// (16 total); SpellVisualEvent's $noninline,id$ and $noninline,relation$
        /// are both excluded (8 total).
        /// </para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (uint LayoutHash, int FieldsCount)> Expected =
            new Dictionary<string, (uint, int)>
            {
                ["SpellXSpellVisual"]        = (0x8D0B1C8F, 12),
                ["SpellMissile"]             = (0x086B0CE5, 15),
                ["SpellVisual"]              = (0x17962AA0, 15),
                ["SpellVisualEvent"]         = (0xA85282AD, 8),
                ["SpellVisualMissile"]       = (0xBE91C62B, 16),
                ["SpellVisualKit"]           = (0x6D87523A, 5),
                ["SpellVisualKitEffect"]     = (0xD906832B, 2),
                ["SpellVisualEffectName"]    = (0x8AA2AAED, 14),
                ["SpellVisualKitModelAttach"]= (0xB8BDA6F9, 21),
                ["SpellChainEffects"]        = (0x31DC7CA9, 60),
                ["SpellMissileMotion"]       = (0xA76EF553, 4),
            };

        public static LayoutMismatch Check(string name, uint layoutHash, int fieldsCount)
        {
            if (!Expected.TryGetValue(name, out var expected))
                return null;

            if (expected.LayoutHash == layoutHash && expected.FieldsCount == fieldsCount)
                return null;

            return new LayoutMismatch
            {
                Table = name,
                ExpectedLayoutHash = expected.LayoutHash,
                ActualLayoutHash = layoutHash,
                ExpectedFieldsCount = expected.FieldsCount,
                ActualFieldsCount = fieldsCount,
            };
        }
    }
}
