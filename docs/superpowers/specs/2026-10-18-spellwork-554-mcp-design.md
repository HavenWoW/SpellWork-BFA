# SpellWork 5.5.4 (70449) MCP variant — design

**Status:** approved for bring-up milestone (2026-10-18)
**Forks:** `main` branch (the 8.3.7 / `spellwork-837` variant)
**Produces:** `out/mcp554`, registered as the MCP server `spellwork-554`, alongside `spellwork-548` and `spellwork-837`.

## What this builds (bring-up milestone)

A SpellWork MCP variant that loads the MoP-Classic **5.5.4.70449** client data —
809 **WDC5** `.db2` files at `F:\WorkDir\mop-clientdata\70449\dbc\enUS` — and serves
three tools end-to-end: `server_info`, `get_spell` (json + text), and
`search_spells`. Registered as `spellwork-554`.

It is a **bring-up**, deliberately a subset. It builds:

- A new DB2 reader layer in `SpellWork.Core` built on **DBCD 2.3.1** (reads WDC5),
  replacing `DBFileReaderLib` (which tops out at WDC3 and cannot open these files).
- A **DBCD→POCO mapper** so the existing structure-class / `Storage<T>` / `SpellInfo`
  model survives the reader change.
- Only the stores needed for the three tools: **SpellName, Spell, SpellMisc,
  SpellEffect, SpellDuration, SpellRange, SpellClassOptions, Difficulty**.
- `SpellInfo` trimmed to the fields those stores feed: name, description,
  school, the 17 attribute words, per-effect data, and spell family.

## What this explicitly does NOT build (later milestones)

- The other 11 MCP tools: `get_spell_triggers`, `compare_spells`, `decode_flags`,
  `list_enum`, `calculate_spell_scaling`, `list_db2`, `get_db2_row`, `query_db2`,
  `query_world_db`, `get_spell_db_overrides`, `get_spell_visual_chain`.
- **Spell scaling** (`GtSpellScaling` game table + `SpellScaling` db2) and the
  `scaling`/`context` sections of `get_spell`.
- The **visual chain** stores (`SpellVisual*`, `SpellXSpellVisual`, missiles, kits).
- **Difficulty overrides** — bring-up reads base (DifficultyID 0) rows only, as
  `main` does; the difficulty-indexed read path is a later milestone.
- The world-database (MySQL) tools, items (`Item`/`ItemSparse`), skills, and the
  remaining ~45 stores. Their structure classes are removed from this branch and
  re-introduced per table, with correct 70449 layouts, as each milestone wires them.

## Decisions that are expensive to change later

1. **Fork base = `main` (8.3.7), not `5.4.8`.** 554 is a modern WDC5 client whose
   schema (a separate `SpellName` table holding names, locstrings, the modern
   `SpellMisc`/`SpellEffect` shapes, modern enums) matches 8.3.7, not the 5.4.8
   WDB2 world. Approved by the user 2026-10-18.

