# SpellWork.Core Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract SpellWork's spell decoding into a headless `netstandard2.0` library that the existing WinForms GUI and a future .NET 9 MCP server can both consume, proving via golden-file regression that no rendering behaviour changed.

**Architecture:** A new `SpellWork.Core` holds the DB2 loading, structures, enums and `SpellInfo` decoding. `SpellInfo.Write` is retargeted from `RichTextBox` to an `ISpellInfoWriter` interface; the GUI supplies a `RichTextBox`-backed implementation so it renders identically, and Core supplies a `StringBuilder`-backed one for headless use. JSON output is produced separately by reading `SpellInfo`'s public properties, not through the writer.

**Tech Stack:** C# / .NET Framework 4.8 (GUI), netstandard2.0 (Core), .NET 9 (tests), `DBFileReaderLib` (vendored, netstandard2.0), xUnit.

**Spec:** `docs/superpowers/specs/2026-08-01-spellwork-mcp-design.md` — this plan implements §4, §5 and §10. The MCP server (§6) and visual chain (§7) are separate plans written after this one lands.

## Global Constraints

- Branch: `8.x-mcp`. Never commit to `8.x`.
- Target frameworks: `SpellWork.Core` = `netstandard2.0`, `SpellWork` = `net48`, `SpellWork.Core.Tests` = `net9.0`. `netstandard2.0` is mandatory for Core — it is the only TFM both .NET Framework 4.8 and .NET 9 can reference.
- `SpellWork.Core` must not reference `System.Windows.Forms` or `System.Drawing`. Enforced by a test in Task 4.
- `SpellWork.Core` must contain zero `Console.Write`/`Console.WriteLine` calls. Stdout is the JSON-RPC channel in the future MCP host, and stray writes silently kill the transport. Enforced by a test in Task 3.
- `DBFileReaderLib` is referenced by HintPath `..\packages\DBFileReaderLib.1.0.0.0\lib\DBFileReaderLib.dll`. Do not add a NuGet package for it; it is vendored.
- **Build the solution with Visual Studio MSBuild, never `dotnet build`.** `SpellWork.csproj` is a legacy non-SDK project whose WinForms `.resx` files carry non-string resources; the .NET SDK's MSBuild rejects them with MSB3822/MSB3823 and the build fails before compiling a line. Export this once per shell and use `$MSBUILD` in every build command:

  ```bash
  export MSBUILD="/c/Program Files/Microsoft Visual Studio/2022/Enterprise/MSBuild/Current/Bin/MSBuild.exe"
  "$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug
  ```

  PowerShell equivalent: `& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" -restore SpellWork.sln -p:Configuration=Debug`. The `-restore` is required, not decorative — without it NuGet fails with "doesn't list 'win' as a RuntimeIdentifier". Verified working against the current tree, which builds to `SpellWork\bin\Debug\SpellWork.exe` with warnings but no errors.

- **`dotnet` is still correct for the two SDK-style projects.** `dotnet build SpellWork.Core -c Debug` and every `dotnet test SpellWork.Core.Tests` command work normally and do not touch the legacy project. Only solution-wide builds need MSBuild.
- **Launch the GUI with its working directory set to `SpellWork\bin\Debug`.** The persisted settings default to the *relative* paths `dbc` and `gt` (`SpellWork/app.config:29,35`), resolved against the current directory, not the exe's. Launched from the repository root the app shows a "Missing files" dialog and exits. The client data is junctioned into `SpellWork\bin\Debug\dbc` and `...\gt` — `bin/` is gitignored, so nothing machine-specific is committed, but `git clean -fdx` destroys the junctions and they must be recreated:

  ```powershell
  New-Item -ItemType Junction -Path "SpellWork\bin\Debug\dbc" -Target "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\dbc"
  New-Item -ItemType Junction -Path "SpellWork\bin\Debug\gt"  -Target "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\gt"
  ```

  This applies only to launching the GUI. The `--dump-goldens` mode takes its paths as arguments and does not depend on the junctions or the working directory.
- Golden comparisons normalise line endings to `\n` on both sides before comparing. `RichTextBox.Text` returns `\n` while `StringBuilder` + `Environment.NewLine` produces `\r\n`; without normalisation every golden test fails for a reason that has nothing to do with the refactor.
- One commit per task, message in the repository's existing style (`area: imperative summary`).

---

### Task 1: Make DBC.Load awaitable and capture baseline goldens

The spec requires goldens captured before the writer extraction. `DBC.Load()` is `public static async void`, so no caller can know when loading finished — `Program.Main` already calls it and starts the GUI immediately, a pre-existing race. Making it awaitable is scaffolding for a deterministic capture and cannot affect `SpellInfo.Write` output, so the spec's intent is preserved.

**Files:**
- Modify: `SpellWork/DBC/DBC.cs:84` (signature only)
- Modify: `SpellWork/Program.cs:17` (add `args`, add dump mode)
- Create: `SpellWork.Core.Tests/Goldens/` (output directory, created by the run)

**Interfaces:**
- Consumes: nothing.
- Produces: `Task DBC.Load()` — same body, awaitable. Golden files at `SpellWork.Core.Tests/Goldens/spell-<id>.txt`, UTF-8 without BOM, `\n` line endings.

- [ ] **Step 1: Change the Load signature**

In `SpellWork/DBC/DBC.cs`, change line 84 from `public static async void Load()` to:

```csharp
public static async Task Load()
```

The body is unchanged. `System.Threading.Tasks` is already imported at line 14.

- [ ] **Step 2: Await it in Program.Main**

In `SpellWork/Program.cs`, replace line 36 `DBC.DBC.Load();` with:

```csharp
                DBC.DBC.Load().GetAwaiter().GetResult();
```

`Main` is `[STAThread]` and synchronous; `GetAwaiter().GetResult()` blocks until loading completes and rethrows the original exception rather than an `AggregateException`, so the existing `catch (ArgumentException)` and `catch (DirectoryNotFoundException)` handlers below still match.

- [ ] **Step 3: Build and confirm the GUI still starts**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug`
Expected: build succeeds. Launch `SpellWork\bin\Debug\SpellWork.exe`, confirm the spell list populates. The list is now guaranteed populated at startup rather than racing.

- [ ] **Step 4: Add the golden dump mode**

The dump mode takes its paths as arguments and never opens a dialog. Both matter: this step runs headless, where a modal `MessageBox` does not fail the run, it hangs it forever.

In `SpellWork/Program.cs`, change the signature at line 17 to `static void Main(string[] args)`, then insert this block immediately after the four `CultureInfo` lines and **before** the existing `var dbcPath = ...` check — it must run before that check, because it supplies the paths the check validates:

```csharp
            // Temporary golden-capture harness (removed in Task 6). Overrides the
            // persisted settings in memory only -- nothing is written back.
            if (args.Length == 4 && args[0] == "--dump-goldens")
            {
                Settings.Default.DbcPath = args[2];
                Settings.Default.GtPath = args[3];
                RunGoldenDump(args[1]);
                return;
            }
```

Then add these three methods to the `Program` class:

```csharp
        private static void RunGoldenDump(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            try
            {
                DBC.DBC.Load().GetAwaiter().GetResult();
                DumpGoldens(outputDirectory);
            }
            catch (Exception ex)
            {
                // Deliberately not a MessageBox: this runs with no one at the
                // keyboard, where a modal dialog hangs the process rather than
                // failing it. The caller checks for this file.
                File.WriteAllText(Path.Combine(outputDirectory, "ERROR.txt"), ex.ToString());
            }
        }

        // Temporary harness: captures the pre-refactor rendering of SpellInfo.Write
        // so the SpellWork.Core extraction can be proven behaviour-preserving.
        // Removed in Task 6 once the goldens are committed.
        private static void DumpGoldens(string outputDirectory)
        {

            var ids = new List<int> { 116, 133, 172, 589, 1459, 2825 };

            // None of the fixed ids exercise reagents, totems, content-tuning
            // scaling or PPM procs, so pick the lowest-id spell for each.
            foreach (var id in new[]
                     {
                         FirstIdWhere(s => s.Reagents != null),
                         FirstIdWhere(s => s.Totems != null),
                         FirstIdWhere(s => s.Scaling != null),
                         FirstIdWhere(s => s.ProcsPerMinute != null)
                     })
            {
                if (id != 0)
                    ids.Add(id);
            }

            var missing = new List<int>();

            using (var form = new Form())
            using (var rtb = new RichTextBox())
            {
                form.Controls.Add(rtb);
                var unused = form.Handle; // force handle creation; SelectionFont needs one

                foreach (var id in ids.Distinct().OrderBy(id => id))
                {
                    if (!DBC.DBC.SpellInfoStore.ContainsKey(id))
                    {
                        missing.Add(id);
                        continue;
                    }

                    // No Clear() needed -- SpellInfo.Write opens with rtb.Clear().
                    DBC.DBC.SpellInfoStore[id].Write(rtb);
                    File.WriteAllText(
                        Path.Combine(outputDirectory, $"spell-{id}.txt"),
                        rtb.Text.Replace("\r\n", "\n"),
                        new UTF8Encoding(false));
                }
            }

            if (missing.Count > 0)
                File.WriteAllText(
                    Path.Combine(outputDirectory, "MISSING.txt"),
                    string.Join(Environment.NewLine, missing));
        }

        private static int FirstIdWhere(Func<Spell.SpellInfo, bool> predicate)
        {
            return DBC.DBC.SpellInfoStore.Values
                .Where(predicate)
                .OrderBy(s => s.ID)
                .Select(s => s.ID)
                .FirstOrDefault();
        }
```

Add to the usings at the top of `Program.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
```

- [ ] **Step 5: Rebuild with the dump mode**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug`
Expected: build succeeds.

- [ ] **Step 6: Capture the goldens**

The client data is the BFA-HavenCore-Corruption build output, already extracted and verified present: 715 `.db2` files under `dbc\enUS` and the game tables under `gt`. Use these paths exactly:

```bash
SpellWork/bin/Debug/SpellWork.exe --dump-goldens SpellWork.Core.Tests/Goldens \
  "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\dbc" \
  "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\gt"
```

Note the DBC argument is the `dbc` directory itself, not `dbc\enUS` — `DBC.Load` appends the locale.

Expected: the process exits without showing a window, and `SpellWork.Core.Tests\Goldens\` contains 8-10 `spell-<id>.txt` files.

Check, in this order:
1. **`ERROR.txt` present** → loading threw. Read it; do not proceed.
2. **`MISSING.txt` present** → those spell ids are absent from this client data. Fewer goldens is survivable, but report which ids in your task report.
3. **`spell-133.txt`** starts with `ID - 133 Fireball` and contains an `Effect0` section. An empty or one-line file means loading did not complete — do not proceed.

- [ ] **Step 7: Commit**

```bash
git add SpellWork/DBC/DBC.cs SpellWork/Program.cs SpellWork.Core.Tests/Goldens
git commit -m "test: capture baseline spell rendering goldens

DBC.Load was async void, so no caller could observe completion --
Program.Main started the GUI while loading was still in flight. Changed to
async Task and awaited at the single call site, which also makes a
deterministic golden capture possible.

