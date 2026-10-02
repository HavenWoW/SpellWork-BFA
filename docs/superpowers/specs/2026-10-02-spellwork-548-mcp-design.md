# SpellWork 5.4.8 MCP Server — Design

Branch: `5.4.8` (cut from `main` at `875e92a`). Client: 5.4.8 (build 18414).
Reference data: `C:\MoP\MOPPREMIUM\Repack` (`dbc/`, `db2/`) and the repack's
MySQL `world` schema.

## 1. Goal

The fourteen MCP tools that `main` serves for 8.3.7 data, served for 5.4.8
data from a separate build on a separate branch. An agent registers both
(`spellwork-837`, `spellwork-548`) and talks to each the same way.

Success means: every tool answers against the reference data, layout
verification reports zero mismatches on it, `get_spell_visual_chain` resolves
the full chain down to effect model names, and the data-independent test suite
passes in CI.

## 2. Scope

**Built:** `SpellWork.Core` and `SpellWork.Mcp` ported to the 5.4.8 data
model, plus `SpellWork.Core.Tests` and `SpellWork.Mcp.Tests`.

**Not built:**

- The WinForms project. It is removed from this branch's `SpellWork.sln` and
  directory; CI builds Core, Mcp and the tests only.
- `DBCache.bin` hotfix support. `SPELLWORK_HOTFIX_CACHE` is dropped from config.
- Any shared code with `main`. Fixes travel between branches by cherry-pick.
- Models for `.dbc`/`.db2` files no tool reads. `list_db2` still lists them.

## 3. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | Branch from our `main`, not from `upstream/5.4.8` | Keeps the Core/Mcp infrastructure, tests and CI. Upstream supplies only the data model. |
| D2 | Structures come from upstream 5.4.8 SpellWork, re-checked against WoWDBDefs and the reference files | 37 of 38 upstream structures matched on field count; the visual tables did not (see §5). |
| D3 | A `difficulty` argument is a `Difficulty.dbc` id (0 = base) | 5.4.8 keys `SpellEffect`, `SpellPower` and `SpellTargetRestrictions` by that id directly. 5.4.8 has no MapDifficulty-to-Difficulty translation. |
| D4 | Visual chain starts from `SpellMisc.SpellVisual[0..1]` | 5.4.8 has no `SpellXSpellVisual`. Visuals aren't per difficulty. |
| D5 | When both `X.dbc` and `X.db2` exist, load `.db2` | The reference `dbc/` folder has stale 5.3-layout `SpellVisual.dbc` and `SpellVisualEffectName.dbc` beside the correct 5.4 `.db2` files. |
| D6 | The locale subdirectory is optional | The reference data keeps the default locale at the root of `dbc/` and only `ruRU` in a subfolder. |
| D7 | Layout verification uses field count + record size | WDBC/WDB2 headers have no layout hash. |

## 4. Architecture

Unchanged from `main`. Core loads stores into static `Storage<T>` properties
on `DBC` and builds `SpellInfoStore`. Mcp wraps it in `SpellDataService` and
exposes tools through `ModelContextProtocol.Server`. The changes are inside
those boxes:

```
DBC.LoadAsync ──► per-table file resolution (.db2 > .dbc, locale optional)
              ──► DBFileReaderLib (WDBC / WDB2)
              ──► LayoutVerification (fields + record size)
              ──► BuildSpellInfoStore (Spell.dbc FKs + per-spell indexes)
SpellInfo ──► SpellInfoJsonSerializer / PlainTextSpellInfoWriter
SpellVisualChainResolver (5.4.8 chain) ──► get_spell visual section + get_spell_visual_chain
```

## 5. Data model

### 5.1 Stores

Spell tables loaded from `dbc/`: Spell, SpellMisc, SpellEffect,
SpellEffectScaling, SpellAuraOptions, SpellAuraRestrictions,
SpellCastingRequirements, SpellCastTimes, SpellCategories,
SpellClassOptions, SpellCooldowns, SpellDescriptionVariables, SpellDuration,
SpellEquippedItems, SpellInterrupts, SpellLevels, SpellPower, SpellRadius,
SpellRange, SpellRuneCost, SpellScaling, SpellShapeshift,
SpellTargetRestrictions, SpellTotems, SpellProcsPerMinute.

