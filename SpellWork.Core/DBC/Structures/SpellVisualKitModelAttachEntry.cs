// CoA 3.3.5a layout: WoWDBDefs SpellVisualKitModelAttach.dbd 3.3.5.12340 (10 fields, 40 bytes; not loaded by the CoA server)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualKitModelAttachEntry
    {
        [Index(true)] public int ID;
        public int ParentSpellVisualKitID;
        public int SpellVisualEffectNameID;
        public int AttachmentID;
        [Cardinality(3)] public float[] Offset = new float[3];
        public float Yaw;
        public float Pitch;
        public float Roll;
    }
}
