# SpellWork MCP Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `SpellWork.Mcp`, a read-only MCP stdio server exposing thirteen tools that let an agent query WoW 8.3.7 spell data, DB2 stores and a TrinityCore world database.

**Architecture:** A net9.0 console host (`ModelContextProtocol` C# SDK, `Host.CreateApplicationBuilder`) references the existing netstandard2.0 `SpellWork.Core` extracted in Plan 1. `SpellDataService` loads client data once behind a `Lazy<Task<SpellData>>`; every tool awaits it and returns a JSON string. Before any tool ships, render context (`Level`, `ItemLevel`, `MapDifficulty`) is threaded through `SpellInfo` as an explicit parameter so concurrent requests at different levels cannot corrupt each other through process-wide statics.

**Tech Stack:** C# / .NET 9 (server) over netstandard2.0 (core), `ModelContextProtocol` SDK, `Microsoft.Extensions.Hosting`, `System.Text.Json`, `MySql.Data` 8.0.33, xUnit + `Xunit.SkippableFact`.

**Source spec:** `docs/superpowers/specs/2026-08-01-spellwork-mcp-design.md`
**Prior-work record:** `docs/superpowers/2026-08-01-core-extraction-handoff.md` — read the "Blocking Plan 2" and "Plan gap" sections; Tasks 1 and 2 exist because of them.

## Global Constraints

- **Read-only. There is no write path.** No tool may call `WorldDatabase.Insert`, write a `.db2`, or issue anything but `SELECT` to MySQL. Spec §11: "Any write path. Explicitly excluded, permanently."
- **Nothing but JSON-RPC is ever written to stdout.** All logging goes to stderr (spec §5.4, §9). Every `Console.Write*` in the MCP project is a defect; logging is configured with `o.LogToStandardErrorThreshold = LogLevel.Trace`.
- **A misconfigured server must still start and still answer** (spec §9). Config errors never terminate the process; they surface as structured tool errors naming the offending path and the environment variable to fix.
- **Server-side result cap of 500 rows on every tool** (spec §6). Where a tool takes `limit`, the effective limit is `Math.Min(limit, 500)` and the response reports both the cap and whether it truncated.
- **`query_world_db` accepts a single `SELECT` statement only.** Multiple statements, DDL and DML keywords are rejected before the query reaches MySQL, and the documentation directs the operator to connect as a read-only MySQL user. Both controls are required: the parser is a guard, the read-only grant is the guarantee (spec §9).
- **`SpellWork.Core` stays `netstandard2.0`.** No C# language feature or API that fails to compile there. `SpellWork.Mcp` and both test projects are `net9.0`.
- **The ten golden files in `SpellWork.Core.Tests/Goldens/` must stay byte-identical.** They are the proof that GUI rendering did not change. No task in this plan updates a golden. If a change would alter rendered output, stop and report it rather than re-capturing.
- **Tests that need client data are gated on `SPELLWORK_DBC_PATH`** and skip with an explicit message when it is unset, using `Xunit.SkippableFact` (spec §10). They never fail for want of data.
- **Build commands.** Use `dotnet build SpellWork.Core.Tests/SpellWork.Core.Tests.csproj` / `dotnet test` for the SDK-style projects. The full solution — which contains the legacy non-SDK WinForms project — builds only with `"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" SpellWork.sln -restore`. **When a build's warning count is what you are checking, pass `-t:Rebuild`** — an incremental build skips the WinForms project and silently suppresses its warnings. This has produced a false "zero warnings" twice on this codebase.
- **Locale, paths and defaults are exactly:** `SPELLWORK_DBC_PATH` (required), `SPELLWORK_GT_PATH` (required), `SPELLWORK_LOCALE` (default `enUS`), `SPELLWORK_HOTFIX_CACHE` (optional), `SPELLWORK_MYSQL_HOST` / `_PORT` / `_USER` / `_PASS` / `_WORLD_DB` (all optional; MySQL tools disabled unless host, user and world-db are all set).
- **`get_spell_visual_chain` is out of scope for this plan.** It is Plan 3 (spec §7). This plan ships thirteen of the fourteen tools in spec §6.
- Commit after every task. Conventional-commit prefixes (`feat:`, `refactor:`, `test:`, `docs:`).

---

## File Structure

**Modified in `SpellWork.Core` (netstandard2.0):**

| File | Responsibility |
| --- | --- |
| `SpellWork.Core/Spell/RenderContext.cs` *(new)* | Immutable `{Level, ItemLevel, MapDifficulty}` triple. The only thing that decides what level a spell renders at. |
| `SpellWork.Core/Spell/SpellInfo.cs` | Takes `RenderContext` as a parameter instead of reading `DBC.Selected*`. |
| `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs` | Same; plus the spec §6 field-set expansion (Task 2). |
| `SpellWork.Core/Database/WorldDatabase.cs` | Gains a generic `SelectRows` reader for `query_world_db`. |

**New project `SpellWork.Mcp` (net9.0, console):**

| File | Responsibility |
| --- | --- |
| `Program.cs` | Host bootstrap, stderr logging, stdio transport, DI registration. |
| `McpServerConfig.cs` | Reads the nine environment variables; maps them onto `SpellWorkConfig`. |
| `SpellDataService.cs` | One-time client-data load behind `Lazy<Task<SpellData>>`; exposes `SpellData` (loaded flag, error, durations, store counts). |
| `ToolResponse.cs` | The `ok` / `error` JSON envelope every tool returns. |
| `SqlGuard.cs` | The SELECT-only validator. Security-relevant; no dependency on any database. |
| `Tools/DiagnosticTools.cs` | `server_info` |
| `Tools/SpellTools.cs` | `get_spell`, `search_spells`, `get_spell_triggers`, `compare_spells` |
| `Tools/DecoderTools.cs` | `decode_flags`, `list_enum`, `calculate_spell_scaling` |
| `Tools/Db2Tools.cs` | `list_db2`, `get_db2_row`, `query_db2` |
| `Tools/DatabaseTools.cs` | `query_world_db`, `get_spell_db_overrides` |

**New project `SpellWork.Mcp.Tests` (net9.0, xUnit):** unit tests for `SqlGuard`, `McpServerConfig`, `ToolResponse` and the decoder tools, plus the stdio smoke test. It references `SpellWork.Mcp`; `SpellWork.Core.Tests` must not, because that dependency runs backwards.

---

### Task 1: Thread `RenderContext` through `SpellInfo`

The handoff calls this "Blocking Plan 2": `DBC.SelectedLevel` / `SelectedItemLevel` / `SelectedMapDifficulty` are process-wide mutable statics read from inside the damage arithmetic. Any tool taking a `level` argument would have to mutate a global first, so two concurrent requests give silently wrong numbers to whichever loses the race — no exception, not reproducible from the response. Nothing else in this plan is safe to ship until this is done.

**Files:**
- Create: `SpellWork.Core/Spell/RenderContext.cs`
- Modify: `SpellWork.Core/Spell/SpellInfo.cs:227` (`Write`), `:233`, `:464`, `:613` (`CalculateBaseEffectValue`), `:620`, `:641`, `:678-679`, `:685`, `:698` (`CalculateEffectValues`), `:730` (`AppendEffectInfo`), `:764-769`
- Modify: `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs:58` (`Serialize`), `:106-126`, `:282` (`BuildEffects`), `:322`
- Modify: `SpellWork/Forms/FormMain.cs` (the `Write` call site)
- Test: `SpellWork.Core.Tests/RenderContextTests.cs` (new); existing golden tests updated for the new signature

**Interfaces:**
- Produces: `SpellWork.Spell.RenderContext` with `uint Level`, `uint ItemLevel`, `MapDifficultyEntry MapDifficulty`, constructor `RenderContext(uint level, uint itemLevel, MapDifficultyEntry mapDifficulty)`, and `static RenderContext FromGlobals()`.
- Produces: `SpellInfo.Write(ISpellInfoWriter rtb, RenderContext context)`, `internal EffectValueBreakdown SpellInfo.CalculateEffectValues(SpellEffectEntry effect, RenderContext context)`, `internal float SpellInfo.CalculateBaseEffectValue(SpellEffectEntry effect, RenderContext context)`, `SpellInfoJsonSerializer.Serialize(SpellInfo spell, RenderContext context, IReadOnlyCollection<string> sections = null)`.
- Consumes: nothing from earlier tasks.

**Design note the implementer must follow.** `RenderContext` is a *required* parameter on all four methods — not optional with a null fallback to the statics. A defaulted parameter leaves the global read in the arithmetic, which is the bug. `FromGlobals()` exists so the GUI's call site stays a one-liner and is the *only* place in the codebase that reads `DBC.Selected*` for rendering. `DBC.SelectedLevel` and friends stay where they are as GUI-owned state written by `FormMain.cs:151-153`; this task stops Core from reading them, it does not delete them.

- [ ] **Step 1: Write the failing test**

Create `SpellWork.Core.Tests/RenderContextTests.cs`:

```csharp
using SpellWork.DBC;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class RenderContextTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public RenderContextTests(ClientDataFixture fixture) => _fixture = fixture;

        // Spell 606 "Mind Rot" takes the ExpectedStat branch of
        // CalculateBaseEffectValue, so its base value is a direct function of
        // the render context's level. If the context is not actually reaching
        // the arithmetic, both calls return the same number.
        [SkippableFact]
        public void CalculateBaseEffectValue_UsesContextLevel_NotGlobals()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[606];
            var effect = spell.Effects[0];

            var atLevel120 = spell.CalculateBaseEffectValue(effect, new RenderContext(120, 475, null));
            var atLevel20 = spell.CalculateBaseEffectValue(effect, new RenderContext(20, 475, null));

            Assert.NotEqual(atLevel120, atLevel20);
        }

        // The point of the whole task: two contexts alive at once must not
        // interfere. Interleave them and assert each still produces the value
        // it produces alone.
        [SkippableFact]
        public void ConcurrentContexts_DoNotInterfere()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[606];
            var effect = spell.Effects[0];
            var low = new RenderContext(20, 475, null);
            var high = new RenderContext(120, 475, null);

            var lowAlone = spell.CalculateBaseEffectValue(effect, low);
            var highAlone = spell.CalculateBaseEffectValue(effect, high);

            System.Threading.Tasks.Parallel.For(0, 200, i =>
            {
                var context = (i % 2 == 0) ? low : high;
                var expected = (i % 2 == 0) ? lowAlone : highAlone;
                Assert.Equal(expected, spell.CalculateBaseEffectValue(effect, context));
            });
        }

        // Guards the GUI: FromGlobals must snapshot exactly what the statics
        // hold, or the WinForms app renders at the wrong level.
        [SkippableFact]
        public void FromGlobals_SnapshotsTheStatics()
        {
            _fixture.SkipIfNoClientData();

            var context = RenderContext.FromGlobals();

            Assert.Equal(DBC.DBC.SelectedLevel, context.Level);
            Assert.Equal(DBC.DBC.SelectedItemLevel, context.ItemLevel);
            Assert.Same(DBC.DBC.SelectedMapDifficulty, context.MapDifficulty);
        }
    }
}
```

`CalculateBaseEffectValue` is `internal`; `SpellWork.Core.Tests` already sees it. If it does not, add `[assembly: InternalsVisibleTo("SpellWork.Core.Tests")]` — check `SpellWork.Core/Properties/AssemblyInfo.cs` or the existing `EffectValueBreakdown` tests for how it is already done rather than adding a second mechanism.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~RenderContextTests"`
Expected: FAIL to compile — `RenderContext` does not exist, and `CalculateBaseEffectValue` takes one argument.

- [ ] **Step 3: Create `RenderContext`**

Create `SpellWork.Core/Spell/RenderContext.cs`:

```csharp
using SpellWork.DBC.Structures;

namespace SpellWork.Spell
{
    /// <summary>
    /// The level, item level and map difficulty a spell is being rendered at.
    /// Passed explicitly rather than read from statics: the MCP server answers
    /// concurrent requests at different levels, and a shared mutable global
    /// would hand the loser of that race a silently wrong number.
    /// </summary>
    public sealed class RenderContext
    {
        public RenderContext(uint level, uint itemLevel, MapDifficultyEntry mapDifficulty)
        {
            Level = level;
            ItemLevel = itemLevel;
            MapDifficulty = mapDifficulty;
        }

        public uint Level { get; }
        public uint ItemLevel { get; }
        public MapDifficultyEntry MapDifficulty { get; }

        /// <summary>
        /// Snapshots the GUI's selection. This is the only supported read of
        /// <see cref="DBC.DBC.SelectedLevel"/> and friends: the WinForms app
        /// owns that state, everything else passes a context explicitly.
        /// </summary>
        public static RenderContext FromGlobals() =>
            new RenderContext(DBC.DBC.SelectedLevel, DBC.DBC.SelectedItemLevel, DBC.DBC.SelectedMapDifficulty);
    }
}
```

- [ ] **Step 4: Thread the context through `SpellInfo`**

In `SpellWork.Core/Spell/SpellInfo.cs`, change the four signatures and replace every static read with the parameter. The full set of edits:

| Line | Was | Becomes |
| --- | --- | --- |
| 227 | `public void Write(ISpellInfoWriter rtb)` | `public void Write(ISpellInfoWriter rtb, RenderContext context)` |
| 233 | `$" (Level {DBC.DBC.SelectedLevel})"` | `$" (Level {context.Level})"` |
| 464 | `var level = DBC.DBC.SelectedLevel;` | `var level = context.Level;` |
| 606 | `AppendEffectInfo(rtb, effect)` | `AppendEffectInfo(rtb, effect, context)` |
| 613 | `internal float CalculateBaseEffectValue(SpellEffectEntry effect)` | `internal float CalculateBaseEffectValue(SpellEffectEntry effect, RenderContext context)` |
| 620 | `var level = DBC.DBC.SelectedLevel - 1;` | `var level = context.Level - 1;` |
| 641 | `var effectiveItemLevel = (int)DBC.DBC.SelectedItemLevel;` | `var effectiveItemLevel = (int)context.ItemLevel;` |
| 678-679 | `if (DBC.DBC.SelectedMapDifficulty != null) contentTuningId = DBC.DBC.SelectedMapDifficulty.ContentTuningID;` | `if (context.MapDifficulty != null) contentTuningId = context.MapDifficulty.ContentTuningID;` |
| 685 | `ExpectedStat.Evaluate(stat, DBC.DBC.SelectedLevel, …)` | `ExpectedStat.Evaluate(stat, context.Level, …)` |
| 698 | `internal EffectValueBreakdown CalculateEffectValues(SpellEffectEntry effect)` | `internal EffectValueBreakdown CalculateEffectValues(SpellEffectEntry effect, RenderContext context)` |
| 700 | `var baseValue = CalculateBaseEffectValue(effect);` | `var baseValue = CalculateBaseEffectValue(effect, context);` |
| 730 | `private void AppendEffectInfo(ISpellInfoWriter rtb, SpellEffectEntry effect)` | `private void AppendEffectInfo(ISpellInfoWriter rtb, SpellEffectEntry effect, RenderContext context)` |
| ~740 | the `CalculateEffectValues(effect)` call inside `AppendEffectInfo` | `CalculateEffectValues(effect, context)` |
| 764-769 | the three `DBC.DBC.SelectedMapDifficulty` reads in the `MediumPurple` branch | `context.MapDifficulty` |

The 764-769 block becomes:

```csharp
            if (values.UsesExpectedStat && context.MapDifficulty != null)
            {
                rtb.SetColor(TextColor.MediumPurple);
                rtb.AppendFormat(" ({2} - {0}: {1})", DBC.DBC.Map[(int)context.MapDifficulty.MapID].MapName,
                    DBC.DBC.Difficulty[context.MapDifficulty.DifficultyID].Name,
                    context.MapDifficulty.MapID);
            }
```

If any other method in this file calls `CalculateBaseEffectValue`, `CalculateEffectValues` or `AppendEffectInfo`, it needs the parameter too. Find them all with `grep -n "CalculateBaseEffectValue\|CalculateEffectValues\|AppendEffectInfo" SpellWork.Core/Spell/SpellInfo.cs` before you start — do not rely on this table being exhaustive for call sites, only for the static reads.

- [ ] **Step 5: Thread the context through the JSON serializer**

In `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs`:

```csharp
        public static string Serialize(SpellInfo spell, RenderContext context, IReadOnlyCollection<string> sections = null)
```

Add a null check beside the existing one:

```csharp
            if (context == null)
                throw new ArgumentNullException(nameof(context));
```

Replace the `context` section body at `:106-126` (note: the JSON section is *named* `"context"` and the parameter is also called `context` — the section name string is unchanged, only the values it reads):

```csharp
            if (Wants("context"))
            {
                root["level"] = context.Level;
                root["itemLevel"] = context.ItemLevel;

                var mapDifficulty = context.MapDifficulty;
                if (mapDifficulty != null)
                {
                    root["mapDifficulty"] = new Dictionary<string, object>
                    {
                        ["id"] = mapDifficulty.ID,
                        ["contentTuningId"] = mapDifficulty.ContentTuningID,
                    };
                }
            }
```

Change `BuildEffects(SpellInfo spell)` to `BuildEffects(SpellInfo spell, RenderContext context)`, its call site at `:138` to `BuildEffects(spell, context)`, and `:322` to `var values = spell.CalculateEffectValues(effect, context);`.

The comment block at `:98-105` says "computed against `DBC.DBC.SelectedLevel`" — update it to say "computed against the supplied `RenderContext`". A comment naming a mechanism that no longer exists is worse than no comment.

- [ ] **Step 6: Update the GUI call site**

In `SpellWork/Forms/FormMain.cs`, find the `.Write(` call on a `SpellInfo` and pass a snapshot:

```csharp
            spell.Write(_richTextBoxWriter, RenderContext.FromGlobals());
```

Use whatever the local writer variable is actually called — `grep -n "\.Write(" SpellWork/Forms/FormMain.cs` to find it. `FormMain.cs:151-153` still writes the statics from the scaling dialog; leave that alone. `FromGlobals()` is called at render time, so the dialog's values reach the render exactly as before.

- [ ] **Step 7: Update the existing tests for the new signatures**

Every existing call to `Write(...)` or `Serialize(...)` in `SpellWork.Core.Tests` needs the new argument. Pass `RenderContext.FromGlobals()` in the golden tests: the goldens were captured with the statics at their defaults, and `FromGlobals()` reproduces exactly that, so the golden bytes are unchanged. Find them with `grep -rn "\.Write(\|Serialize(" SpellWork.Core.Tests/`.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`
Expected: PASS — the three new tests plus all 44 existing ones. **The golden tests passing unchanged is the deliverable of this task**; if a golden fails, the context is not reproducing the previous behaviour and something in the table above was transcribed wrong. Do not update the golden.

- [ ] **Step 9: Verify the GUI still builds clean**

Run: `"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" SpellWork.sln -restore -t:Rebuild -v:minimal`
Expected: zero warning lines, zero error lines. `-t:Rebuild` is not optional here — an incremental build will not recompile the WinForms project and will not tell you the call site is wrong.

- [ ] **Step 10: Confirm Core no longer reads the statics**

Run: `grep -rn "SelectedLevel\|SelectedItemLevel\|SelectedMapDifficulty" SpellWork.Core/`
Expected: exactly two files — `DBC/DBC.cs` (the declarations, around `:431-433`) and `Spell/RenderContext.cs` (`FromGlobals`). Any hit in `SpellInfo.cs` or `SpellInfoJsonSerializer.cs` is a missed edit.

- [ ] **Step 11: Commit**

```bash
git add SpellWork.Core/Spell/RenderContext.cs SpellWork.Core/Spell/SpellInfo.cs SpellWork.Core/Spell/SpellInfoJsonSerializer.cs SpellWork/Forms/FormMain.cs SpellWork.Core.Tests/
git commit -m "refactor: thread RenderContext through SpellInfo instead of reading statics"
```

---

### Task 2: Expand the JSON field set to satisfy spec §6

The handoff records this as a gap in Plan 1, not a bug in its code: Task 9's field list was written without checking it against the tool surface, so `search_spells(family?)` has nothing to filter on and `get_spell_triggers` has no `effectTriggerSpell`. Also absent: `effectMiscValue`, `effectAmplitude`, mechanic, interrupt flags, stances, cooldowns, levels, equipped-item requirements. Every one of these is needed by a later task, so it is cheaper to add them all here than to discover them one at a time.

**Files:**
- Modify: `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs` — `BuildMisc` (`:161`), `BuildEffects` (`:282`), plus new builders
- Test: `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs` (existing file — add cases)

**Interfaces:**
- Consumes: `RenderContext` and the Task 1 `Serialize(SpellInfo, RenderContext, IReadOnlyCollection<string>)` signature.
- Produces: these JSON keys, which later tasks read by name —
  - root: `spellFamilyName` (`{raw, name}`), `spellClassMask` (`uint[4]`)
  - `misc`: `mechanic` (`{raw, name}`), `startRecoveryCategory`
  - new root section `cooldowns`: `recoveryTime`, `categoryRecoveryTime`, `startRecoveryTime`
  - new root section `levels`: `baseLevel`, `spellLevel`, `maxLevel`, `maxPassiveAuraLevel`
  - new root section `interrupts`: `interruptFlags` (`{raw, flags}`), `auraInterruptFlags` (`{raw, flags}`), `channelInterruptFlags` (`{raw, flags}`)
  - new root section `shapeshift`: `stances`, `stancesNot` (both `long`)
  - new root section `equippedItems`: `equippedItemClass` (`{raw, name}`), `equippedItemSubClassMask`, `equippedItemInventoryTypeMask`
  - `effects[]`: `triggerSpell` (`{id, name}`), `miscValue`, `miscValueB`, `amplitude`, `mechanic` (`{raw, name}`), `spellClassMask` (`uint[4]`)
- Produces: `ValidSections` grows from ten to seventeen — `misc, attributes, effects, powers, targets, reagents, aura, procs, scaling, context, cooldowns, levels, interrupts, shapeshift, equippedItems, items, triggers`. The last two are populated in Task 5; declare all seventeen here so the validator accepts them from the start.

**Design note.** `items` and `triggers` are in the spec's `sections` list for `get_spell`. `visual` is also in that list and belongs to Plan 3 — **do not add `visual` to `ValidSections`**; a section name that validates but never produces data is worse than a clear "unknown section" error. Task 5 fills `items` and `triggers`; this task only reserves their names.

Follow the file's existing conventions exactly: `AddIfNonZero` / `AddIfNotEmpty` for anything whose zero is genuinely "absent", direct assignment for anything whose zero is a real value, `BuildRawAndName` for single-valued enums, `BuildRawAndFlags` for bitmasks. Read `BuildMisc`'s comment at `:181-186` before deciding which a new field gets — that decision is already documented there and this task must not contradict it.

- [ ] **Step 1: Write the failing test**

Add to `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`:

```csharp
        // Fireball (133) is a Mage spell: SpellFamilyName 3 (SPELLFAMILY_MAGE)
        // with a non-empty class mask. search_spells(family:) filters on this
        // and had nothing to read before.
        [SkippableFact]
        public void Serialize_EmitsSpellFamilyNameAndClassMask()
        {
            _fixture.SkipIfNoClientData();

            var json = SpellInfoJsonSerializer.Serialize(
                DBC.DBC.SpellInfoStore[133], RenderContext.FromGlobals());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(3, root.GetProperty("spellFamilyName").GetProperty("raw").GetInt32());
            Assert.Equal("SPELLFAMILY_MAGE", root.GetProperty("spellFamilyName").GetProperty("name").GetString());
            Assert.Equal(4, root.GetProperty("spellClassMask").GetArrayLength());
        }

        // Fireball's effect 0 triggers nothing, but spell 20154 (Seal of
        // Righteousness) has an effect with a non-zero EffectTriggerSpell.
        // get_spell_triggers reads this key. Assert against whatever spell in
        // the store actually has one rather than hard-coding a guess: find it,
        // then assert the key is present and resolves to a real spell name.
        [SkippableFact]
        public void Serialize_EmitsEffectTriggerSpell()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Any(e => e != null && e.EffectTriggerSpell != 0
                                   && DBC.DBC.SpellInfoStore.ContainsKey(e.EffectTriggerSpell)));

            var json = SpellInfoJsonSerializer.Serialize(spell, RenderContext.FromGlobals());
            using var doc = JsonDocument.Parse(json);

            var triggered = doc.RootElement.GetProperty("effects").EnumerateArray()
                .First(e => e.TryGetProperty("triggerSpell", out _))
                .GetProperty("triggerSpell");

            Assert.True(triggered.GetProperty("id").GetInt32() != 0);
            Assert.False(string.IsNullOrEmpty(triggered.GetProperty("name").GetString()));
        }

        // The six new sections must each be filterable, and an unknown one
        // must still be rejected. This is the contract get_spell's `sections`
        // argument depends on.
        [SkippableFact]
        public void Serialize_AcceptsNewSections_AndStillRejectsUnknown()
        {
            _fixture.SkipIfNoClientData();

            var spell = DBC.DBC.SpellInfoStore[133];
            var context = RenderContext.FromGlobals();

            foreach (var section in new[] { "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems", "items", "triggers" })
                SpellInfoJsonSerializer.Serialize(spell, context, new[] { section });

            Assert.Throws<ArgumentException>(() =>
                SpellInfoJsonSerializer.Serialize(spell, context, new[] { "visual" }));
        }