Other tables loaded from `dbc/`: AreaGroup, AreaTable, Difficulty,
OverrideSpellData, ScreenEffect, SkillLine, SkillLineAbility, RandPropPoints,
gtSpellScaling, SpellChainEffects. (Map, MapDifficulty, SpellCategory and
SpellProcsPerMinuteMod are not modelled; nothing reads them. `list_db2` still
lists them.)

WDB2 tables (the `.db2` file wins, D5): Item, Item-sparse, SpellReagents,
SpellMissile, SpellMissileMotion, SpellVisual, SpellVisualKit,
SpellVisualMissile, SpellVisualEffectName, SpellVisualKitModelAttach,
SpellVisualKitAreaModel.

Removed from `main`'s list: SpellName, SpellXSpellVisual, SpellVisualEvent,
SpellVisualKitEffect, ContentTuning*, ExpectedStat*, ItemEffect,
AreaGroupMember, SpellXDescriptionVariables, SpellReagentsCurrency.

### 5.2 Recorded layouts (reference data)

| Table | Fields | Record size |
|---|---|---|
| Spell | 25 | 100 |
| SpellEffect | 30 | 120 |
| SpellMisc | 26 | 104 |
| SpellVisual (.db2) | 30 | 120 |
| SpellVisualKit (.db2) | 39 | 156 |
| SpellVisualMissile (.db2) | 17 | 68 |
| SpellVisualEffectName (.db2) | 9 | 36 |
| SpellVisualKitModelAttach (.db2) | 15 | 60 |
| SpellVisualKitAreaModel (.db2) | 7 | 28 |
| SpellMissile (.db2) | 15 | 60 |
| SpellMissileMotion (.db2) | 5 | 20 |
| SpellChainEffects | 58 | 220 |

Every modelled table gets an entry. The values for tables not listed here are
read from the reference data's headers when the structure is written, and only
after the structure's field list has been checked against WoWDBDefs. `LayoutVerification.Expected`
becomes `(int FieldsCount, int RecordSize)`. `LayoutMismatch.Describe()` names
the file actually opened, extension included, so a stale `.dbc` being picked
up is visible.

### 5.3 `SpellInfo`

Built from a `SpellEntry` and its foreign keys. Spell.dbc carries the name,
rank, description and tooltip as plain strings in the loaded locale, plus ids
into the sub-tables. The id that matters for attributes, cast time, duration,
range, school and visuals is `SpellMiscId`.

Per-spell indexes, built once in `BuildSpellInfoStore`:

- `SpellEffect` grouped by `SpellId`, then by `Difficulty`, then by `Index`.
- `SpellTargetRestrictions`, `SpellAuraOptions`, `SpellCategories`,
  `SpellCooldowns`, `SpellInterrupts`, `SpellLevels` and `SpellMisc` indexed by
  `(SpellID, DifficultyID)` with the same base fallback as effects.
- `SpellPower` grouped by `SpellID` and ordered by `OrderIndex`. It has no
  difficulty column in 5.4.8.
- `SpellEffectScaling` by `SpellEffectId`. This replaces upstream's linear scan
  per effect.
- `SpellTriggerStore` from `SpellEffect.TriggerSpell`, across all difficulties.

For a requested difficulty, effects resolve as follows: for each effect index,
take the row at that difficulty if one exists, otherwise the row at
difficulty 0. That is how the 5.4.8 server resolves them. JSON reports which
difficulty each effect row actually came from.

New fields on `main`'s JSON shape: `runeCost`, effect `scaling`
(coefficient/variance/resource coefficient), `attributes` 0–13 (14 words), and
the `bonusMultiplier`/`damageMultiplier` effect fields. Fields that don't exist
in 5.4.8 (ContentTuning, ExpectedStat, SpellXSpellVisual ids) are left out
rather than set to null.

### 5.4 Enums

