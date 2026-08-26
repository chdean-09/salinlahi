# 13 — Document Change Log
**Project:** Salinlahi
**Version:** 2.2
**Date:** 2026-08-27
**Owner:** Jon Wayne Cabusbusan

---

## Change Log

| Version | Date | Author | Summary | Impacted Documents |
|---------|------|--------|---------|-------------------|
| 2.2 | 2026-08-27 | Sync pass | **SALIN-186 — documentation sync after Sprints 5–7.** Fourteen tickets merged and the docs did not move with them: `LevelFlowMachine`, `LevelPhase`, `JourneyEntryResolver`, `LevelLockResolver`, `SymbolLearningCardController`, `SpokenValueResolver`, `OnLevelAttemptAborted`, `AbortCurrentLevelAttempt`, `ActiveClueDirector` and `ClueChannels` each appeared **zero** times in `docs/system/` while all ten exist in `Assets/Scripts/`. **P0:** doc 02 gains §6.1, the nine-phase LF-CONTRACT-v2 machine (SALIN-178) with its plan-driven skipping and reject rules; doc 09 gains §1.5 measured verification reality (EditMode 782/713/**69 failed**, PlayMode 132/117/**14 failed** at `1a4f28a`, characterised by fixture) and §1.6 stating that CI validates naming only and never compiles or tests, and marks CS-05 ⛔ BLOCKED (21 of 30 `pronunciationClip` fields unassigned); doc 10 was **repaired from double-encoded UTF-8** (55 of 90 lines mojibake, BOM removed), had **seven factually wrong rows corrected** — most seriously REQ-45, recorded ❌ NOT FOUND at P0 while `RecognitionLogger.cs` has been writing `recognition_log.csv` all along, and REQ-32, recorded NOT FOUND while `TracingDojo.unity` exists — had its summary counts recomputed (they did not match their own rows), and gained a Backlog Linkage section, since it previously held **zero** `SALIN-` keys. **P1:** doc 03 §8–§9 (abort semantics, dual pause latches, 9 abort subscribers, journey routing); doc 04 §13 (active-clue channels and the accessibility fallback); doc 06 §10, an implemented-vs-player-reachable table. **P2** drift is registered below rather than half-fixed. | 02, 03, 04, 06, 09, 10, 13 |
| 2.1 | 2026-08-19 | Sync pass | SALIN-143 — Resume-safety verification suite for the revised-content migration: `CampaignSaveResumeSafetyTests` (fresh install, archive-first migration, audio-preference capture, one-shot interruption at archive write and initial commit, relaunch convergence with byte-identical archive reuse, one-time notice acknowledgment, stale-generation journal quarantine, corrupt archive rebuild/safe-reset, corrupt revised-save re-migration) plus `LegacyArchiveServiceTests` idempotency/capture/wrong-campaign coverage and the shared `DictionaryLegacySource` fake. No production contract changes. | 09, 13 |
| 2.0 | 2026-08-18 | Sync pass | SALIN-175 -- Unified learning, practice, recall, and mastery data. Save schema v3 adds `progress.symbolMastery` / `progress.wordMastery` and `AppliedOutcomeReceipt.sessionKind`; `CampaignSaveMigrator.TryUpgradeV1` becomes `TryUpgradeToCurrent`, a range-guarded step chain. Outcome schema v2 adds `sessionKind` and an `evidence` batch, with `CampaignOutcomeValidator.UpgradeToCurrent` at the journal parse boundary. New `LearningTuningSO` referenced from `CampaignConfigSO.learningTuning`; new required `FocusWordDefinition.meaning`. New pure layer under `Assets/Scripts/Data/Learning/` (`MasteryEvaluator`, `ReviewScheduler`, `PracticePriority`, `LearningProgressWriter`, `LearningEvidenceRecorder`) plus the read-only `SaveManager.LearningState` surface. Coordinator dispatches on session kind so practice is structurally unable to alter level completion; Tracing Dojo records Form evidence only. | 02, 03, 04, 05, 09, 13, SystemDiagrams, TDD, GDD |
| 1.9 | 2026-06-01 | Sync pass | SALIN-109 — Replaced standalone `WaveConfigSO` ScriptableObject assets with embedded `WaveDefinition` value type inside `LevelConfigSO`. Added `allowedEnemyTypes` roster. Wave editing now happens in a single Level asset inspector with checkbox-grid cascade. Docs 04, 05, 07, and SystemDiagrams updated. | 04, 05, 07, SystemDiagrams |
| 1.8 | 2026-05-27 | Sync pass | SALIN-98 — Boss audio volume controls: add per-category `*Volume` fields and `bgmVolume` to `BossAudioBankSO` (10 designer-side `[0..1]` sliders that stack on top of the master/BGM/SFX user sliders). `AudioManager.PlaySFX` now accepts a `volumeScale` parameter forwarded to `PlayOneShot`; `AudioManager.FadeInBGM` accepts a `volumeScale` stored as `_bgmScale` and applied for the duration of the BGM (reset to `1f` on `PlayBGM`/`StopBGM`/`FadeOutBGM`). `BossAudio` passes the matching bank volume on every audio call. | 03, 04, 05, 13 |
| 1.7 | 2026-05-27 | Sync pass | SALIN-98 — Boss SFX & BGM: add `BossAudioBankSO` (per-boss audio clip bank SO), `BossAudio` component (9 EventBus subscriptions, footstep coroutine, no-immediate-repeat picker), three new EventBus boss audio events (`OnBossSummonTick`, `OnBossDrawHit`, `OnBossTeleport`), `AudioManager` fade helpers (`FadeInBGM`, `FadeOutBGM`), and `audioBank` field on `BossConfigSO`. | 02, 03, 04, 05, 13, SystemDiagrams |
| 1.6 | 2026-05-26 | Sync pass | SALIN-98 — Boss summon staggered spawning. `BossPhase` field rename + new `delayBetweenMinions`: `summonDuration`→`summonPhaseDuration`, `summonInterval`→`delayBetweenSummons`, `summonBurstMin`→`minionsPerSummonMin`, `summonBurstMax`→`minionsPerSummonMax` (all guarded by `[FormerlySerializedAs]`). `BossSummonTicker` now streams minions one at a time instead of bursting in one frame. | 04, 05, 12, 13, SystemDiagrams |
| 1.5 | 2026-05-26 | Sync pass | SALIN-97 — Added `EnemyGlyphBadge` system. Updated `04_Gameplay_Systems.md`, `05_Data_Contracts_and_ScriptableObjects.md`. Renamed `BossGlyphQueueUI` → `BossDrawCounterUI`. New SO: `GlyphBadgeConfigSO`. Two new fields on `BaybayinCharacterSO` (`badgeSprite`, `scrambledBadgeSprite`); four new fields on `EnemyDataSO`. | 04, 05, 13, SystemDiagrams, TDD, GDD, 06 |
| 1.0 | 2026-03-19 | Jon Wayne Cabusbusan (Claude Code) | Initial generation of complete documentation suite from repository inventory, Salinlahi.md, GDD.md, TDD.md, and all C# implementation files as of Sprint 1 end. 35 requirements traced. 9 P0 gaps identified. | All (00–13) |
| 1.1 | 2026-03-21 | Chad Andrada (Claude Code) | Alignment pass: reconciled system docs against GDD, TDD, Salinlahi.md, and Team README. Fixed Endless Mode unlock condition, kudlit non-goal, missing LevelSelect scene, missing EventBus events (combo/boss/AOE), separated Fast and Sprinter enemy types, added full 9-type enemy roster, added missing SO fields (hitsRequired, isBossLevel, bossConfig, baseSpawnDelay), updated BossConfigSO spec, added Credits to Main Menu, added Combo counter and Pause button to HUD, fixed chapter era names, set PPU to 32, added SUS to UAT instruments. | 01, 02, 03, 04, 05, 06, 07, 09, 10, 13 |
| 1.2 | 2026-03-25 | Chad Andrada (Claude Code) | GDD/TDD alignment pass v2: replaced generic enemy roster with era-themed enemies (Soldado, Fraile, Guardia, Capitan, Soldier, Maestro, Pensionado, General, Heitai, Kisha, Kempei, Shokan), corrected character set to 14 consonants + 3 vowels, added protagonist visibility (32×32 with 3 era designs), added dialogue system (Type A/B), added combo streak reward (5-streak slow), added shrine variants per era, updated naming conventions per Team README, added boss and dialogue test cases, added 7 new requirements (REQ-36 through REQ-42), added 2 new risks and 3 new dependencies. | All (00–13) |
| 1.3 | 2026-03-30 | Chad Andrada (Claude Code) | Sprint 2 progress pass: updated combo streak spec (resets on base hit, 5-streak slow reward, Endless Mode score contribution), corrected level flow to show intro dialogue before waves and boss only after all waves cleared, added RecognitionManager and StreakManager to manager prefab table, removed stale NOT FOUND section from Core Systems (gaps now tracked in RTM), added multi-template note to BaybayinCharacterSO spec, added Endless Mode and SUS/GEQ-S Questionnaire to UI inventory, corrected EN-05 to reflect lazy pool creation, added SUS benchmark (68+) to UAT tools, added 4 new requirements (REQ-43 through REQ-46: daily streak, in-game questionnaire, recognition CSV logging, multi-template support), updated RTM counts to 46 total / 11 P0 / 17 P1 / 5 P2, added Daily Streak and Questionnaire glossary terms, expanded prefab naming table to distinguish on-disk file names from Unity hierarchy display names. | GDD, 02, 03, 05, 06, 09, 10, 12 |
| 1.4 | 2026-05-23 | Sync pass (Claude Code) | SALIN-68 boss-encounter rework sync: documented new boss state machine (Intro/SummoningPhase/WindingDown/Vulnerable/Damaged/Outro), updated BossConfigSO + BossPhase schemas (was NOT FOUND, now Implemented), added BossSummonTicker/BossStateVisuals/PhaseBasedMovement/BossEnemy components, added six new EventBus boss events (OnBossStarted/PhaseStarted/Exhausted/Vulnerable/VulnerabilityExpired/Damaged), added GameState.Practicing + GameManager paused-run snapshot API + EnterPractice/EnterDialoguePause, replaced LegacyBossLabelIconRow with BossGlyphQueueUI + BossVulnerabilityTimerBar + BossHealthBar, documented aspect-locked play column (AspectLockedCamera + PlayAreaContainer + BaseZoneScaler + RenderOrder), deprecated GameOver scene in favor of DefeatScreenUI overlay (SALIN-58), refreshed enemy roster status (9/12 era enemies now implemented; Shielded/Sprinter prefabs removed), refreshed LevelConfigSO (added chapterNumber/chapterName/eraTheme; removed defunct isBossLevel/baseSpawnDelay), refreshed WaveConfigSO (added isIntermissionWave/enemyTypesInWave), expanded EnemyDataSO field table to include era/maxHealth/hurt-feedback/Kisha/Kempei/decoy fields, added EraThemeSO/GameConfigSO/CharacterRegistrySO stubs, added REQ-47 (aspect-locked play column), resolved RISK-07. | 02, 03, 04, 05, 06, 07, 09, 10, 11, 12, 13, SystemDiagrams |

---

## Update Instructions

When updating any document in `docs/system/`:

1. Increment the document's internal `**Version:**` field.
2. Add a row to this change log with:
   - New version number (e.g., `1.1`)
   - Date of change (ISO 8601: `YYYY-MM-DD`)
   - Author name
   - 1–2 sentence summary of what changed and why
   - List of impacted document files (e.g., `03, 10`)
3. If a gap in `10_Requirements_Traceability_Matrix.md` is resolved, update the status from `❌ NOT FOUND` or `⚠ Partial` to `✅ Implemented` and update the summary count.
4. If a new requirement is discovered, assign the next available `REQ-##` ID.
5. Changes to `12_Glossary_and_Naming_Standard.md` must be applied retroactively to all impacted documents.

---

## Planned Update Triggers

Refreshed by SALIN-186. The previous table was keyed to Sprint 2–6 events that have all since passed
(and asked for "all 17" `BaybayinCharacterSO` assets, when the authored set is **18**), so it could no
longer fire on anything.

| Trigger Event | Documents to Update |
|--------------|---------------------|
| PRs #126 / #127 merge (completion badge, Restart button wired) | **06** — the implemented-vs-reachable table in §10 |
| Pronunciation clips authored for `value.a` / `value.ei` / `value.na` / `value.ma` | 04, 06, **09 (unblocks CS-05)**, 10 |
| `numberSprite` authored for Levels 6–15 | 06, 07, 10 |
| Ugat Levels 2–5 narrative and assets land (SALIN-205, 206) | 04, 06, 07, 10 |
| Atomic persistence lands (SALIN-140) | 02 §7, 03, 05, 09, 10 |
| Campaign-wide regression coverage lands (SALIN-177) | 09, 10 |
| `QuestionnaireController` implemented (**REQ-44, the last P0 gap**) | 06, 09, 10, and `docs/capstone/EVALUATION-PROTOCOL.md` |
| Lite/Full build split implemented (REQ-28) | 08, 10 |
| Endless Mode implemented (REQ-33) | 04, 06, 10 |
| Any era's levels become playable end-to-end | 04, 06, 09, 10 |

---

## Known-Stale Register (SALIN-186, P2 — recorded not fixed)

A 3-point ticket cannot resync fourteen documents. The P0 and P1 drift above was corrected; the
following is **known-stale and deliberately left**, so that a reader knows not to trust it rather than
discovering it the hard way. Each needs its own ticket.

| Document | Known drift |
|---|---|
| `05_Data_Contracts_and_ScriptableObjects.md` | Missing `ClueChannels`, `LevelPhasePlan`, the learning-requirement contracts, and `SpokenValueDefinition`. The campaign/era identity text is current. |
| `01_System_Overview.md` | Not reassessed this pass; predates the revised-campaign model. |
| `07_Content_Pipeline.md` | Not reassessed; does not describe authoring `level.<era>.<order>` identities or focus-word decompositions. |
| `08_Mobile_Performance_and_Offline_Constraints.md` | Not reassessed. REQ-03/04/06 remain unmeasured on device. |
| `11_Risks_Dependencies_and_Mitigations.md` | Does not carry the two live content risks: 21 of 30 missing pronunciation clips, and missing `numberSprite` for Levels 6–15. |
| `12_Glossary_and_Naming_Standard.md` | Missing `LevelPhase`, `ClueChannels`, `JourneyEntryKind`, `LevelLockState`, era/level stable-id conventions. |
| `00_Documentation_Index.md` | Not verified against the sections added in this pass. |

### Cross-repository inconsistency found while syncing

**`docs/capstone/GDD.md` states the game has 17 Baybayin characters in five places** (lines 104, 147,
194, 241, 259). The authored set is **18** — 3 vowels and 15 consonants, `RA` being its own glyph where
classic Baybayin folds it into `DA` — and 121 recognition templates cover all 18. Doc 10 REQ-42 now
records this.

This is **not purely a documentation typo**: two of those five mentions describe the Kadiliman final
boss as defeated by "drawing all 17 characters in a timed sequence". If the encounter is implemented
against 17, one authored character is unreachable in the final fight. **Check the boss implementation
before editing the prose to match.**
