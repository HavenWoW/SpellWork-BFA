using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualMissileEntry
    {
        // LAYOUT 9B2241BE, BE91C62B (WoWDBDefs SpellVisualMissile.dbd):
        //   CastOffset[3]
        //   ImpactOffset[3]
        //   $id$ID<32>
        //   SpellVisualEffectNameID<u16>
        //   ...
        //   $noninline,relation$SpellVisualMissileSetID<u16>
        // Unlike every other table in this file, ID is plain "$id$" (inline)
        // AND it is not declared first -- it sits third, after the two
        // offset arrays, in the file's own field order. DBFileReaderLib maps
        // inline fields by declaration order, so ID's field position here
        // must match that: third, not first. Index(false) because it is the
        // inline form (compare SpellXSpellVisualEntry), not the noninline
        // form used by SpellVisualEntry/SpellVisualEventEntry above.
        [Cardinality(3)]
        public float[] CastOffset = new float[3];
        [Cardinality(3)]
        public float[] ImpactOffset = new float[3];
        [Index(false)]
        public int ID;
        public ushort SpellVisualEffectNameID;
        public uint SoundEntriesID;
        public sbyte Attachment;
        public sbyte DestinationAttachment;
        public ushort CastPositionerID;
        public ushort ImpactPositionerID;
        public int FollowGroundHeight;
        public uint FollowGroundDropSpeed;
        public ushort FollowGroundApproach;
        public uint Flags;
        public ushort SpellMissileMotionID;
        public uint AnimKitID;
        public sbyte ClutterLevel;
        // "$noninline,relation$SpellVisualMissileSetID<u16>" -- the parent
        // SpellVisualMissileSet id. Plain field, no attribute, declared last:
        // same pattern as the relation columns above.
        public ushort SpellVisualMissileSetID;
    }
}
