// CoA 3.3.5a layout: WoWDBDefs ScreenEffect.dbd 3.3.5.12340 (10 fields, 40 bytes; not loaded by the CoA server)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class ScreenEffectEntry
    {
        [Index(true)] public int ID;
        public string Name;
        public int Effect;
        [Cardinality(4)] public int[] Param = new int[4];
        public int LightParamsID;
        public int SoundAmbienceID;
        public int ZoneMusicID;
    }
}
