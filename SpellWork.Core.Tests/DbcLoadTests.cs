using System;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class DbcLoadTests
    {
        private readonly ClientDataFixture _fixture;
        public DbcLoadTests(ClientDataFixture fixture) => _fixture = fixture;

        private static bool EnUs =>
            LocaleSlots.Resolve(Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS", out _) == 0;

        [SkippableFact]
        public void SpellIsKeyedById()
        {
            _fixture.SkipIfNoClientData();
            Skip.IfNot(EnUs, "names are asserted in the enUS slot");
            Assert.Equal("Fireball", DBC.DBC.Spell[133].Name);
            Assert.Equal("Shadow Word: Pain", DBC.DBC.Spell[589].Name);
        }

        // Header record counts of the CoA reference extraction.
        [SkippableFact]
        public void ReferenceRowCounts()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal(209510, DBC.DBC.Spell.Count);
            Assert.Equal(23148, DBC.DBC.SpellVisual.Count);
            Assert.Equal(29584, DBC.DBC.SpellVisualKit.Count);
            Assert.Equal(18742, DBC.DBC.SpellVisualEffectName.Count);
            Assert.Equal(3811, DBC.DBC.SpellDifficulty.Count);
            Assert.Equal(36849, DBC.DBC.SpellAffect.Count);
            Assert.Equal(6636, DBC.DBC.SpellChainEffects.Count);
            Assert.Equal(563769, DBC.DBC.Item.Count);
        }

        // Raw columns of Spell.dbc row 133, read off the file by hand.
        [SkippableFact]
        public void FireballRow()
        {
            _fixture.SkipIfNoClientData();
            var row = DBC.DBC.Spell[133];
            Assert.Equal(4, row.SchoolMask);
            Assert.Equal(67, row.SpellVisualID[0]);
            Assert.Equal(0, row.SpellVisualID[1]);
            Assert.Equal(new[] { 2, 6, 6 }, row.Effect);
            Assert.Equal(16712190u, row.NameLangFlags);
            if (EnUs)
            {
                Assert.Equal("Rank 1", row.NameSubtext);
                Assert.StartsWith("Hurls a fiery ball", row.Description);
            }
        }

        // The 177-byte layout: five packed uint8 columns at bytes 156-160,
        // then Combo (string), RenderLayer, TextureLength, WavePhase.
        [SkippableFact]
        public void SpellChainEffectsBytesDecode()
        {
            _fixture.SkipIfNoClientData();
            var row = DBC.DBC.SpellChainEffects[1];
            Assert.Equal(@"Textures\SpellChainEffects\Lightning.blp", row.Texture);
            Assert.Equal(1000, row.SegDuration);
            Assert.Equal(new byte[] { 255, 255, 255, 255, 3 }, new[] { row.Alpha, row.Red, row.Green, row.Blue, row.BlendMode });
            Assert.Equal(2.0f, row.TextureLength);

            var row127 = DBC.DBC.SpellChainEffects[127];
            Assert.Equal(new byte[] { 255, 255, 28, 28, 3 }, new[] { row127.Alpha, row127.Red, row127.Green, row127.Blue, row127.BlendMode });
            Assert.Equal(2, row127.RenderLayer);
        }

        [SkippableFact]
        public void LocaleSlotFollowsConfig()
        {
            _fixture.SkipIfNoClientData();
            var expected = LocaleSlots.Resolve(Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS", out var fallback);
            Assert.Equal(expected, DBC.DBC.LocaleSlot);
            Assert.Equal(fallback, DBC.DBC.LocaleSlotFallback);
        }
    }
}
