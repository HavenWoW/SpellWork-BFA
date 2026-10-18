using SpellWork.DBC;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellEntry
    {
        [Index(true)]
        public int ID;
        public string NameSubtext;
        public string Description;
        public string AuraDescription;
    }
}
