# 03 — Core Systems
**Project:** Salinlahi
**Version:** 2.0
**Date:** 2026-06-03
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
| `SuppressDrawingInput(bool suppressed)` | Any state | Suppresses (`true`) or restores (`false`) drawing input; `AcceptsDrawingInput` returns `false` while suppressed, even if `Playing`. Auto-released when state transitions to `GameOver` or `LevelComplete`. |
| `HandleGameOver()` (private) | `OnGameOver` fired | state → `GameOver`; resets `_drawingSuppressed = false`; clears paused-run snapshot; DefeatScreenUI overlay handles UI |
| `HandleLevelComplete()` (private) | `OnLevelComplete` fired | state → `LevelComplete`; resets `_drawingSuppressed = false`; clears paused-run snapshot |

**Properties:**
- `CurrentLevel` (`LevelConfigSO`) — the active level config; set via `SetLevel`.
- `LastDefeatHearts` (`int`) — hearts remaining at last defeat (consumed by DefeatScreenUI).
- `CurrentBoss` (`BossController`) — non-null during a boss encounter; set via `SetCurrentBoss`.
- `AcceptsDrawingInput` (`bool`) — true when `CurrentState` is `Playing` or `Practicing` **and** drawing is not suppressed (`!_drawingSuppressed`). `StrokeCapture` gates all input on this property.

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
private const string SCENE_ALMANAC      = "Almanac";
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
| `LoadAlmanac()` | `Almanac` |
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
| `PlaySFX(AudioClip clip, float volumeScale = 1f)` | Plays clip one-shot on `_sfxSource`; null-safe. `volumeScale` is clamped to `[0,1]` and forwarded to `AudioSource.PlayOneShot`, stacking with the user-facing master & SFX sliders. |
| `PlayBGM(AudioClip clip)` | Assigns clip to `_bgmSource`, loops, plays; guards against re-playing the same clip. Resets `_bgmScale` to `1f` so non-boss BGM plays at full authored level. |
| `StopBGM()` | Stops `_bgmSource` and resets `_bgmScale` to `1f`. |
| `FadeInBGM(AudioClip clip, float seconds, float volumeScale = 1f) → Coroutine` | Fades from the current BGM (if any) to `clip` over `seconds`. Cancels any in-flight fade. `seconds ≤ 0` snaps without fading. No-ops if `clip` is null. Uses `Time.unscaledDeltaTime` so fades work during pause (`timeScale == 0`). `volumeScale` is stored as `_bgmScale` and applied as `master * bgm * _bgmScale` until the next `PlayBGM`/`StopBGM`/`FadeOutBGM`. |
| `FadeOutBGM(float seconds) → Coroutine` | Fades the current BGM out then stops the source. Cancels any in-flight fade. `seconds ≤ 0` is equivalent to `StopBGM()`. Resets `_bgmScale` to `1f` when the fade completes. |

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
| Boss | `OnBossVulnerabilityWindowActive` | `RaiseBossVulnerabilityWindowActive(int)` | `int` phaseIndex |
| Boss | `OnBossVulnerabilityExpired` | `RaiseBossVulnerabilityExpired(int)` | `int` phaseIndex |
| Boss | `OnBossDamaged` | `RaiseBossDamaged(int, int)` | `int phaseIndex, int hpRemaining` |
| Boss | `OnBossDefeated` | `RaiseBossDefeated()` | none |
| Boss Audio | `OnBossSummonTick` | `RaiseBossSummonTick()` | none — raised by `BossSummonTicker` at each summon tick |
| Boss Audio | `OnBossDrawHit` | `RaiseBossDrawHit()` | none — raised by `BossController` on `BossRouteResult.Hit` |
| Boss Audio | `OnBossTeleport` | `RaiseBossTeleport()` | none — raised by `PhaseBasedMovement.TeleportNow` on each snap |
| Dialogue | `OnDialogueStarted` | `RaiseDialogueStarted()` | none |
| Dialogue | `OnDialogueComplete` | `RaiseDialogueComplete()` | none |
| Almanac | `OnCharacterUnlocked` | `RaiseCharacterUnlocked(BaybayinCharacterSO)` | `BaybayinCharacterSO` — raised after `CharacterUnlockProgress.TryMarkUnlocked` returns `true`; `AlmanacController` subscribes to rebuild the characters grid |

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

---

## 7. Almanac Systems

### 7.1 `CharacterUnlockProgress` (static data helper)

**Location:** `Assets/Scripts/Core/CharacterUnlockProgress.cs`
**Type:** `static` class — no MonoBehaviour, no Singleton, no EventBus events raised internally.

Persists the set of unlocked `BaybayinCharacterSO` IDs in `PlayerPrefs` under the key `salinlahi.almanac.character_ids`. IDs are stored as a pipe-separated string and normalized with `Trim().ToLowerInvariant()` on every read and write.

| Method | Behavior |
|--------|----------|
| `HasUnlocked(BaybayinCharacterSO)` | Returns `true` if the character's normalized ID is in the persisted set. |
| `TryMarkUnlocked(BaybayinCharacterSO, out string)` | Adds the normalized ID if not present, saves via `PlayerPrefs.Save()`, returns `true` on new unlock. |
| `ClearAllUnlocked()` | Deletes the PlayerPrefs key and clears the in-memory set. |

