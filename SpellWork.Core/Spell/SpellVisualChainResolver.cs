using System.Collections.Generic;
using System.Linq;
using SpellWork.DBC.Structures;

namespace SpellWork.Spell
{
    /// <summary>
    /// Walks a spell's visual chain. Pure: reads DBC, returns POCOs, formats
    /// nothing -- SpellWork.Mcp and the WinForms renderer both consume this so
    /// they cannot drift apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Kits are reached from two directions in this build, not three.</b>
    /// The design called for a "missile" direction -- a kit id carried on
    /// <c>SpellVisualMissile</c> rows -- alongside "visual"
    /// (<see cref="SpellVisualEntry.MissileTargetingKit"/>) and "event"
    /// (<see cref="SpellVisualEventEntry.SpellVisualKitID"/>). Checked against
    /// WoWDBDefs' own <c>SpellVisualMissile.dbd</c> COLUMNS block, the only
    /// "Kit"-named field on that table is <c>AnimKitID</c>, and WoWDBDefs
    /// declares it <c>int&lt;AnimKit::ID&gt;</c> -- a reference to the
    /// unrelated <c>AnimKit</c> table, not <c>SpellVisualKit</c>. Measured
    /// directly: of 85 distinct non-zero <c>AnimKitID</c> values, only
    /// 71.76% happen to exist as <c>SpellVisualKit</c> keys, far below the
    /// &gt;99% this plan otherwise treats as a real reference and consistent
    /// with coincidental low-id-range overlap between two unrelated tables
    /// (the same trap Task 4 hit with <c>SpellVisualKitEffect.EffectType 3</c>
    /// and <c>SpellChainEffects</c>). Wiring <c>AnimKitID</c> in here would
    /// occasionally report a real <c>SpellVisualKit</c> row that has nothing
    /// to do with the spell being resolved -- a worse failure than the
    /// "missile" direction simply never firing. "visual" and "event" are
    /// each independently confirmed at 100% (1595 and 103443 distinct
    /// non-zero ids respectively, against real client data). So
    /// <see cref="SpellVisualKitRef.ReachedVia"/> has exactly two values,
    /// "visual" and "event" -- not two of three. A "missile" direction is not
    /// merely unpopulated in this build, it is unrepresentable in this schema,
    /// and documenting it as a possible value would have consumers writing
    /// branches that can never run.
    /// </para>
    /// <para>
    /// <b><see cref="SpellMissileInfo"/> comes from <c>SpellVisualMissile</c>
    /// and <c>SpellMissileMotion</c>, not the standalone <c>SpellMissile</c>
    /// table.</b> The standalone table is keyed by the spell's own id
    /// (<c>SpellMissileEntry.SpellID</c>) and carries no field connecting it
    /// to <c>SpellVisual</c>, <c>SpellVisualMissile</c> or
    /// <c>SpellMissileMotion</c> at all -- measured directly: Fireball (133)
    /// has zero rows in it, and the table covers only 330 spells total.
    /// <c>SpellVisualMissile</c>, by contrast, is reached from the visual via
    /// <c>SpellVisualMissileSetID</c> and carries <c>SpellMissileMotionID</c>
    /// (measured: 1258 distinct non-zero values, 100% resolve into
    /// <c>SpellMissileMotion</c>) -- confirmed for Fireball specifically,
    /// whose resolved motion name is literally "Mage - Fire - Fireball".
    /// Consequently <see cref="SpellMissileInfo.MissileID"/> holds a
    /// <c>SpellVisualMissile</c> row id, not a <c>SpellMissile</c> id --
    /// see that property's own doc comment.
    /// </para>
    /// <para>
    /// <b>Not implemented, and out of this task's scope: <c>SpellMissile</c>
    /// itself is real, unused data.</b> Unlike the chain this resolver walks,
    /// <c>SpellMissileEntry.SpellID</c> is a direct spell -&gt; missile-physics
    /// link (pitch, speed, gravity, collision radius, all per-spell) that
    /// nothing in this codebase currently reads. Surfacing it would be a
    /// scope addition -- a sibling lookup keyed by <c>spellId</c> directly,
    /// unrelated to the <c>SpellVisual</c> graph walked here -- and is left
    /// for a future task.
    /// </para>
    /// </remarks>
    public static class SpellVisualChainResolver
    {
        /// <param name="spellId">The spell to resolve.</param>
        /// <param name="difficulty">
        /// Difficulty id to filter SpellXSpellVisual variants by. Pass -1 for
        /// every difficulty -- 0 is a real difficulty id, so it cannot double
        /// as "all".
        /// </param>
        /// <param name="maxKits">
        /// Ceiling on kits across the whole response (not per variant). A
        /// spell with many difficulty variants fans out fast, and 264047
        /// kit-effect rows sit downstream of this walk.
        /// </param>
        public static SpellVisualChain Resolve(int spellId, int difficulty = 0, int maxKits = 500) =>
            // One index, held for the whole walk. Everything below reads the
            // stores through this snapshot rather than off DBC.DBC directly,
            // so a reload part-way through a call cannot produce a chain
            // stitched together from two generations of client data.
            Resolve(SpellVisualChainIndex.Current(), spellId, difficulty, maxKits);

