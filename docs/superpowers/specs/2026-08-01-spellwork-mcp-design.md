# SpellWork MCP Server — Design

**Date:** 2026-08-01
**Branch:** `8.x-mcp` (off `8.x`)
**Status:** Approved design, pending implementation plan

## 1. Goal

Expose SpellWork's 8.3.7 spell knowledge to Claude Code as a local MCP server, so that an agent
working in an unrelated repository — primarily the BFA-HavenCore-Corruption emulator (TrinityCore
derivative, client 8.3.7 build 35662) — can query spell data, decode flags, resolve visual chains,
and cross-check the emulator's MySQL databases without a human driving the WinForms GUI.

The server is **read-only**. It has no write path to DB2 files or to MySQL.

## 2. Context

SpellWork on branch `8.x` is a .NET Framework 4.8 WinForms application targeting client 8.3.0
(34220). It loads `.db2` files through `DBFileReaderLib` (netstandard2.0, vendored at
`packages/DBFileReaderLib.1.0.0.0/lib/DBFileReaderLib.dll`), builds a `SpellInfo` object graph, and
renders a decoded dump into a `RichTextBox`.

Three facts drive this design:

- **The emulator's extracted client data is exactly SpellWork's expected input.** TrinityCore's
  extractors produce `<emulator-build-dir>/ClientData/dbc/<locale>/*.db2` (715 files) and
  `ClientData/gt/*.txt`. That maps directly onto SpellWork's `DbcPath` / `Locale` / `GtPath`
  settings, so the MCP server reads the same ground truth `worldserver` reads.
- **`netstandard2.0` bridges both runtimes.** It is the only target framework consumable by both
  the existing .NET Framework 4.8 GUI and a modern .NET server, and `DBFileReaderLib` already
  targets it.
- **Upstream has already validated the decoupling pattern.** The `origin/refactor` branch (2015,
  6.x-era) introduced `SpellWorkLib/ISpellInfoWriter.cs` to separate `SpellInfo` from `RichTextBox`,
  then added an HTML writer for a web frontend. That code is far too old to merge, but the shape —
  and the interface name — are reused here.

This repository is a clone of upstream `TrinityCore/SpellWork`, not a fork; `8.x` sits at
`origin/8.x` with no local commits. Work happens on the local `8.x-mcp` branch. Publishing later
requires forking on GitHub and pushing there.

## 3. Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Placement | Local `8.x-mcp` branch in this clone | Direct source reuse; upstream `8.x` fixes still rebase cleanly |
| Refactor shape | Extract `SpellWork.Core` (netstandard2.0) | One copy of decoding logic; GUI and MCP cannot drift |
| Host | Official `ModelContextProtocol` NuGet package, stdio | Attribute-based tool discovery; stdio is correct for a local tool |
| Default output | Structured JSON, `format:"text"` for the classic dump | JSON is cheaper and more precise for an agent; text preserves SpellWork's formatting knowledge |
| Spell source | Client DB2 only; MySQL as separate explicit tools | No hotfix-table schema mapping in v1; overlay can be added later without changing the tool surface |
| Visual chain | In scope for v1 | The data is present in ClientData and the layout risk is manageable (§7) |

## 4. Architecture

Four projects in `SpellWork.sln`:

| Project | TFM | Role |
| --- | --- | --- |
| `SpellWork.Core` | netstandard2.0 | Headless DB2 loading, structures, decoding, writers |
| `SpellWork` | net48, WinForms | Existing GUI, now referencing Core |
| `SpellWork.Mcp` | net9.0 | stdio MCP server |
| `SpellWork.Core.Tests` | net9.0 | xUnit |

**Moves into `SpellWork.Core`:** `DBC/DBC.cs`, all 47 `DBC/Structures/*`, `GameTables/*`,
`Spell/{SpellInfo,SpellEnums,ExpectedStat,ProcInfo}.cs`, `Extensions/{Extensions,LinqExtensions}.cs`,
and a headless replacement for `Database/MySQLConnect.cs`.

