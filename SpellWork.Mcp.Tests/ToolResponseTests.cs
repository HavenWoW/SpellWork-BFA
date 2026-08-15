using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class ToolResponseTests
    {
        [Fact]
        public void Ok_WrapsPayloadUnderOkTrue()
        {
            var json = ToolResponse.Ok(new Dictionary<string, object> { ["id"] = 133 });
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(133, doc.RootElement.GetProperty("result").GetProperty("id").GetInt32());
        }

        [Fact]
        public void Error_CarriesCodeMessageAndDetails()
        {
            var json = ToolResponse.Error("dbc_path_missing", "SPELLWORK_DBC_PATH is not set.",
                new Dictionary<string, object> { ["variable"] = "SPELLWORK_DBC_PATH" });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.False(root.GetProperty("ok").GetBoolean());
            Assert.Equal("dbc_path_missing", root.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal("SPELLWORK_DBC_PATH is not set.", root.GetProperty("error").GetProperty("message").GetString());
            Assert.Equal("SPELLWORK_DBC_PATH", root.GetProperty("error").GetProperty("details").GetProperty("variable").GetString());
        }

        [Fact]
        public void MaxRows_IsFiveHundred()
        {
            Assert.Equal(500, ToolResponse.MaxRows);
        }

        private static List<object> Rows(int count) =>
            Enumerable.Range(0, count).Select(i => (object)i).ToList();

        // A tool that forgot to clamp its own query (e.g. Skip(offset).Take(
        // limit) with a caller-supplied limit above MaxRows) must not be able
        // to flood the agent's context. Paged is the backstop.
        [Fact]
        public void Paged_ClampsRowCountAboveMaxRows_AndMarksTruncated()
        {
            var page = ToolResponse.Paged(Rows(600), totalMatches: 600, offset: 0, limit: 600);

            Assert.Equal(500, page["count"]);
            Assert.Equal(500, ((List<object>)page["rows"]).Count);
            Assert.Equal(500, page["limit"]);
            Assert.True((bool)page["truncated"]);
        }

        [Fact]
        public void Paged_PartialPageMidResult_IsMarkedTruncated()
        {
            var page = ToolResponse.Paged(Rows(100), totalMatches: 250, offset: 0, limit: 100);

            Assert.Equal(100, page["count"]);
            Assert.Equal(100, page["limit"]);
            Assert.True((bool)page["truncated"]);
        }

        [Fact]
        public void Paged_CompleteFinalPage_IsNotTruncated()
        {
            // 200 rows already served (offset), this call returns the last
            // 50 of 250 total matches -- nothing withheld.
            var page = ToolResponse.Paged(Rows(50), totalMatches: 250, offset: 200, limit: 100);

            Assert.Equal(50, page["count"]);
            Assert.False((bool)page["truncated"]);
        }

        // The envelope's "limit" is the ceiling actually applied, not the
        // (absurdly large) value the caller asked for -- even when the cap
        // never had to trim any rows because fewer were available.
        [Fact]
        public void Paged_ReportsEffectiveLimit_NotRequestedLimit()
        {
            var page = ToolResponse.Paged(Rows(3), totalMatches: 3, offset: 0, limit: 10_000);

            Assert.Equal(500, page["limit"]);
            Assert.Equal(3, page["count"]);
            Assert.False((bool)page["truncated"]);
        }

        [Fact]
        public void Paged_NegativeOffset_TreatedAsZero()
        {
            var page = ToolResponse.Paged(Rows(50), totalMatches: 50, offset: -5, limit: 50);

            Assert.Equal(0, page["offset"]);
            Assert.False((bool)page["truncated"]);
        }

        [Fact]
        public void Paged_NegativeLimit_TreatedAsZero_NoRowsReturned()
        {
            var page = ToolResponse.Paged(Rows(50), totalMatches: 50, offset: 0, limit: -10);

            Assert.Equal(0, page["limit"]);
            Assert.Equal(0, page["count"]);
            Assert.True((bool)page["truncated"]);
        }

        // Rows(50), not an empty list: handed nothing, this test passed
        // against any implementation at all -- it proved empty-in/empty-out
        // rather than that limit 0 clamps rows away. With 50 rows supplied,
        // the assertions only hold if the clamp actually discards them.
        [Fact]
        public void Paged_ZeroLimit_TreatedAsZero_NoRowsReturned()
        {
            var page = ToolResponse.Paged(Rows(50), totalMatches: 50, offset: 0, limit: 0);

            Assert.Equal(0, page["limit"]);
            Assert.Equal(0, page["count"]);
            Assert.Empty((List<object>)page["rows"]);
            Assert.True((bool)page["truncated"]);
        }
    }
}
