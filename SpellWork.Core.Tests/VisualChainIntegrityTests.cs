using System.Collections.Generic;
using System.Linq;
using SpellWork.Spell;
using Xunit;
using Xunit.Abstractions;

namespace SpellWork.Core.Tests
{
    // Spec §6/§10: on the reference data, every non-zero reference the chain
    // follows either resolves or is listed in UnresolvedReferences -- and
    // nothing else is listed. The expected set is computed here, straight off
    // the raw stores with ContainsKey, independently of the resolver, so a
    // resolver that drops or invents a reference fails this test rather than
    // hiding behind it.
    public class VisualChainIntegrityTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;
        private readonly ITestOutputHelper _output;

        public VisualChainIntegrityTests(ClientDataFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [SkippableFact(Skip = "coa: Task 7")]
        public void EveryReferenceResolvesOrIsListed()
        {
            _fixture.SkipIfNoClientData();

            var visuals = DBC.DBC.SpellVisual;
            var kits = DBC.DBC.SpellVisualKit;
            var effectNames = DBC.DBC.SpellVisualEffectName;
            var attachmentsByKit = DBC.DBC.SpellVisualKitModelAttach.Values.ToLookup(a => a.ParentSpellVisualKitID);

            var walked = 0;
            var withVariants = 0;
            var referencesChecked = 0;
            var byStore = new Dictionary<string, int>();
            var distinctByStore = new Dictionary<string, HashSet<string>>();

            foreach (var spell in DBC.DBC.SpellInfoStore.Values)
            {
                walked++;
                var section = SpellVisualChainResolver.ResolveSection(spell.ID, int.MaxValue);
                Assert.Empty(section.MissingStores);
                var chain = section.Chain;
                Assert.False(chain.Truncated);

                var expected = new HashSet<string>();
                var expectedKits = new HashSet<int>();
                var ids = spell.Spell.SpellVisualID;
                if (ids.Any(id => id != 0))
                    withVariants++;

                for (var slot = 0; slot < 2; slot++)
                {
                    var vid = ids[slot];
                    if (vid == 0)
                        continue;
                    referencesChecked++;
                    if (!visuals.ContainsKey(vid))
                    {
                        expected.Add($"SpellVisual#{vid} (from Spell#{spell.ID}.SpellVisualID[{slot}])");
                        continue;
                    }

                    var v = visuals[vid];
                    var kitFields = new (string Slot, int Id)[]
                    {
                        ("PrecastKit", v.PrecastKit), ("CastKit", v.CastKit), ("ImpactKit", v.ImpactKit),
                        ("StateKit", v.StateKit), ("StateDoneKit", v.StateDoneKit), ("ChannelKit", v.ChannelKit),
                        ("CasterImpactKit", v.CasterImpactKit), ("TargetImpactKit", v.TargetImpactKit),
                        ("MissileTargetingKit", v.MissileTargetingKit), ("InstantAreaKit", v.InstantAreaKit),
                        ("ImpactAreaKit", v.ImpactAreaKit), ("PersistentAreaKit", v.PersistentAreaKit),
                    };
                    foreach (var (kitSlot, kitId) in kitFields.Where(k => k.Id != 0))
                    {
                        referencesChecked++;
                        if (kits.ContainsKey(kitId))
                            expectedKits.Add(kitId);
                        else
                            expected.Add($"SpellVisualKit#{kitId} (from SpellVisual#{vid}.{kitSlot})");
                    }

                    // MissileMotion is reported raw (interim, Task 7) and is not a
                    // reference the chain follows, so it must never be listed.
                    Assert.Equal(v.MissileMotion,
                        chain.Variants.First(x => x.Slot == slot).Unk14MissileMotion);

                    if (v.MissileModel != 0)
                    {
                        referencesChecked++;
                        if (!effectNames.ContainsKey(v.MissileModel))
                            expected.Add($"SpellVisualEffectName#{v.MissileModel} (from SpellVisual#{vid}.MissileModel)");
                    }
                }

                foreach (var kitId in expectedKits)
                {
                    var k = kits[kitId];
                    var effects = new (string Slot, int Id)[]
                    {
                        ("Head", k.HeadEffect), ("Chest", k.ChestEffect), ("Base", k.BaseEffect),
                        ("LeftHand", k.LeftHandEffect), ("RightHand", k.RightHandEffect), ("Breath", k.BreathEffect),
                        ("LeftWeapon", k.LeftWeaponEffect), ("RightWeapon", k.RightWeaponEffect),
                        ("Special0", k.SpecialEffect[0]), ("Special1", k.SpecialEffect[1]), ("Special2", k.SpecialEffect[2]),
                        ("World", k.WorldEffect),
                    };
                    foreach (var (effectSlot, effectId) in effects.Where(e => e.Id != 0))
                    {
                        referencesChecked++;
                        if (!effectNames.ContainsKey(effectId))
                            expected.Add($"SpellVisualEffectName#{effectId} (from SpellVisualKit#{kitId}.{effectSlot})");
                    }
                    foreach (var a in attachmentsByKit[kitId].Where(a => a.SpellVisualEffectNameID != 0))
                    {
                        referencesChecked++;
                        if (!effectNames.ContainsKey(a.SpellVisualEffectNameID))
                            expected.Add($"SpellVisualEffectName#{a.SpellVisualEffectNameID} (from SpellVisualKitModelAttach#{a.ID}.SpellVisualEffectNameID)");
                    }
                }

                Assert.True(expected.SetEquals(chain.UnresolvedReferences),
                    $"spell {spell.ID}: expected [{string.Join("; ", expected)}], resolver listed [{string.Join("; ", chain.UnresolvedReferences)}]");
                Assert.Equal(chain.UnresolvedReferences.Count, chain.UnresolvedReferences.Distinct().Count());
                Assert.True(expectedKits.SetEquals(chain.Kits.Select(x => x.KitID)), $"spell {spell.ID}: kit set differs");

                foreach (var reference in chain.UnresolvedReferences)
                {
                    var store = reference.Substring(0, reference.IndexOfAny(new[] { '#', ' ' }));
                    byStore[store] = byStore.TryGetValue(store, out var n) ? n + 1 : 1;
                    if (!distinctByStore.TryGetValue(store, out var set))
                        distinctByStore[store] = set = new HashSet<string>();
                    set.Add(reference.Substring(0, reference.IndexOf(" (from")));
                }
            }

            _output.WriteLine($"spells walked: {walked}; with a visual: {withVariants}; references checked: {referencesChecked}");
            foreach (var kv in byStore.OrderBy(k => k.Key))
                _output.WriteLine($"unresolved {kv.Key}: {kv.Value} listings, {distinctByStore[kv.Key].Count} distinct ids");
            if (byStore.Count == 0)
                _output.WriteLine("unresolved: none");

            // On the reference data every gated-store reference resolves; only
            // the ungated SpellMissileMotion (from SpellVisualMissile rows) may
            // have listings, and the set-equality above proves each is genuine.
            var gated = new[] { "SpellVisual", "SpellVisualKit", "SpellVisualEffectName", "SpellVisualKitModelAttach" };
            Assert.Empty(byStore.Keys.Intersect(gated));
            Assert.Equal(DBC.DBC.SpellInfoStore.Count, walked);
            Assert.True(walked > 90000, $"only {walked} spells walked");
        }
    }
}
