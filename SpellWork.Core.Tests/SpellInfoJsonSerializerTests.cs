using System;
using System.Linq;
using System.Text.Json;
using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class SpellInfoJsonSerializerTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellInfoJsonSerializerTests(ClientDataFixture fixture) => _fixture = fixture;

        // Section-name validation (Serialize's `sections` filter loop,
        // SpellInfoJsonSerializer.cs:80-91) runs before any field is read off
        // `spell` -- SpellInfo's constructor resolves nothing without a load, and
        // every property BuildEffects/BuildMisc/etc. reach is null-conditional
        // on entries this minimal instance never sets. So the validation path
        // needs no DBC load at all; building the smallest possible SpellInfo
        // here (rather than gating on client data) is what lets these tests
        // actually execute -- and catch a regression -- on a machine with no
        // SPELLWORK_DBC_PATH set, unlike a SkippableFact that quietly skips.
        private static SpellInfo MinimalSpell() =>
            new SpellInfo(new SpellEntry { ID = 1, Name = "Test" });

        [SkippableFact]
        public void Serialize_emits_id_name_and_decoded_attributes()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals());
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(133, root.GetProperty("id").GetInt32());
            Assert.Equal("Fireball", root.GetProperty("name").GetString());
            Assert.True(root.TryGetProperty("effects", out var effects));
            Assert.True(effects.GetArrayLength() > 0);
        }

        [SkippableFact]
        public void Serialize_honours_the_sections_filter()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(
                DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals(), new[] { "misc" });

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Both halves are needed. Asserting only that `effects` is gone
            // passes for a serialiser that returns {"id":…,"name":…} and drops
            // everything else for every filter.
            Assert.False(root.TryGetProperty("effects", out _));
            Assert.False(root.TryGetProperty("attributes", out _));
            Assert.True(root.TryGetProperty("misc", out _));
            Assert.Equal(133, root.GetProperty("id").GetInt32());
            Assert.Equal("Fireball", root.GetProperty("name").GetString());
        }

        // Fireball's SCHOOL_DAMAGE effect carries its spell-power scaling in
        // EffectBonusCoefficient (1.5 in 5.4.8). This fails on a serialiser
        // that emits basePoints and nothing else about magnitude.
        [SkippableFact]
        public void Serialize_carries_the_spell_power_coefficient()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals());
            using var document = JsonDocument.Parse(json);

            var effect = document.RootElement.GetProperty("effects")
                .EnumerateArray().Single(e => e.GetProperty("index").GetInt32() == 0);

            Assert.Equal(1.5, effect.GetProperty("spellPowerCoefficient").GetDouble(), 3);
        }

        // Spell 1459 (Arcane Brilliance) effect 0 uses SpellRadius 12, a
        // 100-yard radius. Asserting the
        // resolved values (not just that a "radius" key exists) is what
        // would catch BuildRadius regressing to emitting the bare index.
        [SkippableFact]
        public void Serialize_resolves_the_radius_index_to_its_store_values()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[1459], RenderContext.FromGlobals());
            using var document = JsonDocument.Parse(json);

            var effect = document.RootElement.GetProperty("effects")
                .EnumerateArray().Single(e => e.GetProperty("index").GetInt32() == 0);

            var radius = effect.GetProperty("radius");
            Assert.Equal(12, radius.GetProperty("id").GetInt32());
            Assert.Equal(100.00, radius.GetProperty("radius").GetDouble(), 3);
            Assert.Equal(100.00, radius.GetProperty("maxRadius").GetDouble(), 3);
        }

        // Spell 43093 (Grievous Throw) has SpellEffect rows at difficulties
        // 0, 2 and 4. effects holds one entry per index resolved at the
        // context's difficulty, and difficultyId says which row it came from:
        // 2 when the spell has its own rows there, 0 when it falls back.
        [SkippableFact]
        public void Serialize_reports_the_difficulty_each_effect_row_came_from()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            int[] DifficultyIds(int difficulty)
            {
                var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[43093], new RenderContext(90, 463, difficulty));
                using var document = JsonDocument.Parse(json);
                return document.RootElement.GetProperty("effects").EnumerateArray()
                    .Select(e => e.GetProperty("difficultyId").GetInt32()).ToArray();
            }

            Assert.Equal(new[] { 2, 2 }, DifficultyIds(2));
            Assert.Equal(new[] { 0, 0 }, DifficultyIds(1));
        }

        // Fix round 1, item 4: an unrecognised section name used to return a
        // near-empty document instead of telling the caller it asked for
        // something that does not exist.
        //
        // Task 2 review, fix round 1: this used to assert "items" throws.
        // Task 2 correctly added "items" to ValidSections (the brief requires
        // it -- Task 5 populates it), which made that premise false: on any
        // machine with client data this failed with "No exception was
        // thrown", and the SkippableFact gate hid the failure here. Ungated:
        // validation needs no DBC load (see MinimalSpell), so this now
        // actually runs here instead of skipping.
        //
        // Task 7: the same premise went stale a second time. Fix round 1
        // replaced "items" with "visual" on the reasoning that "visual" was
        // "guaranteed to stay unrecognised" because the visual-chain tooling
        // belonged to a later plan; that plan is this one, and "visual" is now
        // implemented. The lesson is that a reserved-but-unimplemented section
        // name is exactly the wrong thing to assert rejection against -- every
        // one of them is a section someone intends to build. This now uses a
        // name that is not reserved, not planned, and not a word this schema
        // would ever adopt.
        [Fact]
        public void Serialize_throws_on_an_unrecognised_section_name()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => SpellInfoJsonSerializer.Serialize(MinimalSpell(), new RenderContext(1, 1, 0), new[] { "notasection" }));

            Assert.Contains("notasection", ex.Message);
        }

        // Fix round 1, item 4: a mis-cased section name (as a model might
        // send) must reach the same filter as its canonical spelling, not
        // silently fall through to "no section recognised".
        [SkippableFact]
        public void Serialize_accepts_a_mis_cased_section_name()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(
                DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals(), new[] { "Effects" });

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("effects", out var effects));
            Assert.True(effects.GetArrayLength() > 0);

            // The echoed sections list lets a caller tell "filtered out" from
            // "genuinely empty"; it should report the canonical name even
            // though the caller asked using a different case.
            var sections = root.GetProperty("sections").EnumerateArray()
                .Select(s => s.GetString()).ToList();
            Assert.Equal(new[] { "effects" }, sections);
        }

        // Fix round 2, item 1: Enum.GetName only matches a single declared
        // member exactly, so a [Flags] enum's combined value used to fall
        // through to the fallback and stringify the boxed integer --
        // "targetCreatureType": {"raw": 8385, "name": "8385"} -- where the
        // GUI's own ToString() decomposes it. Verified directly against the
        // loaded store (not just taken from the re-review): spell 118 has
        // exactly one TargetRestrictions row, TargetCreatureType == 8385,
        // which is BEAST (1) | HUMANOID (64) | CRITTER (128) | WILD_PET
        // (8192) -- those four names are asserted individually, not just a
        // count, so a test that happened to emit four wrong names would not
        // pass.
        [SkippableFact]
        public void Serialize_decomposes_a_combined_flags_value_into_named_bits()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[118], RenderContext.FromGlobals());
            using var document = JsonDocument.Parse(json);

            var target = document.RootElement.GetProperty("targets")
                .EnumerateArray().Single();
            var creatureType = target.GetProperty("targetCreatureType");

            Assert.Equal(8385, creatureType.GetProperty("raw").GetInt32());

            var names = creatureType.GetProperty("flags").EnumerateArray()
                .Select(f => f.GetProperty("name").GetString()).ToList();

            Assert.Equal(4, names.Count);
            Assert.Contains("BEAST", names);
            Assert.Contains("HUMANOID", names);
            Assert.Contains("CRITTER", names);
            Assert.Contains("WILD_PET", names);

            // A single-bit value must still yield exactly one entry, so the
            // helper cannot regress into always emitting every bit position
            // 0-31 regardless of whether it is set. Spell 133's schoolMask is
            // 4 (SPELL_SCHOOL_MASK_FIRE alone), verified directly against the
            // store.
            var fireballJson = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals());
            using var fireballDocument = JsonDocument.Parse(fireballJson);
            var schoolMask = fireballDocument.RootElement.GetProperty("misc").GetProperty("schoolMask");

            Assert.Equal(4, schoolMask.GetProperty("raw").GetInt32());
            var schoolFlags = schoolMask.GetProperty("flags").EnumerateArray().ToList();
            Assert.Single(schoolFlags);
            Assert.Equal("SPELL_SCHOOL_MASK_FIRE", schoolFlags[0].GetProperty("name").GetString());
        }

        // Task 8 review, "Task 2": a set bit with no declared enum member
        // used to come back from this JSON path as a bare decimal string in
        // the name position (Enum.GetName returns null for it, and
        // GetEnumName's fallback stringified the boxed integer) -- the exact
        // failure SpellWork.Mcp/Tools/DecoderTools.cs's decode_flags exists
        // to prevent. BuildFlagList is the shared per-bit primitive behind
        // every flag-decomposition call site in SpellInfoJsonSerializer
        // (procFlags/procFlagsEx directly, schoolMask/preventionType/targets/
        // targetCreatureType/interrupt flags via BuildFlagsList), so pinning
        // it here covers all of them without needing a loaded spell: it is
        // `internal` (SpellWork.Core.Tests has InternalsVisibleTo) purely so
        // this test can call it directly, exactly as MinimalSpell() below
        // lets the section-validation tests run without client data.
        //
        // SpellSchoolMask is real, already-declared production data (not a
        // synthetic enum): bits 0-6 are declared, bit 30 is not -- the same
        // combination SpellWork.Mcp.Tests.DecoderToolsTests.
        // DecodeFlags_ReportsUnmatchedBits uses on the MCP side, so the two
        // tools are pinned against the same case.
        [Fact]
        public void BuildFlagList_UnmatchedBit_IsNeverReportedAsAName()
        {
            var flags = SpellInfoJsonSerializer.BuildFlagList(
                typeof(SpellSchoolMask), 4u | (1u << 30), out var unmatchedBits);

            var matched = Assert.Single(flags) as System.Collections.Generic.Dictionary<string, object>;
            Assert.NotNull(matched);
            Assert.Equal("SPELL_SCHOOL_MASK_FIRE", matched["name"]);

            var unmatchedBit = Assert.Single(unmatchedBits);
            Assert.Equal(30, unmatchedBit);

            // The specific regression: no matched entry's "name" is ever a
            // string that parses as a plain number.
            Assert.All(flags, f =>
            {
                var name = ((System.Collections.Generic.Dictionary<string, object>)f)["name"] as string;
                Assert.False(long.TryParse(name, out _));
            });
        }

        // basePoints is computed against the render context, so the document
        // records which level, item level and difficulty produced it. Emitted
        // directly (not through AddIfNonZero): a level or difficulty of 0 is a
        // real render context, not an absent one.
        [SkippableFact]
        public void Serialize_records_the_render_context_at_the_document_root()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals());
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(DBC.DBC.SelectedLevel, root.GetProperty("level").GetUInt32());
            Assert.Equal(DBC.DBC.SelectedItemLevel, root.GetProperty("itemLevel").GetUInt32());
            Assert.Equal(DBC.DBC.SelectedDifficulty, root.GetProperty("difficulty").GetInt32());
        }

        // "context" must be filterable like every other section: a caller
        // asking only for ["effects"] should not be forced to carry level/
        // itemLevel/difficulty, and a caller explicitly asking for
        // ["context"] should get them without needing anything else.
        [SkippableFact]
        public void Serialize_omits_the_render_context_when_not_requested()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(
                DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals(), new[] { "effects" });
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.False(root.TryGetProperty("level", out _));
            Assert.False(root.TryGetProperty("itemLevel", out _));
            Assert.False(root.TryGetProperty("difficulty", out _));

            var contextJson = SpellInfoJsonSerializer.Serialize(
                DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals(), new[] { "context" });
            using var contextDocument = JsonDocument.Parse(contextJson);
            Assert.True(contextDocument.RootElement.TryGetProperty("level", out _));
        }

        // Fireball (133) is a Mage spell: SpellFamilyName 3 (SPELLFAMILY_MAGE)
        // with a non-empty class mask. search_spells(family:) filters on this
        // and had nothing to read before.
        [SkippableFact]
        public void Serialize_EmitsSpellFamilyNameAndClassMask()
        {
            _fixture.SkipIfNoClientData();

            var json = SpellInfoJsonSerializer.Serialize(
                DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(3, root.GetProperty("spellFamilyName").GetProperty("raw").GetInt32());
            Assert.Equal("SPELLFAMILY_MAGE", root.GetProperty("spellFamilyName").GetProperty("name").GetString());
            Assert.Equal(4, root.GetProperty("spellClassMask").GetArrayLength());
        }

        // Fireball's effect 0 triggers nothing, but spell 20154 (Seal of
        // Righteousness) has an effect with a non-zero EffectTriggerSpell.
        // get_spell_triggers reads this key. Assert against whatever spell in
        // the store actually has one rather than hard-coding a guess: find it,
        // then assert the key is present and resolves to a real spell name.
        [SkippableFact]
        public void Serialize_EmitsEffectTriggerSpell()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Any(e => e != null && e.Entry.EffectTriggerSpell != 0
                                   && DBC.DBC.SpellInfoStore.ContainsKey(e.Entry.EffectTriggerSpell)));

            var json = SpellInfoJsonSerializer.Serialize(spell, RenderContext.FromGlobals());
            using var doc = JsonDocument.Parse(json);

            var triggered = doc.RootElement.GetProperty("effects").EnumerateArray()
                .First(e => e.TryGetProperty("triggerSpell", out _))
                .GetProperty("triggerSpell");

            Assert.True(triggered.GetProperty("id").GetInt32() != 0);
            Assert.False(string.IsNullOrEmpty(triggered.GetProperty("name").GetString()));
        }

        // The eight new/reserved sections must each be filterable, and an
        // unknown one must still be rejected. This is the contract
        // get_spell's `sections` argument depends on.
        //
        // Task 2 review, fix round 1: this was gated behind
        // _fixture.SkipIfNoClientData() but every assertion here is "did this
        // throw or not" -- section-name validation runs before any spell
        // field is touched (SpellInfoJsonSerializer.cs:80-91), so it needs no
        // loaded spell at all. Gating it behind client data was what let the
        // "items"/"visual" regression above ship silently: this test would
        // have caught it, but skipped instead. Ungated via MinimalSpell so it
        // actually executes -- and can actually fail -- on a machine with no
        // SPELLWORK_DBC_PATH set.
        //
        // The rejection half uses a name that is genuinely not a section and
        // never will be; asserting the throw against a reserved name that
        // later gets implemented is what made this line need editing before.
        [Fact]
        public void Serialize_AcceptsNewSections_AndStillRejectsUnknown()
        {
            var spell = MinimalSpell();
            var context = new RenderContext(1, 1, 0);

            foreach (var section in new[] { "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems", "items", "triggers" })
                SpellInfoJsonSerializer.Serialize(spell, context, new[] { section });

            Assert.Throws<ArgumentException>(() =>
                SpellInfoJsonSerializer.Serialize(spell, context, new[] { "notasection" }));
        }

        [SkippableFact]
        public void Serialize_TriggersSection_ReportsBothDirections()
        {
            _fixture.SkipIfNoClientData();

            // Pick a spell that is known to both trigger and be triggered:
            // find one whose id appears in SpellTriggerStore as a key and
            // which also has a non-zero EffectTriggerSpell.
            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                DBC.DBC.SpellTriggerStore.ContainsKey(s.ID)
                && s.Effects.Any(e => e != null && e.Entry.EffectTriggerSpell != 0));

            var json = SpellInfoJsonSerializer.Serialize(
                spell, RenderContext.FromGlobals(), new[] { "triggers" });
            using var doc = JsonDocument.Parse(json);
            var triggers = doc.RootElement.GetProperty("triggers");

            Assert.True(triggers.GetProperty("triggers").GetArrayLength() > 0);
            Assert.True(triggers.GetProperty("triggeredBy").GetArrayLength() > 0);
        }

        [SkippableFact]
        public void Serialize_ItemsSection_ListsItemsGrantingTheSpell()
        {
            _fixture.SkipIfNoClientData();

            // 5.4.8 has no ItemEffect table: an item names its spells in
            // Item-sparse.db2's five SpellId columns.
            var item = DBC.DBC.ItemSparse.Values.First(i => i.SpellId.Any(id => id != 0 && DBC.DBC.SpellInfoStore.ContainsKey(id)));
            var spell = DBC.DBC.SpellInfoStore[item.SpellId.First(id => id != 0 && DBC.DBC.SpellInfoStore.ContainsKey(id))];

            var json = SpellInfoJsonSerializer.Serialize(
                spell, RenderContext.FromGlobals(), new[] { "items" });
            using var doc = JsonDocument.Parse(json);

            Assert.Contains(doc.RootElement.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetInt32() == item.ID);
        }

        // Spell 43621 (Gust of Wind) has SpellMisc rows at difficulties 2 and 4
        // whose Attributes[0] is 0x00000100, while its base (difficulty 0) row
        // has Attributes[0] == 0. Found by scanning SpellMisc for a non-zero
        // difficulty row whose first attribute word differs from the base row.
        // Every difficulty-keyed section must read the row at the requested
        // difficulty (base fallback), in both the JSON and the text render.
        [SkippableFact]
        public void Serialize_reads_the_SpellMisc_row_at_the_requested_difficulty()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var spell = DBC.DBC.SpellInfoStore[43621];

            uint[] Group0Bits(int difficulty)
            {
                var json = SpellInfoJsonSerializer.Serialize(spell, new RenderContext(90, 463, difficulty), new[] { "attributes" });
                using var document = JsonDocument.Parse(json);
                return document.RootElement.TryGetProperty("attributes", out var attributes)
                    ? attributes.EnumerateArray().Where(a => a.GetProperty("group").GetInt32() == 0)
                        .Select(a => a.GetProperty("raw").GetUInt32()).ToArray()
                    : new uint[0];
            }

            Assert.Equal(new uint[] { 0x100 }, Group0Bits(2));
            Assert.Empty(Group0Bits(0));
            Assert.Empty(Group0Bits(1)); // no row at 1: base row

            string Text(int difficulty)
            {
                var writer = new PlainTextSpellInfoWriter();
                spell.Write(writer, new RenderContext(90, 463, difficulty));
                return writer.ToString();
            }

            Assert.Contains("Attributes: 0x00000100", Text(2));
            Assert.DoesNotContain("Attributes: 0x00000100", Text(0));
        }
    }
}
