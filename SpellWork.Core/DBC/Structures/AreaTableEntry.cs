// CoA 3.3.5a layout: CoA DBCfmt.h AreaTableEntryfmt (36 fields, 144 bytes); names from WoWDBDefs AreaTable.dbd 3.3.5.12340
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class AreaTableEntry
    {
        [Index(true)] public int ID;
        public int ContinentID;
        public int ParentAreaID;
        public int AreaBit;
        public uint Flags;
        public int SoundProviderPref;
        public int SoundProviderPrefUnderwater;
        public int AmbienceID;
        public int ZoneMusic;
        public int IntroSound;
        public int ExplorationLevel;
        [Cardinality(16)] public string[] AreaNameLang = new string[16];
        public uint AreaNameLangFlags;
        public int FactionGroupMask;
        [Cardinality(4)] public int[] LiquidTypeID = new int[4];
        public float MinElevation;
        public float AmbientMultiplier;
        public int LightID;

        public string AreaName => LocaleSlots.Pick(AreaNameLang);
    }
}
