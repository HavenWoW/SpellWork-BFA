using System.Runtime.CompilerServices;

// SpellWork.Core.Tests reaches internal members (CalculateBaseEffectValue,
// CalculateEffectValues) directly rather than only through the public Write
// / SpellInfoJsonSerializer.Serialize surface, so it can assert on the
// arithmetic in isolation from text/JSON rendering.
[assembly: InternalsVisibleTo("SpellWork.Core.Tests")]
