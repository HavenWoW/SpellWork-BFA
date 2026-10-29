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

        // Review Focus 1: the row exists but the mode's slot is 0.
        [Fact]
        public void ModeWithAZeroSlotResolvesToTheRequestedSpell()
        {
            var (resolved, variants) = Resolver().Resolve(100, 2);
            Assert.Equal(100, resolved);
            Assert.Equal(new[] { 100, 200, 400 }, variants);
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

            // Review Focus 1 on data: 25H slot is 0 -> the requested spell.
            Assert.Equal(354326, resolver.Resolve(354326, 3).ResolvedSpellId);
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
