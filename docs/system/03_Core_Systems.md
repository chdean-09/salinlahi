# 03 — Core Systems
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Jon Wayne Cabusbusan

---

## 1. Game State Management — `GameManager.cs`

### 1.1 Location
`Assets/Scripts/Core/GameManager.cs`

### 1.2 Responsibility
Owns the authoritative `GameState` enum and all state transition methods. Subscribes to `OnGameOver` and `OnLevelComplete` from the EventBus to react to gameplay outcomes.

### 1.3 State Enum

```csharp
public enum GameState { Idle, Playing, Paused, GameOver, LevelComplete }
```

### 1.4 Public API

| Method | Precondition | Postcondition |
|--------|-------------|---------------|
| `StartGame()` | Any state | `Time.timeScale = 1f`; state → `Playing` |
| `PauseGame()` | state == `Playing` | `Time.timeScale = 0f`; state → `Paused` |
| `ResumeGame()` | state == `Paused` | `Time.timeScale = 1f`; state → `Playing` |
| `HandleGameOver()` (private) | `OnGameOver` fired | state → `GameOver`; calls `SceneLoader.Instance.LoadGameOver()` |
| `HandleLevelComplete()` (private) | `OnLevelComplete` fired | state → `LevelComplete` |

### 1.5 Invariants
- `PauseGame()` is a no-op when `CurrentState != Playing`. Guard is enforced in code.
- `ResumeGame()` is a no-op when `CurrentState != Paused`. Guard is enforced in code.
- All state changes log to `DebugLogger`.

[EVIDENCE: Assets/Scripts/Core/GameManager.cs]

---

## 2. Scene Loading — `SceneLoader.cs`

### 2.1 Location
`Assets/Scripts/Core/SceneLoader.cs`

### 2.2 Responsibility
Wraps `SceneManager.LoadSceneAsync` in coroutines. Single source of truth for all scene name constants. Resets `Time.timeScale` to `1f` before every scene load to prevent a paused game from locking the next scene.

### 2.3 Scene Name Constants (internal)

```csharp
private const string SCENE_BOOTSTRAP    = "Bootstrap";
private const string SCENE_MAIN_MENU    = "MainMenu";
private const string SCENE_LEVEL_SELECT = "LevelSelect";   // PLANNED — GDD §5.1, TDD §1.1
private const string SCENE_GAMEPLAY     = "Gameplay";
private const string SCENE_GAME_OVER    = "GameOver";
```

### 2.4 Public API

| Method | Loads Scene |
|--------|-------------|
| `LoadMainMenu()` | `MainMenu` |
| `LoadLevelSelect()` | `LevelSelect` (PLANNED) |
| `LoadGameplay()` | `Gameplay` |
| `LoadGameOver()` | `GameOver` |
| `ReloadCurrentScene()` | Active scene (name retrieved at call time) |

### 2.5 Invariants
- Scene name strings are never duplicated outside this file. Any scene rename requires only this one edit.
- `Time.timeScale` is always reset to `1f` at the start of `LoadRoutine`.
- Loading progress is reported every frame via `DebugLogger`.

[EVIDENCE: Assets/Scripts/Core/SceneLoader.cs]

---

## 3. Audio Management — `AudioManager.cs`

### 3.1 Location
`Assets/Scripts/Core/AudioManager.cs`

### 3.2 Responsibility
Owns two `AudioSource` components: `_bgmSource` (background music, looped) and `_sfxSource` (one-shot SFX). Subscribes to EventBus to play audio reactively.

### 3.3 EventBus Subscriptions

| Event | Handler | Behavior |
|-------|---------|----------|
| `OnEnemyDefeated(BaybayinCharacterSO)` | `PlayPronunciationClip` | Plays `character.pronunciationClip` via `_sfxSource.PlayOneShot` |
| `OnBaseHit` | `PlayBaseHitSound` | **STUB** — Sprint 2 will assign a clip via Inspector |

### 3.4 Public API

| Method | Behavior |
|--------|----------|
| `PlaySFX(AudioClip clip)` | Plays clip one-shot on `_sfxSource`; null-safe |
| `PlayBGM(AudioClip clip)` | Assigns clip to `_bgmSource`, loops, plays; guards against re-playing the same clip |
| `StopBGM()` | Stops `_bgmSource` |

### 3.5 Sprint 2 TODOs (marked in code)
- `PlayBaseHitSound` is a stub. Requires assignment of a base-hit SFX `AudioClip` via Inspector.

