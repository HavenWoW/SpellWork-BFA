using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // Requires the 5.4.8 client data (SPELLWORK_DBC_PATH / SPELLWORK_DB2_PATH).
    // Each golden is a text and a JSON rendering at RenderContext(90, 463, difficulty).
    //
    // To regenerate after an intentional rendering change, set
    // SPELLWORK_REGEN_GOLDENS=1 and run this class; the files are rewritten in
    // the source tree (SpellWork.Core.Tests/Goldens) and the run then passes.
    // Review the diff by hand against the raw rows before committing.
    public class GoldenRenderingTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public GoldenRenderingTests(ClientDataFixture fixture) => _fixture = fixture;

        // (golden name, spell id, difficulty)
        private static readonly (string Name, int Id, int Difficulty)[] Cases =
        {
            ("spell-133", 133, 0),
            ("spell-589", 589, 0),
            ("spell-116", 116, 0),
            ("spell-43093-d2", 43093, 2),
            ("spell-42477", 42477, 0),
            ("spell-49998", 49998, 0),
            ("spell-15786", 15786, 0),
            ("spell-6296", 6296, 0),
            ("spell-16870", 16870, 0),
            ("spell-2825", 2825, 0),
            ("spell-6603", 6603, 0),
            ("spell-52140", 52140, 0),   // two visual variants (SpellMisc.SpellVisualID[0] and [1])
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

        [SkippableTheory(Skip = "coa: Task 6")]
        [MemberData(nameof(GoldenFiles))]
        public void Rendering_matches_the_golden(string goldenName)
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var c = Cases.Single(x => x.Name == goldenName);
            Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(c.Id), $"spell {c.Id} is missing from this client data");
            var spell = DBC.DBC.SpellInfoStore[c.Id];
            var context = new RenderContext(90, 463, c.Difficulty);

            var writer = new PlainTextSpellInfoWriter();
            spell.Write(writer, context);
            var text = writer.ToString();
            var json = SpellInfoJsonSerializer.Serialize(spell, context);

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
