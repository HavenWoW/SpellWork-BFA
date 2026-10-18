# SpellWork 5.5.4 (70449) MCP variant — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a `spellwork-554` MCP variant that loads the WDC5 5.5.4.70449 client data via DBCD and serves `server_info`, `get_spell`, and `search_spells`.

**Architecture:** Fork the `main` (8.3.7) variant onto a new `5.5.4` branch. Replace `DBFileReaderLib` with **DBCD 2.3.1**, bump `SpellWork.Core` to `net9.0`, and insert a **DBCD→POCO mapper** so the existing `Storage<T>`/`*Entry`/`SpellInfo` model is preserved. Port the bring-up stores to the 70449 layouts, trim `SpellInfo` and the store set to that subset, and publish `out/mcp554`.

**Tech Stack:** C# / .NET 9, xUnit + Xunit.SkippableFact, DBCD 2.3.1 (+ DBCD.IO, DBDefsLib), ModelContextProtocol 2.0.0.

**Spec:** `docs/superpowers/specs/2026-10-18-spellwork-554-mcp-design.md` (read it — this plan argues from it).

## Global Constraints

- All projects target **`net9.0`**. Core references **`DBCD` 2.3.1**; `DBFileReaderLib` is removed from every csproj and referenced nowhere.
- Core provides its own `Storage<T>`, `IndexAttribute`, `CardinalityAttribute`, and new `ColumnAttribute` (no `DBFileReaderLib.Attributes`).
- Mapper column resolution: `[Column("name")]` → field name → field name + `"_lang"`. Mapper coerces each value into the POCO field's declared type (element-wise for arrays) and never throws on a legal narrow/widen.
- Build string format `x.x.x.xxxxx`; default `5.5.4.70449`. `DBCD.Load` resolves 70449 by WDC5 layout-hash fallback.
- Layout verification is by **column-name set** against DBCD `AvailableColumns`; mismatches are recorded, not thrown.
- Valid difficulty ids are derived from the loaded `Difficulty` store, not hardcoded.
- stdout is MCP JSON-RPC only; logging to stderr. Read-only server.

## Review Focus

- **Locstring columns** surface only as `*_lang` (`Name_lang`, `DisplayName_lang`): the mapper must resolve a bare field name to its `_lang` column. → Task 2.
- **Narrowing coercion**: DBCD gives `Int16`/`Byte`/`SByte`/`UInt16`/`Single` while POCO fields may be `int`/`uint`/`float`; `Coerce` must handle every such pair, and arrays element-wise, without throwing. → Task 2.
- **A missing store file at runtime** must leave that store null and be reported in `UnloadedStores`/`server_info`, not abort the whole load. → Task 5.
- **Bad `difficulty` arg to `get_spell`** returns `unknown_difficulty` listing the ids actually loaded from the `Difficulty` store. → Task 8.
- **Unknown spell id** to `get_spell` returns a not-found error with nearest-name suggestions drawn from the `SpellName` store. → Task 8.

---

### Task 1: New branch + project retargeting + DBCD dependency

**Files:**
- Modify: `SpellWork.Core/SpellWork.Core.csproj`, `SpellWork.Mcp/SpellWork.Mcp.csproj`, `SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`, `SpellWork.Mcp.Tests/*.csproj` (if present)

**Interfaces:**
- Produces: a solution that targets `net9.0` everywhere and references `DBCD` 2.3.1; `DBFileReaderLib` removed. (Will not compile until Task 2 provides the replacement types — that is expected; the build-green gate is at the end of Task 2.)

- [ ] **Step 1:** Branch from `main`: `git worktree add ../sw-554 -b 5.5.4 main` (per superpowers:using-git-worktrees). All subsequent work happens in that worktree.
- [ ] **Step 2:** In `SpellWork.Core.csproj`: change `<TargetFramework>netstandard2.0</TargetFramework>` → `net9.0`; remove the `<Reference Include="DBFileReaderLib">` block; add `<PackageReference Include="DBCD" Version="2.3.1" />`.
- [ ] **Step 3:** In `SpellWork.Mcp.csproj` and the test csproj(s): remove their `DBFileReaderLib` HintPath `<Reference>` blocks (DBCD now flows transitively via the Core `ProjectReference`). Leave TFMs at `net9.0`.
- [ ] **Step 4:** Commit. `git commit -m "554: retarget Core to net9.0, swap DBFileReaderLib for DBCD 2.3.1"`

### Task 2: Core reader layer — attributes, Storage<T>, DBCD→POCO mapper

