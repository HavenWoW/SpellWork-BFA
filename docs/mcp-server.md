# SpellWork MCP server

A read-only MCP server exposing SpellWork's 8.3.7 spell data, DB2 stores and
world database to an agent. Fourteen tools; see `server_info` first when
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

Set these on the MCP server entry (`claude mcp add -e NAME=value`, spelled
`--env NAME=value` in full, or the `env` block in the config file).

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
| `get_spell_visual_chain` | The resolved visual chain for one spell: variants, missile, and deduplicated kits with their effects |

### Visual

`get_spell_visual_chain` walks `SpellXSpellVisual` → `SpellVisual` →
`SpellVisualEvent`/`SpellVisualMissile` → `SpellVisualKit` →
`SpellVisualKitEffect`. Two things about its output are easy to misread:

- `spellVisualMissileId` is a `SpellVisualMissile` row id, **not** a
  `SpellMissile` id. The standalone `SpellMissile` table is keyed by the spell
  itself and is not reachable from this chain.
- `effectType`/`effect` are raw ids. Only `effectType` 2 has a confirmed target
  table (`SpellVisualKitModelAttach`) in this build, so no name is invented for
  the rest.

**`get_spell_visual_chain`'s `difficulty` is the one exception to this
server's difficulty convention.** Everywhere else — including `get_spell` — a
bare `difficulty` argument is a `MapDifficulty` row id. Here it is a raw
`SpellXSpellVisual.DifficultyID`, used to filter the variant rows directly.
The two id spaces are not interchangeable: `SpellXSpellVisual.DifficultyID` is
a byte, so no `MapDifficulty` row id of 256 or more can ever match a row. Feed
it the value `get_spell` reports as `visual.difficulty`, or one of the values
it lists in `variantsAtOtherDifficulties` — not the `difficulty` you passed to
`get_spell`. Pass `-1` for every difficulty; `0` is a real difficulty id, not a
wildcard. Kits are capped at 500 across the whole response, not per variant.

`get_spell`'s `visual` section reports the same chain through the same
resolver, filtered to `get_spell`'s own `difficulty` argument like the rest of
that document. That argument names a `MapDifficulty` row, and that row's
`DifficultyID` is the id `SpellXSpellVisual` is keyed by, so `get_spell`
translates it and echoes the translated value back as `visual.difficulty`;
leaving the argument unset means difficulty 0. `get_spell`'s `json` and `text`
formats resolve the section with one shared call, so they report the same
visuals for the same arguments.

Two states that would otherwise look identical to "this spell has no visual"
are reported explicitly, by all three renderings:

- When any of the six stores the walk needs (`SpellXSpellVisual`,
  `SpellVisual`, `SpellVisualEvent`, `SpellVisualMissile`, `SpellVisualKit`,
  `SpellVisualKitEffect`) did not load, the `json` section reports
  `storeUnloaded: true` instead of a variant list, the `text` section prints
  `Visual chain unavailable -- these stores did not load: ...`, and
  `get_spell_visual_chain` returns `store_not_loaded` naming the missing files.
  All three branch on one answer computed in one place, so they cannot disagree
  about it. `SpellMissileMotion` is read too but is not gated on: ids it cannot
  resolve are already listed in `unresolvedReferences`.
- When the difficulty filter empties the list but the spell has variants
  elsewhere, `variantsAtOtherDifficulties` names those difficulties — in
  `get_spell`'s section and in `get_spell_visual_chain` alike. An empty
  `variants` with no such key means the spell has no `SpellXSpellVisual` rows
  at all. Use `get_spell_visual_chain(difficulty: -1)` to see every difficulty
  in one call.

`search_spells`, `query_db2`, `query_world_db`, `list_enum`,
`get_spell_triggers` and `compare_spells` cap results at 500 rows and report
whether they truncated.

`search_spells`, `query_db2` and `list_enum` return that page in one shared
envelope: `rows` with `count` (rows **in this response**), `totalMatches` (how
many matched in total), `offset`, `limit` (the ceiling actually applied, not
the value asked for) and `truncated`. Page past a truncated result by raising
`offset`.

`get_spell_db_overrides` caps at 500 rows **per table**, not per call. It reads
seven `spell_*` tables in one call and applies the cap to each independently,
so a single response can carry up to 3500 rows; `truncatedTables` names the
tables that hit the cap. A running budget shared across the seven is not
implemented.

`list_db2` is deliberately uncapped. It is the discovery entry point — the one
tool whose job is to report what exists — and it takes no `offset`, so a cap
would conceal stores with no way to page to them. Its rows are a name and two
flags each, not data rows.

## Known limitations

- SpellWork models 55 of the 715 `.db2` files. `get_db2_row` and `query_db2`
  work on the modelled ones; the other 660 are listed by `list_db2` but return
  `store_not_modelled`, because their fields are only available positionally.
- The visual chain resolves against the layouts recorded in
  `LayoutVerification`. Those layouts were measured against one client build;
  a `.db2` whose layout hash or field count differs from the recorded one is
  reported by `server_info` under `layoutMismatches`. Check that key before
  trusting visual output from a client build other than the one these
  structures were written against — a mismatched layout decodes into
  plausible-looking but wrong ids.
- Client data loads once per process. There is no reload; restart the server
  after changing the DBC path.
