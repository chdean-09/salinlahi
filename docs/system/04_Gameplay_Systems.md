# 04 — Gameplay Systems
**Project:** Salinlahi
**Version:** 1.5
**Date:** 2026-05-24
**Owner:** Gameplay Developer (Jon Wayne Cabusbusan / Chad Andrada)

---

## 1. Enemy Lifecycle

### 1.1 Prefab Structure

| Component | Script | Requirement |
|-----------|--------|------------|
| Root `GameObject` | `Enemy.cs` | `[RequireComponent(typeof(EnemyMover))]` |
| `SpriteRenderer` | (Unity built-in) | Set to `walkFrames[0]` on initialize |
| `Collider2D` | (Unity built-in, 2D trigger) | Required by `EnemyMover` |
| Movement | `EnemyMover.cs` | `[RequireComponent(typeof(Collider2D))]` |

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs]
[EVIDENCE: Assets/Prefabs/Enemies/[Enemy] Standard.prefab]

### 1.2 Lifecycle States

```
Pool (inactive, SetActive(false))
  │
  ├─ EnemyPool.Get(EnemyDataSO data)
  │     └─ pool.Get() → OnGet: SetActive(true)
  │     └─ Enemy.Initialize(data, pool)
  │           ├─ _data = data
  │           ├─ _mover.SetSpeed(data.moveSpeed)  → _active = true
  │           └─ _renderer.sprite = data.walkFrames[0]
  │
  ├─ [Active in scene — EnemyMover.Update() moves enemy down]
  │
  ├─ PATH A: Player draws correct character
  │     └─ Enemy.Defeat()
  │           ├─ EventBus.RaiseEnemyDefeated(Character)
  │           └─ Enemy.ReturnToPool() → pool.Release(this) → OnRelease: SetActive(false)
  │
  └─ PATH B: Enemy reaches PlayerBase trigger
        └─ EnemyMover.OnTriggerEnter2D() [tag: "PlayerBase"]
              ├─ EventBus.RaiseBaseHit()
              └─ Enemy.ReturnToPool() → pool.Release(this) → OnRelease: SetActive(false)

OnDisable() called on either path:
  └─ StopAllCoroutines()
  └─ _mover.Stop() → _active = false
```

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs, Initialize(), Defeat(), ReturnToPool(), OnDisable()]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs, OnTriggerEnter2D()]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyPool.cs]

### 1.3 Public Enemy API

| Member | Type | Description |
|--------|------|-------------|
| `Character` | `BaybayinCharacterSO` (get) | Baybayin character this enemy carries; sourced from `_data.assignedCharacter` |
| `EnemyID` | `string` (get) | Enemy type identifier; sourced from `_data.enemyID` |
| `Initialize(EnemyDataSO, IObjectPool<Enemy>)` | method | Called by EnemyPool; sets data and speed |
| `Defeat()` | method | Raises defeated event and returns to pool |
| `ReturnToPool()` | method | Returns to pool without raising event; used on base-hit path |

---

## 2. Movement Logic — `EnemyMover.cs`

### 2.1 Movement Contract

- **Direction:** `Vector2.down` in `Space.World` (portrait orientation; top-to-bottom).
- **Speed:** `float _speed`, set by `Enemy.Initialize()` via `SetSpeed(float)`.
- **Active flag:** `bool _active`. Set to `true` by `SetSpeed()`; set to `false` by `Stop()`.
- `Stop()` is called from `Enemy.OnDisable()`, guaranteeing movement halts when enemy is deactivated.

```csharp
// Update loop (frame-rate-independent)
transform.Translate(Vector2.down * _speed * Time.deltaTime, Space.World);
```

### 2.2 Base Collision

`OnTriggerEnter2D` fires when the enemy's `Collider2D` intersects any other collider. Only the `"PlayerBase"` tag triggers the base-hit path. All other collisions are silently ignored.

**Requirement:** The PlayerBase `GameObject` must have tag `"PlayerBase"` assigned in the Unity Inspector.
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs — `CompareTag("PlayerBase")`]

### 2.3 Speed Default

`EnemyDataSO.moveSpeed` defaults to `1.5f` world units per second.
[EVIDENCE: Assets/Scripts/Data/EnemyDataSO.cs — `public float moveSpeed = 1.5f`]

---

## 3. Object Pooling — `EnemyPool.cs`

### 3.1 Pool Configuration

