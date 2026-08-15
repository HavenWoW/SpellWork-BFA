using System.Globalization;

namespace SpellWork.Database
{
    public sealed class SpellProcEventEntry
    {
        public uint Id;
        public string SpellName;
        public byte SchoolMask;
        public ushort SpellFamilyName;
        public uint[] SpellFamilyMask;
        public uint ProcFlags;
        public uint ProcEx;
        public float PpmRate;
        public float CustomChance;
        public uint Cooldown;

        public string[] ToArray()
        {
            return new[]
            {
                Id.ToString(),
                SpellName,
                SchoolMask.ToString(),
                SpellFamilyName.ToString(),
                SpellFamilyMask[0].ToString(),
                SpellFamilyMask[1].ToString(),
                SpellFamilyMask[2].ToString(),
                ProcFlags.ToString(),
                ProcEx.ToString(),
                PpmRate.ToString(CultureInfo.InvariantCulture),
                CustomChance.ToString(CultureInfo.InvariantCulture),
                Cooldown.ToString()
            };
        }
    }
}
