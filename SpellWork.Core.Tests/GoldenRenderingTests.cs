using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // Requires the CoA 3.3.5a reference data (SPELLWORK_DBC_PATH). Each golden
    // is a text and a JSON rendering at RenderContext(80, 0, difficulty). A
    // golden with a difficulty renders the way a tool does: the requested id
    // is resolved through SpellDifficultyView first, the resolved spell is
    // rendered, and the view is passed to the serializer.
    //
    // To regenerate after an intentional rendering change, set
    // SPELLWORK_REGEN_GOLDENS=1 and run this class; the files are rewritten in
    // the source tree (SpellWork.Core.Tests/Goldens) and the run then passes.
    // Review the diff by hand against the raw rows before committing.
    public class GoldenRenderingTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public GoldenRenderingTests(ClientDataFixture fixture) => _fixture = fixture;

        // (golden name, spell id, SpellDifficulty mode or null). Spec 8's
        // coverage list; each was checked by hand against its raw Spell.dbc row.
        private static readonly (string Name, int Id, int? Difficulty)[] Cases =
        {
            ("spell-133", 133, null),       // direct damage (3 effects on CoA), missile visual
            ("spell-589", 589, null),       // DoT (periodic damage, 3000 ms)
            ("spell-16864", 16864, null),   // proc aura (PROC_TRIGGER_SPELL) + trigger spell 816871
            ("spell-16870", 16870, null),   // proc flags/chance/charges from the row; triggered by 816871
            ("spell-49998", 49998, null),   // rune cost 797, SpellCategory row 1196
            ("spell-113-d3", 113, 3),       // SpellDifficulty row 1: mode 3 empty -> mode 1 = 142182
            ("spell-273123", 273123, null), // two distinct visual variants (SpellVisual 128 and 8253, both resolve)
            ("spell-2484", 2484, null),     // totem (RequiredTotemCategoryID 2, SUMMON effect)
            ("spell-8017", 8017, null),     // item enchant (ENCHANT_ITEM_TEMPORARY in effect slot 1 only)
            ("spell-1459", 1459, null),     // CoA-custom aura 345, radius 27
            ("spell-3561", 3561, null),     // reagent (17031), no world DB so no item name
            ("spell-5952", 5952, null),     // effect-major class masks: effects 0 and 2 differ, effect 1 empty
        };

        public static IEnumerable<object[]> GoldenFiles() => Cases.Select(c => new object[] { c.Name });

        private static IEnumerable<string> GoldenNamesOnDisk()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Goldens");
            return Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "spell-*.txt").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToList()
                : new List<string>();
        }

        // A [Theory] with zero cases passes vacuously; make a missing goldens
        // folder loud instead, and require every golden to have both files.
        [Fact]
        public void Golden_files_are_present()
        {
            var names = GoldenNamesOnDisk().ToList();
            Assert.NotEmpty(names);
            foreach (var c in Cases)
            {
                Assert.Contains(c.Name, names);
                Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "Goldens", c.Name + ".json")),
                    $"{c.Name}.json is missing");
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GoldenFiles))]
        public void Rendering_matches_the_golden(string goldenName)
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var c = Cases.Single(x => x.Name == goldenName);
            Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(c.Id), $"spell {c.Id} is missing from this client data");
            var view = SpellDifficultyView.For(c.Id, c.Difficulty);
            var spell = DBC.DBC.SpellInfoStore[view.ResolvedSpellId];
            var context = new RenderContext(80, 0, c.Difficulty);

            var writer = new PlainTextSpellInfoWriter();
            spell.Write(writer, context);
            var text = writer.ToString();
            var json = SpellInfoJsonSerializer.Serialize(spell, context, null, view);

            var dir = Path.Combine(AppContext.BaseDirectory, "Goldens");
            if (Environment.GetEnvironmentVariable("SPELLWORK_REGEN_GOLDENS") == "1")
            {
                var source = SourceGoldenDirectory();
                File.WriteAllText(Path.Combine(source, goldenName + ".txt"), Normalise(text));
                File.WriteAllText(Path.Combine(source, goldenName + ".json"), Normalise(json));
                return;
            }

            Assert.Equal(Normalise(File.ReadAllText(Path.Combine(dir, goldenName + ".txt"))), Normalise(text));
            Assert.Equal(Normalise(File.ReadAllText(Path.Combine(dir, goldenName + ".json"))), Normalise(json));
        }

        private static string SourceGoldenDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SpellWork.Core.Tests.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            var goldens = Path.Combine(dir.FullName, "Goldens");
            Directory.CreateDirectory(goldens);
            return goldens;
        }

        private static string Normalise(string text) => text.Replace("\r\n", "\n");
    }
}