Added a temporary --dump-goldens mode that renders a fixed set of spells
plus the lowest-id spell exercising reagents, totems, content-tuning
scaling and PPM procs, so the SpellWork.Core extraction can be proven to
change no rendering behaviour."
```

---

### Task 2: Create SpellWork.Core and the test project

Mechanical file moves only. No logic changes, so any behaviour difference after this task is a mistake.

**Files:**
- Create: `SpellWork.Core/SpellWork.Core.csproj`
- Create: `SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`
- Move: `SpellWork/DBC/Structures/*.cs` (46 files) → `SpellWork.Core/DBC/Structures/`
- Move: `SpellWork/GameTables/**` → `SpellWork.Core/GameTables/`
- Move: `SpellWork/Extensions/LinqExtensions.cs` → `SpellWork.Core/Extensions/`
- Split: `SpellWork/Extensions/Extensions.cs` → the non-UI half moves to `SpellWork.Core/Extensions/Extensions.cs`, the control half stays as `SpellWork/Extensions/ControlExtensions.cs`
- Modify: `SpellWork/SpellWork.csproj` (drop moved `<Compile Include>` entries, add `<ProjectReference>`)
- Modify: `SpellWork.sln`

**Interfaces:**
- Consumes: Task 1's awaitable `DBC.Load`.
- Produces: `SpellWork.Core` assembly containing namespaces `SpellWork.DBC.Structures`, `SpellWork.GameTables`, `SpellWork.Extensions`. Namespaces are deliberately unchanged so no `using` statements need editing anywhere.

- [ ] **Step 1: Create the Core project**

Create `SpellWork.Core/SpellWork.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <RootNamespace>SpellWork</RootNamespace>
    <AssemblyName>SpellWork.Core</AssemblyName>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="DBFileReaderLib">
      <HintPath>..\packages\DBFileReaderLib.1.0.0.0\lib\DBFileReaderLib.dll</HintPath>
    </Reference>
  </ItemGroup>

</Project>
```

SDK-style projects glob `**/*.cs` automatically, so moved files need no `<Compile Include>` entries.

- [ ] **Step 2: Create the test project**

Create `SpellWork.Core.Tests/SpellWork.Core.Tests.csproj`:

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
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SpellWork.Core\SpellWork.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Goldens\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Move the files**

```bash
mkdir -p SpellWork.Core/DBC/Structures SpellWork.Core/GameTables/Structures SpellWork.Core/Extensions
git mv SpellWork/DBC/Structures/*.cs SpellWork.Core/DBC/Structures/
git mv SpellWork/GameTables/GameTables.cs SpellWork.Core/GameTables/
git mv SpellWork/GameTables/Structures/*.cs SpellWork.Core/GameTables/Structures/
# GtSpellScalingEntry is `internal class`; widen it to `public`. DBC.cs and
# SpellInfo.cs name it as the GameTable<T> type argument and stay in the GUI
# assembly, which cannot see an internal type across the assembly boundary.
git mv SpellWork/Extensions/Extensions.cs SpellWork.Core/Extensions/
git mv SpellWork/Extensions/LinqExtensions.cs SpellWork.Core/Extensions/
```

`LinqExtensions.cs` moves untouched — it only uses `System`, `System.Linq` and `System.Reflection`. `Extensions.cs` cannot; Step 4 splits it.

- [ ] **Step 4: Split the control extensions back out of Extensions.cs**

`SpellWork/Extensions/Extensions.cs` is one static class mixing string/numeric helpers with eight WinForms control helpers. Moved whole it would fail to compile in Core, which has no `System.Windows.Forms` reference, and would trip the hygiene test added in Task 4.

Cut these eight methods out of `SpellWork.Core/Extensions/Extensions.cs` and delete its `using System.Data;` and `using System.Windows.Forms;` lines:

| Method | Bound to |
| --- | --- |
| `SetCheckedItemFromFlag(this CheckedListBox, uint)` | `CheckedListBox` |
| `GetFlagsValue(this CheckedListBox)` | `CheckedListBox` |
| `SetFlags<T>(this CheckedListBox)` | `CheckedListBox` |
| `SetFlags<T>(this CheckedListBox, string)` | `CheckedListBox` |
| `SetFlags(this CheckedListBox, Type, string)` | `CheckedListBox` |
| `SetEnumValues<T>(this ComboBox, string)` | `ComboBox`, `DataTable` |
| `SetEnumValuesDirect<T>(this ComboBox, bool)` | `ComboBox` |
| `SetStructFields<T>(this ComboBox)` | `ComboBox`, `DataTable` |

Everything else stays in Core, including `FullNameAttribute` — `LinqExtensions.CompareType` and `SpellEnums.cs` both decorate members with it.

Create `SpellWork/Extensions/ControlExtensions.cs` holding the eight verbatim, with this header:

```csharp
using System;
using System.Data;
using System.Reflection;
using System.Windows.Forms;

namespace SpellWork.Extensions
{
    // Split out of Extensions.cs when the rest of it moved to SpellWork.Core.
    // Same namespace, so no call site changes: C# resolves extension methods by
    // namespace, not by declaring class.
    public static class ControlExtensions
    {
```

These call `NormalizeString()` and `GetFullName()`, which now live in Core's `Extensions` — same namespace, so they resolve without a `using` change. Add `<Compile Include="Extensions\ControlExtensions.cs" />` to `SpellWork/SpellWork.csproj`.

- [ ] **Step 5: Update the GUI csproj**

In `SpellWork/SpellWork.csproj`, delete every `<Compile Include="DBC\Structures\...">` line (lines 92-137), the `<Compile Include="GameTables\GameTables.cs" />` and `<Compile Include="GameTables\Structures\GtSpellScalingEntry.cs" />` lines, the `<Compile Include="Extensions\LinqExtensions.cs" />` line, and the `<Compile Include="Extensions\Extensions.cs" />` line.

Leave the `DBFileReaderLib` `<Reference>` block (lines 68-70) in place. It does *not* arrive transitively: a legacy non-SDK project does not inherit a HintPath `<Reference>` declared in an SDK-style `<ProjectReference>` target — that flows only through NuGet's `project.assets.json`, which legacy projects do not consume. `DBC.cs` still names `Storage<>` directly, so deleting the block fails the build with `CS0246` (verified in Task 2). Both projects reference the same vendored DLL.

Add before the closing `</Project>`:

```xml
  <ItemGroup>
    <ProjectReference Include="..\SpellWork.Core\SpellWork.Core.csproj">
      <Project>{5AFE025C-5DBA-4AC2-9269-533A981F8B8B}</Project>
      <Name>SpellWork.Core</Name>
    </ProjectReference>
  </ItemGroup>
```

The GUID must be the one `dotnet sln add` wrote into `SpellWork.sln` for `SpellWork.Core` (do Step 6 first and copy it from there). MSBuild resolves the reference by path regardless, but a mismatched GUID confuses Visual Studio's solution explorer.

Also change line 65 `<AutoGenerateBindingRedirects>false</AutoGenerateBindingRedirects>` to `true`. A net48 project consuming a netstandard2.0 assembly pulls in facade assemblies that need redirects; leaving this false produces `FileLoadException` at runtime rather than at build time.

- [ ] **Step 6: Add both projects to the solution**

```bash
dotnet sln SpellWork.sln add SpellWork.Core/SpellWork.Core.csproj SpellWork.Core.Tests/SpellWork.Core.Tests.csproj
```

- [ ] **Step 7: Build and run the GUI**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug`
Expected: all three projects build. If the net48 project fails to resolve types from Core, the ProjectReference GUID is wrong — regenerate it by adding the reference through Visual Studio instead of by hand.

Launch `SpellWork\bin\Debug\SpellWork.exe` and confirm the spell list still populates and clicking a spell still renders.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor: move DB2 structures and game tables into SpellWork.Core

Creates a netstandard2.0 SpellWork.Core and an xUnit test project, and
moves the 46 DB2 structure classes, the game-table reader and the non-UI
extension helpers into it. Namespaces are unchanged, so no using statements
move with them.

Extensions.cs could not move whole: eight of its methods extend
CheckedListBox and ComboBox. Those stay behind as ControlExtensions in the
same namespace, which C# resolves identically, so no call site moved.

netstandard2.0 is required rather than preferred: it is the only target
framework both the .NET Framework 4.8 GUI and the planned .NET 9 MCP
server can reference, and the vendored DBFileReaderLib already targets it.

No logic changes."
```

---

### Task 3: Introduce SpellWorkConfig and ILogger

Removes Core's two remaining couplings to the GUI: `Settings.Default` and `Console.WriteLine`.

**Files:**
- Create: `SpellWork.Core/SpellWorkConfig.cs`
- Create: `SpellWork.Core/ISpellWorkLogger.cs`
- Modify: `SpellWork/DBC/DBC.cs` **in place** — it cannot move yet, see below
- Create: `SpellWork.Core.Tests/CoreHygieneTests.cs`
- Modify: `SpellWork/Program.cs`

`DBC.cs` stays in the GUI project for now. It builds `SpellInfoStore` out of
`SpellWork.Spell.SpellInfo` (DBC.cs:81,129,159-160 and ~20 more), and
`SpellInfo.cs` cannot reach Core until Task 5 — it takes a `RichTextBox`
(SpellInfo.cs:212). Moving DBC.cs here would make Core depend on the GUI
assembly that already depends on Core. Task 5 Step 1 moves it, once
`SpellInfo` has landed. Everything else in this task happens now; only the
`git mv` waits.

**Interfaces:**
- Consumes: `Task DBC.Load()` from Task 1.
- Produces:
  - `class SpellWorkConfig { string DbcPath; string GtPath; string Locale; string HotfixCachePath; string MySqlHost; string MySqlPortOrPipe; string MySqlUser; string MySqlPassword; string WorldDatabase; bool UseDatabase; }`
  - `interface ISpellWorkLogger { void Warn(string message); }` plus `NullSpellWorkLogger` (discards) — later plans add a stderr implementation.
  - `static Task DBC.LoadAsync(SpellWorkConfig config, ISpellWorkLogger logger)`

- [ ] **Step 1: Write the failing hygiene test**

Create `SpellWork.Core.Tests/CoreHygieneTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class CoreHygieneTests
    {
        private static string CoreSourceRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SpellWork.sln")))
                dir = dir.Parent;

            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "SpellWork.Core");
        }

        [Fact]
        public void Core_writes_nothing_to_stdout()
        {
            var offenders = Directory
                .EnumerateFiles(CoreSourceRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains("Console.Write"))
                .Select(f => Path.GetFileName(f))
                .ToList();

            Assert.True(offenders.Count == 0,
                "stdout is the JSON-RPC channel for the MCP host; these files would " +
                "corrupt it: " + string.Join(", ", offenders));
        }
    }
}
```

- [ ] **Step 2: Run it and confirm it is wired up**

Run: `dotnet test SpellWork.Core.Tests -c Debug --filter Core_writes_nothing_to_stdout`
Expected: **PASS, vacuously.** Core has no offenders yet. This is the one test in the plan that cannot start red from real code — it asserts an absence. A failure here means `CoreSourceRoot()` did not find the solution root, not that an offender exists.

A test that has only ever passed proves nothing, so make it fail on purpose before trusting it. Add a throwaway line `// Console.Write` to `SpellWork.Core/SpellWorkConfig.cs` after Step 3 creates it, re-run, and confirm it **FAILS naming `SpellWorkConfig.cs`**. Delete the line and confirm it passes again. (The check is a substring scan, so a commented-out occurrence trips it — that is the point: it cannot be fooled and it needs no compile.) Nothing of this survives into the commit.

The test does real work in Task 5, where `DBC.cs` and its twenty warnings actually land in Core — already converted to `logger.Warn` by this task, so it stays green there.

- [ ] **Step 3: Create the config and logger types**

Create `SpellWork.Core/SpellWorkConfig.cs`:

```csharp
namespace SpellWork
{
    public class SpellWorkConfig
    {
        public string DbcPath { get; set; }
        public string GtPath { get; set; }
        public string Locale { get; set; } = "enUS";
        public string HotfixCachePath { get; set; }

        public string MySqlHost { get; set; }
        public string MySqlPortOrPipe { get; set; }
        public string MySqlUser { get; set; }
        public string MySqlPassword { get; set; }
        public string WorldDatabase { get; set; }
        public bool UseDatabase { get; set; }
    }
}
```

Create `SpellWork.Core/ISpellWorkLogger.cs`:

```csharp
namespace SpellWork
{
    public interface ISpellWorkLogger
    {
        void Warn(string message);
    }

    public sealed class NullSpellWorkLogger : ISpellWorkLogger
    {
        public static readonly NullSpellWorkLogger Instance = new NullSpellWorkLogger();

        private NullSpellWorkLogger() { }

        public void Warn(string message) { }
    }
}
```

- [ ] **Step 4: Retarget DBC.Load**

In `SpellWork/DBC/DBC.cs` — still in the GUI project, see the Files note — delete the `using SpellWork.Properties;` line, change the signature at line 84 to:

```csharp
        public static async Task LoadAsync(SpellWorkConfig config, ISpellWorkLogger logger)
```

Then replace the four `Settings.Default` reads:

| Line | Was | Becomes |
| --- | --- | --- |
| 89 | `Settings.Default.HotfixCachePath` | `config.HotfixCachePath` |
| 94 | `Settings.Default.HotfixCachePath` | `config.HotfixCachePath` |
| 108 | `{Settings.Default.DbcPath}\{Settings.Default.Locale}` | `{config.DbcPath}\{config.Locale}` |
| 429 | `{Settings.Default.GtPath}\SpellScaling.txt` | `{config.GtPath}\SpellScaling.txt` |

Replace all 20 `Console.WriteLine(...)` calls with `logger.Warn(...)`. They are all single-argument interpolated strings, so the change is `Console.WriteLine(` → `logger.Warn(` in each case. The one at line 93-95 spans lines; keep its interpolation intact.

Then confirm none survived: `rg -n "Console\.Write" SpellWork/DBC/DBC.cs` must print nothing. The hygiene test cannot catch a miss here — `DBC.cs` is not under `SpellWork.Core/` until Task 5, and a `Console.WriteLine` left behind now becomes a Task 5 failure instead.

- [ ] **Step 5: Wire the GUI to the new signature**

In `SpellWork/Program.cs`, replace the `DBC.DBC.Load().GetAwaiter().GetResult();` line with:

```csharp
                var config = new SpellWorkConfig
                {
                    DbcPath = Settings.Default.DbcPath,
                    GtPath = Settings.Default.GtPath,
                    Locale = Settings.Default.Locale,
                    HotfixCachePath = Settings.Default.HotfixCachePath,
                    MySqlHost = Settings.Default.Host,
                    MySqlPortOrPipe = Settings.Default.PortOrPipe,
                    MySqlUser = Settings.Default.User,
                    MySqlPassword = Settings.Default.Pass,
                    WorldDatabase = Settings.Default.WorldDbName,
                    UseDatabase = Settings.Default.UseDbConnect
                };

                DBC.DBC.LoadAsync(config, NullSpellWorkLogger.Instance).GetAwaiter().GetResult();
```

Extract that `new SpellWorkConfig { ... }` block into a `private static SpellWorkConfig BuildConfig()` and call it from both places, because `RunGoldenDump` also calls `DBC.DBC.Load()` and will no longer compile. Its body becomes:

```csharp
                DBC.DBC.LoadAsync(BuildConfig(), NullSpellWorkLogger.Instance).GetAwaiter().GetResult();
                DumpGoldens(outputDirectory);
```

The `Settings.Default.DbcPath = args[2];` overrides in `Main` still work — `BuildConfig` reads `Settings.Default` after they are applied.

- [ ] **Step 6: Build, run the GUI, re-verify a golden**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug`, then re-run the same dump against a scratch directory:

```bash
SpellWork/bin/Debug/SpellWork.exe --dump-goldens /tmp/goldens-check \
  "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\dbc" \
  "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\gt"

diff -r /tmp/goldens-check SpellWork.Core.Tests/Goldens
```

Expected: `diff` reports nothing. Nothing in this task touches rendering, so any difference is a mistake introduced here.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: replace Settings.Default and Console.WriteLine in DBC loading

Cuts DBC loading's two remaining ties to the WinForms host so that Task 5
can move the file into SpellWork.Core once SpellInfo is there. Paths now
arrive via an injected SpellWorkConfig, and the
twenty unknown-reference warnings go through ISpellWorkLogger instead of
Console.WriteLine.

The logging change is not cosmetic. The planned MCP server speaks JSON-RPC
over stdout, where a stray Console.WriteLine corrupts the stream and kills
the transport with no diagnosable error. CoreHygieneTests now fails the
build if one reappears."
```

---

### Task 4: ISpellInfoWriter and PlainTextSpellInfoWriter

**Files:**
- Create: `SpellWork.Core/Spell/ISpellInfoWriter.cs`
- Create: `SpellWork.Core/Spell/SpellInfoWriterBase.cs`
- Create: `SpellWork.Core/Spell/PlainTextSpellInfoWriter.cs`
- Create: `SpellWork.Core.Tests/PlainTextSpellInfoWriterTests.cs`
- Modify: `SpellWork.Core.Tests/CoreHygieneTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `ISpellInfoWriter` with the 13 members below; `TextColor` and `TextStyle` enums; `abstract SpellInfoWriterBase`; `PlainTextSpellInfoWriter` with `string ToString()` returning accumulated text. Task 5 calls these from `SpellInfo.Write`; Task 6 derives the GUI writer from `SpellInfoWriterBase`.

- [ ] **Step 1: Write the failing writer tests**

Create `SpellWork.Core.Tests/PlainTextSpellInfoWriterTests.cs`:

```csharp
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class PlainTextSpellInfoWriterTests
    {
        [Fact]
        public void AppendFormatLine_appends_formatted_text_and_a_newline()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLine("ID - {0} {1}", 133, "Fireball");
            Assert.Equal("ID - 133 Fireball\n", writer.ToString().Replace("\r\n", "\n"));
        }

        [Fact]
        public void Styling_calls_do_not_emit_text()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.SetBold();
            writer.SetStyle(TextColor.Blue, TextStyle.Bold);
            writer.SetColor(TextColor.Red);
            writer.SetDefaultStyle();
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public void Clear_discards_everything_written_so_far()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendText("discarded");
            writer.Clear();
            Assert.Equal("", writer.ToString());
        }

        // Mirrors RichTextBoxExtensions.IsValueConsideredNullForFormatting so the
        // two writers cannot disagree about what counts as "null" for formatting.
        [Theory]
        [InlineData(0, false)]
        [InlineData(5, true)]
        public void AppendFormatLineIfNotNull_skips_zero_ints(int value, bool expectOutput)
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("v: {0}", value);
            Assert.Equal(expectOutput, writer.ToString().Length > 0);
        }

        [Fact]
        public void AppendFormatLineIfNotNull_skips_empty_strings()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("v: {0}", "");
            Assert.Equal("", writer.ToString());
        }

        [Fact]
        public void AppendFormatLineIfNotNull_always_prints_booleans()
        {
            var writer = new PlainTextSpellInfoWriter();
            writer.AppendFormatLineIfNotNull("v: {0}", false);
            Assert.Equal("v: False\n", writer.ToString().Replace("\r\n", "\n"));
        }
    }
}
```

- [ ] **Step 2: Run and verify failure**

Run: `dotnet test SpellWork.Core.Tests -c Debug --filter PlainTextSpellInfoWriterTests`
Expected: FAIL — `PlainTextSpellInfoWriter` does not exist.

- [ ] **Step 3: Write the interface**

Create `SpellWork.Core/Spell/ISpellInfoWriter.cs`:

```csharp
namespace SpellWork.Spell
{
    // Only the six colours and two styles SpellInfo actually uses, so Core
    // needs no System.Drawing reference.
    public enum TextColor { Black, Blue, DarkBlue, DarkRed, MediumPurple, Red }

    public enum TextStyle { Regular, Bold }

    public interface ISpellInfoWriter
    {
        void Clear();
        void Append(object text);
        void AppendText(string text);
        void AppendLine();
        void AppendLine(string text);
        void AppendFormat(string format, params object[] args);
        void AppendFormatLine(string format, params object[] args);
        void AppendFormatIfNotNull<T>(string format, T arg);
        void AppendFormatLineIfNotNull<T>(string format, T arg);
        void SetColor(TextColor color);
        void SetStyle(TextColor color, TextStyle style);
        void SetBold();
        void SetDefaultStyle();
    }
}
```

- [ ] **Step 4: Write the base class**

Create `SpellWork.Core/Spell/SpellInfoWriterBase.cs`. The `IsValueConsideredNullForFormatting` body is moved verbatim from `SpellWork/Extensions/RichTextBoxExtensions.cs:37-73` so both writers apply identical rules:

```csharp
using System;

namespace SpellWork.Spell
{
    public abstract class SpellInfoWriterBase : ISpellInfoWriter
    {
        public abstract void Clear();
        public abstract void AppendText(string text);

        public abstract void SetColor(TextColor color);
        public abstract void SetStyle(TextColor color, TextStyle style);
        public abstract void SetBold();
        public abstract void SetDefaultStyle();

        public void Append(object text) => AppendText(text.ToString());

        public void AppendLine() => AppendText(Environment.NewLine);

        public void AppendLine(string text) => AppendText(text + Environment.NewLine);

        public void AppendFormat(string format, params object[] args)
            => AppendText(string.Format(format, args));

        public void AppendFormatLine(string format, params object[] args)
            => AppendText(string.Format(format, args) + Environment.NewLine);

        public void AppendFormatIfNotNull<T>(string format, T arg)
        {
            if (!IsValueConsideredNullForFormatting(arg))
                AppendFormat(format, arg);
        }

        public void AppendFormatLineIfNotNull<T>(string format, T arg)
        {
            if (!IsValueConsideredNullForFormatting(arg))
                AppendFormatLine(format, arg);
        }

        private static bool IsValueConsideredNullForFormatting<T>(T arg)
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                    return true;
                case TypeCode.Object:
                    return arg == null;
                case TypeCode.Boolean:
                    return false; // always print a boolean
                case TypeCode.Char:
                    return Convert.ToChar(arg) == '\0';
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                    return Convert.ToInt64(arg) == 0L;
                case TypeCode.UInt64:
                    return Convert.ToUInt64(arg) == 0uL;
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return Math.Abs(Convert.ToDecimal(arg)) <= 1.0E-5M;
                case TypeCode.DateTime:
                    return false;
                case TypeCode.String:
                    return string.IsNullOrEmpty(Convert.ToString(arg));
                default:
                    break;
            }

            return true;
        }
    }
}
```

- [ ] **Step 5: Write the plain-text writer**

Create `SpellWork.Core/Spell/PlainTextSpellInfoWriter.cs`:

```csharp
using System.Text;

namespace SpellWork.Spell
{
    public sealed class PlainTextSpellInfoWriter : SpellInfoWriterBase
    {
        private readonly StringBuilder _builder = new StringBuilder();

        public override void Clear() => _builder.Clear();

        public override void AppendText(string text) => _builder.Append(text);

        public override void SetColor(TextColor color) { }

        public override void SetStyle(TextColor color, TextStyle style) { }

        public override void SetBold() { }

        public override void SetDefaultStyle() { }

        public override string ToString() => _builder.ToString();
    }
}
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test SpellWork.Core.Tests -c Debug --filter PlainTextSpellInfoWriterTests`
Expected: PASS, 7 test cases — five `[Fact]`s plus the two `[InlineData]` rows of the `[Theory]`.

- [ ] **Step 7: Add the no-UI-references hygiene test**

Append this method to `CoreHygieneTests`:

```csharp
        [Fact]
        public void Core_references_no_ui_assemblies()
        {
            var offenders = Directory
                .EnumerateFiles(CoreSourceRoot(), "*.cs", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var text = File.ReadAllText(f);
                    return text.Contains("using System.Windows.Forms")
                        || text.Contains("using System.Drawing");
                })
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "SpellWork.Core must stay headless; these files reference UI " +
                "assemblies: " + string.Join(", ", offenders));
        }
```

- [ ] **Step 8: Run the full suite**

Run: `dotnet test SpellWork.Core.Tests -c Debug`
Expected: PASS. `SpellInfo.cs` is still in the GUI project, so Core is clean.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: add ISpellInfoWriter and a plain-text implementation

Introduces the abstraction SpellInfo will render through, derived from the
thirteen members SpellInfo actually calls on its RichTextBox. TextColor and
TextStyle carry only the six colours and two styles in use, so Core needs
no System.Drawing reference.

SpellInfoWriterBase implements the format overloads and the *IfNotNull
variants in terms of a single abstract AppendText, so the null-formatting
rules moved verbatim from RichTextBoxExtensions exist in exactly one place
and cannot drift between the GUI and headless renderings."
```

---

### Task 5: Port SpellInfo to ISpellInfoWriter

The task the goldens exist for.

**Files:**
- Move: `SpellWork/Spell/{SpellInfo,SpellEnums,ExpectedStat}.cs` → `SpellWork.Core/Spell/`, then modify
- Move: `SpellWork/DBC/DBC.cs` → `SpellWork.Core/DBC/DBC.cs` (deferred from Task 3; no edits, it was already retargeted there)
- Create: `SpellWork.Core.Tests/GoldenRenderingTests.cs`
- Modify: `SpellWork/SpellWork.csproj`

**Interfaces:**
- Consumes: `ISpellInfoWriter`, `TextColor`, `TextStyle` from Task 4; `LoadAsync` from Task 3.
- Produces: `void SpellInfo.Write(ISpellInfoWriter writer)` replacing `Write(RichTextBox)`. Task 6 calls it with the GUI writer.

- [ ] **Step 1: Move the four files**

```bash
git mv SpellWork/Spell/SpellInfo.cs SpellWork.Core/Spell/
git mv SpellWork/Spell/SpellEnums.cs SpellWork.Core/Spell/
git mv SpellWork/Spell/ExpectedStat.cs SpellWork.Core/Spell/
git mv SpellWork/DBC/DBC.cs SpellWork.Core/DBC/DBC.cs
```

Remove the three matching `<Compile Include="Spell\...">` lines and `<Compile Include="DBC\DBC.cs" />` from `SpellWork/SpellWork.csproj`.

`DBC.cs` is the move Task 3 deferred: it holds `SpellInfoStore` and constructs
`SpellInfo` objects, so it could not cross into Core until `SpellInfo` did.
It needs no edits here — Task 3 already replaced its `Settings.Default` reads
and its twenty `Console.WriteLine` calls. Run
`dotnet test SpellWork.Core.Tests -c Debug --filter Core_writes_nothing_to_stdout`
right after the move: it should still pass. A failure naming `DBC.cs` means
Task 3 missed a `Console.WriteLine`.

- [ ] **Step 2: Retarget the signatures**

In `SpellWork.Core/Spell/SpellInfo.cs`, delete `using System.Drawing;` and `using System.Windows.Forms;` from the header, then change these four signatures:

| Line | Was | Becomes |
| --- | --- | --- |
| 212 | `public void Write(RichTextBox rtb)` | `public void Write(ISpellInfoWriter rtb)` |
| 677 | `private void AppendEffectInfo(RichTextBox rtb, SpellEffectEntry effect)` | `private void AppendEffectInfo(ISpellInfoWriter rtb, SpellEffectEntry effect)` |
| 810 | `private static void AppendRadius(RichTextBox rtb, string name, uint radiusIndex)` | `private static void AppendRadius(ISpellInfoWriter rtb, string name, uint radiusIndex)` |
| 819 | `private static void AuraModTypeName(RichTextBox rtb, SpellEffectEntry effect)` | `private static void AuraModTypeName(ISpellInfoWriter rtb, SpellEffectEntry effect)` |
| 892 | `private void AppendItemInfo(RichTextBox rtb)` | `private void AppendItemInfo(ISpellInfoWriter rtb)` |

Keeping the parameter named `rtb` means the ~200 call sites in the body need no edit.

- [ ] **Step 3: Fix the colour and style call sites**

Six `Color.*` values and two `FontStyle.*` values are used. Apply these replacements throughout `SpellInfo.cs`:

| Was | Becomes |
| --- | --- |
| `Color.Black` | `TextColor.Black` |
| `Color.Blue` | `TextColor.Blue` |
| `Color.DarkBlue` | `TextColor.DarkBlue` |
| `Color.DarkRed` | `TextColor.DarkRed` |
| `Color.MediumPurple` | `TextColor.MediumPurple` |
| `Color.Red` | `TextColor.Red` |
| `FontStyle.Bold` | `TextStyle.Bold` |
| `FontStyle.Regular` | `TextStyle.Regular` |

The four bare `rtb.SelectionColor = X;` assignments become `rtb.SetColor(X);`.

- [ ] **Step 3b: Make numbers render the same on both runtimes**

Discovered during execution, and reworked after review — the account below is what
was actually built (commit `09dc109`), not the first attempt.

`spell-2661`'s golden reads `BasePoints = 0.88 to 1.13`, captured from the net48 GUI,
but the net9 test host renders `1.12`. `baseValue = 1`, `variance = 0.25`, so
`delta = 0.125` and the upper bound is an exact IEEE midpoint. Nothing in the port
causes it — but the whole point of Core is that the GUI and the MCP server decode
identically, so they must also print identically.

Pulling that thread found three defects, not one:

| | Defect |
| --- | --- |
| A | `SpellInfoWriterBase` called the `CurrentCulture` overload of `string.Format`, so Core emitted `0,88` on a `de-DE` host. The goldens passed here only because this machine is `de-CH`, which uses `.` as its decimal separator. |
| B | `"F"`: .NET Framework formats from a 7-significant-digit view and rounds midpoints away from zero; .NET Core 3.0+ formats the exact value and rounds half-to-even. |
| C | Bare `{0}`: Framework's default float `ToString` is 7 significant digits (15 for `double`); Core emits the shortest round-trippable string. `1f/3f` is `0.3333333` on net48 and `0.33333334` on net9. |

The first attempt added a `private static decimal F2(float)` helper to `SpellInfo` and
wrapped four `{0:F}` arguments with it. That was abandoned. It could not reach
`AppendRadius`, which formatted three floats by **string interpolation** — formatting
happens at the interpolation site, before the writer ever sees the value — and it
could not be applied at the two `*IfNotNull` sites at all, because
`IsValueConsideredNullForFormatting` suppresses when `|x| <= 1.0E-5M` and runs on
whatever it is handed: `Speed = 0.004` prints `Speed 0.00` today and would have
vanished if pre-rounded.

Fix it once, at the writer boundary, in `SpellWork.Core/Spell/FrameworkNumberFormat.cs`:
an `internal sealed class` implementing `IFormatProvider` and `ICustomFormatter`.
`GetFormat` returns `this` for `ICustomFormatter` and delegates everything else to
`CultureInfo.InvariantCulture`. `Format` intercepts `float` and `double` and returns
`null` for everything else, which hands the argument back to `string.Format`'s
`IFormattable` path — still with this provider, hence still invariant.

| Specifier | Rule |
| --- | --- |
| `"F"`, `"f"`, `"F0"`–`"F15"` | `Math.Round((decimal)value, digits, MidpointRounding.AwayFromZero)` — the `decimal` conversion rounds to 7 significant digits for `float` (15 for `double`), reproducing the Framework view, and `decimal` formatting is identical on both runtimes. Bare `"F"` means 2 digits. |
| null, `""`, `"G"`, `"g"` | `"G7"` for `float`, `"G15"` for `double` — Microsoft's documented recipe for pre-.NET-Core-3.0 formatting. |
| anything else | `value.ToString(format, CultureInfo.InvariantCulture)` — pin the culture, leave the semantics alone. |

Guard the `decimal` cast before it happens: `NaN`, infinity, `Math.Abs(value) >= 7.9e28`
and a digit count outside `0..15` all fall back to
`value.ToString(format, CultureInfo.InvariantCulture)`. The cast throws
`OverflowException` where the old code merely printed a number, and it now sits on the
unconditional per-effect rendering path — an unguarded case is a crash, not a cosmetic
bug.

Then wire it up: `AppendFormat` and `AppendFormatLine` pass
`FrameworkNumberFormat.Instance` to `string.Format`, and `Append(object)` becomes
`AppendFormat("{0}", text)` rather than `text.ToString()`. Because formatting now runs
*after* `IsValueConsideredNullForFormatting` has tested the raw argument, the two
`*IfNotNull` sites are covered for free — the carve-out dissolves instead of needing a
workaround. Leave `IsValueConsideredNullForFormatting` itself untouched.

Two sites in `SpellInfo` still need edits, because they bypass the writer:

- `AppendRadius` interpolates three floats. Rewrite as
  `rtb.AppendFormatLine("{0}Radius (Id {1}) {2:F} (Min: {3:F} Max: {4:F})", …)`,
  hoisting the thrice-repeated `SpellRadius[(int)radiusIndex]` lookup into a local.
  **The rendered text must not change** — same words, same spacing, same parentheses.
- Nothing else. The other interpolations (`216`, `253`, `687`, `689`, `923`) carry
  ints, enums and strings, which are culture-safe.

The `{0:F}` specifiers throughout `SpellInfo` stay exactly as they are.

- [ ] **Step 3c: Let the test host run under a real culture**

Do **not** set `<InvariantGlobalization>true</InvariantGlobalization>` on the test
project. The first attempt did, and it was wrong twice over: it made the golden suite
pass on `de-DE` while making it structurally incapable of noticing that Core was
culture-dependent, and in invariant mode `new CultureInfo("de-DE")` throws, so the
culture test below cannot even run.

The guarantee belongs in the library (Step 3b), and the test host must run under a
real culture for the tests to mean anything.

**Plan 2's MCP host therefore needs no culture pinning for rendering** — Core is
invariant by construction. Pinning `CultureInfo.InvariantCulture` at startup is still
cheap insurance for anything outside the writer path.

- [ ] **Step 4: Build Core**

Run: `dotnet build SpellWork.Core -c Debug`
Expected: succeeds. Remaining errors are missed `Color.`/`FontStyle.`/`SelectionColor` sites — the compiler names each one.

- [ ] **Step 5: Write the golden regression test**

Create `SpellWork.Core.Tests/GoldenRenderingTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    // Requires extracted client data. Set SPELLWORK_DBC_PATH and SPELLWORK_GT_PATH
    // to <emulator-build-dir>\ClientData\dbc and ...\ClientData\gt.
    public class GoldenRenderingTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public GoldenRenderingTests(ClientDataFixture fixture) => _fixture = fixture;

        public static IEnumerable<object[]> GoldenFiles()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Goldens");
            if (!Directory.Exists(dir))
                yield break;

            foreach (var file in Directory.EnumerateFiles(dir, "spell-*.txt").OrderBy(f => f))
                yield return new object[] { Path.GetFileNameWithoutExtension(file) };
        }

        [SkippableTheory]
        [MemberData(nameof(GoldenFiles))]
        public void Rendering_matches_the_pre_refactor_golden(string goldenName)
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var id = int.Parse(goldenName.Substring("spell-".Length));
            Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(id),
                $"spell {id} is missing from this client data");

            var writer = new PlainTextSpellInfoWriter();
            DBC.DBC.SpellInfoStore[id].Write(writer);

            var expected = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Goldens", goldenName + ".txt"));

            Assert.Equal(Normalise(expected), Normalise(writer.ToString()));
        }

        // RichTextBox.Text yields \n; StringBuilder + Environment.NewLine yields \r\n.
        // Without this every golden fails for a reason unrelated to the refactor.
        private static string Normalise(string text) => text.Replace("\r\n", "\n");
    }
}
```

Create `SpellWork.Core.Tests/ClientDataFixture.cs`:

```csharp
using System;

namespace SpellWork.Core.Tests
{
    public sealed class ClientDataFixture
    {
        public const string SkipReason =
            "Set SPELLWORK_DBC_PATH and SPELLWORK_GT_PATH to the extracted ClientData directories.";

        public bool Loaded { get; }

        public ClientDataFixture()
        {
            var dbcPath = Environment.GetEnvironmentVariable("SPELLWORK_DBC_PATH");
            var gtPath = Environment.GetEnvironmentVariable("SPELLWORK_GT_PATH");

            if (string.IsNullOrEmpty(dbcPath) || string.IsNullOrEmpty(gtPath))
                return;

            var config = new SpellWorkConfig
            {
                DbcPath = dbcPath,
                GtPath = gtPath,
                Locale = Environment.GetEnvironmentVariable("SPELLWORK_LOCALE") ?? "enUS"
            };

            DBC.DBC.LoadAsync(config, NullSpellWorkLogger.Instance).GetAwaiter().GetResult();
            Loaded = true;
        }
    }
}
```

Add the skip package to `SpellWork.Core.Tests.csproj`:

```xml
    <PackageReference Include="Xunit.SkippableFact" Version="1.4.13" />
```

- [ ] **Step 6: Run the goldens**

```bash
set SPELLWORK_DBC_PATH=<emulator-build-dir>\ClientData\dbc
set SPELLWORK_GT_PATH=<emulator-build-dir>\ClientData\gt
dotnet test SpellWork.Core.Tests -c Debug --filter GoldenRenderingTests
```

Expected: PASS for every golden. A failure here is the whole point of the task — the diff shows exactly which line the extraction changed. Fix `SpellInfo.cs` until they match; never edit a golden to make a test pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: render SpellInfo through ISpellInfoWriter

Moves SpellInfo, SpellEnums and ExpectedStat into SpellWork.Core and
retargets the five rendering methods from RichTextBox to ISpellInfoWriter.
The parameter keeps its rtb name so the ~200 call sites in the body are
untouched; only the six Color values, two FontStyle values and four
SelectionColor assignments changed.

GoldenRenderingTests asserts the output still matches the rendering
captured before any of this moved. Goldens are compared with line endings
normalised, because RichTextBox.Text yields \\n while StringBuilder with
Environment.NewLine yields \\r\\n."
```

---

### Task 6: Rewire the GUI through RichTextBoxSpellInfoWriter

**Files:**
- Create: `SpellWork/Extensions/RichTextBoxSpellInfoWriter.cs`
- Modify: `SpellWork/Forms/FormMain.cs:164,353,620`
- Modify: `SpellWork/Forms/FormSearch.cs:98`
- Modify: `SpellWork/Spell/SpellCompare.cs:26,27`
- Modify: `SpellWork/Program.cs` (remove the temporary dump mode)
- Modify: `SpellWork/SpellWork.csproj`

**Interfaces:**
- Consumes: `SpellInfoWriterBase`, `TextColor`, `TextStyle` from Task 4; `SpellInfo.Write(ISpellInfoWriter)` from Task 5.
- Produces: `RichTextBoxSpellInfoWriter(RichTextBox target)`.

- [ ] **Step 1: Write the GUI writer**

Create `SpellWork/Extensions/RichTextBoxSpellInfoWriter.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;
using SpellWork.Spell;

namespace SpellWork.Extensions
{
    public sealed class RichTextBoxSpellInfoWriter : SpellInfoWriterBase
    {
        private readonly RichTextBox _target;

        public RichTextBoxSpellInfoWriter(RichTextBox target) => _target = target;

        public override void Clear() => _target.Clear();

        public override void AppendText(string text) => _target.AppendText(text);

        public override void SetColor(TextColor color) => _target.SelectionColor = Resolve(color);

        public override void SetStyle(TextColor color, TextStyle style)
            => _target.SetStyle(Resolve(color), Resolve(style));

        public override void SetBold() => _target.SetBold();

        public override void SetDefaultStyle() => _target.SetDefaultStyle();

        private static Color Resolve(TextColor color)
        {
            switch (color)
            {
                case TextColor.Blue:         return Color.Blue;
                case TextColor.DarkBlue:     return Color.DarkBlue;
                case TextColor.DarkRed:      return Color.DarkRed;
                case TextColor.MediumPurple: return Color.MediumPurple;
                case TextColor.Red:          return Color.Red;
                default:                     return Color.Black;
            }
        }

        private static FontStyle Resolve(TextStyle style)
            => style == TextStyle.Bold ? FontStyle.Bold : FontStyle.Regular;
    }
}
```

Add `<Compile Include="Extensions\RichTextBoxSpellInfoWriter.cs" />` to `SpellWork/SpellWork.csproj`.

- [ ] **Step 2: Update the four call sites**

| File:line | Was | Becomes |
| --- | --- | --- |
| `Forms/FormMain.cs:164` | `_spellList[_lvSpellList.SelectedIndices[0]].Write(_rtSpellInfo);` | `_spellList[_lvSpellList.SelectedIndices[0]].Write(new RichTextBoxSpellInfoWriter(_rtSpellInfo));` |
| `Forms/FormMain.cs:353` | `spell.Write(_rtbProcSpellInfo);` | `spell.Write(new RichTextBoxSpellInfoWriter(_rtbProcSpellInfo));` |
| `Forms/FormMain.cs:620` | `spell.Write(_rtbProcSpellInfo);` | `spell.Write(new RichTextBoxSpellInfoWriter(_rtbProcSpellInfo));` |
| `Forms/FormSearch.cs:98` | `_spellList[_lvSpellList.SelectedIndices[0]].Write(_rtbSpellInfo);` | `_spellList[_lvSpellList.SelectedIndices[0]].Write(new RichTextBoxSpellInfoWriter(_rtbSpellInfo));` |
| `Spell/SpellCompare.cs:26` | `spell1.Write(rtb1);` | `spell1.Write(new RichTextBoxSpellInfoWriter(rtb1));` |
| `Spell/SpellCompare.cs:27` | `spell2.Write(rtb2);` | `spell2.Write(new RichTextBoxSpellInfoWriter(rtb2));` |

`SpellCompare.Compare` keeps its two `RichTextBox` parameters — it reads `rtb1.Text`,
calls `rtb2.Find` and sets `SelectionBackColor`, so it is genuinely a UI method. Only
the two `Write` calls change. These two sites were missed when Task 6 was first
written; without them the GUI still does not compile after Task 6.

Add `using SpellWork.Extensions;` to both files if not already present.

- [ ] **Step 3: Build, and hand the visual check back**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug`
Expected: build succeeds.

**Operator step — do not attempt this yourself.** Someone with eyes on the screen must launch `SpellWork\bin\Debug\SpellWork.exe`, select Fireball (133), and confirm the header is bold, "Triggered by spell" lines are blue and bold, and effect blocks are coloured as before. This is the one check the goldens cannot make: they compare text, not styling, so a writer that dropped every colour call would still pass Step 4.

Record in your report that this step is outstanding. Do not mark the task done on your own authority as though it passed.

- [ ] **Step 4: Re-capture goldens through the GUI path and compare**

Before removing the dump mode, run it once more — this is now the *GUI* writer's output, since Step 2 rewired the call sites:

```bash
SpellWork/bin/Debug/SpellWork.exe --dump-goldens /tmp/goldens-gui \
  "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\dbc" \
  "F:\WorkDir\BFA-HavenCore-Corruption\build\bin\Debug\ClientData\gt"

diff -r /tmp/goldens-gui SpellWork.Core.Tests/Goldens
```

Expected: `diff` reports nothing. This proves the GUI writer and the plain-text writer produce the same text, so the two paths have not diverged.

Note the dump harness renders through `RichTextBoxSpellInfoWriter` only if you also updated its `DumpGoldens` call from `Write(rtb)` to `Write(new RichTextBoxSpellInfoWriter(rtb))`. Do that — otherwise `SpellInfo.Write` no longer accepts a `RichTextBox` and `Program.cs` will not compile.

- [ ] **Step 5: Remove the temporary dump mode**

Delete `RunGoldenDump`, `DumpGoldens` and `FirstIdWhere` from `SpellWork/Program.cs`, the `if (args.Length == 4 && args[0] == "--dump-goldens")` block, and the now-unused `System.Collections.Generic`, `System.Linq` and `System.Text` usings. **Keep `BuildConfig`** — `Main` still calls it. Keep `static void Main(string[] args)`; it costs nothing and the MCP work may want it.

- [ ] **Step 6: Build and run the full suite**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug && dotnet test SpellWork.Core.Tests -c Debug`
Expected: build succeeds, all tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: render the GUI through RichTextBoxSpellInfoWriter

Supplies the WinForms implementation of ISpellInfoWriter, delegating to the
existing RichTextBoxExtensions so styling is unchanged, and updates the four
Write call sites in FormMain and FormSearch.

Verified by re-running the golden dump through the GUI writer and diffing
against the goldens captured before the extraction: byte-identical, so the
GUI and headless paths render the same text. Styling was checked by eye,
which the text goldens cannot cover.

Removes the temporary --dump-goldens harness."
```

---

### Task 7: Split ProcInfo from TreeView

**Files:**
- Move: `SpellWork/Spell/ProcInfo.cs` → `SpellWork.Core/Spell/ProcInfo.cs`, then modify
- Create: `SpellWork/Extensions/ProcTreeViewAdapter.cs`
- Modify: `SpellWork/Forms/FormMain.cs` (the `ProcInfo.Fill` call site)
- Modify: `SpellWork/SpellWork.csproj`

**Interfaces:**
- Consumes: `SpellFamilyNames` from `SpellEnums.cs` (Task 5).
- Produces:
  - `enum ProcTreeNodeKind { FamilyMask, Skill, Spell }`
  - `class ProcTreeNode { string Key; string Text; string ToolTip; ProcTreeNodeKind Kind; List<ProcTreeNode> Children; }`
  - `static List<ProcTreeNode> ProcInfo.BuildFamilyTree(SpellFamilyNames family)`
  - `static void ProcTreeViewAdapter.Populate(TreeView target, List<ProcTreeNode> nodes)`

- [ ] **Step 1: Read the current implementation**

Read `SpellWork/Spell/ProcInfo.cs` in full before changing anything. It is shorter
and stranger than it looks, and three details drive the whole design:

1. **`Fill` builds two different kinds of node.** It first adds **128** top-level
   nodes, one per bit of the 128-bit spell-class mask, each with
   `Text = "0x{mask[3]:X8} {mask[2]:X8} {mask[1]:X8} {mask[0]:X8}"` and
   `ImageKey = "family.ico"` — and **no `ForeColor`**, so they render in the
   default colour. It then walks the family's spells and adds each one as a
   *child* of every mask node whose bit that spell's `SpellClassMask` contains.
   Only those children get `Name`, `ImageKey` (`plus.ico`/`munus.ico`),
   `ForeColor` (blue/red) and `ToolTipText`.
2. **A spell node is attached under more than one parent** when its mask has
   several bits set. The same spell legitimately appears many times in the tree.
3. **The inner loop keys off `node.Index`** to recompute each parent's mask.
   There is no `Index` on a plain list element, so use the node's position in
   the list — the 128 nodes are added in bit order, so position equals the old
   `Index` exactly. Keep them in that order or the masks stop matching.

The traversal — the LINQ join, the 128-bit mask walk and the `ContainsElement`
test — is what belongs in Core. The colours and icons are presentation and stay
in the GUI. `ToolTipText` is neither: it is *content* the traversal computes, so
the model carries it and the adapter merely hands it to WinForms.

`ProcInfo.SpellProc` and `ProcInfo.Update` move to Core with the class and need
no changes. `FormMain` reads them at seven places; leave every one alone.

- [ ] **Step 2: Write the failing test**

Create `SpellWork.Core.Tests/ProcInfoTests.cs`:

```csharp
using System.Linq;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class ProcInfoTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public ProcInfoTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void BuildFamilyTree_returns_one_node_per_mask_bit()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var nodes = ProcInfo.BuildFamilyTree(SpellFamilyNames.SPELLFAMILY_MAGE);

            Assert.Equal(128, nodes.Count);
            Assert.All(nodes, n => Assert.Equal(ProcTreeNodeKind.FamilyMask, n.Kind));
            Assert.Equal("0x00000000 00000000 00000000 00000001", nodes[0].Text);
            Assert.Equal("0x80000000 00000000 00000000 00000000", nodes[127].Text);
        }

        [SkippableFact]
        public void BuildFamilyTree_attaches_the_familys_spells_to_their_mask_bits()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var nodes = ProcInfo.BuildFamilyTree(SpellFamilyNames.SPELLFAMILY_MAGE);
            var spells = nodes.SelectMany(n => n.Children).ToList();

            // The 128 mask nodes exist whether or not the join found anything,
            // so asserting on `nodes` alone would pass against an empty family.
            // These assertions fail if the traversal stops finding spells.
            Assert.NotEmpty(spells);

            Assert.All(spells, s =>
            {
                Assert.False(string.IsNullOrEmpty(s.Text));
                Assert.False(string.IsNullOrEmpty(s.ToolTip));
                Assert.True(DBC.DBC.SpellInfoStore.ContainsKey(int.Parse(s.Key)),
                    $"child node Key '{s.Key}' is not a spell id");
                Assert.True(s.Kind == ProcTreeNodeKind.Skill || s.Kind == ProcTreeNodeKind.Spell);
            });

            // Every spell in the family carries SPELLFAMILY_MAGE by construction.
            Assert.All(spells, s => Assert.Equal((uint)SpellFamilyNames.SPELLFAMILY_MAGE,
                DBC.DBC.SpellInfoStore[int.Parse(s.Key)].SpellFamilyName));
        }
    }
}
```

The first test pins the mask-bit layout, which is the part most likely to break
silently under a rewrite: bit 0 must land in `mask[0]` and bit 127 in `mask[3]`,
and the string interpolates them high word first.

The second test exists because the obvious assertion is worthless here. 128 mask
nodes are added unconditionally, before the spell join runs, so
`Assert.NotEmpty(nodes)` and "every node has text" both pass even if the
traversal finds no spells at all. Assert on the *children*.

- [ ] **Step 3: Run and verify failure**

Run: `dotnet test SpellWork.Core.Tests -c Debug --filter ProcInfoTests`
Expected: FAIL — `BuildFamilyTree` does not exist.

- [ ] **Step 4: Move and split**

```bash
git mv SpellWork/Spell/ProcInfo.cs SpellWork.Core/Spell/ProcInfo.cs
```

Remove `<Compile Include="Spell\ProcInfo.cs">` from `SpellWork/SpellWork.csproj`.

In `SpellWork.Core/Spell/ProcInfo.cs`, delete the `System.Drawing` and `System.Windows.Forms` usings, add `System.Collections.Generic`, and add:

```csharp
    public enum ProcTreeNodeKind
    {
        FamilyMask,
        Skill,
        Spell
    }

    public class ProcTreeNode
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public string ToolTip { get; set; }
        public ProcTreeNodeKind Kind { get; set; }
        public List<ProcTreeNode> Children { get; } = new List<ProcTreeNode>();
    }
```

`Kind` rather than the more obvious `bool IsSkill`, because the tree holds three
kinds of node and a boolean can only describe two. The 128 mask nodes are neither
skill nor non-skill: the original sets no `ForeColor` on them at all, so under an
`IsSkill` model the adapter would paint all 128 of them red — a visible
regression the goldens cannot catch, since they render spell text and never touch
this tree.

Replace `public static void Fill(TreeView familyTree, SpellFamilyNames spellfamily)`
with `public static List<ProcTreeNode> BuildFamilyTree(SpellFamilyNames spellfamily)`.
Keep the LINQ join and the mask arithmetic exactly as they are. The mechanical
substitutions:

| Original | Becomes |
| --- | --- |
| `familyTree.Nodes.Clear()` | drop it — you are returning a fresh list |
| `new TreeNode { Text = …, ImageKey = "family.ico" }` then `familyTree.Nodes.Add(node)` | `new ProcTreeNode { Text = …, Kind = ProcTreeNodeKind.FamilyMask }`, added to the list |
| `foreach (TreeNode node in familyTree.Nodes)` … `node.Index` | iterate the list by position (`for (var index = 0; index < nodes.Count; ++index)`) and use `index` where `node.Index` was |
| `var child = node.Nodes.Add(name.ToString())` | `var child = new ProcTreeNode { Text = name.ToString() }`, added to `nodes[index].Children` |
| `child.Name = spell.ID.ToString()` | `child.Key = spell.ID.ToString()` |
| `child.ImageKey = isSkill ? "plus.ico" : "munus.ico"` | drop — the adapter derives it from `Kind` |
| `child.ForeColor = isSkill ? Color.Blue : Color.Red` | `child.Kind = isSkill ? ProcTreeNodeKind.Skill : ProcTreeNodeKind.Spell` |
| `child.ToolTipText = toolTip.ToString()` | `child.ToolTip = toolTip.ToString()` |

Return the list. Note the same spell is attached under **every** mask node whose
bit it matches — that is existing behaviour, not a bug, so do not add a
"already added" guard.

`ProcInfo` also uses `AppendFormatLine`, `NormalizeString` and `ContainsElement`
from `SpellWork.Extensions`. All three already live in `SpellWork.Core/Extensions/`,
so the `using SpellWork.Extensions;` line stays and resolves.

- [ ] **Step 5: Write the GUI adapter**

Create `SpellWork/Extensions/ProcTreeViewAdapter.cs`:

```csharp
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SpellWork.Spell;

namespace SpellWork.Extensions
{
    public static class ProcTreeViewAdapter
    {
        public static void Populate(TreeView target, List<ProcTreeNode> nodes)
        {
            target.Nodes.Clear();
            foreach (var node in nodes)
                target.Nodes.Add(Convert(node));
        }

        private static TreeNode Convert(ProcTreeNode source)
        {
            var node = new TreeNode(source.Text)
            {
                Name = source.Key ?? string.Empty,
                ToolTipText = source.ToolTip ?? string.Empty
            };

            // The three kinds carry the presentation the headless model
            // deliberately does not: a mask node keeps the TreeView's default
            // fore colour, which is why this is a switch and not a ternary on
            // a boolean.
            switch (source.Kind)
            {
                case ProcTreeNodeKind.Skill:
                    node.ImageKey = "plus.ico";
                    node.ForeColor = Color.Blue;
                    break;
                case ProcTreeNodeKind.Spell:
                    node.ImageKey = "munus.ico";
                    node.ForeColor = Color.Red;
                    break;
                default:
                    node.ImageKey = @"family.ico";
                    break;
            }

            foreach (var child in source.Children)
                node.Nodes.Add(Convert(child));

            return node;
        }
    }
}
```

Add `<Compile Include="Extensions\ProcTreeViewAdapter.cs" />` to `SpellWork/SpellWork.csproj`.

- [ ] **Step 6: Update the call site**

`SpellWork/Forms/FormMain.cs:348` reads `ProcInfo.Fill(_tvFamilyTree, spellfamily);`. Replace it with:

```csharp
            ProcTreeViewAdapter.Populate(_tvFamilyTree, ProcInfo.BuildFamilyTree(spellfamily));
```

- [ ] **Step 7: Run tests and check the GUI**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug && dotnet test SpellWork.Core.Tests -c Debug`
Expected: all pass, including the hygiene tests now that `ProcInfo.cs` is in Core.

Launch the GUI, open the Proc Info tab, pick a spell family, and confirm the tree populates with blue skill entries and red non-skill entries as before.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor: split ProcInfo traversal from TreeView population

ProcInfo.Fill both walked a spell family and built WinForms TreeNodes.
The traversal moves to Core as BuildFamilyTree returning ProcTreeNode, and
ProcTreeViewAdapter in the GUI converts that model into TreeNodes, keeping
the blue-for-skill and red-for-other colouring."
```

---

### Task 8: Headless world-database client

**Files:**
- Move: `SpellWork/Database/{MySQLConnect,Structures}.cs` → `SpellWork.Core/Database/`, then modify
- Modify: `SpellWork/Forms/FormMain.cs` (takes the config, holds the instance, error handling)
- Modify: `SpellWork/Forms/FormSettings.cs:33,36` — **also a call site**, see Step 3
- Modify: `SpellWork/Program.cs` — `BuildConfig` must become reachable and the config passed to `FormMain`
- Modify: `SpellWork/SpellWork.csproj`, `SpellWork.Core/SpellWork.Core.csproj`

**Interfaces:**
- Consumes: `SpellWorkConfig` from Task 3.
- Produces:
  ```csharp
  class WorldDatabase
  {
      WorldDatabase(SpellWorkConfig config);
      bool Connected { get; private set; }         // set by TestConnection
      List<string> Dropped { get; }                // instance, was static
      bool TestConnection(out string error);
      List<SpellProcEventEntry> SelectProc(string query);
      void Insert(string query);
  }
  ```
  No longer static, no `MessageBox`.

`Connected` is not optional. `TestConnection` returns a bool for the caller that
runs it, but the GUI reads a *persistent* connected state at five later points
(`FormMain.cs:99,116,502,555,608`) that are nowhere near the test. Drop the
property and the GUI stops compiling.

`Structures.cs` needs no changes at all beyond the move: it has no UI reference
and already formats its two floats with `CultureInfo.InvariantCulture`
(`Structures.cs:31-32`), so it carries none of the culture problems Task 5 dealt
with.

- [ ] **Step 1: Move the files and add the package**

```bash
git mv SpellWork/Database/MySQLConnect.cs SpellWork.Core/Database/WorldDatabase.cs
git mv SpellWork/Database/Structures.cs SpellWork.Core/Database/
```

Remove both `<Compile Include="Database\...">` entries from `SpellWork/SpellWork.csproj`, and delete the `MySql.Data` `<PackageReference>` there (lines 260-262). Add to `SpellWork.Core/SpellWork.Core.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="MySql.Data" Version="8.0.33" />
  </ItemGroup>
```

**The version bump is required, not opportunistic.** The GUI pins `MySql.Data` 6.9.9, whose package contains only `lib/net40` and `lib/net45` — there is no netstandard asset, so referencing it from a `netstandard2.0` project fails restore with NU1202 before a single line compiles. I downloaded the 8.0.33 package and listed it to confirm: it ships `lib/netstandard2.0/MySql.Data.dll` *and* `lib/net48/MySql.Data.dll` (alongside net462, netstandard2.1, net6.0, net7.0), so one reference serves Core and, transitively, the GUI. Take that as verified; do not spend a step re-checking it.

8.0.33 keeps the `MySql.Data.MySqlClient` namespace and `MySqlException.Number`, so the code in this task is unaffected. It does pull real dependencies (BouncyCastle, Google.Protobuf, K4os.Compression.LZ4, ZstdSharp, System.Configuration.ConfigurationManager) where 6.9.9 had none. Those reach the net48 GUI transitively and are the reason `AutoGenerateBindingRedirects` was flipped to `true` in Task 2 — if the GUI throws `FileLoadException` on first connect, that setting reverted.

If restore or the GUI misbehaves under 8.0.33, the fallback is `MySqlConnector` (MIT, netstandard2.0, no transitive graph). It is a separate namespace, so taking it means rewriting the `using` and the fully-qualified type names in this file — more work, cleaner result. Do not attempt it unless 8.0.33 actually fails.

- [ ] **Step 2: Convert to an instance class with no UI**

In `SpellWork.Core/Database/WorldDatabase.cs`: rename the class from `MySqlConnection` to `WorldDatabase` (the old name collided with the driver's own type, which is why the file is littered with fully-qualified `MySql.Data.MySqlClient.MySqlConnection`), delete the `System.Windows.Forms` and `SpellWork.Properties` usings, and make it non-static holding a `SpellWorkConfig` field set by the constructor. Replace each `Settings.Default.X` with `_config.X` per the mapping in Task 3.

Replace `TestConnect()` with:

```csharp
        public bool TestConnection(out string error)
        {
            error = null;

            if (!_config.UseDatabase)
                return false;

            try
            {
                using (var connection = new MySql.Data.MySqlClient.MySqlConnection(ConnectionString))
                {
                    connection.Open();
                }

                return true;
            }
            catch (MySqlException ex)
            {
                error = $"Errno {ex.Number}{Environment.NewLine}{ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
```

`SelectProc` returns its `List<SpellProcEventEntry>` instead of mutating a static field. `GetSpellName` keeps appending to `Dropped`, which stays an instance field.

- [ ] **Step 3: Update the GUI call sites**

There are **two** forms to update, not one, and the config has to be plumbed to
both. `Program.Main` currently calls `BuildConfig()` inline and does
`Application.Run(new FormMain())`, so nothing downstream can see the config.

**`Program.cs`:** hoist the config into a local and hand it to the form. Change
`BuildConfig` from `private static` to `internal static` — `FormSettings` needs
it too, for the reason below.

```csharp
            var config = BuildConfig();
            DBC.DBC.LoadAsync(config, NullSpellWorkLogger.Instance).GetAwaiter().GetResult();
            Application.Run(new FormMain(config));
```

**`FormMain.cs`:** take a `SpellWorkConfig` constructor parameter, construct one
`WorldDatabase` from it into a field, and replace each static call. Where
`TestConnect()` was called (lines 97, 114), show the `MessageBox` from the
returned `error`:

```csharp
            if (!_database.TestConnection(out var error) && error != null)
                MessageBox.Show(error);
```

`Connected` (99, 116, 502, 555, 608), `Insert` (503, 609) and `Dropped` (591)
become reads on `_database`.

`SelectProc`'s result needs a **field on `FormMain`**, not a local. The old code
called `SelectProc` at line 579 and then read the static `SpellProcEvent` from
three places in *different event handlers* — `_lvDataList.VirtualListSize` at
586, the selection handler at 616, and `RetrieveVirtualItem` at 661. A local
cannot span those. Add a `List<SpellProcEventEntry>` field, initialised empty so
the virtual-list callbacks are safe before the first query, and assign it inside
the existing `try` at 577-581. Leaving the previous results in place when the
query throws matches what the old static did when `Open()` failed.

**`FormSettings.cs`:** lines 33 and 36 also call `TestConnect()` and read
`Connected`, and the brief's original file list missed them. This form is a
special case: it writes the user's edits into `Settings.Default` (lines 19-31)
and *then* tests, so it must test the **new** settings, not the config captured
at startup. Build a fresh one and use a throwaway instance:

```csharp
            var connected = new WorldDatabase(Program.BuildConfig()).TestConnection(out var error);

            if (((Button)sender).Text != @"Save")
            {
                if (connected)
                    MessageBox.Show(@"Connection successful!", @"MySQL Connections!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else if (error != null)
                    MessageBox.Show(error);
            }
```

This form needs no `Connected` property and no stored instance — it tests once
and then `Application.Restart()`s. Note it gains an error `MessageBox` it did not
have before: `TestConnect` used to raise one from inside the database class, and
moving that out is the whole point of the task, so the GUI has to raise it here
or the user gets silence on a failed connection.

- [ ] **Step 4: Build and run the full suite**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug && dotnet test SpellWork.Core.Tests -c Debug`
Expected: build succeeds, all tests pass, hygiene tests confirm Core has no UI references.

Launch the GUI with a MySQL connection configured and confirm the proc tab still loads `spell_proc_event` rows. If you have no MySQL to hand, confirm instead that the GUI starts cleanly with `UseDbConnect` unticked.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: make the world-database client headless

Moves MySQLConnect into Core as WorldDatabase, converting it from a static
class that raised MessageBoxes and mutated static state into an instance
taking SpellWorkConfig and returning results. Connection failures now come
back as an out string that the GUI renders, so Core needs no UI reference.

Renaming off MySqlConnection also removes the collision with the driver's
own type that forced every usage to be fully qualified.

Bumps MySql.Data 6.9.9 -> 8.0.33 because it had to: 6.9.9 ships only
lib/net40 and lib/net45, so a netstandard2.0 project cannot reference it at
all. The namespace and the exception surface are unchanged."
```

---

### Task 9: SpellInfoJsonSerializer

Per spec §5.1.1, JSON is produced from `SpellInfo`'s properties, not through `ISpellInfoWriter`.

**Files:**
- Create: `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs`
- Create: `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`
- Modify: `SpellWork.Core/SpellWork.Core.csproj`
- Modify: `SpellWork/SpellWork.csproj` — the `System.Runtime.CompilerServices.Unsafe`
  pin, see Step 1
- Modify: `SpellWork.Core/Spell/SpellInfo.cs` — `CalculateBaseEffectValue` must
  become reachable, see Step 4

**Interfaces:**
- Consumes: `SpellInfo` from Task 5.
- Produces: `static string SpellInfoJsonSerializer.Serialize(SpellInfo spell, IReadOnlyCollection<string> sections = null)` returning a JSON object. `sections` filters to `misc`, `attributes`, `effects`, `powers`, `targets`, `reagents`, `aura`, `procs`, `scaling`; null means all. The MCP plan's `get_spell` calls this directly.

- [ ] **Step 1: Add the JSON dependency and fix the Unsafe pin**

Add to `SpellWork.Core/SpellWork.Core.csproj`:

```xml
    <PackageReference Include="System.Text.Json" Version="8.0.5" />
```

`System.Text.Json` is not built into netstandard2.0 and must be referenced
explicitly.

**In the same step, change `SpellWork/SpellWork.csproj:200` from
`System.Runtime.CompilerServices.Unsafe` 4.5.3 to 6.0.0.** This is not
housekeeping — it is required, and it also retires a defect Task 8 left behind.

Restore already emits `NU1605` on the committed branch today:

```
warning NU1605: Detected package downgrade: System.Runtime.CompilerServices.Unsafe from 6.0.0 to 4.5.3
  SpellWork -> SpellWork.Core -> MySql.Data 8.0.33 -> System.Runtime.CompilerServices.Unsafe (>= 6.0.0)
  SpellWork -> System.Runtime.CompilerServices.Unsafe (>= 4.5.3)
```

Task 8's MySql.Data 6.9.9 → 8.0.33 bump introduced it. It is a *warning* rather
than an error in this legacy project, which is why it slipped through, but it is
printed on every restore and the generated binding redirect caps
`System.Runtime.CompilerServices.Unsafe` at `4.0.4.1` — below what the graph
asks for. Task 8 recorded it as deferred on the grounds that the assembly never
actually loads on the classic MySQL protocol path. `System.Text.Json` ends that:
it depends on `Unsafe >= 6.0.0` and uses it on every serialisation, so from this
task onward the assembly does load.

I verified the whole thing end to end before writing this. With both changes
applied: `NU1605` disappears, the solution builds, `SpellWork.exe` is produced,
and `SpellWork.exe.config` gains `newVersion="6.0.0.0"` where it previously had
`4.0.4.1`. Take that as established; do not spend a step re-deriving it. If you
see `NU1605` after Step 1, you changed one file and not the other.

- [ ] **Step 2: Write the failing tests**

Create `SpellWork.Core.Tests/SpellInfoJsonSerializerTests.cs`:

```csharp
using System.Linq;
using System.Text.Json;
using SpellWork.Spell;
using Xunit;

namespace SpellWork.Core.Tests
{
    public class SpellInfoJsonSerializerTests : IClassFixture<ClientDataFixture>
    {
        private readonly ClientDataFixture _fixture;

        public SpellInfoJsonSerializerTests(ClientDataFixture fixture) => _fixture = fixture;

        [SkippableFact]
        public void Serialize_emits_id_name_and_decoded_attributes()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[133]);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(133, root.GetProperty("id").GetInt32());
            Assert.Equal("Fireball", root.GetProperty("name").GetString());
            Assert.True(root.TryGetProperty("effects", out var effects));
            Assert.True(effects.GetArrayLength() > 0);
        }

        [SkippableFact]
        public void Serialize_pairs_raw_flag_values_with_decoded_names()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(DBC.DBC.SpellInfoStore[133]);
            using var document = JsonDocument.Parse(json);

            var attributes = document.RootElement.GetProperty("attributes")
                .EnumerateArray().ToList();

            // Fireball sets exactly four attribute bits, one in each of four
            // different groups. Pinning the count and the decoded names is what
            // makes this test able to fail: a foreach over the array asserts
            // nothing at all if the serialiser emits an empty one, and
            // "the property exists" cannot catch a wrong bit-to-name mapping.
            Assert.Equal(4, attributes.Count);

            Assert.All(attributes, a =>
            {
                Assert.True(a.TryGetProperty("raw", out _));
                Assert.True(a.TryGetProperty("name", out _));
            });

            var names = attributes.Select(a => a.GetProperty("name").GetString()).ToList();
            Assert.Contains("SPELL_ATTR0_NOT_SHAPESHIFT", names);
            Assert.Contains("SPELL_ATTR4_AREA_TARGET_CHAIN", names);
            Assert.Contains("SPELL_ATTR6_CANT_TARGET_CROWD_CONTROLLED", names);

            // SpellAtributeEx13 is declared `internal`, so this one also proves
            // the serialiser reaches the non-public attribute enums.
            Assert.Contains("SPELL_ATTR13_UNK0", names);
        }

        [SkippableFact]
        public void Serialize_honours_the_sections_filter()
        {
            Skip.IfNot(_fixture.Loaded, ClientDataFixture.SkipReason);

            var json = SpellInfoJsonSerializer.Serialize(
                DBC.DBC.SpellInfoStore[133], new[] { "misc" });

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Both halves are needed. Asserting only that `effects` is gone
            // passes for a serialiser that returns {"id":…,"name":…} and drops
            // everything else for every filter.
            Assert.False(root.TryGetProperty("effects", out _));
            Assert.False(root.TryGetProperty("attributes", out _));
            Assert.True(root.TryGetProperty("misc", out _));
            Assert.Equal(133, root.GetProperty("id").GetInt32());
            Assert.Equal("Fireball", root.GetProperty("name").GetString());
        }
    }
}
```

The four attribute values come from the committed golden
`SpellWork.Core.Tests/Goldens/spell-133.txt:63-66`, which the text path already
renders as `Attributes: 0x00010000 (SPELL_ATTR0_NOT_SHAPESHIFT)` and so on — so
the two formats are pinned to the same data without the JSON test reading the
golden or depending on its formatting.

- [ ] **Step 3: Run and verify failure**

Run: `dotnet test SpellWork.Core.Tests -c Debug --filter SpellInfoJsonSerializerTests`
Expected: FAIL — `SpellInfoJsonSerializer` does not exist.

- [ ] **Step 4: Implement the serializer**

Create `SpellWork.Core/Spell/SpellInfoJsonSerializer.cs`. Build a `Dictionary<string, object>` and serialise it with `JsonSerializer.Serialize`. Required behaviour:

- `id`, `name`, `description`, `tooltip` always present.
- `attributes`: one entry per set bit across `Attributes` through
  `AttributesEx13`, each
  `{ "group": <n>, "raw": <uint>, "name": "<enum member name>" }`. Unset flags
  are omitted.
- `effects`: one object per non-null entry in `Effects`, carrying `index`,
  `difficultyId`, `effect` (raw plus `SpellEffects` name), `auraType` (raw plus
  `AuraType` name) when non-zero, `basePoints`, `targetA` and `targetB` (raw
  plus `Targets` name), `radius`, `maxRadius`, `chainTargets`.
- `powers`, `targets`, `reagents`, `aura`, `procs`, `scaling`: emitted from the
  matching `SpellInfo` properties, each omitted entirely when empty or null —
  see the mapping below, the property names are not what you would guess.
- Any property whose value is zero, null or empty string is omitted, matching the spec's "zero and empty fields omitted".
- `sections` filters top-level keys; `id` and `name` are always emitted regardless.

Use `new JsonSerializerOptions { WriteIndented = false }`.

Six details you cannot get from the property names alone. I checked each
against the code; none of them are guesses.

**a. There is no single attribute enum.** `SpellEnums.cs` declares fourteen —
`SpellAtribute`, `SpellAtributeEx`, `SpellAtributeEx2` … `SpellAtributeEx13`
(singular "Atribute", one `t`, at `SpellEnums.cs:1522-2039`). So the mapping is
a 14-way dispatch from group index to enum type, not one `Enum.GetName` call.
`SpellAtributeEx11`, `Ex12` and `Ex13` are declared **`internal`**, which is
fine inside Core but means they must not appear in any public signature.

Emit **one entry per set bit**, as stated above. That deliberately differs from
the text path, which renders the whole group value through a single cast and
shows a combined name when several bits are set. Per-bit is the right shape for
a JSON consumer; the difference is intentional and should not be "fixed".

**b. `basePoints` is not `EffectBasePoints`.** `SpellInfo.AppendEffectInfo`
renders `CalculateBaseEffectValue(effect)`, which folds in `Coefficient`,
`Scaling` and the spell's level data, and when `Math.Abs(effect.Variance)` is
over `1.0E-5f` it prints a *range*, `baseValue ± |baseValue × Variance × 0.5|`.
The raw `EffectBasePoints` field is a different number. JSON must emit the
computed value, or the MCP server and the GUI will report different damage for
the same spell — which is the one divergence this whole extraction exists to
prevent. Emit `basePoints` as the computed value, and when the variance branch
applies also emit `basePointsMin` and `basePointsMax`.

`CalculateBaseEffectValue` is currently `private`. Make it `internal` — Core and
its test project only. Do not make it public and do not copy its body into the
serialiser.

**c. `Effects` holds one entry per (effect index, difficulty) pair**, not one
per effect. `AppendEffectInfo` prints `Difficulty: Id {n}` for exactly this
reason. So `index` alone does not identify an entry, which is why `difficultyId`
is in the list above. Do **not** iterate `SpellEffectInfoStore` instead: it is
keyed on `EffectIndex` only (`DBC.cs:159`), so later difficulties overwrite
earlier ones and you would silently drop rows.

**d. `Effects` can contain nulls.** `HasEffect`, `HasAura`, `HasTargetA` and
`HasTargetB` (`SpellInfo.cs:968-986`) all guard with `eff != null`. Skip nulls.

**e. `radius` needs a lookup, and there are two.** `EffectRadiusIndex` is a
`uint[2]` of *indices*. `AppendRadius` (`SpellInfo.cs:808-816`) resolves each
against `DBC.DBC.SpellRadius` and reads `.Radius`, `.RadiusMin` and
`.MaxRadius`, returning early when the key is absent. Do the same: emit
`radius` from index `[0]` and `maxRadius` from index `[1]`, each an object
carrying the id and the three values, and omit either when the store has no
such key.

**f. The section names map onto these properties:**

| Section | Source |
| --- | --- |
| `misc` | the `Misc`-backed scalars — `Speed`, `SchoolMask`, `CastingTimeIndex`, `RangeIndex`, `SpellIconFileDataID`, `ActiveIconFileDataID`, plus `Category`, `DamageClass`, `Dispel`, `PreventionType` |
| `powers` | `Powers` — a `List<SpellPowerEntry>` initialised to empty, **never null**; omit when `Count == 0` |
| `targets` | `TargetRestrictions` — likewise a never-null `List<>`; omit when empty |
| `reagents` | `Reagents` and `ReagentsCurrency` |
| `aura` | `AuraOptions`, plus the `CumulativeAura` and duration scalars |
| `procs` | `ProcFlags`, `ProcFlagsEx`, `ProcChance`, `ProcCharges`, `ProcCooldown` (all `AuraOptions`-backed) **and** `BaseProcRate` / `ProcsPerMinuteFlags` from `ProcsPerMinute`. Decode `ProcFlags` against the `ProcFlags` enum and `ProcFlagsEx` against `ProcFlagsEx`, same raw-plus-name shape as `attributes` |
| `scaling` | `Scaling` |

`Powers` and `TargetRestrictions` being non-nullable lists is why the rule above
says "empty or null" rather than the plan's original "when its backing entry is
null" — that phrasing would have left both sections emitted as `[]` forever.

- [ ] **Step 5: Run the tests**

Run: `dotnet test SpellWork.Core.Tests -c Debug --filter SpellInfoJsonSerializerTests`
Expected: PASS, 3 tests.

- [ ] **Step 6: Run everything**

Run: `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug && dotnet test SpellWork.Core.Tests -c Debug`
Expected: build succeeds; all tests pass. Confirm the goldens still pass — this task must not have touched the text path.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: serialise SpellInfo to structured JSON

Adds SpellInfoJsonSerializer reading SpellInfo's public properties and
pairing each raw flag value with its decoded name from SpellEnums.

JSON deliberately does not go through ISpellInfoWriter. SpellInfo.Write
emits formatted lines, so a JSON writer would have to re-parse the tool's
own formatted output to recover structure. The properties already are the
structured model; the text writer renders them. The two formats now share
the object graph but not the rendering path, so a formatting change cannot
break JSON and vice versa."
```

---

## Definition of done

- [ ] `"$MSBUILD" -restore SpellWork.sln -p:Configuration=Debug` succeeds for all three projects.
- [ ] `dotnet test SpellWork.Core.Tests -c Debug` passes with `SPELLWORK_DBC_PATH` and `SPELLWORK_GT_PATH` set, and skips cleanly without them.
- [ ] The GUI launches, lists spells, renders a spell with correct bold and colours, and populates the proc tree.
- [ ] `SpellWork.Core` contains no `Console.Write`, no `System.Windows.Forms` and no `System.Drawing` — enforced by `CoreHygieneTests`.
- [ ] Every golden matches, having been captured before any code moved.
- [ ] Nine commits on `8.x-mcp`, one per task.

## Next plans

- **Plan 2 — MCP server.** The 14 tools from spec §6 on a `SpellWork.Mcp` net9.0 host, consuming `SpellInfoJsonSerializer`, `PlainTextSpellInfoWriter`, `SpellWorkConfig` and `WorldDatabase` from this plan.
- **Plan 3 — Visual chain.** The 8 DB2 structures and resolver from spec §7, including the layout-hash verification that makes them trustworthy.
