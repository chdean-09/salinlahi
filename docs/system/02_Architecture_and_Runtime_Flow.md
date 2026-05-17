# 02 — Architecture and Runtime Flow
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Jon Wayne Cabusbusan

---

## 1. Scene Inventory

| Scene Name | File | Role |
|------------|------|------|
| Bootstrap | `Assets/_Scenes/Bootstrap.unity` | Instantiates all manager singletons; auto-transitions to MainMenu |
| MainMenu | `Assets/_Scenes/MainMenu.unity` | Entry point for user: Play, Endless, Tracing Dojo, Settings |
| LevelSelect | `Assets/_Scenes/LevelSelect.unity` | 15 level buttons grouped by chapter; unlock progression; 3 shrines per era |
| Gameplay | `Assets/_Scenes/Gameplay.unity` | Core defense loop: enemies, drawing canvas, HUD |
| GameOver | `Assets/_Scenes/GameOver.unity` | Post-defeat stats; Retry and Return-to-Menu actions |

[EVIDENCE: Assets/_Scenes/ directory listing]
[EVIDENCE: docs/capstone/GDD.md, §5.1 Player Journey]
[EVIDENCE: docs/capstone/TDD.md, §1.1 — five scenes specified]

---

## 2. Scene Lifecycle Flow

```
Cold Start
│
├─ Bootstrap.unity loads
│     └─ Manager prefabs Awake() in order:
│           ├─ Singleton<GameManager>.Awake()   → DontDestroyOnLoad
│           ├─ Singleton<SceneLoader>.Awake()   → DontDestroyOnLoad
│           ├─ Singleton<AudioManager>.Awake()  → DontDestroyOnLoad
│           └─ Singleton<EnemyPool>.Awake()     → DontDestroyOnLoad + pool init
│     └─ BootstrapLoader.Start() [waits 1 frame]
│           └─ SceneLoader.LoadMainMenu()
│
├─ MainMenu.unity loads
│     └─ MainMenuUI.cs wires Play button → SceneLoader.LoadGameplay()
│
├─ Gameplay.unity loads
│     └─ GameManager.StartGame() called → GameState.Playing
│     └─ LevelFlowController plays optional Type A intro dialogue
│     └─ WaveManager drives waves (all levels, including boss levels)
│     └─ [All waves cleared]
│           └─ LevelFlowController checks LevelConfigSO.isBossLevel
│                 ├─ false → EventBus.RaiseLevelComplete()
│                 └─ true → BossController activates boss encounter
│                       └─ [All boss phases cleared] → EventBus.RaiseBossDefeated()
│                             └─ EventBus.RaiseLevelComplete()
│     └─ EnemyPool.Get(data) → enemy active in scene
│     └─ [Player draws] → RecognitionManager → EventBus.RaiseCharacterRecognized()
│     └─ Enemy.Defeat() → EventBus.RaiseEnemyDefeated()
│     └─ [Enemy reaches base] → EventBus.RaiseBaseHit() → HeartSystem
│     └─ [Hearts == 0] → EventBus.RaiseGameOver()
│           └─ GameManager.HandleGameOver() → GameState.GameOver
│                 └─ SceneLoader.LoadGameOver()
│
└─ GameOver.unity loads
      └─ GameOverUI.cs wires Retry → SceneLoader.LoadGameplay()
      └─ GameOverUI.cs wires Menu  → SceneLoader.LoadMainMenu()
```

[EVIDENCE: Assets/Scripts/Core/BootstrapLoader.cs, Start()]
[EVIDENCE: Assets/Scripts/Core/SceneLoader.cs, LoadRoutine()]
[EVIDENCE: Assets/Scripts/Core/GameManager.cs, HandleGameOver()]
[EVIDENCE: docs/capstone/GDD.md, §5.1 Player Journey]

---

## 3. Manager Lifecycle

All managers follow the `Singleton<T>` base class lifecycle:

### 3.1 Singleton<T> Contract

```csharp
// Assets/Scripts/Utilities/Singleton.cs
protected virtual void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this as T;
    DontDestroyOnLoad(gameObject);
}
```

**Rules:**
- Only one instance of any Singleton type may exist at runtime. Duplicates are immediately destroyed.
- All Singleton instances survive scene loads via `DontDestroyOnLoad`.
- Managers must be instantiated in Bootstrap before any gameplay scene loads.

[EVIDENCE: Assets/Scripts/Utilities/Singleton.cs]

### 3.2 Manager Prefabs

| Prefab | Script | Instantiated In |
|--------|--------|----------------|
| `[Manager] GameManager.prefab` | `GameManager.cs` | Bootstrap scene |
| `[Manager] SceneLoader.prefab` | `SceneLoader.cs` | Bootstrap scene |
| `[Manager] AudioManager.prefab` | `AudioManager.cs` | Bootstrap scene |
| `[Manager] EnemyPool.prefab` | `EnemyPool.cs` | Bootstrap scene |
| `[Manager] RecognitionManager.prefab` | `RecognitionManager.cs` | Bootstrap scene |
| `[Manager] ComboManager.prefab` | `ComboManager.cs` | Bootstrap scene |
| `[Manager] ActiveEnemyTracker.prefab` | `ActiveEnemyTracker.cs` | Bootstrap scene |
| `[Manager] CombatResolver.prefab` | `CombatResolver.cs` | Bootstrap scene |

[EVIDENCE: Assets/Prefabs/Managers/ directory]

---

## 4. Event-Driven Interactions

