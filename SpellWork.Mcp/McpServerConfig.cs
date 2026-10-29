using System;
using System.Collections;
using System.Collections.Generic;
using SpellWork;

namespace SpellWork.Mcp
{
    /// <summary>
    /// The SPELLWORK_* environment variables, read once at startup.
    /// Never throws: a misconfigured server must still start and still answer
    /// (spec section 9), so a missing required path becomes a null here and a
    /// structured per-tool error later.
    /// </summary>
    public sealed class McpServerConfig
    {
        public string DbcPath { get; private set; }

        /// <summary>
        /// SPELLWORK_DB2_PATH and SPELLWORK_GT_PATH: accepted so a 5.4.8
        /// registration still starts, and ignored (spec section 7). 3.3.5a
        /// has no .db2 files and no gt tables; the loader reads .dbc files
        /// from DbcPath only, so neither value is passed on.
        /// </summary>
        public string Db2Path { get; private set; }
        public string GtPath { get; private set; }

        /// <summary>The accepted-and-ignored variables that are set, in name order.</summary>
        public IReadOnlyList<string> IgnoredVariables
        {
            get
            {
                var ignored = new List<string>();
                if (Db2Path != null)
                    ignored.Add("SPELLWORK_DB2_PATH");
                if (GtPath != null)
                    ignored.Add("SPELLWORK_GT_PATH");
                return ignored;
            }
        }
        public string Locale { get; private set; }
        public string MySqlHost { get; private set; }
        public string MySqlPortOrPipe { get; private set; }
        public string MySqlUser { get; private set; }
        public string MySqlPassword { get; private set; }
        public string WorldDatabase { get; private set; }

        /// <summary>
        /// All three of host, user and database, or none of the MySQL tools
        /// work. A half-set config produces a driver error the caller cannot
        /// act on; "not configured" is actionable.
        /// </summary>
        public bool MySqlConfigured =>
            !string.IsNullOrWhiteSpace(MySqlHost) &&
            !string.IsNullOrWhiteSpace(MySqlUser) &&
            !string.IsNullOrWhiteSpace(WorldDatabase);

        public static McpServerConfig FromEnvironment()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                values[(string)entry.Key] = entry.Value as string;

            return FromEnvironment(values);
        }

        public static McpServerConfig FromEnvironment(IDictionary<string, string> environment)
        {
            string Get(string key) =>
                environment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

            return new McpServerConfig
            {
                DbcPath = Get("SPELLWORK_DBC_PATH"),
                Db2Path = Get("SPELLWORK_DB2_PATH"),
                GtPath = Get("SPELLWORK_GT_PATH"),
                Locale = Get("SPELLWORK_LOCALE") ?? "enUS",
                MySqlHost = Get("SPELLWORK_MYSQL_HOST"),
                MySqlPortOrPipe = Get("SPELLWORK_MYSQL_PORT") ?? "3306",
                MySqlUser = Get("SPELLWORK_MYSQL_USER"),
                MySqlPassword = Get("SPELLWORK_MYSQL_PASS") ?? string.Empty,
                WorldDatabase = Get("SPELLWORK_MYSQL_WORLD_DB"),
            };
        }

        public SpellWorkConfig ToSpellWorkConfig() => new SpellWorkConfig
        {
            DbcPath = DbcPath,
            Locale = Locale,
            MySqlHost = MySqlHost,
            MySqlPortOrPipe = MySqlPortOrPipe,
            MySqlUser = MySqlUser,
            MySqlPassword = MySqlPassword,
            WorldDatabase = WorldDatabase,
            UseDatabase = MySqlConfigured,
        };
    }
}
