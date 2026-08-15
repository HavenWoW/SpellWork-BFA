using System.Collections.Generic;
using System.Linq;
using DBFileReaderLib;
using SpellWork.DBC.Structures;

namespace SpellWork.Spell
{
    /// <summary>
    /// The group-by-parent lookups <see cref="SpellVisualChainResolver"/> walks,
    /// built once per generation of <see cref="DBC.DBC"/>'s stores instead of
    /// once per call.
    /// <para>
    /// Before this existed, every <c>Resolve</c> scanned four tables end to end
    /// -- <c>SpellXSpellVisual</c> (133k rows), <c>SpellVisualEvent</c> (170k),
    /// <c>SpellVisualMissile</c> (12k) and <c>SpellVisualKitEffect</c> (264k),
    /// roughly 580k row visits -- and paid nearly all of it even for a spell
    /// with no visuals at all. That was tolerable while only
    /// <c>get_spell_visual_chain</c> called it and became a 23x regression on
    /// <c>get_spell</c>'s default path once the "visual" section shipped.
    /// </para>
    /// <para>
    /// <b>Staleness is the real risk here, not speed.</b> A stale index is a
    /// correctness bug, and a wrong answer is far worse than the 47ms this
    /// saves. Neither host reloads client data in a live process today -- the
    /// WinForms host calls <c>LoadAsync</c> once at startup and restarts the
    /// process to change settings, and the MCP host loads once and never again
    /// -- but the stores are process-wide statics that anything can assign, and
    /// the test suites already do: <c>McpClientDataFixture.RunPartialLoad</c>
    /// nulls every one of them, loads a deliberately incomplete directory over
    /// the top, and restores them afterwards, and
    /// <c>SpellVisualChainIndexTests</c> swaps one store for a second instance
    /// over the same file. An index that survived either of those would answer
    /// from stores that are no longer installed. So the cache is keyed on the
    /// <b>identity of the store objects it was built from</b>, not on a "have I
    /// built yet" flag. <c>DBC.LoadAsync</c> constructs a brand new
    /// <c>Storage&lt;T&gt;</c> per store and assigns it
    /// (<c>DBC.cs:212-217</c>), so any reload changes every reference and
    /// <see cref="MatchesCurrentStores"/> reports false before a single stale
    /// row can be read.
    /// </para>
    /// <para>
    /// Each store reference is captured into a local <b>once</b> during
    /// <see cref="Build"/> and used for both the lookup and the recorded
    /// identity, so an index can never claim to have been built from a store it
    /// did not actually read. A caller holds one snapshot for the whole of its
    /// walk, which also makes a single <c>Resolve</c> internally consistent:
    /// every table it touches comes from one generation, where the previous
    /// code re-read <c>DBC.DBC</c>'s statics live at four different points.
    /// </para>
    /// </summary>
    internal sealed class SpellVisualChainIndex
    {
        // The exact store instances this index was built from. Compared by
        // reference, never by content: content comparison would be both
        // expensive and wrong, since a reload can produce byte-identical data
        // in a new object and an index over the old object is still stale.
        private readonly Storage<SpellXSpellVisualEntry> _spellXSpellVisual;
        private readonly Storage<SpellVisualEventEntry> _spellVisualEvent;
        private readonly Storage<SpellVisualMissileEntry> _spellVisualMissile;
        private readonly Storage<SpellVisualKitEffectEntry> _spellVisualKitEffect;

        private static readonly object BuildLock = new object();
        private static volatile SpellVisualChainIndex _cached;

        /// <summary>Every <c>SpellXSpellVisual</c> row, keyed by <c>SpellID</c>.</summary>
        public ILookup<int, SpellXSpellVisualEntry> VariantsBySpell { get; private set; }

        /// <summary>Every <c>SpellVisualEvent</c> row, keyed by <c>SpellVisualID</c>.
        /// Null when the store is not loaded, which is how the resolver tells
        /// "this visual fires no events" from "the event table is missing".</summary>
        public ILookup<int, SpellVisualEventEntry> EventsByVisual { get; private set; }

