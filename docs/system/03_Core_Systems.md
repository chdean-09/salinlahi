# 03 — Core Systems
**Project:** Salinlahi
**Version:** 1.4
**Date:** 2026-05-23
**Owner:** Jon Wayne Cabusbusan

---

## 1. Game State Management — `GameManager.cs`

### 1.1 Location
`Assets/Scripts/Core/GameManager.cs`

### 1.2 Responsibility
Owns the authoritative `GameState` enum and all state transition methods. Subscribes to `OnGameOver` and `OnLevelComplete` from the EventBus to react to gameplay outcomes.

### 1.3 State Enum

```csharp
public enum GameState { Idle, Playing, Paused, GameOver, LevelComplete, Practicing }
```

### 1.4 Public API

| Method | Precondition | Postcondition |
|--------|-------------|---------------|
| `StartGame()` | Any state | `Time.timeScale = 1f`; state → `Playing` |
| `PauseGame()` | state == `Playing` | `Time.timeScale = 0f`; state → `Paused` |
| `ResumeGame()` | state == `Paused` | `Time.timeScale = 1f`; state → `Playing` |
| `EnterPractice()` | Any state | `Time.timeScale = 1f`; state → `Practicing` |
| `ExitPractice()` | state == `Practicing` | state → `Idle` (no-op otherwise) |
| `EnterDialoguePause()` | state == `Playing` | Caches previous state; `Time.timeScale = 0f`; state → `Paused` |
| `ExitDialoguePause()` | state == `Paused` | `Time.timeScale = 1f`; state → previous (cached) state |
| `SetLevel(LevelConfigSO)` | Any state | Sets `CurrentLevel` property |
| `SetCurrentBoss(BossController)` | Internal | Setter used by `BossController.StartBoss` to publish `CurrentBoss` |
| `HandleGameOver()` (private) | `OnGameOver` fired | state → `GameOver`; clears paused-run snapshot; DefeatScreenUI overlay handles UI |
| `HandleLevelComplete()` (private) | `OnLevelComplete` fired | state → `LevelComplete`; clears paused-run snapshot |

**Properties:**
- `CurrentLevel` (`LevelConfigSO`) — the active level config; set via `SetLevel`.
- `LastDefeatHearts` (`int`) — hearts remaining at last defeat (consumed by DefeatScreenUI).
- `CurrentBoss` (`BossController`) — non-null during a boss encounter; set via `SetCurrentBoss`.
- `AcceptsDrawingInput` (`bool`) — true when `CurrentState` is `Playing` or `Practicing`.

### 1.5 Invariants
- `PauseGame()` is a no-op when `CurrentState != Playing`. Guard is enforced in code.
- `ResumeGame()` is a no-op when `CurrentState != Paused`. Guard is enforced in code.
- All state changes log to `DebugLogger`.

