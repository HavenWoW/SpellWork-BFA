using System;
using System.Collections.Generic;
using System.Linq;
using SpellWork.DBC.Structures;

namespace SpellWork.DBC
{
    /// <summary>
    /// Spec D4: a 3.3.5 per-difficulty spell is a separate Spell row, named
    /// by mode (0 = 10N, 1 = 25N, 2 = 10H, 3 = 25H) in a SpellDifficulty.dbc
    /// row.
    /// <para>
    /// A spell belongs to the SpellDifficulty row that lists it, exactly as
    /// the CoA server builds its lookup (DBCStores.cpp: every listed spell id
    /// -> row id, later rows overwriting earlier ones, rows without both
    /// mode-0 and mode-1 spells skipped). The <c>Spell.SpellDifficultyID</c>
    /// column is not used: the CoA server does not read it (SpellEntryfmt
    /// 'x'), and on the reference data 1912 of the 2100 spells that set it
    /// name a row that does not list them (spell 16 Hellfire names row 1654,
    /// whose spells are 354326/354448/354449).
    /// </para>
    /// </summary>
    public sealed class SpellDifficultyResolver
    {
        public const int ModeCount = 4;

        private readonly IReadOnlyDictionary<int, SpellDifficultyEntry> _rows;
        private readonly Dictionary<int, int> _rowBySpell = new Dictionary<int, int>();

        /// <param name="rows">The SpellDifficulty store keyed by row id; null means none loaded.</param>
        public SpellDifficultyResolver(IReadOnlyDictionary<int, SpellDifficultyEntry> rows)
        {
            _rows = rows ?? new Dictionary<int, SpellDifficultyEntry>();

            foreach (var row in _rows.Values.OrderBy(r => r.ID))
            {
                if (row.DifficultySpellID[0] <= 0 || row.DifficultySpellID[1] <= 0)
                    continue;

                foreach (var spellId in row.DifficultySpellID)
                    if (spellId > 0)
                        _rowBySpell[spellId] = row.ID;
            }
        }

        /// <summary>
        /// The spell id serving <paramref name="mode"/> and every variant of
        /// the spell's SpellDifficulty row (non-zero, mode order, distinct).
        /// A spell in no row, or whose row has 0 for the mode, resolves to
        /// <paramref name="spellId"/> itself (Review Focus 1); no row gives no variants.
        /// </summary>
        public (int ResolvedSpellId, IReadOnlyList<int> Variants) Resolve(int spellId, int mode)
        {
            if (mode < 0 || mode >= ModeCount)
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "SpellDifficulty mode is 0-3 (10N, 25N, 10H, 25H).");

            var rowId = RowFor(spellId);
            if (rowId == 0)
                return (spellId, Array.Empty<int>());

            var row = _rows[rowId];
            var variants = row.DifficultySpellID.Where(id => id > 0).Distinct().ToList();
            var resolved = row.DifficultySpellID[mode];
            return (resolved > 0 ? resolved : spellId, variants);
        }

        /// <summary>The SpellDifficulty row id listing <paramref name="spellId"/>, or 0.</summary>
        public int RowFor(int spellId) => _rowBySpell.TryGetValue(spellId, out var rowId) ? rowId : 0;
    }
}
