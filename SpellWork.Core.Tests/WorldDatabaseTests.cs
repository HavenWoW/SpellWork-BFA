using System;
using SpellWork.Database;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class WorldDatabaseTests
    {
        [Fact]
        public void TestConnection_reports_not_configured_without_an_error()
        {
            var database = new WorldDatabase(new SpellWorkConfig { UseDatabase = false });

            Assert.False(database.TestConnection(out var error));
            Assert.Null(error);
            Assert.False(database.Connected);
        }

        [Fact]
        public void SelectItemNames_with_no_ids_does_not_touch_the_database()
        {
            // No host configured: any query would throw.
            var database = new WorldDatabase(new SpellWorkConfig { UseDatabase = false });

            Assert.Empty(database.SelectItemNames(new[] { 0, -1 }));
            Assert.Empty(database.SelectItemNames(null));
        }

        // Live MySQL: skipped unless SPELLWORK_TEST_MYSQL_HOST is set.
        // Item 17031 (Rune of Teleportation) is the reagent of 3561
        // Teleport: Stormwind; 2147483 is no item.
        [SkippableFact]
        public void SelectItemNames_reads_item_template_names_by_entry()
        {
            var host = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_HOST");
            Skip.If(string.IsNullOrWhiteSpace(host), "SPELLWORK_TEST_MYSQL_HOST not set.");

            var database = new WorldDatabase(new SpellWorkConfig
            {
                UseDatabase = true,
                MySqlHost = host,
                MySqlPortOrPipe = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PORT") ?? "3306",
                MySqlUser = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_USER") ?? "",
                MySqlPassword = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PASS") ?? "",
                WorldDatabase = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world",
            });

            var names = database.SelectItemNames(new[] { 17031, 2147483 });

            Assert.Equal("Rune of Teleportation", names[17031]);
            Assert.False(names.ContainsKey(2147483));
        }
    }
}