        /// <summary>
        /// Walks a caller-supplied index instead of the current one. Exists so a
        /// test can resolve against a deliberately incomplete set of stores --
        /// built by <see cref="SpellVisualChainIndex.BuildFrom"/> -- without
        /// assigning null to <c>DBC.DBC</c>'s process-wide statics while other
        /// test classes are reading them.
        /// </summary>
        internal static SpellVisualChain Resolve(
            SpellVisualChainIndex index, int spellId, int difficulty, int maxKits)
        {
            var chain = new SpellVisualChain
            {
                SpellID = spellId,
                Variants = new List<SpellVisualVariant>(),
                UnresolvedReferences = new List<string>(),
            };

            var unresolved = new UnresolvedReferenceSet();

            // ILookup returns an empty sequence for a key it does not hold, so
            // an unknown spell id -- and a missing SpellXSpellVisual store,
            // which yields an empty lookup -- both fall through to the
            // rows.Count == 0 return below. That is the same empty shape as
            // before: whether the store is absent is MissingStores' question to
            // answer, not something to signal by throwing here.
            var rows = index.VariantsBySpell[spellId]
                .Where(x => difficulty < 0 || x.DifficultyID == difficulty)
                .OrderBy(x => x.DifficultyID)
                .ThenByDescending(x => x.Priority)
                .ToList();

            if (rows.Count == 0)
            {
                chain.UnresolvedReferences = unresolved.InOrder;
                return chain;
            }

            // Resolve each row's SpellVisual once, up front -- Storage<T>
            // indexing is a dictionary lookup, not a scan, so this is not the
            // nested-scan shape the design warns against. Guards SpellVisual
            // being null: every lookup then simply misses, and every variant
            // reports VisualResolved = false instead of throwing.
            var resolvedVisuals = new Dictionary<int, SpellVisualEntry>();
            foreach (var row in rows)
            {
                var visualId = (int)row.SpellVisualID;
                if (resolvedVisuals.ContainsKey(visualId))
                    continue;

                if (index.SpellVisual != null && index.SpellVisual.TryGetValue(visualId, out var visual))
                    resolvedVisuals[visualId] = visual;
            }

            var eventsByVisual = index.EventsByVisual;
            var missilesBySet = index.MissilesBySet;

            var variants = new List<SpellVisualVariant>(rows.Count);
            var kitBudget = maxKits;
            var truncated = false;

            foreach (var row in rows)
            {
                var variant = new SpellVisualVariant
                {
                    SpellVisualID = (int)row.SpellVisualID,
                    DifficultyID = row.DifficultyID,
                    Probability = row.Probability,
                    Priority = row.Priority,
                    Kits = new List<SpellVisualKitRef>(),
                };

                if (!resolvedVisuals.TryGetValue(variant.SpellVisualID, out var visual))
                {
                    // Reported, not silently dropped: an id that does not
                    // resolve is a fact about the data the caller needs, and
                    // omitting it looks identical to the spell having no visual.
                    variant.VisualResolved = false;
                    unresolved.Add($"SpellVisual {row.SpellVisualID}");
                    variants.Add(variant);
                    continue;
                }

                variant.VisualResolved = true;

                variant.Kits = GatherKits(visual, variant.SpellVisualID, eventsByVisual, index.SpellVisualKit, ref kitBudget, ref truncated, unresolved);

                variant.Missile = ResolveMissile(visual, missilesBySet, index.SpellMissileMotion, unresolved);

                variants.Add(variant);
            }

            // SpellVisualKitEffect (264047 rows) is grouped by parent kit once
            // per store generation, so this is one keyed lookup per kit rather
            // than the full scan per call it used to be -- the single most
            // expensive thing a spell with no visuals at all still paid for.
            // Every kit already carries an empty effect list, so a missing
            // SpellVisualKitEffect store leaves them empty rather than null.
            var effectsByKit = index.EffectsByKit;
            if (effectsByKit != null)
            {
                foreach (var variant in variants)
                {
                    foreach (var kit in variant.Kits)
                    {
                        kit.Effects = effectsByKit[kit.KitID]
                            .Select(e => new SpellVisualKitEffectInfo { EffectType = e.EffectType, Effect = e.Effect })
                            .ToList();
                    }
                }
            }

            chain.Variants = variants;
            chain.Truncated = truncated;
            chain.UnresolvedReferences = unresolved.InOrder;
            return chain;
        }

