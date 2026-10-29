# SpellWork CoA (3.3.5a) MCP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serve the fourteen SpellWork MCP tools against the CoA 3.3.5a client data, published to `out/mcpcoa/` and registered globally as `spellwork-coa`.

**Architecture:** Port the `5.4.8` branch's Core/Mcp to the 3.3.5a data model. The 3.3.5 `Spell.dbc` is one fat row; Core keeps its public `SpellInfo` surface by synthesising per-effect objects (`SpellEffectEntry`-shaped, but plain classes, not stores) from the row's three indexed effect column groups, so serializers, renderers and tools change as little as possible. Difficulty moves from row-level (5.4.8) to spell-id level via `SpellDifficulty.dbc`.

**Tech Stack:** .NET (as on `5.4.8`), DBFileReaderLib (WDBC), xUnit, ModelContextProtocol.Server, MySql.Data.

**Spec:** `docs/superpowers/specs/2026-10-29-spellwork-coa-mcp-design.md`. Pattern reference: `docs/superpowers/plans/2026-10-02-spellwork-548-mcp.md` (how the 5.4.8 port was done; reuse its techniques, not its values).

## Global Constraints

- Worktree `F:\WorkDir\SpellWork\.claude\worktrees\sw-coa`, branch `coa`. Commit messages prefixed `coa: `. Never push.
- Reference data: `SPELLWORK_DBC_PATH=F:/WorkDir/azerothcore-wotlk-coa/env/dist/data/dbc`, `SPELLWORK_LOCALE=enUS`. No DB2 folder.
- Authority for layouts: `F:\WorkDir\azerothcore-wotlk-coa\src\server\shared\DataStores\DBCfmt.h` and `src\server\game\DataStores\DBCStructure.h`; field names from WoWDBDefs build 3.3.5.12340; record sizes/field counts from the reference headers (spec §4.1 table).
- Authority for enums: CoA `src/server/shared/SharedDefines.h`, `src/server/game/Spells/Auras/SpellAuraDefines.h`, `src/server/game/Spells/SpellDefines.h`. TOTAL_AURAS = 367, TOTAL_SPELL_EFFECTS = 199, attribute words 0–7.
- `DBC.Version = "SpellWork CoA 3.3.5a (12340)"`, `MaxLevel = 100`, `MaxEffectIndex = 3`, attribute words = 8.
- Every data test skips when `SPELLWORK_DBC_PATH` is unset; MySQL tests skip when `SPELLWORK_TEST_MYSQL_*` is unset.
- The data-independent suite must pass with no env vars set.

## Review Focus

1. **Spell id with a SpellDifficulty row but the requested mode's slot is 0** → `get_spell(difficulty=3)` returns the base spell and says `resolvedSpellId == id`, never errors. Test in Task 4.
2. **CoA-custom aura/effect ids (> stock 316 / 164)** → decoded by name from the CoA enum, never "Unknown". Test in Task 3.
3. **Empty localized strings in a non-enUS slot** → `SPELLWORK_LOCALE=deDE` yields empty name strings but the load succeeds and `server_info` reports the slot used. Test in Task 2.
4. **`search_spells` over 209,510 rows** → a broad name query returns within the existing result cap and in under 2 s on the reference data. Test in Task 7.
5. **Stale/odd layouts (SpellChainEffects record size 177)** → reported in `server_info.layoutMismatches` if the structure disagrees, never a crash; other tools keep answering. Test in Task 2.

---

### Task 1: Branch hygiene, version constants, CI

**Files:**
- Modify: `SpellWork.Core/DBC/DBC.cs` (constants), `.github/workflows/ci.yml` (branches `[main, 5.4.8, coa]`), `SpellWork.Core.Tests/ClientDataFixture.cs` + `SpellWork.Mcp.Tests/McpClientDataFixture.cs` (skip message says CoA 3.3.5a).

