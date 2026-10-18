using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellClassOptionsEntry
    {
        [Index(true)]
        public int ID;
        public int SpellID;
        public uint ModalNextSpell;
        public byte SpellClassSet;
        [Cardinality(4)]
        public int[] SpellClassMask = new int[4];
    }
}