        /// <summary>
        /// The whole visual section for one spell at one difficulty: whether the
        /// data is even available, the chain if it is, and the difficulties the
        /// spell does have variants at when the filter emptied the list.
        /// <para>
        /// This is the entry point every host uses -- the text render, the JSON
        /// section and <c>get_spell_visual_chain</c> -- precisely so none of them
        /// can assemble a different answer from the parts. See
        /// <see cref="SpellVisualSection"/> for the divergence that made this
        /// necessary.
        /// </para>
        /// </summary>
        /// <param name="difficulty">
        /// A raw <c>SpellXSpellVisual.DifficultyID</c>, not a <c>MapDifficulty</c>
        /// row id. Pass -1 for every difficulty; 0 is a real difficulty id.
        /// </param>
        public static SpellVisualSection ResolveSection(int spellId, int difficulty = 0, int maxKits = 500) =>
            ResolveSection(SpellVisualChainIndex.Current(), spellId, difficulty, maxKits);

        /// <summary>
        /// <see cref="ResolveSection(int, int, int)"/> against a caller-supplied
        /// index, for the same reason as <see cref="Resolve(SpellVisualChainIndex, int, int, int)"/>:
        /// it is the only way to exercise the store-missing branch of both
        /// renderers without mutating process-wide statics.
        /// </summary>
        internal static SpellVisualSection ResolveSection(
            SpellVisualChainIndex index, int spellId, int difficulty, int maxKits)
        {
            var missingStores = index.MissingStores;

            // Resolved anyway, and deliberately: an unavailable section still
            // carries an empty chain rather than null, so a host that forgets to
            // check Unavailable renders nothing instead of throwing. It is cheap
            // -- a missing store means the lookups it feeds are empty.
            var chain = Resolve(index, spellId, difficulty, maxKits);

            return new SpellVisualSection
            {
                MissingStores = missingStores,
                Chain = chain,
                Difficulty = difficulty,

                // Only asked for when the filter came back empty, because that
                // is the only case where "no variants" is ambiguous. Never asked
                // for when the stores are missing: the answer would be an
                // artefact of the absent store, not a fact about the spell.
                DifficultiesWithVariants = missingStores.Count == 0 && chain.Variants.Count == 0
                    ? DifficultiesFor(index, spellId)
                    : null,
            };
        }

