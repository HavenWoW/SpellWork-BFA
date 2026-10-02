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
        /// A Difficulty.dbc id; 0 is the base difficulty (no Difficulty row).
        /// </param>
        public RenderContext(uint level, uint itemLevel, int difficulty)
        {
            Level = level;
            ItemLevel = itemLevel;
            Difficulty = difficulty;
        }

        public uint Level { get; }
        public uint ItemLevel { get; }
        public int Difficulty { get; }

        /// <summary>
        /// Snapshots the process-wide selection in <see cref="DBC.DBC.SelectedLevel"/>
        /// and friends. Everything that can pass a context explicitly does.
        /// </summary>
        public static RenderContext FromGlobals() =>
            new RenderContext(DBC.DBC.SelectedLevel, DBC.DBC.SelectedItemLevel, DBC.DBC.SelectedDifficulty);
    }
}
