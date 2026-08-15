# Spell Visual Chain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve a spell's visual chain from `SpellXSpellVisual` down through kits, missiles and effect names, and expose it as the fourteenth MCP tool `get_spell_visual_chain`.

**Architecture:** Nine hand-written `netstandard2.0` structure classes join the 46 existing ones in `SpellWork.Core/DBC/Structures/`, registered as `Storage<T>` properties on `DBC`. A layout-hash verification hook in the existing load loop records — not throws on — any table whose on-disk layout differs from the one the structure was written against. A pure resolver walks the graph and deduplicates kits reached from three directions; `SpellWork.Mcp` and the WinForms renderer both consume it.

**Tech Stack:** C# `netstandard2.0` (Core), .NET 9 (`SpellWork.Mcp`, tests), .NET Framework 4.8 (WinForms), `DBFileReaderLib` 1.0.0.0 (binary NuGet package), xunit + `Xunit.SkippableFact`.

---

## Measured ground truth

Every number below was measured on this machine against
`F:/WorkDir/BFA-HavenCore-Corruption/build/bin/Debug/ClientData/dbc/enUS`
before this plan was written. Treat them as the acceptance criteria they are —
if your build reports different values, **stop and report it** rather than
editing the expected values to match.

| Table | LayoutHash | FieldsCount | Status |
| --- | --- | --- | --- |
| `SpellXSpellVisual` | `0x8D0B1C8F` | 12 | already modelled |
| `SpellMissile` | `0x086B0CE5` | 15 | already modelled, no consumer |
| `SpellVisual` | `0x17962AA0` | 15 | Task 2 |
| `SpellVisualEvent` | `0xA85282AD` | 8 | Task 2 |
| `SpellVisualMissile` | `0xBE91C62B` | 16 | Task 2 |
| `SpellVisualKit` | `0x6D87523A` | 5 | Task 3 |
| `SpellVisualKitEffect` | `0xD906832B` | 2 | Task 3 |
| `SpellVisualEffectName` | `0x8AA2AAED` | 14 | Task 3 |
| `SpellVisualKitModelAttach` | `0xB8BDA6F9` | 21 | Task 3 |
| `SpellChainEffects` | `0x31DC7CA9` | 60 | Task 4 |
| `SpellMissileMotion` | `0xA76EF553` | 4 | Task 4 |

### Three facts that will cost you a day if you learn them the hard way

**1. `FieldsCount` is not the number of C# fields.** It excludes the
`$noninline,id$` ID column and counts an array as **one** field. Verified twice:

- `SpellVisualKit` — WoWDBDefs `LAYOUT 6D87523A` lists ID + `ClutterLevel`,
  `FallbackSpellVisualKitID`, `DelayMin`, `DelayMax`, `Flags[2]`. Five storage
  fields. `FieldsCount` reports 5. TrinityCore's `SpellVisualKitLoadInfo`
  (`DB2LoadInfo.h:5802`) declares **seven** `DB2FieldMeta` entries for the same
  table, because it counts the ID and expands `Flags[2]` into `Flags1`/`Flags2`.
  Both are right about the layout; they count differently.
- `SpellVisual` — 15 fields after the ID, and `FieldsCount` reports 15.

So the assertion to write is
`reader.FieldsCount == <C# fields excluding [Index] ID, arrays counted once>`.

**2. `DBReader.RecordsCount` is not the row count. Never use it.**
`SpellVisualKit.db2` reports `RecordsCount = 169` against a 903 KB file, while
`Storage<SpellVisualKitEntry>.Count` returns **112,694**. `RecordsCount`
reflects only the inline section; the bulk of the table lives in offset-map
sections that only materialise when `Storage<T>` is built. Asserting on
`RecordsCount` would make every referential-integrity test in this plan look
catastrophically broken for a reason that has nothing to do with your layout.
**Every row-count assertion in this plan uses `Storage<T>.Count`.**

**3. The layout hash cannot catch a wrong *type* at the same width.**
`int` vs `float` vs `uint` are all 32 bits, so a field declared `int` where the
data is `float` produces silent garbage with a matching hash and a matching
field count. `SpellVisual.MissileCastOffset[3]` and `MissileImpactOffset[3]`
are the specific hazard — they are 3-element position vectors, and the only
thing that will tell you whether they are `int` or `float` is reading the
`COLUMNS` block of the `.dbd` and sanity-checking the decoded values. Control 3
(referential integrity) does not cover them either, because they are not
references. Eyeball the values.

### Measured reference values used as test constants

Loaded on this machine during planning, against the reference client data:

| Fact | Measured |
| --- | --- |
| `Storage<SpellVisualKitEntry>.Count` | **112,694** (ids 1…134,914) — against `RecordsCount` 169 |
| `Storage<SpellXSpellVisualEntry>.Count` | **136,553** — against `RecordsCount` 136,559 |
| Spell **133** (Fireball) | exactly **one** row: `SpellVisualID 69095`, `DifficultyID 0`, `Priority 0`, `Probability 1` |
| Spell **282039** | **five** rows spanning difficulties `0, 14, 15, 16, 17` |
| Spells with >1 distinct `DifficultyID` | **106** |

Fireball having a single variant is why the difficulty-filter test in Task 5
uses 282039 instead: `all >= zero` on a one-row spell is `1 >= 1`, which passes
without exercising the filter at all.

`SpellVisualKit`'s structure was loaded with TrinityCore's field list during
planning and decoded cleanly, so its layout in Task 3 is verified, not proposed.

### Where the definitions come from

WoWDBDefs, read at
`https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/<Table>.dbd`.
Both hashes spot-checked during planning (`6D87523A`, `17962AA0`) are present in
those files, so the primary control is known to work end to end.

A `.dbd` has a `COLUMNS` block (types and foreign keys, once) followed by
`LAYOUT`/`BUILD` blocks (ordered field lists, per layout). **Match on the
`LAYOUT` hash from the table above, never on the build number** — a single
`LAYOUT` line can list several hashes and span many builds. Take the field
*order* from the `LAYOUT` block and the field *type* from `COLUMNS`.

Use WoWDBDefs as a reference only. **No new runtime dependency, no vendored
`.dbd` files** — the structures are hand-written C# in the style of the
existing 46.

### Naming

Where TrinityCore and WoWDBDefs disagree, **follow WoWDBDefs** and note the
divergence in an XML doc comment. The one known case: WoWDBDefs calls
`SpellVisualKit`'s second field `ClutterLevel`; TrinityCore calls it
`FallbackPriority` (`DB2Structure.h:3438`). Same width, same slot, different
name — a naming choice, not a layout risk.

---

## Global Constraints

- **`SpellWork.Core` must stay `netstandard2.0`-clean.** It is consumed by .NET Framework 4.8 (WinForms) and .NET 9 (MCP + tests).
- **No `Console.Write*` anywhere under `SpellWork.Mcp`.** stdout carries JSON-RPC frames and nothing else; a stray write kills the transport with no diagnosis.
- **The MCP server is read-only.** No write path to DB2 files or to MySQL, permanently (spec §11).
- **No test may open a socket to the live MySQL server.**
- **Server-side result cap of 500 rows** on every list-shaped tool response (`ToolResponse.MaxRows`), reported via `truncated`.
- Client-data-dependent tests are gated with `[SkippableFact]` + `Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason)` and must pass (as skips) with the env vars unset.
- `GameTable<T>.Open` throws duplicate-key on a **second** successful client-data load in one process. One fixture per process; never construct a second.
- Golden-file renders (`SpellWork.Core.Tests`) must stay byte-identical unless a task explicitly changes render output, in which case the golden is re-captured **in that same commit**.
- Structure classes are hand-written C# matching the existing style: `sealed class <Name>Entry`, public fields (not properties), `[Index(false)]` on the id, `[Cardinality(n)]` on arrays.
- **The store class is `SpellWork.DBC.DBC` and must always be written `DBC.DBC.X`.** Plain `DBC.X` does **not** compile, even with `using SpellWork.DBC;` — every file here is nested under the `SpellWork` namespace, which makes the sibling *namespace* `SpellWork.DBC` visible unqualified as `DBC` and take priority over the *class* of the same name (CS0234). Neither a `using` nor a `using DBC = …;` alias wins against that. The collision is documented at `SpellWork.Mcp/SpellDataService.cs:10-18` and `SpellWork/Program.cs` resolves it the same way. Test code in this plan that shows a bare `DBC.SpellVisual` is wrong — write `DBC.DBC.SpellVisual`.
- Structure classes live in `namespace SpellWork.DBC.Structures`; `RenderContext` and the spell types live in `namespace SpellWork.Spell`.