**Integration with ProgressManager:** `ProgressManager.ClearAllProgress()` calls `CharacterUnlockProgress.ClearAllUnlocked()` so a full progress reset also wipes the Almanac unlock state.

[EVIDENCE: Assets/Scripts/Core/CharacterUnlockProgress.cs]
[EVIDENCE: Assets/Scripts/Core/ProgressManager.cs — ClearAllProgress()]

---

### 7.2 `AlmanacController` (scene MonoBehaviour)

**Location:** `Assets/Scripts/UI/Almanac/AlmanacController.cs`
**Scene:** `Assets/_Scenes/Almanac.unity`

Scene orchestrator for the Almanac screen. Reads `CharacterRegistrySO` and `AlmanacEnemyRegistrySO`, builds two grid views (Characters tab and Enemies tab) into `GridLayoutGroup`-backed `ScrollRect`s, and manages tab switching.

**Enemy reveal gate:** An enemy entry renders as a revealed cell only when it is **both** discovered (`AlmanacEnemyDiscovery.IsDiscovered`) **and** in the Spanish era (`IsSpanishEra`). Non-Spanish-era enemies are placeholders for chapters whose content has not shipped, so they fall through to the locked `?` rendering (non-interactable, no detail) — mirroring a locked Baybayin character. The "Discovered" counter applies the same gate, so a non-Spanish enemy counts toward the total but not toward the discovered numerator.

**Public API:**

| Member | Behavior |
|--------|----------|
| `ShowCharacters()` | Activates the Characters tab panel. |
| `ShowEnemies()` | Activates the Enemies tab panel. |
| `HandleCharacterUnlocked(BaybayinCharacterSO)` | EventBus subscriber for `OnCharacterUnlocked`; rebuilds the characters grid to reflect the new unlock. |
| `CountUnlockedCharacters(IReadOnlyList<BaybayinCharacterSO>)` (static) | Returns the count of entries for which `CharacterUnlockProgress.HasUnlocked` is `true`. |
| `CountDiscoveredEnemies(IReadOnlyList<AlmanacEnemyEntry>, Func<EnemyDataSO,bool>)` (static) | Returns the count of entries for which the supplied predicate is `true`. The controller passes `data => IsDiscovered(data) && IsSpanishEra(data)` so the counter matches the reveal gate. |
| `IsSpanishEra(EnemyDataSO)` (static) | Returns `true` only for a non-null enemy whose `era == Era.Spanish`. The "currently" gate that hides unfinished-chapter enemies behind a `?`. |
| `FormatCounter(string label, int revealed, int total)` (static) | Returns a `"label x/y"` formatted string for the HUD counter labels. |

[EVIDENCE: Assets/Scripts/UI/Almanac/AlmanacController.cs]

---

### 7.3 `AlmanacCell` (MonoBehaviour)

**Location:** `Assets/Scripts/UI/Almanac/AlmanacCell.cs`

One grid cell in the Almanac Characters or Enemies panel. Displays a portrait sprite (or a `?` silhouette when locked) and a boss border when applicable.

| Member | Behavior |
|--------|----------|
| `Setup(Sprite, bool isRevealed, bool isBoss, Action onSelect)` | Configures the cell sprite, lock overlay, boss border, and tap callback. |
| `ShouldShowBossBorder(bool isBoss, bool isRevealed)` (static) | Returns `true` when both flags are `true`. |
| `ShouldBeInteractable(bool isRevealed)` (static) | Returns `isRevealed`. Locked cells are non-interactable. |

[EVIDENCE: Assets/Scripts/UI/Almanac/AlmanacCell.cs]

---

### 7.4 `AlmanacDetailScroll` (MonoBehaviour)

**Location:** `Assets/Scripts/UI/Almanac/AlmanacDetailScroll.cs`

Overlay panel that expands into view when the player taps a revealed Almanac cell. Displays a portrait, name, and description. Animates via `CanvasGroup` (alpha fade) and `RectTransform` scale.

| Method | Behavior |
|--------|----------|
| `Show(Sprite portrait, string title, string description)` | Populates fields and runs the expand animation. |
| `Hide()` | Collapses and hides the overlay. |

[EVIDENCE: Assets/Scripts/UI/Almanac/AlmanacDetailScroll.cs]

---

### 7.5 `AlmanacEnemyDiscovery` (static seam)

**Location:** `Assets/Scripts/UI/Almanac/AlmanacEnemyDiscovery.cs`
**Type:** `static` class.

Single contact point between the Almanac UI and enemy discovery state. Currently a **temporary stub**: `IsDiscovered(EnemyDataSO data)` returns `data != null` (all non-null enemies appear discovered). This is intentionally isolated so a teammate's `EnemyDiscoveryProgress` feature can replace the implementation without touching any other Almanac code.

**Integration note:** When `EnemyDiscoveryProgress` is merged, replace the body of `IsDiscovered` with `EnemyDiscoveryProgress.HasDiscovered(data)`.

[EVIDENCE: Assets/Scripts/UI/Almanac/AlmanacEnemyDiscovery.cs]