# SpellWork 5.4.8 MCP Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port `SpellWork.Core` and `SpellWork.Mcp` on branch `5.4.8` so all fourteen MCP tools answer against 5.4.8 (build 18414) client data, the visual chain included.

**Architecture:** Same shape as `main`. Core loads WDBC/WDB2 files into static `Storage<T>` properties on `DBC` through the vendored `DBFileReaderLib`, then builds `SpellInfoStore`. Mcp wraps that in `SpellDataService` and exposes tools. What changes is the data model (structures, enums, per-difficulty resolution, scaling, visual chain), file resolution (`.db2` wins over `.dbc`, the locale folder is optional) and the override-table list.

**Tech Stack:** C# (Core netstandard2.0, Mcp and tests net9.0), DBFileReaderLib (vendored), ModelContextProtocol, MySql.Data, xUnit + Xunit.SkippableFact.

**Spec:** `docs/superpowers/specs/2026-10-02-spellwork-548-mcp-design.md`. Read it before starting any task.

## Global Constraints

- Branch `5.4.8`. Never merge into `main` and never push without the user's say-so.
- Git in this checkout needs `-c safe.directory=F:/WorkDir/SpellWork` on every command (repo ownership mismatch). Every `git` line below is written as `git -c safe.directory=F:/WorkDir/SpellWork ...`.
- Client build 18414, `MaxLevel = 90`, `MAX_CLASSES = 12`.
- Every structure's ID field carries `[Index(true)]`. **Without it DBFileReaderLib keys WDBC/WDB2 rows by record position, not ID** (verified: spell key 133 returned "Shadow Word: Pain", ID 589).
- When `X.db2` and `X.dbc` both exist, load `X.db2`.
- A `difficulty` argument is a `Difficulty.dbc` id. 0 is the base and has no Difficulty row. Valid rows: 1,2,3,4,5,6,7,8,9,11,12,14.
- Every tool keeps `main`'s envelope (`ToolResponse`), its 500-row caps and its `truncated`/`totalMatches` semantics.
- Reference data for local runs: `SPELLWORK_DBC_PATH=C:/MoP/MOPPREMIUM/Repack/dbc`, `SPELLWORK_DB2_PATH=C:/MoP/MOPPREMIUM/Repack/db2`, `SPELLWORK_LOCALE=enUS` (the folder doesn't exist, so the root is used). MySQL: host `127.0.0.1`, port `3306`, user `root`, password `ascent`, db `world`. Only for the developer's local runs; never commit these values.
- Test commands: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj -c Release --nologo` and `dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj -c Release --nologo`. Baseline before Task 1: Core 35 passed / 80 skipped, Mcp 205 passed / 59 skipped, with no data env vars set.
- "Run with data" below means: set the reference-data env vars above in the same shell, then run the test command.

## Review Focus

1. **Stale `.dbc` beside a correct `.db2`.** `SpellVisual.dbc` (38 fields, 5.3 layout) sits next to `SpellVisual.db2` (30 fields). A user who sets only `SPELLWORK_DBC_PATH` must still get the `.db2`, and `server_info` must name the file it opened. Pinned in Task 2 (resolver test) and Task 3 (data test).
2. **Records keyed by position.** An `[Index(true)]` left off any structure gives plausible-but-wrong rows. Pinned in Task 3 by a reflection test over every store's entry type, plus a data test that `Spell[133].Name == "Fireball"`.
3. **Difficulty with a partial override.** A difficulty that overrides some effect indices but not others has to mix rows and say which difficulty each one came from. Pinned in Task 4 (43093 Grievous Throw at difficulty 1 → base rows; at difficulty 2 → both rows from 2).
4. **Negative scaling class.** `SpellScaling.Class <= -1` selects generic gtSpellScaling columns (`MAX_CLASSES - 2 - class`). Getting it wrong yields a zero or a wrong-class value, not an error. Pinned in Task 5.
5. **Missing locale folder.** The default `SPELLWORK_LOCALE=enUS` doesn't exist in the reference layout. That must fall back to the root and be reported, not fail the load. Pinned in Task 2.

---

## File Structure

| Path | Responsibility | Task |
|---|---|---|
| `SpellWork.sln`, `SpellWork/` (deleted), `.github/workflows/ci.yml`, `coverity.yml` | Drop WinForms; build with `dotnet` | 1 |
| `SpellWork.Core/DBC/ClientFileResolver.cs` (new) | Map a table name to the one file to open | 2 |
| `SpellWork.Core/SpellWorkConfig.cs`, `SpellWork.Mcp/McpServerConfig.cs` | `Db2Path`; drop hotfix; GT optional | 2 |
| `SpellWork.Core/DBC/LayoutVerification.cs` | Expected (fields, record size) per table | 3 |
| `SpellWork.Core/DBC/Structures/*.cs` | 5.4.8 row types | 3 |
| `SpellWork.Core/DBC/DBC.cs` | Store list, loader, `BuildSpellInfoStore` | 3, 4 |
| `SpellWork.Core/DBC/DifficultyIndex.cs` (new) | `(spellId, difficulty)` → row, with base fallback | 4 |
| `SpellWork.Core/Spell/SpellEnums.cs` | 5.4.8 enums | 4 |
| `SpellWork.Core/Spell/SpellInfo.cs`, `RenderContext.cs` | 5.4.8 spell model + text rendering | 4, 6 |
| `SpellWork.Core/Spell/SpellScalingCalculator.cs` (new) | SkyFire_548 `CalcValue` port | 5 |
| `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs` | 5.4.8 JSON | 6 |
| `SpellWork.Core/Spell/SpellVisualChain*.cs`, `SpellVisualSection.cs` | 5.4.8 chain | 7 |
| `SpellWork.Mcp/Tools/*.cs`, `SpellDataService.cs`, `EnumCatalog.cs` | Tool surface changes | 4, 7, 8 |
| `SpellWork.Mcp/Tools/DatabaseTools.cs`, `SpellWork.Core/Database/*` | Override tables | 9 |
| `docs/mcp-server.md`, `README.md` | 5.4.8 docs | 10 |

Deleted (8.3.7-only): `ContentTuningEntry`, `ContentTuningXExpectedEntry`, `ExpectedStatEntry`, `ExpectedStatModEntry`, `ItemEffectEntry`, `AreaGroupMemberEntry`, `SpellNameEntry`, `SpellXSpellVisualEntry`, `SpellVisualEventEntry`, `SpellVisualKitEffectEntry`, `SpellReagentsCurrencyEntry`, `SpellXDescriptionVariables`, `MapDifficultyEntry`, `Spell/ExpectedStat.cs`, `GameTables/` (the folder; gtSpellScaling becomes a DBC store).

---

### Task 1: Branch hygiene: drop WinForms, build with dotnet

**Files:**
- Modify: `SpellWork.sln`, `.github/workflows/ci.yml`, `.github/workflows/coverity.yml`
- Delete: `SpellWork/` (whole directory)

**Interfaces:** Consumes nothing. Produces a solution that `dotnet build SpellWork.sln -c Release` builds.

- [ ] **Step 1: Remove the project from the solution**

```bash
cd F:/WorkDir/SpellWork
dotnet sln SpellWork.sln remove SpellWork/SpellWork.csproj
git -c safe.directory=F:/WorkDir/SpellWork rm -r -q SpellWork
```

- [ ] **Step 2: Check nothing else references it**

Run: `grep -rn "SpellWork.csproj\|SpellWork\\\\SpellWork\|net48" --include=*.csproj --include=*.sln --include=*.yml --include=*.md .`
Expected: hits only in `README.md` and `docs/` (fixed in Task 10). If a `.csproj` hits, remove that `ProjectReference`.

- [ ] **Step 3: Switch CI to dotnet**

In `.github/workflows/ci.yml`, delete the `microsoft/setup-msbuild@v3` step and replace the two msbuild steps with:

```yaml
      - name: Restore
        run: dotnet restore SpellWork.sln --nologo
      - name: Build
        run: dotnet build SpellWork.sln -c $env:CONFIGURATION --no-restore --nologo
```

Delete any step that zips or uploads `SpellWork.exe`/`SpellWork.zip`. Keep the `dotnet publish SpellWork.Mcp\SpellWork.Mcp.csproj ... -o mcp-publish` step, and rename its uploaded artifact from whatever it is to `SpellWork.Mcp-5.4.8`. In `coverity.yml`, replace `cov-build --dir cov-int msbuild SpellWork.sln -t:Rebuild ...` with `cov-build --dir cov-int dotnet build SpellWork.sln -c $env:CONFIGURATION --no-incremental --nologo`, and delete its setup-msbuild and msbuild-restore steps in favour of `dotnet restore SpellWork.sln --nologo`.

- [ ] **Step 4: Build and test**

Run: `dotnet build SpellWork.sln -c Release --nologo` → `0 Fehler`/`0 Error(s)`.
Run both test commands → same counts as the baseline in Global Constraints.

- [ ] **Step 5: Commit**

```bash
git -c safe.directory=F:/WorkDir/SpellWork add -A
git -c safe.directory=F:/WorkDir/SpellWork commit -m "5.4.8: drop WinForms project, build with dotnet"
```

---

### Task 2: File resolution and configuration

**Files:**
- Create: `SpellWork.Core/DBC/ClientFileResolver.cs`
- Modify: `SpellWork.Core/SpellWorkConfig.cs`, `SpellWork.Mcp/McpServerConfig.cs`
- Test: `SpellWork.Core.Tests/ClientFileResolverTests.cs` (new), `SpellWork.Mcp.Tests/McpServerConfigTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class ResolvedClientFile { string Path; string Extension /* "db2" | "dbc" */; bool LocaleFallback; }`
  - `public static ResolvedClientFile ClientFileResolver.Resolve(string fileName, SpellWorkConfig config)`. Returns null if no file exists.
  - `public static string ClientFileResolver.EffectiveDbcDirectory(SpellWorkConfig config, out bool localeFallback)`
  - `SpellWorkConfig.Db2Path` (string, nullable). `HotfixCachePath` is removed. `GtPath` stays, but nothing reads it any more.

Resolution rule. Candidate directories, in order: `<DbcPath>/<Locale>` if that directory exists (otherwise `<DbcPath>`, with `LocaleFallback = true`), then `Db2Path` if set. For the `.db2` extension, check every candidate directory first. Only if no `.db2` exists anywhere, check every candidate for `.dbc`. File names are tried as given (`Item-sparse` keeps its hyphen).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.IO;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class ClientFileResolverTests : System.IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "swres-" + System.Guid.NewGuid().ToString("N"));
        public ClientFileResolverTests() { Directory.CreateDirectory(_root); }
        public void Dispose() { Directory.Delete(_root, true); }

        private string Touch(params string[] parts)
        {
            var p = Path.Combine(_root, Path.Combine(parts));
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, new byte[] { 0x57, 0x44, 0x42, 0x43 });
            return p;
        }

        [Fact]
        public void Db2WinsOverDbcInSameDirectory()
        {
            Touch("dbc", "SpellVisual.dbc");
            var db2 = Touch("dbc", "SpellVisual.db2");
            var r = ClientFileResolver.Resolve("SpellVisual", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Locale = "enUS" });
            Assert.Equal(db2, r.Path);
            Assert.Equal("db2", r.Extension);
        }

        [Fact]
        public void Db2InDb2PathWinsOverDbcInDbcPath()
        {
            Touch("dbc", "SpellVisual.dbc");
            var db2 = Touch("db2", "SpellVisual.db2");
            var r = ClientFileResolver.Resolve("SpellVisual", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Db2Path = Path.Combine(_root, "db2") });
            Assert.Equal(db2, r.Path);
        }

        [Fact]
        public void MissingLocaleFolderFallsBackToRootAndSaysSo()
        {
            var dbc = Touch("dbc", "Spell.dbc");
            var r = ClientFileResolver.Resolve("Spell", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Locale = "enUS" });
            Assert.Equal(dbc, r.Path);
            Assert.True(r.LocaleFallback);
        }

        [Fact]
        public void ExistingLocaleFolderIsPreferred()
        {
            Touch("dbc", "Spell.dbc");
            var ru = Touch("dbc", "ruRU", "Spell.dbc");
            var r = ClientFileResolver.Resolve("Spell", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc"), Locale = "ruRU" });
            Assert.Equal(ru, r.Path);
            Assert.False(r.LocaleFallback);
        }

        [Fact]
        public void HyphenatedNameResolves()
        {
            var p = Touch("dbc", "Item-sparse.db2");
            Assert.Equal(p, ClientFileResolver.Resolve("Item-sparse", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") }).Path);
        }

        [Fact]
        public void AbsentFileIsNull()
        {
            Directory.CreateDirectory(Path.Combine(_root, "dbc"));
            Assert.Null(ClientFileResolver.Resolve("Nope", new SpellWorkConfig { DbcPath = Path.Combine(_root, "dbc") }));
        }
    }
}
```

- [ ] **Step 2: Run them and confirm they fail**

Run: `dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj -c Release --nologo --filter ClientFileResolverTests`
Expected: build error, `ClientFileResolver` does not exist.

- [ ] **Step 3: Implement**

