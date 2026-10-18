using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellNameEntry
    {
        [Index(true)]
        public int ID;
        public string Name;
    }
}
