// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/ScreenEffectEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class ScreenEffectEntry
    {
        [Index(true)] public int ID;
        public string Name;
        public uint Unk0;
        public float Unk1;
        public uint Unk2;
        public uint Unk3;
        public uint Unk4;
        public int Unk5;
        public uint Unk6;
        public uint Unk7;
        public int Unk8;
        public uint Unk9;
        public uint Unk10;
        public uint Unk11;
    }
}