        /// <summary>Every <c>SpellVisualMissile</c> row, keyed by
        /// <c>SpellVisualMissileSetID</c>. Null when the store is not loaded.</summary>
        public ILookup<ushort, SpellVisualMissileEntry> MissilesBySet { get; private set; }

        /// <summary>Every <c>SpellVisualKitEffect</c> row, keyed by
        /// <c>ParentSpellVisualKitID</c>. Null when the store is not loaded.</summary>
        public ILookup<int, SpellVisualKitEffectEntry> EffectsByKit { get; private set; }

        // Captured alongside the lookups so a whole walk reads one generation.
        // These three are keyed dictionary lookups rather than scans, so they
        // are held for consistency, not for speed.
        public Storage<SpellVisualEntry> SpellVisual { get; private set; }
        public Storage<SpellVisualKitEntry> SpellVisualKit { get; private set; }
        public Storage<SpellMissileMotionEntry> SpellMissileMotion { get; private set; }

        /// <summary>
        /// The <c>.db2</c> base names of the stores this walk needs that are not
        /// loaded, in chain order. This is the single definition of "is the
        /// visual chain's data available" -- both hosts ask it rather than each
        /// hand-rolling a null check, because two hand-rolled gates disagreed
        /// once already (the MCP tool answered <c>ok:true</c> with empty
        /// variants for a state in which <c>get_spell</c> answered
        /// <c>storeUnloaded</c>).
        /// <para>
        /// <c>SpellMissileMotion</c> is deliberately <b>not</b> in this list
        /// even though the walk reads it. Its absence is already self-reporting:
        /// every motion id it cannot resolve is recorded in
        /// <see cref="SpellVisualChain.UnresolvedReferences"/>, so it degrades
        /// visibly rather than silently, and gating the whole chain on it would
        /// refuse an otherwise complete answer over one missing name lookup.
        /// The six listed here have no such fallback -- each one's absence turns
        /// into an empty list that reads as a fact about the spell.
        /// </para>
        /// </summary>
        public IReadOnlyList<string> MissingStores { get; private set; }

        private SpellVisualChainIndex(
            Storage<SpellXSpellVisualEntry> spellXSpellVisual,
            Storage<SpellVisualEventEntry> spellVisualEvent,
            Storage<SpellVisualMissileEntry> spellVisualMissile,
            Storage<SpellVisualKitEffectEntry> spellVisualKitEffect)
        {
            _spellXSpellVisual = spellXSpellVisual;
            _spellVisualEvent = spellVisualEvent;
            _spellVisualMissile = spellVisualMissile;
            _spellVisualKitEffect = spellVisualKitEffect;
        }

        /// <summary>
        /// The index for the stores currently on <see cref="DBC.DBC"/>, built if
        /// the cached one was built from different store objects (or if there is
        /// no cached one yet).
        /// </summary>
        internal static SpellVisualChainIndex Current()
        {
            // Read the volatile field once into a local: re-reading it below
            // could validate one instance and return another.
            var snapshot = _cached;
            if (snapshot != null && snapshot.MatchesCurrentStores())
                return snapshot;

            lock (BuildLock)
            {
                // Re-check inside the lock -- another thread may have rebuilt
                // for this same generation while this one waited, and building
                // twice would be wasteful rather than wrong.
                snapshot = _cached;
                if (snapshot != null && snapshot.MatchesCurrentStores())
                    return snapshot;

                snapshot = Build();
                _cached = snapshot;
                return snapshot;
            }
        }

        /// <summary>
        /// True when every store this index was built from is still the object
        /// <see cref="DBC.DBC"/> currently holds. Null matches null, so an index
        /// built while a store was missing is correctly invalidated the moment
        /// that store loads.
        /// </summary>
        internal bool MatchesCurrentStores() =>
            ReferenceEquals(_spellXSpellVisual, DBC.DBC.SpellXSpellVisual) &&
            ReferenceEquals(_spellVisualEvent, DBC.DBC.SpellVisualEvent) &&
            ReferenceEquals(_spellVisualMissile, DBC.DBC.SpellVisualMissile) &&
            ReferenceEquals(_spellVisualKitEffect, DBC.DBC.SpellVisualKitEffect) &&
            ReferenceEquals(SpellVisual, DBC.DBC.SpellVisual) &&
            ReferenceEquals(SpellVisualKit, DBC.DBC.SpellVisualKit) &&
            ReferenceEquals(SpellMissileMotion, DBC.DBC.SpellMissileMotion);

