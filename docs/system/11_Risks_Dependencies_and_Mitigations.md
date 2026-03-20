# 11 — Risks, Dependencies, and Mitigations
**Project:** Salinlahi
**Version:** 1.0
**Date:** 2026-03-19
**Owner:** Jon Wayne Cabusbusan (Scrum Master)

---

## 1. Technical Risks

| Risk ID | Risk Description | Probability | Impact | Owner | Mitigation | Trigger Condition |
|---------|-----------------|-------------|--------|-------|-----------|------------------|
| RISK-01 | $P recognizer accuracy insufficient at 0.60 confidence for Baybayin characters — too many false positives or negatives | High | Critical | Jon Wayne | Implement recognizer in Sprint 2 immediately; run accuracy test with 10 testers × all 17 characters; tune `minimumConfidence` if needed; record all threshold changes in CLAUDE.md | Sprint 2 internal test shows < 70% acceptance on correct draws OR > 20% acceptance on clearly wrong draws |
| RISK-02 | Recognition latency exceeds 50ms budget on target Android hardware | Medium | High | Jon Wayne | Profile `DollarPRecognizer` on lowest-spec target device in Sprint 2; if over budget, reduce `resamplePointCount` incrementally (32→24→16) and re-test accuracy | Stopwatch measurement exceeds 50ms at p95 |
| RISK-03 | Object pool max size (20) exceeded during boss waves, causing runtime `Destroy` calls and GC spikes | Low | Medium | Jon Wayne | Profile enemy count during boss wave design; increase `_maxSize` in `EnemyPool` Inspector before Sprint 3 boss integration | Unity Profiler shows `Destroy` calls during active gameplay |
| RISK-04 | `WaveManager` implementation deferred beyond Sprint 2 blocks all gameplay testing | High | Critical | Jon Wayne | WaveManager is designated Sprint 2 P0 deliverable; daily standup blocker escalation if not started by Day 3 of Sprint 2 | Sprint 2 Day 3 with no WaveManager skeleton |
| RISK-05 | `HeartSystem` not implemented causes GameOver to never trigger | High | Critical | Jon Wayne | HeartSystem is Sprint 2 P0 deliverable alongside WaveManager | End of Sprint 1 regression checklist fails WV-06 |
| RISK-06 | Template .txt files for all 17 characters not authored before Sprint 2 integration | High | High | Chad | Template authoring is Chad's Sprint 1 offline task; must produce all 17 files before DollarPRecognizer is integrated | Sprint 2 DollarPRecognizer integration blocked waiting for templates |
| RISK-07 | `BossConfigSO` and boss phase system require significant design time; Sprint 3 scope may slip | Medium | Medium | Chad | Stub boss encounter as "extended wave" in Sprint 3 if full phase system is not ready; delay boss phases to Sprint 4 | Sprint 3 end without working boss encounter at Level 5 |
| RISK-08 | iOS build submission requires paid Apple Developer account; not confirmed provisioned | Unknown | High | Ian Clyde | Confirm Apple Developer account status before Sprint 4; if unavailable, submit Android only for Sprint 4 testing | Sprint 4 build target requires iOS IPA |
| RISK-09 | Duplicate event subscriptions cause double-firing (e.g., scene reload without OnDisable cleanup) | Medium | Medium | All | Enforce OnEnable/OnDisable pattern strictly; add CS-03 regression test to every sprint checklist | Console shows duplicate event handler errors |
| RISK-10 | `ScriptableObjects/Characters/` folder empty — no BaybayanCharacterSO assets authored | High | Critical | Chad | Character SO authoring is Sprint 1 content deliverable; Chad must author all 17 SOs by end of Sprint 1 | Sprint 2 enemy spawning fails due to null assignedCharacter |
| RISK-11 | GC spikes on low-RAM Android (1–2 GB) from non-obvious allocations in hot paths | Low | Medium | Jon Wayne | Run Memory Profiler in Sprint 4; audit all `Update()` paths for hidden allocations (string interpolation, LINQ, boxing) | Frame time chart shows irregular spikes > 3ms |
| RISK-12 | `Time.timeScale = 0` persists into next scene if SceneLoader is bypassed (e.g., direct `SceneManager.LoadScene` call) | Low | High | Jon Wayne | Never call `SceneManager.LoadScene` directly; all scene loads must go through `SceneLoader` — enforced by code review | MainMenu scene loads but game appears frozen |

