using System.Linq;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class DifficultyIndexTests
    {
        private sealed class Row { public int Spell, Diff; public string Tag; }

        private static DifficultyIndex<Row> Index() => DifficultyIndex<Row>.Build(new[]
        {
            new Row { Spell = 1, Diff = 0, Tag = "base" },
            new Row { Spell = 1, Diff = 2, Tag = "heroic" },
            new Row { Spell = 2, Diff = 3, Tag = "only10" },
        }, r => r.Spell, r => r.Diff);

        [Fact] public void ExactDifficultyWins() { Assert.Equal("heroic", Index().Get(1, 2, out var d).Tag); Assert.Equal(2, d); }
        [Fact] public void MissingDifficultyFallsBackToBase() { Assert.Equal("base", Index().Get(1, 1, out var d).Tag); Assert.Equal(0, d); }
        [Fact] public void NoBaseAndNoExactIsNull() { Assert.Null(Index().Get(2, 1, out _)); }
        [Fact] public void DifficultiesAreListedAscending() => Assert.Equal(new[] { 0, 2 }, Index().DifficultiesFor(1));
        [Fact] public void UnknownSpellHasNoDifficulties() => Assert.Empty(Index().DifficultiesFor(99));
    }
}