| Parameter | Inspector Field | Default |
|-----------|----------------|---------|
| Enemy prefab registry | `_enemyPrefab` | Per-`enemyID` prefab registry — each `EnemyDataSO.enemyID` maps to its prefab. Boss prefab `[Enemy] Boss_ElInquisidor.prefab` is registered alongside regular enemies. |
| Default capacity | `_defaultCapacity` | `10` |
| Maximum size | `_maxSize` | `20` |
| Collection check | Hardcoded `false` | Avoids runtime overhead in builds |

### 3.2 Pool Operations

| Operation | Method | Effect |
|-----------|--------|--------|
| Get enemy | `EnemyPool.Get(EnemyDataSO)` | Retrieves from pool → `SetActive(true)` → `Enemy.Initialize()` |
| Return enemy | `Enemy.ReturnToPool()` | `pool.Release(this)` → `SetActive(false)` |
| Pool overflow | Unity ObjectPool internal | Calls `OnDestroyEnemy()` → `Destroy(gameObject)` only when max exceeded |
| Create new | `CreateEnemy()` (internal) | `Instantiate(_enemyPrefab)` → `SetActive(false)` |

**Rule:** No `Instantiate` or `Destroy` call is permitted in the gameplay loop. All enemy creation and destruction must go through `EnemyPool`.

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyPool.cs]
[EVIDENCE: Assets/Scripts/Utilities/ObjectPool.cs — `PooledObject<T>` base]

---

## 4. Combat Resolution

### 4.1 Implemented (Sprint 1)

| Step | Evidence |
|------|----------|
| Enemy carries `BaybayinCharacterSO` | `Enemy.Character` property; `EnemyDataSO.assignedCharacter` |
| Enemy defeat raises `OnEnemyDefeated` | `Enemy.Defeat()` → `EventBus.RaiseEnemyDefeated()` |
| Audio plays on defeat | `AudioManager.OnEnemyDefeated` → `PlayPronunciationClip()` |
| Base hit raises `OnBaseHit` | `EnemyMover.OnTriggerEnter2D()` → `EventBus.RaiseBaseHit()` |
| GameOver triggered | `GameManager.HandleGameOver()` on `OnGameOver` |

### 4.2 Implemented — Full Combat Systems

| Requirement | Specified In | Implementation |
|-------------|-------------|----------------|
| $P recognizer matches drawn strokes to `characterID` | TDD §3.3; Salinlahi.md §3.3.3 | `DollarPRecognizer.cs` |
| `RecognitionManager` fires `OnCharacterRecognized` | TDD §3.3 | `RecognitionManager.cs` |
| `WaveManager` listens to `OnCharacterRecognized` and calls `Enemy.Defeat()` on matched enemy | TDD §3.3 | `WaveManager.cs` |
| `HeartSystem` decrements hearts on `OnBaseHit`; fires `OnGameOver` at 0 | TDD §3.3; GDD §2.3 | `HeartSystem.cs` |
| Combo counter tracks consecutive correct drawings | TDD §3.3 | `ComboManager.cs` |
| AOE burst mechanic (3+ same-character enemies on screen) | TDD §3.3 | `CombatResolver.cs` |
| Combo system: 5-streak triggers focus mode slow effect on all enemies | GDD §3.2; Team README §9 | `ComboManager.cs` |

[EVIDENCE: docs/capstone/TDD.md, §3.3 Combat Resolution]

---

## 5. Win and Lose Conditions

### 5.1 Story Mode

| Condition | Trigger | Outcome |
|-----------|---------|---------|
| Win | All waves in a level cleared without hearts reaching 0 (boss levels: boss also defeated) | `EventBus.RaiseLevelComplete()` → `GameState.LevelComplete` |
| Lose | Shrine (PlayerBase) loses all 3 hearts | `EventBus.RaiseGameOver()` → `GameState.GameOver` → `SceneLoader.LoadGameOver()` |

### 5.2 Endless Mode

| Condition | Trigger |
|-----------|---------|
| No win condition | Game runs until hearts reach 0 |
| Score | Based on waves survived, enemies defeated, and longest combo |

### 5.3 Heart System Specification

- Default heart count: **3**
- Hearts lost: **1 per base hit**
- `HeartSystem` must fire `EventBus.RaiseHeartsChanged(currentHearts)` on every decrement
- `HeartSystem` must fire `EventBus.RaiseGameOver()` when `currentHearts == 0`

