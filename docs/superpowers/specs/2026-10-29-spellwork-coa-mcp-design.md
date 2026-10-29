# SpellWork CoA (3.3.5a) MCP Server — Design

Branch: `coa` (cut from `5.4.8` at `185ef00`). Client: 3.3.5a (build 12340),
customised by the CoA AzerothCore fork.
Reference data: `F:\WorkDir\azerothcore-wotlk-coa\env\dist\data\dbc` (WDBC only).
Authoritative source for layouts and enums: the CoA server source at
`F:\WorkDir\azerothcore-wotlk-coa\src` (`DBCfmt.h`, `DBCStructure.h`,
`SharedDefines.h`, `SpellAuraDefines.h`, `SpellInfo.cpp`).
World schema: `F:\WorkDir\azerothcore-wotlk-coa\data\sql\base\db_world`.

## 1. Goal

The fourteen MCP tools that `5.4.8` serves, served for the CoA 3.3.5a data from
a separate build on a separate branch, registered globally as `spellwork-coa`
next to `spellwork-548`, `spellwork-554` and `spellwork-837`.

Success means: every tool answers against the reference data, layout
verification reports zero mismatches, `get_spell_visual_chain` resolves the
chain down to effect model names, enums match the CoA server headers, and the
data-independent test suite passes in CI.

## 2. Scope

**Built:** `SpellWork.Core` and `SpellWork.Mcp` ported to the 3.3.5a data
model (single fat `Spell.dbc`), plus both test projects; publish to
`out/mcpcoa/`; global registration `spellwork-coa`.

**Not built:**

- The WinForms project (already absent on `5.4.8`).
- Models for CoA-only DBCs the server does not load (SpellAddon, SpellCharges,
  SpellCustomAttr, SpellAlternativeCost, SpellTags, …). `list_db2` lists them
  as unmodelled. `SpellAffect.dbc` *is* loaded by the CoA server and is modelled.
- Merging `spell_dbc` rows into `get_spell`. They are reported as overrides.
- Any shared code with other branches. Fixes travel by cherry-pick.

## 3. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | Branch from `5.4.8`, not `main` or `upstream/master` | 5.4.8 already has WDBC reading, field+record-size layout verification, and a visual chain rooted in the spell row. 3.3.5 is closer to it than to 8.3.7. |
| D2 | Structures follow the CoA `DBCfmt.h`/`DBCStructure.h`, names from WoWDBDefs 3.3.5.12340, sizes from the reference headers | The CoA fork may diverge from stock; its own loader is the truth. |
| D3 | All spell data comes from one `SpellEntry` (234 fields, 936 bytes) | 3.3.5 has no SpellMisc/SpellEffect split. Effects are the 3 indexed column groups. |
| D4 | `difficulty` = SpellDifficulty mode index 0–3 (10N, 25N, 10H, 25H), **optional on every tool** (omitted = the id as given, no resolution). Given, it resolves to the variant spell id through the `SpellDifficulty.dbc` row **that lists the spell** (row membership, as the CoA server builds its lookup; rows lacking mode 0 or 1 skipped, ids absent from Spell.dbc treated as empty), with the server's heroic fallback (mode 2→0, 3→1), then the requested spell. *Corrected during implementation:* the original text resolved via `Spell.SpellDifficultyId`, which the server does not read and which, on the reference data, names a row not listing the spell for 1912 of 2100 spells; a defaulted mode 0 would also have silently mapped 25N ids to 10N | That is how the CoA server resolves per-difficulty spells (`SpellMgr::GetSpellIdForDifficulty`). JSON reports the requested mode, the resolved id and all variants. |
| D5 | Enums generated from the CoA headers (TOTAL_AURAS 367, TOTAL_SPELL_EFFECTS 199, attributes 0–7, classes incl. CoA classes 12+, families) | CoA adds ~50 auras and ~34 effects over stock; upstream SpellWork enums would mislabel them. |
| D6 | Locale = Spell.dbc string slot (enUS = 0); `SPELLWORK_LOCALE` picks the slot, default enUS | 3.3.5 stores 16 locale strings per row in one file; there are no locale folders. |
| D7 | Layout verification = field count + record size (unchanged from 5.4.8) | WDBC has no layout hash. |

## 4. Data model

### 4.1 Stores and recorded layouts (reference headers)

