using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // Requires extracted client data. Set SPELLWORK_DBC_PATH (plus
    // SPELLWORK_LOCALE / SPELLWORK_DEFS_PATH / SPELLWORK_BUILD as needed).
    // Set SPELLWORK_REGEN_GOLDENS=1 to (re)write the source-tree goldens.
    public class GoldenRenderingTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public GoldenRenderingTests(ClientDataFixture fixture) => _fixture = fixture;

        // Representative 70449 spells covering the bring-up renderer variety.
        // Rendering covers identity, triggered-by, misc, attributes and effects
        // at DBC.MaxLevel / MaxItemLevel with raw base points.
        private static readonly string[] Cases = new[]
        {
            "spell-133", "spell-17", "spell-5143", "spell-774", "spell-122"
        };

        private static bool Regen =>
            Environment.GetEnvironmentVariable("SPELLWORK_REGEN_GOLDENS") == "1";

        public static IEnumerable<object[]> GoldenFiles()
        {
            // Under regen the files may not exist yet, so enumerate Cases.
            if (Regen)
            {
                foreach (var c in Cases)
                    yield return new object[] { c };
                yield break;
            }

            var dir = Path.Combine(AppContext.BaseDirectory, "Goldens");
            if (!Directory.Exists(dir))
                yield break;

            foreach (var file in Directory.EnumerateFiles(dir, "spell-*.txt").OrderBy(f => f))
                if (Cases.Contains(Path.GetFileNameWithoutExtension(file)))
                    yield return new object[] { Path.GetFileNameWithoutExtension(file) };
        }

        // GoldenFiles() yield-breaks when the Goldens directory is missing, and a
        // [Theory] with zero cases passes vacuously -- it would not be obvious
        // from a green test run that the branch's safety net had gone missing.
        // This makes that failure mode loud instead of silent.
        [Fact]
        public void Golden_files_are_present()
        {
            if (Regen)
                return;

            var dir = Path.Combine(AppContext.BaseDirectory, "Goldens");
            foreach (var c in Cases)
            {
                Assert.True(File.Exists(Path.Combine(dir, c + ".txt")), $"missing golden {c}.txt");
                Assert.True(File.Exists(Path.Combine(dir, c + ".json")), $"missing golden {c}.json");
            }
            Assert.Equal(Cases.Length, GoldenFiles().Count());
        }

        [SkippableTheory]
        [MemberData(nameof(GoldenFiles))]
        public void Rendering_matches_the_golden(string goldenName)
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var id = int.Parse(goldenName.Substring("spell-".Length));
            Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(id),
                $"spell {id} is missing from this client data");

            var spell = DBC.DBC.SpellInfoStore[id];
            var writer = new PlainTextSpellInfoWriter();
            spell.Write(writer, RenderContext.FromGlobals());
            var text = writer.ToString();
            var json = SpellInfoJsonSerializer.Serialize(spell, RenderContext.FromGlobals());

            if (Regen)
            {
                var srcDir = Path.Combine(FindTestProjectDir(), "Goldens");
                Directory.CreateDirectory(srcDir);
                File.WriteAllText(Path.Combine(srcDir, goldenName + ".txt"), Normalise(text));
                File.WriteAllText(Path.Combine(srcDir, goldenName + ".json"), Normalise(json));
                return;
            }

            var dir = Path.Combine(AppContext.BaseDirectory, "Goldens");
            var expectedText = File.ReadAllText(Path.Combine(dir, goldenName + ".txt"));
            var expectedJson = File.ReadAllText(Path.Combine(dir, goldenName + ".json"));

            Assert.Equal(Normalise(expectedText), Normalise(text));
            Assert.Equal(Normalise(expectedJson), Normalise(json));
        }

        private static string FindTestProjectDir()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "SpellWork.Core.Tests.csproj")))
                d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("test project dir not found");
        }

        // RichTextBox.Text yields \n; StringBuilder + Environment.NewLine yields \r\n.
        // Without this every golden fails for a reason unrelated to the refactor.
        private static string Normalise(string text) => text.Replace("\r\n", "\n");
    }
}
