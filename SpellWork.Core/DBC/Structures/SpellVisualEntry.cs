// 5.4.8 layout: WoWDBDefs SpellVisual.dbd BUILD 5.4.0.17266-5.4.8.18414 (the .db2, 30 fields; the .dbc of the same name in the reference data is a stale 5.3 layout)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEntry
    {
        [Index(true)] public int ID;
        public int Unk1;
        public int PrecastKit;
        public int CastKit;
        public int ImpactKit;
        public int StateKit;
        public int StateDoneKit;
        public int ChannelKit;
        public int MissileDestinationAttachment;
        public int AnimEventSoundID;
        public int Unk10;
        public int CasterImpactKit;
        public int TargetImpactKit;
        public int MissileAttachment;
        public int MissileMotion;
        public int MissileTargetingKit;
        public int InstantAreaKit;
        public int ImpactAreaKit;
        public int PersistentAreaKit;
        [Cardinality(3)] public float[] MissileCastOffset = new float[3];
        [Cardinality(3)] public float[] MissileImpactOffset = new float[3];
        public int Unk21;
        public int Unk22;
        public int Unk23;
        public int Unk24;
        public int SpellVisualMissileSetID;
    }
}