---

## 2. Production Dependencies

| Dep ID | Dependency | Type | Owner | Required By | Status |
|--------|-----------|------|-------|------------|--------|
| DEP-01 | All 17 BaybayanCharacterSO assets authored with valid `characterID`, `syllable`, `displaySprite` | Content | Chad | Sprint 2 integration | NOT CONFIRMED |
| DEP-02 | All 17 recognition template `.txt` files in `Assets/Resources/Templates/` | Content | Chad | Sprint 2 DollarPRecognizer | NOT CONFIRMED |
| DEP-03 | All 17 pronunciation `AudioClip` assets assigned in `BaybayanCharacterSO` | Content/Audio | Ian Clyde | Sprint 2 audio integration | NOT CONFIRMED |
| DEP-04 | `[Enemy] Standard.prefab` has `EnemyDataSO` assigned in Inspector | Configuration | Chad | Sprint 1 wave testing | LIKELY DONE — prefab exists |
| DEP-05 | PlayerBase `GameObject` exists in Gameplay scene with tag `"PlayerBase"` | Scene setup | Jon Wayne | Sprint 2 `EnemyMover` base-hit test | NOT CONFIRMED |
| DEP-06 | Android keystore signed and configured | Build | Ian Clyde | Sprint 1 Android build (confirmed via git commit) | ✅ DONE — commit `ddc6ea3` |
| DEP-07 | Apple Developer Program account provisioned | Build | Ian Clyde | Sprint 4 iOS submission | UNKNOWN |
| DEP-08 | Levels 1–3 LevelConfigSO and WaveConfigSO assets fully authored | Content | Chad | Sprint 1–2 wave system | NOT CONFIRMED |
| DEP-09 | BGM audio clip for Gameplay scene | Audio | Ian Clyde | Sprint 2 audio integration | NOT CONFIRMED |
| DEP-10 | Base hit and game over SFX clips | Audio | Ian Clyde | Sprint 2 audio stubs replaced | NOT CONFIRMED |

---

## 3. Dependency Graph (Critical Path)

```
BaybayanCharacterSO assets (DEP-01)
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
| Runtime Instantiate/Destroy risk | EnemyPool with ObjectPool<Enemy> eliminates game-loop allocations | EnemyPool.cs; ObjectPool.cs comment |
| Scene timeScale lock risk | SceneLoader always resets Time.timeScale = 1f before load | SceneLoader.cs, LoadRoutine() |
| Duplicate Singleton risk | Singleton<T> destroys duplicates in Awake | Singleton.cs |
| EventBus memory leak risk | OnEnable/OnDisable pattern enforced + documented | EventBus.cs comment; GameManager.cs; AudioManager.cs |
| Release log overhead risk | DebugLogger with compile-symbol strip | DebugLogger.cs |

---

## 5. Sprint Timeline Risk Summary

| Sprint | End Date | Critical Deliverable | Risk if Missed |
|--------|----------|---------------------|----------------|
| Sprint 1 | 2026-03-27 | Core loop skeleton; first Android build | All subsequent sprints slip |
| Sprint 2 | 2026-04-10 | Full recognition + feedback; playable game | UAT preparation blocked |
| Sprint 3 | 2026-04-24 | Levels 1–10; Chapter 1 boss; new enemy types | Scope reduction required |
| Sprint 4 | 2026-05-08 | Levels 11–15; Endless Mode; Lite/Full split | Store submission at risk |
| Sprint 5 | 2026-05-22 | UAT with 50–100 participants; art final | Academic evaluation affected |
| Sprint 6 | 2026-05-29 | Store submission; final docs | Project incomplete |

[EVIDENCE: docs/capstone/GDD.md, §6.1 Milestones]