- [ ] **Step 1:** Set the constants from Global Constraints; `MaxClasses` = the CoA `MAX_CLASSES` value from `SharedDefines.h`.
- [ ] **Step 2:** `dotnet build SpellWork.sln -c Release` — expected: builds (still the 5.4.8 model).
- [ ] **Step 3:** Commit `coa: version constants and CI branch`.

### Task 2: 3.3.5 structures, store list, loader, layout verification

**Files:**
- Delete: every structure in `SpellWork.Core/DBC/Structures/` not in spec §4.1 (SpellMisc, SpellEffect, SpellEffectScaling, SpellPower, SpellTargetRestrictions, SpellAuraOptions, SpellCategories, SpellCooldowns, SpellInterrupts, SpellLevels, SpellScaling, GtSpellScaling, Difficulty, SpellVisualMissile, SpellAuraRestrictions, SpellCastingRequirements, SpellClassOptions, SpellEquippedItems, SpellShapeshift, SpellTotems, SpellReagents, ItemSparse, SpellProcsPerMinute, RandPropPoints).
- Rewrite: `SpellEntry.cs` (234 fields), `SpellVisualEntry`, `SpellVisualKitEntry`, `SpellVisualEffectNameEntry`, `SpellVisualKitModelAttachEntry`, `SpellVisualKitAreaModelEntry`, `SpellMissileEntry`, `SpellMissileMotionEntry`, `SpellChainEffectsEntry`, `SpellRangeEntry`, `SpellRuneCostEntry`, `SpellDurationEntry`, `SpellCastTimesEntry`, `SpellRadiusEntry`, `SpellDescriptionVariablesEntry`, `SkillLineEntry`, `SkillLineAbilityEntry`, `AreaGroupEntry`, `AreaTableEntry`, `OverrideSpellDataEntry`, `ScreenEffectEntry`, `ItemEntry`.
- Create: `SpellDifficultyEntry.cs`, `SpellCategoryEntry.cs`, `SpellIconEntry.cs`, `SpellAffectEntry.cs`, `SpellItemEnchantmentEntry.cs`, `SpellShapeshiftFormEntry.cs`, `SpellFocusObjectEntry.cs`, `TotemCategoryEntry.cs`.
- Modify: `DBC.cs` (store properties, `FileNameFor`), `LayoutVerification.cs` (`Expected` = spec §4.1 values; `Describe()` text says "CoA 3.3.5a (12340)"), `ClientFileResolver.cs` (`.dbc` only; locale is no longer a folder).
- Test: `LayoutVerificationTests.cs`, `ClientFileResolverTests.cs`, `DbcLoadTests.cs`.

**Interfaces — Produces:**
- `SpellEntry` public fields named per WoWDBDefs 3.3.5.12340, arrays for indexed groups: `int[] Effect` (3), `int[] EffectDieSides`, `float[] EffectRealPointsPerLevel`, `int[] EffectBasePoints`, `int[] EffectMechanic`, `int[] ImplicitTargetA`, `int[] ImplicitTargetB`, `int[] EffectRadiusIndex`, `int[] EffectAura`, `int[] EffectAuraPeriod`, `float[] EffectAmplitude`, `int[] EffectChainTargets`, `int[] EffectItemType`, `int[] EffectMiscValue`, `int[] EffectMiscValueB`, `int[] EffectTriggerSpell`, `float[] EffectPointsPerCombo`, `uint[] EffectSpellClassMaskA/B/C`, `float[] EffectChainAmplitude` (DamageMultiplier), `float[] EffectBonusCoefficient`; `uint[] Attributes` (8, gathered from Attributes, AttributesEx..Ex7); `int[] SpellVisualID` (2); `int[] Reagent` (8), `int[] ReagentCount` (8); `int[] Totem` (2); `int[] RequiredTotemCategoryID` (2); `string Name`, `NameSubtext`, `Description`, `AuraDescription` — the strings of the configured locale slot (Task 2 Step 4); plus every scalar column. `SpellDifficultyID`, `RuneCostID`, `SpellMissileID`, `SpellDescriptionVariableID`, `SpellIconID`, `ActiveIconID`, `SpellPriority`, `PowerDisplayID` included.
- `DBC.SpellDifficulty: Storage<SpellDifficultyEntry>` with `int[] DifficultySpellID` (4).
- `DBC.LocaleSlot: int` — slot used for strings; `DBC.LocaleFallback` removed.