**Stays in the WinForms project:** `Forms/*`, `Extensions/RichTextBoxExtensions.cs`,
`Extensions/TreeViewExtensions.cs`, `Spell/SpellCompare.cs`, `Program.cs`, `Settings`.

`SpellCompare` stays GUI-side deliberately: its diff highlighting is built on `RichTextBox.Find` and
`SelectionBackColor` and is genuinely a rendering concern. The MCP server gets a structured field
differ instead, which serves an agent better than highlighted text would.

`ProcInfo.Fill` currently populates a `TreeView` directly. It is split: the family/mask traversal
moves to Core returning a plain node model, and the GUI keeps a thin adapter that walks that model
into its `TreeView`.

## 5. Components

### 5.1 `ISpellInfoWriter`

The interface surface is derived from exactly what `SpellInfo.cs` calls on its `RichTextBox`
parameter — 12 distinct members, of which 97 of the ~200 calls are `AppendFormatLine`:

```csharp
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
```

`TextColor` and `TextStyle` are Core-local enums so `SpellWork.Core` takes no dependency on
`System.Drawing`. Their members are exactly the values `SpellInfo.cs` uses today — six colours and
two styles — not the full `System.Drawing.Color` surface.

`SpellInfo.Write(RichTextBox)` becomes `SpellInfo.Write(ISpellInfoWriter)`. An abstract
`SpellInfoWriterBase` implements the `*IfNotNull` variants and the format overloads in terms of a
single abstract `AppendText`, so the null-formatting rules in
`RichTextBoxExtensions.IsValueConsideredNullForFormatting` exist in one place and cannot diverge
between implementations. Two concrete writers derive from it:

- `RichTextBoxSpellInfoWriter` — in the WinForms project, delegating to today's
  `RichTextBoxExtensions` so GUI rendering stays byte-identical.
- `PlainTextSpellInfoWriter` — in Core, backed by `StringBuilder`; styling calls are no-ops.

### 5.1.1 JSON is not a writer

JSON output deliberately does **not** go through `ISpellInfoWriter`. `SpellInfo.Write` emits
formatted lines, and structure cannot be recovered from a call like
`AppendFormatLine("Family {0} ({1}), flag [0] 0x{2:X8}", …)` without re-parsing the tool's own
output — a fragile design that would silently drift from the text path.

Instead, `SpellInfoJsonSerializer` (Core) reads `SpellInfo`'s public properties directly. Those
properties — `ID`, `Name`, `Attributes` through `AttributesEx13`, `Effects`, `Powers`,
`TargetRestrictions`, and the rest — already *are* the structured model; the text writer is a
rendering of them, not their source. The serializer pairs each raw value with its decoded name from
`SpellEnums.cs` and omits zero and empty fields.

This means the two output formats share the `SpellInfo` object graph but not the rendering path,
which is the correct coupling: a formatting change cannot break JSON, and a serialization change
cannot break the GUI.

### 5.2 `SpellWorkConfig`

Replaces the `Settings.Default` static reach-through inside Core. The coupling is small: `DBC.cs`
reads four settings (`DbcPath`, `Locale`, `GtPath`, `HotfixCachePath`) and `MySQLConnect.cs` reads
six (`Host`, `PortOrPipe`, `User`, `Pass`, `WorldDbName`, `UseDbConnect`). The GUI populates the
config from `Settings.Default`; the MCP server populates it from environment variables.

### 5.3 `SpellDataService`

Singleton in the MCP host owning the loaded stores. Initialises lazily on the first data tool call
behind a `Lazy<Task<SpellData>>`, so concurrent first calls cannot double-load, and a load failure is
cached as a failure rather than retried on every call.

### 5.4 Defects fixed during extraction

Two existing behaviours are incompatible with running as a server and are fixed as part of the move:

