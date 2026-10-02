using System;
using System.Collections.Generic;
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
        public void OverrideTables_AreTheRepackSchemaList()
        {
            Assert.Equal(new[]
            {
                ("spell_proc_event", "entry"),
                ("spell_proc", "spellId"),
                ("spell_script_names", "spell_id"),
                ("spell_custom_attribute", "spell"),
                ("spell_bonus_data", "entry"),
                ("spell_threat", "entry"),
                ("spell_ranks", "spell_id"),
                ("spell_linked_spell", "spell_trigger"),
                ("spell_dbc", "Id"),
                ("spelleffect_dbc", "EffectSpellId"),
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
        public async Task RepackCustomSpellOverridesIncludeMiscByMiscId()
        {
            // spell_dbc row: Id 3617 -> SpellMiscId 200000
            var json = JsonDocument.Parse(await ToolsWithMySql().GetSpellDbOverrides(3617)).RootElement.GetProperty("result");
            var misc = json.GetProperty("tables").GetProperty("spellmisc_dbc");
            Assert.Equal("spellMiscId", misc.GetProperty("matchedBy").GetString());
            Assert.Contains(misc.GetProperty("rows").EnumerateArray(), r => r.GetProperty("Id").GetInt32() == 200000);
        }

        [Fact]
        public void ComputeMiscIds_UnionsClientAndSpellDbc()
        {
            Assert.Equal(new[] { 7 }, DatabaseTools.ComputeMiscIds(7, new int[0]));
            Assert.Equal(new[] { 200000 }, DatabaseTools.ComputeMiscIds(null, new[] { 200000 }));
            Assert.Equal(new[] { 7, 200000 }, DatabaseTools.ComputeMiscIds(200000, new[] { 7, 200000 }));
            Assert.Empty(DatabaseTools.ComputeMiscIds(null, new int[0]));
            Assert.Empty(DatabaseTools.ComputeMiscIds(0, new[] { 0 }));
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
            var rows = db.SelectProc("SELECT entry, SchoolMask, SpellFamilyName, SpellFamilyMask0, SpellFamilyMask1, SpellFamilyMask2, SpellFamilyMask3, procFlags, procEx, phase, ppmRate, CustomChance, Cooldown FROM spell_proc_event LIMIT 20");
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
        public async Task EveryListedTableIsQueried()
        {
            var tables = JsonDocument.Parse(await ToolsWithMySql().GetSpellDbOverrides(133)).RootElement.GetProperty("result").GetProperty("tables");
            foreach (var t in new[] { "spell_proc_event", "spell_proc", "spell_script_names", "spell_custom_attribute", "spell_bonus_data", "spell_threat", "spell_ranks", "spell_linked_spell", "spell_dbc", "spelleffect_dbc", "spellmisc_dbc" })
                Assert.True(tables.TryGetProperty(t, out _), t);
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
