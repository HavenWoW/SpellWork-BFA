using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class Db2ToolsTests : IClassFixture<McpClientDataFixture>
    {
        private readonly McpClientDataFixture _fixture;

        public Db2ToolsTests(McpClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task ListDb2_FlagsModelledAndUnmodelledStores()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.ListDb2();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");
            var stores = result.GetProperty("stores").EnumerateArray().ToList();

            Assert.Contains(stores, s => s.GetProperty("name").GetString() == "Spell"
                                         && s.GetProperty("modelled").GetBoolean());
            Assert.True(result.GetProperty("modelledCount").GetInt32() > 0);
        }

        [SkippableFact]
        public async Task GetDb2Row_ModelledStore_ReturnsNamedFields()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.GetDb2Row("Spell", 133);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("fieldNamesAvailable").GetBoolean());
            Assert.Equal("Fireball", result.GetProperty("row").GetProperty("Name").GetString());
        }

        [SkippableFact]
        public async Task GetDb2Row_UnknownStore_ListsAvailableStores()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.GetDb2Row("NotAStore", 1);
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("unknown_store", error.GetProperty("code").GetString());
            Assert.True(error.GetProperty("details").GetProperty("modelledStores").GetArrayLength() > 0);
        }

        [SkippableFact]
        public async Task ListDb2MarksShadowedDbc()
        {
            _fixture.SkipIfNoClientData();

            var rows = await Stores(_fixture.Db2Tools);
            var stale = rows.Single(r => r.GetProperty("name").GetString() == "SpellVisual" && r.GetProperty("extension").GetString() == "dbc");
            Assert.True(stale.GetProperty("shadowed").GetBoolean());
            Assert.False(stale.GetProperty("loaded").GetBoolean());

            var current = rows.Single(r => r.GetProperty("name").GetString() == "SpellVisual" && r.GetProperty("extension").GetString() == "db2");
            Assert.True(current.GetProperty("loaded").GetBoolean());
            Assert.True(current.GetProperty("modelled").GetBoolean());
            Assert.False(current.TryGetProperty("shadowed", out _));
        }

        [SkippableFact]
        public async Task ListDb2_ReportsUnmodelledFilesAndFileNames()
        {
            _fixture.SkipIfNoClientData();

            var rows = await Stores(_fixture.Db2Tools);
            Assert.Contains(rows, r => !r.GetProperty("modelled").GetBoolean() && r.GetProperty("extension").GetString() == "db2");
            Assert.Contains(rows, r => r.GetProperty("name").GetString() == "Item-sparse" && r.GetProperty("modelled").GetBoolean());
        }

        [SkippableFact]
        public async Task GetDb2RowAcceptsFileName()
        {
            _fixture.SkipIfNoClientData();

            var anyItem = SpellWork.DBC.DBC.ItemSparse.Keys.First();
            using var doc = JsonDocument.Parse(await _fixture.Db2Tools.GetDb2Row("Item-sparse", anyItem));
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal("ItemSparse", result.GetProperty("store").GetString());
            Assert.Equal(anyItem, result.GetProperty("row").GetProperty("ID").GetInt32());
        }

        [SkippableFact]
        public async Task QueryDb2AcceptsFileName()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.Db2Tools.QueryDb2("gtSpellScaling", limit: 1));
            Assert.Equal("GtSpellScaling", doc.RootElement.GetProperty("result").GetProperty("store").GetString());
        }

        private static async Task<System.Collections.Generic.List<JsonElement>> Stores(SpellWork.Mcp.Tools.Db2Tools tools)
        {
            var doc = JsonDocument.Parse(await tools.ListDb2());
            return doc.RootElement.GetProperty("result").GetProperty("stores").EnumerateArray().ToList();
        }

        [SkippableFact]
        public async Task GetDb2Row_MissingId_ReturnsRowNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.GetDb2Row("Spell", 999999999);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("row_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task QueryDb2_FilterMatchesOnFieldValue()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.QueryDb2("SpellEffect", filter: "SpellID=133");
            using var doc = JsonDocument.Parse(json);
            var rows = doc.RootElement.GetProperty("result").GetProperty("rows").EnumerateArray().ToList();

            Assert.NotEmpty(rows);
            foreach (var row in rows)
                Assert.Equal(133, row.GetProperty("SpellID").GetInt32());
        }

        // Companion to QueryDb2_FilterMatchesOnFieldValue: proves the filter
        // is actually excluding rows, not just failing to include wrong ones.
        // An implementation that ignored the filter entirely would return the
        // same (much larger) totalMatches here as for the SpellID=133 query.
        [SkippableFact]
        public async Task QueryDb2_FilterExcludesNonMatchingRows()
        {
            _fixture.SkipIfNoClientData();

            var filteredJson = await _fixture.Db2Tools.QueryDb2("SpellEffect", filter: "SpellID=133");
            using var filteredDoc = JsonDocument.Parse(filteredJson);
            var filteredTotal = filteredDoc.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32();

            var unfilteredJson = await _fixture.Db2Tools.QueryDb2("SpellEffect", limit: 1);
            using var unfilteredDoc = JsonDocument.Parse(unfilteredJson);
            var unfilteredTotal = unfilteredDoc.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32();

            Assert.True(filteredTotal > 0);
            Assert.True(filteredTotal < unfilteredTotal);
        }

        // Review finding: the brief's own documented example filter is
        // "SpellID=133,DifficultyID=0" -- two comma-joined clauses -- but
        // nothing verified both are actually applied. An implementation that
        // kept only the last clause (EffectIndex=0, matching many spells,
        // not just 133) or only the first (SpellID=133, matching every
        // effect of spell 133, not just index 0) would each fail one of the
        // assertions below; only applying both clauses together satisfies
        // all of them.
        [SkippableFact]
        public async Task QueryDb2_MultiClauseFilter_AppliesAllClauses()
        {
            _fixture.SkipIfNoClientData();

            // Spell 53 has four SpellEffect rows (EffectIndex 0-3) in this
            // client-data build, confirmed by inspection -- unlike spell 133
            // (Fireball), which has only one, so 133 could not distinguish
            // "both clauses applied" from "only the first was".
            var singleClauseJson = await _fixture.Db2Tools.QueryDb2("SpellEffect", filter: "SpellID=53");
            using var singleDoc = JsonDocument.Parse(singleClauseJson);
            var singleTotal = singleDoc.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32();

            // Test assumption, asserted rather than silently relied on:
            // spell 53 needs more than one effect for the second clause to
            // have anything to narrow away.
            Assert.True(singleTotal > 1);

            var multiClauseJson = await _fixture.Db2Tools.QueryDb2("SpellEffect", filter: "SpellID=53,EffectIndex=0");
            using var multiDoc = JsonDocument.Parse(multiClauseJson);
            var result = multiDoc.RootElement.GetProperty("result");
            var rows = result.GetProperty("rows").EnumerateArray().ToList();

            Assert.NotEmpty(rows);
            // Narrower than SpellID=53 alone: the second clause must have
            // done something, not merely failed to contradict the first.
            Assert.True(result.GetProperty("totalMatches").GetInt32() < singleTotal);
            foreach (var row in rows)
            {
                Assert.Equal(53, row.GetProperty("SpellID").GetInt32());
                Assert.Equal(0, row.GetProperty("EffectIndex").GetInt32());
            }
        }

        // Review finding: TryGetStore built Path.Combine(DbcPath, Locale,
        // requested + ".db2") and called File.Exists on it without
        // validating requested. Path.Combine's "a later rooted segment wins"
        // rule means a store name that is itself an absolute path discards
        // DbcPath and Locale entirely, so the probe can land anywhere on
        // disk -- a containment escape, even though it is read-only and
        // File.Exists never throws. Proven with a file this test owns
        // (nowhere near the configured DBC directory) rather than assuming
        // a particular OS file exists.
        [SkippableFact]
        public async Task GetDb2Row_StoreNameEscapingConfiguredDirectory_ReturnsUnknownStore()
        {
            _fixture.SkipIfNoClientData();

            var probeBasePath = Path.Combine(Path.GetTempPath(),
                "spellwork-db2-containment-probe-" + Guid.NewGuid().ToString("N"));
            var probeFilePath = probeBasePath + ".db2";
            File.WriteAllText(probeFilePath, "not a real db2 file -- existence alone must not matter");
            try
            {
                // requested + ".db2" reproduces probeFilePath exactly (an
                // absolute, rooted path), so a File.Exists-on-caller-input
                // implementation resolves straight to this file regardless
                // of DbcPath/Locale.
                var json = await _fixture.Db2Tools.GetDb2Row(probeBasePath, 1);
                using var doc = JsonDocument.Parse(json);
                var error = doc.RootElement.GetProperty("error");

                // An absolute path is never a valid store name: it must be
                // unknown_store, never store_not_modelled (which would mean
                // the tool just confirmed a file outside the configured
                // directory exists).
                Assert.Equal("unknown_store", error.GetProperty("code").GetString());
            }
            finally
            {
                File.Delete(probeFilePath);
            }
        }

        [SkippableFact]
        public async Task QueryDb2_StoreNameEscapingConfiguredDirectory_ReturnsUnknownStore()
        {
            _fixture.SkipIfNoClientData();

            var probeBasePath = Path.Combine(Path.GetTempPath(),
                "spellwork-db2-containment-probe-" + Guid.NewGuid().ToString("N"));
            var probeFilePath = probeBasePath + ".db2";
            File.WriteAllText(probeFilePath, "not a real db2 file -- existence alone must not matter");
            try
            {
                var json = await _fixture.Db2Tools.QueryDb2(probeBasePath);
                using var doc = JsonDocument.Parse(json);
                var error = doc.RootElement.GetProperty("error");

                Assert.Equal("unknown_store", error.GetProperty("code").GetString());
            }
            finally
            {
                File.Delete(probeFilePath);
            }
        }

        [SkippableFact]
        public async Task QueryDb2_ClampsLimitToMaxRows()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.QueryDb2("Spell", limit: 10000);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("rows").GetArrayLength() <= ToolResponse.MaxRows);
            Assert.True(result.GetProperty("truncated").GetBoolean());
        }

        [SkippableFact]
        public async Task QueryDb2_UnknownFilterField_ReturnsStructuredError()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.QueryDb2("Spell", filter: "NotAField=1");
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("unknown_field", error.GetProperty("code").GetString());
            Assert.True(error.GetProperty("details").GetProperty("availableFields").GetArrayLength() > 0);
        }

        // Spec 6.1: a store SpellWork does not model must produce an explicit
        // refusal naming the limitation, not empty positional rows presented
        // as if they were data.
        [SkippableFact]
        public async Task QueryDb2_UnmodelledStore_SaysSoExplicitly()
        {
            _fixture.SkipIfNoClientData();

            var listed = await _fixture.Db2Tools.ListDb2();
            using var listedDoc = JsonDocument.Parse(listed);
            var unmodelled = listedDoc.RootElement.GetProperty("result").GetProperty("stores")
                .EnumerateArray().FirstOrDefault(s => !s.GetProperty("modelled").GetBoolean());

            Skip.If(unmodelled.ValueKind == JsonValueKind.Undefined,
                "No unmodelled .db2 files present in the configured DBC path.");

            var json = await _fixture.Db2Tools.QueryDb2(unmodelled.GetProperty("name").GetString());
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("store_not_modelled", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetDb2Row_UnmodelledStore_SaysSoExplicitly()
        {
            _fixture.SkipIfNoClientData();

            var listed = await _fixture.Db2Tools.ListDb2();
            using var listedDoc = JsonDocument.Parse(listed);
            var unmodelled = listedDoc.RootElement.GetProperty("result").GetProperty("stores")
                .EnumerateArray().FirstOrDefault(s => !s.GetProperty("modelled").GetBoolean());

            Skip.If(unmodelled.ValueKind == JsonValueKind.Undefined,
                "No unmodelled .db2 files present in the configured DBC path.");

            var json = await _fixture.Db2Tools.GetDb2Row(unmodelled.GetProperty("name").GetString(), 1);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("store_not_modelled", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        // ---------------------------------------------------------------
        // One missing .db2 must degrade one store, not the whole load.
        //
        // DBC.LoadAsync's per-store catch used to cover only
        // DirectoryNotFoundException and TargetInvocationException.
        // DBReader throws FileNotFoundException when an individual .db2 is
        // absent while the locale directory itself exists (confirmed
        // directly: "Could not find file '...\\SpellName.db2'"), and that
        // escaped the Parallel.ForEach, propagated out of LoadAsync and
        // aborted everything -- forty-five stores that opened perfectly well
        // discarded because a forty-sixth was missing. Db2Tools'
        // store_not_loaded error and its per-store "loaded" flag were
        // therefore unreachable: shipped correct, but dead code behind an
        // invariant that was accidental rather than guaranteed.
        //
        // Every test below reads state the fixture captured while a
        // deliberately incomplete locale directory was installed on DBC.DBC's
        // statics; see McpClientDataFixture.RunPartialLoad for why that load
        // happens there and not here.
        // ---------------------------------------------------------------

        // The assertion that bites: pre-fix this reported ErrorCode
        // "load_failed" and Loaded false, because the FileNotFoundException
        // reached SpellDataService's catch.
        [SkippableFact]
        public void PartialLoad_OneMissingDb2_DoesNotAbortTheEntireLoad()
        {
            _fixture.SkipIfNoClientData();

            Assert.Null(_fixture.PartialResult.ErrorCode);
            Assert.True(_fixture.PartialResult.Loaded);

            foreach (var store in McpClientDataFixture.PresentStoreNames)
            {
                Assert.True(_fixture.PartialResult.StoreCounts[store] > 0,
                    $"{store}.db2 was present in the partial directory but loaded no rows.");
            }
        }

        // server_info renders storeCounts from exactly this dictionary (it
        // filters the zeroes out for display), so a missing store reads there
        // as a store with no rows -- the same as a store that loaded and
        // happened to be empty. list_db2's "loaded" flag below is what tells
        // those two apart; this pins the half server_info can carry.
        [SkippableFact]
        public void PartialLoad_MissingStore_CountsZeroInServerInfoStoreCounts()
        {
            _fixture.SkipIfNoClientData();

            Assert.Equal(0, _fixture.PartialResult.StoreCounts[McpClientDataFixture.MissingStoreName]);
        }

        [SkippableFact]
        public void PartialLoad_MissingStore_IsFlaggedNotLoadedByListDb2()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(_fixture.PartialListDb2Json);
            var stores = doc.RootElement.GetProperty("result").GetProperty("stores")
                .EnumerateArray().ToList();

            var missing = stores.Single(s =>
                s.GetProperty("name").GetString() == McpClientDataFixture.MissingStoreName);
            Assert.True(missing.GetProperty("modelled").GetBoolean());
            Assert.False(missing.GetProperty("loaded").GetBoolean());
            Assert.Equal(0, missing.GetProperty("rowCount").GetInt32());

            foreach (var name in McpClientDataFixture.PresentStoreNames)
            {
                var present = stores.Single(s => s.GetProperty("name").GetString() == name);
                Assert.True(present.GetProperty("loaded").GetBoolean(),
                    $"{name}.db2 was present in the partial directory but list_db2 flagged it unloaded.");
                Assert.True(present.GetProperty("rowCount").GetInt32() > 0);
            }
        }

        [SkippableFact]
        public void PartialLoad_MissingStore_ReachesStoreNotLoaded()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(_fixture.PartialMissingStoreRowJson);
            var error = doc.RootElement.GetProperty("error");

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("store_not_loaded", error.GetProperty("code").GetString());
            Assert.Contains(McpClientDataFixture.MissingStoreName, error.GetProperty("message").GetString());
            Assert.Equal("SPELLWORK_DBC_PATH",
                error.GetProperty("details").GetProperty("variable").GetString());
        }

        // DBC.UnloadedStores is the same fact list_db2 renders per store, in
        // the one form a caller can act on before any tool has been invoked --
        // and, unlike anything Db2Tools returns, it is readable when LoadAsync
        // throws instead of returning. The WinForms host reads exactly this to
        // decide whether to open (Program.StoppedOnIncompleteData); the MCP
        // server deliberately does not, which is why the assertions above still
        // describe a server that answered.
        [SkippableFact]
        public void PartialLoad_UnloadedStores_NamesThatStoreAndOnlyTheAbsentOnes()
        {
            _fixture.SkipIfNoClientData();

            Assert.Contains(McpClientDataFixture.MissingStoreName, _fixture.PartialUnloadedStores);

            foreach (var name in McpClientDataFixture.PresentStoreNames)
            {
                Assert.DoesNotContain(name, _fixture.PartialUnloadedStores);
            }

            // Every name is a real modelled store, so a host can put it
            // straight into "<name>.db2 is missing" without translating it.
            foreach (var name in _fixture.PartialUnloadedStores)
            {
                Assert.NotNull(typeof(SpellWork.DBC.DBC).GetProperty(name,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            }

            // Sorted, so the dialog the GUI builds from it is stable across
            // runs rather than ordered by however reflection happened to
            // enumerate.
            Assert.Equal(
                _fixture.PartialUnloadedStores.OrderBy(name => name, StringComparer.Ordinal).ToList(),
                _fixture.PartialUnloadedStores);
        }

        // The other half of "degraded, not broken": the stores whose files
        // were present still answer normally in the same process state.
        [SkippableFact]
        public void PartialLoad_PresentStore_StillAnswers()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(_fixture.PartialPresentStoreQueryJson);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(McpClientDataFixture.PresentStoreNames[0], result.GetProperty("store").GetString());
            Assert.True(result.GetProperty("rows").GetArrayLength() > 0);
        }

        // The partial load must leave the suite's real client data exactly as
        // it found it -- if the snapshot/restore in RunPartialLoad were
        // incomplete, every other client-data test in this assembly would fail
        // in ways that pointed anywhere but here.
        [SkippableFact]
        public async Task PartialLoad_RestoresTheRealStoresAfterwards()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.GetDb2Row(McpClientDataFixture.MissingStoreName, 1);
            using var doc = JsonDocument.Parse(json);

            // Whatever this store answers now, it must not be "not loaded":
            // the real client data ships the file.
            if (!doc.RootElement.GetProperty("ok").GetBoolean())
            {
                Assert.NotEqual("store_not_loaded",
                    doc.RootElement.GetProperty("error").GetProperty("code").GetString());
            }
        }
    }
}
