using System;
using System.Collections.Generic;
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

        // Section-name validation runs before any field is read, and every
        // lookup SpellInfo/the serializer make is null-safe without a load,
        // so these synthetic spells need no client data: the shape tests
        // below run (and can fail) on a machine with no SPELLWORK_DBC_PATH.
        private static SpellInfo MinimalSpell() =>
            new SpellInfo(new SpellEntry { ID = 1, NameLang = Enumerable.Repeat("Test", 9).ToArray() });

        // An id no SpellDifficulty row lists and no data spell uses.
        private const int SyntheticId = 1999999;

        private static string[] Slots(string value) => Enumerable.Repeat(value, 9).ToArray();

        // Synthetic 3.3.5 Spell row: effect 0 school damage with a die
        // range, effect 1 a dummy aura with every other effect column set,
        // effect 2 empty. Effect 1's class mask is the effect-major group B
        // (columns 125-127).
        private static SpellEntry SyntheticRow()
        {
            var row = new SpellEntry
            {
                ID = SyntheticId,
                NameLang = Slots("Synthetic Bolt"),
                NameSubtextLang = Slots("Rank 3"),
                ProcTypeMask = 0x00010000 | (1u << 30),
                ProcChance = 35,
                ProcCharges = 2,
                SpellLevel = 10,
                BaseLevel = 10,
            };
            row.Attributes[0] = 0x00010000;
            row.Attributes[7] = 0x00000001;
            row.Reagent[0] = 17031;
            row.ReagentCount[0] = 2;
            row.Reagent[3] = 17020;
            row.ReagentCount[3] = 1;

            row.Effect[0] = 2;            // SCHOOL_DAMAGE
            row.EffectBasePoints[0] = 99;
            row.EffectDieSides[0] = 21;
            row.ImplicitTargetA[0] = 6;

            row.Effect[1] = 6;            // APPLY_AURA
            row.EffectAura[1] = 4;        // DUMMY
            row.EffectBasePoints[1] = 4;
            row.EffectRealPointsPerLevel[1] = 1.5f;
            row.EffectPointsPerCombo[1] = 3f;
            row.EffectAmplitude[1] = 0.25f;
            row.EffectAuraPeriod[1] = 3000;
            row.EffectChainTargets[1] = 4;
            row.EffectItemType[1] = 7;
            row.EffectMechanic[1] = 12;
            row.ImplicitTargetA[1] = 1;
            row.ImplicitTargetB[1] = 15;
            row.EffectRadiusIndex[1] = 13;
            row.EffectMiscValue[1] = 127;
            row.EffectMiscValueB[1] = -3;
            row.EffectTriggerSpell[1] = 133;
            row.EffectSpellClassMaskB[0] = 0x1;
            row.EffectSpellClassMaskB[1] = 0x20;
            row.EffectSpellClassMaskB[2] = 0x300;
            row.EffectChainAmplitude[1] = 0.5f;
            row.EffectBonusCoefficient[1] = 0.75f;
            return row;
        }

        private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

        // Step 1 of the brief: the top-level and per-effect shape of a 3.3.5
        // document, on a synthetic row.
        [Fact]
        public void Synthetic_row_has_the_3_3_5_top_level_shape()
        {
            var root = Parse(SpellInfoJsonSerializer.Serialize(new SpellInfo(SyntheticRow()), new RenderContext(80, 0)));

            Assert.Equal(SyntheticId, root.GetProperty("id").GetInt32());
            Assert.Equal("Synthetic Bolt", root.GetProperty("name").GetString());
            Assert.Equal("Rank 3", root.GetProperty("rank").GetString());

            var difficulty = root.GetProperty("difficulty");
            Assert.Equal(JsonValueKind.Null, difficulty.GetProperty("requested").ValueKind);
            Assert.Equal(SyntheticId, difficulty.GetProperty("resolvedSpellId").GetInt32());
            Assert.Equal(0, difficulty.GetProperty("variants").GetArrayLength());

            var attributes = root.GetProperty("attributes");
            Assert.Equal(8, attributes.GetArrayLength());
            Assert.Equal(0x00010000u, attributes[0].GetProperty("value").GetUInt32());
            Assert.Equal(1u, attributes[7].GetProperty("value").GetUInt32());

            Assert.Equal(SyntheticId, root.GetProperty("requestedId").GetInt32());
            Assert.Equal("Rank 3", root.GetProperty("nameSubtext").GetString());

            // 5.4.8 "procs": procFlags is the matched-bit array itself.
            var procs = root.GetProperty("procs");
            Assert.Equal("PROC_FLAG_DONE_SPELL_MAGIC_DMG_CLASS_NEG", Assert.Single(procs.GetProperty("procFlags").EnumerateArray()).GetProperty("name").GetString());
            Assert.Equal(30, Assert.Single(procs.GetProperty("procFlagsUnmatchedBits").EnumerateArray()).GetInt32());
            Assert.Equal(35, procs.GetProperty("procChance").GetInt32());
            Assert.Equal(2, procs.GetProperty("procCharges").GetInt32());

            // 5.4.8 "reagents": {items: [{itemId, count}]}.
            var reagents = root.GetProperty("reagents").GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, reagents.Count);
            Assert.Equal(17031, reagents[0].GetProperty("itemId").GetInt32());
            Assert.Equal(2, reagents[0].GetProperty("count").GetInt32());
            Assert.Equal(17020, reagents[1].GetProperty("itemId").GetInt32());

            Assert.Equal(JsonValueKind.Object, root.GetProperty("misc").ValueKind);
            foreach (var absent in new[] { "scaling", "effectDifficulties", "procFlags", "procChance", "castTime", "range",
                         "castingTimeIndex", "mechanic", "dispel", "preventionType", "category" })
                Assert.False(root.TryGetProperty(absent, out _), $"'{absent}' must not be emitted at the root");

            // No world database was passed: items is empty and says why.
            Assert.Equal(0, root.GetProperty("items").GetArrayLength());
            Assert.Equal(SpellInfoJsonSerializer.ItemsWithoutWorldDatabaseNote, root.GetProperty("itemsNote").GetString());
        }

        // Items using the spell come from item_template (spellid_1..5) when the
        // world database supplies them: one entry per slot naming the spell.
        [Fact]
        public void Items_section_lists_the_item_template_slots_supplied()
        {
            var uses = new[]
            {
                new SpellWork.Database.ItemSpellUse { Entry = 5175, Name = "Earth Totem", Slot = 1, Trigger = 0 },
                new SpellWork.Database.ItemSpellUse { Entry = 9999, Name = "Proc Blade", Slot = 2, Trigger = 2 },
            };

            var root = Parse(SpellInfoJsonSerializer.Serialize(new SpellInfo(SyntheticRow()), new RenderContext(80, 0),
                new[] { "items" }, itemsUsingSpell: uses));

            Assert.False(root.TryGetProperty("itemsNote", out _));
            var items = root.GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(2, items.Count);
            Assert.Equal(5175, items[0].GetProperty("id").GetInt32());
            Assert.Equal("Earth Totem", items[0].GetProperty("name").GetString());
            Assert.Equal(1, items[0].GetProperty("slot").GetInt32());
            Assert.Equal("ITEM_SPELLTRIGGER_ON_USE", items[0].GetProperty("trigger").GetProperty("name").GetString());
            Assert.Equal(2, items[1].GetProperty("trigger").GetProperty("raw").GetInt32());
            Assert.Equal("ITEM_SPELLTRIGGER_CHANCE_ON_HIT", items[1].GetProperty("trigger").GetProperty("name").GetString());

            // A configured world database with no matching item: empty, no note.
            var none = Parse(SpellInfoJsonSerializer.Serialize(new SpellInfo(SyntheticRow()), new RenderContext(80, 0),
                new[] { "items" }, itemsUsingSpell: new SpellWork.Database.ItemSpellUse[0]));
            Assert.Equal(0, none.GetProperty("items").GetArrayLength());
            Assert.False(none.TryGetProperty("itemsNote", out _));
        }

        [Fact]
        public void Synthetic_row_has_the_3_3_5_effect_shape()
        {
            var root = Parse(SpellInfoJsonSerializer.Serialize(new SpellInfo(SyntheticRow()), new RenderContext(80, 0)));
            var effects = root.GetProperty("effects").EnumerateArray().ToList();
            Assert.Equal(new[] { 0, 1 }, effects.Select(e => e.GetProperty("index").GetInt32()));

            // Always present: the 5.4.8 always-emitted keys plus the CoA
            // additions (dieSides, auraPeriod, itemType, damageMultiplier).
            var alwaysKeys = new[]
            {
                "index", "effect", "basePoints", "targetA", "targetB",
                "dieSides", "auraPeriod", "itemType", "damageMultiplier",
            };
            foreach (var effect in effects)
            {
                foreach (var key in alwaysKeys)
                    Assert.True(effect.TryGetProperty(key, out _), $"effect {effect.GetProperty("index")} lacks '{key}'");
                foreach (var absent in new[] { "difficulty", "resolvedDifficulty", "scaling", "aura", "implicitTargetA",
                             "implicitTargetB", "pointsPerComboPoint", "maxRadius" })
                    Assert.False(effect.TryGetProperty(absent, out _), $"effect must not carry '{absent}'");
            }

            // Effect 0: CalcValue base is BasePoints + 1 with a die; the range
            // is 100..120. 5.4.8 omits the zero-valued keys.
            var damage = effects[0];
            Assert.Equal("SPELL_EFFECT_SCHOOL_DAMAGE", damage.GetProperty("effect").GetProperty("name").GetString());
            Assert.Equal(100, damage.GetProperty("basePoints").GetDouble());
            Assert.Equal(100, damage.GetProperty("basePointsMin").GetDouble());
            Assert.Equal(120, damage.GetProperty("basePointsMax").GetDouble());
            Assert.Equal(21, damage.GetProperty("dieSides").GetInt32());
            Assert.Equal(6, damage.GetProperty("targetA").GetProperty("raw").GetInt32());
            foreach (var zero in new[] { "auraType", "triggerSpell", "spellClassMask", "radius", "chainTargets",
                         "miscValue", "miscValueB", "amplitude", "mechanic", "valuePerResource", "realPointsPerLevel",
                         "bonusMultiplier", "spellPowerCoefficient" })
                Assert.False(damage.TryGetProperty(zero, out _), $"zero '{zero}' must be omitted as in 5.4.8");

            var aura = effects[1];
            Assert.Equal("SPELL_AURA_DUMMY", aura.GetProperty("auraType").GetProperty("name").GetString());
            // Level 80, spell level 10: 4 + (int)(70 * 1.5) = 109, no die.
            Assert.Equal(109, aura.GetProperty("basePoints").GetDouble());
            Assert.False(aura.TryGetProperty("basePointsMin", out _));
            Assert.Equal(1.5, aura.GetProperty("realPointsPerLevel").GetDouble(), 5);
            Assert.Equal(3, aura.GetProperty("valuePerResource").GetDouble(), 5);
            Assert.Equal(0.25, aura.GetProperty("amplitude").GetDouble(), 5);
            Assert.Equal(3000, aura.GetProperty("auraPeriod").GetInt32());
            Assert.Equal(4, aura.GetProperty("chainTargets").GetInt32());
            Assert.Equal(7, aura.GetProperty("itemType").GetInt32());
            Assert.Equal(12, aura.GetProperty("mechanic").GetProperty("raw").GetInt32());
            Assert.Equal(1, aura.GetProperty("targetA").GetProperty("raw").GetInt32());
            Assert.Equal(15, aura.GetProperty("targetB").GetProperty("raw").GetInt32());
            Assert.Equal(127, aura.GetProperty("miscValue").GetInt32());
            Assert.Equal(-3, aura.GetProperty("miscValueB").GetInt32());
            Assert.Equal(133, aura.GetProperty("triggerSpell").GetProperty("id").GetInt32());
            Assert.Equal(0.5, aura.GetProperty("damageMultiplier").GetDouble(), 5);
            Assert.Equal(0.75, aura.GetProperty("bonusMultiplier").GetDouble(), 5);
            Assert.Equal(0.75, aura.GetProperty("spellPowerCoefficient").GetDouble(), 5);

            // Effect-major (CoA flag96 per effect): effect 1's mask is the B
            // group, columns 125-127, in word order.
            Assert.Equal(new uint[] { 0x1, 0x20, 0x300 }, aura.GetProperty("spellClassMask").EnumerateArray().Select(x => x.GetUInt32()));
        }

        // Item names come from the world database (item_template); without
        // one there is no name and the key is omitted, not null.
        [Fact]
        public void Reagent_names_appear_only_when_item_names_are_supplied()
        {
            var spell = new SpellInfo(SyntheticRow());
            var context = new RenderContext(80, 0);

            var without = Parse(SpellInfoJsonSerializer.Serialize(spell, context, new[] { "reagents" }));
            Assert.All(without.GetProperty("reagents").GetProperty("items").EnumerateArray(), r => Assert.False(r.TryGetProperty("name", out _)));

            var names = new Dictionary<int, string> { [17031] = "Rune of Teleportation" };
            var with = Parse(SpellInfoJsonSerializer.Serialize(spell, context, new[] { "reagents" }, itemNames: names));
            var reagents = with.GetProperty("reagents").GetProperty("items").EnumerateArray().ToList();
            Assert.Equal("Rune of Teleportation", reagents[0].GetProperty("name").GetString());
            Assert.False(reagents[1].TryGetProperty("name", out _)); // 17020 not in the lookup
        }

        // The difficulty block echoes the requested mode; a tool that resolved
        // the id itself passes its view through unchanged.
        [Fact]
        public void Difficulty_block_reports_the_requested_mode_and_a_passed_view()
        {
            var spell = new SpellInfo(SyntheticRow());

            // Present whatever the filter: "effects" alone still carries it.
            var requested = Parse(SpellInfoJsonSerializer.Serialize(spell, new RenderContext(80, 0, 2), new[] { "effects" }))
                .GetProperty("difficulty");
            Assert.Equal(2, requested.GetProperty("requested").GetInt32());
            Assert.Equal(SyntheticId, requested.GetProperty("resolvedSpellId").GetInt32()); // in no row: itself

            var view = new SpellDifficultyView(1, SyntheticId, new[] { 7, SyntheticId });
            var passed = Parse(SpellInfoJsonSerializer.Serialize(spell, new RenderContext(80, 0, 1), new[] { "context" }, view))
                .GetProperty("difficulty");
            Assert.Equal(1, passed.GetProperty("requested").GetInt32());
            Assert.Equal(new[] { 7, SyntheticId }, passed.GetProperty("variants").EnumerateArray().Select(x => x.GetInt32()));
        }

        // The requested id sits at the root next to the resolved id (I5): a
        // tool that resolved 25N -> 10N passes a view naming both.
        [Fact]
        public void Root_reports_the_requested_id_beside_the_described_id()
        {
            var spell = new SpellInfo(SyntheticRow());
            var view = new SpellDifficultyView(0, 1234567, SyntheticId, new[] { SyntheticId, 1234567 });
            var root = Parse(SpellInfoJsonSerializer.Serialize(spell, new RenderContext(80, 0, 0), new[] { "effects" }, view));

            Assert.Equal(SyntheticId, root.GetProperty("id").GetInt32());
            Assert.Equal(1234567, root.GetProperty("requestedId").GetInt32());
            Assert.Equal(SyntheticId, root.GetProperty("difficulty").GetProperty("resolvedSpellId").GetInt32());
        }

        // Pins the 5.4.8 envelope (185ef00 goldens): containers and key names
        // every spellwork server shares. CoA additions may sit beside them;
        // the 5.4.8 paths must exist and the CoA-only renames must not.
        [Fact]
        public void Document_keeps_the_5_4_8_key_paths()
        {
            var row = SyntheticRow();
            row.Targets = 0x2;
            row.MaxTargets = 3;
            var root = Parse(SpellInfoJsonSerializer.Serialize(new SpellInfo(row), new RenderContext(80, 0)));

            var paths = new HashSet<string>();
            CollectPaths(root, "", paths);

            foreach (var expected in new[]
                     {
                         "id", "name", "level", "itemLevel", "nameSubtext",
                         "misc", "misc.mechanic", "misc.damageClass", "misc.dispel", "misc.preventionType",
                         "spellFamilyName", "spellClassMask", "attributes[].word", "attributes[].value", "attributes[].flags",
                         "effects[].index", "effects[].effect.raw", "effects[].effect.name", "effects[].auraType.raw",
                         "effects[].basePoints", "effects[].basePointsMin", "effects[].basePointsMax",
                         "effects[].valuePerResource", "effects[].realPointsPerLevel", "effects[].bonusMultiplier",
                         "effects[].spellPowerCoefficient", "effects[].targetA.raw", "effects[].targetB.name",
                         "effects[].chainTargets", "effects[].triggerSpell.id", "effects[].miscValue", "effects[].miscValueB",
                         "effects[].amplitude", "effects[].mechanic.raw", "effects[].spellClassMask",
                         "targets[].difficultyId", "targets[].targets.raw", "targets[].targets.flags", "targets[].maxTargets",
                         "reagents.items[].itemId", "reagents.items[].count",
                         "procs.procFlags[].raw", "procs.procFlags[].name", "procs.procChance", "procs.procCharges",
                         "triggers.triggers", "triggers.triggeredBy", "items",
                     })
                Assert.True(paths.Contains(expected), $"5.4.8 key path '{expected}' is missing");

            foreach (var renamed in new[]
                     {
                         "procFlags", "procChance", "procCharges", "castingTimeIndex", "rangeIndex", "mechanic", "category",
                         "effects[].aura", "effects[].implicitTargetA", "effects[].implicitTargetB",
                         "effects[].pointsPerComboPoint", "reagents[].item", "reagents.items[].item", "targets.targets",
                     })
                Assert.False(paths.Contains(renamed), $"'{renamed}' is not a 5.4.8 key path");
            Assert.DoesNotContain(paths, p => p.Contains("Hostile") || p.Contains("Friendly"));
        }

        private static void CollectPaths(JsonElement element, string prefix, HashSet<string> paths)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
                        paths.Add(path);
                        CollectPaths(property.Value, path, paths);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        CollectPaths(item, prefix + "[]", paths);
                    break;
            }
        }

        // An unrecognised section name is a caller mistake, not a near-empty
        // document. The name used is one no schema would adopt.
        [Fact]
        public void Serialize_throws_on_an_unrecognised_section_name()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => SpellInfoJsonSerializer.Serialize(MinimalSpell(), new RenderContext(1, 1), new[] { "notasection" }));

            Assert.Contains("notasection", ex.Message);
        }

        // The 5.4.8 section names are kept (spec 6: every spellwork server is
        // called the same way); only "scaling" is gone with 3.3.5's lack of
        // scaling data.
        [Fact]
        public void Serialize_accepts_the_5_4_8_sections_except_scaling()
        {
            var spell = MinimalSpell();
            var context = new RenderContext(1, 1);

            foreach (var section in new[] { "misc", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs",
                         "context", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems", "items", "triggers", "totems", "visual" })
                SpellInfoJsonSerializer.Serialize(spell, context, new[] { section });

            foreach (var gone in new[] { "scaling", "general", "notasection" })
                Assert.Throws<ArgumentException>(() => SpellInfoJsonSerializer.Serialize(spell, context, new[] { gone }));
        }

        // BuildFlagList is the per-bit primitive behind every flag list here;
        // a set bit with no declared member goes to unmatchedBits, never into
        // the name position as a decimal string.
        [Fact]
        public void BuildFlagList_UnmatchedBit_IsNeverReportedAsAName()
        {
            var flags = SpellInfoJsonSerializer.BuildFlagList(
                typeof(SpellSchoolMask), 4u | (1u << 30), out var unmatchedBits);

            var matched = Assert.Single(flags) as Dictionary<string, object>;
            Assert.NotNull(matched);
            Assert.Equal("SPELL_SCHOOL_MASK_FIRE", matched["name"]);

            var unmatchedBit = Assert.Single(unmatchedBits);
            Assert.Equal(30, unmatchedBit);

            Assert.All(flags, f =>
            {
                var name = ((Dictionary<string, object>)f)["name"] as string;
                Assert.False(long.TryParse(name, out _));
            });
        }

        // ---- reference data ------------------------------------------------

        private SpellInfo Spell(int id)
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);
            Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(id), $"spell {id} is missing from the reference data");
            return DBC.DBC.SpellInfoStore[id];
        }

        [SkippableFact]
        public void Fireball_has_the_3_3_5_shape()
        {
            var fireball = Spell(133);
            var json = Parse(SpellInfoJsonSerializer.Serialize(fireball, new RenderContext(80, 0)));

            Assert.Equal("Fireball", json.GetProperty("name").GetString());
            Assert.Equal("Rank 1", json.GetProperty("rank").GetString());
            Assert.Equal(8, json.GetProperty("attributes").GetArrayLength());
            Assert.False(json.TryGetProperty("scaling", out _));
            Assert.True(json.TryGetProperty("misc", out _));
            Assert.False(json.TryGetProperty("effectDifficulties", out _));

            // CoA Fireball rank 1: SCHOOL_DAMAGE 13 d9 (14..22), then two auras.
            var effects = json.GetProperty("effects").EnumerateArray().ToList();
            Assert.Equal(3, effects.Count);
            Assert.Equal("SPELL_EFFECT_SCHOOL_DAMAGE", effects[0].GetProperty("effect").GetProperty("name").GetString());
            Assert.Equal(14, effects[0].GetProperty("basePointsMin").GetDouble());
            Assert.Equal(22, effects[0].GetProperty("basePointsMax").GetDouble());
            Assert.Equal(9, effects[0].GetProperty("dieSides").GetInt32());
            Assert.Equal("SPELL_AURA_PERIODIC_DAMAGE", effects[1].GetProperty("auraType").GetProperty("name").GetString());
            Assert.Equal(2000, effects[1].GetProperty("auraPeriod").GetInt32());
            Assert.All(effects, e => Assert.False(e.TryGetProperty("resolvedDifficulty", out _)));
        }

        [SkippableFact]
        public void Serialize_pairs_raw_flag_values_with_decoded_names()
        {
            var spell = Spell(133);
            var raw = spell.Attributes[0];
            Assert.NotEqual(0u, raw);
            var bit = Enumerable.Range(0, 32).Select(i => 1u << i)
                .First(m => (raw & m) != 0 && Enum.IsDefined(typeof(SpellAtribute), m));
            var expectedName = Enum.GetName(typeof(SpellAtribute), bit);

            var json = Parse(SpellInfoJsonSerializer.Serialize(spell, new RenderContext(80, 0)));
            var word0 = json.GetProperty("attributes").EnumerateArray().First(a => a.GetProperty("word").GetInt32() == 0);
            Assert.Equal(raw, word0.GetProperty("value").GetUInt32());
            Assert.Contains(word0.GetProperty("flags").EnumerateArray(), f => f.GetProperty("name").GetString() == expectedName);
        }

        // A spell in SpellDifficulty row 1 (113 / 142182 / 369533 / 0): mode 3
        // has no spell, so it falls back to mode 1 (SpellMgr.cpp:592-602).
        [SkippableFact]
        public void Difficulty_block_resolves_a_SpellDifficulty_spell()
        {
            var chains = Spell(113);

            var none = Parse(SpellInfoJsonSerializer.Serialize(chains, new RenderContext(80, 0))).GetProperty("difficulty");
            Assert.Equal(JsonValueKind.Null, none.GetProperty("requested").ValueKind);
            Assert.Equal(113, none.GetProperty("resolvedSpellId").GetInt32());
            Assert.Equal(new[] { 113, 142182, 369533 }, none.GetProperty("variants").EnumerateArray().Select(x => x.GetInt32()));

            var heroic = Parse(SpellInfoJsonSerializer.Serialize(chains, new RenderContext(80, 0, 3))).GetProperty("difficulty");
            Assert.Equal(3, heroic.GetProperty("requested").GetInt32());
            Assert.Equal(142182, heroic.GetProperty("resolvedSpellId").GetInt32());

            var writer = new PlainTextSpellInfoWriter();
            chains.Write(writer, new RenderContext(80, 0, 3));
            Assert.Contains("SpellDifficulty variants: 113, 142182, 369533", writer.ToString());
            Assert.Contains("Difficulty mode 3 (25H) resolves to spell 142182", writer.ToString());
        }

        // The per-level term is folded into basePoints; realPointsPerLevel is
        // the raw coefficient beside it, and the text says "includes".
        [SkippableFact]
        public void Per_level_coefficient_is_not_presented_as_additive()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);
            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Any(e => e != null && Math.Abs(e.Entry.EffectRealPointsPerLevel) > 1e-5f));
            var json = Parse(SpellInfoJsonSerializer.Serialize(spell, new RenderContext(80, 0)));
            var effects = json.GetProperty("effects").EnumerateArray().ToList();
            Assert.Contains(effects, e => Math.Abs(e.GetProperty("realPointsPerLevel").GetDouble()) > 1e-5);
            Assert.All(effects, e => Assert.False(e.TryGetProperty("perLevelValue", out _)));

            var writer = new PlainTextSpellInfoWriter();
            spell.Write(writer, new RenderContext(80, 0));
            var text = writer.ToString();
            Assert.DoesNotContain("+ Level *", text);
            Assert.Contains("per level)", text);
        }

        [SkippableFact]
        public void Totems_and_computed_aura_value_are_rendered()
        {
            var context = new RenderContext(80, 0);
            var totem = Spell(2484);
            var json = Parse(SpellInfoJsonSerializer.Serialize(totem, context));
            Assert.Equal(DBC.DBC.Spell[2484].RequiredTotemCategoryID[0], json.GetProperty("totems").GetProperty("totemCategory")[0].GetInt32());
            var w = new PlainTextSpellInfoWriter();
            totem.Write(w, context);
            Assert.Contains("Totems: TotemCategory [2, 0]", w.ToString());

            // The aura line shows the CalcValue base, not the raw column.
            var pain = Spell(589);
            var expected = pain.GetEffectValues(pain.Effects[0], context).BaseValue;
            var swp = new PlainTextSpellInfoWriter();
            pain.Write(swp, context);
            Assert.Contains($"value = {expected},", swp.ToString());
        }

        [SkippableFact]
        public void Serialize_honours_the_sections_filter()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(133), RenderContext.Default, new[] { "misc" }));

            Assert.False(json.TryGetProperty("effects", out _));
            Assert.False(json.TryGetProperty("attributes", out _));
            Assert.False(json.TryGetProperty("procs", out _));
            Assert.True(json.TryGetProperty("schoolMask", out _));
            var misc = json.GetProperty("misc");
            Assert.Equal(16, misc.GetProperty("castingTimeIndex").GetProperty("id").GetInt32());
            var range = misc.GetProperty("rangeIndex");
            Assert.Equal(35, range.GetProperty("id").GetInt32());
            // 5.4.8 names: Negative = hostile (slot 0), Positive = friendly (slot 1).
            Assert.Equal(DBC.DBC.SpellRange[35].RangeMax[0], range.GetProperty("maxRangeNegative").GetSingle());
            Assert.Equal(DBC.DBC.SpellRange[35].RangeMax[1], range.GetProperty("maxRangePositive").GetSingle());
            Assert.Equal(133, json.GetProperty("id").GetInt32());
            Assert.Equal("Fireball", json.GetProperty("name").GetString());
        }

        [SkippableFact]
        public void Serialize_accepts_a_mis_cased_section_name()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(133), RenderContext.Default, new[] { "Effects" }));

            Assert.True(json.GetProperty("effects").GetArrayLength() > 0);
            Assert.Equal(new[] { "effects" }, json.GetProperty("sections").EnumerateArray().Select(s => s.GetString()));
        }

        // Arcane Intellect (1459): both effects use SpellRadius 27; the
        // resolved values must be the store row's, not the bare index.
        [SkippableFact]
        public void Serialize_resolves_the_radius_index_to_its_store_values()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(1459), RenderContext.Default));
            var radius = json.GetProperty("effects")[0].GetProperty("radius");
            var row = DBC.DBC.SpellRadius[27];

            Assert.Equal(27, radius.GetProperty("id").GetInt32());
            Assert.Equal(row.Radius, radius.GetProperty("radius").GetSingle());
            Assert.Equal(row.RadiusMax, radius.GetProperty("maxRadius").GetSingle());
        }

        // Arcane Intellect's effect 1 is aura 345, a CoA-custom aura (stock
        // 3.3.5 ends at 316): decoded by name from the CoA enum.
        [SkippableFact]
        public void Serialize_names_a_CoA_custom_aura()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(1459), RenderContext.Default));
            var aura = json.GetProperty("effects")[1].GetProperty("auraType");

            Assert.Equal(345, aura.GetProperty("raw").GetInt32());
            Assert.Equal(Enum.GetName(typeof(AuraType), 345), aura.GetProperty("name").GetString());
            Assert.False(int.TryParse(aura.GetProperty("name").GetString(), out _));
        }

        // Polymorph (118) TargetCreatureType 0xC1 = BEAST | HUMANOID | CRITTER:
        // a combined [Flags] value decomposes into named bits.
        [SkippableFact]
        public void Serialize_decomposes_a_combined_flags_value_into_named_bits()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(118), RenderContext.Default));
            var target = Assert.Single(json.GetProperty("targets").EnumerateArray());
            Assert.Equal(0, target.GetProperty("difficultyId").GetInt32());
            var creatureType = target.GetProperty("targetCreatureType");

            Assert.Equal(0xC1, creatureType.GetProperty("raw").GetInt32());
            var names = creatureType.GetProperty("flags").EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();
            Assert.Equal(new[] { "BEAST", "HUMANOID", "CRITTER" }, names);

            var fireball = Parse(SpellInfoJsonSerializer.Serialize(Spell(133), RenderContext.Default));
            var schoolMask = fireball.GetProperty("schoolMask");
            Assert.Equal(4, schoolMask.GetProperty("raw").GetInt32());
            Assert.Equal("SPELL_SCHOOL_MASK_FIRE", Assert.Single(schoolMask.GetProperty("flags").EnumerateArray()).GetProperty("name").GetString());
        }

        [SkippableFact]
        public void Serialize_records_the_render_context_at_the_document_root()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(133), RenderContext.Default));

            Assert.Equal(DBC.DBC.MaxLevel, json.GetProperty("level").GetUInt32());
            Assert.Equal(DBC.DBC.DefaultItemLevel, json.GetProperty("itemLevel").GetUInt32());
            Assert.Equal(JsonValueKind.Null, json.GetProperty("difficulty").GetProperty("requested").ValueKind);
        }

        [SkippableFact]
        public void Serialize_omits_the_render_context_when_not_requested()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(133), RenderContext.Default, new[] { "effects" }));
            Assert.False(json.TryGetProperty("level", out _));
            Assert.False(json.TryGetProperty("itemLevel", out _));
            Assert.True(json.TryGetProperty("difficulty", out _)); // always present

            var context = Parse(SpellInfoJsonSerializer.Serialize(Spell(133), RenderContext.Default, new[] { "context" }));
            Assert.True(context.TryGetProperty("level", out _));
        }

        [SkippableFact]
        public void Serialize_EmitsSpellFamilyNameAndClassMask()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(133), RenderContext.Default));

            Assert.Equal(3, json.GetProperty("spellFamilyName").GetProperty("raw").GetInt32());
            Assert.Equal("SPELLFAMILY_MAGE", json.GetProperty("spellFamilyName").GetProperty("name").GetString());
            Assert.Equal(new uint[] { 1, 0, 8 }, json.GetProperty("spellClassMask").EnumerateArray().Select(x => x.GetUInt32()));
        }

        // Death Strike (49998): category 1196 (a SpellCategory row, flags 0)
        // and rune cost 797 (unholy 1, frost 1, 150 runic power).
        [SkippableFact]
        public void Serialize_emits_category_and_rune_cost_from_their_rows()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(49998), RenderContext.Default));

            Assert.Equal(1196, json.GetProperty("misc").GetProperty("category").GetInt32());
            Assert.Equal(0, json.GetProperty("misc").GetProperty("categoryFlags").GetProperty("raw").GetInt32());

            var rune = json.GetProperty("runeCost");
            Assert.Equal(797, rune.GetProperty("id").GetInt32());
            Assert.Equal(0, rune.GetProperty("blood").GetInt32());
            Assert.Equal(1, rune.GetProperty("unholy").GetInt32());
            Assert.Equal(1, rune.GetProperty("frost").GetInt32());
            Assert.Equal(150, rune.GetProperty("runicPower").GetInt32());
        }

        // Omen Clearcasting (16870): ProcTypeMask 0x15510, chance 100, 1 charge.
        [SkippableFact]
        public void Serialize_emits_proc_data_from_the_spell_row()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(16870), RenderContext.Default));

            var procs = json.GetProperty("procs");
            var flags = procs.GetProperty("procFlags").EnumerateArray().Select(f => f.GetProperty("raw").GetUInt32()).ToList();
            Assert.Equal(6, flags.Count);
            Assert.Equal(0x15510u, flags.Aggregate(0u, (mask, bit) => mask | bit));
            Assert.Equal(100, procs.GetProperty("procChance").GetInt32());
            Assert.Equal(1, procs.GetProperty("procCharges").GetInt32());
        }

        // Teleport: Stormwind (3561) uses one Rune of Teleportation (17031);
        // no world database here, so no name.
        [SkippableFact]
        public void Serialize_emits_reagents_from_the_spell_row()
        {
            var json = Parse(SpellInfoJsonSerializer.Serialize(Spell(3561), RenderContext.Default));

            var reagent = Assert.Single(json.GetProperty("reagents").GetProperty("items").EnumerateArray());
            Assert.Equal(17031, reagent.GetProperty("itemId").GetInt32());
            Assert.Equal(1, reagent.GetProperty("count").GetInt32());
            Assert.False(reagent.TryGetProperty("name", out _));
        }

        [SkippableFact]
        public void Serialize_EmitsEffectTriggerSpell()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Any(e => e != null && e.Entry.EffectTriggerSpell != 0
                                   && DBC.DBC.SpellInfoStore.ContainsKey(e.Entry.EffectTriggerSpell)));

            var json = Parse(SpellInfoJsonSerializer.Serialize(spell, RenderContext.Default));
            var triggered = json.GetProperty("effects").EnumerateArray()
                .First(e => e.TryGetProperty("triggerSpell", out _))
                .GetProperty("triggerSpell");

            Assert.True(triggered.GetProperty("id").GetInt32() != 0);
            Assert.False(string.IsNullOrEmpty(triggered.GetProperty("name").GetString()));
        }

        [SkippableFact]
        public void Serialize_TriggersSection_ReportsBothDirections()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                DBC.DBC.SpellTriggerStore.ContainsKey(s.ID)
                && s.Effects.Any(e => e != null && e.Entry.EffectTriggerSpell != 0));

            var triggers = Parse(SpellInfoJsonSerializer.Serialize(spell, RenderContext.Default, new[] { "triggers" }))
                .GetProperty("triggers");

            Assert.True(triggers.GetProperty("triggers").GetArrayLength() > 0);
            Assert.True(triggers.GetProperty("triggeredBy").GetArrayLength() > 0);
        }
    }
}
