using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SpellWork.Core.Tests
{
    /// <summary>
    /// Control 3 of the design's section 7.1. A wrong field order decodes ids
    /// into whatever integer happens to sit at that offset, so reference
    /// resolution rates collapse. These assert a high resolution rate rather
    /// than merely that the load did not throw -- "it loaded" is exactly what
    /// a wrong layout also does.
    /// </summary>
    public class VisualChainIntegrityTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public VisualChainIntegrityTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void SpellVisual_LoadsTheWholeTable()
        {
            _fixture.SkipIfNoClientData();
            // Storage<T>.Count, never DBReader.RecordsCount -- the latter
            // reports only the inline section (169 of SpellVisualKit's 112694).
            Assert.True(DBC.DBC.SpellVisual.Count > 10000,
                $"SpellVisual loaded {DBC.DBC.SpellVisual.Count} rows; expected the full table.");
        }

        [SkippableFact]
        public void EverySpellVisualIdReferencedByXSpellVisual_Resolves()
        {
            _fixture.SkipIfNoClientData();

            var referenced = DBC.DBC.SpellXSpellVisual.Values
                .Select(x => (int)x.SpellVisualID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            var unresolved = referenced.Count(id => !DBC.DBC.SpellVisual.ContainsKey(id));
            var rate = 1.0 - (double)unresolved / referenced.Count;

            Assert.True(rate > 0.99,
                $"Only {rate:P2} of {referenced.Count} SpellVisualIDs resolve " +
                $"({unresolved} unresolved). A collapsed rate means the layout is wrong, " +
                "not that the data is.");
        }

        [SkippableFact]
        public void SpellVisualEvent_ResolvesToItsParentVisual()
        {
            _fixture.SkipIfNoClientData();

            var parents = DBC.DBC.SpellVisualEvent.Values
                .Select(e => e.SpellVisualID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            Skip.If(parents.Count == 0, "SpellVisualEvent carries no parent visual ids.");

            var unresolved = parents.Count(id => !DBC.DBC.SpellVisual.ContainsKey(id));
            var rate = 1.0 - (double)unresolved / parents.Count;

            Assert.True(rate > 0.99,
                $"Only {rate:P2} of {parents.Count} SpellVisualEvent parents resolve.");
        }

        [SkippableFact]
        public void VisualTables_HaveNoLayoutMismatches()
        {
            _fixture.SkipIfNoClientData();
            var visual = DBC.DBC.LayoutMismatches
                .Where(m => m.Table.StartsWith("SpellVisual"))
                .Select(m => m.Describe());
            Assert.Empty(visual);
        }

        [SkippableFact]
        public void SpellVisualKit_LoadsTheWholeTable()
        {
            _fixture.SkipIfNoClientData();
            // Measured: 112694 rows. DBReader.RecordsCount reports 169 for
            // this table because it counts only the inline section -- if this
            // assertion ever gets "fixed" by switching to RecordsCount, the
            // resolver silently loses 99.8% of its kits.
            Assert.True(DBC.DBC.SpellVisualKit.Count > 100000,
                $"SpellVisualKit loaded {DBC.DBC.SpellVisualKit.Count} rows; expected ~112694.");
        }

        [SkippableFact]
        public void EveryKitEffectParent_ResolvesToAKit()
        {
            _fixture.SkipIfNoClientData();

            var parents = DBC.DBC.SpellVisualKitEffect.Values
                .Select(e => e.ParentSpellVisualKitID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            // Correction 4: (double)unresolved / parents.Count with an empty
            // parents list yields NaN, and NaN > 0.99 is false -- so the
            // assertion below would fail with an unreadable "Only NaN%"
            // message instead of saying what actually went wrong.
            // SpellVisualKitEffect is a join table; an empty parent set here
            // means the structure decoded to garbage, not that the data is
            // legitimately absent (compare KitFallbackReferences_ResolveWithinTheSameTable's
            // Skip.If below, where an empty set is genuinely optional data).
            Assert.True(parents.Count > 0,
                "SpellVisualKitEffect yielded no parent kit ids at all -- the structure " +
                "almost certainly decoded to garbage.");

            var unresolved = parents.Count(id => !DBC.DBC.SpellVisualKit.ContainsKey(id));
            var rate = 1.0 - (double)unresolved / parents.Count;

            Assert.True(rate > 0.99,
                $"Only {rate:P2} of {parents.Count} kit-effect parents resolve " +
                $"({unresolved} unresolved).");
        }

        // Measured, not assumed: of all 17 EffectType values observed in real
        // data, only EffectType 2 resolves cleanly into
        // SpellVisualKitModelAttach (n=133507, 100.00%). Every other observed
        // value was checked against SpellChainEffects, SpellVisualEffectName,
        // and SpellVisualKitModelAttach and none cleared even 95% at a
        // comparable sample size -- see task-4-report.md for the full table.
        // This is the one EffectType meaning this plan can assert with
        // confidence; it also matches WoWDBDefs' own COLUMNS comment on
        // SpellVisualKitEffect.Effect ("2: SpellVisualKitModelAttachID").
        private const int ModelAttachEffectType = 2;

        [SkippableFact]
        public void EffectType2_ResolvesToSpellVisualKitModelAttach()
        {
            _fixture.SkipIfNoClientData();

            var ids = DBC.DBC.SpellVisualKitEffect.Values
                .Where(e => e.EffectType == ModelAttachEffectType && e.Effect != 0)
                .Select(e => e.Effect)
                .ToList();

            Skip.If(ids.Count == 0, $"No SpellVisualKitEffect row has EffectType == {ModelAttachEffectType}.");

            var unresolved = ids.Count(id => !DBC.DBC.SpellVisualKitModelAttach.ContainsKey(id));
            var rate = 1.0 - (double)unresolved / ids.Count;

            Assert.True(rate > 0.99,
                $"Only {rate:P2} of {ids.Count} EffectType=={ModelAttachEffectType} Effect ids resolve " +
                $"into SpellVisualKitModelAttach ({unresolved} unresolved).");
        }

        [SkippableFact]
        public void KitFallbackReferences_ResolveWithinTheSameTable()
        {
            _fixture.SkipIfNoClientData();

            var fallbacks = DBC.DBC.SpellVisualKit.Values
                .Select(k => k.FallbackSpellVisualKitID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            Skip.If(fallbacks.Count == 0, "No kit declares a fallback kit.");

            var unresolved = fallbacks.Count(id => !DBC.DBC.SpellVisualKit.ContainsKey(id));
            Assert.True(1.0 - (double)unresolved / fallbacks.Count > 0.99,
                $"{unresolved} of {fallbacks.Count} fallback kit ids do not resolve.");
        }

        [SkippableFact]
        public void SpellChainEffects_MatchesItsRecordedLayout()
        {
            _fixture.SkipIfNoClientData();
            Assert.DoesNotContain(DBC.DBC.LayoutMismatches, m => m.Table == "SpellChainEffects");
            // Measured: 4838 rows. The brief's ~4722 was a planning-time
            // estimate, not a hard constant -- the >4000 floor is the actual
            // acceptance bar.
            Assert.True(DBC.DBC.SpellChainEffects.Count > 4000,
                $"SpellChainEffects loaded {DBC.DBC.SpellChainEffects.Count} rows; expected ~4722.");
        }

        [SkippableFact]
        public void SpellMissileMotion_MatchesItsRecordedLayout()
        {
            _fixture.SkipIfNoClientData();
            Assert.DoesNotContain(DBC.DBC.LayoutMismatches, m => m.Table == "SpellMissileMotion");
            Assert.True(DBC.DBC.SpellMissileMotion.Count > 1000,
                $"SpellMissileMotion loaded {DBC.DBC.SpellMissileMotion.Count} rows; expected ~1461.");
        }

        // SpellMissile has been loaded since before this plan and never read.
        // Assert it is real before the resolver depends on it.
        [SkippableFact]
        public void SpellMissile_IsLoadedAndMatchesItsLayout()
        {
            _fixture.SkipIfNoClientData();
            Assert.DoesNotContain(DBC.DBC.LayoutMismatches, m => m.Table == "SpellMissile");
            Assert.True(DBC.DBC.SpellMissile.Count > 300,
                $"SpellMissile loaded {DBC.DBC.SpellMissile.Count} rows; expected ~330.");
        }

        // Control 2 (decode plausibility) for SpellChainEffects specifically:
        // at 60 fields it is by far the widest table in this plan, and a
        // transposed *pair* of same-width fields still matches the layout
        // hash and the field count -- neither Control 1 above nor the
        // row-count checks would notice. These three fields were chosen
        // because garbage decodes them in a way real data does not.
        [SkippableFact]
        public void SpellChainEffects_SelfReferencesResolveWithinTheTable()
        {
            _fixture.SkipIfNoClientData();

            // SpellChainEffectID[11] is a self-referencing id array (up to 11
            // sibling rows). It is the strongest check available: a wrong
            // field order would decode it from whatever bytes happen to sit
            // at that offset, and the odds of ~4800 unrelated integers
            // landing on valid keys in a ~4800-row table by chance are
            // negligible. Measured: 4224 non-zero entries, all resolve.
            var selfRefs = DBC.DBC.SpellChainEffects.Values
                .SelectMany(e => e.SpellChainEffectID)
                .Where(id => id != 0)
                .Select(id => (int)id)
                .ToList();

            Skip.If(selfRefs.Count == 0, "No SpellChainEffects row declares a sibling reference.");

            var unresolved = selfRefs.Count(id => !DBC.DBC.SpellChainEffects.ContainsKey(id));
            var rate = 1.0 - (double)unresolved / selfRefs.Count;

            Assert.True(rate > 0.99,
                $"Only {rate:P2} of {selfRefs.Count} SpellChainEffectID self-references resolve " +
                $"({unresolved} unresolved).");
        }

        [SkippableFact]
        public void SpellChainEffects_ScaleFloatsAreFiniteAndSane()
        {
            _fixture.SkipIfNoClientData();

            // TexCoordScale sits between two other 32-bit fields (NoiseScale,
            // SegDuration) that are the same width but different C# types
            // (float, uint) -- if the pair either side of it were swapped, the
            // bit pattern reinterpreted as float is overwhelmingly likely to be
            // NaN, infinite, or a denormal near zero (a millisecond duration
            // like 500 reinterpreted as float is 7e-43) rather than the modest
            // signed scale factor real data holds.
            //
            // Measured directly against this build (4838 rows): TexCoordScale
            // spans -9..20, and all 4417 non-zero values fall inside
            // 0.001 <= |v| <= 64 -- every single one, not a majority. So the
            // band below is asserted for all of them rather than as a rate,
            // with roughly 3x headroom over the largest real magnitude and 40
            // orders of magnitude over the denormals a uint transposition
            // produces. The previous bound here was |v| < 1000, which is 50x
            // looser than the measured maximum and, more to the point, admits
            // every denormal a swap would create.
            var rows = DBC.DBC.SpellChainEffects.Values;
            Assert.NotEmpty(rows);

            var nonZero = rows.Count(e => e.TexCoordScale != 0f);
            Assert.True(nonZero > 0,
                "No SpellChainEffects row has a non-zero TexCoordScale; this check has become vacuous.");

            Assert.All(rows, e =>
            {
                Assert.False(float.IsNaN(e.TexCoordScale) || float.IsInfinity(e.TexCoordScale),
                    $"SpellChainEffects {e.ID}: TexCoordScale is not finite ({e.TexCoordScale}).");

                if (e.TexCoordScale == 0f)
                    return; // legitimately unset on 421 of 4838 rows

                var magnitude = Math.Abs(e.TexCoordScale);
                Assert.True(magnitude >= 0.001f && magnitude <= 64f,
                    $"SpellChainEffects {e.ID}: TexCoordScale {e.TexCoordScale} is outside the measured " +
                    "0.001..64 band for a UV scale factor -- a neighbouring field read as float looks like this.");
            });
        }

        [SkippableFact]
        public void SpellChainEffects_TextureFileDataIdsAreNeverNegative()
        {
            _fixture.SkipIfNoClientData();

            // TextureFileDataID[3] is declared int, not uint, per its LAYOUT
            // annotation -- but real FileData ids are always positive. A
            // field-order error that shifts this array onto neighbouring
            // bytes has roughly even odds of setting the sign bit; real data
            // never does.
            //
            // "id >= 0" alone is the canonical weak shape, though: it is
            // satisfied by 0, by 7, and by every all-zero row, so an array that
            // had shifted onto a run of small counters or flags would pass it
            // unchanged. Measured against this build (4838 rows, 14514 id
            // slots): 5576 are non-zero, 466 distinct, and every non-zero one
            // falls between 122946 and 3196160 -- the FileDataID range this
            // client's assets actually occupy. Both the non-emptiness and the
            // band are asserted below, so a shifted array has to land on
            // plausible seven-figure values to survive.
            var rows = DBC.DBC.SpellChainEffects.Values;
            Assert.NotEmpty(rows);

            var ids = rows.SelectMany(e => e.TextureFileDataID).ToList();
            var populated = ids.Count(id => id != 0);
            Assert.True(populated > 0,
                "No SpellChainEffects row references a texture at all; this check has become vacuous.");

            Assert.All(ids, id =>
            {
                Assert.True(id >= 0, $"SpellChainEffects TextureFileDataID {id} is negative.");
                Assert.True(id == 0 || (id >= 1000 && id <= 100000000),
                    $"SpellChainEffects TextureFileDataID {id} is not a plausible FileDataID " +
                    "(measured range in this build: 122946..3196160).");
            });
        }

        // Broadening item B per code review, using semantic Min/Max ordering
        // invariants elsewhere in the same 60-field struct.
        //
        // Naively asserting min <= max across all rows is too weak here:
        // SpellChainEffects carries plenty of rows where a given Min/Max
        // pair is simply unused, with both fields left at their default
        // (almost always 0). Those equal rows pass min <= max regardless of
        // which field is which -- a swap does not touch them -- so they
        // only dilute the rate a swap would actually produce.
        //
        // The fix: restrict to rows where the pair strictly differs
        // (min != max). Among those rows, a same-width transposition of the
        // two fields inverts the comparison on every single one (decoded
        // "min" becomes the true max and vice versa), so the correct-order
        // rate p among differing rows becomes exactly 1-p under a swap --
        // no dilution term, no approximation.
        //
        // Measured directly against real client data (4838 SpellChainEffects
        // rows), restricted to rows where the pair differs:
        //
        //   Pair                              differ n  correct   swap
        //   MinFlickerOnDuration/Max              1310   94.89%   5.11%
        //   MinDurationBetweenJointJumps/Max       2127   89.80%  10.20%
        //   MinFlickerOffDuration/Max              1407   84.36%  15.64%
        //   MinWaveAngle/Max                        607   73.97%  26.03%
        //
        // These four separate cleanly: the worst correct-order case
        // (73.97%) sits well above the best swap case among them (26.03%),
        // so the 0.70 threshold below has 4 points of margin under the
        // worst real rate and 44 points of margin over the worst swap rate.
        //
        // Three more Min/Max pairs exist in the struct, were measured the
        // same way, and are deliberately NOT asserted because real data
        // does not separate them cleanly enough to use as a swap detector:
        //   MinWaveSpin/Max  (differ=415,  correct=61.45%, swap=38.55%)
        //   MinArcAngle/Max  (differ=1189, correct=57.78%, swap=42.22%)
        //   MinArcSpin/Max   (differ=1009, correct=40.04%, swap=59.96%) --
        //     min > max is the *majority* outcome among this pair's
        //     differing rows in real data, so it has no usable ordering
        //     invariant at all, at any threshold or denominator.
        //
        // Two further candidates were rejected before measurement, because
        // they carry no ordering invariant to begin with:
        //   StartWidth/EndWidth: no in-game reason a chain effect's start
        //     width must be <= (or >=) its end width -- ribbons taper both
        //     directions.
        //   MinorJointScale/MajorJointScale: despite the naming, these are
        //     two different physical quantities (per-minor-joint vs
        //     per-major-joint scale), not a min/max bound on one quantity.
        //
        // This check, together with the three original Control-2/3 spot
        // checks above (TexCoordScale, TextureFileDataID, SpellChainEffectID
        // self-refs), gives item B eleven examined positions in the
        // 60-field struct -- still not general field-order coverage across
        // all 60 fields. That coverage comes from the reviewer's independent
        // WoWDBDefs re-derivation of every field; see task-4-report.md.
        [SkippableFact]
        public void SpellChainEffects_MinMaxPairsHoldAmongDifferingRows()
        {
            _fixture.SkipIfNoClientData();

            const double MinCorrectOrderRate = 0.70;

            void Check<T>(IEnumerable<T> rows, string name, Func<T, float> min, Func<T, float> max)
            {
                int differ = 0, correctOrder = 0;
                foreach (var row in rows)
                {
                    var mn = min(row);
                    var mx = max(row);
                    if (mn == mx) continue; // swap-invariant; carries no signal
                    differ++;
                    if (mn < mx) correctOrder++;
                }

                // Fail loudly rather than pass vacuously if the client data
                // ever stops exercising this pair.
                Assert.True(differ > 0, $"{name}: no rows differ; this check has become vacuous.");

                var rate = (double)correctOrder / differ;
                Assert.True(rate > MinCorrectOrderRate,
                    $"{name}: only {rate:P2} of {differ} differing rows hold min < max " +
                    $"(threshold {MinCorrectOrderRate:P0}). A field-order swap would show " +
                    $"roughly {1 - rate:P2} here, not {rate:P2}.");
            }

            var rows = DBC.DBC.SpellChainEffects.Values;
            Check(rows, "MinDurationBetweenJointJumps/Max", e => e.MinDurationBetweenJointJumps, e => e.MaxDurationBetweenJointJumps);
            Check(rows, "MinWaveAngle/Max", e => e.MinWaveAngle, e => e.MaxWaveAngle);
            Check(rows, "MinFlickerOnDuration/Max", e => e.MinFlickerOnDuration, e => e.MaxFlickerOnDuration);
            Check(rows, "MinFlickerOffDuration/Max", e => e.MinFlickerOffDuration, e => e.MaxFlickerOffDuration);
        }
    }
}