```

Add `using System.Linq;` and `using System.Text.Json;` if the file does not already have them.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "FullyQualifiedName~SpellInfoJsonSerializerTests"`
Expected: FAIL — `spellFamilyName` is not present, and the new section names throw `ArgumentException`.

- [ ] **Step 3: Grow `ValidSections`**

```csharp
        private static readonly string[] ValidSections =
        {
            "misc", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs", "scaling",
            "context", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems",
            // Populated in Task 5. Declared here so the section validator and
            // get_spell's documented `sections` list agree from the start.
            "items", "triggers",
        };
```

The comment at `:66-68` says "one of the ten section names" — update the count.

- [ ] **Step 4: Add the root-level family fields**

In `Serialize`, after the `misc` block:

```csharp
            // Emitted at the root rather than inside "misc": search_spells
            // filters on family, and burying the filter key one level deep
            // makes every caller destructure to reach it. Both are emitted
            // unconditionally -- SPELLFAMILY_GENERIC is 0 and is a real
            // family, not an absent one.
            if (Wants("misc"))
            {
                root["spellFamilyName"] = BuildRawAndName(spell.SpellFamilyName, typeof(SpellFamilyNames));
                root["spellClassMask"] = spell.SpellClassMask;
            }
```

Confirm the enum is named `SpellFamilyNames` in `SpellEnums.cs` — `ProcInfo.BuildFamilyTree(SpellFamilyNames)` already takes it, so it exists; check the exact spelling before compiling.

- [ ] **Step 5: Add `mechanic` and `startRecoveryCategory` to `BuildMisc`**

Inside `BuildMisc`, alongside the existing `category`:

```csharp
            AddIfNonZero(misc, "startRecoveryCategory", spell.StartRecoveryCategory);

            // MECHANIC_NONE is 0 and meaningful -- same reasoning as
            // damageClass/dispel above, so this is emitted unconditionally
            // rather than through AddIfNonZero.
            misc["mechanic"] = BuildRawAndName(spell.Mechanic, typeof(Mechanics));
```

Check the mechanic enum's real name in `SpellEnums.cs` (`grep -n "enum Mechanics\|enum Mechanic" SpellWork.Core/Spell/SpellEnums.cs`) and use it verbatim.

- [ ] **Step 6: Add the five new sections**

In `Serialize`, after the `scaling` block:

```csharp
            if (Wants("cooldowns"))
                AddIfNotEmpty(root, "cooldowns", BuildCooldowns(spell));

            if (Wants("levels"))
                AddIfNotEmpty(root, "levels", BuildLevels(spell));

            if (Wants("interrupts"))
                AddIfNotEmpty(root, "interrupts", BuildInterrupts(spell));

            if (Wants("shapeshift"))
                AddIfNotEmpty(root, "shapeshift", BuildShapeshift(spell));

            if (Wants("equippedItems"))
                AddIfNotEmpty(root, "equippedItems", BuildEquippedItems(spell));
```

And the five builders:

```csharp
        private static Dictionary<string, object> BuildCooldowns(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            AddIfNonZero(result, "recoveryTime", spell.RecoveryTime);
            AddIfNonZero(result, "categoryRecoveryTime", spell.CategoryRecoveryTime);
            AddIfNonZero(result, "startRecoveryTime", spell.StartRecoveryTime);
            return result;
        }

        private static Dictionary<string, object> BuildLevels(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            if (spell.Levels == null)
                return result;

            AddIfNonZero(result, "baseLevel", spell.Levels.BaseLevel);
            AddIfNonZero(result, "spellLevel", spell.Levels.SpellLevel);
            AddIfNonZero(result, "maxLevel", spell.Levels.MaxLevel);
            AddIfNonZero(result, "maxPassiveAuraLevel", spell.Levels.MaxPassiveAuraLevel);
            return result;
        }

        private static Dictionary<string, object> BuildInterrupts(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            if (spell.Interrupts == null)
                return result;

            if (spell.InterruptFlags != 0)
                result["interruptFlags"] = BuildRawAndFlags((uint)spell.InterruptFlags, typeof(SpellInterruptFlags));
            if (spell.AuraInterruptFlags != 0)
                result["auraInterruptFlags"] = BuildRawAndFlags((uint)spell.AuraInterruptFlags, typeof(SpellAuraInterruptFlags));
            if (spell.ChannelInterruptFlags != 0)
                result["channelInterruptFlags"] = BuildRawAndFlags((uint)spell.ChannelInterruptFlags, typeof(SpellChannelInterruptFlags));
            return result;
        }

        private static Dictionary<string, object> BuildShapeshift(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            AddIfNonZero(result, "stances", spell.Stances);
            AddIfNonZero(result, "stancesNot", spell.StancesNot);
            return result;
        }

        private static Dictionary<string, object> BuildEquippedItems(SpellInfo spell)
        {
            var result = new Dictionary<string, object>();
            if (spell.EquippedItems == null)
                return result;

            AddIfNonZero(result, "equippedItemSubClassMask", spell.EquippedItems.EquippedItemSubclass);
            AddIfNonZero(result, "equippedItemInventoryTypeMask", spell.EquippedItems.EquippedItemInvTypes);
            if (spell.EquippedItems.EquippedItemClass >= 0)
                result["equippedItemClass"] = BuildRawAndName((uint)spell.EquippedItems.EquippedItemClass, typeof(ItemClass));
            return result;
        }
```

Three things to verify against the actual source before compiling, because these member names are the likeliest transcription error in this task:
1. `SpellInfo`'s interrupt/level properties — `grep -n "InterruptFlags\|AuraInterruptFlags\|ChannelInterruptFlags\|MaxPassiveAuraLevel" SpellWork.Core/Spell/SpellInfo.cs`. If a property does not exist, read the field off `spell.Interrupts` / `spell.Levels` directly instead of adding a new property.
2. `AddIfNonZero` has overloads for `int`, `uint`, `short`, `byte`, `float` — **not `long`**. `Stances` and `StancesNot` are `long`. Add a `long` overload beside the others, mirroring them exactly:
   ```csharp
        private static void AddIfNonZero(IDictionary<string, object> dict, string key, long value)
        {
            if (value != 0)
                dict[key] = value;
        }
   ```
3. The enum names `SpellInterruptFlags`, `SpellAuraInterruptFlags` and `ItemClass` — confirm each in `SpellEnums.cs`. If `ItemClass` does not exist, emit `equippedItemClass` as a bare int rather than inventing an enum.

- [ ] **Step 7: Add the six new effect fields**

In `BuildEffects`, after the `chainTargets` line:

```csharp
                // get_spell_triggers reads this. Resolve the name here rather
                // than leaving the caller a bare id: an id that resolves to
                // nothing is a data problem the caller should see, and it is
                // only visible if we tried.
                if (effect.EffectTriggerSpell != 0)
                {
                    entry["triggerSpell"] = new Dictionary<string, object>
                    {
                        ["id"] = effect.EffectTriggerSpell,
                        ["name"] = DBC.DBC.SpellInfoStore.TryGetValue(effect.EffectTriggerSpell, out var triggered)
                            ? triggered.Name
                            : null,
                    };
                }

                AddIfNonZero(entry, "miscValue", effect.EffectMiscValue[0]);
                AddIfNonZero(entry, "miscValueB", effect.EffectMiscValue[1]);
                AddIfNonZero(entry, "amplitude", effect.EffectAmplitude);

                if (effect.EffectMechanic != 0)
                    entry["mechanic"] = BuildRawAndName((uint)effect.EffectMechanic, typeof(Mechanics));

                var effectClassMask = Array.ConvertAll(effect.EffectSpellClassMask, i => (uint)i);
                if (effectClassMask[0] != 0 || effectClassMask[1] != 0 || effectClassMask[2] != 0 || effectClassMask[3] != 0)
                    entry["spellClassMask"] = effectClassMask;
```

`EffectMiscValue` is an `int[2]` and `EffectAmplitude` a `float` — both already confirmed present on `SpellEffectEntry`. The class-mask emptiness check mirrors `SpellInfo.cs:782` exactly; keep it identical rather than writing a `.Any()` variant, so the two paths cannot drift.

- [ ] **Step 8: Run the tests**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`
Expected: PASS — new cases plus all existing ones. **The golden tests must still pass**: this task touches only the JSON path, so any golden failure means an edit landed in the text renderer by mistake.

- [ ] **Step 9: Sanity-check the output by eye**

The tests assert presence, not sanity. Add a temporary throwaway test (or use `dotnet test --logger "console;verbosity=detailed"` with a `Console.WriteLine` in a scratch test) that serialises spell 133 with no section filter, and read the JSON. Confirm `spellFamilyName.name` is `SPELLFAMILY_MAGE`, `misc.mechanic.name` is a real mechanic name and not a bare decimal, and no new key holds a nonsense value. Delete the scratch test before committing — a test that prints and asserts nothing is a defect, not a check.

- [ ] **Step 10: Commit**

```bash
git add SpellWork.Core/Spell/SpellInfoJsonSerializer.cs SpellWork.Core.Tests/
git commit -m "feat: expand spell JSON field set to cover the MCP tool surface"
```

---

### Task 3: `SpellWork.Mcp` host, config and stdio discipline

A stdio MCP server communicates by writing JSON-RPC frames to stdout. **One stray `Console.WriteLine` anywhere in the process corrupts the stream and the client disconnects**, so the very first thing this project needs is a test that proves stdout carries nothing else. Everything after this task adds tools to a host that is already known-good.

**Files:**
- Create: `SpellWork.Mcp/SpellWork.Mcp.csproj`, `SpellWork.Mcp/Program.cs`, `SpellWork.Mcp/McpServerConfig.cs`, `SpellWork.Mcp/ToolResponse.cs`
- Create: `SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`, `SpellWork.Mcp.Tests/McpServerConfigTests.cs`, `SpellWork.Mcp.Tests/ToolResponseTests.cs`, `SpellWork.Mcp.Tests/StdioSmokeTests.cs`
- Modify: `SpellWork.sln`

**Interfaces:**
- Consumes: nothing from Tasks 1-2.
- Produces: `SpellWork.Mcp.McpServerConfig` with `SpellWorkConfig ToSpellWorkConfig()`, `bool MySqlConfigured`, `string DbcPath`, `string GtPath`, `string Locale`, `string HotfixCachePath`, and `static McpServerConfig FromEnvironment(IDictionary<string,string> environment)`.
- Produces: `SpellWork.Mcp.ToolResponse` with `static string Ok(object payload)`, `static string Error(string code, string message, IDictionary<string,object> details = null)`, and `const int MaxRows = 500`.

- [ ] **Step 1: Create the server project**

Create `SpellWork.Mcp/SpellWork.Mcp.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>disable</Nullable>
    <RootNamespace>SpellWork.Mcp</RootNamespace>
    <AssemblyName>SpellWork.Mcp</AssemblyName>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="2.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SpellWork.Core\SpellWork.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Same reason as SpellWork.Core.Tests: Storage<T> is declared in
         DBFileReaderLib, which Core references by HintPath rather than as a
         package, so it does not flow transitively (CS0012 otherwise). -->
    <Reference Include="DBFileReaderLib">
      <HintPath>..\packages\DBFileReaderLib.1.0.0.0\lib\DBFileReaderLib.dll</HintPath>
    </Reference>
  </ItemGroup>

</Project>
```

`ModelContextProtocol` reached stable **2.0.0**, which is what Task 3 actually
referenced — this plan was drafted against `0.3.0-preview.4` and the version
above has been corrected to match what shipped. The API shape the later tasks
use (`AddMcpServer`, `WithStdioServerTransport`, `WithToolsFromAssembly`,
`[McpServerToolType]`, `[McpServerTool]`) was confirmed against 2.0.0's own
docs and is unchanged. Newline-delimited JSON-RPC framing works as written; no
`Content-Length` framing adaptation is needed.

- [ ] **Step 2: Write the config reader test**

Create `SpellWork.Mcp.Tests/McpServerConfigTests.cs`:

```csharp
using System.Collections.Generic;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class McpServerConfigTests
    {
        [Fact]
        public void FromEnvironment_DefaultsLocaleToEnUs()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\data\dbc",
                ["SPELLWORK_GT_PATH"] = @"C:\data\gt",
            });

            Assert.Equal("enUS", config.Locale);
        }

        [Fact]
        public void FromEnvironment_HonoursExplicitLocale()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\data\dbc",
                ["SPELLWORK_GT_PATH"] = @"C:\data\gt",
                ["SPELLWORK_LOCALE"] = "deDE",
            });

            Assert.Equal("deDE", config.Locale);
        }

        // A partial MySQL config is a configuration mistake, not a working
        // connection. Requiring all three of host/user/database means the
        // MySQL tools report "not configured" instead of failing to connect
        // with a confusing driver error.
        [Fact]
        public void MySqlConfigured_RequiresHostUserAndDatabase()
        {
            var partial = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = "localhost",
            });
            Assert.False(partial.MySqlConfigured);

            var complete = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_MYSQL_HOST"] = "localhost",
                ["SPELLWORK_MYSQL_USER"] = "reader",
                ["SPELLWORK_MYSQL_WORLD_DB"] = "world",
            });
            Assert.True(complete.MySqlConfigured);
        }

        // Missing required paths must not throw -- a misconfigured server
        // still starts and still answers (spec section 9). The error surfaces
        // per-tool, not at construction.
        [Fact]
        public void FromEnvironment_WithNothingSet_DoesNotThrow()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>());

            Assert.Null(config.DbcPath);
            Assert.False(config.MySqlConfigured);
        }

        [Fact]
        public void ToSpellWorkConfig_MapsEveryValue()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\data\dbc",
                ["SPELLWORK_GT_PATH"] = @"C:\data\gt",
                ["SPELLWORK_HOTFIX_CACHE"] = @"C:\data\DBCache.bin",
                ["SPELLWORK_MYSQL_HOST"] = "db.local",
                ["SPELLWORK_MYSQL_PORT"] = "3307",
                ["SPELLWORK_MYSQL_USER"] = "reader",
                ["SPELLWORK_MYSQL_PASS"] = "secret",
                ["SPELLWORK_MYSQL_WORLD_DB"] = "world",
            });

            var core = config.ToSpellWorkConfig();

            Assert.Equal(@"C:\data\dbc", core.DbcPath);
            Assert.Equal(@"C:\data\gt", core.GtPath);
            Assert.Equal("enUS", core.Locale);
            Assert.Equal(@"C:\data\DBCache.bin", core.HotfixCachePath);
            Assert.Equal("db.local", core.MySqlHost);
            Assert.Equal("3307", core.MySqlPortOrPipe);
            Assert.Equal("reader", core.MySqlUser);
            Assert.Equal("secret", core.MySqlPassword);
            Assert.Equal("world", core.WorldDatabase);
            Assert.True(core.UseDatabase);
        }
    }
}
```

`FromEnvironment` takes a dictionary rather than reading `Environment.GetEnvironmentVariable` directly so it is testable without mutating the test process's environment — parallel xUnit tests that set real environment variables interfere with each other.

- [ ] **Step 3: Write the response-envelope test**

Create `SpellWork.Mcp.Tests/ToolResponseTests.cs`:

```csharp
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class ToolResponseTests
    {
        [Fact]
        public void Ok_WrapsPayloadUnderOkTrue()
        {
            var json = ToolResponse.Ok(new Dictionary<string, object> { ["id"] = 133 });
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(133, doc.RootElement.GetProperty("result").GetProperty("id").GetInt32());
        }

        [Fact]
        public void Error_CarriesCodeMessageAndDetails()
        {
            var json = ToolResponse.Error("dbc_path_missing", "SPELLWORK_DBC_PATH is not set.",
                new Dictionary<string, object> { ["variable"] = "SPELLWORK_DBC_PATH" });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.False(root.GetProperty("ok").GetBoolean());
            Assert.Equal("dbc_path_missing", root.GetProperty("error").GetProperty("code").GetString());
            Assert.Equal("SPELLWORK_DBC_PATH is not set.", root.GetProperty("error").GetProperty("message").GetString());
            Assert.Equal("SPELLWORK_DBC_PATH", root.GetProperty("error").GetProperty("details").GetProperty("variable").GetString());
        }

        [Fact]
        public void MaxRows_IsFiveHundred()
        {
            Assert.Equal(500, ToolResponse.MaxRows);
        }
    }
}
```

- [ ] **Step 4: Create the test project and run to verify failure**

Create `SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Xunit.SkippableFact" Version="1.4.13" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SpellWork.Mcp\SpellWork.Mcp.csproj" />
  </ItemGroup>

</Project>
```

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`
Expected: FAIL to compile — `McpServerConfig` and `ToolResponse` do not exist.

- [ ] **Step 5: Implement `McpServerConfig`**

