namespace SpellWork
{
    public interface ISpellWorkLogger
    {
        void Warn(string message);
    }

    public sealed class NullSpellWorkLogger : ISpellWorkLogger
    {
        public static readonly NullSpellWorkLogger Instance = new NullSpellWorkLogger();

        private NullSpellWorkLogger() { }

        public void Warn(string message) { }
    }
}
