using System.Collections.Generic;
using System.Linq;
using SpellWork.DBC.Structures;

namespace SpellWork.Spell
{
    /// <summary>
    /// The group-by-parent lookups the visual chain walks, built once per
    /// generation of <see cref="DBC.DBC"/>'s stores instead of once per call
    /// (the integrity test walks every spell).
    /// <para>
    /// The cache is keyed on the <b>identity of the store objects</b> it was
    /// built from, not on a "built yet" flag: <c>DBC.LoadAsync</c> assigns a new
    /// <c>Storage&lt;T&gt;</c> per store, and tests swap or null stores, so any
    /// change of instance makes <see cref="MatchesCurrentStores"/> false before
    /// a stale row can be read. Each store is read off <c>DBC.DBC</c> exactly
    /// once per build, so a whole walk reads one generation.
    /// </para>
    /// </summary>
    internal sealed class SpellVisualChainIndex
    {
        private static readonly object BuildLock = new object();
        private static volatile SpellVisualChainIndex _cached;

        public IReadOnlyDictionary<int, SpellVisualEntry> SpellVisual { get; private set; }
        public IReadOnlyDictionary<int, SpellVisualKitEntry> SpellVisualKit { get; private set; }
        public IReadOnlyDictionary<int, SpellVisualMissileEntry> SpellVisualMissile { get; private set; }
        public IReadOnlyDictionary<int, SpellVisualEffectNameEntry> SpellVisualEffectName { get; private set; }
        public IReadOnlyDictionary<int, SpellVisualKitModelAttachEntry> SpellVisualKitModelAttach { get; private set; }
        public IReadOnlyDictionary<int, SpellMissileMotionEntry> SpellMissileMotion { get; private set; }

        /// <summary>SpellVisualMissile rows keyed by <c>SpellVisualMissileSetID</c>, ordered by ID within a set.</summary>
        public ILookup<int, SpellVisualMissileEntry> MissilesBySet { get; private set; }

        /// <summary>SpellVisualKitModelAttach rows keyed by <c>ParentSpellVisualKitID</c>, ordered by ID within a kit.</summary>
        public ILookup<int, SpellVisualKitModelAttachEntry> AttachmentsByKit { get; private set; }

        /// <summary>
        /// The gated stores (spec §6) that are not loaded, in chain order.
        /// SpellMissileMotion is read but not gated: ids it cannot resolve are
        /// listed in <see cref="SpellVisualChain.UnresolvedReferences"/>.
        /// </summary>
        public IReadOnlyList<string> MissingStores { get; private set; }

        private SpellVisualChainIndex() { }

        internal static SpellVisualChainIndex Current()
        {
            var snapshot = _cached;
            if (snapshot != null && snapshot.MatchesCurrentStores())
                return snapshot;

            lock (BuildLock)
            {
                snapshot = _cached;
                if (snapshot != null && snapshot.MatchesCurrentStores())
                    return snapshot;

                snapshot = BuildFrom(
                    DBC.DBC.SpellVisual,
                    DBC.DBC.SpellVisualKit,
                    DBC.DBC.SpellVisualMissile,
                    DBC.DBC.SpellVisualEffectName,
                    DBC.DBC.SpellVisualKitModelAttach,
                    DBC.DBC.SpellMissileMotion);
                _cached = snapshot;
                return snapshot;
            }
        }

        /// <summary>True when every store this index was built from is still the
        /// object <see cref="DBC.DBC"/> holds. Null matches null.</summary>
        internal bool MatchesCurrentStores() =>
            ReferenceEquals(SpellVisual, DBC.DBC.SpellVisual) &&
            ReferenceEquals(SpellVisualKit, DBC.DBC.SpellVisualKit) &&
            ReferenceEquals(SpellVisualMissile, DBC.DBC.SpellVisualMissile) &&
            ReferenceEquals(SpellVisualEffectName, DBC.DBC.SpellVisualEffectName) &&
            ReferenceEquals(SpellVisualKitModelAttach, DBC.DBC.SpellVisualKitModelAttach) &&
            ReferenceEquals(SpellMissileMotion, DBC.DBC.SpellMissileMotion);

        /// <summary>Builds an index over exactly these stores, touching no global
        /// state, so a test can supply in-memory or missing stores.</summary>
        internal static SpellVisualChainIndex BuildFrom(
            IReadOnlyDictionary<int, SpellVisualEntry> spellVisual,
            IReadOnlyDictionary<int, SpellVisualKitEntry> spellVisualKit,
            IReadOnlyDictionary<int, SpellVisualMissileEntry> spellVisualMissile,
            IReadOnlyDictionary<int, SpellVisualEffectNameEntry> spellVisualEffectName,
            IReadOnlyDictionary<int, SpellVisualKitModelAttachEntry> spellVisualKitModelAttach,
            IReadOnlyDictionary<int, SpellMissileMotionEntry> spellMissileMotion)
        {
            var missing = new List<string>();
            if (spellVisual == null) missing.Add("SpellVisual");
            if (spellVisualKit == null) missing.Add("SpellVisualKit");
            if (spellVisualMissile == null) missing.Add("SpellVisualMissile");
            if (spellVisualEffectName == null) missing.Add("SpellVisualEffectName");
            if (spellVisualKitModelAttach == null) missing.Add("SpellVisualKitModelAttach");

            return new SpellVisualChainIndex
            {
                SpellVisual = spellVisual,
                SpellVisualKit = spellVisualKit,
                SpellVisualMissile = spellVisualMissile,
                SpellVisualEffectName = spellVisualEffectName,
                SpellVisualKitModelAttach = spellVisualKitModelAttach,
                SpellMissileMotion = spellMissileMotion,
                MissilesBySet = (spellVisualMissile?.Values ?? Enumerable.Empty<SpellVisualMissileEntry>())
                    .OrderBy(m => m.ID).ToLookup(m => m.SpellVisualMissileSetID),
                AttachmentsByKit = (spellVisualKitModelAttach?.Values ?? Enumerable.Empty<SpellVisualKitModelAttachEntry>())
                    .OrderBy(a => a.ID).ToLookup(a => a.ParentSpellVisualKitID),
                MissingStores = missing,
            };
        }
    }
}