**Files:**
- Create: `SpellWork.Core/DBC/DbcAttributes.cs`, `SpellWork.Core/DBC/Storage.cs`, `SpellWork.Core/DBC/DbcdStorageMapper.cs`
- Test: `SpellWork.Core.Tests/DbcdStorageMapperTests.cs`

**Interfaces:**
- Produces:
  - `IndexAttribute(bool nonInline = false) { bool NonInline }`, `CardinalityAttribute(int count) { int Count }`, `ColumnAttribute(string name) { string Name }` — all `[AttributeUsage(AttributeTargets.Field)]`, namespace `SpellWork.DBC`.
  - `public sealed class Storage<T> : Dictionary<int, T>` (namespace `SpellWork.DBC`).
  - `public static Storage<T> DbcdStorageMapper.Map<T>(DBCD.IDBCDStorage source) where T : class, new()`
  - `internal static object DbcdStorageMapper.Coerce(object value, Type targetType)` — scalar + array coercion.
  - `internal static string DbcdStorageMapper.ResolveColumn(System.Reflection.FieldInfo field, System.Collections.Generic.ICollection<string> available)`

- [ ] **Step 1: Write the failing unit tests** (no client data needed) in `DbcdStorageMapperTests.cs`:

```csharp
[Fact] void Coerce_widens_boxed_short_to_int()        => Assert.Equal(5,   DbcdStorageMapper.Coerce((short)5, typeof(int)));
[Fact] void Coerce_byte_to_uint()                     => Assert.Equal(3u,  DbcdStorageMapper.Coerce((byte)3, typeof(uint)));
[Fact] void Coerce_sbyte_to_int_negative()            => Assert.Equal(-1,  DbcdStorageMapper.Coerce((sbyte)-1, typeof(int)));
[Fact] void Coerce_int_array_elementwise_to_uint()    => Assert.Equal(new uint[]{1,2}, (uint[])DbcdStorageMapper.Coerce(new int[]{1,2}, typeof(uint[])));
[Fact] void ResolveColumn_prefers_exact_then_lang()   // field "Name" with available {"ID","Name_lang"} resolves to "Name_lang"
[Fact] void ResolveColumn_honours_Column_attribute()  // field with [Column("EffectPos_facing")] resolves to that exact name
```

- [ ] **Step 2: Run to verify they fail** — `dotnet test --filter DbcdStorageMapperTests` → FAIL (types/methods missing).
- [ ] **Step 3: Implement** the three files. `Coerce`: if `targetType.IsArray`, coerce each element to the element type into a new typed array; else `Convert.ChangeType(value, targetType)` with a null/`DBNull` guard returning the type's default. `ResolveColumn`: `[Column].Name` if set and present; else field name if present; else field name + `"_lang"` if present; else return field name (so a `Check` mismatch is what flags it, not an exception). `Map<T>`: reflect `typeof(T)`'s public instance fields in declared order, precompute each field's resolved column, then for every `KeyValuePair<int,DBCDRow>` in `source` create a `T`, set the `[Index]` field from the row key, set each other field to `Coerce(row[column], field.FieldType)`, and add to the `Storage<T>` under the key.
- [ ] **Step 4: Run** — `dotnet test --filter DbcdStorageMapperTests` → PASS.
- [ ] **Step 5: Build the whole solution** — `dotnet build` → succeeds (the Task 1 retarget is now green because Core's own types replace `DBFileReaderLib`'s). Any remaining `DBFileReaderLib`/`DBReader`/`GameTable` references in unported files are removed in Tasks 3–7; if the build still references them, comment-delete those members now and note it.
- [ ] **Step 6: Commit.** `git commit -m "554: Core reader layer — attributes, Storage<T>, DBCD mapper"`

### Task 3: Config — defs path + build string

**Files:**
- Modify: `SpellWork.Core/SpellWorkConfig.cs` (add `DefsPath`, `Build`), `SpellWork.Mcp/McpServerConfig.cs` (bind `SPELLWORK_DEFS_PATH`, `SPELLWORK_BUILD`), `SpellWork.Mcp/SpellWork.Mcp.csproj` (bundle `defs/`)
- Test: `SpellWork.Mcp.Tests` env-binding test (follow the existing `FromEnvironment(IDictionary)` test pattern)

