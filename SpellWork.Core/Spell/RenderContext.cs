namespace SpellWork.Spell
{
    /// <summary>
    /// The level, item level and difficulty a spell is being rendered at.
    /// Passed explicitly rather than read from statics: the MCP server answers
    /// concurrent requests at different levels. Bring-up (5.5.4): no map
    /// difficulty / scaling stores are loaded, so these are informational.
    /// </summary>
    public sealed class RenderContext
    {
        public RenderContext(uint level, uint itemLevel, int difficultyId = 0)
        {
            Level = level;
            ItemLevel = itemLevel;
            DifficultyId = difficultyId;
        }

        public uint Level { get; }
        public uint ItemLevel { get; }
        public int DifficultyId { get; }

        /// <summary>Snapshots the GUI's selection.</summary>
        public static RenderContext FromGlobals() =>
            new RenderContext(DBC.DBC.SelectedLevel, DBC.DBC.SelectedItemLevel);
    }
}
