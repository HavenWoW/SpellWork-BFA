using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // No MySQL server is started, reached or required by anything in this
    // file. The "not configured" path is the one that always runs in CI, and
    // the guard runs ahead of every connection attempt by design.
    public class DatabaseToolsTests
    {
        private static DatabaseTools ToolsWithoutMySql()
        {
            // FromEnvironment with an empty dictionary: no SPELLWORK_MYSQL_*
            // variables, so MySqlConfigured is false however the machine
            // running the suite happens to be set up. Nothing here calls
            // GetAsync(), so no client-data load is triggered either.
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>());
            return new DatabaseTools(new SpellDataService(config, NullLoggerFactoryStub.Logger));
        }

        // Spec section 9: MySQL being unconfigured is reported in the
        // response, and every other tool is unaffected.
        [Fact]
        public async Task QueryWorldDb_WithoutConfiguration_SaysSo()
        {
            var json = await ToolsWithoutMySql().QueryWorldDb("SELECT 1");
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("mysql_not_configured", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        // The guard runs before anything touches the network, so this
        // rejection is observable with no server present -- and that is
        // exactly the property that makes the guard testable.
        [Fact]
        public async Task QueryWorldDb_RejectsNonSelect_BeforeReachingTheDatabase()
        {
            var json = await ToolsWithoutMySql().QueryWorldDb("DROP TABLE spell_proc");
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("sql_rejected", error.GetProperty("code").GetString());
            Assert.Contains("SELECT", error.GetProperty("message").GetString());
        }

        [Fact]
        public async Task QueryWorldDb_RejectsStackedStatements()
        {
            var json = await ToolsWithoutMySql().QueryWorldDb("SELECT 1; DROP TABLE spell_proc");
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("sql_rejected", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        // Ordering, stated as a test rather than left to a comment: an
        // implementation that checked MySqlConfigured first would answer
        // "mysql_not_configured" here and the caller would never learn that
        // the query itself is the problem. It would also make every guard
        // rejection unobservable without a server.
        [Fact]
        public async Task QueryWorldDb_GuardRunsBeforeTheConfigurationCheck()
        {
            var json = await ToolsWithoutMySql().QueryWorldDb("SELECT 1 ; ");
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("sql_rejected", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [Fact]
        public async Task GetSpellDbOverrides_WithoutConfiguration_SaysSo()
        {
            var json = await ToolsWithoutMySql().GetSpellDbOverrides(133);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("mysql_not_configured", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        // query_world_db builds its own envelope rather than going through
        // ToolResponse.Paged (it stops reading at the limit, so it has no
        // total to report), which means the value it echoes as "limit" has to
        // be made to agree with Paged's by hand. Math.Min(limit, MaxRows)
        // alone does not: it echoes -5 back to a caller that received zero
        // rows, while every other paging tool would report 0. The echo is the
        // only thing clamped here -- the row count is unchanged, since a limit
        // of 0 and a limit of -5 both return no rows.
        [Fact]
        public void EffectiveLimit_AgreesWithPaged()
        {
            foreach (var requested in new[] { -5, -1, 0, 1, 100, ToolResponse.MaxRows, ToolResponse.MaxRows + 1, 100000 })
            {
                var paged = ToolResponse.Paged(new List<object>(), 0, 0, requested);
                Assert.Equal((int)paged["limit"], DatabaseTools.EffectiveLimit(requested));
            }

            Assert.Equal(0, DatabaseTools.EffectiveLimit(-5));
            Assert.Equal(ToolResponse.MaxRows, DatabaseTools.EffectiveLimit(100000));
        }

        // Two of the seven override tables are absent from the world schema
        // this server is primarily built against, so this classification runs
        // on every real call. A genuine fault -- a permission denial, a syntax
        // error, a wrong id column -- must never be filed as "that table just
        // isn't in this schema", which is what an implementation matching on
        // message text would eventually do.
        [Fact]
        public void IsMissingTable_OnlyForErrorNoSuchTable()
        {
            Assert.False(DatabaseTools.IsMissingTable(new InvalidOperationException("Table 'x' doesn't exist")));
            Assert.False(DatabaseTools.IsMissingTable(new Exception("Unknown column 'entry' in 'where clause'")));

            Assert.True(DatabaseTools.IsMissingTable(MySqlError(DatabaseTools.MySqlErrorNoSuchTable)));
            Assert.False(DatabaseTools.IsMissingTable(MySqlError(1054)));   // unknown column
            Assert.False(DatabaseTools.IsMissingTable(MySqlError(1142)));   // SELECT command denied
        }

        [Fact]
        public void OverrideTables_AreTheAzerothCoreSchemaList()
        {
            Assert.Equal(new[]
            {
                ("spell_proc", "SpellId"),
                ("spell_proc_event", "entry"),
                ("spell_script_names", "spell_id"),
                ("spell_custom_attr", "spell_id"),
                ("spell_bonus_data", "entry"),
                ("spell_threat", "entry"),
                ("spell_ranks", "spell_id"),
                ("spell_linked_spell", "spell_trigger"),
                ("spell_dbc", "ID"),
                ("spell_cooldown_overrides", "Id"),
                ("spell_area", "spell"),
                ("spell_group", "spell_id"),
                ("spell_required", "spell_id"),
            }, DatabaseTools.OverrideTables);
        }

        // Live-MySQL tests: skipped unless SPELLWORK_TEST_MYSQL_HOST is set.
        private static DatabaseTools ToolsWithMySql()
        {
            var host = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_HOST");
            Skip.If(string.IsNullOrWhiteSpace(host), "SPELLWORK_TEST_MYSQL_HOST not set.");

            var values = new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = host,
                ["SPELLWORK_MYSQL_PORT"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PORT") ?? "3306",
                ["SPELLWORK_MYSQL_USER"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_USER") ?? "",
                ["SPELLWORK_MYSQL_PASS"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PASS") ?? "",
                ["SPELLWORK_MYSQL_WORLD_DB"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world",
            };

            var config = McpServerConfig.FromEnvironment(values);
            return new DatabaseTools(new SpellDataService(config, NullLoggerFactoryStub.Logger));
        }

        [SkippableFact]
        public void SelectProc_ReadsPhaseSetColumnWithoutThrowing()
        {
            var host = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_HOST");
            Skip.If(string.IsNullOrWhiteSpace(host), "SPELLWORK_TEST_MYSQL_HOST not set.");

            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = host,
                ["SPELLWORK_MYSQL_PORT"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PORT") ?? "3306",
                ["SPELLWORK_MYSQL_USER"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_USER") ?? "",
                ["SPELLWORK_MYSQL_PASS"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PASS") ?? "",
                ["SPELLWORK_MYSQL_WORLD_DB"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world",
            });
            var db = new SpellWork.Database.WorldDatabase(config.ToSpellWorkConfig());
            var rows = db.SelectProc("SELECT entry, SchoolMask, SpellFamilyName, SpellFamilyMask0, SpellFamilyMask1, SpellFamilyMask2, procFlags, procEx, procPhase, ppmRate, CustomChance, Cooldown FROM spell_proc_event LIMIT 20");
            Assert.NotEmpty(rows);
        }

        [Fact]
        public void ReadPhase_ParsesSetStringsAndNumbers()
        {
            Assert.Equal(1u, SpellWork.Database.WorldDatabase.ReadPhase("PROC_PHASE_HIT"));
            Assert.Equal(6u, SpellWork.Database.WorldDatabase.ReadPhase("PROC_PHASE_CAST,PROC_PHASE_AFTER_CAST"));
            Assert.Equal(0u, SpellWork.Database.WorldDatabase.ReadPhase(""));
            Assert.Equal(3u, SpellWork.Database.WorldDatabase.ReadPhase(3));
        }

        [SkippableFact]
        public async Task EveryListedTableIsQueriedOrReportedAbsent()
        {
            var result = JsonDocument.Parse(await ToolsWithMySql().GetSpellDbOverrides(133)).RootElement.GetProperty("result");
            var tables = result.GetProperty("tables");
            var missing = result.TryGetProperty("missingTables", out var m)
                ? m.EnumerateArray().Select(e => e.GetString()).ToHashSet()
                : new HashSet<string>();
            var unavailable = result.TryGetProperty("unavailableTables", out var u)
                ? u.EnumerateObject().Select(p => p.Name).ToHashSet()
                : new HashSet<string>();

            Assert.Empty(unavailable);
            foreach (var (table, _) in DatabaseTools.OverrideTables)
                Assert.True(tables.TryGetProperty(table, out _) || missing.Contains(table), table);
            Assert.DoesNotContain("spellmisc_dbc", tables.EnumerateObject().Select(p => p.Name));
        }

        [Fact]
        public void KeysFor_FollowsEachTablesLoaderConvention()
        {
            // id 20, first rank 10
            Assert.Equal(new[] { (20L, "exact"), (-10L, "allRanks") }, DatabaseTools.KeysFor("spell_proc", 20, 10));
            Assert.Equal(new[] { (20L, "exact"), (-10L, "allRanks") }, DatabaseTools.KeysFor("spell_proc_event", 20, 10));
            Assert.Equal(new[] { (20L, "exact"), (-10L, "allRanks") }, DatabaseTools.KeysFor("spell_script_names", 20, 10));
            Assert.Equal(new[] { (20L, "exact"), (10L, "firstRank") }, DatabaseTools.KeysFor("spell_group", 20, 10));
            Assert.Equal(new[] { (20L, "exact"), (-20L, "onRemoval") }, DatabaseTools.KeysFor("spell_linked_spell", 20, 10));
            Assert.Equal(new[] { (20L, "exact"), (10L, "firstRank") }, DatabaseTools.KeysFor("spell_bonus_data", 20, 10));
            Assert.Equal(new[] { (20L, "exact"), (10L, "firstRank") }, DatabaseTools.KeysFor("spell_threat", 20, 10));
            Assert.Equal(new[] { (20L, "exact") }, DatabaseTools.KeysFor("spell_custom_attr", 20, 10));
            // a first-rank spell is its own first rank: the group key collapses
            Assert.Equal(new[] { (10L, "exact") }, DatabaseTools.KeysFor("spell_group", 10, 10));
            Assert.Equal(new[] { (10L, "exact"), (-10L, "allRanks") }, DatabaseTools.KeysFor("spell_proc", 10, 10));
            foreach (var table in DatabaseTools.MatchRules.Keys)
                Assert.Contains(DatabaseTools.OverrideTables, t => t.Table == table);
        }

        // SpellMgr.cpp:990-1019: spell_bonus_data / spell_threat use the
        // exact row, else the first rank's row -- never both.
        [Theory]
        [InlineData("spell_bonus_data")]
        [InlineData("spell_threat")]
        public void ApplyFallback_KeepsTheFirstRankRowOnlyWithoutAnExactRow(string table)
        {
            Dictionary<string, object> Row(string matchedBy) => new Dictionary<string, object> { ["matchedBy"] = matchedBy };

            var onlyFirst = DatabaseTools.ApplyFallback(table, new List<Dictionary<string, object>> { Row("firstRank") });
            Assert.Equal("firstRank", Assert.Single(onlyFirst)["matchedBy"]);

            var both = DatabaseTools.ApplyFallback(table, new List<Dictionary<string, object>> { Row("firstRank"), Row("exact") });
            Assert.Equal("exact", Assert.Single(both)["matchedBy"]);

            // spell_group is additive (membership covers every rank): untouched.
            var group = DatabaseTools.ApplyFallback("spell_group", new List<Dictionary<string, object>> { Row("firstRank"), Row("exact") });
            Assert.Equal(2, group.Count);
        }

        [SkippableTheory]
        [InlineData("spell_bonus_data")]
        [InlineData("spell_threat")]
        public async Task FirstRankRow_IsReturnedForALaterRankSpellWithoutItsOwnRow(string table)
        {
            var tools = ToolsWithMySql();
            var db = new SpellWork.Database.WorldDatabase(McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_HOST"),
                ["SPELLWORK_MYSQL_PORT"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PORT") ?? "3306",
                ["SPELLWORK_MYSQL_USER"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_USER") ?? "",
                ["SPELLWORK_MYSQL_PASS"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PASS") ?? "",
                ["SPELLWORK_MYSQL_WORLD_DB"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world",
            }).ToSpellWorkConfig());

            var found = db.SelectRows(
                $"SELECT r.`spell_id` AS rank_spell, r.`first_spell_id` AS first_spell FROM `spell_ranks` r " +
                $"JOIN `{table}` t ON t.`entry` = r.`first_spell_id` " +
                $"WHERE r.`spell_id` <> r.`first_spell_id` " +
                $"AND NOT EXISTS (SELECT 1 FROM `{table}` x WHERE x.`entry` = r.`spell_id`) " +
                "ORDER BY r.`spell_id` LIMIT 1", 1);
            Skip.If(found.Rows.Count == 0, $"No rank-2+ spell that relies on its first rank's {table} row.");

            var rankSpell = Convert.ToInt32(found.Rows[0]["rank_spell"]);
            var first = Convert.ToInt32(found.Rows[0]["first_spell"]);
            var rows = JsonDocument.Parse(await tools.GetSpellDbOverrides(rankSpell)).RootElement
                .GetProperty("result").GetProperty("tables").GetProperty(table).EnumerateArray().ToList();
            var row = Assert.Single(rows);
            Assert.Equal(first, row.GetProperty("entry").GetInt32());
            Assert.Equal("firstRank", row.GetProperty("matchedBy").GetString());

            // The first rank itself: its row is exact, reported once.
            var own = JsonDocument.Parse(await tools.GetSpellDbOverrides(first)).RootElement
                .GetProperty("result").GetProperty("tables").GetProperty(table).EnumerateArray().ToList();
            Assert.Equal("exact", Assert.Single(own).GetProperty("matchedBy").GetString());
        }

        [SkippableTheory]
        [InlineData(null)]
        [InlineData("world")]
        [InlineData("")]
        public async Task QueryWorldDb_DefaultDatabase_IsTheConfiguredWorldDatabase(string database)
        {
            var tools = ToolsWithMySql();
            var configured = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world";

            var json = database == null
                ? await tools.QueryWorldDb("SELECT 1 AS one")
                : await tools.QueryWorldDb("SELECT 1 AS one", database);
            var root = JsonDocument.Parse(json).RootElement;
            Assert.True(root.GetProperty("ok").GetBoolean(), json);
            Assert.Equal(configured, root.GetProperty("result").GetProperty("database").GetString());
            Assert.Equal(1, root.GetProperty("result").GetProperty("count").GetInt32());

            // The configured name itself still works; any other name does not.
            var named = JsonDocument.Parse(await tools.QueryWorldDb("SELECT 1 AS one", configured)).RootElement;
            Assert.True(named.GetProperty("ok").GetBoolean());
            var other = JsonDocument.Parse(await tools.QueryWorldDb("SELECT 1 AS one", "not_" + configured)).RootElement;
            Assert.Equal("unknown_database", other.GetProperty("error").GetProperty("code").GetString());
        }

        // No MySQL needed: with SPELLWORK_MYSQL_WORLD_DB=acore_world (the
        // documented setup) an omitted database passes the name check and
        // reaches the connection, which fails here (port 1) as query_failed
        // -- not unknown_database.
        [Fact]
        public async Task QueryWorldDb_OmittedDatabase_PassesTheNameCheckForANonWorldName()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = "127.0.0.1",
                ["SPELLWORK_MYSQL_PORT"] = "1",
                ["SPELLWORK_MYSQL_USER"] = "nobody",
                ["SPELLWORK_MYSQL_WORLD_DB"] = "acore_world",
            });
            var tools = new DatabaseTools(new SpellDataService(config, NullLoggerFactoryStub.Logger));

            var code = JsonDocument.Parse(await tools.QueryWorldDb("SELECT 1")).RootElement
                .GetProperty("error").GetProperty("code").GetString();
            Assert.Equal("query_failed", code);

            var other = JsonDocument.Parse(await tools.QueryWorldDb("SELECT 1", "characters")).RootElement
                .GetProperty("error").GetProperty("code").GetString();
            Assert.Equal("unknown_database", other);
        }

        [Fact]
        public void WorldDatabaseAlias_IsOmittedEmptyOrWorld()
        {
            Assert.True(DatabaseTools.IsWorldDatabaseAlias(null));
            Assert.True(DatabaseTools.IsWorldDatabaseAlias(""));
            Assert.True(DatabaseTools.IsWorldDatabaseAlias("World"));
            Assert.False(DatabaseTools.IsWorldDatabaseAlias("acore_world"));
        }

        [SkippableFact]
        public async Task AllRanksRow_IsReturnedForALaterRankSpell()
        {
            var tools = ToolsWithMySql();
            var db = new SpellWork.Database.WorldDatabase(McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_HOST"),
                ["SPELLWORK_MYSQL_PORT"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PORT") ?? "3306",
                ["SPELLWORK_MYSQL_USER"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_USER") ?? "",
                ["SPELLWORK_MYSQL_PASS"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PASS") ?? "",
                ["SPELLWORK_MYSQL_WORLD_DB"] = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world",
            }).ToSpellWorkConfig());

            var found = db.SelectRows(
                "SELECT p.`SpellId` AS proc_id, r.`spell_id` AS rank_spell FROM `spell_proc` p " +
                "JOIN `spell_ranks` r ON r.`first_spell_id` = -p.`SpellId` AND r.`spell_id` <> r.`first_spell_id` " +
                "ORDER BY r.`spell_id` LIMIT 1", 1);
            Skip.If(found.Rows.Count == 0, "No rank-2+ spell with an all-ranks spell_proc row in this database.");

            var rankSpell = Convert.ToInt32(found.Rows[0]["rank_spell"]);
            var procId = Convert.ToInt32(found.Rows[0]["proc_id"]);
            var tables = JsonDocument.Parse(await tools.GetSpellDbOverrides(rankSpell)).RootElement
                .GetProperty("result").GetProperty("tables");
            Assert.Contains(tables.GetProperty("spell_proc").EnumerateArray(),
                r => r.GetProperty("SpellId").GetInt32() == procId && r.GetProperty("matchedBy").GetString() == "allRanks");
        }

        /// <summary>
        /// MySqlException is sealed and every constructor is non-public, so a
        /// numbered driver error can only be built by reflection. No server is
        /// contacted: this is a plain object construction.
        /// </summary>
        private static MySqlException MySqlError(int number)
        {
            var constructor = typeof(MySqlException).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(int) }, null);

            Assert.True(constructor != null,
                "MySql.Data no longer has MySqlException(string, int); update this helper to match the driver.");

            return (MySqlException)constructor.Invoke(new object[] { "test error " + number, number });
        }
    }
}
