// 5.4.8 layout: WoWDBDefs SpellCooldowns.dbd BUILD 5.4.0.17266-5.4.8.18414
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellCooldownsEntry
    {
        [Index(true)] public int ID;
        public int SpellID;
        public int DifficultyID;
        public int CategoryRecoveryTime;
        public int RecoveryTime;
        public int StartRecoveryTime;
    }
}
