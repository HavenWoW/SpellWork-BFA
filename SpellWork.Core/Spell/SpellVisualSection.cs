using System.Collections.Generic;

namespace SpellWork.Spell
{
    /// <summary>
    /// The one answer every rendering of the visual chain branches on (text
    /// block, get_spell's JSON <c>visual</c>, get_spell_visual_chain). Deciding
    /// "is the data there" once, here, means no host can decide it differently.
    /// </summary>
    public sealed class SpellVisualSection
    {
        /// <summary>Gated stores that did not load. Non-empty means storeUnloaded.</summary>
        public IReadOnlyList<string> MissingStores { get; set; }

        /// <summary>Null when <see cref="MissingStores"/> is non-empty: an empty
        /// chain in that state would read as a fact about the spell.</summary>
        public SpellVisualChain Chain { get; set; }

        public bool Unavailable => MissingStores != null && MissingStores.Count > 0;
    }
}