### 1.6 Paused Run Snapshot
`GameManager` exposes a paused-run snapshot API used by the Pause/Retry flow to restore a run mid-level instead of resetting. The snapshot caches the level id, hearts remaining, current wave index, spawned count, wave progress, and an array of `PausedEnemySnapshot` records — one per live enemy, each capturing `EnemyData`, `Character`, `Position`, and `CurrentHealth`. Snapshot accessors (`CachePausedRunSnapshot`, `TryConsumePausedRunHearts`, `TryGetPausedRunEnemies`, `TryGetPausedRunWaveIndex`, `TryGetPausedRunWaveProgress`, `TryGetPausedRunLevelId`, `ClearPausedRunSnapshotForLevel`, `DiscardPausedRunSnapshot`) gate restoration to the matching level id. The snapshot is cleared on `HandleGameOver` and `HandleLevelComplete`.

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
private const string SCENE_GAMEPLAY     = "Gameplay";
private const string SCENE_LEVEL_SELECT = "LevelSelect";
private const string SCENE_GAME_OVER    = "GameOver";
```

### 2.4 Public API

| Method | Loads Scene |
|--------|-------------|
| `LoadScene(string)` | Unified entry; in-progress guard prevents overlapping loads |
| `LoadMainMenu()` | `MainMenu` |
| `LoadGameplay()` | `Gameplay` |
| `LoadSandboxGameplay()` | `Gameplay` (editor/sandbox builds only) |
| `LoadLevelSelect()` | `LevelSelect` |
| `LoadGameOver()` | `[System.Obsolete]` — DefeatScreenUI overlay replaced this scene (SALIN-58); logs a warning |
| `ReloadCurrentScene()` | Active scene (name retrieved at call time) |

### 2.5 Invariants
- Scene name strings are never duplicated outside this file. Any scene rename requires only this one edit.
- `Time.timeScale` is always reset to `1f` at the start of `LoadRoutine`.
- Loading progress is reported every frame via `DebugLogger`.

### 2.6 Fade Stub
`SceneLoader` builds a Screen-Space-Overlay black-fade `Canvas` at runtime via `CreateFadeCanvas()` and fades to/from black around `SceneManager.LoadSceneAsync` using `Time.unscaledDeltaTime`. Sort order is taken from `RenderOrder.LoadingCanvas`. The implementation is an interim stub for the planned SALIN-44 `TransitionManager`. `LoadRoutine` wraps the load in `try/finally` so `_isLoading` and the fade alpha always reset, even if the coroutine is interrupted.

### 2.7 CleanupGameplayRun
Every scene-load entry first calls `EnemyPool.Instance?.ReturnAllCheckedOut()` to return live enemies to the pool before the next scene loads. In Editor/sandbox builds, `SandboxMode.Deactivate()` runs ahead of cleanup so sandbox state does not leak across scene loads.

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
| Enemy | `OnBaseHit` | `RaiseBaseHit(int)` | `int` (damage amount) |
| Enemy | `OnAOETriggered` | `RaiseAOETriggered(int)` | `int` (defeated count) |
| Game State | `OnGameOver` | `RaiseGameOver()` | none |
| Game State | `OnLevelComplete` | `RaiseLevelComplete()` | none |
| Game State | `OnWaveStarted` | `RaiseWaveStarted(int)` | `int` waveIndex |
| Game State | `OnWaveCleared` | `RaiseWaveCleared(int)` | `int` waveIndex |
| Recognition | `OnCharacterRecognized` | `RaiseCharacterRecognized(string)` | `string` characterID |
| Recognition | `OnRecognitionResolved` | `RaiseRecognitionResolved(RecognitionResult, bool, float)` | `RecognitionResult, bool, float` |
| Recognition | `OnDrawingFailed` | `RaiseDrawingFailed()` | none |
| Recognition | `OnDrawingStarted` | `RaiseDrawingStarted()` | none |
| UI | `OnHeartsChanged` | `RaiseHeartsChanged(int)` | `int` currentHearts |
| Combat | `OnEnemyTargeted` | `RaiseEnemyTargeted(Enemy)` | `Enemy` |
| Combat | `OnDrawingMissed` | `RaiseDrawingMissed()` | none |
| Combo | `OnComboChanged` | `RaiseComboChanged(int)` | `int` currentStreak |
| Focus | `OnFocusModeActivated` | `RaiseFocusModeActivated()` | none |
| Focus | `OnFocusModeDeactivated` | `RaiseFocusModeDeactivated()` | none |
| Pause | `OnGamePaused` | `RaiseGamePaused()` | none |
| Pause | `OnGameResumed` | `RaiseGameResumed()` | none |
| Boss | `OnBossStarted` | `RaiseBossStarted(BossConfigSO)` | `BossConfigSO` |
| Boss | `OnBossPhaseStarted` | `RaiseBossPhaseStarted(int)` | `int` phaseIndex |
| Boss | `OnBossExhausted` | `RaiseBossExhausted(int)` | `int` phaseIndex |
| Boss | `OnBossVulnerable` | `RaiseBossVulnerable(int)` | `int` phaseIndex |
| Boss | `OnBossVulnerabilityExpired` | `RaiseBossVulnerabilityExpired(int)` | `int` phaseIndex |
| Boss | `OnBossDamaged` | `RaiseBossDamaged(int, int)` | `int phaseIndex, int hpRemaining` |
| Boss | `OnBossDefeated` | `RaiseBossDefeated()` | none |
| Dialogue | `OnDialogueStarted` | `RaiseDialogueStarted()` | none |
| Dialogue | `OnDialogueComplete` | `RaiseDialogueComplete()` | none |

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