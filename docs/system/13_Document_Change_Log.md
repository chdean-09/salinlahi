# 13 — Document Change Log
**Project:** Salinlahi
**Version:** 2.1
**Date:** 2026-08-19
**Owner:** Jon Wayne Cabusbusan

---

## Change Log

| Version | Date | Author | Summary | Impacted Documents |
|---------|------|--------|---------|-------------------|
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

| Trigger Event | Documents to Update |
|--------------|---------------------|
| Sprint 2 complete (recognition + WaveManager implemented) | 03, 04, 09, 10, 11 |
| BaybayinCharacterSO assets authored (all 17) | 05, 07, 10 |
| HUD implemented | 06, 09, 10 |
| HeartSystem implemented | 04, 09, 10 |
| Boss system implemented (Sprint 3) | 04, 05, 07, 10 |
| Lite/Full build split implemented | 08, 10 |
| UAT completed (Sprint 5) | 09, 10, 11 |
| Store submission (Sprint 6) | 08, 11, 13 |
| Dialogue system implemented (Sprint 3) | 03, 06, 09, 10 |
| Era-themed enemies implemented | 04, 07, 09, 10 |
