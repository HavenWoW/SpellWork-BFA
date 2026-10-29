using System;
using System.Collections.Generic;

namespace SpellWork.Spell
{
    /// <summary>
    /// The JSON <c>difficulty</c> block (spec D4): the SpellDifficulty mode
    /// the caller asked for (null when none was asked for), the spell id that
    /// mode resolves to, and every variant of the spell's SpellDifficulty row.
    /// A tool that resolves a requested id to a different spell passes the
    /// view it resolved with, so the document can say both.
    /// </summary>
    public sealed class SpellDifficultyView
    {
        public SpellDifficultyView(int? requested, int resolvedSpellId, IReadOnlyList<int> variants)
        {
            Requested = requested;
            ResolvedSpellId = resolvedSpellId;
            Variants = variants ?? Array.Empty<int>();
        }

        public int? Requested { get; }
        public int ResolvedSpellId { get; }
        public IReadOnlyList<int> Variants { get; }

        /// <summary>
        /// Resolves <paramref name="spellId"/> through <see cref="DBC.DBC.DifficultyResolver"/>.
        /// With no <paramref name="mode"/> the id is kept as given (no
        /// resolution); the variants are reported either way.
        /// </summary>
        public static SpellDifficultyView For(int spellId, int? mode)
        {
            var resolver = DBC.DBC.DifficultyResolver;
            var (resolved, variants) = resolver.Resolve(spellId, mode ?? 0);
            return new SpellDifficultyView(mode, mode.HasValue ? resolved : spellId, variants);
        }
    }
}