- **`DBC.Load()` is `public static async void`** (`SpellWork/DBC/DBC.cs:84`). Fire-and-forget: no
  caller can await completion, and any load exception is unobservable. Becomes
  `Task LoadAsync(SpellWorkConfig, ILogger)`.
- **`DBC.cs` writes twenty `Console.WriteLine` warnings** for unknown referenced spells. On
  stdout these corrupt the JSON-RPC stream and kill the MCP transport. All logging routes through
  `ILogger` to stderr. This is the single most likely cause of a silently broken server, so the
  smoke test in §10 explicitly asserts stdout carries nothing but JSON-RPC.

Two further oddities are noted but deliberately left alone, being harmless and out of scope:
`DBC.cs:115` passes a property's current value as the target object of a static `SetValue` (ignored
for statics), and `SpellMissile` is declared and loaded at `DBC.cs:74` but never read — the latter is
consumed by the visual work in §7.

## 6. Tool surface

Fourteen tools. All take and return JSON; all enforce a server-side result cap of 500 rows so a broad
query cannot flood the agent's context.

**Spell reference**

- `get_spell(id, format="json"|"text", sections?, difficulty=0, level?, itemLevel?)` — the full
  decoded view. `sections` filters to any of `misc`, `attributes`, `effects`, `powers`, `targets`,
  `reagents`, `aura`, `procs`, `scaling`, `items`, `triggers`, `visual`.
- `search_spells(name?, family?, auraType?, effectType?, targetType?, attribute?, schoolMask?, limit=50, offset=0)`
  — returns id, name and a one-line summary per hit.
- `get_spell_triggers(id)` — both directions, from the existing `SpellTriggerStore`.

**Visual**

- `get_spell_visual_chain(id, difficulty=0)` — see §7.

**Decoders and calculators**

- `decode_flags(kind, value)` — `kind` covers attributes (0–13), proc flags, school mask, interrupt
  flags, shapeshift mask, creature-type mask, and the other masks modelled in `SpellEnums.cs`.
- `list_enum(name, filter?)` — id↔name maps drawn from `SpellEnums.cs`, so the agent can resolve
  names to ids without guessing.
- `calculate_spell_scaling(id, level, itemLevel, mapId?, difficulty?)` — per-effect computed values
  via the content-tuning path added in commit `9da256e`.
- `compare_spells(idA, idB)` — structured field diff.

**Raw DB2**

- `list_db2()` — every `.db2` present, flagged as modelled or unmodelled.
- `get_db2_row(store, id)`
- `query_db2(store, filter?, limit=50, offset=0)`

**MySQL**

- `query_world_db(sql, database="world", limit=100)` — single `SELECT` only (§9).
- `get_spell_db_overrides(id)` — convenience wrapper collecting rows from `spell_proc`,
  `spell_script_names`, `spell_custom_attr`, `spell_bonus_data`, `spell_threat`, `spell_ranks` and
  `spell_dbc` for one spell id.

**Diagnostics**

- `server_info()` — configured paths, locale, store counts, load duration, MySQL reachability. The
  first thing to call when something looks wrong.

### 6.1 Known limitation: unmodelled DB2 field names

SpellWork models 47 of the 715 `.db2` files. For those, `get_db2_row` and `query_db2` return real
field names. For the remaining 668, `DBFileReaderLib` reads rows but exposes fields only positionally,
so results come back as `field_0`, `field_1`, … The tool response states this explicitly per row
rather than presenting positional data as if it were named. Resolving it properly means adopting
WoWDBDefs for all stores, which shares its groundwork with §7 and is recorded as a follow-up in §11.

## 7. Visual chain resolution

SpellWork today loads `SpellXSpellVisual.db2` and surfaces `SpellVisualID` as a bare integer on one
line (`SpellInfo.cs:255`). `AppendSpellVisualInfo()` (`SpellInfo.cs:922`) is entirely commented out —
dead pre-Legion code still referencing `_spell.SpellVisual[0]` and `SpellMissileMotion`, never ported
when Blizzard split the visual tables. `SpellVisual` and `SpellMissileMotion` storages are commented
out at `DBC.cs:78-79`.

