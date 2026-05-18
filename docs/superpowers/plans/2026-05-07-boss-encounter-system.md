# Boss Encounter System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a reusable boss encounter framework — a phase state machine, dependency-injected ability components, and hit routing through a `BossController` — together with the fully playable Level 5 boss (El Inquisidor, 3 phases + 2 intermissions + summon ability). Levels 10 and 15 ship with config-only stubs (1-phase placeholders) that are beatable but not yet content-rich.

**Architecture:** A boss is an `Enemy` (subclass `BossEnemy` for the `IsBoss=true` override and the `TakeDamage` no-op) co-located with a single `BossController` MonoBehaviour on one prefab. `BossController` owns a state machine over `BossPhase`. Boss-specific behaviours (movement pattern, summon adds, label scrambling) live as small ability MonoBehaviours that subscribe to `EventBus` boss events; they never touch `BossController` internals. `CombatResolver` gets one new top-of-method boss-route check that hands draws to `BossController.TryRouteDraw` before AOE/closest-match. `BossConfigSO` is rewritten from a placeholder to a phase-list schema; `LevelConfigSO.isBossLevel` is removed (truth value becomes `bossConfig != null`).

**Tech Stack:** Unity 6 LTS (6000.3.9f1), C# 9.0, NUnit (Unity Test Framework 1.6.0), `UnityEngine.Pool`. No new packages.

**Ticket:** SALIN-68. Branch is already on `feature/SALIN-68-boss-encounter`. Use `SALIN-68` in commit messages.

---

## Acceptance criteria → section map

This map points each acceptance criterion from §14 of the spec at the section that satisfies it.