Create `SpellWork.Mcp/McpServerConfig.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using SpellWork;

namespace SpellWork.Mcp
{
    /// <summary>
    /// The nine SPELLWORK_* environment variables, read once at startup.
    /// Never throws: a misconfigured server must still start and still answer
    /// (spec section 9), so a missing required path becomes a null here and a
    /// structured per-tool error later.
    /// </summary>
    public sealed class McpServerConfig
    {
        public string DbcPath { get; private set; }
        public string GtPath { get; private set; }
        public string Locale { get; private set; }
        public string HotfixCachePath { get; private set; }
        public string MySqlHost { get; private set; }
        public string MySqlPortOrPipe { get; private set; }
        public string MySqlUser { get; private set; }
        public string MySqlPassword { get; private set; }
        public string WorldDatabase { get; private set; }

        /// <summary>
        /// All three of host, user and database, or none of the MySQL tools
        /// work. A half-set config produces a driver error the caller cannot
        /// act on; "not configured" is actionable.
        /// </summary>
        public bool MySqlConfigured =>
            !string.IsNullOrWhiteSpace(MySqlHost) &&
            !string.IsNullOrWhiteSpace(MySqlUser) &&
            !string.IsNullOrWhiteSpace(WorldDatabase);

        public static McpServerConfig FromEnvironment()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                values[(string)entry.Key] = entry.Value as string;

            return FromEnvironment(values);
        }

        public static McpServerConfig FromEnvironment(IDictionary<string, string> environment)
        {
            string Get(string key) =>
                environment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

            return new McpServerConfig
            {
                DbcPath = Get("SPELLWORK_DBC_PATH"),
                GtPath = Get("SPELLWORK_GT_PATH"),
                Locale = Get("SPELLWORK_LOCALE") ?? "enUS",
                HotfixCachePath = Get("SPELLWORK_HOTFIX_CACHE"),
                MySqlHost = Get("SPELLWORK_MYSQL_HOST"),
                MySqlPortOrPipe = Get("SPELLWORK_MYSQL_PORT") ?? "3306",
                MySqlUser = Get("SPELLWORK_MYSQL_USER"),
                MySqlPassword = Get("SPELLWORK_MYSQL_PASS") ?? string.Empty,
                WorldDatabase = Get("SPELLWORK_MYSQL_WORLD_DB"),
            };
        }

        public SpellWorkConfig ToSpellWorkConfig() => new SpellWorkConfig
        {
            DbcPath = DbcPath,
            GtPath = GtPath,
            Locale = Locale,
            HotfixCachePath = HotfixCachePath,
            MySqlHost = MySqlHost,
            MySqlPortOrPipe = MySqlPortOrPipe,
            MySqlUser = MySqlUser,
            MySqlPassword = MySqlPassword,
            WorldDatabase = WorldDatabase,
            UseDatabase = MySqlConfigured,
        };
    }
}
```

Check `SpellWorkConfig`'s property setters — if any is get-only or the type is a constructor-initialised class rather than an object-initialisable one, adapt the mapping. Do not change `SpellWorkConfig` itself; the WinForms app also constructs it.

- [ ] **Step 6: Implement `ToolResponse`**

Create `SpellWork.Mcp/ToolResponse.cs`:

```csharp
using System.Collections.Generic;
using System.Text.Json;

namespace SpellWork.Mcp
{
    /// <summary>
    /// Every tool returns one of these two shapes. A uniform envelope means
    /// the agent can tell "the server answered and the answer is no" apart
    /// from "the call blew up", which a bare exception message cannot.
    /// </summary>
    public static class ToolResponse
    {
        /// <summary>Server-side result cap (spec section 6). A broad query must
        /// not be able to flood the agent's context.</summary>
        public const int MaxRows = 500;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = false,
        };

        public static string Ok(object payload) =>
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ok"] = true,
                ["result"] = payload,
            }, Options);

        public static string Error(string code, string message, IDictionary<string, object> details = null)
        {
            var error = new Dictionary<string, object>
            {
                ["code"] = code,
                ["message"] = message,
            };

            if (details != null && details.Count > 0)
                error["details"] = details;

            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = error,
            }, Options);
        }

        /// <summary>
        /// Applies the row cap and reports it. Callers pass the full match
        /// count so the response can say how much was withheld -- silently
        /// truncating reads as "that was everything".
        /// </summary>
        public static Dictionary<string, object> Paged(IList<object> rows, int totalMatches, int offset, int limit) =>
            new Dictionary<string, object>
            {
                ["rows"] = rows,
                ["count"] = rows.Count,
                ["totalMatches"] = totalMatches,
                ["offset"] = offset,
                ["limit"] = limit,
                ["truncated"] = offset + rows.Count < totalMatches,
                ["maxRows"] = MaxRows,
            };
    }
}
```

- [ ] **Step 7: Write the host**

Create `SpellWork.Mcp/Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpellWork.Mcp
{
    public static class Program
    {
        public static async System.Threading.Tasks.Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // stdout carries JSON-RPC frames and nothing else. Any log line
            // that reaches it corrupts the stream and the client disconnects,
            // so every provider is pinned to stderr.
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services.AddSingleton(McpServerConfig.FromEnvironment());

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            await builder.Build().RunAsync();
        }
    }
}
```

`WithToolsFromAssembly()` scans the calling assembly for `[McpServerToolType]` classes, so later tasks add tools without touching this file. Verify the exact namespaces and extension-method names against the installed SDK version — `AddMcpServer` lives in `ModelContextProtocol.Server` or an `Microsoft.Extensions.DependencyInjection` extension namespace depending on version. Add whatever `using` the compiler asks for; do not guess.

- [ ] **Step 8: Write the stdio smoke test**

Create `SpellWork.Mcp.Tests/StdioSmokeTests.cs`. This is the test that makes stdout discipline enforceable rather than aspirational:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class StdioSmokeTests
    {
        // Spawns the built server, performs the JSON-RPC initialize
        // handshake, and asserts every line stdout produced parses as JSON.
        // A single Console.WriteLine anywhere in the process fails this.
        [Fact]
        public async Task Stdout_CarriesOnlyWellFormedJsonRpc()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());

            Assert.NotEmpty(lines);
            foreach (var line in lines)
            {
                var exception = Record.Exception(() => JsonDocument.Parse(line).Dispose());
                Assert.True(exception == null, $"stdout line is not JSON: {line}");
            }
        }

        // Spec section 9: a misconfigured server must still start and still
        // answer. Handshaking with no SPELLWORK_* variables set at all is the
        // worst case, and it must still complete.
        [Fact]
        public async Task Server_StartsAndAnswers_WithNoConfiguration()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = null,
                ["SPELLWORK_GT_PATH"] = null,
            });

            Assert.Contains(lines, line => line.Contains("\"result\""));
        }

        internal static async Task<List<string>> RunHandshakeAsync(IDictionary<string, string> environment)
        {
            var exePath = Path.Combine(AppContext.BaseDirectory, "SpellWork.Mcp.dll");
            var startInfo = new ProcessStartInfo("dotnet", $"\"{exePath}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var pair in environment)
            {
                if (pair.Value == null)
                    startInfo.Environment.Remove(pair.Key);
                else
                    startInfo.Environment[pair.Key] = pair.Value;
            }

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            const string initialize =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"2024-11-05\",\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"smoke\",\"version\":\"1.0\"}}}";
            const string listTools = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}";

            await process.StandardInput.WriteLineAsync(initialize);
            await process.StandardInput.FlushAsync();
            await process.StandardInput.WriteLineAsync(
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            await process.StandardInput.FlushAsync();
            await process.StandardInput.WriteLineAsync(listTools);
            await process.StandardInput.FlushAsync();

            var lines = new List<string>();
            var readTask = Task.Run(async () =>
            {
                string line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                {
                    if (line.Length > 0)
                        lines.Add(line);
                    if (lines.Count >= 2)
                        break;
                }
            });

            if (await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(30))) != readTask)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException("MCP server did not respond to the initialize handshake within 30s.");
            }

            process.StandardInput.Close();
            try { process.Kill(entireProcessTree: true); } catch { }

            return lines;
        }
    }
}
```

The test project must be able to find the server assembly. Add to `SpellWork.Mcp.Tests.csproj` so the server's output lands beside the test binaries — the `ProjectReference` already does this for a library reference, but confirm `SpellWork.Mcp.dll` actually appears in the test output directory and adjust `exePath` if the SDK puts it elsewhere.

If the SDK's stdio transport expects `Content-Length`-framed messages rather than newline-delimited JSON, adapt the writer to that framing — read the SDK's own transport source or samples to confirm which. The assertions do not change; only how bytes are written and split.

- [ ] **Step 9: Add both projects to the solution and run everything**

```bash
dotnet sln SpellWork.sln add SpellWork.Mcp/SpellWork.Mcp.csproj
dotnet sln SpellWork.sln add SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj
dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj
```

Expected: PASS, all tests including both smoke tests.

- [ ] **Step 10: Verify the solution still builds clean**

Run: `"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" SpellWork.sln -restore -t:Rebuild -v:minimal`
Expected: zero warning lines, zero error lines. Adding projects to a solution is exactly the change that reintroduces a NuGet version conflict, and only `-t:Rebuild` will show it.

- [ ] **Step 11: Commit**

```bash
git add SpellWork.Mcp/ SpellWork.Mcp.Tests/ SpellWork.sln
git commit -m "feat: add SpellWork.Mcp stdio host with config, response envelope and smoke tests"
```

---

### Task 4: `SpellDataService` and `server_info`

The first real tool. `server_info` is what the agent calls when something looks wrong, so it must work even when nothing else does — including when the DBC path is garbage.

**Files:**
- Create: `SpellWork.Mcp/SpellDataService.cs`, `SpellWork.Mcp/Tools/DiagnosticTools.cs`
- Modify: `SpellWork.Mcp/Program.cs` (register the service)
- Test: `SpellWork.Mcp.Tests/SpellDataServiceTests.cs`, `SpellWork.Mcp.Tests/StdioSmokeTests.cs` (assert `server_info` lists)

**Interfaces:**
- Consumes: `McpServerConfig`, `ToolResponse` (Task 3); `SpellWork.DBC.DBC.LoadAsync(SpellWorkConfig, ISpellWorkLogger)`, `SpellWork.GameTables.GameTable<T>`, `SpellWork.Database.WorldDatabase`.
- Produces: `SpellWork.Mcp.SpellDataService` with `Task<SpellData> GetAsync()`; `SpellWork.Mcp.SpellData` with `bool Loaded`, `string ErrorCode`, `string ErrorMessage`, `string ErrorVariable`, `TimeSpan LoadDuration`, `IReadOnlyDictionary<string,int> StoreCounts`, `int SpellCount`.
- Produces: tool `server_info` (no arguments).

**Design note.** `Lazy<Task<SpellData>>` with `LazyThreadSafetyMode.ExecutionAndPublication` gives exactly one load for the process lifetime no matter how many concurrent tool calls arrive first. `DBC.LoadAsync` has no load guard and `GameTable<T>.Open` populates with `_records.Add`, so a *second* load in the same process throws a duplicate-key `ArgumentException` — the `Lazy` is what makes that unreachable. Do not add a "reload" tool.

`DBC.LoadAsync` swallows `DirectoryNotFoundException` per store, so a wrong path yields silently empty stores rather than an exception. `SpellDataService` must therefore check the paths itself *before* loading and report a structured error; an empty `SpellInfoStore` after a successful load is the second signal.

- [ ] **Step 1: Write the failing test**

Create `SpellWork.Mcp.Tests/SpellDataServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class SpellDataServiceTests
    {
        private static SpellDataService ServiceFor(IDictionary<string, string> environment) =>
            new SpellDataService(McpServerConfig.FromEnvironment(environment), NullLoggerFactoryStub.Logger);

        [Fact]
        public async Task GetAsync_WithNoDbcPath_ReportsTheVariableToFix()
        {
            var data = await ServiceFor(new Dictionary<string, string>()).GetAsync();

            Assert.False(data.Loaded);
            Assert.Equal("dbc_path_missing", data.ErrorCode);
            Assert.Equal("SPELLWORK_DBC_PATH", data.ErrorVariable);
        }

        [Fact]
        public async Task GetAsync_WithNonexistentDbcPath_NamesThePath()
        {
            var data = await ServiceFor(new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = @"C:\definitely\not\here",
                ["SPELLWORK_GT_PATH"] = @"C:\definitely\not\here\gt",
            }).GetAsync();

            Assert.False(data.Loaded);
            Assert.Equal("dbc_path_not_found", data.ErrorCode);
            Assert.Contains(@"C:\definitely\not\here", data.ErrorMessage);
        }

        // The whole point of the Lazy: concurrent first callers must produce
        // one load, not N. A second DBC.LoadAsync in the same process throws
        // a duplicate-key ArgumentException from GameTable<T>.Open.
        [Fact]
        public async Task GetAsync_LoadsExactlyOnce_UnderConcurrency()
        {
            var service = ServiceFor(new Dictionary<string, string>());

            var results = await Task.WhenAll(new[]
            {
                service.GetAsync(), service.GetAsync(), service.GetAsync(),
                service.GetAsync(), service.GetAsync(), service.GetAsync(),
            });

            foreach (var result in results)
                Assert.Same(results[0], result);
        }
    }
}
```

`NullLoggerFactoryStub.Logger` is a one-line helper — put it in the same file:

```csharp
    internal static class NullLoggerFactoryStub
    {
        public static Microsoft.Extensions.Logging.ILogger<SpellDataService> Logger { get; } =
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SpellDataService>.Instance;
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~SpellDataServiceTests"`
Expected: FAIL to compile — `SpellDataService` does not exist.

- [ ] **Step 3: Implement `SpellDataService`**

Create `SpellWork.Mcp/SpellDataService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpellWork.DBC;
using SpellWork.GameTables;

namespace SpellWork.Mcp
{
    /// <summary>The outcome of the one client-data load this process performs.</summary>
    public sealed class SpellData
    {
        public bool Loaded { get; internal set; }
        public string ErrorCode { get; internal set; }
        public string ErrorMessage { get; internal set; }
        public string ErrorVariable { get; internal set; }
        public TimeSpan LoadDuration { get; internal set; }
        public IReadOnlyDictionary<string, int> StoreCounts { get; internal set; } =
            new Dictionary<string, int>();
        public int SpellCount { get; internal set; }
    }

    /// <summary>
    /// Loads DBC and GameTable data exactly once, on first use.
    /// ExecutionAndPublication rather than a plain lock: DBC.LoadAsync has no
    /// load guard and GameTable&lt;T&gt;.Open populates with _records.Add, so a
    /// second load in the same process throws a duplicate-key
    /// ArgumentException. The Lazy is what makes that unreachable.
    /// </summary>
    public sealed class SpellDataService
    {
        private readonly McpServerConfig _config;
        private readonly ILogger<SpellDataService> _logger;
        private readonly Lazy<Task<SpellData>> _data;

        public SpellDataService(McpServerConfig config, ILogger<SpellDataService> logger)
        {
            _config = config;
            _logger = logger;
            _data = new Lazy<Task<SpellData>>(LoadAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public McpServerConfig Config => _config;

        public Task<SpellData> GetAsync() => _data.Value;

        private async Task<SpellData> LoadAsync()
        {
            var missing = ValidatePaths();
            if (missing != null)
                return missing;

            var stopwatch = Stopwatch.StartNew();
            var result = new SpellData();

            try
            {
                await DBC.LoadAsync(_config.ToSpellWorkConfig(), new LoggerAdapter(_logger));
                GameTable.LoadAll(_config.GtPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client data load failed.");
                result.ErrorCode = "load_failed";
                result.ErrorMessage = ex.Message;
                result.LoadDuration = stopwatch.Elapsed;
                return result;
            }

            stopwatch.Stop();
            result.LoadDuration = stopwatch.Elapsed;
            result.StoreCounts = CountStores();
            result.SpellCount = DBC.SpellInfoStore.Count;

            // DBC.LoadAsync swallows DirectoryNotFoundException per store, so
            // a path that exists but holds no .db2 files "succeeds" with every
            // store empty. Zero spells is that case, and the caller needs to
            // hear about it rather than get empty results forever.
            if (result.SpellCount == 0)
            {
                result.ErrorCode = "no_data_loaded";
                result.ErrorMessage =
                    $"No spells were loaded from '{_config.DbcPath}\\{_config.Locale}'. " +
                    "Check that the path holds the .db2 files for the configured locale.";
                result.ErrorVariable = "SPELLWORK_DBC_PATH";
                return result;
            }

            result.Loaded = true;
            _logger.LogInformation("Loaded {SpellCount} spells in {Duration}.", result.SpellCount, result.LoadDuration);
            return result;
        }

        private SpellData ValidatePaths()
        {
            if (string.IsNullOrWhiteSpace(_config.DbcPath))
                return Failure("dbc_path_missing", "SPELLWORK_DBC_PATH is not set.", "SPELLWORK_DBC_PATH");

            if (string.IsNullOrWhiteSpace(_config.GtPath))
                return Failure("gt_path_missing", "SPELLWORK_GT_PATH is not set.", "SPELLWORK_GT_PATH");

            if (!Directory.Exists(_config.DbcPath))
                return Failure("dbc_path_not_found",
                    $"SPELLWORK_DBC_PATH points at '{_config.DbcPath}', which does not exist.", "SPELLWORK_DBC_PATH");

            if (!Directory.Exists(_config.GtPath))
                return Failure("gt_path_not_found",
                    $"SPELLWORK_GT_PATH points at '{_config.GtPath}', which does not exist.", "SPELLWORK_GT_PATH");

            return null;
        }

        private static SpellData Failure(string code, string message, string variable) => new SpellData
        {
            Loaded = false,
            ErrorCode = code,
            ErrorMessage = message,
            ErrorVariable = variable,
        };

        private static IReadOnlyDictionary<string, int> CountStores()
        {
            var counts = new Dictionary<string, int>();
            foreach (var property in typeof(DBC).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                if (!property.PropertyType.IsGenericType ||
                    property.PropertyType.GetGenericTypeDefinition() != typeof(Storage<>))
                    continue;

                var storage = property.GetValue(null) as System.Collections.ICollection;
                counts[property.Name] = storage?.Count ?? 0;
            }

            return counts;
        }

        private sealed class LoggerAdapter : ISpellWorkLogger
        {
            private readonly ILogger _logger;
            public LoggerAdapter(ILogger logger) => _logger = logger;
            public void Warn(string message) => _logger.LogWarning("{Message}", message);
        }
    }
}
```

Two things to check against the real source before compiling:
1. How the WinForms app loads game tables — `grep -rn "GameTable" SpellWork/Forms/FormMain.cs SpellWork.Core/GameTables/GameTables.cs`. There may be no `GameTable.LoadAll`; if loading is per-type or driven from `DBC.LoadAsync` already, mirror whatever `FormMain` does rather than inventing an API.
2. `ISpellWorkLogger`'s namespace — it is `SpellWork.ISpellWorkLogger` or similar; add the `using` the compiler asks for.

- [ ] **Step 4: Register the service**

In `Program.cs`, after the `McpServerConfig` registration:

```csharp
            builder.Services.AddSingleton<SpellDataService>();
```

- [ ] **Step 5: Run the service tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~SpellDataServiceTests"`
Expected: PASS.

- [ ] **Step 6: Implement `server_info`**

Create `SpellWork.Mcp/Tools/DiagnosticTools.cs`:

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Database;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class DiagnosticTools
    {
        private readonly SpellDataService _data;

        public DiagnosticTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "server_info")]
        [Description("Configured paths, locale, store counts, load duration and MySQL reachability. Call this first when something looks wrong.")]
        public async Task<string> ServerInfo()
        {
            var data = await _data.GetAsync();
            var config = _data.Config;

            var payload = new Dictionary<string, object>
            {
                ["version"] = SpellWork.DBC.DBC.Version,
                ["dbcPath"] = config.DbcPath,
                ["gtPath"] = config.GtPath,
                ["locale"] = config.Locale,
                ["hotfixCachePath"] = config.HotfixCachePath,
                ["clientDataLoaded"] = data.Loaded,
                ["loadDurationMs"] = (int)data.LoadDuration.TotalMilliseconds,
                ["spellCount"] = data.SpellCount,
                // Only the non-empty stores: 44 entries of which most are zero
                // on a misconfigured server is noise, and the zeros are the
                // same information as their absence.
                ["storeCounts"] = data.StoreCounts.Where(pair => pair.Value > 0)
                    .OrderBy(pair => pair.Key)
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                ["mysqlConfigured"] = config.MySqlConfigured,
            };

            if (!data.Loaded && data.ErrorCode != null)
            {
                payload["clientDataError"] = new Dictionary<string, object>
                {
                    ["code"] = data.ErrorCode,
                    ["message"] = data.ErrorMessage,
                    ["variable"] = data.ErrorVariable,
                };
            }

            if (config.MySqlConfigured)
            {
                var database = new WorldDatabase(config.ToSpellWorkConfig());
                payload["mysqlReachable"] = database.TestConnection(out var error);
                if (error != null)
                    payload["mysqlError"] = error;
            }

            return ToolResponse.Ok(payload);
        }
    }
}
```

- [ ] **Step 7: Assert the tool lists over stdio**

Add to `SpellWork.Mcp.Tests/StdioSmokeTests.cs`:

```csharp
        [Fact]
        public async Task ToolsList_IncludesServerInfo()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());

            Assert.Contains(lines, line => line.Contains("server_info"));
        }
```

- [ ] **Step 8: Run all tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`
Expected: PASS.

- [ ] **Step 9: Verify by hand that `server_info` answers on a broken config**

```bash
SPELLWORK_DBC_PATH=/nope SPELLWORK_GT_PATH=/nope dotnet run --project SpellWork.Mcp
```

Paste the initialize handshake and a `tools/call` for `server_info` on stdin. Expected: a JSON-RPC response whose payload has `clientDataLoaded: false` and `clientDataError.variable: "SPELLWORK_DBC_PATH"`. The process must still be alive afterwards. Ctrl-C to exit.

- [ ] **Step 10: Commit**

```bash
git add SpellWork.Mcp/SpellDataService.cs SpellWork.Mcp/Tools/DiagnosticTools.cs SpellWork.Mcp/Program.cs SpellWork.Mcp.Tests/
git commit -m "feat: add SpellDataService and the server_info diagnostic tool"
```

---

### Task 5: `get_spell`

The tool the whole server exists for. Both output formats, section filtering, and an explicit render context per call — which is what Task 1 made possible.

