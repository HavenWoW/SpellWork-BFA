using System.Collections.Generic;
using System.Linq;
using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // The chain: Spell.SpellVisualID[0..1] -> SpellVisual -> kits, missile
    // model, effect names, model attachments (spec section 5). The data
    // anchors below are still the 5.4.8 ones; Task 7 re-reads them off the
    // CoA reference data.
    public class SpellVisualChainResolverTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellVisualChainResolverTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact(Skip = "coa: Task 7")]
        public void FireballChain()
        {
            _fixture.SkipIfNoClientData();
            var section = SpellVisualChainResolver.ResolveSection(133);
            Assert.Empty(section.MissingStores);
            var chain = section.Chain;
            var v = Assert.Single(chain.Variants);
            Assert.Equal(0, v.Slot);
            Assert.Equal(67, v.SpellVisualID);
            Assert.Contains(v.KitSlots, k => k.Slot == "PrecastKit" && k.KitID == 30);
            Assert.Contains(v.KitSlots, k => k.Slot == "CastKit" && k.KitID == 38);
            Assert.Contains(v.KitSlots, k => k.Slot == "ImpactKit" && k.KitID == 286);
            var missile = Assert.Single(v.Missiles);
            Assert.Equal(88, missile.ID);
            Assert.Equal(@"Spells\Fireball_Missile_Low.mdx", missile.Name);
            var precast = chain.Kits.Single(k => k.KitID == 30);
            Assert.Contains(precast.Effects, e => e.Name == @"Spells\Fire_Precast_Hand.mdx");
            Assert.Contains("visual 67 (slot 0) PrecastKit", precast.ReachedVia);
        }

        [SkippableFact(Skip = "coa: Task 7")]
        public void FrostboltMissile()
        {
            _fixture.SkipIfNoClientData();
            var v = Assert.Single(SpellVisualChainResolver.ResolveSection(116).Chain.Variants);
            Assert.Equal(13, v.SpellVisualID);
            Assert.Equal(@"Spells\Frostbolt.mdx", Assert.Single(v.Missiles).Name);
        }

        [SkippableFact]
        public void KitSharedBySlotsIsListedOnce()
        {
            _fixture.SkipIfNoClientData();
            var chain = SpellVisualChainResolver.ResolveSection(133).Chain;
            Assert.Equal(chain.Kits.Select(k => k.KitID).Distinct().Count(), chain.Kits.Count);
        }

        [SkippableFact]
        public void SpellWithoutVisualHasNoVariants()
        {
            _fixture.SkipIfNoClientData();
            var id = DBC.DBC.SpellInfoStore.Values.First(s => s.Spell.SpellVisualID[0] == 0 && s.Spell.SpellVisualID[1] == 0).ID;
            var section = SpellVisualChainResolver.ResolveSection(id);
            Assert.Empty(section.MissingStores);
            Assert.Empty(section.Chain.Variants);
        }

        // ---- data-independent: an index over in-memory stores ----

        private static SpellVisualChainIndex InMemoryIndex(
            IEnumerable<SpellVisualEntry> visuals,
            IEnumerable<SpellVisualKitEntry> kits,
            IEnumerable<SpellVisualEffectNameEntry> effectNames = null,
            IEnumerable<SpellVisualKitModelAttachEntry> attachments = null,
            IEnumerable<SpellMissileMotionEntry> motions = null)
        {
            return SpellVisualChainIndex.BuildFrom(
                visuals.ToDictionary(v => v.ID),
                kits.ToDictionary(k => k.ID),
                (effectNames ?? Enumerable.Empty<SpellVisualEffectNameEntry>()).ToDictionary(e => e.ID),
                (attachments ?? Enumerable.Empty<SpellVisualKitModelAttachEntry>()).ToDictionary(a => a.ID),
                (motions ?? Enumerable.Empty<SpellMissileMotionEntry>()).ToDictionary(m => m.ID));
        }

        [Fact]
        public void MissingKitIdIsReportedWithItsProvenance()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, PrecastKit = 10, CastKit = 999 } },
                new[] { new SpellVisualKitEntry { ID = 10 } });

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500);

            Assert.Contains("SpellVisualKit#999 (from SpellVisual#1.CastKit)", chain.UnresolvedReferences);
            Assert.False(chain.Truncated);
            var kit = Assert.Single(chain.Kits);
            Assert.Equal(10, kit.KitID);
            // The raw slot reference is kept even though the kit did not resolve.
            Assert.Contains(Assert.Single(chain.Variants).KitSlots, k => k.Slot == "CastKit" && k.KitID == 999);
        }

        [Fact]
        public void KitCapSetsTruncated()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, PrecastKit = 10, CastKit = 11 } },
                new[] { new SpellVisualKitEntry { ID = 10 }, new SpellVisualKitEntry { ID = 11 } });

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 1);

            Assert.True(chain.Truncated);
            Assert.Single(chain.Kits);
            Assert.Empty(chain.UnresolvedReferences);
        }

        [Fact]
        public void KitSharedAcrossVariantsAndSlotsIsListedOnceWithEveryPath()
        {
            var index = InMemoryIndex(
                new[]
                {
                    new SpellVisualEntry { ID = 1, PrecastKit = 10, CastKit = 10 },
                    new SpellVisualEntry { ID = 2, ImpactKit = 10 },
                },
                new[] { new SpellVisualKitEntry { ID = 10, HeadEffect = 7, SpecialEffect = new[] { 0, 8, 0 } } },
                effectNames: new[] { new SpellVisualEffectNameEntry { ID = 7, Name = "head.mdx" } },
                attachments: new[] { new SpellVisualKitModelAttachEntry { ID = 3, ParentSpellVisualKitID = 10, AttachmentID = 1, SpellVisualEffectNameID = 7 } });

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 2 }, 500);

            Assert.Equal(new[] { 0, 1 }, chain.Variants.Select(v => v.Slot));
            var kit = Assert.Single(chain.Kits);
            Assert.Equal(new[] { "visual 1 (slot 0) PrecastKit", "visual 1 (slot 0) CastKit", "visual 2 (slot 1) ImpactKit" }, kit.ReachedVia);
            Assert.Equal(new[] { "Head", "Special1" }, kit.Effects.Select(e => e.Slot));
            Assert.Equal("head.mdx", kit.Effects[0].Name);
            Assert.Null(kit.Effects[1].Name);
            var attach = Assert.Single(kit.ModelAttachments);
            Assert.Equal("head.mdx", attach.Name);
            Assert.Equal(new[] { "SpellVisualEffectName#8 (from SpellVisualKit#10.Special1)" }, chain.UnresolvedReferences);
        }

        // Interim 3.3.5 mapping until Task 7 verifies the columns on data:
        // the missile is SpellVisual.MissileModel, MissileMotion is raw.
        [Fact]
        public void MissileModelResolvesAndMissileMotionIsRaw()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, MissileModel = 7, MissileMotion = 77 } },
                new SpellVisualKitEntry[0],
                effectNames: new[] { new SpellVisualEffectNameEntry { ID = 7, Name = "bolt.mdx" } },
                motions: new[] { new SpellMissileMotionEntry { ID = 77, Name = "Spiral" } });

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500);

            var v = Assert.Single(chain.Variants);
            Assert.Equal("bolt.mdx", Assert.Single(v.Missiles).Name);
            Assert.Equal(77, v.Unk14MissileMotion);
            Assert.Empty(chain.UnresolvedReferences);
        }

        [Fact]
        public void Unk14MissileMotionIsEmittedRawOnlyWhenNonZero()
        {
            var withColumn = SpellVisualChainJson.Describe(new SpellVisualVariant
            {
                Slot = 0, SpellVisualID = 1, VisualResolved = true, Unk14MissileMotion = 77,
                KitSlots = new List<KitSlotRef>(), Missiles = new List<SpellVisualMissileInfo>(),
            });
            var without = SpellVisualChainJson.Describe(new SpellVisualVariant
            {
                Slot = 0, SpellVisualID = 1, VisualResolved = true,
                KitSlots = new List<KitSlotRef>(), Missiles = new List<SpellVisualMissileInfo>(),
            });

            Assert.Equal(77, withColumn["unk14MissileMotion"]);
            Assert.False(without.ContainsKey("unk14MissileMotion"));
            Assert.False(withColumn.ContainsKey("missileMotion"));
        }

        [Fact]
        public void UnresolvedVisualAndMissileModelAreReported()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, MissileModel = 4 } },
                new SpellVisualKitEntry[0]);

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 2 }, 500);

            Assert.True(chain.Variants[0].VisualResolved);
            Assert.False(chain.Variants[1].VisualResolved);
            Assert.Equal(new[]
            {
                "SpellVisualEffectName#4 (from SpellVisual#1.MissileModel)",
                "SpellVisual#2 (from Spell#5.SpellVisualID[1])",
            }, chain.UnresolvedReferences);
        }

        [Theory]
        [InlineData(0, "SpellVisual")]
        [InlineData(1, "SpellVisualKit")]
        [InlineData(3, "SpellVisualEffectName")]
        [InlineData(4, "SpellVisualKitModelAttach")]
        public void EachGatedStoreIsReportedWhenMissing(int which, string expected)
        {
            var index = SpellVisualChainIndex.BuildFrom(
                which == 0 ? null : new Dictionary<int, SpellVisualEntry>(),
                which == 1 ? null : new Dictionary<int, SpellVisualKitEntry>(),
                which == 3 ? null : new Dictionary<int, SpellVisualEffectNameEntry>(),
                which == 4 ? null : new Dictionary<int, SpellVisualKitModelAttachEntry>(),
                null);

            Assert.Equal(new[] { expected }, index.MissingStores);
            var section = SpellVisualChainResolver.ResolveSection(index, 5, new[] { 1, 0 }, 500);
            Assert.Equal(new[] { expected }, section.MissingStores);
            Assert.Null(section.Chain);
        }

        [Fact]
        public void SpellMissileMotionIsNotGated()
        {
            var index = SpellVisualChainIndex.BuildFrom(
                new Dictionary<int, SpellVisualEntry>(),
                new Dictionary<int, SpellVisualKitEntry>(),
                new Dictionary<int, SpellVisualEffectNameEntry>(),
                new Dictionary<int, SpellVisualKitModelAttachEntry>(),
                null);

            Assert.Empty(index.MissingStores);
        }

        [Fact]
        public void Current_ReusesTheIndexWhileTheStoresAreUnchanged()
        {
            var first = SpellVisualChainIndex.Current();
            var second = SpellVisualChainIndex.Current();
            Assert.Same(first, second);
            Assert.True(first.MatchesCurrentStores());
        }
    }
}