2. **Reader = DBCD 2.3.1 (nuget.org), DBFileReaderLib removed.** Proven to read all
   four core WDC5 tables for build `5.5.4.70449` (names resolved by WDC5 layout-hash
   fallback, since the .dbd's 5.5.4 line stops at 70032). Approved 2026-10-18.

3. **`SpellWork.Core` retargets `netstandard2.0` → `net9.0`.** DBCD's floor is
   `netstandard2.1`, so Core cannot reference it at 2.0. Core's only consumers
   (`SpellWork.Mcp`, the test projects) are already `net9.0`, so the bump is free.
   The alternative (isolating DBCD in a separate net assembly behind an abstraction)
   was rejected as needless indirection for a single-consumer library.

4. **DBCD→POCO mapper preserves the structure-class model.** DBCD returns
   name-keyed `DBCDRow`s, not attribute-decorated POCOs. Rather than rewrite
   `SpellInfo` and the tools to a dynamic model, a mapper populates the existing
   `*Entry` POCOs from a `IDBCDStorage`. This localizes the reader change to the
   loader + mapper and keeps the large downstream surface intact. The mapper is the
   contract everything else depends on, so its rules are fixed here:
   - **Column resolution** per POCO field: `[Column("exact_name")]` if present;
     else the field name; else the field name + `"_lang"` (locstrings surface as
     `Name_lang`, `DisplayName_lang`, …).
   - **Type coercion**: DBCD column types are mixed (`Int16`, `Byte`, `SByte`,
     `UInt16`, `UInt32`, `Single`, and typed arrays). The mapper coerces each value
     into the POCO field's declared type (element-wise for arrays) via a single
     `Coerce(object, Type)` helper, so structure classes may use convenient widened
     types. Coercion must never throw on a legal narrowing/widening.
   - **Id**: the `[Index(true)]` field is set from the DBCD row key; the store is
     keyed by that id.

5. **Layout verification by column-name set, not record size or layout hash.** WDC5
   columns are named (from the .dbd), so `LayoutVerification` checks that every
   expected column name for a modelled store is present in DBCD's `AvailableColumns`
   and that the field count matches. This replaces 5.4.8's `(FieldsCount, RecordSize)`
   and `main`'s `(LayoutHash, FieldsCount)` schemes. Mismatches are recorded, not
   thrown, and surfaced in `server_info.layoutMismatches`.

6. **Difficulty ids are data-driven.** The valid-difficulty-id set and the base/raid
   difficulty semantics come from the loaded `Difficulty` store (20 rows for 70449),
   not a hardcoded list. `get_spell`'s bad-difficulty error lists the loaded ids.

## Global constraints

- All projects target **`net9.0`**. Core adds `<PackageReference Include="DBCD" Version="2.3.1" />`; the `DBFileReaderLib` HintPath reference is removed from `SpellWork.Core.csproj`, `SpellWork.Mcp.csproj`, and both test csprojs.
- `DBFileReaderLib` is not referenced anywhere on this branch. Core provides its own `Storage<T>`, `IndexAttribute`, `CardinalityAttribute`, and a new `ColumnAttribute`.
- stdout carries only MCP JSON-RPC; all logging goes to stderr (unchanged from `main`).
- Build string format is `x.x.x.xxxxx`; the default is `5.5.4.70449`.
- The server stays read-only. Nothing writes to client files.

## Configuration (env, bring-up)

| Variable | Required | Meaning |
| --- | --- | --- |
| `SPELLWORK_DBC_PATH` | yes | Folder holding the locale subfolder of `.db2` files, e.g. `F:\WorkDir\mop-clientdata\70449\dbc` |
| `SPELLWORK_LOCALE` | no (`enUS`) | Locale subfolder; the `.db2` files load from `{DBC_PATH}\{LOCALE}` |
| `SPELLWORK_DEFS_PATH` | no | WoWDBDefs `definitions/` dir (the `.dbd` files). Defaults to the `defs/` folder bundled into the publish |
| `SPELLWORK_BUILD` | no (`5.5.4.70449`) | Build string passed to `DBCD.Load` |
| `SPELLWORK_DB2_PATH` | no, unused | Accepted for compatibility; all 70449 stores are WDC5 `.db2` in the locale dir |
| `SPELLWORK_GT_PATH` | no, unused in bring-up | Reserved for the scaling milestone (`{GT_PATH}\SpellScaling.txt`) |

The bundled `defs/` means the published server is self-contained; `SPELLWORK_DEFS_PATH`
overrides it when pointing at a live WoWDBDefs checkout.

## Reference: 70449 WDC5 layouts (bring-up stores)

Column names, runtime types and array cardinalities, read from the client via DBCD
2.3.1 on 2026-10-18 (probe in the session scratchpad). Deltas vs `main`'s 8.3.7
structures are called out.

- **SpellName** (98,183): `ID:int`, `Name_lang:string`.
- **Spell** (98,183): `ID:int`, `NameSubtext_lang:string`, `Description_lang:string`, `AuraDescription_lang:string`.
- **SpellMisc** (103,075): `ID:int`, `Attributes:int[17]` (**was [14]**), `DifficultyID:short` (**was byte**), `CastingTimeIndex:ushort`, `DurationIndex:ushort`, `RangeIndex:ushort`, `SchoolMask:byte`, `Speed:float`, `LaunchDelay:float`, `MinDuration:float`, `SpellIconFileDataID:int`, `ActiveIconFileDataID:int`, `ContentTuningID:int`, `ShowFutureSpellPlayerConditionID:int` (**new**), `SpellID:int`.
- **SpellEffect** (142,837): `ID:int`, `DifficultyID:short`, `EffectIndex:int`, `Effect:uint`, `EffectAmplitude:float`, `EffectAttributes:int`, `EffectAura:short`, `EffectAuraPeriod:int`, `EffectBasePoints:int` (**int; separate** `EffectBasePointsF:float`), `EffectBonusCoefficient:float`, `EffectChainAmplitude:float`, `EffectChainTargets:int`, `EffectDieSides:int`, `EffectItemType:int`, `EffectMechanic:int`, `EffectPointsPerResource:float`, `EffectPos_facing:float` (**underscore — needs `[Column]`**), `EffectRealPointsPerLevel:float`, `EffectTriggerSpell:int`, `BonusCoefficientFromAP:float`, `PvpMultiplier:float`, `Coefficient:float`, `Variance:float`, `ResourceCoefficient:float`, `GroupSizeBasePointsCoefficient:float`, `EffectBasePointsF:float`, `Field_5_5_4_67090_025:int` (**build-specific, unnamed**), `EffectMiscValue:int[2]`, `EffectRadiusIndex:uint[2]`, `EffectSpellClassMask:int[4]`, `ImplicitTarget:short[2]`, `SpellID:int`.
- **SpellClassOptions** (8,705): `ID:int`, `SpellID:int`, `ModalNextSpell:uint`, `SpellClassSet:byte`, `SpellClassMask:int[4]`.
- **SpellDuration** (212): `ID:int`, `Duration:int`, `DurationPerLevel:uint`, `MaxDuration:int`.
- **SpellRange** (107): `ID:int`, `DisplayName_lang:string`, `DisplayNameShort_lang:string`, `Flags:int`, `RangeMin:float[2]`, `RangeMax:float[2]`.
- **Difficulty** (20): `ID:int`, `Name_lang:string`, `InstanceType:byte`, `OrderIndex:byte`, `OldEnumValue:sbyte`, `FallbackDifficultyID:short`, `MinPlayers:byte`, `MaxPlayers:byte`, `Flags:int`, `ItemContext:byte`, `ToggleDifficultyID:short`, `GroupSizeHealthCurveID:ushort`, `GroupSizeDmgCurveID:ushort`, `GroupSizeSpellPointsCurveID:ushort`, `Field_1_15_4_56400_013:int`.

The plan is at `docs/superpowers/plans/2026-10-18-spellwork-554-mcp.md`.
