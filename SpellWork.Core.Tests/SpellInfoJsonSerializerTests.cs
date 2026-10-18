using System;
using System.Text.Json;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class SpellInfoJsonSerializerTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellInfoJsonSerializerTests(ClientDataFixture fixture) => _fixture = fixture;

        [Fact]
        public void BuildFlagList_reports_declared_bits_by_name_and_unknown_bits_separately()
        {
            // Bit 0 of SpellSchoolMask is declared; bit 31 is not.
            var flags = SpellInfoJsonSerializer.BuildFlagList(typeof(SpellSchoolMask), 1u | (1u << 31), out var unmatched);

            Assert.Single(flags);
            Assert.Single(unmatched);
            Assert.Equal(31, (int)unmatched[0]);
        }

        [SkippableFact]
        public void Serialize_rejects_unknown_sections()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[SpellInfoAssemblyTests.KnownSpellId];

            Assert.Throws<ArgumentException>(() =>
                SpellInfoJsonSerializer.Serialize(spell, RenderContext.FromGlobals(), new[] { "scaling" }));
        }

        [SkippableFact]
        public void Serialize_emits_bring_up_sections_for_a_live_spell()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[SpellInfoAssemblyTests.KnownSpellId];
            var json = SpellInfoJsonSerializer.Serialize(spell, RenderContext.FromGlobals());

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal(spell.ID, root.GetProperty("id").GetInt32());
            Assert.Equal(spell.Name, root.GetProperty("name").GetString());
            Assert.True(root.TryGetProperty("misc", out _));
            Assert.True(root.TryGetProperty("effects", out var effects));
            Assert.True(effects.GetArrayLength() > 0);
            Assert.False(root.TryGetProperty("scaling", out _));
        }
    }
}