**Interfaces:**
- Produces: `SpellWorkConfig.DefsPath : string`, `SpellWorkConfig.Build : string`; `McpServerConfig` reads `SPELLWORK_DEFS_PATH` (default: the publish's `defs/` dir) and `SPELLWORK_BUILD` (default `"5.5.4.70449"`), mapped through `ToSpellWorkConfig()`.

- [ ] **Step 1:** Write the failing test: `FromEnvironment` with `SPELLWORK_BUILD` unset yields `Build == "5.5.4.70449"`; when set, echoes it; `SPELLWORK_DEFS_PATH` unset resolves to the bundled `defs` path.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Add the two properties and bindings. Default `DefsPath` to `Path.Combine(AppContext.BaseDirectory, "defs")`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** In `SpellWork.Mcp.csproj` add a `<None Include="defs\**" CopyToOutputDirectory="PreserveNewest" />` item and copy the bring-up `.dbd` files (the 8 bring-up tables + any they reference) from WoWDBDefs `definitions/` into `SpellWork.Mcp/defs/`.
- [ ] **Step 6: Commit.** `git commit -m "554: config for defs path and build string"`

### Task 4: Structure classes — bring-up set at 70449 layout

**Files:**
- Create/overwrite in `SpellWork.Core/DBC/Structures/`: `SpellNameEntry.cs`, `SpellEntry.cs`, `SpellMiscEntry.cs`, `SpellEffectEntry.cs`, `SpellClassOptionsEntry.cs`, `SpellDurationEntry.cs`, `SpellRangeEntry.cs`, `DifficultyEntry.cs`
- Delete: every other `*Entry.cs` in `Structures/` (re-introduced in later milestones)
- Test: covered by Task 5's load test

**Interfaces:**
- Produces: the eight `*Entry` POCO types, fields matching the spec's "70449 WDC5 layouts" section exactly, with `[Index(true)] int ID` first, `[Cardinality(N)]` on arrays, and `[Column("…")]` where the DBCD column name differs from a valid C# identifier.
- Consumes: the attributes from Task 2.

- [ ] **Step 1:** Write the eight classes from the spec's layout section. Notes that decide the one reasonable form: `SpellName.Name` (field) maps to `Name_lang` (no attribute — mapper appends `_lang`); `SpellEffect.EffectPos_facing` needs `[Column("EffectPos_facing")]` (underscore not a clean identifier — name the field `EffectPosFacing` + the attribute); `Field_5_5_4_67090_025` and `Field_1_15_4_56400_013` keep those exact names as fields (valid identifiers) so no attribute is needed; `SpellMisc.Attributes` is `[Cardinality(17)] int[]`; array element types match the spec (`EffectRadiusIndex` `uint[2]`, `ImplicitTarget` `short[2]`, `SpellRange.RangeMin/RangeMax` `float[2]`).
- [ ] **Step 2:** Delete the non-bring-up structure files. `git rm SpellWork.Core/DBC/Structures/<each>.cs`.
- [ ] **Step 3: Commit.** `git commit -m "554: bring-up structures at 70449 layout; drop the rest"` (solution will not fully build until Tasks 5–7 trim the references; that is the next tasks' gate.)

### Task 5: DBC.cs — constants, store set, DBCD load, reduced SpellInfo build

**Files:**
- Modify: `SpellWork.Core/DBC/DBC.cs`
- Modify: `SpellWork.Core/DBC/ClientFileResolver.cs` (or fold path logic into DBC) to locate `{DbcPath}\{Locale}` and feed DBCD providers
- Test: `SpellWork.Core.Tests/DbcLoadTests.cs` (re-pinned), using `ClientDataFixture`

**Interfaces:**
- Produces: `DBC.Version = "SpellWork 5.5.4 (70449)"`, `DBC.MaxLevel = 90`; `Storage<T>` properties for exactly {SpellName, Spell, SpellMisc, SpellEffect, SpellClassOptions, SpellDuration, SpellRange, Difficulty}; `DBC.ValidDifficultyIds : IReadOnlyCollection<int>` (keys of the `Difficulty` store, plus `0`); `LoadAsync(SpellWorkConfig, ISpellWorkLogger)`.
- Consumes: Task 2 mapper, Task 3 config, Task 4 structures, Task 7 `LayoutVerification.Check`.

- [ ] **Step 1: Write the failing load tests** (SkippableFact, skip when `SPELLWORK_DBC_PATH` unset):
```csharp
Assert.True(DBC.SpellName.Count > 90000);           // 98,183 for 70449
Assert.Equal("Fireball", DBC.SpellInfoStore[133].Name);   // confirm against real 70449 data in Step 3
Assert.Contains(0, DBC.ValidDifficultyIds);
Assert.True(DBC.Difficulty.Count == 20);
```
- [ ] **Step 2: Run** → FAIL (compile/load).
- [ ] **Step 3: Implement `LoadAsync`:** construct `new DBCD.DBCD(new FilesystemDBCProvider(dbcDir), new FilesystemDBDProvider(config.DefsPath))` where `dbcDir = Path.Combine(config.DbcPath, config.Locale)` (fall back to `config.DbcPath` with `LocaleFallback=true` when the locale dir is absent). For each store property: `try { var s = dbcd.Load(FileNameFor(p), config.Build); _layoutMismatches += LayoutVerification.Check(name, s.AvailableColumns); prop.SetValue(null, DbcdStorageMapper.Map<T>(s)); _openedFiles[...] = ...; } catch (FileNotFoundException/DirectoryNotFoundException) { leave null; }`. After the loop, gate on `SpellName != null` → `BuildSpellInfoStore`. Set `ValidDifficultyIds` from `Difficulty?.Keys`. Confirm the spot-check ids/names in Step 1 against the real data and correct them if 133 isn't Fireball in 70449.
- [ ] **Step 3b: Update `ClientDataFixture`** to pass `DefsPath` (from `SPELLWORK_DEFS_PATH`, defaulting to the WoWDBDefs `definitions/` dir) and `Build` into the `SpellWorkConfig` it loads, so the data-backed tests can resolve the `.dbd` files. Data-backed tests skip when `SPELLWORK_DBC_PATH` is unset, as today.
- [ ] **Step 4: Implement the reduced `BuildSpellInfoStore`:** `foreach (var n in SpellName) SpellInfoStore[n.Value.ID] = new SpellInfo(n.Value, Spell.TryGet(n.Value.ID))`, then attach (base rows, DifficultyID==0 only) SpellMisc (+ SpellDuration via DurationIndex, + SpellRange via RangeIndex), SpellEffect (grouped by SpellID, building `SpellTriggerStore`), and SpellClassOptions. No scaling, no visuals, no other stores.
- [ ] **Step 5: Run** → PASS.
- [ ] **Step 6: Commit.** `git commit -m "554: DBC load via DBCD, reduced SpellInfo build"`

### Task 6: SpellInfo — trim to bring-up members

**Files:**
- Modify: `SpellWork.Core/Spell/SpellInfo.cs`
- Modify: the `SpellEffectInfo` helper it uses, if present, to drop scaling-only math
- Test: `SpellWork.Core.Tests/SpellInfoAssemblyTests.cs` (re-pinned)

**Interfaces:**
- Produces: `SpellInfo` with `ID`, `Name` (from `SpellName`), `Description`/`Tooltip` (from `Spell`), `SchoolMask` + `Attributes…AttributesEx16` (17 words from `Misc.Attributes`), `SpellFamilyName` (from `ClassOptions.SpellClassSet`), `Effects : List<SpellEffectEntry>`, `DurationEntry`, `Range`, and null-safe accessors for the member stores not loaded in bring-up.

- [ ] **Step 1:** Write a failing assembly test: for a known 70449 spell, `info.Name`, `info.SchoolMask`, `info.Attributes` length == 17, and `info.Effects.Count` match expected values (fill exact values in Step 3 from real data).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Remove `SpellInfo` members/accessors that read deleted stores (AuraRestrictions, Categories, Cooldowns, Interrupts, Levels, Powers, Reagents, ReagentsCurrency, Scaling, Shapeshift, TargetRestrictions, Totems, SpellXSpellVisual, DescriptionVariables, CastingRequirements, EquippedItems, ProcsPerMinute, visuals). Extend the attribute accessors to expose words 0..16 (add `AttributesEx14/15/16`). Keep name from `SpellName`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5: Commit.** `git commit -m "554: trim SpellInfo to bring-up members"`

### Task 7: LayoutVerification — column-name check

**Files:**
- Modify: `SpellWork.Core/DBC/LayoutVerification.cs`
- Test: `SpellWork.Core.Tests/LayoutVerificationTests.cs` (re-pinned)

**Interfaces:**
- Produces: `LayoutMismatch { string Table; string[] MissingColumns; int ExpectedFieldsCount; int ActualFieldsCount; string Describe(); }`; `Expected : IReadOnlyDictionary<string,(string[] Columns, int FieldsCount)>` keyed by file name for the 8 bring-up stores (columns = the spec's DBCD column names, e.g. `SpellName` → `{"ID","Name_lang"}` count 2); `static LayoutMismatch Check(string name, IReadOnlyCollection<string> availableColumns)`.

- [ ] **Step 1:** Write failing tests: every bring-up store name has an `Expected` entry; `Check` returns null when all expected columns are present and count matches; returns a `LayoutMismatch` naming the missing column(s) otherwise; a name absent from `Expected` returns null.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the column-set `Check` and the 8 `Expected` entries (copy column names from the spec's layout section).
- [ ] **Step 4:** Run → PASS. Add a SkippableFact: real 70449 data yields **zero** mismatches across the bring-up stores.
- [ ] **Step 5: Commit.** `git commit -m "554: layout verification by column name"`

### Task 8: MCP tools — server_info, get_spell, search_spells for 554

**Files:**
- Modify: `SpellWork.Mcp/Tools/DiagnosticTools.cs`, `SpellWork.Mcp/Tools/SpellTools.cs`, `SpellWork.Mcp/SpellDataService.cs` (CountStores reflection still works)
- Delete/exclude: the other tool classes (visual, world-db, db2, scaling, compare, triggers, decode, list) — remove from the assembly so only the three bring-up tools register
- Test: `SpellWork.Mcp.Tests` tool tests re-pinned to the three tools

**Interfaces:**
- Consumes: `DBC` statics, `SpellDataService.GetAsync()`, `DBC.ValidDifficultyIds`.
- Produces: `server_info` (version `SpellWork 5.5.4 (70449)`, dbcPath, defsPath, build, locale/localeFallback, clientDataLoaded, loadDurationMs, spellCount, storeCounts, layoutMismatches); `get_spell(int id, string format="json", string[] sections=null, uint difficulty=0)` over sections {misc, attributes, effects}; `search_spells(name, family, auraType, effectType, targetType, attribute, schoolMask, limit, offset)`.

- [ ] **Step 1:** Write failing tool tests: `server_info` reports the 5.5.4 version and `clientDataLoaded==true`; `get_spell` of a known id returns its name; `get_spell` with `difficulty=99` returns `unknown_difficulty` listing `DBC.ValidDifficultyIds`; `get_spell` of an absent id returns not-found with nearest names; `search_spells(name:"fire")` returns matches paged.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Fork the three tool methods from `main`, dropping sections/branches that read unloaded stores; wire `difficulty` validation to `DBC.ValidDifficultyIds`; remove the other tool classes so `WithToolsFromAssembly` registers only these three.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5: Commit.** `git commit -m "554: server_info, get_spell, search_spells"`

### Task 9: Goldens + green test suite

**Files:**
- Create: `SpellWork.Core.Tests/Goldens/` entries for 4–6 chosen 70449 spells (json + txt)
- Modify: `GoldenRenderingTests.cs` `Cases` array; delete 5.4.8/8.3.7-only cases
- Test: the golden suite itself

- [ ] **Step 1:** Choose 4–6 representative 70449 spells (a damage spell, an aura, a multi-effect, a channeled). Set `Cases` to `(name,id,difficulty=0)`.
- [ ] **Step 2:** Generate goldens by running the suite once with `SPELLWORK_REGEN_GOLDENS=1` (+ the client-data env). Review the generated text/json for sanity (names resolved, 17 attribute words, effects present).
- [ ] **Step 3:** Run the full suite without regen → PASS. `Golden_files_are_present` guard passes.
- [ ] **Step 4: Commit.** `git commit -m "554: goldens and green suite"`

### Task 10: Publish + register + docs

**Files:**
- Modify: `docs/mcp-server.md` (554 build/register section)
- Create: nothing in-repo beyond docs; the publish is an artifact

- [ ] **Step 1:** `dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o out/mcp554` (from the worktree; confirm `defs/` lands in the output).
- [ ] **Step 2:** Register: `claude mcp add --scope user spellwork-554 -e SPELLWORK_DBC_PATH=F:/WorkDir/mop-clientdata/70449/dbc -e SPELLWORK_LOCALE=enUS -- dotnet <abs>/out/mcp554/SpellWork.Mcp.dll` (defs bundled; add `-e SPELLWORK_DEFS_PATH=…` only to override).
- [ ] **Step 3:** Smoke-test the live server: `server_info` shows `SpellWork 5.5.4 (70449)`, `spellCount ≈ 98183`, zero `layoutMismatches`; `get_spell` and `search_spells` answer.
- [ ] **Step 4:** Update `docs/mcp-server.md` with the 554 build/register lines and the bring-up tool subset. Commit. Finish the branch per superpowers:finishing-a-development-branch.