### Verification (every task)

```bash
dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj
dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj
"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" SpellWork.sln -restore -t:Rebuild -v:minimal
```

Run both suites **with and without** the client-data env vars and report all
four counts. `-t:Rebuild` is mandatory whenever you report a warning count —
this codebase has twice shipped a warning that an incremental build hid.

```
SPELLWORK_DBC_PATH=F:/WorkDir/BFA-HavenCore-Corruption/build/bin/Debug/ClientData/dbc
SPELLWORK_GT_PATH=F:/WorkDir/BFA-HavenCore-Corruption/build/bin/Debug/ClientData/gt
SPELLWORK_LOCALE=enUS
```

Baseline at the start of this plan: **Core 55/55 and Mcp 257/257 with client
data; Core 21 passed / 34 skipped and Mcp 204 / 53 skipped without; zero
failures; `-t:Rebuild` zero warnings across five projects.**

---

## File Structure

**Created**

| File | Responsibility |
| --- | --- |
| `SpellWork.Core/DBC/LayoutVerification.cs` | Expected-layout table + the mismatch record. One responsibility: answer "does this file's layout match what the structure was written against?" |
| `SpellWork.Core/DBC/Structures/SpellVisualEntry.cs` | One structure per file, matching the existing 46. |
| `…/SpellVisualEventEntry.cs`, `…/SpellVisualMissileEntry.cs` | " |
| `…/SpellVisualKitEntry.cs`, `…/SpellVisualKitEffectEntry.cs` | " |
| `…/SpellVisualEffectNameEntry.cs`, `…/SpellVisualKitModelAttachEntry.cs` | " |
| `…/SpellChainEffectsEntry.cs`, `…/SpellMissileMotionEntry.cs` | " |
| `SpellWork.Core/Spell/SpellVisualChain.cs` | The resolved-tree POCOs. Data only, no walking logic. |
| `SpellWork.Core/Spell/SpellVisualChainResolver.cs` | The graph walk. Pure: `DBC` in, `SpellVisualChain` out, no I/O and no formatting. |
| `SpellWork.Mcp/Tools/VisualTools.cs` | The `get_spell_visual_chain` tool. Serialisation and caps only. |
| `SpellWork.Core.Tests/LayoutVerificationTests.cs` | Control 1. |
| `SpellWork.Core.Tests/VisualChainIntegrityTests.cs` | Control 3. |
| `SpellWork.Core.Tests/SpellVisualChainResolverTests.cs` | Resolver behaviour. |
| `SpellWork.Mcp.Tests/VisualToolsTests.cs` | Tool contract. |

**Modified**

| File | Change |
| --- | --- |
| `SpellWork.Core/DBC/DBC.cs` | 9 new `Storage<T>` properties; uncomment `SpellVisual`/`SpellMissileMotion` at `:77-78`; call the layout check in the load loop at `:164`. |
| `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs` | Add `"visual"` to `ValidSections` (`:59-64`) and a `BuildVisual` section. |
| `SpellWork.Core/Spell/SpellInfo.cs` | Replace the commented-out `AppendSpellVisualInfo()` body at `:1001`. |
| `SpellWork.Mcp/SpellDataService.cs` | Surface layout mismatches in `server_info`. |
| `docs/mcp-server.md` | Fourteenth tool; `visual` section; the `RecordsCount` note. |
| `SpellWork.Mcp.Tests/StdioSmokeTests.cs` | `ExpectedTools` 13 → 14. |

---

## Task 1: Layout verification hook

The control everything else rests on. It lands first and alone so that every
later task's structure is verified by machinery that is already proven, and so
a reviewer can reject the control without rejecting nine structures.

**Design decision — record, do not throw.** A layout mismatch means *this
client build differs from the one the structure was written against*, which is
a diagnosis, not a crash. Throwing would take out the whole load (and the GUI
with it) over a table the user may not even be asking about, and would repeat
the `FileNotFoundException` mistake this codebase already fixed once. Mismatches
are collected and surfaced; tests assert the collection is empty for the
reference build.

**Files:**
- Create: `SpellWork.Core/DBC/LayoutVerification.cs`
- Modify: `SpellWork.Core/DBC/DBC.cs` (load loop at `:160-175`)
- Test: `SpellWork.Core.Tests/LayoutVerificationTests.cs`

**Interfaces:**
- Consumes: `DBFileReaderLib.DBReader.LayoutHash` (`uint`), `.FieldsCount` (`int`) — both public, confirmed by reflection against the shipped DLL.
- Produces:
  - `SpellWork.DBC.LayoutVerification.Expected` — `IReadOnlyDictionary<string, (uint LayoutHash, int FieldsCount)>`
  - `SpellWork.DBC.LayoutVerification.Check(string name, uint layoutHash, int fieldsCount)` → `LayoutMismatch` or `null`
  - `SpellWork.DBC.LayoutMismatch` — `sealed class` with `string Table`, `uint ExpectedLayoutHash`, `uint ActualLayoutHash`, `int ExpectedFieldsCount`, `int ActualFieldsCount`, `string Describe()`
  - `SpellWork.DBC.DBC.LayoutMismatches` — `IReadOnlyList<LayoutMismatch>`, populated by `LoadAsync`

- [ ] **Step 1: Write the failing test**

Create `SpellWork.Core.Tests/LayoutVerificationTests.cs`:

```csharp
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class LayoutVerificationTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public LayoutVerificationTests(ClientDataFixture fixture) => _fixture = fixture;

        // A table nobody has written an expected layout for must not be
        // reported as a mismatch -- 46 of the 46 existing structures have no
        // entry yet, and reporting all of them would bury the real signal.
        [Fact]
        public void Check_UnknownTable_ReturnsNull()
        {
            Assert.Null(LayoutVerification.Check("NoSuchTable", 0xDEADBEEF, 99));
        }

        [Fact]
        public void Check_MatchingLayout_ReturnsNull()
        {
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];
            Assert.Null(LayoutVerification.Check(
                "SpellXSpellVisual", expected.LayoutHash, expected.FieldsCount));
        }

        [Fact]
        public void Check_WrongLayoutHash_ReportsBothValues()
        {
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];
            var mismatch = LayoutVerification.Check("SpellXSpellVisual", 0x11112222, expected.FieldsCount);

            Assert.NotNull(mismatch);
            Assert.Equal(0x11112222u, mismatch.ActualLayoutHash);
            Assert.Equal(expected.LayoutHash, mismatch.ExpectedLayoutHash);
            // The message has to carry both hashes: "SpellXSpellVisual has the
            // wrong layout" sends the reader to WoWDBDefs with nothing to
            // search for.
            Assert.Contains("11112222", mismatch.Describe());
            Assert.Contains(expected.LayoutHash.ToString("X8"), mismatch.Describe());
        }

        [Fact]
        public void Check_WrongFieldCount_IsReportedEvenWhenHashMatches()
        {
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];
            var mismatch = LayoutVerification.Check(
                "SpellXSpellVisual", expected.LayoutHash, expected.FieldsCount + 1);

            Assert.NotNull(mismatch);
            Assert.Equal(expected.FieldsCount + 1, mismatch.ActualFieldsCount);
        }

        // The measured values for the reference build. If this fails, the
        // client data changed -- do not "fix" it by editing Expected.
        [SkippableFact]
        public void ReferenceBuild_HasNoLayoutMismatches()
        {
            _fixture.SkipIfNoClientData();
            Assert.Empty(DBC.LayoutMismatches);
        }

        [SkippableFact]
        public void SpellXSpellVisual_MatchesMeasuredLayout()
        {
            _fixture.SkipIfNoClientData();
            var expected = LayoutVerification.Expected["SpellXSpellVisual"];
            Assert.Equal(0x8D0B1C8Fu, expected.LayoutHash);
            Assert.Equal(12, expected.FieldsCount);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~LayoutVerification"`
Expected: FAIL — `LayoutVerification` does not exist.

- [ ] **Step 3: Write `LayoutVerification`**

