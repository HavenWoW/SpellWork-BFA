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
    /// mode-0 and mode-1 spells skipped, spell ids absent from Spell.dbc
    /// loaded as 0 -- DBCStores.cpp:519-543). The <c>Spell.SpellDifficultyID</c>
    /// column is not used: the CoA server does not read it (SpellEntryfmt
    /// 'x'), and on the reference data 1912 of the 2100 spells that set it
    /// name a row that does not list them (spell 16 Hellfire names row 1654,
    /// whose spells are 354326/354448/354449).
    /// </para>
    /// </summary>
    public sealed class SpellDifficultyResolver
    {
        public const int ModeCount = 4;

        // Row id -> its four mode slots with every id <= 0 or absent from
        // Spell.dbc set to 0, as DBCStores.cpp:525-536 builds newEntry.
        private readonly Dictionary<int, int[]> _slotsByRow = new Dictionary<int, int[]>();
        private readonly Dictionary<int, int> _rowBySpell = new Dictionary<int, int>();

        /// <param name="rows">The SpellDifficulty store keyed by row id; null means none loaded.</param>
        /// <param name="spellExists">Whether a spell id has a Spell.dbc row; null treats every id as present.</param>
        public SpellDifficultyResolver(IReadOnlyDictionary<int, SpellDifficultyEntry> rows, Func<int, bool> spellExists = null)
        {
            spellExists = spellExists ?? (_ => true);

            foreach (var row in (rows ?? new Dictionary<int, SpellDifficultyEntry>()).Values.OrderBy(r => r.ID))
            {
                var slots = row.DifficultySpellID
                    .Select(id => id > 0 && spellExists(id) ? id : 0)
                    .ToArray();

                // "id0-1 must be always set!" (DBCStores.cpp:538)
                if (slots[0] <= 0 || slots[1] <= 0)
                    continue;

                _slotsByRow[row.ID] = slots;
                foreach (var spellId in slots)
                    if (spellId > 0)
                        _rowBySpell[spellId] = row.ID;
            }
        }

        /// <summary>
        /// The spell id serving <paramref name="mode"/> and every variant of
        /// the spell's SpellDifficulty row (non-zero, mode order, distinct).
        /// Mirrors SpellMgr::GetSpellIdForDifficulty (SpellMgr.cpp:592-602):
        /// a heroic mode (2, 3) whose slot is 0 falls back to the normal mode
        /// of the same size (0, 1); if that slot is 0 too, or the spell is in
        /// no row, the result is <paramref name="spellId"/> itself. No row
        /// gives no variants.
        /// </summary>
        public (int ResolvedSpellId, IReadOnlyList<int> Variants) Resolve(int spellId, int mode)
        {
            if (mode < 0 || mode >= ModeCount)
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "SpellDifficulty mode is 0-3 (10N, 25N, 10H, 25H).");

            var rowId = RowFor(spellId);
            if (rowId == 0)
                return (spellId, Array.Empty<int>());

            var slots = _slotsByRow[rowId];
            var variants = slots.Where(id => id > 0).Distinct().ToList();

            // SpellMgr.cpp:592-596: DUNGEON_DIFFICULTY_HEROIC is 1, so modes
            // 2 and 3 drop to 0 and 1.
            if (slots[mode] <= 0 && mode > 1)
                mode -= 2;

            // SpellMgr.cpp:598-602: still 0 -> the source spell.
            var resolved = slots[mode];
            return (resolved > 0 ? resolved : spellId, variants);
        }

        /// <summary>The SpellDifficulty row id listing <paramref name="spellId"/>, or 0.</summary>
        public int RowFor(int spellId) => _rowBySpell.TryGetValue(spellId, out var rowId) ? rowId : 0;
    }
}
