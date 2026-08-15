# SpellWork.Core extraction — handoff

Branch `8.x-mcp`, commits `566f12e..febf217`. Companion to
`plans/2026-08-01-spellwork-core-extraction.md` (Plan 1, complete) and
`specs/2026-08-01-spellwork-mcp-design.md`.

This records what the extraction found but deliberately did not fix, so Plan 2
(the MCP server) and Plan 3 (visual chains) do not rediscover it.

## What shipped

`SpellWork.Core` (netstandard2.0) now holds `SpellInfo`, `ProcInfo`, the DBC
stores, `SpellWorkConfig`, `WorldDatabase`, and two renderers behind
`ISpellInfoWriter` — `PlainTextSpellInfoWriter` and `SpellInfoJsonSerializer`.
The WinForms app renders through `RichTextBoxSpellInfoWriter`.

The governing constraint was byte-identical GUI output. Ten golden files in
`SpellWork.Core.Tests/Goldens/` were captured from the pre-branch build in Task
1 and carry git status `A` across the entire branch — added once, never
modified. 44 tests, zero build warnings.

`FrameworkNumberFormat` exists because .NET Framework and .NET Core disagree on
`{0:F}`: Framework formats a `float` through a 7-significant-digit view with
`MidpointRounding.AwayFromZero`, Core uses the exact value and banker's
rounding. `FormatSingle` and `FormatDouble` are deliberately separate methods
that each cast straight to `decimal` — widening a `float` to `double` first
would yield 15 significant digits and change the output. Do not merge them.

`SpellInfo.CalculateEffectValues` is `internal` and returns an
`EffectValueBreakdown` that both the text path and the JSON serializer render
from. This is the seam that stops the GUI and the MCP server reporting
different damage for the same spell; it exists because they *did* diverge
during Task 9 (Fireball serialised as 0 damage, its spell-power coefficient
dropped). Anything that needs effect magnitude goes through it.

## Blocking Plan 2: process-wide mutable state

**`SpellWork.Core` is not safe for concurrent use.** The extraction moved the
state without scoping it.

| Location | State |
| --- | --- |
| `DBC/DBC.cs:431-433` | `SelectedLevel`, `SelectedItemLevel`, `SelectedMapDifficulty` |
| `GameTables/GameTables.cs` | `GameTable<T>._records`, a static `Dictionary` |
| `DBC/DBC.cs:81` | `SpellTriggerStore` is a plain `Dictionary` while `SpellInfoStore` beside it is a `ConcurrentDictionary` |
| `Spell/ProcInfo.cs:27-28` | static `SpellProc`, `Update` |

`DBC.Selected*` is read at `SpellInfo.cs:233, 464, 620, 641, 678-679, 685,
764-769` and written from `FormMain.cs:151-153`.

Concurrent `get_spell` at a fixed level is fine — the stores are populated once
and then only read. The failure is any tool taking a `level` or `difficulty`
argument: there is no way to render at a level without mutating a global first,
so two such requests in flight give silently wrong numbers to whichever loses
the race. No exception, not reproducible from the response.

**Fix before any concurrent handler ships:** thread a
`RenderContext { Level, ItemLevel, MapDifficulty }` through `SpellInfo.Write`,
`CalculateEffectValues` and `CalculateBaseEffectValue`, defaulting to the
current statics so the GUI is untouched. `CalculateEffectValues` being the
single shared arithmetic path makes this much cheaper than it would have been
before the extraction.

`DBC.LoadAsync` also has no load guard, and `GameTable<T>.Open` populates with
`_records.Add`, so a second load in the same process throws a duplicate-key
`ArgumentException`. Invisible to the GUI, which loads once; it breaks any
"reload client data" operation. `ClientDataFixture` works around this with its
own static double-checked guard — the workaround exists because the guard is
missing from the thing that needs it.

## Plan gap: the JSON field set cannot satisfy spec §6

Task 9's field list was written without checking it against the tool surface in
spec §6, and `SpellInfoJsonSerializer` implements that list faithfully. The
gap is in the plan, not the code:

- `search_spells(family?)` has nothing to filter on — `spellFamilyName` is not serialised
- `get_spell_triggers` has no `effectTriggerSpell`

Also absent: `effectMiscValue`, `effectAmplitude`, mechanic, interrupt flags,
stances, cooldowns, levels, equipped-item requirements. Schedule this as its own
task rather than discovering it mid-implementation.

## Known defects, deliberately not fixed

**`ProcInfo` renders wrong text for 13 spells.** `SpellInfo.cs:192-217` never
resets `i` between the `ProcFlags` and `ProcFlagsEx` loops, so the second loop
indexes `ProcFlagDesc[i + 32]` from wherever the first stopped. Spell 213602
renders `"  53"` where the JSON correctly emits `PROC_EX_MISS`. This is the one
place the GUI and the serializer disagree about the same spell.

