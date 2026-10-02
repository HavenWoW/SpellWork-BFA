# SpellWork MCP server

A read-only MCP server exposing SpellWork's 5.4.8 (build 18414) spell data,
DBC/DB2 stores and world database to an agent. Fourteen tools; see
`server_info` first when anything looks wrong.

## Build

```
dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o out/mcp548
```

## Register

```
claude mcp add --scope user spellwork-548 \
  -e SPELLWORK_DBC_PATH=C:/MoP/MOPPREMIUM/Repack/dbc \
  -e SPELLWORK_DB2_PATH=C:/MoP/MOPPREMIUM/Repack/db2 \
  -- dotnet /absolute/path/to/out/mcp548/SpellWork.Mcp.dll
```

`--scope user` makes it available in every project. Add the `SPELLWORK_MYSQL_*`
variables below to enable the database tools.

## Configuration

Set these on the MCP server entry (`claude mcp add -e NAME=value`, spelled
`--env NAME=value` in full, or the `env` block in the config file).

| Variable | Required | Meaning |
| --- | --- | --- |
| `SPELLWORK_DBC_PATH` | yes | Folder holding the client `.dbc` files |
| `SPELLWORK_DB2_PATH` | no | Folder holding the client `.db2` files. When a table exists as both, the `.db2` is loaded |
| `SPELLWORK_GT_PATH` | no, unused | Accepted for compatibility; gtSpellScaling is a DBC store in 5.4.8 |
| `SPELLWORK_LOCALE` | no (`enUS`) | Locale subfolder under the DBC path. If it does not exist, the DBC path itself is used and `server_info` reports `localeFallback` |
| `SPELLWORK_MYSQL_HOST` | no | Omit to disable the MySQL tools |
| `SPELLWORK_MYSQL_PORT` | no (`3306`) | Port, or a named pipe when host is `.` |
| `SPELLWORK_MYSQL_USER` | no | See the security note below |
| `SPELLWORK_MYSQL_PASS` | no | |
| `SPELLWORK_MYSQL_WORLD_DB` | no | Database name, e.g. `world` |

`SPELLWORK_HOTFIX_CACHE` no longer exists: 5.4.8 has no `DBCache.bin` hotfix
layer.

The MySQL tools require host, user and world-db together; a partial
configuration reports "not configured" rather than failing to connect.

A misconfigured server still starts and still answers: a bad path produces a
structured error from the data tools naming the variable to fix, not a dead
process.

## Security: use a read-only MySQL user

**`query_world_db` runs SQL that the agent composed.** Two controls stand
between that and your database, and both are required:

1. The server rejects anything that is not a single `SELECT`: multiple
   statements, DDL and DML are refused before the query reaches MySQL.
2. **You connect as a MySQL user with only `SELECT` granted.**

The parser is a guard. The read-only grant is the guarantee. Do not point this
server at an account that can write. The repack's only MySQL account is
`root`, so create a dedicated one before registering:

```sql
CREATE USER 'spellwork_ro'@'localhost' IDENTIFIED BY '<password>';
GRANT SELECT ON world.* TO 'spellwork_ro'@'localhost';
```

Nothing in this server writes to client files or to MySQL, by design.

## Tools

| Tool | What it does |
| --- | --- |
| `server_info` | Configured paths, locale, `clientBuild` (18414), `localeFallback`, `openedFiles` (the exact file each store was read from), `layoutMismatches` (expected and actual field count and record size), store counts, load duration, MySQL reachability |
| `get_spell` | The full decoded view of one spell, as JSON or rendered text |
| `search_spells` | Find spells by name, family, aura/effect/target type, attribute or school |
| `get_spell_triggers` | What a spell triggers and what triggers it |
| `compare_spells` | Structured field diff of two spells |
| `decode_flags` | Split a bitmask into named flags |
| `list_enum` | The id-to-name map for any enum |
| `calculate_spell_scaling` | Per-effect values at a given level and difficulty from gtSpellScaling |
| `list_db2` | Every `.dbc` and `.db2` file present, flagged modelled / loaded / shadowed |
| `get_db2_row` | One row from a modelled store, with real field names |
| `query_db2` | Filtered rows from a modelled store |
| `query_world_db` | One read-only `SELECT` against the world database |
| `get_spell_db_overrides` | Rows for one spell from the world database's `spell_*` override tables and `spellmisc_dbc` |
| `get_spell_visual_chain` | The resolved visual chain for one spell: variants, missiles and deduplicated kits with their effects |

### Difficulty

Every `difficulty` argument is a **`Difficulty.dbc` id**. `0` is the base (the
default) and has no Difficulty row; the valid ids are
`0,1,2,3,4,5,6,7,8,9,11,12,14`. Anything else returns `unknown_difficulty`
listing the valid ones.