**Files:**
- Create: `SpellWork.Mcp/Tools/SpellTools.cs`
- Modify: `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs` (fill in the `items` and `triggers` sections reserved in Task 2)
- Test: `SpellWork.Mcp.Tests/SpellToolsTests.cs`, `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`

**Interfaces:**
- Consumes: `SpellDataService.GetAsync()`, `ToolResponse`, `RenderContext`, `SpellInfoJsonSerializer.Serialize(SpellInfo, RenderContext, IReadOnlyCollection<string>)`, `SpellInfo.Write(ISpellInfoWriter, RenderContext)`, `PlainTextSpellInfoWriter`.
- Produces: tool `get_spell(id, format, sections, difficulty, level, itemLevel)`.
- Produces: `SpellWork.Mcp.Tools.SpellTools` — Tasks 6 and 7 add methods to this same class.
- Produces: helper `SpellTools.ResolveContext(uint? level, uint? itemLevel, uint difficulty)` returning `RenderContext`, reused by Task 9's `calculate_spell_scaling`.
- Produces: JSON sections `items` (an array of `{id, name}` for items whose `ItemEffect` rows reference this spell) and `triggers` (`{triggers: int[], triggeredBy: int[]}`).

**Design note.** `difficulty` selects a `MapDifficultyEntry`, not a `SpellEffect.DifficultyID`. Resolve it against `DBC.MapDifficulty` by id; `difficulty=0` means no map difficulty (null), matching the GUI's "nothing selected" state. `level` and `itemLevel` default to `DBC.MaxLevel` (120) and 475 — the same values `DBC.cs:431-433` initialises the statics to, so an unparameterised `get_spell` renders exactly what the GUI shows on startup.

- [ ] **Step 1: Write the failing serializer tests for the two new sections**

Add to `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`:

```csharp
        [SkippableFact]
        public void Serialize_TriggersSection_ReportsBothDirections()
        {
            _fixture.SkipIfNoClientData();

            // Pick a spell that is known to both trigger and be triggered:
            // find one whose id appears in SpellTriggerStore as a key and
            // which also has a non-zero EffectTriggerSpell.
            var spell = DBC.DBC.SpellInfoStore.Values.First(s =>
                DBC.DBC.SpellTriggerStore.ContainsKey(s.ID)
                && s.Effects.Any(e => e != null && e.EffectTriggerSpell != 0));

            var json = SpellInfoJsonSerializer.Serialize(
                spell, RenderContext.FromGlobals(), new[] { "triggers" });
            using var doc = JsonDocument.Parse(json);
            var triggers = doc.RootElement.GetProperty("triggers");

            Assert.True(triggers.GetProperty("triggers").GetArrayLength() > 0);
            Assert.True(triggers.GetProperty("triggeredBy").GetArrayLength() > 0);
        }

        [SkippableFact]
        public void Serialize_ItemsSection_ListsItemsGrantingTheSpell()
        {
            _fixture.SkipIfNoClientData();

            var effect = DBC.DBC.ItemEffect.Values.First(e => e.SpellID != 0
                && DBC.DBC.SpellInfoStore.ContainsKey(e.SpellID));
            var spell = DBC.DBC.SpellInfoStore[effect.SpellID];

            var json = SpellInfoJsonSerializer.Serialize(
                spell, RenderContext.FromGlobals(), new[] { "items" });
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("items").GetArrayLength() > 0);
        }
```

Check `ItemEffectEntry`'s field names first (`grep -n "" SpellWork.Core/DBC/Structures/ItemEffectEntry.cs`) — `SpellID` and the parent-item field may be named differently, and the join from `ItemEffect` to `Item`/`ItemSparse` differs by build. Use whatever `SpellInfo.AppendItemInfo` already does; that method already performs this exact join for the GUI, so read it and mirror it rather than deriving a second one.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj --filter "Serialize_TriggersSection|Serialize_ItemsSection"`
Expected: FAIL — `triggers` and `items` are not in the document.

- [ ] **Step 3: Implement the two sections**

In `SpellInfoJsonSerializer.Serialize`, after the `equippedItems` block:

```csharp
            if (Wants("triggers"))
                root["triggers"] = BuildTriggers(spell);

            if (Wants("items"))
                AddIfNotEmpty(root, "items", BuildItems(spell));
```

`triggers` is emitted unconditionally when requested, including when both arrays are empty: "this spell triggers nothing" is an answer, and an absent key would be indistinguishable from "the section was filtered out".

```csharp
        // Forward direction from the spell's own effects; reverse direction
        // from SpellTriggerStore, which DBC.LoadAsync builds during load.
        private static Dictionary<string, object> BuildTriggers(SpellInfo spell)
        {
            var triggers = spell.Effects
                .Where(effect => effect != null && effect.EffectTriggerSpell != 0)
                .Select(effect => effect.EffectTriggerSpell)
                .Distinct()
                .OrderBy(id => id)
                .Cast<object>()
                .ToList();

            var triggeredBy = new List<object>();
            if (DBC.DBC.SpellTriggerStore.TryGetValue(spell.ID, out var sources))
                triggeredBy.AddRange(sources.OrderBy(id => id).Cast<object>());

            return new Dictionary<string, object>
            {
                ["triggers"] = triggers,
                ["triggeredBy"] = triggeredBy,
            };
        }

        private static List<object> BuildItems(SpellInfo spell)
        {
            var result = new List<object>();
            foreach (var itemEffect in DBC.DBC.ItemEffect.Values.Where(e => e.SpellID == spell.ID))
            {
                if (!DBC.DBC.ItemSparse.TryGetValue(itemEffect.ParentItemID, out var item))
                    continue;

                result.Add(new Dictionary<string, object>
                {
                    ["id"] = itemEffect.ParentItemID,
                    ["name"] = item.Display,
                });
            }

            return result;
        }
```

`ParentItemID` and `Display` are guesses at the 8.3.7 field names. **Read `SpellInfo.AppendItemInfo` and copy the join it already performs**, including which store it reads the name from. If `AppendItemInfo` iterates `DBC.Item` rather than `ItemSparse`, do the same.

`BuildItems` scans every `ItemEffect` row per call. If `ItemEffect` has more than a few tens of thousands of rows this is fine for one spell but not in a loop; `compare_spells` (Task 7) calls `Serialize` twice, which is still fine. Do not add a cache — YAGNI until a tool proves slow.

- [ ] **Step 4: Run the serializer tests**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`
Expected: PASS, goldens still green.

- [ ] **Step 5: Write the failing tool test**

Create `SpellWork.Mcp.Tests/SpellToolsTests.cs`:

```csharp
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class SpellToolsTests : IClassFixture<McpDataFixture>
    {
        private readonly McpDataFixture _fixture;

        public SpellToolsTests(McpDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task GetSpell_ReturnsJsonForFireball()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(133, doc.RootElement.GetProperty("result").GetProperty("id").GetInt32());
            Assert.Equal("Fireball", doc.RootElement.GetProperty("result").GetProperty("name").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_TextFormat_ReturnsRenderedText()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133, format: "text");
            using var doc = JsonDocument.Parse(json);

            var text = doc.RootElement.GetProperty("result").GetProperty("text").GetString();
            Assert.Contains("ID - 133", text);
        }

        // The reason Task 1 exists: two levels in flight at once must produce
        // two different renderings, and each must be the one it produces alone.
        [SkippableFact]
        public async Task GetSpell_HonoursLevel()
        {
            _fixture.SkipIfNoClientData();

            var atTwenty = await _fixture.SpellTools.GetSpell(606, level: 20);
            var atOneTwenty = await _fixture.SpellTools.GetSpell(606, level: 120);

            Assert.NotEqual(BasePoints(atTwenty), BasePoints(atOneTwenty));

            static double BasePoints(string json)
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("effects")[0]
                    .GetProperty("basePoints").GetDouble();
            }
        }

        [SkippableFact]
        public async Task GetSpell_UnknownId_ReturnsNotFoundWithSuggestions()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(999999999);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.False(root.GetProperty("ok").GetBoolean());
            Assert.Equal("spell_not_found", root.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_UnknownSection_ReturnsStructuredErrorNotAnException()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133, sections: new[] { "nonsense" });
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("invalid_section", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task GetSpell_UnknownFormat_ReturnsStructuredError()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpell(133, format: "xml");
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("invalid_format", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }
}
```

Create `SpellWork.Mcp.Tests/McpDataFixture.cs` — every later tool test uses it, so build it once here:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    /// <summary>
    /// One loaded SpellDataService shared by every tool test in the assembly.
    /// Client data loads once per process and cannot load twice, so this is a
    /// collection fixture rather than per-class.
    /// </summary>
    public sealed class McpDataFixture
    {
        public McpDataFixture()
        {
            var environment = new Dictionary<string, string>
            {
                ["SPELLWORK_DBC_PATH"] = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH"),
                ["SPELLWORK_GT_PATH"] = Environment.GetEnvironmentVariable("SPELLWORK_GT_PATH"),
                ["SPELLWORK_LOCALE"] = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE"),
            };

            Config = McpServerConfig.FromEnvironment(environment);
            Data = new SpellDataService(Config, NullLogger<SpellDataService>.Instance);
            SpellTools = new SpellTools(Data);
        }

        public McpServerConfig Config { get; }
        public SpellDataService Data { get; }
        public SpellTools SpellTools { get; }

        public bool HasClientData => !string.IsNullOrWhiteSpace(Config.DbcPath);

        public void SkipIfNoClientData() =>
            Skip.IfNot(HasClientData, "SPELLWORK_DBC_PATH is not set; skipping tests that need client data.");
    }
}
```

`SpellWork.Core.Tests/ClientDataFixture.cs` already solves this problem for the Core tests — read it and match its skip message and its double-checked load guard rather than inventing a second convention.

- [ ] **Step 6: Run to verify failure**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~SpellToolsTests"`
Expected: FAIL to compile — `SpellTools` does not exist.

- [ ] **Step 7: Implement `SpellTools.GetSpell`**

Create `SpellWork.Mcp/Tools/SpellTools.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Spell;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class SpellTools
    {
        private readonly SpellDataService _data;

        public SpellTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "get_spell")]
        [Description("The full decoded view of one spell. format is \"json\" or \"text\". sections filters to any of: misc, attributes, effects, powers, targets, reagents, aura, procs, scaling, context, cooldowns, levels, interrupts, shapeshift, equippedItems, items, triggers.")]
        public async Task<string> GetSpell(
            int id,
            string format = "json",
            string[] sections = null,
            uint difficulty = 0,
            uint? level = null,
            uint? itemLevel = null)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(id, out var spell))
                return NotFound(id);

            if (!string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
            {
                return ToolResponse.Error("invalid_format",
                    $"Unknown format '{format}'. Valid formats are: json, text.");
            }

            var context = ResolveContext(level, itemLevel, difficulty);

            if (string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
            {
                var writer = new PlainTextSpellInfoWriter();
                spell.Write(writer, context);
                return ToolResponse.Ok(new Dictionary<string, object>
                {
                    ["id"] = spell.ID,
                    ["name"] = spell.Name,
                    ["text"] = writer.ToString(),
                });
            }

            try
            {
                // Serialize returns a complete JSON document, so it is spliced
                // in as raw JSON rather than re-serialised as a string value.
                var document = SpellInfoJsonSerializer.Serialize(spell, context, sections);
                return "{\"ok\":true,\"result\":" + document + "}";
            }
            catch (ArgumentException ex)
            {
                // The serializer throws for an unknown section name. That is a
                // caller mistake, not a server fault: it must come back as a
                // structured error naming the valid sections, not as an
                // exception the transport turns into a protocol-level failure.
                return ToolResponse.Error("invalid_section", ex.Message);
            }
        }

        internal static RenderContext ResolveContext(uint? level, uint? itemLevel, uint difficulty)
        {
            SpellWork.DBC.Structures.MapDifficultyEntry mapDifficulty = null;
            if (difficulty != 0)
                SpellWork.DBC.DBC.MapDifficulty.TryGetValue((int)difficulty, out mapDifficulty);

            return new RenderContext(
                level ?? SpellWork.DBC.DBC.MaxLevel,
                itemLevel ?? 475,
                mapDifficulty);
        }

        internal static string DataError(SpellData data) =>
            ToolResponse.Error(data.ErrorCode ?? "client_data_unavailable",
                data.ErrorMessage ?? "Client data is not loaded.",
                data.ErrorVariable == null
                    ? null
                    : new Dictionary<string, object> { ["variable"] = data.ErrorVariable });

        // Spec section 9: an unknown id gets nearest-name suggestions from
        // SpellName, so the agent can recover from a typo or a stale id
        // without a second round trip.
        internal static string NotFound(int id) =>
            ToolResponse.Error("spell_not_found", $"No spell with id {id}.",
                new Dictionary<string, object>
                {
                    ["nearestIds"] = SpellWork.DBC.DBC.SpellInfoStore.Keys
                        .OrderBy(candidate => Math.Abs((long)candidate - id))
                        .Take(5)
                        .Select(candidate => new Dictionary<string, object>
                        {
                            ["id"] = candidate,
                            ["name"] = SpellWork.DBC.DBC.SpellInfoStore[candidate].Name,
                        })
                        .Cast<object>()
                        .ToList(),
                });
    }
}
```

The raw-JSON splice in the `json` branch is deliberate — `ToolResponse.Ok` would escape the whole document into a string value and the agent would have to parse twice. If `PlainTextSpellInfoWriter` does not expose the rendered text via `ToString()`, use whatever accessor it does have (`grep -n "public" SpellWork.Core/Spell/PlainTextSpellInfoWriter.cs`).

`MaxLevel` is `DBC.MaxLevel` (120) and the item-level default 475 comes from `DBC.cs:431-433` — do not hard-code 120 if `MaxLevel` is reachable.

- [ ] **Step 8: Run all tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj` and `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`
Expected: PASS both. Without `SPELLWORK_DBC_PATH` set, the data-dependent tests skip with the explicit message rather than failing.

- [ ] **Step 9: Commit**

```bash
git add SpellWork.Mcp/Tools/SpellTools.cs SpellWork.Core/Spell/SpellInfoJsonSerializer.cs SpellWork.Mcp.Tests/ SpellWork.Core.Tests/
git commit -m "feat: add the get_spell tool with json and text formats"
```

---

### Task 6: `search_spells`

**Files:**
- Modify: `SpellWork.Mcp/Tools/SpellTools.cs`
- Test: `SpellWork.Mcp.Tests/SpellToolsTests.cs`

**Interfaces:**
- Consumes: `SpellTools` (Task 5), `ToolResponse.Paged`, `ToolResponse.MaxRows`, the `spellFamilyName` field added in Task 2.
- Produces: tool `search_spells(name, family, auraType, effectType, targetType, attribute, schoolMask, limit, offset)` returning `{rows, count, totalMatches, offset, limit, truncated, maxRows}` where each row is `{id, name, summary}`.

**Design note.** Every filter is optional and they AND together. `name` is a case-insensitive substring match, not a regex — a regex from a model is a denial-of-service vector against a 227,000-row scan. `family`, `effectType`, `auraType` and `targetType` each accept **either** an integer **or** an enum member name, because an agent that has just read `list_enum` output has the name, not the number.

- [ ] **Step 1: Write the failing test**

Add to `SpellWork.Mcp.Tests/SpellToolsTests.cs`:

```csharp
        [SkippableFact]
        public async Task SearchSpells_ByName_FindsFireball()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(name: "Fireball");
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("totalMatches").GetInt32() > 0);
            Assert.Contains(result.GetProperty("rows").EnumerateArray(),
                row => row.GetProperty("id").GetInt32() == 133);
        }

        [SkippableFact]
        public async Task SearchSpells_ByFamilyName_AcceptsEnumNameOrNumber()
        {
            _fixture.SkipIfNoClientData();

            var byName = await _fixture.SpellTools.SearchSpells(family: "SPELLFAMILY_MAGE", limit: 5);
            var byNumber = await _fixture.SpellTools.SearchSpells(family: "3", limit: 5);

            Assert.Equal(TotalMatches(byName), TotalMatches(byNumber));
            Assert.True(TotalMatches(byName) > 0);

            static int TotalMatches(string json)
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("totalMatches").GetInt32();
            }
        }

        // The cap is a hard server-side limit, not a suggestion: a model that
        // asks for 10000 rows must not be able to flood its own context.
        [SkippableFact]
        public async Task SearchSpells_ClampsLimitToMaxRows()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(name: "a", limit: 10000);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("rows").GetArrayLength() <= ToolResponse.MaxRows);
            Assert.Equal(ToolResponse.MaxRows, result.GetProperty("limit").GetInt32());
        }

        [SkippableFact]
        public async Task SearchSpells_TruncatedFlag_IsTrueWhenMoreRemain()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(name: "a", limit: 5);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(5, result.GetProperty("rows").GetArrayLength());
            Assert.True(result.GetProperty("truncated").GetBoolean());
        }

        [SkippableFact]
        public async Task SearchSpells_UnknownEnumName_ReturnsStructuredError()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.SearchSpells(family: "SPELLFAMILY_NOT_A_THING");
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("invalid_enum_value", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task SearchSpells_Offset_PagesWithoutOverlap()
        {
            _fixture.SkipIfNoClientData();

            var first = await _fixture.SpellTools.SearchSpells(name: "Fire", limit: 10, offset: 0);
            var second = await _fixture.SpellTools.SearchSpells(name: "Fire", limit: 10, offset: 10);

            Assert.Empty(Ids(first).Intersect(Ids(second)));

            static System.Collections.Generic.List<int> Ids(string json)
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("rows").EnumerateArray()
                    .Select(row => row.GetProperty("id").GetInt32()).ToList();
            }
        }
```

Add `using System.Linq;` to the test file.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~SearchSpells"`
Expected: FAIL to compile — `SearchSpells` does not exist.

- [ ] **Step 3: Implement the enum-argument parser**

Add to `SpellTools`, above `SearchSpells`:

```csharp
        /// <summary>
        /// Accepts "3" or "SPELLFAMILY_MAGE" for the same filter. An agent
        /// that has just read list_enum output holds the name, not the number,
        /// and making it convert first is a round trip for nothing.
        /// Returns false and sets <paramref name="error"/> when the text
        /// matches neither.
        /// </summary>
        internal static bool TryParseEnumArgument(string text, Type enumType, out long value, out string error)
        {
            value = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (long.TryParse(text, out value))
                return true;

            foreach (var name in Enum.GetNames(enumType))
            {
                if (!string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = Convert.ToInt64(Enum.Parse(enumType, name));
                return true;
            }

            error = $"'{text}' is neither a number nor a member of {enumType.Name}. " +
                    $"Call list_enum(\"{enumType.Name}\") for the valid names.";
            return false;
        }
```

- [ ] **Step 4: Implement `SearchSpells`**

```csharp
        [McpServerTool(Name = "search_spells")]
        [Description("Find spells by any combination of name substring, family, aura type, effect type, target type, attribute bit and school mask. Enum arguments accept either a number or an enum member name. Returns id, name and a one-line summary per hit.")]
        public async Task<string> SearchSpells(
            string name = null,
            string family = null,
            string auraType = null,
            string effectType = null,
            string targetType = null,
            string attribute = null,
            uint? schoolMask = null,
            int limit = 50,
            int offset = 0)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            long? familyValue = null, auraValue = null, effectValue = null, targetValue = null, attributeValue = null;

            if (!TryResolve(family, typeof(SpellFamilyNames), ref familyValue, out var error) ||
                !TryResolve(auraType, typeof(AuraType), ref auraValue, out error) ||
                !TryResolve(effectType, typeof(SpellEffects), ref effectValue, out error) ||
                !TryResolve(targetType, typeof(Targets), ref targetValue, out error) ||
                !TryResolve(attribute, typeof(SpellAtribute), ref attributeValue, out error))
            {
                return ToolResponse.Error("invalid_enum_value", error);
            }

            // No lower floor: ToolResponse.Paged is the authoritative clamp and
            // its contract says a non-positive limit yields zero rows, never one
            // and never "unbounded". A second, disagreeing clamp here is exactly
            // the drift Paged was made authoritative to prevent.
            var effectiveLimit = Math.Min(limit, ToolResponse.MaxRows);
            var effectiveOffset = Math.Max(0, offset);

            var matches = SpellWork.DBC.DBC.SpellInfoStore.Values
                .Where(spell => Matches(spell, name, familyValue, auraValue, effectValue, targetValue, attributeValue, schoolMask))
                .OrderBy(spell => spell.ID)
                .ToList();

            var rows = matches
                .Skip(effectiveOffset)
                .Take(effectiveLimit)
                .Select(spell => (object)new Dictionary<string, object>
                {
                    ["id"] = spell.ID,
                    ["name"] = spell.Name,
                    ["summary"] = Summarise(spell),
                })
                .ToList();

            return ToolResponse.Ok(ToolResponse.Paged(rows, matches.Count, effectiveOffset, effectiveLimit));
        }

        private static bool TryResolve(string text, Type enumType, ref long? slot, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (!TryParseEnumArgument(text, enumType, out var value, out error))
                return false;

            slot = value;
            return true;
        }

        private static bool Matches(SpellInfo spell, string name, long? family, long? aura,
            long? effect, long? target, long? attribute, uint? schoolMask)
        {
            if (name != null && (spell.Name == null ||
                spell.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0))
                return false;

            if (family.HasValue && spell.SpellFamilyName != family.Value)
                return false;

            if (schoolMask.HasValue && (spell.SchoolMask & schoolMask.Value) == 0)
                return false;

            // An attribute filter names a single bit, and the bit's position
            // determines which of the fourteen Attributes words holds it. The
            // enum values in SpellAtribute are already the shifted masks, so
            // this only ever tests word 0; the other thirteen words have their
            // own enums and are reachable through decode_flags.
            if (attribute.HasValue && (spell.Attributes & (uint)attribute.Value) == 0)
                return false;

            if (aura.HasValue && !spell.Effects.Any(e => e != null && e.EffectAura == aura.Value))
                return false;

            if (effect.HasValue && !spell.Effects.Any(e => e != null && e.Effect == effect.Value))
                return false;

            if (target.HasValue && !spell.Effects.Any(e => e != null &&
                (e.ImplicitTarget[0] == target.Value || e.ImplicitTarget[1] == target.Value)))
                return false;

            return true;
        }

        // One line, cheap to compute: the first effect's type and aura, which
        // is what distinguishes near-identically-named spells in a hit list.
        private static string Summarise(SpellInfo spell)
        {
            var effect = spell.Effects.FirstOrDefault(e => e != null);
            if (effect == null)
                return "no effects";

            var description = $"{(SpellEffects)effect.Effect}";
            if (effect.EffectAura != 0)
                description += $" / {(AuraType)effect.EffectAura}";

            return description;
        }
```