[EVIDENCE: docs/capstone/GDD.md, §2.3 Win/Lose Conditions]
[EVIDENCE: docs/capstone/TDD.md, §3.3 Combat Resolution]

---

## 6. Wave Progression Logic

### 6.1 WaveManager Specification

`WaveManager` is implemented in `Assets/Scripts/Gameplay/Wave/WaveManager.cs`. The following describes its behavior per `TDD.md §3.2`.

- At level load: reads `LevelConfigSO` to get ordered `List<WaveConfigSO>`.
- For each `WaveConfigSO` in order:
  1. Wait `waveStartDelay` seconds.
  2. Fire `EventBus.RaiseWaveStarted(waveIndex)`.
  3. Spawn `enemyCount` enemies at intervals of `spawnInterval` seconds.
  4. Enemy type and character drawn from `WaveConfigSO.charactersInWave`.
  5. When all enemies in wave are defeated or return to pool: advance to next wave.
- After all waves complete: fire `EventBus.RaiseLevelComplete()`.
- Boss levels (5, 10, 15): when `LevelConfigSO.bossConfig != null`, `WaveManager.RunBossEncounter` activates the boss immediately and the level's `waves` list is ignored. `OnLevelComplete` is raised by `BossController` (not `WaveManager`) when the boss outro finishes.

### 6.2 WaveConfigSO Fields Used by WaveManager

| Field | Type | Purpose |
|-------|------|---------|
| `waveID` | `string` | Unique identifier for debug logging |
| `waveNumber` | `int` | Display index in HUD |
| `charactersInWave` | `List<BaybayinCharacterSO>` | Pool of characters enemies can carry |
| `enemyCount` | `int` | Total enemies spawned in this wave (default: 5) |
| `spawnInterval` | `float` | Seconds between spawns (default: 3f) |
| `waveStartDelay` | `float` | Seconds before first spawn in wave (default: 1f) |

[EVIDENCE: Assets/Scripts/Data/WaveConfigSO.cs]
[EVIDENCE: docs/capstone/TDD.md, §3.2 Wave Management]
[EVIDENCE: docs/capstone/GDD.md, §2.4 Game Modes]

---

## 7. Enemy Types (Content Specification)

The following enemy types are specified in the GDD §4.3 and the Team README §9. Enemies are organized by historical era with three tiers per era: Regular (32×32), Variant (32×32, unique mechanic), and Elite (48×48). Bosses are 64×64.

### 7.1 Spanish Era (Chapter 1)

| Enemy ID | Tier | Movement / Behavior | First Appears | Priority | Status |
|----------|------|---------------------|--------------|----------|--------|
| `"soldado"` | Regular | Walks straight down at base speed | Level 1 | Must Ship | Implemented (prefab + SO) |
| `"fraile"` | Variant | Phaser: Baybayin label fades in and out on a timer. Player must memorize the character. Robe glides smoothly. | Level 2 | Must Ship | PLANNED |
| `"guardia"` | Variant | Fast: moves at 1.5× Soldado speed | Level 3 | Must Ship | PLANNED |
| `"capitan"` | Elite (48×48) | Shielded: requires 2 correct drawings (`maxHealth = 2`). First hit breaks visible armor. Moves at 0.7× speed. | Level 4 | Must Ship | PLANNED |

### 7.2 American Era (Chapter 2)

| Enemy ID | Tier | Movement / Behavior | First Appears | Priority | Status |
|----------|------|---------------------|--------------|----------|--------|
| `"soldier"` | Regular | Walks straight at base speed | Level 6 | Must Ship | Implemented (prefab + SO) |
| `"maestro"` | Variant | Decoy: displays a Baybayin character but drawing it PENALIZES the player (lose 1 heart). Must be ignored. Visually subtly warmer than real enemies. | Level 7 | Should Ship | Implemented (prefab + SO) |
| `"pensionado"` | Variant | Zigzag: moves in a sine wave pattern while descending | Level 8 | Should Ship | Implemented (prefab + SO) |
| `"general"` | Elite (48×48) | Commander: while alive, all nearby American enemies move 1.3× faster. General moves slowly (0.7×). Kill the General to remove the speed buff. | Level 9 | Should Ship | Implemented (prefab + SO) |

### 7.3 Japanese Era (Chapter 3)