`SpellWork.Core/SpellWorkConfig.cs`: delete `HotfixCachePath` and add `public string Db2Path { get; set; }` after `DbcPath`.

`SpellWork.Core/DBC/ClientFileResolver.cs`:

```csharp
using System.Collections.Generic;
using System.IO;

namespace SpellWork.DBC
{
    public sealed class ResolvedClientFile
    {
        public string Path { get; set; }
        public string Extension { get; set; }
        public bool LocaleFallback { get; set; }
    }

    /// <summary>
    /// Picks the one file a store loads from. A 5.4.8 extraction can hold a
    /// stale pre-5.4 <c>.dbc</c> next to the current <c>.db2</c> of the same
    /// table (SpellVisual, SpellVisualEffectName in the reference data), so
    /// <c>.db2</c> is searched everywhere before <c>.dbc</c> is considered.
    /// </summary>
    public static class ClientFileResolver
    {
        public static string EffectiveDbcDirectory(SpellWorkConfig config, out bool localeFallback)
        {
            localeFallback = false;
            if (string.IsNullOrEmpty(config.DbcPath))
                return null;
            if (!string.IsNullOrEmpty(config.Locale))
            {
                var localized = System.IO.Path.Combine(config.DbcPath, config.Locale);
                if (Directory.Exists(localized))
                    return localized;
            }
            localeFallback = true;
            return config.DbcPath;
        }

        public static ResolvedClientFile Resolve(string fileName, SpellWorkConfig config)
        {
            var dbcDir = EffectiveDbcDirectory(config, out var fallback);
            var dirs = new List<string>();
            if (dbcDir != null) dirs.Add(dbcDir);
            if (!string.IsNullOrEmpty(config.Db2Path)) dirs.Add(config.Db2Path);

            foreach (var ext in new[] { "db2", "dbc" })
                foreach (var dir in dirs)
                {
                    var candidate = System.IO.Path.Combine(dir, fileName + "." + ext);
                    if (File.Exists(candidate))
                        return new ResolvedClientFile { Path = candidate, Extension = ext, LocaleFallback = fallback };
                }
            return null;
        }
    }
}
```

`SpellWork.Mcp/McpServerConfig.cs`: remove `HotfixCachePath` and the `SPELLWORK_HOTFIX_CACHE` read; add `Db2Path = Get("SPELLWORK_DB2_PATH")`; change the doc comment from "nine" to "nine" (still nine: `DB2_PATH` replaces `HOTFIX_CACHE`). Wherever `McpServerConfig` builds a `SpellWorkConfig`, copy `Db2Path` across. Where config validation reports a missing `SPELLWORK_GT_PATH` as an error, drop that check: GT is no longer required. Grep `GtPath` in `SpellWork.Mcp` and remove every "required" check on it.

- [ ] **Step 4: Update the config tests**

In `SpellWork.Mcp.Tests/McpServerConfigTests.cs`, delete assertions that mention `SPELLWORK_HOTFIX_CACHE`/`HotfixCachePath` or require `SPELLWORK_GT_PATH`, and add:

```csharp
[Fact]
public void ReadsDb2Path()
{
    var c = McpServerConfig.FromEnvironment(new Dictionary<string, string>
    {
        ["SPELLWORK_DBC_PATH"] = "C:/x/dbc",
        ["SPELLWORK_DB2_PATH"] = "C:/x/db2",
    });
    Assert.Equal("C:/x/db2", c.Db2Path);
}
```

Fix every compile error in the test projects caused by the removed `HotfixCachePath` the same way: delete the line.

- [ ] **Step 5: Run the tests**

Run both test commands. Expected: `ClientFileResolverTests` 6 passed, and everything else passes (counts as baseline +7 passed, minus deleted hotfix assertions).

- [ ] **Step 6: Commit**

```bash
git -c safe.directory=F:/WorkDir/SpellWork add -A
git -c safe.directory=F:/WorkDir/SpellWork commit -m "5.4.8: resolve client files .db2-first with optional locale folder"
```

---

### Task 3: 5.4.8 structures, store list, loader and layout verification

The model swap starts here. Core won't compile between Step 3 and the end of Task 4. **Tasks 3 and 4 are reviewed together, as one gate**, and are committed only after Task 4's Step 9 is green. Commit locally at the end of each task anyway, with a `WIP` prefix, and squash both commits when Task 4 is done.

**Files:**
- Modify: `SpellWork.Core/DBC/LayoutVerification.cs`, `SpellWork.Core/DBC/DBC.cs`
- Replace: every file in `SpellWork.Core/DBC/Structures/`
- Delete: the 8.3.7-only structures listed in File Structure, and `SpellWork.Core/GameTables/`
- Test: `SpellWork.Core.Tests/LayoutVerificationTests.cs` (rewrite), `SpellWork.Core.Tests/DbcLoadTests.cs` (rewrite)

