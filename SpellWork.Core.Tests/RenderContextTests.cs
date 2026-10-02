using SpellWork.DBC;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class RenderContextTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public RenderContextTests(ClientDataFixture fixture) => _fixture = fixture;

        // FromGlobals must snapshot exactly what the statics hold, or a caller
        // relying on the process-wide selection renders at the wrong level.
        [SkippableFact]
        public void FromGlobals_SnapshotsTheStatics()
        {
            _fixture.SkipIfNoClientData();

            var context = RenderContext.FromGlobals();

            Assert.Equal(DBC.DBC.SelectedLevel, context.Level);
            Assert.Equal(DBC.DBC.SelectedItemLevel, context.ItemLevel);
            Assert.Equal(DBC.DBC.SelectedDifficulty, context.Difficulty);
        }
    }
}
