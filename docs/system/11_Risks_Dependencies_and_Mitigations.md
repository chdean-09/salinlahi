# 11 — Risks, Dependencies, and Mitigations
**Project:** Salinlahi
**Version:** 1.5
**Date:** 2026-08-31
**Owner:** Jon Wayne Cabusbusan (Scrum Master)

---

## 1. Technical Risks

| Risk ID | Risk Description | Probability | Impact | Owner | Mitigation | Trigger Condition |
|---------|-----------------|-------------|--------|-------|-----------|------------------|
| RISK-01 | $P recognizer accuracy insufficient at 0.60 confidence for Baybayin characters — too many false positives or negatives | Low (was High) | Critical | Jon Wayne | **LARGELY MITIGATED.** Leave-one-out evaluation over the 121 authored templates now scores 121/121. The last outstanding failure was HA, which was misrecognized because `ScaleToSquare` normalized per-axis and discarded the aspect ratio that defines it (HA bounding-box aspect 5.53–12.77 vs ≤3.94 for every other character); the aspect-aware hybrid in `ScaleToSquare` took LOO from 119/121 to 121/121. `minimumConfidence` remains `0.60`. **Residual risk:** LOO over authored templates is not the same as accuracy on naive first-time players, which is what UAT must still establish. | UAT shows < 70% acceptance on correct draws OR > 20% acceptance on clearly wrong draws |
| RISK-02 | Recognition latency exceeds 50ms budget on target Android hardware | Medium | High | Jon Wayne | Profile `DollarPRecognizer` on lowest-spec target device in Sprint 2; if over budget, reduce `resamplePointCount` incrementally (32→24→16) and re-test accuracy | Stopwatch measurement exceeds 50ms at p95 |
| RISK-03 | Object pool max size (20) exceeded during boss waves, causing runtime `Destroy` calls and GC spikes | Low | Medium | Jon Wayne | Profile enemy count during boss wave design; increase `_maxSize` in `EnemyPool` Inspector before Sprint 3 boss integration. `BossSummonTicker` summon ticks add 2–3 enemies per `summonInterval`, so pool sizing was reviewed before SALIN-68 wire-up; `BossConfigSO.summonHorizontalBounds` clamps summons on-screen. | Unity Profiler shows `Destroy` calls during active gameplay |
| RISK-04 | ~~`WaveManager` implementation deferred beyond Sprint 2 blocks all gameplay testing~~ | — | — | — | **RESOLVED** — `WaveManager.cs` implemented | — |
| RISK-05 | ~~`HeartSystem` not implemented causes GameOver to never trigger~~ | — | — | — | **RESOLVED** — `HeartSystem.cs` implemented | — |
| RISK-06 | ~~Template .txt files for all 18 characters not authored before Sprint 2 integration~~ | — | — | — | **RESOLVED** — 121 template files authored in `Assets/Resources/Templates/`, covering all 18 characters | — |
| RISK-07 | ~~`BossConfigSO` and boss phase system require significant design time; Sprint 3 scope may slip~~ | — | — | — | **RESOLVED** — `BossController` + `BossConfigSO` + `BossPhase` fully implemented at `Assets/Scripts/Gameplay/Boss/` and `Assets/Scripts/Data/` | — |
| RISK-08 | iOS build submission requires paid Apple Developer account; not confirmed provisioned | Unknown | High | Ian Clyde | Confirm Apple Developer account status before Sprint 4; if unavailable, submit Android only for Sprint 4 testing | Sprint 4 build target requires iOS IPA |
| RISK-09 | Duplicate event subscriptions cause double-firing (e.g., scene reload without OnDisable cleanup) | Medium | Medium | All | Enforce OnEnable/OnDisable pattern strictly; add CS-03 regression test to every sprint checklist | Console shows duplicate event handler errors |
| RISK-10 | ~~`ScriptableObjects/Characters/` folder empty — no BaybayinCharacterSO assets authored~~ | — | — | — | **RESOLVED** — all 18 `Char_*.asset` files authored, plus `CharacterRegistry_Default.asset` | — |
| RISK-11 | GC spikes on low-RAM Android (1–2 GB) from non-obvious allocations in hot paths | Low | Medium | Jon Wayne | Run Memory Profiler in Sprint 4; audit all `Update()` paths for hidden allocations (string interpolation, LINQ, boxing) | Frame time chart shows irregular spikes > 3ms |
| RISK-12 | `Time.timeScale = 0` persists into next scene if SceneLoader is bypassed (e.g., direct `SceneManager.LoadScene` call) | Low | High | Jon Wayne | Never call `SceneManager.LoadScene` directly; all scene loads must go through `SceneLoader` — enforced by code review | MainMenu scene loads but game appears frozen |
| RISK-13 | Dialogue system scope creep — Type A panels and Type B popups may consume more Sprint 3 time than budgeted | Medium | Medium | Chad | Dialogue is Moderate scope per Team README §12. Type B popups are explicitly cuttable. If behind, scale to Minimal (text crawl only). | Sprint 3 Day 5 with no working Type A panel |
| RISK-14 | Boss battle balancing — too hard frustrates, too easy feels anticlimactic | Medium | Medium | Chad | Start boss phase timers generous; tighten based on playtest feedback. Playtest bosses separately and early. | Internal playtest shows > 80% fail rate or < 20% fail rate on boss |
| RISK-15 | Aspect-locked play column requires every gameplay UI to live under `PlayAreaContainer`; new UI authored against the device viewport will visibly misalign on tablets | Low | Medium | Jon Wayne | Code review checklist for new gameplay UI: parent under `PlayAreaContainer`. `BaseZoneScaler` and `DrawingCanvas` already subscribe to `OnPlayAreaChanged` and re-fit automatically. | New HUD element appears outside the 9:16 column on a tablet aspect ratio |