- [ ] **Step 1: Write failing tests.** `LayoutVerificationTests.Expected_matches_spec_table`: assert `LayoutVerification.Expected["Spell"] == (234, 936)`, `["SpellVisual"] == (32, 128)`, `["SpellVisualKit"] == (38, 152)`, `["SpellChainEffects"] == (48, 177)`, `["SpellAffect"] == (3, 12)`, and that `Expected.Keys` equals `DBC.StoreFileNames()` as sets. `ClientFileResolverTests`: a temp dir with a synthetic WDBC `Spell.dbc` resolves; a `.db2` beside it is ignored. `LocaleSlotTests.Maps_names`: `enUS→0, koKR→1, frFR→2, deDE→3, zhCN→4, zhTW→5, esES→6, esMX→7, ruRU→8`; unknown → 0 with a fallback flag.
- [ ] **Step 2:** Run `dotnet test SpellWork.Core.Tests --filter "LayoutVerification|ClientFileResolver|LocaleSlot"` — expected: FAIL.
- [ ] **Step 3: Write structures.** Each file's field order follows the CoA `DBCfmt.h` string (`x` columns still occupy a field), names from WoWDBDefs, string columns as `string` (localized `s` groups: 16 slots + flags = 17 fields; the structure keeps all 16 as `string[]` and exposes the slot via the Step 4 accessor). Before committing each structure, open the reference file and check its header against spec §4.1. `SpellChainEffects` (177 bytes): if the WoWDBDefs 3.3.5 field list can't be made to sum to 177, model what is certain and leave it as a reported mismatch — do not pad to fit.
- [ ] **Step 4: Locale slot.** `DBC.LoadAsync` sets `LocaleSlot` from `SpellWorkConfig.Locale`; `SpellEntry.Name` etc. read `NameLang[DBC.LocaleSlot]`. Same for `SpellRange`, `SkillLine`, `AreaTable` display names.
- [ ] **Step 5:** `BuildSpellInfoStore` is stubbed to compile only (Task 4 rewrites it). Run the Step 2 filter plus `DbcLoadTests` with data env vars set — expected: PASS, zero layout mismatches except possibly SpellChainEffects (Review Focus 5: assert the load completes and other stores are non-null when it mismatches).
- [ ] **Step 6:** Commit `coa: 3.3.5a structures, store list, layout verification`.

### Task 3: CoA enums

**Files:**
- Rewrite: `SpellWork.Core/Spell/SpellEnums.cs`.
- Modify: `SpellWork.Mcp/EnumCatalog.cs` (enum list), `SpellWork.Mcp.Tests/EnumCatalogTests.cs`, `DecoderToolsTests.cs`.

- [ ] **Step 1: Failing tests.** `EnumCatalogTests`: `AuraType` has 367 members ending at `TOTAL_AURAS-1` = 366; `SpellEffects` has 199 (0..198); every `SpellAttr0..7` flag value exists; `Classes` includes the CoA classes (e.g. `CLASS_BARBARIAN = 12`, `CLASS_WITCH_DOCTOR = 13`). `DecoderToolsTests`: `decode_flags` on an aura id > 316 returns the CoA header name, not `Unk`.
- [ ] **Step 2:** Run `dotnet test SpellWork.Mcp.Tests --filter "EnumCatalog|DecoderTools"` — FAIL.
- [ ] **Step 3: Generate** the enums from the CoA headers with a throwaway script in the scratchpad (not committed): AuraType, SpellEffects, Targets, SpellAttr0–7, ProcFlags, ProcFlagsEx (if present), Mechanics, Powers, SpellFamilyNames, DispelType, SpellSchoolMask, Classes, Races, SpellCastTargetFlags, SpellInterruptFlags, AuraInterruptFlags, ChannelInterruptFlags, SpellCustomAttributes. Keep the C# enum type names `SpellEnums.cs` already uses so callers compile; remove enums that only exist for 5.4.8 tables (e.g. SpellAttr8–13) and fix their callers.
- [ ] **Step 4:** Run tests — PASS. Commit `coa: enums from CoA SharedDefines/SpellAuraDefines`.

