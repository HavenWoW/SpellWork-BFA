using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class SpellToolsTests : IClassFixture<McpClientDataFixture>
    {
        private readonly McpClientDataFixture _fixture;

        public SpellToolsTests(McpClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task GetSpell_ReturnsJsonForFireball()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(133, doc.RootElement.GetProperty("result").GetProperty("id").GetInt32());
            Assert.Equal("Fireball", doc.RootElement.GetProperty("result").GetProperty("name").GetString());
        }

        [SkippableFact]
        public async Task UnknownDifficultyIsAStructuredError()
        {
            _fixture.SkipIfNoClientData();

            foreach (var format in new[] { "json", "text" })
            {
                using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, format: format, difficulty: 10)); // 10 has no Difficulty row
                var error = doc.RootElement.GetProperty("error");
                Assert.Equal("unknown_difficulty", error.GetProperty("code").GetString());
                Assert.Contains(2, error.GetProperty("details").GetProperty("valid").EnumerateArray().Select(x => x.GetInt32()));
            }
        }

        [SkippableFact]
        public async Task GetSpell_KnownDifficulty_IsAcceptedInBothFormats()
        {
            _fixture.SkipIfNoClientData();

            using var json = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(43093, difficulty: 2));
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(2, json.RootElement.GetProperty("result").GetProperty("difficulty").GetProperty("requested").GetInt32());

            using var text = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(43093, format: "text", difficulty: 2));
            Assert.True(text.RootElement.GetProperty("ok").GetBoolean());
        }

        [SkippableFact]
        public async Task SearchByAttributeWord6Hex()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(attribute: "6:0x1"));
            var expected = SpellWork.DBC.DBC.SpellInfoStore.Values.Count(s => (s.Attributes[6] & 1u) != 0);
            Assert.Equal(expected, doc.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32());
        }

        [SkippableFact]
        public async Task SearchByAttribute_WordPrefixSelectsTheWord_AndNamesResolvePerWord()
        {
            _fixture.SkipIfNoClientData();

            using var word7 = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(attribute: "7:SPELL_ATTR7_ALLOW_SPELL_REFLECTION"));
            var expected = SpellWork.DBC.DBC.SpellInfoStore.Values.Count(s => (s.Attributes[7] & 1u) != 0);
            Assert.Equal(expected, word7.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32());

            // No prefix keeps the original meaning: word 0.
            using var plain = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(attribute: "SPELL_ATTR0_PASSIVE"));
            var passive = SpellWork.DBC.DBC.SpellInfoStore.Values.Count(s => (s.Attributes[0] & 0x40u) != 0);
            Assert.Equal(passive, plain.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32());
        }

        [SkippableTheory]
        [InlineData("8:0x1")]
        [InlineData("13:0x1")]
        [InlineData("14:0x1")]
        [InlineData("-1:0x1")]
        [InlineData("x:1")]
        public async Task SearchByAttribute_BadWord_IsInvalidEnumValue(string attribute)
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.SearchSpells(attribute: attribute));
            Assert.Equal("invalid_enum_value", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_VisualSection_LoadedShape()
        {
            _fixture.SkipIfNoClientData();

            // Real data has the visual stores; just pin the loaded shape here.
            // The gated shape is pinned in SpellWork.Core.Tests.
            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, sections: new[] { "visual" }));
            Assert.False(doc.RootElement.GetProperty("result").GetProperty("visual").GetProperty("storeUnloaded").GetBoolean());
        }

        [SkippableFact]
        public async Task GetSpell_TextFormat_ReturnsRenderedText()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133, format: "text");
            using var doc = JsonDocument.Parse(json);

            var text = doc.RootElement.GetProperty("result").GetProperty("text").GetString();
            Assert.Contains("ID - 133", text);
        }

        [SkippableFact]
        public async Task GetSpell_UnknownId_ReturnsNotFoundWithSuggestions()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(999999999);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.False(root.GetProperty("ok").GetBoolean());
            Assert.Equal("spell_not_found", root.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_UnknownSection_ReturnsStructuredErrorNotAnException()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133, sections: new[] { "nonsense" });
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("invalid_section", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_UnknownFormat_ReturnsStructuredError()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133, format: "xml");
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("invalid_format", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task SearchSpells_ByName_FindsFireball()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(name: "Fireball");
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("totalMatches").GetInt32() > 0);
            Assert.Contains(result.GetProperty("rows").EnumerateArray(),
                row => row.GetProperty("id").GetInt32() == 133);
        }

        [SkippableFact]
        public async Task SearchSpells_ByFamilyName_AcceptsEnumNameOrNumber()
        {
            _fixture.SkipIfNoClientData();

            var byName = await _fixture.SpellTools.SearchSpells(family: "SPELLFAMILY_MAGE", limit: 5);
            var byNumber = await _fixture.SpellTools.SearchSpells(family: "3", limit: 5);
            var unfiltered = await _fixture.SpellTools.SearchSpells(limit: 1);

            Assert.Equal(TotalMatches(byName), TotalMatches(byNumber));
            Assert.True(TotalMatches(byName) > 0);

            // Bound from above as well as below: a filter that silently did
            // nothing would return the same large unfiltered total down both
            // the name and number paths and pass the equality assertion
            // above unchanged. Only a total strictly smaller than the
            // universe proves the filter narrowed anything at all.
            Assert.True(TotalMatches(byName) < TotalMatches(unfiltered));

            static int TotalMatches(string json)
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32();
            }
        }

        // The cap is a hard server-side limit, not a floor: ToolResponse.Paged
        // is the single authoritative place limit=0 means zero rows, not "at
        // least one" and not "unbounded". totalMatches > 0 is asserted in the
        // same test so it cannot pass because the search matched nothing.
        [SkippableFact]
        public async Task SearchSpells_ZeroLimit_ReturnsNoRows()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(name: "a", limit: 0);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("totalMatches").GetInt32() > 0);
            Assert.Equal(0, result.GetProperty("count").GetInt32());
            Assert.Empty(result.GetProperty("rows").EnumerateArray());
            Assert.Equal(0, result.GetProperty("limit").GetInt32());
        }

        // Review Focus 4: a broad name query over every Spell row (209,510 on
        // the reference data) answers in under 2 s and honours the row cap.
        // The data load is awaited first so only the search itself is timed.
        [SkippableFact]
        public async Task SearchSpells_BroadName_IsFastAndCapped()
        {
            _fixture.SkipIfNoClientData();
            await _fixture.LoadedService.GetAsync();

            foreach (var limit in new int?[] { null, 10000 })
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var json = limit.HasValue
                    ? await _fixture.SpellTools.SearchSpells(name: "a", limit: limit.Value)
                    : await _fixture.SpellTools.SearchSpells(name: "a");
                stopwatch.Stop();

                using var doc = JsonDocument.Parse(json);
                var result = doc.RootElement.GetProperty("result");
                Assert.True(stopwatch.Elapsed < System.TimeSpan.FromSeconds(2),
                    $"search_spells(name: \"a\", limit: {limit?.ToString() ?? "default"}) took {stopwatch.ElapsedMilliseconds} ms");
                Assert.True(result.GetProperty("totalMatches").GetInt32() > ToolResponse.MaxRows, "the query must be broad enough to hit the cap");
                Assert.True(result.GetProperty("rows").GetArrayLength() <= ToolResponse.MaxRows);
                Assert.True(result.GetProperty("truncated").GetBoolean());
            }
        }

        // The cap is a hard server-side limit, not a suggestion: a model that
        // asks for 10000 rows must not be able to flood its own context.
        [SkippableFact]
        public async Task SearchSpells_ClampsLimitToMaxRows()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(name: "a", limit: 10000);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("rows").GetArrayLength() <= ToolResponse.MaxRows);
            Assert.Equal(ToolResponse.MaxRows, result.GetProperty("limit").GetInt32());
        }

        [SkippableFact]
        public async Task SearchSpells_TruncatedFlag_IsTrueWhenMoreRemain()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(name: "a", limit: 5);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(5, result.GetProperty("rows").GetArrayLength());
            Assert.True(result.GetProperty("truncated").GetBoolean());
        }

        [SkippableFact]
        public async Task SearchSpells_UnknownEnumName_ReturnsStructuredError()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(family: "SPELLFAMILY_NOT_A_THING");
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("invalid_enum_value", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task SearchSpells_Offset_PagesWithoutOverlap()
        {
            _fixture.SkipIfNoClientData();

            var first = await _fixture.SpellTools.SearchSpells(name: "Fire", limit: 10, offset: 0);
            var second = await _fixture.SpellTools.SearchSpells(name: "Fire", limit: 10, offset: 10);

            // "Fire" matches far more than 20 spells, so both pages must be
            // fully populated -- otherwise an empty second page would make
            // the intersection assertion below pass trivially.
            Assert.Equal(10, Ids(first).Count);
            Assert.Equal(10, Ids(second).Count);
            Assert.Empty(Ids(first).Intersect(Ids(second)));

            static System.Collections.Generic.List<int> Ids(string json)
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("rows").EnumerateArray()
                    .Select(row => row.GetProperty("id").GetInt32()).ToList();
            }
        }

        [SkippableFact]
        public async Task GetSpellTriggers_ReportsForwardDirectionWithEffectIndex()
        {
            _fixture.SkipIfNoClientData();

            var spell = SpellWork.DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Any(e => e != null && e.Entry.EffectTriggerSpell != 0
                                   && SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(e.Entry.EffectTriggerSpell)));

            var json = await _fixture.SpellTools.GetSpellTriggers(spell.ID);
            using var doc = JsonDocument.Parse(json);
            var triggers = doc.RootElement.GetProperty("result").GetProperty("triggers");

            Assert.True(triggers.GetArrayLength() > 0);
            var first = triggers[0];
            Assert.True(first.GetProperty("id").GetInt32() != 0);
            Assert.False(string.IsNullOrEmpty(first.GetProperty("name").GetString()));
            Assert.True(first.GetProperty("effectIndex").GetInt32() >= 0);
        }

        [SkippableFact]
        public async Task GetSpellTriggers_ReportsReverseDirection()
        {
            _fixture.SkipIfNoClientData();

            // SpellTriggerStore.Keys.First() is not by itself guaranteed to be
            // a spell that exists in SpellInfoStore: EffectTriggerSpell can
            // (and, in real client data, does -- roughly 1 in 30 keys here)
            // name an id that no other table has a row for, e.g. a spell
            // that belongs to an expansion or table this client data does
            // not carry. get_spell_triggers correctly returns spell_not_found
            // for such an id, the same as every other tool does for an id
            // outside SpellInfoStore, so the id under test here must itself
            // be a loaded spell -- otherwise this test is asserting on
            // whichever key the dictionary's enumeration order happens to
            // put first, which is exactly what produced the failure this
            // filter fixes.
            var triggeredId = SpellWork.DBC.DBC.SpellTriggerStore.Keys
                .First(candidate => SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(candidate));

            var json = await _fixture.SpellTools.GetSpellTriggers(triggeredId);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("result").GetProperty("triggeredBy").GetArrayLength() > 0);
        }

        [SkippableFact]
        public async Task GetSpellTriggers_UnknownId_ReturnsNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpellTriggers(999999999);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("spell_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task CompareSpells_IdenticalIds_ReportNoDifferences()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 133);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("identical").GetBoolean());
            Assert.Equal(0, result.GetProperty("differences").GetArrayLength());
        }

        [SkippableFact]
        public async Task CompareSpells_DifferentSpells_ReportPathedDifferences()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 116);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.False(result.GetProperty("identical").GetBoolean());
            var differences = result.GetProperty("differences");
            Assert.True(differences.GetArrayLength() > 0);

            // Every difference must name where it is, or the output is a wall
            // of values with no way to act on it.
            foreach (var difference in differences.EnumerateArray())
                Assert.False(string.IsNullOrEmpty(difference.GetProperty("path").GetString()));

            // 133 Fireball is Fire, 116 Frostbolt is Frost -- the school mask
            // differing is the one difference we can name in advance.
            Assert.Contains(differences.EnumerateArray(),
                d => d.GetProperty("path").GetString().Contains("schoolMask"));
        }

        // The two tests below exercise Diff's cap directly, through
        // reflection against its private recursion, rather than only
        // through CompareSpells and real spell data. CompareSpells(133, 116)
        // -- the pair CompareSpells_CapsDifferenceCount below uses -- only
        // ever produces 37 differences in this client data, so an assertion
        // built solely on that pair would pass against any implementation,
        // capped or not, and would prove nothing about the cap itself. A
        // synthetic JSON document exercises the cap for real, cheaply, and
        // this pair of tests caught two real defects in Diff on first run
        // (both reproduced and confirmed before the fix below was applied):
        //   1. the "missing key" / "missing array index" fast paths inside
        //      Diff's object/array loops were not individually gated by the
        //      cap check -- only the function-entry check was -- so a
        //      single call whose keys/indices are mostly one-sided could add
        //      far more than MaxRows differences in one pass (a 2000-key
        //      one-sided object produced exactly 2000 differences, not 500)
        //      before any recursive call got a chance to see the cap.
        //   2. "differences.Count > MaxRows" is false exactly when ordinary
        //      single-increment recursion lands precisely on the cap, even
        //      though further, never-visited differences exist past that
        //      point (5000 one-by-one differences stopped the recursion at
        //      exactly 500 while 4500 more were never inspected, and the
        //      old ">" formula reported truncated=false) -- violating the
        //      same "truncated is never false while rows are missing" rule
        //      ToolResponse.Paged documents. CompareSpells answers this by
        //      collecting one difference past the cap as a probe: it calls
        //      Diff with MaxRows + 1, so "count > MaxRows" is exact in both
        //      directions -- true only when a difference really was withheld,
        //      false when exactly MaxRows exist and nothing was.
        // Diff takes an explicit cap rather than reading ToolResponse.MaxRows
        // itself: CompareSpells passes MaxRows + 1 (see the boundary tests
        // below), so every reflection call here must supply one too.
        private static System.Collections.Generic.List<object> RunDiff(JsonElement a, JsonElement b, int cap)
        {
            var differences = new System.Collections.Generic.List<object>();
            var method = typeof(SpellWork.Mcp.Tools.SpellTools).GetMethod("Diff",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method.Invoke(null, new object[] { "", a, b, differences, cap });
            return differences;
        }

        [Fact]
        public void Diff_ObjectWithManyOneSidedKeys_NeverExceedsMaxRows()
        {
            var propsA = new System.Collections.Generic.Dictionary<string, object>();
            for (var i = 0; i < ToolResponse.MaxRows * 4; i++)
                propsA[$"field{i:D5}"] = i; // present only in A -- none of these keys exist in B

            using var docA = JsonDocument.Parse(JsonSerializer.Serialize(propsA));
            using var docB = JsonDocument.Parse("{}");

            var differences = RunDiff(docA.RootElement, docB.RootElement, ToolResponse.MaxRows);

            Assert.Equal(ToolResponse.MaxRows, differences.Count);
        }

        // Review finding 1: the previous version of this test used
        // equal-length arrays (every element present on both sides, only
        // values differing), which only ever walks the recursive
        // value-mismatch path -- never the array's own "missing index" fast
        // path in the loop body, which is one of the two branches that
        // actually carried the uncapped bug (see the class comment above).
        // Hand-tracing confirmed the old test passed identically against the
        // brief's unfixed Diff. This version drives every iteration through
        // "i >= arrayA.Count" instead by making side A empty and side B far
        // larger than MaxRows, so it genuinely exercises the branch that was
        // broken. Confirmed by mutation: temporarily removing this loop's
        // "if (differences.Count >= cap) break;" line reproduced the original
        // bug and made this assertion fail with differences.Count == 2000,
        // not 500 (see task-7-report.md for the full transcript); reverting
        // the removal makes it pass again.
        [Fact]
        public void Diff_ArrayWithManyMissingIndices_NeverExceedsMaxRows()
        {
            var arrayA = System.Array.Empty<int>();
            var arrayB = System.Linq.Enumerable.Range(0, 2000).ToArray();

            using var docA = JsonDocument.Parse(JsonSerializer.Serialize(arrayA));
            using var docB = JsonDocument.Parse(JsonSerializer.Serialize(arrayB));

            var differences = RunDiff(docA.RootElement, docB.RootElement, ToolResponse.MaxRows);

            Assert.Equal(ToolResponse.MaxRows, differences.Count);
        }

        // Review finding 2: pins the exact boundary CompareSpells's probe
        // (cap = MaxRows + 1) is meant to distinguish -- "exactly MaxRows
        // differences, nothing withheld" from "more than MaxRows, some
        // withheld" -- in both directions, using the same formula
        // CompareSpells applies to Diff's output ("differences.Count >
        // ToolResponse.MaxRows").
        [Fact]
        public void Diff_ExactlyMaxRowsDifferences_IsNotTruncated()
        {
            var arrayA = System.Linq.Enumerable.Range(0, ToolResponse.MaxRows).ToArray();
            var arrayB = System.Linq.Enumerable.Range(1, ToolResponse.MaxRows).ToArray(); // every element differs, exactly MaxRows of them

            using var docA = JsonDocument.Parse(JsonSerializer.Serialize(arrayA));
            using var docB = JsonDocument.Parse(JsonSerializer.Serialize(arrayB));

            var differences = RunDiff(docA.RootElement, docB.RootElement, ToolResponse.MaxRows + 1);

            Assert.Equal(ToolResponse.MaxRows, differences.Count);
            Assert.False(differences.Count > ToolResponse.MaxRows);
        }

        [Fact]
        public void Diff_MaxRowsPlusOneDifferences_IsTruncated_AndRowsCapAtMaxRows()
        {
            var arrayA = System.Linq.Enumerable.Range(0, ToolResponse.MaxRows + 1).ToArray();
            var arrayB = System.Linq.Enumerable.Range(1, ToolResponse.MaxRows + 1).ToArray(); // MaxRows + 1 differences

            using var docA = JsonDocument.Parse(JsonSerializer.Serialize(arrayA));
            using var docB = JsonDocument.Parse(JsonSerializer.Serialize(arrayB));

            var differences = RunDiff(docA.RootElement, docB.RootElement, ToolResponse.MaxRows + 1);

            Assert.Equal(ToolResponse.MaxRows + 1, differences.Count);
            Assert.True(differences.Count > ToolResponse.MaxRows);
            Assert.Equal(ToolResponse.MaxRows,
                System.Linq.Enumerable.Take(differences, ToolResponse.MaxRows).Count());
        }

        // ---------------------------------------------------------------
        // SpellInfoJsonSerializer.Serialize writes "id" and "name" into the
        // root unconditionally, outside every Wants(...) gate, so before
        // CompareSpells stripped them each cross-spell diff carried at least
        // those two guaranteed differences. "identical" was therefore true
        // only when idA == idB -- an advertised output field permanently
        // false for the only comparison anyone runs -- and totalDifferences
        // was inflated by two, consuming two of the 500 difference slots.
        //
        // All three tests below fail against the unfixed code, measured
        // rather than assumed: compare(38695, 38696) reported identical=False
        // with exactly 2 differences, and compare(133, 116) reported
        // totalDifferences 37 instead of 35.
        // ---------------------------------------------------------------

        // The assertion the review asked for: two *different* spell ids that
        // compare identical. On the CoA 3.3.5a data 34 "Teleport Duskwood"
        // and 35 "Teleport Elwynn" are distinct ids with distinct names whose
        // compared sections are identical (the destination lives in the world
        // database, not Spell.dbc). Measured, not assumed. Pre-fix this
        // returned identical=False with the two identity differences and
        // nothing else, which is the whole bug in one call.
        [SkippableFact]
        public async Task CompareSpells_DistinctSpellsWithTheSameDefinition_AreIdentical()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(34, 35);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("identical").GetBoolean(),
                "Two spells with identical definitions must compare identical even though their ids differ.");
            Assert.Equal(0, result.GetProperty("differences").GetArrayLength());
            Assert.Equal(0, result.GetProperty("totalDifferences").GetInt32());
            Assert.False(result.GetProperty("truncated").GetBoolean());

            // Nothing is lost by dropping the identity paths: which spell is
            // which is still reported, in the blocks that exist to say so.
            Assert.Equal(34, result.GetProperty("a").GetProperty("id").GetInt32());
            Assert.Equal(35, result.GetProperty("b").GetProperty("id").GetInt32());
            Assert.NotEqual(result.GetProperty("a").GetProperty("name").GetString(),
                result.GetProperty("b").GetProperty("name").GetString());
        }

        [SkippableFact]
        public async Task CompareSpells_NeverReportsIdentityAsADifference()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 116);
            using var doc = JsonDocument.Parse(json);
            var differences = doc.RootElement.GetProperty("result").GetProperty("differences");

            Assert.All(differences.EnumerateArray(), difference =>
            {
                var path = difference.GetProperty("path").GetString();
                Assert.NotEqual("id", path);
                Assert.NotEqual("name", path);
            });
        }

        // Pins the drop at exactly two. 133 vs 116 produces 65 differences
        // against the CoA 3.3.5a (12340) reference data -- measured, not
        // inferred. If a future change reintroduces a root field outside the
        // section gates, this fails rather than quietly inflating every diff
        // again.
        [SkippableFact]
        public async Task CompareSpells_TotalDifferences_ExcludesTheTwoIdentityPaths()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 116);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(65, result.GetProperty("totalDifferences").GetInt32());
            Assert.Equal(65, result.GetProperty("differences").GetArrayLength());
        }

        [SkippableFact]
        public async Task CompareSpells_CapsDifferenceCount()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 116);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("result").GetProperty("differences").GetArrayLength()
                <= ToolResponse.MaxRows);
        }

        [SkippableFact]
        public async Task GetSpell_HonoursLevel()
        {
            _fixture.SkipIfNoClientData();

            // A spell whose effect 0 base differs between level 10 and 90 under CalcValue.
            double Calc(SpellWork.DBC.Structures.SpellEntry e, int level) => SpellWork.Spell.SpellScalingCalculator.Value(
                e.EffectBasePoints[0], e.EffectDieSides[0], e.EffectRealPointsPerLevel[0], e.EffectPointsPerCombo[0],
                e.SpellLevel, e.BaseLevel, e.MaxLevel, level).Base;
            var spell = SpellWork.DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Count > 0 && Calc(s.Spell, 10) != Calc(s.Spell, 90));

            async Task<double> BaseAt(uint level)
            {
                var json = await _fixture.SpellTools.GetSpell(spell.ID, level: level);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("effects")[0].GetProperty("basePoints").GetDouble();
            }

            Assert.Equal(Calc(spell.Spell, 10), await BaseAt(10));
            Assert.Equal(Calc(spell.Spell, 90), await BaseAt(90));
        }

        [SkippableFact]
        public async Task GetSpell_ClampsLevelAbove90()
        {
            _fixture.SkipIfNoClientData();

            async Task<string> EffectsAt(uint level)
            {
                var json = await _fixture.SpellTools.GetSpell(133, level: level);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("effects").ToString();
            }

            Assert.Equal(await EffectsAt(90), await EffectsAt(150));
        }
    
        // ---------------------------------------------------------------
        // difficulty (spec D4): optional on every tool that takes it. null
        // keeps the id; 0-3 resolves through SpellDifficulty.dbc; anything
        // else is unknown_difficulty naming 0-3.
        // ---------------------------------------------------------------

        private static (int Mode0, int Mode1) DifficultyPair()
        {
            var entry = SpellWork.DBC.DBC.SpellDifficulty.Values.First(e =>
                e.DifficultySpellID[0] != 0 && e.DifficultySpellID[1] != 0 &&
                e.DifficultySpellID[0] != e.DifficultySpellID[1] &&
                SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(e.DifficultySpellID[0]) &&
                SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(e.DifficultySpellID[1]));
            return (entry.DifficultySpellID[0], entry.DifficultySpellID[1]);
        }

        [SkippableFact]
        public async Task GetSpell_OmittedDifficulty_KeepsTheIdAndReportsVariants()
        {
            _fixture.SkipIfNoClientData();

            var (mode0, mode1) = DifficultyPair();
            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(mode1));
            var result = doc.RootElement.GetProperty("result");
            var difficulty = result.GetProperty("difficulty");

            Assert.Equal(mode1, result.GetProperty("id").GetInt32());
            Assert.Equal(JsonValueKind.Null, difficulty.GetProperty("requested").ValueKind);
            Assert.Equal(mode1, difficulty.GetProperty("resolvedSpellId").GetInt32());
            Assert.Contains(mode0, difficulty.GetProperty("variants").EnumerateArray().Select(v => v.GetInt32()));
        }

        [SkippableFact]
        public async Task GetSpell_ExplicitDifficulty_RendersTheResolvedVariant()
        {
            _fixture.SkipIfNoClientData();

            var (mode0, mode1) = DifficultyPair();
            foreach (var format in new[] { "json", "text" })
            {
                using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(mode0, format: format, difficulty: 1));
                var result = doc.RootElement.GetProperty("result");
                Assert.Equal(mode1, result.GetProperty("id").GetInt32());
                if (format == "json")
                {
                    var difficulty = result.GetProperty("difficulty");
                    Assert.Equal(1, difficulty.GetProperty("requested").GetInt32());
                    Assert.Equal(mode1, difficulty.GetProperty("resolvedSpellId").GetInt32());
                }
                else
                {
                    Assert.Equal(mode0, result.GetProperty("requestedId").GetInt32());
                    Assert.Contains("ID - " + mode1, result.GetProperty("text").GetString());
                }
            }

            // Mode 0 maps the 25N id back to the 10N one.
            using var back = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(mode1, difficulty: 0));
            Assert.Equal(mode0, back.RootElement.GetProperty("result").GetProperty("id").GetInt32());
        }

        [SkippableFact]
        public async Task GetSpell_HeroicModeWithoutAVariant_FallsBackLikeTheServer()
        {
            _fixture.SkipIfNoClientData();

            var entry = SpellWork.DBC.DBC.SpellDifficulty.Values.FirstOrDefault(e =>
                e.DifficultySpellID[0] != 0 && e.DifficultySpellID[2] == 0 &&
                SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(e.DifficultySpellID[0]));
            Skip.If(entry == null, "No SpellDifficulty row without a 10H variant.");

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(entry.DifficultySpellID[0], difficulty: 2));
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(entry.DifficultySpellID[0], result.GetProperty("id").GetInt32());
            Assert.Equal(2, result.GetProperty("difficulty").GetProperty("requested").GetInt32());
        }

        [SkippableTheory]
        [InlineData(4u)]
        [InlineData(99u)]
        public async Task GetSpell_DifficultyOutsideZeroToThree_NamesTheValidRange(uint difficulty)
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(133, difficulty: difficulty));
            var error = doc.RootElement.GetProperty("error");
            Assert.Equal("unknown_difficulty", error.GetProperty("code").GetString());
            Assert.Contains("0-3", error.GetProperty("message").GetString());
            Assert.Equal(new[] { 0, 1, 2, 3 },
                error.GetProperty("details").GetProperty("valid").EnumerateArray().Select(v => v.GetInt32()).ToArray());
        }

        [SkippableFact]
        public async Task GetSpellTriggers_Difficulty_IsOptionalAndResolves()
        {
            _fixture.SkipIfNoClientData();

            var (mode0, mode1) = DifficultyPair();
            using (var plain = JsonDocument.Parse(await _fixture.SpellTools.GetSpellTriggers(mode0)))
            {
                var result = plain.RootElement.GetProperty("result");
                Assert.Equal(mode0, result.GetProperty("id").GetInt32());
                Assert.Equal(mode0, result.GetProperty("resolvedSpellId").GetInt32());
            }

            using (var resolved = JsonDocument.Parse(await _fixture.SpellTools.GetSpellTriggers(mode0, difficulty: 1)))
            {
                var result = resolved.RootElement.GetProperty("result");
                Assert.Equal(mode0, result.GetProperty("id").GetInt32());
                Assert.Equal(mode1, result.GetProperty("resolvedSpellId").GetInt32());
            }

            using var bad = JsonDocument.Parse(await _fixture.SpellTools.GetSpellTriggers(mode0, difficulty: 4));
            Assert.Equal("unknown_difficulty", bad.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task CompareSpells_Difficulty_ResolvesBothIds()
        {
            _fixture.SkipIfNoClientData();

            var (mode0, mode1) = DifficultyPair();
            // The 10N id against itself at mode 1 is the 25N variant against itself.
            using var doc = JsonDocument.Parse(await _fixture.SpellTools.CompareSpells(mode0, mode0, difficulty: 1));
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(mode1, result.GetProperty("a").GetProperty("id").GetInt32());
            Assert.Equal(mode1, result.GetProperty("b").GetProperty("id").GetInt32());
            Assert.Equal(mode0, result.GetProperty("a").GetProperty("requestedId").GetInt32());
            Assert.True(result.GetProperty("identical").GetBoolean());

            using var bad = JsonDocument.Parse(await _fixture.SpellTools.CompareSpells(mode0, mode1, difficulty: 7));
            Assert.Equal("unknown_difficulty", bad.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        // ---------------------------------------------------------------
        // items and reagent names come from the world database when it is
        // configured; without it, items is [] plus itemsNote.
        // ---------------------------------------------------------------

        [SkippableFact]
        public async Task GetSpell_WithoutWorldDatabase_ItemsIsEmptyWithANote()
        {
            _fixture.SkipIfNoClientData();
            Skip.If(_fixture.LoadedService.Config.MySqlConfigured, "The loaded service has a world database.");

            using var doc = JsonDocument.Parse(await _fixture.SpellTools.GetSpell(3561, sections: new[] { "items", "reagents" }));
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(0, result.GetProperty("items").GetArrayLength());
            Assert.Equal(SpellWork.Spell.SpellInfoJsonSerializer.ItemsWithoutWorldDatabaseNote, result.GetProperty("itemsNote").GetString());
            Assert.False(result.GetProperty("reagents")[0].TryGetProperty("name", out _));
        }

        private static McpServerConfig TestWorldDatabaseConfig()
        {
            var host = System.Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_HOST");
            Skip.If(string.IsNullOrWhiteSpace(host), "SPELLWORK_TEST_MYSQL_HOST not set.");

            return McpServerConfig.FromEnvironment(new System.Collections.Generic.Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = System.Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH"),
                ["SPELLWORK_MYSQL_HOST"] = host,
                ["SPELLWORK_MYSQL_PORT"] = System.Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PORT") ?? "3306",
                ["SPELLWORK_MYSQL_USER"] = System.Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_USER") ?? "",
                ["SPELLWORK_MYSQL_PASS"] = System.Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PASS") ?? "",
                ["SPELLWORK_MYSQL_WORLD_DB"] = System.Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world",
            });
        }

        // A configured but unreachable world database does not fail
        // get_spell: the client-data document is still returned, with the
        // error next to it and an itemsNote that does not claim "not configured".
        [SkippableFact]
        public async Task GetSpell_UnreachableWorldDatabase_StillAnswersAndSaysWhy()
        {
            _fixture.SkipIfNoClientData();

            var config = McpServerConfig.FromEnvironment(new System.Collections.Generic.Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = System.Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH"),
                ["SPELLWORK_MYSQL_HOST"] = "127.0.0.1",
                ["SPELLWORK_MYSQL_PORT"] = "1",
                ["SPELLWORK_MYSQL_USER"] = "nobody",
                ["SPELLWORK_MYSQL_WORLD_DB"] = "nothing",
            });
            var tools = new SpellWork.Mcp.Tools.SpellTools(_fixture.LoadedService.WithConfig(config));

            using var doc = JsonDocument.Parse(await tools.GetSpell(3561, sections: new[] { "items", "reagents" }));
            var result = doc.RootElement.GetProperty("result");
            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(3561, result.GetProperty("id").GetInt32());
            Assert.Equal(0, result.GetProperty("items").GetArrayLength());
            Assert.False(string.IsNullOrEmpty(result.GetProperty("worldDatabaseError").GetString()));
            Assert.NotEqual(SpellWork.Spell.SpellInfoJsonSerializer.ItemsWithoutWorldDatabaseNote,
                result.GetProperty("itemsNote").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_WithWorldDatabase_ListsItemsAndReagentNames()
        {
            _fixture.SkipIfNoClientData();
            var config = TestWorldDatabaseConfig();
            // Same loaded client data, different config: a second load would
            // race every other test over DBC.DBC's statics.
            var tools = new SpellWork.Mcp.Tools.SpellTools(_fixture.LoadedService.WithConfig(config));

            var database = new SpellWork.Database.WorldDatabase(config.ToSpellWorkConfig());
            var used = database.SelectRows(
                "SELECT `entry`, `spellid_1` FROM `item_template` WHERE `spellid_1` > 0 ORDER BY `entry`", 200);
            var spellId = used.Rows.Select(r => System.Convert.ToInt32(r["spellid_1"]))
                .First(id => SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(id));

            using (var doc = JsonDocument.Parse(await tools.GetSpell(spellId, sections: new[] { "items" })))
            {
                var result = doc.RootElement.GetProperty("result");
                Assert.False(result.TryGetProperty("itemsNote", out _));
                var items = result.GetProperty("items").EnumerateArray().ToList();
                Assert.NotEmpty(items);
                Assert.All(items, item => Assert.False(string.IsNullOrEmpty(item.GetProperty("name").GetString())));
            }

            // 3561 Teleport: Stormwind takes a Rune of Teleportation.
            using (var doc = JsonDocument.Parse(await tools.GetSpell(3561, sections: new[] { "reagents" })))
            {
                var first = doc.RootElement.GetProperty("result").GetProperty("reagents")[0];
                Assert.False(string.IsNullOrEmpty(first.GetProperty("name").GetString()));
            }
        }
    }
}
