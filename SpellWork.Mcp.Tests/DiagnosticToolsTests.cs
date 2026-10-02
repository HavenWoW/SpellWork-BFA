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

            Assert.Equal(18414, result.GetProperty("clientBuild").GetInt32());
            Assert.EndsWith("SpellVisual.db2", result.GetProperty("openedFiles").GetProperty("SpellVisual").GetString());
            Assert.True(result.GetProperty("localeFallback").ValueKind is JsonValueKind.True or JsonValueKind.False);
            Assert.Equal(0, result.GetProperty("layoutMismatches").GetArrayLength());
            Assert.False(result.TryGetProperty("hotfixCache", out _));
            Assert.False(result.TryGetProperty("hotfixCachePath", out _));
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
            Assert.True(result.GetProperty("localeFallback").GetBoolean());
        }
    }
}
