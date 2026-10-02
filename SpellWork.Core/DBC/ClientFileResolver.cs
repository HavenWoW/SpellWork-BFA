using System.Collections.Generic;
using System.IO;

namespace SpellWork.DBC
{
    public sealed class ResolvedClientFile
    {
        public string Path { get; set; }
        public string Extension { get; set; }
        public bool LocaleFallback { get; set; }
    }

    /// <summary>
    /// Picks the one file a store loads from. A 5.4.8 extraction can hold a
    /// stale pre-5.4 <c>.dbc</c> next to the current <c>.db2</c> of the same
    /// table (SpellVisual, SpellVisualEffectName in the reference data), so
    /// <c>.db2</c> is searched everywhere before <c>.dbc</c> is considered.
    /// </summary>
    public static class ClientFileResolver
    {
        public static string EffectiveDbcDirectory(SpellWorkConfig config, out bool localeFallback)
        {
            localeFallback = false;
            if (string.IsNullOrEmpty(config.DbcPath))
                return null;
            if (!string.IsNullOrEmpty(config.Locale))
            {
                var localized = System.IO.Path.Combine(config.DbcPath, config.Locale);
                if (Directory.Exists(localized))
                    return localized;
            }
            localeFallback = true;
            return config.DbcPath;
        }

        public static ResolvedClientFile Resolve(string fileName, SpellWorkConfig config)
        {
            var dbcDir = EffectiveDbcDirectory(config, out var fallback);
            var dirs = new List<string>();
            if (dbcDir != null) dirs.Add(dbcDir);
            if (!string.IsNullOrEmpty(config.Db2Path)) dirs.Add(config.Db2Path);

            foreach (var ext in new[] { "db2", "dbc" })
                foreach (var dir in dirs)
                {
                    var candidate = System.IO.Path.Combine(dir, fileName + "." + ext);
                    if (File.Exists(candidate))
                        return new ResolvedClientFile { Path = candidate, Extension = ext, LocaleFallback = fallback };
                }
            return null;
        }
    }
}
