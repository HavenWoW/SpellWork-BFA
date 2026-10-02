// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellRadiusEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellRadiusEntry
    {
        [Index(true)] public int ID;
        public float Radius;
        public float RadiusPerLevel;
        public float UnkRadiusMop;
        public float MaxRadius;
    }
}