**Interfaces:**
- Produces:
  - `LayoutVerification.Expected : IReadOnlyDictionary<string, (int FieldsCount, int RecordSize)>`, keyed by file name (`"Item-sparse"`, not `"ItemSparse"`).
  - `LayoutVerification.Check(string fileName, string openedPath, int fieldsCount, int recordSize) → LayoutMismatch` (null if it matches or isn't listed).
  - `LayoutMismatch { string Table; string File; int ExpectedFieldsCount; int ActualFieldsCount; int ExpectedRecordSize; int ActualRecordSize; string Describe(); }`
  - `DBC.OpenedFiles : IReadOnlyDictionary<string /*store property*/, ResolvedClientFile>`
  - `DBC.LocaleFallback : bool`
  - `DBC.Version = "SpellWork 5.4.8 (18414)"`, `DBC.MaxLevel = 90`, `DBC.MaxClasses = 12`
  - Store properties named as in the store table below. The property name is the file name, except `ItemSparse` → file `Item-sparse` and `GtSpellScaling` → file `gtSpellScaling`. The mapping lives in `DBC.FileNameFor(PropertyInfo)`.

**Structure rules (for every file):**
1. `using DBFileReaderLib.Attributes;`, `namespace SpellWork.DBC.Structures`, `public sealed class <Name>Entry`.
2. First field `[Index(true)] public int ID;`.
3. Arrays: `[Cardinality(n)] public T[] Name = new T[n];`.
4. Strings in 5.4.8 WDBC/WDB2 are single `string` fields (no locstrings). This was verified on Spell.dbc: 25 fields, 100 bytes.
5. Declare fields in file order. Don't skip unknown columns; name them `Unk<index>`.
6. Comment line 1 of each class with its source: `// 5.4.8 layout: WoWDBDefs <Table>.dbd BUILD 5.4.0.17266-5.4.8.18414` or `// 5.4.8 layout: upstream TrinityCore/SpellWork 5.4.8 SpellWork/DBC/Structures/<File>.cs`.

**Store table.** Columns: property, file, the source of the field list, and the expected (fields, record size) for `LayoutVerification.Expected`.

| Property | File | Source of field list | Fields | Size |
|---|---|---|---|---|
| Spell | Spell.dbc | code below | 25 | 100 |
| SpellMisc | SpellMisc.dbc | code below | 26 | 104 |
| SpellEffect | SpellEffect.dbc | code below | 30 | 120 |
| SpellEffectScaling | SpellEffectScaling.dbc | code below | 6 | 24 |
| SpellPower | SpellPower.dbc | code below | 13 | 52 |
| SpellTargetRestrictions | SpellTargetRestrictions.dbc | code below | 9 | 36 |
| SpellAuraOptions | SpellAuraOptions.dbc | code below | 9 | 36 |
| SpellCategories | SpellCategories.dbc | code below | 10 | 40 |
| SpellCooldowns | SpellCooldowns.dbc | code below | 6 | 24 |
| SpellInterrupts | SpellInterrupts.dbc | code below | 8 | 32 |
| SpellLevels | SpellLevels.dbc | code below | 6 | 24 |
| SpellScaling | SpellScaling.dbc | code below | 9 | 36 |
| SpellRuneCost | SpellRuneCost.dbc | code below | 6 | 24 |
| GtSpellScaling | gtSpellScaling.dbc | code below | 2 | 8 |
| Difficulty | Difficulty.dbc | code below | 12 | 48 |
| SpellVisual | SpellVisual.db2 | code below | 30 | 120 |
| SpellVisualKit | SpellVisualKit.db2 | code below | 39 | 156 |
| SpellVisualMissile | SpellVisualMissile.db2 | code below | 17 | 68 |
| SpellVisualEffectName | SpellVisualEffectName.db2 | code below | 9 | 36 |
| SpellVisualKitModelAttach | SpellVisualKitModelAttach.db2 | code below | 15 | 60 |
| SpellVisualKitAreaModel | SpellVisualKitAreaModel.db2 | code below | 7 | 28 |
| SpellAuraRestrictions | SpellAuraRestrictions.dbc | upstream 5.4.8 | 11 | 44 |
| SpellCastingRequirements | SpellCastingRequirements.dbc | upstream 5.4.8 | 7 | 28 |
| SpellCastTimes | SpellCastTimes.dbc | upstream 5.4.8 | 4 | 16 |
| SpellClassOptions | SpellClassOptions.dbc | upstream 5.4.8 | 7 | 28 |
| SpellDescriptionVariables | SpellDescriptionVariables.dbc | upstream 5.4.8 | 2 | 8 |
| SpellDuration | SpellDuration.dbc | upstream 5.4.8 | 4 | 16 |
| SpellEquippedItems | SpellEquippedItems.dbc | upstream 5.4.8 | 6 | 24 |
| SpellRadius | SpellRadius.dbc | upstream 5.4.8 | 5 | 20 |
| SpellRange | SpellRange.dbc | upstream 5.4.8 | 8 | 32 |
| SpellShapeshift | SpellShapeshift.dbc | upstream 5.4.8 (two `ulong` = 4 fields) | 6 | 24 |
| SpellTotems | SpellTotems.dbc | upstream 5.4.8 | 5 | 20 |
| SpellReagents | SpellReagents.db2 | upstream 5.4.8 | 19 | 76 |
| SpellMissile | SpellMissile.db2 | upstream 5.4.8 | 15 | 60 |
| SpellMissileMotion | SpellMissileMotion.db2 | upstream 5.4.8 | 5 | 20 |
| AreaGroup | AreaGroup.dbc | upstream 5.4.8 | 8 | 32 |
| AreaTable | AreaTable.dbc | upstream 5.4.8 | 30 | 120 |
| OverrideSpellData | OverrideSpellData.dbc | upstream 5.4.8 | 13 | 52 |
| ScreenEffect | ScreenEffect.dbc | upstream 5.4.8 | 14 | 56 |
| SkillLine | SkillLine.dbc | upstream 5.4.8 | 9 | 36 |
| SkillLineAbility | SkillLineAbility.dbc | upstream 5.4.8 | 13 | 52 |
| Item | Item.db2 | upstream 5.4.8 | 8 | 32 |
| ItemSparse | Item-sparse.db2 | upstream 5.4.8 | 134 | 536 |
| SpellProcsPerMinute | SpellProcsPerMinute.dbc | WoWDBDefs | 3 | 12 |
| RandPropPoints | RandPropPoints.dbc | WoWDBDefs | 16 | 64 |
| SpellChainEffects | SpellChainEffects.dbc | WoWDBDefs (has sub-32-bit columns; WDBC still stores 4 bytes per column, so declare every column `int`/`float`/`string` and get 58 declared fields) | 58 | 220 |

"upstream 5.4.8" means the file at `git -c safe.directory=F:/WorkDir/SpellWork show upstream/5.4.8:SpellWork/DBC/Structures/<Name>Entry.cs`. Run `git -c safe.directory=F:/WorkDir/SpellWork fetch https://github.com/TrinityCore/SpellWork.git 5.4.8:refs/remotes/upstream/5.4.8` first if that ref is missing. Port it field for field and apply the structure rules: replace `[StoragePresence(..., ArraySize = n)]` with `[Cardinality(n)]`, delete every property and method (navigation lives in `SpellInfo`), and rename `uint Id` to `[Index(true)] public int ID`. "WoWDBDefs" means `https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/<Table>.dbd`, in the block whose BUILD line covers 18414.

- [ ] **Step 1: Write the layout tests first**

Replace `SpellWork.Core.Tests/LayoutVerificationTests.cs` with:

```csharp
using System.Linq;
using System.Reflection;
using DBFileReaderLib.Attributes;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class LayoutVerificationTests
    {
        private readonly ClientDataFixture _fixture;
        public LayoutVerificationTests(ClientDataFixture fixture) => _fixture = fixture;

        [Fact]
        public void EveryStoreHasAnExpectedLayout()
        {
            var missing = DBC.DBC.StoreFileNames().Where(n => !LayoutVerification.Expected.ContainsKey(n)).ToList();
            Assert.Empty(missing);
        }

        // DBFileReaderLib keys WDBC/WDB2 rows by record position unless the
        // ID field is [Index(true)]; leaving it off yields plausible wrong rows.
        [Fact]
        public void EveryEntryTypeIndexesItsIdField()
        {
            foreach (var t in DBC.DBC.StoreEntryTypes())
            {
                var id = t.GetField("ID", BindingFlags.Public | BindingFlags.Instance);
                Assert.True(id != null, $"{t.Name} has no ID field");
                Assert.True(t.GetFields(BindingFlags.Public | BindingFlags.Instance).First().Name == "ID", $"{t.Name}: ID is not the first field");
                var index = id.GetCustomAttribute<IndexAttribute>();
                Assert.True(index != null && index.NonInline, $"{t.Name}.ID lacks [Index(true)]");
            }
        }

        [Fact]
        public void MatchingLayoutIsNotAMismatch() =>
            Assert.Null(LayoutVerification.Check("Spell", "x/Spell.dbc", 25, 100));

        [Fact]
        public void DifferentRecordSizeIsAMismatchNamingTheFile()
        {
            var m = LayoutVerification.Check("SpellVisual", "x/SpellVisual.dbc", 38, 152);
            Assert.NotNull(m);
            Assert.Contains("SpellVisual.dbc", m.Describe());
            Assert.Equal(30, m.ExpectedFieldsCount);
            Assert.Equal(120, m.ExpectedRecordSize);
        }

        [SkippableFact]
        public void ReferenceDataHasNoMismatches()
        {
            _fixture.SkipIfNoClientData();
            Assert.Empty(DBC.DBC.LayoutMismatches.Select(m => m.Describe()));
        }

        [SkippableFact]
        public void StaleDbcBesideDb2IsNotTheOneOpened()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal("db2", DBC.DBC.OpenedFiles["SpellVisual"].Extension);
            Assert.Equal("db2", DBC.DBC.OpenedFiles["SpellVisualEffectName"].Extension);
        }
    }
}
```

Check the collection name the existing tests use (`grep -rn "CollectionDefinition" SpellWork.Core.Tests`) and use that name instead of `"ClientData"` if it differs.

Replace `SpellWork.Core.Tests/DbcLoadTests.cs` with tests that pin keying and counts:

```csharp
using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class DbcLoadTests
    {
        private readonly ClientDataFixture _fixture;
        public DbcLoadTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void SpellIsKeyedById()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal("Fireball", DBC.DBC.Spell[133].Name);
            Assert.Equal("Shadow Word: Pain", DBC.DBC.Spell[589].Name);
        }

        [SkippableFact]
        public void ReferenceRowCounts()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal(94842, DBC.DBC.Spell.Count);
            Assert.Equal(134760, DBC.DBC.SpellEffect.Count);
            Assert.Equal(111077, DBC.DBC.SpellMisc.Count);
            Assert.Equal(28214, DBC.DBC.SpellVisual.Count);
            Assert.Equal(15409, DBC.DBC.SpellVisualEffectName.Count);
            Assert.Equal(1700, DBC.DBC.GtSpellScaling.Count);
        }

        [SkippableFact]
        public void FireballMiscRow()
        {
            _fixture.SkipIfNoClientData();
            var misc = DBC.DBC.SpellMisc[DBC.DBC.Spell[133].MiscID];
            Assert.Equal(68, misc.ID);
            Assert.Equal(133, misc.SpellID);
            Assert.Equal(4, misc.SchoolMask);
            Assert.Equal(28f, misc.Speed);
            Assert.Equal(67, misc.SpellVisualID[0]);
        }

        [SkippableFact]
        public void LocaleFallbackIsReported()
        {
            _fixture.SkipIfNoClientData();
            Assert.True(DBC.DBC.LocaleFallback); // reference data has no enUS folder
        }
    }
}
```

Update `ClientDataFixture`: require only `SPELLWORK_DBC_PATH` (drop the `SPELLWORK_GT_PATH` condition and skip-reason mention), and pass `Db2Path = Environment.GetEnvironmentVariable("SPELLWORK_DB2_PATH")`.

- [ ] **Step 2: Write the verified structures**

These are taken from WoWDBDefs and checked against the reference data during design. Create each as its own file:

```csharp
// SpellEntry.cs — 5.4.8 layout: WoWDBDefs Spell.dbd BUILD 5.4.0.17266-5.4.8.18414 (strings unlocalized in WDBC)
public sealed class SpellEntry
{
    [Index(true)] public int ID;
    public string Name;
    public string NameSubtext;
    public string Description;
    public string AuraDescription;
    public int RuneCostID;
    public int SpellMissileID;
    public int DescriptionVariablesID;
    public int DifficultyID;          // WoWDBDefs: BonusCoefficient float; upstream: SpellDifficultyId. Read as int; unused.
    public int ScalingID;
    public int AuraOptionsID;
    public int AuraRestrictionsID;
    public int CastingRequirementsID;
    public int CategoriesID;
    public int ClassOptionsID;
    public int CooldownsID;
    public int EquippedItemsID;
    public int InterruptsID;
    public int LevelsID;
    public int ReagentsID;
    public int ShapeshiftID;
    public int TargetRestrictionsID;
    public int TotemsID;
    public int RequiredProjectID;
    public int MiscID;
}

// SpellMiscEntry.cs — 5.4.8 file has 14 attribute words (26 fields); WoWDBDefs' 5.4.7 block has 13.
public sealed class SpellMiscEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int DifficultyID;
    [Cardinality(14)] public uint[] Attributes = new uint[14];
    public int CastingTimeIndex;
    public int DurationIndex;
    public int RangeIndex;
    public float Speed;
    [Cardinality(2)] public int[] SpellVisualID = new int[2];
    public int SpellIconID;
    public int ActiveIconID;
    public int SchoolMask;
}

// SpellEffectEntry.cs
public sealed class SpellEffectEntry
{
    [Index(true)] public int ID;
    public int DifficultyID;
    public int Effect;
    public float EffectAmplitude;
    public int EffectAura;
    public int EffectAuraPeriod;
    public int EffectBasePoints;
    public float EffectBonusCoefficient;
    public float EffectChainAmplitude;
    public int EffectChainTargets;
    public int EffectDieSides;
    public int EffectItemType;
    public int EffectMechanic;
    [Cardinality(2)] public int[] EffectMiscValue = new int[2];
    public float EffectPointsPerResource;
    [Cardinality(2)] public int[] EffectRadiusIndex = new int[2];
    public float EffectRealPointsPerLevel;
    [Cardinality(4)] public uint[] EffectSpellClassMask = new uint[4];
    public int EffectTriggerSpell;
    public float EffectPosFacing;
    [Cardinality(2)] public int[] ImplicitTarget = new int[2];
    public int SpellID;
    public int EffectIndex;
    public int EffectAttributes;
}

// SpellEffectScalingEntry.cs
public sealed class SpellEffectScalingEntry
{
    [Index(true)] public int ID;
    public float Coefficient;          // SkyFire: Multiplier
    public float Variance;             // SkyFire: RandomPointsMultiplier
    public float ResourceCoefficient;  // SkyFire: OtherMultiplier (combo)
    public float Unk4;
    public int SpellEffectID;
}

// SpellPowerEntry.cs — no DifficultyID in 5.4.8; several rows per spell ordered by OrderIndex
public sealed class SpellPowerEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int OrderIndex;
    public int PowerType;
    public int ManaCost;
    public int ManaCostPerLevel;
    public int ManaPerSecond;
    public int PowerDisplayID;
    public int AltPowerBarID;
    public float PowerCostPct;
    public float PowerPctPerSecond;
    public int RequiredAuraSpellID;
    public float Unk12;
}

// SpellTargetRestrictionsEntry.cs
public sealed class SpellTargetRestrictionsEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int DifficultyID;
    public float ConeAngle;
    public float Width;
    public int MaxTargets;
    public int MaxTargetLevel;
    public int TargetCreatureType;
    public int Targets;
}

// SpellAuraOptionsEntry.cs
public sealed class SpellAuraOptionsEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int DifficultyID;
    public int CumulativeAura;
    public int ProcChance;
    public int ProcCharges;
    public int ProcTypeMask;
    public int ProcCategoryRecovery;
    public int SpellProcsPerMinuteID;
}

// SpellCategoriesEntry.cs
public sealed class SpellCategoriesEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int DifficultyID;
    public int Category;
    public int DefenseType;
    public int DispelType;
    public int Mechanic;
    public int PreventionType;
    public int StartRecoveryCategory;
    public int ChargeCategory;
}

// SpellCooldownsEntry.cs
public sealed class SpellCooldownsEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int DifficultyID;
    public int CategoryRecoveryTime;
    public int RecoveryTime;
    public int StartRecoveryTime;
}

// SpellInterruptsEntry.cs
public sealed class SpellInterruptsEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int DifficultyID;
    [Cardinality(2)] public int[] AuraInterruptFlags = new int[2];
    [Cardinality(2)] public int[] ChannelInterruptFlags = new int[2];
    public int InterruptFlags;
}

// SpellLevelsEntry.cs
public sealed class SpellLevelsEntry
{
    [Index(true)] public int ID;
    public int SpellID;
    public int DifficultyID;
    public int BaseLevel;
    public int MaxLevel;
    public int SpellLevel;
}

// SpellScalingEntry.cs — keyed by Spell.ScalingID, no SpellID column
public sealed class SpellScalingEntry
{
    [Index(true)] public int ID;
    public int CastTimeMin;
    public int CastTimeMax;
    public int CastTimeMaxLevel;
    public int Class;
    public float NerfFactor;       // SkyFire: CoefBase
    public int NerfMaxLevel;       // SkyFire: CoefLevelBase
    public int MaxScalingLevel;    // SkyFire: MaxLevel
    public int ScalesFromItemLevel;
}

// SpellRuneCostEntry.cs
public sealed class SpellRuneCostEntry
{
    [Index(true)] public int ID;
    public int Blood;
    public int Unholy;
    public int Frost;
    public int Chromatic;
    public int RunicPower;
}

// GtSpellScalingEntry.cs — row key = (class column) * 100 + level - 1
public sealed class GtSpellScalingEntry
{
    [Index(true)] public int ID;
    public float Value;
}

// DifficultyEntry.cs
public sealed class DifficultyEntry
{
    [Index(true)] public int ID;
    public int FallbackDifficultyID;
    public int InstanceType;
    public int MinPlayers;
    public int MaxPlayers;
    public int OldEnumValue;
    public int Flags;
    public int ToggleDifficultyID;
    public int GroupSizeHealthCurveID;
    public int GroupSizeDmgCurveID;
    public int GroupSizeSpellPointsCurveID;
    public string Name;
}

// SpellVisualEntry.cs — the .db2 (30 fields). The .dbc of the same name in the reference data is a stale 5.3 layout.
public sealed class SpellVisualEntry
{
    [Index(true)] public int ID;
    public int Unk1;
    public int PrecastKit;
    public int CastKit;
    public int ImpactKit;
    public int StateKit;
    public int StateDoneKit;
    public int ChannelKit;
    public int MissileDestinationAttachment;
    public int AnimEventSoundID;
    public int Unk10;
    public int CasterImpactKit;
    public int TargetImpactKit;
    public int MissileAttachment;
    public int MissileMotion;
    public int MissileTargetingKit;
    public int InstantAreaKit;
    public int ImpactAreaKit;
    public int PersistentAreaKit;
    [Cardinality(3)] public float[] MissileCastOffset = new float[3];
    [Cardinality(3)] public float[] MissileImpactOffset = new float[3];
    public int Unk21;
    public int Unk22;
    public int Unk23;
    public int Unk24;
    public int SpellVisualMissileSetID;
}

// SpellVisualKitEntry.cs
public sealed class SpellVisualKitEntry
{
    [Index(true)] public int ID;
    public int StartAnimID;
    public int AnimID;
    public int AnimKitID;
    public int HeadEffect;
    public int ChestEffect;
    public int BaseEffect;
    public int LeftHandEffect;
    public int RightHandEffect;
    public int BreathEffect;
    public int LeftWeaponEffect;
    public int RightWeaponEffect;
    [Cardinality(3)] public int[] SpecialEffect = new int[3];
    public int WorldEffect;
    public int SoundID;
    public int ShakeID;
    [Cardinality(4)] public int[] CharProc = new int[4];
    [Cardinality(4)] public float[] CharParamZero = new float[4];
    [Cardinality(4)] public float[] CharParamOne = new float[4];
    [Cardinality(4)] public float[] CharParamTwo = new float[4];
    [Cardinality(4)] public float[] CharParamThree = new float[4];
    public int Flags;
}

// SpellVisualMissileEntry.cs
public sealed class SpellVisualMissileEntry
{
    [Index(true)] public int ID;
    public int SpellVisualMissileSetID;
    public int SpellVisualEffectNameID;
    public int DestinationAttachment;
    public int SoundEntriesID;
    public int Attachment;
    public int Unk6;
    public int FollowGroundDropSpeed;
    public int FollowGroundApproach;
    public int Flags;
    public int SpellMissileMotionID;
    [Cardinality(3)] public float[] ImpactOffset = new float[3];
    [Cardinality(3)] public float[] CastOffset = new float[3];
}

// SpellVisualEffectNameEntry.cs — WDB2 packs Type into one byte + 3 padding bytes (9 fields, 36 bytes)
public sealed class SpellVisualEffectNameEntry
{
    [Index(true)] public int ID;
    public string Name;
    public float AreaEffectSize;
    public float Scale;
    public float MinAllowedScale;
    public float MaxAllowedScale;
    public byte Type;
    [Cardinality(3)] public byte[] Padding = new byte[3];
    public float Alpha;
    public int Flags;
}

// SpellVisualKitModelAttachEntry.cs
public sealed class SpellVisualKitModelAttachEntry
{
    [Index(true)] public int ID;
    public int ParentSpellVisualKitID;
    public int SpellVisualEffectNameID;
    public int AttachmentID;
    [Cardinality(3)] public float[] Offset = new float[3];
    public float Yaw;
    public float Pitch;
    public float Roll;
    public int Unk8;
    public int StartAnimID;
    public int AnimID;
    public int EndAnimID;
    public int AnimKitID;
}

// SpellVisualKitAreaModelEntry.cs — not reachable from the chain in 5.4.8 (no parent-kit column)
public sealed class SpellVisualKitAreaModelEntry
{
    [Index(true)] public int ID;
    public int ModelFileDataID;
    public int Flags;
    public int LifeTime;
    public float EmissionRate;
    public float Spacing;
    public float ModelScale;
}
```

Each file carries the `using DBFileReaderLib.Attributes;` + `namespace SpellWork.DBC.Structures` wrapper. Then port the "upstream 5.4.8" and "WoWDBDefs" rows of the store table following the structure rules.

- [ ] **Step 3: Rewrite `LayoutVerification.cs`**

```csharp
using System.Collections.Generic;
using System.Globalization;

namespace SpellWork.DBC
{
    public sealed class LayoutMismatch
    {
        public string Table { get; set; }
        public string File { get; set; }
        public int ExpectedFieldsCount { get; set; }
        public int ActualFieldsCount { get; set; }
        public int ExpectedRecordSize { get; set; }
        public int ActualRecordSize { get; set; }

        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0}: {1} fields / {2}-byte records (expected {3} / {4}). The structure was written against the 5.4.8 (18414) layout; " +
            "a different file (stale extraction, other build) decodes into plausible but wrong values.",
            File, ActualFieldsCount, ActualRecordSize, ExpectedFieldsCount, ExpectedRecordSize);
    }

    public static class LayoutVerification
    {
        public static readonly IReadOnlyDictionary<string, (int FieldsCount, int RecordSize)> Expected =
            new Dictionary<string, (int, int)>
            {
                // Read from the 5.4.8 (18414) reference extraction's headers; the
                // field lists were checked against WoWDBDefs. Keyed by file name.
                ["Spell"] = (25, 100),
                ["SpellMisc"] = (26, 104),
                ["SpellEffect"] = (30, 120),
                ["SpellEffectScaling"] = (6, 24),
                ["SpellPower"] = (13, 52),
                ["SpellTargetRestrictions"] = (9, 36),
                ["SpellAuraOptions"] = (9, 36),
                ["SpellCategories"] = (10, 40),
                ["SpellCooldowns"] = (6, 24),
                ["SpellInterrupts"] = (8, 32),
                ["SpellLevels"] = (6, 24),
                ["SpellScaling"] = (9, 36),
                ["SpellRuneCost"] = (6, 24),
                ["gtSpellScaling"] = (2, 8),
                ["Difficulty"] = (12, 48),
                ["SpellVisual"] = (30, 120),
                ["SpellVisualKit"] = (39, 156),
                ["SpellVisualMissile"] = (17, 68),
                ["SpellVisualEffectName"] = (9, 36),
                ["SpellVisualKitModelAttach"] = (15, 60),
                ["SpellVisualKitAreaModel"] = (7, 28),
                ["SpellAuraRestrictions"] = (11, 44),
                ["SpellCastingRequirements"] = (7, 28),
                ["SpellCastTimes"] = (4, 16),
                ["SpellClassOptions"] = (7, 28),
                ["SpellDescriptionVariables"] = (2, 8),
                ["SpellDuration"] = (4, 16),
                ["SpellEquippedItems"] = (6, 24),
                ["SpellRadius"] = (5, 20),
                ["SpellRange"] = (8, 32),
                ["SpellShapeshift"] = (6, 24),
                ["SpellTotems"] = (5, 20),
                ["SpellReagents"] = (19, 76),
                ["SpellMissile"] = (15, 60),
                ["SpellMissileMotion"] = (5, 20),
                ["AreaGroup"] = (8, 32),
                ["AreaTable"] = (30, 120),
                ["OverrideSpellData"] = (13, 52),
                ["ScreenEffect"] = (14, 56),
                ["SkillLine"] = (9, 36),
                ["SkillLineAbility"] = (13, 52),
                ["Item"] = (8, 32),
                ["Item-sparse"] = (134, 536),
                ["SpellProcsPerMinute"] = (3, 12),
                ["RandPropPoints"] = (16, 64),
                ["SpellChainEffects"] = (58, 220),
            };

        public static LayoutMismatch Check(string fileName, string openedPath, int fieldsCount, int recordSize)
        {
            if (!Expected.TryGetValue(fileName, out var e))
                return null;
            if (e.FieldsCount == fieldsCount && e.RecordSize == recordSize)
                return null;
            return new LayoutMismatch
            {
                Table = fileName, File = System.IO.Path.GetFileName(openedPath),
                ExpectedFieldsCount = e.FieldsCount, ActualFieldsCount = fieldsCount,
                ExpectedRecordSize = e.RecordSize, ActualRecordSize = recordSize,
            };
        }
    }
}
```

The dictionary has exactly one entry per row of the store table (46).

- [ ] **Step 4: Rewrite the store list and loader in `DBC.cs`**

- Constants: `Version = "SpellWork 5.4.8 (18414)"`, `MaxLevel = 90`, `MaxItemLevel = 600`, add `public const int MaxClasses = 12;`.
- Replace the property list with one `public static Storage<XEntry> X { get; set; }` per row of the store table.
- Add the name mapping and the two enumeration helpers the tests use:

```csharp
public static string FileNameFor(PropertyInfo p) =>
    p.Name == "ItemSparse" ? "Item-sparse" :
    p.Name == "GtSpellScaling" ? "gtSpellScaling" : p.Name;

public static IEnumerable<string> StoreFileNames() => StorageProperties().Select(FileNameFor);
public static IEnumerable<Type> StoreEntryTypes() => StorageProperties().Select(p => p.PropertyType.GetGenericArguments()[0]);

private static readonly ConcurrentDictionary<string, ResolvedClientFile> _openedFiles = new ConcurrentDictionary<string, ResolvedClientFile>();
public static IReadOnlyDictionary<string, ResolvedClientFile> OpenedFiles => _openedFiles;
public static bool LocaleFallback { get; private set; }
```

- In `LoadAsync`: delete the `HotfixReader` block. Clear `_openedFiles` next to `_layoutMismatches`. Call `ClientFileResolver.EffectiveDbcDirectory(config, out var fb); LocaleFallback = fb;`. Replace the `Parallel.ForEach` body's open with:

```csharp
var file = ClientFileResolver.Resolve(FileNameFor(dbc), config);
if (file == null)
    return; // store stays null -> reported by UnloadedStores, exactly like the old FileNotFoundException path
var reader = new DBReader(file.Path);
var mismatch = LayoutVerification.Check(FileNameFor(dbc), file.Path, reader.FieldsCount, reader.RecordSize);
if (mismatch != null)
    _layoutMismatches.Add(mismatch);
_openedFiles[dbc.Name] = file;
dynamic storage = Activator.CreateInstance(dbc.PropertyType, reader);
dbc.SetValue(null, storage);
```

Keep the existing `FileNotFoundException`/`DirectoryNotFoundException`/`TargetInvocationException` catches and their comments. Update the comments that mention `.db2`-only, `SpellName` or hotfixes so they're true for 5.4.8.
- The post-load guard changes from `if (SpellName != null)` to `if (Spell != null && SpellMisc != null && SpellEffect != null)`, and the `GameTable<...>.Open(...)` line is deleted.
- `SelectedMapDifficulty` becomes `public static int SelectedDifficulty = 0;` and `SelectedItemLevel = 463`.

- [ ] **Step 5: Stop here and go straight to Task 4.** Core doesn't compile yet: `SpellInfo` still references 8.3.7 types. Local WIP commit:

```bash
git -c safe.directory=F:/WorkDir/SpellWork add -A
git -c safe.directory=F:/WorkDir/SpellWork commit -m "WIP 5.4.8: structures, store list, layout verification"
```

---

### Task 4: Enums, per-difficulty index, SpellInfo assembly, compile green

**Files:**
- Replace: `SpellWork.Core/Spell/SpellEnums.cs`
- Create: `SpellWork.Core/DBC/DifficultyIndex.cs`
- Modify: `SpellWork.Core/DBC/DBC.cs` (`BuildSpellInfoStore`), `SpellWork.Core/Spell/SpellInfo.cs`, `SpellWork.Core/Spell/RenderContext.cs`, `SpellWork.Core/Spell/ProcInfo.cs`, `SpellWork.Core/Database/*`, every `SpellWork.Mcp` file that fails to compile
- Delete: `SpellWork.Core/Spell/ExpectedStat.cs`, `SpellWork.Core/Spell/SpellVisualChain*.cs`, `SpellWork.Core/Spell/SpellVisualSection.cs` and their tests (`SpellVisualChain*Tests.cs`, `SpellVisualSectionTests.cs`, `VisualChainIntegrityTests.cs`). Task 7 recreates them.
- Test: `SpellWork.Core.Tests/DifficultyIndexTests.cs` (new), `SpellWork.Core.Tests/SpellInfoAssemblyTests.cs` (new), `SpellWork.Mcp.Tests/EnumCatalogTests.cs`, `SpellWork.Mcp.Tests/DecoderToolsTests.cs`

**Interfaces:**
- Consumes (Task 3): every store, `DBC.MaxClasses`, `DBC.SelectedDifficulty`.
- Produces:
  - `public sealed class DifficultyIndex<T>` with `static DifficultyIndex<T> Build(IEnumerable<T> rows, Func<T,int> spellId, Func<T,int> difficultyId)`, `T Get(int spellId, int difficulty, out int resolvedDifficulty)` (exact difficulty first, then 0; null if neither) and `IReadOnlyList<int> DifficultiesFor(int spellId)` (ascending, includes 0 if present).
  - `public sealed class ResolvedEffect { SpellEffectEntry Entry; int ResolvedDifficulty; SpellEffectScalingEntry Scaling; }`
  - `SpellInfo` members: `SpellEntry Spell`, `SpellMiscEntry Misc` (difficulty-0 row via `Spell.MiscID`), `string Name` (=`Spell.Name`), `IReadOnlyList<SpellPowerEntry> Powers` (ordered by OrderIndex), `SpellRuneCostEntry RuneCost`, `SpellScalingEntry Scaling` (via `Spell.ScalingID`), `IReadOnlyList<ResolvedEffect> EffectsFor(int difficulty)` (one per effect index, ascending), `IReadOnlyList<int> EffectDifficulties`, and difficulty-aware getters `AuraOptionsFor(int d)`, `CategoriesFor(int d)`, `CooldownsFor(int d)`, `InterruptsFor(int d)`, `LevelsFor(int d)`, `TargetRestrictionsFor(int d)`, `MiscFor(int d)`. Each returns the row at `d`, falling back to `d=0`. The existing no-argument properties (`AuraOptions`, `Categories`, ...) remain and mean difficulty 0.
  - `uint[] SpellInfo.Attributes` (length 14), plus the existing `Attributes`, `AttributesEx` ... `AttributesEx13` getters reading from it.
  - `RenderContext(uint level, uint itemLevel, int difficulty)`, with properties `Level`, `ItemLevel`, `Difficulty`. `RenderContext.FromGlobals()` reads `DBC.SelectedLevel`, `SelectedItemLevel`, `SelectedDifficulty`.
  - Enum type names kept from `main`: `AuraType`, `SpellEffects`, `Targets`, `SpellAtribute`, `SpellAtributeEx` … `SpellAtributeEx13`, `ProcFlags`, `ProcFlagsEx`, `Mechanics`, `Powers`, `SpellFamilyNames`, `DispelType`, `SpellSchoolMask`, `Classes`. A type that has no 5.4.8 counterpart is deleted, together with every reference to it.

- [ ] **Step 1: Write the DifficultyIndex tests**

```csharp
using System.Linq;
using SpellWork.DBC;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class DifficultyIndexTests
    {
        private sealed class Row { public int Spell, Diff; public string Tag; }

        private static DifficultyIndex<Row> Index() => DifficultyIndex<Row>.Build(new[]
        {
            new Row { Spell = 1, Diff = 0, Tag = "base" },
            new Row { Spell = 1, Diff = 2, Tag = "heroic" },
            new Row { Spell = 2, Diff = 3, Tag = "only10" },
        }, r => r.Spell, r => r.Diff);

        [Fact] public void ExactDifficultyWins() { Assert.Equal("heroic", Index().Get(1, 2, out var d).Tag); Assert.Equal(2, d); }
        [Fact] public void MissingDifficultyFallsBackToBase() { Assert.Equal("base", Index().Get(1, 1, out var d).Tag); Assert.Equal(0, d); }
        [Fact] public void NoBaseAndNoExactIsNull() { Assert.Null(Index().Get(2, 1, out _)); }
        [Fact] public void DifficultiesAreListedAscending() => Assert.Equal(new[] { 0, 2 }, Index().DifficultiesFor(1));
        [Fact] public void UnknownSpellHasNoDifficulties() => Assert.Empty(Index().DifficultiesFor(99));
    }
}
```

- [ ] **Step 2: Implement `DifficultyIndex<T>`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpellWork.DBC
{
    /// <summary>
    /// (spell, difficulty) -> row for the 5.4.8 tables that carry both
    /// columns. A difficulty with no row of its own falls back to the base
    /// (difficulty 0) row, which is how the 5.4.8 server resolves them.
    /// </summary>
    public sealed class DifficultyIndex<T> where T : class
    {
        private readonly Dictionary<int, SortedDictionary<int, T>> _bySpell;
        private DifficultyIndex(Dictionary<int, SortedDictionary<int, T>> bySpell) => _bySpell = bySpell;

        public static DifficultyIndex<T> Build(IEnumerable<T> rows, Func<T, int> spellId, Func<T, int> difficultyId)
        {
            var map = new Dictionary<int, SortedDictionary<int, T>>();
            foreach (var row in rows)
            {
                if (!map.TryGetValue(spellId(row), out var byDiff))
                    map[spellId(row)] = byDiff = new SortedDictionary<int, T>();
                byDiff[difficultyId(row)] = row; // duplicate (spell, difficulty): last row wins, as in the server's store load
            }
            return new DifficultyIndex<T>(map);
        }

        public T Get(int spellId, int difficulty, out int resolvedDifficulty)
        {
            resolvedDifficulty = 0;
            if (!_bySpell.TryGetValue(spellId, out var byDiff))
                return null;
            if (byDiff.TryGetValue(difficulty, out var exact)) { resolvedDifficulty = difficulty; return exact; }
            return byDiff.TryGetValue(0, out var baseRow) ? baseRow : null;
        }

        public IReadOnlyList<int> DifficultiesFor(int spellId) =>
            _bySpell.TryGetValue(spellId, out var byDiff) ? byDiff.Keys.ToList() : (IReadOnlyList<int>)Array.Empty<int>();
    }
}
```

Run: `dotnet test SpellWork.Core.Tests/... --filter DifficultyIndexTests` won't build yet because Core doesn't compile. Continue to Step 3; these tests run in Step 9.

- [ ] **Step 3: Replace `SpellEnums.cs` with the 5.4.8 sets**

Sources, in precedence order:
1. SkyFire_548 server headers: `https://raw.githubusercontent.com/ProjectSkyfire/SkyFire_548/master/src/server/game/Miscellaneous/SharedDefines.h` (effects `SPELL_EFFECT_*`, targets `TARGET_*`, attributes `SPELL_ATTR0..13_*`, mechanics, powers, families, dispel, school mask, classes) and `.../src/server/game/Spells/Auras/SpellAuraDefines.h` (`SPELL_AURA_*`, proc flags are in `.../Spells/Auras/SpellAuraDefines.h` or `.../Entities/Unit/Unit.h` — grep `PROC_FLAG_`).
2. Upstream 5.4.8 SpellWork `SpellWork/Spell/SpellEnums.cs`, for anything the headers don't define.

Rules: keep `main`'s C# enum type names (Interfaces block). Copy each value's numeric id exactly. Keep `main`'s member-name style (`SPELL_AURA_PERIODIC_DAMAGE`), and use `UNK_<n>` for unnamed values. Don't carry over any 8.3.7-only member. The `[Description]` attributes, if `main` uses them on a type, stay on the same members where the meaning is unchanged.

Spot values that the tests in Step 7 pin (these come from SkyFire_548 and are well known for 5.4.8):
- `AuraType`: 3 `SPELL_AURA_PERIODIC_DAMAGE`, 4 `SPELL_AURA_DUMMY`, 8 `SPELL_AURA_PERIODIC_HEAL`, 42 `SPELL_AURA_PROC_TRIGGER_SPELL`, 107 `SPELL_AURA_ADD_FLAT_MODIFIER`, 108 `SPELL_AURA_ADD_PCT_MODIFIER`.
- `SpellEffects`: 2 `SPELL_EFFECT_SCHOOL_DAMAGE`, 6 `SPELL_EFFECT_APPLY_AURA`, 10 `SPELL_EFFECT_HEAL`, 64 `SPELL_EFFECT_TRIGGER_SPELL`.
- `Targets`: 1 `TARGET_UNIT_CASTER`, 6 `TARGET_UNIT_TARGET_ENEMY`, 25 `TARGET_UNIT_TARGET_ANY`.

If a header in your fetch disagrees with one of these spot values, stop and report it; don't change the test.

- [ ] **Step 4: Rebuild `BuildSpellInfoStore`**

Replace the body with this approach. It's sequential where the work is cheap and keeps `Task.WhenAll` for the large independent groupings:

```csharp
private static async Task BuildSpellInfoStore(ISpellWorkLogger logger)
{
    foreach (var spell in Spell.Values)
        SpellInfoStore[spell.ID] = new SpellInfo(spell);

    var scalingByEffect = SpellEffectScaling.Values
        .GroupBy(s => s.SpellEffectID).ToDictionary(g => g.Key, g => g.First());

    AuraOptionsIndex   = DifficultyIndex<SpellAuraOptionsEntry>.Build(SpellAuraOptions.Values, r => r.SpellID, r => r.DifficultyID);
    CategoriesIndex    = DifficultyIndex<SpellCategoriesEntry>.Build(SpellCategories.Values, r => r.SpellID, r => r.DifficultyID);
    CooldownsIndex     = DifficultyIndex<SpellCooldownsEntry>.Build(SpellCooldowns.Values, r => r.SpellID, r => r.DifficultyID);
    InterruptsIndex    = DifficultyIndex<SpellInterruptsEntry>.Build(SpellInterrupts.Values, r => r.SpellID, r => r.DifficultyID);
    LevelsIndex        = DifficultyIndex<SpellLevelsEntry>.Build(SpellLevels.Values, r => r.SpellID, r => r.DifficultyID);
    TargetRestrictionsIndex = DifficultyIndex<SpellTargetRestrictionsEntry>.Build(SpellTargetRestrictions.Values, r => r.SpellID, r => r.DifficultyID);
    MiscIndex          = DifficultyIndex<SpellMiscEntry>.Build(SpellMisc.Values, r => r.SpellID, r => r.DifficultyID);

    await Task.WhenAll(Task.Run(() =>
    {
        foreach (var effect in SpellEffect.Values)
        {
            if (!SpellInfoStore.TryGetValue(effect.SpellID, out var info))
            {
                logger.Warn($"Spell effect {effect.ID} is referencing unknown spell {effect.SpellID}, ignoring!");
                continue;
            }
            scalingByEffect.TryGetValue(effect.ID, out var scaling);
            info.AddEffect(effect, scaling);

            if (effect.EffectTriggerSpell != 0)
            {
                if (!SpellTriggerStore.TryGetValue(effect.EffectTriggerSpell, out var set))
                    SpellTriggerStore[effect.EffectTriggerSpell] = set = new SortedSet<int>();
                set.Add(effect.SpellID);
            }
        }
    }), Task.Run(() =>
    {
        foreach (var power in SpellPower.Values.OrderBy(p => p.OrderIndex))
            if (SpellInfoStore.TryGetValue(power.SpellID, out var info))
                info.AddPower(power);
            else
                logger.Warn($"SpellPower: Unknown spell {power.SpellID} referenced, ignoring!");
    }));
}

public static DifficultyIndex<SpellAuraOptionsEntry> AuraOptionsIndex { get; private set; }
public static DifficultyIndex<SpellCategoriesEntry> CategoriesIndex { get; private set; }
public static DifficultyIndex<SpellCooldownsEntry> CooldownsIndex { get; private set; }
public static DifficultyIndex<SpellInterruptsEntry> InterruptsIndex { get; private set; }
public static DifficultyIndex<SpellLevelsEntry> LevelsIndex { get; private set; }
public static DifficultyIndex<SpellTargetRestrictionsEntry> TargetRestrictionsIndex { get; private set; }
public static DifficultyIndex<SpellMiscEntry> MiscIndex { get; private set; }
```

`SpellTriggerStore`'s declared type stays `IDictionary<int, ISet<int>>`. Cast or adjust the local to match.

- [ ] **Step 5: Re-model `SpellInfo` around `SpellEntry`**

In `SpellInfo.cs`:
- Constructor `public SpellInfo(SpellEntry spell)`. It sets `Spell`, then resolves single-row FKs once, each guarded by `TryGetValue` and left null when id 0 or the row is missing: `Misc = DBC.SpellMisc[spell.MiscID]`, `Scaling` via `ScalingID`, `RuneCost` via `RuneCostID`, `AuraRestrictions` via `AuraRestrictionsID`, `CastingRequirements` via `CastingRequirementsID`, `ClassOptions` via `ClassOptionsID`, `EquippedItems` via `EquippedItemsID`, `Reagents` via `ReagentsID`, `Shapeshift` via `ShapeshiftID`, `Totems` via `TotemsID`, `DescriptionVariables` via `DescriptionVariablesID`, `DurationEntry` and `Range` from `Misc.DurationIndex`/`Misc.RangeIndex`.
- `AuraOptions`, `Categories`, `Cooldowns`, `Interrupts`, `Levels` become getters that call `XFor(0)`. `XFor(int d) => DBC.DBC.XIndex.Get(ID, d, out _)`. `TargetRestrictionsFor(int d)` does the same. `ProcsPerMinute` resolves from `AuraOptions?.SpellProcsPerMinuteID`.
- Effects storage:

```csharp
private readonly Dictionary<int, SortedDictionary<int, ResolvedEffect>> _effectsByDifficulty =
    new Dictionary<int, SortedDictionary<int, ResolvedEffect>>();

internal void AddEffect(SpellEffectEntry e, SpellEffectScalingEntry scaling)
{
    lock (_effectsByDifficulty)
    {
        if (!_effectsByDifficulty.TryGetValue(e.DifficultyID, out var byIndex))
            _effectsByDifficulty[e.DifficultyID] = byIndex = new SortedDictionary<int, ResolvedEffect>();
        byIndex[e.EffectIndex] = new ResolvedEffect { Entry = e, ResolvedDifficulty = e.DifficultyID, Scaling = scaling };
    }
}

public IReadOnlyList<int> EffectDifficulties => _effectsByDifficulty.Keys.OrderBy(k => k).ToList();

/// Per effect index: the row at <paramref name="difficulty"/> if one exists, else the base row.
public IReadOnlyList<ResolvedEffect> EffectsFor(int difficulty)
{
    _effectsByDifficulty.TryGetValue(0, out var baseRows);
    _effectsByDifficulty.TryGetValue(difficulty, out var exact);
    var indices = new SortedSet<int>();
    if (baseRows != null) indices.UnionWith(baseRows.Keys);
    if (exact != null) indices.UnionWith(exact.Keys);
    return indices.Select(i => exact != null && exact.TryGetValue(i, out var x) ? x : baseRows[i]).ToList();
}

public IReadOnlyList<ResolvedEffect> Effects => EffectsFor(0);
```

`ResolvedEffect` goes in the same file. A spell can have an effect index only at a non-zero difficulty. That's why the indexer above unions both key sets: such an index exists at that difficulty and is absent at 0.
- `Name => Spell.Name`. Attributes: `public uint[] Attributes => Misc?.Attributes ?? new uint[14];` plus named getters `AttributesEx13 => Attributes[13]` and so on. `SchoolMask => Misc?.SchoolMask ?? 0`.
- Every 8.3.7 member that has no 5.4.8 source is deleted, together with its use in `Write(...)`: `SpellName`, `SpellXSpellVisual`, `ReagentsCurrency`, ContentTuning reads, `ExpectedStat` calls. In `CalculateBaseEffectValue`, temporarily keep only the non-scaling branch (`EffectBasePoints` + die sides, as in `main`'s `else` branch without ExpectedStat). Task 5 replaces it.
- The visual section of `Write(...)` is removed in this task and returns in Task 7. Delete `AppendSpellVisualInfo` and `WriteVisualChain` and their call site.

Update `RenderContext` to `(uint level, uint itemLevel, int difficulty)` per the Interfaces block. Then fix every compile error in Core (`ProcInfo.cs`, `SpellInfoJsonSerializer.cs`, `Database/*`, extensions). The rule: map 1:1 to the new member if it exists; otherwise delete the line or block. In `SpellInfoJsonSerializer`, read effects via `EffectsFor(context.Difficulty)`, use `ResolvedEffect.Entry` where `main` used the entry, and drop the visual section (returns in Task 7).

- [ ] **Step 6: Make Mcp compile**

Build: `dotnet build SpellWork.sln -c Release --nologo`. Fix each Mcp error with the same rule. Specific mappings:
- `get_spell`'s `difficulty` argument now means a Difficulty id. Replace the MapDifficulty lookup with: `if (difficulty != 0 && !DBC.DBC.Difficulty.ContainsKey(difficulty)) return ToolResponse.Error("unknown_difficulty", $"No Difficulty.dbc row {difficulty}.", new Dictionary<string, object> { ["valid"] = DBC.DBC.Difficulty.Keys.OrderBy(k => k).Prepend(0).ToList() });` then `new RenderContext(level, itemLevel, difficulty)`.
- `VisualTools.GetSpellVisualChain` temporarily returns `ToolResponse.Error("not_implemented", "Visual chain is being ported to 5.4.8 (Task 7).")`, and its signature becomes `(int id)`. Task 7 replaces this.
- `calculate_spell_scaling`: keep the signature and call `GetEffectValues` as before (Task 5 fixes the numbers). Delete the ExpectedStat/ContentTuning parts.
- `SpellDataService`/`DiagnosticTools`: `DBC.Version`, store counts and `LayoutMismatches` keep working. `layoutMismatches` entries serialize the new `LayoutMismatch` properties.

- [ ] **Step 7: Rewrite the enum-dependent Mcp tests**

In `SpellWork.Mcp.Tests/EnumCatalogTests.cs` and `DecoderToolsTests.cs`, replace every assertion on an 8.3.7 id/name with the Step 3 spot values. Add:

```csharp
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
```

Adapt the `using`s, and check `main`'s exact name for word 1: if it's `SpellAtributeEx`, keep the expression above; if it's `SpellAtributeEx1`, change it. Delete Core/Mcp tests that pin 8.3.7-only behaviour (ExpectedStat, ContentTuning, MapDifficulty translation, `variantsAtOtherDifficulties`, golden files). List each deleted test in the commit message body. Delete `SpellWork.Core.Tests/Goldens/*.txt` and disable `GoldenRenderingTests` by deleting the file; Task 6 recreates both.

- [ ] **Step 8: Write the SpellInfo assembly tests**

```csharp
using System.Linq;
using Xunit;

namespace SpellWork.Core.Tests
{
    [Collection("ClientData")]
    public sealed class SpellInfoAssemblyTests
    {
        private readonly ClientDataFixture _fixture;
        public SpellInfoAssemblyTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void FireballAssembles()
        {
            _fixture.SkipIfNoClientData();
            var s = DBC.DBC.SpellInfoStore[133];
            Assert.Equal("Fireball", s.Name);
            Assert.Equal(4, s.SchoolMask);
            var e = Assert.Single(s.EffectsFor(0));
            Assert.Equal(91, e.Entry.ID);
            Assert.Equal(2, e.Entry.Effect);                 // SCHOOL_DAMAGE
            Assert.Equal(1.5f, e.Entry.EffectBonusCoefficient);
            Assert.NotNull(e.Scaling);
            Assert.Equal(1.5f, e.Scaling.Coefficient);
            Assert.Equal(0.24f, e.Scaling.Variance, 3);
        }

        [SkippableFact]
        public void DifficultyWithoutRowsFallsBackToBase()
        {
            _fixture.SkipIfNoClientData();
            var s = DBC.DBC.SpellInfoStore[43093]; // Grievous Throw: rows at 0, 2, 4
            var effects = s.EffectsFor(1);
            Assert.Equal(new[] { 0, 1 }, effects.Select(x => x.Entry.EffectIndex));
            Assert.All(effects, x => Assert.Equal(0, x.ResolvedDifficulty));
            Assert.Equal(new[] { 5000, 28499 }, effects.Select(x => x.Entry.EffectBasePoints));
        }

        [SkippableFact]
        public void DifficultyWithRowsUsesThem()
        {
            _fixture.SkipIfNoClientData();
            var effects = DBC.DBC.SpellInfoStore[43093].EffectsFor(2);
            Assert.All(effects, x => Assert.Equal(2, x.ResolvedDifficulty));
            Assert.Equal(new[] { 7500, 45124 }, effects.Select(x => x.Entry.EffectBasePoints));
        }

        [SkippableFact]
        public void EffectDifficultiesListed()
        {
            _fixture.SkipIfNoClientData();
            Assert.Equal(new[] { 0, 2, 4 }, DBC.DBC.SpellInfoStore[43093].EffectDifficulties);
        }

        [SkippableFact]
        public void TriggerStoreIsBuilt()
        {
            _fixture.SkipIfNoClientData();
            Assert.NotEmpty(DBC.DBC.SpellTriggerStore);
        }
    }
}
```

- [ ] **Step 9: Run everything, without and with data**

Run both test commands with no env vars → 0 failed.
Run both with data → 0 failed. In particular `LayoutVerificationTests` (0 mismatches, `.db2` opened), `DbcLoadTests`, `SpellInfoAssemblyTests` and `DifficultyIndexTests` all pass.
If `ReferenceDataHasNoMismatches` fails, its message names the file and both sizes. Fix the structure against WoWDBDefs, never the expected value (the expected values were read from the files).

- [ ] **Step 10: Squash Tasks 3+4 and commit**

```bash
git -c safe.directory=F:/WorkDir/SpellWork reset --soft HEAD~1
git -c safe.directory=F:/WorkDir/SpellWork add -A
git -c safe.directory=F:/WorkDir/SpellWork commit -m "5.4.8: data model swap (structures, loader, layouts, enums, per-difficulty SpellInfo)"
```

---

### Task 5: 5.4.8 effect scaling

**Files:**
- Create: `SpellWork.Core/Spell/SpellScalingCalculator.cs`
- Modify: `SpellWork.Core/Spell/SpellInfo.cs` (`CalculateBaseEffectValue`, `CalculateEffectValues`), `SpellWork.Mcp/Tools/DecoderTools.cs` (`CalculateSpellScaling`)
- Test: `SpellWork.Core.Tests/SpellScalingCalculatorTests.cs` (new), `SpellWork.Mcp.Tests/DecoderToolsTests.cs`

**Interfaces:**
- Consumes: `SpellScalingEntry`, `SpellEffectScalingEntry`, `DBC.GtSpellScaling`, `DBC.MaxClasses`, `ResolvedEffect`.
- Produces:
  - `public static class SpellScalingCalculator`
  - `public static int GtKey(int scalingClass, int level, int maxScalingLevel)`
  - `public static float Multiplier(SpellScalingEntry scaling, int level, Func<int, float?> gtLookup)`. Returns 0 when the gt row is missing.
  - `public static (float Base, float Min, float Max, float PerComboPoint) Value(SpellScalingEntry scaling, SpellEffectScalingEntry effectScaling, int level, Func<int, float?> gtLookup)`
- Port of SkyFire_548 `SpellEffectInfo::CalcValue` (`src/server/game/Spells/SpellInfo.cpp`, lines 122–163 at `master`):

```
gtKeyBase    = class <= -1 ? MAX_CLASSES - 2 - class : class - 1
levelForKey  = MaxScalingLevel != 0 ? min(MaxScalingLevel, level) : level
multiplier   = gt[gtKeyBase * 100 + levelForKey - 1]
if CastTimeMax > 0 && CastTimeMaxLevel > level:
    multiplier *= (CastTimeMin + (level - 1) * (CastTimeMax - CastTimeMin) / (CastTimeMaxLevel - 1)) / CastTimeMax   // integer division inside, as in C++
if NerfMaxLevel > level:
    multiplier *= (1 - NerfFactor) * (level - 1) / (NerfMaxLevel - 1) + NerfFactor
base  = Coefficient * multiplier
delta = Variance * Coefficient * multiplier * 0.5        // displayed as [base - delta, base + delta]
combo = ResourceCoefficient * multiplier
```

  Only when `effectScaling?.Coefficient != 0`. Otherwise the effect uses `EffectBasePoints` + die sides + `EffectRealPointsPerLevel`, as `main`'s non-scaling branch does.

- [ ] **Step 1: Write the failing tests**

```csharp
using SpellWork.DBC.Structures;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public sealed class SpellScalingCalculatorTests
    {
        [Theory]
        [InlineData(8, 90, 0, 7 * 100 + 89)]      // mage, level 90
        [InlineData(8, 90, 85, 7 * 100 + 84)]     // capped by MaxScalingLevel
        [InlineData(-1, 90, 0, 11 * 100 + 89)]    // MAX_CLASSES - 2 - (-1) = 11
        [InlineData(-2, 90, 0, 12 * 100 + 89)]    // Mana Gem style
        public void GtKey(int cls, int level, int maxLevel, int expected) =>
            Assert.Equal(expected, SpellScalingCalculator.GtKey(cls, level, maxLevel));

        [Fact]
        public void CastTimeAndNerfFactorsApply()
        {
            var s = new SpellScalingEntry { Class = 8, CastTimeMin = 1000, CastTimeMax = 3000, CastTimeMaxLevel = 21, NerfFactor = 0.5f, NerfMaxLevel = 11 };
            // level 11: cast = 1000 + 10*2000/20 = 2000 -> *2000/3000; nerf: (1-0.5)*10/10 + 0.5 = 1.0
            var m = SpellScalingCalculator.Multiplier(s, 11, key => 300f);
            Assert.Equal(300f * 2000f / 3000f, m, 3);
        }

        [Fact]
        public void ValueRangeUsesVariance()
        {
            var s = new SpellScalingEntry { Class = 8 };
            var e = new SpellEffectScalingEntry { Coefficient = 1.5f, Variance = 0.24f, ResourceCoefficient = 0f };
            var v = SpellScalingCalculator.Value(s, e, 90, key => 1000f);
            Assert.Equal(1500f, v.Base, 3);
            Assert.Equal(1500f - 180f, v.Min, 3);
            Assert.Equal(1500f + 180f, v.Max, 3);
        }

        [Fact]
        public void MissingGtRowIsZeroNotAThrow() =>
            Assert.Equal(0f, SpellScalingCalculator.Multiplier(new SpellScalingEntry { Class = 8 }, 90, key => null));
    }
}
```

Plus a data test in `SpellInfoAssemblyTests`:

```csharp
[SkippableFact]
public void FireballScalesFromGtAtLevel90()
{
    _fixture.SkipIfNoClientData();
    var s = DBC.DBC.SpellInfoStore[133];
    var key = SpellWork.Spell.SpellScalingCalculator.GtKey(s.Scaling.Class, 90, s.Scaling.MaxScalingLevel);
    var gt = DBC.DBC.GtSpellScaling[key].Value;
    var v = s.GetEffectValues(s.EffectsFor(0)[0], new SpellWork.Spell.RenderContext(90, 463, 0));
    Assert.Equal((float)System.Math.Round(1.5f * SpellWork.Spell.SpellScalingCalculator.Multiplier(s.Scaling, 90, k => DBC.DBC.GtSpellScaling.TryGetValue(k, out var r) ? r.Value : (float?)null)), v.BaseValue);
    Assert.True(gt > 0);
}
```

- [ ] **Step 2: Run them and confirm they fail** (`SpellScalingCalculator` doesn't exist).

- [ ] **Step 3: Implement `SpellScalingCalculator`** exactly per the formula block. Use `int` arithmetic for the cast-time numerator, as in C++.

- [ ] **Step 4: Wire it into `SpellInfo`**. In `CalculateBaseEffectValue(ResolvedEffect effect, RenderContext ctx)`: if `effect.Scaling != null && effect.Scaling.Coefficient != 0 && Scaling != null`, return `Math.Round(Value(...).Base)` with `gtLookup = k => DBC.DBC.GtSpellScaling.TryGetValue(k, out var r) ? r.Value : (float?)null` and `level = (int)ctx.Level`. Otherwise return `EffectBasePoints + (DieSides == 1 ? 1 : 0)` plus `EffectRealPointsPerLevel * (clamp(level, BaseLevel, MaxLevel) - SpellLevel)` from `LevelsFor(ctx.Difficulty)`. `CalculateEffectValues` sets `Variance` from `effect.Scaling?.Variance`, `ValuePerResource` from the combo value, and `SpellPowerCoefficient` from `EffectBonusCoefficient`. Remove `AttackPowerCoefficient` and `UsesExpectedStat` from `EffectValueBreakdown`/`EffectValues`, and fix their readers. Change the signatures that took a `SpellEffectEntry` to `ResolvedEffect`.

- [ ] **Step 5: `calculate_spell_scaling`**. Clamp `level` to 1..90. Report per effect: `index`, `resolvedDifficulty`, `base`, `min`, `max`, `perComboPoint`, `spellPowerCoefficient`, `scaled` (bool), and add `"itemLevelApplied": false` at top level (5.4.8 effect scaling doesn't read item level; spec §5.5). Update `DecoderToolsTests` assertions on this tool's JSON keys to these names.

- [ ] **Step 6: Run both test commands, without and with data.** All pass.

- [ ] **Step 7: Commit** `"5.4.8: port SkyFire_548 effect scaling (gtSpellScaling)"`.

---

### Task 6: Text rendering, JSON shape and goldens

**Files:**
- Modify: `SpellWork.Core/Spell/SpellInfo.cs` (`Write`, `AppendEffectInfo`), `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs`
- Create: `SpellWork.Core.Tests/GoldenRenderingTests.cs`, `SpellWork.Core.Tests/Goldens/*.txt` and `*.json`
- Test: `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`

**Interfaces:**
- Consumes: Tasks 4–5.
- Produces: JSON keys that later tasks and agents rely on:
  - top level: `id`, `name`, `nameSubtext`, `description`, `auraDescription`, `difficulty` (the requested one), `effectDifficulties` (int[]), `attributes` (array of 14 objects `{ word, value, flags[] }`), `schoolMask`, `runeCost` (`{ blood, unholy, frost, chromatic, runicPower }` or absent), `powers` (array, ordered), `scaling` (`{ class, castTimeMin, castTimeMax, castTimeMaxLevel, nerfFactor, nerfMaxLevel, maxScalingLevel }` or absent), plus every `main` key whose source still exists.
  - each effect: `main`'s keys + `resolvedDifficulty`, `bonusMultiplier` (`EffectBonusCoefficient`), `scaling` (`{ coefficient, variance, resourceCoefficient }` or absent).
  - Keys with no 5.4.8 source are left out, not set to null (spec §5.3).

- [ ] **Step 1: Write the JSON shape tests**

In `SpellInfoJsonSerializerTests.cs`, replace the 8.3.7 shape assertions with data tests:

```csharp
[SkippableFact]
public void FireballJsonShape()
{
    _fixture.SkipIfNoClientData();
    var json = System.Text.Json.JsonDocument.Parse(SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[133], new RenderContext(90, 463, 0))).RootElement;
    Assert.Equal("Fireball", json.GetProperty("name").GetString());
    Assert.Equal(14, json.GetProperty("attributes").GetArrayLength());
    Assert.False(json.TryGetProperty("contentTuningId", out _));
    var effect = json.GetProperty("effects")[0];
    Assert.Equal(0, effect.GetProperty("resolvedDifficulty").GetInt32());
    Assert.Equal(1.5, effect.GetProperty("scaling").GetProperty("coefficient").GetDouble(), 3);
}

[SkippableFact]
public void ResolvedDifficultyIsPerEffect()
{
    _fixture.SkipIfNoClientData();
    var json = System.Text.Json.JsonDocument.Parse(SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[43093], new RenderContext(90, 463, 2))).RootElement;
    Assert.Equal(2, json.GetProperty("difficulty").GetInt32());
    Assert.Equal(new[] { 0, 2, 4 }, json.GetProperty("effectDifficulties").EnumerateArray().Select(x => x.GetInt32()));
    Assert.All(json.GetProperty("effects").EnumerateArray(), e => Assert.Equal(2, e.GetProperty("resolvedDifficulty").GetInt32()));
}
```

Use the serializer's real entry-point name (`grep -n "public static" SpellWork.Core/Spell/SpellInfoJsonSerializer.cs`). Delete tests that assert removed 8.3.7 keys.

- [ ] **Step 2: Run them and confirm they fail.**

- [ ] **Step 3: Update the serializer and `Write`.** Implement the key list above. In `Write(...)`, render with the same section order and wording as `main`, plus: a `Rune cost:` line when `RuneCost` is set; `Attributes` 0–13; per effect a `(difficulty N)` suffix when `ResolvedDifficulty != 0`; scaling `Coefficient/Variance` per effect; and a header line `Effect difficulties: 0, 2, 4` when there's more than one.

- [ ] **Step 4: Run them and confirm they pass.**

- [ ] **Step 5: Goldens.** Recreate `GoldenRenderingTests.cs` with `main`'s mechanism (compare `PlainTextSpellInfoWriter` output against `Goldens/spell-<id>.txt`, and JSON against `Goldens/spell-<id>.json`, at `RenderContext(90, 463, 0)`). Copy `main`'s regeneration switch (`git -c safe.directory=F:/WorkDir/SpellWork show main:SpellWork.Core.Tests/GoldenRenderingTests.cs`). Golden ids, one per model feature (spec §10):

| Id | Covers |
|---|---|
| 133 Fireball | direct damage, effect scaling, missile visual |
| 589 Shadow Word: Pain | DoT (periodic aura) |
| 116 Frostbolt | scaling + second school/visual |
| 43093 Grievous Throw | difficulty-specific effects (render at difficulty 2 as `spell-43093-d2`) |
| 42477 Chain Heal (NPC) | chain targets, difficulty rows |
| 49998 Death Strike | rune cost (verify `RuneCostID != 0` in data; if 0, pick any spell with `RuneCostID != 0` via `query_db2`) |
| 3599 Searing Totem | totem |
| 16870 Clearcasting | proc aura |
| 2825 Bloodlust | multi-effect aura, cooldown/category |
| 6603 Auto Attack | no effects scaling, plain base points |

If an id is absent from `DBC.Spell` or doesn't exhibit its feature, replace it with a spell that does (find one with a LINQ query over the stores in a scratch test), and note the substitution in the commit message.

Regenerate, then **check each golden by hand against raw rows** (`get_db2_row` equivalent: inspect `DBC.Spell[id]`, `SpellMisc`, `SpellEffect`). At minimum confirm name, school, effect type/aura names and base points match. Record in the commit message body that the goldens were hand-checked, and how.

- [ ] **Step 6: Run both test commands, without and with data.** All pass.

- [ ] **Step 7: Commit** `"5.4.8: text/JSON rendering and hand-checked goldens"`.

---

### Task 7: Visual chain

**Files:**
- Create: `SpellWork.Core/Spell/SpellVisualChain.cs`, `SpellVisualChainIndex.cs`, `SpellVisualChainResolver.cs`, `SpellVisualChainJson.cs`, `SpellVisualSection.cs`
- Modify: `SpellWork.Core/Spell/SpellInfo.cs` (visual section in `Write`), `SpellInfoJsonSerializer.cs` (`visual`), `SpellWork.Mcp/Tools/VisualTools.cs`
- Test: `SpellWork.Core.Tests/SpellVisualChainResolverTests.cs`, `VisualChainIntegrityTests.cs`, `SpellWork.Mcp.Tests/VisualToolsTests.cs`

**Interfaces:**
- Consumes: SpellMisc, SpellVisual, SpellVisualKit, SpellVisualMissile, SpellVisualEffectName, SpellVisualKitModelAttach, SpellMissileMotion.
- Produces:

```csharp
public sealed class SpellVisualChain
{
    public int SpellID { get; set; }
    public IReadOnlyList<SpellVisualVariant> Variants { get; set; }
    public IReadOnlyList<SpellVisualKitRef> Kits { get; set; }        // deduplicated across variants and slots
    public bool Truncated { get; set; }                                // kit cap (500) hit
    public IReadOnlyList<string> UnresolvedReferences { get; set; }   // "SpellVisualKit#123 (from SpellVisual#67.ImpactKit)"
}
public sealed class SpellVisualVariant
{
    public int Slot { get; set; }                    // 0 or 1: index into SpellMisc.SpellVisualID
    public int SpellVisualID { get; set; }
    public bool VisualResolved { get; set; }
    public IReadOnlyList<KitSlotRef> KitSlots { get; set; }   // ("PrecastKit", 30) ...
    public IReadOnlyList<SpellVisualMissileInfo> Missiles { get; set; }
    public SpellMissileMotionInfo MissileMotion { get; set; }  // from SpellVisual.MissileMotion, null if 0
}
public sealed class KitSlotRef { public string Slot { get; set; } public int KitID { get; set; } }
public sealed class SpellVisualKitRef
{
    public int KitID { get; set; }
    public IReadOnlyList<string> ReachedVia { get; set; }      // "visual 67 (slot 0) PrecastKit"
    public IReadOnlyList<KitEffectRef> Effects { get; set; }   // non-zero Head..World/Special[i] slots
    public IReadOnlyList<KitModelAttachInfo> ModelAttachments { get; set; }
}
public sealed class KitEffectRef { public string Slot { get; set; } public int EffectNameID { get; set; } public string Name { get; set; } }
public sealed class KitModelAttachInfo { public int ID { get; set; } public int AttachmentID { get; set; } public int EffectNameID { get; set; } public string Name { get; set; } }
public sealed class SpellVisualMissileInfo { public int ID { get; set; } public int EffectNameID { get; set; } public string Name { get; set; } public int MotionID { get; set; } public string MotionName { get; set; } }
public sealed class SpellMissileMotionInfo { public int ID { get; set; } public string Name { get; set; } }
public sealed class SpellVisualSection
{
    public IReadOnlyList<string> MissingStores { get; set; }   // non-empty => storeUnloaded
    public SpellVisualChain Chain { get; set; }                // null when MissingStores non-empty
}
public static class SpellVisualChainResolver
{
    public static SpellVisualSection ResolveSection(int spellId, int maxKits = 500);
    public static IReadOnlyList<string> MissingStores();
}
```

The index (`SpellVisualChainIndex`) builds `ILookup<int, SpellVisualMissileEntry> MissilesBySet` and `ILookup<int, SpellVisualKitModelAttachEntry> AttachmentsByKit` once per loaded store set. Copy `main`'s `Current()`/`MatchesCurrentStores()` caching pattern: `git -c safe.directory=F:/WorkDir/SpellWork show main:SpellWork.Core/Spell/SpellVisualChainIndex.cs`. Gated stores (spec §6): SpellVisual, SpellVisualKit, SpellVisualMissile, SpellVisualEffectName, SpellVisualKitModelAttach. SpellMissileMotion isn't gated; ids it can't resolve go to `UnresolvedReferences`. The variant source is `SpellInfo.Misc.SpellVisualID[0..1]`, non-zero ones only. Kit slot order: PrecastKit, CastKit, ImpactKit, StateKit, StateDoneKit, ChannelKit, CasterImpactKit, TargetImpactKit, MissileTargetingKit, InstantAreaKit, ImpactAreaKit, PersistentAreaKit. Kit effect slot order: Head, Chest, Base, LeftHand, RightHand, Breath, LeftWeapon, RightWeapon, Special0, Special1, Special2, World. `SpellMissileMotionEntry`'s name field is whatever the ported upstream structure calls it. Use that field.

- [ ] **Step 1: Write the failing tests** (anchors verified on the reference data during planning)

```csharp
[SkippableFact]
public void FireballChain()
{
    _fixture.SkipIfNoClientData();
    var section = SpellVisualChainResolver.ResolveSection(133);
    Assert.Empty(section.MissingStores);
    var chain = section.Chain;
    var v = Assert.Single(chain.Variants);
    Assert.Equal(0, v.Slot);
    Assert.Equal(67, v.SpellVisualID);
    Assert.Contains(v.KitSlots, k => k.Slot == "PrecastKit" && k.KitID == 30);
    Assert.Contains(v.KitSlots, k => k.Slot == "CastKit" && k.KitID == 38);
    Assert.Contains(v.KitSlots, k => k.Slot == "ImpactKit" && k.KitID == 286);
    var missile = Assert.Single(v.Missiles);
    Assert.Equal(88, missile.ID);
    Assert.Equal(@"Spells\Fireball_Missile_Low.mdx", missile.Name);
    var precast = chain.Kits.Single(k => k.KitID == 30);
    Assert.Contains(precast.Effects, e => e.Name == @"Spells\Fire_Precast_Hand.mdx");
    Assert.Contains("visual 67 (slot 0) PrecastKit", precast.ReachedVia);
}

[SkippableFact]
public void FrostboltMissile()
{
    _fixture.SkipIfNoClientData();
    var v = Assert.Single(SpellVisualChainResolver.ResolveSection(116).Chain.Variants);
    Assert.Equal(13, v.SpellVisualID);
    Assert.Equal(@"Spells\Frostbolt.mdx", Assert.Single(v.Missiles).Name);
}

[SkippableFact]
public void KitSharedBySlotsIsListedOnce()
{
    _fixture.SkipIfNoClientData();
    var chain = SpellVisualChainResolver.ResolveSection(133).Chain;
    Assert.Equal(chain.Kits.Select(k => k.KitID).Distinct().Count(), chain.Kits.Count);
}

[SkippableFact]
public void SpellWithoutVisualHasNoVariants()
{
    _fixture.SkipIfNoClientData();
    var id = DBC.DBC.SpellInfoStore.Values.First(s => s.Misc != null && s.Misc.SpellVisualID[0] == 0 && s.Misc.SpellVisualID[1] == 0).ID;
    var section = SpellVisualChainResolver.ResolveSection(id);
    Assert.Empty(section.MissingStores);
    Assert.Empty(section.Chain.Variants);
}
```

A data-independent test in the same file. It builds the index from in-memory stores through an `internal static SpellVisualChain Resolve(SpellVisualChainIndex index, int spellId, int[] visualIds, int maxKits)` overload, which the resolver must expose for this; grant `InternalsVisibleTo` if `main` doesn't already. The test checks that a missing kit id goes to `UnresolvedReferences` as `"SpellVisualKit#999 (from SpellVisual#1.CastKit)"`, and that `maxKits = 1` on a two-kit visual sets `Truncated`.

`VisualChainIntegrityTests` (data): for every spell, every non-zero kit/missile-set/effect-name id either resolves or appears in `UnresolvedReferences`. Assert `UnresolvedReferences` contains no entries from the gated stores **other than** ids that are genuinely absent from the file. Compute the absent set with `ContainsKey` and assert equality, so the test can't hide a resolver bug.

Mcp `VisualToolsTests`: the tool takes only `id`. Passing `difficulty` is a schema error (the MCP SDK rejects unknown args, or the tool signature lacks it; assert whichever `main`'s tests already pin for unknown args). A response has `variants[0].slot`, and `storeUnloaded: true` + `store_not_loaded` when a gated store is null. Port `main`'s store-unloaded test, which nulls a store temporarily.

- [ ] **Step 2: Run them and confirm they fail.**

- [ ] **Step 3: Implement** the model, index, resolver and `SpellVisualChainJson`. JSON keys: `slot`, `spellVisualId`, `visualResolved`, `kitSlots[{slot,kitId}]`, `missiles[{id,effectNameId,name,motionId,motionName}]`, `missileMotion`, `kits[{kitId,reachedVia[],effects[{slot,effectNameId,name}],modelAttachments[{id,attachmentId,effectNameId,name}]}]`, `unresolvedReferences`, `truncated`, `storeUnloaded`.

- [ ] **Step 4: Wire it in.** `VisualTools.GetSpellVisualChain(int id)` returns the chain JSON, `store_not_loaded` naming the missing files, or `spell_not_found`. `get_spell`'s JSON `visual` section and the text renderer's visual block go through the same single `ResolveSection` call, as on `main` (spec: all renderings branch on one answer). Text format: one `Visual (slot N): SpellVisual #id` line per variant, then `  <Slot>Kit #id: <effect names>`, `  Missile #id: <name> (motion ...)`, and `Visual chain unavailable -- these stores did not load: ...` when gated.

- [ ] **Step 5: Run both test commands, without and with data.** All pass. Regenerate the goldens from Task 6 (the visual block is new). Diff them, and confirm only the visual block changed.

- [ ] **Step 6: Commit** `"5.4.8: full visual chain (SpellMisc -> SpellVisual -> kits, missiles, effect names, model attachments)"`.

---

### Task 8: Remaining tool surface

**Files:**
- Modify: `SpellWork.Mcp/Tools/SpellTools.cs`, `Db2Tools.cs`, `DiagnosticTools.cs`, `DecoderTools.cs`, `SpellWork.Mcp/EnumCatalog.cs`, `SpellWork.Mcp/SpellDataService.cs`
- Test: `SpellWork.Mcp.Tests/SpellToolsTests.cs`, `Db2ToolsTests.cs`, `DiagnosticToolsTests.cs`

**Interfaces:**
- Consumes: Tasks 2–7.
- Produces (tool contracts per spec §7):
  - `server_info`: adds `clientBuild: 18414`, `localeFallback` (bool), `openedFiles` (`{ store: "path" }`), `layoutMismatches[{ table, file, expectedFieldsCount, actualFieldsCount, expectedRecordSize, actualRecordSize }]`. `hotfixCache` is removed.
  - `list_db2`: one row per `.dbc`/`.db2` file found in the effective DBC directory and `Db2Path`. Each row: `name`, `extension`, `modelled`, `loaded`, plus `shadowed: true` on a `.dbc` whose `.db2` twin is the one loaded. It stays uncapped.
  - `get_db2_row`/`query_db2`: accept a store property name or a file name (`Item-sparse` → `ItemSparse`).
  - `search_spells`: the `attribute` filter accepts word 0–13. Family/aura/effect/target filters use the 5.4.8 enums.
  - `get_spell`: `difficulty` validated per Task 4 Step 6; text and JSON both honour it.
  - `compare_spells`, `get_spell_triggers`: no contract change; they must compile and pass against the new model.

- [ ] **Step 1: Write the failing tests**

```csharp
// DiagnosticToolsTests
[SkippableFact]
public async Task ServerInfoReportsOpenedFilesAndBuild()
{
    _fixture.SkipIfNoClientData();
    var json = JsonDocument.Parse(await _tools.ServerInfo()).RootElement;
    Assert.Equal(18414, json.GetProperty("clientBuild").GetInt32());
    Assert.EndsWith("SpellVisual.db2", json.GetProperty("openedFiles").GetProperty("SpellVisual").GetString());
    Assert.Equal(0, json.GetProperty("layoutMismatches").GetArrayLength());
    Assert.False(json.TryGetProperty("hotfixCache", out _));
}

// Db2ToolsTests
[SkippableFact]
public async Task ListDb2MarksShadowedDbc()
{
    _fixture.SkipIfNoClientData();
    var rows = JsonDocument.Parse(await _tools.ListDb2()).RootElement.GetProperty("rows").EnumerateArray().ToList();
    var stale = rows.Single(r => r.GetProperty("name").GetString() == "SpellVisual" && r.GetProperty("extension").GetString() == "dbc");
    Assert.True(stale.GetProperty("shadowed").GetBoolean());
    Assert.False(stale.GetProperty("loaded").GetBoolean());
}

[SkippableFact]
public async Task GetDb2RowAcceptsFileName()
{
    _fixture.SkipIfNoClientData();
    var anyItem = DBC.DBC.ItemSparse.Keys.First();
    var json = JsonDocument.Parse(await _tools.GetDb2Row("Item-sparse", anyItem)).RootElement;
    Assert.Equal(anyItem, json.GetProperty("row").GetProperty("ID").GetInt32());
}

// SpellToolsTests
[SkippableFact]
public async Task UnknownDifficultyIsAStructuredError()
{
    _fixture.SkipIfNoClientData();
    var json = JsonDocument.Parse(await _tools.GetSpell(133, difficulty: 10)).RootElement; // 10 has no Difficulty row
    Assert.Equal("unknown_difficulty", json.GetProperty("error").GetProperty("code").GetString());
}

[SkippableFact]
public async Task SearchByAttributeWord13()
{
    _fixture.SkipIfNoClientData();
    var json = JsonDocument.Parse(await _tools.SearchSpells(attribute: "13:0x1")).RootElement;
    Assert.True(json.GetProperty("totalMatches").GetInt32() >= 0);
}
```

Use the existing tests' real envelope paths (`error.code`, `rows`, `row`). Check one existing test in each file and match it. Use the existing argument format of `SearchSpells`'s attribute filter: read `SpellTools.SearchSpells`, keep its syntax and only widen the accepted word range to 0–13.

- [ ] **Step 2: Run them and confirm they fail.**
- [ ] **Step 3: Implement** the contract changes above.
- [ ] **Step 4: Run both test commands, without and with data.** All pass. Then run `StdioSmokeTests` with data to confirm the tool list is still exactly the fourteen names.
- [ ] **Step 5: Commit** `"5.4.8: tool surface (server_info, list_db2, difficulty, search)"`.

---

### Task 9: World database overrides

**Files:**
- Modify: `SpellWork.Mcp/Tools/DatabaseTools.cs`, `SpellWork.Core/Database/WorldDatabase.cs`, `SpellWork.Core/Database/Structures.cs` (if the proc-event shape needs it)
- Test: `SpellWork.Mcp.Tests/DatabaseToolsTests.cs`

**Interfaces:**
- Produces: `get_spell_db_overrides(id)` reads the spec §7 table list. `spellmisc_dbc` is matched by SpellMisc id: the set `{ DBC.Spell[id].MiscID if present } ∪ { spell_dbc.SpellMiscId where spell_dbc.Id = id }`. The response, per table, keeps `main`'s shape and adds `matchedBy` (`"spellId"` or `"spellMiscId"`) on `spellmisc_dbc`. Each table is still capped at 500 rows, and `truncatedTables` is unchanged.

- [ ] **Step 1: Write the failing tests**

Data-independent: the table list constant contains exactly these 11 `(table, column)` pairs, in this order: `("spell_proc_event","entry")`, `("spell_proc","spellId")`, `("spell_script_names","spell_id")`, `("spell_custom_attribute","spell")`, `("spell_bonus_data","entry")`, `("spell_threat","entry")`, `("spell_ranks","spell_id")`, `("spell_linked_spell","spell_trigger")`, `("spell_dbc","Id")`, `("spelleffect_dbc","EffectSpellId")`. `spellmisc_dbc` is handled separately. Make the constant `internal` and test it directly.

MySQL-backed. Copy `DatabaseToolsTests`' existing skip mechanism for live MySQL, using `SPELLWORK_TEST_MYSQL_*` or whatever it already reads:

```csharp
[SkippableFact]
public async Task RepackCustomSpellOverridesIncludeMiscByMiscId()
{
    SkipIfNoMySql();
    // spell_dbc row: Id 3617 -> SpellMiscId 200000 (verified during design)
    var json = JsonDocument.Parse(await _tools.GetSpellDbOverrides(3617)).RootElement;
    var misc = json.GetProperty("tables").GetProperty("spellmisc_dbc");
    Assert.Equal("spellMiscId", misc.GetProperty("matchedBy").GetString());
    Assert.Contains(misc.GetProperty("rows").EnumerateArray(), r => r.GetProperty("Id").GetInt32() == 200000);
}

[SkippableFact]
public async Task EveryListedTableIsQueried()
{
    SkipIfNoMySql();
    var tables = JsonDocument.Parse(await _tools.GetSpellDbOverrides(133)).RootElement.GetProperty("tables");
    foreach (var t in new[] { "spell_proc_event", "spell_proc", "spell_script_names", "spell_custom_attribute", "spell_bonus_data", "spell_threat", "spell_ranks", "spell_linked_spell", "spell_dbc", "spelleffect_dbc", "spellmisc_dbc" })
        Assert.True(tables.TryGetProperty(t, out _), t);
}
```

Match the response paths (`tables.<name>.rows`) to `main`'s actual shape. Read `GetSpellDbOverrides` first and adjust these two tests to it, keeping their intent.

- [ ] **Step 2: Run them and confirm they fail.**
- [ ] **Step 3: Implement.** Replace `OverrideTables` and update its comment block: name this repack's schema and the fact that missing tables are still reported as absent. Add the `spellmisc_dbc` two-step lookup with parameterized SQL (`@id`), never string-concatenated. `WorldDatabase` already queries `spell_proc_event`; check its column reads against `entry,SchoolMask,SpellFamilyName,SpellFamilyMask0..3,procFlags,procEx,phase,ppmRate,CustomChance,Cooldown` and fix the ordinal reads if they're off.
- [ ] **Step 4: Run Mcp tests without data, with data, and with data + MySQL.** All pass. Run `SqlGuardTests`, unchanged and green.
- [ ] **Step 5: Commit** `"5.4.8: world DB override tables for the repack schema"`.

---

### Task 10: Docs, publish, end-to-end check

**Files:**
- Modify: `docs/mcp-server.md`, `README.md`

- [ ] **Step 1: Docs.** In `README.md`: "This branch tracks **5.4.8 (build 18414)**", only Core/Mcp/tests listed, a build command `dotnet build SpellWork.sln -c Release`, and the data note (DBC folder plus optional DB2 folder; `.db2` wins). In `docs/mcp-server.md`: the 5.4.8 config table (`SPELLWORK_DB2_PATH` added, `SPELLWORK_HOTFIX_CACHE` removed, `SPELLWORK_GT_PATH` optional/unused, locale fallback); `difficulty` = Difficulty.dbc id with the valid list `0,1,2,3,4,5,6,7,8,9,11,12,14`; a rewrite of the Visual section for the 5.4.8 chain (slots, no difficulty argument, KitAreaModel unreachable); the 11 override tables and the `spellmisc_dbc` join; Known limitations replaced with: stale `.dbc` shadowing, header build 18273 on the `.db2` files, and the repack's custom spells appearing only as overrides. The registration example uses the server name `spellwork-548` and output `out/mcp548`.

- [ ] **Step 2: Publish and smoke-test over stdio**

```bash
dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o out/mcp548 --nologo
```

With the reference-data env vars set, run `StdioSmokeTests` against the published output, the way `main`'s smoke test locates the server (read the test for its path variable). Expected: pass, with fourteen tools listed.

- [ ] **Step 3: Full verification run**

Run both test commands three ways: no env vars, data, data + MySQL. Record the pass/skip counts of each run in the commit message body.

- [ ] **Step 4: Commit** `"5.4.8: docs and publish layout"`.

- [ ] **Step 5: Hand back to the user.** Don't push. Give them the registration command:

```bash
claude mcp add --scope user spellwork-548 \
  -e SPELLWORK_DBC_PATH=C:/MoP/MOPPREMIUM/Repack/dbc \
  -e SPELLWORK_DB2_PATH=C:/MoP/MOPPREMIUM/Repack/db2 \
  -e SPELLWORK_MYSQL_HOST=127.0.0.1 -e SPELLWORK_MYSQL_USER=<read-only user> \
  -e SPELLWORK_MYSQL_PASS=<password> -e SPELLWORK_MYSQL_WORLD_DB=world \
  -- dotnet F:/WorkDir/SpellWork/out/mcp548/SpellWork.Mcp.dll
```

Point out that the repack's only MySQL account is `root`. Per `docs/mcp-server.md`, they should create a `SELECT`-only user before registering.
