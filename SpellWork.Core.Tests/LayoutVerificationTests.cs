using System.Linq;
using System.Reflection;
using DBFileReaderLib.Attributes;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class LayoutVerificationTests
    {
        private readonly ClientDataFixture _fixture;
        public LayoutVerificationTests(ClientDataFixture fixture) => _fixture = fixture;

        [Fact]
        public void EveryStoreHasAnExpectedLayout()
        {
            var missing = DBC.DBC.StoreFileNames().Where(n => !LayoutVerification.Expected.ContainsKey(n)).ToList();
            Assert.Empty(missing);
        }

        // DBFileReaderLib keys WDBC/WDB2 rows by record position unless the
        // ID field is [Index(true)]; leaving it off yields plausible wrong rows.
        [Fact]
        public void EveryEntryTypeIndexesItsIdField()
        {
            foreach (var t in DBC.DBC.StoreEntryTypes())
            {
                var id = t.GetField("ID", BindingFlags.Public | BindingFlags.Instance);
                Assert.True(id != null, $"{t.Name} has no ID field");
                Assert.True(t.GetFields(BindingFlags.Public | BindingFlags.Instance).First().Name == "ID", $"{t.Name}: ID is not the first field");
                var index = id.GetCustomAttribute<IndexAttribute>();
                Assert.True(index != null && index.NonInline, $"{t.Name}.ID lacks [Index(true)]");
            }
        }

        [Fact]
        public void MatchingLayoutIsNotAMismatch() =>
            Assert.Null(LayoutVerification.Check("Spell", "x/Spell.dbc", 25, 100));

        [Fact]
        public void DifferentRecordSizeIsAMismatchNamingTheFile()
        {
            var m = LayoutVerification.Check("SpellVisual", "x/SpellVisual.dbc", 38, 152);
            Assert.NotNull(m);
            Assert.Contains("SpellVisual.dbc", m.Describe());
            Assert.Equal(30, m.ExpectedFieldsCount);
            Assert.Equal(120, m.ExpectedRecordSize);
        }

        [SkippableFact]
        public void ReferenceDataHasNoMismatches()
        {
            _fixture.SkipIfNoClientData();
            Assert.Empty(DBC.DBC.LayoutMismatches.Select(m => m.Describe()));
        }

        [SkippableFact]
        public void StaleDbcBesideDb2IsNotTheOneOpened()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal("db2", DBC.DBC.OpenedFiles["SpellVisual"].Extension);
            Assert.Equal("db2", DBC.DBC.OpenedFiles["SpellVisualEffectName"].Extension);
        }
    }
}
