# 01 — System Overview
**Project:** Salinlahi
**Version:** 1.0
**Date:** 2026-03-19
**Owner:** Jon Wayne Cabusbusan

---

## 1. Product Purpose

Salinlahi is a 2D pixel art mobile defense game whose core mechanic is drawing Baybayin characters on a touchscreen to defeat enemies. The product's explicit goals are:

1. **Entertainment-first gameplay** — the game must be enjoyable as a game independent of its educational content.
2. **Intrinsic integration of learning** — drawing Baybayin characters is not a separate educational layer; it is the only attack input. Playing the game and practicing Baybayin are the same single activity.
3. **Durable learning outcome** — leveraging the Drawing Effect (Fernandes et al., 2018) and the Phonological Loop (Baddeley, 2000) so that players retain Baybayin character-to-syllable associations after play.

[EVIDENCE: docs/capstone/Salinlahi.md, §3.5.1 Objective 1]
[EVIDENCE: docs/capstone/GDD.md, §1 Overview]

---

## 2. Scope

| In Scope | Evidence |
|----------|----------|
| Portrait-mode vertical defense gameplay on Android and iOS | GDD §1.3 Platforms |
| $P Point-Cloud gesture recognition for 17 Baybayin consonant characters | Salinlahi.md §3.3.3; RecognitionConfigSO.cs |
| Story Mode: 15 levels across 3 chapters, boss encounters at levels 5, 10, 15 | GDD §2.4 |
| Endless Mode: Unlocked after completing Story Mode or defeating the final boss, random characters, high-score tracking | GDD §2.4 |
| Tracing Dojo: Pressure-free practice mode for all 17 characters, no enemies | GDD §2.4 |
| Enemy wave system driven by ScriptableObject data (LevelConfigSO, WaveConfigSO) | Salinlahi.md §3.5.1; LevelConfigSO.cs |
| Two-build split: Salinlahi Lite (free, levels 1–3) and Salinlahi Full (PHP 149, all content) | TDD §7.2; Salinlahi.md §3.4 |
| Fully offline operation — zero network calls at runtime | GDD §1.3; TDD header |
| Singleton manager architecture with DontDestroyOnLoad | GameManager.cs; Singleton.cs |
| Unity Object Pool for enemy recycling (no runtime Instantiate/Destroy in game loop) | EnemyPool.cs |
| Audio feedback: pronunciation clip plays on every successful character recognition | TDD §6; AudioManager.cs |

---

## 3. Non-Goals

| Non-Goal | Rationale |
|----------|-----------|
| Online multiplayer or leaderboard server | Fully offline by design |
| Machine learning / CNN-based recognition | Requires training data and model binary; not in scope |
| Android/iOS cloud save | Not specified in any source document |
| In-app purchases or advertising | Business model is premium + lite; no IAP or ads |
| Diacritical marks (kudlit) recognition in MVP | Recognition scope limited to 17 consonant base characters; kudlit modifier mechanic is a Should Ship feature that may be deferred post-launch (GDD §3.3 Chapter 2; Team README Feature Priority Matrix) |
| Roman-alphabet romanization input | Drawing is the only combat input; no alternative input path |
| 3D geometry or 3D renderer | URP 2D only; no 3D geometry anywhere in project |

[EVIDENCE: docs/capstone/Salinlahi.md, §1.5 Scope and Limitations]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.4 Business Model]

---

## 4. Runtime Context

| Property | Value | Evidence |
|----------|-------|----------|
| Engine | Unity 6 LTS | TDD header |
| Renderer | Universal Render Pipeline (URP) 2D | TDD §1 Architecture Overview |
| Language | C# | TDD header |
| Orientation | Portrait only | GDD §1.3 |
| Network requirement | None (fully offline) | GDD §1.3 |
| Minimum target hardware | Mid-range Android/iOS device | TDD §7.3 |
| Frame rate target | 60 fps consistent during wave gameplay | TDD §7.3 |
| APK/IPA size target | Under 100 MB | TDD §7.3 |
| Cold-start-to-gameplay | Under 5 seconds | TDD §7.3 |
| Recognition latency | Under 50 ms from finger lift to combat result | TDD §7.3; Salinlahi.md §3.3.3 |
| Input | Touchscreen freehand drawing only | GDD §2.2 |

---

## 5. High-Level Architecture Summary

Salinlahi uses a **Bootstrap-then-manager-singleton** architecture. On launch, the Bootstrap scene instantiates all persistent manager prefabs (GameManager, SceneLoader, AudioManager, EnemyPool) via `DontDestroyOnLoad`. After one frame, `BootstrapLoader` triggers a load to the MainMenu scene. Managers persist across all subsequent scene loads.

Cross-system communication is handled exclusively through a **static EventBus** (`EventBus.cs`). No manager holds a direct reference to another manager beyond what is exposed through the static `Instance` accessor. All subscribers must register in `OnEnable` and deregister in `OnDisable`.

Game content (levels, waves, enemies, characters, recognition configuration) is defined entirely in **ScriptableObject assets**, allowing level designers to adjust difficulty without modifying or recompiling C# code.

Enemy objects are managed through a **Unity `ObjectPool<Enemy>`** instance in `EnemyPool`. No `Instantiate` or `Destroy` call occurs during the gameplay loop. Enemies are retrieved from the pool (`EnemyPool.Get(data)`) and returned to it on defeat or base-hit.

The **$P Point-Cloud Recognizer** operates fully on-device against 17 pre-loaded template files stored in `Assets/Resources/Templates/`. Recognition produces a confidence score; scores at or above `0.60` are accepted.

```
App Launch
    └── Bootstrap Scene
          └── BootstrapLoader.Start()
                └── [1 frame] → SceneLoader.LoadMainMenu()
                      └── MainMenu Scene
                            └── [Play] → SceneLoader.LoadGameplay()
                                  └── Gameplay Scene
                                        ├── WaveManager (NOT FOUND in implementation)
                                        ├── Enemy ← EnemyPool
                                        ├── EnemyMover
                                        └── EventBus
                                              ├── OnGameOver → GameManager → SceneLoader.LoadGameOver()
                                              └── OnLevelComplete → GameManager
```

[EVIDENCE: Assets/Scripts/Core/BootstrapLoader.cs]
[EVIDENCE: Assets/Scripts/Core/GameManager.cs]
[EVIDENCE: Assets/Scripts/Core/EventBus.cs]
[EVIDENCE: docs/capstone/TDD.md, §1 Architecture Overview]
