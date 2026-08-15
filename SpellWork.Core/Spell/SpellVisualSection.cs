using System.Collections.Generic;

namespace SpellWork.Spell
{
    /// <summary>
    /// Everything a host needs to render the visual section for one spell at one
    /// difficulty, decided once in Core so no host can decide it differently.
    /// <para>
    /// <b>Why this type exists.</b> Three renderings of this chain ship in this
    /// repo -- <c>SpellInfo.AppendSpellVisualInfo</c> (text),
    /// <c>SpellInfoJsonSerializer.BuildVisual</c> (json) and
    /// <c>VisualTools.GetSpellVisualChain</c> (the MCP tool) -- and each one used
    /// to assemble its own answer from <see cref="SpellVisualChainResolver"/>'s
    /// separate entry points. They did not stay in step: the JSON section and the
    /// tool consulted <see cref="SpellVisualChainResolver.MissingStores"/> while
    /// the text render never did, so a client-data directory missing
    /// <c>SpellVisualEvent.db2</c> made <c>get_spell(format:"text")</c> print a
    /// visual block with no kits under it at all -- which reads as "this spell's
    /// visual reaches no kits" -- while the other two correctly reported the
    /// store was absent. Making the three questions one object means a host can
    /// still choose how to print an unavailable chain, but not whether to notice
    /// it.
    /// </para>
    /// </summary>
    public sealed class SpellVisualSection
    {
        /// <summary>
        /// The <c>.db2</c> base names of the stores this walk needs that are not
        /// loaded -- <see cref="SpellVisualChainResolver.MissingStores"/> as of
        /// the moment this section was resolved. Empty when the chain below is a
        /// complete answer.
        /// </summary>
        public IReadOnlyList<string> MissingStores { get; set; }

        /// <summary>
        /// True when this section cannot answer the question at all, because a
        /// store the walk cannot do without did not load. Every host must branch
        /// on this before reading <see cref="Chain"/>: an empty chain in this
        /// state is an artefact of the missing store, not a fact about the spell.
        /// </summary>
        public bool Unavailable => MissingStores != null && MissingStores.Count > 0;

        /// <summary>
        /// The resolved chain, filtered to <see cref="Difficulty"/>. Present but
        /// empty when <see cref="Unavailable"/> -- never null, so a host that
        /// forgets the branch produces an empty render rather than a crash.
        /// </summary>
        public SpellVisualChain Chain { get; set; }

        /// <summary>The raw <c>SpellXSpellVisual.DifficultyID</c> this section was filtered to.</summary>
        public int Difficulty { get; set; }

        /// <summary>
        /// Null when <see cref="Chain"/> has variants. Otherwise every difficulty
        /// the spell does have variants at, so a list emptied by the filter is
        /// never rendered as "this spell has no visual".
        /// </summary>
        public IReadOnlyList<int> DifficultiesWithVariants { get; set; }
    }
}
