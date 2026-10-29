using System.Linq;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // Task 8 review, finding 1: EnumCatalog's static constructor now catches
    // ReflectionTypeLoadException from Assembly.GetTypes() and falls back to
    // the types that did load, so a partial load degrades the catalogue
    // rather than permanently killing decode_flags/list_enum for the rest of
    // the process (a static constructor's exception is cached and rethrown
    // as TypeInitializationException on every later touch).
    //
    // Constructing an actual ReflectionTypeLoadException here is not cheap:
    // it needs a type in SpellWork.Core.dll that references a dependency
    // missing at load time, and faking that (e.g. loading a deliberately
    // broken copy of the assembly into this process) would test a scenario
    // that does not reflect how SpellWork.Core actually fails today, not the
    // guard clause itself -- so, per the review's own instruction not to
    // fake that scenario, this instead pins the invariant the guard exists
    // to protect: after the (real, unfaked) static constructor has run, the
    // catalogue is non-empty and holds every enum the spec names by name.
    public class EnumCatalogTests
    {
        [Fact]
        public void ColdStaticInit_ProducesANonEmptyCatalogue()
        {
            Assert.True(EnumCatalog.Names.Count > 0);
        }

        [Fact]
        public void ColdStaticInit_ContainsEverySpecNamedEnum()
        {
            foreach (var name in new[] { "SpellSchoolMask", "SpellEffects", "AuraType", "Targets", "SpellFamilyNames" })
                Assert.True(EnumCatalog.TryResolve(name, out _), $"{name} missing after cold static init.");
        }

        // CoA 3.3.5a (AzerothCore CoA fork headers) ids.
        [Theory]
        [InlineData("AuraType", 3, "SPELL_AURA_PERIODIC_DAMAGE")]
        [InlineData("AuraType", 42, "SPELL_AURA_PROC_TRIGGER_SPELL")]
        [InlineData("AuraType", 317, "SPELL_AURA_ASCENSION_MOD_ABSORB_AMOUNT_PCT")]
        [InlineData("AuraType", 366, "SPELL_AURA_ASCENSION_LAST")]
        [InlineData("SpellEffects", 2, "SPELL_EFFECT_SCHOOL_DAMAGE")]
        [InlineData("SpellEffects", 64, "SPELL_EFFECT_TRIGGER_SPELL")]
        [InlineData("Classes", 12, "CLASS_BARBARIAN")]
        [InlineData("Classes", 13, "CLASS_WITCH_DOCTOR")]
        [InlineData("Classes", 32, "CLASS_SPIRIT_MAGE")]
        public void CoaEnumValues(string enumName, int id, string name)
        {
            var t = EnumType(enumName);
            Assert.Equal(name, System.Enum.GetName(t, System.Enum.ToObject(t, id)));
        }

        private static System.Type EnumType(string name) =>
            typeof(SpellWork.Spell.SpellInfo).Assembly.GetTypes().Single(x => x.IsEnum && x.Name == name);

        // The CoA headers leave some ids undefined (gaps), so the member count
        // is below TOTAL_AURAS / TOTAL_SPELL_EFFECTS; what is pinned is that the
        // enum reaches the last id (TOTAL - 1) and defines nothing beyond it.
        [Theory]
        [InlineData("AuraType", 366, "TOTAL_AURAS")]
        [InlineData("SpellEffects", 198, "TOTAL_SPELL_EFFECTS")]
        public void IdRangeEndsAtTotalMinusOne(string enumName, int lastId, string sentinel)
        {
            var t = EnumType(enumName);
            var values = System.Enum.GetValues(t).Cast<object>().Select(System.Convert.ToInt64).ToList();
            Assert.Equal(lastId, values.Max());
            Assert.DoesNotContain(sentinel, System.Enum.GetNames(t));
        }

        [Fact]
        public void AttributeWordsCoverZeroToSeven()
        {
            var names = typeof(SpellWork.Spell.SpellInfo).Assembly.GetTypes().Where(x => x.IsEnum).Select(x => x.Name).ToList();
            Assert.Contains("SpellAtribute", names);
            for (var i = 1; i <= 7; i++) Assert.Contains("SpellAtributeEx" + (i == 1 ? "" : i.ToString()), names);
            for (var i = 8; i <= 13; i++) Assert.DoesNotContain("SpellAtributeEx" + i, names);
        }

        [Fact]
        public void EverySpellAttrFlagValueExists()
        {
            var types = new[] { "SpellAtribute", "SpellAtributeEx", "SpellAtributeEx2", "SpellAtributeEx3",
                                "SpellAtributeEx4", "SpellAtributeEx5", "SpellAtributeEx6", "SpellAtributeEx7" };
            foreach (var typeName in types)
            {
                var t = EnumType(typeName);
                var bits = System.Enum.GetValues(t).Cast<object>().Select(System.Convert.ToUInt32).ToHashSet();
                for (var bit = 0; bit < 32; bit++)
                    Assert.True(bits.Contains(1u << bit), $"{typeName} lacks bit {bit}");
            }
        }
    }
}
