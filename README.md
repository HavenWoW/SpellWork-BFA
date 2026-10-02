# SpellWork

[![CI](https://github.com/HavenWoW/SpellWork/actions/workflows/ci.yml/badge.svg)](https://github.com/HavenWoW/SpellWork/actions/workflows/ci.yml)
[![Coverity Scan](https://img.shields.io/coverity/scan/33228.svg)](https://scan.coverity.com/projects/spellwork)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

SpellWork browses and displays information about World of Warcraft spells: the
decoded spell record, its effects, auras, targeting, scaling, proc conditions
and visual chain, read straight from client data and, optionally, a
world database.

## Client support

This branch tracks **5.4.8 (build 18414)**, the Mists of Pandaria client. It
reads the client's `.dbc` and `.db2` files directly.

Structures are version-specific: data from another client build is reported by
`server_info` under `layoutMismatches` (expected and actual field count and
record size) rather than silently misread. For other client versions, see the
other branches of this repository and
[upstream TrinityCore/SpellWork](https://github.com/TrinityCore/SpellWork).

## What's in here

| Project | Target | Purpose |
| --- | --- | --- |
| `SpellWork.Core` | netstandard2.0 | DBC/DB2 loading, world DB access, the spell model, rendering and visual-chain resolution |
| `SpellWork.Mcp` | net9.0 | Read-only [Model Context Protocol](docs/mcp-server.md) server exposing the data to an AI agent |
| `SpellWork.Core.Tests`, `SpellWork.Mcp.Tests` | net9.0 | xUnit suites, including golden-output rendering and layout verification |

The WinForms desktop UI is not part of this branch.

## Client data

Client data is **not** redistributable and is not included here. Point the
server at the folders holding the client's files:

- **DBC folder** (`SPELLWORK_DBC_PATH`), holding the `.dbc` files. An optional
  locale subfolder (default `enUS`) is used when present; otherwise the folder
  itself is read.
- **DB2 folder** (`SPELLWORK_DB2_PATH`, optional), holding the `.db2` files.

When a table exists as both `X.db2` and `X.dbc`, the `.db2` wins. A world
database connection is optional; without it the database tools report
"not configured" and everything else still works.

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

`DBFileReaderLib.dll` is vendored under `packages/` because it is not published
on NuGet and every project references it by `HintPath`. It is tracked
deliberately, past the `**/packages/*` ignore rule.

## MCP server

`SpellWork.Mcp` exposes this data to an AI agent over the Model Context
Protocol, read-only, as fourteen tools. See
**[docs/mcp-server.md](docs/mcp-server.md)** for build, registration and
configuration.

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
