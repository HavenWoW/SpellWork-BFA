using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    /// <summary>
    /// The two renderings of <c>get_spell</c>'s visual section, driven through
    /// states where a store the walk needs did not load.
    /// <para>
    /// <b>The bug this file exists for.</b> The JSON section and
    /// <c>get_spell_visual_chain</c> both consulted
    /// <c>SpellVisualChainResolver.MissingStores</c>; the text render did not.
    /// Against a client-data directory missing <c>SpellVisualEvent.db2</c>,
    /// <c>get_spell(133, format:"json")</c> answered
    /// <c>"visual": {"storeUnloaded": true}</c> and
    /// <c>get_spell_visual_chain(133)</c> answered <c>store_not_loaded</c>,
    /// while <c>get_spell(133, format:"text")</c> printed the visual header, the
    /// variant line and the missile line with no kits under them at all -- which
    /// an agent reads as "Fireball's visual reaches no kits". Five of the six
    /// stores degrade like that, quietly and differently; only a missing
    /// <c>SpellXSpellVisual</c> renders nothing at all.
    /// </para>
    /// <para>
    /// <b>Why these tests would have caught it, where the existing ones did
    /// not.</b> <c>Serialize_VisualSection_WhenStoreUnloaded_...</c> branches on
    /// the same <c>MissingStores()</c> expression the code under test uses, so
    /// it pins the shape of the answer and never the gate. The tests below never
    /// evaluate that expression: they install a specific missing store and
    /// assert on rendered output, so a renderer that skipped the check would
    /// print chain content and fail here. And they drive
    /// <c>SpellInfo.AppendSpellVisualInfo</c> itself -- the real entry point --
    /// rather than recomposing what it does from resolver calls, which is
    /// exactly the seam the bug lived in.
    /// </para>
    /// <para>
    /// No <c>DBC.DBC</c> static is written anywhere here. Every partial state is
    /// an index built over a chosen subset of the currently-loaded stores
    /// (<see cref="SpellVisualChainIndexTests.BuildWithout"/>), passed to the
    /// index-taking overloads, so nothing races the test classes reading those
    /// statics alongside this one.
    /// </para>
    /// </summary>
    public class SpellVisualSectionTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellVisualSectionTests(ClientDataFixture fixture) => _fixture = fixture;

        // Fireball. Measured: one SpellXSpellVisual row (visual 69095,
        // difficulty 0), which resolves, carries a missile, and reaches three
        // event-sourced kits. Enough moving parts that a missing store shows up
        // as a *different* wrong answer for each of the six.
        private const int Fireball = 133;
        private const int FireballVisualId = 69095;

        // Every store the walk cannot do without. Not a copy of the resolver's
        // own list for its own sake: each one is asserted to produce the same
        // refusal from both renderers, and a store dropped from the gate fails
        // its row here.
        [SkippableTheory]
        [InlineData("SpellXSpellVisual")]
        [InlineData("SpellVisual")]
        [InlineData("SpellVisualEvent")]
        [InlineData("SpellVisualMissile")]
        [InlineData("SpellVisualKit")]
        [InlineData("SpellVisualKitEffect")]
        public void MissingStore_BothFormatsRefuseToAnswer_RatherThanAnsweringDifferently(string storeName)
        {
            _fixture.SkipIfNoClientData();

            var index = SpellVisualChainIndexTests.BuildWithout(storeName);
            var section = SpellVisualChainResolver.ResolveSection(index, Fireball, 0, 500);

            Assert.True(section.Unavailable, $"{storeName} is missing, so the section must be unavailable.");

            // text
            var text = RenderText(index);
            Assert.Contains("Spell visual:", text);
            Assert.Contains("Visual chain unavailable -- these stores did not load: " + storeName + ".db2.", text);

            // ...and nothing from the chain itself leaked past the refusal.
            // Each of these is what an ungated render actually printed for one
            // of the six: the variant line, kits with no effects, an empty kit
            // list under a resolved variant, or a list of unresolved ids.
            Assert.DoesNotContain("Visual " + FireballVisualId, text);
            Assert.DoesNotContain("Kit ", text);
            Assert.DoesNotContain("Unresolved reference", text);
            Assert.DoesNotContain("SpellVisualMissile ", text);

            // json, from the same section object -- so this asserts the two
            // hosts agree about this state, not merely that each says something.
            var json = SpellInfoJsonSerializer.DescribeChain(section);
            Assert.True((bool)json["storeUnloaded"]);
            Assert.False(json.ContainsKey("variants"),
                "An unloaded store must not present as a spell with zero variants.");

            // The difficulty fallback must not fire here either: naming the
            // difficulties a spell "does" have variants at, computed from a
            // store that did not load, would be a fabricated answer.
            Assert.Null(section.DifficultiesWithVariants);
        }

        [SkippableFact]
        public void EveryStoreLoaded_BothFormatsAnswerNormally()
        {
            _fixture.SkipIfNoClientData();

            // The positive control the theory above needs to mean anything: a
            // gate stuck permanently on would satisfy every assertion there.
            var index = SpellVisualChainIndexTests.BuildWithout(null);
            var section = SpellVisualChainResolver.ResolveSection(index, Fireball, 0, 500);

            Assert.False(section.Unavailable);

            var text = RenderText(index);
            Assert.DoesNotContain("Visual chain unavailable", text);
            Assert.Contains("Visual " + FireballVisualId, text);
            Assert.Contains("Kit ", text);

            var json = SpellInfoJsonSerializer.DescribeChain(section);
            Assert.False(json.ContainsKey("storeUnloaded"));
            Assert.NotEmpty((System.Collections.IEnumerable)json["variants"]);
        }

        /// <summary>
        /// The same refusal with no client data at all -- a plain
        /// <see cref="FactAttribute"/>, so this one assertion runs in both
        /// configurations of this suite. An index over seven nulls is the state
        /// a first-ever load starts from and the state
        /// <c>McpClientDataFixture.RunPartialLoad</c> installs deliberately.
        /// </summary>
        [Fact]
        public void NoStoresAtAll_TextRenderNamesThemRatherThanRenderingSilence()
        {
            var index = SpellVisualChainIndex.BuildFrom(null, null, null, null, null, null, null);
            var section = SpellVisualChainResolver.ResolveSection(index, Fireball, 0, 500);

            Assert.True(section.Unavailable);
            Assert.Equal(6, section.MissingStores.Count);

            var text = RenderText(index);
            Assert.Contains("Visual chain unavailable", text);
            Assert.Contains("SpellXSpellVisual.db2", text);
            Assert.Contains("SpellVisualKitEffect.db2", text);

            Assert.True(SpellInfoJsonSerializer.DescribeChain(section).ContainsKey("storeUnloaded"));
        }

        /// <summary>
        /// Renders Fireball's visual section through
        /// <c>SpellInfo.AppendSpellVisualInfo</c> against the given index. The
        /// SpellInfo is built from a bare <c>SpellNameEntry</c> rather than read
        /// out of <c>SpellInfoStore</c> so the no-client-data test above can use
        /// it too; that section reads nothing off the spell but its id.
        /// </summary>
        private static string RenderText(SpellVisualChainIndex index)
        {
            var writer = new PlainTextSpellInfoWriter();
            new SpellInfo(new SpellNameEntry { ID = Fireball, Name = "Fireball" }, null)
                .AppendSpellVisualInfo(writer, new RenderContext(1, 1, null), index);
            return writer.ToString();
        }
    }
}