Add `using SpellWork.DBC.Structures;` and whatever namespace `SpellEnums.cs` declares. Verify the enum type names — `SpellFamilyNames`, `AuraType`, `SpellEffects`, `Targets`, `SpellAtribute` (note the codebase's existing spelling with one `t`) — against `SpellEnums.cs` before compiling.

- [ ] **Step 5: Run the tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Check the scan is not pathologically slow**

Time one broad search by hand:

```bash
dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "SearchSpells_ClampsLimitToMaxRows" -- --logger "console;verbosity=detailed"
```

A single-substring scan over ~227,000 spells should complete well under a second. If it takes more than about two seconds, say so in the task report — do not add a cache or an index speculatively.

- [ ] **Step 7: Commit**

```bash
git add SpellWork.Mcp/Tools/SpellTools.cs SpellWork.Mcp.Tests/
git commit -m "feat: add the search_spells tool"
```

---

### Task 7: `get_spell_triggers` and `compare_spells`

**Files:**
- Modify: `SpellWork.Mcp/Tools/SpellTools.cs`
- Test: `SpellWork.Mcp.Tests/SpellToolsTests.cs`

**Interfaces:**
- Consumes: `SpellTools` (Tasks 5-6), `BuildTriggers`'s output shape via `Serialize(spell, context, ["triggers"])`, `DBC.SpellTriggerStore`.
- Produces: tool `get_spell_triggers(id)` returning `{id, name, triggers: [{id, name, effectIndex}], triggeredBy: [{id, name}]}`.
- Produces: tool `compare_spells(idA, idB)` returning `{a: {id, name}, b: {id, name}, differences: [{path, a, b}], identical: bool}`.

**Design note.** `get_spell_triggers` reports both directions (spec §6). The forward direction comes from the spells' own effects and carries the effect index, which the JSON `triggers` section does not; the reverse comes from `DBC.SpellTriggerStore`, built during load. `SpellTriggerStore` is a plain `Dictionary`, not a `ConcurrentDictionary` — it is written once during `DBC.LoadAsync` and only read afterwards, so concurrent reads are safe, but **nothing in this plan may write to it**.

`compare_spells` diffs the two serialized documents rather than reimplementing a field-by-field comparison. That way it automatically covers every field Task 2 added and every field a future task adds, and it cannot drift from `get_spell`.

- [ ] **Step 1: Write the failing tests**

Add to `SpellWork.Mcp.Tests/SpellToolsTests.cs`:

```csharp
        [SkippableFact]
        public async Task GetSpellTriggers_ReportsForwardDirectionWithEffectIndex()
        {
            _fixture.SkipIfNoClientData();

            var spell = SpellWork.DBC.DBC.SpellInfoStore.Values.First(s =>
                s.Effects.Any(e => e != null && e.EffectTriggerSpell != 0
                                   && SpellWork.DBC.DBC.SpellInfoStore.ContainsKey(e.EffectTriggerSpell)));

            var json = await _fixture.SpellTools.GetSpellTriggers(spell.ID);
            using var doc = JsonDocument.Parse(json);
            var triggers = doc.RootElement.GetProperty("result").GetProperty("triggers");

            Assert.True(triggers.GetArrayLength() > 0);
            var first = triggers[0];
            Assert.True(first.GetProperty("id").GetInt32() != 0);
            Assert.False(string.IsNullOrEmpty(first.GetProperty("name").GetString()));
            Assert.True(first.GetProperty("effectIndex").GetInt32() >= 0);
        }

        [SkippableFact]
        public async Task GetSpellTriggers_ReportsReverseDirection()
        {
            _fixture.SkipIfNoClientData();

            var triggeredId = SpellWork.DBC.DBC.SpellTriggerStore.Keys.First();

            var json = await _fixture.SpellTools.GetSpellTriggers(triggeredId);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("result").GetProperty("triggeredBy").GetArrayLength() > 0);
        }

        [SkippableFact]
        public async Task GetSpellTriggers_UnknownId_ReturnsNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.GetSpellTriggers(999999999);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("spell_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task CompareSpells_IdenticalIds_ReportNoDifferences()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 133);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("identical").GetBoolean());
            Assert.Equal(0, result.GetProperty("differences").GetArrayLength());
        }

        [SkippableFact]
        public async Task CompareSpells_DifferentSpells_ReportPathedDifferences()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 116);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.False(result.GetProperty("identical").GetBoolean());
            var differences = result.GetProperty("differences");
            Assert.True(differences.GetArrayLength() > 0);

            // Every difference must name where it is, or the output is a wall
            // of values with no way to act on it.
            foreach (var difference in differences.EnumerateArray())
                Assert.False(string.IsNullOrEmpty(difference.GetProperty("path").GetString()));

            // 133 Fireball is Fire, 116 Frostbolt is Frost -- the school mask
            // differing is the one difference we can name in advance.
            Assert.Contains(differences.EnumerateArray(),
                d => d.GetProperty("path").GetString().Contains("schoolMask"));
        }

        [SkippableFact]
        public async Task CompareSpells_CapsDifferenceCount()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.SpellTools.CompareSpells(133, 116);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("result").GetProperty("differences").GetArrayLength()
                <= ToolResponse.MaxRows);
        }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "GetSpellTriggers|CompareSpells"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement `GetSpellTriggers`**

Add to `SpellTools`:

```csharp
        [McpServerTool(Name = "get_spell_triggers")]
        [Description("Which spells this spell triggers (from its own effects, with the effect index) and which spells trigger it.")]
        public async Task<string> GetSpellTriggers(int id)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(id, out var spell))
                return NotFound(id);

            var triggers = spell.Effects
                .Where(effect => effect != null && effect.EffectTriggerSpell != 0)
                .Select(effect => (object)new Dictionary<string, object>
                {
                    ["id"] = effect.EffectTriggerSpell,
                    ["name"] = SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(effect.EffectTriggerSpell, out var target)
                        ? target.Name
                        : null,
                    ["effectIndex"] = effect.EffectIndex,
                })
                .ToList();

            var triggeredBy = new List<object>();
            if (SpellWork.DBC.DBC.SpellTriggerStore.TryGetValue(id, out var sources))
            {
                triggeredBy.AddRange(sources.OrderBy(sourceId => sourceId)
                    .Select(sourceId => (object)new Dictionary<string, object>
                    {
                        ["id"] = sourceId,
                        ["name"] = SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(sourceId, out var source)
                            ? source.Name
                            : null,
                    }));
            }

            return ToolResponse.Ok(new Dictionary<string, object>
            {
                ["id"] = spell.ID,
                ["name"] = spell.Name,
                ["triggers"] = triggers.Take(ToolResponse.MaxRows).ToList(),
                ["triggeredBy"] = triggeredBy.Take(ToolResponse.MaxRows).ToList(),
                ["truncated"] = triggers.Count > ToolResponse.MaxRows || triggeredBy.Count > ToolResponse.MaxRows,
            });
        }
```

A `name` of `null` here is deliberate and meaningful: it says the effect points at a spell id that does not exist in this client data, which is a real data problem the caller should see rather than one the tool hides.

- [ ] **Step 4: Implement `CompareSpells`**

```csharp
        [McpServerTool(Name = "compare_spells")]
        [Description("Structured field-by-field diff of two spells. Returns each differing JSON path with both values.")]
        public async Task<string> CompareSpells(int idA, int idB)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(idA, out var spellA))
                return NotFound(idA);
            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(idB, out var spellB))
                return NotFound(idB);

            var context = ResolveContext(null, null, 0);

            // Diff the serialized documents rather than comparing fields by
            // hand: this covers every field get_spell emits, including ones
            // added later, and cannot drift away from what get_spell returns.
            // "context" is excluded -- both sides render at the same context
            // by construction, so it can only ever produce noise.
            var sections = new[]
            {
                "misc", "attributes", "effects", "powers", "targets", "reagents", "aura", "procs",
                "scaling", "cooldowns", "levels", "interrupts", "shapeshift", "equippedItems",
            };

            using var documentA = System.Text.Json.JsonDocument.Parse(
                SpellInfoJsonSerializer.Serialize(spellA, context, sections));
            using var documentB = System.Text.Json.JsonDocument.Parse(
                SpellInfoJsonSerializer.Serialize(spellB, context, sections));

            var differences = new List<object>();
            // Collect one past the cap, not exactly the cap: MaxRows
            // differences could mean "that is genuinely every difference" or
            // "the recursion stopped there and more were never inspected",
            // and those two situations must not report the same way. The
            // (MaxRows + 1)th difference, if the recursion ever reaches it, is
            // the probe that tells them apart -- it is never shown to the
            // caller, only used to answer "was anything withheld".
            Diff("", documentA.RootElement, documentB.RootElement, differences, ToolResponse.MaxRows + 1);

            var truncated = differences.Count > ToolResponse.MaxRows;

            var result = new Dictionary<string, object>
            {
                ["a"] = new Dictionary<string, object> { ["id"] = spellA.ID, ["name"] = spellA.Name },
                ["b"] = new Dictionary<string, object> { ["id"] = spellB.ID, ["name"] = spellB.Name },
                ["identical"] = differences.Count == 0,
                ["differences"] = differences.Take(ToolResponse.MaxRows).ToList(),
                ["truncated"] = truncated,
            };

            // Only meaningful as a true total when nothing was withheld: once
            // truncated is true, differences.Count is capped at MaxRows + 1 by
            // the probe above -- an artefact of how far the recursion was
            // allowed to run, not the real number of differences, which was
            // never counted. Reporting that under a name that says "total"
            // would be actively misleading, so the field is omitted rather
            // than mislabelled: it appears only when its value is exact.
            if (!truncated)
                result["totalDifferences"] = differences.Count;

            return ToolResponse.Ok(result);
        }

        private static void Diff(string path, System.Text.Json.JsonElement a, System.Text.Json.JsonElement b,
            List<object> differences, int cap)
        {
            // Stop descending once the cap is reached: a deep recursion that
            // collects 50,000 differences to then discard all but 500 wastes
            // the time of the caller waiting on it. The caller decides what
            // "reached" means -- CompareSpells passes MaxRows + 1 so the one
            // extra difference it allows through can serve as the truncation
            // probe described where Diff is called.
            if (differences.Count >= cap)
                return;

            if (a.ValueKind != b.ValueKind)
            {
                differences.Add(Difference(path, a, b));
                return;
            }

            switch (a.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    var keys = a.EnumerateObject().Select(p => p.Name)
                        .Union(b.EnumerateObject().Select(p => p.Name))
                        .OrderBy(name => name, StringComparer.Ordinal);
                    foreach (var key in keys)
                    {
                        // The check at the top of this method runs once per
                        // call. Without also checking here, a single object
                        // whose keys are mostly one-sided adds every one of
                        // them in this same loop, unboundedly, before the
                        // check is reconsidered -- the recursive Diff call
                        // below is never reached for a missing key, so its own
                        // entry check gets no chance to stop this branch.
                        if (differences.Count >= cap)
                            break;

                        var childPath = path.Length == 0 ? key : path + "." + key;
                        var hasA = a.TryGetProperty(key, out var childA);
                        var hasB = b.TryGetProperty(key, out var childB);

                        if (!hasA || !hasB)
                        {
                            differences.Add(new Dictionary<string, object>
                            {
                                ["path"] = childPath,
                                ["a"] = hasA ? childA.ToString() : null,
                                ["b"] = hasB ? childB.ToString() : null,
                            });
                            continue;
                        }

                        Diff(childPath, childA, childB, differences, cap);
                    }
                    break;

                case System.Text.Json.JsonValueKind.Array:
                    var arrayA = a.EnumerateArray().ToList();
                    var arrayB = b.EnumerateArray().ToList();
                    for (var i = 0; i < Math.Max(arrayA.Count, arrayB.Count); i++)
                    {
                        // Same reason as the object loop above: a length
                        // mismatch adds one difference per surplus index
                        // without ever recursing, so this loop must test the
                        // cap itself.
                        if (differences.Count >= cap)
                            break;

                        var childPath = $"{path}[{i}]";
                        if (i >= arrayA.Count || i >= arrayB.Count)
                        {
                            differences.Add(new Dictionary<string, object>
                            {
                                ["path"] = childPath,
                                ["a"] = i < arrayA.Count ? arrayA[i].ToString() : null,
                                ["b"] = i < arrayB.Count ? arrayB[i].ToString() : null,
                            });
                            continue;
                        }

                        Diff(childPath, arrayA[i], arrayB[i], differences, cap);
                    }
                    break;

                default:
                    if (!string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal))
                        differences.Add(Difference(path, a, b));
                    break;
            }
        }

        private static Dictionary<string, object> Difference(string path,
            System.Text.Json.JsonElement a, System.Text.Json.JsonElement b) =>
            new Dictionary<string, object>
            {
                ["path"] = path,
                ["a"] = a.ToString(),
                ["b"] = b.ToString(),
            };
```

Array diffing is positional. That is correct for `effects` (index 0 is index 0) and acceptable elsewhere; do not add fuzzy matching.

- [ ] **Step 5: Run the tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add SpellWork.Mcp/Tools/SpellTools.cs SpellWork.Mcp.Tests/
git commit -m "feat: add the get_spell_triggers and compare_spells tools"
```

---

### Task 8: `decode_flags` and `list_enum`

The two tools that let the agent resolve names to numbers without guessing. Neither needs client data — they read `SpellEnums.cs` through reflection — so both must work even when the DBC path is broken.

**Files:**
- Create: `SpellWork.Mcp/EnumCatalog.cs`, `SpellWork.Mcp/Tools/DecoderTools.cs`
- Test: `SpellWork.Mcp.Tests/DecoderToolsTests.cs`

**Interfaces:**
- Consumes: `ToolResponse` (Task 3).
- Produces: `SpellWork.Mcp.EnumCatalog` with `static IReadOnlyDictionary<string, Type> ByName`, `static bool TryResolve(string name, out Type type)`, `static IReadOnlyList<string> Names`.
- Produces: tool `decode_flags(kind, value)` returning `{kind, value, flags: [{bit, value, name}], unmatchedBits: int[]}`.
- Produces: tool `list_enum(name, filter)` returning `{name, isFlags, members: [{name, value}], count, truncated}`.

**Design note.** `EnumCatalog` reflects over the assembly containing `SpellEnums.cs` and collects every public enum. Building the catalogue by reflection rather than a hand-written list means a new enum added to `SpellEnums.cs` is decodable the day it lands. Name lookup is case-insensitive.

`decode_flags` decomposes per bit — never `Enum.GetName(type, value)` on a combined value, which returns null for any value that is not exactly one declared member and makes a `.ToString()` fallback print a bare decimal. `unmatchedBits` reports set bits with no declared member, so an unknown flag is visible rather than silently dropped. `SpellInfoJsonSerializer.BuildFlagList` already does exactly this decomposition — read it and match its semantics; if the two disagree about a mask, they will disagree in a bug report.

- [ ] **Step 1: Write the failing tests**

Create `SpellWork.Mcp.Tests/DecoderToolsTests.cs`:

```csharp
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // No client data needed: these read SpellEnums.cs through reflection, so
    // they are plain Facts and must pass on a machine with no DBC path set.
    public class DecoderToolsTests
    {
        private readonly DecoderTools _tools = new DecoderTools(
            new SpellDataService(McpServerConfig.FromEnvironment(
                new System.Collections.Generic.Dictionary<string, string>()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SpellDataService>.Instance));

        [Fact]
        public async Task DecodeFlags_SchoolMask_SplitsEveryBit()
        {
            // SPELL_SCHOOL_MASK_PHYSICAL (1) | SPELL_SCHOOL_MASK_FIRE (4) = 5
            var json = await _tools.DecodeFlags("SpellSchoolMask", 5);
            using var doc = JsonDocument.Parse(json);
            var flags = doc.RootElement.GetProperty("result").GetProperty("flags");

            Assert.Equal(2, flags.GetArrayLength());
            Assert.Contains(flags.EnumerateArray(), f => f.GetProperty("value").GetInt64() == 1);
            Assert.Contains(flags.EnumerateArray(), f => f.GetProperty("value").GetInt64() == 4);
            foreach (var flag in flags.EnumerateArray())
                Assert.False(string.IsNullOrEmpty(flag.GetProperty("name").GetString()));
        }

        // The failure mode this tool exists to avoid: a combined value must
        // never come back as a bare decimal from a ToString() fallback.
        [Fact]
        public async Task DecodeFlags_NeverReturnsABareNumberAsAName()
        {
            var json = await _tools.DecodeFlags("SpellSchoolMask", 5);
            using var doc = JsonDocument.Parse(json);

            foreach (var flag in doc.RootElement.GetProperty("result").GetProperty("flags").EnumerateArray())
                Assert.False(long.TryParse(flag.GetProperty("name").GetString(), out _));
        }

        // An undeclared bit is a data or version problem. Reporting it is the
        // whole difference between "this mask means nothing here" and a
        // silently shorter flag list.
        [Fact]
        public async Task DecodeFlags_ReportsUnmatchedBits()
        {
            var json = await _tools.DecodeFlags("SpellSchoolMask", 1u << 30);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.Equal(0, result.GetProperty("flags").GetArrayLength());
            Assert.Contains(result.GetProperty("unmatchedBits").EnumerateArray(), b => b.GetInt32() == 30);
        }

        [Fact]
        public async Task DecodeFlags_ZeroValue_ReturnsEmptyFlagsNotAnError()
        {
            var json = await _tools.DecodeFlags("SpellSchoolMask", 0);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(0, doc.RootElement.GetProperty("result").GetProperty("flags").GetArrayLength());
        }

        [Fact]
        public async Task DecodeFlags_UnknownKind_ListsWhatIsAvailable()
        {
            var json = await _tools.DecodeFlags("NotAnEnum", 1);
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("unknown_enum", error.GetProperty("code").GetString());
            Assert.True(error.GetProperty("details").GetProperty("availableKinds").GetArrayLength() > 0);
        }

        [Fact]
        public async Task ListEnum_RoundTripsWithDecodeFlags()
        {
            var listed = await _tools.ListEnum("SpellSchoolMask");
            using var doc = JsonDocument.Parse(listed);

            foreach (var member in doc.RootElement.GetProperty("result").GetProperty("members").EnumerateArray())
            {
                var value = member.GetProperty("value").GetInt64();
                if (value == 0 || (value & (value - 1)) != 0)
                    continue; // zero members and composite members are not single bits

                var decoded = await _tools.DecodeFlags("SpellSchoolMask", (ulong)value);
                using var decodedDoc = JsonDocument.Parse(decoded);
                var names = decodedDoc.RootElement.GetProperty("result").GetProperty("flags")
                    .EnumerateArray().Select(f => f.GetProperty("name").GetString()).ToList();

                Assert.Contains(member.GetProperty("name").GetString(), names);
            }
        }

        [Fact]
        public async Task ListEnum_FilterIsCaseInsensitiveSubstring()
        {
            var json = await _tools.ListEnum("SpellEffects", filter: "heal");
            using var doc = JsonDocument.Parse(json);
            var members = doc.RootElement.GetProperty("result").GetProperty("members");

            Assert.True(members.GetArrayLength() > 0);
            foreach (var member in members.EnumerateArray())
                Assert.Contains("HEAL", member.GetProperty("name").GetString().ToUpperInvariant());
        }

        [Fact]
        public async Task ListEnum_UnknownName_ListsWhatIsAvailable()
        {
            var json = await _tools.ListEnum("NotAnEnum");
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("unknown_enum", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [Fact]
        public void Catalog_ContainsTheEnumsTheSpecNames()
        {
            foreach (var name in new[] { "SpellSchoolMask", "SpellEffects", "AuraType", "Targets", "SpellFamilyNames" })
                Assert.True(EnumCatalog.TryResolve(name, out _), $"{name} is missing from the catalogue.");
        }

        // Spec section 10 asks for coverage of *every* decode_flags kind, not
        // a sample. The catalogue is the kind list, so drive the test from it:
        // an enum added to SpellEnums.cs is covered the day it lands, and one
        // that decodes into bare numbers fails here rather than in use.
        public static TheoryData<string> EveryKind()
        {
            var data = new TheoryData<string>();
            foreach (var name in EnumCatalog.Names)
                data.Add(name);
            return data;
        }

        [Theory]
        [MemberData(nameof(EveryKind))]
        public async Task DecodeFlags_EveryKind_DecodesItsOwnDeclaredBits(string kind)
        {
            Assert.True(EnumCatalog.TryResolve(kind, out var enumType));

            foreach (var member in System.Enum.GetNames(enumType))
            {
                var value = unchecked((ulong)System.Convert.ToInt64(System.Enum.Parse(enumType, member)));

                // Only single-bit members round-trip through a per-bit
                // decoder. Zero members and composite members (masks that
                // combine other members) are excluded by construction, not
                // skipped for convenience.
                if (value == 0 || (value & (value - 1)) != 0)
                    continue;

                var json = await _tools.DecodeFlags(kind, value);
                using var doc = JsonDocument.Parse(json);
                var result = doc.RootElement.GetProperty("result");

                Assert.Equal(0, result.GetProperty("unmatchedBits").GetArrayLength());
                var names = result.GetProperty("flags").EnumerateArray()
                    .Select(f => f.GetProperty("name").GetString()).ToList();

                Assert.Single(names);
                // Aliases are legal -- two members may share a value -- so
                // assert the decoded name is *a* member with this value, not
                // that it is this particular spelling.
                Assert.Equal(value,
                    unchecked((ulong)System.Convert.ToInt64(System.Enum.Parse(enumType, names[0]))));
            }
        }

        [Theory]
        [MemberData(nameof(EveryKind))]
        public async Task ListEnum_EveryKind_ReturnsItsMembers(string kind)
        {
            var json = await _tools.ListEnum(kind);
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.GetProperty("ok").GetBoolean(), $"list_enum failed for {kind}");
            Assert.Equal(kind, doc.RootElement.GetProperty("result").GetProperty("name").GetString());
        }
    }
}
```

