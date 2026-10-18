using System.Linq;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class LayoutVerificationTests
    {
        private static readonly string[] BringUp =
        {
            "SpellName", "Spell", "SpellMisc", "SpellEffect",
            "SpellClassOptions", "SpellDuration", "SpellRange", "Difficulty",
        };

        [Fact]
        public void Expected_CoversEveryBringUpStore()
        {
            foreach (var name in BringUp)
                Assert.True(LayoutVerification.Expected.ContainsKey(name), name);
            Assert.Equal(BringUp.Length, LayoutVerification.Expected.Count);
        }

        [Fact]
        public void Check_AllColumnsPresent_ReturnsNull()
        {
            foreach (var name in BringUp)
            {
                var cols = LayoutVerification.Expected[name].Columns.ToArray();
                Assert.Null(LayoutVerification.Check(name, cols));
            }
        }

        [Fact]
        public void Check_MissingColumn_NamesIt()
        {
            var cols = LayoutVerification.Expected["SpellRange"].Columns
                .Where(c => c != "DisplayNameShort_lang").ToArray();

            var mismatch = LayoutVerification.Check("SpellRange", cols);

            Assert.NotNull(mismatch);
            Assert.Equal(new[] { "DisplayNameShort_lang" }, mismatch.MissingColumns);
            Assert.Equal(6, mismatch.ExpectedFieldsCount);
            Assert.Equal(5, mismatch.ActualFieldsCount);
            Assert.Contains("DisplayNameShort_lang", mismatch.Describe());
        }

        [Fact]
        public void Check_UnlistedTable_ReturnsNull()
        {
            Assert.Null(LayoutVerification.Check("NotAModelledTable", new[] { "ID" }));
        }
    }
}
