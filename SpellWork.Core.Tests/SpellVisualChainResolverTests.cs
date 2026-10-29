using System.Collections.Generic;
using System.Linq;
using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // The chain: Spell.SpellVisualID[0..1] -> SpellVisual -> kits, missile
    // (MissileModel -> SpellVisualEffectName, MissileMotion ->
    // SpellMissileMotion), effect names, model attachments (spec section 5).
    // Data anchors are read off the CoA 3.3.5a reference data.
    public class SpellVisualChainResolverTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellVisualChainResolverTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
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
            Assert.Equal(365, missile.EffectNameID);
            Assert.Equal("Fireball Missile Low", missile.Name);
            Assert.Equal(@"Spells\Fireball_Missile_Low.mdx", missile.FileName);
            Assert.Equal(0, missile.MotionID);
            Assert.Null(missile.MotionName);
            var precast = chain.Kits.Single(k => k.KitID == 30);
            Assert.Contains(precast.Effects, e => e.Name == "Fire Precast Hand" && e.FileName == @"Spells\Fire_Precast_Hand.mdx");
            Assert.Contains("visual 67 (slot 0) PrecastKit", precast.ReachedVia);
            Assert.Empty(chain.UnresolvedReferences);
        }

        [SkippableFact]
        public void FrostboltMissile()
        {
            _fixture.SkipIfNoClientData();
            var v = Assert.Single(SpellVisualChainResolver.ResolveSection(116).Chain.Variants);
            Assert.Equal(13, v.SpellVisualID);
            var missile = Assert.Single(v.Missiles);
            Assert.Equal("Frost Bolt Missile", missile.Name);
            Assert.Equal(@"Spells\Frostbolt.mdx", missile.FileName);
        }

        // SpellVisual 272 (spell 29473): MissileModel 365, MissileMotion 13.
        [SkippableFact]
        public void MissileMotionResolvesOnData()
        {
            _fixture.SkipIfNoClientData();
            var v = Assert.Single(SpellVisualChainResolver.ResolveSection(29473).Chain.Variants);
            Assert.Equal(272, v.SpellVisualID);
            var missile = Assert.Single(v.Missiles);
            Assert.Equal(13, missile.MotionID);
            Assert.Equal("Parabola", missile.MotionName);
            Assert.Equal("Fireball Missile Low", missile.Name);
        }

        // SpellVisual 436 (spell 280570): MissileModel -1, a sentinel, not an id.
        [SkippableFact]
        public void NegativeMissileModelOnDataIsRawAndNotListed()
        {
            _fixture.SkipIfNoClientData();
            var chain = SpellVisualChainResolver.ResolveSection(280570).Chain;
            var v = Assert.Single(chain.Variants);
            Assert.Equal(436, v.SpellVisualID);
            var missile = Assert.Single(v.Missiles);
            Assert.Equal(-1, missile.EffectNameID);
            Assert.Null(missile.Name);
            Assert.DoesNotContain(chain.UnresolvedReferences, r => r.StartsWith("SpellVisualEffectName#-"));
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

        [Fact]
        public void MissileModelAndMotionResolve()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, MissileModel = 7, MissileMotion = 77 } },
                new SpellVisualKitEntry[0],
                effectNames: new[] { new SpellVisualEffectNameEntry { ID = 7, Name = "Bolt", FileName = @"Spells\Bolt.mdx" } },
                motions: new[] { new SpellMissileMotionEntry { ID = 77, Name = "Spiral" } });

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500);

            var missile = Assert.Single(Assert.Single(chain.Variants).Missiles);
            Assert.Equal(7, missile.EffectNameID);
            Assert.Equal("Bolt", missile.Name);
            Assert.Equal(@"Spells\Bolt.mdx", missile.FileName);
            Assert.Equal(77, missile.MotionID);
            Assert.Equal("Spiral", missile.MotionName);
            Assert.Empty(chain.UnresolvedReferences);
        }

        [Fact]
        public void MotionWithoutModelIsStillAMissile()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, MissileMotion = 77 } },
                new SpellVisualKitEntry[0],
                motions: new[] { new SpellMissileMotionEntry { ID = 77, Name = "Spiral" } });

            var missile = Assert.Single(Assert.Single(SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500).Variants).Missiles);
            Assert.Equal(0, missile.EffectNameID);
            Assert.Null(missile.Name);
            Assert.Equal("Spiral", missile.MotionName);
        }

        [Fact]
        public void VisualWithoutMissileColumnsHasNoMissile()
        {
            var index = InMemoryIndex(new[] { new SpellVisualEntry { ID = 1 } }, new SpellVisualKitEntry[0]);
            Assert.Empty(Assert.Single(SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500).Variants).Missiles);
        }

        [Fact]
        public void UnresolvedMissileMotionIsListed()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, MissileMotion = 78 } },
                new SpellVisualKitEntry[0]);

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500);

            Assert.Null(Assert.Single(Assert.Single(chain.Variants).Missiles).MotionName);
            Assert.Equal(new[] { "SpellMissileMotion#78 (from SpellVisual#1.MissileMotion)" }, chain.UnresolvedReferences);
        }

        // Negative MissileModel values (-1..-5 on the reference data) are not
        // SpellVisualEffectName ids: reported raw, never resolved or listed.
        [Fact]
        public void NegativeMissileModelIsRawAndNotListed()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, MissileModel = -1 } },
                new SpellVisualKitEntry[0]);

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500);

            var missile = Assert.Single(Assert.Single(chain.Variants).Missiles);
            Assert.Equal(-1, missile.EffectNameID);
            Assert.Null(missile.Name);
            Assert.Empty(chain.UnresolvedReferences);
        }

        [Fact]
        public void MissileJsonCarriesModelAndMotion_AndNoRawColumn14()
        {
            var json = SpellVisualChainJson.Describe(new SpellVisualVariant
            {
                Slot = 0, SpellVisualID = 1, VisualResolved = true,
                KitSlots = new List<KitSlotRef>(),
                Missiles = new List<SpellVisualMissileInfo>
                {
                    new SpellVisualMissileInfo { EffectNameID = 7, Name = "Bolt", FileName = "b.mdx", MotionID = 77, MotionName = "Spiral" },
                },
            });

            Assert.False(json.ContainsKey("unk14MissileMotion"));
            var missile = Assert.Single((List<Dictionary<string, object>>)json["missiles"]);
            Assert.Equal(new[] { "effectNameId", "name", "fileName", "motionId", "motionName" }, missile.Keys);
            Assert.Equal("b.mdx", missile["fileName"]);
            Assert.Equal("Spiral", missile["motionName"]);
        }

        [Fact]
        public void ModelAttachmentsAreFoundByParentKitOnly()
        {
            var index = InMemoryIndex(
                new[] { new SpellVisualEntry { ID = 1, PrecastKit = 10 } },
                new[] { new SpellVisualKitEntry { ID = 10 }, new SpellVisualKitEntry { ID = 11 } },
                effectNames: new[] { new SpellVisualEffectNameEntry { ID = 7, Name = "Glow", FileName = "glow.mdx" } },
                attachments: new[]
                {
                    new SpellVisualKitModelAttachEntry { ID = 5, ParentSpellVisualKitID = 10, AttachmentID = 2, SpellVisualEffectNameID = 7 },
                    new SpellVisualKitModelAttachEntry { ID = 4, ParentSpellVisualKitID = 10, AttachmentID = 1, SpellVisualEffectNameID = 9 },
                    new SpellVisualKitModelAttachEntry { ID = 6, ParentSpellVisualKitID = 11, AttachmentID = 1, SpellVisualEffectNameID = 7 },
                });

            var chain = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 0 }, 500);

            var kit = Assert.Single(chain.Kits);
            Assert.Equal(new[] { 4, 5 }, kit.ModelAttachments.Select(a => a.ID));
            Assert.Equal("glow.mdx", kit.ModelAttachments[1].FileName);
            Assert.Equal(new[] { "SpellVisualEffectName#9 (from SpellVisualKitModelAttach#4.SpellVisualEffectNameID)" }, chain.UnresolvedReferences);
        }

        [Fact]
        public void DefaultKitCapIs500_AndTheCapAppliesAcrossBothVariants()
        {
            Assert.Equal(500, SpellVisualChainResolver.DefaultMaxKits);

            // 24 distinct kits across two variants, cap 20: the first 20 are reported.
            var visuals = new[]
            {
                new SpellVisualEntry { ID = 1, PrecastKit = 1, CastKit = 2, ImpactKit = 3, StateKit = 4, StateDoneKit = 5, ChannelKit = 6,
                    CasterImpactKit = 7, TargetImpactKit = 8, MissileTargetingKit = 9, InstantAreaKit = 10, ImpactAreaKit = 11, PersistentAreaKit = 12 },
                new SpellVisualEntry { ID = 2, PrecastKit = 13, CastKit = 14, ImpactKit = 15, StateKit = 16, StateDoneKit = 17, ChannelKit = 18,
                    CasterImpactKit = 19, TargetImpactKit = 20, MissileTargetingKit = 21, InstantAreaKit = 22, ImpactAreaKit = 23, PersistentAreaKit = 24 },
            };
            var index = InMemoryIndex(visuals, Enumerable.Range(1, 24).Select(i => new SpellVisualKitEntry { ID = i }));

            var capped = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 2 }, 20);
            Assert.Equal(Enumerable.Range(1, 20), capped.Kits.Select(k => k.KitID));
            Assert.True(capped.Truncated);

            var full = SpellVisualChainResolver.Resolve(index, 5, new[] { 1, 2 }, SpellVisualChainResolver.DefaultMaxKits);
            Assert.Equal(24, full.Kits.Count);
            Assert.False(full.Truncated);
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
