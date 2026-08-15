using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class SpellVisualChainResolverTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellVisualChainResolverTests(ClientDataFixture fixture) => _fixture = fixture;

        // Fireball. Measured during planning: exactly one SpellXSpellVisual
        // row -- SpellVisualID 69095, DifficultyID 0, Priority 0,
        // Probability 1. One row, so do not use this spell for anything that
        // needs several variants.
        private const int Fireball = 133;

        [SkippableFact]
        public void Resolve_Fireball_FindsItsVisual()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);

            Assert.Equal(Fireball, chain.SpellID);
            Assert.NotEmpty(chain.Variants);
            Assert.Contains(chain.Variants, v => v.SpellVisualID == 69095);
            Assert.All(chain.Variants, v => Assert.True(v.VisualResolved,
                $"SpellVisualID {v.SpellVisualID} did not resolve in SpellVisual."));
        }

        [SkippableFact]
        public void Resolve_Fireball_ReachesKits()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);
            var kits = chain.Variants.SelectMany(v => v.Kits).ToList();

            Assert.NotEmpty(kits);
            // Every kit id we report must exist -- reporting an id we could
            // not resolve as though it were a kit is the failure mode that
            // makes the whole tool untrustworthy.
            Assert.All(kits, k => Assert.True(DBC.DBC.SpellVisualKit.ContainsKey(k.KitID)));
        }

        // Measured against this build's real client data (temporary
        // diagnostic, deleted afterward): Fireball's variant (SpellVisualID
        // 69095) resolves MissileID 10539, MotionID 3932, and MotionName
        // "Mage - Fire - Fireball" -- none of these were assumed. MissileID
        // is asserted to resolve in SpellVisualMissile (not merely
        // non-zero) specifically because it is deliberately a
        // SpellVisualMissile row id and not a SpellMissile id -- see
        // SpellMissileInfo.MissileID's own doc comment -- so confirming it
        // resolves in the right table is the only way to show it is a real
        // row rather than just a nonzero integer.
        [SkippableFact]
        public void Resolve_Fireball_ResolvesItsMissileInfo()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);
            var variant = chain.Variants.Single(v => v.SpellVisualID == 69095);

            Assert.NotNull(variant.Missile);
            Assert.NotEqual(0, variant.Missile.MissileID);
            Assert.True(DBC.DBC.SpellVisualMissile.ContainsKey(variant.Missile.MissileID),
                $"MissileID {variant.Missile.MissileID} does not resolve in SpellVisualMissile.");
            Assert.NotEqual(0, variant.Missile.MotionID);
            Assert.Equal("Mage - Fire - Fireball", variant.Missile.MotionName);
        }

        // "Volley". Measured across this build's real data specifically to
        // give this test something real to catch: of 84693 SpellVisual rows,
        // 2119 have their MissileTargetingKit ("visual" direction) ALSO
        // reachable via a SpellVisualEvent row on the SAME visual ("event"
        // direction) -- a genuine same-variant collision, affecting 2476
        // distinct spells in total. Spell 220582 is one of them and is the
        // simplest case found: exactly one SpellXSpellVisual row (visual
        // 3300, difficulty 0), whose MissileTargetingKit is 74630, and whose
        // one SpellVisualEvent row also carries SpellVisualKitID 74630 --
        // same id, both directions, one variant. Not a synthetic case: if
        // GatherKits' seenInVariant dedup were deleted, this spell's one
        // variant would report kit 74630 twice.
        private const int KitCollisionSpell = 220582;
        private const int KitCollisionSharedKitId = 74630;

        [SkippableFact]
        public void Resolve_DeduplicatesKitsReachedFromMoreThanOneDirection()
        {
            _fixture.SkipIfNoClientData();
            Skip.IfNot(DBC.DBC.SpellInfoStore.ContainsKey(KitCollisionSpell),
                $"Spell {KitCollisionSpell} is absent from this build.");

            var chain = SpellVisualChainResolver.Resolve(KitCollisionSpell);
            var variant = Assert.Single(chain.Variants);
            var ids = variant.Kits.Select(k => k.KitID).ToList();

            // The measured collision must actually be present, or this test
            // is not exercising what its comment claims -- fail loudly here
            // rather than let a changed build silently turn this vacuous.
            Assert.Contains(KitCollisionSharedKitId, ids);
            Assert.Equal(1, ids.Count(id => id == KitCollisionSharedKitId));
            Assert.Equal(ids.Count, ids.Distinct().Count());

            // The surviving entry is the one the *first* direction found, and
            // the two directions are walked in a fixed order. This is also the
            // only test that pins a "visual"-reached kit: Fireball's
            // MissileTargetingKit is 0, so Resolve_RecordsHowEachKitWasReached
            // only ever sees "event".
            Assert.Equal("visual", variant.Kits.Single(k => k.KitID == KitCollisionSharedKitId).ReachedVia);

            // Fireball is kept as a secondary, defensive check only -- it
            // never exercises a real collision (its MissileTargetingKit is 0,
            // and its 3 event-sourced kit ids are already distinct from each
            // other), so on its own it cannot distinguish a working dedup
            // from a deleted one. The KitCollisionSpell case above is what
            // makes this test mean something.
            var fireballChain = SpellVisualChainResolver.Resolve(Fireball);
            foreach (var fireballVariant in fireballChain.Variants)
            {
                var fireballIds = fireballVariant.Kits.Select(k => k.KitID).ToList();
                Assert.Equal(fireballIds.Count, fireballIds.Distinct().Count());
            }
        }

        [SkippableFact]
        public void Resolve_RecordsHowEachKitWasReached()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);
            var kits = chain.Variants.SelectMany(v => v.Kits).ToList();

            // Assert.All over an empty list passes: without this the whole
            // assertion below evaporates the moment kit resolution regresses.
            Assert.NotEmpty(kits);

            // Two values, not three. SpellVisualMissile carries no reference to
            // SpellVisualKit at all (its only "Kit"-named column points at the
            // unrelated AnimKit table), so a "missile" direction is not merely
            // unpopulated in this build -- it is unrepresentable in this schema,
            // and admitting it here would let a real regression to zero-of-three
            // hide behind a set that was never producible. Same two-element set
            // SpellVisualChainJsonTests pins for the JSON key; they described
            // different vocabularies for one field until this was corrected.
            Assert.All(kits, k => Assert.Contains(k.ReachedVia, new[] { "visual", "event" }));

            // Fireball's MissileTargetingKit is 0, so every kit it reaches comes
            // from the event direction. Pinning that stops the assertion above
            // from being satisfied by a resolver that had stopped setting
            // ReachedVia at all and left it at some single constant.
            Assert.All(kits, k => Assert.Equal("event", k.ReachedVia));
        }

        [SkippableFact]
        public void Resolve_UnknownSpell_ReturnsEmptyRatherThanThrowing()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(999999999);

            Assert.Empty(chain.Variants);
            Assert.False(chain.Truncated);
        }

        // NOT Fireball. Measured: spell 133 has exactly ONE SpellXSpellVisual
        // row (visual 69095, difficulty 0), so `all >= zero` would be 1 >= 1 --
        // passing while proving nothing. Spell 282039 has five rows spanning
        // difficulties 0, 14, 15, 16 and 17; 106 spells in this build have
        // more than one distinct difficulty.
        private const int MultiDifficultySpell = 282039;

        [SkippableFact]
        public void Resolve_DifficultyFilter_NarrowsTheVariants()
        {
            _fixture.SkipIfNoClientData();
            Skip.IfNot(DBC.DBC.SpellInfoStore.ContainsKey(MultiDifficultySpell),
                $"Spell {MultiDifficultySpell} is absent from this build.");

            var all = SpellVisualChainResolver.Resolve(MultiDifficultySpell, difficulty: -1);
            var zero = SpellVisualChainResolver.Resolve(MultiDifficultySpell, difficulty: 0);

            Assert.All(zero.Variants, v => Assert.Equal(0, v.DifficultyID));
            Assert.True(all.Variants.Count > zero.Variants.Count,
                $"difficulty -1 returned {all.Variants.Count} variants and difficulty 0 returned " +
                $"{zero.Variants.Count}; the filter is not narrowing anything.");
        }

        // The cap has to bite on a real budget, not on an empty input.
        [SkippableFact]
        public void Resolve_MaxKits_CapsAndFlagsTruncation()
        {
            _fixture.SkipIfNoClientData();

            // Measured: Fireball reaches three kits, all event-sourced. The
            // precondition is asserted, not skipped on: Fireball losing its
            // kits is a regression in the thing this whole file tests, and
            // turning that into a green skip is how a broken resolver ships
            // with a clean run. Skip.If belongs on data that legitimately
            // varies between builds, not on the premise of the assertion.
            var uncapped = SpellVisualChainResolver.Resolve(Fireball).Variants.SelectMany(v => v.Kits).Count();
            Assert.True(uncapped > 1,
                $"Fireball resolved {uncapped} kits uncapped; the cap below cannot bite, and " +
                "one kit or fewer means kit resolution itself has regressed.");

            var capped = SpellVisualChainResolver.Resolve(Fireball, maxKits: 1);
            var kits = capped.Variants.SelectMany(v => v.Kits).Count();

            // Exactly one, not "at most one": zero would also satisfy a <= and
            // would mean the budget refused a kit it had room for.
            Assert.Equal(1, kits);
            Assert.True(capped.Truncated);
        }
    }

    /// <summary>
    /// Addition B from the task instructions: every <c>Storage&lt;T&gt;</c>
    /// on <c>DBC.DBC</c> is null until client data loads, and the MCP host
    /// (Task 6) can call this resolver in exactly that state -- a
    /// null-dereference there would surface as a crashed tool call. This is
    /// a plain, unskippable <see cref="Fact"/>: no <see cref="ClientDataFixture"/>,
    /// no client data, so it runs (and must pass) in every configuration.
    /// <para>
    /// <b>Honesty about what this actually proves, in-process:</b>
    /// <c>DBC.DBC</c>'s <c>Storage&lt;T&gt;</c> properties are static and
    /// process-wide (see <see cref="ClientDataFixture"/>'s own remarks), and
    /// every other test class in this assembly shares one
    /// <c>IClassFixture&lt;ClientDataFixture&gt;</c> load-once instance. In a
    /// full-suite run <em>without</em> <c>SPELLWORK_DBC_PATH</c> set, nothing
    /// in the process ever calls <c>DBC.DBC.LoadAsync</c>, so this test
    /// genuinely exercises the null-store guard -- delete the guard in
    /// <see cref="SpellVisualChainResolver.Resolve"/> and this test fails
    /// with a <see cref="System.NullReferenceException"/>, confirmed by
    /// running it that way. In a full-suite run <em>with</em> client data
    /// configured, some other test class's fixture is very likely to have
    /// already populated those same static stores by the time this test
    /// executes (xunit does not guarantee this runs first), so a pass in
    /// that configuration is not proof the guard fired -- it is proof the
    /// query for a spell id that cannot exist returned nothing, which is
    /// true either way. The spell id below is deliberately not a real spell
    /// (unlike <c>Resolve_UnknownSpell_ReturnsEmptyRatherThanThrowing</c>
    /// above, whose purpose is different: that one requires loaded data via
    /// <see cref="ClientDataFixture.SkipIfNoClientData"/> and asserts the
    /// per-id miss path on a real store) so that this test cannot become
    /// flaky depending on which configuration or test ordering runs it.
    /// </para>
    /// </summary>
    public class SpellVisualChainResolverUnloadedStoreTests
    {
        private const int NoSuchSpell = -1;

        [Fact]
        public void Resolve_UnloadedStores_ReturnsEmptyRatherThanThrowing()
        {
            var chain = SpellVisualChainResolver.Resolve(NoSuchSpell);

            Assert.Empty(chain.Variants);
            Assert.False(chain.Truncated);
            Assert.Empty(chain.UnresolvedReferences);
        }
    }
}
