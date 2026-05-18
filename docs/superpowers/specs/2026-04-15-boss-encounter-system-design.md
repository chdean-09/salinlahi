# Boss Encounter System — Design Spec

**Date:** 2026-04-15 (rewritten 2026-05-06)
**Status:** Draft
**Jira:** SALIN-68 (boss framework + El Inquisidor)
**Sources of truth:** [GDD §4.3](../../capstone/GDD.md), [TDD §3.2](../../capstone/TDD.md), existing code in `Assets/Scripts/`

> **Rewrite note (2026-05-06):** This document was reworked after the SALIN-68 placeholder landed. The placeholder added `BossConfigSO`, `LevelConfigSO.isBossLevel`/`bossConfig`, an `Enemy.IsBoss` virtual, and a stub `WaveManager.RunBossEncounter` path — but no phase framework, no `BossController`, no hit routing. SALIN-68 has not started in earnest. This rewrite reconciles the original framework ambition with the placeholder fields, the now-shipped AOE feature ([CombatResolver.cs](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs)), and the now-shipped `WaveSpawner.SpawnWave(WaveConfigSO, Action, int)` and `ActiveEnemyTracker.IsClear` APIs.

---

## 1. Goal

Ship a reusable boss encounter framework and the fully playable **Chapter 1 boss (El Inquisidor, Level 5)**. Levels 10 and 15 receive **config-only stubs** — the framework supports their full mechanics, but their `BossConfigSO` assets start as placeholder 1-phase fights until content is authored in later sprints.

**Non-goals:**
- Implementing Superintendent's label-scrambling ability (stub only).
- Implementing Kadiliman's full 3-phase era-themed fight content (stub only).
- Boss cinematics / dialogue integration (out of scope; can hook into `OnBossStarted` / `OnBossDefeated` later).

## 2. Scope per Boss

| Boss | Level | Framework support | Content authored this spec |
|---|---|---|---|
| El Inquisidor | 5 | Full | Full — 3 phases, intro/outro, summon ability |
| The Superintendent | 10 | Full (label-scramble ability stub) | Placeholder 1-phase |
| Kadiliman | 15 | Full (multi-char phases) | Placeholder 1-phase |

## 3. Architecture Overview

A boss is **an `Enemy` with a `BossController` MonoBehaviour on the same prefab.** This re-uses the existing `EnemyPool`, `WaveSpawner.SpawnEnemy`, sprite/animator infrastructure, and `ActiveEnemyTracker` registration. `BossController` adds the phase state machine and is the source of truth for "is the boss currently targetable / what characters does the player need to draw / what phase are we in."

```
LevelConfigSO (bossConfig != null)
      │
      ▼
WaveManager.RunAllWavesRoutine ──branches──▶ RunBossEncounter(bossConfig)
                                                    │
                                                    ▼
                                       WaveSpawner.SpawnEnemy(bossEnemyData)
                                                    │
                                                    ▼
                                       Enemy + BossController + ability
                                       components on one GameObject
                                                    │
              ┌────────────────────────────────────┼─────────────────────────────────────┐
              ▼                                    ▼                                     ▼
     BossConfigSO (data)                    Ability components               CombatResolver routes draws
     - phases[]                             (subscribe to BossController     via GameManager.CurrentBoss
     - intro/outro                          events; never touch internals)   before AOE / closest-match
```

**Design principle: composition over inheritance.** `BossController` is a single MonoBehaviour that owns a state machine over `BossPhase`. Boss-specific behaviors (summon adds, scramble labels, move in patterns) live as **small ability MonoBehaviours** on the boss prefab that subscribe to `BossController` events. Adding a new boss = new prefab + new config asset + (optionally) new ability components. **No `BossController` subclassing. No abstract ScriptableObject hierarchies.** `Enemy` may be subclassed (e.g., `BossEnemy : Enemy`) when boss-specific behavior cannot be expressed via composition — see §13.7 for the `Enemy.TakeDamage` no-op case.