| Table | Fields | Record size | Use |
|---|---|---|---|
| Spell | 234 | 936 | everything spell |
| SpellCastTimes / SpellDuration / SpellRadius | 4 | 16 | lookups |
| SpellRange | 40 | 160 | lookups (localized name) |
| SpellRuneCost | 5 | 20 | rune cost |
| SpellDifficulty | 5 | 20 | D4 |
| SpellCategory | 2 | 8 | category flags |
| SpellDescriptionVariables | 2 | 8 | description |
| SpellIcon | 2 | 8 | icon path |
| SpellAffect (CoA) | 3 | 12 | per CoA `SpellAffectfmt "nii"` |
| SpellItemEnchantment | 38 | 152 | enchant spells |
| SpellShapeshiftForm / SpellFocusObject / TotemCategory | 35 / 18 / 20 | 140 / 72 / 80 | names |
| SkillLine / SkillLineAbility | 56 / 14 | 224 / 56 | skill info |
| AreaGroup / AreaTable / OverrideSpellData / ScreenEffect | 8 / 36 / 12 / 10 | 32 / 144 / 48 / 40 | as 5.4.8 |
| SpellVisual | 32 | 128 | chain |
| SpellVisualKit | 38 | 152 | chain |
| SpellVisualEffectName | 7 | 28 | chain |
| SpellVisualKitModelAttach | 10 | 40 | chain |
| SpellVisualKitAreaModel | 3 | 12 | modelled, not chained |
| SpellMissile / SpellMissileMotion | 15 / 5 | 60 / 20 | chain |
| SpellChainEffects | 48 | 177 | modelled; record size is not a multiple of 4, so the structure needs byte fields — verify against WoWDBDefs |

Removed from 5.4.8's list: every Spell* sub-table (SpellMisc, SpellEffect,
SpellEffectScaling, SpellAuraOptions, …, SpellPower, SpellScaling,
SpellProcsPerMinute), Difficulty, gtSpellScaling, RandPropPoints,
Item-sparse, SpellReagents. `Item.dbc` (8 fields) stays for item class lookups;
item **names** come from `item_template` when the world DB is configured,
otherwise reagents/items are reported by id.

### 4.2 `SpellInfo`

Built from one `SpellEntry` row. Effects 0–2 are read from the indexed column
groups (Effect, DieSides, RealPointsPerLevel, BasePoints, Mechanic,
ImplicitTargetA/B, RadiusIndex, ApplyAuraName, Amplitude, MultipleValue,
ChainTarget, ItemType, MiscValue/B, TriggerSpell, PointsPerComboPoint,
SpellClassMask[3], DamageMultiplier, BonusMultiplier). JSON keeps 5.4.8's
envelope; fields that do not exist in 3.3.5 (scaling coefficients, per-row
difficulty, SpellMisc ids) are omitted. Attributes are 8 words (0–7).
`SpellTriggerStore` is built from TriggerSpell across all spells.

### 4.3 Scaling

`calculate_spell_scaling(id, level, itemLevel, difficulty)` uses the 3.3.5
`SpellInfo::CalcValue` rule from the CoA source: base = BasePoints +
(RealPointsPerLevel × (clamp(level, BaseLevel, MaxLevel>0 ? MaxLevel : level) −
max(BaseLevel, SpellLevel))) with DieSides range, plus PointsPerComboPoint reported separately.
Level clamps to 1..100 (CoA `MAX_LEVEL`). `itemLevel` is echoed with
`"itemLevelApplied": false`.

## 5. Visual chain

Entry: `Spell.SpellVisual[0]` and `[1]`; each non-zero id is a variant with
`slot: 0|1`.

```
SpellVisual
 ├─ PrecastKit, CastKit, ImpactKit, StateKit, StateDoneKit, ChannelKit,
 │  CasterImpactKit, TargetImpactKit, MissileTargetingKit, InstantAreaKit,
 │  ImpactAreaKit, PersistentAreaKit                     ──► SpellVisualKit
 ├─ MissileModel ──► SpellVisualEffectName
 └─ MissileMotion ──► SpellMissileMotion   (column meaning to verify on data, as 5.4.8 §6 did)
SpellVisualKit
 ├─ Head/Chest/Base/LeftHand/RightHand/Breath/LeftWeapon/RightWeapon/
 │  Special[0..2]/World effect ──► SpellVisualEffectName
 └─ SpellVisualKitModelAttach rows (by ParentSpellVisualKitID) ──► SpellVisualEffectName
```

Envelope, dedup, 500-kit cap, `unresolvedReferences`, `storeUnloaded` gate:
unchanged from 5.4.8. Integrity test: every non-zero reference on the
reference data resolves or is listed as unresolved. Any column whose meaning
does not hold up on the data is reported raw, labelled unverified, as 5.4.8
did for its column 14.

