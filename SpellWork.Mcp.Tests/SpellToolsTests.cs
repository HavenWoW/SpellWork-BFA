using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class SpellToolsTests : IClassFixture<McpClientDataFixture>
    {
        private readonly McpClientDataFixture _fixture;

        public SpellToolsTests(McpClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task GetSpell_KnownId_ReturnsNameAsJson()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133));
            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(133, result.GetProperty("id").GetInt32());
            Assert.Equal("Fireball", result.GetProperty("name").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_TextFormat_ContainsName()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, "text"));
            Assert.Contains("Fireball", doc.RootElement.GetProperty("result").GetProperty("text").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_SectionFilter_IsAccepted_AndUnknownSectionRejected()
        {
            _fixture.SkipIfNoClientData();

            using var ok = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, sections: new[] { "effects" }));
            Assert.True(ok.RootElement.GetProperty("ok").GetBoolean());

            using var bad = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, sections: new[] { "visual" }));
            Assert.Equal("invalid_section", bad.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_UnknownDifficulty_ListsValidIds()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, difficulty: 99));
            var error = doc.RootElement.GetProperty("error");
            Assert.Equal("unknown_difficulty", error.GetProperty("code").GetString());
            var ids = error.GetProperty("details").GetProperty("validDifficultyIds")
                .EnumerateArray().Select(e => e.GetInt32()).ToList();
            Assert.Equal(SpellWork.DBC.DBC.ValidDifficultyIds.OrderBy(x => x), ids.OrderBy(x => x));
        }

        [SkippableFact]
        public async Task GetSpell_ValidDifficulty_Works()
        {
            _fixture.SkipIfNoClientData();

            // Bring-up: only base difficulty 0 is valid (no overrides applied).
            Assert.Equal(new[] { 0 }, SpellWork.DBC.DBC.ValidDifficultyIds.ToArray());
            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, difficulty: 0));
            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        }

        [SkippableFact]
        public async Task GetSpell_InvalidFormat_IsRejected()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, "xml"));
            Assert.Equal("invalid_format", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_AbsentId_ReturnsNotFoundWithNearestNames()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(int.MaxValue));
            var error = doc.RootElement.GetProperty("error");
            Assert.Equal("spell_not_found", error.GetProperty("code").GetString());
            Assert.NotEmpty(error.GetProperty("details").GetProperty("nearestIds").EnumerateArray());
        }

        [SkippableFact]
        public async Task SearchSpells_ByName_ReturnsPagedMatches()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(name: "fire", limit: 5));
            var result = doc.RootElement.GetProperty("result");
            var rows = result.GetProperty("rows").EnumerateArray().ToList();
            Assert.Equal(5, rows.Count);
            Assert.True(result.GetProperty("totalMatches").GetInt32() > 5);
            Assert.True(result.GetProperty("truncated").GetBoolean());
            Assert.All(rows, r => Assert.Contains("fire", r.GetProperty("name").GetString(),
                StringComparison.OrdinalIgnoreCase));
            var ids = rows.Select(r => r.GetProperty("id").GetInt32()).ToList();
            Assert.Equal(ids.OrderBy(x => x), ids);
        }

        [SkippableFact]
        public async Task SearchSpells_EnumByNameAndNumber_Agree()
        {
            _fixture.SkipIfNoClientData();

            using var byName = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(family: "SPELLFAMILY_MAGE", limit: 1));
            using var byNumber = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(family: "3", limit: 1));
            var nameTotal = byName.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32();
            Assert.Equal(nameTotal, byNumber.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32());
            Assert.True(nameTotal > 0);
        }

        [SkippableFact]
        public async Task SearchSpells_BadEnum_IsRejected()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(auraType: "NOT_AN_AURA"));
            Assert.Equal("invalid_enum_value", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }
}