| Enemy ID | Tier | Movement / Behavior | First Appears | Priority | Status |
|----------|------|---------------------|--------------|----------|--------|
| `"heitai"` | Regular | Walks straight but inherently 1.2× faster than Soldado/Soldier | Level 11 | Must Ship | Implemented (prefab + SO) |
| `"kisha"` | Variant | Sprinter: walks normally, pauses briefly, then charges at 2.5× speed | Level 12 | Should Ship | Implemented (prefab + SO) |
| `"kempei"` | Variant | Censor: while alive, scrambles the Baybayin labels on all nearby enemies to show wrong characters. Kill Kempei first to restore correct labels. | Level 13 | Should Ship | Implemented (prefab + SO) |
| `"shokan"` | Elite (48×48) | Shielded + Corruption Veil: requires 2 hits like Capitan, plus all three era corruption colors swirl around sprite creating visual noise. | Level 14 | Should Ship | Implemented (prefab + SO) |

**Legacy placeholder SOs (no prefab yet):** `EnemyData_Shielded.asset`, `EnemyData_Sprinter.asset`, `EnemyData_Boss.asset` exist in `Assets/ScriptableObjects/` but have no live prefab — retained for sandbox/test wiring only.

### 7.4 Bosses (64×64)

| Boss ID | Era | Level | Mechanic | Status |
|---------|-----|-------|----------|--------|
| `"el_inquisidor"` | Spanish | 5 | Phase-based. Can summon Soldado reinforcements during phases. | Implemented (`[Enemy] Boss_ElInquisidor.prefab`) |
| `"superintendent"` | American | 10 | Phase-based. Decree ability temporarily scrambles nearby Baybayin labels. | PLANNED |
| `"kadiliman"` | Final | 15 | Phase-based. Formless shadow entity. Summons enemies from all three eras. Drawing all 17 characters defeats it. | PLANNED |

[EVIDENCE: Assets/Prefabs/Enemies/ — Soldado, Soldier, Heitai, Maestro, Pensionado, General, Kisha, Kempei, Shokan, Boss_ElInquisidor]
[EVIDENCE: Assets/ScriptableObjects/EnemyData_*.asset]
[EVIDENCE: docs/capstone/GDD.md, §4.3 Enemies — full era-themed roster]
[EVIDENCE: Team README §9 — Enemy Type Roster with introduction levels and historical context]

---

## 8. Boss Encounter System (Implemented)

The Spanish-era boss (`el_inquisidor`) is implemented as a self-contained phase-based encounter. The boss is excluded from the regular pool/AOE/closest-match logic and is damaged only through an authoritative routing call from `CombatResolver` into `BossController`.

### 8.1 Components on the Boss Prefab

| Script | Role |
|--------|------|
| `BossEnemy` (extends `Enemy`) | `IsBoss => true` so `CombatResolver` excludes it; `TakeDamage` is a no-op (warns) so only `BossController.TryRouteDraw` can damage the boss. `OnEnable` reasserts `SpriteRenderer.sortingOrder = RenderOrder.Boss`. |
| `BossController` | Authoritative state machine. Owns phases, vulnerability, HP, and outro. Raises all boss events. |
| `BossSummonTicker` | Stateless helper. Plays a summon-tell animation on the boss SpriteRenderer, then spawns 2–3 minions per tick at the boss's CURRENT position, applying `summonHorizontalBounds` clamp. |
| `PhaseBasedMovement` | Drives the boss transform per phase movement pattern (Hover/Pace/Teleport). Imperative API: `StartPattern(phase)`, `StopPattern()`, `TeleportNow(phase)` (called by BossController on Teleport ticks). |
| `BossStateVisuals` | Panting bob + red tint during `WindingDown`/`Vulnerable`; collapse animation on entering `Vulnerable`; stand-up tween on exiting `Vulnerable` (`Damaged` or timeout). |
| `BossDamageFeedback` | Two-tier damage feedback: small-hit (per glyph) and emphasized (phase damage). Exposes `IsHurtPaused` and `CriticalColor` consumed by movement and state visuals. |

### 8.2 State Machine

`BossController.State` enum: `Idle, Intro, SummoningPhase, WindingDown, Vulnerable, Damaged, Outro, Defeated`.

Per-phase loop: `SummoningPhase → WindingDown → Vulnerable → (if not damaged) SummoningPhase`. The loop repeats until the player completes the `Vulnerable` window. On success the controller transitions to `Damaged`, deducts one HP, and advances to the next phase — or to `Outro` on the final phase.

