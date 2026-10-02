// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SkillLineAbilityEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SkillLineAbilityEntry
    {
        [Index(true)] public int ID;
        public uint SkillId;                                        // m_skillLine
        public uint SpellId;                                        // m_spell
        public uint Racemask;                                       // m_raceMask
        public uint Classmask;                                      // m_classMask
        public uint RacemaskNot;                                    // m_excludeRace
        public uint ClassmaskNot;                                   // m_excludeClass
        public uint ReqSkillValue;                                  // m_minSkillLineRank
        public uint ForwardSpellid;                                 // m_supercededBySpell
        public uint LearnOnGetSkill;                                // m_acquireMethod
        public uint MaxValue;                                       // m_trivialSkillLineRankHigh
        public uint MinValue;                                       // m_trivialSkillLineRankLow
        public uint CharacterPoints;                                // m_characterPoints - MoP: no longer an array
    }
}
