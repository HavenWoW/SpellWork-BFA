// 5.4.8 layout: WoWDBDefs SpellRuneCost.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellRuneCostEntry
    {
        [Index(true)] public int ID;
        public int Blood;
        public int Unholy;
        public int Frost;
        public int Chromatic;
        public int RunicPower;
    }
}