### Task 4: SpellInfo assembly, difficulty, triggers

**Files:**
- Create: `SpellWork.Core/Spell/SpellEffectInfo335.cs` — **or** repurpose `SpellEffectEntry` as a plain class in `SpellWork.Core/Spell/` (not under `DBC/Structures`, not a store). Pick the one that keeps the most consumers unchanged.
- Delete: `SpellWork.Core/DBC/DifficultyIndex.cs` and its test.
- Rewrite: `SpellInfo.cs`, `DBC.BuildSpellInfoStore`.
- Create: `SpellWork.Core/DBC/SpellDifficultyResolver.cs`.
- Test: `SpellInfoAssemblyTests.cs`, `EffectsForMixingTests.cs` (delete or rewrite), new `SpellDifficultyResolverTests.cs`.

**Interfaces — Produces:**
- `SpellInfo(SpellEntry)` keeps: `ID, Name, Description, Tooltip, Attributes (uint[8]), AttributesEx..Ex7, Effects: IReadOnlyList<ResolvedEffect>` (only non-zero effect slots, `ResolvedEffect.Entry.EffectIndex` = 0..2, `ResolvedDifficulty` always 0), `RuneCost, DurationEntry, Range, CastTimeEntry, DescriptionVariables, Icon`, plus `Reagents => (int Item, int Count)[]`, `Totems`, `Category`.
- Removed from `SpellInfo`: `Misc, Scaling, Powers, *For(int d)` per-difficulty accessors, `EffectDifficulties`, `ProcsPerMinute`. Callers in Core/Mcp are updated in this task or the owning later task; the build must be green at the end of this task.
- `SpellDifficultyResolver.Resolve(int spellId, int mode) -> (int ResolvedSpellId, IReadOnlyList<int> Variants)`; `mode` 0–3; a mode slot of 0 or a spell with `SpellDifficultyID == 0` resolves to `spellId`; mode outside 0–3 throws `ArgumentOutOfRangeException`.
- `DBC.SpellTriggerStore` built from `EffectTriggerSpell[0..2]` of every row.

- [ ] **Step 1: Failing tests.** `SpellDifficultyResolverTests` with an in-memory `SpellDifficulty` store: row `{ID=7, DifficultySpellID=[100,200,0,400]}` and spell 100 pointing at 7 → `Resolve(100,1) == (200, [100,200,400])`, `Resolve(100,2) == (100, …)` (Review Focus 1), `Resolve(999,3) == (999, [])`, `Resolve(100,4)` throws. `SpellInfoAssemblyTests` (data): a stock spell (Fireball rank 1, id 133) has 1–2 effects with `Effect[0] == SCHOOL_DAMAGE` and its `SpellVisual[0]` non-zero.
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement. Build a spell-id → SpellDifficultyID index once in `BuildSpellInfoStore`.
- [ ] **Step 4:** `dotnet build SpellWork.sln` green; run Core tests (data and no-data) — PASS except renderer/JSON/golden/visual/scaling tests, which later tasks own; mark those `[Fact(Skip="coa: Task N")]` here, and the owning task removes the skip.
- [ ] **Step 5:** Commit `coa: SpellInfo from the 3.3.5 Spell row; SpellDifficulty resolution`.

### Task 5: 3.3.5 scaling

**Files:** Rewrite `SpellWork.Core/Spell/SpellScalingCalculator.cs`; test `SpellScalingCalculatorTests.cs`; modify the `calculate_spell_scaling` tool in `SpellWork.Mcp/Tools/SpellTools.cs`.

