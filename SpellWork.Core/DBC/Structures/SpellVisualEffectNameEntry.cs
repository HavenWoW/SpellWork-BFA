using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEffectNameEntry
    {
        // LAYOUT 10206967, 8AA2AAED (WoWDBDefs SpellVisualEffectName.dbd)
        // marks ID "$noninline,id$" -- Index(true). FieldsCount=14 excludes
        // only ID; unlike the other three tables in this task this one
        // carries no $noninline,relation$ column.
        [Index(true)]
        public int ID;
        public int ModelFileDataID;
        public float BaseMissileSpeed;
        public float Scale;
        public float MinAllowedScale;
        public float MaxAllowedScale;
        public float Alpha;
        public uint Flags;
        public int TextureFileDataID;
        public float EffectRadius;
        public uint Type;
        public int GenericID;
        public uint RibbonQualityID;
        public int DissolveEffectID;
        public int ModelPosition;
    }
}
