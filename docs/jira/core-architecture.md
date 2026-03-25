# Epic: Core Architecture & Infrastructure (SALIN-1)

**Status:** ✅ Complete | **Priority:** High

Foundational Unity systems shared across the entire game: Singleton pattern, EventBus, SceneLoader, ObjectPool, GameManager state machine, Bootstrap scene, and team Git workflow.

---

## SALIN-9 — Implement Singleton\<T\> Generic Base Class

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-10, SALIN-13, SALIN-24 |
| **Blocked By** | — |

Generic abstract MonoBehaviour base class inherited by all manager scripts (GameManager, AudioManager, EventBus, etc.). Enforces a single instance per type across scenes using `DontDestroyOnLoad`. Logs a warning and self-destructs if a duplicate instance is detected at runtime.

**Acceptance Criteria:**
- `Singleton<T>` is a generic abstract MonoBehaviour in `Assets/Scripts/Utilities/`
- `Instance` property returns the single live instance
- A second instance created at runtime destroys itself and logs a warning
- `DontDestroyOnLoad` is applied so the instance persists across scene loads
- GameManager, AudioManager, and EventBus all inherit from it without errors

---

## SALIN-10 — Implement EventBus.cs — Central Publish/Subscribe System

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-13, SALIN-15, SALIN-17, SALIN-18, SALIN-29 |
| **Blocked By** | SALIN-9 |

Centralised pub/sub system for all cross-system communication. 18 events defined across domains: game state, combat, recognition, audio, and UI. All systems subscribe and publish through EventBus rather than holding direct references to each other — enforcing loose coupling throughout the project.

**Acceptance Criteria:**
- `EventBus` exposes typed `Subscribe<T>` and `Publish<T>` methods
- All 18 events are defined and typed (GameStateChangedEvent, EnemyDefeatedEvent, HeartsChangedEvent, OnCharacterRecognized, OnDrawingFailed, etc.)
- Subscribing and unsubscribing is safe to call in Awake/OnDestroy without null errors
- No direct component references between unrelated systems — all communication via EventBus

---

## SALIN-11 — Implement SceneLoader.cs with Async Loading and Fade Stub

| Field      | Value |
|------------|-------|
| **Status** | In Progress |
| **Assignee** | Wayne |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-23, SALIN-43 |
| **Blocked By** | SALIN-9, SALIN-14 |

`SceneLoader.cs` Singleton that loads scenes asynchronously via `LoadSceneAsync`. Exposes `LoadScene(string sceneName)` as the sole public API. Includes a fade-in/fade-out canvas group stub (alpha 0→1 on load, 1→0 on unload) to be completed by `TransitionManager` in SALIN-44.

**Remaining work:** `LevelSelect.unity` scene file does not yet exist. It must be created as part of closing this ticket — the scene is referenced by `SceneLoader` and required by SALIN-43.

**Acceptance Criteria:**
- `SceneLoader.LoadScene(sceneName)` loads asynchronously without freezing the main thread
- Calling `LoadScene` while a load is in progress is a no-op (logged warning)
- `LevelSelect.unity` scene file created with a base Canvas and Camera setup
- Bootstrap scene transitions to MainMenu without errors
- Scene does not flicker or throw null errors on any transition

---

## SALIN-12 — Implement ObjectPool.cs — Generic Unity Object Pool Wrapper

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-15, SALIN-16 |
| **Blocked By** | SALIN-9 |

Wraps `UnityEngine.Pool.ObjectPool<T>` in a generic `ObjectPool<T>` MonoBehaviour. Used by `EnemyPool` and any future projectile systems. Prevents `Instantiate`/`Destroy` calls during gameplay, keeping GC pressure low on mobile.

**Acceptance Criteria:**
- `ObjectPool<T>` exposes `Get()` and `Return(T obj)` methods
- Pool pre-warms with a configurable initial capacity set in the Inspector
- `Get()` reactivates a pooled object or instantiates a new one if the pool is empty
- `Return()` deactivates the object and places it back in the pool
- 50 enemies can be spawned and returned without `Instantiate` being called after initial warm-up

---

## SALIN-13 — Implement GameManager.cs State Machine

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-17, SALIN-29, SALIN-31 |
| **Blocked By** | SALIN-9, SALIN-10 |

Singleton state machine with states: `Idle`, `Playing`, `Paused`, `GameOver`, `LevelComplete`. `ChangeState(GameState newState)` performs the transition and publishes `GameStateChangedEvent` so HUD, spawner, and input systems can react without coupling to GameManager directly. Time scale managed on pause/resume.

**Acceptance Criteria:**
- `GameManager.State` enum: `Idle`, `Playing`, `Paused`, `GameOver`, `LevelComplete`
- `ChangeState()` publishes `GameStateChangedEvent` via EventBus
- Time scale set to 0 on Paused, restored to 1 on Playing
- Transitioning to the same state logs a warning and does nothing
- All state transitions testable from the Bootstrap scene console

---

## SALIN-14 — Configure Bootstrap.unity Scene and All Manager Prefab Shells

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-11, SALIN-23 |
| **Blocked By** | SALIN-9, SALIN-10, SALIN-12, SALIN-13 |

`Bootstrap.unity` is scene index 0 in Build Settings. A `BootstrapLoader` MonoBehaviour instantiates all manager Singleton prefabs from an Inspector array, then calls `SceneLoader.LoadScene("MainMenu")`. All managers persist across scenes via `DontDestroyOnLoad`.

**Acceptance Criteria:**
- Bootstrap is scene index 0 in Build Settings
- `BootstrapLoader` instantiates GameManager, AudioManager, EventBus, SceneLoader, ObjectPool prefabs
- After instantiation, automatically calls `SceneLoader.LoadScene("MainMenu")`
- Running from Bootstrap reaches MainMenu without errors
- All manager Singletons accessible from MainMenu scene via `Instance`

---

## SALIN-26 — Git Branching Strategy and PR Review Checklist

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | Medium |
| **Sprint** | Sprint 1 |
| **Blocks** | — |
| **Blocked By** | — |

Documents and enforces the team's Git workflow. `CONTRIBUTING.md` defines branch naming (`feature/`, `fix/`, `spike/`, `release/`), commit message format, and merge strategy. `main` branch is protected with required PR approval. `.github/pull_request_template.md` contains a checklist for every PR.

**Acceptance Criteria:**
- `CONTRIBUTING.md` exists in repo root with branch naming, commit format, and merge strategy
- `main` is protected: direct pushes blocked, PRs require ≥1 approval
- `.github/pull_request_template.md` includes: code compiles, tested on device, no debug logs, tickets linked
- All team members have confirmed they understand the workflow (comment on ticket)
- First feature PR opened following the new process
