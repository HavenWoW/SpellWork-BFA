// CoA 3.3.5a layout: CoA DBCfmt.h SkillLineAbilityfmt (14 fields, 56 bytes); names from WoWDBDefs SkillLineAbility.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SkillLineAbilityEntry
    {
        [Index(true)] public int ID;
        public int SkillLine;
        public int Spell;
        public uint RaceMask;
        public uint ClassMask;
        public uint ExcludeRace;
        public uint ExcludeClass;
        public int MinSkillLineRank;
        public int SupercededBySpell;
        public int AcquireMethod;
        public int TrivialSkillLineRankHigh;
        public int TrivialSkillLineRankLow;
        [Cardinality(2)] public int[] CharacterPoints = new int[2];
    }
}
