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

        // 5.4.8 (SkyFire_548) ids, not 8.3.7 ones.
        [Theory]
        [InlineData("AuraType", 3, "SPELL_AURA_PERIODIC_DAMAGE")]
        [InlineData("AuraType", 42, "SPELL_AURA_PROC_TRIGGER_SPELL")]
        [InlineData("SpellEffects", 2, "SPELL_EFFECT_SCHOOL_DAMAGE")]
        [InlineData("SpellEffects", 64, "SPELL_EFFECT_TRIGGER_SPELL")]
        [InlineData("Targets", 25, "TARGET_UNIT_TARGET_ANY")]
        public void FiveFourEightEnumValues(string enumName, int id, string name)
        {
            var t = typeof(SpellWork.Spell.SpellInfo).Assembly.GetTypes().Single(x => x.IsEnum && x.Name == enumName);
            Assert.Equal(name, System.Enum.GetName(t, System.Enum.ToObject(t, id)));
        }

        [Fact]
        public void AttributeWordsCoverZeroToThirteen()
        {
            var names = typeof(SpellWork.Spell.SpellInfo).Assembly.GetTypes().Where(x => x.IsEnum).Select(x => x.Name).ToList();
            Assert.Contains("SpellAtribute", names);
            for (var i = 1; i <= 13; i++) Assert.Contains("SpellAtributeEx" + (i == 1 ? "" : i.ToString()), names);
        }
    }
}
