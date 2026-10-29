using System;
using System.Collections.Generic;

namespace SpellWork.DBC
{
    /// <summary>
    /// 3.3.5 keeps every locale's strings in one WDBC row: each localized
    /// column is 16 string slots plus a flags word. The locale picks the
    /// slot (spec D6); there are no locale folders.
    /// </summary>
    public static class LocaleSlots
    {
        public const int SlotCount = 16;

        private static readonly IReadOnlyDictionary<string, int> Slots =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["enUS"] = 0, ["enGB"] = 0,
                ["koKR"] = 1,
                ["frFR"] = 2,
                ["deDE"] = 3,
                ["zhCN"] = 4,
                ["zhTW"] = 5,
                ["esES"] = 6,
                ["esMX"] = 7,
                ["ruRU"] = 8,
            };

        /// <summary>
        /// The string slot for <paramref name="locale"/>. An unknown or empty
        /// locale yields slot 0 (enUS) with <paramref name="fallback"/> set.
        /// </summary>
        public static int Resolve(string locale, out bool fallback)
        {
            if (!string.IsNullOrEmpty(locale) && Slots.TryGetValue(locale, out var slot))
            {
                fallback = false;
                return slot;
            }

            fallback = true;
            return 0;
        }

        /// <summary>The configured slot's string of a localized column; never null.</summary>
        public static string Pick(string[] localized)
        {
            var slot = DBC.LocaleSlot;
            if (localized == null || slot < 0 || slot >= localized.Length)
                return string.Empty;
            return localized[slot] ?? string.Empty;
        }
    }
}
