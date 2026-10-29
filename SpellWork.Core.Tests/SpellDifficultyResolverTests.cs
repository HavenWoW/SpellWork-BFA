using System;
using System.Collections.Generic;
using SpellWork.DBC;
using SpellWork.DBC.Structures;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class SpellDifficultyResolverTests
    {
        private static SpellDifficultyResolver Resolver() => new SpellDifficultyResolver(
            new Dictionary<int, SpellDifficultyEntry>
            {
                [7] = new SpellDifficultyEntry { ID = 7, DifficultySpellID = new[] { 100, 200, 0, 400 } },
            });

        [Fact]
        public void ModeWithASpellResolvesToIt()
        {
            var (resolved, variants) = Resolver().Resolve(100, 1);
            Assert.Equal(200, resolved);
            Assert.Equal(new[] { 100, 200, 400 }, variants);
        }

        // SpellMgr.cpp:592-596: 10H with a 0 slot falls back to 10N (mode 0).
        [Fact]
        public void HeroicModeWithAZeroSlotFallsBackToNormalOfTheSameSize()
        {
            var (resolved, variants) = Resolver().Resolve(200, 2);
            Assert.Equal(100, resolved);
            Assert.Equal(new[] { 100, 200, 400 }, variants);
            Assert.Equal(100, Resolver().Resolve(100, 2).ResolvedSpellId);
        }

        // 25H with a 0 slot falls back to 25N (mode 1).
        [Fact]
        public void Mode3WithAZeroSlotFallsBackToMode1()
        {
            var resolver = new SpellDifficultyResolver(new Dictionary<int, SpellDifficultyEntry>
            {
                [9] = new SpellDifficultyEntry { ID = 9, DifficultySpellID = new[] { 10, 11, 12, 0 } },
            });
            Assert.Equal(11, resolver.Resolve(10, 3).ResolvedSpellId);
            Assert.Equal(12, resolver.Resolve(10, 2).ResolvedSpellId);
        }

        // DBCStores.cpp:527-536: an id with no Spell.dbc row is loaded as 0.
        [Fact]
        public void SpellMissingFromSpellDbcIsDropped()
        {
            var rows = new Dictionary<int, SpellDifficultyEntry>
            {
                [7] = new SpellDifficultyEntry { ID = 7, DifficultySpellID = new[] { 100, 200, 300, 400 } },
            };
            var resolver = new SpellDifficultyResolver(rows, id => id != 300);

            var (resolved, variants) = resolver.Resolve(100, 2);
            Assert.Equal(100, resolved); // slot 2 dropped -> mode 0
            Assert.Equal(new[] { 100, 200, 400 }, variants);
            Assert.Equal(0, resolver.RowFor(300));
        }

        // A dropped mode-1 spell leaves the row without "id0-1", so the row is ignored.
        [Fact]
        public void RowWhoseMode1SpellIsMissingIsIgnored()
        {
            var rows = new Dictionary<int, SpellDifficultyEntry>
            {
                [7] = new SpellDifficultyEntry { ID = 7, DifficultySpellID = new[] { 100, 200, 300, 400 } },
            };
            var (resolved, variants) = new SpellDifficultyResolver(rows, id => id != 200).Resolve(100, 1);
            Assert.Equal(100, resolved);
            Assert.Empty(variants);
        }

        [Fact]
        public void SpellWithoutARowResolvesToItselfWithNoVariants()
        {
            var (resolved, variants) = Resolver().Resolve(999, 3);
            Assert.Equal(999, resolved);
            Assert.Empty(variants);
        }

        // Any spell a row lists belongs to it, as in the CoA server's lookup.
        [Fact]
        public void AVariantResolvesThroughItsRow()
        {
            Assert.Equal(100, Resolver().Resolve(400, 0).ResolvedSpellId);
            Assert.Equal(7, Resolver().RowFor(200));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void ModeOutsideZeroToThreeThrows(int mode) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Resolver().Resolve(100, mode));

        // The server skips rows without both mode-0 and mode-1 spells.
        [Fact]
        public void RowWithoutModeZeroAndOneIsIgnored()
        {
            var resolver = new SpellDifficultyResolver(new Dictionary<int, SpellDifficultyEntry>
            {
                [8] = new SpellDifficultyEntry { ID = 8, DifficultySpellID = new[] { 300, 0, 301, 0 } },
            });
            var (resolved, variants) = resolver.Resolve(300, 2);
            Assert.Equal(300, resolved);
            Assert.Empty(variants);
        }

        [Fact]
        public void NoStoreResolvesEverySpellToItself() =>
            Assert.Equal(5, new SpellDifficultyResolver(null).Resolve(5, 3).ResolvedSpellId);
    }

    [Collection("ClientData")]
    public sealed class SpellDifficultyResolverDataTests
    {
        private readonly ClientDataFixture _fixture;
        public SpellDifficultyResolverDataTests(ClientDataFixture fixture) => _fixture = fixture;

        // SpellDifficulty row 1654 = (354326, 354448, 354449, 0).
        [SkippableFact]
        public void ReferenceRowResolves()
        {
            _fixture.SkipIfNoClientData();
            var resolver = DBC.DBC.DifficultyResolver;
            Assert.Equal(1654, resolver.RowFor(354326));
            Assert.Equal(354448, resolver.Resolve(354326, 1).ResolvedSpellId);
            Assert.Equal(new[] { 354326, 354448, 354449 }, resolver.Resolve(354326, 0).Variants);

            // 25H slot is 0 -> falls back to 25N (SpellMgr.cpp:592-596).
            Assert.Equal(354448, resolver.Resolve(354326, 3).ResolvedSpellId);

            // Row 1 = (113, 142182, 369533, 0).
            Assert.Equal(142182, resolver.Resolve(113, 3).ResolvedSpellId);
        }

        // Row 1908 = (2100635, 2100636, 2100637, 2100637); 2100636 has no
        // Spell.dbc row, so mode 1 is 0 and the server skips the whole row.
        [SkippableFact]
        public void RowNamingAMissingSpellIsFiltered()
        {
            _fixture.SkipIfNoClientData();
            Assert.False(DBC.DBC.Spell.ContainsKey(2100636));
            var (resolved, variants) = DBC.DBC.DifficultyResolver.Resolve(2100635, 1);
            Assert.Equal(2100635, resolved);
            Assert.DoesNotContain(2100636, variants);
        }

        // Spell 16 (Hellfire) names row 1654 in its own SpellDifficultyID
        // column but is not listed by it; the server ignores the column.
        [SkippableFact]
        public void SpellDifficultyIdColumnIsNotFollowed()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal(1654, DBC.DBC.Spell[16].SpellDifficultyID);
            var (resolved, variants) = DBC.DBC.DifficultyResolver.Resolve(16, 1);
            Assert.Equal(16, resolved);
            Assert.Empty(variants);
        }
    }
}
