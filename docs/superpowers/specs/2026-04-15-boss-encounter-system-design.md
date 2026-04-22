# Boss Encounter System — Design Spec

**Date:** 2026-04-15
**Status:** Approved (pending spec review)
**Jira:** SALIN-TBD (boss framework + El Inquisidor)
**Sources of truth:** [GDD §4.3](../../capstone/GDD.md), [TDD §3.2](../../capstone/TDD.md), existing code in `Assets/Scripts/`

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

Boss levels are **dedicated levels** — [LevelConfigSO](../../../Assets/Scripts/Data/LevelConfigSO.cs) gains a `bossConfig` field. When set, `WaveManager` hands off to a new `BossController` and the normal wave flow is skipped entirely for that level.

```
LevelConfigSO (bossConfig != null)
      │
      ▼
WaveManager.Start() ──branches──▶ BossController.StartBoss(bossConfig)
                                         │
              ┌──────────────────────────┼──────────────────────────┐
              ▼                          ▼                          ▼
     BossConfigSO (data)       Ability components           CombatResolver
     - phases[]                (MonoBehaviours on           reads BossController
     - intro/outro             the boss prefab,             to route hits on
                               subscribe to events)         required characters
```

**Design principle: composition over inheritance.** `BossController` is a single MonoBehaviour that owns a state machine over `BossPhase`. Boss-specific behaviors (summon adds, scramble labels, move in patterns) live as **small ability MonoBehaviours** on the boss prefab that subscribe to `BossController` events. Adding a new boss = new prefab + new config asset + (optionally) new ability components. **No `BossController` subclassing. No abstract ScriptableObject hierarchies.**

## 4. Level Integration

**Change to [LevelConfigSO.cs](../../../Assets/Scripts/Data/LevelConfigSO.cs):**

```csharp
[Header("Boss")]
[Tooltip("If set, this level is a boss encounter. Waves list is ignored.")]
public BossConfigSO bossConfig;
```

**Change to [WaveManager.cs](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs) (minimal branch at entry to `Start`):**

```csharp
private void Start()
{
    // ... existing GameManager / level resolution ...
    if (_levelConfig.bossConfig != null)
    {
        _bossController.StartBoss(_levelConfig.bossConfig);
        return;
    }
    StartWaves();
}
```

`WaveManager`'s existing wave logic is unchanged. Levels 1–4 and 6–9 are completely unaffected.

## 5. Data: `BossConfigSO`

```csharp
[CreateAssetMenu(fileName = "BossConfig", menuName = "Salinlahi/Boss Config")]
public class BossConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string bossName;          // "El Inquisidor"
    public Sprite bossSprite;        // 64×64
    public GameObject bossPrefab;    // prefab with BossController + ability components

    [Header("Phases")]
    public List<BossPhase> phases;   // ordered; phase count = boss's effective hearts

    [Header("Intro / Outro")]
    public float introDuration;      // seconds boss is invulnerable on entry
    public float outroDuration;      // seconds before OnLevelComplete fires after defeat
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

**No `defeatSequence` field, no `maxHealth` field.** Phase count is the single source of truth for boss hearts.

## 6. `BossController` — State Machine

Single MonoBehaviour on the boss prefab.

### Public API

```csharp
public class BossController : MonoBehaviour
{
    public BossConfigSO Config { get; private set; }
    public BossPhase CurrentPhase { get; private set; }
    public int CurrentPhaseIndex { get; private set; }
    public bool IsTargetable { get; private set; }
    public IReadOnlyList<BaybayinCharacterSO> RequiredCharacters => CurrentPhase?.requiredCharacters;
    public IReadOnlyCollection<BaybayinCharacterSO> DrawnThisPhase => _drawnThisPhase;

    public void StartBoss(BossConfigSO config);
    public void TryHit(BaybayinCharacterSO drawn);

