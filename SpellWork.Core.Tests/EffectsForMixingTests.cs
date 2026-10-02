using System.Linq;
using SpellWork.DBC;
using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class EffectsForMixingTests
    {
        private static SpellEffectEntry Row(int id, int difficulty, int index) =>
            new SpellEffectEntry { ID = id, DifficultyID = difficulty, EffectIndex = index, SpellID = 1 };

        // Base rows at index 0 and 1, difficulty 2 overriding index 1 and
        // adding an index 2 the base lacks: the result takes each index from
        // the difficulty that has it, and says which one that was.
        [Fact]
        public void EffectsFor_MixesBaseAndDifficultyRowsPerIndex()
        {
            var info = new SpellInfo(new SpellEntry { ID = 1 });
            info.AddEffect(Row(100, 0, 0), null);
            info.AddEffect(Row(101, 0, 1), null);
            info.AddEffect(Row(201, 2, 1), null);
            info.AddEffect(Row(202, 2, 2), null);

            var effects = info.EffectsFor(2);

            Assert.Equal(new[] { 100, 201, 202 }, effects.Select(e => e.Entry.ID).ToArray());
            Assert.Equal(new[] { 0, 2, 2 }, effects.Select(e => e.ResolvedDifficulty).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, effects.Select(e => e.Entry.EffectIndex).ToArray());

            // Base view has no index 2, and a difficulty with no rows is the base.
            Assert.Equal(new[] { 100, 101 }, info.EffectsFor(0).Select(e => e.Entry.ID).ToArray());
            Assert.Equal(new[] { 100, 101 }, info.EffectsFor(1).Select(e => e.Entry.ID).ToArray());
        }
    }
}