v1 resolves the chain:

```
SpellXSpellVisual                    (already modelled)
  └─ SpellVisual
      ├─ SpellVisualEvent            ─┐
      ├─ SpellVisualMissile          ─┤ each reaches kits by id
      └─ (kit ids on SpellVisual)    ─┘
                                      │
                                      └─ SpellVisualKit
                                          └─ SpellVisualKitEffect
                                              ├─ SpellVisualEffectName
                                              ├─ SpellVisualKitModelAttach
                                              └─ SpellChainEffects
```

`SpellVisualKit` is reached from three directions — kit ids carried on `SpellVisual` itself, on
`SpellVisualEvent` rows, and on `SpellVisualMissile` rows — so the resolver walks kit references as a
set gathered from all three rather than as a single linear path. The exact set of kit-bearing fields
per table is confirmed from the layout under §7.1 control 1 before the resolver is written; the
resolver deduplicates kits shared between events and missiles.

`SpellMissile` and `SpellMissileMotion` are wired in alongside, which gives the already-loaded but
unread `SpellMissile` store a consumer.

`get_spell_visual_chain(id)` returns the resolved tree: visual id, per-difficulty and per-condition
variants from `SpellXSpellVisual`, missile model and motion, and the kit effects with their
`SpellVisualEffectName` model/texture references.

### 7.1 Layout risk and how it is controlled

This is the riskiest part of the project. `DBFileReaderLib` maps fields by declaration order, so a
structure class whose fields are the right count but the wrong order produces silent garbage rather
than an error. Three controls apply, in order of strength:

1. **Layout hash verification.** The WDC3 header exposes `LayoutHash` and `FieldsCount`, both
   surfaced by `DBFileReaderLib`. WoWDBDefs records a `LAYOUT` hash per definition version. For each
   new table, the build's layout hash is matched against the `.dbd` before the field list under that
   layout is trusted. This is the primary control and it must pass before a structure is considered
   correct.
2. **Cross-check against the emulator.** BFA-HavenCore-Corruption carries verified build-35662
   layouts for two of these tables — `SpellVisualKitEntry` at `src/server/game/DataStores/DB2Structure.h:3436`
   and `SpellXSpellVisualEntry` at `:3446`. Those are compared directly.
3. **Referential-integrity tests.** Every `SpellVisualID` referenced by `SpellXSpellVisual` must
   resolve in `SpellVisual`, every kit id must resolve in `SpellVisualKit`, and so on down the chain.
   A wrong layout collapses these resolution rates, so the tests assert a near-100% resolution rate
   rather than merely asserting the load did not throw.

Field-count mismatches already fail loudly — `DBC.cs:120-125` catches the `ArgumentException`
`DBFileReaderLib` raises and reports `Failed to load {name}.db2`. It is field *order* among
same-width types that fails silently, which is what control 1 and control 3 exist to catch.

Structure classes are hand-written C# in the style of the existing 47, using WoWDBDefs purely as a
reference. No new runtime dependency and no vendored definition files are introduced.

## 8. Configuration

SpellWork 8.x parses 8.3.7 structures only, so exactly one client data source is valid. Per-project
auto-discovery would be over-engineering; configuration is by environment variable, set once at
registration:

| Variable | Required | Meaning |
| --- | --- | --- |
| `SPELLWORK_DBC_PATH` | yes | `<emulator-build-dir>/ClientData/dbc` |
| `SPELLWORK_GT_PATH` | yes | `<emulator-build-dir>/ClientData/gt` |
| `SPELLWORK_LOCALE` | no (`enUS`) | Locale subdirectory under the DBC path |
| `SPELLWORK_HOTFIX_CACHE` | no | Client `DBCache.bin`, already supported by `DBC.Load` |
| `SPELLWORK_MYSQL_HOST` / `_PORT` / `_USER` / `_PASS` / `_WORLD_DB` | no | Omit to disable the MySQL tools |