    public event Action<int> OnPhaseStarted;
    public event Action<int> OnPhaseCleared;
    public event Action OnIntermissionStarted;
    public event Action OnIntermissionCleared;
    // Boss defeat is observed only via EventBus.OnBossDefeated / OnLevelComplete — no local event.
}
```

### States

```
Intro → PhaseActive → PhaseClearedIntermission → PhaseActive → ... → Outro → (Defeated)
```

### Transition Rules

| State | `IsTargetable` | Behavior | Exit condition |
|---|---|---|---|
| Intro | false | Plays intro animation | `config.introDuration` elapsed → PhaseActive(0) |
| PhaseActive | **true** | Boss moves per current phase; ability components run | All `requiredCharacters` drawn → raise `OnPhaseCleared`; if `intermissionWave != null` → PhaseClearedIntermission, else → next PhaseActive or Outro |
| PhaseClearedIntermission | false | Spawns intermission wave; waits for active enemies to reach 0 | All adds cleared + `postIntermissionDelay` elapsed → next PhaseActive |
| Outro | false | Plays defeat animation | `config.outroDuration` elapsed → raise `OnBossDefeated` + `OnLevelComplete` |

### `TryHit(drawn)` Logic

```
IF boss is not in PhaseActive: ignore (should never be called by CombatResolver — IsTargetable is false).
IF drawn ∈ CurrentPhase.requiredCharacters AND drawn ∉ _drawnThisPhase:
    add to _drawnThisPhase
    IF _drawnThisPhase now contains all requiredCharacters:
        raise OnPhaseCleared(CurrentPhaseIndex)
        advance to intermission or next phase
ELSE:
    EventBus.RaiseDrawingFailed()
    (covers: wrong character, duplicate draw in same phase)
```

`_drawnThisPhase` is a `HashSet<BaybayinCharacterSO>` reset on each `OnPhaseStarted`.

## 7. Hit Routing — `CombatResolver` Change

One new rule at the top of the resolve method (~5 lines):

> If `GameManager.Instance.CurrentBoss != null` AND `CurrentBoss.IsTargetable` AND `drawn ∈ CurrentBoss.RequiredCharacters` AND `drawn ∉ CurrentBoss.DrawnThisPhase`: call `CurrentBoss.TryHit(drawn)` and return. Otherwise, proceed with existing closest-match and AOE logic on non-boss enemies.

**Consequences:**
- The boss is never enumerated by closest-match. AOE is inherently scoped to non-boss enemies (no conflict with the upcoming AOE feature).
- During intermissions, `IsTargetable` is false, so **every** draw — including characters that would match a hypothetical boss phase — routes to adds via the normal closest-match path. The boss's required-character list has no effect while `IsTargetable` is false.
- If the player draws a required character during an active phase while adds on screen happen to share that character, the draw routes to the boss and the adds are not affected. Content authoring should avoid this collision, but the engine behavior is deterministic either way.
- `GameManager` gains a `public BossController CurrentBoss { get; set; }` — set in `StartBoss`, cleared on outro.

## 8. Ability Components

Small MonoBehaviours attached to the boss prefab. Each finds `BossController` via `GetComponent<BossController>()` in `Awake` and subscribes to its events in `OnEnable` / unsubscribes in `OnDisable`. They never touch `BossController` internals — only public events and methods.

### 8.1 `SummonWaveOnPhaseStart` (implement for El Inquisidor)

Spawns a mini-wave of adds **during** an active phase (distinct from intermissions). Configurable:
- List of phase indices to trigger on.
- `WaveConfigSO` reference for the wave to spawn.

El Inquisidor uses this for his GDD-specified "summons Soldado reinforcements during phases."

### 8.2 `PhaseBasedMovement` (implement for all)

Reads `CurrentPhase.movementPattern` on `OnPhaseStarted` and drives a movement coroutine. Exists as its own component so `BossController` doesn't need to know about Unity transforms — keeps the state machine testable in isolation.

### 8.3 `ScrambleLabelsWhilePhaseActive` — **STUB ONLY, do not implement**

Architecturally accommodated: on `OnPhaseStarted` it would start scrambling nearby enemy labels; on `OnPhaseCleared` it would stop. No file needs to be written this sprint — just note the shape so nothing in the framework blocks it later.

## 9. EventBus Additions

Add to [EventBus.cs](../../../Assets/Scripts/Core/EventBus.cs):

```csharp
public static event Action<BossConfigSO> OnBossStarted;
public static event Action<int> OnBossPhaseStarted;   // phase index
public static event Action<int> OnBossPhaseCleared;   // phase index
public static event Action OnBossDefeated;

