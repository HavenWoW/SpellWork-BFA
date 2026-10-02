// 5.4.8 layout: WoWDBDefs SpellVisualKitAreaModel.dbd BUILD 5.4.0.17266-5.4.8.18414 (not reachable from the chain in 5.4.8: no parent-kit column)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualKitAreaModelEntry
    {
        [Index(true)] public int ID;
        public int ModelFileDataID;
        public int Flags;
        public int LifeTime;
        public float EmissionRate;
        public float Spacing;
        public float ModelScale;
    }
}
