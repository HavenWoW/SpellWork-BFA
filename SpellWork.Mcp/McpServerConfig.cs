using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SpellWork;

namespace SpellWork.Mcp
{
    /// <summary>
    /// The nine SPELLWORK_* environment variables, read once at startup.
    /// Never throws: a misconfigured server must still start and still answer
    /// (spec section 9), so a missing required path becomes a null here and a
    /// structured per-tool error later.
    /// </summary>
    public sealed class McpServerConfig
    {
        public string DbcPath { get; private set; }
        public string GtPath { get; private set; }
        public string Locale { get; private set; }
        public string HotfixCachePath { get; private set; }
        public string DefsPath { get; private set; }
        public string Build { get; private set; }
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
                GtPath = Get("SPELLWORK_GT_PATH"),
                Locale = Get("SPELLWORK_LOCALE") ?? "enUS",
                HotfixCachePath = Get("SPELLWORK_HOTFIX_CACHE"),
                DefsPath = Get("SPELLWORK_DEFS_PATH") ?? Path.Combine(AppContext.BaseDirectory, "defs"),
                Build = Get("SPELLWORK_BUILD") ?? "5.5.4.70449",
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
            GtPath = GtPath,
            Locale = Locale,
            HotfixCachePath = HotfixCachePath,
            DefsPath = DefsPath,
            Build = Build,
            MySqlHost = MySqlHost,
            MySqlPortOrPipe = MySqlPortOrPipe,
            MySqlUser = MySqlUser,
            MySqlPassword = MySqlPassword,
            WorldDatabase = WorldDatabase,
            UseDatabase = MySqlConfigured,
        };
    }
}