        /// <summary>
        /// The <c>.db2</c> base names of the stores this walk needs that are not
        /// currently loaded, empty when everything it reads is available.
        /// <para>
        /// This is the <b>single</b> answer to "is the visual chain's data
        /// available", and both hosts must ask it rather than each null-checking
        /// whichever stores come to mind. They did the latter once and
        /// disagreed: with only <c>SpellXSpellVisual.db2</c> missing, the MCP
        /// tool returned <c>ok:true</c> with an empty variant list -- which
        /// reads as "this spell has no visuals" -- while <c>get_spell</c>
        /// correctly reported <c>storeUnloaded</c>. <c>DBC.LoadAsync</c>
        /// swallows <c>FileNotFoundException</c> per store, so a partial
        /// extraction is a supported state and this is reachable, not
        /// theoretical.
        /// </para>
        /// <para>
        /// See <see cref="SpellVisualChainIndex.MissingStores"/> for why
        /// <c>SpellMissileMotion</c> is read by this walk but deliberately not
        /// gated on.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> MissingStores() => SpellVisualChainIndex.Current().MissingStores;

        /// <summary>
        /// Every difficulty id this spell has a <c>SpellXSpellVisual</c> row
        /// for, ascending. Callers use it to tell "this spell has no visuals"
        /// apart from "this spell has no visuals <i>at the difficulty you
        /// asked for</i>" -- a distinction that has to be drawn explicitly,
        /// because both look like an empty variant list.
        /// </summary>
        public static IReadOnlyList<int> DifficultiesFor(int spellId) =>
            DifficultiesFor(SpellVisualChainIndex.Current(), spellId);

        internal static IReadOnlyList<int> DifficultiesFor(SpellVisualChainIndex index, int spellId) =>
            index.VariantsBySpell[spellId]
                .Select(x => (int)x.DifficultyID)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

        /// <summary>
        /// Gathers this variant's kit ids as an ordered set -- <paramref
        /// name="visual"/>'s own <c>MissileTargetingKit</c> first
        /// (<c>ReachedVia = "visual"</c>), then <paramref
        /// name="eventsByVisual"/>'s rows for this visual in file order
        /// (<c>"event"</c>) -- skipping ids already seen in this variant
        /// (dedup is per-variant, not global; see the type's own remarks),
        /// resolving each surviving id against <c>SpellVisualKit</c>,
        /// decrementing the *global* <paramref name="kitBudget"/> only for a
        /// kit actually added, and setting <paramref name="truncated"/> when
        /// the budget is spent and a further candidate is left unadded.
        /// </summary>
        private static List<SpellVisualKitRef> GatherKits(
            SpellVisualEntry visual,
            int visualId,
            ILookup<int, SpellVisualEventEntry> eventsByVisual,
            DBFileReaderLib.Storage<SpellVisualKitEntry> spellVisualKit,
            ref int kitBudget,
            ref bool truncated,
            UnresolvedReferenceSet unresolved)
        {
            var kits = new List<SpellVisualKitRef>();
            var seenInVariant = new HashSet<int>();

            // ref/out parameters of the enclosing method cannot be captured
            // by a nested local function (CS1628) -- mirror them into plain
            // locals, mutate those from TryAdd below, and write the final
            // values back to the ref parameters once both directions have
            // been walked.
            var budget = kitBudget;
            var didTruncate = truncated;

            void TryAdd(int kitId, string reachedVia)
            {
                if (kitId == 0)
                    return;
                if (!seenInVariant.Add(kitId))
                    return; // already reached from an earlier direction in this variant

                // Resolvability is decided BEFORE budget, deliberately: an id
                // that does not resolve was never going to be reported at
                // all, regardless of budget, so it must not consume budget
                // or set Truncated -- Truncated promises the response is
                // missing a kit it would otherwise have reported
                // (SpellVisualChain.cs), and an unresolvable id was never
                // going to be one of those. Not reachable with this build's
                // data (both kit-bearing fields measured at 100% resolution,
                // see the class remarks above), so no test exercises this
                // ordering directly -- kept correct for the contract
                // regardless of whether today's data can trigger it.
                if (spellVisualKit == null || !spellVisualKit.ContainsKey(kitId))
                {
                    // Never report an id we could not resolve as though it
                    // were a real kit -- that is the failure mode that makes
                    // the whole tool untrustworthy.
                    unresolved.Add($"SpellVisualKit {kitId}");
                    return;
                }

                if (budget <= 0)
                {
                    didTruncate = true;
                    return;
                }

                kits.Add(new SpellVisualKitRef { KitID = kitId, ReachedVia = reachedVia, Effects = new List<SpellVisualKitEffectInfo>() });
                budget--;
            }

            TryAdd(visual.MissileTargetingKit, "visual");

            if (eventsByVisual != null)
            {
                foreach (var ev in eventsByVisual[visualId])
                    TryAdd(ev.SpellVisualKitID, "event");
            }

            kitBudget = budget;
            truncated = didTruncate;
            return kits;
        }