[EVIDENCE: Assets/Scripts/Core/AudioManager.cs]
[EVIDENCE: docs/capstone/TDD.md, §6 Audio Feedback System]

---

## 4. EventBus — `EventBus.cs`

### 4.1 Location
`Assets/Scripts/Core/EventBus.cs`

### 4.2 Design
`EventBus` is a `static` class. It holds no state and requires no instantiation. All fields are C# `event Action` delegates with explicit `Raise*` methods as the only legal publish path.

### 4.3 Full Contract

| Category | Event | Raise Method | Payload Type |
|----------|-------|-------------|-------------|
| Enemy | `OnEnemyDefeated` | `RaiseEnemyDefeated(BaybayinCharacterSO)` | `BaybayinCharacterSO` |
| Enemy | `OnBaseHit` | `RaiseBaseHit()` | none |
| Enemy | `OnAOETriggered` | `RaiseAOETriggered(List<BaybayinCharacterSO>)` | `List<BaybayinCharacterSO>` |
| Game State | `OnGameOver` | `RaiseGameOver()` | none |
| Game State | `OnLevelComplete` | `RaiseLevelComplete()` | none |
| Game State | `OnWaveStarted` | `RaiseWaveStarted(int)` | `int` waveIndex |
| Recognition | `OnCharacterRecognized` | `RaiseCharacterRecognized(string)` | `string` characterID |
| Recognition | `OnDrawingFailed` | `RaiseDrawingFailed()` | none |
| Recognition | `OnDrawingStarted` | `RaiseDrawingStarted()` | none |
| UI | `OnHeartsChanged` | `RaiseHeartsChanged(int)` | `int` currentHearts |
| Combo | `OnComboChanged` | `RaiseComboChanged(int)` | `int` currentStreak |
| Combo | `OnComboRewardTriggered` | `RaiseComboRewardTriggered()` | none |
| Boss | `OnBossSpawned` | `RaiseBossSpawned(BossConfigSO)` | `BossConfigSO` |
| Boss | `OnBossPhaseCleared` | `RaiseBossPhaseCleared()` | none |
| Boss | `OnBossDefeated` | `RaiseBossDefeated()` | none |

### 4.4 Usage Rules
1. **Subscribe only in `OnEnable`.** Never subscribe in `Start` or `Awake`.
2. **Unsubscribe only in `OnDisable`.** Memory leaks will occur if subscriptions are not cleaned up.
3. **Never invoke an event directly.** Always use the `Raise*` method. This allows null checks to be centralized.
4. **Never add new events without updating this document and the Traceability Matrix.**

[EVIDENCE: Assets/Scripts/Core/EventBus.cs]

---

## 5. Singleton Base Class — `Singleton<T>`

### 5.1 Location
`Assets/Scripts/Utilities/Singleton.cs`

### 5.2 Policy

| Rule | Enforcement |
|------|-------------|
| Only one instance per type at runtime | Duplicate destroyed in `Awake` |
| Survives all scene transitions | `DontDestroyOnLoad(gameObject)` |
| Must be placed in Bootstrap scene | Convention; enforced by prefab placement |
| `Instance` is read-only externally | `private set` on `Instance` property |
| Subclasses must call `base.Awake()` | Required; see GameManager, AudioManager, EnemyPool, SceneLoader |

### 5.3 Constraint: Do Not Add New Singletons Lightly
Adding a new Singleton requires:
1. Creating a Manager prefab under `Assets/Prefabs/Managers/`
2. Placing the prefab in the Bootstrap scene
3. Updating `02_Architecture_and_Runtime_Flow.md` Manager Prefabs table
4. Updating the Traceability Matrix

[EVIDENCE: Assets/Scripts/Utilities/Singleton.cs]

---

## 6. Debug Logger — `DebugLogger.cs`

### 6.1 Location
`Assets/Scripts/Utilities/DebugLogger.cs`

### 6.2 Policy
`DebugLogger` wraps `Debug.Log` with a conditional compile symbol so all log calls are stripped from release builds automatically. **Never call `Debug.Log` directly in production code.** Use `DebugLogger.Log(message)` only.

[EVIDENCE: Assets/Scripts/Utilities/DebugLogger.cs]
[EVIDENCE: Assets/Scripts/Core/GameManager.cs, SetState() — uses DebugLogger.Log]