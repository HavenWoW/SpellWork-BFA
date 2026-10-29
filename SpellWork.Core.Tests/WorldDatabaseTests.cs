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

        [Fact]
        public void SelectItemsUsingSpell_with_no_spell_does_not_touch_the_database()
        {
            var database = new WorldDatabase(new SpellWorkConfig { UseDatabase = false });
            Assert.Empty(database.SelectItemsUsingSpell(0, 50));
        }

        // Live MySQL: skipped unless SPELLWORK_TEST_MYSQL_HOST is set.
        private static WorldDatabase LiveDatabase()
        {
            var host = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_HOST");
            Skip.If(string.IsNullOrWhiteSpace(host), "SPELLWORK_TEST_MYSQL_HOST not set.");

            return new WorldDatabase(new SpellWorkConfig
            {
                UseDatabase = true,
                MySqlHost = host,
                MySqlPortOrPipe = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PORT") ?? "3306",
                MySqlUser = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_USER") ?? "",
                MySqlPassword = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_PASS") ?? "",
                WorldDatabase = Environment.GetEnvironmentVariable("SPELLWORK_TEST_MYSQL_WORLD_DB") ?? "world",
            });
        }

        // Whatever item the table holds in a spellid_2 slot: looking up that
        // spell must return the item with slot 2 and its spelltrigger_2.
        [SkippableFact]
        public void SelectItemsUsingSpell_reads_every_item_template_spell_slot()
        {
            var database = LiveDatabase();

            var probe = database.SelectRows(
                "SELECT `entry`, `name`, `spellid_2`, `spelltrigger_2` FROM `item_template` WHERE `spellid_2` > 0 ORDER BY `entry` LIMIT 1", 1);
            Skip.If(probe.Rows.Count == 0, "item_template has no item with a spellid_2.");
            var row = probe.Rows[0];
            var spellId = Convert.ToInt32(row["spellid_2"]);

            var uses = database.SelectItemsUsingSpell(spellId, 500);

            var use = Assert.Single(uses, u => u.Entry == Convert.ToInt32(row["entry"]) && u.Slot == 2);
            Assert.Equal(row["name"] as string, use.Name);
            Assert.Equal(Convert.ToInt32(row["spelltrigger_2"]), use.Trigger);
            Assert.Empty(database.SelectItemsUsingSpell(2147483, 500));
        }

        // Item 17031 (Rune of Teleportation) is the reagent of 3561
        // Teleport: Stormwind; 2147483 is no item.
        [SkippableFact]
        public void SelectItemNames_reads_item_template_names_by_entry()
        {
            var database = LiveDatabase();

            var names = database.SelectItemNames(new[] { 17031, 2147483 });

            Assert.Equal("Rune of Teleportation", names[17031]);
            Assert.False(names.ContainsKey(2147483));
        }
    }
}