`get_spell` reads every difficulty-keyed section (SpellMisc, SpellEffect,
SpellCategories, SpellAuraOptions, SpellCooldowns, SpellLevels,
SpellInterrupts, SpellTargetRestrictions) at that difficulty and falls back to
the base row for anything the difficulty does not override, per effect index
for SpellEffect. Each effect reports `resolvedDifficulty`, the difficulty its
row actually came from, so a partially overridden spell shows which effects
are which.

Displayed values are the **rounded (half away from zero)** result of the 5.4.8
server formula, with the level clamped to 1..90. The server truncates, so a
client tooltip may differ by 1. Effects that do not scale report
`realPointsPerLevel`, which is already included in the base value.

`calculate_spell_scaling` uses the same difficulty validation and the
gtSpellScaling formula; it reports `itemLevelApplied: false`, because item
level scaling is not applied.

### Search

`search_spells`' `attribute` filter takes a mask or enum member name tested
against attribute word 0, or `word:value` with the word 0-13, e.g. `13:0x1`.
A bare value is word 0.

### Visual

`get_spell_visual_chain(id)` takes only the spell id: 5.4.8 visuals are not
per difficulty. The chain is `SpellMisc.SpellVisualID[0]` and `[1]` (the base
difficulty row) → `SpellVisual` → its kit columns and `SpellVisualMissile` rows
→ `SpellVisualKit` → effect columns resolved to `SpellVisualEffectName`, plus
`SpellVisualKitModelAttach` rows. Things that are easy to misread:

- Each non-zero visual id is a **variant** with a `slot` of 0 or 1.
- Kits are deduplicated across variants and slots; `reachedVia` names every
  visual, slot and column that reached a kit. Kits are capped at 500 across the
  whole response.
- `unk14MissileMotion` is `SpellVisual` column 14 reported **raw**. WoWDBDefs
  calls it MissileMotion, but almost none of its values are `SpellMissileMotion`
  ids, so its meaning is unverified; it is never resolved and never appears in
  `unresolvedReferences`. The same column is `Unk14MissileMotion` in
  `get_db2_row` and `query_db2`.
- `SpellVisualKitAreaModel` is **not reachable** from the chain: it has no
  parent-kit column in 5.4.8. `SpellMissile` is keyed by spell and is not
  reachable either.
- `unresolvedReferences` lists every non-zero id that has no row, with where it
  came from.

`get_spell`'s `visual` section is the same chain through the same resolver, in
all three renderings. When a store the walk needs did not load, the `json`
section reports `storeUnloaded: true` and `missingStores`, the `text` section
prints `Visual chain unavailable -- these stores did not load: ...`, and
`get_spell_visual_chain` returns `store_not_loaded` naming the missing stores.
That is distinct from a spell that simply has no visual.

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

`get_spell_db_overrides` reads ten tables by spell id: `spell_proc_event`,
`spell_proc`, `spell_script_names`, `spell_custom_attribute`,
`spell_bonus_data`, `spell_threat`, `spell_ranks`, `spell_linked_spell`,
`spell_dbc` and `spelleffect_dbc`. An eleventh, `spellmisc_dbc`, is keyed by
SpellMisc id rather than spell id, so it is joined: its entry is
`{matchedBy: "spellMiscId", miscIds, rows}`, where the ids are the client's
`MiscID` for the spell united with the `SpellMiscId` of any `spell_dbc` rows
found. Tables the schema lacks are listed under `missingTables` and are not an
error. The 500-row cap applies **per table**, so a response can carry up to
5500 rows; `truncatedTables` names the tables that hit it.

### Listing stores

`list_db2` is deliberately uncapped. It is the discovery entry point and takes
no `offset`, so a cap would conceal stores with no way to page to them. It
returns `stores`: one row per `.dbc` or `.db2` file with `extension`,
`modelled`, `loaded` and `shadowed`, plus each modelled store that has no file
(with `extension` null). Only modelled stores can be read with `get_db2_row`
and `query_db2`; other files return `store_not_modelled`.

## Known limitations

- **Stale `.dbc` shadowing.** The client's `.dbc` files can sit next to a
  newer `.db2` (for example `SpellVisual.dbc` has the 5.3 layout of 38 fields
  while `SpellVisual.db2` has 30). The `.db2` is always the one loaded; check
  `list_db2` (`shadowed`) or `server_info` (`openedFiles`) to see which file a
  store came from.
- **Header build 18273.** The `.db2` files carry header build 18273, not 18414.
  The loader does not check it; layouts are verified by field count and record
  size instead, reported under `layoutMismatches`.
- **Custom spells are overrides only.** Spells the repack adds in its world
  database (`spell_dbc`, `spelleffect_dbc`, `spellmisc_dbc`) do not exist in the
  client files, so they appear only through `get_spell_db_overrides`, not
  through `get_spell` or `search_spells`.
- Client data loads once per process. There is no reload; restart the server
  after changing a path.
