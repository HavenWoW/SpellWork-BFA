using System.IO;

namespace SpellWork.DBC
{
    /// <summary>
    /// Picks the directory DBCD reads .db2 files from: {DbcPath}\{Locale} when
    /// that exists, otherwise DbcPath itself (LocaleFallback = true).
    /// </summary>
    public sealed class ClientFileResolver
    {
        public string EffectiveDbcDirectory { get; }
        public bool LocaleFallback { get; }

        public ClientFileResolver(string dbcPath, string locale)
        {
            var localeDir = string.IsNullOrEmpty(locale) ? null : Path.Combine(dbcPath ?? string.Empty, locale);
            if (localeDir != null && Directory.Exists(localeDir))
            {
                EffectiveDbcDirectory = localeDir;
            }
            else
            {
                EffectiveDbcDirectory = dbcPath;
                LocaleFallback = true;
            }
        }
    }
}