## 6. Tool surface

All fourteen tools keep names and envelopes.

| Tool | Change vs 5.4.8 |
|---|---|
| `server_info` | `clientBuild: 12340`, `variant: "coa"`, file per store, layout mismatches |
| `get_spell` | D3/D4 model; locale per D6 |
| `search_spells` | CoA enums; attribute filter words 0–7; 209k spells |
| `calculate_spell_scaling` | §4.3 |
| `get_spell_visual_chain` | §5 |
| `list_enum` / `decode_flags` | CoA enums (D5) |
| `list_db2` / `get_db2_row` / `query_db2` | Over `.dbc` only |
| `get_spell_db_overrides` | table list below |

Override tables (columns from the CoA `db_world` base SQL):

| Table | Spell id column |
|---|---|
| `spell_proc` | `SpellId` |
| `spell_proc_event` | `entry` |
| `spell_script_names` | `spell_id` |
| `spell_custom_attr` | `spell_id` |
| `spell_bonus_data` | `entry` |
| `spell_threat` | `entry` |
| `spell_ranks` | `spell_id` |
| `spell_linked_spell` | `spell_trigger` |
| `spell_dbc` | `ID` |
| `spell_cooldown_overrides` | `Id` |
| `spell_area` | `spell` |
| `spell_group` | `spell_id` |
| `spell_required` | `spell_id` |

Missing tables report as absent; 500-row cap per table stays.

*Corrected during implementation:* matching follows each table's CoA loader,
not the plain id alone. `spell_proc`, `spell_proc_event` and
`spell_script_names` also match `-firstRank` (all ranks); `spell_group` also
matches the first rank (only first ranks are listed); `spell_linked_spell`
also matches `-id` (on aura removal). The first rank comes from
`spell_ranks.first_spell_id`. Every row reports `matchedBy`: `exact`,
`allRanks`, `firstRank` or `onRemoval`. *Final review:* `spell_bonus_data` and
`spell_threat` fall back to the first rank's row only when the spell has no
row of its own (`SpellMgr.cpp:990-1019`); `spell_proc_event` is legacy, not
loaded by the CoA server, and documented as such.

§5 note: the 3.3.5 missile is `SpellVisual.MissileModel`/`MissileMotion`, not a
row of its own, so a `missiles` entry has no `id` (5.4.8 had one) and carries
the effect name's `fileName`. `SpellChainEffects` (§4.1, §9) loads with zero
layout mismatches on the reference data.

## 7. Configuration and registration

`SPELLWORK_DBC_PATH` (required), `SPELLWORK_LOCALE` (default enUS),
`SPELLWORK_MYSQL_HOST/PORT/USER/PASS/WORLD_DB` (optional, world DB; unchanged
names). `SPELLWORK_DB2_PATH` and
`SPELLWORK_GT_PATH` are accepted and ignored.

```
claude mcp add --scope user spellwork-coa \
  -e SPELLWORK_DBC_PATH=F:/WorkDir/azerothcore-wotlk-coa/env/dist/data/dbc \
  -e SPELLWORK_LOCALE=enUS \
  -- dotnet F:/WorkDir/SpellWork/out/mcpcoa/SpellWork.Mcp.dll
```

## 8. Testing

As 5.4.8 §10, retargeted: data-independent tests (SqlGuard, CoA enum decoding,
JSON shape, layout constants, tool contracts, file resolution) in CI;
reference-data tests (zero mismatches, store counts, visual-chain integrity,
difficulty resolution on a spell with a SpellDifficulty row) skipped when
`SPELLWORK_DBC_PATH` is unset; goldens for ~10 spells covering direct damage,
DoT, proc aura, trigger, rune cost, SpellDifficulty variants, missile visual,
two visual variants, totem, item enchant, plus at least one CoA-custom aura or
effect id; MySQL tests skipped when `SPELLWORK_TEST_MYSQL_*` is unset.

## 9. Open risks

- **CoA layout drift.** Custom columns could hide inside stock-sized files.
  Mitigation: structures follow CoA `DBCfmt.h`, and goldens are checked by hand
  against raw rows.
- **Size.** Spell.dbc has 209,510 rows (stock ~49k). Search and load time must
  be measured; `server_info` reports load time.
- **SpellChainEffects record size 177.** Needs byte-level fields; if WoWDBDefs
  disagrees with the file, the store is reported as a mismatch rather than
  guessed.
