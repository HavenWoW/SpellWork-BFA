using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class DiagnosticToolsTests : IClassFixture<McpClientDataFixture>
    {
        private readonly McpClientDataFixture _fixture;

        public DiagnosticToolsTests(McpClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task ServerInfoReportsOpenedFilesAndBuild()
        {
            _fixture.SkipIfNoClientData();

            var json = await new DiagnosticTools(_fixture.LoadedService).ServerInfo();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(12340, result.GetProperty("clientBuild").GetInt32());
            Assert.Equal("coa", result.GetProperty("variant").GetString());
            Assert.Equal(JsonValueKind.Number, result.GetProperty("localeSlot").ValueKind);
            var openedFiles = result.GetProperty("openedFiles");
            foreach (var store in new[] { "Spell", "SpellDifficulty", "SpellAffect", "SpellVisual" })
            {
                Assert.EndsWith(store + ".dbc", openedFiles.GetProperty(store).GetProperty("path").GetString());
                Assert.Equal("dbc", openedFiles.GetProperty(store).GetProperty("extension").GetString());
            }
            Assert.True(result.GetProperty("loadDurationMs").GetInt32() > 0);
            Assert.Equal(0, result.GetProperty("layoutMismatches").GetArrayLength());
            Assert.False(result.TryGetProperty("hotfixCache", out _));
        }

        // Spec section 7: SPELLWORK_DB2_PATH and SPELLWORK_GT_PATH are
        // accepted (a 5.4.8 registration still starts) and ignored; the
        // server says which of them it ignored. Data-independent.
        [Fact]
        public async Task ServerInfo_WithoutData_StillNamesBuildVariantAndIgnoredVariables()
        {
            var config = McpServerConfig.FromEnvironment(new System.Collections.Generic.Dictionary<string, string>
            {
                ["SPELLWORK_DB2_PATH"] = "C:/nowhere/db2",
                ["SPELLWORK_GT_PATH"] = "C:/nowhere/gt",
            });
            var json = await new DiagnosticTools(new SpellDataService(config, NullLoggerFactoryStub.Logger)).ServerInfo();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(12340, result.GetProperty("clientBuild").GetInt32());
            Assert.Equal("coa", result.GetProperty("variant").GetString());
            Assert.False(result.GetProperty("clientDataLoaded").GetBoolean());
            Assert.Equal(new[] { "SPELLWORK_DB2_PATH", "SPELLWORK_GT_PATH" },
                result.GetProperty("ignoredVariables").EnumerateArray().Select(v => v.GetString()).ToArray());
        }

        // The fixture's partial load leaves the statics partial until it
        // reloads; a later server_info must see every store again.
        [SkippableFact]
        public async Task ServerInfo_AfterThePartialLoad_ListsEveryOpenedStore()
        {
            _fixture.SkipIfNoClientData();

            var json = await new DiagnosticTools(_fixture.LoadedService).ServerInfo();
            using var doc = JsonDocument.Parse(json);
            var opened = doc.RootElement.GetProperty("result").GetProperty("openedFiles");

            Assert.Equal(SpellWork.DBC.DBC.StoreFileNames().Count(), opened.EnumerateObject().Count());
            Assert.Empty(SpellWork.DBC.DBC.UnloadedStores);
        }

        // Captured while only two stores were on disk: openedFiles names
        // exactly those two (the stores the load did not open are null).
        [SkippableFact]
        public void ServerInfo_DuringPartialLoad_OpenedFilesMatchesLoadedStores()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(_fixture.PartialServerInfoJson);
            var result = doc.RootElement.GetProperty("result");
            var opened = result.GetProperty("openedFiles").EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();

            Assert.Equal(McpClientDataFixture.PresentStoreNames.OrderBy(n => n).ToList(), opened);
            // 3.3.5a: the locale is a string slot, not a folder (spec D6).
            Assert.Equal(0, result.GetProperty("localeSlot").GetInt32());
        }
    }
}
