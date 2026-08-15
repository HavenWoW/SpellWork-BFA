using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SpellWork.Mcp
{
    /// <summary>
    /// Every public enum in SpellWork.Core, by name, case-insensitively.
    /// Built by reflection rather than hand-listed so an enum added to
    /// SpellEnums.cs is decodable the day it lands.
    /// </summary>
    public static class EnumCatalog
    {
        static EnumCatalog()
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            // Task 8 review, finding 1: Assembly.GetTypes() can throw
            // ReflectionTypeLoadException if any type in the assembly fails
            // to load (a missing dependency for one unrelated class
            // elsewhere in SpellWork.Core is enough). An exception thrown
            // out of a static constructor is cached by the CLR and rethrown
            // as TypeInitializationException on every subsequent touch of
            // this class -- decode_flags and list_enum would then be
            // permanently dead for the rest of the process, with an error
            // that names neither tool. ex.Types holds the ones that *did*
            // load (with null in place of each that did not); enums have no
            // external dependencies of their own, so they are exactly the
            // types most likely to still be present even when something
            // else in the assembly could not be resolved. A partially
            // populated catalogue -- degrading a missing enum to
            // unknown_enum, which already lists what is available -- is
            // strictly better than no tools at all.
            Type[] types;
            try
            {
                types = typeof(SpellWork.Spell.SpellInfo).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types)
            {
                if (!type.IsEnum || !type.IsPublic)
                    continue;

                // Last one wins would hide a name collision. There are none
                // today; if one appears, the first registration keeps the name
                // and the duplicate is reachable only by full type name.
                if (!map.ContainsKey(type.Name))
                    map[type.Name] = type;
            }

            ByName = map;
            Names = map.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        public static IReadOnlyDictionary<string, Type> ByName { get; }
        public static IReadOnlyList<string> Names { get; }

        public static bool TryResolve(string name, out Type type)
        {
            type = null;
            return !string.IsNullOrWhiteSpace(name) && ByName.TryGetValue(name, out type);
        }
    }
}