Create `SpellWork.Core/DBC/LayoutVerification.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;

namespace SpellWork.DBC
{
    /// <summary>
    /// One table's on-disk layout disagreeing with the layout its structure
    /// class was written against.
    /// </summary>
    public sealed class LayoutMismatch
    {
        public string Table { get; set; }
        public uint ExpectedLayoutHash { get; set; }
        public uint ActualLayoutHash { get; set; }
        public int ExpectedFieldsCount { get; set; }
        public int ActualFieldsCount { get; set; }

        /// <summary>
        /// Both values, both named, in the form the reader needs to search
        /// WoWDBDefs with. A message that says only "wrong layout" sends them
        /// there with nothing to look for.
        /// </summary>
        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0}.db2 layout hash 0x{1:X8} (expected 0x{2:X8}), {3} fields (expected {4}). " +
            "The structure class was written against a different client build; " +
            "look up LAYOUT {1:X8} in WoWDBDefs before trusting any field of it.",
            Table, ActualLayoutHash, ExpectedLayoutHash, ActualFieldsCount, ExpectedFieldsCount);
    }

    /// <summary>
    /// Control 1 of the three in the design's section 7.1, and the strongest.
    /// <para>
    /// DBFileReaderLib maps fields by declaration order, so a structure whose
    /// fields are the right count but the wrong order decodes to silent
    /// garbage rather than an error. A field-count mismatch already fails
    /// loudly (DBC.cs catches the ArgumentException). Field *order* among
    /// same-width types is what fails quietly, and the WDC3 layout hash is
    /// computed over the field layout, so it catches exactly that.
    /// </para>
    /// <para>
    /// Only tables listed here are checked. An unlisted table returns null
    /// rather than a mismatch: the 46 structures that predate this mechanism
    /// have no recorded expected layout, and reporting all of them would bury
    /// the signal this exists to produce.
    /// </para>
    /// </summary>
    public static class LayoutVerification
    {
        /// <summary>
        /// Measured against the 8.3.7 client data at
        /// BFA-HavenCore-Corruption/build/bin/Debug/ClientData/dbc/enUS.
        /// <para>
        /// FieldsCount excludes the non-inline id column and counts an array
        /// as one field -- SpellVisualKit reports 5 for ID + ClutterLevel +
        /// FallbackSpellVisualKitID + DelayMin + DelayMax + Flags[2].
        /// </para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (uint LayoutHash, int FieldsCount)> Expected =
            new Dictionary<string, (uint, int)>
            {
                ["SpellXSpellVisual"] = (0x8D0B1C8F, 12),
                ["SpellMissile"]      = (0x086B0CE5, 15),
            };

        public static LayoutMismatch Check(string name, uint layoutHash, int fieldsCount)
        {
            if (!Expected.TryGetValue(name, out var expected))
                return null;

            if (expected.LayoutHash == layoutHash && expected.FieldsCount == fieldsCount)
                return null;

            return new LayoutMismatch
            {
                Table = name,
                ExpectedLayoutHash = expected.LayoutHash,
                ActualLayoutHash = layoutHash,
                ExpectedFieldsCount = expected.FieldsCount,
                ActualFieldsCount = fieldsCount,
            };
        }
    }
}
```

- [ ] **Step 4: Wire it into the load loop**

In `SpellWork.Core/DBC/DBC.cs`, add the backing field and public view next to `UnloadedStores`:

```csharp
        private static readonly ConcurrentBag<LayoutMismatch> _layoutMismatches =
            new ConcurrentBag<LayoutMismatch>();

        /// <summary>
        /// Tables whose on-disk layout differs from the one their structure
        /// class was written against, worst case first is not meaningful here
        /// so they are sorted by table name. Empty on a matching client build.
        /// <para>
        /// Recorded rather than thrown. A mismatch means this client build
        /// differs from the reference one, which is a diagnosis; throwing
        /// would abort the whole load over a table the caller may not be
        /// asking about, which is the mistake the FileNotFoundException catch
        /// below already exists to avoid repeating.
        /// </para>
        /// </summary>
        public static IReadOnlyList<LayoutMismatch> LayoutMismatches
        {
            get
            {
                var mismatches = new List<LayoutMismatch>(_layoutMismatches);
                mismatches.Sort((a, b) => string.CompareOrdinal(a.Table, b.Table));
                return mismatches;
            }
        }
```

Add `using System.Collections.Concurrent;` if it is not already present.

In the `Parallel.ForEach` body, immediately after the `DBReader` is constructed
(currently `DBC.cs:164`) and **before** `Activator.CreateInstance`:

```csharp
                       var db2Reader = new DBReader($@"{config.DbcPath}\{config.Locale}\{name}.db2");

                       // Check the layout before building the Storage<T>, not
                       // after: a mismatched layout still constructs happily
                       // and the point is to know before anyone reads a field.
                       var mismatch = LayoutVerification.Check(
                           name, db2Reader.LayoutHash, db2Reader.FieldsCount);
                       if (mismatch != null)
                           _layoutMismatches.Add(mismatch);

                       dynamic storage = Activator.CreateInstance(dbc.PropertyType, db2Reader);
```

Clear the bag at the top of `LoadAsync`, alongside the other per-load resets, so
a second load in one process does not double-report:

```csharp
            while (_layoutMismatches.TryTake(out _)) { }
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~LayoutVerification"`
Expected: PASS — 4 unconditional, 2 skippable.

- [ ] **Step 6: Prove the check actually fires**

A test that only ever sees matching data proves nothing. Temporarily change
`["SpellXSpellVisual"] = (0x8D0B1C8F, 12)` to `(0x8D0B1C90, 12)`, run
`ReferenceBuild_HasNoLayoutMismatches` with client data configured, and confirm
it **fails** naming `SpellXSpellVisual`. Revert the edit. Report in your task
report that you did this and what the failure message said — this is the only
evidence that the control works against real data.

- [ ] **Step 7: Full verification and commit**

```bash
git add SpellWork.Core/DBC/LayoutVerification.cs SpellWork.Core/DBC/DBC.cs SpellWork.Core.Tests/LayoutVerificationTests.cs
git commit -m "feat: verify DB2 layout hashes against the structures they were written against"
```

---

## Task 2: The visual-side structures

`SpellVisual`, `SpellVisualEvent` and `SpellVisualMissile` — everything reached
directly from a `SpellXSpellVisual.SpellVisualID`.

**Files:**
- Create: `SpellWork.Core/DBC/Structures/SpellVisualEntry.cs`, `SpellVisualEventEntry.cs`, `SpellVisualMissileEntry.cs`
- Modify: `SpellWork.Core/DBC/DBC.cs` (properties; uncomment `:77`), `SpellWork.Core/DBC/LayoutVerification.cs` (three `Expected` entries)
- Test: `SpellWork.Core.Tests/VisualChainIntegrityTests.cs`

**Interfaces:**
- Consumes: `LayoutVerification.Expected`, `DBC.LayoutMismatches` (Task 1).
- Produces: `DBC.SpellVisual`, `DBC.SpellVisualEvent`, `DBC.SpellVisualMissile` — all `Storage<T>` keyed by `int` id.

- [ ] **Step 1: Derive the three field lists from WoWDBDefs**

For each of `SpellVisual`, `SpellVisualEvent`, `SpellVisualMissile`, fetch
`https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/<Table>.dbd`
and locate the `LAYOUT` block containing the hash from the table below. Take
field **order** from that block and field **type** from `COLUMNS`.

| Table | LayoutHash | FieldsCount |
| --- | --- | --- |
| `SpellVisual` | `0x17962AA0` | 15 |
| `SpellVisualEvent` | `0xA85282AD` | 8 |
| `SpellVisualMissile` | `0xBE91C62B` | 16 |

`SpellVisual`'s field list under `LAYOUT 514F85ED, 17962AA0` was read during
planning and is reproduced here so you can check your own lookup against it —
**verify it rather than trusting it**, and if yours differs, yours wins and you
say so in your report:

```
$noninline,id$ID<32>
MissileCastOffset[3]
MissileImpactOffset[3]
AnimEventSoundID<u32>
Flags<32>
MissileAttachment<8>
MissileDestinationAttachment<8>
MissileCastPositionerID<u32>
MissileImpactPositionerID<u32>
MissileTargetingKit<32>
HostileSpellVisualID<u32>
CasterSpellVisualID<u32>
SpellVisualMissileSetID<u16>
DamageNumberDelay<u16>
LowViolenceSpellVisualID<u32>
RaidSpellVisualMissileSetID<u32>
```

