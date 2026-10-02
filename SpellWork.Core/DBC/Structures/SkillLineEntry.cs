// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SkillLineEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SkillLineEntry
    {
        [Index(true)] public int ID;
        public int CategoryId;
        public string Name;
        public string Description;
        public uint SpellIcon;
        public string AlternateVerb;
        public uint CanLink;
        public uint UnkMop1;
        public uint UnkMopFlags1;
    }
}
