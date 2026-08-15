using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    /// <summary>
    /// Pins the agent-facing key names <see cref="SpellVisualChainJson"/>
    /// publishes.
    /// <para>
    /// One definition now serves both hosts -- <c>get_spell</c>'s "visual"
    /// section and the <c>get_spell_visual_chain</c> tool -- so a rename here
    /// changes both outputs at once, and until this test existed only
    /// <c>visualResolved</c> and <c>kits</c> were asserted anywhere: renaming
    /// <c>spellVisualMissileId</c> or <c>effectType</c> would have broken every
    /// consumer with a fully green suite. The names are asserted as complete
    /// sets rather than individually, so an added key is caught too -- a key
    /// added to this projection appears in both hosts' output whether they
    /// wanted it or not.
    /// </para>
    /// </summary>
    public class SpellVisualChainJsonTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellVisualChainJsonTests(ClientDataFixture fixture) => _fixture = fixture;

        // Fireball: the one spell measured to exercise every node type in this
        // projection at once -- a resolved variant, kits with effects, and
        // missile info with a resolved motion name.
        private const int Fireball = 133;

        [SkippableFact]
        public void Describe_Fireball_EmitsExactlyTheDocumentedKeys()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);
            var variant = chain.Variants.First(v => v.VisualResolved && v.Kits.Count > 0 && v.Missile != null);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(SpellVisualChainJson.Describe(variant)));
            var root = doc.RootElement;

            Assert.Equal(
                new[] { "spellVisualId", "difficultyId", "probability", "priority", "visualResolved", "kits", "missile" },
                Keys(root));

            var kit = root.GetProperty("kits")[0];
            Assert.Equal(new[] { "kitId", "reachedVia", "effects" }, Keys(kit));

            // "visual" or "event", never "missile": SpellVisualMissile carries
            // no kit reference in this schema at all.
            Assert.Contains(kit.GetProperty("reachedVia").GetString(), new[] { "visual", "event" });

            var effect = kit.GetProperty("effects")[0];
            Assert.Equal(new[] { "effectType", "effect" }, Keys(effect));

            // spellVisualMissileId, not missileId: it is a SpellVisualMissile
            // row id, and an agent reading "missileId" would reasonably take it
            // for a SpellMissile id, which is a table this chain never reaches.
            var missile = root.GetProperty("missile");
            Assert.Equal(new[] { "spellVisualMissileId", "motionId", "motionName" }, Keys(missile));
        }

        private static IEnumerable<string> Keys(JsonElement element) =>
            element.EnumerateObject().Select(p => p.Name).ToList();
    }
}
