// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/ItemSparseEntry.cs
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class ItemSparseEntry
    {
        [Index(true)] public int ID;
        public uint Quality;
        public uint Flags;
        public uint Flags2;
        public uint Flags3;
        public float Unk1;
        public float Unk2;
        public uint BuyCount;
        public uint BuyPrice;
        public uint SellPrice;
        public uint InventoryType;
        public int AllowableClass;
        public int AllowableRace;
        public uint ItemLevel;
        public int RequiredLevel;
        public uint RequiredSkill;
        public uint RequiredSkillRank;
        public uint RequiredSpell;
        public uint RequiredHonorRank;
        public uint RequiredCityRank;
        public uint RequiredReputationFaction;
        public uint RequiredReputationRank;
        public uint MaxCount;
        public uint Stackable;
        public uint ContainerSlots;
        [Cardinality(10)] public int[] ItemStatType = new int[10];
        [Cardinality(10)] public uint[] ItemStatValue = new uint[10];
        [Cardinality(10)] public int[] ItemStatUnk1 = new int[10];
        [Cardinality(10)] public int[] ItemStatUnk2 = new int[10];
        public uint ScalingStatDistribution;
        public uint DamageType;
        public uint Delay;
        public float RangedModRange;
        [Cardinality(5)] public int[] SpellId = new int[5];
        [Cardinality(5)] public int[] SpellTrigger = new int[5];
        [Cardinality(5)] public int[] SpellCharges = new int[5];
        [Cardinality(5)] public int[] SpellCooldown = new int[5];
        [Cardinality(5)] public int[] SpellCategory = new int[5];
        [Cardinality(5)] public int[] SpellCategoryCooldown = new int[5];
        public uint Bonding;
        public string Name;
        public string Name2;
        public string Name3;
        public string Name4;
        public string Description;
        public uint PageText;
        public uint LanguageID;
        public uint PageMaterial;
        public uint StartQuest;
        public uint LockID;
        public int Material;
        public uint Sheath;
        public uint RandomProperty;
        public uint RandomSuffix;
        public uint ItemSet;
        public uint Area;
        public uint Map;
        public uint BagFamily;
        public uint TotemCategory;
        [Cardinality(3)] public uint[] Color = new uint[3];
        [Cardinality(3)] public uint[] Content = new uint[3];
        public int SocketBonus;
        public uint GemProperties;
        public float ArmorDamageModifier;
        public uint Duration;
        public uint ItemLimitCategory;
        public uint HolidayId;
        public float StatScalingFactor;
        public int CurrencySubstitutionId;
        public int CurrencySubstitutionCount;
    }
}
