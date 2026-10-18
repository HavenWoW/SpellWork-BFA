using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class McpServerConfigTests
    {
        [Fact]
        public void FromEnvironment_DefaultsLocaleToEnUs()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\data\dbc",
                ["SPELLWORK_GT_PATH"] = @"C:\data\gt",
            });

            Assert.Equal("enUS", config.Locale);
        }

        [Fact]
        public void FromEnvironment_BuildDefaultsTo554()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>());

            Assert.Equal("5.5.4.70449", config.Build);
            Assert.Equal("5.5.4.70449", config.ToSpellWorkConfig().Build);
        }

        [Fact]
        public void FromEnvironment_HonoursExplicitBuild()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_BUILD"] = "5.5.4.99999",
            });

            Assert.Equal("5.5.4.99999", config.Build);
            Assert.Equal("5.5.4.99999", config.ToSpellWorkConfig().Build);
        }

        [Fact]
        public void FromEnvironment_DefsPathDefaultsToBundledDefsDirectory()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>());

            var expected = Path.Combine(AppContext.BaseDirectory, "defs");
            Assert.Equal(expected, config.DefsPath);
            Assert.Equal(expected, config.ToSpellWorkConfig().DefsPath);
        }

        [Fact]
        public void FromEnvironment_HonoursExplicitDefsPath()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_DEFS_PATH"] = @"C:\data\defs",
            });

            Assert.Equal(@"C:\data\defs", config.DefsPath);
            Assert.Equal(@"C:\data\defs", config.ToSpellWorkConfig().DefsPath);
        }

        [Fact]
        public void FromEnvironment_HonoursExplicitLocale()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\data\dbc",
                ["SPELLWORK_GT_PATH"] = @"C:\data\gt",
                ["SPELLWORK_LOCALE"] = "deDE",
            });

            Assert.Equal("deDE", config.Locale);
        }

        // A partial MySQL config is a configuration mistake, not a working
        // connection. Requiring all three of host/user/database means the
        // MySQL tools report "not configured" instead of failing to connect
        // with a confusing driver error.
        [Fact]
        public void MySqlConfigured_RequiresHostUserAndDatabase()
        {
            var partial = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = "localhost",
            });
            Assert.False(partial.MySqlConfigured);

            var complete = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = "localhost",
                ["SPELLWORK_MYSQL_USER"] = "reader",
                ["SPELLWORK_MYSQL_WORLD_DB"] = "world",
            });
            Assert.True(complete.MySqlConfigured);
        }

        // Missing required paths must not throw -- a misconfigured server
        // still starts and still answers (spec section 9). The error surfaces
        // per-tool, not at construction.
        [Fact]
        public void FromEnvironment_WithNothingSet_DoesNotThrow()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>());

            Assert.Null(config.DbcPath);
            Assert.False(config.MySqlConfigured);
        }

        [Fact]
        public void ToSpellWorkConfig_MapsEveryValue()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\data\dbc",
                ["SPELLWORK_GT_PATH"] = @"C:\data\gt",
                ["SPELLWORK_HOTFIX_CACHE"] = @"C:\data\DBCache.bin",
                ["SPELLWORK_MYSQL_HOST"] = "db.local",
                ["SPELLWORK_MYSQL_PORT"] = "3307",
                ["SPELLWORK_MYSQL_USER"] = "reader",
                ["SPELLWORK_MYSQL_PASS"] = "secret",
                ["SPELLWORK_MYSQL_WORLD_DB"] = "world",
            });

            var core = config.ToSpellWorkConfig();

            Assert.Equal(@"C:\data\dbc", core.DbcPath);
            Assert.Equal(@"C:\data\gt", core.GtPath);
            Assert.Equal("enUS", core.Locale);
            Assert.Equal(@"C:\data\DBCache.bin", core.HotfixCachePath);
            Assert.Equal("db.local", core.MySqlHost);
            Assert.Equal("3307", core.MySqlPortOrPipe);
            Assert.Equal("reader", core.MySqlUser);
            Assert.Equal("secret", core.MySqlPassword);
            Assert.Equal("world", core.WorldDatabase);
            Assert.True(core.UseDatabase);
        }
    }
}
