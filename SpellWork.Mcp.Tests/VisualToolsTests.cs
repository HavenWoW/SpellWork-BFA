using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class VisualToolsTests : IClassFixture<McpClientDataFixture>
    {
        private readonly McpClientDataFixture _fixture;

        public VisualToolsTests(McpClientDataFixture fixture) => _fixture = fixture;

        private const int Fireball = 133;

        // Spec §6 / D4: visuals are not per difficulty, so the tool takes only
        // `id`. The MCP SDK builds the input schema from the method signature,
        // so pinning the signature pins the schema: a caller passing
        // `difficulty` gets a schema error rather than a silently ignored arg.
        [Fact]
        public void GetSpellVisualChain_TakesOnlyId()
        {
            var parameters = typeof(VisualTools).GetMethod(nameof(VisualTools.GetSpellVisualChain))
                .GetParameters().Select(p => p.Name).ToArray();
            Assert.Equal(new[] { "id" }, parameters);
        }

        [SkippableFact]
        public async Task GetSpellVisualChain_Fireball_ReturnsTheChain()
        {
            _fixture.SkipIfNoClientData();

            var json = await new VisualTools(_fixture.LoadedService).GetSpellVisualChain(Fireball);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(Fireball, result.GetProperty("id").GetInt32());
            Assert.False(result.GetProperty("storeUnloaded").GetBoolean());
            var variant = result.GetProperty("variants")[0];
            Assert.Equal(0, variant.GetProperty("slot").GetInt32());
            Assert.Equal(67, variant.GetProperty("spellVisualId").GetInt32());
            Assert.True(variant.GetProperty("visualResolved").GetBoolean());
            Assert.Contains(variant.GetProperty("kitSlots").EnumerateArray(),
                k => k.GetProperty("slot").GetString() == "PrecastKit" && k.GetProperty("kitId").GetInt32() == 30);
            Assert.Equal(@"Spells\Fireball_Missile_Low.mdx",
                variant.GetProperty("missiles")[0].GetProperty("name").GetString());
            Assert.NotEqual(0, result.GetProperty("kits").GetArrayLength());
            Assert.False(result.GetProperty("truncated").GetBoolean());
            Assert.Equal(JsonValueKind.Array, result.GetProperty("unresolvedReferences").ValueKind);
        }

        [SkippableFact]
        public async Task GetSpellVisualChain_UnknownSpell_ReportsSpellNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await new VisualTools(_fixture.LoadedService).GetSpellVisualChain(999999999);
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("spell_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_JsonCarriesTheVisualSection()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(Fireball, sections: new[] { "visual" });
            using var doc = JsonDocument.Parse(json);

            var visual = doc.RootElement.GetProperty("result").GetProperty("visual");
            Assert.Equal(67, visual.GetProperty("variants")[0].GetProperty("spellVisualId").GetInt32());
        }

        [SkippableFact]
        public async Task GetSpell_TextCarriesTheVisualBlock()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(Fireball, format: "text");
            using var doc = JsonDocument.Parse(json);

            Assert.Contains("Visual (slot 0): SpellVisual #67", doc.RootElement.GetProperty("result").GetProperty("text").GetString());
        }
    }

    public class VisualToolsUnloadedStoreTests
    {
        private const int NonexistentSpellId = 999999999;

        // No client data configured: the gate refuses before the id is looked at.
        [Fact]
        public async Task GetSpellVisualChain_NoClientDataConfigured_ReportsStoreNotLoaded()
        {
            var service = new SpellDataService(
                McpServerConfig.FromEnvironment(new Dictionary<string, string>()),
                NullLoggerFactoryStub.Logger);

            var json = await new VisualTools(service).GetSpellVisualChain(NonexistentSpellId);
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            var error = doc.RootElement.GetProperty("error");
            Assert.Equal("store_not_loaded", error.GetProperty("code").GetString());
            Assert.True(error.GetProperty("details").GetProperty("storeUnloaded").GetBoolean());
            Assert.Contains("no client data is loaded at all", error.GetProperty("message").GetString());
        }
    }

    // Nulls a process-wide store for the duration of one test, so it must not
    // run alongside anything else that reads DBC.DBC.
    [CollectionDefinition("VisualStoreMutation", DisableParallelization = true)]
    public sealed class VisualStoreMutationCollection { }

    [Collection("VisualStoreMutation")]
    public class VisualToolsGatedStoreTests : IClassFixture<McpClientDataFixture>
    {
        private readonly McpClientDataFixture _fixture;

        public VisualToolsGatedStoreTests(McpClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task GatedStoreNulled_EveryRenderingSaysStoreUnloaded()
        {
            _fixture.SkipIfNoClientData();

            var original = SpellWork.DBC.DBC.SpellVisualKitModelAttach;
            try
            {
                SpellWork.DBC.DBC.SpellVisualKitModelAttach = null;

                using (var doc = JsonDocument.Parse(await new VisualTools(_fixture.LoadedService).GetSpellVisualChain(133)))
                {
                    Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
                    var error = doc.RootElement.GetProperty("error");
                    Assert.Equal("store_not_loaded", error.GetProperty("code").GetString());
                    Assert.True(error.GetProperty("details").GetProperty("storeUnloaded").GetBoolean());
                    Assert.Contains("SpellVisualKitModelAttach", error.GetProperty("message").GetString());
                }

                using (var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, sections: new[] { "visual" })))
                {
                    var visual = doc.RootElement.GetProperty("result").GetProperty("visual");
                    Assert.True(visual.GetProperty("storeUnloaded").GetBoolean());
                    Assert.False(visual.TryGetProperty("variants", out _));
                }

                using (var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, format: "text")))
                {
                    Assert.Contains("Visual chain unavailable -- these stores did not load: SpellVisualKitModelAttach",
                        doc.RootElement.GetProperty("result").GetProperty("text").GetString());
                }
            }
            finally
            {
                SpellWork.DBC.DBC.SpellVisualKitModelAttach = original;
            }

            using var restored = JsonDocument.Parse(await new VisualTools(_fixture.LoadedService).GetSpellVisualChain(133));
            Assert.True(restored.RootElement.GetProperty("ok").GetBoolean());
        }
    }
}
