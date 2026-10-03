# SALIN-166 — Revised Era and Content Model Reconciliation

**Date:** 2026-08-09
**Status:** Approved by the SALIN-166 owner; Jira team sign-off pending
**Jira:** [SALIN-166](https://jnwync.atlassian.net/browse/SALIN-166)
**Repository planning ID:** `TW-SPK-001`
**Work type:** Time-boxed spike; no runtime implementation
**Owner:** Chad Denard Andrada

## 1. Plain-language decision

The revised game is a new campaign built around Ugat, Ugnayan, and Pamana. It is not a relabeling of the existing Spanish, American, and Japanese campaign.

Salinlahi will keep the useful technical foundation—drawing recognition, pooling, scene loading, dialogue rendering, audio, hearts, themes, and reusable data containers—but it will introduce new stable campaign identities and new content. Old campaign names remain visible only to migration code, compatibility adapters, and archived historical data.

Because the old levels do not prove completion of the revised learning objectives, historical campaign progress will be archived and the revised journey will begin at Ugat Level 1. Player preferences such as audio volume are preserved. Migration must create a recoverable legacy snapshot before any revised save becomes active.

This spike records the decisions that SALIN-170, SALIN-171, and SALIN-185 must implement. It does not change C# code, Unity assets, scenes, or player data.

## 2. Authoritative source and evidence

### 2.1 Approved mechanics baseline

The approved planning source is:

- Live sheet: [CORE GAME MECHANICS](https://docs.google.com/spreadsheets/d/1IOxaJ3qPTl0zdLl0b6PKc8eam6qgam62BzxsauNwGyY/edit?usp=sharing)
- Reproducible export: `C:\Users\asus\Downloads\CORE GAME MECHANICS.xlsx`
- Approved SHA-256: `33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83`
- Approval date: 2026-08-09

The workbook contains the ten inspected sheets `DSM`, `ENEMIES`, `Game Overview`, `USER FLOW`, `Core Mechanics`, `Era 1 - Ugat`, `Era 2 - Ugnayan`, `Era 3 - Pamana`, `Character Mastery`, and `Completion Rules`.

Jira currently cites SHA-256 `34dad782a025b3acd3dcfc9bdfb2ce5c595fe81e6bd1789c9042849b63c27eb7`. The files differ at the byte level, but the user confirmed that the inspected XLSX was downloaded from the shared Google Sheet and approved it for SALIN-166. The Jira checksum is retained as historical provenance; it is not the implementation baseline.

### 2.2 Approved campaign facts

The identity contract assumes:

- Three eras: Ugat, Ugnayan, and Pamana.
- Five ordered levels per era and 15 main levels in total.
- Two focus-word slots per level and 30 slots in total.
- Seventeen canonical visual Baybayin symbols.
- Contextual spoken values, including E/I, O/U, and distinct DA and RA uses of one DA/RA visual symbol.
- No kudlit or modified consonant forms in the current campaign scope.
- Previously introduced symbols remain available for cumulative learning.
- Combat completion alone does not complete or unlock a level.
- The revised journey begins at Ugat Level 1.

Exact labels, decompositions, pronunciation, introduction order, and media approval remain SALIN-167 and SALIN-188 responsibilities.

## 3. Current repository baseline

The inspected `dev` snapshot contains:

- `EraConfigSO` with a display name, level-select background/banner sprites, and an ordered level list.
- Only one configured `EraConfigSO` asset, `Era_01`, containing Levels 1–5.
- Fifteen `LevelConfigSO` assets, but several later assets have incomplete chapter/theme data.
- `EraThemeSO` assets named Spanish, American, and Japanese; only the Spanish theme is wired into Levels 1–5.
- `LevelConfigSO` fields for numeric identity, chapter text, visual theme, waves, rosters, bosses, old tutorials, dialogue, BGM, protagonist flags, and broad combat toggles.
- `EnemyDataSO.Era` values `Spanish`, `American`, and `Japanese`; the enum also drives behavior such as General's American-era aura.
- Eighteen `Char_*.asset` objects because `Char_DA` and `Char_RA` are separate.
- `Char_DA` referenced throughout existing level assets and the default registry; `Char_RA` is only present in the default registry.
- Campaign and discovery progress split across multiple `PlayerPrefs` keys with no content-schema version, save-schema version, migration marker, backup, or rollback boundary.
- Level select, enemy Almanac gates, tests, and documentation that still encode colonial-era semantics.
- No analytics SDK or implemented telemetry writer.

The repository foundation is reusable, but the campaign identity and save model are not compatible with the revised mechanics without explicit replacement or adaptation.

## 4. Goals

1. Separate revised campaign identity from old colonial-era semantics.
2. Define stable IDs that do not depend on display text, list position, Unity GUIDs, or localized copy.
3. Record a field-by-field reuse, rename, replace, or archive decision for current data.
4. Establish content, save, and identity-manifest version 1 contracts.
5. Define deterministic compatibility and migration behavior from the current unversioned build.
6. Archive historical campaign progress without treating it as revised learning mastery.
7. Preserve approved player preferences.
8. Define backup, interruption recovery, validation failure, safe reset, and rollback behavior before implementation.
9. Give SALIN-170, SALIN-171, and SALIN-185 one consistent contract.
10. Prevent dependent stories from mixing old and revised era meanings.

## 5. Non-goals

- Writing runtime C# code or tests.
- Editing ScriptableObject assets, scenes, prefabs, build settings, or PlayerPrefs.
- Authoring the 15 revised level assets; SALIN-172 owns production authoring.
- Approving exact educational decompositions, audio, glyph art, or cultural copy; SALIN-167 and SALIN-188 own that approval.
- Prototyping challenge modes or replacing the level flow; SALIN-168 and SALIN-178 own those surfaces.
- Implementing the versioned schema; SALIN-170 owns implementation.
- Implementing save files or migration; SALIN-171 owns implementation.
- Implementing atomic completion transactions; SALIN-174 owns that boundary.
- Removing historical content or broad cleanup; SALIN-185 may remove or archive only evidence-backed candidates.
- Editing `docs/capstone/Salinlahi.md`.

## 6. Considered approaches

### 6.1 Keep old runtime IDs and change display names

This has the lowest immediate migration cost, but `Spanish`, `American`, and `Japanese` would silently mean Ugat, Ugnayan, and Pamana in some systems while retaining colonial-faction meaning elsewhere. That ambiguity would spread into saves, tests, enemy behavior, analytics identifiers, and documentation.

**Decision:** rejected.

### 6.2 Introduce revised canonical IDs with a legacy adapter

Ugat, Ugnayan, and Pamana receive new stable IDs. Old identifiers are read only through explicit migration/compatibility code. Reusable Unity assets retain GUIDs where practical, but runtime identity never depends on those GUIDs.

**Decision:** approved.

### 6.3 Use neutral ordinal IDs such as `era.01`

Neutral IDs tolerate future display-name changes, but they are harder to inspect and debug while requiring the same migration work. Ugat, Ugnayan, and Pamana are approved campaign concepts, not temporary labels.

**Decision:** rejected.

## 7. Canonical identity model

### 7.1 Campaign root

SALIN-170 should introduce one `CampaignConfigSO` root. It owns the identity manifest, approved global tuning, and an ordered list containing exactly the three revised `EraConfigSO` assets. Runtime campaign lookup begins at this root rather than searching arbitrary assets or relying on scene lists.

The root has one responsibility: expose and validate a complete versioned campaign. Save-file I/O, migration, level-flow orchestration, and presentation remain separate consumers.

### 7.2 Identity rules

- IDs are lowercase ASCII dotted strings.
- IDs are immutable after shipping within a campaign lineage.
- Display names, localized text, Unity asset names, filenames, GUIDs, and numeric order are not identity.
- A changed meaning receives a new ID instead of silently reusing an old one.
- Legacy aliases are accepted only at import or compatibility boundaries.
- Runtime consumers operate on canonical IDs after import.
- Unknown IDs fail validation; they do not fall back to numeric level order or asset names.

### 7.3 Campaign and era IDs

| Concept | Stable ID | Display name |
|---|---|---|
| Revised campaign | `campaign.revised-v1` | Salinlahi revised campaign |
| Era 1 | `era.ugat` | Ugat |
| Era 2 | `era.ugnayan` | Ugnayan |
| Era 3 | `era.pamana` | Pamana |

`Spanish`, `American`, and `Japanese` are not aliases for these era IDs. They describe legacy campaign/faction content and may only appear in the legacy mapping, archived data, or isolated old behavior until cleanup is approved.

### 7.4 Level IDs

Levels use an era-local stable ID plus a separate global order:

| Global order | Stable ID |
|---:|---|
| 1–5 | `level.ugat.01` through `level.ugat.05` |
| 6–10 | `level.ugnayan.01` through `level.ugnayan.05` |
| 11–15 | `level.pamana.01` through `level.pamana.05` |

The existing `levelNumber` may remain as presentation/order metadata. It must not be the save key, content lookup key, test identity, or future analytics identity.

The two inline focus-word slots use `<level-id>.focus.01` and `<level-id>.focus.02`. They remain embedded in the level configuration, as required by SALIN-170, rather than becoming 30 standalone ScriptableObjects.

### 7.5 Symbol and spoken-value IDs

Canonical visual symbol IDs are:

`symbol.a`, `symbol.ei`, `symbol.ba`, `symbol.ma`, `symbol.na`, `symbol.ta`, `symbol.ou`, `symbol.ka`, `symbol.ga`, `symbol.sa`, `symbol.wa`, `symbol.ya`, `symbol.dara`, `symbol.ha`, `symbol.la`, `symbol.nga`, and `symbol.pa`.

A focus-word occurrence stores both the canonical visual symbol ID and its approved contextual spoken value. E/I, O/U, and DA/RA interpretation comes from authored word decomposition, never from gameplay inference.

`Char_DA` is the preferred Unity asset to carry `symbol.dara` because it is already referenced throughout the current levels and registry. `DA` and `RA` become legacy/context aliases of `symbol.dara`. `Char_RA` is not an independently learned revised symbol and becomes an import alias or archived asset after reference validation.

## 8. Versioned identity manifest

The revised campaign embeds one manifest with these approved values:

| Field | Version 1 decision |
|---|---|
| `identityManifestVersion` | `1` |
| `campaignId` | `campaign.revised-v1` |
| `contentSchemaVersion` | `1` |
| `saveSchemaVersion` | `1` |
| `sourceWorkbookSha256` | `33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83` |
| `supportedSourceContentSchemas` | Unversioned legacy `0`; revised `1` |
| `supportedSourceSaveSchemas` | Unversioned legacy `0`; revised `1` |
| `migrationId` | `legacy-v0-to-revised-v1` |
| `minimumReadableSaveSchema` | `1` after migration |
| `maximumReadableSaveSchema` | `1` |

The manifest is the authority used by content validation and save compatibility. Migration state is stored inside the revised save envelope; implementation must not infer success from the presence or absence of an individual PlayerPrefs key.

### 8.1 Supported build families

| Build family | Content | Save | Behavior |
|---|---:|---:|---|
| Current legacy repository snapshot (`bundleVersion` 1.0) | Implicit `0` | Implicit `0` | Reads and writes legacy data only. It cannot read a revised save. |
| Revised v1 campaign build | `1` | `1` | Reads v1, imports legacy v0 once, and writes only v1 campaign progress. |
| Future build with schema greater than 1 | Greater than `1` | Greater than `1` | Must publish an explicit compatibility/migration manifest before it is accepted. |

A revised v1 build rejects an unknown higher content or save schema. It must not guess compatibility or overwrite the unknown data.

## 9. Field-by-field reconciliation

Classifications mean:

- **Reuse:** the field's meaning remains valid; references/content may still change.
- **Rename:** the meaning remains, but the current name is misleading.
- **Replace:** the old meaning is incompatible with revised content.
- **Adapter:** accepted temporarily only at a legacy boundary.
- **Archive:** preserved as historical evidence but not active revised content.

### 9.1 `EraConfigSO`

| Current field | Classification | Decision |
|---|---|---|
| `eraName` | Rename | Become `displayName`; SALIN-170 may preserve deserialization with `FormerlySerializedAs`. |
| `backgroundSprite` | Reuse | Retain the presentation reference; assign revised approved art. |
| `bannerSprite` | Reuse | Retain the presentation reference; assign revised approved art. |
| `levels` | Reuse and validate | Exactly five non-null, unique, correctly ordered revised levels per era. |
| No stable ID | Add | Add canonical era ID. |
| No explicit order | Add | Add era presentation order separate from identity. |

Three active era assets are required. The current single `Era_01` asset is not sufficient for the revised campaign.

### 9.2 `LevelConfigSO`

| Current field | Classification | Decision |
|---|---|---|
| `levelName` | Rename/re-author | Use as player-facing `displayName`; replace old content. |
| `levelNumber` | Reuse narrowly | Global order 1–15 only; not identity. |
| `chapterNumber` | Replace | Derive era/order from the campaign/era relationship. |
| `chapterName` | Replace | Derive display name from the owning era. No duplicated era identity. |
| `eraTheme` | Reuse | Point to the approved revised era theme. |
| `numberSprite` | Reuse | Presentation only. |
| `waves` | Reuse container, re-author content | Preserve ordered wave data where the revised defense rules allow it. |
| `allowedCharacters` | Adapter | Retain for legacy wave compatibility; it is not the revised learning source. |
| `allowedEnemyTypes` | Reuse container, re-author content | Populate from approved revised manifestations/enemies. |
| `bossConfig` | Reuse reference shape | Point only to the approved Paglimot encounter model after SALIN-169/SALIN-184 decisions. |
| `isAvailableInLite` | Reuse if product still supports it | Distribution metadata, never campaign identity. |
| `focusModeEnabled` | Review/replace | Keep only if the revised five-trace power rules still require this exact behavior. |
| `multiKillChainEnabled` | Review/replace | Keep only if active-clue combat approves this exact behavior. |
| `tutorialSequence` | Adapter | Legacy flow only; revised challenge phases do not depend on it. |
| `onboardingSequence` | Adapter | Legacy flow only; revised challenge phases do not depend on it. |
| `introDialogue` | Reuse reference, replace content | Retain rendering integration and assign revised narrative. |
| `outroDialogue` | Reuse reference, replace content | Retain rendering integration and assign revised narrative. |
| `bgmClip` | Reuse reference | Assign approved revised audio. |
| `hasProtagonist` | Reuse if still required | Presentation behavior only. |
| `protagonistWalksIn` | Reuse if still required | Presentation behavior only. |
| No stable ID | Add | Add canonical level ID. |
| No focus words | Add | Two inline focus-word records with ordered decompositions. |
| No cumulative pool | Add | Add explicit cumulative symbol pool and validation. |
| No phase/challenge contract | Add | Consume the approved SALIN-168 state model. |
| No save compatibility metadata | Add at campaign boundary | Link the level to the versioned campaign manifest. |

No level-specific controller or `if (levelNumber == ...)` branch may encode revised campaign identity.

### 9.3 `EraThemeSO`

| Current field | Classification | Decision |
|---|---|---|
| `eraName` | Rename | Player-facing/debug display name only; canonical identity lives in campaign content. |
| Background, ground, shrine, base-zone, foliage, bush, torch fields | Reuse | Preserve the theme container and retarget/reuse approved visuals. |
| Pillar mode/color/sprite | Reuse | Device presentation behavior is independent of campaign identity. |

Spanish/American/Japanese asset filenames may remain temporarily to preserve Unity GUIDs, but revised runtime lookup and UI must not derive identity from those filenames.

### 9.4 `BaybayinCharacterSO`

| Current field | Classification | Decision |
|---|---|---|
| `characterID` | Replace as canonical identity | Add canonical visual symbol ID and a list of legacy aliases. |
| `syllable` | Replace/extend | A symbol supports authored contextual spoken values instead of one inferred scalar value. |
| `description` | Re-author | Use reviewed educational/cultural copy. |
| `displaySprite`, `almanacSprite`, `badgeSprite`, `scrambledBadgeSprite` | Reuse references | Keep approved media and report missing/conflicting art to SALIN-167/SALIN-176. |
| `pronunciationClip` | Reuse/extend | Media must correspond to the contextual spoken value used by the occurrence. |
| `templateFileName` | Reuse per canonical symbol | One canonical visual template identity; aliases must not create duplicate learned symbols. |
| No introduction metadata | Add | Record first era/level and supported-form metadata through the content catalog. |

### 9.5 `EnemyDataSO`

| Current field/group | Classification | Decision |
|---|---|---|
| `enemyID` | Adapter/re-author | Preserve as a legacy alias where needed; revised enemies/manifestations receive stable content IDs. |
| Display name, subtitle, description | Re-author | Replace colonial roster/lore with approved revised content. |
| Movement, hearts, sprites, assigned symbol, pooling-compatible data | Reuse where approved | Technical behavior can remain after content validation. |
| `Era era` | Replace/isolate | Legacy enemy faction only. It is never the revised campaign era. |
| General aura's same-era check | Replace contract | Use an explicit faction/tag/modifier contract if the mechanic survives review. |
| Bespoke phaser, zigzag, charge, censor, aura fields | Review | Reuse only when the revised manifestation catalog explicitly approves the behavior. |

### 9.6 Dialogue, cutscenes, enemies, bosses, and level-select presentation

- Reuse rendering/controllers and reference shapes where their behavior remains valid.
- Replace old story, memory, boss, enemy, and colonial-era presentation content.
- Do not infer era identity from a scene name, asset name, enemy enum, or background.
- Level select consumes exactly three revised era configs and stable level IDs.
- The current Almanac `IsSpanishEra` reveal gate is legacy and must not gate revised discovery.

## 10. Historical progress inventory and policy

### 10.1 Preserved preferences

These remain separate from campaign progress and are carried forward unchanged:

| Key | Decision |
|---|---|
| `salinlahi.audio.master_volume` | Preserve |
| `salinlahi.audio.bgm_volume` | Preserve |
| `salinlahi.audio.sfx_volume` | Preserve |
| Future approved accessibility preferences | Preserve when introduced |

### 10.2 Archived and reset campaign data

These keys are captured in the legacy archive, but they do not grant revised mastery or completion:

| Legacy key or family | Revised decision |
|---|---|
| `SelectedLevel` | Archive old value; revised active level becomes `level.ugat.01`. |
| `salinlahi.progress.unlocked.1` through `.15` | Archive and reset. |
| `salinlahi.progress.stars.1` through `.15` | Archive and reset. |
| `salinlahi.progress.endless_unlocked` | Archive and reset; revised unlock rules decide future availability. |
| `salinlahi.tutorial.level1_ftue_seen` | Archive and reset. |
| `salinlahi.tutorial.level1_ftue_beat_index` | Archive and reset. |
| `salinlahi.tutorial.level2_advanced_focus_chain_v3_seen` | Archive and reset. |
| `salinlahi.tutorial.level2_advanced_focus_chain_v3_beat_index` | Archive and reset. |
| Older Level 2 tutorial seen/beat keys | Archive and reset. |
| `salinlahi.almanac.character_ids` | Archive and reset to revised learning state. |
| `salinlahi.discovery.enemy_ids` | Archive and reset to revised discovery state. |
| `salinlahi.almanac.boss_ids` | Archive and reset to revised discovery state. |

Legacy PlayerPrefs remain untouched for at least the v1 migration compatibility window. Revised runtime code ignores them after a completed migration marker. Later deletion requires separate approval and evidence.

## 11. Migration strategy for SALIN-171

### 11.1 Files

SALIN-171 should use files under `Application.persistentDataPath` with these logical roles:

- Primary revised save: `campaign-save.json`
- Pending atomic write: `campaign-save.tmp`
- Last valid revised backup: `campaign-save.bak`
- Immutable legacy archive: `legacy-progress-v0.json`

Exact platform-safe replace APIs are an implementation detail, but the roles and recovery behavior are mandatory.

### 11.2 First migration

1. Load and validate the embedded identity manifest before consuming campaign progress.
2. Check for a valid revised primary save.
3. If no completed revised save exists, inventory every known legacy progress key and approved preference key.
4. Serialize the complete legacy inventory plus source metadata into `legacy-progress-v0.json`.
5. Validate the archive by reading it back before creating revised progress.
6. Create a new v1 journey at `level.ugat.01` with no revised completion, mastery, rewards, memories, or discovery claims.
7. Leave approved audio/accessibility preferences in their separate preference storage.
8. Write the v1 save to `campaign-save.tmp` and validate the complete document.
9. Rotate a valid existing primary to `campaign-save.bak` when applicable.
10. Promote the valid temporary file to `campaign-save.json` through the atomic persistence boundary.
11. Record `migrationId: legacy-v0-to-revised-v1` and completion state inside the saved envelope.
12. Show the migration notice once after the valid revised save becomes active.

The legacy archive must exist before the first active revised save. Migration is idempotent: repeating any completed step yields the same revised journey and never duplicates rewards or notices.

### 11.3 Deterministic recovery order

On launch, SALIN-171 chooses data in this order:

1. A valid, schema-compatible primary with a completed transaction.
2. A valid, schema-compatible temporary file whose transaction is newer and can be safely promoted.
3. A valid, schema-compatible backup.
4. A valid legacy archive or still-present legacy PlayerPrefs, followed by an idempotent migration rerun.
5. A clean revised v1 initialization when no prior data exists.
6. A documented safe revised reset when prior files exist but none validate; corrupt files are retained for diagnostics and are not silently overwritten.

Timestamp alone never overrides schema, transaction, checksum, and structural validation.

### 11.4 Failure and rollback behavior

- If archive creation or validation fails, migration stops and legacy keys remain active and untouched.
- If temporary revised-save validation fails, discard or quarantine the temporary file; do not replace primary or set the migration marker.
- If promotion is interrupted, launch recovery selects primary, temporary, or backup using the deterministic order above.
- If post-promotion validation fails, restore the last valid backup. If no backup exists, reconstruct from the valid legacy archive. If reconstruction cannot validate, initialize the documented safe revised reset while retaining all failed inputs.
- A legacy build cannot read revised v1 progress. Supported downgrade/testing restores the legacy archive/keys in an isolated test environment; it never makes the old build interpret the v1 JSON.
- Revised runtime never combines old level completion with new mastery, memories, or unlocks.

## 12. Runtime data flow after implementation

1. Bootstrap loads the revised campaign manifest and validates supported versions.
2. The save/migration service resolves one valid v1 progress snapshot before campaign consumers initialize.
3. Level select reads canonical era and level IDs from the campaign configuration.
4. A selected level passes its stable ID to gameplay; numeric order is presentation metadata only.
5. Learning, practice, combat, context, and Results consume the same canonical symbol and level identities.
6. Atomic completion writes one coherent transaction through the SALIN-174 boundary.
7. UI, tests, and any future analytics use stable IDs and approved display data rather than legacy enums or asset names.

No consumer reads legacy PlayerPrefs directly after migration is complete.

## 13. Impact list

| Area | Impact |
|---|---|
| Level select | Configure three era assets, revised names/art, exactly five levels each, stable ID navigation, and revised lock reasons. |
| Backgrounds/themes | Reuse theme infrastructure; retarget or replace old colonial art and stop deriving identity from theme filenames. |
| Levels | Re-author 15 configs against the approved workbook and new schema; preserve compatible runtime references. |
| Characters | Reconcile 18 assets into 17 visual identities; add contextual values and aliases; validate media. |
| Enemies | Separate campaign era from legacy faction behavior; replace old roster/lore and review bespoke mechanics. |
| Bosses | Replace old colonial boss semantics with approved Paglimot scope while reusing the phase framework where valid. |
| Dialogue/cutscenes | Reuse rendering; replace narrative and memory content and stable-reference it from revised levels. |
| Saves | Replace fragmented campaign PlayerPrefs with versioned atomic JSON; preserve preferences separately. |
| Almanac/Codex/Archive | Remove the Spanish-only reveal gate; use revised discovery and canonical symbol identities. |
| Gameplay flow | Consume stable IDs and the SALIN-168/SALIN-170 contracts; no level-number identity branches. |
| Analytics | No SDK currently exists. Reserve stable IDs for future events; SALIN-166 does not add telemetry. |
| Automated tests | Replace old era identity expectations; add manifest, validation, migration, DA/RA alias, and compatibility coverage. |
| Documentation | Update the owning system docs, diagrams, GDD, and TDD when implementation changes those contracts. |
| Build/release | Embed the approved manifest and reject unsupported content/save versions before campaign play. |

## 14. Validation and error handling

SALIN-170 content validation must fail before gameplay when any of these conditions occur:

- The identity manifest is missing or has an unsupported version.
- The workbook hash in the approved manifest does not match the authored content baseline.
- There are not exactly three eras or five levels per era.
- An era, level, focus slot, symbol, spoken value, enemy, memory, or reward stable ID is missing or duplicated.
- Global level order is not exactly 1–15 or conflicts with era-local IDs.
- The campaign does not contain exactly 30 required focus-word slots.
- A focus-word decomposition references an unknown symbol/value or depends on kudlit/modified consonants.
- The symbol catalog does not resolve to 17 canonical visual symbols.
- The approved catalog does not resolve to exactly 18 contextual spoken values.
- DA and RA resolve to separate learned visual identities.
- PA is assessed before the approved instruction point.
- Cumulative pools omit required prior symbols or introduce unapproved symbols.
- Required media/reference fields are missing.
- Active revised content uses Spanish/American/Japanese as campaign era identity.

Runtime content errors fail closed with a clear diagnostic and no progress write. Save errors follow the recovery strategy in Section 11 and never silently mix schemas.

## 15. Verification strategy for follow-up tickets

### 15.1 SALIN-170 schema tests

- Manifest version support and rejection.
- Unique stable IDs and stable lookup independent of display text/order.
- Exactly 3 eras, 15 levels, 30 focus slots, 17 visual symbols, and approved contextual values.
- Correct era/level membership and global order.
- DA/RA aliasing to `symbol.dara` without duplicate learning identity.
- E/I and O/U contextual authored values.
- No kudlit or unsupported forms.
- Safe deserialization or explicit migration of existing assets.
- No revised consumer relies on `EnemyDataSO.Era` or `levelNumber` as campaign identity.

### 15.2 SALIN-171 migration tests

- No prior save.
- Every supported legacy key populated.
- Partial legacy keys.
- Already-migrated v1 save.
- Repeated migration execution.
- Missing, malformed, stale, or interrupted primary/temp/backup combinations.
- Archive write/read-back failure.
- Temporary validation failure.
- Interruption before and after promotion.
- Post-promotion validation failure and rollback.
- Unknown higher schema rejection.
- Preserved audio/accessibility preferences.
- Reset campaign completion, learning, discovery, memories, rewards, and selected level.
- One migration notice and no duplicate rewards.

### 15.3 Manual Unity checks after implementation

- Open Level Select and confirm three eras appear in Ugat, Ugnayan, Pamana order.
- Confirm each era displays exactly five correct level nodes and revised art.
- Enter representative Levels 1, 6, and 11 and confirm the correct theme/content loads by stable ID.
- Upgrade a legacy test profile and confirm the revised journey opens at Ugat Level 1 while audio settings remain unchanged.
- Force an interrupted/corrupt save case and confirm recovery chooses the documented valid source without losing the legacy archive.
- Confirm DA and RA word contexts display one visual symbol with the correct contextual pronunciation.

## 16. Documentation ownership after implementation

Architectural implementation must update the documents whose behavior changes:

- `docs/system/02_Architecture_and_Runtime_Flow.md` for bootstrap, manifest, migration, and revised campaign flow.
- `docs/system/03_Core_Systems.md` for the save/migration and progress boundaries.
- `docs/system/04_Gameplay_Systems.md` for revised era/enemy/flow semantics.
- `docs/system/05_Data_Contracts_and_ScriptableObjects.md` for the versioned campaign, era, level, symbol, and compatibility contracts.
- `docs/system/09_Test_Strategy_and_Acceptance_Criteria.md` for schema and migration acceptance.
- `docs/capstone/SystemDiagrams.md` for data, class, and runtime-flow diagrams.
- `docs/capstone/TDD.md` and `docs/capstone/GDD.md` where the revised campaign replaces documented behavior.

Each changed system document must bump its Version and Date headers. `docs/capstone/Salinlahi.md` remains explicitly excluded.

## 17. Ownership and delivery sequence

1. **SALIN-166:** approve this reconciliation, manifest versions, stable-ID policy, and migration policy.
2. **SALIN-167/SALIN-188:** approve the educational matrix, decompositions, labels, pronunciation, and cultural content.
3. **SALIN-168:** approve the reusable revised challenge/phase state model.
4. **SALIN-170:** implement the content schema, manifest, stable IDs, aliases, and editor validation.
5. **SALIN-171:** implement deterministic v0 archive and v1 migration/recovery.
6. **SALIN-172 and level stories:** author revised level assets after schema and content approval.
7. **SALIN-174/SALIN-175:** implement atomic completion and unified learning/mastery state.
8. **SALIN-185:** align active naming and archive/remove only verified legacy candidates.

SALIN-170 may begin when SALIN-166 is approved, but its final educational validation must consume SALIN-167/SALIN-188 and its challenge fields must consume SALIN-168. SALIN-171 may be designed against this contract immediately and implemented after the v1 save envelope is frozen.

## 18. Completion criteria trace

| SALIN-166 requirement | Recorded output |
|---|---|
| Field-by-field rename/reuse/replace mapping | Sections 9 and 10 |
| Runtime era identifier decision | Sections 6 and 7 |
| Versioned identity manifest | Section 8 |
| Content/save versions and supported sources | Sections 8.1 and 10 |
| Level select, backgrounds, enemies, dialogue, saves, analytics/test IDs, docs impact | Section 13 |
| Preserve/reset policy | Section 10 |
| Backup and archive policy | Section 11.1–11.2 |
| Interruption recovery | Section 11.3 |
| Rollback and downgrade behavior | Section 11.4 |
| Explicit adapters/replacements for mixed semantics | Sections 7 and 9 |
| Follow-up representation | Sections 16 and 17 |

## 19. Approved decisions

1. Use the downloaded workbook hash `33f7355f...c8e83` as the reproducible planning baseline.
2. Create new canonical revised IDs rather than relabeling old runtime identities.
3. Use semantic IDs for Ugat, Ugnayan, Pamana, their levels, and canonical symbols.
4. Keep old identifiers only in explicit migration/legacy adapters.
5. Use `Char_DA` as the preferred carrier for one canonical DA/RA visual identity; treat `Char_RA` as an alias/archive candidate.
6. Preserve reusable technical containers and media references, but replace incompatible campaign meaning/content.
7. Archive historical campaign progress and start the revised journey at Ugat Level 1.
8. Preserve audio and approved accessibility preferences separately.
9. Require a validated archive, temporary write, primary save, backup, deterministic recovery, and idempotent migration.
10. Reject unsupported schema versions and invalid content rather than guessing or silently falling back.

## 20. Handoff

Chad Denard Andrada approved this technical direction in writing on 2026-08-09. Jon Wayne Cabusbusan, the Jira reporter and revised-backlog owner, remains the required second approver for team sign-off.

The detailed completion and handoff procedure is recorded in `docs/superpowers/plans/2026-08-09-salin-166-revised-era-content-model.md`. It covers the review record, Jira workflow, explicit approvers, and the handoff contracts for SALIN-170, SALIN-171, and SALIN-185 without adding runtime implementation to this spike.