Every enum name above must be checked against `SpellEnums.cs` before you write the implementation — `grep -n "public enum" SpellWork.Core/Spell/SpellEnums.cs`. Fix the test to the real names; do not rename the enums.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~DecoderToolsTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement `EnumCatalog`**

Create `SpellWork.Mcp/EnumCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

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
            foreach (var type in typeof(SpellWork.Spell.SpellInfo).Assembly.GetTypes())
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
```

- [ ] **Step 4: Implement the tools**

Create `SpellWork.Mcp/Tools/DecoderTools.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class DecoderTools
    {
        private readonly SpellDataService _data;

        public DecoderTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "decode_flags")]
        [Description("Split a bitmask into its named flags. kind is an enum name such as SpellSchoolMask, SpellAtribute or ProcFlags — call list_enum with no filter to see every available kind. Set bits with no declared member are reported in unmatchedBits.")]
        public Task<string> DecodeFlags(string kind, ulong value)
        {
            if (!EnumCatalog.TryResolve(kind, out var enumType))
                return Task.FromResult(UnknownEnum(kind));

            var flags = new List<object>();
            var unmatched = new List<object>();

            for (var bit = 0; bit < 64; bit++)
            {
                var mask = 1UL << bit;
                if ((value & mask) == 0)
                    continue;

                // Per bit, never Enum.GetName on the combined value: on a
                // [Flags] enum that returns null for anything that is not
                // exactly one declared member, and the usual ToString()
                // fallback then prints a bare decimal that reads like a name.
                var name = FindMemberName(enumType, mask);
                if (name == null)
                {
                    unmatched.Add(bit);
                    continue;
                }

                flags.Add(new Dictionary<string, object>
                {
                    ["bit"] = bit,
                    ["value"] = mask,
                    ["name"] = name,
                });
            }

            return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
            {
                ["kind"] = enumType.Name,
                ["value"] = value,
                ["flags"] = flags,
                ["unmatchedBits"] = unmatched,
            }));
        }

        [McpServerTool(Name = "list_enum")]
        [Description("The id-to-name map for one enum, so names can be resolved without guessing. Call with no name to list every available enum. filter is a case-insensitive substring match on member names.")]
        public Task<string> ListEnum(string name = null, string filter = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
                {
                    ["enums"] = EnumCatalog.Names,
                    ["count"] = EnumCatalog.Names.Count,
                }));
            }

            if (!EnumCatalog.TryResolve(name, out var enumType))
                return Task.FromResult(UnknownEnum(name));

            var members = Enum.GetNames(enumType)
                .Where(member => filter == null ||
                    member.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(member => new
                {
                    Name = member,
                    Value = Convert.ToInt64(Enum.Parse(enumType, member)),
                })
                .OrderBy(member => member.Value)
                .ToList();

            return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
            {
                ["name"] = enumType.Name,
                ["isFlags"] = enumType.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0,
                ["members"] = members
                    .Take(ToolResponse.MaxRows)
                    .Select(member => (object)new Dictionary<string, object>
                    {
                        ["name"] = member.Name,
                        ["value"] = member.Value,
                    })
                    .ToList(),
                ["count"] = members.Count,
                ["truncated"] = members.Count > ToolResponse.MaxRows,
            }));
        }

        private static string FindMemberName(Type enumType, ulong mask)
        {
            foreach (var member in Enum.GetNames(enumType))
            {
                // Convert.ToInt64 then reinterpret: an enum with a negative
                // underlying value (a signed backing type with the sign bit
                // set) must still match the unsigned mask for that bit.
                var memberValue = unchecked((ulong)Convert.ToInt64(Enum.Parse(enumType, member)));
                if (memberValue == mask)
                    return member;
            }

            return null;
        }

        private static string UnknownEnum(string requested) =>
            ToolResponse.Error("unknown_enum", $"No enum named '{requested}'.",
                new Dictionary<string, object>
                {
                    ["availableKinds"] = EnumCatalog.Names,
                });
    }
}
```

`SpellDataService` is injected but unused by these two methods — `decode_flags` and `list_enum` work without client data by design, which is why their tests are plain `Fact`s. **Do not remove the constructor parameter:** Task 9 adds `calculate_spell_scaling` to this same class and it needs the service.

`Convert.ToInt64` throws `OverflowException` on a `ulong`-backed enum member above `long.MaxValue`. If any enum in `SpellEnums.cs` has an underlying type of `ulong`, switch both `FindMemberName` and `ListEnum` to read the raw value via `Convert.ToUInt64` for those types. Check with `grep -n "public enum.*: ulong" SpellWork.Core/Spell/SpellEnums.cs` before assuming there are none.

- [ ] **Step 5: Register the tools and run**

`WithToolsFromAssembly()` finds `[McpServerToolType]` classes automatically, so no `Program.cs` change is needed. Run:

`dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`
Expected: PASS — including on a machine with no `SPELLWORK_DBC_PATH`, since none of these tests are `SkippableFact`.

- [ ] **Step 6: Confirm both tools list over stdio**

Add to `SpellWork.Mcp.Tests/StdioSmokeTests.cs`:

```csharp
        [Fact]
        public async Task ToolsList_IncludesTheDecoderTools()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());

            Assert.Contains(lines, line => line.Contains("decode_flags"));
            Assert.Contains(lines, line => line.Contains("list_enum"));
        }
```

- [ ] **Step 7: Commit**

```bash
git add SpellWork.Mcp/EnumCatalog.cs SpellWork.Mcp/Tools/DecoderTools.cs SpellWork.Mcp.Tests/
git commit -m "feat: add the decode_flags and list_enum tools"
```

---

### Task 9: `calculate_spell_scaling`

Per-effect computed values at an arbitrary level, item level and map difficulty. This is the tool that most directly needs Task 1: it exists to be called repeatedly at different levels, and before `RenderContext` there was no way to do that without mutating a global.

**Files:**
- Modify: `SpellWork.Mcp/Tools/DecoderTools.cs`
- Modify: `SpellWork.Core/Spell/SpellInfo.cs` (expose the breakdown publicly)
- Test: `SpellWork.Mcp.Tests/DecoderToolsTests.cs`

**Interfaces:**
- Consumes: `SpellInfo.CalculateEffectValues` (Task 1); `SpellTools.DataError` and `SpellTools.NotFound` (Task 5, both `internal static`). This tool builds its own `RenderContext` rather than calling `SpellTools.ResolveContext`, because `level` and `itemLevel` are required arguments here and the map-difficulty lookup additionally accepts a `mapId`.
- Produces: `public EffectValues SpellInfo.GetEffectValues(SpellEffectEntry effect, RenderContext context)` — a public projection of the `internal EffectValueBreakdown`, with the same seven members: `float BaseValue`, `float Variance`, `float ValuePerResource`, `float PerLevelValue`, `float SpellPowerCoefficient`, `float AttackPowerCoefficient`, `bool UsesExpectedStat`.
- Produces: tool `calculate_spell_scaling(id, level, itemLevel, mapId, difficulty)` returning `{id, name, level, itemLevel, mapDifficulty, effects: [{index, effect, basePoints, basePointsMin, basePointsMax, valuePerResource, perLevelValue, spellPowerCoefficient, attackPowerCoefficient, usesExpectedStat}]}`.

**Design note.** `EffectValueBreakdown` is `internal` and stays that way — it is the extraction's private arithmetic seam and the handoff records that both renderers go through it. Rather than widening it to `public`, add a public read-only projection. This keeps `SpellWork.Core.Tests`'s existing `internal` access working and gives the MCP project a stable surface that does not expose mutable public fields.

`mapId` and `difficulty` together identify a `MapDifficultyEntry`. When both are given, look up the row matching both; when only `difficulty` is given, fall back to `ResolveContext`'s by-id lookup. Report which entry was actually selected in the response so a caller can tell a silent miss from a match.

- [ ] **Step 1: Write the failing test**

Add to `SpellWork.Mcp.Tests/DecoderToolsTests.cs` (these need client data, so they are `SkippableFact` and the class gains the fixture):

```csharp
        // Add to the class: an McpDataFixture, injected the same way
        // SpellToolsTests does, plus IClassFixture<McpDataFixture>.

        [SkippableFact]
        public async Task CalculateSpellScaling_ProducesDifferentValuesPerLevel()
        {
            _fixture.SkipIfNoClientData();

            var atTwenty = await _fixture.DecoderTools.CalculateSpellScaling(606, level: 20, itemLevel: 475);
            var atOneTwenty = await _fixture.DecoderTools.CalculateSpellScaling(606, level: 120, itemLevel: 475);

            Assert.NotEqual(FirstBasePoints(atTwenty), FirstBasePoints(atOneTwenty));

            static double FirstBasePoints(string json)
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("result").GetProperty("effects")[0]
                    .GetProperty("basePoints").GetDouble();
            }
        }

        // The numbers this tool reports and the numbers get_spell reports must
        // be the same numbers. They come from one arithmetic path
        // (CalculateEffectValues) precisely so they cannot diverge; this
        // asserts that they actually do not.
        [SkippableFact]
        public async Task CalculateSpellScaling_AgreesWithGetSpell()
        {
            _fixture.SkipIfNoClientData();

            var scaling = await _fixture.DecoderTools.CalculateSpellScaling(133, level: 60, itemLevel: 475);
            var spell = await _fixture.SpellTools.GetSpell(133, level: 60, itemLevel: 475);

            using var scalingDoc = JsonDocument.Parse(scaling);
            using var spellDoc = JsonDocument.Parse(spell);

            var fromScaling = scalingDoc.RootElement.GetProperty("result").GetProperty("effects")[0];
            var fromSpell = spellDoc.RootElement.GetProperty("result").GetProperty("effects")[0];

            // Every value field, not just basePoints: the two tools emit
            // through separately-written code and the only thing stopping
            // them drifting is that both suppress on the same condition.
            // Assert that, or the drift is invisible until someone reports it.
            foreach (var field in new[]
            {
                "basePoints", "basePointsMin", "basePointsMax", "valuePerResource",
                "perLevelValue", "spellPowerCoefficient", "attackPowerCoefficient", "usesExpectedStat",
            })
            {
                var inScaling = fromScaling.TryGetProperty(field, out var scalingValue);
                var inSpell = fromSpell.TryGetProperty(field, out var spellValue);

                Assert.True(inScaling == inSpell,
                    $"'{field}' is present in one tool's output and absent from the other's.");
                if (inScaling)
                    Assert.Equal(spellValue.ToString(), scalingValue.ToString());
            }
        }

        [SkippableFact]
        public async Task CalculateSpellScaling_ReportsTheResolvedMapDifficulty()
        {
            _fixture.SkipIfNoClientData();

            var difficultyId = SpellWork.DBC.DBC.MapDifficulty.Keys.First();
            var json = await _fixture.DecoderTools.CalculateSpellScaling(
                133, level: 120, itemLevel: 475, difficulty: (uint)difficultyId);

            using var doc = JsonDocument.Parse(json);
            var mapDifficulty = doc.RootElement.GetProperty("result").GetProperty("mapDifficulty");

            Assert.Equal(difficultyId, mapDifficulty.GetProperty("id").GetInt32());
        }

        [SkippableFact]
        public async Task CalculateSpellScaling_UnknownId_ReturnsNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.DecoderTools.CalculateSpellScaling(999999999, level: 60, itemLevel: 475);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("spell_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
```

Add `DecoderTools DecoderTools { get; }` to `McpDataFixture`, constructed as `new DecoderTools(Data)`.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "CalculateSpellScaling"`
Expected: FAIL to compile.

- [ ] **Step 3: Add the public projection to `SpellInfo`**

In `SpellWork.Core/Spell/SpellInfo.cs`, beside `EffectValueBreakdown`:

```csharp
    /// <summary>
    /// Read-only public view of <see cref="EffectValueBreakdown"/>. The
    /// breakdown itself stays internal: it is the shared arithmetic seam that
    /// stops the GUI and the MCP server reporting different damage, and
    /// exposing its mutable fields would invite a caller to write to it.
    /// </summary>
    public sealed class EffectValues
    {
        internal EffectValues(EffectValueBreakdown breakdown)
        {
            BaseValue = breakdown.BaseValue;
            Variance = breakdown.Variance;
            ValuePerResource = breakdown.ValuePerResource;
            PerLevelValue = breakdown.PerLevelValue;
            SpellPowerCoefficient = breakdown.SpellPowerCoefficient;
            AttackPowerCoefficient = breakdown.AttackPowerCoefficient;
            UsesExpectedStat = breakdown.UsesExpectedStat;
        }

        public float BaseValue { get; }
        public float Variance { get; }
        public float ValuePerResource { get; }
        public float PerLevelValue { get; }
        public float SpellPowerCoefficient { get; }
        public float AttackPowerCoefficient { get; }
        public bool UsesExpectedStat { get; }
    }
```

And on `SpellInfo`:

```csharp
        public EffectValues GetEffectValues(SpellEffectEntry effect, RenderContext context) =>
            new EffectValues(CalculateEffectValues(effect, context));
```

- [ ] **Step 4: Implement the tool**

Add to `SpellWork.Mcp/Tools/DecoderTools.cs`:

```csharp
        [McpServerTool(Name = "calculate_spell_scaling")]
        [Description("Per-effect computed values at a specific level, item level and map difficulty. Use this to see how a spell's numbers change with level rather than reading get_spell repeatedly.")]
        public async Task<string> CalculateSpellScaling(
            int id,
            uint level,
            uint itemLevel,
            uint? mapId = null,
            uint difficulty = 0)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            if (!SpellWork.DBC.DBC.SpellInfoStore.TryGetValue(id, out var spell))
                return SpellTools.NotFound(id);

            var mapDifficulty = ResolveMapDifficulty(mapId, difficulty);
            var context = new SpellWork.Spell.RenderContext(level, itemLevel, mapDifficulty);

            var effects = new List<object>();
            foreach (var effect in spell.Effects)
            {
                if (effect == null)
                    continue;

                var values = spell.GetEffectValues(effect, context);
                var entry = new Dictionary<string, object>
                {
                    ["index"] = effect.EffectIndex,
                    ["effect"] = new Dictionary<string, object>
                    {
                        ["raw"] = effect.Effect,
                        ["name"] = Enum.GetName(typeof(SpellWork.Spell.SpellEffects), effect.Effect),
                    },
                    ["basePoints"] = values.BaseValue,
                };

                if (Math.Abs(values.Variance) > 1.0E-5f)
                {
                    var delta = Math.Abs(values.BaseValue * values.Variance * 0.5f);
                    entry["basePointsMin"] = values.BaseValue - delta;
                    entry["basePointsMax"] = values.BaseValue + delta;
                }

                // Emitted on exactly the conditions BuildEffects uses, so
                // this tool and get_spell cannot report the same effect
                // differently. BuildEffects suppresses these four through
                // AddIfNonZero, which tests against exact zero -- match that,
                // do not substitute a 1.0E-5 epsilon here. A third threshold
                // variant in a third place is how the GUI and the JSON path
                // drifted apart during Plan 1.
                if (values.ValuePerResource != 0)
                    entry["valuePerResource"] = values.ValuePerResource;
                if (values.PerLevelValue != 0)
                    entry["perLevelValue"] = values.PerLevelValue;
                if (values.SpellPowerCoefficient != 0)
                    entry["spellPowerCoefficient"] = values.SpellPowerCoefficient;
                if (values.AttackPowerCoefficient != 0)
                    entry["attackPowerCoefficient"] = values.AttackPowerCoefficient;
                if (values.UsesExpectedStat)
                    entry["usesExpectedStat"] = true;

                effects.Add(entry);
            }

            var payload = new Dictionary<string, object>
            {
                ["id"] = spell.ID,
                ["name"] = spell.Name,
                ["level"] = level,
                ["itemLevel"] = itemLevel,
                ["effects"] = effects,
            };

            // Reported so a caller can tell "I asked for difficulty 14 and got
            // it" from "I asked for difficulty 14, nothing matched, and these
            // numbers are for no difficulty at all".
            payload["mapDifficulty"] = mapDifficulty == null
                ? null
                : new Dictionary<string, object>
                {
                    ["id"] = mapDifficulty.ID,
                    ["mapId"] = mapDifficulty.MapID,
                    ["difficultyId"] = mapDifficulty.DifficultyID,
                    ["contentTuningId"] = mapDifficulty.ContentTuningID,
                };

            return ToolResponse.Ok(payload);
        }

        private static SpellWork.DBC.Structures.MapDifficultyEntry ResolveMapDifficulty(uint? mapId, uint difficulty)
        {
            if (mapId.HasValue)
            {
                return SpellWork.DBC.DBC.MapDifficulty.Values.FirstOrDefault(entry =>
                    entry.MapID == mapId.Value &&
                    (difficulty == 0 || entry.DifficultyID == difficulty));
            }

            if (difficulty == 0)
                return null;

            SpellWork.DBC.DBC.MapDifficulty.TryGetValue((int)difficulty, out var byId);
            return byId;
        }
```

`SpellTools.DataError` and `SpellTools.NotFound` are `internal static` on `SpellTools` (Task 5) — they are reused here rather than duplicated so all three tools produce byte-identical error shapes. If they are `private`, widen them to `internal static`.

`Enum.GetName` is safe here because `SpellEffects` is not a `[Flags]` enum and `effect.Effect` is always exactly one member. Do not copy this pattern to a mask field — that is what `decode_flags` is for.

- [ ] **Step 5: Run the tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj` and `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`
Expected: PASS both, goldens still green.

- [ ] **Step 6: Commit**

```bash
git add SpellWork.Core/Spell/SpellInfo.cs SpellWork.Mcp/Tools/DecoderTools.cs SpellWork.Mcp.Tests/
git commit -m "feat: add the calculate_spell_scaling tool"
```

---

### Task 10: `list_db2`, `get_db2_row` and `query_db2`

Raw access to the DB2 stores. Spec §6.1 is the constraint that shapes this task: SpellWork models 47 of the 715 `.db2` files. Modelled stores return real field names; the rest come back positionally as `field_0`, `field_1`, … and **the response must say so per row** rather than presenting positional data as if it were named.

**Files:**
- Create: `SpellWork.Mcp/Tools/Db2Tools.cs`
- Test: `SpellWork.Mcp.Tests/Db2ToolsTests.cs`

**Interfaces:**
- Consumes: `SpellDataService`, `ToolResponse`, `SpellTools.DataError`.
- Produces: tool `list_db2()` returning `{stores: [{name, modelled, loaded, rowCount}], modelledCount, presentCount}`.
- Produces: tool `get_db2_row(store, id)` returning `{store, id, modelled, fieldNamesAvailable, row: {…}}`.
- Produces: tool `query_db2(store, filter, limit, offset)` returning the `ToolResponse.Paged` shape with a `modelled` and `fieldNamesAvailable` flag alongside.

**Design note.** This task covers only the *modelled* stores plus a directory listing of the unmodelled ones. Reading an unmodelled `.db2` through `DBFileReaderLib` positionally is possible but is a second, larger piece of work — the spec records adopting WoWDBDefs as a §11 follow-up, and it shares groundwork with the visual chain in Plan 3. So `list_db2` reports every `.db2` file present with a `modelled` flag, and `get_db2_row` / `query_db2` return a structured `store_not_modelled` error for the other 668, naming the limitation and pointing at §6.1. **Do not** silently return `field_0`-style rows for stores this task cannot actually read — an empty positional row is worse than a clear refusal.

`filter` on `query_db2` is `field=value` pairs joined by commas, e.g. `"SpellID=133,DifficultyID=0"`. Comparison is by the field's string form, case-insensitively. No operators, no expressions: a filter language invented here is one an agent will get wrong, and `query_world_db` already exists for anything richer.

- [ ] **Step 1: Write the failing tests**

Create `SpellWork.Mcp.Tests/Db2ToolsTests.cs`:

