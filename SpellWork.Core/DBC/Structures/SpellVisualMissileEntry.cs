// 5.4.8 layout: WoWDBDefs SpellVisualMissile.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualMissileEntry
    {
        [Index(true)] public int ID;
        public int SpellVisualMissileSetID;
        public int SpellVisualEffectNameID;
        public int DestinationAttachment;
        public int SoundEntriesID;
        public int Attachment;
        public int Unk6;
        public int FollowGroundDropSpeed;
        public int FollowGroundApproach;
        public int Flags;
        public int SpellMissileMotionID;
        [Cardinality(3)] public float[] ImpactOffset = new float[3];
        [Cardinality(3)] public float[] CastOffset = new float[3];
    }
}
