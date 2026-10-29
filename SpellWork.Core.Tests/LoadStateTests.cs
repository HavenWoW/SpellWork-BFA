using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DBFileReaderLib;
using Xunit;

namespace SpellWork.Core.Tests
{
    [CollectionDefinition("StaticMutation", DisableParallelization = true)]
    public sealed class StaticMutationCollection { }

    public sealed class AttributeEnumTests
    {
        [Fact]
        public void EveryAttributeWordEnumHasThirtyTwoMembers()
        {
            var types = typeof(DBC.DBC).Assembly.GetTypes()
                .Where(t => t.IsEnum && (t.Name == "SpellAtribute" || t.Name.StartsWith("SpellAtributeEx", StringComparison.Ordinal)))
                .ToList();

            Assert.Equal(8, types.Count);
            foreach (var type in types)
                Assert.True(Enum.GetNames(type).Length == 32, $"{type.Name} has {Enum.GetNames(type).Length} members");
        }
    }

    // These tests rewrite DBC.DBC's process-wide statics, so they run alone
    // (DisableParallelization) and put the real data back in a finally.
    [Collection("StaticMutation")]
    public sealed class LoadStateTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;
        public LoadStateTests(ClientDataFixture fixture) => _fixture = fixture;

        private static SpellWorkConfig RealConfig() => new SpellWorkConfig
        {
            DbcPath = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH"),
            Db2Path = Environment.GetEnvironmentVariable("SPELLWORK_DB2_PATH"),
            Locale = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS",
        };

        private static PropertyInfo Store(string name) =>
            typeof(DBC.DBC).GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        [SkippableTheory]
        [InlineData("SpellRuneCost")]
        [InlineData("SpellDuration")]
        [InlineData("SpellRange")]
        [InlineData("SpellCastTimes")]
        [InlineData("SpellIcon")]
        [InlineData("SpellCategory")]
        [InlineData("SpellDescriptionVariables")]
        [InlineData("SpellDifficulty")]
        public async Task BuildSpellInfoStore_ToleratesAMissingSecondaryStore(string storeName)
        {
            _fixture.SkipIfNoClientData();

            var property = Store(storeName);
            var saved = property.GetValue(null);
            var build = typeof(DBC.DBC).GetMethod("BuildSpellInfoStore", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                property.SetValue(null, null);
                await (Task)build.Invoke(null, new object[] { NullSpellWorkLogger.Instance });

                var fireball = DBC.DBC.SpellInfoStore[133];
                Assert.NotEmpty(fireball.Effects);
                if (storeName == "SpellDuration")
                    Assert.Null(fireball.DurationEntry);
                if (storeName == "SpellRange")
                    Assert.Null(fireball.Range);
            }
            finally
            {
                property.SetValue(null, saved);
                await (Task)build.Invoke(null, new object[] { NullSpellWorkLogger.Instance });
            }
        }

        // Review Focus 3: a locale whose slot is empty loads, yields empty
        // strings, and says which slot it used.
        [SkippableFact]
        public async Task EmptyLocaleSlotLoadsWithEmptyNames()
        {
            _fixture.SkipIfNoClientData();

            var config = RealConfig();
            config.Locale = "deDE";
            try
            {
                await DBC.DBC.LoadAsync(config, NullSpellWorkLogger.Instance);

                Assert.Equal(3, DBC.DBC.LocaleSlot);
                Assert.False(DBC.DBC.LocaleSlotFallback);
                Assert.Equal(DBC.DBC.Spell.Count, DBC.DBC.SpellInfoStore.Count);
                Assert.Equal(string.Empty, DBC.DBC.SpellInfoStore[133].Name);
                Assert.Equal(string.Empty, DBC.DBC.SpellInfoStore[133].Description);
            }
            finally
            {
                await DBC.DBC.LoadAsync(RealConfig(), NullSpellWorkLogger.Instance);
            }
        }

