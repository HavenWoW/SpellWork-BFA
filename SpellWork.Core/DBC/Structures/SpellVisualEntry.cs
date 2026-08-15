using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEntry
    {
        // LAYOUT 514F85ED, 17962AA0 (WoWDBDefs SpellVisual.dbd) marks ID
        // "$noninline,id$" -- stored in the file's id-list section, not the
        // row's inline bytes -- so this is Index(true), matching every other
        // noninline-id table in this codebase (SpellMissileEntry,
        // SpellEffectEntry). Do not copy [Index(false)] from a draft that
        // read this as inline; that value is for SpellXSpellVisualEntry-style
        // "$id$" tables, not this one.
        [Index(true)]
        public int ID;
        [Cardinality(3)]
        public float[] MissileCastOffset = new float[3];
        [Cardinality(3)]
        public float[] MissileImpactOffset = new float[3];
        public uint AnimEventSoundID;
        public int Flags;
        public sbyte MissileAttachment;
        public sbyte MissileDestinationAttachment;
        public uint MissileCastPositionerID;
        public uint MissileImpactPositionerID;
        public int MissileTargetingKit;
        public uint HostileSpellVisualID;
        public uint CasterSpellVisualID;
        public ushort SpellVisualMissileSetID;
        public ushort DamageNumberDelay;
        public uint LowViolenceSpellVisualID;
        public uint RaidSpellVisualMissileSetID;
    }
}
