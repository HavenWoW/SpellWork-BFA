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

        // Task 6, step 5: server_info gains a layoutMismatches key, present
        // only when DBC.DBC.LayoutMismatches is non-empty (SpellWork.Core's
        // LayoutVerificationTests.ReferenceBuild_HasNoLayoutMismatches already
        // pins that this build's client data matches every structure class
        // LayoutVerification.Expected checks). Confirms the key stays absent
        // -- not present-and-empty -- on the reference build this server is
        // developed against.
        [SkippableFact]
        public async Task ServerInfo_ReferenceBuild_HasNoLayoutMismatchesKey()
        {
            _fixture.SkipIfNoClientData();

            var json = await new DiagnosticTools(_fixture.LoadedService).ServerInfo();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.False(result.TryGetProperty("layoutMismatches", out _),
                "This build's client data should match every structure class LayoutVerification.Expected checks.");
        }
    }
}
