// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellMissileEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellMissileEntry
    {
        [Index(true)] public int ID;
        public uint Flags;
        public float DefaultPitchMin;
        public float DefaultPitchMax;
        public float DefaultSpeedMin;
        public float DefaultSpeedMax;
        public float RandomizeFacingMin;
        public float RandomizeFacingMax;
        public float RandomizePitchMin;
        public float RandomizePitchMax;
        public float RandomizeSpeedMin;
        public float RandomizeSpeedMax;
        public float Gravity;
        public float MaxDuration;
        public float CollisionRadius;
    }
}
