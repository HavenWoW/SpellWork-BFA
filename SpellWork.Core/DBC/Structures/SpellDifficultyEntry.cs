// CoA 3.3.5a layout: CoA DBCfmt.h SpellDifficultyfmt "niiii" (5 fields, 20 bytes); names from WoWDBDefs SpellDifficulty.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellDifficultyEntry
    {
        [Index(true)] public int ID;
        /// <summary>Spell id per mode: 10N, 25N, 10H, 25H. 0 = no variant for that mode.</summary>
        [Cardinality(4)] public int[] DifficultySpellID = new int[4];
    }
}
