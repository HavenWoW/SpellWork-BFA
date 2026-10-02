namespace SpellWork
{
    public class SpellWorkConfig
    {
        public string DbcPath { get; set; }
        public string Db2Path { get; set; }
        public string GtPath { get; set; }
        public string Locale { get; set; } = "enUS";

        public string MySqlHost { get; set; }
        public string MySqlPortOrPipe { get; set; }
        public string MySqlUser { get; set; }
        public string MySqlPassword { get; set; }
        public string WorldDatabase { get; set; }
        public bool UseDatabase { get; set; }
    }
}
