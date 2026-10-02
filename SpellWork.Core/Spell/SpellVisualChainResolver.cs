using System.Collections.Generic;
using System.Linq;
using SpellWork.DBC.Structures;

namespace SpellWork.Spell
{
    /// <summary>
    /// Walks the 5.4.8 visual chain (spec §6). Every non-zero reference either
    /// resolves or is listed in <see cref="SpellVisualChain.UnresolvedReferences"/>;
    /// an id that does not resolve is never reported as though it were a row.
    /// </summary>
    public static class SpellVisualChainResolver
    {
        public const int DefaultMaxKits = 500;

        /// <summary>The SpellVisual kit columns, in report order.</summary>
        private static readonly (string Slot, System.Func<SpellVisualEntry, int> Get)[] KitColumns =
        {
            ("PrecastKit", v => v.PrecastKit),
            ("CastKit", v => v.CastKit),
            ("ImpactKit", v => v.ImpactKit),
            ("StateKit", v => v.StateKit),
            ("StateDoneKit", v => v.StateDoneKit),
            ("ChannelKit", v => v.ChannelKit),
            ("CasterImpactKit", v => v.CasterImpactKit),
            ("TargetImpactKit", v => v.TargetImpactKit),
            ("MissileTargetingKit", v => v.MissileTargetingKit),
            ("InstantAreaKit", v => v.InstantAreaKit),
            ("ImpactAreaKit", v => v.ImpactAreaKit),
            ("PersistentAreaKit", v => v.PersistentAreaKit),
        };

        /// <summary>The SpellVisualKit effect columns, in report order.</summary>
        private static readonly (string Slot, System.Func<SpellVisualKitEntry, int> Get)[] EffectColumns =
        {
            ("Head", k => k.HeadEffect),
            ("Chest", k => k.ChestEffect),
            ("Base", k => k.BaseEffect),
            ("LeftHand", k => k.LeftHandEffect),
            ("RightHand", k => k.RightHandEffect),
            ("Breath", k => k.BreathEffect),
            ("LeftWeapon", k => k.LeftWeaponEffect),
            ("RightWeapon", k => k.RightWeaponEffect),
            ("Special0", k => k.SpecialEffect[0]),
            ("Special1", k => k.SpecialEffect[1]),
            ("Special2", k => k.SpecialEffect[2]),
            ("World", k => k.WorldEffect),
        };

        /// <summary>
        /// The section every host renders from: the gated stores that are
        /// missing, or the resolved chain. Visual ids come from the spell's
        /// difficulty-0 SpellMisc row (D4: visuals are not per difficulty).
        /// </summary>
        public static SpellVisualSection ResolveSection(int spellId, int maxKits = DefaultMaxKits)
        {
            int[] visualIds = null;
            if (DBC.DBC.SpellInfoStore != null && DBC.DBC.SpellInfoStore.TryGetValue(spellId, out var spell))
                visualIds = spell.Misc?.SpellVisualID;

            return ResolveSection(SpellVisualChainIndex.Current(), spellId, visualIds ?? new int[2], maxKits);
        }

        internal static SpellVisualSection ResolveSection(SpellVisualChainIndex index, int spellId, int[] visualIds, int maxKits)
        {
            if (index.MissingStores.Count > 0)
                return new SpellVisualSection { MissingStores = index.MissingStores, Chain = null };

            return new SpellVisualSection
            {
                MissingStores = index.MissingStores,
                Chain = Resolve(index, spellId, visualIds, maxKits),
            };
        }

        public static IReadOnlyList<string> MissingStores() => SpellVisualChainIndex.Current().MissingStores;