```csharp
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class Db2ToolsTests : IClassFixture<McpDataFixture>
    {
        private readonly McpDataFixture _fixture;

        public Db2ToolsTests(McpDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public async Task ListDb2_FlagsModelledAndUnmodelledStores()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.ListDb2();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");
            var stores = result.GetProperty("stores").EnumerateArray().ToList();

            Assert.Contains(stores, s => s.GetProperty("name").GetString() == "SpellName"
                                         && s.GetProperty("modelled").GetBoolean());
            Assert.True(result.GetProperty("modelledCount").GetInt32() > 0);
        }

        [SkippableFact]
        public async Task GetDb2Row_ModelledStore_ReturnsNamedFields()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.GetDb2Row("SpellName", 133);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("fieldNamesAvailable").GetBoolean());
            Assert.Equal("Fireball", result.GetProperty("row").GetProperty("Name").GetString());
        }

        [SkippableFact]
        public async Task GetDb2Row_UnknownStore_ListsAvailableStores()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.GetDb2Row("NotAStore", 1);
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("unknown_store", error.GetProperty("code").GetString());
            Assert.True(error.GetProperty("details").GetProperty("modelledStores").GetArrayLength() > 0);
        }

        [SkippableFact]
        public async Task GetDb2Row_MissingId_ReturnsRowNotFound()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.GetDb2Row("SpellName", 999999999);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("row_not_found", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [SkippableFact]
        public async Task QueryDb2_FilterMatchesOnFieldValue()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.QueryDb2("SpellEffect", filter: "SpellID=133");
            using var doc = JsonDocument.Parse(json);
            var rows = doc.RootElement.GetProperty("result").GetProperty("rows").EnumerateArray().ToList();

            Assert.NotEmpty(rows);
            foreach (var row in rows)
                Assert.Equal(133, row.GetProperty("SpellID").GetInt32());
        }

        [SkippableFact]
        public async Task QueryDb2_ClampsLimitToMaxRows()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.QueryDb2("SpellName", limit: 10000);
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("result");

            Assert.True(result.GetProperty("rows").GetArrayLength() <= ToolResponse.MaxRows);
            Assert.True(result.GetProperty("truncated").GetBoolean());
        }

        [SkippableFact]
        public async Task QueryDb2_UnknownFilterField_ReturnsStructuredError()
        {
            _fixture.SkipIfNoClientData();

            var json = await _fixture.Db2Tools.QueryDb2("SpellName", filter: "NotAField=1");
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("unknown_field", error.GetProperty("code").GetString());
            Assert.True(error.GetProperty("details").GetProperty("availableFields").GetArrayLength() > 0);
        }

        // Spec 6.1: a store SpellWork does not model must produce an explicit
        // refusal naming the limitation, not empty positional rows presented
        // as if they were data.
        [SkippableFact]
        public async Task QueryDb2_UnmodelledStore_SaysSoExplicitly()
        {
            _fixture.SkipIfNoClientData();

            var listed = await _fixture.Db2Tools.ListDb2();
            using var listedDoc = JsonDocument.Parse(listed);
            var unmodelled = listedDoc.RootElement.GetProperty("result").GetProperty("stores")
                .EnumerateArray().FirstOrDefault(s => !s.GetProperty("modelled").GetBoolean());

            Skip.If(unmodelled.ValueKind == JsonValueKind.Undefined,
                "No unmodelled .db2 files present in the configured DBC path.");

            var json = await _fixture.Db2Tools.QueryDb2(unmodelled.GetProperty("name").GetString());
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("store_not_modelled", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }
}
```

Add `Db2Tools Db2Tools { get; }` to `McpDataFixture`.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~Db2ToolsTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Implement `Db2Tools`**

Create `SpellWork.Mcp/Tools/Db2Tools.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class Db2Tools
    {
        private readonly SpellDataService _data;

        public Db2Tools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "list_db2")]
        [Description("Every .db2 file present in the configured DBC path, flagged as modelled (real field names available) or unmodelled. Only modelled stores can be read with get_db2_row and query_db2.")]
        public async Task<string> ListDb2()
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            var modelled = ModelledStores();
            var stores = new List<object>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in modelled.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                seen.Add(pair.Key);
                stores.Add(new Dictionary<string, object>
                {
                    ["name"] = pair.Key,
                    ["modelled"] = true,
                    ["loaded"] = pair.Value != null,
                    ["rowCount"] = (pair.Value as ICollection)?.Count ?? 0,
                });
            }

            var directory = Path.Combine(_data.Config.DbcPath, _data.Config.Locale);
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory, "*.db2").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!seen.Add(name))
                        continue;

                    stores.Add(new Dictionary<string, object>
                    {
                        ["name"] = name,
                        ["modelled"] = false,
                        ["loaded"] = false,
                        ["rowCount"] = 0,
                    });
                }
            }

            return ToolResponse.Ok(new Dictionary<string, object>
            {
                ["stores"] = stores,
                ["modelledCount"] = modelled.Count,
                ["presentCount"] = stores.Count,
                ["note"] = "Unmodelled stores expose fields only positionally and are not readable by get_db2_row or query_db2 in this version.",
            });
        }

        [McpServerTool(Name = "get_db2_row")]
        [Description("One row from a modelled DB2 store, by id, with real field names. Call list_db2 for the available stores.")]
        public async Task<string> GetDb2Row(string store, int id)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            if (!TryGetStore(store, out var name, out var storage, out var error))
                return error;

            var dictionary = (IDictionary)storage;
            if (!dictionary.Contains(id))
            {
                return ToolResponse.Error("row_not_found", $"Store '{name}' has no row with id {id}.",
                    new Dictionary<string, object> { ["store"] = name, ["rowCount"] = dictionary.Count });
            }

            return ToolResponse.Ok(new Dictionary<string, object>
            {
                ["store"] = name,
                ["id"] = id,
                ["modelled"] = true,
                ["fieldNamesAvailable"] = true,
                ["row"] = ToRow(dictionary[id]),
            });
        }

        [McpServerTool(Name = "query_db2")]
        [Description("Rows from a modelled DB2 store. filter is comma-separated field=value pairs, e.g. \"SpellID=133,DifficultyID=0\"; values are compared as case-insensitive strings. No operators or expressions.")]
        public async Task<string> QueryDb2(string store, string filter = null, int limit = 50, int offset = 0)
        {
            var data = await _data.GetAsync();
            if (!data.Loaded)
                return SpellTools.DataError(data);

            if (!TryGetStore(store, out var name, out var storage, out var error))
                return error;

            var dictionary = (IDictionary)storage;
            var recordType = storage.GetType().GetGenericArguments()[0];
            var fields = RowMembers(recordType).ToList();

            var predicates = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                foreach (var clause in filter.Split(','))
                {
                    var parts = clause.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                    {
                        return ToolResponse.Error("invalid_filter",
                            $"Filter clause '{clause}' is not of the form field=value.");
                    }

                    var field = parts[0].Trim();
                    if (!fields.Any(member => string.Equals(member, field, StringComparison.OrdinalIgnoreCase)))
                    {
                        return ToolResponse.Error("unknown_field",
                            $"Store '{name}' has no field '{field}'.",
                            new Dictionary<string, object> { ["availableFields"] = fields });
                    }

                    predicates.Add(new KeyValuePair<string, string>(field, parts[1].Trim()));
                }
            }

            // No lower floor: ToolResponse.Paged is the authoritative clamp and
            // its contract says a non-positive limit yields zero rows, never one
            // and never "unbounded". A second, disagreeing clamp here is exactly
            // the drift Paged was made authoritative to prevent.
            var effectiveLimit = Math.Min(limit, ToolResponse.MaxRows);
            var effectiveOffset = Math.Max(0, offset);

            var matched = new List<object>();
            var total = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                var row = ToRow(entry.Value);
                if (!predicates.All(predicate => Matches(row, predicate)))
                    continue;

                total++;
                if (total <= effectiveOffset || matched.Count >= effectiveLimit)
                    continue;

                matched.Add(row);
            }

            var payload = ToolResponse.Paged(matched, total, effectiveOffset, effectiveLimit);
            payload["store"] = name;
            payload["modelled"] = true;
            payload["fieldNamesAvailable"] = true;
            return ToolResponse.Ok(payload);
        }

        private static bool Matches(Dictionary<string, object> row, KeyValuePair<string, string> predicate)
        {
            var key = row.Keys.FirstOrDefault(k => string.Equals(k, predicate.Key, StringComparison.OrdinalIgnoreCase));
            if (key == null)
                return false;

            var value = row[key];
            return string.Equals(FormatValue(value), predicate.Value, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;

            // An array field compares against its comma-joined form so a
            // filter can still address it, rather than matching on the CLR
            // type name that ToString() would produce.
            if (value is IEnumerable enumerable && !(value is string))
                return string.Join(",", enumerable.Cast<object>().Select(FormatValue));

            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> ToRow(object record)
        {
            var row = new Dictionary<string, object>();
            if (record == null)
                return row;

            foreach (var field in record.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                row[field.Name] = field.GetValue(record);

            foreach (var property in record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length == 0)
                    row[property.Name] = property.GetValue(record);
            }

            return row;
        }

        private static IEnumerable<string> RowMembers(Type recordType) =>
            recordType.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name)
                .Concat(recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0).Select(p => p.Name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal);

        private static Dictionary<string, object> ModelledStores()
        {
            var stores = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in typeof(SpellWork.DBC.DBC).GetProperties(BindingFlags.Static | BindingFlags.Public))
            {
                if (!property.PropertyType.IsGenericType ||
                    property.PropertyType.GetGenericTypeDefinition() != typeof(Storage<>))
                    continue;

                stores[property.Name] = property.GetValue(null);
            }

            return stores;
        }

        private bool TryGetStore(string requested, out string name, out object storage, out string error)
        {
            name = null;
            storage = null;
            error = null;

            var modelled = ModelledStores();
            var match = modelled.Keys.FirstOrDefault(key =>
                string.Equals(key, requested, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                // Distinguish "no such file" from "present but not modelled":
                // the second is a known limitation with a documented reason,
                // and telling the caller "unknown store" for it sends them
                // looking for a typo that is not there.
                var path = Path.Combine(_data.Config.DbcPath ?? string.Empty, _data.Config.Locale ?? string.Empty,
                    (requested ?? string.Empty) + ".db2");

                error = File.Exists(path)
                    ? ToolResponse.Error("store_not_modelled",
                        $"'{requested}.db2' is present but SpellWork does not model it, so its fields are only " +
                        "available positionally. Reading unmodelled stores is not supported in this version.",
                        new Dictionary<string, object> { ["store"] = requested })
                    : ToolResponse.Error("unknown_store", $"No store named '{requested}'.",
                        new Dictionary<string, object>
                        {
                            ["modelledStores"] = modelled.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                        });
                return false;
            }

            if (modelled[match] == null)
            {
                error = ToolResponse.Error("store_not_loaded",
                    $"Store '{match}' is modelled but was not loaded — its .db2 file is missing from " +
                    $"'{_data.Config.DbcPath}\\{_data.Config.Locale}'.",
                    new Dictionary<string, object> { ["variable"] = "SPELLWORK_DBC_PATH" });
                return false;
            }

            name = match;
            storage = modelled[match];
            return true;
        }
    }
}
```

`Storage<T>` lives in `DBFileReaderLib` — add the `using` the compiler asks for, and confirm it implements `IDictionary` (it is used as `DBC.SpellRadius[(int)radiusIndex]` and `.Values` / `.TryGetValue` throughout `SpellInfo.cs`, so it does; if it implements only `IDictionary<int,T>` and not the non-generic `IDictionary`, cast to `IEnumerable` and enumerate `KeyValuePair<int,T>` instead).

- [ ] **Step 4: Run the tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Check `query_db2` on the largest store**

`SpellName` has roughly 227,000 rows. Time `QueryDb2("SpellName", limit: 500)` — reflection per row over a store that size is the one place in this plan where the naive approach might be too slow. If it takes more than about two seconds, report the timing in the task report rather than optimising speculatively; the fix (caching the `FieldInfo`/`PropertyInfo` arrays per type) is small and can be a follow-up.

- [ ] **Step 6: Commit**

```bash
git add SpellWork.Mcp/Tools/Db2Tools.cs SpellWork.Mcp.Tests/
git commit -m "feat: add the list_db2, get_db2_row and query_db2 tools"
```

---

### Task 11: `SqlGuard`, `query_world_db` and `get_spell_db_overrides`

**This is the one security-relevant component in the plan.** The agent composes these queries itself, so the validator is the thing standing between a model's output and a live database.

Spec §9, verbatim: *"`query_world_db` accepts a single `SELECT` statement only. Multiple statements, DDL and DML keywords are rejected before the query reaches MySQL, and the documentation directs the operator to connect as a read-only MySQL user. Both controls are required: the parser is a guard, the read-only grant is the guarantee."*

Both. The validator is not the security boundary on its own and must not be described as one — Task 12's documentation carries the read-only-grant instruction, and that is not optional.

**Files:**
- Create: `SpellWork.Mcp/SqlGuard.cs`, `SpellWork.Mcp/Tools/DatabaseTools.cs`
- Modify: `SpellWork.Core/Database/WorldDatabase.cs` (add a generic row reader)
- Test: `SpellWork.Mcp.Tests/SqlGuardTests.cs`, `SpellWork.Mcp.Tests/DatabaseToolsTests.cs`

**Interfaces:**
- Consumes: `WorldDatabase(SpellWorkConfig)`, `TestConnection(out string)`, `McpServerConfig.MySqlConfigured`, `ToolResponse`.
- Produces: `SpellWork.Mcp.SqlGuard` with `static bool TryValidate(string sql, out string error)`.
- Produces: `SpellWork.Database.WorldDatabase.SelectRows(string sql, int limit)` returning `QueryResult { List<string> Columns; List<Dictionary<string,object>> Rows; }`.
- Produces: tool `query_world_db(sql, database, limit)`; tool `get_spell_db_overrides(id)`.

**Design note.** `SqlGuard` is tested with no database anywhere near it (spec §10) — it is a pure function over a string, and every test in `SqlGuardTests` is a plain `Fact` that runs on any machine.

The validator is deliberately a whitelist-shaped rejector, not a SQL parser: strip comments, require the first keyword to be `SELECT` (or `WITH` … `SELECT`), reject any statement separator outside a string literal, and reject a fixed list of DDL/DML keywords appearing as whole words outside string literals. Anything it cannot confidently classify is rejected. A validator that guesses in the caller's favour is not a guard.

`WorldDatabase.SelectRows` uses `MySqlCommand` with the query text as given — parameterisation is not possible for an agent-composed query — which is exactly why the guard runs first and why the documented connection is a read-only grant.

- [ ] **Step 1: Write the validator tests**

Create `SpellWork.Mcp.Tests/SqlGuardTests.cs`:

```csharp
using Xunit;

namespace SpellWork.Mcp.Tests
{
    // No database, by design (spec section 10): the validator is a pure
    // function over a string and is tested independently of MySQL.
    public class SqlGuardTests
    {
        [Theory]
        [InlineData("SELECT * FROM spell_proc WHERE SpellId = 133")]
        [InlineData("select id, name from spell_dbc limit 10")]
        [InlineData("  SELECT 1  ")]
        [InlineData("SELECT * FROM spell_proc WHERE comment = 'a; DROP TABLE x'")]
        [InlineData("SELECT * FROM spell_bonus_data WHERE entry IN (1,2,3)")]
        [InlineData("WITH ranked AS (SELECT id FROM spell_ranks) SELECT * FROM ranked")]
        [InlineData("SELECT * FROM spell_proc -- trailing comment")]
        [InlineData("SELECT /* inline */ * FROM spell_proc")]
        [InlineData("SELECT * FROM spell_proc WHERE name = 'it''s fine'")]
        public void Accepts(string sql)
        {
            Assert.True(SqlGuard.TryValidate(sql, out var error), $"rejected with: {error}");
        }

        [Theory]
        // Not a SELECT at all
        [InlineData("DELETE FROM spell_proc")]
        [InlineData("UPDATE spell_proc SET SpellId = 1")]
        [InlineData("INSERT INTO spell_proc VALUES (1)")]
        [InlineData("DROP TABLE spell_proc")]
        [InlineData("TRUNCATE spell_proc")]
        [InlineData("ALTER TABLE spell_proc ADD COLUMN x INT")]
        [InlineData("CREATE TABLE x (id INT)")]
        [InlineData("GRANT ALL ON *.* TO 'x'@'%'")]
        [InlineData("CALL some_procedure()")]
        [InlineData("SET @x = 1")]
        // Stacked statements
        [InlineData("SELECT 1; DROP TABLE spell_proc")]
        [InlineData("SELECT 1;DELETE FROM spell_proc")]
        [InlineData("SELECT 1 ; ")]
        // DML hidden behind a leading SELECT
        [InlineData("SELECT * FROM spell_proc INTO OUTFILE '/tmp/x'")]
        [InlineData("SELECT * FROM (DELETE FROM spell_proc) t")]
        [InlineData("SELECT * FROM spell_proc FOR UPDATE")]
        // Comment-obscured stacking
        [InlineData("SELECT 1 -- \n; DROP TABLE spell_proc")]
        [InlineData("SELECT 1 /* */; DROP TABLE spell_proc")]
        // Nothing at all
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Rejects(string sql)
        {
            Assert.False(SqlGuard.TryValidate(sql, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        // The message is part of the contract: a rejection the agent cannot
        // act on produces a retry loop of the same rejected query.
        [Fact]
        public void RejectionMessage_NamesTheReason()
        {
            SqlGuard.TryValidate("DELETE FROM spell_proc", out var error);
            Assert.Contains("SELECT", error);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~SqlGuardTests"`
Expected: FAIL to compile — `SqlGuard` does not exist.

- [ ] **Step 3: Implement `SqlGuard`**

Create `SpellWork.Mcp/SqlGuard.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SpellWork.Mcp
{
    /// <summary>
    /// Rejects anything that is not a single read-only SELECT.
    /// <para>
    /// This is one of the two controls spec section 9 requires, not the whole
    /// boundary: the operator must also connect as a read-only MySQL user.
    /// The parser is a guard, the read-only grant is the guarantee. Do not
    /// relax this on the basis that the grant exists, and do not skip the
    /// grant on the basis that this exists.
    /// </para>
    /// <para>
    /// Deliberately conservative. It is not a SQL parser and does not try to
    /// be: anything it cannot confidently classify is rejected, because a
    /// validator that guesses in the caller's favour is not a guard. The
    /// queries this refuses that it arguably should not are a cost worth
    /// paying.
    /// </para>
    /// </summary>
    public static class SqlGuard
    {
        // Whole-word matches only, checked outside string literals. INTO
        // catches SELECT ... INTO OUTFILE, which writes a file from a SELECT.
        private static readonly string[] ForbiddenKeywords =
        {
            "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE", "ALTER", "CREATE", "REPLACE",
            "GRANT", "REVOKE", "RENAME", "LOCK", "UNLOCK", "CALL", "EXEC", "EXECUTE",
            "SET", "LOAD", "HANDLER", "PREPARE", "DEALLOCATE", "INTO", "OUTFILE", "DUMPFILE",
            "SLEEP", "BENCHMARK", "SHUTDOWN", "FLUSH", "KILL", "USE",
        };

        public static bool TryValidate(string sql, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(sql))
            {
                error = "The query is empty. Provide a single SELECT statement.";
                return false;
            }

            if (!TryStripComments(sql, out var stripped, out error))
                return false;

            var trimmed = stripped.Trim();
            if (trimmed.Length == 0)
            {
                error = "The query contains nothing but comments. Provide a single SELECT statement.";
                return false;
            }

            // A trailing semicolon is stripped once; anything after it -- or a
            // second one -- means more than one statement was submitted.
            if (trimmed.EndsWith(";", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();

            if (ContainsOutsideLiteral(trimmed, ";"))
            {
                error = "Only a single statement is allowed; the query contains a statement separator.";
                return false;
            }

            if (!Regex.IsMatch(trimmed, @"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase))
            {
                error = "Only SELECT statements are allowed. The query must begin with SELECT (or WITH … SELECT).";
                return false;
            }

            // WITH must actually lead to a SELECT, not a CTE-prefixed DELETE.
            if (Regex.IsMatch(trimmed, @"^\s*WITH\b", RegexOptions.IgnoreCase) &&
                !Regex.IsMatch(trimmed, @"\bSELECT\b", RegexOptions.IgnoreCase))
            {
                error = "Only SELECT statements are allowed. A WITH clause must lead to a SELECT.";
                return false;
            }

            var masked = MaskLiterals(trimmed);
            foreach (var keyword in ForbiddenKeywords)
            {
                if (Regex.IsMatch(masked, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
                {
                    error = $"Only read-only SELECT statements are allowed; the query contains '{keyword}'.";
                    return false;
                }
            }

            // FOR UPDATE / LOCK IN SHARE MODE take write locks. Neither
            // contains a forbidden keyword on its own.
            if (Regex.IsMatch(masked, @"\bFOR\s+UPDATE\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(masked, @"\bFOR\s+SHARE\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(masked, @"\bIN\s+SHARE\s+MODE\b", RegexOptions.IgnoreCase))
            {
                error = "Locking reads are not allowed; remove FOR UPDATE / FOR SHARE / IN SHARE MODE.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Removes -- , # and block comments, tracking string literals so a
        /// sequence inside a literal is not mistaken for a comment. Returns
        /// false for an unterminated literal or block comment: a query the
        /// validator cannot finish reading is one it cannot vouch for.
        /// </summary>
        private static bool TryStripComments(string sql, out string stripped, out string error)
        {
            var builder = new StringBuilder(sql.Length);
            error = null;
            stripped = null;

            var index = 0;
            while (index < sql.Length)
            {
                var c = sql[index];

                if (c == '\'' || c == '"' || c == '`')
                {
                    var quote = c;
                    builder.Append(c);
                    index++;

                    var closed = false;
                    while (index < sql.Length)
                    {
                        if (sql[index] == '\\' && index + 1 < sql.Length)
                        {
                            builder.Append(sql[index]).Append(sql[index + 1]);
                            index += 2;
                            continue;
                        }

                        if (sql[index] == quote)
                        {
                            // '' inside a '-quoted literal is an escaped quote.
                            if (index + 1 < sql.Length && sql[index + 1] == quote)
                            {
                                builder.Append(quote).Append(quote);
                                index += 2;
                                continue;
                            }

                            builder.Append(quote);
                            index++;
                            closed = true;
                            break;
                        }

                        builder.Append(sql[index]);
                        index++;
                    }

                    if (!closed)
                    {
                        error = "The query has an unterminated string literal.";
                        return false;
                    }

                    continue;
                }

                if (c == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
                {
                    while (index < sql.Length && sql[index] != '\n')
                        index++;
                    builder.Append(' ');
                    continue;
                }

                if (c == '#')
                {
                    while (index < sql.Length && sql[index] != '\n')
                        index++;
                    builder.Append(' ');
                    continue;
                }

                if (c == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
                {
                    var end = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        error = "The query has an unterminated block comment.";
                        return false;
                    }

                    index = end + 2;
                    builder.Append(' ');
                    continue;
                }

                builder.Append(c);
                index++;
            }

            stripped = builder.ToString();
            return true;
        }

        private static bool ContainsOutsideLiteral(string sql, string needle) =>
            MaskLiterals(sql).IndexOf(needle, StringComparison.Ordinal) >= 0;

        /// <summary>
        /// Replaces the contents of every string literal with spaces so a
        /// keyword or separator inside quoted text cannot trigger a rejection.
        /// The literal's own quotes are kept so lengths and offsets still line
        /// up with the input.
        /// </summary>
        private static string MaskLiterals(string sql)
        {
            var builder = new StringBuilder(sql.Length);
            var index = 0;

            while (index < sql.Length)
            {
                var c = sql[index];
                if (c != '\'' && c != '"' && c != '`')
                {
                    builder.Append(c);
                    index++;
                    continue;
                }

                var quote = c;
                builder.Append(quote);
                index++;

                while (index < sql.Length)
                {
                    if (sql[index] == '\\' && index + 1 < sql.Length)
                    {
                        builder.Append("  ");
                        index += 2;
                        continue;
                    }

                    if (sql[index] == quote)
                    {
                        if (index + 1 < sql.Length && sql[index + 1] == quote)
                        {
                            builder.Append("  ");
                            index += 2;
                            continue;
                        }

                        builder.Append(quote);
                        index++;
                        break;
                    }

                    builder.Append(' ');
                    index++;
                }
            }

            return builder.ToString();
        }
    }
}
```

The `Rejects` case `"SELECT * FROM (DELETE FROM spell_proc) t"` is caught by the `DELETE` keyword scan, and `"SELECT 1 -- \n; DROP TABLE spell_proc"` by comment stripping followed by the separator check — trace both by hand before running the tests, because a validator that passes its tests for the wrong reason is the worst outcome here.

- [ ] **Step 4: Run the validator tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~SqlGuardTests"`
Expected: PASS, every case.