| AC | Where it is satisfied |
|---|---|
| AC-1 `BossConfigSO` exists with `phases`, intro/outro durations, identity fields, and `bossEnemyData`; old fields removed | [§1.1](#11-rewrite-bossconfigsocs--phase-schema) + [§1.2](#12-add-bossphase-and-bossmovementpattern) |
| AC-2 `BossPhase.requiredCharacters` is a list; phase clears when all drawn once (any order) | [§1.2](#12-add-bossphase-and-bossmovementpattern) + [§5.5](#55-tryroutedraw-and-phase-advance) + [§10.2](#102-test-2--phase-with-3-required-chars-clears-when-all-drawn-any-order) test |
| AC-3 `LevelConfigSO.isBossLevel` removed; `WaveManager.RunAllWavesRoutine` branches on `bossConfig != null` | [§1.3](#13-update-levelconfigsocs--remove-isbosslevel) + [§7.1](#71-replace-the-isbosslevel-branch) |
| AC-4 `BossController` implements Intro → PhaseActive → Intermission → Outro state machine | [§5](#task-5-code--bosscontroller-state-machine) |
| AC-5 `IsTargetable` false during Intro/Intermission/Outro; verified by test | [§5.3](#53-state-enum-and-istargetable) + [§10.5](#105-test-5--intro-istargetable-is-false) and [§10.6](#106-test-6--intermission-istargetable-is-false) tests |
| AC-6 `CombatResolver` calls `TryRouteDraw` before AOE and closest-match; `Hit`/`Duplicate` short-circuit; `NotRouted` falls through | [§8](#task-8-code--combatresolver-boss-route) |
| AC-7 Wrong character draws fall through to closest-match/AOE on adds; duplicate required draws raise `OnDrawingFailed` and are consumed | [§5.5](#55-tryroutedraw-and-phase-advance) + [§10.3](#103-test-3--duplicate-required-draw-raises-ondrawingfailed-and-is-consumed) test |
| AC-8 Last phase cleared raises `OnBossDefeated` and (after outro) `OnLevelComplete` | [§5.6](#56-state-transition-coroutine) + [§10.4](#104-test-4--last-phase-cleared-raises-onbossdefeated-and-onlevelcomplete) test |
| AC-9 El Inquisidor plays as a full 3-phase encounter with summon ability and 2 intermission waves | [§13.2](#132-create-bossconfig_elinquisidorasset) + [§13.5](#135-create-intermission-wave-configs) + [§14.1](#141-create-enemy-boss_elinquisidorprefab) + [§17.2](#172-verify-el-inquisidor-end-to-end) playtest |
| AC-10 Superintendent and Kadiliman have config-only placeholder assets that load and are beatable | [§13.3](#133-create-bossconfig_superintendentasset-stub) + [§13.4](#134-reshape-bossconfig_kadilimanasset-to-the-new-schema-stub) |
| AC-11 Boss label icon row renders, follows the boss, greys out drawn characters, hides during intermission, fits 360dp | [§9](#task-9-code--bosslabeliconrow) + [§16](#task-16-manual-unity--scene-wiring-for-bosslabeliconrow) |
| AC-12 EditMode unit tests cover state machine cases | [§10](#task-10-tests--editmode-bosscontrollertests) |
| AC-13 PlayMode smoke test covers El Inquisidor end-to-end | [§11](#task-11-tests--playmode-elinquisidortest-smoke) |
| AC-14 All boss coroutines use `WaitForSeconds`; no `WaitForSecondsRealtime` | [§5.6](#56-state-transition-coroutine) + [§6.1](#61-create-phasebasedmovementcs) + [§6.2](#62-create-summonwaveonphasestartcs) (each step asserts) |
| AC-15 `Enemy.IsBoss` returns true on the boss; AOE excludes it; `FindClosestToBase` never returns it | [§4.1](#41-create-bossenemycs--enemy-subclass) + verified by Phase 0 (`CombatResolver` filters `IsBoss` already) |
| AC-16 `GameManager.CurrentBoss` set in `StartBoss`; cleared in `OnDisable`; not set in `OnEnable` | [§3](#task-3-code--gamemanagercurrentboss) + [§5.4](#54-startboss-and-currentboss-lifecycle) |
| AC-17 No hearts lost from wrong/duplicate boss draws | Falls out of [§5.5](#55-tryroutedraw-and-phase-advance) + [§8](#task-8-code--combatresolver-boss-route) — wrong falls through, duplicate consumed; verified by [§17.2](#172-verify-el-inquisidor-end-to-end) playtest |
| AC-18 `StartBoss(config, spawner)` accepts spawner explicitly; tests inject a stub | [§5.4](#54-startboss-and-currentboss-lifecycle) + [§10.7](#107-test-7--intermission-spawning-uses-injected-spawner) test |
| AC-19 Cross-system events flow through EventBus; only `OnDrawnThisPhaseChanged` is local | [§2](#task-2-code--eventbus-boss-events) + [§5.2](#52-public-api) |

---

## Phase 0 audit snapshot

Recorded against the working tree on `feature/SALIN-68-boss-encounter` (commit `4c0a56d`). Re-run the audit if the branch has been rebased — line numbers may shift.

| Symbol | Location | Status |
|---|---|---|
| `BossConfigSO` (placeholder) | [Assets/Scripts/Data/BossConfigSO.cs](../../../Assets/Scripts/Data/BossConfigSO.cs), 28 lines | Has `bossName`, `bossID`, `maxHealth=10`, `moveSpeed=0.5`, `phaseCount=1`, `bossSprite`, `animatorController`, `bossEnemyData`. **Rewritten end-to-end in §1.1.** Old fields removed. |
| `LevelConfigSO.isBossLevel` | [Assets/Scripts/Data/LevelConfigSO.cs:23](../../../Assets/Scripts/Data/LevelConfigSO.cs#L23) | bool field. **Removed in §1.3.** Truth value becomes `bossConfig != null`. |
| `LevelConfigSO.bossConfig` | [Assets/Scripts/Data/LevelConfigSO.cs:26](../../../Assets/Scripts/Data/LevelConfigSO.cs#L26) | Retained as-is. |
| `WaveManager.RunAllWavesRoutine` boss branch | [Assets/Scripts/Gameplay/Wave/WaveManager.cs:275-279](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L275-L279) | Currently: `if (_levelConfig.isBossLevel && _levelConfig.bossConfig != null) yield return RunBossEncounter`. **Edited in §7.1** to drop `isBossLevel`. |
| `WaveManager.RunBossEncounter` (placeholder) | [WaveManager.cs:364-418](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L364-L418) | Spawns one Enemy with a random allowed character, waits `IsClear`, raises `OnBossDefeated`/`OnLevelComplete`. **Replaced wholesale in §7.2.** |
| `WaveManager._spawner` | [WaveManager.cs:16](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L16) | `[SerializeField] private WaveSpawner _spawner;` — passed into `BossController.StartBoss`. |
| `WaveSpawner.SpawnEnemy(EnemyDataSO)` | [WaveSpawner.cs:29](../../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs#L29) | Exists. Returns `Enemy`. **Used as-is in §7.2.** |
| `WaveSpawner.SpawnWave(WaveConfigSO, Action, int)` | [WaveSpawner.cs:99](../../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs#L99) | Exists. **Called from `SummonWaveOnPhaseStart` in §6.2 and from intermission flow in §5.6.** |
| `CombatResolver.HandleCharacterRecognized` | [CombatResolver.cs:22](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs#L22) | Boss-exclusion filters at lines 40 (real-match count) and 58 (burst targets) already in place. **Boss-route block prepended in §8.1.** |
| `Enemy.IsBoss` virtual | [Enemy.cs:45](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L45) | `public virtual bool IsBoss => false;` — overridden by `BossEnemy` in §4.1. |
| `Enemy.Character` | [Enemy.cs:39](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L39) | Returns `_runtimeCharacter ?? _data?.assignedCharacter`. Boss `EnemyDataSO` has `assignedCharacter = null` (§13.1) and the boss is never assigned a runtime character, so `Character` is null on the boss — already invisible to `FindClosestToBase` (which short-circuits on null). |
| `Enemy.TakeDamage(int)` | [Enemy.cs:167-189](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L167-L189) | Exists. **Overridden as a no-op + warning in `BossEnemy` (§4.1)** so future direct-damage code can't bypass the phase gate. |
| `Enemy.AssignCharacter` | [Enemy.cs:65](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L65) | Exists; not used for the boss. |
| `Enemy.Awake` | [Enemy.cs:47-57](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L47-L57) | Caches `_mover`/`_renderer`. **No edit required** — `BossController` is fetched via `GetComponent` from `WaveManager` after spawn, not by `Enemy`. |
| `Enemy.Initialize(EnemyDataSO)` | [Enemy.cs:72-134](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L72-L134) | Validates `data != null`, `_mover != null`, `data.maxHealth > 0`. The boss `EnemyDataSO` (§13.1) sets `maxHealth = 1` (a non-zero placeholder so `Initialize` does not abort) — phase index is the real HP, `TakeDamage` no-op stops `_currentHealth` from ever changing. |
| `EnemyPool.Get(EnemyDataSO)` | [EnemyPool.cs:90](../../../Assets/Scripts/Gameplay/Enemy/EnemyPool.cs#L90) | Resolves pool by `data.enemyID`. Boss prefab must be registered with its own `enemyID` (§14.2) so it does not fall back to the default pool. |
| `ActiveEnemyTracker.IsClear` / `ActiveCount` | [ActiveEnemyTracker.cs:15-24](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs#L15-L24) | Both exist. **Used by §5.6** to wait for intermission adds to clear. |
| `ActiveEnemyTracker.FindClosestToBase(string)` | [ActiveEnemyTracker.cs:52](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs#L52) | Skips enemies with `Character == null`. Boss is invisible to it by construction. |
| `GameManager.CurrentLevel` | [GameManager.cs:9](../../../Assets/Scripts/Core/GameManager.cs#L9) | Exists. **`CurrentBoss` property added next to it in §3.** |
| `GameManager.PauseGame` | [GameManager.cs:75-81](../../../Assets/Scripts/Core/GameManager.cs#L75-L81) | Sets `Time.timeScale = 0`. All boss coroutines must use `WaitForSeconds` (scaled) — enforced by code review and called out on each ability-component header. |
| `EventBus` boss events | [EventBus.cs:43](../../../Assets/Scripts/Core/EventBus.cs#L43) | Only `OnBossDefeated` exists today. **§2 adds five more** (`OnBossStarted`, `OnBossPhaseStarted`, `OnBossPhaseCleared`, `OnBossIntermissionStarted`, `OnBossIntermissionCleared`). |
| `EventBus.OnDrawingFailed` / `OnLevelComplete` | [EventBus.cs:13,20](../../../Assets/Scripts/Core/EventBus.cs#L13) | Exist; reused unchanged. |
| `BaybayinCharacterSO.displaySprite` / `characterID` | [BaybayinCharacterSO.cs:7,12](../../../Assets/Scripts/Data/BaybayinCharacterSO.cs#L7) | Both exist. `displaySprite` is the icon-row source (§9). |
| `ActiveEnemyTrackerTests` fixture pattern | [Assets/Tests/Editor/Gameplay/ActiveEnemyTrackerTests.cs](../../../Assets/Tests/Editor/Gameplay/ActiveEnemyTrackerTests.cs) | Reference for `BossControllerTests` — `ScriptableObject.CreateInstance`, reflection for `Singleton.Instance`, `[TearDown]` cleanup. **Reused in §10.** |
| `BossConfig_Kadiliman.asset` | [Assets/ScriptableObjects/BossConfig_Kadiliman.asset](../../../Assets/ScriptableObjects/BossConfig_Kadiliman.asset) | Existing placeholder under the OLD schema. **Reshaped in §13.4** to fit the new schema as a 1-phase stub. |
| `EnemyData_Boss.asset` | [Assets/ScriptableObjects/EnemyData_Boss.asset](../../../Assets/ScriptableObjects/EnemyData_Boss.asset) | Has `assignedCharacter` set, `maxHealth = 10`. **Replaced** by per-boss EnemyDataSOs (§13.1) — left in place as legacy until SALIN-68 ships, then optionally deleted. |
| `Level5_Config.asset` | [Assets/ScriptableObjects/Levels/Level5_Config.asset](../../../Assets/ScriptableObjects/Levels/Level5_Config.asset) | Currently empty waves, no boss reference. **Wired in §15.1** to point at `BossConfig_ElInquisidor`. |
| Existing enemy prefabs (template) | [Assets/Prefabs/Enemies/](../../../Assets/Prefabs/Enemies/) | 9 prefabs, none labelled "Boss". **`[Enemy] Boss_ElInquisidor.prefab` created in §14.1** by duplicating an existing variant prefab. |

> **NOTE on `BossConfig_Kadiliman.asset` schema migration** When `BossConfigSO.cs` is rewritten in §1.1 with a different field shape, Unity will silently drop the YAML's stale fields (`maxHealth`, `phaseCount`, etc.) on the next asset save and write only the new fields. The `bossEnemyData` and identity fields (`bossName`, `bossID`) survive because their names are unchanged. Re-saving the asset (§13.4) is what writes the new fields explicitly.

---

## Task 1: Code — Data layer (`BossConfigSO`, `BossPhase`, `LevelConfigSO`)

Rewrite `BossConfigSO` end-to-end (the placeholder schema is incompatible with the phase-list design). Add the `BossPhase` and `BossMovementPattern` types, then drop `LevelConfigSO.isBossLevel`.

### §1.1 Rewrite `BossConfigSO.cs` — phase schema

**Files:**
- Modify: [Assets/Scripts/Data/BossConfigSO.cs](../../../Assets/Scripts/Data/BossConfigSO.cs)

- [ ] **Step 1.1.1: Open the file**

  Project window → `Assets/Scripts/Data/BossConfigSO.cs` → double-click. Confirm the current shape matches the audit (placeholder fields `maxHealth`, `phaseCount`, `bossSprite`, etc.).

- [ ] **Step 1.1.2: Replace the file body**

  Select all (Ctrl+A), delete, paste:

  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  // Configuration for a single boss encounter. Phase count is the source of
  // truth for the boss's effective HP — there is no separate maxHealth field.
  [CreateAssetMenu(fileName = "BossConfig", menuName = "Salinlahi/Boss Config")]
  public class BossConfigSO : ScriptableObject
  {
      [Header("Identity")]
      public string bossName;
      public string bossID;
      [Tooltip("Optional HUD/portrait sprite, distinct from the in-world Enemy sprite.")]
      public Sprite bossSprite;

      [Header("Spawning")]
      [Tooltip("EnemyDataSO defining the boss's prefab, base sprite, animator, and collision behavior. Its assignedCharacter MUST be null so the boss is invisible to FindClosestToBase.")]
      public EnemyDataSO bossEnemyData;

      [Header("Phases")]
      [Tooltip("Ordered. Phase count = boss's effective HP. Last phase clear ends the encounter.")]
      public List<BossPhase> phases;

      [Header("Intro / Outro")]
      [Tooltip("Seconds the boss is invulnerable on entry while the intro animation plays.")]
      public float introDuration = 2.0f;
      [Tooltip("Seconds before OnLevelComplete fires after the last phase is cleared.")]
      public float outroDuration = 2.5f;
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 1.1.3: Verify compile (will FAIL — `BossPhase` does not exist yet)**

  Switch to Unity. The Console will show `CS0246: 'BossPhase' could not be found`. This is expected — proceed to §1.2 to define it.

### §1.2 Add `BossPhase` and `BossMovementPattern`

**Files:**
- Create: [Assets/Scripts/Data/BossPhase.cs](../../../Assets/Scripts/Data/BossPhase.cs)

`BossPhase` is a serializable plain class (not a ScriptableObject) so it embeds inside `BossConfigSO.phases` directly in the Inspector. Keeping it in its own file makes it discoverable and lets the test fixture reference it without pulling in `BossConfigSO`.

- [ ] **Step 1.2.1: Create the file**

  Project window → `Assets/Scripts/Data/` → right-click → `Create → Scripting → MonoBehaviour Script`. Type `BossPhase` → Enter. Open the file.

- [ ] **Step 1.2.2: Replace the file body**

  Select all, delete, paste:

  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  public enum BossMovementPattern { Hover, Pace, Teleport }

  // Single phase definition embedded in BossConfigSO.phases. Phase clears when
  // every requiredCharacters entry has been drawn exactly once, in any order.
  [System.Serializable]
  public class BossPhase
  {
      [Header("Gate")]
      [Tooltip("Characters the player must draw (any order, each once) to clear this phase.")]
      public List<BaybayinCharacterSO> requiredCharacters;

      [Header("Movement")]
      public BossMovementPattern movementPattern;
      [Tooltip("Movement speed in world units per second. 0 = stationary (Hover) or teleport-only (Teleport).")]
      public float movementSpeed;

      [Header("Intermission (after this phase clears)")]
      [Tooltip("Mini-wave spawned before the next phase begins. Null = no intermission.")]
      public WaveConfigSO intermissionWave;
      [Tooltip("Seconds to wait after the intermission wave clears before the next phase starts.")]
      public float postIntermissionDelay;
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 1.2.3: Verify compile**

  Switch to Unity, wait for `Compiling...` to clear. Console should be clean. If you still see errors, check that `WaveConfigSO` and `BaybayinCharacterSO` are in the same assembly (they are — `Assembly-CSharp`).

- [ ] **Step 1.2.4: Sanity check on the existing Kadiliman asset**

  Project window → click `Assets/ScriptableObjects/BossConfig_Kadiliman.asset`. Inspector shows the new shape: `Boss Name`, `Boss ID`, `Boss Sprite`, `Boss Enemy Data`, `Phases (Size 0)`, `Intro Duration`, `Outro Duration`. The old placeholder fields are gone. (The on-disk YAML still has the old fields, but Unity ignores them.) **Do not save the asset yet** — §13.4 reshapes it explicitly.

### §1.3 Update `LevelConfigSO.cs` — remove `isBossLevel`

**Files:**
- Modify: [Assets/Scripts/Data/LevelConfigSO.cs](../../../Assets/Scripts/Data/LevelConfigSO.cs)

- [ ] **Step 1.3.1: Open the file**

  Project window → `Assets/Scripts/Data/LevelConfigSO.cs` → double-click.

- [ ] **Step 1.3.2: Delete the `isBossLevel` block**

  Use Ctrl+F to jump to `isBossLevel` (line 23). Delete these three lines:

  ```csharp
      [Tooltip("True if this level is a boss encounter")]
      public bool isBossLevel;

  ```

  And update the tooltip on `bossConfig` from "only used if isBossLevel is true" to:

  ```csharp
      [Tooltip("If set, this level is a boss encounter. Waves list is ignored.")]
      public BossConfigSO bossConfig;
  ```

  The final `[Header("Boss")]` block should now read:

  ```csharp
      [Header("Boss")]
      [Tooltip("If set, this level is a boss encounter. Waves list is ignored.")]
      public BossConfigSO bossConfig;
  ```

  Save (Ctrl+S).

- [ ] **Step 1.3.3: Verify compile (will FAIL on WaveManager.cs)**

  Switch to Unity. Expected error: `CS1061: 'LevelConfigSO' does not contain a definition for 'isBossLevel'` at `WaveManager.cs:275`. Will be fixed in §7.1. Leave the error in place for now — Unity may refuse to enter Play mode but inspectors still work, which is fine for the rest of Task 1 / Task 2 / Task 3.

- [ ] **Step 1.3.4: Commit**

  ```bash
  git add Assets/Scripts/Data/BossConfigSO.cs Assets/Scripts/Data/BossPhase.cs Assets/Scripts/Data/LevelConfigSO.cs
  git commit -m "feat(data): SALIN-68 introduce phase-list BossConfigSO and BossPhase"
  ```

  > **NOTE on the WaveManager compile error after this commit** The repo is intentionally left in a broken-build state between §1.3 and §7.1. This is acceptable because Tasks 2–6 do not require Play mode and do not call into the broken branch. If you need a green build between commits, defer §1.3 until §7 is ready and bundle them.

---

## Task 2: Code — `EventBus` boss events

The spec calls for five new boss events (six counting the existing `OnBossDefeated`). All cross-system boss notifications go through `EventBus`; only `OnDrawnThisPhaseChanged` is a per-controller local event (defined in §5.2).

### §2.1 Add the events

**Files:**
- Modify: [Assets/Scripts/Core/EventBus.cs](../../../Assets/Scripts/Core/EventBus.cs)

- [ ] **Step 2.1.1: Open the file and locate the boss block**

  Open `Assets/Scripts/Core/EventBus.cs`. Use Ctrl+F to find `// -- Boss Events --` (line 42). The current block reads:

  ```csharp
      // -- Boss Events --
      public static event Action OnBossDefeated;
  ```

- [ ] **Step 2.1.2: Replace the boss-events block**

  Replace the two-line block with:

  ```csharp
      // -- Boss Events --
      public static event Action<BossConfigSO> OnBossStarted;
      public static event Action<int> OnBossPhaseStarted;        // phase index (0-based)
      public static event Action<int> OnBossPhaseCleared;        // phase index (0-based)
      public static event Action OnBossIntermissionStarted;
      public static event Action OnBossIntermissionCleared;
      public static event Action OnBossDefeated;
  ```

  Save (Ctrl+S).

- [ ] **Step 2.1.3: Add the corresponding raisers**

  Use Ctrl+F to find `RaiseBossDefeated` (line 70). The current line reads:

  ```csharp
      public static void RaiseBossDefeated() => OnBossDefeated?.Invoke();
  ```

  Replace with:

  ```csharp
      public static void RaiseBossStarted(BossConfigSO config) => OnBossStarted?.Invoke(config);
      public static void RaiseBossPhaseStarted(int phaseIndex) => OnBossPhaseStarted?.Invoke(phaseIndex);
      public static void RaiseBossPhaseCleared(int phaseIndex) => OnBossPhaseCleared?.Invoke(phaseIndex);
      public static void RaiseBossIntermissionStarted() => OnBossIntermissionStarted?.Invoke();
      public static void RaiseBossIntermissionCleared() => OnBossIntermissionCleared?.Invoke();
      public static void RaiseBossDefeated() => OnBossDefeated?.Invoke();
  ```

  Save (Ctrl+S).

- [ ] **Step 2.1.4: Verify compile**

  Switch to Unity. `EventBus.cs` itself should compile cleanly. The pre-existing `WaveManager.cs:275` error from §1.3 is still present.

- [ ] **Step 2.1.5: Commit**

  ```bash
  git add Assets/Scripts/Core/EventBus.cs
  git commit -m "feat(events): SALIN-68 add boss phase / intermission EventBus events"
  ```

---

## Task 3: Code — `GameManager.CurrentBoss`

`CombatResolver` needs a way to find the currently active `BossController` without scene-scanning every recognition. The spec mandates this is set inside `BossController.StartBoss` (not `OnEnable` — the controller has no config at that moment).

### §3.1 Add the property and internal setter

**Files:**
- Modify: [Assets/Scripts/Core/GameManager.cs](../../../Assets/Scripts/Core/GameManager.cs)

- [ ] **Step 3.1.1: Open and locate the property block**

  Open `Assets/Scripts/Core/GameManager.cs`. Use Ctrl+F to find `public LevelConfigSO CurrentLevel` (line 9). The current block (lines 8–13) reads:

  ```csharp
      public GameState CurrentState { get; private set; } = GameState.Idle;
      public LevelConfigSO CurrentLevel { get; private set; }
      public int LastDefeatHearts { get; private set; }

      public bool AcceptsDrawingInput =>
          CurrentState == GameState.Playing || CurrentState == GameState.Practicing;
  ```

- [ ] **Step 3.1.2: Add `CurrentBoss` and the internal setter**

  Position the cursor at the end of `public int LastDefeatHearts { get; private set; }` (line 10). Press Enter and add:

  ```csharp
      public BossController CurrentBoss { get; private set; }
      internal void SetCurrentBoss(BossController boss) => CurrentBoss = boss;
  ```

  The block should now read:

  ```csharp
      public GameState CurrentState { get; private set; } = GameState.Idle;
      public LevelConfigSO CurrentLevel { get; private set; }
      public int LastDefeatHearts { get; private set; }
      public BossController CurrentBoss { get; private set; }
      internal void SetCurrentBoss(BossController boss) => CurrentBoss = boss;
  ```

  Save (Ctrl+S).

- [ ] **Step 3.1.3: Verify compile (will FAIL — `BossController` not yet defined)**

  Switch to Unity. Expected error: `CS0246: 'BossController' could not be found`. Will be fixed when §5.1 lands.

  > **NOTE** Resist the temptation to defer this step until after §5.1. Adding the property here keeps the GameManager change in its own commit (one concept per commit) and makes the dependency direction explicit (`BossController` calls `GameManager`, not the other way around). The transient compile error is intentional.

- [ ] **Step 3.1.4: Commit**

  ```bash
  git add Assets/Scripts/Core/GameManager.cs
  git commit -m "feat(core): SALIN-68 add GameManager.CurrentBoss property"
  ```

---

## Task 4: Code — `BossEnemy` subclass

A boss is an `Enemy` subclass that overrides `IsBoss` (so AOE/closest-match keep ignoring it) and overrides `TakeDamage` as a no-op (so a future direct-damage code path cannot bypass the phase gate). The boss prefab uses `BossEnemy` as its `MonoBehaviour` script instead of `Enemy`.

### §4.1 Create `BossEnemy.cs` — Enemy subclass

**Files:**
- Create: [Assets/Scripts/Gameplay/Enemy/BossEnemy.cs](../../../Assets/Scripts/Gameplay/Enemy/BossEnemy.cs)

- [ ] **Step 4.1.1: Make `Enemy.TakeDamage` virtual**

  Open `Assets/Scripts/Gameplay/Enemy/Enemy.cs`. Use Ctrl+F to find `public void TakeDamage(int amount)` (line 167). Change:

  ```csharp
      public void TakeDamage(int amount)
  ```

  to:

  ```csharp
      public virtual void TakeDamage(int amount)
  ```

  Save (Ctrl+S).

- [ ] **Step 4.1.2: Create the script file**

  Project window → `Assets/Scripts/Gameplay/Enemy/` → right-click empty space → `Create → Scripting → MonoBehaviour Script`. Type `BossEnemy` → Enter. Open the file.

- [ ] **Step 4.1.3: Replace the body**

  Select all, delete, paste:

  ```csharp
  using UnityEngine;

  // Boss-specific Enemy subclass. Two responsibilities only:
  //   1. IsBoss returns true so CombatResolver excludes the boss from AOE
  //      and closest-match.
  //   2. TakeDamage no-ops with a warning, so a future direct-damage code path
  //      (projectile, contact damage, etc.) cannot bypass BossController's
  //      phase gate. All boss damage flows through BossController.TryRouteDraw.
  // Co-located with BossController on the boss prefab.
  public class BossEnemy : Enemy
  {
      public override bool IsBoss => true;

      public override void TakeDamage(int amount)
      {
          DebugLogger.LogWarning(
              $"BossEnemy.TakeDamage called with amount={amount}. "
              + "Boss damage is gated by BossController.TryRouteDraw — "
              + "this call has been ignored. Investigate the caller.");
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 4.1.4: Verify compile**

  Switch to Unity. `BossEnemy.cs` should compile. The pre-existing errors from §1.3 (`isBossLevel`) and §3.1 (`BossController`) remain.

- [ ] **Step 4.1.5: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Enemy/Enemy.cs Assets/Scripts/Gameplay/Enemy/BossEnemy.cs
  git commit -m "feat(enemy): SALIN-68 add BossEnemy subclass with damage gate"
  ```

---

## Task 5: Code — `BossController` state machine

The heart of the framework. One MonoBehaviour, one state machine, one source of truth for "is the boss currently targetable, what characters does the player need to draw, what phase are we in." It owns the phase advance, the intermission spawn (via injected `WaveSpawner`), and the intro/outro timing. **Subscribes to nothing** — outside systems subscribe to it (via `EventBus` for cross-system events, or via `OnDrawnThisPhaseChanged` for the icon-row UI).

### §5.1 Create the file skeleton

**Files:**
- Create: [Assets/Scripts/Gameplay/Boss/BossController.cs](../../../Assets/Scripts/Gameplay/Boss/BossController.cs)

- [ ] **Step 5.1.1: Create the `Boss` folder and the file**

  Project window → `Assets/Scripts/Gameplay/` → right-click → `Create → Folder` → name it `Boss`. Inside `Boss/` → right-click → `Create → Scripting → MonoBehaviour Script`. Type `BossController` → Enter. Open the file.

- [ ] **Step 5.1.2: Add namespace using-block and skeleton**

  Replace the entire file body with the §5.2 → §5.7 content combined into one paste. The combined paste is given in the next step.

### §5.2 Public API

### §5.3 State enum and `IsTargetable`

### §5.4 `StartBoss` and `CurrentBoss` lifecycle

### §5.5 `TryRouteDraw` and phase advance

### §5.6 State-transition coroutine

### §5.7 `OnDisable` cleanup

- [ ] **Step 5.2.1: Paste the full body**

  These six sub-sections (§5.2–§5.7) compose into one file. Paste this body verbatim:

  ```csharp
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using UnityEngine;

  // Single MonoBehaviour state machine that drives a boss encounter.
  // Co-located with BossEnemy on the boss prefab.
  //
  // Lifecycle:
  //   WaveManager.RunBossEncounter spawns the boss Enemy via WaveSpawner,
  //   gets BossController via GetComponent, and calls StartBoss(config, spawner).
  //   StartBoss is the lifecycle entry point — OnEnable does NOT begin the
  //   encounter, because at OnEnable the controller has no config yet.
  //
  // States: Intro -> PhaseActive -> [PhaseClearedIntermission ->] PhaseActive -> ... -> Outro -> Defeated
  //
  // Pause: All coroutines use WaitForSeconds (scaled time). When GameManager
  // calls Time.timeScale = 0 the encounter halts automatically.
  // DO NOT use WaitForSecondsRealtime in this subsystem.
  [RequireComponent(typeof(BossEnemy))]
  public class BossController : MonoBehaviour
  {
      private enum State { Idle, Intro, PhaseActive, PhaseClearedIntermission, Outro, Defeated }

      public BossConfigSO Config { get; private set; }
      public BossPhase CurrentPhase { get; private set; }
      public int CurrentPhaseIndex { get; private set; } = -1;
      public bool IsTargetable => _state == State.PhaseActive;
      public bool IsDefeated { get; private set; }
      public IReadOnlyList<BaybayinCharacterSO> RequiredCharacters =>
          CurrentPhase != null ? CurrentPhase.requiredCharacters : null;
      public IReadOnlyCollection<BaybayinCharacterSO> DrawnThisPhase => _drawnThisPhase;

      // Local event — fired on every successful Hit. UI listens for per-icon
      // grey-out. Kept local because subscribers need the controller-instance
      // handle to read DrawnThisPhase / RequiredCharacters mid-phase.
      public event Action OnDrawnThisPhaseChanged;

      private State _state = State.Idle;
      private WaveSpawner _spawner;
      private readonly HashSet<BaybayinCharacterSO> _drawnThisPhase = new();
      private Coroutine _stateRoutine;

      // ---- Lifecycle ----

      public void StartBoss(BossConfigSO config, WaveSpawner spawner)
      {
          if (config == null)
          {
              DebugLogger.LogError("BossController.StartBoss: config is null. Aborting.");
              return;
          }
          if (spawner == null)
          {
              DebugLogger.LogError("BossController.StartBoss: spawner is null. Aborting.");
              return;
          }
          if (config.phases == null || config.phases.Count == 0)
          {
              DebugLogger.LogError("BossController.StartBoss: config has no phases. Aborting.");
              return;
          }

          Config = config;
          _spawner = spawner;
          IsDefeated = false;
          CurrentPhaseIndex = -1;
          CurrentPhase = null;
          _drawnThisPhase.Clear();

          // Set CurrentBoss BEFORE raising OnBossStarted so subscribers
          // resolving GameManager.Instance.CurrentBoss in the handler see this
          // controller, not null.
          if (GameManager.Instance != null)
              GameManager.Instance.SetCurrentBoss(this);

          EventBus.RaiseBossStarted(config);

          if (_stateRoutine != null)
              StopCoroutine(_stateRoutine);
          _stateRoutine = StartCoroutine(RunEncounter());
      }

      private void OnDisable()
      {
          if (_stateRoutine != null)
          {
              StopCoroutine(_stateRoutine);
              _stateRoutine = null;
          }
          if (GameManager.Instance != null && GameManager.Instance.CurrentBoss == this)
              GameManager.Instance.SetCurrentBoss(null);
      }

      // ---- Hit routing ----

      public BossRouteResult TryRouteDraw(string characterID)
      {
          if (!IsTargetable || CurrentPhase == null)
              return BossRouteResult.NotRouted;
          if (CurrentPhase.requiredCharacters == null
              || CurrentPhase.requiredCharacters.Count == 0)
              return BossRouteResult.NotRouted;

          BaybayinCharacterSO matched = null;
          for (int i = 0; i < CurrentPhase.requiredCharacters.Count; i++)
          {
              BaybayinCharacterSO so = CurrentPhase.requiredCharacters[i];
              if (so == null) continue;
              if (so.characterID == characterID)
              {
                  matched = so;
                  break;
              }
          }

          if (matched == null)
              return BossRouteResult.NotRouted;

          if (_drawnThisPhase.Contains(matched))
          {
              EventBus.RaiseDrawingFailed();
              return BossRouteResult.Duplicate;
          }

          _drawnThisPhase.Add(matched);
          OnDrawnThisPhaseChanged?.Invoke();

          int requiredCount = 0;
          for (int i = 0; i < CurrentPhase.requiredCharacters.Count; i++)
              if (CurrentPhase.requiredCharacters[i] != null) requiredCount++;

          if (_drawnThisPhase.Count >= requiredCount)
          {
              EventBus.RaiseBossPhaseCleared(CurrentPhaseIndex);
              // The state coroutine watches _drawnThisPhase.Count vs. requiredCount
              // on each frame and advances. Hit signal already raised.
          }

          return BossRouteResult.Hit;
      }

      // ---- State coroutine ----

      private IEnumerator RunEncounter()
      {
          // Intro
          _state = State.Intro;
          yield return new WaitForSeconds(Mathf.Max(0f, Config.introDuration));

          // Phases
          for (int i = 0; i < Config.phases.Count; i++)
          {
              CurrentPhaseIndex = i;
              CurrentPhase = Config.phases[i];
              _drawnThisPhase.Clear();

              _state = State.PhaseActive;
              EventBus.RaiseBossPhaseStarted(i);

              // Wait for the phase to clear (TryRouteDraw raises BossPhaseCleared
              // when the count is met; we observe the same condition here so
              // we don't depend on the order of subscriber invocation).
              yield return new WaitUntil(() =>
                  _drawnThisPhase.Count >= CountNonNull(CurrentPhase.requiredCharacters));

              // Intermission (if configured AND this is not the final phase)
              bool isFinalPhase = (i == Config.phases.Count - 1);
              if (!isFinalPhase && CurrentPhase.intermissionWave != null)
              {
                  _state = State.PhaseClearedIntermission;
                  EventBus.RaiseBossIntermissionStarted();

                  yield return StartCoroutine(_spawner.SpawnWave(CurrentPhase.intermissionWave));

                  // Wait for adds to clear
                  yield return new WaitUntil(() =>
                  {
                      ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
                      return tracker == null || tracker.IsClear;
                  });

                  if (CurrentPhase.postIntermissionDelay > 0f)
                      yield return new WaitForSeconds(CurrentPhase.postIntermissionDelay);

                  EventBus.RaiseBossIntermissionCleared();
              }
          }

          // Outro
          _state = State.Outro;
          IsDefeated = true;
          yield return new WaitForSeconds(Mathf.Max(0f, Config.outroDuration));

          _state = State.Defeated;
          EventBus.RaiseBossDefeated();
          EventBus.RaiseLevelComplete();

          // Return the boss Enemy to the pool. ResetForPool clears _data, so the
          // next encounter's spawn re-initializes cleanly.
          BossEnemy bossEnemy = GetComponent<BossEnemy>();
          if (bossEnemy != null)
              bossEnemy.ReturnToPool();

          _stateRoutine = null;
      }

      private static int CountNonNull(List<BaybayinCharacterSO> list)
      {
          if (list == null) return 0;
          int n = 0;
          for (int i = 0; i < list.Count; i++)
              if (list[i] != null) n++;
          return n;
      }
  }

  public enum BossRouteResult
  {
      NotRouted,   // characterID not in current phase's required list — caller falls through to AOE/closest-match
      Hit,         // valid required character drawn for the first time this phase
      Duplicate    // required character already drawn this phase — consumed, raises OnDrawingFailed
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 5.2.2: Verify compile**

  Switch to Unity, wait for `Compiling...` to clear. The Console should show **only** the pre-existing `WaveManager.cs:275` error from §1.3 (which §7.1 fixes). All other errors from §3.1 and §4.1 should be gone — `BossController` and `BossRouteResult` are now defined.

  > **NOTE on the `[RequireComponent(typeof(BossEnemy))]` attribute** This guarantees the boss prefab cannot drop `BossEnemy` while keeping `BossController` — so `GetComponent<BossEnemy>()` in `RunEncounter` always succeeds. If a future boss variant needs a different `Enemy` subclass, drop the attribute and add a null guard.

  > **NOTE on `EventBus.RaiseLevelComplete()` from inside `RunEncounter`** This is intentional — the spec mandates the boss is the source of `OnLevelComplete` during boss encounters, and `WaveManager.RunBossEncounter` (§7.2) will explicitly NOT call `CompleteRun()` for boss levels.

- [ ] **Step 5.2.3: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Boss/BossController.cs
  git commit -m "feat(boss): SALIN-68 BossController state machine and TryRouteDraw"
  ```

---

## Task 6: Code — Ability components (`PhaseBasedMovement`, `SummonWaveOnPhaseStart`)

Both components are MonoBehaviours that sit on the boss prefab next to `BossController`. They never touch `BossController` internals — they read `Config.phases[i]` (or `CurrentPhase`) and subscribe to `EventBus` boss events.

### §6.1 Create `PhaseBasedMovement.cs`

**Files:**
- Create: [Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs](../../../Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs)

Reads `CurrentPhase.movementPattern` on `OnBossPhaseStarted` and drives a movement coroutine. Pulled into its own component so the state machine (§5) does not need to know about Unity transforms — keeps `BossController` testable in isolation.

- [ ] **Step 6.1.1: Create the file**

  Project window → `Assets/Scripts/Gameplay/Boss/` → right-click → `Create → Scripting → MonoBehaviour Script`. Name it `PhaseBasedMovement`. Open it.

- [ ] **Step 6.1.2: Replace the body**

  Select all, delete, paste:

  ```csharp
  using System.Collections;
  using UnityEngine;

  // Drives the boss's transform per CurrentPhase.movementPattern.
  // Subscribes to EventBus boss events; never touches BossController internals.
  // All coroutines use WaitForSeconds (scaled). Do not use WaitForSecondsRealtime.
  [RequireComponent(typeof(BossController))]
  public class PhaseBasedMovement : MonoBehaviour
  {
      [Header("Pace Pattern")]
      [Tooltip("Horizontal range (world units) the boss paces left/right around its starting X.")]
      [SerializeField] private float _paceHalfRange = 1.5f;

      [Header("Teleport Pattern")]
      [Tooltip("Seconds between teleport jumps.")]
      [SerializeField] private float _teleportInterval = 1.5f;
      [Tooltip("Horizontal range (world units) for teleport destination, around starting X.")]
      [SerializeField] private float _teleportHalfRange = 2.0f;

      private BossController _boss;
      private Vector3 _baseLocalPosition;
      private Coroutine _movementRoutine;

      private void Awake()
      {
          _boss = GetComponent<BossController>();
          _baseLocalPosition = transform.localPosition;
      }

      private void OnEnable()
      {
          EventBus.OnBossPhaseStarted += HandlePhaseStarted;
          EventBus.OnBossPhaseCleared += HandlePhaseCleared;
          EventBus.OnBossIntermissionStarted += StopMovement;
          EventBus.OnBossDefeated += StopMovement;
      }

      private void OnDisable()
      {
          EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
          EventBus.OnBossPhaseCleared -= HandlePhaseCleared;
          EventBus.OnBossIntermissionStarted -= StopMovement;
          EventBus.OnBossDefeated -= StopMovement;
          StopMovement();
      }

      private void HandlePhaseStarted(int phaseIndex)
      {
          if (_boss == null || _boss.CurrentPhase == null) return;
          StopMovement();
          _baseLocalPosition = transform.localPosition;
          _movementRoutine = StartCoroutine(RunPattern(_boss.CurrentPhase));
      }

      private void HandlePhaseCleared(int phaseIndex) => StopMovement();

      private void StopMovement()
      {
          if (_movementRoutine != null)
          {
              StopCoroutine(_movementRoutine);
              _movementRoutine = null;
          }
      }

      private IEnumerator RunPattern(BossPhase phase)
      {
          switch (phase.movementPattern)
          {
              case BossMovementPattern.Hover:
                  // Stationary — nothing to do beyond holding position.
                  yield break;

              case BossMovementPattern.Pace:
                  yield return Pace(phase.movementSpeed);
                  break;

              case BossMovementPattern.Teleport:
                  yield return Teleport();
                  break;
          }
      }

      private IEnumerator Pace(float speed)
      {
          float dir = 1f;
          while (true)
          {
              float dx = dir * speed * Time.deltaTime;
              Vector3 next = transform.localPosition + new Vector3(dx, 0f, 0f);
              if (next.x > _baseLocalPosition.x + _paceHalfRange) dir = -1f;
              else if (next.x < _baseLocalPosition.x - _paceHalfRange) dir = 1f;
              else transform.localPosition = next;
              yield return null;
          }
      }

      private IEnumerator Teleport()
      {
          while (true)
          {
              yield return new WaitForSeconds(_teleportInterval);
              float x = _baseLocalPosition.x + Random.Range(-_teleportHalfRange, _teleportHalfRange);
              transform.localPosition = new Vector3(x, _baseLocalPosition.y, _baseLocalPosition.z);
          }
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 6.1.3: Verify compile**

  Switch to Unity, wait for `Compiling...` to clear. Pre-existing `WaveManager.cs:275` error stays.

- [ ] **Step 6.1.4: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs
  git commit -m "feat(boss): SALIN-68 add PhaseBasedMovement ability component"
  ```

### §6.2 Create `SummonWaveOnPhaseStart.cs`

**Files:**
- Create: [Assets/Scripts/Gameplay/Boss/SummonWaveOnPhaseStart.cs](../../../Assets/Scripts/Gameplay/Boss/SummonWaveOnPhaseStart.cs)

This is the load-bearing threat source for El Inquisidor. The boss does not directly attack the shrine — threat comes from summoned adds walking toward the shrine. A boss without `SummonWaveOnPhaseStart` is non-threatening, which is acceptable for stubs.

- [ ] **Step 6.2.1: Create the file**

  Project window → `Assets/Scripts/Gameplay/Boss/` → right-click → `Create → Scripting → MonoBehaviour Script`. Name it `SummonWaveOnPhaseStart`. Open it.

- [ ] **Step 6.2.2: Replace the body**

  Select all, delete, paste:

  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  // Spawns a mini-wave of adds during specified active phases (distinct from
  // the post-phase intermission, which is owned by BossController).
  // El Inquisidor uses this for his "summons Soldado reinforcements" ability.
  [RequireComponent(typeof(BossController))]
  public class SummonWaveOnPhaseStart : MonoBehaviour
  {
      [Tooltip("Phase indices (0-based) that should trigger the summon. Phases not listed are skipped.")]
      [SerializeField] private List<int> _triggerOnPhaseIndices = new();

      [Tooltip("Wave config to spawn when one of the listed phases starts.")]
      [SerializeField] private WaveConfigSO _waveToSpawn;

      [Tooltip("Optional explicit reference. If left empty, this component finds the WaveSpawner via FindFirstObjectByType at Awake — required because prefabs cannot reference scene objects directly.")]
      [SerializeField] private WaveSpawner _spawner;

      private BossController _boss;

      private void Awake()
      {
          _boss = GetComponent<BossController>();
          if (_spawner == null)
              _spawner = FindFirstObjectByType<WaveSpawner>();
      }

      private void OnEnable()
      {
          EventBus.OnBossPhaseStarted += HandlePhaseStarted;
      }

      private void OnDisable()
      {
          EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
      }

      private void HandlePhaseStarted(int phaseIndex)
      {
          if (_waveToSpawn == null) return;
          if (_spawner == null)
          {
              DebugLogger.LogWarning("SummonWaveOnPhaseStart: WaveSpawner reference not set — skipping summon.");
              return;
          }
          if (_triggerOnPhaseIndices == null || !_triggerOnPhaseIndices.Contains(phaseIndex))
              return;

          // Ignore if the boss is not actually targetable — defensive against
          // event-ordering surprises during unit tests.
          if (_boss != null && !_boss.IsTargetable) return;

          StartCoroutine(_spawner.SpawnWave(_waveToSpawn));
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 6.2.3: Verify compile**

  Switch to Unity. Console should still show only the `WaveManager.cs:275` error.

- [ ] **Step 6.2.4: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Boss/SummonWaveOnPhaseStart.cs
  git commit -m "feat(boss): SALIN-68 add SummonWaveOnPhaseStart ability component"
  ```

---

## Task 7: Code — `WaveManager.RunBossEncounter` rewrite

The placeholder body is replaced wholesale with a hand-off to `BossController.StartBoss`. The boss is the source of `OnLevelComplete` during boss encounters; `WaveManager.CompleteRun()` is intentionally NOT called.

### §7.1 Replace the `isBossLevel` branch

**Files:**
- Modify: [Assets/Scripts/Gameplay/Wave/WaveManager.cs](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs)

- [ ] **Step 7.1.1: Locate and replace the branch**

  Open `WaveManager.cs`. Use Ctrl+F to find `_levelConfig.isBossLevel` (line 275). The current four-line block is:

  ```csharp
          if (_levelConfig.isBossLevel && _levelConfig.bossConfig != null)
          {
              yield return StartCoroutine(RunBossEncounter(_levelConfig.bossConfig));
              yield break;
          }
  ```

  Replace with:

  ```csharp
          if (_levelConfig.bossConfig != null)
          {
              yield return StartCoroutine(RunBossEncounter(_levelConfig.bossConfig));
              yield break;
          }
  ```

  Save (Ctrl+S).

### §7.2 Replace the `RunBossEncounter` body

- [ ] **Step 7.2.1: Locate the placeholder method**

  Use Ctrl+F to find `private IEnumerator RunBossEncounter` (line 364). The whole method body (lines 364–418) is the placeholder.

- [ ] **Step 7.2.2: Replace the entire method**

  Select from `private IEnumerator RunBossEncounter(BossConfigSO bossConfig)` (line 364) through the closing `}` of the method (line 418). Replace with:

  ```csharp
      private IEnumerator RunBossEncounter(BossConfigSO bossConfig)
      {
          if (bossConfig.bossEnemyData == null
              || bossConfig.phases == null
              || bossConfig.phases.Count == 0)
          {
              DebugLogger.LogError("WaveManager: BossConfig is incomplete (missing bossEnemyData or phases). Aborting boss encounter.");
              AbortRun();
              yield break;
          }

          // Spawn the boss as a regular Enemy. No character assigned —
          // BossController.TryRouteDraw replaces character matching.
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

          // Wait for the boss to be defeated (Outro complete) — boss raises
          // OnLevelComplete itself.
          yield return new WaitUntil(() => !CanContinueRun() || boss.IsDefeated);

          if (!CanContinueRun())
          {
              AbortRun();
              yield break;
          }

          // BossController is the source of OnLevelComplete during boss
          // encounters. CompleteRun is intentionally NOT called here.
          _running = false;
          _waveRoutine = null;
      }
  ```

  Save (Ctrl+S).

- [ ] **Step 7.2.3: Verify compile**

  Switch to Unity, wait for `Compiling...` to clear. The Console should now be **clean** — all transient errors from Tasks 1–6 are resolved.

  > **NOTE on `_running` and `_waveRoutine`** These are existing private fields on `WaveManager` used to gate `RunRoutine` re-entry. They are confirmed present in the file from prior work. If the verify step shows `CS0103: '_running' could not be found`, the assumption is wrong — search `WaveManager.cs` for the running-state field name and adapt. (Do not invent a new field.)

- [ ] **Step 7.2.4: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Wave/WaveManager.cs
  git commit -m "feat(wave): SALIN-68 hand off boss encounters to BossController"
  ```

---

## Task 8: Code — `CombatResolver` boss-route

One new block at the top of `HandleCharacterRecognized`, before the AOE-count loop.

### §8.1 Insert the boss-route block

**Files:**
- Modify: [Assets/Scripts/Gameplay/Combat/CombatResolver.cs](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs)

- [ ] **Step 8.1.1: Open and locate `HandleCharacterRecognized`**

  Open `CombatResolver.cs`. Use Ctrl+F to find `private void HandleCharacterRecognized(string characterID)` (line 22). The current method opens with:

  ```csharp
      private void HandleCharacterRecognized(string characterID)
      {
          ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
          if (tracker == null)
              return;
  ```

- [ ] **Step 8.1.2: Add the boss-route block**

  Position the cursor on a new blank line **immediately after** the opening `{` of `HandleCharacterRecognized`. Paste:

  ```csharp
          // Boss route — runs before AOE and closest-match. If the active boss
          // is targetable and the draw matches a required character, the boss
          // consumes the draw (Hit or Duplicate). Otherwise we fall through.
          BossController boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
          if (boss != null && boss.IsTargetable)
          {
              BossRouteResult routed = boss.TryRouteDraw(characterID);
              if (routed != BossRouteResult.NotRouted)
                  return;
          }

  ```

  The opening of the method should now read:

  ```csharp
      private void HandleCharacterRecognized(string characterID)
      {
          // Boss route — runs before AOE and closest-match. ...
          BossController boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
          if (boss != null && boss.IsTargetable)
          {
              BossRouteResult routed = boss.TryRouteDraw(characterID);
              if (routed != BossRouteResult.NotRouted)
                  return;
          }

          ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
          if (tracker == null)
              return;
  ```

  Save (Ctrl+S).

- [ ] **Step 8.1.3: Verify compile**

  Switch to Unity, wait for `Compiling...` to clear. Console clean.

- [ ] **Step 8.1.4: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Combat/CombatResolver.cs
  git commit -m "feat(combat): SALIN-68 route draws to BossController before AOE"
  ```

---

## Task 9: Code — `BossLabelIconRow`

UI component rendered as a child of the Gameplay Canvas. It listens to boss events, rebuilds an icon row from `BossController.RequiredCharacters` on phase start, follows the boss's screen position, and greys out icons as `OnDrawnThisPhaseChanged` fires.

### §9.1 Create the script

**Files:**
- Create: [Assets/Scripts/UI/BossLabelIconRow.cs](../../../Assets/Scripts/UI/BossLabelIconRow.cs)

- [ ] **Step 9.1.1: Create the file**

  Project window → `Assets/Scripts/UI/` → right-click → `Create → Scripting → MonoBehaviour Script`. Name `BossLabelIconRow`. Open it.

- [ ] **Step 9.1.2: Replace the body**

  Select all, delete, paste:

  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.UI;

  // Renders a row of Baybayin character icons above the boss representing
  // the current phase's required characters. Each icon greys out as the
  // player draws it. Hides during intermission and outro.
  // Lives on the Gameplay Canvas — set up in §16.
  public class BossLabelIconRow : MonoBehaviour
  {
      [Header("Icon Prefab (Image with RectTransform 32x32)")]
      [SerializeField] private Image _iconPrefab;
      [SerializeField] private RectTransform _container;

      [Header("Visuals")]
      [SerializeField] private float _iconSize = 32f;
      [SerializeField] private float _iconGap = 4f;
      [SerializeField] private float _drawnAlpha = 0.4f;
      [SerializeField] private Color _drawnTint = new(0.5f, 0.5f, 0.5f, 1f);
      [SerializeField] private Vector2 _bossWorldOffset = new(0f, 1.0f);

      [Header("Camera (optional — falls back to Camera.main)")]
      [SerializeField] private Camera _gameplayCamera;

      private readonly List<Image> _spawnedIcons = new();
      private readonly Dictionary<BaybayinCharacterSO, Image> _iconByChar = new();
      private BossController _boss;
      private Transform _bossTransform;

      private void Awake()
      {
          if (_container == null) _container = (RectTransform)transform;
          if (_gameplayCamera == null) _gameplayCamera = Camera.main;
          gameObject.SetActive(false);
      }

      private void OnEnable()
      {
          EventBus.OnBossStarted += HandleBossStarted;
          EventBus.OnBossPhaseStarted += HandlePhaseStarted;
          EventBus.OnBossPhaseCleared += HandlePhaseCleared;
          EventBus.OnBossIntermissionStarted += HideRow;
          EventBus.OnBossDefeated += HandleBossDefeated;
      }

      private void OnDisable()
      {
          EventBus.OnBossStarted -= HandleBossStarted;
          EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
          EventBus.OnBossPhaseCleared -= HandlePhaseCleared;
          EventBus.OnBossIntermissionStarted -= HideRow;
          EventBus.OnBossDefeated -= HandleBossDefeated;
          UnsubscribeFromBossInstance();
      }

      private void HandleBossStarted(BossConfigSO config)
      {
          gameObject.SetActive(true);
          // Boss transform — locate via GameManager.CurrentBoss (set inside StartBoss).
          if (GameManager.Instance != null && GameManager.Instance.CurrentBoss != null)
              _bossTransform = GameManager.Instance.CurrentBoss.transform;
      }

      private void HandlePhaseStarted(int phaseIndex)
      {
          UnsubscribeFromBossInstance();

          _boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
          if (_boss == null) return;

          _boss.OnDrawnThisPhaseChanged += RefreshIconStates;

          BuildIcons(_boss.RequiredCharacters);
          RefreshIconStates();
      }

      private void HandlePhaseCleared(int phaseIndex)
      {
          // Flash + hide. Per spec: row re-shows on the next OnBossPhaseStarted.
          HideRow();
      }

      private void HandleBossDefeated()
      {
          UnsubscribeFromBossInstance();
          ClearIcons();
          gameObject.SetActive(false);
      }

      private void UnsubscribeFromBossInstance()
      {
          if (_boss != null)
              _boss.OnDrawnThisPhaseChanged -= RefreshIconStates;
          _boss = null;
      }

      private void HideRow()
      {
          for (int i = 0; i < _spawnedIcons.Count; i++)
              if (_spawnedIcons[i] != null)
                  _spawnedIcons[i].gameObject.SetActive(false);
      }

      private void BuildIcons(IReadOnlyList<BaybayinCharacterSO> required)
      {
          ClearIcons();
          if (_iconPrefab == null || _container == null || required == null) return;

          int count = 0;
          for (int i = 0; i < required.Count; i++) if (required[i] != null) count++;

          // Center the row horizontally on the container.
          float totalWidth = count * _iconSize + Mathf.Max(0, count - 1) * _iconGap;
          float x = -totalWidth * 0.5f + _iconSize * 0.5f;

          for (int i = 0; i < required.Count; i++)
          {
              BaybayinCharacterSO so = required[i];
              if (so == null) continue;

              Image icon = Instantiate(_iconPrefab, _container);
              icon.gameObject.SetActive(true);
              icon.sprite = so.displaySprite;
              icon.color = Color.white;
              icon.preserveAspect = true;

              RectTransform rt = (RectTransform)icon.transform;
              rt.sizeDelta = new Vector2(_iconSize, _iconSize);
              rt.anchoredPosition = new Vector2(x, 0f);
              x += _iconSize + _iconGap;

              _spawnedIcons.Add(icon);
              _iconByChar[so] = icon;
          }
      }

      private void ClearIcons()
      {
          for (int i = 0; i < _spawnedIcons.Count; i++)
              if (_spawnedIcons[i] != null)
                  Destroy(_spawnedIcons[i].gameObject);
          _spawnedIcons.Clear();
          _iconByChar.Clear();
      }

      private void RefreshIconStates()
      {
          if (_boss == null) return;

          IReadOnlyCollection<BaybayinCharacterSO> drawn = _boss.DrawnThisPhase;
          foreach (KeyValuePair<BaybayinCharacterSO, Image> kv in _iconByChar)
          {
              bool isDrawn = drawn != null && drawn.Contains(kv.Key);
              Color c = isDrawn ? _drawnTint : Color.white;
              c.a = isDrawn ? _drawnAlpha : 1f;
              kv.Value.color = c;
          }
      }

      private void Update()
      {
          // Follow the boss's screen position.
          if (_bossTransform == null || _gameplayCamera == null || _container == null) return;

          Vector3 worldPos = _bossTransform.position + (Vector3)_bossWorldOffset;
          Vector2 screenPos = _gameplayCamera.WorldToScreenPoint(worldPos);
          _container.position = new Vector3(screenPos.x, screenPos.y, _container.position.z);
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 9.1.3: Verify compile**

  Switch to Unity. Console clean.

- [ ] **Step 9.1.4: Commit**

  ```bash
  git add Assets/Scripts/UI/BossLabelIconRow.cs
  git commit -m "feat(ui): SALIN-68 BossLabelIconRow renders required-character icons"
  ```

---

## Task 10: Tests — EditMode `BossControllerTests`

EditMode tests using the `ActiveEnemyTrackerTests` fixture pattern: `ScriptableObject.CreateInstance` for SOs, programmatic `GameObject` + `AddComponent` for the controller, reflection for non-public state. The tests cover the seven cases from spec §12.1 and inject a stub `WaveSpawner` for the intermission test.

**Files:**
- Create: [Assets/Tests/Editor/Gameplay/BossControllerTests.cs](../../../Assets/Tests/Editor/Gameplay/BossControllerTests.cs)

### §10.0 Make `WaveSpawner.SpawnWave` virtual (test-fixture precursor)

The `FakeWaveSpawner` test double (used by tests 6 and 7) needs to override `SpawnWave`. C# does not let `new` shadowing intercept calls dispatched through a base-typed reference (`BossController._spawner` is typed as `WaveSpawner`). The cleanest fix is one keyword — make the production method virtual.

**Files:**
- Modify: [Assets/Scripts/Gameplay/Wave/WaveSpawner.cs](../../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs)

- [ ] **Step 10.0.1: Add `virtual` to `SpawnWave`**

  Open `WaveSpawner.cs`. Use Ctrl+F to find `public IEnumerator SpawnWave(WaveConfigSO wave` (line 99). Change:

  ```csharp
      public IEnumerator SpawnWave(WaveConfigSO wave, Action onEnemySpawned = null, int spawnOffset = 0)
  ```

  to:

  ```csharp
      public virtual IEnumerator SpawnWave(WaveConfigSO wave, Action onEnemySpawned = null, int spawnOffset = 0)
  ```

  Save (Ctrl+S). Verify Unity recompiles cleanly.

- [ ] **Step 10.0.2: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Wave/WaveSpawner.cs
  git commit -m "refactor(wave): SALIN-68 make WaveSpawner.SpawnWave virtual for test doubles"
  ```

### §10.1 Test 1 — non-required draw is `NotRouted`, no advance

### §10.2 Test 2 — phase with 3 required chars clears when all drawn (any order)

### §10.3 Test 3 — duplicate required draw raises `OnDrawingFailed` and is consumed

### §10.4 Test 4 — last phase cleared raises `OnBossDefeated` and `OnLevelComplete`

### §10.5 Test 5 — Intro: `IsTargetable` is false

### §10.6 Test 6 — Intermission: `IsTargetable` is false

### §10.7 Test 7 — intermission spawning uses injected spawner

### §10.8 Test 8 — `IsDefeated` flips at the start of Outro

- [ ] **Step 10.1: Create the file**

  Project window → `Assets/Tests/Editor/Gameplay/` → right-click → `Create → C# Script`. Name `BossControllerTests`. Open the file.

- [ ] **Step 10.2: Paste the full body**

  Select all, delete, paste:

  ```csharp
  using System.Collections;
  using System.Collections.Generic;
  using System.Reflection;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Salinlahi.Tests.Editor.Gameplay
  {
      [TestFixture]
      public class BossControllerTests
      {
          private readonly List<Object> _objectsToDestroy = new();
          private int _onDrawingFailedCount;
          private int _onBossDefeatedCount;
          private int _onLevelCompleteCount;
          private int _onPhaseStartedCount;
          private int _onPhaseClearedCount;

          [SetUp]
          public void SetUp()
          {
              _onDrawingFailedCount = 0;
              _onBossDefeatedCount = 0;
              _onLevelCompleteCount = 0;
              _onPhaseStartedCount = 0;
              _onPhaseClearedCount = 0;

              EventBus.OnDrawingFailed += () => _onDrawingFailedCount++;
              EventBus.OnBossDefeated += () => _onBossDefeatedCount++;
              EventBus.OnLevelComplete += () => _onLevelCompleteCount++;
              EventBus.OnBossPhaseStarted += _ => _onPhaseStartedCount++;
              EventBus.OnBossPhaseCleared += _ => _onPhaseClearedCount++;
          }

          [TearDown]
          public void TearDown()
          {
              for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                  if (_objectsToDestroy[i] != null)
                      Object.DestroyImmediate(_objectsToDestroy[i]);
              _objectsToDestroy.Clear();
          }

          // ---- Test 1 — §10.1 ----
          [UnityTest]
          public IEnumerator NonRequiredDraw_ReturnsNotRouted_NoAdvance()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BaybayinCharacterSO ka = CreateChar("KA");
              BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                  new List<BossPhase> { CreatePhase(new[] { ba }) });

              (BossController boss, FakeWaveSpawner _) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());

              // Advance frames so Intro (0s) elapses and PhaseActive begins.
              yield return null;
              yield return null;

              BossRouteResult result = boss.TryRouteDraw(ka.characterID);
              Assert.AreEqual(BossRouteResult.NotRouted, result);
              Assert.AreEqual(0, boss.CurrentPhaseIndex < 0 ? 0 : boss.DrawnThisPhase.Count);
          }

          // ---- Test 2 — §10.2 ----
          [UnityTest]
          public IEnumerator ThreeRequiredChars_ClearsWhenAllDrawn_AnyOrder()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BaybayinCharacterSO ka = CreateChar("KA");
              BaybayinCharacterSO ga = CreateChar("GA");
              BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                  new List<BossPhase> { CreatePhase(new[] { ba, ka, ga }) });

              (BossController boss, FakeWaveSpawner _) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());
              yield return null; yield return null;

              Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("GA"));
              Assert.AreEqual(0, _onPhaseClearedCount);
              Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
              Assert.AreEqual(0, _onPhaseClearedCount);
              Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("KA"));
              Assert.AreEqual(1, _onPhaseClearedCount);
          }

          // ---- Test 3 — §10.3 ----
          [UnityTest]
          public IEnumerator DuplicateRequiredDraw_RaisesOnDrawingFailed_Consumed()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BaybayinCharacterSO ka = CreateChar("KA");
              BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                  new List<BossPhase> { CreatePhase(new[] { ba, ka }) });

              (BossController boss, _) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());
              yield return null; yield return null;

              Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
              BossRouteResult dup = boss.TryRouteDraw("BA");
              Assert.AreEqual(BossRouteResult.Duplicate, dup);
              Assert.AreEqual(1, _onDrawingFailedCount);
              Assert.AreEqual(0, _onPhaseClearedCount, "Duplicate must not clear the phase.");
          }

          // ---- Test 4 — §10.4 ----
          [UnityTest]
          public IEnumerator LastPhaseCleared_RaisesOnBossDefeated_AndOnLevelComplete()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0.05f, phases:
                  new List<BossPhase> { CreatePhase(new[] { ba }) });

              (BossController boss, _) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());
              yield return null; yield return null;

              Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));

              // Wait outroDuration + a frame.
              float t = 0f;
              while (t < 0.2f) { yield return null; t += Time.deltaTime; }

              Assert.AreEqual(1, _onBossDefeatedCount);
              Assert.AreEqual(1, _onLevelCompleteCount);
          }

          // ---- Test 5 — §10.5 ----
          [UnityTest]
          public IEnumerator Intro_IsTargetableFalse_TryRouteDrawReturnsNotRouted()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BossConfigSO config = CreateConfig(introDuration: 0.2f, outroDuration: 0f, phases:
                  new List<BossPhase> { CreatePhase(new[] { ba }) });

              (BossController boss, _) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());
              yield return null;

              Assert.IsFalse(boss.IsTargetable, "IsTargetable must be false during Intro.");
              Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("BA"));
          }

          // ---- Test 6 — §10.6 ----
          [UnityTest]
          public IEnumerator Intermission_IsTargetableFalse_TryRouteDrawReturnsNotRouted()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BaybayinCharacterSO ka = CreateChar("KA");
              WaveConfigSO intermission = ScriptableObject.CreateInstance<WaveConfigSO>();
              _objectsToDestroy.Add(intermission);
              SetField(intermission, "enemyCount", 1);

              BossPhase phase1 = CreatePhase(new[] { ba });
              phase1.intermissionWave = intermission;
              phase1.postIntermissionDelay = 1f; // long enough to assert during

              BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                  new List<BossPhase> { phase1, CreatePhase(new[] { ka }) });

              (BossController boss, FakeWaveSpawner spawner) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());
              yield return null; yield return null;

              Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA")); // clears phase 1
              yield return null;

              Assert.IsFalse(boss.IsTargetable, "IsTargetable must be false during intermission.");
              Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("KA"));
              Assert.AreEqual(1, spawner.SpawnWaveCallCount,
                  "Intermission must spawn the configured wave exactly once.");
          }

          // ---- Test 7 — §10.7 ----
          [UnityTest]
          public IEnumerator IntermissionSpawning_UsesInjectedSpawner()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BaybayinCharacterSO ka = CreateChar("KA");
              WaveConfigSO intermission = ScriptableObject.CreateInstance<WaveConfigSO>();
              _objectsToDestroy.Add(intermission);
              SetField(intermission, "enemyCount", 1);

              BossPhase phase1 = CreatePhase(new[] { ba });
              phase1.intermissionWave = intermission;

              BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                  new List<BossPhase> { phase1, CreatePhase(new[] { ka }) });

              (BossController boss, FakeWaveSpawner spawner) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());
              yield return null; yield return null;

              boss.TryRouteDraw("BA");
              yield return null;

              Assert.AreSame(intermission, spawner.LastSpawnedWave,
                  "BossController must call SpawnWave on the injected spawner with the configured wave.");
          }

          // ---- Test 8 — §10.8 ----
          [UnityTest]
          public IEnumerator IsDefeated_FlipsAtStartOfOutro()
          {
              BaybayinCharacterSO ba = CreateChar("BA");
              BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 1f, phases:
                  new List<BossPhase> { CreatePhase(new[] { ba }) });

              (BossController boss, _) = CreateBossWithFakeSpawner();
              boss.StartBoss(config, GetFakeSpawner());
              yield return null; yield return null;

              Assert.IsFalse(boss.IsDefeated);
              boss.TryRouteDraw("BA");
              yield return null;
              Assert.IsTrue(boss.IsDefeated, "IsDefeated must flip true at the start of Outro.");
          }

          // ---- Helpers ----

          private BaybayinCharacterSO CreateChar(string id)
          {
              BaybayinCharacterSO so = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
              so.characterID = id;
              _objectsToDestroy.Add(so);
              return so;
          }

          private BossPhase CreatePhase(IReadOnlyList<BaybayinCharacterSO> required)
          {
              BossPhase phase = new BossPhase();
              phase.requiredCharacters = new List<BaybayinCharacterSO>(required);
              phase.movementPattern = BossMovementPattern.Hover;
              phase.movementSpeed = 0f;
              return phase;
          }

          private BossConfigSO CreateConfig(float introDuration, float outroDuration, List<BossPhase> phases)
          {
              BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
              config.bossName = "TestBoss";
              config.bossID = "TEST";
              config.introDuration = introDuration;
              config.outroDuration = outroDuration;
              config.phases = phases;

              EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
              data.enemyID = "test_boss";
              data.maxHealth = 1;
              data.moveSpeed = 0f;
              config.bossEnemyData = data;
              _objectsToDestroy.Add(data);

              _objectsToDestroy.Add(config);
              return config;
          }

          private FakeWaveSpawner _fakeSpawner;
          private GameObject _spawnerGO;

          private (BossController, FakeWaveSpawner) CreateBossWithFakeSpawner()
          {
              GameObject bossGO = new GameObject("Boss_Test");
              bossGO.SetActive(false);
              bossGO.AddComponent<SpriteRenderer>();
              bossGO.AddComponent<BoxCollider2D>();
              bossGO.AddComponent<EnemyMover>();
              BossEnemy enemy = bossGO.AddComponent<BossEnemy>();
              SetField(enemy, "_showDebugLabels", false);
              BossController controller = bossGO.AddComponent<BossController>();
              bossGO.SetActive(true);
              _objectsToDestroy.Add(bossGO);

              _spawnerGO = new GameObject("FakeWaveSpawner");
              _fakeSpawner = _spawnerGO.AddComponent<FakeWaveSpawner>();
              _objectsToDestroy.Add(_spawnerGO);

              return (controller, _fakeSpawner);
          }

          private WaveSpawner GetFakeSpawner() => _fakeSpawner;

          private static void SetField(object target, string fieldName, object value)
          {
              FieldInfo f = target.GetType().GetField(fieldName,
                  BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
              Assert.IsNotNull(f, $"Missing field '{fieldName}' on {target.GetType().Name}.");
              f.SetValue(target, value);
          }

          // ---- Test double ----

          // Subclass of WaveSpawner that intercepts SpawnWave so the test does
          // not need real EnemyPool/spawn-points. SpawnEnemy is not exercised
          // because the test boss is constructed by hand (not pool-spawned).
          // Requires WaveSpawner.SpawnWave to be virtual (made so in §10.0).
          private class FakeWaveSpawner : WaveSpawner
          {
              public int SpawnWaveCallCount;
              public WaveConfigSO LastSpawnedWave;

              public override IEnumerator SpawnWave(WaveConfigSO wave, System.Action onEnemySpawned = null, int spawnOffset = 0)
              {
                  SpawnWaveCallCount++;
                  LastSpawnedWave = wave;
                  yield break; // complete synchronously — no enemies to track
              }
          }
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 10.3: Run the test file**

  Unity menu bar → `Window → General → Test Runner`. Switch to the `EditMode` tab. Right-click `BossControllerTests` → `Run`. All 8 tests should pass on the first run.

- [ ] **Step 10.4: Commit**

  ```bash
  git add Assets/Tests/Editor/Gameplay/BossControllerTests.cs
  git commit -m "test(boss): SALIN-68 EditMode coverage of BossController state machine"
  ```

---

## Task 11: Tests — PlayMode `ElInquisidorTest` (smoke)

One end-to-end PlayMode test that spawns the El Inquisidor encounter and drives it via direct calls to `BossController.TryRouteDraw`. Asserts the full Intro → 3 phases → 2 intermissions → Outro sequence.

**Files:**
- Create: [Assets/Tests/PlayMode/Gameplay/ElInquisidorTest.cs](../../../Assets/Tests/PlayMode/Gameplay/ElInquisidorTest.cs)
- Possibly create: `Assets/Tests/PlayMode/` folder + `Salinlahi.Tests.PlayMode.asmdef`

### §11.1 Create the PlayMode test assembly (if missing)

- [ ] **Step 11.1.1: Check whether the PlayMode asmdef already exists**

  Project window → `Assets/Tests/PlayMode/`. If the folder does not exist, create it: right-click `Assets/Tests/` → `Create → Folder` → name `PlayMode`. Inside `PlayMode/` → check for an `.asmdef` file. If none, right-click → `Create → Assembly Definition`. Name `Salinlahi.Tests.PlayMode`. Inspector → `Test Assemblies` checked, `Platforms → Editor` only (uncheck others).

- [ ] **Step 11.1.2: Add references**

  Inspector → Assembly Definition References → add:
  - `Assembly-CSharp`
  - `UnityEngine.TestRunner`
  - `UnityEditor.TestRunner`
  - `nunit.framework.dll` (under "Override References")

  Apply. Wait for Unity to recompile.

### §11.2 Use the existing Gameplay scene

The smoke test reuses the production `Bootstrap` + `Gameplay` scene wiring rather than constructing a fresh test scene from scratch. This avoids re-authoring the singleton bootstrap, EnemyPool registrations, spawn points, and HUD references — all of which are already correctly wired in the live scenes.

- [ ] **Step 11.2.1: Add Bootstrap and Gameplay scenes to Build Settings**

  Unity menu bar → `File → Build Profiles → Scene List`. Confirm `Assets/_Scenes/Bootstrap.unity` and `Assets/_Scenes/Gameplay.unity` are both present and enabled. (They likely already are — check, do not duplicate.)

  > **NOTE on PlayMode test discovery** PlayMode tests can `SceneManager.LoadScene` any scene that's in the Build Settings list. They cannot load arbitrary scenes by path. If the Build Settings list does not include the Gameplay scene, the test's `LoadScene` call fails silently.

### §11.3 Add the smoke test

- [ ] **Step 11.3.1: Create the test file**

  Project window → `Assets/Tests/PlayMode/` → create folder `Gameplay/`. Inside, right-click → `Create → C# Script`. Name `ElInquisidorTest`. Open it.

- [ ] **Step 11.3.2: Replace the body**

  Select all, delete, paste:

  ```csharp
  using System.Collections;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using UnityEngine.TestTools;

  namespace Salinlahi.Tests.PlayMode.Gameplay
  {
      [TestFixture]
      public class ElInquisidorTest
      {
          private int _onLevelCompleteCount;

          [SetUp]
          public void SetUp()
          {
              _onLevelCompleteCount = 0;
              EventBus.OnLevelComplete += OnLevelCompleteHandler;
          }

          [TearDown]
          public void TearDown()
          {
              EventBus.OnLevelComplete -= OnLevelCompleteHandler;
          }

          private void OnLevelCompleteHandler() => _onLevelCompleteCount++;

          [UnityTest]
          public IEnumerator ElInquisidor_IntroThreePhasesTwoIntermissionsOutro_RaisesOnLevelComplete()
          {
              // Load the production Bootstrap scene first so all manager
              // singletons (GameManager, EnemyPool, ActiveEnemyTracker, etc.)
              // come up. Bootstrap auto-transitions to MainMenu — bypass that
              // by loading Gameplay directly after Bootstrap's Awake/Start.
              SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
              yield return null; yield return null;

              SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
              yield return null; yield return null;

              // Wire the level config — point WaveManager at a LevelConfig
              // that references BossConfig_ElInquisidor.
              LevelConfigSO level = Resources.Load<LevelConfigSO>("Test/Level5_ElInquisidor_TestRig");
              Assume.That(level, Is.Not.Null,
                  "Test rig level not found. Author Resources/Test/Level5_ElInquisidor_TestRig.asset.");

              GameManager.Instance.SetLevel(level);

              // Kick off the run via the public entry point (verified at
              // WaveManager.cs:66).
              WaveManager wm = Object.FindFirstObjectByType<WaveManager>();
              Assume.That(wm, Is.Not.Null, "WaveManager not present in Gameplay scene.");
              wm.StartLevel(level);

              // Wait for the boss to spawn and Intro to elapse.
              float waited = 0f;
              while (GameManager.Instance.CurrentBoss == null && waited < 5f)
              {
                  yield return null;
                  waited += Time.deltaTime;
              }
              Assert.IsNotNull(GameManager.Instance.CurrentBoss, "Boss did not spawn within 5s.");
              BossController boss = GameManager.Instance.CurrentBoss;

              // Drive the encounter: 3 phases, 1 char each, with 2 intermissions.
              for (int phaseIdx = 0; phaseIdx < boss.Config.phases.Count; phaseIdx++)
              {
                  // Wait until this phase becomes targetable.
                  float t = 0f;
                  while ((!boss.IsTargetable || boss.CurrentPhaseIndex != phaseIdx) && t < 5f)
                  {
                      yield return null;
                      t += Time.deltaTime;
                  }
                  Assert.IsTrue(boss.IsTargetable, $"Phase {phaseIdx} did not become targetable within 5s.");

                  // Draw each required character once.
                  System.Collections.Generic.IReadOnlyList<BaybayinCharacterSO> required = boss.RequiredCharacters;
                  for (int i = 0; i < required.Count; i++)
                  {
                      if (required[i] == null) continue;
                      boss.TryRouteDraw(required[i].characterID);
                      yield return null;
                  }
              }

              // Wait for OnLevelComplete with a generous timeout (covers outro + intermission spawn waits).
              float endWait = 0f;
              while (_onLevelCompleteCount == 0 && endWait < 30f)
              {
                  yield return null;
                  endWait += Time.deltaTime;
              }

              Assert.AreEqual(1, _onLevelCompleteCount, "OnLevelComplete must fire exactly once.");
          }
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 11.3.3: Author the test rig level config**

  Project window → `Assets/Resources/Test/` (create folder if missing) → right-click → `Create → Salinlahi → Level Config`. Name `Level5_ElInquisidor_TestRig`. Inspector → set `levelName = "Test L5"`, `levelNumber = 5`, drop `BossConfig_ElInquisidor` (created in §13.2) into `bossConfig`. Save.

- [ ] **Step 11.3.4: Run the test**

  Test Runner → `PlayMode` tab → run `ElInquisidor_IntroThreePhasesTwoIntermissionsOutro_RaisesOnLevelComplete`. Should pass within ~30 seconds.

- [ ] **Step 11.3.5: Commit**

  ```bash
  git add Assets/Tests/PlayMode/ Assets/Resources/Test/Level5_ElInquisidor_TestRig.asset
  git commit -m "test(boss): SALIN-68 PlayMode smoke test for El Inquisidor"
  ```

---

## Task 12: Manual Unity — Boss prefab creation

The framework now compiles and is unit-tested. The next group of tasks creates the runtime assets the framework consumes: prefabs, EnemyDataSOs, BossConfigSOs, intermission waves, and level wiring.

### §12.1 Create `[Enemy] Boss_ElInquisidor.prefab`

**Files:**
- Create: `Assets/Prefabs/Enemies/[Enemy] Boss_ElInquisidor.prefab`

- [ ] **Step 12.1.1: Duplicate an existing enemy prefab as a starting point**

  Project window → `Assets/Prefabs/Enemies/[Enemy] Soldier.prefab` → right-click → `Duplicate`. Rename the copy to `[Enemy] Boss_ElInquisidor.prefab`.

- [ ] **Step 12.1.2: Open in Prefab Mode**

  Double-click `[Enemy] Boss_ElInquisidor.prefab`. Hierarchy shows the root GameObject (renamed from "Soldier copy") still has `Transform`, `SpriteRenderer`, `BoxCollider2D`, `EnemyMover`, `Enemy`, `Rigidbody2D`.

- [ ] **Step 12.1.3: Rename the root GameObject**

  Hierarchy → click the root → press F2 → type `[Enemy] Boss_ElInquisidor` → Enter.

- [ ] **Step 12.1.4: Replace `Enemy` with `BossEnemy`**

  Inspector → find the `Enemy` component → click the gear icon (top-right of the component) → `Remove Component`. Now click `Add Component` → search `BossEnemy` → click. The new `BossEnemy` script appears with the same serialized fields (the `_data` slot is cleared because it was on the old `Enemy` instance).

  > **Why not "Replace Component"?** Unity has no in-place script-replace; remove + add is the standard workflow. The Inspector's serialized fields on the new component start at defaults — this is fine because we re-assign `_data` next.

- [ ] **Step 12.1.5: Assign the boss data slot**

  We have not yet authored `EnemyData_Boss_ElInquisidor` (§13.1). Leave the `_data` slot empty for now — the prefab will have it set in §13.1's later step.

- [ ] **Step 12.1.6: Add `BossController`**

  Inspector → `Add Component` → search `BossController` → click. No serialized fields appear (per §5 there are none — the controller takes its config via `StartBoss`).

- [ ] **Step 12.1.7: Add `PhaseBasedMovement`**

  Inspector → `Add Component` → search `PhaseBasedMovement` → click. Set:
  - `Pace Half Range = 1.5`
  - `Teleport Interval = 1.5`
  - `Teleport Half Range = 2.0`

- [ ] **Step 12.1.8: Add `SummonWaveOnPhaseStart`**

  Inspector → `Add Component` → search `SummonWaveOnPhaseStart` → click. Configure:
  - `Trigger On Phase Indices` → size `2`, elements `0` and `1` (phases 1 and 2 trigger summons; phase 3 does not because intermission wave is null on phase 2 anyway and we want phase 3 quiet).
  - `Wave To Spawn` → leave empty for now; assign in §13.5 after the wave config exists.
  - `Spawner` → leave empty for now; assign in §16 (scene wiring).

- [ ] **Step 12.1.9: Verify `Rigidbody2D` and `BoxCollider2D`**

  Inspector → confirm:
  - `Rigidbody2D.Body Type = Kinematic`
  - `BoxCollider2D.Is Trigger = checked`
  - `Transform.Local Scale = (0.5, 0.5, 1)` (boss is bigger than rank-and-file — adjust later via the artist's sprite size if needed)

- [ ] **Step 12.1.10: Exit Prefab Mode and save**

  Click the breadcrumb `<` → save when prompted. `File → Save Project` to write to disk.

- [ ] **Step 12.1.11: Commit**

  ```bash
  git add "Assets/Prefabs/Enemies/[Enemy] Boss_ElInquisidor.prefab"
  git commit -m "feat(prefab): SALIN-68 add El Inquisidor boss prefab skeleton"
  ```

---

## Task 13: Manual Unity — Data assets

### §13.1 Create `EnemyData_Boss_ElInquisidor`

**Files:**
- Create: `Assets/ScriptableObjects/EnemyData_Boss_ElInquisidor.asset`

The boss EnemyDataSO sets `assignedCharacter = null` so `Enemy.Character` is null on the boss — making it invisible to `ActiveEnemyTracker.FindClosestToBase`. `maxHealth = 1` is a non-zero placeholder that satisfies `Enemy.Initialize`'s validation; it never decrements because `BossEnemy.TakeDamage` is a no-op.

- [ ] **Step 13.1.1: Create the asset**

  Project window → `Assets/ScriptableObjects/` → right-click → `Create → Salinlahi → Enemy Data`. Name `EnemyData_Boss_ElInquisidor`. (If "Salinlahi → Enemy Data" is not in the menu, look for the menu name in `EnemyDataSO.cs` `[CreateAssetMenu(...)]` attribute.)

- [ ] **Step 13.1.2: Configure values**

  Inspector → fill in:

  | Field | Value |
  |---|---|
  | Enemy ID | `elinquisidor` |
  | Move Speed | `0` |
  | Max Health | `1` |
  | Walk Frames | leave empty (boss uses static sprite or its own animator) |
  | Animator Controller | leave empty |
  | Assigned Character | **leave empty (None)** — this is critical |
  | Is Decoy | unchecked |
  | Deals Contact Damage | unchecked (boss does not melee the shrine) |

  > **NOTE on `Assigned Character` being null** Unity allows null SO references and the `Enemy.Character` property handles null gracefully (`_data?.assignedCharacter` is null, so `Character` returns null). `ActiveEnemyTracker.FindClosestToBase` skips entries where `e.Character == null` (line 62), so the boss is invisible to character-targeted draws.

- [ ] **Step 13.1.3: Wire into the prefab**

  Project window → `Assets/Prefabs/Enemies/[Enemy] Boss_ElInquisidor.prefab` → double-click. Hierarchy → root → Inspector → `BossEnemy → _data` → drag `EnemyData_Boss_ElInquisidor.asset` into the slot.

  Exit Prefab Mode (`<`), save.

### §13.2 Create `BossConfig_ElInquisidor.asset`

**Files:**
- Create: `Assets/ScriptableObjects/BossConfig_ElInquisidor.asset`

- [ ] **Step 13.2.1: Create the asset**

  Project window → `Assets/ScriptableObjects/` → right-click → `Create → Salinlahi → Boss Config`. Name `BossConfig_ElInquisidor`.

- [ ] **Step 13.2.2: Configure identity**

  Inspector → fill:

  | Field | Value |
  |---|---|
  | Boss Name | `El Inquisidor` |
  | Boss ID | `ELINQUISIDOR` |
  | Boss Sprite | leave empty (or assign a portrait if available) |
  | Boss Enemy Data | drag `EnemyData_Boss_ElInquisidor.asset` |
  | Intro Duration | `2.0` |
  | Outro Duration | `2.5` |

- [ ] **Step 13.2.3: Configure `Phases` (size 3)**

  Inspector → `Phases → Size = 3`.

  **Phase 0 (BA):**

  | Field | Value |
  |---|---|
  | Required Characters → Size | `1` |
  | Required Characters [0] | drag `Char_BA.asset` from `Assets/ScriptableObjects/` |
  | Movement Pattern | `Hover` |
  | Movement Speed | `0` |
  | Intermission Wave | leave empty for now (assigned in §13.5 after `Boss_L5_Intermission1` exists) |
  | Post Intermission Delay | `1.0` |

  **Phase 1 (KA):**

  | Field | Value |
  |---|---|
  | Required Characters → Size | `1` |
  | Required Characters [0] | drag `Char_KA.asset` |
  | Movement Pattern | `Pace` |
  | Movement Speed | `1.0` |
  | Intermission Wave | leave empty for now (§13.5) |
  | Post Intermission Delay | `1.0` |

  **Phase 2 (GA):**

  | Field | Value |
  |---|---|
  | Required Characters → Size | `1` |
  | Required Characters [0] | drag `Char_GA.asset` |
  | Movement Pattern | `Teleport` |
  | Movement Speed | `0` |
  | Intermission Wave | leave empty (final phase, no intermission) |
  | Post Intermission Delay | `0` |

  Save (Ctrl+S).

### §13.3 Create `BossConfig_Superintendent.asset` (stub)

**Files:**
- Create: `Assets/ScriptableObjects/BossConfig_Superintendent.asset`

- [ ] **Step 13.3.1: Create and configure the placeholder**

  Project window → `Assets/ScriptableObjects/` → right-click → `Create → Salinlahi → Boss Config`. Name `BossConfig_Superintendent`. Inspector:

  | Field | Value |
  |---|---|
  | Boss Name | `The Superintendent` |
  | Boss ID | `SUPERINTENDENT` |
  | Boss Enemy Data | drag `EnemyData_Boss_ElInquisidor.asset` (re-used as a placeholder; replaced in a later content sprint) |
  | Intro Duration | `2.0` |
  | Outro Duration | `2.5` |
  | Phases → Size | `1` |
  | Phases[0] → Required Characters → Size | `1` |
  | Phases[0] → Required Characters [0] | drag `Char_A.asset` (or any Chapter-2 character) |
  | Phases[0] → Movement Pattern | `Hover` |
  | Phases[0] → Movement Speed | `0` |
  | Phases[0] → Intermission Wave | empty |
  | Phases[0] → Post Intermission Delay | `0` |

  Save (Ctrl+S).

### §13.4 Reshape `BossConfig_Kadiliman.asset` to the new schema (stub)

- [ ] **Step 13.4.1: Open the asset**

  Project window → `Assets/ScriptableObjects/BossConfig_Kadiliman.asset`. Inspector now shows the new shape (`Phases` empty, identity fields populated from the migrated YAML).

- [ ] **Step 13.4.2: Configure as a 1-phase placeholder**

  Apply identical shape to §13.3.2 with values:

  | Field | Value |
  |---|---|
  | Boss Name | `Kadiliman` (unchanged) |
  | Boss ID | `KADILIMAN` |
  | Boss Enemy Data | drag `EnemyData_Boss_ElInquisidor.asset` (placeholder) |
  | Intro Duration | `2.0` |
  | Outro Duration | `2.5` |
  | Phases → Size | `1` |
  | Phases[0] → Required Characters → Size | `1` |
  | Phases[0] → Required Characters [0] | drag any Chapter-3 character (e.g. `Char_NGA.asset`) |
  | Phases[0] → Movement Pattern | `Hover` |
  | Phases[0] → Movement Speed | `0` |

  Save (Ctrl+S). The on-disk YAML now reflects the new schema; the old fields are gone.

### §13.5 Create intermission wave configs

**Files:**
- Create: `Assets/ScriptableObjects/Waves/Boss_L5_Intermission1.asset`
- Create: `Assets/ScriptableObjects/Waves/Boss_L5_Intermission2.asset`

- [ ] **Step 13.5.1: Create `Boss_L5_Intermission1.asset`**

  Project window → `Assets/ScriptableObjects/Waves/` → right-click → `Create → Salinlahi → Wave Config`. Name `Boss_L5_Intermission1`. Inspector:

  | Field | Value |
  |---|---|
  | Wave Start Delay | `0.5` |
  | Enemy Count | `3` |
  | Spawn Interval | `1.0` |
  | Enemy Types In Wave → Size | `1` |
  | Enemy Types In Wave [0] | drag `EnemyData_Soldado.asset` |
  | Characters In Wave → Size | `0` (use `EnemyData_Soldado.assignedCharacter` as fallback) |

- [ ] **Step 13.5.2: Create `Boss_L5_Intermission2.asset`**

  Same procedure. Values:

  | Field | Value |
  |---|---|
  | Wave Start Delay | `0.5` |
  | Enemy Count | `5` |
  | Spawn Interval | `0.8` |
  | Enemy Types In Wave [0] | drag `EnemyData_Soldado.asset` |
  | Characters In Wave → Size | `0` |

- [ ] **Step 13.5.3: Wire intermission waves into `BossConfig_ElInquisidor`**

  Open `BossConfig_ElInquisidor.asset`:
  - `Phases[0] → Intermission Wave` → drag `Boss_L5_Intermission1.asset`
  - `Phases[1] → Intermission Wave` → drag `Boss_L5_Intermission2.asset`

  Save (Ctrl+S).

### §13.6 Create the El Inquisidor "summon mid-phase" wave

**Files:**
- Create: `Assets/ScriptableObjects/Waves/Boss_L5_Summon.asset`

This is the wave `SummonWaveOnPhaseStart` triggers during phases 0 and 1 (separate from the post-phase intermissions).

- [ ] **Step 13.6.1: Create the asset**

  Same procedure as §13.5.1. Name `Boss_L5_Summon`. Values:

  | Field | Value |
  |---|---|
  | Wave Start Delay | `1.0` |
  | Enemy Count | `2` |
  | Spawn Interval | `1.5` |
  | Enemy Types In Wave [0] | drag `EnemyData_Soldado.asset` |
  | Characters In Wave → Size | `0` |

- [ ] **Step 13.6.2: Wire into the boss prefab**

  Project window → `[Enemy] Boss_ElInquisidor.prefab` → double-click → root → Inspector → `SummonWaveOnPhaseStart → Wave To Spawn` → drag `Boss_L5_Summon.asset`. Exit Prefab Mode, save.

### §13.7 Commit data assets

- [ ] **Step 13.7.1: Commit**

  ```bash
  git add Assets/ScriptableObjects/EnemyData_Boss_ElInquisidor.asset Assets/ScriptableObjects/BossConfig_ElInquisidor.asset Assets/ScriptableObjects/BossConfig_Superintendent.asset Assets/ScriptableObjects/BossConfig_Kadiliman.asset Assets/ScriptableObjects/Waves/Boss_L5_Intermission1.asset Assets/ScriptableObjects/Waves/Boss_L5_Intermission2.asset Assets/ScriptableObjects/Waves/Boss_L5_Summon.asset "Assets/Prefabs/Enemies/[Enemy] Boss_ElInquisidor.prefab"
  git commit -m "feat(content): SALIN-68 author El Inquisidor + boss config stubs"
  ```

---

## Task 14: Manual Unity — Register the boss prefab in `EnemyPool`

The default `EnemyPool` uses one prefab (`_enemyPrefab`). Boss prefabs must be registered explicitly so `EnemyPool.Get(EnemyDataSO)` resolves the right pool by `enemyID`.

### §14.1 Add the boss to `EnemyPool` registrations

**Files:**
- Modify: the `EnemyPool` Manager prefab (or the live `EnemyPool` GameObject in the Bootstrap scene)

- [ ] **Step 14.1.1: Locate the EnemyPool**

  Project window → search for `Manager_EnemyPool.prefab` (or whatever Bootstrap uses). If the manager is created at runtime in the Bootstrap scene, open `Assets/_Scenes/Bootstrap.unity` and select the `EnemyPool` GameObject. (Confirm by scanning Bootstrap for the singleton instance.)

- [ ] **Step 14.1.2: Add a registration entry**

  Inspector → `EnemyPool → Registered Enemy Prefabs`. Increase size by 1. New entry:

  | Field | Value |
  |---|---|
  | Enemy ID | `elinquisidor` (must match `EnemyData_Boss_ElInquisidor.enemyID`, lowercased) |
  | Prefab | drag `[Enemy] Boss_ElInquisidor.prefab` |
  | Default Capacity | `1` |
  | Max Size | `1` |

  Save (`Ctrl+S` in the scene; if the EnemyPool lives in a prefab, save the prefab and the project).

- [ ] **Step 14.1.3: Commit**

  ```bash
  git add Assets/_Scenes/Bootstrap.unity   # or the manager prefab path
  git commit -m "chore(pool): SALIN-68 register El Inquisidor boss prefab in EnemyPool"
  ```

---

## Task 15: Manual Unity — Wire levels to boss configs

### §15.1 Wire `Level5_Config.asset` to `BossConfig_ElInquisidor`

- [ ] **Step 15.1.1: Open the asset**

  Project window → `Assets/ScriptableObjects/Levels/Level5_Config.asset`. Inspector.

- [ ] **Step 15.1.2: Configure**

  | Field | Value |
  |---|---|
  | Level Name | `Level 5 — El Inquisidor` |
  | Level Number | `5` |
  | Chapter Number | `1` |
  | Waves | leave empty (boss level) |
  | Allowed Characters | size `3`, drag `Char_BA`, `Char_KA`, `Char_GA` (used by intermission/summon adds and the recognition pipeline) |
  | Boss Config | drag `BossConfig_ElInquisidor.asset` |

  Save.

### §15.2 Wire `Level10_Config.asset` to `BossConfig_Superintendent`

- [ ] **Step 15.2.1: Configure**

  Same shape. `Boss Config = BossConfig_Superintendent.asset`. `Allowed Characters` = the chapter-2 single-character used by the stub (matching `Char_A` from §13.3.2). Save.

### §15.3 Wire `Level15_Config.asset` to `BossConfig_Kadiliman`

- [ ] **Step 15.3.1: Configure**

  Same shape. `Boss Config = BossConfig_Kadiliman.asset`. `Allowed Characters` = the chapter-3 character used by the stub. Save.

### §15.4 Commit level wiring

- [ ] **Step 15.4.1: Commit**

  ```bash
  git add Assets/ScriptableObjects/Levels/Level5_Config.asset Assets/ScriptableObjects/Levels/Level10_Config.asset Assets/ScriptableObjects/Levels/Level15_Config.asset
  git commit -m "feat(content): SALIN-68 wire levels 5/10/15 to boss configs"
  ```

---

## Task 16: Manual Unity — Scene wiring for `BossLabelIconRow`

The icon row needs to live on the Gameplay scene's Canvas, with an icon-prefab Image child to instantiate and a reference to the gameplay camera. It also needs the `WaveSpawner` reference assigned to the boss prefab's `SummonWaveOnPhaseStart` (deferred from §12.1.8).

### §16.1 Create the icon prefab

- [ ] **Step 16.1.1: Open the Gameplay scene**

  Project window → `Assets/_Scenes/Gameplay.unity` → double-click.

- [ ] **Step 16.1.2: Create a UI Image template**

  Hierarchy → find the main Canvas (typically `Canvas` under `UI` or `HUD`). Right-click on it → `UI → Image`. Rename to `BossIconTemplate`.

  - RectTransform → size `(32, 32)`.
  - Image → `Source Image = None`, `Color = white`, `Preserve Aspect = checked`.
  - Set the GameObject inactive (uncheck the box at the top of the Inspector). This is the prefab template; clones are activated when added to the row.

- [ ] **Step 16.1.3: Drag `BossIconTemplate` into a prefab**

  Drag the `BossIconTemplate` from Hierarchy into `Assets/Prefabs/UI/` (create the folder if missing). Name `BossIconTemplate.prefab`. Confirm "Original Prefab" when prompted.

  Delete the template from the Hierarchy (the prefab now lives in Assets).

### §16.2 Create the icon-row container

- [ ] **Step 16.2.1: Add an empty GameObject**

  Hierarchy → right-click on the Canvas → `Create Empty`. Name `BossLabelIconRow`. Position the RectTransform anchor preset at `middle center`, anchored position `(0, 0)`, size `(0, 0)` (the script positions it via screen-space tracking).

- [ ] **Step 16.2.2: Add the script**

  Inspector → `Add Component` → search `BossLabelIconRow`. Configure:

  | Field | Value |
  |---|---|
  | Icon Prefab | drag `Assets/Prefabs/UI/BossIconTemplate.prefab`'s `Image` component |
  | Container | drag the RectTransform of `BossLabelIconRow` itself |
  | Icon Size | `32` |
  | Icon Gap | `4` |
  | Drawn Alpha | `0.4` |
  | Drawn Tint | grey `(0.5, 0.5, 0.5, 1)` |
  | Boss World Offset | `(0, 1)` |
  | Gameplay Camera | drag the scene's main camera |

- [ ] **Step 16.2.3: Save the scene**

  Ctrl+S.

### §16.3 Wire the `WaveSpawner` reference into the boss prefab

- [ ] **Step 16.3.1: Open the boss prefab in Prefab Mode**

  Project window → `[Enemy] Boss_ElInquisidor.prefab` → double-click.

- [ ] **Step 16.3.2: Confirm the spawner is auto-resolved at runtime**

  The `SummonWaveOnPhaseStart` component's `Awake` (paste body in §6.2.2) calls `FindFirstObjectByType<WaveSpawner>()` when the serialized `_spawner` slot is empty — Unity prevents prefabs from holding direct scene-object references, so this scene lookup is the supported pattern. Leave the Inspector slot `None` on the prefab. The runtime lookup happens once per boss encounter (acceptable cost).

- [ ] **Step 16.3.3: Exit and save the prefab**

  Click `<` to exit Prefab Mode. Save Project.

### §16.4 Commit scene wiring

- [ ] **Step 16.4.1: Commit**

  ```bash
  git add Assets/_Scenes/Gameplay.unity Assets/Prefabs/UI/BossIconTemplate.prefab "Assets/Prefabs/Enemies/[Enemy] Boss_ElInquisidor.prefab"
  git commit -m "feat(ui): SALIN-68 wire BossLabelIconRow into Gameplay scene"
  ```

---

## Task 17: Manual playtest verification

End-to-end smoke test in the editor. This is where the framework meets reality and the visual feel is validated.

### §17.1 Open the Bootstrap scene

- [ ] **Step 17.1.1: Open**

  `File → Open Scene → Assets/_Scenes/Bootstrap.unity`. Press Play.

### §17.2 Verify El Inquisidor end-to-end

- [ ] **Step 17.2.1: Navigate to Level 5**

  From MainMenu, choose Chapter 1 → Level 5. The Gameplay scene loads.

- [ ] **Step 17.2.2: Observe Intro**

  Within ~2 seconds the boss appears on screen. The icon row is empty during Intro (no `OnBossPhaseStarted` yet). No drawing recognition resolves on the boss yet — try drawing `BA` and confirm the Console says `CombatResolver: No enemy carries BA -- miss` (the boss-route check returns `NotRouted` because `IsTargetable` is false).

- [ ] **Step 17.2.3: Phase 1 (BA)**

  After Intro elapses, `BA` icon appears above the boss. Draw `BA`. Console: `EventBus: OnBossPhaseCleared(0)`. Icon row hides. Adds spawn from the intermission wave (3 Soldados over 3s). Draw their characters to clear them.

- [ ] **Step 17.2.4: Phase 2 (KA, with Pace movement)**

  Adds clear → 1 second delay → boss starts pacing horizontally. `KA` icon appears. The `SummonWaveOnPhaseStart` fires (2 extra Soldados appear mid-phase). Draw `KA`. Phase clears. Second intermission wave (5 Soldados over 4s).

- [ ] **Step 17.2.5: Phase 3 (GA, Teleport)**

  Adds clear → 1 second delay → boss begins teleporting between locations every 1.5s. `GA` icon appears. Draw `GA`. Phase clears. Outro plays for 2.5s. `OnBossDefeated` and `OnLevelComplete` fire. Level Complete screen appears.

- [ ] **Step 17.2.6: Verify hearts unaffected by wrong/duplicate draws**

  Restart Level 5. During phase 1, draw `KA` (not required). Confirm: Hearts unchanged. Draw `BA` (clear phase 1 → intermission). Restart again. During phase 1, draw `BA` then `BA` again. Confirm: First draw clears the phase; second draw raises `OnDrawingFailed` (the toast/SFX you have wired for failed recognition). Hearts unchanged either way.

### §17.3 Verify Level 10 stub plays through

- [ ] **Step 17.3.1: Play Level 10**

  Open Level 10. Boss spawns (using the El Inquisidor prefab visually since we re-used `EnemyData_Boss_ElInquisidor` as the placeholder). Draw the single required character. Boss defeated. Level Complete.

### §17.4 Verify Level 15 stub plays through

- [ ] **Step 17.4.1: Play Level 15**

  Same as §17.3. Confirm Kadiliman's stub plays cleanly.

### §17.5 Verify pool reuse on retry

- [ ] **Step 17.5.1: Beat Level 5 → return to menu → replay Level 5**

  Confirm: the second encounter starts cleanly (no leftover icons, no leftover boss instance, `IsTargetable` not true on Intro). If anything sticks, regress to §5 — `BossController.OnDisable` may not be clearing `GameManager.CurrentBoss` correctly.

### §17.6 Stop play

- [ ] **Step 17.6.1: Stop**

  Stop Play mode. Acceptance criteria from §14 of the spec are satisfied.

---

## Manual Unity configuration — at-a-glance summary

The list below is a quick checklist of every step that requires action **inside the Unity Editor** (i.e., clicks the user must perform that no Edit/Write tool can do for them).

1. **[§1.1.3 / §1.2.3 / §1.3.3]** Compile-checks after data-layer edits. `WaveManager.cs:275` error is expected and resolves at §7.1.
2. **[§5.2.2]** Compile-check after `BossController.cs` lands.
3. **[§7.2.3]** Compile-check after `WaveManager.RunBossEncounter` rewrite — Console must be clean.
4. **[§10.3]** Run all 8 EditMode tests in the Test Runner.
5. **[§11.3.4]** Run the PlayMode `ElInquisidor_*` smoke test.
6. **[§12.1]** Create `[Enemy] Boss_ElInquisidor.prefab` — duplicate Soldier, replace `Enemy` with `BossEnemy`, add `BossController`, `PhaseBasedMovement`, `SummonWaveOnPhaseStart`.
7. **[§13.1]** Author `EnemyData_Boss_ElInquisidor.asset` with `enemyID="elinquisidor"`, `assignedCharacter=null`, `maxHealth=1`.
8. **[§13.2]** Author `BossConfig_ElInquisidor.asset` with 3 phases and intro/outro durations.
9. **[§13.3 / §13.4]** Author `BossConfig_Superintendent.asset` and reshape `BossConfig_Kadiliman.asset` as 1-phase stubs.
10. **[§13.5 / §13.6]** Author 3 wave configs: `Boss_L5_Intermission1`, `Boss_L5_Intermission2`, `Boss_L5_Summon`. Wire intermissions into `BossConfig_ElInquisidor`. Wire summon into the boss prefab's `SummonWaveOnPhaseStart`.
11. **[§14.1]** Register the boss prefab in `EnemyPool` registrations with `enemyID="elinquisidor"`.
12. **[§15.1 / §15.2 / §15.3]** Wire `Level5_Config.asset`, `Level10_Config.asset`, `Level15_Config.asset` to point at their boss configs.
13. **[§16.1 / §16.2]** Create `BossIconTemplate.prefab` and add `BossLabelIconRow` to the Gameplay scene Canvas. Wire its serialized fields.
14. **[§16.3]** Apply the `FindFirstObjectByType<WaveSpawner>()` fallback in `SummonWaveOnPhaseStart.Awake` (prefabs cannot reference scene objects).
15. **[§17]** Manual playtest of Level 5 (full encounter), Level 10/15 (stubs), and pool-reuse on retry.

---

## Notes on architecture and trade-offs

- **Why subclass `Enemy` instead of writing a `BossDamageGate` sibling?** The spec is explicit: `Enemy.TakeDamage` must never be callable on the boss. The cleanest contract enforcement is `override void TakeDamage`. A sibling component would require an `if (HasBossGate) return;` check inside `Enemy.TakeDamage` — coupling that the framework should avoid. The subclass is two methods; nothing more is gained by going wider.
- **Why `BossController` is co-located with `BossEnemy` rather than a top-level scene object.** The boss already needs an `Enemy` for the spawn/pool lifecycle. Putting `BossController` on the same GameObject means the boss's transform = the controller's transform = the ability components' transform. Movement, summon-anchor positions, and icon-row tracking all read one value. An external controller would need a `Transform` reference on top.
- **Why the state machine lives in one coroutine.** State transitions, intro/outro timing, intermission spawn-and-wait, and phase advance all read the same `Config` and `_drawnThisPhase`. A multi-coroutine design would have to coordinate stop/start across them. The single coroutine reads top-down and exits cleanly when `OnDisable` calls `StopCoroutine`.
- **Why `OnDrawnThisPhaseChanged` is local rather than on EventBus.** Two reasons. (a) The icon-row UI needs a controller-instance handle to read `RequiredCharacters` and `DrawnThisPhase`; an `EventBus` event would lose that handle and force the UI to re-resolve `GameManager.CurrentBoss` on every draw. (b) There is exactly one `CurrentBoss` at a time — broadcast to `EventBus` would be wasted work, since only the icon row cares.
- **Why `CurrentBoss` is set inside `StartBoss` rather than `OnEnable`.** At `OnEnable` time the controller has no `Config` — `RequiredCharacters` would return null, and any subscriber that fires immediately on `OnEnable` (e.g., a future analytics hook) would observe an inconsistent state. Tying lifecycle to `StartBoss` ensures `CurrentBoss` is never observable mid-init.
- **Why `BossController.RunEncounter` raises `OnLevelComplete` directly.** The boss is the source of truth for "encounter is over" — `WaveManager` is just a relay. If `WaveManager.CompleteRun()` were called instead, it would race the outro animation. The current shape (boss raises after `outroDuration`, `WaveManager` polls `IsDefeated` and tears down its own state) is sequential by construction.
- **Why `AssignCharacter` is unused on the boss.** The spec mandates `Enemy.Character = null` so the boss is invisible to closest-match; achieving that via `AssignCharacter(null)` would work, but the cleaner approach is the EnemyDataSO's `assignedCharacter = null` (since the boss never carries a character at any point). This avoids one runtime `AssignCharacter` call and keeps "boss never carries a character" expressed as data, not code.
- **Why we don't add an `IBossSpawnDelegate` interface up front.** The spec asks for explicit dependency injection (`StartBoss(config, spawner)`), which we provide. The interface is only needed if `FakeWaveSpawner`'s `new`-shadow fails at runtime (deferred fix in §10.3). Premature interface introduction would be YAGNI; the test runs the contract before complexity grows.

---

## Plan complete. Two execution options:

1. **Subagent-Driven (recommended)** — Dispatch a fresh subagent per task with review between tasks. Use [superpowers:subagent-driven-development](../../../C:/Users/asus/.claude/plugins/cache/superpowers-marketplace/superpowers/5.0.5/skills/subagent-driven-development/SKILL.md) to run.
2. **Inline Execution** — Execute tasks in the current session using [superpowers:executing-plans](../../../C:/Users/asus/.claude/plugins/cache/superpowers-marketplace/superpowers/5.0.5/skills/executing-plans/SKILL.md) with checkpoints between tasks.

Reply with the chosen approach to start.
