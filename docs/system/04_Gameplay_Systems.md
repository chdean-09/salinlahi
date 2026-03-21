# 04 — Gameplay Systems
**Project:** Salinlahi
**Version:** 1.0
**Date:** 2026-03-19
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
| Enemy prefab | `_enemyPrefab` | Must be assigned: `[Enemy] Standard.prefab` |
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

### 4.2 NOT FOUND — Required for Full Combat

| Requirement | Specified In | Status |
|-------------|-------------|--------|
| $P recognizer matches drawn strokes to `characterID` | TDD §3.3; Salinlahi.md §3.3.3 | NOT FOUND |
| `RecognitionManager` fires `OnCharacterRecognized` | TDD §3.3 | NOT FOUND |
| `WaveManager` listens to `OnCharacterRecognized` and calls `Enemy.Defeat()` on matched enemy | TDD §3.3 | NOT FOUND |
| `HeartSystem` decrements hearts on `OnBaseHit`; fires `OnGameOver` at 0 | TDD §3.3; GDD §2.3 | NOT FOUND |
| Combo counter tracks consecutive correct drawings | TDD §3.3 | NOT FOUND |
| AOE burst mechanic (3+ same-character enemies on screen) | TDD §3.3 | NOT FOUND |

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

### 5.3 Heart System Specification (PLANNED)

- Default heart count: **3**
- Hearts lost: **1 per base hit**
- `HeartSystem` must fire `EventBus.RaiseHeartsChanged(currentHearts)` on every decrement
- `HeartSystem` must fire `EventBus.RaiseGameOver()` when `currentHearts == 0`

[EVIDENCE: docs/capstone/GDD.md, §2.3 Win/Lose Conditions]
[EVIDENCE: docs/capstone/TDD.md, §3.3 Combat Resolution]

---

## 6. Wave Progression Logic (PLANNED)

### 6.1 WaveManager Specification

`WaveManager` is specified in `TDD.md §3.2` but **has no implementation file**. The following is derived from source documents only and is marked as planned/unimplemented.

- At level load: reads `LevelConfigSO` to get ordered `List<WaveConfigSO>`.
- For each `WaveConfigSO` in order:
  1. Wait `waveStartDelay` seconds.
  2. Fire `EventBus.RaiseWaveStarted(waveIndex)`.
  3. Spawn `enemyCount` enemies at intervals of `spawnInterval` seconds.
  4. Enemy type and character drawn from `WaveConfigSO.charactersInWave`.
  5. When all enemies in wave are defeated or return to pool: advance to next wave.
- After all waves complete: fire `EventBus.RaiseLevelComplete()`.
- Boss levels (5, 10, 15): after final wave, activate boss via `BossConfigSO`.

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

The following enemy types are specified in `EnemyDataSO.enemyID` comments, the GDD §4.3, and the Team README §9.

| Enemy ID | Movement | Special Rule | First Appears | Priority | Sprint Target |
|----------|----------|--------------|--------------|----------|--------------|
| `"standard"` | Straight, `moveSpeed = 1.5f` | None | Level 1 | Must Ship | Sprint 1 ✓ |
| `"fast"` | Straight, 1.3× base speed | None | Level 2 | Must Ship | Sprint 3 |
| `"chain"` | Straight, linked word formation | Must defeat front-to-back in order | Level 3 | Should Ship | Sprint 3 |
| `"shielded"` | Straight | Requires 2 correct drawings to defeat (`hitsRequired = 2`) | Level 6 | Should Ship | Sprint 3 |
| `"sprinter"` | Pauses mid-screen, then rushes toward base | None | Level 7 | Should Ship | Sprint 3 |
| `"phaser"` | Straight | Blinks displayed character on and off | Level 8 | Nice to Have | Sprint 4 |
| `"decoy"` | Straight | Damages player if killed; must be ignored | Level 11 | Nice to Have | Sprint 4 |
| `"zigzagger"` | Sine wave lateral pattern while descending | None | Level 12 | Nice to Have | Sprint 4 |
| `"healer"` | Straight | Restores 1 heart when defeated | Level 13 | Nice to Have | Sprint 4 |

[EVIDENCE: Assets/Scripts/Data/EnemyDataSO.cs — enemyID comment: `"standard", "fast", "chain"`]
[EVIDENCE: docs/capstone/GDD.md, §4.3 Enemies — full roster]
[EVIDENCE: Team README §9 — Enemy Type Roster with introduction levels]
