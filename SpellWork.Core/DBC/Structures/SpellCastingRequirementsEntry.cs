// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellCastingRequirementsEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellCastingRequirementsEntry
    {
        [Index(true)] public int ID;
        public uint FacingCasterFlags;
        public uint MinFactionId;
        public uint MinReputation;
        public int AreaGroupId;
        public uint RequiredAuraVision;
        public uint RequiresSpellFocus;
    }
}