public static void RaiseBossStarted(BossConfigSO c) => OnBossStarted?.Invoke(c);
public static void RaiseBossPhaseStarted(int i)     => OnBossPhaseStarted?.Invoke(i);
public static void RaiseBossPhaseCleared(int i)     => OnBossPhaseCleared?.Invoke(i);
public static void RaiseBossDefeated()              => OnBossDefeated?.Invoke();
```

Existing `OnDrawingFailed` and `OnLevelComplete` are reused unchanged.

## 10. UI — Boss Label Icon Row

Unlike normal enemies (one character above their head), bosses display a **row of Baybayin character icons** representing the current phase's required characters. Each icon greys out as the player draws it.

**Sizing rules** (must accommodate smallest supported device — 360dp Android portrait):

- Icons: 32×32 logical px.
- Gap between icons: 4px.
- Max 6 icons per row. 6 icons = 6×32 + 5×4 = **212px**, fits 360dp with margins.
- If a phase has >6 chars (should not happen in this spec's content), wrap to a second row.
- Row is centered horizontally on the boss's screen position and follows during movement.
- **Active state:** full color, subtle pulse animation.
- **Drawn state:** 40% alpha, desaturated.

**Kadiliman phase-1 check:** 6 Spanish characters = exact tightest case, fits without wrap. Phase 2 (6 American) same. Phase 3 (5 Japanese) easier.

## 11. Content Examples

### El Inquisidor (full)

```
BossConfig_ElInquisidor:
  bossName: "El Inquisidor"
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

Placeholder 1-phase `BossConfigSO`, required char = [A], hover, no intermission. Plays but has no scramble ability. Level 10 is beatable but not interesting until content sprint.

### Kadiliman (stub)

Same shape as Superintendent stub. Framework already supports the real design (3 era-themed phases with 6/6/5 chars), but the asset is a 1-phase placeholder until content is authored.

## 12. Testing Strategy

### 12.1 Edit-mode unit tests (`Assets/Tests/EditMode/BossControllerTests.cs`)

`BossController` is a MonoBehaviour but its state logic is testable without a scene. Drive it directly:

- `StartBoss` with a programmatically-constructed `BossConfigSO`.
- Call `TryHit` with various characters.
- Assert `CurrentPhaseIndex`, `IsTargetable`, and event firing.

Target cases:
1. Phase with 1 required char: correct draw advances; wrong draw raises `OnDrawingFailed` and stays.
2. Phase with 3 required chars: must draw all 3 in any order to clear.
3. Duplicate draw in same phase raises `OnDrawingFailed`.
4. Last phase cleared raises `OnBossDefeated` and `OnLevelComplete`.
5. Intro is not targetable.
6. Intermission is not targetable.

Fake `WaveSpawner` via a no-op stub (see §13.1 — requires extracting a spawn interface or making `SpawnWave` injectable).

### 12.2 Play-mode smoke test (`Assets/Tests/PlayMode/ElInquisidorTest.cs`)

One end-to-end test: load a test scene with a minimal `LevelConfigSO` pointing at `BossConfig_ElInquisidor`, drive input via test hooks on `BossController.TryHit`, assert the full Intro → 3 phases → Outro → `OnLevelComplete` sequence.

### 12.3 Ability component tests

`SummonWaveOnPhaseStart` and `PhaseBasedMovement` each get edit-mode tests with a fake `BossController` stub exposing events manually.

---

## 13. Risks & Cross-Feature Dependencies

**These items create work for features outside this spec. Each should become its own Jira task.**

### 13.1 🟠 `WaveSpawner` coupling — REFACTOR REQUIRED

**Problem:** `BossController` needs to spawn intermission waves. Currently the "spawn a whole `WaveConfigSO`" logic lives inline inside [WaveManager.SpawnWaveRoutine](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L150-L161). `BossController` cannot call into it without dragging `WaveManager` along.

**Fix:** Extract spawn-a-wave logic onto `WaveSpawner` itself:

```csharp
// New method on WaveSpawner
public IEnumerator SpawnWave(WaveConfigSO wave, Action onEnemySpawned = null);
```

Both `WaveManager` and `BossController` call `_spawner.SpawnWave(wave)`. ~20 lines moved, no behavior change.

**Dependency:** this refactor must land before `BossController` intermission logic can be implemented. **Create a separate SALIN ticket: "Refactor: move wave-spawn loop from WaveManager into WaveSpawner."**

### 13.2 🟠 `ActiveEnemyTracker` — shared source of truth

**Problem:** `WaveManager` currently tracks `_activeEnemyCount` locally ([WaveManager.cs:18](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L18)). `BossController` needs the same count for intermission completion. Two local counters would drift.

**Fix:** Both systems must read from [ActiveEnemyTracker](../../../Assets/Scripts/Gameplay/) (per [CLAUDE.md](../../../CLAUDE.md) this class exists; verify during implementation). If it doesn't yet expose a public count, add one.