**Interfaces — Produces:** `SpellScalingCalculator.Value(int basePoints, int dieSides, float realPointsPerLevel, float pointsPerComboPoint, int spellLevel, int baseLevel, int maxLevel, int casterLevel) -> (float Base, float Min, float Max, float PerComboPoint)`.

- [ ] **Step 1: Read** CoA `SpellEffectInfo::CalcValue` in `src/server/game/Spells/SpellInfo.cpp` and record its exact level clamp and die handling in a code comment (spec §4.3 is the summary; the CoA source wins on any difference).
- [ ] **Step 2: Failing tests** with hand-computed values from that source: basePoints 9, dieSides 1, realPointsPerLevel 0 at level 80 → (10,10,10,0); basePoints 99, dieSides 21, realPointsPerLevel 2.5, spellLevel 20, maxLevel 30, level 80 → Base/Min/Max as the CoA formula gives (level clamped to 30). Tool test: `calculate_spell_scaling(level: 150)` clamps to 100 and echoes `itemLevelApplied: false`.
- [ ] **Step 3:** Implement; remove the 5.4.8 gt lookup and the `difficulty` argument's row-level meaning (it now resolves the spell id via Task 4's resolver first).
- [ ] **Step 4:** Tests PASS. Commit `coa: 3.3.5 CalcValue scaling`.

### Task 6: Text rendering, JSON shape, goldens

**Files:** `SpellInfoJsonSerializer.cs`, `SpellInfoWriterBase.cs`, `PlainTextSpellInfoWriter.cs`, `ProcInfo.cs`, `RenderContext.cs`; tests `SpellInfoJsonSerializerTests.cs`, `PlainTextSpellInfoWriterTests.cs`, `ProcInfoTests.cs`, `GoldenRenderingTests.cs`, `Goldens/`.

- [ ] **Step 1: JSON shape tests** (data-independent, synthetic `SpellEntry`): top level has `id, name, rank (NameSubtext), difficulty {requested, resolvedSpellId, variants}, attributes` (8 words), `procFlags, procChance, procCharges` from the row, `runeCost`, `reagents [{item, count, name?}]`, effects with `index, effect, aura, basePoints, dieSides, realPointsPerLevel, pointsPerComboPoint, amplitude, chainTargets, implicitTargetA/B, radius, miscValue/B, triggerSpell, spellClassMask[3], damageMultiplier, bonusMultiplier`. Absent: `scaling`, `misc`, per-effect `difficulty`. Item names appear only when the world DB is configured.
- [ ] **Step 2:** Run — FAIL. **Step 3:** Implement. **Step 4:** Run — PASS.
- [ ] **Step 5: Goldens.** Pick ~10 spell ids from the reference data covering spec §8's list (incl. one spell using a CoA-custom aura and one with a SpellDifficulty row); generate text+JSON goldens; check each against the raw `Spell.dbc` row by hand (write the check script in the scratchpad) before committing. Remove Task 4's skips for these tests.
- [ ] **Step 6:** Commit `coa: JSON/text rendering and goldens`.

### Task 7: Visual chain and search

**Files:** `SpellVisualChainResolver.cs`, `SpellVisualChainIndex.cs`, `SpellVisualChain.cs`, `SpellVisualChainJson.cs`, `SpellVisualSection.cs`; tests `SpellVisualChainResolverTests.cs`, `VisualChainIntegrityTests.cs`, `SpellToolsTests.cs` (search).

- [ ] **Step 1: Check columns on data.** For SpellVisual's `MissileModel` and `MissileMotion` columns, count non-zero values that resolve in SpellVisualEffectName / SpellMissileMotion. A column with < 95 % resolution is reported raw and labelled unverified (as 5.4.8 did for its column 14). Record the counts in the commit message.
- [ ] **Step 2: Failing tests.** Resolver unit tests with in-memory stores per spec §5 (two slots, kit dedup across slots, ModelAttach by `ParentSpellVisualKitID`, unresolved ids listed, 500-kit cap). Integrity test (data): every non-zero reference resolves or is in `unresolvedReferences`. Search perf test (data, Review Focus 4): `search_spells(name: "a")` returns in < 2 s and honours the existing result cap.
- [ ] **Step 3:** Implement: root at `SpellEntry.SpellVisualID[0..1]`; remove SpellVisualMissile handling.
- [ ] **Step 4:** PASS. Commit `coa: visual chain from Spell.SpellVisual`.

