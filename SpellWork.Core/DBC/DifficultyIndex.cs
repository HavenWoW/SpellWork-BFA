using System;
using System.Collections.Generic;
using System.Linq;

namespace SpellWork.DBC
{
    /// <summary>
    /// (spell, difficulty) -> row for the 5.4.8 tables that carry both
    /// columns. A difficulty with no row of its own falls back to the base
    /// (difficulty 0) row, which is how the 5.4.8 server resolves them.
    /// </summary>
    public sealed class DifficultyIndex<T> where T : class
    {
        private readonly Dictionary<int, SortedDictionary<int, T>> _bySpell;
        private DifficultyIndex(Dictionary<int, SortedDictionary<int, T>> bySpell) => _bySpell = bySpell;

        public static DifficultyIndex<T> Build(IEnumerable<T> rows, Func<T, int> spellId, Func<T, int> difficultyId)
        {
            var map = new Dictionary<int, SortedDictionary<int, T>>();
            foreach (var row in rows)
            {
                if (!map.TryGetValue(spellId(row), out var byDiff))
                    map[spellId(row)] = byDiff = new SortedDictionary<int, T>();
                byDiff[difficultyId(row)] = row; // duplicate (spell, difficulty): last row wins, as in the server's store load
            }
            return new DifficultyIndex<T>(map);
        }

        public T Get(int spellId, int difficulty, out int resolvedDifficulty)
        {
            resolvedDifficulty = 0;
            if (!_bySpell.TryGetValue(spellId, out var byDiff))
                return null;
            if (byDiff.TryGetValue(difficulty, out var exact)) { resolvedDifficulty = difficulty; return exact; }
            return byDiff.TryGetValue(0, out var baseRow) ? baseRow : null;
        }

        public IReadOnlyList<int> DifficultiesFor(int spellId) =>
            _bySpell.TryGetValue(spellId, out var byDiff) ? byDiff.Keys.ToList() : (IReadOnlyList<int>)Array.Empty<int>();
    }
}
