# Salinlahi Spec-Gap Backlog

Derived from `docs/audit/AUDIT.md` (dev @ `cb41a966`, 2026-09-11). **Re-verified 2026-09-11 against the same HEAD; see Changelog and the Re-verification table below.** Jira reconciliation lives in `docs/audit/JIRA-MERGE.md` and `docs/audit/jira-import-final.csv`. One task = one outcome, sized for a single work session (S ≈ half day, M ≈ one day, L ≈ two to three days or an art/audio batch). Tasks are numbered in execution order: every task depends only on tasks with a lower number. "Demo" marks what the demo needs. **Ruling 2026-09-11: the demo target is all 15 levels**, so every content, flow, and combat task is demo-required; only polish and accessibility tasks (T22, T23, T25, T50 to T55, T57 to T59) are nice-to-have.

**Parallel lanes.** Tasks in different lanes can run at the same time once their blockers are done:

| Lane | Tasks |
|---|---|
| Design rulings | T01, T59 |
| Content data (ScriptableObjects, Editor tools) | T02, T04, T05, T06, T09, T10, T12, T34, T35, T36, T37, T38, T39, T40, T60, T62 |
| Flow / save code | T03, T07, T08, T11, T13, T14, T20, T21, T25, T58, T63, T65, T66, T67 |
| Combat code | T15, T23, T41, T61 |
| UI screens | T16, T17, T18, T19, T22, T26, T27, T28, T29, T30, T31, T42, T43, T44, T45, T56 |
| Art | T33, T47, T64 |
| Audio | T32, T46 |
| Accessibility / settings | T50, T51, T52, T53, T54, T57 |
| Analytics | T55 |
| Dropped by ruling (ids kept) | T24, T48, T49 |

**Plan review 2026-09-11:** nine issues found (AUDIT.md §6.2), all resolved 2026-09-11 (R6 rule sheet in §6.3, R7 word tiles on paragraph units, R8 RA at Level 13); T03, T05, T11, T12, T18, T34, T35, T41, T56, T61 amended; T66 and T67 added. Known numbering exceptions (ids kept stable for the Jira import; schedule by the dependency columns, not the id): T66 must run right after T11 because T35, T37, T40, T41 depend on it; T62 must run before T56.

**Rulings log:** all questions raised by the audit were answered by the team on 2026-09-11; see `AUDIT.md` §6 and §6.1. Tasks T60 to T64 were added from those rulings.


---

## Changelog

**2026-09-11 (refresh, Part A of the Jira merge task).** Re-verified every task against `dev` at HEAD `cb41a966` (2026-09-11 20:09:57 +0800, "Merge pull request #198 from chdean-09/feature/stage-background-tiles"). This is the **same commit** the audit was written against; `git log cb41a966..HEAD` is empty, so no task can have been closed by code since the audit. Changes in this version:

- Added a **Status (re-verified)** line to all 67 tasks with fresh `file:line` evidence read at HEAD.
- **T33** downgraded to PARTIALLY-DONE and rescoped: the colonial roster was deleted before the audit base (`979db581`), the pool's default shell is `[Enemy] Corrupted.prefab`, and all 17 corruption `EnemyDataSO`s already carry walk frames, so the visual gap the task described no longer exists. The remaining work is registering the 17 ids (or silencing the fallback) and per-type pooling. The acceptance text is amended below.
- **T18 / T31 / T47** annotated: bare-glyph outline art for all 18 characters now exists (`Assets/Art/UI/GlyphOutlines/`, `BaybayinCharacterSO.glyphOutlineSprite`), Jira SALIN-209. Badge, Almanac, and level-number art gaps are unchanged.
- **T32, T46, T41** annotated as covered by existing Jira tickets (SALIN-208, SALIN-207); they stay in this file for traceability but are **not** in `jira-import-final.csv`. See `docs/audit/JIRA-MERGE.md`.
- **T05, T34, T58, T30** annotated with the Jira conflicts found in the duplicate pass (Open Questions OQ-1, OQ-3, OQ-4 in `JIRA-MERGE.md`).
- No task is NOW-DONE; none is OBSOLETE by code. T24, T48, T49 remain dropped by ruling only. Dependency order is unchanged and still valid (every `Blocked by` id is lower or listed as a known exception: T66 after T11, T62 before T56).
- **2026-09-12 (Jira import):** the 59 surviving tasks were created in Jira as **SALIN-213 to SALIN-271** (mapping in each task heading below and in `jira-import-final.csv`, column `Jira Key`); 67 Blocks and 64 Relates links created; SALIN-144/146/147/152/155/165/182/184/207/210 amended; SALIN-208/212 commented; SALIN-70 closed as Won't Do. Full log in `JIRA-MERGE.md` §Import log.
- **2026-09-12:** the ten Jira-merge Open Questions were ruled (`JIRA-MERGE.md`). T34 amended (OQ-1: two blanks; the workbook of 2026-09-11 supersedes the 2026-09-01 one-blank amendment), T35 (OQ-2), T38 (OQ-6), T58 and T20/T21 (OQ-3: checkpoint on every completed syllable). No task ids or dependencies changed.
- Previous version: 2026-09-11 (plan review R1–R9 applied). Original: 2026-09-11 (audit).

### Re-verification table (dev @ `cb41a966`)

