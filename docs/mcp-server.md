# SpellWork MCP server (CoA 3.3.5a)

A read-only MCP server exposing SpellWork's **CoA 3.3.5a (build 12340)** spell
data, DBC stores and world database to an agent. Fourteen tools; see
`server_info` first when anything looks wrong.

**Installing and registering with Claude Code:** see
[Register with Claude Code](../README.md#register-with-claude-code) in the
README (prerequisites, download or build, PowerShell / bash / cmd commands,
verification, `.mcp.json`, troubleshooting). This page is the configuration
and tool reference.

## Build

```
dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o out/mcpcoa
```

Release builds of the `coa` branch are attached to the `vcoa-*` tags as
`SpellWork.Mcp-coa.zip`. Either way the server is framework-dependent and runs
as `dotnet <folder>/SpellWork.Mcp.dll` on the .NET 9 runtime.

## Configuration

Set these on the MCP server entry (`claude mcp add -e NAME=value`, spelled
`--env NAME=value` in full, or the `env` block in `.mcp.json`).

| Variable | Required | Meaning |
| --- | --- | --- |
| `SPELLWORK_DBC_PATH` | yes | Folder that directly holds the client `.dbc` files (`Spell.dbc`, ...). For a CoA server checkout: `<azerothcore-wotlk-coa>/env/dist/data/dbc` |
| `SPELLWORK_LOCALE` | no (`enUS`) | Which of the sixteen string slots of every localized column to read: `enUS`/`enGB` 0, `koKR` 1, `frFR` 2, `deDE` 3, `zhCN` 4, `zhTW` 5, `esES` 6, `esMX` 7, `ruRU` 8. An unknown value reads slot 0 and `server_info` reports `localeSlotFallback: true`. 3.3.5a has no locale folders |
| `SPELLWORK_MYSQL_HOST` | no | Omit to disable the MySQL tools |
| `SPELLWORK_MYSQL_PORT` | no (`3306`) | Port, or a named pipe when host is `.` |
| `SPELLWORK_MYSQL_USER` | no | See the security note below |
| `SPELLWORK_MYSQL_PASS` | no | |
| `SPELLWORK_MYSQL_WORLD_DB` | no | World database name, `acore_world` for AzerothCore |
| `SPELLWORK_DB2_PATH` | no, ignored | Accepted so a registration copied from another SpellWork build still starts; 3.3.5a has no `.db2` files. `server_info` lists it under `ignoredVariables` when set |
| `SPELLWORK_GT_PATH` | no, ignored | As above; 3.3.5a has no gt tables |

The MySQL tools require host, user and world-db together; a partial
configuration reports "not configured" rather than failing to connect. The
connection times out after 5 s and a query after 15 s.

A misconfigured server still starts and still answers: a bad path produces a
structured error from the data tools (`dbc_path_missing`,
`dbc_path_not_found`, `no_data_loaded`, `load_failed`) naming the variable to
fix, and `server_info` reports it under `clientDataError`, not a dead process.

## Security: use a read-only MySQL user

**`query_world_db` runs SQL that the agent composed.** Two controls stand
between that and your database, and both are required:

1. The server rejects anything that is not a single `SELECT`: multiple
   statements, DDL and DML are refused before the query reaches MySQL.
2. **You connect as a MySQL user with only `SELECT` granted.**

The parser is a guard. The read-only grant is the guarantee. Do not point this
server at an account that can write; create a dedicated one:

```sql
CREATE USER 'spellwork_ro'@'127.0.0.1' IDENTIFIED BY '<password>';
GRANT SELECT ON acore_world.* TO 'spellwork_ro'@'127.0.0.1';
```

Nothing in this server writes to client files or to MySQL, by design.

## Tools

| Tool | What it does |
| --- | --- |
| `server_info` | `version`, configured paths, `clientBuild` (12340), `variant` (`coa`), `locale`, `localeSlot`, `localeSlotFallback`, `ignoredVariables`, `openedFiles` (per store: `path`, `extension`), `layoutMismatches` (expected and actual field count and record size; empty on matching data), `storeCounts`, `spellCount`, `loadDurationMs`, `clientDataLoaded` / `clientDataError`, MySQL `mysqlConfigured` / `mysqlReachable` / `mysqlError` |
| `get_spell` | The full decoded view of one spell (one `Spell.dbc` row), as JSON or rendered text |
| `search_spells` | Find spells by name, family, aura/effect/target type, attribute or school |
| `get_spell_triggers` | What a spell triggers and what triggers it |
| `compare_spells` | Structured field diff of two spells |
| `decode_flags` | Split a bitmask into named flags |
| `list_enum` | The id-to-name map for any enum |
| `calculate_spell_scaling` | Per-effect values at a given level from the 3.3.5 `CalcValue` rule |
| `list_db2` | Every `.dbc` file present, flagged modelled / loaded |
| `get_db2_row` | One row from a modelled store, with real field names |
| `query_db2` | Filtered rows from a modelled store |
| `query_world_db` | One read-only `SELECT` against the world database |
| `get_spell_db_overrides` | Rows that apply to one spell from the thirteen AzerothCore/CoA `spell_*` override tables |
| `get_spell_visual_chain` | The resolved visual chain for one spell: variants, missiles and deduplicated kits with their effects |

The `*_db2` names are kept so agents call every SpellWork server alike; on
this build they read `.dbc` stores.

### Enums

Enums come from the CoA server headers (`SharedDefines.h`,
`SpellAuraDefines.h`, `SpellDefines.h`): 367 aura types and 199 spell effects
including the CoA additions beyond stock 3.3.5a, attribute words 0–7, the CoA
classes and spell families. A CoA-custom aura or effect id decodes by name.

### Difficulty

3.3.5a has no per-row difficulty: a per-difficulty spell is a **separate spell
id**, listed by mode in a `SpellDifficulty.dbc` row. Every `difficulty`
argument is therefore a **SpellDifficulty mode**: `0` 10-normal, `1`
25-normal, `2` 10-heroic, `3` 25-heroic. Anything else returns
`unknown_difficulty` listing the valid modes.

`difficulty` is **optional on every tool that takes it** (`get_spell`,
`get_spell_triggers`, `compare_spells`, `calculate_spell_scaling`). Omitted,
the id is used exactly as given. Given, the id is resolved first, the way the
CoA server does it (`SpellMgr::GetSpellIdForDifficulty`):

- The spell's row is the `SpellDifficulty` row **that lists the spell id**.
  The `Spell.SpellDifficultyID` column is not used: the server does not read
  it, and on the CoA data most spells that set it name a row that does not
  list them.
- Rows without both a mode-0 and a mode-1 spell are ignored, and slots naming
  a spell id missing from `Spell.dbc` count as empty (as the server loads
  them).
- An empty heroic slot falls back to the normal mode of the same size
  (mode 2 → 0, mode 3 → 1). If that is empty too, or the spell is in no row,
  the requested id itself is the answer, never an error.

Because resolution is by row membership, mode `0` on a 25-man id maps it to
its 10-man id. Pass `difficulty` only when you want that mapping.

`get_spell` always carries a top-level `difficulty` block
`{requested, resolvedSpellId, variants}` (`requested` is null when omitted;
`variants` lists every spell id of the row, empty when the spell is in none),
and the document, `id` included, describes the resolved spell; the text format
adds `requestedId`. `compare_spells` applies the mode to both ids and reports
`requestedId` and the resolved id for each; `get_spell_triggers` and
`calculate_spell_scaling` report `resolvedSpellId`. `get_spell_visual_chain`
takes no `difficulty` (see Visual).

### Scaling

`calculate_spell_scaling` and `get_spell`'s effect `basePoints` use the 3.3.5
`SpellInfo::CalcValue` rule from the CoA source: `BasePoints +
RealPointsPerLevel × (level clamped to BaseLevel..MaxLevel − SpellLevel)`,
with the `DieSides` roll range and `PointsPerComboPoint` reported separately
(`min` / `max` / `perComboPoint` per effect in `calculate_spell_scaling`;
`basePointsMin` / `basePointsMax` in `get_spell` when there is a range). The level clamps to 1..100 (CoA
`MAX_LEVEL`). `itemLevel` is echoed with `itemLevelApplied: false`; `mapId`
is accepted for compatibility and ignored.

### Search

`search_spells` covers all 209,510 spells of the CoA `Spell.dbc`. Its
`attribute` filter takes a mask or `SpellAtribute` member name tested against
attribute word 0, or `word:value` with the word 0–7, e.g. `7:0x1`. A bare
value is word 0.

### Visual

`get_spell_visual_chain(id)` takes only the spell id: visuals are not per
difficulty (pass a variant's own id to see its visual). The chain is:

```
Spell.SpellVisualID[0], [1]           each non-zero id is a variant, slot 0 or 1
 └─ SpellVisual
     ├─ PrecastKit, CastKit, ImpactKit, StateKit, StateDoneKit, ChannelKit,
     │  CasterImpactKit, TargetImpactKit, MissileTargetingKit, InstantAreaKit,
     │  ImpactAreaKit, PersistentAreaKit             ──► SpellVisualKit  (kitSlots)
     ├─ MissileModel   ──► SpellVisualEffectName      (missiles[].effectNameId, name, fileName)
     └─ MissileMotion  ──► SpellMissileMotion         (missiles[].motionId, motionName)
SpellVisualKit
 ├─ Head, Chest, Base, LeftHand, RightHand, Breath, LeftWeapon, RightWeapon,
 │  Special0..2, World effect columns ──► SpellVisualEffectName  (effects)
 └─ SpellVisualKitModelAttach rows (by ParentSpellVisualKitID)
                               ──► SpellVisualEffectName  (modelAttachments)
```

Things that are easy to misread:

- **Missiles.** Each variant has at most one `missiles` entry, present when
  `MissileModel` or `MissileMotion` is non-zero. Unlike the 5.4.8 build there
  is no missile row of its own, so the entry has **no `id`**; it carries
  `effectNameId`, `name`, `fileName` (the model path), `motionId` and
  `motionName`. A **negative** `MissileModel` (-1..-5) is a sentinel, not an
  effect-name id: it is reported raw with a null `name` and is never listed in
  `unresolvedReferences`.
- Kits are deduplicated across variants and slots; `reachedVia` names every
  visual, slot and column that reached a kit. Kits are capped at 500 across
  the whole response (`truncated`).
- `SpellVisualKitAreaModel` is modelled but **not reachable**: no kit column
  references it. `Spell.SpellMissileID` is **not followed**: only about 73 % of
  its non-zero values resolve in `SpellMissile` on the reference data.
  `SpellChainEffects` is modelled (readable with `get_db2_row` / `query_db2`)
  but not part of the chain.
- `unresolvedReferences` lists every non-zero id that has no row, with where it
  came from.

`get_spell`'s `visual` section is the same chain through the same resolver.
When a store the walk needs did not load, the `json` section reports
`storeUnloaded: true` and `missingStores`, the `text` section prints
`Visual chain unavailable -- these stores did not load: ...`, and
`get_spell_visual_chain` returns `store_not_loaded` naming the missing stores
among `SpellVisual`, `SpellVisualKit`, `SpellVisualEffectName` and
`SpellVisualKitModelAttach`. That is distinct from a spell that simply has no
visual.

### Items

`get_spell`'s `items` section lists the world database's `item_template`
items whose `spellid_1`..`spellid_5` name the spell, and each reagent carries
its `item_template` name. Without a world database, `items` is empty with an
`itemsNote`, and reagents are reported by id.

### Result sizes

`search_spells`, `query_db2`, `query_world_db`, `list_enum`,
`get_spell_triggers` and `compare_spells` cap results at 500 rows and report
whether they truncated.

`search_spells`, `query_db2` and `list_enum` return that page in one shared
envelope: `rows` with `count` (rows **in this response**), `totalMatches` (how
many matched in total), `offset`, `limit` (the ceiling actually applied, not
the value asked for) and `truncated`. Page past a truncated result by raising
`offset`.

### World database overrides

`get_spell_db_overrides(id)` reads thirteen AzerothCore/CoA tables (columns
from the CoA `db_world` base SQL):

| Table | Spell id column | Also matches |
| --- | --- | --- |
| `spell_proc` | `SpellId` | `-firstRank` (`allRanks`) |
| `spell_proc_event` | `entry` | `-firstRank` (`allRanks`) |
| `spell_script_names` | `spell_id` | `-firstRank` (`allRanks`) |
| `spell_custom_attr` | `spell_id` | |
| `spell_bonus_data` | `entry` | |
| `spell_threat` | `entry` | |
| `spell_ranks` | `spell_id` | |
| `spell_linked_spell` | `spell_trigger` | `-id` (`onRemoval`) |
| `spell_dbc` | `ID` | |
| `spell_cooldown_overrides` | `Id` | |
| `spell_area` | `spell` | |
| `spell_group` | `spell_id` | `firstRank` (`firstRank`) |
| `spell_required` | `spell_id` | |

The id forms follow each table's server loader, so the tool returns every row
that applies to the spell, not only rows keyed by its exact id. The first rank
comes from `spell_ranks` (`first_spell_id`; the spell itself when it has no
chain). Every returned row carries **`matchedBy`**:

- `exact` — keyed by the spell id itself;
- `allRanks` — a negative key naming the spell's first rank, which the server
  applies to every rank (`spell_proc`, `spell_proc_event`,
  `spell_script_names`);
- `firstRank` — `spell_group` lists only first ranks and membership covers
  every rank;
- `onRemoval` — a negative `spell_linked_spell.spell_trigger`: the link fires
  when the spell's aura is removed.

Tables the schema lacks are listed under `missingTables` and are not an error;
other per-table failures appear under `unavailableTables`. The 500-row cap
applies **per table**; `truncatedTables` names the tables that hit it.

## Known limitations

- **CoA-only DBCs are not modelled.** Files the CoA server does not load
  (`SpellAddon`, `SpellCharges`, `SpellCustomAttr`, `SpellAlternativeCost`,
  `SpellTags`, ...) appear in `list_db2` with `modelled: false` and cannot be
  read with `get_db2_row` / `query_db2`. `SpellAffect.dbc`, which the CoA
  server does load, is modelled.
- **`spell_dbc` is not merged.** Spells or fields the world database adds in
  `spell_dbc` do not change `get_spell` or `search_spells`; they appear only
  through `get_spell_db_overrides`.
- **SpellChainEffects** (48 fields, 177 bytes; five packed `uint8` colour /
  blend columns) is modelled exactly and loads with zero layout mismatches on
  the reference data. The CoA server itself does not load it.
- **Non-enUS locales.** The CoA files may leave other locale slots empty; the
  load still succeeds and names come back empty. `server_info` reports the
  slot used.
- **World database per call.** Each world-database call opens its own
  connection; an unreachable server costs the 5 s connection timeout on every
  call that needs it. The `text` format of `get_spell` does not add
  world-database data.
- Client data loads once per process. There is no reload; restart the server
  (restart Claude Code) after changing a path or the files.
