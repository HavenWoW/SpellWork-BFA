using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // No client data needed: these read SpellEnums.cs through reflection, so
    // they are plain Facts and must pass on a machine with no DBC path set.
    public class DecoderToolsTests : IClassFixture<McpClientDataFixture>
    {
        private readonly DecoderTools _tools = new DecoderTools(
            new SpellDataService(McpServerConfig.FromEnvironment(
                new System.Collections.Generic.Dictionary<string, string>()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SpellDataService>.Instance));

        private readonly McpClientDataFixture _fixture;

        public DecoderToolsTests(McpClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task CalculateSpellScaling_ProducesDifferentValuesPerLevel()
        {
            _fixture.SkipIfNoClientData();

            var atTwenty = await _fixture.DecoderTools.CalculateSpellScaling(606, level: 20, itemLevel: 475);
            var atOneTwenty = await _fixture.DecoderTools.CalculateSpellScaling(606, level: 120, itemLevel: 475);

            Assert.NotEqual(FirstBasePoints(atTwenty), FirstBasePoints(atOneTwenty));

            static double FirstBasePoints(string json)
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("effects")[0]
                    .GetProperty("basePoints").GetDouble();
            }
        }

        // The numbers this tool reports and the numbers get_spell reports must
        // be the same numbers. They come from one arithmetic path
        // (CalculateEffectValues) precisely so they cannot diverge; this
        // asserts that they actually do not.
        [SkippableFact]
        public async Task CalculateSpellScaling_AgreesWithGetSpell()
        {
            _fixture.SkipIfNoClientData();

            var scaling = await _fixture.DecoderTools.CalculateSpellScaling(133, level: 60, itemLevel: 475);
            var spell = await _fixture.SpellTools.GetSpell(133, level: 60, itemLevel: 475);

            using var scalingDoc = JsonDocument.Parse(scaling);
            using var spellDoc = JsonDocument.Parse(spell);

            var fromScaling = scalingDoc.RootElement.GetProperty("result").GetProperty("effects")[0];
            var fromSpell = spellDoc.RootElement.GetProperty("result").GetProperty("effects")[0];

            // Every value field, not just basePoints: the two tools emit
            // through separately-written code and the only thing stopping
            // them drifting is that both suppress on the same condition.
            // Assert that, or the drift is invisible until someone reports it.
            foreach (var field in new[]
            {
                "basePoints", "basePointsMin", "basePointsMax", "valuePerResource",
                "perLevelValue", "spellPowerCoefficient", "attackPowerCoefficient", "usesExpectedStat",
            })
            {
                var inScaling = fromScaling.TryGetProperty(field, out var scalingValue);
                var inSpell = fromSpell.TryGetProperty(field, out var spellValue);

                Assert.True(inScaling == inSpell,
                    $"'{field}' is present in one tool's output and absent from the other's.");
                if (inScaling)
                    Assert.Equal(spellValue.ToString(), scalingValue.ToString());
            }
        }

        // Spell 133 (Fireball) effect 0 at level 60 / itemLevel 475 only
        // ever populates 3 of the 8 fields the loop above walks: basePoints,
        // spellPowerCoefficient and usesExpectedStat (its Coefficient is 0,
        // its Variance is 0, and it has no resource/per-level/attack-power
        // terms). A field absent from both outputs makes
        // "inScaling == inSpell" pass vacuously, so a test built on 133
        // alone asserts real agreement on fewer than half the fields it
        // claims to cover.
        //
        // Spell 67793 (Claw, a Druid cat-form ability) was chosen by
        // scanning every spell's first effect at this level/itemLevel for
        // the combination that exercises the most fields at once: it has a
        // non-zero Variance (basePointsMin/basePointsMax), a non-zero
        // EffectBonusCoefficient and BonusCoefficientFromAP
        // (spellPowerCoefficient/attackPowerCoefficient), and its base
        // value scales through ExpectedStat (usesExpectedStat) -- 6 of the
        // 8 fields, all confirmed equal between calculate_spell_scaling and
        // get_spell. Combined with 133 above, every field except
        // valuePerResource and perLevelValue is exercised by a real,
        // non-vacuous equality assertion; those two are mutually exclusive
        // with usesExpectedStat in CalculateEffectValues (SpellInfo.cs) --
        // see the task report for a spell that does drive them, left out of
        // this suite to keep the case count small once the vacuous-pass gap
        // was closed to 6 of 8.
        [SkippableFact]
        public async Task CalculateSpellScaling_AgreesWithGetSpell_ClawCoversVarianceAndAttackPower()
        {
            _fixture.SkipIfNoClientData();

            var scaling = await _fixture.DecoderTools.CalculateSpellScaling(67793, level: 60, itemLevel: 475);
            var spell = await _fixture.SpellTools.GetSpell(67793, level: 60, itemLevel: 475);

            using var scalingDoc = JsonDocument.Parse(scaling);
            using var spellDoc = JsonDocument.Parse(spell);

            var fromScaling = scalingDoc.RootElement.GetProperty("result").GetProperty("effects")[0];
            var fromSpell = spellDoc.RootElement.GetProperty("result").GetProperty("effects")[0];

            var fieldsPresent = 0;
            foreach (var field in new[]
            {
                "basePoints", "basePointsMin", "basePointsMax", "valuePerResource",
                "perLevelValue", "spellPowerCoefficient", "attackPowerCoefficient", "usesExpectedStat",
            })
            {
                var inScaling = fromScaling.TryGetProperty(field, out var scalingValue);
                var inSpell = fromSpell.TryGetProperty(field, out var spellValue);

                Assert.True(inScaling == inSpell,
                    $"'{field}' is present in one tool's output and absent from the other's.");
                if (inScaling)
                {
                    fieldsPresent++;
                    Assert.Equal(spellValue.ToString(), scalingValue.ToString());
                }
            }

            // Pins the "6 of 8" claim above: if a future data update makes
            // Claw stop exercising one of these fields, this fails loudly
            // instead of silently going back to a weaker, partially-vacuous
            // assertion.
            Assert.Equal(6, fieldsPresent);
            Assert.True(fromScaling.GetProperty("usesExpectedStat").GetBoolean());
            Assert.True(fromScaling.TryGetProperty("basePointsMin", out _));
            Assert.True(fromScaling.TryGetProperty("attackPowerCoefficient", out _));
        }

        // Promoted from Minor on review: valuePerResource and perLevelValue
        // had zero equality coverage anywhere in this suite -- 133 and
        // 67793 both route through the ExpectedStat/UsesExpectedStat branch
        // of CalculateEffectValues, which never touches those two fields
        // (see the mutual-exclusivity note above). Spell 256375 (Firebomb)
        // takes the other branch: Coefficient is ~0 and ExpectedStat is
        // None, so its first effect drives EffectPointsPerResource and
        // EffectRealPointsPerLevel directly. Found the same way as 67793,
        // by scanning every spell's first effect at this level/itemLevel
        // and confirmed to agree between calculate_spell_scaling and
        // get_spell before being written in here. Without this case, a
        // regression that reintroduced the 1.0E-5f epsilon in place of the
        // exact-zero suppression on valuePerResource/perLevelValue (see the
        // comment at DecoderTools.cs's ValuePerResource/PerLevelValue
        // checks -- precisely the drift class this task exists to prevent)
        // would go undetected by the whole suite.
        [SkippableFact]
        public async Task CalculateSpellScaling_AgreesWithGetSpell_FirebombCoversResourceAndLevelScaling()
        {
            _fixture.SkipIfNoClientData();

            var scaling = await _fixture.DecoderTools.CalculateSpellScaling(256375, level: 60, itemLevel: 475);
            var spell = await _fixture.SpellTools.GetSpell(256375, level: 60, itemLevel: 475);

            using var scalingDoc = JsonDocument.Parse(scaling);
            using var spellDoc = JsonDocument.Parse(spell);

            var fromScaling = scalingDoc.RootElement.GetProperty("result").GetProperty("effects")[0];
            var fromSpell = spellDoc.RootElement.GetProperty("result").GetProperty("effects")[0];

            var fieldsPresent = 0;
            foreach (var field in new[]
            {
                "basePoints", "basePointsMin", "basePointsMax", "valuePerResource",
                "perLevelValue", "spellPowerCoefficient", "attackPowerCoefficient", "usesExpectedStat",
            })
            {
                var inScaling = fromScaling.TryGetProperty(field, out var scalingValue);
                var inSpell = fromSpell.TryGetProperty(field, out var spellValue);

                Assert.True(inScaling == inSpell,
                    $"'{field}' is present in one tool's output and absent from the other's.");
                if (inScaling)
                {
                    fieldsPresent++;
                    Assert.Equal(spellValue.ToString(), scalingValue.ToString());
                }
            }

            // Pins the "5 of 8" claim above the same way the Claw case pins
            // its own count.
            Assert.Equal(5, fieldsPresent);
            Assert.True(fromScaling.TryGetProperty("valuePerResource", out _));
            Assert.True(fromScaling.TryGetProperty("perLevelValue", out _));
        }

        // Strengthened per review: the original version of this test looked
        // a row up by key and asserted the returned row's own id equalled
        // that key -- true of any Storage<T>.TryGetValue call, not a
        // property of ResolveMapDifficulty's logic. This instead looks the
        // same row up independently (TryGetValue, not through the tool) and
        // asserts every field the tool copies out of it -- mapId,
        // difficultyId, contentTuningId -- actually matches that row, which
        // a "return some other row" or "return a blank record" regression
        // would not.
        [SkippableFact]
        public async Task CalculateSpellScaling_ReportsTheResolvedMapDifficulty()
        {
            _fixture.SkipIfNoClientData();

            var difficultyId = SpellWork.DBC.DBC.MapDifficulty.Keys.First();
            Assert.True(SpellWork.DBC.DBC.MapDifficulty.TryGetValue(difficultyId, out var expected));

            var json = await _fixture.DecoderTools.CalculateSpellScaling(
                133, level: 120, itemLevel: 475, difficulty: (uint)difficultyId);

            using var doc = JsonDocument.Parse(json);
            var mapDifficulty = doc.RootElement.GetProperty("result").GetProperty("mapDifficulty");

            Assert.Equal(expected.ID, mapDifficulty.GetProperty("id").GetUInt32());
            Assert.Equal(expected.MapID, mapDifficulty.GetProperty("mapId").GetUInt32());
            Assert.Equal(expected.DifficultyID, mapDifficulty.GetProperty("difficultyId").GetInt32());
            Assert.Equal(expected.ContentTuningID, mapDifficulty.GetProperty("contentTuningId").GetInt32());
        }

        // Missing contract case: a difficulty value present in no row must
        // yield mapDifficulty: null, never a silent fallback to some other
        // row. DecoderTools.cs implements this correctly today (TryGetValue
        // leaves byId unset), but nothing pinned it -- a regression that
        // fell back to, say, MapDifficulty.Values.First() on a miss would
        // have passed every other test in this class.
        [SkippableFact]
        public async Task CalculateSpellScaling_UnknownDifficulty_ReportsNullMapDifficulty()
        {
            _fixture.SkipIfNoClientData();

            var missingDifficulty = (uint)(SpellWork.DBC.DBC.MapDifficulty.Keys.Max() + 1);
            var json = await _fixture.DecoderTools.CalculateSpellScaling(
                133, level: 60, itemLevel: 475, difficulty: missingDifficulty);

            using var doc = JsonDocument.Parse(json);
            var mapDifficulty = doc.RootElement.GetProperty("result").GetProperty("mapDifficulty");

            Assert.Equal(JsonValueKind.Null, mapDifficulty.ValueKind);
        }

        // ResolveMapDifficulty's mapId.HasValue branch (DecoderTools.cs) --
        // the "when both are given, match both; with mapId alone, any row
        // for that map matches" behaviour the brief's design note exists to
        // specify -- was previously never exercised: no test in this class
        // passed mapId at all. Map 34 has exactly one MapDifficultyEntry
        // row (id=8, DifficultyID=1), so FirstOrDefault's result is
        // deterministic here, and DifficultyID=1 (not 0) means the
        // "difficulty == 0" short-circuit for an unspecified difficulty is
        // actually load-bearing: dropping it would require DifficultyID ==
        // 0 for a mapId-only call and this row would stop matching.
        [SkippableFact]
        public async Task CalculateSpellScaling_MapIdAlone_ResolvesTheOnlyRowForThatMap()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.DecoderTools.CalculateSpellScaling(133, level: 60, itemLevel: 475, mapId: 34);
            using var doc = JsonDocument.Parse(json);
            var mapDifficulty = doc.RootElement.GetProperty("result").GetProperty("mapDifficulty");

            Assert.Equal(8u, mapDifficulty.GetProperty("id").GetUInt32());
            Assert.Equal(34u, mapDifficulty.GetProperty("mapId").GetUInt32());
            Assert.Equal(1, mapDifficulty.GetProperty("difficultyId").GetInt32());
        }

        // Map 33 carries three MapDifficultyEntry rows sharing one MapID
        // with distinct DifficultyIDs (1, 2, 19) -- the "match both" shape
        // the brief's design note describes. Row id=839 is DifficultyID=2;
        // none of the three rows' own primary-key id is 33, so a mutation
        // that matched on entry.ID instead of entry.MapID cannot pick the
        // right row by coincidence here.
        [SkippableFact]
        public async Task CalculateSpellScaling_MapIdAndDifficulty_ResolvesTheMatchingRow()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.DecoderTools.CalculateSpellScaling(
                133, level: 60, itemLevel: 475, mapId: 33, difficulty: 2);
            using var doc = JsonDocument.Parse(json);
            var mapDifficulty = doc.RootElement.GetProperty("result").GetProperty("mapDifficulty");

            Assert.Equal(839u, mapDifficulty.GetProperty("id").GetUInt32());
            Assert.Equal(33u, mapDifficulty.GetProperty("mapId").GetUInt32());
            Assert.Equal(2, mapDifficulty.GetProperty("difficultyId").GetInt32());
        }

        [SkippableFact]
        public async Task CalculateSpellScaling_UnknownId_ReturnsNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.DecoderTools.CalculateSpellScaling(999999999, level: 60, itemLevel: 475);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("spell_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [Fact]
        public async Task DecodeFlags_SchoolMask_SplitsEveryBit()
        {
            // SPELL_SCHOOL_MASK_NORMAL (1) | SPELL_SCHOOL_MASK_FIRE (4) = 5
            var json = await _tools.DecodeFlags("SpellSchoolMask", 5);
            using var doc = JsonDocument.Parse(json);
            var flags = doc.RootElement.GetProperty("result").GetProperty("flags");

            Assert.Equal(2, flags.GetArrayLength());
            Assert.Contains(flags.EnumerateArray(), f => f.GetProperty("value").GetInt64() == 1);
            Assert.Contains(flags.EnumerateArray(), f => f.GetProperty("value").GetInt64() == 4);
            foreach (var flag in flags.EnumerateArray())
                Assert.False(string.IsNullOrEmpty(flag.GetProperty("name").GetString()));
        }

        // The failure mode this tool exists to avoid: a combined value must
        // never come back as a bare decimal from a ToString() fallback.
        [Fact]
        public async Task DecodeFlags_NeverReturnsABareNumberAsAName()
        {
            var json = await _tools.DecodeFlags("SpellSchoolMask", 5);
            using var doc = JsonDocument.Parse(json);

            foreach (var flag in doc.RootElement.GetProperty("result").GetProperty("flags").EnumerateArray())
                Assert.False(long.TryParse(flag.GetProperty("name").GetString(), out _));
        }

        // An undeclared bit is a data or version problem. Reporting it is the
        // whole difference between "this mask means nothing here" and a
        // silently shorter flag list.
        [Fact]
        public async Task DecodeFlags_ReportsUnmatchedBits()
        {
            var json = await _tools.DecodeFlags("SpellSchoolMask", 1u << 30);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(0, result.GetProperty("flags").GetArrayLength());
            Assert.Contains(result.GetProperty("unmatchedBits").EnumerateArray(), b => b.GetInt32() == 30);
        }

        [Fact]
        public async Task DecodeFlags_ZeroValue_ReturnsEmptyFlagsNotAnError()
        {
            var json = await _tools.DecodeFlags("SpellSchoolMask", 0);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(0, doc.RootElement.GetProperty("result").GetProperty("flags").GetArrayLength());
        }

        [Fact]
        public async Task DecodeFlags_UnknownKind_ListsWhatIsAvailable()
        {
            var json = await _tools.DecodeFlags("NotAnEnum", 1);
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("unknown_enum", error.GetProperty("code").GetString());
            Assert.True(error.GetProperty("details").GetProperty("availableKinds").GetArrayLength() > 0);
        }

        [Fact]
        public async Task ListEnum_RoundTripsWithDecodeFlags()
        {
            var listed = await _tools.ListEnum("SpellSchoolMask");
            using var doc = JsonDocument.Parse(listed);

            foreach (var member in doc.RootElement.GetProperty("result").GetProperty("rows").EnumerateArray())
            {
                var value = member.GetProperty("value").GetInt64();
                if (value == 0 || (value & (value - 1)) != 0)
                    continue; // zero members and composite members are not single bits

                var decoded = await _tools.DecodeFlags("SpellSchoolMask", (ulong)value);
                using var decodedDoc = JsonDocument.Parse(decoded);
                var names = decodedDoc.RootElement.GetProperty("result").GetProperty("flags")
                    .EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();

                Assert.Contains(member.GetProperty("name").GetString(), names);
            }
        }

        [Fact]
        public async Task ListEnum_FilterIsCaseInsensitiveSubstring()
        {
            var json = await _tools.ListEnum("SpellEffects", filter: "heal");
            using var doc = JsonDocument.Parse(json);
            var members = doc.RootElement.GetProperty("result").GetProperty("rows");

            Assert.True(members.GetArrayLength() > 0);
            foreach (var member in members.EnumerateArray())
                Assert.Contains("HEAL", member.GetProperty("name").GetString().ToUpperInvariant());
        }

        // The largest enum in the catalogue, discovered rather than named: the
        // paging tests below need one big enough to page through, and
        // hard-coding "AuraType" (495 members today, five under the cap, and
        // growing every patch) would silently stop testing paging the day a
        // bigger one lands or that one shrinks.
        private static string LargestEnumName()
        {
            string largest = null;
            var most = 0;
            foreach (var name in EnumCatalog.Names)
            {
                Assert.True(EnumCatalog.TryResolve(name, out var type));
                var count = System.Enum.GetNames(type).Length;
                if (count <= most)
                    continue;

                most = count;
                largest = name;
            }

            return largest;
        }

        private static int MemberCount(string enumName)
        {
            Assert.True(EnumCatalog.TryResolve(enumName, out var type));
            return System.Enum.GetNames(type).Length;
        }

        // The divergence this test exists to prevent: list_enum used to set
        // "count" to the number of members that *matched* while every other
        // tool on this server sets it to the number of rows *in this
        // response*. An agent that learned count == rows.Length from
        // search_spells read "count: 495" from AuraType and believed it had
        // received 495 members when it had received far fewer. Asserting
        // count == rows.Length *and* totalMatches == the full size pins both
        // halves; the old shape fails the first.
        [Fact]
        public async Task ListEnum_CountIsRowsReturned_AndTotalMatchesIsTheFullSize()
        {
            var enumName = LargestEnumName();
            var json = await _tools.ListEnum(enumName, limit: 10);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(10, result.GetProperty("rows").GetArrayLength());
            Assert.Equal(10, result.GetProperty("count").GetInt32());
            Assert.Equal(MemberCount(enumName), result.GetProperty("totalMatches").GetInt32());
            Assert.Equal(10, result.GetProperty("limit").GetInt32());
            Assert.Equal(0, result.GetProperty("offset").GetInt32());
            Assert.True(result.GetProperty("truncated").GetBoolean());
        }

        // Without offset, a truncated enum's withheld members were simply
        // unreachable -- exactly the failure this branch reasoned about and
        // deliberately avoided for list_db2. Paging must actually advance,
        // not re-serve the first page.
        [Fact]
        public async Task ListEnum_OffsetReturnsDifferentMembers()
        {
            var enumName = LargestEnumName();

            var firstJson = await _tools.ListEnum(enumName, limit: 10, offset: 0);
            var secondJson = await _tools.ListEnum(enumName, limit: 10, offset: 10);

            using var firstDoc = JsonDocument.Parse(firstJson);
            using var secondDoc = JsonDocument.Parse(secondJson);

            var first = Names(firstDoc);
            var second = Names(secondDoc);

            Assert.Equal(10, first.Count);
            Assert.Equal(10, second.Count);
            Assert.Empty(first.Intersect(second));
            Assert.Equal(10, secondDoc.RootElement.GetProperty("result").GetProperty("offset").GetInt32());

            static System.Collections.Generic.List<string> Names(JsonDocument doc) =>
                doc.RootElement.GetProperty("result").GetProperty("rows").EnumerateArray()
                    .Select(m => m.GetProperty("name").GetString()).ToList();
        }

        // The last page must not claim more is coming, and it must be
        // reachable: paging to the final offset returns the tail and reports
        // truncated == false, which is what tells an agent it can stop.
        [Fact]
        public async Task ListEnum_FinalPage_IsReachableAndNotTruncated()
        {
            var enumName = LargestEnumName();
            var total = MemberCount(enumName);

            var json = await _tools.ListEnum(enumName, limit: 10, offset: total - 3);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(3, result.GetProperty("count").GetInt32());
            Assert.Equal(3, result.GetProperty("rows").GetArrayLength());
            Assert.Equal(total, result.GetProperty("totalMatches").GetInt32());
            Assert.False(result.GetProperty("truncated").GetBoolean());
        }

        // list_enum's limit defaults to MaxRows, so a caller that passes
        // neither paging parameter gets exactly what this tool returned before
        // it was routed through Paged -- every member of every enum in the
        // catalogue, since none is above the cap today.
        [SkippableFact]
        public async Task ListEnum_DefaultLimit_StillReturnsEveryMember()
        {
            var enumName = LargestEnumName();
            var total = MemberCount(enumName);
            Skip.If(total > ToolResponse.MaxRows,
                $"{enumName} has grown past MaxRows; the default page is no longer the whole enum.");

            var json = await _tools.ListEnum(enumName);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(total, result.GetProperty("count").GetInt32());
            Assert.Equal(total, result.GetProperty("totalMatches").GetInt32());
            Assert.False(result.GetProperty("truncated").GetBoolean());
        }

        // The same cross-tool agreement DatabaseTools.EffectiveLimit_AgreesWithPaged
        // asserts for query_world_db, asserted end-to-end through list_enum's
        // own response: the echoed "limit" is the effective applied ceiling for
        // every input, including the non-positive ones that yield zero rows,
        // and never the raw value the caller asked for.
        [Fact]
        public async Task ListEnum_EffectiveLimit_AgreesWithPaged()
        {
            var enumName = LargestEnumName();

            foreach (var requested in new[]
                { -5, -1, 0, 1, 100, ToolResponse.MaxRows, ToolResponse.MaxRows + 1, 100000 })
            {
                var expected = (int)ToolResponse.Paged(
                    new System.Collections.Generic.List<object>(), 0, 0, requested)["limit"];

                var json = await _tools.ListEnum(enumName, limit: requested);
                using var doc = JsonDocument.Parse(json);
                var result = doc.RootElement.GetProperty("result");

                Assert.Equal(expected, result.GetProperty("limit").GetInt32());
                Assert.Equal(DatabaseTools.EffectiveLimit(requested),
                    result.GetProperty("limit").GetInt32());
                Assert.True(result.GetProperty("count").GetInt32() <= expected,
                    $"limit {requested} returned more rows than its own effective ceiling.");
                Assert.Equal(result.GetProperty("count").GetInt32(),
                    result.GetProperty("rows").GetArrayLength());
            }
        }

        // A non-positive limit yields zero rows -- never one, never unbounded
        // -- and says so rather than reporting the raw value back.
        [Fact]
        public async Task ListEnum_ZeroLimit_ReturnsNoRowsAndSaysItTruncated()
        {
            var enumName = LargestEnumName();
            var json = await _tools.ListEnum(enumName, limit: 0);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(0, result.GetProperty("count").GetInt32());
            Assert.Equal(0, result.GetProperty("rows").GetArrayLength());
            Assert.Equal(0, result.GetProperty("limit").GetInt32());
            Assert.True(result.GetProperty("truncated").GetBoolean());
            Assert.Equal(MemberCount(enumName), result.GetProperty("totalMatches").GetInt32());
        }

        // isFlags and name describe the enum, not the page of members this
        // call happens to return, so they stay outside the page envelope --
        // and must survive the move into it.
        [Fact]
        public async Task ListEnum_KeepsEnumSpecificFieldsAlongsideThePage()
        {
            var json = await _tools.ListEnum("SpellSchoolMask", limit: 2);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal("SpellSchoolMask", result.GetProperty("name").GetString());
            Assert.True(result.GetProperty("isFlags").GetBoolean());
            Assert.Equal(2, result.GetProperty("count").GetInt32());
        }

        [Fact]
        public async Task ListEnum_UnknownName_ListsWhatIsAvailable()
        {
            var json = await _tools.ListEnum("NotAnEnum");
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("unknown_enum", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [Fact]
        public void Catalog_ContainsTheEnumsTheSpecNames()
        {
            foreach (var name in new[] { "SpellSchoolMask", "SpellEffects", "AuraType", "Targets", "SpellFamilyNames" })
                Assert.True(EnumCatalog.TryResolve(name, out _), $"{name} is missing from the catalogue.");
        }

        // Spec section 10 asks for coverage of *every* decode_flags kind, not
        // a sample. The catalogue is the kind list, so drive the test from it:
        // an enum added to SpellEnums.cs is covered the day it lands, and one
        // that decodes into bare numbers fails here rather than in use.
        public static TheoryData<string> EveryKind()
        {
            var data = new TheoryData<string>();
            foreach (var name in EnumCatalog.Names)
                data.Add(name);
            return data;
        }

        // SpellEnums.cs declares one ulong-backed enum (ShapeshiftFormMask),
        // and one of its members (FORM_ALL) is the all-bits-set combination
        // 0xFFFFFFFFFFFFFFFF -- above long.MaxValue. System.Convert.ToInt64
        // throws OverflowException converting that member, regardless of
        // which bit the test is actually driving at, because Enum.GetNames
        // walks every declared member. Read through Convert.ToUInt64 for a
        // ulong-backed enum instead, mirroring the fix inside DecoderTools
        // itself (EnumCatalog.ByName exposes the Type needed to tell which
        // enums those are).
        private static ulong ToUInt64Value(System.Type enumType, object boxedMember)
        {
            return System.Enum.GetUnderlyingType(enumType) == typeof(ulong)
                ? System.Convert.ToUInt64(boxedMember)
                : unchecked((ulong)System.Convert.ToInt64(boxedMember));
        }

        [Theory]
        [MemberData(nameof(EveryKind))]
        public async Task DecodeFlags_EveryKind_DecodesItsOwnDeclaredBits(string kind)
        {
            Assert.True(EnumCatalog.TryResolve(kind, out var enumType));

            foreach (var member in System.Enum.GetNames(enumType))
            {
                var value = ToUInt64Value(enumType, System.Enum.Parse(enumType, member));

                // Only single-bit members round-trip through a per-bit
                // decoder. Zero members and composite members (masks that
                // combine other members) are excluded by construction, not
                // skipped for convenience.
                if (value == 0 || (value & (value - 1)) != 0)
                    continue;

                var json = await _tools.DecodeFlags(kind, value);
                using var doc = JsonDocument.Parse(json);
                var result = doc.RootElement.GetProperty("result");

                Assert.Equal(0, result.GetProperty("unmatchedBits").GetArrayLength());
                var names = result.GetProperty("flags").EnumerateArray()
                    .Select(f => f.GetProperty("name").GetString()).ToList();

                Assert.Single(names);
                // Aliases are legal -- two members may share a value -- so
                // assert the decoded name is *a* member with this value, not
                // that it is this particular spelling.
                Assert.Equal(value,
                    ToUInt64Value(enumType, System.Enum.Parse(enumType, names[0])));
            }
        }

        [Theory]
        [MemberData(nameof(EveryKind))]
        public async Task ListEnum_EveryKind_ReturnsItsMembers(string kind)
        {
            var json = await _tools.ListEnum(kind);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean(), $"list_enum failed for {kind}");
            Assert.Equal(kind, doc.RootElement.GetProperty("result").GetProperty("name").GetString());
        }
    }
}
