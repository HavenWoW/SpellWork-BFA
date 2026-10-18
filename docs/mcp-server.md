# SpellWork MCP server (5.5.4.70449)

A read-only MCP server exposing SpellWork's **5.5.4.70449** spell data to an
agent. Data is read from **WDC5** `.db2` files through the **DBCD 2.3.1**
library (not DBFileReaderLib). The server reports itself as
`SpellWork 5.5.4 (70449)`. This is the bring-up variant: three tools, eight
stores. See `server_info` first when anything looks wrong.

## Build

```
dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o out/mcp554
```

## Register

```
claude mcp add --scope user spellwork-554 -e SPELLWORK_DBC_PATH=F:/WorkDir/mop-clientdata/70449/dbc -e SPELLWORK_LOCALE=enUS -- dotnet /absolute/path/to/out/mcp554/SpellWork.Mcp.dll
```

`--scope user` makes it available in every project. The WoWDBDefs `.dbd`
definitions are bundled into the publish (the `defs/` folder), so
`SPELLWORK_DEFS_PATH` is optional and only needed to override them with a live
WoWDBDefs checkout.

## Configuration

Set these on the MCP server entry (`claude mcp add -e NAME=value`, spelled
`--env NAME=value` in full, or the `env` block in the config file).

| Variable | Required | Meaning |
| --- | --- | --- |
| `SPELLWORK_DBC_PATH` | yes | Folder holding the locale subfolder of `.db2` files |
| `SPELLWORK_LOCALE` | no (`enUS`) | Locale subfolder; files load from `{DBC_PATH}/{LOCALE}` |
| `SPELLWORK_DEFS_PATH` | no | WoWDBDefs `.dbd` folder; defaults to the bundled `defs/` |
| `SPELLWORK_BUILD` | no (`5.5.4.70449`) | Build used to pick the layout, format `x.x.x.xxxxx` |

`SPELLWORK_DB2_PATH` and `SPELLWORK_GT_PATH` are accepted but unused in this
bring-up. The MySQL / world-database variables are not used: the world-db
tools are not part of this variant.

A misconfigured server still starts and still answers. A bad path produces a
structured error from the data tools naming the variable to fix, not a dead
process.

Nothing in this server writes to `.db2` files.

## Tools

Exactly three tools in this bring-up.

| Tool | What it does |
| --- | --- |
| `server_info` | Configured paths, build and locale, `clientDataLoaded`, `spellCount` (about 98183), `storeCounts` for the 8 stores, and `layoutMismatches` |
| `get_spell` | The full decoded view of one spell, as JSON or rendered text. `difficulty` is validated against the loaded `Difficulty` store |
| `search_spells` | Find spells by name, family, aura/effect/target type, attribute or school; paged |

Notes:

- Effect values render as raw base points. Scaling is deferred.
- Attribute words 14-16 have no named flags yet.
- Spells are base rows only; difficulty overrides are not applied.

## Not in this variant

Later milestones, deliberately absent here:

- The other 11 tools: `get_spell_triggers`, `compare_spells`, `decode_flags`,
  `list_enum`, `calculate_spell_scaling`, `list_db2`, `get_db2_row`,
  `query_db2`, `query_world_db`, `get_spell_db_overrides`,
  `get_spell_visual_chain`.
- Spell scaling and the visual chain.
- Difficulty overrides (base rows only).
- The world database, items and skills.
- The other ~45 stores.

## Known limitations

- The variant models 8 stores: `SpellName`, `Spell`, `SpellMisc`,
  `SpellEffect`, `SpellClassOptions`, `SpellDuration`, `SpellRange` and
  `Difficulty`.
- A `.db2` whose layout hash or field count differs from the recorded one is
  reported by `server_info` under `layoutMismatches`. Check that key before
  trusting output from a client build other than 5.5.4.70449; a mismatched
  layout decodes into plausible-looking but wrong ids.
- Client data loads once per process. There is no reload; restart the server
  after changing the DBC path.
