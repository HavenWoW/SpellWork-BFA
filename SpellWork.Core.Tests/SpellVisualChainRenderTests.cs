using System.Collections.Generic;
using System.Text.Json;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    /// <summary>
    /// Both renderings of a visual chain -- the text writer and the JSON
    /// section -- driven from chains built by hand rather than from client
    /// data.
    /// <para>
    /// This exists because several branches in both renderers are unreachable
    /// with this build's data: every kit-bearing field resolves at 100%, so
    /// nothing produces an unresolved <c>SpellVisualID</c>, an
    /// <c>unresolvedReferences</c> entry, or a truncated chain, and a format
    /// typo in any of them would ship with a fully green suite. Every value
    /// below is chosen to be zero or empty where a zero would be dangerous:
    /// <c>AppendFormatLineIfNotNull</c> treats 0 as absent for every integral
    /// type, so a real id of 0 disappearing from the render is a bug this file
    /// has shipped before.
    /// </para>
    /// </summary>
    public class SpellVisualChainRenderTests
    {
        private static SpellVisualChain ChainWithEveryShape() => new SpellVisualChain
        {
            SpellID = 42,
            Truncated = true,
            UnresolvedReferences = new[] { "SpellVisualKit 777", "SpellMissileMotion 888" },
            Variants = new List<SpellVisualVariant>
            {
                // Every field zero, and the visual unresolved: the combination
                // most likely to vanish from a render that used a
                // treats-zero-as-absent helper anywhere.
                new SpellVisualVariant
                {
                    SpellVisualID = 0,
                    DifficultyID = 0,
                    Priority = 0,
                    Probability = 0,
                    VisualResolved = false,
                    Kits = new List<SpellVisualKitRef>(),
                },
                new SpellVisualVariant
                {
                    SpellVisualID = 111,
                    DifficultyID = 23,
                    Priority = 3,
                    Probability = 0.5f,
                    VisualResolved = true,
                    Missile = new SpellMissileInfo { MissileID = 222, MotionID = 0, MotionName = null },
                    Kits = new List<SpellVisualKitRef>
                    {
                        new SpellVisualKitRef
                        {
                            KitID = 333,
                            ReachedVia = "event",
                            Effects = new List<SpellVisualKitEffectInfo>
                            {
                                new SpellVisualKitEffectInfo { EffectType = 0, Effect = 0 },
                            },
                        },
                    },
                },
            },
        };

        [Fact]
        public void WriteVisualChain_PrintsEveryNode_IncludingZeroValuedIds()
        {
            var writer = new PlainTextSpellInfoWriter();

            SpellInfo.WriteVisualChain(writer, ChainWithEveryShape(), 23, null);
            var text = writer.ToString();

            Assert.Contains("Spell visual:", text);

            // The unresolved variant is reported, not dropped: omitting it
            // would look identical to a spell that has no visual there.
            Assert.Contains("Visual 0 (difficulty 0): no SpellVisual.db2 row", text);

            Assert.Contains("Visual 111 (difficulty 23, priority 3, probability 0", text);

            // Motion id 0 with no name: the id still prints, and no empty
            // quoted name is appended.
            Assert.Contains("SpellVisualMissile 222, motion 0", text);
            Assert.DoesNotContain("motion 0 \"\"", text);

            Assert.Contains("Kit 333 (via event)", text);
            Assert.Contains("Effect type 0: 0", text);

            Assert.Contains("(more kits are reachable than are shown here)", text);
            Assert.Contains("Unresolved reference: SpellVisualKit 777", text);
            Assert.Contains("Unresolved reference: SpellMissileMotion 888", text);
        }

        [Fact]
        public void WriteVisualChain_NoVariantsAnywhere_RendersNothingAtAll()
        {
            var writer = new PlainTextSpellInfoWriter();

            SpellInfo.WriteVisualChain(writer, EmptyChain(), 0, null);

            // Not a header with nothing under it: a spell with no visuals
            // renders exactly as it did before this section existed.
            Assert.Equal(string.Empty, writer.ToString());
        }

        [Fact]
        public void WriteVisualChain_FilteredToNothing_SaysSoRatherThanRenderingNothing()
        {
            var writer = new PlainTextSpellInfoWriter();

            SpellInfo.WriteVisualChain(writer, EmptyChain(), 0, new[] { 8, 23 });
            var text = writer.ToString();

            // The hazard of filtering by difficulty: silence here would state
            // something false about the spell, and would differ from what the
            // JSON section reports for the same arguments.
            Assert.Contains("No visual at difficulty 0.", text);
            Assert.Contains("8, 23", text);
        }

        [Fact]
        public void DescribeChain_CarriesTruncationAndUnresolvedReferences()
        {
            var json = JsonSerializer.Serialize(
                SpellInfoJsonSerializer.DescribeChain(ChainWithEveryShape(), 23, null));
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(23, doc.RootElement.GetProperty("difficulty").GetInt32());
            Assert.True(doc.RootElement.GetProperty("truncated").GetBoolean());
            Assert.Equal(2, doc.RootElement.GetProperty("variants").GetArrayLength());

            var unresolved = doc.RootElement.GetProperty("unresolvedReferences");
            Assert.Equal(2, unresolved.GetArrayLength());
            Assert.Equal("SpellVisualKit 777", unresolved[0].GetString());

            // Absent, not present-and-empty: this key only appears when the
            // filter is what emptied the list.
            Assert.False(doc.RootElement.TryGetProperty("variantsAtOtherDifficulties", out _));
        }

        [Fact]
        public void DescribeChain_FilteredToNothing_NamesTheDifficultiesThatDoHaveVariants()
        {
            var json = JsonSerializer.Serialize(
                SpellInfoJsonSerializer.DescribeChain(EmptyChain(), 0, new[] { 8, 23 }));
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(0, doc.RootElement.GetProperty("variants").GetArrayLength());

            var other = doc.RootElement.GetProperty("variantsAtOtherDifficulties");
            Assert.Equal(new[] { 8, 23 }, new[] { other[0].GetInt32(), other[1].GetInt32() });

            // No unresolvedReferences key on an empty list -- omitted entirely
            // rather than emitted empty, the same as everywhere else.
            Assert.False(doc.RootElement.TryGetProperty("unresolvedReferences", out _));
        }

        private static SpellVisualChain EmptyChain() => new SpellVisualChain
        {
            SpellID = 42,
            Variants = new List<SpellVisualVariant>(),
            UnresolvedReferences = new List<string>(),
        };
    }
}