`SpellEnums.cs` is replaced with the 5.4.8 sets: aura types, effect types,
implicit targets, attributes 0–13, proc flags, mechanics, power types,
families, dispel types. The source is upstream 5.4.8 SpellWork's
`SpellEnums.cs`. When it disagrees with or is missing an entry that the
TrinityCore/SkyFire 5.4.8 `SharedDefines.h` / `SpellAuraDefines.h` has, those
headers win. `list_enum` and `decode_flags` serve these sets, so their tests
change with them.

### 5.5 Scaling

`calculate_spell_scaling(level, itemLevel, difficulty)`. Upstream 5.4.8 used
the Cataclysm/MoP formula: `SpellScaling` (class, min/max level, cast-time
scaling, `NerfFactor`) times the `gtSpellScaling` row for
`(class-1)*100 + level-1`, times `SpellEffectScaling` coefficient/variance.
Levels clamp to 1..90 (`MaxLevel = 90`). SkyFire_548 `CalcValue` has no item-level path; `itemLevel` is echoed with
`"itemLevelApplied": false` for every spell.

## 6. Visual chain (full)

Entry: `SpellMisc.SpellVisual[0]` and `[1]` for the spell. Each non-zero id is
a **variant**, and reports `slot: 0|1`.

```
SpellVisual
 ├─ kits: PrecastKit, CastKit, ImpactKit, StateKit, StateDoneKit, ChannelKit,
 │        CasterImpactKit, TargetImpactKit, MissileTargetingKit,
 │        InstantAreaKit, ImpactAreaKit, PersistentAreaKit   ──► SpellVisualKit
 ├─ column 14 (WoWDBDefs "MissileMotion") ── raw value only, not followed (see below)
 └─ SpellVisualMissileSetID ──► SpellVisualMissile rows (by set id)
        ├─ SpellVisualEffectNameID ──► SpellVisualEffectName
        └─ SpellMissileMotionID   ──► SpellMissileMotion

SpellVisualKit
 ├─ Head/Chest/Base/LeftHand/RightHand/Breath/LeftWeapon/RightWeapon/
 │  Special[0..2]/World effect ──► SpellVisualEffectName (name, model path)
 └─ SpellVisualKitModelAttach rows (by ParentSpellVisualKitID)
          └─ SpellVisualEffectNameID ──► SpellVisualEffectName
```

`SpellVisualKitAreaModel` is not part of the chain. In the 5.4.0.17266–
5.4.8.18414 layouts it has no parent-kit column, and none of the chain tables
above reference it. It stays modelled for `get_db2_row`/`query_db2`. The tool
documentation says this, the same way `main` documents `SpellMissile` as
unreachable.

SpellVisual column 14 is named `MissileMotion` in WoWDBDefs, but on the
reference data 384 of its 388 non-zero values miss SpellMissileMotion, and all
388 fall inside SpellVisualKit's (dense) id range. Its meaning is unconfirmed,
so the structure names it `Unk14MissileMotion` and the chain reports it per
variant as a raw id (`unk14MissileMotion`, omitted when 0; a text line labelled
unverified). It is never resolved and never appears in `unresolvedReferences`.

Kits are deduplicated across variants and slots. Each kit lists the slots
that reference it. `main`'s envelope stays: `variants`, `kits`,
`unresolvedReferences`, `storeUnloaded`, the 500-kit cap. Two parts of `main`'s
contract go away: the `difficulty` argument (D4) and
`variantsAtOtherDifficulties`. `get_spell_visual_chain` takes `id` only.

The gate on `storeUnloaded` covers SpellVisual, SpellVisualKit,
SpellVisualMissile, SpellVisualEffectName and SpellVisualKitModelAttach.
SpellMissileMotion is read but isn't gated on; ids it can't resolve appear in
`unresolvedReferences`. Integrity tests must show that every non-zero
reference on the reference data either resolves or is listed there.

## 7. Tool surface

All fourteen tools keep their names and response envelopes. Differences from
`main`:

| Tool | Change |
|---|---|
| `server_info` | Reports `clientBuild: 18414`, the file actually opened per store, and layout mismatches as fields + record size |
| `get_spell` | `difficulty` = Difficulty.dbc id (D3); new 5.4.8 fields (§5.3); visual section per §6 |
| `search_spells` | Filters follow the 5.4.8 enums; attribute filter covers words 0–13 |
| `calculate_spell_scaling` | §5.5 |
| `get_spell_visual_chain` | §6; `difficulty` removed |
| `list_db2` | Lists `.dbc` and `.db2`, with extension and a `modelled` flag |
| `get_db2_row` / `query_db2` | Work on every modelled store |
| `get_spell_db_overrides` | Table list below |

