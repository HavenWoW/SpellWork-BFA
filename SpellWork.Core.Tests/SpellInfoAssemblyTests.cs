using Xunit;

namespace SpellWork.Core.Tests
{
    public class SpellInfoAssemblyTests : IClassFixture<ClientDataFixture>
    {
        public const int KnownSpellId = 133; // Fireball

        private readonly ClientDataFixture _fixture;

        public SpellInfoAssemblyTests(ClientDataFixture fixture) => _fixture = fixture;

        // Values pinned from the live 70449 client data.
        [SkippableFact]
        public void Known_spell_assembles_from_70449_data()
        {
            _fixture.SkipIfNoClientData();

            var info = DBC.DBC.SpellInfoStore[KnownSpellId];

            Assert.Equal("Fireball", info.Name);
            Assert.Equal(4u, info.SchoolMask);          // fire
            Assert.Equal(17, info.AttributeWords.Length);
            Assert.Equal(3u, info.SpellFamilyName);     // mage
            Assert.Equal(1, info.Effects.Count);
            Assert.Equal(2u, info.Effects[0].Effect);   // SPELL_EFFECT_SCHOOL_DAMAGE
        }
    }
}