- [ ] **Step 5: Prove the tests bite**

Temporarily change `TryValidate` to `{ error = null; return true; }` and re-run. Every `Rejects` case must fail. Restore the implementation. A validator whose tests pass against a stub is not a tested validator — do this, and say in the task report that you did.

- [ ] **Step 6: Add the generic reader to `WorldDatabase`**

In `SpellWork.Core/Database/WorldDatabase.cs`, alongside `SelectProc`:

```csharp
        public sealed class QueryResult
        {
            public List<string> Columns { get; } = new List<string>();
            public List<Dictionary<string, object>> Rows { get; } = new List<Dictionary<string, object>>();
            public bool Truncated { get; set; }
        }

        /// <summary>
        /// Runs a caller-supplied SELECT and returns up to <paramref name="limit"/> rows.
        /// The query text is not parameterisable — it is composed by the
        /// caller — which is why SpellWork.Mcp.SqlGuard validates it first and
        /// why the documented MySQL account is a read-only grant.
        /// </summary>
        public QueryResult SelectRows(string sql, int limit)
        {
            var result = new QueryResult();

            using (var connection = new MySqlConnection(ConnectionString))
            {
                var command = new MySqlCommand(sql, connection);
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    for (var i = 0; i < reader.FieldCount; i++)
                        result.Columns.Add(reader.GetName(i));

                    while (reader.Read())
                    {
                        if (result.Rows.Count >= limit)
                        {
                            result.Truncated = true;
                            break;
                        }

                        var row = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var value = reader.GetValue(i);
                            row[result.Columns[i]] = value == DBNull.Value ? null : value;
                        }

                        result.Rows.Add(row);
                    }
                }
            }

            return result;
        }
```

- [ ] **Step 7: Write the database-tool tests**

Create `SpellWork.Mcp.Tests/DatabaseToolsTests.cs`. These must pass **without** a MySQL server — the "not configured" path is the one that always runs in CI:

```csharp
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SpellWork.Mcp.Tools;
using Xunit;

namespace SpellWork.Mcp.Tests
{
    public class DatabaseToolsTests
    {
        private static DatabaseTools ToolsWithoutMySql()
        {
            var config = McpServerConfig.FromEnvironment(new Dictionary<string, string>());
            return new DatabaseTools(new SpellDataService(config, NullLogger<SpellDataService>.Instance));
        }

        // Spec section 9: MySQL being unconfigured is reported in the
        // response, and every other tool is unaffected.
        [Fact]
        public async Task QueryWorldDb_WithoutConfiguration_SaysSo()
        {
            var json = await ToolsWithoutMySql().QueryWorldDb("SELECT 1");
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("mysql_not_configured", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        // The guard runs before anything touches the network, so this
        // rejection is observable with no server present -- and that is
        // exactly the property that makes the guard testable.
        [Fact]
        public async Task QueryWorldDb_RejectsNonSelect_BeforeReachingTheDatabase()
        {
            var json = await ToolsWithoutMySql().QueryWorldDb("DROP TABLE spell_proc");
            using var doc = JsonDocument.Parse(json);
            var error = doc.RootElement.GetProperty("error");

            Assert.Equal("sql_rejected", error.GetProperty("code").GetString());
            Assert.Contains("SELECT", error.GetProperty("message").GetString());
        }

        [Fact]
        public async Task QueryWorldDb_RejectsStackedStatements()
        {
            var json = await ToolsWithoutMySql().QueryWorldDb("SELECT 1; DROP TABLE spell_proc");
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("sql_rejected", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        [Fact]
        public async Task GetSpellDbOverrides_WithoutConfiguration_SaysSo()
        {
            var json = await ToolsWithoutMySql().GetSpellDbOverrides(133);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("mysql_not_configured", doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
    }
}
```

The rejection check must run **before** the not-configured check for `QueryWorldDb`, or `QueryWorldDb_RejectsNonSelect_BeforeReachingTheDatabase` cannot pass without a server. That ordering is also the right one on its own terms: a malformed query is a caller error regardless of whether a database is reachable.

- [ ] **Step 8: Implement `DatabaseTools`**

Create `SpellWork.Mcp/Tools/DatabaseTools.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using SpellWork.Database;

namespace SpellWork.Mcp.Tools
{
    [McpServerToolType]
    public sealed class DatabaseTools
    {
        // Every table get_spell_db_overrides collects, with the column that
        // holds the spell id in each. TrinityCore names these inconsistently,
        // which is why the column travels with the table name rather than
        // being assumed.
        private static readonly (string Table, string IdColumn)[] OverrideTables =
        {
            ("spell_proc", "SpellId"),
            ("spell_script_names", "spell_id"),
            ("spell_custom_attr", "entry"),
            ("spell_bonus_data", "entry"),
            ("spell_threat", "entry"),
            ("spell_ranks", "spell_id"),
            ("spell_dbc", "Id"),
        };

        private readonly SpellDataService _data;

        public DatabaseTools(SpellDataService data) => _data = data;

        [McpServerTool(Name = "query_world_db")]
        [Description("Run one read-only SELECT against the configured world database. Multiple statements, DDL and DML are rejected. The server should be configured with a read-only MySQL user.")]
        public Task<string> QueryWorldDb(string sql, string database = "world", int limit = 100)
        {
            // Validate first, unconditionally: a malformed query is a caller
            // error whether or not a database happens to be reachable, and
            // rejecting before any connection is attempted is what makes this
            // testable with no server present.
            if (!SqlGuard.TryValidate(sql, out var rejection))
                return Task.FromResult(ToolResponse.Error("sql_rejected", rejection));

            var config = _data.Config;
            if (!config.MySqlConfigured)
                return Task.FromResult(NotConfigured());

            if (!string.Equals(database, config.WorldDatabase, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ToolResponse.Error("unknown_database",
                    $"This server is configured for database '{config.WorldDatabase}'. " +
                    "Set SPELLWORK_MYSQL_WORLD_DB to query a different one.",
                    new Dictionary<string, object> { ["configuredDatabase"] = config.WorldDatabase }));
            }

            // No lower floor: ToolResponse.Paged is the authoritative clamp and
            // its contract says a non-positive limit yields zero rows, never one
            // and never "unbounded". A second, disagreeing clamp here is exactly
            // the drift Paged was made authoritative to prevent.
            var effectiveLimit = Math.Min(limit, ToolResponse.MaxRows);

            try
            {
                var worldDatabase = new WorldDatabase(config.ToSpellWorkConfig());
                var result = worldDatabase.SelectRows(sql, effectiveLimit);

                return Task.FromResult(ToolResponse.Ok(new Dictionary<string, object>
                {
                    ["database"] = config.WorldDatabase,
                    ["columns"] = result.Columns,
                    ["rows"] = result.Rows.Cast<object>().ToList(),
                    ["count"] = result.Rows.Count,
                    ["limit"] = effectiveLimit,
                    ["truncated"] = result.Truncated,
                    ["maxRows"] = ToolResponse.MaxRows,
                }));
            }
            catch (Exception ex)
            {
                // The driver's message names the syntax error or the missing
                // table, which is what the agent needs to fix its query.
                return Task.FromResult(ToolResponse.Error("query_failed", ex.Message));
            }
        }

        [McpServerTool(Name = "get_spell_db_overrides")]
        [Description("Rows for one spell id from spell_proc, spell_script_names, spell_custom_attr, spell_bonus_data, spell_threat, spell_ranks and spell_dbc.")]
        public Task<string> GetSpellDbOverrides(int id)
        {
            var config = _data.Config;
            if (!config.MySqlConfigured)
                return Task.FromResult(NotConfigured());

            var worldDatabase = new WorldDatabase(config.ToSpellWorkConfig());
            var tables = new Dictionary<string, object>();
            var errors = new Dictionary<string, object>();

            foreach (var (table, idColumn) in OverrideTables)
            {
                // id is an int parameter, not caller-supplied text, so this
                // interpolation cannot carry an injection. Everything else in
                // the statement is a compile-time constant.
                var sql = $"SELECT * FROM `{table}` WHERE `{idColumn}` = {id}";

                try
                {
                    var result = worldDatabase.SelectRows(sql, ToolResponse.MaxRows);
                    tables[table] = result.Rows.Cast<object>().ToList();
                }
                catch (Exception ex)
                {
                    // A missing table is normal -- not every world database
                    // has every one of these. Reporting it per table beats
                    // failing the whole call.
                    errors[table] = ex.Message;
                }
            }

            var payload = new Dictionary<string, object>
            {
                ["id"] = id,
                ["database"] = config.WorldDatabase,
                ["tables"] = tables,
            };

            if (errors.Count > 0)
                payload["unavailableTables"] = errors;

            return Task.FromResult(ToolResponse.Ok(payload));
        }

        private static string NotConfigured() =>
            ToolResponse.Error("mysql_not_configured",
                "No MySQL connection is configured. Set SPELLWORK_MYSQL_HOST, SPELLWORK_MYSQL_USER and " +
                "SPELLWORK_MYSQL_WORLD_DB (and SPELLWORK_MYSQL_PASS / SPELLWORK_MYSQL_PORT if needed).",
                new Dictionary<string, object>
                {
                    ["variables"] = new[]
                    {
                        "SPELLWORK_MYSQL_HOST", "SPELLWORK_MYSQL_PORT", "SPELLWORK_MYSQL_USER",
                        "SPELLWORK_MYSQL_PASS", "SPELLWORK_MYSQL_WORLD_DB",
                    },
                });
    }
}
```

Verify the seven table names and their id columns against a TrinityCore world schema for this branch before committing — `spell_proc` in particular replaced `spell_proc_event` at some point, and `WorldDatabase.GetSpellName` still references `spell_proc_event` in its `DELETE` statement builder. If a column name is wrong the tool returns "unavailable" for that table rather than failing, which makes a typo easy to miss; check them.

- [ ] **Step 9: Run all tests**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj` and `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`
Expected: PASS both, with no MySQL server running.

- [ ] **Step 10: Commit**

```bash
git add SpellWork.Mcp/SqlGuard.cs SpellWork.Mcp/Tools/DatabaseTools.cs SpellWork.Core/Database/WorldDatabase.cs SpellWork.Mcp.Tests/
git commit -m "feat: add the SELECT-only guard, query_world_db and get_spell_db_overrides"
```

---

### Task 12: Registration documentation and the full-surface smoke test

The last task: prove all thirteen tools list over a real stdio handshake, and write the registration instructions — including the read-only-grant instruction that spec §9 makes a required half of the `query_world_db` security model.

**Files:**
- Create: `docs/mcp-server.md`
- Modify: `SpellWork.Mcp.Tests/StdioSmokeTests.cs`
- Modify: `README.md` (a pointer to the new doc)

**Interfaces:**
- Consumes: every tool from Tasks 4-11.
- Produces: nothing later tasks depend on — this plan ends here.

- [ ] **Step 1: Write the full-surface smoke test**

Replace the individual `ToolsList_*` assertions in `SpellWork.Mcp.Tests/StdioSmokeTests.cs` with one exhaustive test:

```csharp
        // The complete tool surface this plan ships. get_spell_visual_chain is
        // the fourteenth tool in spec section 6 and belongs to Plan 3; when it
        // lands, add it here and this list becomes fourteen.
        private static readonly string[] ExpectedTools =
        {
            "server_info",
            "get_spell",
            "search_spells",
            "get_spell_triggers",
            "compare_spells",
            "decode_flags",
            "list_enum",
            "calculate_spell_scaling",
            "list_db2",
            "get_db2_row",
            "query_db2",
            "query_world_db",
            "get_spell_db_overrides",
        };

        [Fact]
        public async Task ToolsList_ContainsEveryToolExactlyOnce()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());
            var listing = lines.FirstOrDefault(line => line.Contains("\"tools\""));
            Assert.NotNull(listing);

            using var doc = JsonDocument.Parse(listing);
            var names = doc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
                .Select(tool => tool.GetProperty("name").GetString())
                .ToList();

            Assert.Equal(ExpectedTools.Length, names.Count);
            foreach (var expected in ExpectedTools)
                Assert.Single(names, name => name == expected);
        }

        // Every tool must carry a description: it is the only thing the agent
        // has to choose between them, and an undescribed tool is one that gets
        // called wrong or not at all.
        [Fact]
        public async Task EveryTool_HasANonEmptyDescription()
        {
            var lines = await RunHandshakeAsync(new Dictionary<string, string>());
            var listing = lines.First(line => line.Contains("\"tools\""));

            using var doc = JsonDocument.Parse(listing);
            foreach (var tool in doc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString();
                Assert.True(tool.TryGetProperty("description", out var description),
                    $"{name} has no description.");
                Assert.False(string.IsNullOrWhiteSpace(description.GetString()), $"{name} has an empty description.");
            }
        }
```

Add `using System.Linq;`. The `RunHandshakeAsync` helper currently stops after two lines — raise its stop condition so the `tools/list` response is definitely among them, and keep the 30-second timeout.

- [ ] **Step 2: Run it**

Run: `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj --filter "FullyQualifiedName~StdioSmokeTests"`
Expected: PASS. A count mismatch here means a tool class is missing its `[McpServerToolType]` attribute or a method its `[McpServerTool]`.

- [ ] **Step 3: Write the registration documentation**

Create `docs/mcp-server.md`:

````markdown
# SpellWork MCP server

A read-only MCP server exposing SpellWork's 8.3.7 spell data, DB2 stores and
world database to an agent. Thirteen tools; see `server_info` first when
anything looks wrong.

## Build

```
dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o out/mcp
```

## Register

```
claude mcp add --scope user spellwork -- dotnet /absolute/path/to/out/mcp/SpellWork.Mcp.dll
```

`--scope user` makes it available in every project.

## Configuration

Set these on the MCP server entry (`claude mcp add --env NAME=value`, or the
`env` block in the config file).

| Variable | Required | Meaning |
| --- | --- | --- |
| `SPELLWORK_DBC_PATH` | yes | `<emulator-build-dir>/ClientData/dbc` |
| `SPELLWORK_GT_PATH` | yes | `<emulator-build-dir>/ClientData/gt` |
| `SPELLWORK_LOCALE` | no (`enUS`) | Locale subdirectory under the DBC path |
| `SPELLWORK_HOTFIX_CACHE` | no | Client `DBCache.bin` |
| `SPELLWORK_MYSQL_HOST` | no | Omit to disable the MySQL tools |
| `SPELLWORK_MYSQL_PORT` | no (`3306`) | Port, or a named pipe when host is `.` |
| `SPELLWORK_MYSQL_USER` | no | See the security note below |
| `SPELLWORK_MYSQL_PASS` | no | |
| `SPELLWORK_MYSQL_WORLD_DB` | no | Database name, e.g. `world` |

The MySQL tools require host, user and world-db together; a partial
configuration reports "not configured" rather than failing to connect.

A misconfigured server still starts and still answers — a bad path produces a
structured error from the data tools naming the variable to fix, not a dead
process.

## Security: use a read-only MySQL user

**`query_world_db` runs SQL that the agent composed.** Two controls stand
between that and your database, and both are required:

1. The server rejects anything that is not a single `SELECT` — multiple
   statements, DDL and DML are refused before the query reaches MySQL.
2. **You connect as a MySQL user with only `SELECT` granted.**

The parser is a guard. The read-only grant is the guarantee. Do not point this
server at an account that can write:

```sql
CREATE USER 'spellwork_ro'@'localhost' IDENTIFIED BY '<password>';
GRANT SELECT ON world.* TO 'spellwork_ro'@'localhost';
```

Nothing in this server writes to DB2 files or to MySQL, by design.

## Tools

| Tool | What it does |
| --- | --- |
| `server_info` | Configured paths, locale, store counts, load duration, MySQL reachability |
| `get_spell` | The full decoded view of one spell, as JSON or rendered text |
| `search_spells` | Find spells by name, family, aura/effect/target type, attribute or school |
| `get_spell_triggers` | What a spell triggers and what triggers it |
| `compare_spells` | Structured field diff of two spells |
| `decode_flags` | Split a bitmask into named flags |
| `list_enum` | The id↔name map for any enum |
| `calculate_spell_scaling` | Per-effect values at a given level, item level and difficulty |
| `list_db2` | Every `.db2` present, flagged modelled or unmodelled |
| `get_db2_row` | One row from a modelled store, with real field names |
| `query_db2` | Filtered rows from a modelled store |
| `query_world_db` | One read-only `SELECT` against the world database |
| `get_spell_db_overrides` | Rows for one spell from the seven `spell_*` override tables |

Every tool caps results at 500 rows and reports whether it truncated.

## Known limitations

- SpellWork models 47 of the ~715 `.db2` files. `get_db2_row` and `query_db2`
  work on the modelled ones; the rest are listed by `list_db2` but return
  `store_not_modelled`, because their fields are only available positionally.
- Spell visual chains are not yet available. That is a separate piece of work.
- Client data loads once per process. There is no reload; restart the server
  after changing the DBC path.
````

Verify the `claude mcp add` syntax against the current CLI (`claude mcp add --help`) before committing — a registration command that does not work is the one documentation error that stops anyone from using this at all.

- [ ] **Step 4: Point `README.md` at it**

Add a short section to `README.md` — match the file's existing heading style and tone rather than importing a new one:

```markdown
## MCP server

`SpellWork.Mcp` exposes this data to an AI agent over the Model Context
Protocol, read-only. See [docs/mcp-server.md](docs/mcp-server.md) for build,
registration and configuration.
```

- [ ] **Step 5: End-to-end check by hand**

With real client data configured, register the server and, from a Claude Code session, call `server_info`, then `get_spell` for 133, then `decode_flags` for a school mask. Confirm each returns data and that nothing in the transcript shows a transport error. Report in the task report what you actually ran and what came back — this is the first time the server is exercised by a real MCP client rather than the smoke test's scripted handshake.

- [ ] **Step 6: Full verification**

```bash
dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj
dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj
"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" SpellWork.sln -restore -t:Rebuild -v:minimal
```

Expected: both suites green, and zero warning or error lines from the rebuild. `-t:Rebuild` is required — this branch has twice shipped a warning that an incremental build hid.

- [ ] **Step 7: Commit**

```bash
git add docs/mcp-server.md README.md SpellWork.Mcp.Tests/
git commit -m "docs: add MCP server registration guide and full-surface smoke test"
```

---
