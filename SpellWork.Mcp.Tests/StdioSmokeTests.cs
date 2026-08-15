using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class StdioSmokeTests
    {
        // Spawns the built server, performs the JSON-RPC initialize
        // handshake, and asserts every line stdout produced parses as JSON.
        // A single Console.WriteLine anywhere in the process fails this.
        [Fact]
        public async Task Stdout_CarriesOnlyWellFormedJsonRpc()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());

            Assert.NotEmpty(lines);
            foreach (var line in lines)
            {
                var exception = Record.Exception(() => JsonDocument.Parse(line).Dispose());
                Assert.True(exception == null, $"stdout line is not JSON: {line}");
            }
        }

        // Spec section 9: a misconfigured server must still start and still
        // answer. Handshaking with no SPELLWORK_* variables set at all is the
        // worst case, and it must still complete.
        [Fact]
        public async Task Server_StartsAndAnswers_WithNoConfiguration()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = null,
                ["SPELLWORK_GT_PATH"] = null,
            });

            Assert.Contains(lines, line => line.Contains("\"result\""));
        }

        // Selects the tools/list response and nothing else. Matching on the
        // bare word "tools" is not enough: the initialize result declares
        // {"tools":{"listChanged":true}} among its capabilities and arrives
        // first, so a looser match picks the handshake frame and then fails
        // looking for result.tools on it. The listing is the frame whose
        // "tools" is an *array*. A double quote inside a JSON string value is
        // always escaped, so this exact sequence cannot occur inside a tool
        // description or input schema -- only in the real listing frame.
        private const string ToolsListingMarker = "\"tools\":[";

        // The complete tool surface this plan ships. get_spell_visual_chain
        // (Task 6) is the fourteenth and last.
        private static readonly string[] ExpectedTools =
        {
            "server_info",
            "get_spell",
            "search_spells",
            "get_spell_triggers",
            "compare_spells",
            "decode_flags",
            "list_enum",
            "calculate_spell_scaling",
            "list_db2",
            "get_db2_row",
            "query_db2",
            "query_world_db",
            "get_spell_db_overrides",
            "get_spell_visual_chain",
        };

        [Fact]
        public async Task ToolsList_ContainsEveryToolExactlyOnce()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());
            var listing = lines.FirstOrDefault(line => line.Contains(ToolsListingMarker));
            Assert.NotNull(listing);

            using var doc = JsonDocument.Parse(listing);
            var names = doc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
                .Select(tool => tool.GetProperty("name").GetString())
                .ToList();

            Assert.Equal(ExpectedTools.Length, names.Count);
            foreach (var expected in ExpectedTools)
                Assert.Single(names, name => name == expected);
        }

        // Every tool must carry a description: it is the only thing the agent
        // has to choose between them, and an undescribed tool is one that gets
        // called wrong or not at all.
        [Fact]
        public async Task EveryTool_HasANonEmptyDescription()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());
            var listing = lines.First(line => line.Contains(ToolsListingMarker));

            using var doc = JsonDocument.Parse(listing);
            foreach (var tool in doc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString();
                Assert.True(tool.TryGetProperty("description", out var description),
                    $"{name} has no description.");
                Assert.False(string.IsNullOrWhiteSpace(description.GetString()), $"{name} has an empty description.");
            }
        }

        internal static async Task<List<string>> RunHandshakeAsync(IDictionary<string, string> environment)
        {
            var exePath = Path.Combine(AppContext.BaseDirectory, "SpellWork.Mcp.dll");
            var startInfo = new ProcessStartInfo("dotnet", $"\"{exePath}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var pair in environment)
            {
                if (pair.Value == null)
                    startInfo.Environment.Remove(pair.Key);
                else
                    startInfo.Environment[pair.Key] = pair.Value;
            }

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            const string initialize =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"2024-11-05\",\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"smoke\",\"version\":\"1.0\"}}}";
            const string listTools = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}";

            await process.StandardInput.WriteLineAsync(initialize);
            await process.StandardInput.FlushAsync();
            await process.StandardInput.WriteLineAsync(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            await process.StandardInput.FlushAsync();
            await process.StandardInput.WriteLineAsync(listTools);
            await process.StandardInput.FlushAsync();

            var lines = new List<string>();
            var readTask = Task.Run(async () =>
            {
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                {
                    if (line.Length == 0)
                        continue;

                    lines.Add(line);

                    // Stop on the tools/list response itself, not on a line
                    // count. Exactly two responses are ever produced -- the
                    // initialize result and the tools/list result;
                    // notifications/initialized is a notification and draws
                    // none -- so any count above two never arrives and the
                    // 30-second timeout below would fail every test in this
                    // file. A predicate says what we are actually waiting for
                    // and cannot drift out of step with the frame count.
                    if (line.Contains(ToolsListingMarker))
                        break;
                }
            });

            if (await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(30))) != readTask)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException("MCP server did not respond to the initialize handshake within 30s.");
            }

            process.StandardInput.Close();
            try { process.Kill(entireProcessTree: true); } catch { }

            return lines;
        }
    }
}
