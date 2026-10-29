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

        // A minimal WDBC file: header with zero records.
        private string WriteWdbc(params string[] parts)
        {
            var p = Path.Combine(_root, Path.Combine(parts));
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            var bytes = new byte[21];
            System.Text.Encoding.ASCII.GetBytes("WDBC").CopyTo(bytes, 0);
            System.BitConverter.GetBytes(0u).CopyTo(bytes, 4);   // records
            System.BitConverter.GetBytes(234u).CopyTo(bytes, 8); // fields
            System.BitConverter.GetBytes(936u).CopyTo(bytes, 12); // record size
            System.BitConverter.GetBytes(1u).CopyTo(bytes, 16);  // string block (one NUL)
            File.WriteAllBytes(p, bytes);
            return p;
        }

        [Fact]
        public void SyntheticWdbcResolves()
        {
            var spell = WriteWdbc("dbc", "Spell.dbc");
            var r = ClientFileResolver.Resolve("Spell", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") });
            Assert.Equal(spell, r.Path);
            Assert.Equal("dbc", r.Extension);
        }

        [Fact]
        public void Db2BesideTheDbcIsIgnored()
        {
            var spell = WriteWdbc("dbc", "Spell.dbc");
            File.WriteAllBytes(Path.Combine(_root, "dbc", "Spell.db2"), new byte[] { 0x57, 0x44, 0x42, 0x32 });
            var r = ClientFileResolver.Resolve("Spell", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") });
            Assert.Equal(spell, r.Path);
            Assert.Equal("dbc", r.Extension);
        }

        [Fact]
        public void LoneDb2IsNotResolved()
        {
            Directory.CreateDirectory(Path.Combine(_root, "dbc"));
            File.WriteAllBytes(Path.Combine(_root, "dbc", "SpellVisual.db2"), new byte[] { 0x57, 0x44, 0x42, 0x32 });
            Assert.Null(ClientFileResolver.Resolve("SpellVisual", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") }));
        }

        // 3.3.5 keeps all locales in one file; a locale-named folder is not consulted.
        [Fact]
        public void LocaleIsNotAFolder()
        {
            var root = WriteWdbc("dbc", "Spell.dbc");
            WriteWdbc("dbc", "deDE", "Spell.dbc");
            var r = ClientFileResolver.Resolve("Spell", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Locale = "deDE" });
            Assert.Equal(root, r.Path);
        }

        [Fact]
        public void AbsentFileIsNull()
        {
            Directory.CreateDirectory(Path.Combine(_root, "dbc"));
            Assert.Null(ClientFileResolver.Resolve("Nope", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") }));
        }

        [Fact]
        public void UnsetDbcPathResolvesNothing() =>
            Assert.Null(ClientFileResolver.Resolve("Spell", new SpellWorkConfig()));
    }
}
