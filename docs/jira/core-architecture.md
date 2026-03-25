# Epic: Core Architecture & Infrastructure (SALIN-1)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

Foundational Unity systems shared across the entire game: Singleton pattern, EventBus, SceneLoader, ObjectPool, GameManager state machine, Bootstrap scene, and team Git workflow.

---

## SALIN-9 — Implement Singleton\<T\> Generic Base Class

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Create a generic `Singleton<T>` MonoBehaviour base class that all manager scripts (GameManager, AudioManager, etc.) will inherit from. It must enforce a single instance across scenes using DontDestroyOnLoad and log a warning if a duplicate is detected.

**Acceptance Criteria:**
- `Singleton<T>` is a generic abstract MonoBehaviour in `Assets/Scripts/Core/`
- `Instance` property returns the single live instance
- If a second instance is created at runtime, it destroys itself and logs a warning
- `DontDestroyOnLoad` is applied so the instance persists across scene loads
- At least three manager scripts (GameManager, AudioManager, EventBus) inherit from it successfully without errors

---

## SALIN-10 — Implement EventBus.cs — Central Publish/Subscribe System

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

AC rewrite applied — see traceability comment.

---

## SALIN-11 — Implement SceneLoader.cs with Async Loading and Fade Stub

| Field | Value |
|-------|-------|
| **Status** | In Progress |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Create `SceneLoader.cs` that loads scenes asynchronously using `LoadSceneAsync`. It must expose a simple API (`LoadScene(string sceneName)`) and include a fade-in/fade-out stub that will be wired up properly in SALIN-44.

**Acceptance Criteria:**
- `SceneLoader.LoadScene(sceneName)` loads the target scene asynchronously without freezing the main thread
- A coroutine-based fade stub is present (canvas group alpha 0→1 on load, 1→0 on unload)
- Calling `LoadScene` while a load is already in progress is a no-op (logged warning)
- Bootstrap scene can trigger a load to MainMenu without errors
- Scene does not flicker or throw errors on the transition

---

## SALIN-12 — Implement ObjectPool.cs — Generic Unity Object Pool Wrapper

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Wrap Unity's built-in `UnityEngine.Pool.ObjectPool<T>` in a generic `ObjectPool<T>` MonoBehaviour component. Enemy spawning and projectile systems will use this instead of `Instantiate`/`Destroy` to keep GC pressure low on mobile.

**Acceptance Criteria:**
- `ObjectPool<T>` exposes `Get()` and `Return(T obj)` methods
- Pool pre-warms with a configurable initial capacity set in the Inspector
- `Get()` re-activates a pooled object or instantiates a new one if the pool is empty
- `Return()` deactivates the object and places it back in the pool
- A basic integration test scene confirms 50 enemies can be spawned and returned without `Instantiate` being called after initial warm-up

---

## SALIN-13 — Implement GameManager.cs State Machine (Playing/Paused/GameOver/LevelComplete)

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `GameManager.cs` as a Singleton state machine with four states: `Playing`, `Paused`, `GameOver`, and `LevelComplete`. State transitions must publish EventBus events so the HUD, enemy spawner, and input system can react without coupling directly to GameManager.

**Acceptance Criteria:**
- `GameManager.State` enum has `Playing`, `Paused`, `GameOver`, `LevelComplete`
- `ChangeState(GameState newState)` performs the transition and publishes a `GameStateChangedEvent`
- Time scale is set to 0 on `Paused` and restored to 1 on `Playing`
- Cannot transition to the same state (logged warning)
- Console shows the correct state printed on each transition when tested manually in the Bootstrap scene

---

## SALIN-14 — Configure Bootstrap.unity Scene and All Manager Prefab Shells

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Set up the `Bootstrap.unity` scene as the first scene in Build Settings (index 0). It should instantiate all manager Singleton prefabs via a `BootstrapLoader` script and then immediately load the MainMenu scene.

**Acceptance Criteria:**
- `Bootstrap` is scene index 0 in Build Settings
- A `BootstrapLoader` MonoBehaviour instantiates each manager prefab listed in its Inspector array
- After instantiation, it calls `SceneLoader.LoadScene("MainMenu")` automatically
- Running the game from Bootstrap reaches MainMenu without errors
- All manager Singletons persist and are accessible from MainMenu scene

---

## SALIN-26 — Git Branching Strategy and PR Review Checklist

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Document and enforce the team's Git branching strategy and create a PR review checklist.

**Acceptance Criteria:**
- `CONTRIBUTING.md` in the repo root defines: branch naming convention (`feature/`, `fix/`, `spike/`, `release/`), commit message format, and merge strategy
- `main` branch is protected: direct pushes blocked, PRs require at least 1 approval
- A PR description template is added to `.github/pull_request_template.md`
- All team members have confirmed they understand the workflow
- First real feature PR is opened following the new process
