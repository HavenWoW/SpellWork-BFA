using System.Collections.Generic;

namespace SpellWork.Spell
{
    /// <summary>
    /// The full visual chain resolved for one spell: every
    /// <c>SpellXSpellVisual</c> variant (one per difficulty/priority
    /// combination), the kits and missile info each one reaches, and every
    /// reference along the way that did not resolve. Produced by
    /// <see cref="SpellVisualChainResolver.Resolve"/>; data only, no
    /// behaviour, so the WinForms renderer and the MCP tool can each format
    /// it their own way from the same shape.
    /// </summary>
    public sealed class SpellVisualChain
    {
        public int SpellID { get; set; }
        public IReadOnlyList<SpellVisualVariant> Variants { get; set; }

        /// <summary>
        /// True once the global <c>maxKits</c> budget ran out while at least
        /// one more kit was still reachable -- i.e. the response is missing
        /// kits it would otherwise have reported, not merely that the budget
        /// happened to match the total found.
        /// </summary>
        public bool Truncated { get; set; }

        /// <summary>
        /// Every id this walk tried to resolve and could not -- a
        /// <c>SpellVisual</c>, <c>SpellVisualKit</c>, <c>SpellVisualMissile</c>
        /// set or <c>SpellMissileMotion</c> id that does not exist in its
        /// table. An id that fails to resolve is reported here and never
        /// appears as a populated object elsewhere in this chain: reporting
        /// an unresolved id as though it were real is worse than omitting it.
        /// </summary>
        public IReadOnlyList<string> UnresolvedReferences { get; set; }
    }

    /// <summary>
    /// One <c>SpellXSpellVisual</c> row for the spell -- a specific
    /// difficulty/priority combination and the visual it selects.
    /// </summary>
    public sealed class SpellVisualVariant
    {
        public int SpellVisualID { get; set; }
        public int DifficultyID { get; set; }
        public float Probability { get; set; }
        public int Priority { get; set; }

        /// <summary>
        /// False when <see cref="SpellVisualID"/> does not exist in
        /// <c>SpellVisual</c> -- every other field on this variant is then
        /// left at its default rather than guessed at.
        /// </summary>
        public bool VisualResolved { get; set; }

        public IReadOnlyList<SpellVisualKitRef> Kits { get; set; }

        /// <summary>
        /// Null when the visual carries no missile set at all
        /// (<c>SpellVisualMissileSetID == 0</c>) or the set does not resolve
        /// -- either way that failure is recorded in
        /// <see cref="SpellVisualChain.UnresolvedReferences"/> instead of a
        /// fabricated zero-valued object.
        /// </summary>
        public SpellMissileInfo Missile { get; set; }
    }

    /// <summary>
    /// A <c>SpellVisualKit</c> reached from a variant, plus which of the two
    /// directions found it first.
    /// </summary>
    public sealed class SpellVisualKitRef
    {
        public int KitID { get; set; }

        /// <summary>
        /// "visual" or "event", and nothing else. The design originally
        /// allowed a third "missile" direction; it was dropped because
        /// <c>SpellVisualMissile</c> carries no reference to
        /// <c>SpellVisualKit</c> at all -- its only "Kit"-named column is
        /// <c>AnimKitID</c>, which WoWDBDefs declares as a reference to the
        /// unrelated <c>AnimKit</c> table. So this is not "never produced in
        /// this build", it is not producible from this schema; see the
        /// resolver's own remarks for the measurements.
        /// </summary>
        public string ReachedVia { get; set; }

        public IReadOnlyList<SpellVisualKitEffectInfo> Effects { get; set; }
    }

    /// <summary>
    /// One <c>SpellVisualKitEffect</c> row for a kit, carried raw. Only
    /// <c>EffectType == 2</c> has a confirmed meaning in this build
    /// (<c>SpellVisualKitModelAttach</c>, n=133507, 100.00%) -- every other
    /// discriminator value was measured during planning and none cleared a
    /// trustworthy resolution rate into any candidate table, so this type
    /// deliberately carries the two raw ints and no resolved-target field or
    /// enum. The consumer (renderer or MCP tool) interprets <see cref="Effect"/>
    /// using <see cref="EffectType"/> itself.
    /// </summary>
    public sealed class SpellVisualKitEffectInfo
    {
        public int EffectType { get; set; }
        public int Effect { get; set; }
    }

    /// <summary>
    /// The missile carrying a variant's projectile, resolved from
    /// <c>SpellVisualMissile</c> (found via the visual's
    /// <c>SpellVisualMissileSetID</c>) and <c>SpellMissileMotion</c>. Not the
    /// standalone <c>SpellMissile</c> table -- see the resolver's remarks.
    /// </summary>
    public sealed class SpellMissileInfo
    {
        /// <summary>
        /// The <c>SpellVisualMissile</c> row's own <c>ID</c> -- deliberately
        /// <b>not</b> a <c>SpellMissile</c> id, despite the name. The
        /// standalone <c>SpellMissile</c> table is keyed by the spell itself
        /// (<c>SpellMissileEntry.SpellID</c>) and has no field connecting it
        /// to <c>SpellVisual</c>, <c>SpellVisualMissile</c>, or
        /// <c>SpellMissileMotion</c> -- see the resolver's own remarks for
        /// the measurement that established this. This property is not
        /// renamed because Tasks 6 and 7 are written against this shape;
        /// read the id at face value as "which SpellVisualMissile row", not
        /// "which SpellMissile row".
        /// </summary>
        public int MissileID { get; set; }
        public int MotionID { get; set; }

        /// <summary>Null when <see cref="MotionID"/> is 0 (no motion set) or
        /// does not resolve in <c>SpellMissileMotion</c>.</summary>
        public string MotionName { get; set; }
    }
}
