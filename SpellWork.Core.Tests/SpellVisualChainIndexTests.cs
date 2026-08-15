using System;
using System.IO;
using DBFileReaderLib;
using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    /// <summary>
    /// The visual chain's lookups are cached across calls, so the question
    /// these tests exist to answer is not "is it fast" but "can it ever serve
    /// rows from a store that is no longer loaded". A stale index would be a
    /// correctness bug several orders of magnitude worse than the ~32 ms/spell
    /// the cache removes.
    /// </summary>
    public class SpellVisualChainIndexTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellVisualChainIndexTests(ClientDataFixture fixture) => _fixture = fixture;

        private const int Fireball = 133;

        [Fact]
        public void Current_ReusesTheIndexWhileTheStoresAreUnchanged()
        {
            // The whole point of the cache. Runs in both configurations: with
            // no client data every store is null, and null stores are just as
            // much a generation to be cached as loaded ones.
            var first = SpellVisualChainIndex.Current();
            var second = SpellVisualChainIndex.Current();

            Assert.Same(first, second);
            Assert.True(first.MatchesCurrentStores());
        }

        [SkippableFact]
        public void Current_RebuildsWhenAStoreInstanceIsReplaced()
        {
            _fixture.SkipIfNoClientData();

            // A second Storage<T> over the same .db2 file: a different object
            // holding identical rows. Identical content is deliberate --
            // DBC.DBC's statics are process-wide and other test classes read
            // them concurrently, so this swap has to be invisible to them. It
            // also makes the test check the property that actually matters: the
            // index is keyed on the store's *identity*, not on whether its
            // content happens to have changed. DBC.LoadAsync assigns a brand
            // new Storage<T> on every reload, so identity is exactly the signal
            // a reload produces, and content equality is exactly the case a
            // weaker design would get wrong.
            var replacement = new Storage<SpellXSpellVisualEntry>(Db2Path("SpellXSpellVisual"));
            var original = DBC.DBC.SpellXSpellVisual;
            Assert.NotSame(original, replacement);

            var before = SpellVisualChainIndex.Current();
            Assert.True(before.MatchesCurrentStores());

            try
            {
                DBC.DBC.SpellXSpellVisual = replacement;

                // Served stale would mean MatchesCurrentStores stayed true and
                // Current() handed back the index built over `original`.
                Assert.False(before.MatchesCurrentStores());

                var after = SpellVisualChainIndex.Current();
                Assert.NotSame(before, after);
                Assert.True(after.MatchesCurrentStores());

                // And the rebuilt index is a working one, not merely a new
                // object: rebuilding into something that resolves nothing would
                // satisfy every assertion above.
                Assert.NotEmpty(SpellVisualChainResolver.Resolve(Fireball).Variants);
            }
            finally
            {
                DBC.DBC.SpellXSpellVisual = original;
            }

            // Restoring is itself an instance change, so it must rebuild again
            // rather than resurrect the pre-swap index by luck.
            var restored = SpellVisualChainIndex.Current();
            Assert.True(restored.MatchesCurrentStores());
            Assert.NotEmpty(SpellVisualChainResolver.Resolve(Fireball).Variants);
        }

        // Each of the six stores whose absence the walk cannot signal any other
        // way, checked one at a time so a gate that quietly stopped covering
        // one of them fails here rather than in production. Built through
        // BuildFrom rather than by assigning to DBC.DBC: nulling a
        // process-wide store while other test classes resolve chains against
        // it would break them.
        [SkippableTheory]
        [InlineData("SpellXSpellVisual")]
        [InlineData("SpellVisual")]
        [InlineData("SpellVisualEvent")]
        [InlineData("SpellVisualMissile")]
        [InlineData("SpellVisualKit")]
        [InlineData("SpellVisualKitEffect")]
        public void MissingStores_NamesEachStoreTheWalkCannotDoWithout(string storeName)
        {
            _fixture.SkipIfNoClientData();

            var index = BuildWithout(storeName);

            Assert.Equal(new[] { storeName }, index.MissingStores);
        }

        [SkippableFact]
        public void MissingStores_DoesNotGateOnSpellMissileMotion()
        {
            _fixture.SkipIfNoClientData();

            // SpellMissileMotion is read by the walk but deliberately left out
            // of the gate: every id it cannot resolve is already recorded in
            // UnresolvedReferences, so it degrades visibly instead of silently.
            // Refusing a whole chain over one missing name lookup would be a
            // regression, so the exclusion is pinned here rather than left to
            // be "corrected" later by someone counting the stores the walk
            // touches.
            Assert.Empty(BuildWithout("SpellMissileMotion").MissingStores);
        }

        [SkippableFact]
        public void MissingStores_IsEmptyWithRealClientData()
        {
            _fixture.SkipIfNoClientData();

            // The gate must not be so broad that a complete extraction still
            // trips it -- that would turn get_spell's visual section into a
            // permanent "storeUnloaded" and this tool into a liar.
            Assert.Empty(SpellVisualChainResolver.MissingStores());
            Assert.Empty(BuildWithout(null).MissingStores);
        }

        // Every currently-loaded store except the named one, which is passed as
        // null. Reads DBC.DBC but never writes it -- which is the whole point:
        // it is the only way to exercise a missing store without breaking the
        // test classes running concurrently against the same statics. Shared
        // with SpellVisualSectionTests, which needs the same partial states to
        // drive both renderers through them; pass null for the complete set.
        internal static SpellVisualChainIndex BuildWithout(string storeName)
        {
            bool Absent(string candidate) => string.Equals(storeName, candidate, StringComparison.Ordinal);

            return SpellVisualChainIndex.BuildFrom(
                Absent("SpellXSpellVisual") ? null : DBC.DBC.SpellXSpellVisual,
                Absent("SpellVisual") ? null : DBC.DBC.SpellVisual,
                Absent("SpellVisualEvent") ? null : DBC.DBC.SpellVisualEvent,
                Absent("SpellVisualMissile") ? null : DBC.DBC.SpellVisualMissile,
                Absent("SpellVisualKit") ? null : DBC.DBC.SpellVisualKit,
                Absent("SpellVisualKitEffect") ? null : DBC.DBC.SpellVisualKitEffect,
                Absent("SpellMissileMotion") ? null : DBC.DBC.SpellMissileMotion);
        }

        private static string Db2Path(string name)
        {
            var dbcPath = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH");
            var locale = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS";
            return Path.Combine(dbcPath, locale, name + ".db2");
        }
    }
}