---

## 2. Production Dependencies

| Dep ID | Dependency | Type | Owner | Required By | Status |
|--------|-----------|------|-------|------------|--------|
| DEP-01 | All 18 BaybayinCharacterSO assets authored with valid `characterID`, `syllable`, `displaySprite` | Content | Chad | Sprint 2 integration | ✅ DONE — 18 `Char_*.asset` present |
| DEP-02 | Recognition template `.txt` files in `Assets/Resources/Templates/` covering all 18 characters | Content | Chad | Sprint 2 DollarPRecognizer | ✅ DONE — 121 template files present |
| DEP-03 | All 17 taught-set pronunciation `AudioClip` assets assigned in `BaybayinCharacterSO.pronunciationClip` (`Char_RA` excluded: no spoken value, taught by no level) | Content/Audio | Ian Clyde | Sprint 2 audio integration | ⚠️ PARTIAL — 7 of 18 assigned; **11 missing**. Audio feedback on recognition is silent for those 11 characters. |
| DEP-04 | `[Enemy] Standard.prefab` has `EnemyDataSO` assigned in Inspector | Configuration | Chad | Sprint 1 wave testing | LIKELY DONE — prefab exists |
| DEP-05 | PlayerBase `GameObject` exists in Gameplay scene with tag `"PlayerBase"` | Scene setup | Jon Wayne | Sprint 2 `EnemyMover` base-hit test | ✅ DONE — tag defined in `ProjectSettings/TagManager.asset`; present in `Gameplay.unity` and `Level_01_Tutorial.unity` |
| DEP-06 | Android keystore signed and configured | Build | Ian Clyde | Sprint 1 Android build (confirmed via git commit) | ✅ DONE — commit `ddc6ea3` |
| DEP-07 | Apple Developer Program account provisioned | Build | Ian Clyde | Sprint 4 iOS submission | UNKNOWN |
| DEP-08 | Levels 1–3 LevelConfigSO assets (with embedded WaveDefinitions) fully authored | Content | Chad | Sprint 1–2 wave system | NOT CONFIRMED |
| DEP-09 | BGM audio clip for Gameplay scene | Audio | Ian Clyde | Sprint 2 audio integration | NOT CONFIRMED |
| DEP-10 | Base hit and game over SFX clips | Audio | Ian Clyde | Sprint 2 audio stubs replaced | NOT CONFIRMED |
| DEP-11 | DialogueSequence ScriptableObjects (30–40 assets, 2 per level + boss intros/outros) | Content | Chad | Sprint 3 dialogue integration | NOT CONFIRMED |
| DEP-12 | Dialogue portrait sprites (6–10 total, 96×96 px) | Art | Pixel Artist | Sprint 3 (Art Batch 3) | NOT CONFIRMED |
| DEP-13 | 3 shrine sprite variants (Baybayin Altar, Ancestral Door, Scroll Shrine) at 64×96 px with 4 damage states each | Art | Pixel Artist | Sprint 2 (Art Batch 1 for Spanish shrine) | NOT CONFIRMED |

