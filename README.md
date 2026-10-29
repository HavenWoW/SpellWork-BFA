# SpellWork

[![CI](https://github.com/HavenWoW/SpellWork/actions/workflows/ci.yml/badge.svg)](https://github.com/HavenWoW/SpellWork/actions/workflows/ci.yml)
[![Coverity Scan](https://img.shields.io/coverity/scan/33228.svg)](https://scan.coverity.com/projects/spellwork)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

SpellWork browses and displays information about World of Warcraft spells: the
decoded spell record, its effects, auras, targeting, scaling, proc conditions
and visual chain, read straight from client data and, optionally, a
world database.

## Client support

This branch tracks **CoA 3.3.5a (build 12340)**: the Wrath of the Lich King
client as customised by the CoA AzerothCore fork (`azerothcore-wotlk-coa`). It
reads the client's `.dbc` files directly; 3.3.5a has no `.db2` files.

Structures follow the CoA server's own `DBCfmt.h` / `DBCStructure.h`, so the
CoA-specific auras, effects and classes decode by name. Data from another
client build is reported by `server_info` under `layoutMismatches` (expected
and actual field count and record size) rather than silently misread. For
other client versions, see the other branches of this repository (`5.4.8`,
`main`) and [upstream TrinityCore/SpellWork](https://github.com/TrinityCore/SpellWork).

## Register with Claude Code

`SpellWork.Mcp` is a read-only [Model Context Protocol](docs/mcp-server.md)
server. Registered with Claude Code, it lets an agent look up, search, decode
and compare CoA spells, their visual chains and their world-database
overrides. Setup takes five minutes.

### 1. Prerequisites

- **.NET 9 runtime** (the SDK also works). Check with:

  ```
  dotnet --list-runtimes
  ```

  The list must contain a `Microsoft.NETCore.App 9.0.x` line. If it does not,
  install the .NET 9 runtime from <https://dotnet.microsoft.com/download/dotnet/9.0>.
- **The CoA client `dbc` folder.** Client data is not redistributable and is
  not shipped with SpellWork; extract it yourself. In a CoA server checkout it
  is `<azerothcore-wotlk-coa>/env/dist/data/dbc` (the folder holding
  `Spell.dbc`).
- **Claude Code** (`claude --version`).

### 2. Get the server

Either:

- **Download** `SpellWork.Mcp-coa.zip` from the
  [releases page](https://github.com/HavenWoW/SpellWork/releases) (tags
  `vcoa-*`) and unzip it to a stable folder, for example `C:/SpellWork/mcpcoa`.
  The folder must contain `SpellWork.Mcp.dll`. Do not leave it in Downloads:
  the registration points at this path.
- **or build from source** (needs the .NET 9 SDK), from a checkout of the
  `coa` branch:

  ```
  dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o out/mcpcoa
  ```

  The server is then `out/mcpcoa/SpellWork.Mcp.dll` inside the checkout.

### 3. Register

`--scope user` makes the server available in every project. Use forward
slashes in paths, also on Windows. Replace the two example paths with your
`dbc` folder and your `SpellWork.Mcp.dll`; only the line-continuation
character differs between shells.

PowerShell (backtick):

```powershell
claude mcp add --scope user spellwork-coa `
  -e SPELLWORK_DBC_PATH=C:/azerothcore-wotlk-coa/env/dist/data/dbc `
  -e SPELLWORK_LOCALE=enUS `
  -- dotnet C:/SpellWork/mcpcoa/SpellWork.Mcp.dll
```

bash / zsh / Git Bash (backslash):

```bash
claude mcp add --scope user spellwork-coa \
  -e SPELLWORK_DBC_PATH=C:/azerothcore-wotlk-coa/env/dist/data/dbc \
  -e SPELLWORK_LOCALE=enUS \
  -- dotnet C:/SpellWork/mcpcoa/SpellWork.Mcp.dll
```

cmd.exe (caret):

```bat
claude mcp add --scope user spellwork-coa ^
  -e SPELLWORK_DBC_PATH=C:/azerothcore-wotlk-coa/env/dist/data/dbc ^
  -e SPELLWORK_LOCALE=enUS ^
  -- dotnet C:/SpellWork/mcpcoa/SpellWork.Mcp.dll
```

The general form is:

```
claude mcp add --scope user spellwork-coa -e SPELLWORK_DBC_PATH=<dbc path> -e SPELLWORK_LOCALE=enUS -- dotnet <path>/SpellWork.Mcp.dll
```

Everything before `--` is for `claude`; everything after it is the command
that starts the server.

**Optional: world database.** To enable `query_world_db`,
`get_spell_db_overrides` and item names, add the world-database variables
(bash shown; use the same continuation character as above for your shell):

```bash
claude mcp add --scope user spellwork-coa \
  -e SPELLWORK_DBC_PATH=C:/azerothcore-wotlk-coa/env/dist/data/dbc \
  -e SPELLWORK_LOCALE=enUS \
  -e SPELLWORK_MYSQL_HOST=127.0.0.1 \
  -e SPELLWORK_MYSQL_PORT=3306 \
  -e SPELLWORK_MYSQL_USER=<user> \
  -e SPELLWORK_MYSQL_PASS=<password> \
  -e SPELLWORK_MYSQL_WORLD_DB=acore_world \
  -- dotnet C:/SpellWork/mcpcoa/SpellWork.Mcp.dll
```

`query_world_db` runs SQL the agent composed. The server refuses anything but
a single `SELECT`, but connect as a MySQL user that can **only** `SELECT`:

```sql
CREATE USER 'spellwork_ro'@'127.0.0.1' IDENTIFIED BY '<password>';
GRANT SELECT ON acore_world.* TO 'spellwork_ro'@'127.0.0.1';
```

(The host part must match how the server connects: `'127.0.0.1'` for
`SPELLWORK_MYSQL_HOST=127.0.0.1`, `'localhost'` for `localhost`.) The
password ends up in Claude Code's config file (`~/.claude.json` for user
scope), which is one more reason to use this read-only account and not `root`.

### 4. Verify

```
claude mcp list
```

must show a line like

```
spellwork-coa: dotnet C:/SpellWork/mcpcoa/SpellWork.Mcp.dll - ✔ Connected
```

`claude mcp get spellwork-coa` shows the full entry, including the variables.
Then, inside a Claude Code session, `/mcp` lists `spellwork-coa` with its
fourteen tools. Ask Claude to *"call spellwork-coa server_info"* and check:

- `clientBuild` is `12340` and `variant` is `coa`;
- `layoutMismatches` is empty (`[]`);
- `clientDataLoaded` is `true` and `spellCount` is in the hundreds of thousands
  (209,510 on the reference data);
- with the world database configured, `mysqlReachable` is `true`.

The first call loads the client data, which takes a few seconds.

### 5. Alternative: per-project `.mcp.json`

To share the server with everyone working in one repository instead of
registering it for yourself, put a `.mcp.json` at the project root (Claude
Code asks each user to approve project servers on first use):

```json
{
  "mcpServers": {
    "spellwork-coa": {
      "command": "dotnet",
      "args": ["C:/SpellWork/mcpcoa/SpellWork.Mcp.dll"],
      "env": {
        "SPELLWORK_DBC_PATH": "C:/azerothcore-wotlk-coa/env/dist/data/dbc",
        "SPELLWORK_LOCALE": "enUS"
      }
    }
  }
}
```

`claude mcp add --scope project ...` writes the same file. Keep MySQL
passwords out of a committed `.mcp.json`.

### 6. Troubleshooting

- **`✘ Failed to connect`** — the server process did not start. Usually the
  `SpellWork.Mcp.dll` path is wrong (check it with `claude mcp get spellwork-coa`)
  or `dotnet` is not on the `PATH` Claude Code sees (`dotnet --list-runtimes`
  in a fresh terminal; use the full path to `dotnet.exe` as the command if
  needed), or the .NET 9 runtime is missing.
- **Connected, but tools answer `dbc_path_missing`, `dbc_path_not_found` or
  `no_data_loaded`** — `SPELLWORK_DBC_PATH` is unset, wrong, or names a folder
  without the `.dbc` files. A bad data path does not stop the
  server; `server_info` reports it under `clientDataError` naming the
  variable. Point it at the folder that directly contains `Spell.dbc`.
- **`layoutMismatches` is not empty** — the `dbc` folder is from another
  client build (stock 3.3.5a, or another expansion). Use the CoA client's
  files.
- **Name clash with other SpellWork servers.** The `spellwork-548`,
  `spellwork-554` and `spellwork-837` builds (other branches) can be
  registered beside this one; every registration needs its own name and its
  own `SpellWork.Mcp.dll` folder. `claude mcp add` refuses a name that already
  exists in that scope.
- **`mysql_not_configured` / `connection_failed`** — the MySQL tools need
  `SPELLWORK_MYSQL_HOST`, `_USER` and `_WORLD_DB` together; `server_info`
  shows `mysqlReachable` and `mysqlError`.
- **Remove:** `claude mcp remove spellwork-coa --scope user`.
- **Change a setting:** remove it and run `claude mcp add` again.
- **Update:** download the new release, unzip it over the same folder (or
  re-run `dotnet publish`), then restart Claude Code. The registration stays
  valid while the path is unchanged. The server loads client data once per
  process, so changed `dbc` files also need a restart.

The full configuration and tool reference is in
[docs/mcp-server.md](docs/mcp-server.md).

## What's in here

| Project | Target | Purpose |
| --- | --- | --- |
| `SpellWork.Core` | netstandard2.0 | DBC loading, world DB access, the spell model, rendering and visual-chain resolution |
| `SpellWork.Mcp` | net9.0 | Read-only [Model Context Protocol](docs/mcp-server.md) server exposing the data to an AI agent |
| `SpellWork.Core.Tests`, `SpellWork.Mcp.Tests` | net9.0 | xUnit suites, including golden-output rendering and layout verification |

The WinForms desktop UI is not part of this branch.

## Client data

Client data is **not** redistributable and is not included here. Point the
server at the **DBC folder** (`SPELLWORK_DBC_PATH`) holding the client's
`.dbc` files. 3.3.5a keeps all sixteen locales' strings inside each file, so
`SPELLWORK_LOCALE` (default `enUS`) picks a string slot, not a subfolder.
`SPELLWORK_DB2_PATH` and `SPELLWORK_GT_PATH` are accepted and ignored.

A world database connection (the CoA `acore_world` schema) is optional;
without it the database tools report "not configured" and everything else
still works.

## Building from source

Requires the **.NET 9 SDK**.

```
dotnet build SpellWork.sln -c Release
dotnet test SpellWork.Core.Tests/SpellWork.Core.Tests.csproj -c Release --nologo
dotnet test SpellWork.Mcp.Tests/SpellWork.Mcp.Tests.csproj -c Release --nologo
```

Tests that require real client data or MySQL skip themselves when none is
configured, so a clean checkout reports a large skip count rather than
failures. Only the data-independent tests (SQL guarding, flag decoding, JSON
shape, layout constants, MCP tool contracts) run everywhere.
Environment variables that enable the data tests: `SPELLWORK_DBC_PATH`, `SPELLWORK_LOCALE`; the MySQL tests: `SPELLWORK_TEST_MYSQL_HOST`, `_PORT`, `_USER`, `_PASS`, `_WORLD_DB`.

`DBFileReaderLib.dll` is vendored under `packages/` because it is not published
on NuGet and every project references it by `HintPath`. It is tracked
deliberately, past the `**/packages/*` ignore rule.

## MCP server

`SpellWork.Mcp` exposes this data to an AI agent over the Model Context
Protocol, read-only, as fourteen tools. Registration is described
[above](#register-with-claude-code); **[docs/mcp-server.md](docs/mcp-server.md)**
is the full configuration and tool reference.

> **Security:** `query_world_db` executes SQL composed by the agent. The server
> rejects anything that is not a single `SELECT`, but that parser is a guard,
> not a guarantee — connect as a MySQL user with only `SELECT` granted.

## Static analysis

[Coverity Scan project 33228](https://scan.coverity.com/projects/spellwork)
analyses this codebase. `.github/workflows/coverity.yml` builds under
`cov-build` and submits, weekly and on demand — not per push, because Coverity
rate-limits submissions.

The workflow skips itself unless `COVERITY_SCAN_TOKEN` and
`COVERITY_SCAN_EMAIL` exist as repository secrets. Both come from the project's
Coverity dashboard (*Project Settings* for the token, *Submit Build* for the
exact `project=` value the upload expects).

## History

SpellWork is a tool originally created by Chestr (also known as DiSlord); the
C# rebuild is [TrinityCore/SpellWork](https://github.com/TrinityCore/SpellWork),
which this repository is derived from.

Thanks to:

- Chestr — for the idea and the released source of the original spell_work
- TOM_RUS — for code samples
- Shauren — for updating it for TrinityCore

## License

[MIT](LICENSE). Copyright (c) 2011-2017 TrinityCore; unattributed 2010-2012
Chestr, LordJZ, Konctantin.
