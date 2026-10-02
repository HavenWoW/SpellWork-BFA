// 5.4.8 layout: WoWDBDefs AreaTable.dbd BUILD 5.1.0.16309-5.4.8.18414 (upstream 5.4.8 puts the first string one column early)
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class AreaTableEntry
    {
        [Index(true)] public int ID;
        public int ContinentID;
        public int ParentAreaID;
        public int AreaBit;
        [Cardinality(2)] public int[] Flags = new int[2];
        public int SoundProviderPref;
        public int SoundProviderPrefUnderwater;
        public int AmbienceID;
        public int ZoneMusic;
        public string ZoneName;
        public int IntroSound;
        public int ExplorationLevel;
        public string AreaName;
        public int FactionGroupMask;
        [Cardinality(4)] public int[] LiquidTypeID = new int[4];
        public float MinElevation;
        public float AmbientMultiplier;
        public int LightID;
        public int MountFlags;
        public int UwIntroSound;
        public int UwZoneMusic;
        public int UwAmbience;
        public int WorldPvpID;
        public int PvpCombatWorldStateID;
        public int WildBattlePetLevelMin;
        public int WildBattlePetLevelMax;
    }
}
