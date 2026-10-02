// 5.4.8 layout: WoWDBDefs SpellVisualKitModelAttach.dbd BUILD 5.4.0.17266-5.4.8.18414
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
        public int Unk8;
        public int StartAnimID;
        public int AnimID;
        public int EndAnimID;
        public int AnimKitID;
    }
}
