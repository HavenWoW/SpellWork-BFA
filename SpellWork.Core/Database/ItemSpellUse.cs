namespace SpellWork.Database
{
    /// <summary>
    /// One item_template spell slot naming a spell: the item, its name, the
    /// slot (1-5) and that slot's spelltrigger (ItemSpelltriggerType).
    /// </summary>
    public sealed class ItemSpellUse
    {
        public int Entry { get; set; }
        public string Name { get; set; }
        public int Slot { get; set; }
        public int Trigger { get; set; }
    }
}