All cross-system communication uses `EventBus.cs`. No direct manager-to-manager method calls occur except via `Instance` for single-frame operations (e.g., `SceneLoader.Instance.LoadGameOver()`).

### 4.1 EventBus Contract Table

| Event | Payload | Raise Method |
|-------|---------|-------------|
| `OnEnemySpawned` | `Enemy` | `RaiseEnemySpawned(Enemy)` |
| `OnEnemyDefeated` | `BaybayinCharacterSO` | `RaiseEnemyDefeated(BaybayinCharacterSO)` |
| `OnBaseHit` | `int` (damage) | `RaiseBaseHit(int)` |
| `OnGameOver` | none | `RaiseGameOver()` |
| `OnLevelComplete` | none | `RaiseLevelComplete()` |
| `OnWaveStarted` | `int` (wave index) | `RaiseWaveStarted(int)` |
| `OnWaveCleared` | `int` (wave index) | `RaiseWaveCleared(int)` |
| `OnCharacterRecognized` | `string` (characterID) | `RaiseCharacterRecognized(string)` |
| `OnRecognitionResolved` | `RecognitionResult, bool, float` | `RaiseRecognitionResolved(RecognitionResult, bool, float)` |
| `OnDrawingFailed` | none | `RaiseDrawingFailed()` |
| `OnDrawingStarted` | none | `RaiseDrawingStarted()` |
| `OnHeartsChanged` | `int` (current hearts) | `RaiseHeartsChanged(int)` |
| `OnEnemyTargeted` | `Enemy` | `RaiseEnemyTargeted(Enemy)` |
| `OnDrawingMissed` | none | `RaiseDrawingMissed()` |
| `OnAOETriggered` | `int` (defeated count) | `RaiseAOETriggered(int)` |
| `OnComboChanged` | `int` (current streak) | `RaiseComboChanged(int)` |
| `OnFocusModeActivated` | none | `RaiseFocusModeActivated()` |
| `OnFocusModeDeactivated` | none | `RaiseFocusModeDeactivated()` |
| `OnGamePaused` | none | `RaiseGamePaused()` |
| `OnGameResumed` | none | `RaiseGameResumed()` |
| `OnBossDefeated` | none | `RaiseBossDefeated()` |
| `OnDialogueStarted` | none | `RaiseDialogueStarted()` |
| `OnDialogueComplete` | none | `RaiseDialogueComplete()` |

[EVIDENCE: Assets/Scripts/Core/EventBus.cs]

### 4.2 Subscription Rules (Enforced by Code Comment)

> "Subscribe in OnEnable. Unsubscribe in OnDisable. No exceptions."

[EVIDENCE: Assets/Scripts/Core/EventBus.cs, line 1 comment]

---

## 5. Critical Sequence Flows

### 5.1 Enemy Defeat Flow

```
Player lifts finger
  → StrokeCapture captures point cloud
    → RecognitionManager runs $P algorithm
      → confidence ≥ 0.60?
          YES → EventBus.RaiseCharacterRecognized(characterID)
                  → WaveManager finds matching enemy
                    → Enemy.Defeat()
                        → EventBus.RaiseEnemyDefeated(character)
                            → AudioManager.PlayPronunciationClip(character)
                        → Enemy.ReturnToPool()
          NO  → EventBus.RaiseDrawingFailed()
                  → HUD shows red flash / X mark
```

[EVIDENCE: docs/capstone/TDD.md, §3.3 Combat Resolution]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs, Defeat()]
[EVIDENCE: Assets/Scripts/Core/AudioManager.cs, PlayPronunciationClip()]

### 5.2 Base Hit / Game Over Flow

```
EnemyMover.OnTriggerEnter2D(PlayerBase tag)
  → EventBus.RaiseBaseHit()
    → HeartSystem decrements hearts
      → EventBus.RaiseHeartsChanged(currentHearts)
        → HUD updates heart display
  → Enemy.ReturnToPool()
  → hearts == 0?
      YES → EventBus.RaiseGameOver()
              → GameManager.HandleGameOver()
                  → GameState = GameOver
                  → SceneLoader.LoadGameOver()
```

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs, OnTriggerEnter2D()]
[EVIDENCE: Assets/Scripts/Core/GameManager.cs, HandleGameOver()]
[EVIDENCE: docs/capstone/GDD.md, §2.3 Win/Lose Conditions]

### 5.3 Scene Load Flow

```
SceneLoader.LoadXxx()
  → LoadRoutine(sceneName) [Coroutine]
      → Time.timeScale = 1f  (always reset before scene change)
      → SceneManager.LoadSceneAsync(sceneName)
          → while (!op.isDone): DebugLogger.Log progress
```

[EVIDENCE: Assets/Scripts/Core/SceneLoader.cs, LoadRoutine()]

---

## 6. GameState Transition Diagram

```
                  ┌─────────┐
                  │  Idle   │ ← initial state on Bootstrap
                  └────┬────┘
                       │ StartGame()
                  ┌────▼────┐
             ┌───►│ Playing │◄───────────┐
             │    └────┬────┘            │
             │         │ PauseGame()     │ ResumeGame()
             │    ┌────▼────┐            │
             │    │ Paused  │────────────┘
             │    └─────────┘
             │
             │ HandleGameOver()          HandleLevelComplete()
             │    ┌──────────┐           ┌───────────────┐
             └────┤ GameOver │           │ LevelComplete │
                  └──────────┘           └───────────────┘
```

[EVIDENCE: Assets/Scripts/Core/GameManager.cs, enum GameState; SetState()]
