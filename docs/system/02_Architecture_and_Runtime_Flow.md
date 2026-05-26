# 02 — Architecture and Runtime Flow
**Project:** Salinlahi
**Version:** 1.6
**Date:** 2026-05-27
**Owner:** Jon Wayne Cabusbusan

---

## 1. Scene Inventory

| Scene Name | File | Role |
|------------|------|------|
| Bootstrap | `Assets/_Scenes/Bootstrap.unity` | Instantiates all manager singletons; auto-transitions to MainMenu |
| MainMenu | `Assets/_Scenes/MainMenu.unity` | Entry point for user: Play, Endless, Tracing Dojo, Settings |
| LevelSelect | `Assets/_Scenes/LevelSelect.unity` | Live; level grid with unlock progression and per-era shrine grouping |
| TracingDojo | `Assets/_Scenes/TracingDojo.unity` | Live; practice mode for tracing Baybayin glyphs |
| Gameplay | `Assets/_Scenes/Gameplay.unity` | Core defense loop: enemies, drawing canvas, HUD |
| GameOver | `Assets/_Scenes/GameOver.unity` | Deprecated — replaced by DefeatScreenUI overlay in Gameplay scene (SALIN-58) |

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
└─ Gameplay.unity loads
      └─ GameManager.StartGame() called → GameState.Playing
      └─ WaveManager drives waves (all levels, including boss levels)
      └─ [All waves cleared]
            └─ WaveManager checks LevelConfigSO.bossConfig != null
                  ├─ null → EventBus.RaiseLevelComplete()
                  └─ non-null → WaveSpawner.SpawnBossEnemy(bossConfig)
                              → BossController.StartBoss(config, spawner)
                                    └─ [Final phase cleared] → RunOutro coroutine
                                          → EventBus.RaiseBossDefeated()
                                          → EventBus.RaiseLevelComplete()
      └─ EnemyPool.Get(data) → enemy active in scene
      └─ [Player draws] → RecognitionManager → EventBus.RaiseCharacterRecognized()
      └─ Enemy.Defeat() → EventBus.RaiseEnemyDefeated()
      └─ [Enemy reaches base] → EventBus.RaiseBaseHit() → HeartSystem
      └─ [Hearts == 0] → EventBus.RaiseGameOver()
            └─ GameManager.HandleGameOver() → GameState.GameOver
                  └─ DefeatScreenUI overlay handles UI (Retry / Menu actions in-scene)
```

[EVIDENCE: Assets/Scripts/Core/BootstrapLoader.cs, Start()]
[EVIDENCE: Assets/Scripts/Core/SceneLoader.cs, LoadRoutine()]
[EVIDENCE: Assets/Scripts/Core/GameManager.cs, HandleGameOver()]
[EVIDENCE: Assets/Scripts/Gameplay/Waves/WaveManager.cs, boss dispatch]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossController.cs, StartBoss() / RunOutro()]
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
| `[Manager] ProgressManager.prefab` | `ProgressManager.cs` | Bootstrap scene |

[EVIDENCE: Assets/Prefabs/Managers/ directory]

---

## 4. Event-Driven Interactions

All cross-system communication uses `EventBus.cs`. No direct manager-to-manager method calls occur except via `Instance` for single-frame operations (e.g., `SceneLoader.Instance.LoadGameplay()`).

### 4.1 EventBus Contract Table

| Event | Payload | Raise Method |
|-------|---------|-------------|
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
| `OnBossStarted` | `BossConfigSO` | `RaiseBossStarted(BossConfigSO)` |
| `OnBossPhaseStarted` | `int` (phaseIndex) | `RaiseBossPhaseStarted(int)` |
| `OnBossExhausted` | `int` (phaseIndex) | `RaiseBossExhausted(int)` |
| `OnBossVulnerable` | `int` (phaseIndex) | `RaiseBossVulnerable(int)` |
| `OnBossVulnerabilityWindowActive` | `int` (phaseIndex) | `RaiseBossVulnerabilityWindowActive(int)` |
| `OnBossVulnerabilityExpired` | `int` (phaseIndex) | `RaiseBossVulnerabilityExpired(int)` |
| `OnBossDamaged` | `int phaseIndex, int hpRemaining` | `RaiseBossDamaged(int, int)` |
| `OnBossDefeated` | none | `RaiseBossDefeated()` |
| `OnBossSummonTick` | none | `RaiseBossSummonTick()` — raised by `BossSummonTicker.PlayTickAndSpawn` at each summon tick |
| `OnBossDrawHit` | none | `RaiseBossDrawHit()` — raised by `BossController.TryRouteDraw` on `BossRouteResult.Hit` |
| `OnBossTeleport` | none | `RaiseBossTeleport()` — raised by `PhaseBasedMovement.TeleportNow` on each Teleport-pattern snap |
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
                  → DefeatScreenUI overlay handles in-scene UI (SALIN-58)
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