Fifteen fields after the ID — which is why `FieldsCount` is 15.

**`MissileCastOffset` and `MissileImpactOffset` need your attention.** They are
3-element vectors and the layout hash cannot tell `int[3]` from `float[3]` —
both are 12 bytes. Read the `COLUMNS` block for their declared type, and after
loading, print a few decoded values: plausible position offsets are small
signed floats (roughly -50…50). If you see values like `1065353216` you have
`int` where `float` belongs. Report which you chose and what the values looked
like.

- [ ] **Step 2: Write the failing integrity test**

Create `SpellWork.Core.Tests/VisualChainIntegrityTests.cs`:

```csharp
using System.Linq;
using SpellWork.DBC;
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
            Assert.True(DBC.SpellVisual.Count > 10000,
                $"SpellVisual loaded {DBC.SpellVisual.Count} rows; expected the full table.");
        }

        [SkippableFact]
        public void EverySpellVisualIdReferencedByXSpellVisual_Resolves()
        {
            _fixture.SkipIfNoClientData();

            var referenced = DBC.SpellXSpellVisual.Values
                .Select(x => (int)x.SpellVisualID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            var unresolved = referenced.Count(id => !DBC.SpellVisual.ContainsKey(id));
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

            var parents = DBC.SpellVisualEvent.Values
                .Select(e => e.SpellVisualID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            Skip.If(parents.Count == 0, "SpellVisualEvent carries no parent visual ids.");

            var unresolved = parents.Count(id => !DBC.SpellVisual.ContainsKey(id));
            var rate = 1.0 - (double)unresolved / parents.Count;

            Assert.True(rate > 0.99,
                $"Only {rate:P2} of {parents.Count} SpellVisualEvent parents resolve.");
        }

        [SkippableFact]
        public void VisualTables_HaveNoLayoutMismatches()
        {
            _fixture.SkipIfNoClientData();
            var visual = DBC.LayoutMismatches
                .Where(m => m.Table.StartsWith("SpellVisual"))
                .Select(m => m.Describe());
            Assert.Empty(visual);
        }
    }
}
```

- [ ] **Step 3: Run it and watch it fail**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~VisualChainIntegrity"`
Expected: FAIL to compile — `DBC.SpellVisual` does not exist.

- [ ] **Step 4: Write the three structure classes**

One file each in `SpellWork.Core/DBC/Structures/`, matching the existing style
exactly — `sealed class`, public **fields**, `[Index(false)]` on the id,
`[Cardinality(n)]` on arrays. `SpellVisualEntry.cs` takes this shape (fill the
remaining fields from your Step 1 lookup):

```csharp
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualEntry
    {
        [Index(false)]
        public int ID;
        [Cardinality(3)]
        public float[] MissileCastOffset = new float[3];
        [Cardinality(3)]
        public float[] MissileImpactOffset = new float[3];
        public uint AnimEventSoundID;
        public int Flags;
        public sbyte MissileAttachment;
        public sbyte MissileDestinationAttachment;
        public uint MissileCastPositionerID;
        public uint MissileImpactPositionerID;
        public int MissileTargetingKit;
        public uint HostileSpellVisualID;
        public uint CasterSpellVisualID;
        public ushort SpellVisualMissileSetID;
        public ushort DamageNumberDelay;
        public uint LowViolenceSpellVisualID;
        public uint RaidSpellVisualMissileSetID;
    }
}
```

- [ ] **Step 5: Register the stores**

In `SpellWork.Core/DBC/DBC.cs`, delete the commented-out line at **`:78`**
(`// public static Storage<SpellVisualEntry> SpellVisual { get; public set; }` —
note it would not even compile as written, `get; public set;` is not valid C#,
so it was never live code you are restoring) and add three live properties in
the same block as the other spell stores:

```csharp
        public static Storage<SpellVisualEntry>                 SpellVisual { get; set; }
        public static Storage<SpellVisualEventEntry>            SpellVisualEvent { get; set; }
        public static Storage<SpellVisualMissileEntry>          SpellVisualMissile { get; set; }
```

Add the three measured entries to `LayoutVerification.Expected`:

```csharp
                ["SpellVisual"]        = (0x17962AA0, 15),
                ["SpellVisualEvent"]   = (0xA85282AD, 8),
                ["SpellVisualMissile"] = (0xBE91C62B, 16),
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~VisualChainIntegrity"`
Expected: PASS. A resolution rate below 99% means the field order is wrong —
go back to the `.dbd`, do not lower the threshold.

- [ ] **Step 7: Confirm the offsets decoded as positions**

With client data configured, dump `MissileCastOffset` for ten rows where it is
non-zero and put the values in your task report. Small signed values (roughly
-50…50) mean `float` was right. Values in the hundreds of millions mean the
field is `int` and you have found the exact failure mode the layout hash cannot
catch.

- [ ] **Step 8: Full verification and commit**

```bash
git add SpellWork.Core/DBC/Structures/SpellVisual*.cs SpellWork.Core/DBC/DBC.cs SpellWork.Core/DBC/LayoutVerification.cs SpellWork.Core.Tests/VisualChainIntegrityTests.cs
git commit -m "feat: model SpellVisual, SpellVisualEvent and SpellVisualMissile"
```

---

## Task 3: The kit-side structures

`SpellVisualKit`, `SpellVisualKitEffect`, `SpellVisualEffectName` and
`SpellVisualKitModelAttach` — the subtree every kit reference lands in.

**Files:**
- Create: `SpellWork.Core/DBC/Structures/SpellVisualKitEntry.cs`, `SpellVisualKitEffectEntry.cs`, `SpellVisualEffectNameEntry.cs`, `SpellVisualKitModelAttachEntry.cs`
- Modify: `SpellWork.Core/DBC/DBC.cs`, `SpellWork.Core/DBC/LayoutVerification.cs`
- Test: `SpellWork.Core.Tests/VisualChainIntegrityTests.cs` (extend)

**Interfaces:**
- Consumes: `DBC.SpellVisual`, `DBC.SpellVisualEvent`, `DBC.SpellVisualMissile` (Task 2).
- Produces: `DBC.SpellVisualKit`, `DBC.SpellVisualKitEffect`, `DBC.SpellVisualEffectName`, `DBC.SpellVisualKitModelAttach`.

| Table | LayoutHash | FieldsCount |
| --- | --- | --- |
| `SpellVisualKit` | `0x6D87523A` | 5 |
| `SpellVisualKitEffect` | `0xD906832B` | 2 |
| `SpellVisualEffectName` | `0x8AA2AAED` | 14 |
| `SpellVisualKitModelAttach` | `0xB8BDA6F9` | 21 |

- [ ] **Step 1: Write `SpellVisualKitEntry` from the verified layout**

This one was read from WoWDBDefs during planning and cross-checked against
TrinityCore, and it **loads correctly against the real file** — 112,694 rows,
ids 1…134914. Write it exactly as below:

```csharp
using DBFileReaderLib.Attributes;

namespace SpellWork.DBC.Structures
{
    public sealed class SpellVisualKitEntry
    {
        [Index(false)]
        public int ID;
        /// <summary>
        /// WoWDBDefs calls this ClutterLevel; TrinityCore calls the same byte
        /// FallbackPriority (DB2Structure.h:3438). Same width, same slot --
        /// a naming disagreement, not a layout one. WoWDBDefs wins here.
        /// </summary>
        public sbyte ClutterLevel;
        public int FallbackSpellVisualKitID;
        public ushort DelayMin;
        public ushort DelayMax;
        [Cardinality(2)]
        public int[] Flags = new int[2];
    }
}
```

- [ ] **Step 2: Derive the other three from WoWDBDefs**

Same procedure as Task 2 Step 1, matching on the hashes in the table above.

`SpellVisualKitEffect` reporting `FieldsCount = 2` is expected, not a lookup
error — it is a narrow join table. Watch for an `EffectType` discriminator
selecting how `Effect` is interpreted; if one exists, model it as a plain
integer field and let the resolver interpret it. Do not invent an enum.

- [ ] **Step 3: Write the failing integrity tests**