Override tables (columns confirmed against the reference `world` schema):

| Table | Spell id column |
|---|---|
| `spell_proc_event` | `entry` |
| `spell_proc` | `spellId` |
| `spell_script_names` | `spell_id` |
| `spell_custom_attribute` | `spell` |
| `spell_bonus_data` | `entry` |
| `spell_threat` | `entry` |
| `spell_ranks` | `spell_id` |
| `spell_linked_spell` | `spell_trigger` |
| `spell_dbc` | `Id` |
| `spelleffect_dbc` | `EffectSpellId` |
| `spellmisc_dbc` | `Id` **is a SpellMisc id** — matched against `spell_dbc.SpellMiscId` for this spell and against the client `Spell.SpellMiscId` |

A missing table still reports as absent rather than as a failure, as on
`main`. The 500-rows-per-table cap stays; with 11 tables a single response can
be 5500 rows.

## 8. Configuration

Same variables as `main`, minus `SPELLWORK_HOTFIX_CACHE`. Additions:

- `SPELLWORK_DB2_PATH` (optional): a second directory searched for `.db2`
  files. If it's unset, `.db2` files are looked up in `SPELLWORK_DBC_PATH`.
- `SPELLWORK_GT_PATH` is accepted for compatibility but unused: 5.4.8 keeps
  `gtSpellScaling.dbc` in the DBC folder, which is where it is read from.
- `SPELLWORK_LOCALE`: when the subfolder doesn't exist, the DBC root is used
  and `server_info` says so.

Registration: `claude mcp add --scope user spellwork-548 -e SPELLWORK_DBC_PATH=C:/MoP/MOPPREMIUM/Repack/dbc ... -- dotnet <path>/out/mcp548/SpellWork.Mcp.dll`.

## 9. Error handling

Unchanged from `main`: a bad path or a missing store gives a structured error
that names the variable or file, never a dead process. New case: when a store
loaded from a stale or mismatched layout, `server_info.layoutMismatches` names
the file (D5, D7). Tools that read that store keep answering.

## 10. Testing

- **Data-independent** (run in CI): SqlGuard, flag decoding against the 5.4.8
  enums, JSON shape, layout constants, tool contracts, file-resolution rules
  (.db2 over .dbc, optional locale) against a temp directory with synthetic
  WDBC/WDB2 headers.
- **Reference-data** (skip when `SPELLWORK_DBC_PATH` is unset): zero layout
  mismatches; store counts; visual-chain integrity (every reference resolves
  or is listed); per-difficulty effect resolution on a raid spell with
  difficulty-specific rows.
- **Goldens**: text and JSON for about 10 spells chosen to cover each part of
  the model: a direct-damage spell with scaling, a DoT, a proc aura, a
  triggered spell, a spell with rune cost, a spell with difficulty-specific
  effects, a spell with a missile visual, a spell with two visual variants, a
  totem, an item-enchant spell. Goldens are generated, then checked by hand
  against the raw rows before they're committed.
- **MySQL** (skip when `SPELLWORK_TEST_MYSQL_*` is unset): the override table
  list and the `spellmisc_dbc` join, against the reference schema.

## 11. Open risks

- **Enum correctness.** Upstream's 5.4.8 enum file is old and partly `Unk`.
  Wrong names in `decode_flags` look plausible and are hard to spot. Mitigation:
  §5.4's precedence rule and a spot-check of about 20 aura/effect ids against
  the repack's own server behaviour.
- **Build drift.** The WDB2 files say build 18273, not 18414. Layouts match
  WoWDBDefs for both, but `server_info` reports the header build per file so a
  drift is visible.
- **Repack customisation.** `spell_dbc`/`spelleffect_dbc`/`spellmisc_dbc` add
  spells that aren't in the client data (ids ≥ 200000). This server reports
  them as overrides only; it doesn't merge them into `get_spell`.
