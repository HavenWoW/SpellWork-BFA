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
        public async Task ServerInfo_Reports554Version_AndLoadedData()
        {
            _fixture.SkipIfNoClientData();

            var json = await new DiagnosticTools(_fixture.LoadedService).ServerInfo();
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal("SpellWork 5.5.4 (70449)", result.GetProperty("version").GetString());
            Assert.True(result.GetProperty("clientDataLoaded").GetBoolean());
            Assert.True(result.GetProperty("spellCount").GetInt32() > 0);
            Assert.Equal("5.5.4.70449", result.GetProperty("build").GetString());
            Assert.True(result.TryGetProperty("defsPath", out _));
            Assert.True(result.TryGetProperty("localeFallback", out _));
            Assert.False(result.TryGetProperty("gtPath", out _));
            Assert.True(result.GetProperty("storeCounts").EnumerateObject().Any());
        }

        [SkippableFact]
        public async Task ServerInfo_ReferenceBuild_HasNoLayoutMismatchesKey()
        {
            _fixture.SkipIfNoClientData();

            var json = await new DiagnosticTools(_fixture.LoadedService).ServerInfo();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.False(result.TryGetProperty("layoutMismatches", out _));
        }
    }
}
