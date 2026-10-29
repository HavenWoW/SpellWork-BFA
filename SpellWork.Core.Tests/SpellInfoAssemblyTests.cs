using System.Linq;
using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class SpellInfoAssemblyTests
    {
        private readonly ClientDataFixture _fixture;
        public SpellInfoAssemblyTests(ClientDataFixture fixture) => _fixture = fixture;

        // Fireball rank 1. Stock 3.3.5 has two effects; the CoA row carries
        // three (SCHOOL_DAMAGE, APPLY_AURA, APPLY_AURA), read off Spell.dbc.
        [SkippableFact]
        public void FireballAssembles()
        {
            _fixture.SkipIfNoClientData();
            var s = DBC.DBC.SpellInfoStore[133];
            Assert.Equal(4, s.SchoolMask);
            Assert.InRange(s.Effects.Count, 1, 3);
            Assert.Equal((int)SpellEffects.SPELL_EFFECT_SCHOOL_DAMAGE, s.Effects[0].Entry.Effect);
            Assert.Equal(new[] { 0, 1, 2 }, s.Effects.Select(e => e.Entry.EffectIndex));
            Assert.All(s.Effects, e => Assert.Equal(0, e.ResolvedDifficulty));
            Assert.All(s.Effects, e => Assert.Equal(133, e.Entry.SpellID));
            Assert.NotEqual(0, s.Spell.SpellVisualID[0]);
            Assert.Equal(8, s.Attributes.Length);
        }

        [SkippableFact]
        public void TriggerStoreIsBuilt()
        {
            _fixture.SkipIfNoClientData();
            Assert.NotEmpty(DBC.DBC.SpellTriggerStore);

            // Every listed source really triggers the key in a live effect slot.
            foreach (var pair in DBC.DBC.SpellTriggerStore.Take(200))
                foreach (var source in pair.Value)
                    Assert.Contains(DBC.DBC.SpellInfoStore[source].Effects, e => e.Entry.EffectTriggerSpell == pair.Key);
        }

        [SkippableFact]
        public void EverySpellRowHasASpellInfo()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal(DBC.DBC.Spell.Count, DBC.DBC.SpellInfoStore.Count);
        }
    }

    // Data-independent: one synthetic Spell row.
    public sealed class SpellInfoFromRowTests
    {
        private static SpellEntry Row()
        {
            var row = new SpellEntry { ID = 42 };
            row.Effect = new[] { 2, 0, 6 };
            row.EffectBasePoints = new[] { 9, 99, 4 };
            row.EffectDieSides = new[] { 1, 0, 3 };
            row.EffectRealPointsPerLevel = new[] { 0f, 0f, 2.5f };
            row.ImplicitTargetA = new[] { 6, 0, 1 };
            row.ImplicitTargetB = new[] { 0, 0, 15 };
            row.EffectRadiusIndex = new[] { 0, 0, 13 };
            row.EffectAura = new[] { 0, 0, 3 };
            row.EffectAuraPeriod = new[] { 0, 0, 3000 };
            row.EffectMiscValue = new[] { 0, 0, 11 };
            row.EffectMiscValueB = new[] { 0, 0, 12 };
            row.EffectTriggerSpell = new[] { 0, 0, 777 };
            row.EffectPointsPerCombo = new[] { 0f, 0f, 1.5f };
            row.EffectChainAmplitude = new[] { 1f, 0f, 0.5f };
            row.EffectBonusCoefficient = new[] { 0f, 0f, 0.2f };
            row.EffectSpellClassMaskA = new uint[] { 1, 2, 3 };
            row.EffectSpellClassMaskB = new uint[] { 4, 5, 6 };
            row.EffectSpellClassMaskC = new uint[] { 7, 8, 9 };
            row.Reagent = new[] { 0, 2589, 0, 0, 0, 0, 0, 0 };
            row.ReagentCount = new[] { 5, 2, 0, 0, 0, 0, 0, 0 };
            return row;
        }

        [Fact]
        public void OnlyNonZeroEffectSlotsAreEffects()
        {
            var info = new SpellInfo(Row());
            Assert.Equal(new[] { 0, 2 }, info.Effects.Select(e => e.Entry.EffectIndex));
            Assert.All(info.Effects, e => Assert.Equal(0, e.ResolvedDifficulty));
        }

        [Fact]
        public void EffectSlotReadsItsColumnGroup()
        {
            var e = new SpellInfo(Row()).Effects[1].Entry;
            Assert.Equal(42, e.SpellID);
            Assert.Equal(6, e.Effect);
            Assert.Equal(4, e.EffectBasePoints);
            Assert.Equal(3, e.EffectDieSides);
            Assert.Equal(2.5f, e.EffectRealPointsPerLevel);
            Assert.Equal(new[] { 1, 15 }, e.ImplicitTarget);
            Assert.Equal(13, e.EffectRadiusIndex);
            Assert.Equal(3, e.EffectAura);
            Assert.Equal(3000, e.EffectAuraPeriod);
            Assert.Equal(new[] { 11, 12 }, e.EffectMiscValue);
            Assert.Equal(777, e.EffectTriggerSpell);
            Assert.Equal(1.5f, e.EffectPointsPerCombo);
            Assert.Equal(0.5f, e.EffectChainAmplitude);
            Assert.Equal(0.2f, e.EffectBonusCoefficient);
        }

        // Effect i's mask is columns 122+3i..124+3i (CoA flag96 layout).
        [Fact]
        public void EffectClassMaskIsEffectMajor()
        {
            var info = new SpellInfo(Row());
            Assert.Equal(new uint[] { 1, 2, 3 }, info.Effects[0].Entry.EffectSpellClassMask);
            Assert.Equal(new uint[] { 7, 8, 9 }, info.Effects[1].Entry.EffectSpellClassMask);
        }

        [Fact]
        public void ReagentsSkipEmptySlots()
        {
            Assert.Equal(new[] { (2589, 2) }, new SpellInfo(Row()).Reagents);
        }

        [Fact]
        public void LookupRowsAreNullWithoutStores()
        {
            var info = new SpellInfo(Row());
            Assert.Null(info.RuneCost);
            Assert.Null(info.DurationEntry);
            Assert.Null(info.Range);
            Assert.Null(info.CastTimeEntry);
            Assert.Null(info.Icon);
            Assert.Null(info.Category);
            Assert.Null(info.DescriptionVariables);
            Assert.Equal(string.Empty, info.Name);
        }
    }
}
