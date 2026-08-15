using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualKitModelAttachEntry
    {
        // LAYOUT 75650E39, B8BDA6F9 (WoWDBDefs SpellVisualKitModelAttach.dbd).
        // Unlike the other three tables in this task, ID here is plain
        // "$id$" (inline), not "$noninline,id$", and it is not declared
        // first in the file's own field order -- it sits third, after the
        // two offset arrays (same pattern as SpellVisualMissileEntry.ID).
        // DBFileReaderLib maps inline fields by declaration order, so
        // Offset/OffsetVariation must stay ahead of ID here. Index(false)
        // because it is the inline form. FieldsCount=21 counts this inline
        // ID (rule 2) and excludes only the trailing relation column.
        [Cardinality(3)]
        public float[] Offset = new float[3];
        [Cardinality(3)]
        public float[] OffsetVariation = new float[3];
        [Index(false)]
        public int ID;
        public ushort SpellVisualEffectNameID;
        public sbyte AttachmentID;
        public ushort PositionerID;
        public float Yaw;
        public float Pitch;
        public float Roll;
        public float YawVariation;
        public float PitchVariation;
        public float RollVariation;
        public float Scale;
        public float ScaleVariation;
        public short StartAnimID;
        public short AnimID;
        public short EndAnimID;
        public ushort AnimKitID;
        public byte Flags;
        public uint LowDefModelAttachID;
        public float StartDelay;
        // "$noninline,relation$ParentSpellVisualKitID<32>" -- the parent
        // SpellVisualKit id. Plain field, no attribute, declared last: same
        // pattern as the relation columns above.
        public int ParentSpellVisualKitID;
    }
}
