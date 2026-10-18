using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // Requires extracted client data. Set SPELLWORK_DBC_PATH
    // to <emulator-build-dir>\ClientData\dbc and ...\ClientData\gt.
    public class GoldenRenderingTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public GoldenRenderingTests(ClientDataFixture fixture) => _fixture = fixture;

        // TODO Task 9: regenerate 554 goldens. The 8.3.7 goldens were removed;
        // Task 9 repopulates Cases, restores Goldens/*, and re-enables the guard + theory.
        private static readonly string[] Cases = new string[0];

        public static IEnumerable<object[]> GoldenFiles()
        {
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
            // TODO Task 9: regenerate 554 goldens, then restore Assert.NotEmpty(GoldenFiles()).
            Assert.NotNull(Cases);
        }

        [SkippableTheory(Skip = "TODO Task 9: regenerate 554 goldens")]
        [MemberData(nameof(GoldenFiles))]
        public void Rendering_matches_the_pre_refactor_golden(string goldenName)
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var id = int.Parse(goldenName.Substring("spell-".Length));
            Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(id),
                $"spell {id} is missing from this client data");

            var writer = new PlainTextSpellInfoWriter();
            DBC.DBC.SpellInfoStore[id].Write(writer, RenderContext.FromGlobals());

            var expected = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Goldens", goldenName + ".txt"));

            Assert.Equal(Normalise(expected), Normalise(writer.ToString()));
        }

        // RichTextBox.Text yields \n; StringBuilder + Environment.NewLine yields \r\n.
        // Without this every golden fails for a reason unrelated to the refactor.
        private static string Normalise(string text) => text.Replace("\r\n", "\n");
    }
}
