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

        // Spec §4.1, read from the CoA reference extraction's WDBC headers.
        [Fact]
        public void Expected_matches_spec_table()
        {
            Assert.Equal((234, 936), LayoutVerification.Expected["Spell"]);
            Assert.Equal((32, 128), LayoutVerification.Expected["SpellVisual"]);
            Assert.Equal((38, 152), LayoutVerification.Expected["SpellVisualKit"]);
            Assert.Equal((48, 177), LayoutVerification.Expected["SpellChainEffects"]);
            Assert.Equal((3, 12), LayoutVerification.Expected["SpellAffect"]);
            Assert.Equal(
                DBC.DBC.StoreFileNames().OrderBy(n => n, System.StringComparer.Ordinal),
                LayoutVerification.Expected.Keys.OrderBy(n => n, System.StringComparer.Ordinal));
        }

        // DBFileReaderLib keys WDBC rows by record position unless the ID
        // field is [Index(true)]; leaving it off yields plausible wrong rows.
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

        // The structure itself must add up to the recorded layout: WDBC rows
        // are read field by field, so one missing or extra column shifts
        // every later field. Field count counts array elements; the record
        // size sums the element sizes (string = 4-byte offset).
        [Fact]
        public void EveryStructureSumsToItsExpectedLayout()
        {
            var wrong = new System.Collections.Generic.List<string>();
            foreach (var p in typeof(DBC.DBC).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                if (!p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() != typeof(DBFileReaderLib.Storage<>))
                    continue;

                int fields = 0, bytes = 0;
                foreach (var f in p.PropertyType.GetGenericArguments()[0].GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var count = f.GetCustomAttribute<CardinalityAttribute>()?.Count ?? 1;
                    var element = f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType;
                    var size = element == typeof(string) ? 4 : System.Runtime.InteropServices.Marshal.SizeOf(element);
                    fields += count;
                    bytes += count * size;
                }

                var expected = LayoutVerification.Expected[DBC.DBC.FileNameFor(p)];
                if (expected != (fields, bytes))
                    wrong.Add($"{p.Name}: structure {fields}/{bytes}, expected {expected.FieldsCount}/{expected.RecordSize}");
            }

            Assert.Empty(wrong);
        }

        [Fact]
        public void MatchingLayoutIsNotAMismatch() =>
            Assert.Null(LayoutVerification.Check("Spell", "x/Spell.dbc", 234, 936));

        [Fact]
        public void DifferentRecordSizeIsAMismatchNamingTheFileAndBuild()
        {
            var m = LayoutVerification.Check("SpellVisual", "x/SpellVisual.dbc", 30, 120);
            Assert.NotNull(m);
            Assert.Contains("SpellVisual.dbc", m.Describe());
            Assert.Contains("CoA 3.3.5a (12340)", m.Describe());
            Assert.Equal(32, m.ExpectedFieldsCount);
            Assert.Equal(128, m.ExpectedRecordSize);
        }

        [SkippableFact]
        public void ReferenceDataHasNoMismatches()
        {
            _fixture.SkipIfNoClientData();
            Assert.Empty(DBC.DBC.LayoutMismatches.Select(m => m.Describe()));
        }

        [SkippableFact]
        public void EveryStoreLoadsFromADbc()
        {
            _fixture.SkipIfNoClientData();
            Assert.Empty(DBC.DBC.UnloadedStores);
            Assert.All(DBC.DBC.OpenedFiles.Values, f => Assert.Equal("dbc", f.Extension));
        }
    }
}
