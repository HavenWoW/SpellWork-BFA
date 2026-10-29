// CoA 3.3.5a layout: CoA DBCfmt.h AreaGroupEntryfmt "niiiiiii" (8 fields, 32 bytes); names from WoWDBDefs AreaGroup.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class AreaGroupEntry
    {
        [Index(true)] public int ID;
        [Cardinality(6)] public int[] AreaID = new int[6];
        public int NextAreaID;
    }
}
