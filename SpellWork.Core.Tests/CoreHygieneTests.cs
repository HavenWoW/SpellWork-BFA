using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class CoreHygieneTests
    {
        private static string CoreSourceRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SpellWork.sln")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "SpellWork.Core");
        }

        [Fact]
        public void Core_writes_nothing_to_stdout()
        {
            var offenders = Directory
                .EnumerateFiles(CoreSourceRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains("Console.Write"))
                .Select(f => Path.GetFileName(f))
                .ToList();

            Assert.True(offenders.Count == 0,
                "stdout is the JSON-RPC channel for the MCP host; these files would " +
                "corrupt it: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Core_references_no_ui_assemblies()
        {
            var offenders = Directory
                .EnumerateFiles(CoreSourceRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var text = File.ReadAllText(f);
                    return text.Contains("using System.Windows.Forms")
                        || text.Contains("using System.Drawing");
                })
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "SpellWork.Core must stay headless; these files reference UI " +
                "assemblies: " + string.Join(", ", offenders));
        }
    }
}
