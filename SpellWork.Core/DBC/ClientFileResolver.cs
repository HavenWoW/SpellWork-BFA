using System.IO;

namespace SpellWork.DBC
{
    public sealed class ResolvedClientFile
    {
        public string Path { get; set; }
        /// <summary>Always <c>dbc</c>: the 3.3.5a client has no <c>.db2</c> stores.</summary>
        public string Extension { get; set; }
    }

    /// <summary>
    /// Picks the one file a store loads from: <c>&lt;DbcPath&gt;/&lt;name&gt;.dbc</c>.
    /// The 3.3.5a client keeps every locale's strings inside the one file
    /// (spec D6), so the locale is a string slot, not a folder, and a
    /// <c>.db2</c> of the same name is never considered.
    /// </summary>
    public static class ClientFileResolver
    {
        /// <summary>The directory stores are read from, or null when DbcPath is unset.</summary>
        public static string EffectiveDbcDirectory(SpellWorkConfig config) =>
            string.IsNullOrEmpty(config?.DbcPath) ? null : config.DbcPath;

        public static ResolvedClientFile Resolve(string fileName, SpellWorkConfig config)
        {
            var dbcDir = EffectiveDbcDirectory(config);
            if (dbcDir == null)
                return null;

            var candidate = System.IO.Path.Combine(dbcDir, fileName + ".dbc");
            return File.Exists(candidate) ? new ResolvedClientFile { Path = candidate, Extension = "dbc" } : null;
        }
    }
}
