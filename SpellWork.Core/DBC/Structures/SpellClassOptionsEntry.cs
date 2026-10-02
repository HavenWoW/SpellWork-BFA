// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellClassOptionsEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellClassOptionsEntry
    {
        [Index(true)] public int ID;
        public uint ModalNextSpell;
        [Cardinality(4)] public uint[] SpellFamilyFlags = new uint[4];
        public uint SpellFamilyName;
    }
}