Append to `SpellWork.Core.Tests/VisualChainIntegrityTests.cs`:

```csharp
        [SkippableFact]
        public void SpellVisualKit_LoadsTheWholeTable()
        {
            _fixture.SkipIfNoClientData();
            // Measured: 112694 rows. DBReader.RecordsCount reports 169 for
            // this table because it counts only the inline section -- if this
            // assertion ever gets "fixed" by switching to RecordsCount, the
            // resolver silently loses 99.8% of its kits.
            Assert.True(DBC.SpellVisualKit.Count > 100000,
                $"SpellVisualKit loaded {DBC.SpellVisualKit.Count} rows; expected ~112694.");
        }

        [SkippableFact]
        public void EveryKitEffectParent_ResolvesToAKit()
        {
            _fixture.SkipIfNoClientData();

            var parents = DBC.SpellVisualKitEffect.Values
                .Select(e => e.ParentSpellVisualKitID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            var unresolved = parents.Count(id => !DBC.SpellVisualKit.ContainsKey(id));
            var rate = 1.0 - (double)unresolved / parents.Count;

            Assert.True(rate > 0.99,
                $"Only {rate:P2} of {parents.Count} kit-effect parents resolve " +
                $"({unresolved} unresolved).");
        }

        [SkippableFact]
        public void KitFallbackReferences_ResolveWithinTheSameTable()
        {
            _fixture.SkipIfNoClientData();

            var fallbacks = DBC.SpellVisualKit.Values
                .Select(k => k.FallbackSpellVisualKitID)
                .Where(id => id != 0)
                .Distinct()
                .ToList();

            Skip.If(fallbacks.Count == 0, "No kit declares a fallback kit.");

            var unresolved = fallbacks.Count(id => !DBC.SpellVisualKit.ContainsKey(id));
            Assert.True(1.0 - (double)unresolved / fallbacks.Count > 0.99,
                $"{unresolved} of {fallbacks.Count} fallback kit ids do not resolve.");
        }
```

Rename `ParentSpellVisualKitID` to whatever your Step 2 lookup actually calls
the field, and say so in your report.

- [ ] **Step 4: Run it and watch it fail**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~VisualChainIntegrity"`
Expected: FAIL to compile — the four stores do not exist yet.

- [ ] **Step 5: Register the stores and expected layouts**

```csharp
        public static Storage<SpellVisualKitEntry>              SpellVisualKit { get; set; }
        public static Storage<SpellVisualKitEffectEntry>        SpellVisualKitEffect { get; set; }
        public static Storage<SpellVisualEffectNameEntry>       SpellVisualEffectName { get; set; }
        public static Storage<SpellVisualKitModelAttachEntry>   SpellVisualKitModelAttach { get; set; }
```

```csharp
                ["SpellVisualKit"]            = (0x6D87523A, 5),
                ["SpellVisualKitEffect"]      = (0xD906832B, 2),
                ["SpellVisualEffectName"]     = (0x8AA2AAED, 14),
                ["SpellVisualKitModelAttach"] = (0xB8BDA6F9, 21),
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~VisualChainIntegrity"`
Expected: PASS, including `SpellVisualKit.Count > 100000`.

- [ ] **Step 7: Full verification and commit**

```bash
git add SpellWork.Core/DBC/Structures/ SpellWork.Core/DBC/DBC.cs SpellWork.Core/DBC/LayoutVerification.cs SpellWork.Core.Tests/VisualChainIntegrityTests.cs
git commit -m "feat: model the SpellVisualKit subtree"
```

---

## Task 4: The leaf structures

`SpellChainEffects` and `SpellMissileMotion`, plus giving the already-loaded
but unread `SpellMissile` store its first consumer.

**Files:**
- Create: `SpellWork.Core/DBC/Structures/SpellChainEffectsEntry.cs`, `SpellMissileMotionEntry.cs`
- Modify: `SpellWork.Core/DBC/DBC.cs` (uncomment `:78`), `SpellWork.Core/DBC/LayoutVerification.cs`
- Test: `SpellWork.Core.Tests/VisualChainIntegrityTests.cs` (extend)

**Interfaces:**
- Consumes: Tasks 2 and 3.
- Produces: `DBC.SpellChainEffects`, `DBC.SpellMissileMotion`.

| Table | LayoutHash | FieldsCount |
| --- | --- | --- |
| `SpellChainEffects` | `0x31DC7CA9` | 60 |
| `SpellMissileMotion` | `0xA76EF553` | 4 |

- [ ] **Step 1: Derive both from WoWDBDefs**

Same procedure. **`SpellChainEffects` has 60 fields** — it is by far the widest
table in this plan and the easiest to get subtly wrong. Transcribe it in one
pass, then count your fields (arrays once, excluding the id) and confirm you
get exactly 60 before writing any test.

`SpellMissileMotion` carries a script/name string; model strings as `string`
exactly as the existing structures with names do.

- [ ] **Step 2: Write the failing test**

Append to `SpellWork.Core.Tests/VisualChainIntegrityTests.cs`:

```csharp
        [SkippableFact]
        public void SpellChainEffects_MatchesItsRecordedLayout()
        {
            _fixture.SkipIfNoClientData();
            Assert.DoesNotContain(DBC.LayoutMismatches, m => m.Table == "SpellChainEffects");
            Assert.True(DBC.SpellChainEffects.Count > 4000,
                $"SpellChainEffects loaded {DBC.SpellChainEffects.Count} rows; expected ~4722.");
        }

        [SkippableFact]
        public void SpellMissileMotion_MatchesItsRecordedLayout()
        {
            _fixture.SkipIfNoClientData();
            Assert.DoesNotContain(DBC.LayoutMismatches, m => m.Table == "SpellMissileMotion");
            Assert.True(DBC.SpellMissileMotion.Count > 1000,
                $"SpellMissileMotion loaded {DBC.SpellMissileMotion.Count} rows; expected ~1461.");
        }

        // SpellMissile has been loaded since before this plan and never read.
        // Assert it is real before the resolver depends on it.
        [SkippableFact]
        public void SpellMissile_IsLoadedAndMatchesItsLayout()
        {
            _fixture.SkipIfNoClientData();
            Assert.DoesNotContain(DBC.LayoutMismatches, m => m.Table == "SpellMissile");
            Assert.True(DBC.SpellMissile.Count > 300,
                $"SpellMissile loaded {DBC.SpellMissile.Count} rows; expected ~330.");
        }
```

- [ ] **Step 3: Run it and watch it fail**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~VisualChainIntegrity"`
Expected: FAIL to compile.

- [ ] **Step 4: Write the structures and register them**

Delete the commented-out `SpellMissileMotion` line at **`DBC.cs:77`** (Task 2
removed the `SpellVisual` one below it at `:78`, so re-check the line number
before editing) and add:

```csharp
        public static Storage<SpellChainEffectsEntry>           SpellChainEffects { get; set; }
        public static Storage<SpellMissileMotionEntry>          SpellMissileMotion { get; set; }
```

```csharp
                ["SpellChainEffects"]  = (0x31DC7CA9, 60),
                ["SpellMissileMotion"] = (0xA76EF553, 4),
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~VisualChainIntegrity"`
Expected: PASS.

- [ ] **Step 6: Confirm every visual table is now covered**

`DBC.LayoutMismatches` must be empty and `LayoutVerification.Expected` must hold
all eleven tables from the ground-truth table at the top of this plan. Add:

```csharp
        [Fact]
        public void Expected_CoversEveryVisualChainTable()
        {
            var required = new[]
            {
                "SpellXSpellVisual", "SpellMissile", "SpellVisual", "SpellVisualEvent",
                "SpellVisualMissile", "SpellVisualKit", "SpellVisualKitEffect",
                "SpellVisualEffectName", "SpellVisualKitModelAttach", "SpellChainEffects",
                "SpellMissileMotion",
            };
            foreach (var table in required)
                Assert.True(LayoutVerification.Expected.ContainsKey(table),
                    $"{table} has no recorded expected layout, so nothing verifies its field order.");
        }
```

Put it in `LayoutVerificationTests.cs`; it needs no client data.

- [ ] **Step 7: Full verification and commit**