Registered with `claude mcp add --scope user`, making it available in every project.

## 9. Error handling

The governing rule: a misconfigured server must still start and still answer. A dead stdio server is
indistinguishable from a broken one from the agent's side, and yields no diagnosis.

- Missing or invalid DBC path — the server starts normally; data tools return a structured error
  naming the offending path and the variable to fix. The process never exits on a config error.
- All logging goes to stderr. Nothing but JSON-RPC is ever written to stdout (§5.4).
- MySQL unreachable or unconfigured — MySQL tools report it in their response; every other tool is
  unaffected.
- Unknown spell id — a structured "not found" plus nearest-name suggestions from `SpellName`.
- `query_world_db` accepts a single `SELECT` statement only. Multiple statements, DDL and DML
  keywords are rejected before the query reaches MySQL, and the documentation directs the operator to
  connect as a read-only MySQL user. Both controls are required: the parser is a guard, the read-only
  grant is the guarantee. This matters because the agent composes these queries itself.
- Visual chain — an unresolvable link returns the unresolved id with an explicit `resolved: false`
  rather than omitting the node, so a layout regression is visible in output instead of looking like
  a spell with no visual.

## 10. Testing

SpellWork has no test framework today; `SpellWork.Core.Tests` (xUnit, net9.0) introduces one.

**Golden-file regression is the centrepiece.** Before any refactoring, the current GUI's rendered
output is captured for a spread of spells — a simple direct-damage spell, a passive, a PPM aura, one
with content-tuning scaling, one with reagents and totems, and one with many effects. After the
refactor, `PlainTextSpellInfoWriter` must reproduce those files exactly. This is what demonstrates
that extracting `ISpellInfoWriter` changed no behaviour, and it is the reason the goldens must be
captured first.

Also covered:

- Unit tests for every `decode_flags` kind and for `list_enum` round-tripping.
- Unit tests for the `SELECT`-only validator, including the injection attempts it must reject. This
  is the one security-relevant component and is tested independently of any database.
- Visual-chain referential-integrity tests per §7.1 control 3, asserting resolution rates.
- An MCP smoke test performing a scripted stdio handshake, asserting all fourteen tools list, and
  asserting stdout contains only well-formed JSON-RPC frames across a full load cycle.

Tests requiring ClientData are gated on `SPELLWORK_DBC_PATH` and skip with an explicit message when
it is unset, rather than failing.

## 11. Out of scope, recorded as follow-ups

- **Hotfix overlay.** Merging the emulator's `hotfixes` MySQL tables over the DB2 rows so `get_spell`
  shows what `worldserver` actually serves, including server-only custom spells. Deferred from v1 to
  avoid mapping roughly twenty hotfix tables onto the DB2 structures. Adding it later introduces a
  `source` parameter without disturbing the existing tool surface.
- **WoWDBDefs for all stores.** Would give real field names for the 668 unmodelled `.db2` files
  (§6.1) and replace the hand-written structures in §7 with generated ones.
- **HTTP transport.** Not needed for a local tool.
- **Any write path.** Explicitly excluded, permanently.

## 12. Open risks

| Risk | Impact | Control |
| --- | --- | --- |
| Visual-table field order wrong in a way that still loads | Silently wrong output | §7.1 — layout hash first, referential-integrity tests as backstop |
| A stray `Console.WriteLine` reaches stdout | MCP transport dies with no diagnosis | §5.4 plus the stdout assertion in §10 |
| Golden files captured after refactoring rather than before | Regression suite proves nothing | Capture is the first implementation step, before any code moves |
| GUI regression from the `ISpellInfoWriter` extraction | Breaks a working tool | GUI retained specifically as a visual cross-check; goldens cover the render path |
| Upstream `8.x` moves under the branch | Painful rebase | Core extraction is mechanical file moves, which rebase predictably |