        // Review Focus 5: a file whose layout disagrees with its structure is
        // reported, never a failed load, and the other stores still load.
        [Fact]
        public async Task MismatchedLayoutIsReportedAndTheLoadCompletes()
        {
            var dir = Path.Combine(Path.GetTempPath(), "spellwork-core-mismatch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // SpellChainEffects with 47 fields / 176-byte records: one row.
                WriteWdbc(Path.Combine(dir, "SpellChainEffects.dbc"), 47, 176, 1);
                // A well-formed SpellRuneCost: 5 fields / 20 bytes, two rows.
                WriteWdbc(Path.Combine(dir, "SpellRuneCost.dbc"), 5, 20, 2);

                await DBC.DBC.LoadAsync(new SpellWorkConfig { DbcPath = dir, Locale = "enUS" }, NullSpellWorkLogger.Instance);

                var mismatch = Assert.Single(DBC.DBC.LayoutMismatches);
                Assert.Equal("SpellChainEffects", mismatch.Table);
                Assert.Equal(177, mismatch.ExpectedRecordSize);
                Assert.Equal(176, mismatch.ActualRecordSize);
                Assert.NotNull(DBC.DBC.SpellRuneCost);
                Assert.Equal(2, DBC.DBC.SpellRuneCost.Count);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
                if (_fixture.Loaded)
                    await DBC.DBC.LoadAsync(RealConfig(), NullSpellWorkLogger.Instance);
                else
                    await DBC.DBC.LoadAsync(new SpellWorkConfig(), NullSpellWorkLogger.Instance);
            }
        }

        // Rows hold ID = row index + 1 in field 0, zeros elsewhere; empty string block.
        private static void WriteWdbc(string path, int fields, int recordSize, int records)
        {
            using var w = new BinaryWriter(File.Create(path));
            w.Write(new[] { (byte)'W', (byte)'D', (byte)'B', (byte)'C' });
            w.Write(records);
            w.Write(fields);
            w.Write(recordSize);
            w.Write(1);
            for (var r = 0; r < records; r++)
            {
                var row = new byte[recordSize];
                BitConverter.GetBytes(r + 1).CopyTo(row, 0);
                w.Write(row);
            }
            w.Write((byte)0);
        }

        [SkippableFact]
        public async Task Reload_NullsStoresItDoesNotLoad_AndOpenedFilesAgree()
        {
            _fixture.SkipIfNoClientData();

            var empty = Path.Combine(Path.GetTempPath(), "spellwork-core-empty-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            try
            {
                await DBC.DBC.LoadAsync(new SpellWorkConfig { DbcPath = empty, Locale = "enUS" }, NullSpellWorkLogger.Instance);

                Assert.Empty(DBC.DBC.OpenedFiles);
                Assert.Empty(DBC.DBC.LayoutMismatches);
                Assert.Equal(DBC.DBC.StoreFileNames().Count(), DBC.DBC.UnloadedStores.Count);
                Assert.Null(DBC.DBC.Spell);
            }
            finally
            {
                try { Directory.Delete(empty, true); } catch (IOException) { }
                await DBC.DBC.LoadAsync(RealConfig(), NullSpellWorkLogger.Instance);
            }

            Assert.Empty(DBC.DBC.UnloadedStores);
            Assert.Equal(DBC.DBC.StoreFileNames().Count(), DBC.DBC.OpenedFiles.Count);
        }
    }
}

namespace SpellWork.Core.Tests
{
    public sealed class VisualChainJsonGateTests
    {
        [Fact]
        public void GatedSection_NamesTheMissingStores()
        {
            var section = new Spell.SpellVisualSection { MissingStores = new[] { "SpellVisual", "SpellVisualKit" }, Chain = null };

            var json = Spell.SpellVisualChainJson.Describe(section);

            Assert.True((bool)json["storeUnloaded"]);
            Assert.Equal(new[] { "SpellVisual", "SpellVisualKit" }, (System.Collections.Generic.IEnumerable<string>)json["missingStores"]);
        }
    }
}