---

## 3. Dependency Graph (Critical Path)

```
BaybayinCharacterSO assets (DEP-01)
  └─ DollarPRecognizer integration (REQ-10, REQ-11, REQ-12)
       └─ WaveManager + WaveSpawner (REQ-26, REQ-27)
            └─ HeartSystem (REQ-15, REQ-16, REQ-17)
                 └─ Full core gameplay loop complete
                      └─ HUD implementation (REQ-30)
                           └─ Sprint 2 "game is playable" milestone

Template files (DEP-02)
  └─ DollarPRecognizer integration (concurrent with DEP-01)

Pronunciation clips (DEP-03)
  └─ Audio feedback system (REQ-19)
       └─ Sprint 2 milestone
```

---

## 4. Mitigations in Effect (Current Sprint)

| Item | Mitigation Applied | Evidence |
|------|-------------------|----------|
| Runtime Instantiate/Destroy risk | EnemyPool with Unity ObjectPool<Enemy> eliminates game-loop allocations | EnemyPool.cs lifecycle |
| Scene timeScale lock risk | SceneLoader always resets Time.timeScale = 1f before load | SceneLoader.cs, LoadRoutine() |
| Duplicate Singleton risk | Singleton<T> destroys duplicates in Awake | Singleton.cs |
| EventBus memory leak risk | OnEnable/OnDisable pattern enforced + documented | EventBus.cs comment; GameManager.cs; AudioManager.cs |
| Release log overhead risk | DebugLogger with compile-symbol strip | DebugLogger.cs |
| One-dimensional character misrecognition | `ScaleToSquare` scales uniformly above `ONE_D_ASPECT_THRESHOLD` (4.5) instead of per-axis, preserving the aspect ratio that defines HA | DollarPRecognizer.cs, `ScaleToSquare` |
| Test artifacts shipping in release content | `PerformanceTestRun*.json` removed from `Assets/Resources/` and gitignored; they are regenerated per batchmode run and no longer dirty the tree | `.gitignore` (SALIN-185 block); PR #145 |

---

## 5. Sprint Timeline Risk Summary

> **Historical.** Every sprint below closed on or before 2026-05-29. The table is retained as the original plan of record and is **not** a statement of current schedule. Live status is tracked in Jira (project `SALIN`); see [docs/backlog/technical-work.md](../backlog/technical-work.md) for the backlog this documentation set links to.

| Sprint | End Date | Critical Deliverable | Risk if Missed |
|--------|----------|---------------------|----------------|
| Sprint 1 | 2026-03-27 | Core loop skeleton; first Android build | All subsequent sprints slip |
| Sprint 2 | 2026-04-10 | Full recognition + feedback; playable game | UAT preparation blocked |
| Sprint 3 | 2026-04-24 | Levels 1–10; Chapter 1 boss; era-themed enemy types; dialogue system (Type A panels) | Scope reduction required |
| Sprint 4 | 2026-05-08 | Levels 11–15; Endless Mode; Lite/Full split | Store submission at risk |
| Sprint 5 | 2026-05-22 | UAT with 50–100 participants; art final | Academic evaluation affected |
| Sprint 6 | 2026-05-29 | Store submission; final docs | Project incomplete |

[EVIDENCE: docs/capstone/GDD.md, §6.1 Milestones]
