// CoA 3.3.5a layout: WoWDBDefs SpellMissile.dbd 3.3.5.12340 (15 fields, 60 bytes; not loaded by the CoA server)
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
