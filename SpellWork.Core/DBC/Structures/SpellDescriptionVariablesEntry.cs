// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/SpellDescriptionVariablesEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellDescriptionVariablesEntry
    {
        [Index(true)] public int ID;
        public string Variables;
    }
}