```bash
git add SpellWork.Core/DBC/Structures/ SpellWork.Core/DBC/DBC.cs SpellWork.Core/DBC/LayoutVerification.cs SpellWork.Core.Tests/
git commit -m "feat: model SpellChainEffects and SpellMissileMotion, completing the visual tables"
```

---

## Task 5: The resolver

The graph walk. Pure and host-agnostic: `DBC` in, POCOs out, no formatting and
no I/O, so the MCP tool and the WinForms renderer share one implementation.

**Files:**
- Create: `SpellWork.Core/Spell/SpellVisualChain.cs`, `SpellWork.Core/Spell/SpellVisualChainResolver.cs`
- Test: `SpellWork.Core.Tests/SpellVisualChainResolverTests.cs`

**Interfaces:**
- Consumes: every store from Tasks 2-4, plus `DBC.SpellXSpellVisual`.
- Produces:
  - `SpellVisualChainResolver.Resolve(int spellId, int difficulty = 0, int maxKits = 500)` → `SpellVisualChain`
  - `SpellVisualChain` with `int SpellID`, `IReadOnlyList<SpellVisualVariant> Variants`, `bool Truncated`, `IReadOnlyList<string> UnresolvedReferences`
  - `SpellVisualVariant` with `int SpellVisualID`, `int DifficultyID`, `float Probability`, `int Priority`, `bool VisualResolved`, `IReadOnlyList<SpellVisualKitRef> Kits`, `SpellMissileInfo Missile`
  - `SpellVisualKitRef` with `int KitID`, `string ReachedVia`, `IReadOnlyList<SpellVisualKitEffectInfo> Effects`
  - `SpellMissileInfo` with `int MissileID`, `int MotionID`, `string MotionName`

**Kits are reached from three directions** — ids carried on `SpellVisual`
itself, on `SpellVisualEvent` rows for that visual, and on `SpellVisualMissile`
rows for that visual. Gather them as a **set**, not a path, and record on each
`SpellVisualKitRef` which direction found it first in `ReachedVia`
(`"visual"`, `"event"` or `"missile"`). A kit reachable two ways appears once.

- [ ] **Step 1: Write the failing tests**

Create `SpellWork.Core.Tests/SpellVisualChainResolverTests.cs`:

```csharp
using System.Linq;
using SpellWork.DBC;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class SpellVisualChainResolverTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellVisualChainResolverTests(ClientDataFixture fixture) => _fixture = fixture;

        // Fireball. Measured during planning: exactly one SpellXSpellVisual
        // row -- SpellVisualID 69095, DifficultyID 0, Priority 0,
        // Probability 1. One row, so do not use this spell for anything that
        // needs several variants.
        private const int Fireball = 133;

        [SkippableFact]
        public void Resolve_Fireball_FindsItsVisual()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);

            Assert.Equal(Fireball, chain.SpellID);
            Assert.NotEmpty(chain.Variants);
            Assert.Contains(chain.Variants, v => v.SpellVisualID == 69095);
            Assert.All(chain.Variants, v => Assert.True(v.VisualResolved,
                $"SpellVisualID {v.SpellVisualID} did not resolve in SpellVisual."));
        }

        [SkippableFact]
        public void Resolve_Fireball_ReachesKits()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);
            var kits = chain.Variants.SelectMany(v => v.Kits).ToList();

            Assert.NotEmpty(kits);
            // Every kit id we report must exist -- reporting an id we could
            // not resolve as though it were a kit is the failure mode that
            // makes the whole tool untrustworthy.
            Assert.All(kits, k => Assert.True(DBC.SpellVisualKit.ContainsKey(k.KitID)));
        }

        [SkippableFact]
        public void Resolve_DeduplicatesKitsReachedFromMoreThanOneDirection()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);

            foreach (var variant in chain.Variants)
            {
                var ids = variant.Kits.Select(k => k.KitID).ToList();
                Assert.Equal(ids.Count, ids.Distinct().Count());
            }
        }

        [SkippableFact]
        public void Resolve_RecordsHowEachKitWasReached()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(Fireball);
            var kits = chain.Variants.SelectMany(v => v.Kits).ToList();

            Assert.All(kits, k => Assert.Contains(k.ReachedVia, new[] { "visual", "event", "missile" }));
        }

        [SkippableFact]
        public void Resolve_UnknownSpell_ReturnsEmptyRatherThanThrowing()
        {
            _fixture.SkipIfNoClientData();

            var chain = SpellVisualChainResolver.Resolve(999999999);

            Assert.Empty(chain.Variants);
            Assert.False(chain.Truncated);
        }

        // NOT Fireball. Measured: spell 133 has exactly ONE SpellXSpellVisual
        // row (visual 69095, difficulty 0), so `all >= zero` would be 1 >= 1 --
        // passing while proving nothing. Spell 282039 has five rows spanning
        // difficulties 0, 14, 15, 16 and 17; 106 spells in this build have
        // more than one distinct difficulty.
        private const int MultiDifficultySpell = 282039;

        [SkippableFact]
        public void Resolve_DifficultyFilter_NarrowsTheVariants()
        {
            _fixture.SkipIfNoClientData();
            Skip.IfNot(DBC.DBC.SpellInfoStore.ContainsKey(MultiDifficultySpell),
                $"Spell {MultiDifficultySpell} is absent from this build.");

            var all = SpellVisualChainResolver.Resolve(MultiDifficultySpell, difficulty: -1);
            var zero = SpellVisualChainResolver.Resolve(MultiDifficultySpell, difficulty: 0);

            Assert.All(zero.Variants, v => Assert.Equal(0, v.DifficultyID));
            Assert.True(all.Variants.Count > zero.Variants.Count,
                $"difficulty -1 returned {all.Variants.Count} variants and difficulty 0 returned " +
                $"{zero.Variants.Count}; the filter is not narrowing anything.");
        }

        // The cap has to bite on a real budget, not on an empty input.
        [SkippableFact]
        public void Resolve_MaxKits_CapsAndFlagsTruncation()
        {
            _fixture.SkipIfNoClientData();

            var capped = SpellVisualChainResolver.Resolve(Fireball, maxKits: 1);
            var kits = capped.Variants.SelectMany(v => v.Kits).Count();

            Assert.True(kits <= 1, $"maxKits: 1 returned {kits} kits.");
            var uncapped = SpellVisualChainResolver.Resolve(Fireball);
            Skip.If(uncapped.Variants.SelectMany(v => v.Kits).Count() <= 1,
                "Fireball has one kit or fewer; this assertion cannot bite.");
            Assert.True(capped.Truncated);
        }
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~SpellVisualChainResolver"`
Expected: FAIL to compile — `SpellVisualChainResolver` does not exist.

- [ ] **Step 3: Write the POCOs**

Create `SpellWork.Core/Spell/SpellVisualChain.cs` holding `SpellVisualChain`,
`SpellVisualVariant`, `SpellVisualKitRef`, `SpellVisualKitEffectInfo` and
`SpellMissileInfo` as the shapes listed under **Interfaces** above. Data only —
no methods beyond property accessors. Keep them in `namespace SpellWork.Spell`.

- [ ] **Step 4: Write the resolver**

Create `SpellWork.Core/Spell/SpellVisualChainResolver.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using SpellWork.DBC;

namespace SpellWork.Spell
{
    /// <summary>
    /// Walks a spell's visual chain. Pure: reads DBC, returns POCOs, formats
    /// nothing -- SpellWork.Mcp and the WinForms renderer both consume this so
    /// they cannot drift apart.
    /// </summary>
    public static class SpellVisualChainResolver
    {
        /// <param name="difficulty">
        /// Difficulty id to filter SpellXSpellVisual variants by. Pass -1 for
        /// every difficulty -- 0 is a real difficulty id, so it cannot double
        /// as "all".
        /// </param>
        /// <param name="maxKits">
        /// Ceiling on kits across the whole response. A spell with many
        /// difficulty variants fans out fast, and 264051 kit-effect rows sit
        /// downstream of this walk.
        /// </param>
        public static SpellVisualChain Resolve(int spellId, int difficulty = 0, int maxKits = 500)
        {
            var chain = new SpellVisualChain { SpellID = spellId };
            var variants = new List<SpellVisualVariant>();
            var unresolved = new List<string>();
            var kitBudget = maxKits;

            var rows = DBC.SpellXSpellVisual.Values
                .Where(x => x.SpellID == spellId)
                .Where(x => difficulty < 0 || x.DifficultyID == difficulty)
                .OrderBy(x => x.DifficultyID)
                .ThenByDescending(x => x.Priority);

            foreach (var row in rows)
            {
                var variant = new SpellVisualVariant
                {
                    SpellVisualID = (int)row.SpellVisualID,
                    DifficultyID = row.DifficultyID,
                    Probability = row.Probability,
                    Priority = row.Priority,
                };

                if (!DBC.SpellVisual.TryGetValue((int)row.SpellVisualID, out var visual))
                {
                    // Reported, not silently dropped: an id that does not
                    // resolve is a fact about the data the caller needs, and
                    // omitting it looks identical to the spell having no visual.
                    variant.VisualResolved = false;
                    unresolved.Add($"SpellVisual {row.SpellVisualID}");
                    variants.Add(variant);
                    continue;
                }

                variant.VisualResolved = true;
                variant.Missile = ResolveMissile(visual, unresolved);
                variant.Kits = GatherKits(visual, ref kitBudget, out var truncated, unresolved);
                if (truncated)
                    chain.Truncated = true;

                variants.Add(variant);
            }

            chain.Variants = variants;
            chain.UnresolvedReferences = unresolved;
            return chain;
        }
    }
}
```

