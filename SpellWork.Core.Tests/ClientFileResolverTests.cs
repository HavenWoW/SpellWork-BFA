using System.IO;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class ClientFileResolverTests : System.IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "swres-" + System.Guid.NewGuid().ToString("N"));
        public ClientFileResolverTests() { Directory.CreateDirectory(_root); }
        public void Dispose() { Directory.Delete(_root, true); }

        private string Touch(params string[] parts)
        {
            var p = Path.Combine(_root, Path.Combine(parts));
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, new byte[] { 0x57, 0x44, 0x42, 0x43 });
            return p;
        }

        [Fact]
        public void ReadBuild_Wdb2Header_YieldsBuildAtOffset24_AndWdbcYieldsNull()
        {
            var wdb2 = new byte[48];
            System.Text.Encoding.ASCII.GetBytes("WDB2").CopyTo(wdb2, 0);
            System.BitConverter.GetBytes(18273u).CopyTo(wdb2, 24);
            var wdb2Path = Path.Combine(_root, "h.db2");
            File.WriteAllBytes(wdb2Path, wdb2);
            Assert.Equal(18273, ClientFileResolver.ReadBuild(wdb2Path));

            var wdbc = new byte[48];
            System.Text.Encoding.ASCII.GetBytes("WDBC").CopyTo(wdbc, 0);
            System.BitConverter.GetBytes(18273u).CopyTo(wdbc, 24);
            var wdbcPath = Path.Combine(_root, "h.dbc");
            File.WriteAllBytes(wdbcPath, wdbc);
            Assert.Null(ClientFileResolver.ReadBuild(wdbcPath));

            var shortPath = Path.Combine(_root, "short.db2");
            File.WriteAllBytes(shortPath, new byte[] { 0x57, 0x44, 0x42, 0x32 });
            Assert.Null(ClientFileResolver.ReadBuild(shortPath));

            var resolved = ClientFileResolver.Resolve("h", new SpellWorkConfig { DbcPath = _root });
            Assert.Equal("db2", resolved.Extension);
            Assert.Equal(18273, resolved.Build);
        }

        [Fact]
        public void Db2WinsOverDbcInSameDirectory()
        {
            Touch("dbc", "SpellVisual.dbc");
            var db2 = Touch("dbc", "SpellVisual.db2");
            var r = ClientFileResolver.Resolve("SpellVisual", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Locale = "enUS" });
            Assert.Equal(db2, r.Path);
            Assert.Equal("db2", r.Extension);
        }

        [Fact]
        public void Db2InDb2PathWinsOverDbcInDbcPath()
        {
            Touch("dbc", "SpellVisual.dbc");
            var db2 = Touch("db2", "SpellVisual.db2");
            var r = ClientFileResolver.Resolve("SpellVisual", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Db2Path = Path.Combine(_root, "db2") });
            Assert.Equal(db2, r.Path);
        }

        [Fact]
        public void MissingLocaleFolderFallsBackToRootAndSaysSo()
        {
            var dbc = Touch("dbc", "Spell.dbc");
            var r = ClientFileResolver.Resolve("Spell", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Locale = "enUS" });
            Assert.Equal(dbc, r.Path);
            Assert.True(r.LocaleFallback);
        }

        [Fact]
        public void ExistingLocaleFolderIsPreferred()
        {
            Touch("dbc", "Spell.dbc");
            var ru = Touch("dbc", "ruRU", "Spell.dbc");
            var r = ClientFileResolver.Resolve("Spell", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Locale = "ruRU" });
            Assert.Equal(ru, r.Path);
            Assert.False(r.LocaleFallback);
        }

        [Fact]
        public void HyphenatedNameResolves()
        {
            var p = Touch("dbc", "Item-sparse.db2");
            Assert.Equal(p, ClientFileResolver.Resolve("Item-sparse", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") }).Path);
        }

        [Fact]
        public void AbsentFileIsNull()
        {
            Directory.CreateDirectory(Path.Combine(_root, "dbc"));
            Assert.Null(ClientFileResolver.Resolve("Nope", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") }));
        }
    }
}