It was left wrong because correcting it changes rendered output and requires
re-capturing a golden, which this branch forbade. The out-of-range *crash* was
fixed (spell 275140 threw `IndexOutOfRangeException`) with a bounds guard that
provably changes no other spell's output: 227,229 of 227,230 spells render
character-identically, 1 previously threw, 0 changed.

Fix the counter deliberately, in its own change, with a golden update.

**Golden coverage holes.** No golden contains `+ resource *`, `+ Level *` or
`+ AP *`, so three of `EffectValueBreakdown`'s six fields are never exercised
by the safety net. Also uncovered: `Max Radius`, `ScreenEffect`,
`OverrideSpellData`, `Allowed areas`, `EquippedItemClass`, `ReagentsCurrency`,
`Triggered by`, `Description variables`, and the `MediumPurple`
map/difficulty branch. Only `spell-15594` has `Proc flag` and it never reaches
the `ProcFlagsEx` loop — the goldens would not have caught the `ProcInfo` bug.

There is also no committed way to re-capture a golden: the `--dump-goldens`
harness was removed in Task 6, though `Program.Main` still takes an unused
`args`. Restore one before any deliberate golden update.

**`RichTextBoxSpellInfoWriter` has no test.** It is what the entire GUI renders
through and the one component the goldens cannot cover, since they were
captured through `PlainTextSpellInfoWriter`. Its `TextColor` → `Color` and
`TextStyle` → `FontStyle` maps are hand-written and unverified.

**`CoreHygieneTests` are substring scans over source text.** A fully-qualified
`System.Console.WriteLine` passes clean. This guard is load-bearing for an MCP
stdio server, where a stray write to stdout corrupts the JSON-RPC stream —
scanning the compiled assembly's references would be strictly better.

## Accepted as-is

- Sign extension in `SpellInfoJsonSerializer.cs:392` / `:591-598` — `TargetCreatureType` is a `short` cast to `int`; a value with bit 15 set would emit phantom flag entries. Inert: 24,987 rows scanned, range 0..32639, no negatives. Same shape for `preventionType`.
- `valuePerResource` threshold asymmetry — the text path suppresses at `Math.Abs(…) <= 1.0E-5`, the JSON at exact zero. No value in this data falls between them.
- `effects[].maxRadius.maxRadius` nesting.
- `SkillLine == 0` as a sentinel in `ProcInfo`.
- `DBFileReaderLib`'s `HintPath`, triplicated across `SpellWork.csproj:69`, `SpellWork.Core.csproj:13` and `SpellWork.Core.Tests.csproj:27`. The test project's copy carries a comment explaining the CS0012 that requires it. A shared `Directory.Build.props` would be better.
- `FormMain` has no parameterless constructor, which breaks the VS WinForms designer for the app's main form.
## Verify with a clean rebuild, not an incremental one

`SpellWork/app.config` hard-coded a binding redirect pinning
`System.Runtime.CompilerServices.Unsafe` to 4.0.4.1. That was correct until
this branch pulled `Unsafe` up to 6.0.0, at which point it contradicted the
redirect `AutoGenerateBindingRedirects` writes and MSBuild raised **MSB3836**.

Every verification build during the branch reported zero warnings, and all of
them were wrong: the WinForms project was already up to date, so it never
recompiled and never re-emitted the warning. It surfaced only on the first
`-restore` build after the merge, and a `-t:Rebuild` confirmed it.

The explicit redirect is now removed — `AutoGenerateBindingRedirects` produces
the right one from whatever NuGet resolves, so a hard-coded copy can only go
stale. **Use `-t:Rebuild` when a build's warning count is the thing you are
checking.**

## Not verified by anyone yet — needs a human at the keyboard

Everything below needs the GUI actually launched
(`SpellWork\bin\Debug\SpellWork.exe`); no automated test covers it.

1. Select Fireball (133): the spell list populates, the header is bold,
   "Triggered by spell" is blue and bold, and effect blocks are coloured.
2. The proc tree renders with its blue/red colouring.
3. The world-database tab against a live MySQL server. `MySql.Data` went
   6.9.9 → 8.0.33 — required, because 6.9.9 ships no netstandard asset and
   cannot be referenced from `SpellWork.Core` at all — and the only test never
   opens a socket. The obvious suspicion was disproved (`SslMode` is
   `Preferred`, not `Required`, for both the named-pipe and TCP connection
   strings this code builds), so this is an untested driver swap rather than a
   predicted breakage. Five-minute check.
