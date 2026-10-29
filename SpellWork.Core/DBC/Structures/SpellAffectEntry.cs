// CoA 3.3.5a layout: CoA-custom table, CoA DBCfmt.h SpellAffectfmt "nii" (3 fields, 12 bytes); names from CoA DBCStructure.h SpellAffectEntry
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellAffectEntry
    {
        [Index(true)] public int ID;
        public int ModifierSpellID;
        /// <summary>Negative means all ranks of the root spell (CoA DBCStructure.h).</summary>
        public int AffectedSpellID;
    }
}
