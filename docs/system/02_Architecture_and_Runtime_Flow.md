# 02 — Architecture and Runtime Flow
**Project:** Salinlahi
**Version:** 1.0
**Date:** 2026-03-19
**Owner:** Jon Wayne Cabusbusan

---

## 1. Scene Inventory

| Scene Name | File | Role |
|------------|------|------|
| Bootstrap | `Assets/_Scenes/Bootstrap.unity` | Instantiates all manager singletons; auto-transitions to MainMenu |
| MainMenu | `Assets/_Scenes/MainMenu.unity` | Entry point for user: Play, Endless, Tracing Dojo, Settings |
| Gameplay | `Assets/_Scenes/Gameplay.unity` | Core defense loop: enemies, drawing canvas, HUD |
| GameOver | `Assets/_Scenes/GameOver.unity` | Post-defeat stats; Retry and Return-to-Menu actions |

[EVIDENCE: Assets/_Scenes/ directory listing]
[EVIDENCE: docs/capstone/GDD.md, §5.1 Player Journey]

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
│     └─ WaveManager (PLANNED) loads LevelConfigSO → drives WaveSpawner
│     └─ EnemyPool.Get(data) → enemy active in scene
│     └─ [Player draws] → RecognitionManager (PLANNED) → EventBus.RaiseCharacterRecognized()
│     └─ Enemy.Defeat() → EventBus.RaiseEnemyDefeated()
│     └─ [Enemy reaches base] → EventBus.RaiseBaseHit() → HeartSystem (PLANNED)
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

[EVIDENCE: Assets/Prefabs/Managers/ directory]

---

## 4. Event-Driven Interactions

All cross-system communication uses `EventBus.cs`. No direct manager-to-manager method calls occur except via `Instance` for single-frame operations (e.g., `SceneLoader.Instance.LoadGameOver()`).

### 4.1 EventBus Contract Table

| Event | Raised By | Subscriber(s) | Payload |
|-------|-----------|---------------|---------|
| `OnEnemyDefeated` | `Enemy.Defeat()` | `AudioManager` | `BaybayanCharacterSO` |
| `OnBaseHit` | `EnemyMover.OnTriggerEnter2D()` | `HeartSystem` (PLANNED) | none |
| `OnGameOver` | `HeartSystem` (PLANNED) | `GameManager` | none |
| `OnLevelComplete` | `WaveManager` (PLANNED) | `GameManager` | none |
| `OnWaveStarted` | `WaveManager` (PLANNED) | `HUD` (PLANNED) | `int waveIndex` |
| `OnCharacterRecognized` | `RecognitionManager` (PLANNED) | `WaveManager` (PLANNED) | `string characterID` |
| `OnDrawingFailed` | `RecognitionManager` (PLANNED) | `HUD` (PLANNED) | none |
| `OnDrawingStarted` | `StrokeCapture` (PLANNED) | `HUD` (PLANNED) | none |
| `OnHeartsChanged` | `HeartSystem` (PLANNED) | `HUD` (PLANNED) | `int currentHearts` |

[EVIDENCE: Assets/Scripts/Core/EventBus.cs]

### 4.2 Subscription Rules (Enforced by Code Comment)

> "Subscribe in OnEnable. Unsubscribe in OnDisable. No exceptions."

[EVIDENCE: Assets/Scripts/Core/EventBus.cs, line 1 comment]

---

## 5. Critical Sequence Flows

### 5.1 Enemy Defeat Flow

```
Player lifts finger
  → StrokeCapture (PLANNED) captures point cloud
    → RecognitionManager (PLANNED) runs $P algorithm
      → confidence ≥ 0.60?
          YES → EventBus.RaiseCharacterRecognized(characterID)
                  → WaveManager (PLANNED) finds matching enemy
                    → Enemy.Defeat()
                        → EventBus.RaiseEnemyDefeated(character)
                            → AudioManager.PlayPronunciationClip(character)
                        → Enemy.ReturnToPool()
          NO  → EventBus.RaiseDrawingFailed()
                  → HUD (PLANNED) shows red flash / X mark
```

[EVIDENCE: docs/capstone/TDD.md, §3.3 Combat Resolution]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs, Defeat()]
[EVIDENCE: Assets/Scripts/Core/AudioManager.cs, PlayPronunciationClip()]

### 5.2 Base Hit / Game Over Flow

```
EnemyMover.OnTriggerEnter2D(PlayerBase tag)
  → EventBus.RaiseBaseHit()
    → HeartSystem (PLANNED) decrements hearts
      → EventBus.RaiseHeartsChanged(currentHearts)
        → HUD (PLANNED) updates heart display
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
