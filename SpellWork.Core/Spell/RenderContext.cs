using System;
namespace SpellWork.Spell
{
    /// <summary>
    /// The level, item level and difficulty a spell is being rendered at.
    /// Passed explicitly rather than read from statics: the MCP server answers
    /// concurrent requests at different levels, and a shared mutable global
    /// would hand the loser of that race a silently wrong number.
    /// </summary>
    public sealed class RenderContext
    {
        /// <param name="difficulty">
        /// The requested SpellDifficulty mode (0 = 10N, 1 = 25N, 2 = 10H,
        /// 3 = 25H), or null for no difficulty resolution (spec D4).
        /// </param>
        public RenderContext(uint level, uint itemLevel, int? difficulty = null)
        {
            if (difficulty.HasValue && (difficulty.Value < 0 || difficulty.Value >= DBC.SpellDifficultyResolver.ModeCount))
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "SpellDifficulty mode is 0-3 (10N, 25N, 10H, 25H).");

            // Levels clamp to 1..MaxLevel (CoA MAX_LEVEL 100).
            Level = Math.Min(Math.Max(level, 1u), (uint)DBC.DBC.MaxLevel);
            ItemLevel = itemLevel;
            Difficulty = difficulty;
        }

        public uint Level { get; }
        public uint ItemLevel { get; }

        /// <summary>The requested SpellDifficulty mode (0-3), or null when none was requested.</summary>
        public int? Difficulty { get; }

        /// <summary>Max level, <see cref="DBC.DBC.DefaultItemLevel"/>, no difficulty.</summary>
        public static RenderContext Default =>
            new RenderContext(DBC.DBC.MaxLevel, DBC.DBC.DefaultItemLevel);
    }
}