**Dependency:** minor. **Create a SALIN ticket if `ActiveEnemyTracker` needs a public `ActiveCount` / `IsClear` API.** Verify first — may already exist.

### 13.3 🟠 `CombatResolver` boss-routing hook

**Problem:** `CombatResolver` needs a way to find the active `BossController`. Scanning the scene per draw is slow and fragile.

**Fix:** `GameManager.CurrentBoss` property, set in `BossController.StartBoss`, cleared on outro. `CombatResolver` reads it in one place.

**Dependency:** small edit to [GameManager](../../../Assets/Scripts/Core/) — add one property. **Create a SALIN ticket: "GameManager: add CurrentBoss reference for boss hit routing."**

### 13.4 🟠 Pause during boss fight

**Problem:** The existing pause system (`OnGamePaused` / `OnGameResumed`) must halt all `BossController` coroutines (phase timers, intermission waits, ability timers).

**Fix:** `BossController` subscribes to pause events in `OnEnable` and stops/restarts coroutines accordingly. Ability components do the same.

**Dependency:** none external. **Include as a line item in the `BossController` implementation ticket.** Watch for it in code review — it's easy to forget until QA catches it.

### 13.5 🟡 AOE interaction with boss label icon row

**Problem:** The upcoming AOE mechanic ("3+ enemies with same character → draw once → AOE") operates on non-boss enemies via closest-match. Boss is excluded from AOE by construction (§7). **No conflict in logic**, but **visually** the icon row must not be obscured by AOE effect particles.

**Dependency:** note for AOE VFX ticket: "Reserve UI layer above boss sprite; do not spawn AOE particles in that region when boss is on screen."

### 13.6 🟡 Icon row asset pipeline

**Problem:** The icon row needs pre-rendered icons for all 17 Baybayin characters, matching the character SO list.

**Fix:** [BaybayinCharacterSO](../../../Assets/Scripts/Data/) must already have an `icon: Sprite` field or equivalent. If not, add one and author the sprites.

**Dependency:** **Create a SALIN ticket: "BaybayinCharacterSO: add icon Sprite field + author 17 icons."** Verify during implementation whether icons already exist.

### 13.7 🟢 Stubs for Superintendent / Kadiliman

**Status:** not a blocker. Config-only placeholder assets ship with this spec. Real content authored in later sprints. Framework supports both full designs — no code changes needed when content lands.

---

## 14. Acceptance Criteria

1. ✅ `BossConfigSO` exists with `phases`, `intro/outroDuration`, identity fields.
2. ✅ `BossPhase.requiredCharacters` is a list; phase clears when all drawn once (any order).
3. ✅ `LevelConfigSO.bossConfig` added; `WaveManager.Start` branches on it.
4. ✅ `BossController` implements Intro → PhaseActive → Intermission → Outro state machine.
5. ✅ Boss is untargetable during Intro, Intermission, and Outro.
6. ✅ `CombatResolver` routes matching-required-character draws to boss, bypassing closest-match and AOE.
7. ✅ Drawing the wrong character (or a duplicate in-phase) raises `EventBus.OnDrawingFailed`.
8. ✅ Last phase cleared raises `EventBus.OnBossDefeated` and (after outro) `EventBus.OnLevelComplete`.
9. ✅ El Inquisidor plays as a full 3-phase encounter with summon ability and 2 intermission waves.
10. ✅ Superintendent and Kadiliman have config-only placeholder assets that load and are beatable.
11. ✅ Boss label icon row renders, follows the boss, and greys out drawn characters. Fits 360dp portrait at 6 icons.
12. ✅ Edit-mode unit tests cover the `BossController` state machine cases listed in §12.1.
13. ✅ Play-mode smoke test covers El Inquisidor end-to-end.
14. ✅ Pause events halt boss coroutines.

## 15. References

- [GDD §4.3 — Enemies and Bosses](../../capstone/GDD.md)
- [TDD §3.2 — Wave Management](../../capstone/TDD.md)
- [04_Gameplay_Systems.md](../../system/04_Gameplay_Systems.md)
- [CLAUDE.md](../../../CLAUDE.md)
- [EventBus.cs](../../../Assets/Scripts/Core/EventBus.cs)
- [WaveManager.cs](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs)
- [LevelConfigSO.cs](../../../Assets/Scripts/Data/LevelConfigSO.cs)