**Why `Enemy + BossController` rather than a standalone prefab.** Three signals from current code: (a) `Enemy.IsBoss` virtual already exists ([Enemy.cs:45](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L45)), (b) AOE already filters by `m.IsBoss` ([CombatResolver.cs:40,58](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs#L40-L58)), (c) the placeholder `BossConfigSO` already references `EnemyDataSO`. Keeping the boss inside the existing `Enemy` lifecycle means we don't duplicate spawning/pooling/visuals. The boss's `Enemy.Character` stays `null` so it is automatically excluded from `ActiveEnemyTracker.FindClosestToBase` ([ActiveEnemyTracker.cs:62](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs#L62)).

## 4. Level Integration

**Change to [LevelConfigSO.cs](../../../Assets/Scripts/Data/LevelConfigSO.cs):**

```csharp
[Header("Boss")]
[Tooltip("If set, this level is a boss encounter. Waves list is ignored.")]
public BossConfigSO bossConfig;

// REMOVE the existing `public bool isBossLevel;` field.
// Truth value is `bossConfig != null`.
```

**Change to [WaveManager.cs](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs):**

The branch already exists in `RunAllWavesRoutine` ([WaveManager.cs:275](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L275)). Replace it with:

```csharp
if (_levelConfig.bossConfig != null)
{
    yield return StartCoroutine(RunBossEncounter(_levelConfig.bossConfig));
    yield break;
}
```

**Replace the placeholder `RunBossEncounter` body** (currently spawns one Enemy and waits for `IsClear` — [WaveManager.cs:364-418](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L364-L418)) with a hand-off to the boss prefab:

```csharp
private IEnumerator RunBossEncounter(BossConfigSO bossConfig)
{
    if (bossConfig.bossEnemyData == null || bossConfig.phases == null || bossConfig.phases.Count == 0)
    {
        DebugLogger.LogError("WaveManager: BossConfig is incomplete. Aborting boss encounter.");
        AbortRun();
        yield break;
    }

    // Spawn the boss as a regular Enemy. No character assigned — phase gate replaces character matching.
    Enemy bossEnemy = _spawner.SpawnEnemy(bossConfig.bossEnemyData);
    if (bossEnemy == null)
    {
        DebugLogger.LogError("WaveManager: Failed to spawn boss. Aborting boss encounter.");
        AbortRun();
        yield break;
    }

    BossController boss = bossEnemy.GetComponent<BossController>();
    if (boss == null)
    {
        DebugLogger.LogError("WaveManager: Boss prefab is missing BossController. Aborting boss encounter.");
        AbortRun();
        yield break;
    }

    boss.StartBoss(bossConfig, _spawner);

    // Wait for the boss to be defeated (Outro complete) — boss raises OnLevelComplete itself.
    yield return new WaitUntil(() => !CanContinueRun() || boss.IsDefeated);

    if (!CanContinueRun())
    {
        AbortRun();
        yield break;
    }

    // BossController is the source of OnLevelComplete during boss encounters.
    // CompleteRun is intentionally NOT called here.
    _running = false;
    _waveRoutine = null;
}
```

`WaveManager`'s normal wave logic is unchanged. Levels 1–4 and 6–9 are completely unaffected.

## 5. Data: `BossConfigSO`

```csharp
[CreateAssetMenu(fileName = "BossConfig", menuName = "Salinlahi/Boss Config")]
public class BossConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string bossName;          // "El Inquisidor"
    public string bossID;            // "ELINQUISIDOR" — for analytics, save data
    public Sprite bossSprite;        // optional HUD/portrait, distinct from Enemy.sprite

    [Header("Spawning")]
    [Tooltip("EnemyDataSO defining the boss's prefab, base sprite, animator, and shrine-collision behavior.")]
    public EnemyDataSO bossEnemyData;

    [Header("Phases")]
    [Tooltip("Ordered. Phase count = boss's effective hearts.")]
    public List<BossPhase> phases;

    [Header("Intro / Outro")]
    public float introDuration = 2.0f;   // seconds boss is invulnerable on entry
    public float outroDuration = 2.5f;   // seconds before OnLevelComplete fires after defeat
}

[System.Serializable]
public class BossPhase
{
    [Header("Gate")]
    [Tooltip("Characters the player must draw (any order, each once) to clear this phase.")]
    public List<BaybayinCharacterSO> requiredCharacters;

    [Header("Movement")]
    public BossMovementPattern movementPattern;
    public float movementSpeed;

    [Header("Intermission (after this phase clears)")]
    [Tooltip("Mini-wave spawned before the next phase begins. Null = no intermission.")]
    public WaveConfigSO intermissionWave;
    public float postIntermissionDelay;
}

public enum BossMovementPattern { Hover, Pace, Teleport }
```

**Why a list for `requiredCharacters`:**
- Supports El Inquisidor's 1-char-per-phase gating (list of length 1).
- Natively supports Kadiliman's "draw all 17" as 3 era-themed phases of ~6 chars each, no special-case code.
- Phase clears when the player has drawn **every** character in the list exactly once, in any order.

**Fields explicitly NOT included** (placeholder fields removed in this rewrite):
- `maxHealth` — phase count is the single source of truth for boss hearts.
- `phaseCount: int` — replaced by `phases.Count`.
- `moveSpeed` — moves to per-phase `BossPhase.movementSpeed`.
- `animatorController` — belongs on the boss's `Enemy` prefab, not the config.

## 6. `BossController` — State Machine

Single MonoBehaviour. Co-located with `Enemy` on the boss prefab.

### Public API

```csharp
public class BossController : MonoBehaviour
{
    public BossConfigSO Config { get; private set; }
    public BossPhase CurrentPhase { get; private set; }
    public int CurrentPhaseIndex { get; private set; }
    public bool IsTargetable { get; private set; }
    public bool IsDefeated { get; private set; }
    public IReadOnlyList<BaybayinCharacterSO> RequiredCharacters => CurrentPhase?.requiredCharacters;
    public IReadOnlyCollection<BaybayinCharacterSO> DrawnThisPhase => _drawnThisPhase;

    public void StartBoss(BossConfigSO config, WaveSpawner spawner);
    public BossRouteResult TryRouteDraw(string characterID);

    // Local event — fired on every successful Hit. UI listens for per-icon grey-out.
    // Kept local (not on EventBus) because subscribers need the controller-instance handle
    // to read DrawnThisPhase/RequiredCharacters mid-phase. All other phase/intermission
    // notifications go through EventBus per CLAUDE.md's "EventBus for cross-system" rule.
    public event Action OnDrawnThisPhaseChanged;
    // Phase, intermission, defeat events: subscribe via EventBus
    // (OnBossStarted / OnBossPhaseStarted / OnBossPhaseCleared /
    //  OnBossIntermissionStarted / OnBossIntermissionCleared / OnBossDefeated).
}

public enum BossRouteResult
{
    NotRouted,   // characterID not in current phase's required list — caller falls through to AOE/closest-match
    Hit,         // valid required character drawn for the first time this phase
    Duplicate    // required character already drawn this phase — consumed, raises OnDrawingFailed
}
```

### `Enemy` co-location

- Boss `Enemy.Character` is `null` for the entire encounter — phase gating replaces character matching. This makes the boss invisible to `ActiveEnemyTracker.FindClosestToBase` and to AOE counting (which already excludes `IsBoss`).
- The boss's `Enemy.IsBoss` override returns `true`.
- `Enemy.TakeDamage` is **never called on the boss**. All hits route through `BossController.TryRouteDraw` and damage bookkeeping is internal (phase index = HP). Document this on the boss prefab.

### Dependencies

`BossController` does **not** find its `WaveSpawner` via `FindObjectOfType` or singleton lookup. `StartBoss(BossConfigSO config, WaveSpawner spawner)` accepts the spawner as an explicit dependency; `WaveManager.RunBossEncounter` passes its own `_spawner` field. This keeps `BossController` testable in EditMode with a stub spawner — see §12.1.

### States

```
Intro → PhaseActive → PhaseClearedIntermission → PhaseActive → ... → Outro → Defeated
```

### Transition Rules

| State | `IsTargetable` | Behavior | Exit condition |
|---|---|---|---|
| Intro | false | Plays intro animation; `EventBus.OnBossStarted` raised on entry | `config.introDuration` elapsed → PhaseActive(0), raise `EventBus.OnBossPhaseStarted(0)` |
| PhaseActive | **true** | Boss moves per current phase; ability components run | All `requiredCharacters` drawn → raise `EventBus.OnBossPhaseCleared`; if `intermissionWave != null` → PhaseClearedIntermission, else → next PhaseActive or Outro |
| PhaseClearedIntermission | false | Spawns intermission wave via the injected `WaveSpawner.SpawnWave`; waits for `ActiveEnemyTracker.IsClear`; `EventBus.OnBossIntermissionStarted` / `Cleared` bracket the state | All adds cleared + `postIntermissionDelay` elapsed → next PhaseActive |
| Outro | false | Plays defeat animation; `IsDefeated = true` set on entry | `config.outroDuration` elapsed → raise `EventBus.OnBossDefeated` + `EventBus.OnLevelComplete`, return boss to pool |

`IsDefeated` flips at the start of Outro — `WaveManager` polls this to know the encounter is over even before `OnLevelComplete` fires.

### `TryRouteDraw(characterID)` Logic

```
IF NOT IsTargetable:                         return NotRouted   (CombatResolver should not have called)
IF characterID is not in RequiredCharacters: return NotRouted   (falls through to AOE/closest-match)
IF characterID is already in DrawnThisPhase:
    EventBus.RaiseDrawingFailed()
    return Duplicate                         (consumed; does NOT fall through)
ELSE:
    add to DrawnThisPhase
    raise OnDrawnThisPhaseChanged
    IF DrawnThisPhase.Count == RequiredCharacters.Count:
        EventBus.RaiseBossPhaseCleared(CurrentPhaseIndex)
        advance to intermission or next phase
    return Hit
```

`_drawnThisPhase` is a `HashSet<BaybayinCharacterSO>` reset whenever a new phase becomes active (just before `EventBus.RaiseBossPhaseStarted` fires). Match is by SO reference, not `characterID` string — `BossController` resolves the string to an SO via the phase's required list (since `requiredCharacters` carries the canonical SOs).

## 7. Hit Routing — `CombatResolver` Change

One new block at the top of `HandleCharacterRecognized` ([CombatResolver.cs:22](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs#L22)), **before AOE detection and closest-match**:

```csharp
private void HandleCharacterRecognized(string characterID)
{
    // Boss route — runs before AOE and closest-match.
    BossController boss = GameManager.Instance?.CurrentBoss;
    if (boss != null && boss.IsTargetable)
    {
        BossRouteResult routed = boss.TryRouteDraw(characterID);
        if (routed != BossRouteResult.NotRouted)
            return;  // Hit or Duplicate — boss consumed the draw.
    }

    // ... existing AOE + closest-match logic unchanged ...
}
```

**Routing precedence (explicit):**
1. Boss-route check (if `CurrentBoss != null && IsTargetable`).
2. AOE mass-defeat check (existing).
3. Closest-match single hit (existing).

**Consequences:**
- The boss is never enumerated by closest-match (`Enemy.Character == null` short-circuits) and is filtered from AOE (`m.IsBoss`).
- During intermissions, `IsTargetable` is false. Every draw falls through to AOE/closest-match on adds.
- During an active phase, drawing a required character that's already been drawn raises `OnDrawingFailed` and is consumed (the player does not accidentally hit adds with a "duplicate" draw).
- During an active phase, drawing a non-required character falls through to AOE/closest-match. If `SummonWaveOnPhaseStart` adds carry that character, they take the hit normally. AOE is possible on adds.
- Content authoring should generally avoid required-char ↔ add-char collisions, but the engine behavior is deterministic when they happen: the boss eats the hit (the boss-route check fires first).

**UX consistency note (deliberate design choice):** wrong-character draws (not in required list) and duplicate-required draws produce different events — wrong falls through (may hit adds via AOE/closest-match, or raise `OnDrawingMissed` if no match), duplicate is consumed via `OnDrawingFailed`. This asymmetry is acceptable: the player perceives both as "that scribble didn't help me," and the difference matters to add-targeting feedback. Resist the temptation to unify them.

**`GameManager.CurrentBoss` lifecycle:** set inside `BossController.StartBoss` (immediately after `Config = config` is assigned), cleared in `BossController.OnDisable`. **Not set in `OnEnable`** — at the moment of `OnEnable` the controller has no config yet (the boss `Enemy` is enabled by `EnemyPool.Get` before `WaveManager` calls `StartBoss`). Tying the lifecycle to `StartBoss` ensures `CurrentBoss` is never observable in an unconfigured state.

## 8. Ability Components

Small MonoBehaviours attached to the boss prefab. Each finds `BossController` via `GetComponent<BossController>()` in `Awake` (used to read state like `CurrentPhase` / `CurrentPhaseIndex` / `IsTargetable`) and subscribes to `EventBus` boss events in `OnEnable` / unsubscribes in `OnDisable`. They never touch `BossController` internals — only its public read-only properties and `EventBus` events.

Because `EventBus` events are global, ability components must filter on `BossController.IsTargetable` (or the matching state predicate) before acting — there is only one `CurrentBoss` at a time, so cross-boss confusion is not a risk, but no-ops during Intro/Intermission/Outro must still be enforced.

### 8.1 `SummonWaveOnPhaseStart` (implement for El Inquisidor)

Spawns a mini-wave of adds **during** an active phase (distinct from intermissions). Configurable:
- List of phase indices to trigger on.
- `WaveConfigSO` reference for the wave to spawn.

Calls `WaveSpawner.SpawnWave(wave, ...)` ([WaveSpawner.cs:99](../../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs#L99)) — already shipped, no API changes needed.

El Inquisidor uses this for his GDD-specified "summons Soldado reinforcements during phases."

This is the **load-bearing threat source**. The boss does not directly attack the shrine in this design; threat comes from summoned adds walking toward the shrine. A boss without `SummonWaveOnPhaseStart` is non-threatening, which is acceptable for stubs but a deliberate content-authoring concern for finished bosses.

### 8.2 `PhaseBasedMovement` (implement for all)

Reads `CurrentPhase.movementPattern` on `EventBus.OnBossPhaseStarted` and drives a movement coroutine. Exists as its own component so `BossController` doesn't need to know about Unity transforms — keeps the state machine testable in isolation.

### 8.3 `ScrambleLabelsWhilePhaseActive` — **STUB ONLY, do not implement**

Architecturally accommodated: on `EventBus.OnBossPhaseStarted` it would start scrambling nearby enemy labels; on `EventBus.OnBossPhaseCleared` it would stop. No file needs to be written this sprint — just note the shape so nothing in the framework blocks it later.

When the Superintendent's content sprint lands, it will need to pair `ScrambleLabelsWhilePhaseActive` with **either** a `SummonWaveOnPhaseStart` or a separate threat ability. Label-scrambling alone provides no shrine threat.

### Coroutines and pause

All ability-component coroutines must use `WaitForSeconds` (scaled time). Pause halts automatically via `Time.timeScale = 0` ([GameManager.cs:78](../../../Assets/Scripts/Core/GameManager.cs#L78)). **Do not use `WaitForSecondsRealtime` in this subsystem** — it will run during pause and de-sync the encounter.

## 9. EventBus Additions

Add to [EventBus.cs](../../../Assets/Scripts/Core/EventBus.cs):

```csharp
public static event Action<BossConfigSO> OnBossStarted;
public static event Action<int> OnBossPhaseStarted;          // phase index
public static event Action<int> OnBossPhaseCleared;          // phase index
public static event Action OnBossIntermissionStarted;
public static event Action OnBossIntermissionCleared;
// OnBossDefeated already exists.

public static void RaiseBossStarted(BossConfigSO c)        => OnBossStarted?.Invoke(c);
public static void RaiseBossPhaseStarted(int i)            => OnBossPhaseStarted?.Invoke(i);
public static void RaiseBossPhaseCleared(int i)            => OnBossPhaseCleared?.Invoke(i);
public static void RaiseBossIntermissionStarted()          => OnBossIntermissionStarted?.Invoke();
public static void RaiseBossIntermissionCleared()          => OnBossIntermissionCleared?.Invoke();
```

`OnBossIntermissionStarted/Cleared` are needed by the icon-row UI (to hide while adds-only) and by `SummonWaveOnPhaseStart`-style abilities (to know not to fire during intermissions).

Existing `OnDrawingFailed` and `OnLevelComplete` are reused unchanged.

## 10. UI — Boss Label Icon Row

Unlike normal enemies (one character above their head), bosses display a **row of Baybayin character icons** representing the current phase's required characters. Each icon greys out as the player draws it.

**Sizing rules** (must accommodate smallest supported device — 360dp Android portrait):

- Icons: 32×32 logical px (uses `BaybayinCharacterSO.displaySprite` — the same sprite shown above normal enemies; no new content field needed).
- Gap between icons: 4px.
- Max 6 icons per row. 6 icons = 6×32 + 5×4 = **212px**, fits 360dp with margins.
- If a phase has >6 chars (should not happen in this spec's content), wrap to a second row.
- Row is centered horizontally on the boss's screen position and follows during movement.
- **Active state:** full color, subtle pulse animation.
- **Drawn state:** 40% alpha, desaturated.

**Subscriptions** (all events on `EventBus` except the per-Hit local event, which carries the controller-instance handle the UI needs to read `RequiredCharacters` / `DrawnThisPhase`):

- `EventBus.OnBossPhaseStarted` → resolve `GameManager.CurrentBoss`, rebuild the icon row from its `RequiredCharacters`.
- `BossController.OnDrawnThisPhaseChanged` (local — subscribed via `GameManager.CurrentBoss` resolved on phase-start) → re-evaluate per-icon active/drawn state.
- `EventBus.OnBossPhaseCleared` → flash + hide.
- `EventBus.OnBossIntermissionStarted` → ensure hidden.
- `EventBus.OnBossIntermissionCleared` → no-op; row re-shows on the following `OnBossPhaseStarted`.

The icon-row UI must re-resolve `GameManager.CurrentBoss` on every `OnBossPhaseStarted` and re-subscribe `OnDrawnThisPhaseChanged` to that controller (unsubscribing on `OnBossPhaseCleared`). Bosses are short-lived per encounter; subscribing once at scene-start is incorrect because the controller instance changes per level.

**Kadiliman phase-1 check:** 6 Spanish characters = exact tightest case, fits without wrap. Phase 2 (6 American) same. Phase 3 (5 Japanese) easier.

## 11. Content Examples

### El Inquisidor (full)

```
BossConfig_ElInquisidor:
  bossName: "El Inquisidor"
  bossID: "ELINQUISIDOR"
  bossEnemyData: EnemyData_Boss_ElInquisidor (custom EnemyDataSO with boss sprite)
  introDuration: 2.0
  outroDuration: 2.5
  phases:
    - requiredCharacters: [BA]
      movement: Hover, speed 0
      intermissionWave: Boss_L5_Intermission1 (3 Soldados over 3s)
      postIntermissionDelay: 1.0
    - requiredCharacters: [KA]
      movement: Pace, speed 1.0
      intermissionWave: Boss_L5_Intermission2 (5 Soldados over 4s)
      postIntermissionDelay: 1.0
    - requiredCharacters: [GA]
      movement: Teleport, speed 0 (teleport cadence baked into PhaseBasedMovement)
      intermissionWave: null
      postIntermissionDelay: 0
```

Prefab ability components: `PhaseBasedMovement`, `SummonWaveOnPhaseStart` (triggers on phases 0 and 1, spawns 2 extra Soldados mid-phase).

### Superintendent (stub)

Placeholder 1-phase `BossConfigSO`, required char = [A], hover, no intermission. Plays but has no scramble ability and no summon. Level 10 is beatable but not interesting until content sprint.

### Kadiliman (stub)

The existing placeholder asset `Assets/ScriptableObjects/BossConfig_Kadiliman.asset` is repurposed to fit the new shape during SALIN-68 implementation. Same shape as Superintendent stub. Framework already supports the real design (3 era-themed phases with 6/6/5 chars), but the asset is a 1-phase placeholder until content is authored.

## 12. Testing Strategy

### 12.1 Edit-mode unit tests (`Assets/Tests/Editor/Gameplay/BossControllerTests.cs`)

`BossController` is a MonoBehaviour but its state logic is testable in EditMode using the same pattern as [ActiveEnemyTrackerTests](../../../Assets/Tests/Editor/Gameplay/ActiveEnemyTrackerTests.cs): `new GameObject().AddComponent<BossController>()`, programmatically constructed `BossConfigSO` via `ScriptableObject.CreateInstance`, drive `TryRouteDraw` directly, assert on `CurrentPhaseIndex`, `IsTargetable`, and event firing.

Target cases:
1. Phase with 1 required char: correct draw → `Hit`, advances. Non-required draw → `NotRouted`, no advance.
2. Phase with 3 required chars: must draw all 3 in any order to clear; first two return `Hit` without phase clear, third returns `Hit` and raises `EventBus.OnBossPhaseCleared`.
3. Duplicate required-char draw in same phase → `Duplicate`, raises `OnDrawingFailed`, no advance, no fall-through.
4. Last phase cleared → `OnBossDefeated` and `OnLevelComplete` fire, after `outroDuration`.
5. Intro: `IsTargetable` is false; `TryRouteDraw` returns `NotRouted` regardless of input.
6. Intermission: `IsTargetable` is false; `TryRouteDraw` returns `NotRouted`.
7. `IsDefeated` flips true at the start of Outro.

Intermission spawning is tested via a fake `WaveSpawner`. The fixture constructs a stub `WaveSpawner` (or a thin test-only subclass that overrides `SpawnWave` to record calls and complete immediately) and passes it into `BossController.StartBoss(config, stubSpawner)`. The injection point is the `StartBoss` parameter — no reflection, no `FindObjectOfType`, no `[SerializeField]` swap.

### 12.2 Play-mode smoke test (`Assets/Tests/PlayMode/Gameplay/ElInquisidorTest.cs`)

One end-to-end test: load a test scene with a minimal `LevelConfigSO` pointing at `BossConfig_ElInquisidor`, drive input via direct calls to `BossController.TryRouteDraw`, assert the full Intro → 3 phases → 2 intermissions → Outro → `OnLevelComplete` sequence.

### 12.3 Ability component tests

`SummonWaveOnPhaseStart` and `PhaseBasedMovement` each get edit-mode tests with a fake `BossController` stub exposing events manually.

---

## 13. Risks & Cross-Feature Dependencies

**These items create work for features outside this spec. Each should become its own Jira task unless noted as in-scope for SALIN-68.**

### 13.1 ✅ `WaveSpawner.SpawnWave` API — already shipped

`WaveSpawner.SpawnWave(WaveConfigSO, Action, int)` exists at [WaveSpawner.cs:99](../../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs#L99). No refactor needed. (This was a blocker risk in the original spec.)

### 13.2 ✅ `ActiveEnemyTracker.IsClear` — already shipped

`ActiveCount` and `IsClear` exist at [ActiveEnemyTracker.cs:15,24](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs#L15-L24). No additions needed.

### 13.3 🟠 `GameManager.CurrentBoss` — IN SCOPE for SALIN-68

`CombatResolver` needs a way to find the active `BossController`. Add a property to [GameManager](../../../Assets/Scripts/Core/GameManager.cs):

```csharp
public BossController CurrentBoss { get; private set; }
internal void SetCurrentBoss(BossController boss) => CurrentBoss = boss;
```

Set inside `BossController.StartBoss` (immediately after `Config = config`), cleared in `BossController.OnDisable`. Do **not** set in `OnEnable` — the controller has no config at that moment, and `CombatResolver` reading a partially-initialized `CurrentBoss` would see `RequiredCharacters` as null.

### 13.4 ✅ Pause — no per-component work needed

`GameManager.PauseGame` sets `Time.timeScale = 0` ([GameManager.cs:78](../../../Assets/Scripts/Core/GameManager.cs#L78)). All `BossController` and ability coroutines using `WaitForSeconds` halt automatically. **The constraint is: never use `WaitForSecondsRealtime` in this subsystem.** Documented on each component header. (This was a per-component risk in the original spec; it dissolves given the project's pause model.)

### 13.5 🟡 AOE interaction with boss label icon row

The AOE mechanic (`CombatResolver._aoeThreshold`, default 3) operates on non-boss enemies via closest-match. Boss is excluded by construction (`m.IsBoss` filter and the boss-route check fires first). **No conflict in logic.** The visual concern stands: the icon row must not be obscured by AOE effect particles when the boss is on screen.

**Dependency:** note for AOE VFX ticket: "Reserve UI layer above boss sprite; do not spawn AOE particles in that region when boss is on screen."

### 13.6 ✅ Icon pipeline — reuse `displaySprite`

`BaybayinCharacterSO.displaySprite` ([BaybayinCharacterSO.cs:13](../../../Assets/Scripts/Data/BaybayinCharacterSO.cs#L13)) is the same sprite shown above normal enemies and is suitable for the icon row at 32×32. **No new field or asset authoring required.** If art demands a separate icon size in a later sprint, add the field then.

### 13.7 🟠 `Enemy.TakeDamage` bypass for boss — DOCUMENT IN CODE

The boss's `Enemy.TakeDamage` is **never called** under normal play. All hits route via `BossController.TryRouteDraw`. A future contributor adding direct-damage code (e.g., a "deal X damage on contact" projectile) could bypass the phase gate.

**Mitigation:** override `Enemy.TakeDamage` on the boss prefab (or in a `BossEnemy` subclass) to no-op or assert. **Include as a line item in the SALIN-68 implementation ticket.**

### 13.8 ✅ Cleanup on scene change / quit — relies on existing infrastructure

If the player quits to menu mid-encounter, `SceneLoader.LoadRoutine` resets `Time.timeScale = 1f` ([SceneLoader.cs:119](../../../Assets/Scripts/Core/SceneLoader.cs#L119)) and Unity unloads the Gameplay scene, which destroys the boss prefab and stops all its coroutines. `BossController.OnDisable` clears `GameManager.CurrentBoss`. **No custom cleanup logic is required in this subsystem.**

### 13.9 🟢 Stubs for Superintendent / Kadiliman

**Status:** not a blocker. Config-only placeholder assets ship with this spec. Real content authored in later sprints. Framework supports both full designs — no code changes needed when content lands.

**Note:** the existing `BossConfig_Kadiliman.asset` placeholder is reshaped during SALIN-68 implementation to fit the new `BossConfigSO` schema. No data migration script needed since the placeholder isn't referenced from any shipped level yet.

---

## 14. Acceptance Criteria

1. ✅ `BossConfigSO` exists with `phases`, `intro/outroDuration`, identity fields, and `bossEnemyData`. Old fields (`maxHealth`, `phaseCount`, `moveSpeed`, `animatorController`) removed.
2. ✅ `BossPhase.requiredCharacters` is a list; phase clears when all drawn once (any order).
3. ✅ `LevelConfigSO.bossConfig` retained; `LevelConfigSO.isBossLevel` removed; `WaveManager.RunAllWavesRoutine` branches on `bossConfig != null`.
4. ✅ `BossController` implements Intro → PhaseActive → Intermission → Outro state machine.
5. ✅ `BossController.IsTargetable` returns false during Intro, Intermission, and Outro; verified by test.
6. ✅ `CombatResolver` calls `BossController.TryRouteDraw` before AOE and closest-match; `Hit` and `Duplicate` short-circuit; `NotRouted` falls through.
7. ✅ Drawing the wrong character during a phase falls through to closest-match/AOE on adds; drawing a duplicate required character raises `EventBus.OnDrawingFailed` and is consumed (no fall-through).
8. ✅ Last phase cleared raises `EventBus.OnBossDefeated` and (after outro) `EventBus.OnLevelComplete`.
9. ✅ El Inquisidor plays as a full 3-phase encounter with summon ability and 2 intermission waves.
10. ✅ Superintendent and Kadiliman have config-only placeholder assets that load and are beatable.
11. ✅ Boss label icon row renders, follows the boss, and greys out drawn characters. Hides during intermission. Fits 360dp portrait at 6 icons.
12. ✅ Edit-mode unit tests cover the `BossController` state machine cases listed in §12.1.
13. ✅ Play-mode smoke test covers El Inquisidor end-to-end.
14. ✅ All `BossController` and ability-component coroutines use scaled time (`WaitForSeconds`); no `WaitForSecondsRealtime` in this subsystem. Verified by code review.
15. ✅ `Enemy.IsBoss` returns true on the boss instance; AOE excludes it; `ActiveEnemyTracker.FindClosestToBase` never returns it.
16. ✅ `GameManager.CurrentBoss` is set inside `BossController.StartBoss` and cleared in `BossController.OnDisable`. Not set in `OnEnable`.
17. ✅ Player loses no hearts from wrong/duplicate draws against the boss; the only heart-loss path during a boss fight is adds reaching the shrine.
18. ✅ `BossController.StartBoss(BossConfigSO, WaveSpawner)` accepts the spawner as an explicit parameter — no `FindObjectOfType`, no scene lookup. EditMode tests inject a stub.
19. ✅ All cross-system boss notifications (`OnBossStarted`, `OnBossPhaseStarted`, `OnBossPhaseCleared`, `OnBossIntermissionStarted`, `OnBossIntermissionCleared`, `OnBossDefeated`) flow through `EventBus`. Only `OnDrawnThisPhaseChanged` is a local `BossController` event, by design.

## 15. References

- [GDD §4.3 — Enemies and Bosses](../../capstone/GDD.md)
- [TDD §3.2 — Wave Management](../../capstone/TDD.md)
- [04_Gameplay_Systems.md](../../system/04_Gameplay_Systems.md)
- [CLAUDE.md](../../../CLAUDE.md)
- [EventBus.cs](../../../Assets/Scripts/Core/EventBus.cs)
- [WaveManager.cs](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs)
- [WaveSpawner.cs](../../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs)
- [LevelConfigSO.cs](../../../Assets/Scripts/Data/LevelConfigSO.cs)
- [BossConfigSO.cs](../../../Assets/Scripts/Data/BossConfigSO.cs)
- [CombatResolver.cs](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs)
- [ActiveEnemyTracker.cs](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs)
- [Enemy.cs](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs)
- [GameManager.cs](../../../Assets/Scripts/Core/GameManager.cs)
- [ActiveEnemyTrackerTests.cs](../../../Assets/Tests/Editor/Gameplay/ActiveEnemyTrackerTests.cs)
