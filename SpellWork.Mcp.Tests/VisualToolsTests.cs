using System.Collections.Generic;
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

        // Fireball. Same spell SpellVisualChainResolverTests.cs uses -- one
        // SpellXSpellVisual row (visual 69095, difficulty 0), confirmed to
        // reach kits and resolve missile info against this build's real data.
        private const int Fireball = 133;

        [SkippableFact]
        public async Task GetSpellVisualChain_Fireball_ReturnsResolvedVisual()
        {
            _fixture.SkipIfNoClientData();

            var json = await new VisualTools(_fixture.LoadedService).GetSpellVisualChain(Fireball);
            using var doc = JsonDocument.Parse(json);

            // ToolResponse.Ok nests every payload under "result" -- see
            // ToolResponse.cs:24-29 and ToolResponseTests.Ok_WrapsPayloadUnderOkTrue.
            // The brief's draft asserted doc.RootElement.GetProperty("variants")
            // directly, which does not exist at the root.
            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            var result = doc.RootElement.GetProperty("result");
            var variants = result.GetProperty("variants");
            Assert.NotEqual(0, variants.GetArrayLength());

            // Spot-check the shape once here rather than in a separate test:
            // at least one variant must actually resolve its visual and reach
            // at least one kit, or "ReturnsResolvedVisual" is not proving what
            // its name claims.
            var resolvedWithKits = false;
            foreach (var variant in variants.EnumerateArray())
            {
                if (variant.GetProperty("visualResolved").GetBoolean() &&
                    variant.GetProperty("kits").GetArrayLength() > 0)
                {
                    resolvedWithKits = true;
                    break;
                }
            }

            Assert.True(resolvedWithKits, "Expected at least one resolved Fireball variant with kits.");
        }

        [SkippableFact]
        public async Task GetSpellVisualChain_UnknownSpell_ReportsSpellNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await new VisualTools(_fixture.LoadedService).GetSpellVisualChain(999999999);
            using var doc = JsonDocument.Parse(json);

            // Same envelope correction as above: the brief's draft asserted
            // doc.RootElement.GetProperty("error").GetString(), but "error" is
            // an object ({"code":...,"message":...}), not a bare string -- see
            // ToolResponse.Error and every other tool's *_ReturnsNotFound test
            // (e.g. SpellToolsTests.GetSpell_UnknownId_ReturnsNotFoundWithSuggestions).
            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("spell_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        // Measured: this build's golden-file id with no SpellXSpellVisual rows
        // at any difficulty, so it is the other half of the distinction below.
        private const int SpellWithNoVisualsAnywhere = 15594;

        [SkippableFact]
        public async Task GetSpellVisualChain_FilteredToNothing_NamesTheDifficultiesThatDoHaveVariants()
        {
            _fixture.SkipIfNoClientData();

            // Fireball has exactly one SpellXSpellVisual row, at difficulty 0,
            // so any other difficulty filters it away. This tool shipped
            // returning a bare {"variants": [], "truncated": false} for that --
            // indistinguishable from a spell that has no visuals at all, which
            // is the exact failure get_spell's json section had already been
            // fixed for. The two hosts now build this from one call.
            var json = await new VisualTools(_fixture.LoadedService).GetSpellVisualChain(Fireball, difficulty: 1);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(0, result.GetProperty("variants").GetArrayLength());

            var elsewhere = result.GetProperty("variantsAtOtherDifficulties");
            Assert.Equal(1, elsewhere.GetArrayLength());
            Assert.Equal(0, elsewhere[0].GetInt32());
        }

        [SkippableFact]
        public async Task GetSpellVisualChain_SpellWithNoVisualsAnywhere_OmitsVariantsAtOtherDifficulties()
        {
            _fixture.SkipIfNoClientData();
            Skip.IfNot(SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(SpellWithNoVisualsAnywhere),
                $"Spell {SpellWithNoVisualsAnywhere} is absent from this build.");

            // Absent key, not an empty one: that is what makes the key above
            // mean something. If it were always present, a caller could not use
            // it to tell "filtered away" from "has none".
            var json = await new VisualTools(_fixture.LoadedService)
                .GetSpellVisualChain(SpellWithNoVisualsAnywhere);
            using var doc = JsonDocument.Parse(json);

            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(0, result.GetProperty("variants").GetArrayLength());
            Assert.False(result.TryGetProperty("variantsAtOtherDifficulties", out _));
        }

        // The brief's third test here (GetSpellVisualChain_ReportsUnloadedStores)
        // skipped unconditionally in both configurations -- SkipIfNoClientData
        // skips when data is absent, and Skip.If(DBC.SpellVisual != null, ...)
        // skips when data is present, because McpClientDataFixture's
        // constructor always loads real data into DBC.DBC's statics before any
        // test body runs. See VisualToolsUnloadedStoreTests below for the
        // replacement: a plain, unskippable Fact in its own class, mirroring
        // SpellVisualChainResolverUnloadedStoreTests (SpellWork.Core.Tests/
        // SpellVisualChainResolverTests.cs), with the same honest caveat about
        // what it can and cannot prove.
    }

    /// <summary>
    /// The tool's behaviour when no client data is configured -- a plain,
    /// unskippable <see cref="Fact"/>, in its own class that does not take
    /// <see cref="McpClientDataFixture"/>, so it runs in every configuration.
    /// <para>
    /// <b>Why the outcome is deterministic, despite the process-wide statics.</b>
    /// This class takes no fixture, so it forms its own xunit collection and
    /// runs concurrently with every fixture-based class in this assembly -- there
    /// is no <c>xunit.runner.json</c>, <c>[CollectionBehavior]</c> or
    /// <c>.runsettings</c> anywhere in the repo, so nothing orders it relative to
    /// them, and <c>DBC.DBC</c>'s <c>Storage&lt;T&gt;</c> properties may or may
    /// not be populated by real data while this body runs. None of that reaches
    /// this test, because the service below is constructed over an <em>empty</em>
    /// environment dictionary: <c>McpServerConfig.FromEnvironment</c> reads only
    /// the dictionary it is handed and never the real environment
    /// (<c>McpServerConfig.cs:45-62</c>), so <c>DbcPath</c> is null,
    /// <c>SpellDataService.ValidatePaths</c> returns <c>dbc_path_missing</c>
    /// before any load is attempted, and <c>data.Loaded</c> is false every time.
    /// The gate's <c>!data.Loaded</c> term therefore trips on every run, in both
    /// configurations of this suite.
    /// </para>
    /// <para>
    /// <b>What this test proves.</b> That the gate produces a clean
    /// <c>store_not_loaded</c> naming what the walk needs, rather than throwing
    /// or falling through to a bad-id error that would misreport a configuration
    /// fault. It does not prove the store-inventory half of the gate fires --
    /// <c>section.Unavailable</c> is not what refuses this call, <c>!data.Loaded</c>
    /// is -- and it cannot, because a service with no configured path never gets
    /// far enough to have stores. The inventory half is covered in
    /// <c>SpellWork.Core.Tests</c>, where
    /// <c>SpellVisualSectionTests</c> drives each of the six missing stores
    /// through an index built without it and
    /// <c>SpellVisualChainIndexTests.MissingStores_NamesEachStoreTheWalkCannotDoWithout</c>
    /// pins the list itself.
    /// </para>
    /// <para>
    /// <b>Superseded rationale, recorded because the reasoning was wrong rather
    /// than merely out of date.</b> This class previously accepted either
    /// <c>store_not_loaded</c> or <c>spell_not_found</c> and described a race
    /// between them: the claim was that another collection's fixture could load
    /// real data first, letting the call reach the spell-id check. It cannot --
    /// the gate above never depended on the statics at all for this service.
    /// The <c>spell_not_found</c> disjunct was dead from the day it was written,
    /// and the conditional message assertion it forced meant the strongest claim
    /// here was only checked some of the time. Both are now unconditional.
    /// </para>
    /// </summary>
    public class VisualToolsUnloadedStoreTests
    {
        // Deliberately not a real spell in any build -- the same id
        // GetSpellVisualChain_UnknownSpell_ReportsSpellNotFound uses above. The
        // gate refuses this call before the id is ever looked at, so the choice
        // no longer matters for correctness; it is kept so that a regression
        // which *removed* the gate surfaces here as spell_not_found rather than
        // as a fabricated success.
        private const int NonexistentSpellId = 999999999;

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

            var message = error.GetProperty("message").GetString();

            // Names what the walk needs, so an operator knows what to extract...
            Assert.Contains("SpellVisualKitEffect", message);

            // ...but does not claim those six files were individually looked for
            // and missed. Nothing was: no path was configured, so no load ran.
            // Naming them as "did not load" sends an operator hunting for files
            // that may be sitting on disk already.
            Assert.DoesNotContain("did not load:", message);
            Assert.Contains("no client data is loaded at all", message);
        }
    }
}