        /// <summary>
        /// Resolves the missile carrying this variant's projectile via
        /// <paramref name="visual"/>'s <c>SpellVisualMissileSetID</c>, then
        /// that row's <c>SpellMissileMotionID</c> into <c>SpellMissileMotion</c>
        /// for the name. Returns null when the visual carries no missile set,
        /// or when the set does not resolve -- either way that absence is
        /// recorded in <paramref name="unresolved"/> rather than returned as
        /// a fabricated zero-valued object.
        /// </summary>
        private static SpellMissileInfo ResolveMissile(
            SpellVisualEntry visual,
            ILookup<ushort, SpellVisualMissileEntry> missilesBySet,
            DBFileReaderLib.Storage<SpellMissileMotionEntry> spellMissileMotion,
            UnresolvedReferenceSet unresolved)
        {
            if (visual.SpellVisualMissileSetID == 0)
                return null; // legitimately absent: no missile set at all

            if (missilesBySet == null)
                return null; // SpellVisualMissile not loaded; whole-store absence, not a specific unresolved id

            // A SpellVisualMissileSetID can carry more than one
            // SpellVisualMissile row (measured: 730 of 11117 sets do, up to
            // 15 in one set), but SpellMissileInfo is singular per variant --
            // ordering by id picks a stable one rather than whatever order
            // the underlying Storage<T> happens to enumerate in.
            var missile = missilesBySet[visual.SpellVisualMissileSetID].OrderBy(m => m.ID).FirstOrDefault();
            if (missile == null)
            {
                unresolved.Add($"SpellVisualMissile set {visual.SpellVisualMissileSetID}");
                return null;
            }

            var info = new SpellMissileInfo { MissileID = missile.ID, MotionID = missile.SpellMissileMotionID };

            if (missile.SpellMissileMotionID != 0)
            {
                if (spellMissileMotion != null &&
                    spellMissileMotion.TryGetValue(missile.SpellMissileMotionID, out var motion))
                {
                    info.MotionName = motion.Name;
                }
                else
                {
                    unresolved.Add($"SpellMissileMotion {missile.SpellMissileMotionID}");
                }
            }

            return info;
        }

        /// <summary>
        /// The chain's <c>UnresolvedReferences</c> while it is being built: a
        /// list that ignores a reference it already holds.
        /// <para>
        /// One unresolvable id reached from several variants is one fact about
        /// the data, not several. Spell 282039 has five <c>SpellXSpellVisual</c>
        /// rows; before this, a single missing <c>SpellVisual</c> id shared by
        /// all five was reported five times, and the count read as though five
        /// separate things were broken. Insertion order is preserved -- it
        /// follows the walk, which is the order a reader retraces it in -- so
        /// this is a set only in what it rejects.
        /// </para>
        /// </summary>
        private sealed class UnresolvedReferenceSet
        {
            private readonly HashSet<string> _seen = new HashSet<string>();
            private readonly List<string> _inOrder = new List<string>();

            public List<string> InOrder => _inOrder;

            public void Add(string reference)
            {
                if (_seen.Add(reference))
                    _inOrder.Add(reference);
            }
        }
    }
}