| Task | Status | Evidence at HEAD |
|---|---|---|
| T01 | STILL-VALID | `docs/design/` holds only `scoring-and-stars.md`; rulings live only in the untracked `docs/audit/AUDIT.md` §6. SALIN-212 (Done) asked for the Q2 ruling and no code moved. |
| T02 | STILL-VALID | `Level1_Config.asset:113` `finalRestorationValue` still references `Char_NA` (guid `3eddd010…`). |
| T03 | STILL-VALID | `CampaignConfigValidator.cs:788-812` still emits Error for missing media; `CampaignSaveService.cs:62-68` still refuses the campaign on any Error. |
| T04 | STILL-VALID | `Level10_Config.asset:277-278` roster is one entry; `Level15_Config.asset:366-367` roster is one entry. |
| T05 | STILL-VALID | `ContentIdentity.cs:9-12` still declares `symbol.dara` with 18 spoken values on 17 symbols; `Char_RA.asset:17,20` `stableId` and `firstIntroductionLevelId` empty. SALIN-212 is Done in Jira but code is unchanged (Open Question OQ-4). |
| T06 | STILL-VALID | `Level1_Config.asset:15` `levelName: Level 1`, `Level6_Config.asset:15` `levelName: Level 6`; `eraTheme` at `:139` / `:206` still points at the legacy theme assets. |
| T07 | STILL-VALID | `[Manager] SaveManager.prefab:47` and `Bootstrap.unity:423` both `_campaign: {fileID: 0}`; `SaveManager.cs:32-43` selects Legacy when null. |
| T08 | STILL-VALID | `CampaignSaveDocument.cs:65-88` `LevelProgressRecord` has no per-objective flags. |
| T09 | STILL-VALID | `FocusWordPreviewController.cs:90-98` and `ActiveCluePresenter.cs:781-799` still read `symbol.syllable`; `Char_RA` has no clip of its own (`Assets/Audio/Pronunciation/` = BA DA HA KA O SA WA). |
| T10 | STILL-VALID | `challengePolicy.tier` is `1` only in `Level1_Config.asset:231`; all fourteen other configs are `tier: 0`. |
| T11 | STILL-VALID | `LevelPhasePlan.cs:51,58` still skip ContextChallenge/MemoryReward when null/empty; `challengeSequence: {fileID: 0}` in Level 6 (`:294`), 7 (`:336`), 8 (`:401`), 10 (`:287`), 13 (`:167`). |
| T12 | STILL-VALID | `learningRequirements` in every config still lists the cumulative pool as Instruction (unchanged since audit base; same commit). |
| T13 | STILL-VALID | `LevelFlowController.cs:259-262` comment: RequiredPractice auto-completes via `ExecuteStubPhase()`. |
| T14 | STILL-VALID | `finalRestorationValue` is read nowhere outside `LevelConfigSO.cs:35` and the validator (grep at HEAD). |
| T15 | STILL-VALID | `activeClueCombatEnabled: 1` only in `Level1_Config.asset`. |
| T16 | STILL-VALID | `ChallengeSession.cs:64-94` tracks hint counts; hint still applies the answer directly (`:219-246`), no modal exists under `Assets/Scripts/UI/`. |
| T17 | STILL-VALID | No `WaveClearedScreenUI` under `Assets/Scripts/UI/`; `LevelFlowController.cs:364-420` goes straight from Defense to ContextChallenge. |
| T18 | STILL-VALID | `ChallengeModeUI.cs` still builds Latin word buttons. Note: `BaybayinCharacterSO.cs:48` `glyphOutlineSprite` now exists and `Assets/Art/UI/GlyphOutlines/` has all 18 glyphs (SALIN-209 Done, verified), so the tile art input is available. |
| T19 | STILL-VALID | `VictoryScreenUI.cs:46-66` shows stars and a text summary only. |
| T20 | STILL-VALID | No `ReadyForDefenseScreenUI` under `Assets/Scripts/UI/`. |
| T21 | STILL-VALID | `DefeatScreenUI.cs:28-66` exposes Retry only. |
| T22 | STILL-VALID | `PauseMenuUI.cs:30-31,126-131` Restart restarts the whole level; no objective view. |
| T23 | STILL-VALID | No freeze/missed-clue handling in `HeartSystem.cs` or `CombatResolver.cs` (grep `Freeze|missed` empty). |
| T24 | OBSOLETE (by ruling Q15, not by code) | `ComboManager.cs` still present; removal is T63. |
| T25 | STILL-VALID | No `SaveStatusIndicator` under `Assets/Scripts/UI/`. |
| T26 | STILL-VALID | No `MemoryCardSO`, `MemoryCardUI`, or `MemoryArchive.unity` in the tree. |
| T27 | STILL-VALID | `LevelCutsceneMapping.asset` `entries: []`; `CutscenePlayer.cs:20-21,74-75,99` skip button wiring unchanged. |
| T28 | STILL-VALID | No `LevelPreviewPanel` under `Assets/Scripts/UI/`; `LevelButton.cs:197-232` loads gameplay directly. |
| T29 | STILL-VALID | No `MissionObjectiveCard` under `Assets/Scripts/UI/`. |
| T30 | STILL-VALID | `FocusWordPreviewController.cs` unchanged (single text block). SALIN-138 is Done in Jira; its AC (words readable) is met, the spec extras are not (OQ-4). |
| T31 | STILL-VALID | `SymbolLearningCardController.cs` has no stroke animation; outline art now exists (see T18). |
| T32 | STILL-VALID | `Assets/Audio/Pronunciation/` = BA DA HA KA O SA WA (7 clips); A, EI, MA, NA absent. Covered by Jira SALIN-208 (see JIRA-MERGE.md). |
| T33 | PARTIALLY-DONE (rescoped) | `Assets/Prefabs/Enemies/` now holds only `[Enemy] Corrupted.prefab` and the El Inquisidor boss (commit `979db581` deleted the colonial roster). `[Manager] EnemyPool.prefab:47` uses the Corrupted shell as the default pool and registers only `elinquisidor` (`:51`); `Enemy.cs:229-236` applies `EnemyDataSO.walkFrames[0]`, and all 17 corruption `EnemyData_*.asset` carry 4 walk frames. Enemies therefore already look distinct; what remains is the `Unknown enemyID` fallback warning (`EnemyPool.cs:194`) and per-type pooling. |
| T34 | STILL-VALID | `Challenge_Ugat02..05_Context.asset` unchanged. Jira SALIN-144/146 amended their AC on 2026-09-01 to one blank, which contradicts the workbook; ruled OQ-1 (2026-09-12): two blanks stand. |
| T35 | STILL-VALID | `Level5_Config`/`Level10_Config` still reference `BossConfig_ElInquisidor` / `BossConfig_Superintendent`; `AlmanacEnemyRegistry_Default.asset` still lists both bosses and Kadiliman (guid resolve at HEAD). |
| T36 | STILL-VALID | No `Dialogue_Ugnayan*` or `Cutscene_Ugnayan*` assets; `Level6..10_Config` `rewardIds` empty (same commit as audit). |
| T37 | STILL-VALID | `challengeSequence: {fileID: 0}` in Level 6, 7, 8, 10 (see T11). |
| T38 | STILL-VALID | `Level13_Config.asset` focus words/pool/requirements empty; `challengeSequence: {fileID: 0}` at `:167`. |
| T39 | STILL-VALID | No `Dialogue_Pamana*` or `Cutscene_Pamana*` assets. |
| T40 | STILL-VALID | No `Challenge_Pamana13_Context` / `Challenge_Pamana15_Context` assets. |
| T41 | STILL-VALID | `Level15_Config` → `BossConfig_Kadiliman` (1 phase, 3 draws, no summons, per SALIN-207 which matches HEAD data). Covered by Jira SALIN-207 (see JIRA-MERGE.md). |
| T42 | STILL-VALID | No `EraCompletionScreenUI` under `Assets/Scripts/UI/`. |
| T43 | STILL-VALID | No `Cutscene_Ending` asset; `MainMenuUI.cs:45-47,159-163` still gates Endless on story completion. |
| T44 | STILL-VALID | No `Hub.unity` under `Assets/_Scenes/`. |
| T45 | STILL-VALID | `MainMenuUI.cs:13-21,70-80` buttons: Continue/Start, Endless, Tracing Dojo, Almanac; no progress display, Exit, or archive entry. |
| T46 | STILL-VALID | 7 of 18 clips exist (see T32). Covered by Jira SALIN-208. |
| T47 | STILL-VALID | `Art/UI/GlyphBadges/` = BA DA HA KA O SA WA (7); `Art/UI/Almanac/*-Almanac.png` = 7; `Art/UI/level1..5.png` only. (Bare glyph outlines are complete: 18 files, SALIN-209.) |
| T48 | OBSOLETE (by ruling Q15) | Not a code change; kept for id stability. |
| T49 | OBSOLETE (by ruling Q15) | Not a code change; kept for id stability. |
| T50 | STILL-VALID | `SettingsPanel.cs:9-12,77-79` exposes only master/BGM/SFX sliders; no captions or narration toggle. |
| T51 | STILL-VALID | No reduced-motion setting in `SettingsPanel.cs`; `HeartDisplay.cs` has no numeral. |
| T52 | STILL-VALID | No text-speed/font-size/contrast controls in `SettingsPanel.cs`. |
| T53 | STILL-VALID | No `Vibrate` call anywhere under `Assets/Scripts/` (grep at HEAD). |
| T54 | STILL-VALID | Placeholder markers in `LevelLockNoticePanel.cs:20-44`, `ChallengeModeUI.cs:208-232`, `PauseMenuUI.cs:30-33` unchanged. |
| T55 | STILL-VALID | `Assets/Scripts/Analytics/` contains only `RecognitionLogger.cs`. |
| T56 | STILL-VALID | `AlmanacController.cs` unchanged; `MainMenuUI.cs:20,171-174` still has the Tracing Dojo button. |
| T57 | STILL-VALID | No assist settings in `SettingsPanel.cs`; `EnemyMover.cs:66-101` has no assist speed factor. |
| T58 | STILL-VALID | `GameManager.cs:216-290` snapshot is in-memory only; no Save and Exit in `PauseMenuUI.cs`. Jira SALIN-165 AC contradicted the spec here; ruled OQ-3 (2026-09-12): syllable-level checkpoint. |
| T59 | STILL-VALID | Workbook unchanged (`~/Downloads/SALINLAHI_Complete_User_Flow.xlsx`, outside the repo; unverified beyond the audit read). |
| T60 | STILL-VALID | `LevelLockNoticePanel.cs:35-44`, `VictoryScreenUI.cs:62-65,106-135`, `WaveManager.cs:27-29,817-856` still use global 1–15. |
| T61 | STILL-VALID | `Enemy.cs:218-222` attaches only the four built ability components; no new flags on `EnemyDataSO.cs`. |
| T62 | STILL-VALID | `EnemyDataSO.cs:74-75,192-196` `Era { Spanish, American, Japanese }`; no lore/lesson fields. |
| T63 | STILL-VALID | `ComboManager.cs`, `FocusModeIndicator.cs`, `FocusModeTeachBeat.cs` present; `ProgressManager.cs:17,537,613-615` and `MainMenuUI.cs:45-47,159-163` still implement Endless. |
| T64 | STILL-VALID | No `EnemyData_*` asset assigned to RA (17 corruption assets + 3 bosses in `Assets/ScriptableObjects/Enemies/`). |
| T65 | STILL-VALID | `Gameplay/Tutorial/Onboarding/Beats/FocusModeTeachBeat.cs` still the Level 2 content. |
| T66 | STILL-VALID | `LevelPhasePlan.cs:53-59` plans one Defense and one ContextChallenge; no segment list on `LevelConfigSO`. |
| T67 | STILL-VALID | `CampaignSaveDocument.cs:7` `CurrentSaveSchemaVersion = 3`; no migration for the T05/T08/T63 shape. |

Legend: STILL-VALID = gap present at HEAD; PARTIALLY-DONE = part of the gap closed at HEAD, task rescoped; OBSOLETE = withdrawn (all three by team ruling, not by code); NOW-DONE / NEW = none this round. "Unverified" appears only for T59 (the workbook lives outside the repo).
---

## Phase A — Data and foundation

### T01 (SALIN-213) — Record the six blocking spec rulings
- **Why:** Q1 (YA or PA finale), Q2 (18 or 20 spoken values), Q3 (Level 1 final syllable), Q4 (era paragraph or two words), Q5 (are Levels 5/10 boss fights), Q12 (wire the revised save now) decide the shape of a dozen later tasks. Picking answers in code would be guessing.
- **Spec IDs:** UF-37, BTN-FINAL, Character Mastery (all rows), Completion Rules "Words restored", Level Flow L1/L5/L10/L15, Global "Safe Saving".
- **Files likely touched:** new `docs/design/spec-rulings-2026-09.md`; the workbook itself.
- **Acceptance (playable test):** Not playable. Done when each question in AUDIT.md §5.1 Q1 to Q5 and §6 Q12 to Q16 has a one-line ruling, an owner, and a date, and the workbook cells that disagreed are corrected.
- **Size:** S · **Demo:** required · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `docs/design/` holds only `scoring-and-stars.md`; rulings live only in the untracked `docs/audit/AUDIT.md` §6. SALIN-212 (Done) asked for the Q2 ruling and no code moved.

### T02 (SALIN-214) — Correct Level 1 final restoration value to MA
- **Why:** `Level1_Config.finalRestorationValue` is `NA`; spec and the educational matrix say `MA` completes AMA. Any final-syllable step built later would validate the wrong symbol on the demo level.
- **Spec IDs:** Level Flow L1, Completion Rules "Last syllable", Global "Final Syllable Rule".
- **Files:** `Assets/ScriptableObjects/Levels/Level1_Config.asset`; `Assets/Editor/Campaign/CampaignLevelDataTool.cs` if the value is tool-generated.
- **Acceptance:** Opening Level1_Config in the Inspector shows `finalRestorationValue = Char_MA / value.ma`; `RevisedCampaignAssetTests` still pass.
- **Size:** S · **Demo:** required · **Blocked by:** T01
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Level1_Config.asset:113` `finalRestorationValue` still references `Char_NA` (guid `3eddd010…`).

### T03 (SALIN-215) — Split the campaign validator into blocking identity errors and content warnings
- **Why:** `CampaignConfigValidator.ValidateMedia` makes every level an Error because no context image or narration clip exists yet, and `CampaignSaveService` refuses the campaign on any Error. Plan review R1/R2: Level 13's empty content (focus words, pool, requirements, final value) is also Errors, so media alone is not enough; and T11's proposed "missing challenge = Error" would re-block boot. Rule: identity issues (manifest, era/level/symbol ids, counts, order, DA/RA) always block; content-completeness issues (media, focus words, requirements, pools, rosters, final value, challenge sequence, reward ids) are Warnings until a strict-mode switch in the release profile turns them into Errors at content-complete.
- **Spec IDs:** UF-01 ("Loads local save data"), Global "Safe Saving", Global "Audio Clues" ("Missing audio falls back to text").
- **Files:** `Assets/Scripts/Data/Validation/CampaignConfigValidator.cs:788-812`; `Assets/Tests/Editor/Data/RevisedCampaignAssetTests.cs:45-51` (remove the deferred-media exclusion once it is a Warning); `Assets/Editor/CampaignConfigValidationMenu.cs` (show warnings).
- **Acceptance:** Running Salinlahi → Campaign → Validate in the Editor lists media gaps as warnings and reports zero errors for Levels 1 to 5.
- **Size:** S · **Demo:** required · **Blocked by:** T01 (Q12)
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `CampaignConfigValidator.cs:788-812` still emits Error for missing media; `CampaignSaveService.cs:62-68` still refuses the campaign on any Error.

### T04 (SALIN-216) — Make Levels 6 to 15 combat rosters equal their cumulative pools
- **Why:** `allowedCharacters` on Levels 6 to 9 includes KA, HA, LA, NGA, PA, SA before they are taught; Level 10 is `[A]`, Level 15 is `[NGA]`. The validator (`ValidateCombatRoster`) already fails these, and boss glyph sampling reads this list.
- **Spec IDs:** Game Overview "Old syllables... remain active", Core Mechanics step 6.0, Level Flow L6 to L15.
- **Files:** `Assets/ScriptableObjects/Levels/Level6_Config.asset` to `Level15_Config.asset`; `Assets/Editor/Campaign/CampaignLevelDataTool.cs`.
- **Acceptance:** Validator reports zero `COMBAT_ROSTER_INVALID` and `CUMULATIVE_POOL_INVALID` issues; in Level 6 no enemy ever carries a glyph outside A, E/I, BA, MA, NA, TA, GA, WA.
- **Size:** M · **Demo:** required · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Level10_Config.asset:277-278` roster is one entry; `Level15_Config.asset:366-367` roster is one entry.