        /// <summary>
        /// Resolves the chain over <paramref name="index"/>. Expects every gated
        /// store to be present (callers go through <see cref="ResolveSection(int,int)"/>).
        /// </summary>
        internal static SpellVisualChain Resolve(SpellVisualChainIndex index, int spellId, int[] visualIds, int maxKits)
        {
            var unresolved = new UnresolvedReferenceSet();
            var variants = new List<SpellVisualVariant>();
            var kits = new List<SpellVisualKitRef>();
            var kitsById = new Dictionary<int, (SpellVisualKitRef Ref, List<string> Via)>();
            var truncated = false;

            for (var slot = 0; slot < visualIds.Length && slot < 2; slot++)
            {
                var visualId = visualIds[slot];
                if (visualId == 0)
                    continue;

                var variant = new SpellVisualVariant
                {
                    Slot = slot,
                    SpellVisualID = visualId,
                    KitSlots = new List<KitSlotRef>(),
                    Missiles = new List<SpellVisualMissileInfo>(),
                };
                variants.Add(variant);

                if (!index.SpellVisual.TryGetValue(visualId, out var visual))
                {
                    unresolved.Add($"SpellVisual#{visualId} (from Spell#{spellId}.SpellVisualID[{slot}])");
                    continue;
                }

                variant.VisualResolved = true;

                var kitSlots = new List<KitSlotRef>();
                foreach (var (kitSlot, get) in KitColumns)
                {
                    var kitId = get(visual);
                    if (kitId == 0)
                        continue;

                    kitSlots.Add(new KitSlotRef { Slot = kitSlot, KitID = kitId });
                    var via = $"visual {visualId} (slot {slot}) {kitSlot}";

                    if (kitsById.TryGetValue(kitId, out var known))
                    {
                        known.Via.Add(via);
                        continue;
                    }

                    // Resolvability before budget: an id that does not resolve
                    // was never going to be reported as a kit, so it neither
                    // consumes the cap nor sets Truncated.
                    if (!index.SpellVisualKit.TryGetValue(kitId, out var kitRow))
                    {
                        unresolved.Add($"SpellVisualKit#{kitId} (from SpellVisual#{visualId}.{kitSlot})");
                        continue;
                    }

                    if (kits.Count >= maxKits)
                    {
                        truncated = true;
                        continue;
                    }

                    var viaList = new List<string> { via };
                    var kitRef = new SpellVisualKitRef
                    {
                        KitID = kitId,
                        ReachedVia = viaList,
                        Effects = ResolveKitEffects(index, kitRow, unresolved),
                        ModelAttachments = ResolveAttachments(index, kitId, unresolved),
                    };
                    kits.Add(kitRef);
                    kitsById[kitId] = (kitRef, viaList);
                }
                variant.KitSlots = kitSlots;

                // Raw and unresolved by design: column 14's meaning is
                // unverified (see SpellVisualEntry.Unk14MissileMotion), so it is
                // neither followed nor listed as an unresolved reference.
                variant.Unk14MissileMotion = visual.Unk14MissileMotion;

                if (visual.SpellVisualMissileSetID != 0)
                {
                    var rows = index.MissilesBySet[visual.SpellVisualMissileSetID].ToList();
                    if (rows.Count == 0)
                        unresolved.Add($"SpellVisualMissile set#{visual.SpellVisualMissileSetID} (from SpellVisual#{visualId}.SpellVisualMissileSetID)");

                    variant.Missiles = rows.Select(m => new SpellVisualMissileInfo
                    {
                        ID = m.ID,
                        EffectNameID = m.SpellVisualEffectNameID,
                        Name = EffectName(index, m.SpellVisualEffectNameID, $"SpellVisualMissile#{m.ID}.SpellVisualEffectNameID", unresolved),
                        MotionID = m.SpellMissileMotionID,
                        MotionName = m.SpellMissileMotionID == 0
                            ? null
                            : MotionName(index, m.SpellMissileMotionID, $"SpellVisualMissile#{m.ID}.SpellMissileMotionID", unresolved),
                    }).ToList();
                }
            }

            return new SpellVisualChain
            {
                SpellID = spellId,
                Variants = variants,
                Kits = kits,
                Truncated = truncated,
                UnresolvedReferences = unresolved.InOrder,
            };
        }

        private static List<KitEffectRef> ResolveKitEffects(SpellVisualChainIndex index, SpellVisualKitEntry kit, UnresolvedReferenceSet unresolved)
        {
            var effects = new List<KitEffectRef>();
            foreach (var (slot, get) in EffectColumns)
            {
                var id = get(kit);
                if (id == 0)
                    continue;

                effects.Add(new KitEffectRef
                {
                    Slot = slot,
                    EffectNameID = id,
                    Name = EffectName(index, id, $"SpellVisualKit#{kit.ID}.{slot}", unresolved),
                });
            }
            return effects;
        }

        private static List<KitModelAttachInfo> ResolveAttachments(SpellVisualChainIndex index, int kitId, UnresolvedReferenceSet unresolved) =>
            index.AttachmentsByKit[kitId].Select(a => new KitModelAttachInfo
            {
                ID = a.ID,
                AttachmentID = a.AttachmentID,
                EffectNameID = a.SpellVisualEffectNameID,
                Name = a.SpellVisualEffectNameID == 0
                    ? null
                    : EffectName(index, a.SpellVisualEffectNameID, $"SpellVisualKitModelAttach#{a.ID}.SpellVisualEffectNameID", unresolved),
            }).ToList();

        // Null for 0 (no reference) and for an id with no row (listed as unresolved).
        private static string EffectName(SpellVisualChainIndex index, int id, string from, UnresolvedReferenceSet unresolved)
        {
            if (id == 0)
                return null;
            if (index.SpellVisualEffectName.TryGetValue(id, out var row))
                return row.Name;
            unresolved.Add($"SpellVisualEffectName#{id} (from {from})");
            return null;
        }

        private static string MotionName(SpellVisualChainIndex index, int id, string from, UnresolvedReferenceSet unresolved)
        {
            if (index.SpellMissileMotion != null && index.SpellMissileMotion.TryGetValue(id, out var row))
                return row.Name;
            unresolved.Add($"SpellMissileMotion#{id} (from {from})");
            return null;
        }

        private sealed class UnresolvedReferenceSet
        {
            private readonly HashSet<string> _seen = new HashSet<string>();

            public List<string> InOrder { get; } = new List<string>();

            public void Add(string reference)
            {
                if (_seen.Add(reference))
                    InOrder.Add(reference);
            }
        }
    }
}
