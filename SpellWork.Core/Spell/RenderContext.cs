using SpellWork.DBC.Structures;

namespace SpellWork.Spell
{
    /// <summary>
    /// The level, item level and map difficulty a spell is being rendered at.
    /// Passed explicitly rather than read from statics: the MCP server answers
    /// concurrent requests at different levels, and a shared mutable global
    /// would hand the loser of that race a silently wrong number.
    /// </summary>
    public sealed class RenderContext
    {
        public RenderContext(uint level, uint itemLevel, MapDifficultyEntry mapDifficulty)
        {
            Level = level;
            ItemLevel = itemLevel;
            MapDifficulty = mapDifficulty;
        }

        public uint Level { get; }
        public uint ItemLevel { get; }
        public MapDifficultyEntry MapDifficulty { get; }

        /// <summary>
        /// Snapshots the GUI's selection. This is the only supported read of
        /// <see cref="DBC.DBC.SelectedLevel"/> and friends: the WinForms app
        /// owns that state, everything else passes a context explicitly.
        /// </summary>
        public static RenderContext FromGlobals() =>
            new RenderContext(DBC.DBC.SelectedLevel, DBC.DBC.SelectedItemLevel, DBC.DBC.SelectedMapDifficulty);
    }
}