### Task 8: Remaining tool surface

**Files:** `SpellWork.Mcp/Tools/*.cs`, `SpellDataService.cs`, `McpServerConfig.cs`; matching tests in `SpellWork.Mcp.Tests`.

- [ ] **Step 1: Failing tests.** `server_info` reports `clientBuild: 12340`, `variant: "coa"`, `localeSlot`, opened file per store, load time ms. `get_spell(difficulty)` accepts 0–3, rejects others with a structured error naming the valid range. `get_spell_triggers`, `compare_spells`, `get_db2_row`/`query_db2` work over every modelled store (incl. SpellDifficulty, SpellAffect). `list_db2` lists every `.dbc` in the folder with a `modelled` flag (the CoA-only unloaded files, e.g. `SpellAddon.dbc`, appear with `modelled: false`). `search_spells` attribute filter accepts words 0–7 only. `McpServerConfig` accepts and ignores `SPELLWORK_DB2_PATH`/`SPELLWORK_GT_PATH`.
- [ ] **Step 2:** Run — FAIL. **Step 3:** Implement. **Step 4:** Full `dotnet test` with and without data env vars — PASS (no remaining `coa: Task` skips).
- [ ] **Step 5:** Commit `coa: tool surface on 3.3.5a`.

### Task 9: World database overrides

**Files:** `SpellWork.Mcp/Tools/DatabaseTools.cs`, `SpellWork.Core/Database/WorldDatabase.cs`; tests `DatabaseToolsTests.cs`.

- [ ] **Step 1:** Replace the override table list with spec §6's 13 tables and columns. Remove the `spellmisc_dbc` join. Confirm each column name against `F:\WorkDir\azerothcore-wotlk-coa\data\sql\base\db_world\<table>.sql`.
- [ ] **Step 2:** Tests: data-independent test asserts the table→column map exactly; MySQL test (skippable) asserts every table is either queried or reported absent. Item names for reagents come from `item_template.name` by `entry`.
- [ ] **Step 3:** PASS. Commit `coa: AzerothCore world override tables`.

### Task 10: Docs, publish, register, end-to-end

**Files:** `README.md`, `docs/mcp-server.md`.

- [ ] **Step 1: Docs.** README: "This branch tracks **CoA 3.3.5a (build 12340)**". `docs/mcp-server.md`: config table (spec §7), `difficulty` = mode 0–3, visual chain per spec §5, the 13 override tables, known limitations (CoA-only DBCs unmodelled, `spell_dbc` not merged, SpellChainEffects status from Task 2).
- [ ] **Step 2: Publish.** `dotnet publish SpellWork.Mcp/SpellWork.Mcp.csproj -c Release -o F:/WorkDir/SpellWork/out/mcpcoa --nologo`.
- [ ] **Step 3: Smoke.** Run `StdioSmokeTests` against the published output with the reference env vars — expected: pass, 14 tools listed.
- [ ] **Step 4: Full verification** — both test projects with no env vars and with data env vars; record pass/skip counts in the commit body. Commit `coa: docs and publish layout`.
- [ ] **Step 5: Register globally** (controller does this, not a subagent):

```bash
claude mcp add --scope user spellwork-coa \
  -e SPELLWORK_DBC_PATH=F:/WorkDir/azerothcore-wotlk-coa/env/dist/data/dbc \
  -e SPELLWORK_LOCALE=enUS \
  -- dotnet F:/WorkDir/SpellWork/out/mcpcoa/SpellWork.Mcp.dll
```

Don't push. Report the MySQL env vars as an optional follow-up.
