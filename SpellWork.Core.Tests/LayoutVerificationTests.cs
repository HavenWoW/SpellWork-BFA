using System;
using System.IO;
using DBFileReaderLib;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class LayoutVerificationTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public LayoutVerificationTests(ClientDataFixture fixture) => _fixture = fixture;

        // A table nobody has written an expected layout for must not be
        // reported as a mismatch. Most modelled structures still have no entry
        // in Expected -- only the eleven visual-chain tables pinned by
        // Expected_CoversEveryVisualChainTable do -- and reporting every
        // unlisted one would bury the real signal.
        [Fact]
        public void Check_UnknownTable_ReturnsNull()
        {
            Assert.Null(LayoutVerification.Check("NoSuchTable", 0xDEADBEEF, 99));
        }

        [Fact]
        public void Check_MatchingLayout_ReturnsNull()
        {
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];
            Assert.Null(LayoutVerification.Check(
                "SpellXSpellVisual", expected.LayoutHash, expected.FieldsCount));
        }

        [Fact]
        public void Check_WrongLayoutHash_ReportsBothValues()
        {
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];
            var mismatch = LayoutVerification.Check("SpellXSpellVisual", 0x11112222, expected.FieldsCount);

            Assert.NotNull(mismatch);
            Assert.Equal(0x11112222u, mismatch.ActualLayoutHash);
            Assert.Equal(expected.LayoutHash, mismatch.ExpectedLayoutHash);
            // The message has to carry both hashes: "SpellXSpellVisual has the
            // wrong layout" sends the reader to WoWDBDefs with nothing to
            // search for.
            Assert.Contains("11112222", mismatch.Describe());
            Assert.Contains(expected.LayoutHash.ToString("X8"), mismatch.Describe());
        }

        [Fact]
        public void Check_WrongFieldCount_IsReportedEvenWhenHashMatches()
        {
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];
            var mismatch = LayoutVerification.Check(
                "SpellXSpellVisual", expected.LayoutHash, expected.FieldsCount + 1);

            Assert.NotNull(mismatch);
            Assert.Equal(expected.FieldsCount + 1, mismatch.ActualFieldsCount);
        }

        // Every .db2 that opened during the load was checked against Expected as
        // it opened, and none disagreed. If this fails, the client data changed
        // -- do not "fix" it by editing Expected.
        [SkippableFact]
        public void ReferenceBuild_HasNoLayoutMismatches()
        {
            _fixture.SkipIfNoClientData();
            Assert.Empty(DBC.DBC.LayoutMismatches);
        }

        // Reads the .db2 itself rather than asking Expected what Expected says.
        // This assertion used to compare LayoutVerification.Expected
        // ["SpellXSpellVisual"] against the two literals that define it one
        // file away -- constant against constant, gated behind
        // SkipIfNoClientData despite reading no client data, so it skipped in
        // one configuration and proved nothing in the other.
        //
        // It is not redundant with ReferenceBuild_HasNoLayoutMismatches, which
        // asserts an empty LayoutMismatches list: nothing is recorded there for
        // a store whose file failed to open, so that test also passes when
        // SpellXSpellVisual.db2 is absent entirely. This one opens the file.
        [SkippableFact]
        public void SpellXSpellVisual_MatchesMeasuredLayout()
        {
            _fixture.SkipIfNoClientData();

            var reader = new DBReader(Db2Path("SpellXSpellVisual"));
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];

            Assert.Equal(expected.LayoutHash, reader.LayoutHash);
            Assert.Equal(expected.FieldsCount, reader.FieldsCount);

            // The measured values for the reference build, pinned so a change
            // to Expected that happened to match a changed file still shows up
            // as a deliberate edit rather than a silent one.
            Assert.Equal(0x8D0B1C8Fu, reader.LayoutHash);
            Assert.Equal(12, reader.FieldsCount);
        }

        private static string Db2Path(string name)
        {
            var dbcPath = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH");
            var locale = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS";
            return Path.Combine(dbcPath, locale, name + ".db2");
        }

        // Needs no client data: this checks the static Expected table itself,
        // not anything read from a .db2 file. Closes out the plan's eleven-
        // table ground truth -- if any of these is missing, that table's
        // field order is never verified against the file it was written for.
        [Fact]
        public void Expected_CoversEveryVisualChainTable()
        {
            var required = new[]
            {
                "SpellXSpellVisual", "SpellMissile", "SpellVisual", "SpellVisualEvent",
                "SpellVisualMissile", "SpellVisualKit", "SpellVisualKitEffect",
                "SpellVisualEffectName", "SpellVisualKitModelAttach", "SpellChainEffects",
                "SpellMissileMotion",
            };
            foreach (var table in required)
                Assert.True(LayoutVerification.Expected.ContainsKey(table),
                    $"{table} has no recorded expected layout, so nothing verifies its field order.");
        }
    }
}