### 8.3 Vulnerability Window Contract

During `Vulnerable`, the controller samples a random glyph from `LevelConfigSO.allowedCharacters` and exposes it via `CurrentExpectedCharacter` / `CurrentExpectedCharacterID`. The window ends when either:

- `CorrectDrawsThisWindow >= phase.requiredCharacterCount` — success → `Damaged` (HP lost, raises `OnBossDamaged`).
- `vulnerabilityTimer` elapses — failure → repeat the phase, **no HP loss**, raises `OnBossVulnerabilityExpired(phaseIndex)`.

### 8.4 Draw Routing

`CombatResolver` consults the boss BEFORE its AOE / closest-match logic.

| `BossController.TryRouteDraw(characterID)` result | Meaning |
|------|---------|
| `BossRouteResult.NotRouted` | Boss not targetable; caller (`CombatResolver`) falls through to AOE/closest-match |
| `BossRouteResult.Hit` | Correct glyph during `Vulnerable`; advances queue, samples next expected glyph. **Ordering invariant:** the controller samples the next glyph BEFORE raising `OnDrawnThisPhaseChanged`, so subscribers reading `CurrentExpectedCharacter` in the handler observe the newly sampled glyph (required by `BossGlyphQueueUI`). |
| `BossRouteResult.WrongGlyph` | Incorrect glyph during `Vulnerable`; consumed (no fall-through), raises `OnDrawingFailed` |

### 8.5 Boss Spawn Integration

`WaveManager.RunBossEncounter(BossConfigSO)` runs when `LevelConfigSO.bossConfig != null`. It calls `WaveSpawner.SpawnBossEnemy(config.bossEnemyData)` (centers the boss horizontally and uses `_bossSpawnPoint.y` if assigned), then `BossController.StartBoss(config, spawner)`. `WaveManager` does NOT raise `OnLevelComplete` for boss levels — the boss controller itself raises both `OnBossDefeated` and `OnLevelComplete` from its `RunOutro` coroutine.

### 8.6 Damage Model

Boss HP equals `BossConfigSO.phases.Count`. There is **no separate `maxHealth`**. Every phase clear deducts exactly 1 HP (`HPRemaining--`). `OnBossDamaged(phaseIndex, hpRemaining)` fires after every phase clear, including the final one (which then transitions to `Outro`).

[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossController.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossSummonTicker.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossStateVisuals.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossDamageFeedback.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/BossEnemy.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Wave/WaveManager.cs — `RunBossEncounter`]

---

## 9. Aspect-Locked Play Column

All gameplay rendering, input, and HUD anchoring are constrained to a fixed 9:16 play column so the game behaves identically on phone and tablet aspect ratios.

- `AspectLockedCamera` (on Main Camera, `[ExecuteAlways]`) enforces a 9:16 play column regardless of device aspect: it adjusts `orthographicSize` per device aspect and exposes `PlayColumnWorldRect`, `PlayColumnScreenRect`, `WorldHalfWidth`, `WorldHalfHeight`, and an `OnPlayAreaChanged` event.
- Reference resolution: 360×640 at PPU 32 → world width 11.25, world height 20.
- `PlayAreaContainer` resizes a `RectTransform` under the gameplay Canvas to cover `PlayColumnScreenRect` so HUD anchors resolve to the play-column corners.
- `BaseZoneScaler` resizes the base-zone fence `SpriteRenderer` to span the play column width (uses `SpriteRenderer.size` for Sliced/Tiled, `transform.localScale.x` for Simple sprites, plus `_overflowPerSide` padding to avoid pillar seams).
- `DrawingCanvas` clamps input screen positions to `PlayColumnScreenRect` so strokes can't drift into the pillared margins on tablets.
- `RenderOrder` centralizes sorting order constants: `EnemyDefault=0`, `Boss=10`, `BossSummon=15`, `EnemyDebugLabel=500`, `DrawingStroke=1000`, `LoadingCanvas=9000`, `SandboxOverlay=9500`.

[EVIDENCE: Assets/Scripts/Gameplay/Camera/AspectLockedCamera.cs]
[EVIDENCE: Assets/Scripts/UI/PlayAreaContainer.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Environment/BaseZoneScaler.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Drawing/DrawingCanvas.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Rendering/RenderOrder.cs]
