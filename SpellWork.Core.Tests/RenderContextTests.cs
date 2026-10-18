using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class RenderContextTests
    {
        // Guards the GUI: FromGlobals must snapshot exactly what the statics hold.
        [Fact]
        public void FromGlobals_SnapshotsTheStatics()
        {
            var context = RenderContext.FromGlobals();

            Assert.Equal(DBC.DBC.SelectedLevel, context.Level);
            Assert.Equal(DBC.DBC.SelectedItemLevel, context.ItemLevel);
            Assert.Equal(0, context.DifficultyId);
        }

        [Fact]
        public void Explicit_context_carries_its_values()
        {
            var context = new RenderContext(20, 100, 5);

            Assert.Equal(20u, context.Level);
            Assert.Equal(100u, context.ItemLevel);
            Assert.Equal(5, context.DifficultyId);
        }
    }
}
