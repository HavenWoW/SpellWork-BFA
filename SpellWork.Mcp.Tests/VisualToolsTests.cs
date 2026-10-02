using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // The 8.3.7 visual chain has no 5.4.8 counterpart; until the 5.4.8 chain
    // lands the tool must say so plainly rather than return an empty chain
    // that reads as "this spell has no visual".
    public class VisualToolsTests
    {
        [Fact]
        public async Task GetSpellVisualChain_ReportsNotImplemented()
        {
            var service = new SpellDataService(
                McpServerConfig.FromEnvironment(new Dictionary<string, string>()),
                NullLoggerFactoryStub.Logger);

            var json = await new VisualTools(service).GetSpellVisualChain(133);
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("not_implemented", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }
}