### T05 (SALIN-217) — Promote RA to a full 18th character (team ruling Q2, 2026-09-11)
- **Why:** The team is firm on 18 characters with DA and RA separate. Code models 17 visual symbols with `symbol.dara` carrying two spoken values, folds recognizer output RA→DA, and `Char_RA` has no `stableId`.
- **Spec IDs:** Game Overview "Character set" (to be corrected from 17 to 18), Character Mastery DA/RA row (to be split), UF-40, Level Flow L11/L13.
- **Files:** `Data/Campaign/ContentIdentity.cs:9-26` (`RevisedSpokenValueCount`, `RevisedSymbolIds`, `RevisedDaraSymbolId`); `Data/Validation/CampaignConfigValidator.cs:153-272` (symbol count, DARA rule, primary value rule); `Data/BaybayinIdCanonicalizer.cs` (remove RA→DA fold); `Gameplay/Recognition/TemplateLoader.cs`; `Char_DA.asset` (drop `value.ra`), `Char_RA.asset` (add `stableId symbol.ra`, `value.ra`, `firstIntroductionLevelId`), `CampaignConfig_RevisedV1.symbols`; Level 11/13 decompositions and pools; `docs/technical/TW-SPK-004` matrix.
- **Also (plan review R8/R9):** RA's introduction level is **Level 13** (`level.pamana.03`, HARAYA; ruling R8); Level 11 stays DA-only. Insert `symbol.ra` before `symbol.pa` in `RevisedSymbolIds`, because the last id is the finale symbol. Apply ruling Q1 here too: `RevisedFinaleSymbolId`/`SpokenValueId` and `ValidateFinalRestoration` (`CampaignConfigValidator.cs:652-661`) switch from PA to YA, `Level15_Config.finalRestorationValue` → YA, `ValidatePaInstructionOrder` stays. Update `ApprovedWorkbookSha256` when the matrix is corrected to 18.
- **Acceptance:** Validator passes with 18 symbols; Almanac shows 18 cells "Learned n/18"; drawing RA in the Dojo reports RA, not DA; HARAYA decomposes to HA · RA · YA with `Char_RA`; the finale check accepts YA.
- **Size:** M · **Demo:** required · **Blocked by:** T01
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `ContentIdentity.cs:9-12` still declares `symbol.dara` with 18 spoken values on 17 symbols; `Char_RA.asset:17,20` `stableId` and `firstIntroductionLevelId` empty. SALIN-212 is Done in Jira but code is unchanged (Open Question OQ-4).

