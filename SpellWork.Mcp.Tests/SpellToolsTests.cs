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
        // compare identical. 38695 "Vertex Color Red" and 38696 "Vertex Color
        // Purple" are distinct ids with distinct names whose fourteen compared
        // sections are byte-identical -- they are two of 561 spells sharing
        // one body in this client data, so this is a representative case and
        // not a curiosity. Pre-fix this returned identical=False with the two
        // identity differences and nothing else, which is the whole bug in one
        // call.
        [SkippableFact]
        public async Task CompareSpells_DistinctSpellsWithTheSameDefinition_AreIdentical()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(38695, 38696);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("identical").GetBoolean(),
                "Two spells with identical definitions must compare identical even though their ids differ.");
            Assert.Equal(0, result.GetProperty("differences").GetArrayLength());
            Assert.Equal(0, result.GetProperty("totalDifferences").GetInt32());
            Assert.False(result.GetProperty("truncated").GetBoolean());

            // Nothing is lost by dropping the identity paths: which spell is
            // which is still reported, in the blocks that exist to say so.
            Assert.Equal(38695, result.GetProperty("a").GetProperty("id").GetInt32());
            Assert.Equal(38696, result.GetProperty("b").GetProperty("id").GetInt32());
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

        // Pins the drop at exactly two. 133 vs 116 produces 30 differences
        // against the 5.4.8 (18414) reference data (35 against the 8.3.7 data
        // this was first measured on) -- measured, not inferred. If a future change reintroduces a
        // root field outside the section gates, this fails rather than
        // quietly inflating every diff again.
        [SkippableFact]
        public async Task CompareSpells_TotalDifferences_ExcludesTheTwoIdentityPaths()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 116);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            // 32 since Task 5: scaled effects now carry computed basePoints plus a
            // Min/Max range, which the two spells no longer share.
            Assert.Equal(32, result.GetProperty("totalDifferences").GetInt32());
            Assert.Equal(32, result.GetProperty("differences").GetArrayLength());
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

            async Task<double> BaseAt(uint level)
            {
                var json = await _fixture.SpellTools.GetSpell(133, level: level);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("effects")[0].GetProperty("basePoints").GetDouble();
            }

            Assert.NotEqual(await BaseAt(10), await BaseAt(90));
        }
    }
}