Implement `GatherKits` to collect kit ids from all three directions into an
ordered set — `SpellVisual`'s own kit fields first (`ReachedVia = "visual"`),
then kits on `SpellVisualEvent` rows whose parent is this visual
(`"event"`), then kits on `SpellVisualMissile` rows for this visual's
`SpellVisualMissileSetID` (`"missile"`) — skipping ids already in the set,
decrementing `kitBudget` per **newly added** kit, and setting `truncated` when
the budget reaches zero with candidates left. Implement `ResolveMissile` to
join `SpellMissile` and `SpellMissileMotion`, appending to `unresolved` for
either miss.

Use the actual kit-bearing field names your Task 2 and Task 3 lookups produced.
**Do not guess them from this plan** — the design deliberately says the exact
set of kit-bearing fields per table is confirmed from the layout before the
resolver is written.

- [ ] **Step 5: Run the tests**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~SpellVisualChainResolver"`
Expected: PASS.

- [ ] **Step 6: Full verification and commit**

```bash
git add SpellWork.Core/Spell/SpellVisualChain.cs SpellWork.Core/Spell/SpellVisualChainResolver.cs SpellWork.Core.Tests/SpellVisualChainResolverTests.cs
git commit -m "feat: resolve the spell visual chain"
```

---

## Task 6: The `get_spell_visual_chain` tool

The fourteenth tool. `[McpServerTool]` auto-discovery means `Program.cs` needs
no edit — a new `[McpServerToolType]` class is picked up by
`WithToolsFromAssembly()`.

**Files:**
- Create: `SpellWork.Mcp/Tools/VisualTools.cs`
- Modify: `SpellWork.Mcp.Tests/StdioSmokeTests.cs` (`ExpectedTools` 13 → 14), `SpellWork.Mcp/SpellDataService.cs` (layout mismatches in `server_info`)
- Test: `SpellWork.Mcp.Tests/VisualToolsTests.cs`

**Interfaces:**
- Consumes: `SpellVisualChainResolver.Resolve` (Task 5), `ToolResponse.Ok`/`.Error`/`.MaxRows`, `SpellDataService`.
- Produces: MCP tool `get_spell_visual_chain`.

- [ ] **Step 1: Write the failing tests**

Create `SpellWork.Mcp.Tests/VisualToolsTests.cs`, following the shape of the
existing tool tests (`SpellToolsTests`), asserting:

```csharp
        [SkippableFact]
        public void GetSpellVisualChain_Fireball_ReturnsResolvedVisual()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(new VisualTools(_fixture.LoadedService).GetSpellVisualChain(133).Result);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            var variants = doc.RootElement.GetProperty("variants");
            Assert.NotEqual(0, variants.GetArrayLength());
        }

        [SkippableFact]
        public void GetSpellVisualChain_UnknownSpell_ReportsSpellNotFound()
        {
            _fixture.SkipIfNoClientData();

            using var doc = JsonDocument.Parse(new VisualTools(_fixture.LoadedService).GetSpellVisualChain(999999999).Result);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("spell_not_found", doc.RootElement.GetProperty("error").GetString());
        }

        // A store this tool needs that did not load must say so, not return
        // an empty chain that reads as "this spell has no visuals".
        [SkippableFact]
        public void GetSpellVisualChain_ReportsUnloadedStores()
        {
            _fixture.SkipIfNoClientData();
            Skip.If(DBC.SpellVisual != null, "SpellVisual loaded; this branch needs it absent.");

            using var doc = JsonDocument.Parse(new VisualTools(_fixture.LoadedService).GetSpellVisualChain(133).Result);
            Assert.Equal("store_not_loaded", doc.RootElement.GetProperty("error").GetString());
        }
```

Also update `ExpectedTools` in `SpellWork.Mcp.Tests/StdioSmokeTests.cs` to
include `"get_spell_visual_chain"`, making it fourteen. The existing
`ToolsList_ContainsEveryToolExactlyOnce` then fails until the tool exists,
which is the point.

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~VisualTools|FullyQualifiedName~StdioSmoke"`
Expected: FAIL — `VisualTools` does not exist and the tool count is 13.

- [ ] **Step 3: Write the tool**

Create `SpellWork.Mcp/Tools/VisualTools.cs`:

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.DBC;
using SpellWork.Spell;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class VisualTools
    {
        private readonly SpellDataService _data;

        public VisualTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "get_spell_visual_chain")]
        [Description("The resolved visual chain for one spell: per-difficulty variants from SpellXSpellVisual, the missile model and motion, and the visual kits with their effects. Kits are reached from the visual itself, from its events and from its missiles, and are deduplicated across all three; each kit reports which direction found it. Pass difficulty -1 for every difficulty. Capped at 500 kits.")]
        public Task<string> GetSpellVisualChain(int id, int difficulty = 0)
        {
            if (DBC.SpellVisual == null || DBC.SpellVisualKit == null)
            {
                return Task.FromResult(ToolResponse.Error("store_not_loaded",
                    "SpellVisual.db2 or SpellVisualKit.db2 did not load, so no visual chain can be " +
                    "resolved. Call server_info to see which stores are missing."));
            }

            if (!DBC.SpellInfoStore.ContainsKey(id))
                return Task.FromResult(ToolResponse.Error("spell_not_found", $"No spell with id {id}."));

            var chain = SpellVisualChainResolver.Resolve(id, difficulty, ToolResponse.MaxRows);

            return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
            {
                ["id"] = chain.SpellID,
                ["difficulty"] = difficulty,
                ["variants"] = chain.Variants.Select(Describe).ToList(),
                ["truncated"] = chain.Truncated,
                ["maxKits"] = ToolResponse.MaxRows,
                ["unresolvedReferences"] = chain.UnresolvedReferences,
            }));
        }
    }
}
```

Write the private `Describe(SpellVisualVariant)` projection to plain
dictionaries. Emit `unresolvedReferences` **only when non-empty**, matching
`AddIfNotEmpty` elsewhere in the codebase.

- [ ] **Step 4: Run the tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`
Expected: PASS, with `ToolsList_ContainsEveryToolExactlyOnce` now seeing 14.

- [ ] **Step 5: Surface layout mismatches in `server_info`**

Add a `layoutMismatches` key to the `server_info` payload, carrying
`DBC.LayoutMismatches.Select(m => m.Describe())`, emitted only when non-empty.
An agent seeing garbled visual data needs one place that says the client build
does not match. Add an assertion that the key is absent on the reference build.

- [ ] **Step 6: Full verification and commit**

```bash
git add SpellWork.Mcp/Tools/VisualTools.cs SpellWork.Mcp/SpellDataService.cs SpellWork.Mcp.Tests/
git commit -m "feat: add get_spell_visual_chain, the fourteenth tool"
```

---

## Task 7: `get_spell` section, GUI render, and documentation

Closes the two loops Plan 2 deliberately left open: the `"visual"` section that
`SpellInfoJsonSerializer` reserves but does not implement, and
`AppendSpellVisualInfo()`, whose body has been commented out since before the
Legion table split.