### T06 (SALIN-218) — Rename levels to spec titles and switch to era stage backgrounds
- **Why:** Level names are legacy colonial titles and every level still uses `EraTheme_Spanish/Japanese/American` although `StageBackground_Ugat/Ugnayan/Pamana` exist. Titles surface on the level preview (T28) and lock notice.
- **Spec IDs:** Level Flow "Level Title" column, UF-09, UF-10.
- **Files:** all 15 `Level*_Config.asset` (`levelName`, `chapterNumber`, `eraTheme`); `Assets/Scripts/Gameplay/Environment/EnvironmentThemeSwapper.cs` if theme type changes.
- **Acceptance:** Starting Level 1 logs `Applied theme 'Ugat'` (not empty) and the Level Select lock notice for Level 2 names "Ang Unang Tinig" rather than "Level 1".
- **Size:** S · **Demo:** required · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Level1_Config.asset:15` `levelName: Level 1`, `Level6_Config.asset:15` `levelName: Level 6`; `eraTheme` at `:139` / `:206` still points at the legacy theme assets.

### T07 (SALIN-219) — Wire the revised campaign into SaveManager and boot in RevisedReady
- **Why:** `_campaign` is null in both the Bootstrap scene and the SaveManager prefab, so the entire atomic save, mastery, reward, and reset stack is dead code at runtime.
- **Spec IDs:** UF-01, UF-02, UF-03, UF-07, Global "Safe Saving", "Locked Content" ("Unlock state persists").
- **Files:** `Assets/Prefabs/Managers/[Manager] SaveManager.prefab:47`; `Assets/_Scenes/Bootstrap.unity:423`; `Assets/Scripts/Core/SaveManager.cs`.
- **Acceptance:** Fresh install → Main Menu shows "Start Journey"; complete Level 1 → `campaign-save.json` appears in `persistentDataPath` with `level.ugat.02 unlocked = true`; Settings shows the Reset Journey button; a corrupted `campaign-save.json` on next launch shows "Journey Save Recovered" and Level 1 is playable.
- **Size:** M · **Demo:** required · **Blocked by:** T02, T03, T04
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `[Manager] SaveManager.prefab:47` and `Bootstrap.unity:423` both `_campaign: {fileID: 0}`; `SaveManager.cs:32-43` selects Legacy when null.

### T08 (SALIN-220) — Persist per-objective completion flags and gate unlock on them
- **Why:** The save records only `completed`/`unlocked`/stars. The spec requires "Unlock checks use completed objective flags" and a "Final syllable flag is part of completion data". Without flags, T13/T14/T20/T21 have nothing to read.
- **Spec IDs:** Completion Rules (all rows), Global "Locked Content", "Final Syllable Rule", UF-27, UF-30.
- **Files:** `Assets/Scripts/Data/Persistence/CampaignSaveDocument.cs:65-88` (`LevelProgressRecord`); `CampaignProgressOutcome.cs`; `CampaignOutcomeCoordinator.cs:232-250, 300-325`; `CampaignOutcomeValidator.cs`; `Gameplay/LevelFlowController.cs:468-492`; tests in `Assets/Tests/Editor/Persistence`.
- **Acceptance:** Complete Level 1 → save shows `storyViewed`, `symbolsPracticed`, `wordsRestored`, `contextPassed`, `finalSyllableRestored` all true for `level.ugat.01`. Force any flag false via the Editor fault menu → Level 2 stays locked and the lock notice names the missing objective.
- **Size:** M · **Demo:** required · **Blocked by:** T07
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `CampaignSaveDocument.cs:65-88` `LevelProgressRecord` has no per-objective flags.

### T09 (SALIN-221) — Author spoken values and labels per the Q2 ruling
- **Why:** E/I and O/U each have one spoken value; `value.ra` reuses `DA.wav`; preview and clue text use `symbol.syllable`. The spec wants the vowel and DA/RA read from the word context.
- **Spec IDs:** Character Mastery E/I, O/U, DA/RA rows; Global "Audio Clues"; Level Flow L9, L11, L13.
- **Files:** `Char_EI.asset`, `Char_OU.asset`, `Char_DA.asset`; `Data/Validation/CampaignConfigValidator.cs:220-233` (value count); `Data/Campaign/ContentIdentity.cs:9`; `UI/HUD/FocusWordPreviewController.cs:90-98` and `UI/HUD/ActiveCluePresenter.cs:781-799` (use `SpokenValueResolver.ResolveLabel`).
- **Acceptance:** In Level 9 the focus preview reads "O · O" for OO and "U · NA" for UNA; in Level 11 the DALA card says "da" and (after T38) HARAYA's middle syllable reads "ra".
- **Size:** M · **Demo:** required · **Blocked by:** T01
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `FocusWordPreviewController.cs:90-98` and `ActiveCluePresenter.cs:781-799` still read `symbol.syllable`; `Char_RA` has no clip of its own (`Assets/Audio/Pronunciation/` = BA DA HA KA O SA WA).

### T10 (SALIN-222) — Apply challenge tiers 1 to 5 to level data
- **Why:** `ChallengeTierPolicy.ForTier` already encodes the spec's difficulty pattern (supportive retries at tiers 1-2, heart penalties at 3-5, one emergency hint with 10% penalty at 5) and combo powers by tier, but only Level 1 sets a tier.
- **Spec IDs:** Core Mechanics "Level Difficulty Pattern", UF-19, UF-25, Completion Rules "Context challenge".
- **Files:** all 15 `Level*_Config.asset` (`challengePolicy.tier` = era-local order 1 to 5).
- **Acceptance:** In Level 3, three wrong placements cost a heart and reset to the checkpoint; in Level 5 the Hint button works once, then refuses, and the results score drops by 10 points.
- **Size:** S · **Demo:** required · **Blocked by:** T01
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `challengePolicy.tier` is `1` only in `Level1_Config.asset:231`; all fourteen other configs are `tier: 0`.

---

## Phase B — Core loop

### T11 (SALIN-223) — Plan ContextChallenge and MemoryReward on every level and refuse completion when content is missing
- **Why:** `LevelPhasePlan` skips the challenge when `challengeSequence` is null and the memory when `rewardIds` is empty, so Levels 6, 7, 8, 10, 13 complete on wave clear alone — the exact silent break the spec warns about.
- **Spec IDs:** Core Mechanics "Victory: Combat alone does not complete the level", UF-23, UF-33 failure state, Global "Locked Content".
- **Files:** `Assets/Scripts/Gameplay/Flow/LevelPhasePlan.cs:43-59`; `Gameplay/LevelFlowController.cs:422-466`; `Assets/Tests/Editor/Gameplay/LevelFlowMachineTests.cs`. Plan review R2: the validator Error for a missing challenge/reward is deferred to the strict-mode switch in T03; this task is the runtime guard only.
- **Acceptance:** Temporarily clear Level 2's `challengeSequence` in the Editor, play it, clear the wave → a "content missing" panel appears and Level 3 does not unlock. Restore the asset → normal flow.
- **Size:** S · **Demo:** required · **Blocked by:** T07
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `LevelPhasePlan.cs:51,58` still skip ContextChallenge/MemoryReward when null/empty; `challengeSequence: {fileID: 0}` in Level 6 (`:294`), 7 (`:336`), 8 (`:401`), 10 (`:287`), 13 (`:167`).

### T12 (SALIN-224) — Make learning cards show only the level's new symbols
- **Why:** Every level's `learningRequirements` lists the whole cumulative pool as `Instruction`, so Level 15 shows 17 cards and a retry replays them all. Spec: "Older review symbols may be skipped in later levels."
- **Spec IDs:** UF-14, Completion Rules "Symbols practiced" (New symbols and review), Level Flow "Learning Flow" column.
- **Files:** all 15 `Level*_Config.asset` (`learningRequirements` kind Instruction only for symbols whose `firstIntroductionLevelId` is this level; review symbols stay in the list as Practice kind so it is never empty, since `ValidateRequirementList` rejects an empty list and the card controller shows Instruction only — plan review R3); `Assets/Editor/Campaign/CampaignLevelDataTool.cs`.
- **Acceptance:** Level 2 shows exactly two cards (BA, TA); Level 3 shows none and goes straight to practice.
- **Size:** S · **Demo:** required · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `learningRequirements` in every config still lists the cumulative pool as Instruction (unchanged since audit base; same commit).

### T13 (SALIN-228) — Build the Required Practice phase (in-level guided tracing)
- **Why:** The phase is a stub that auto-completes. The spec's step 5.0 and UF-15 are the core learning beat: trace each required symbol to a threshold, retry on weak traces, no heart loss.
- **Spec IDs:** UF-15, BTN-CHECK, BTN-RESET, Core Mechanics 5.0, Completion Rules "Symbols practiced", Level Flow "Learning Flow".
- **Files:** `Gameplay/LevelFlowController.cs:244-269` (new `ExecuteRequiredPractice`); new `UI/HUD/RequiredPracticeController.cs`; reuse `Gameplay/Tutorial/Level1TutorialGuideUI.cs` and `ChallengeSession` GuidedTracing mode; `Data/LevelConfigSO.cs` (per-level `practiceAccuracyThreshold`); evidence via `ProgressManager.LevelEvidence`.
- **Acceptance:** Level 1 asks for I, NA, A, MA in turn with a guide, start dot, accuracy ring, attempt counter, Check, Reset, Show Guide; a bad trace shows the missed section and retries without losing a heart; you cannot reach combat until all four pass; a retry after defeat skips symbols already passed.
- **Size:** L · **Demo:** required · **Blocked by:** T07
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `LevelFlowController.cs:259-262` comment: RequiredPractice auto-completes via `ExecuteStubPhase()`.

### T14 (SALIN-229) — Add the final-syllable trace-and-place step to restoration
- **Why:** `finalRestorationValue` is authored but never read. Spec: "Final syllable triggers the restoration scene... Validate exact symbol, trace accuracy, word order, and context slot."
- **Spec IDs:** Global "Final Syllable Rule", Completion Rules "Last syllable", UF-27, UF-37, BTN-FINAL, Level Flow "Final Required Syllable" column.
- **Files:** `Gameplay/ChallengeFlowController.cs`; `Gameplay/ChallengeSession.cs` (new final unit type or trailing GuidedTracing unit bound to `finalRestorationValue`); `Gameplay/LevelFlowController.cs:422-452`; flag write from T08.
- **Acceptance:** In Level 1, after both words are placed, the board shows AMA with its last slot empty and a "short focus moment"; tracing NA is rejected, tracing MA is accepted, the memory cutscene plays, and the save's `finalSyllableRestored` is true. Skipping this step is impossible.
- **Size:** M · **Demo:** required · **Blocked by:** T01, T08, T13
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `finalRestorationValue` is read nowhere outside `LevelConfigSO.cs:35` and the validator (grep at HEAD).

### T15 (SALIN-230) — Set per-level combat mode and clue channels (reframed by ruling Q15)
- **Why:** The 2026-09-11 combat design only requires "defeat the enemy by tracing the correct symbol", which the legacy path already does. Active-clue mode (Level 1 only today) and clue channels remain useful as the difficulty ladder (Roman + outline → image + sound → audio-only → incomplete word). This task decides per level which mode is on and authors the channels; it no longer forces active clue everywhere.
- **Spec IDs:** Core Mechanics "Enemy target", step 6.0, "Level Difficulty Pattern" Hints column, UF-17, UF-18, Level Flow "Combat Flow" column.
- **Files:** all `Level*_Config.asset` (`activeClueCombatEnabled`, `clueChannels`, `audioVisualFallback`, `multiKillChainEnabled`); verify `UI/HUD/ActiveCluePresenter.cs` renders each channel; `Gameplay/Combat/CombatResolver.cs`.
- **Acceptance:** In Level 3 exactly one enemy carries the mark; drawing any other enemy's glyph is a miss; some clues are audio-only with a replay button and Latin fallback when audio is off. In Level 4 a clue appears as an incomplete word.
- **Size:** M · **Demo:** required · **Blocked by:** T04
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `activeClueCombatEnabled: 1` only in `Level1_Config.asset`.

### T16 (SALIN-231) — Replace the free hint with the spec's hint modal and penalties
- **Why:** The current hint reveals the exact answer, has no cost, no confirmation, unlimited uses, and no exhausted state. Spec: hints "explain, reveal, or replay" but "do not automatically finish an entire required word".
- **Spec IDs:** UF-25, BTN-HINT, Global "Hints", Core Mechanics Level 3-5 hint rules.
- **Files:** `Gameplay/ChallengeSession.cs:219-246`; `Gameplay/ChallengeModeUI.cs:102, 234-245`; new `UI/HUD/HintModal.cs`; `Data/Learning/LevelResultsCalculator.cs:42-44`.
- **Acceptance:** Tapping Use Hint opens a modal listing hint types with their star cost; Cancel changes nothing; confirming applies one clue (meaning, replay, first symbol, or slot glow) and the results screen shows the penalty; when hints run out the button reads "No Hints Left" and offers review.
- **Size:** M · **Demo:** required · **Blocked by:** T10
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `ChallengeSession.cs:64-94` tracks hint counts; hint still applies the answer directly (`:219-246`), no modal exists under `Assets/Scripts/UI/`.

### T17 (SALIN-232) — Add the Wave Cleared screen
- **Why:** After the last enemy the flow jumps straight into the challenge with no acknowledgement. Spec: banner, surviving hearts, combat accuracy, "Restore the Memory" button.
- **Spec IDs:** UF-23, Core Mechanics "Victory", Completion Rules "Combat wave".
- **Files:** new `UI/WaveClearedScreenUI.cs`; `Gameplay/LevelFlowController.cs:364-420` (await the screen before ContextChallenge).
- **Acceptance:** Clearing Level 1's fifth wave shows a banner with hearts left and combat accuracy; nothing advances until "Restore the Memory" is tapped.
- **Size:** S · **Demo:** required · **Blocked by:** T11
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `WaveClearedScreenUI` under `Assets/Scripts/UI/`; `LevelFlowController.cs:364-420` goes straight from Defense to ContextChallenge.

### T18 (SALIN-233) — Rebuild the restoration board with Baybayin tiles, Submit, Undo, and Hear
- **Why:** The board is runtime-built Latin word buttons validated on tap. Spec: Baybayin tiles into word slots, Submit validates all, Undo returns a tile, wrong tiles shake and return, Hear Sentence.
- **Spec IDs:** UF-24, UF-26, BTN-SUBMIT, BTN-UNDO, BTN-HEAR, Core Mechanics 7.0, Level Flow "Restoration Flow".
- **Files:** `Gameplay/ChallengeModeUI.cs` (replace), `Gameplay/ChallengeSession.cs:168-217` (batch submit path), `Data/ChallengeSequenceSO.cs` (tile-level tokens), glyph art from `BaybayinCharacterSO.glyphOutlineSprite`.
- **Acceptance:** Level 1 shows INA and AMA as empty Baybayin slots with draggable glyph tiles; a wrong tile shakes back; Undo returns the last tile; Submit locks correct slots and reopens only wrong ones; Hear Word plays the word.
- **Plan review R7:** paragraph units (Levels 5, 10, 15) use word tiles, word-level units use syllable tiles; author each paragraph checkpoint line as its own `ChallengeUnit` so `checkpointOnSuccess` locks earlier lines. Accepted (ruling R7).
- **Size:** L · **Demo:** required · **Blocked by:** T14
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `ChallengeModeUI.cs` still builds Latin word buttons. Note: `BaybayinCharacterSO.cs:48` `glyphOutlineSprite` now exists and `Assets/Art/UI/GlyphOutlines/` has all 18 glyphs (SALIN-209 Done, verified), so the tile art input is available.

### T19 (SALIN-234) — Complete the Level Results screen
- **Why:** The victory panel shows stars and a runtime text summary. Spec lists trace, combat, and context accuracy, best combo, hearts, hints, an improvement tip, Claim Memory, Replay, View Details, and stars from the learning formula.
- **Spec IDs:** UF-28, BTN-CLAIM, BTN-REPLAY, Core Mechanics 8.0.
- **Files:** `UI/VictoryScreenUI.cs`; `Gameplay/LevelFlowController.cs:494-555`; `Data/Learning/LevelResultsCalculator.cs` (add combat accuracy, best combo).
- **Acceptance:** Finishing Level 1 with one heart lost and one hint shows 2 stars, each metric, "Best combo 7", "Hints 1", a tip, and a Claim Memory button; Replay restarts the level without removing prior stars.
- **Size:** M · **Demo:** required · **Blocked by:** T07
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `VictoryScreenUI.cs:46-66` shows stars and a text summary only.

### T20 (SALIN-235) — Add the Ready for Defense screen and a practice checkpoint
- **Why:** Combat starts the instant learning ends. Spec: preview enemy types, clue forms, hearts, "Defend the Scroll"; Back to Map keeps practice progress.
- **Spec IDs:** UF-16, BTN-READY, Global "Safe Saving" ("after practice").
- **Files:** new `UI/ReadyForDefenseScreenUI.cs`; `Gameplay/LevelFlowController.cs`; `Data/Persistence/CampaignSaveDocument.cs` (practice-complete flag from T08 persisted at this point).
- **Acceptance:** After practice a screen shows the level's enemy types and clue forms; Back to Map then re-entering the level skips story and practice and lands on this screen.
- **Size:** M · **Demo:** required · **Blocked by:** T08, T13
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `ReadyForDefenseScreenUI` under `Assets/Scripts/UI/`.

### T21 (SALIN-236) — Rebuild the Level Failed screen with checkpoint recovery
- **Why:** Defeat shows hearts and Retry, which reloads the whole level including every card. Spec: cause, missed symbols, Retry Checkpoint (wave or restoration), Review Symbols, Restart Level, Exit.
- **Spec IDs:** UF-31, UF-32, BTN-RESTART-WAVE, Global "Three Hearts" ("Do not remove completed level rewards").
- **Files:** `UI/DefeatScreenUI.cs`; `Gameplay/LevelFlowController.cs:977-995`; `Core/GameManager.cs:216-290` (promote paused-run snapshot to a checkpoint); `Gameplay/Wave/WaveManager.cs:266-306`.
- **Acceptance:** Lose all hearts on wave 3 of Level 1 → screen names the symbols that reached the Scroll; Retry Checkpoint restarts at wave 3 with full hearts and no cards; Review Symbols opens practice for those symbols.
- **Size:** M · **Demo:** required · **Blocked by:** T20
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `DefeatScreenUI.cs:28-66` exposes Retry only.

### T22 (SALIN-237) — Pause menu: Restart Wave and objective quick view
- **Why:** Restart reloads the whole level; the menu shows no objective, words, or symbols.
- **Spec IDs:** UF-22, BTN-RESTART-WAVE.
- **Files:** `UI/PauseMenuUI.cs:126-132, 222-237`; `Core/SceneLoader.cs:114-125`.
- **Acceptance:** Pause on wave 2 → Restart Wave (after confirm) restarts wave 2 only; the pause panel lists the two target words and the symbols in play.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** T20
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `PauseMenuUI.cs:30-31,126-131` Restart restarts the whole level; no objective view.

### T23 (SALIN-238) — Heart-loss beat: freeze, show the missed clue, queue it for review
- **Why:** A breach only decrements hearts. Spec: brief pause, missed clue shown, clue returned to the review queue.
- **Spec IDs:** UF-21, Global "Three Hearts".
- **Files:** `Gameplay/Base/HeartSystem.cs`, `UI/HUD/HeartDisplay.cs`, `Gameplay/Combat/ActiveClueDirector.cs`, `Feedback/BaseHitFeedbackController.cs`.
- **Acceptance:** When an enemy reaches the Scroll the field freezes for about a second, the missed glyph and syllable are shown, and that symbol reappears as a later clue in the same wave.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No freeze/missed-clue handling in `HeartSystem.cs` or `CombatResolver.cs` (grep `Freeze|missed` empty).

### T24 — DROPPED (ruling Q15, 2026-09-11): combo rewards
Combo powers and Focus Mode are cut; see T63. Kept for id stability only.
- **Status (re-verified 2026-09-11 @ cb41a966):** OBSOLETE (by ruling Q15, not by code). `ComboManager.cs` still present; removal is T63.

### T24 (original) — Combo rewards: show the active power and drop unused rewards on a miss
- **Why:** Powers are granted by tier but never displayed, and the shield survives a miss although the spec says a wrong answer "removes unused reward".
- **Spec IDs:** UF-19, Core Mechanics "Combo".
- **Files:** `Gameplay/Combat/ComboManager.cs:118-148, 201-227`; `UI/HUD/ComboDisplay.cs`.
- **Acceptance:** In a tier-5 level, five correct traces show a shield icon on the bow; a wrong trace removes it; taking a breach with the shield up costs no heart.
- **Size:** S · **Demo:** nice-to-have · **Blocked by:** T10

### T25 (SALIN-239) — Save-status indicator at safe checkpoints
- **Why:** No player-visible save feedback exists. Spec: "Small scroll icon appears, then disappears after write completes"; "Show text, not color alone".
- **Spec IDs:** Global "Safe Saving".
- **Files:** new `UI/SaveStatusIndicator.cs`; hook `CampaignSaveService.TryCommit` results via EventBus.
- **Acceptance:** Completing a level shows "Saving…" then "Saved" for about a second; a forced I/O failure shows "Save failed — retrying".
- **Size:** S · **Demo:** nice-to-have · **Blocked by:** T07
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `SaveStatusIndicator` under `Assets/Scripts/UI/`.

### T26 (SALIN-240) — Memory Card screen and Memory Archive
- **Why:** `unlockedMemoryIds` are stored but there is no card or archive. Spec: card front/back, words, Baybayin, lore, collectible number; archive grouped by era with filters and locked silhouettes.
- **Spec IDs:** UF-29, UF-39, BTN-ARCHIVE, Core Mechanics 8.0 and 9.0.
- **Files:** new `Data/MemoryCardSO.cs` (or fields on `LevelConfigSO`), new `UI/MemoryCardUI.cs`, new `Assets/_Scenes/MemoryArchive.unity` + `UI/Archive/*`; `UI/MainMenuUI.cs`.
- **Acceptance:** Claiming Level 1's memory shows a card with INA and AMA in Baybayin and Latin; the archive from the main menu shows it unlocked and Levels 2 to 15 as silhouettes with "Earn in Level n".
- **Size:** L · **Demo:** required · **Blocked by:** T19
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `MemoryCardSO`, `MemoryCardUI`, or `MemoryArchive.unity` in the tree.

---

## Phase C — Level content

### T27 (SALIN-242) — Wire the prologue cinematic with Skip and a viewed flag
- **Why:** `Level1_Opening.asset` exists but `LevelCutsceneMapping` is empty and the skip button is force-hidden. Spec: prologue plays once, skippable, recorded as seen.
- **Spec IDs:** UF-04, UF-05, BTN-SKIP.
- **Files:** `Assets/ScriptableObjects/Cutscenes/LevelCutsceneMapping.asset`; `UI/CutscenePlayer.cs:75-76, 725-729`; `Data/Persistence/CampaignSaveDocument.cs` (prologue flag, from T08); Filipino copy for the six panels.
- **Acceptance:** Fresh journey → prologue plays before Level 1 with a visible Skip; second launch skips it; Reset Journey brings it back.
- **Size:** M · **Demo:** required · **Blocked by:** T08
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `LevelCutsceneMapping.asset` `entries: []`; `CutscenePlayer.cs:20-21,74-75,99` skip button wiring unchanged.

### T28 (SALIN-243) — Level Preview screen
- **Why:** Tapping a level loads gameplay directly. Spec: title, story summary, target words, new symbols, best stars, "Enter Memory", Hear Target Words, View Symbols.
- **Spec IDs:** UF-10, BTN-NEXT-LEVEL, Core Mechanics 1.0.
- **Files:** new `UI/LevelPreviewPanel.cs`; `UI/LevelButton.cs:197-232`; `UI/VictoryScreenUI.cs:106-135` (Next Level → preview).
- **Acceptance:** Tapping Level 2 on the map shows "Mga Mata ng Bata", BATA and MATA with tap-to-hear, BA and TA as new symbols, and best stars; Enter Memory starts the level.
- **Size:** M · **Demo:** required · **Blocked by:** T06
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `LevelPreviewPanel` under `Assets/Scripts/UI/`; `LevelButton.cs:197-232` loads gameplay directly.

### T29 (SALIN-244) — Mission Objective card
- **Why:** No objective screen. Spec: story goal, language goal, combat goal, hearts, final restoration condition.
- **Spec IDs:** UF-12, Completion Rules "Story viewed".
- **Files:** new `UI/MissionObjectiveCard.cs`; `Gameplay/LevelFlowController.cs:271-311`.
- **Acceptance:** After the Level 1 story a card reads "Restore INA and AMA… final syllable MA"; Study Words continues.
- **Size:** S · **Demo:** required · **Blocked by:** T28
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `MissionObjectiveCard` under `Assets/Scripts/UI/`.

### T30 (SALIN-245) — Focus-word preview: images, tap-to-hear, Baybayin slots, inspection gating
- **Why:** The preview is one static text block.
- **Spec IDs:** UF-13, BTN-HEAR, Core Mechanics 3.0.
- **Files:** `UI/HUD/FocusWordPreviewController.cs`; `Data/Campaign/FocusWordDefinition.cs` (media already present).
- **Acceptance:** Level 1 shows two word cards with an image slot, meaning, syllable buttons that speak, empty Baybayin slots; Continue is disabled until both words were tapped.
- **Size:** M · **Demo:** required · **Blocked by:** T09
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `FocusWordPreviewController.cs` unchanged (single text block). SALIN-138 is Done in Jira; its AC (words readable) is met, the spec extras are not (OQ-4).

### T31 (SALIN-246) — Stroke-order animation on the symbol lesson
- **Why:** Cards are static. Spec: "Observe stroke animation. The symbol lights in sequence."
- **Spec IDs:** UF-14, Core Mechanics 4.0.
- **Files:** `UI/HUD/SymbolLearningCardController.cs`; reuse `Gameplay/Tutorial/TutorialAssistAnimator.cs` and `Resources/Templates/*` stroke data.
- **Acceptance:** Each Level 1 card animates the glyph stroke by stroke with Replay Stroke and Replay Sound buttons.
- **Size:** M · **Demo:** required · **Blocked by:** T12
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `SymbolLearningCardController.cs` has no stroke animation; outline art now exists (see T18).

### T32 (covered by SALIN-208) — Record and wire pronunciation clips for the Level 1 symbols
- **Why:** A, E/I, MA, NA have no audio, so the demo level is silent. Spec: "Spoken syllable" at every lesson and clue.
- **Spec IDs:** UF-14, UF-17, Character Mastery, Global "Audio Clues".
- **Files:** `Assets/Audio/Pronunciation/{A,EI,MA,NA}.wav`; `Char_A/EI/MA/NA.asset` `spokenValues[].pronunciationClip`; `Assets/Editor/BaybayinPronunciationAudioSync.cs`.
- **Acceptance:** Each Level 1 learning card plays its syllable and the replay button is visible.
- **Size:** S · **Demo:** required · **Blocked by:** T09
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Assets/Audio/Pronunciation/` = BA DA HA KA O SA WA (7 clips); A, EI, MA, NA absent. Covered by Jira SALIN-208 (see JIRA-MERGE.md).

### T33 (covered by SALIN-210) — Register the 17 corruption enemies with the pool (rescoped from "author prefabs")
- **Why (amended 2026-09-11):** The audit text and QA B-02 predate `979db581`, which deleted the colonial roster. At HEAD every corruption enemy spawns from the shared `[Enemy] Corrupted.prefab` and takes its look from `EnemyDataSO.walkFrames` (`Enemy.cs:229-236`), so enemies already look distinct. What remains: `[Manager] EnemyPool.prefab:51` registers only `elinquisidor`, so all 17 ids log `Unknown enemyID … Falling back to default pool` (`EnemyPool.cs:194`) and share one pool. Jira SALIN-210 describes the pre-`979db581` state ("spawn as Soldado") and needs its description refreshed rather than a new ticket.
- **Spec IDs:** UF-17 ("Enemies are fragments of Paglimot"), Level Flow "Combat Flow".
- **Files:** new prefabs under `Assets/Prefabs/Enemies/`; `EnemyPool` Inspector list in `Gameplay.unity`; `Assets/Tests/Editor/Data/EnemyPoolRegistrationTests.cs`.
- **Acceptance (amended):** Level 1 shows no `Unknown enemyID` warning; either each corruption id is registered against the Corrupted shell with its own capacity, or the shared-shell design is made explicit (registration list documents it and the warning is downgraded). Per-enemy prefab art is **not** required.
- **Size:** S (was L art) · **Demo:** required · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** PARTIALLY-DONE (rescoped). `Assets/Prefabs/Enemies/` now holds only `[Enemy] Corrupted.prefab` and the El Inquisidor boss (commit `979db581` deleted the colonial roster). `[Manager] EnemyPool.prefab:47` uses the Corrupted shell as the default pool and registers only `elinquisidor` (`:51`); `Enemy.cs:229-236` applies `EnemyDataSO.walkFrames[0]`, and all 17 corruption `EnemyData_*.asset` carry 4 walk frames. Enemies therefore already look distinct; what remains is the `Unknown enemyID` fallback warning (`EnemyPool.cs:194`) and per-type pooling.

### T34 (folded into SALIN-144/145/146/147) — Align Ugat Levels 2 to 5 restoration with the spec
- **Why:** L2 asks for a missing syllable instead of word-to-image matching; L3 and L4 have one blank instead of two; L5 has two syllable units instead of the paragraph or two-word ruling.
- **Spec IDs:** Level Flow L2 to L5 "Restoration Flow", Completion Rules "Words restored" and "Context challenge".
- **Files:** `Assets/ScriptableObjects/Challenges/Challenge_Ugat02..05_Context.asset`; `Assets/Editor/Campaign/Ugat0N*Tool.cs`.
- **Acceptance:** L2 asks which word is the child and which the eyes; L3 and L4 present two blanks in the sentence; L5 matches the T01 Q4 ruling.
- **Ruling OQ-1 (2026-09-12): two blanks stand.** The workbook (modified 2026-09-11) is newer than the one-blank change (`793bcd85`, `df18c2a7`, 2026-09-01) and the narrative doc (`9ca01475`, 2026-09-06). Also rewrite `docs/content/ugat-levels-2-5-narrative.md:137` ("Isang salita lamang ang kulang") and the L4 equivalent so the copy matches two blanks, and re-amend SALIN-144/146. This task is folded into SALIN-144–147 rather than imported.
- **Size:** M · **Demo:** required · **Blocked by:** T01, T18
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Challenge_Ugat02..05_Context.asset` unchanged. Jira SALIN-144/146 amended their AC on 2026-09-01 to one blank, which contradicts the workbook; ruled OQ-1 (2026-09-12): two blanks stand.

### T35 (SALIN-247) — Redesign Level 5 and Level 10 combat per the Q5 ruling
- **Why:** Both are legacy boss fights with no waves; the team ruled to follow the workbook: mixed, armored waves alternating with paragraph blanks, no boss. El Inquisidor and Superintendent are retired from the campaign.
- **Spec IDs:** Level Flow L5, L10; Completion Rules "Combat wave"; UF-17 failure state.
- **Files:** `Level5_Config.asset`, `Level10_Config.asset` (`waves`, `bossConfig`, `allowedCharacters`); `Assets/ScriptableObjects/Enemies/Boss Configs/*`.
- **Acceptance:** Level 5 plays mixed waves alternating with paragraph lines (T66), no boss phase; every glyph asked is in the Ugat pool.
- **Ruling OQ-2 (2026-09-12):** SALIN-184 is rescoped to Level 15 only; SALIN-147/152 AC change from three-phase Paglimot to waves + paragraph.
- **Also:** remove El Inquisidor and Superintendent from `AlmanacEnemyRegistry_Default` and the boss discovery flows.
- **Size:** M · **Demo:** required · **Blocked by:** T01, T04, T66
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Level5_Config`/`Level10_Config` still reference `BossConfig_ElInquisidor` / `BossConfig_Superintendent`; `AlmanacEnemyRegistry_Default.asset` still lists both bosses and Kadiliman (guid resolve at HEAD).

### T36 (SALIN-248) — Author Ugnayan (Levels 6 to 10) narrative assets
- **Why:** No intro/outro dialogue, per-word dialogue, memory cutscene, or reward ids exist for Era 2. Copy is drafted in `docs/content/ugnayan-levels-6-10-narrative.md`.
- **Spec IDs:** UF-11, UF-27, Level Flow L6 to L10 "Intro Dialogue" and "Completion Scene".
- **Files:** new `Dialogue_Ugnayan0N_*.asset`, `Cutscene_Ugnayan0N_Memory.asset`; `Level6..10_Config.asset` (`introDialogue`, `outroDialogue`, `contextMedia`, `focusWords[].media`, `rewardIds`); `Era_02.asset` story/memory references.
- **Acceptance:** Level 6 opens with dialogue, each focus word has an explanation line, and completing it plays a memory cutscene and awards `memory.ugnayan.01`.
- **Size:** L · **Demo:** required · **Blocked by:** T01, T11
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `Dialogue_Ugnayan*` or `Cutscene_Ugnayan*` assets; `Level6..10_Config` `rewardIds` empty (same commit as audit).

### T37 (SALIN-249) — Author challenge sequences for Levels 6, 7, 8, 10
- **Why:** These levels have no `challengeSequence`, so today they complete on wave clear.
- **Spec IDs:** Level Flow L6, L7, L8, L10 "Restoration Flow"; UF-24.
- **Files:** new `Challenge_Ugnayan06/07/08/10_Context.asset` (+ authoring tools); `Level6/7/8/10_Config.asset`.
- **Acceptance:** Each level's restoration matches its spec row (L6 feeling vs action, L7 drag KASAMA to the group, L8 cause/result blanks, L10 per the Q4 ruling).
- **Size:** M · **Demo:** required · **Blocked by:** T01, T11
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `challengeSequence: {fileID: 0}` in Level 6, 7, 8, 10 (see T11).

### T38 (SALIN-250) — Author Level 13 (SANGA, HARAYA) with a context-selected RA
- **Why:** Level 13 has no focus words, pool, or requirements because the authoring tool cannot select `value.ra`.
- **Spec IDs:** Level Flow L13, Character Mastery DA/RA, Global "Audio Clues".
- **Files:** `Assets/Editor/Campaign/CampaignLevelDataTool.cs` (per-syllable spoken value); `Level13_Config.asset`.
- **Acceptance:** Validator reports Level 13 clean; the focus preview shows HARAYA as "ha · ra · ya".
- **Ruling OQ-6 (2026-09-12):** DA and RA are separate; SALIN-155's AC "DA/RA shares one basic character" is to be amended.
- **Size:** M · **Demo:** required · **Blocked by:** T09
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Level13_Config.asset` focus words/pool/requirements empty; `challengeSequence: {fileID: 0}` at `:167`.

### T39 (SALIN-251) — Author Pamana (Levels 11 to 15) narrative assets
- **Why:** Same gap as T36 for Era 3; copy drafted in `docs/content/pamana-levels-11-15-narrative.md`.
- **Spec IDs:** UF-11, UF-27, Level Flow L11 to L15.
- **Files:** new `Dialogue_Pamana0N_*`, `Cutscene_Pamana0N_Memory`; `Level11..15_Config.asset`; `Era_03.asset`.
- **Acceptance:** As T36 for Level 11.
- **Size:** L · **Demo:** required · **Blocked by:** T01
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `Dialogue_Pamana*` or `Cutscene_Pamana*` assets.

### T40 (SALIN-252) — Author Level 13 challenge and the Level 15 final paragraph or two-word challenge
- **Spec IDs:** Level Flow L13, L15; UF-36, UF-37.
- **Files:** new `Challenge_Pamana13_Context.asset`; `Challenge_Pamana15_Context.asset` (per Q4).
- **Acceptance:** Level 13 restores SANGA and HARAYA in the sentence; Level 15 restores the ruled text ending in MALAYA.
- **Size:** M · **Demo:** required · **Blocked by:** T37, T38
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `Challenge_Pamana13_Context` / `Challenge_Pamana15_Context` assets.

### T41 (covered by SALIN-207) — Build the Paglimot three-phase boss for Level 15 (waves per phase, paragraph line per phase)
- **Why:** Level 15 is the legacy "Kadiliman" (4 random-glyph phases, only NGA). Ruling 2026-09-11: keep the boss and fold the mixed waves and era paragraph into it as one encounter, exactly as Level Flow L15 describes: phase 1 summons Ugat-symbol waves, phase 2 Ugnayan, phase 3 all; a paragraph line is restored after each phase; the final action traces YA into MALAYA. Dying in a phase restarts that phase only.
- **Spec IDs:** UF-35, UF-36, UF-37, UF-38 trigger, Level Flow L15, Completion Rules L5 column.
- **Files:** `Gameplay/Boss/BossController.cs`; new `Data/PaglimotBossConfigSO.cs` or extend `BossConfigSO`/`BossPhase`; `Level15_Config.asset`; `UI/Boss/*`.
- **Acceptance:** Phase 1 asks only Ugat symbols and its shield breaks after a full word is restored; dying in phase 2 restarts phase 2; the last action is tracing the ruled final syllable into MALAYA.
- **Size:** L · **Demo:** required · **Blocked by:** T01, T14, T66
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Level15_Config` → `BossConfig_Kadiliman` (1 phase, 3 draws, no summons, per SALIN-207 which matches HEAD data). Covered by Jira SALIN-207 (see JIRA-MERGE.md).

### T42 (SALIN-253) — Era Completion scene and Next Era Unlock
- **Spec IDs:** UF-33, UF-34, BTN-NEXT-ERA, Era sheets "Era Ending Line".
- **Files:** new `UI/EraCompletionScreenUI.cs`; `Gameplay/LevelFlowController.cs` (after Results on era-local level 5); `Era_0N.asset` (ending line, paragraph).
- **Acceptance:** Finishing Level 5 shows the Ugat ending line, the five memory cards, and an "Enter Next Era" button that opens Era 2 on the map.
- **Size:** M · **Demo:** required · **Blocked by:** T26, T36
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `EraCompletionScreenUI` under `Assets/Scripts/UI/`.

### T43 (SALIN-254) — Ending cinematic and post-game hub; decide Endless Mode
- **Spec IDs:** UF-38, UF-41.
- **Files:** new `Cutscene_Ending.asset`; `UI/MainMenuUI.cs:43-48, 159-169`; new post-game state.
- **Acceptance:** Completing Level 15 plays the ending, sets a campaign-complete flag, and the menu offers Mastery Challenge, Replay, Archive, Codex, Ending Gallery. Endless Mode is either specced or removed.
- **Size:** M · **Demo:** required · **Blocked by:** T41
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `Cutscene_Ending` asset; `MainMenuUI.cs:45-47,159-163` still gates Endless on story completion.

### T44 (SALIN-255) — Living Scroll hub (kept by ruling 2026-09-11)
- **Spec IDs:** UF-08, UF-06 ("Era Map" as a sub-destination).
- **Files:** new `Assets/_Scenes/Hub.unity` + `UI/Hub/*`; `Core/SceneLoader.cs`; `UI/MainMenuUI.cs`.
- **Acceptance:** Continue opens a hub with three chambers; Ugnayan and Pamana show "Complete Ugat Level 5" while locked; tapping Ugat opens the era map.
- **Size:** M · **Demo:** required · **Blocked by:** T28
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `Hub.unity` under `Assets/_Scenes/`.

### T45 (SALIN-256) — Main menu completeness: progress display, New Journey, Exit, archive entry
- **Spec IDs:** UF-03, UF-06, UF-07, BTN-START, BTN-CONTINUE, BTN-EXIT.
- **Files:** `UI/MainMenuUI.cs`; `UI/SettingsPanel.cs:112-122`; `UI/ResetJourneyConfirmationPanel.cs`; `Assets/_Scenes/MainMenu.unity`.
- **Acceptance:** Menu shows "Ugat · Level 3 · 13%"; New Journey confirms then plays the prologue; Exit confirms then quits on device; Memory Archive opens the archive.
- **Size:** M · **Demo:** required · **Blocked by:** T07, T26, T27
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `MainMenuUI.cs:13-21,70-80` buttons: Continue/Start, Endless, Tracing Dojo, Almanac; no progress display, Exit, or archive entry.

### T46 (covered by SALIN-208) — Record the remaining pronunciation clips (TA, GA, LA, NGA, PA, YA, plus RA and U per Q2)
- **Spec IDs:** Character Mastery, Global "Audio Clues".
- **Files:** `Assets/Audio/Pronunciation/*.wav`; `Char_*.asset`.
- **Acceptance:** Every learning card in Levels 2 to 15 has a working replay button; DAMA and HARAYA play different middle syllables.
- **Size:** M (audio) · **Demo:** required · **Blocked by:** T32
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. 7 of 18 clips exist (see T32). Covered by Jira SALIN-208.

### T47 (SALIN-257) — Glyph badge, almanac, and level-number art for the remaining symbols and levels
- **Why:** Badges exist for 7 of 17 symbols (Level 1's four are missing, so enemies show Latin text); Almanac art for 7; numbered scrolls for Levels 1 to 5 only.
- **Spec IDs:** UF-09, UF-17, UF-40.
- **Files:** `Assets/Art/UI/GlyphBadges/*.png`, `Almanac/*-Almanac.png`, `level6..15.png`; `Char_*.asset` `badgeSprite`/`almanacSprite`; `Level6..15_Config.asset` `numberSprite`.
- **Acceptance:** Level 1 enemies carry framed glyph badges; Era 2 and 3 on the map show numbered scrolls.
- **Size:** M (art) · **Demo:** required · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Art/UI/GlyphBadges/` = BA DA HA KA O SA WA (7); `Art/UI/Almanac/*-Almanac.png` = 7; `Art/UI/level1..5.png` only. (Bare glyph outlines are complete: 18 files, SALIN-209.)

### T48 — DROPPED (ruling Q15, 2026-09-11): bow-and-arrow presentation
The archer system is withdrawn; the slash VFX stays. Kept for id stability only.
- **Status (re-verified 2026-09-11 @ cb41a966):** OBSOLETE (by ruling Q15). Not a code change; kept for id stability.

### T48 (original) — Bow-and-arrow attack presentation (or spec change)
- **Spec IDs:** Game Overview "Combat Method", Core Mechanics "Correct answer", UF-18.
- **Files:** `Gameplay/Protagonist/ProtagonistAttackController.cs`, `ProtagonistSlashVfx.cs`, new arrow projectile prefab.
- **Acceptance:** A correct trace shows Juan draw and fire an arrow with a trail that lands on the marked enemy.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** T01 (Q14)

### T49 — DROPPED (ruling Q15, 2026-09-11): multi-lane battlefield
Lanes were part of the withdrawn combat rows. Kept for id stability only.
- **Status (re-verified 2026-09-11 @ cb41a966):** OBSOLETE (by ruling Q15). Not a code change; kept for id stability.

### T49 (original) — Multi-lane battlefield for later levels
- **Spec IDs:** Core Mechanics "Player position: Later levels add more lanes", Level Flow L2 (two lanes), L13 (three lanes).
- **Files:** `Gameplay/Wave/WaveSpawner.cs`, `Data/WaveDefinition.cs` (lane count), `Gameplay/Combat/CombatResolver.cs:429-465` (aligned target).
- **Acceptance:** Level 13 spawns enemies in three visible lanes; piercing arrow strikes the enemy behind the target in the same lane.
- **Size:** L · **Demo:** nice-to-have · **Blocked by:** T15

---

## Phase D — Polish and accessibility

### T50 (SALIN-262) — Narration toggle and captions for audio clues
- **Spec IDs:** UF-42, Global "Audio Clues" ("Captions and narration toggle are available"), "Hints" ("Narration and text alternative for every hint").
- **Files:** `UI/SettingsPanel.cs`; `Core/AudioManager.cs`; `UI/HUD/ActiveCluePresenter.cs`; new caption label.
- **Acceptance:** With Captions on, every pronunciation shows its syllable as text; with Narration off, dialogue still shows text and no voice plays.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** T32
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `SettingsPanel.cs:9-12,77-79` exposes only master/BGM/SFX sliders; no captions or narration toggle.

### T51 (SALIN-263) — Reduced motion / reduced flash, larger hearts, hearts numeral
- **Spec IDs:** UF-42, Global "Three Hearts" ("larger heart icons and reduced flash"), "Difficulty Assist" ("Reduced motion").
- **Files:** `UI/SettingsPanel.cs`; `UI/HUD/HeartDisplay.cs:208`; `Feedback/CameraShakeController.cs`, `DamageEdgeFlashController.cs`, `UI/HUD/DrawingFeedback.cs`.
- **Acceptance:** With Reduced Motion on, no camera shake or full-screen flash plays; the HUD shows "3/3" beside the hearts; Large Hearts doubles their size.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No reduced-motion setting in `SettingsPanel.cs`; `HeartDisplay.cs` has no numeral.

### T52 (SALIN-264) — Text speed, font size, high contrast, Restore Defaults, Apply
- **Spec IDs:** UF-42, BTN-SETTINGS.
- **Files:** `UI/SettingsPanel.cs`; `UI/DialogueController.cs:27`; `UI/CutscenePlayer.cs`; TMP style assets.
- **Acceptance:** Changing text speed changes the dialogue typewriter immediately; font size scales all body text; Restore Defaults resets every setting; settings survive a restart.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No text-speed/font-size/contrast controls in `SettingsPanel.cs`.

### T53 (SALIN-265) — Haptics toggle (rescoped by ruling Q13)
- **Why:** The whole-screen drawing surface is the trace pad; the left-handed pad row is amended to N/A. Only haptic feedback on a correct trace remains.
- **Spec IDs:** UF-42, UF-15 ("haptic feedback where supported").
- **Files:** `UI/SettingsPanel.cs`; `Gameplay/Combat/CombatResolver.cs` (vibrate on hit).
- **Acceptance:** With Haptics on, a correct trace vibrates the device; off, it does not; setting persists.
- **Size:** S · **Demo:** nice-to-have · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `Vibrate` call anywhere under `Assets/Scripts/` (grep at HEAD).

### T54 (SALIN-266) — Finalize English player-facing copy (rescoped by ruling Q16)
- **Why:** Lock-notice copy, hint text, challenge UI strings, and pause prompts are placeholders marked not product-approved. English applies to UI copy only; story dialogue, focus-word explanations, and cutscenes stay Filipino (ruling 2026-09-11). No language setting.
- **Spec IDs:** UF-09 lock copy, UF-25, UF-43.
- **Files:** `UI/LevelLockNoticePanel.cs:20-44`; `Gameplay/ChallengeModeUI.cs:208-232`; `UI/HUD/DrawingFeedbackVocabulary.cs`; `UI/PauseMenuUI.cs:30-33`; `UI/ResetJourneyFlow.cs:16-34`.
- **Acceptance:** Every player-facing string has been reviewed and the "placeholder" comments are removed.
- **Size:** S · **Demo:** nice-to-have · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. Placeholder markers in `LevelLockNoticePanel.cs:20-44`, `ChallengeModeUI.cs:208-232`, `PauseMenuUI.cs:30-33` unchanged.

### T55 (SALIN-267) — Emit the BTN-* tracking events to a local, offline event log
- **Spec IDs:** Button and UI Rules "Tracking Event" column (25 events), Game Overview "Offline play".
- **Files:** new `Analytics/UiEventLogger.cs` (pattern of `RecognitionLogger.cs`); hooks in each button handler.
- **Acceptance:** Playing Level 1 start to finish writes `new_journey_started`, `practice_started`, `combat_started`, `restoration_submitted`, `memory_claimed` (etc.) with timestamps to a local CSV.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** —
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Assets/Scripts/Analytics/` contains only `RecognitionLogger.cs`.

### T56 (SALIN-268) — Character Codex upgrade
- **Spec IDs:** UF-40, BTN-CODEX, BTN-PRACTICE, Character Mastery "Mastery Requirement".
- **Files:** `UI/Almanac/AlmanacController.cs`, `AlmanacDetailScroll.cs`; read `SaveManager.LearningState` (`Data/Learning/LearningStateSnapshot.cs`); link to `TracingDojo` with a preselected symbol.
- **Acceptance:** Each unlocked symbol shows spoken values, first use, later uses, and four mastery bars; Practice Symbol opens the Dojo on that symbol; locked cells read "Appears in Ugnayan". The Enemies tab (kept by ruling Q15) lists the 18 corrupted enemies keyed to their symbols with lore from T62, and no legacy colonial enemies. Free practice folds into the Codex: Practice Symbol opens the tracing surface, and the main-menu Tracing Dojo button is removed (ruling 2026-09-11).
- **Size:** M · **Demo:** required · **Blocked by:** T05, T07, T62
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `AlmanacController.cs` unchanged; `MainMenuUI.cs:20,171-174` still has the Tracing Dojo button.

### T57 (SALIN-269) — Difficulty Assist settings
- **Spec IDs:** Global "Difficulty Assist", UF-32 "Change Difficulty Assist".
- **Files:** `UI/SettingsPanel.cs`; `Gameplay/Enemy/EnemyMover.cs` (speed factor); `Gameplay/Recognition/StrokeCapture.cs` (multi-stroke window); `UI/HUD/TraceHintPresenter.cs` (guide opacity); `UI/VictoryScreenUI.cs` (assist icon in details).
- **Acceptance:** Turning on Assist slows enemies and extends the trace window; the results details show an assist icon; all mandatory objectives remain required.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** T10
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No assist settings in `SettingsPanel.cs`; `EnemyMover.cs:66-101` has no assist speed factor.

### T58 (SALIN-270) — Persist the mid-level checkpoint and implement Save and Exit
- **Spec IDs:** UF-43, BTN-EXIT, Global "Safe Saving" ("wave clear, restoration checkpoint").
- **Files:** `Core/GameManager.cs:216-290` (snapshot → save document); `Data/Persistence/CampaignSaveDocument.cs`; `UI/PauseMenuUI.cs:30-33, 239-250`.
- **Acceptance (ruling OQ-3, 2026-09-12):** the checkpoint is written after every cleared wave and after every completed syllable placement in restoration. Pause on wave 3, Save and Exit, kill the app, relaunch → Continue resumes at wave 3 with the same hearts. Place two of INA's syllables, Save and Exit, relaunch → the board reopens with those two syllables locked and the third empty. SALIN-165's "no partial checkpoint" AC is amended to this rule.
- **Size:** M · **Demo:** nice-to-have · **Blocked by:** T20
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `GameManager.cs:216-290` snapshot is in-memory only; no Save and Exit in `PauseMenuUI.cs`. Jira SALIN-165 AC contradicted the spec here; ruled OQ-3 (2026-09-12): syllable-level checkpoint.

### T60 (SALIN-258) — Present levels as Era N, Level 1 to 5 everywhere
- **Why:** Team ruling 2026-09-11. Data already carries `eraLocalOrder` and era-scoped stable ids, but the lock notice, victory screen, wave manager registry, level names, and dialogue copy use global 1 to 15.
- **Spec IDs:** Game Overview "Structure" (3 eras, 5 levels), UF-09, UF-10, UF-28, UF-30.
- **Files:** `UI/LevelLockNoticePanel.cs:35-44`; `UI/VictoryScreenUI.cs:62-65, 106-135` (last-level check by era); `Gameplay/Wave/WaveManager.cs:27-29, 817-856` (registry by stable id); `Data/LevelConfigSO.cs:9-16`; all `Level*_Config.asset` `levelName`; `docs/content/*` naming convention (Ugnayan doc decision 1).
- **Acceptance:** Every player-facing string reads "Ugat · Level 2" or "Era 1 · Level 2", never "Level 7"; the lock notice for Era 2 Level 1 says "Finish Ugat Level 5"; results after Ugat Level 5 do not offer "Next Level" but the era completion flow.
- **Size:** M · **Demo:** required · **Blocked by:** T06
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `LevelLockNoticePanel.cs:35-44`, `VictoryScreenUI.cs:62-65,106-135`, `WaveManager.cs:27-29,817-856` still use global 1–15.

### T61 (SALIN-259) — Implement the designed abilities for the corrupted enemies
- **Rule sheet (ruling R6):** AUDIT.md §6.3 gives every enemy a combat-time rule mapped to an existing hook. Split into one sub-task per enemy; Ugat six first.
- **Why:** The 2026-09-11 combat design gives each of the 17 enemies a signature ability. Four are built (Iligaw mirror copy, Labo phasing, Mantsa staining, Takip covering), two are partial (Salungat decoy penalty, Walang-Awa armor), eleven have only `description` text (AUDIT.md §6.1 table).
- **Spec IDs:** Combat design §6.1 (replaces Core Mechanics combat rows), UF-17.
- **Files:** `Data/EnemyDataSO.cs` (new ability flags); `Gameplay/Enemy/Enemy.cs:218-222` (`EnsureAbilityComponent` hooks); new controllers under `Gameplay/Enemy/` per ability; `Gameplay/Recognition/StrokeCapture.cs` (Gapos slows tracing); `UI/DialogueController.cs` (Nawalang Mukha hides names); `Gameplay/ChallengeSession.cs` (Punit tears sentences, Yapos traps the final symbol); `Assets/Tests/Editor/Gameplay/CorruptionSignatureAbilityTests.cs`.
- **Acceptance:** One task per ability is acceptable if split; the umbrella is done when every enemy in the §6.1 table shows a visible in-play effect matching its description and `CorruptionSignatureAbilityTests` covers each flag. Order of value for the Ugat demo: Abo ng Simula, Uhaw, Bakod, Nawalang Mukha first.
- **Size:** L (split per enemy) · **Demo:** required for the Ugat six · **Blocked by:** T33
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Enemy.cs:218-222` attaches only the four built ability components; no new flags on `EnemyDataSO.cs`.

### T62 (SALIN-260) — Enemy era, lore, and lesson data; retire the colonial era enum
- **Why:** `EnemyDataSO.era` is `Spanish/American/Japanese` and the Almanac reveals only Spanish-era entries. The new design assigns each enemy to Ugat/Ugnayan/Pamana and adds corrupted meaning, true meaning, lore, and restored lesson text that no field holds.
- **Spec IDs:** Combat design §6.1, UF-40, enemy discovery overlay.
- **Files:** `Data/EnemyDataSO.cs:73-75, 192-197` (enum → Ugat/Ugnayan/Pamana; new `corruptedMeaning`, `trueMeaning`, `lore`, `restoredLesson` fields); all 17 `EnemyData_*.asset`; `Gameplay/Enemy/GeneralAura.cs` (reads `era`); `UI/Almanac/AlmanacController.cs:159-162, 252`; `UI/EnemyDiscoveryCopyProvider.cs`; `UI/Almanac/AlmanacDetailScroll.cs`.
- **Acceptance:** Defeating Mantsa in Level 1 shows a discovery card with its ability, corrupted meaning, true meaning, and restored lesson; the Almanac groups enemies by the spec era pools (ruling C2: Takip joins Ugat; Uhaw and Yapos ng Dilim join Ugnayan); the `era` enum is Ugat/Ugnayan/Pamana (ruling C4).
- **Size:** M · **Demo:** required (Ugat six) · **Blocked by:** T05
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `EnemyDataSO.cs:74-75,192-196` `Era { Spanish, American, Japanese }`; no lore/lesson fields.

### T63 (SALIN-225) — Remove combo powers, Focus Mode, and Endless Mode (ruling Q15)
- **Why:** All three are cut. Leaving them in place keeps dead code paths, an inert main-menu button, and Level 2 onboarding beats that teach a mechanic that no longer exists.
- **Spec IDs:** Q15 ruling; UF-06 (menu), UF-19 (withdrawn), UF-38/UF-41 (post-game unlock).
- **Files:** `Gameplay/Combat/ComboManager.cs`, `ComboPower.cs`; `Data/GameConfigSO.cs:8-39`; `UI/HUD/ComboDisplay.cs`, `FocusModeIndicator.cs`; `Gameplay/Enemy/EnemyMover.cs` (focus speed multiplier); `Gameplay/Combat/CombatResolver.cs:386-427` (power effects); `Gameplay/Base/HeartSystem.cs:75-68` (shield check); `Gameplay/Tutorial/Onboarding/Beats/ComboTeachBeat.cs`, `FocusModeTeachBeat.cs`, `Level2AdvancedOnboardingSequence.asset`; `UI/MainMenuUI.cs:32-48, 159-169` and `ProgressManager.cs:611-640` (Endless); `CampaignSaveDocument.cs:39` (`endlessModeUnlocked`, keep field for save compatibility or migrate); related tests (`ComboManagerTests`, `ComboPowerGrantTests`, `ComboPowerResolverTests`, `ComboShieldHeartLossTests`, `ComboTeachBeatTests`).
- **Acceptance:** No combo counter or Focus Mode indicator appears in any level; Level 2 onboarding no longer teaches combos; the main menu has no Endless Mode button; completing Level 15 no longer sets an endless flag; the suite is green with the listed tests removed or rewritten.
- **Size:** M · **Demo:** required · **Blocked by:** T07
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `ComboManager.cs`, `FocusModeIndicator.cs`, `FocusModeTeachBeat.cs` present; `ProgressManager.cs:17,537,613-615` and `MainMenuUI.cs:45-47,159-163` still implement Endless.

### T65 (SALIN-241) — Design and build the Level 2 onboarding replacement
- **Why:** T63 removes the ComboTeach and FocusModeTeach beats that make up `Level2AdvancedOnboardingSequence`. The team will supply what Level 2 teaches instead (Level 1 already covers drawing, the base, and heart loss). Confirmed 2026-09-11.
- **Spec IDs:** UF-05 ("tutorial active"), Level Flow L2 "Learning Flow".
- **Files:** new beats under `Gameplay/Tutorial/Onboarding/Beats/`; `Data/OnboardingSequenceSO.cs`; `Level2AdvancedOnboardingSequence.asset`; `Gameplay/LevelFlowController.cs:674-694` (runtime beat list); `Assets/Tests/Editor/Onboarding/*`.
- **Acceptance:** A first-time player entering Level 2 sees the new onboarding once; second entry skips it; Reset Journey brings it back.
- **Size:** M · **Demo:** required · **Blocked by:** T63 (and the replacement design)
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `Gameplay/Tutorial/Onboarding/Beats/FocusModeTeachBeat.cs` still the Level 2 content.

### T64 (SALIN-261) — Design and author the RA enemy
- **Why:** Ruling C1: DA and RA are separate characters, each with its own enemy. Daan-Lihis stays with DA. RA has no enemy design, asset, or ability yet.
- **Spec IDs:** Combat design §6.1 (new row), Character Mastery (RA row after the split), Level Flow L13 (HARAYA).
- **Files:** new `EnemyData_<RAEnemy>.asset` with description, corrupted meaning, true meaning, lore, restored lesson; prefab and art; `EnemyPool` registration; Level 13 and Level 15 waves; `AlmanacEnemyRegistry_Default.asset`.
- **Acceptance:** Level 13 spawns the RA enemy carrying `Char_RA`; drawing RA defeats it and drawing DA does not; the Almanac Enemies tab shows it in Pamana with its lore.
- **Size:** M (design) + L (art) · **Demo:** required · **Blocked by:** T05, T62
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. No `EnemyData_*` asset assigned to RA (17 corruption assets + 3 bosses in `Assets/ScriptableObjects/Enemies/`).

### T66 (SALIN-226) — Support alternating defense and restoration segments in the level flow
- **Why:** Plan review R4. Level Flow L5 says "Combat alternates with paragraph blanks", L10 says "Combat and restoration run continuously", and L15 restores a paragraph line after each boss phase. `LevelFlowMachine` and `LevelPhasePlan` run Defense exactly once, then ContextChallenge once. Nothing in the backlog added the capability, yet T35, T37, T40, T41 all assume it.
- **Spec IDs:** Level Flow L5/L10/L15, Core Mechanics "Victory: Later levels combine both without pause", UF-23, UF-24, UF-36.
- **Files:** `Data/LevelConfigSO.cs` (segment list: wave group → challenge unit ids → wave group …); `Gameplay/Flow/LevelPhasePlan.cs`, `LevelFlowMachine.cs` (loop Defense/ContextChallenge segments; AtomicSave and Results stay terminal); `Gameplay/LevelFlowController.cs:364-452`; `Gameplay/Wave/WaveManager.cs` (start at a wave index); `Gameplay/ChallengeFlowController.cs` (play a unit subset); segment boundary becomes the checkpoint for T20/T21; `LevelFlowMachineTests`.
- **Acceptance:** In Level 5, clearing waves 1 and 2 opens paragraph line 1; a correct restoration resumes wave 3; dying in wave 3 restarts wave 3 with line 1 still locked.
- **Size:** L · **Demo:** required · **Blocked by:** T11
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `LevelPhasePlan.cs:53-59` plans one Defense and one ContextChallenge; no segment list on `LevelConfigSO`.

### T67 (SALIN-227) — Bump the save schema and migrate or reset development saves
- **Why:** Plan review R5. T05 renames `symbol.dara` to `symbol.da` plus `symbol.ra`, T08 adds objective flags, T63 removes the endless flag. `CampaignSaveValidator` rejects unknown symbol ids, so every existing dev save would fail validation and block boot with a recovery notice.
- **Spec IDs:** Global "Safe Saving" ("Never corrupt prior completed progress"), UF-01.
- **Files:** `Data/Persistence/CampaignSaveDocument.cs:7` (`CurrentSaveSchemaVersion`); `CampaignSaveMigrator.cs`; `CampaignSaveValidator.cs`; `Assets/Tests/Editor/Persistence/CampaignSaveMigrationTests.cs`.
- **Acceptance:** A save written before T05 and T08 opens afterwards with either a migration or a safe-reset notice, and Level 1 is playable; no silent boot block.
- **Size:** M · **Demo:** required · **Blocked by:** T05, T08, T63
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. `CampaignSaveDocument.cs:7` `CurrentSaveSchemaVersion = 3`; no migration for the T05/T08/T63 shape.

### T59 (SALIN-271) — Update the workbook with the code-is-better findings
- **Why:** AUDIT.md §5.5 lists seven behaviours (atomic save, flow machine, deterministic clue, hidden recognizer score, mastery model, reset copy, star formula) that should become spec.
- **Spec IDs:** Global "Safe Saving", Core Mechanics "Enemy target" and "Victory", UF-20, UF-28, UF-40, UF-07.
- **Files:** the workbook; `docs/system/06_UI_UX_and_Player_Flow.md` (stale sections noted in QA).
- **Acceptance:** Each of the seven items has a spec row that a future audit would mark DONE.
- **Size:** S · **Demo:** nice-to-have · **Blocked by:** T01
- **Status (re-verified 2026-09-11 @ cb41a966):** STILL-VALID. Workbook unchanged (`~/Downloads/SALINLAHI_Complete_User_Flow.xlsx`, outside the repo; unverified beyond the audit read).

---

## Demo-required subset (playable Ugat, Levels 1 to 5), in order

T01 → T02 → T03 → T07 → T08 → T11 → T12 → T13 → T14 → T15 → T16 → T17 → T18 → T19 → T21 (via T20) → T27 → T28 → T32 → T33 → T34 → T35

Art (T33) and audio (T32) can start on day one. T04, T06, T10 are cheap data tasks that unblock several of the above and are worth doing early even though they are not strictly demo-required.
