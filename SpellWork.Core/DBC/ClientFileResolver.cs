using System.Collections.Generic;
using System.IO;

namespace SpellWork.DBC
{
    public sealed class ResolvedClientFile
    {
        public string Path { get; set; }
        public string Extension { get; set; }
        public bool LocaleFallback { get; set; }
        /// <summary>Client build from the WDB2 header; null for WDBC or an unreadable header.</summary>
        public int? Build { get; set; }
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

        /// <summary>
        /// Reads the build of a WDB2 file (uint32 at byte offset 24). Returns null
        /// for any other magic (WDBC has no build) or a file too short to hold the header.
        /// </summary>
        public static int? ReadBuild(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var header = new byte[28];
                    var read = 0;
                    while (read < header.Length)
                    {
                        var n = stream.Read(header, read, header.Length - read);
                        if (n <= 0) return null;
                        read += n;
                    }
                    if (header[0] != 'W' || header[1] != 'D' || header[2] != 'B' || header[3] != '2')
                        return null;
                    return (int)System.BitConverter.ToUInt32(header, 24);
                }
            }
            catch (IOException) { return null; }
            catch (System.UnauthorizedAccessException) { return null; }
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
                        return new ResolvedClientFile { Path = candidate, Extension = ext, LocaleFallback = fallback, Build = ext == "db2" ? ReadBuild(candidate) : null };
                }
            return null;
        }
    }
}
