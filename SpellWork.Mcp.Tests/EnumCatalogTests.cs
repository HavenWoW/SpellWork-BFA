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
    }
}