**Files:**
- Modify: `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs` (`ValidSections` at `:59-64`), `SpellWork.Core/Spell/SpellInfo.cs` (`:1001`), `docs/mcp-server.md`
- Test: `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`, `SpellWork.Core.Tests/GoldenRenderingTests.cs`

**Interfaces:**
- Consumes: `SpellVisualChainResolver.Resolve` (Task 5).
- Produces: nothing later tasks depend on — this plan ends here.

- [ ] **Step 1: Write the failing serializer test**

Append to `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`:

```csharp
        [SkippableFact]
        public void Serialize_VisualSection_IsAValidSectionAndCarriesData()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.SpellInfoStore[133];
            using var doc = JsonDocument.Parse(
                SpellInfoJsonSerializer.Serialize(spell, RenderContext.FromGlobals(), new[] { "visual" }));

            Assert.True(doc.RootElement.TryGetProperty("visual", out var visual),
                "The visual section validated but produced no key.");
            Assert.NotEqual(0, visual.GetProperty("variants").GetArrayLength());
        }

        // Guards the trap this codebase already hit once: a section that
        // validates, echoes back in `sections`, and silently drops its key
        // reads as "the spell genuinely has none".
        [SkippableFact]
        public void Serialize_VisualSection_WhenStoreUnloaded_DoesNotClaimTheSpellHasNoVisual()
        {
            _fixture.SkipIfNoClientData();
            Skip.If(DBC.SpellVisual != null, "SpellVisual loaded; this branch needs it absent.");

            var spell = DBC.SpellInfoStore[133];
            using var doc = JsonDocument.Parse(
                SpellInfoJsonSerializer.Serialize(spell, RenderContext.FromGlobals(), new[] { "visual" }));

            Assert.True(doc.RootElement.TryGetProperty("visual", out var visual));
            Assert.True(visual.GetProperty("storeUnloaded").GetBoolean());
        }
```

- [ ] **Step 2: Run it and watch it fail**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~SpellInfoJsonSerializer"`
Expected: FAIL — `"visual"` is rejected as an unknown section.

- [ ] **Step 3: Implement the section**

Add `"visual"` to `ValidSections` and replace the comment at `:53-58` that
explains its absence with one explaining what it now carries. Add:

```csharp
            if (Wants("visual"))
                root["visual"] = BuildVisual(spell);
```

`BuildVisual` returns `{ "storeUnloaded": true }` when `DBC.SpellVisual` is
null, and otherwise the same projection `VisualTools` uses. Assign it
unconditionally — **not** through `AddIfNotEmpty` — so an unloaded store can
never present as "this spell has no visual".

- [ ] **Step 4: Restore the GUI render**

Replace the commented-out body of `AppendSpellVisualInfo()` at
`SpellWork.Core/Spell/SpellInfo.cs:1001` with a real implementation. It is
called at `:638` and currently renders nothing.

**`RenderContext` has no `Difficulty` property** — it exposes `Level`,
`ItemLevel` and `MapDifficulty` (a `MapDifficultyEntry`, which is null when no
map difficulty is selected). Take the difficulty id off `MapDifficulty` when it
is non-null and fall back to `0` otherwise:

```csharp
            var difficulty = context.MapDifficulty?.DifficultyID ?? 0;
            var chain = SpellVisualChainResolver.Resolve(Spell.ID, difficulty);
```

Confirm the member name on `MapDifficultyEntry` before writing this — read the
structure rather than trusting the line above.

Give it the signature the neighbouring appenders use —
`private void AppendSpellVisualInfo(ISpellInfoWriter rtb, RenderContext context)`
— and update the call site. Print the visual id, the missile model and motion
name, and each kit with its effects, following the existing
`rtb.AppendFormatLine` idiom. Return without writing anything when there are no
variants, so spells without visuals render exactly as before.

- [ ] **Step 5: Re-capture the goldens in this same commit**

This changes render output, so the golden files move. Re-capture them, and
**read the diff before accepting it** — confirm every changed line is a visual
line you added, and that no unrelated line moved. Report the number of golden
files that changed and paste one representative diff hunk.

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~GoldenRendering"`
Expected: PASS after re-capture.

- [ ] **Step 6: Document the tool**

In `docs/mcp-server.md`:
- Change "Thirteen tools" to "Fourteen tools".
- Add a **Visual** section to the tool table: `get_spell_visual_chain` — "The resolved visual chain for one spell: variants, missile, and deduplicated kits with their effects."
- Add `visual` to the documented `sections` list for `get_spell`.
- Under **Known limitations**, replace "Spell visual chains are not yet available. That is a separate piece of work." with a note that the chain resolves against the layouts recorded in `LayoutVerification`, and that `server_info` reports `layoutMismatches` when the client build differs.
- Update the modelled-store count from the measured `list_db2` output — nine new stores land in this plan, so the number in the doc is now wrong. **Measure it, do not add nine to the old figure.**

- [ ] **Step 7: Full verification and commit**

```bash
git add SpellWork.Core/Spell/ SpellWork.Core.Tests/ docs/mcp-server.md
git commit -m "feat: add the visual section to get_spell and restore the GUI visual render"
```

---

## Self-review against the spec

**Spec coverage.** §7's chain diagram — `SpellXSpellVisual` (existing) →
`SpellVisual` (Task 2) → `SpellVisualEvent`/`SpellVisualMissile` (Task 2) →
`SpellVisualKit` (Task 3) → `SpellVisualKitEffect` → `SpellVisualEffectName` /
`SpellVisualKitModelAttach` (Task 3) / `SpellChainEffects` (Task 4). Kits
gathered as a set from three directions and deduplicated: Task 5.
`SpellMissile` given a consumer and `SpellMissileMotion` wired in: Tasks 4-5.
`get_spell_visual_chain(id, difficulty=0)` returning visual id, per-difficulty
variants, missile model and motion, and kit effects: Task 6. §7.1 control 1
(layout hash) is Task 1 and is applied in every structure task; control 2
(emulator cross-check) applies to the only two tables the emulator carries,
`SpellVisualKit` and `SpellXSpellVisual` — **the other nine have no emulator
counterpart**, which is why control 3 (referential integrity, Tasks 2-4) is
written as a hard gate rather than a nicety. §6's `visual` section: Task 7.

**Two places this plan knowingly departs from the spec.** §7.1 calls layout
hash "the primary control … it must pass before a structure is considered
correct"; Task 1 records mismatches instead of throwing, because throwing
inside the shared load loop would abort the GUI over a table the caller may not
want — the "must pass" is enforced as a test assertion rather than a runtime
exception, which is strictly stronger during development and strictly safer in
production. And §7 lists the chain as reaching `SpellVisualEffectName` via
`SpellVisualKitEffect`; the field names that carry those references are
confirmed from the layouts in Tasks 2-3 rather than assumed here, as §7 itself
requires.

**Placeholder scan.** No TBDs. Three places deliberately defer to a lookup
rather than stating a field list: the non-`SpellVisual`/`SpellVisualKit` field
orders (Task 2 Step 1, Task 3 Step 2, Task 4 Step 1). That is not a placeholder
— transcribing nine field orders from memory into a plan is precisely the
silent-garbage failure §7.1 exists to prevent, so each carries the measured
layout hash and field count as a machine-checkable acceptance criterion, plus a
test that fails if the lookup was wrong.

**Type consistency.** `SpellVisualChainResolver.Resolve(int, int, int)` is used
with the same signature in Tasks 5, 6 and 7. `LayoutVerification.Check(string,
uint, int)` returns `LayoutMismatch` in Tasks 1-4. `DBC.LayoutMismatches` is
`IReadOnlyList<LayoutMismatch>` in Tasks 1, 4 and 6. Store property names match
their `Storage<T>` types throughout. `SpellXSpellVisualEntry.SpellVisualID` is
`uint` in the existing structure and is cast to `int` at every use in Task 5.

## Known risk this plan does not close

`SpellVisual.MissileCastOffset`/`MissileImpactOffset` are 32-bit 3-element
vectors. If they are `int` where they should be `float`, the layout hash
matches, the field count matches, and the referential-integrity tests all pass —
they are not references. Task 2 Step 7 makes an implementer look at the decoded
values, which is a human check standing where the automated controls do not
reach. It is called out here so a reviewer knows it is a deliberate residual
rather than an oversight.