        // Every store is read out of DBC.DBC exactly once, here, and handed to
        // BuildFrom as a value. Reading DBC.DBC.X a second time inside the
        // build could pick up a different object mid-way and produce an index
        // whose recorded identity does not match the rows it actually holds --
        // the one way this design could go stale.
        private static SpellVisualChainIndex Build() => BuildFrom(
            DBC.DBC.SpellXSpellVisual,
            DBC.DBC.SpellVisual,
            DBC.DBC.SpellVisualEvent,
            DBC.DBC.SpellVisualMissile,
            DBC.DBC.SpellVisualKit,
            DBC.DBC.SpellVisualKitEffect,
            DBC.DBC.SpellMissileMotion);

        /// <summary>
        /// Builds an index over exactly these stores, touching no global state.
        /// Split out from <see cref="Build"/> so a test can check what
        /// <see cref="MissingStores"/> reports for each store independently,
        /// without assigning to <see cref="DBC.DBC"/>'s process-wide statics
        /// while other test classes are reading them.
        /// </summary>
        internal static SpellVisualChainIndex BuildFrom(
            Storage<SpellXSpellVisualEntry> spellXSpellVisual,
            Storage<SpellVisualEntry> spellVisual,
            Storage<SpellVisualEventEntry> spellVisualEvent,
            Storage<SpellVisualMissileEntry> spellVisualMissile,
            Storage<SpellVisualKitEntry> spellVisualKit,
            Storage<SpellVisualKitEffectEntry> spellVisualKitEffect,
            Storage<SpellMissileMotionEntry> spellMissileMotion)
        {
            var index = new SpellVisualChainIndex(
                spellXSpellVisual, spellVisualEvent, spellVisualMissile, spellVisualKitEffect)
            {
                SpellVisual = spellVisual,
                SpellVisualKit = spellVisualKit,
                SpellMissileMotion = spellMissileMotion,
            };

            // ToLookup preserves source order within each group, and Storage<T>
            // is a SortedDictionary so Values enumerates by key. Together those
            // keep every group in exactly the order the previous per-call
            // Where(...).ToLookup(...) produced, which is what makes this
            // refactor byte-identical rather than merely equivalent.
            index.VariantsBySpell = spellXSpellVisual == null
                ? Empty<int, SpellXSpellVisualEntry>()
                : spellXSpellVisual.Values.ToLookup(x => x.SpellID);

            index.EventsByVisual = spellVisualEvent?.Values.ToLookup(e => e.SpellVisualID);
            index.MissilesBySet = spellVisualMissile?.Values.ToLookup(m => m.SpellVisualMissileSetID);
            index.EffectsByKit = spellVisualKitEffect?.Values.ToLookup(e => e.ParentSpellVisualKitID);

            var missing = new List<string>();
            if (spellXSpellVisual == null) missing.Add("SpellXSpellVisual");
            if (spellVisual == null) missing.Add("SpellVisual");
            if (spellVisualEvent == null) missing.Add("SpellVisualEvent");
            if (spellVisualMissile == null) missing.Add("SpellVisualMissile");
            if (spellVisualKit == null) missing.Add("SpellVisualKit");
            if (spellVisualKitEffect == null) missing.Add("SpellVisualKitEffect");
            index.MissingStores = missing;

            return index;
        }

        // VariantsBySpell is the one lookup the resolver indexes without a null
        // check (an absent store is already reported by MissingStores and
        // produces an empty chain), so it gets an empty lookup rather than null.
        private static ILookup<TKey, TElement> Empty<TKey, TElement>() =>
            System.Linq.Enumerable.Empty<TElement>().ToLookup(_ => default(TKey));
    }
}
