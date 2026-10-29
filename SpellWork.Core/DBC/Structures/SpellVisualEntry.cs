// CoA 3.3.5a layout: CoA DBCfmt.h SpellVisualfmt (32 fields, 128 bytes); names from WoWDBDefs SpellVisual.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEntry
    {
        [Index(true)] public int ID;
        public int PrecastKit;
        public int CastKit;
        public int ImpactKit;
        public int StateKit;
        public int StateDoneKit;
        public int ChannelKit;
        public int HasMissile;
        /// <summary>SpellVisualEffectName id of the missile model.</summary>
        public int MissileModel;
        public int MissilePathType;
        public int MissileDestinationAttachment;
        public int MissileSound;
        public int AnimEventSoundID;
        public int Flags;
        public int CasterImpactKit;
        public int TargetImpactKit;
        public int MissileAttachment;
        public int MissileFollowGroundHeight;
        public int MissileFollowGroundDropSpeed;
        public int MissileFollowGroundApproach;
        public int MissileFollowGroundFlags;
        /// <summary>SpellMissileMotion id per WoWDBDefs; its meaning is verified on data by the visual chain (spec section 5).</summary>
        public int MissileMotion;
        public int MissileTargetingKit;
        public int InstantAreaKit;
        public int ImpactAreaKit;
        public int PersistentAreaKit;
        [Cardinality(3)] public float[] MissileCastOffset = new float[3];
        [Cardinality(3)] public float[] MissileImpactOffset = new float[3];
    }
}
