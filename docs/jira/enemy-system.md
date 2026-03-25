# Epic: Enemy System (SALIN-2)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

All enemy behaviour, pooling, wave spawning, and enemy variants (Sprinter, Shielded, Chain, Phaser, Decoy, Zigzagger, Healer).

---

## SALIN-15 — Implement Enemy.cs Core Component and EnemyMover.cs

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `Enemy.cs`, the base MonoBehaviour for all enemies, and `EnemyMover.cs`, which moves the enemy along a straight path toward the player shrine.

**Acceptance Criteria:**
- `Enemy` has `maxHealth`, current health, and `TakeDamage(int amount)` method
- On health reaching 0, publishes `EnemyDefeatedEvent` and calls `EnemyPool.Return(this)`
- On reaching the shrine collider, publishes `EnemyShrineReachedEvent` and returns to pool
- `EnemyMover` moves the enemy at a configurable `speed` toward a target transform each frame
- Manual test: enemy spawned in scene walks toward target, takes damage, is defeated and returned to pool without errors

---

## SALIN-16 — Implement EnemyPool.cs, WaveSpawner.cs, and WaveManager.cs

| Field | Value |
|-------|-------|
| **Status** | In Progress |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Build on `ObjectPool<T>` and `Enemy.cs` to create `EnemyPool.cs`, `WaveSpawner.cs`, and `WaveManager.cs`. WaveManager reads wave data from a ScriptableObject and signals WaveSpawner to release enemies at the configured interval.

**Acceptance Criteria:**
- `EnemyPool` wraps `ObjectPool<Enemy>` and exposes `Spawn(Vector3 position)` and `Return(Enemy e)` methods
- `WaveSpawner` spawns enemies at a configurable spawn point at a configurable interval
- `WaveManager` reads a `WaveDataSO` with `enemyCount`, `spawnInterval`, and `enemyPrefab` fields
- WaveManager transitions to the next wave when all enemies of the current wave are defeated
- Manual test: load a scene, start a wave; enemies spawn, are defeated, wave completes, next wave begins

---

## SALIN-37 — Implement Sprinter Enemy Variant

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-38 — Implement Shielded Enemy Variant (Multi-Hit)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-39 — Implement Chain Enemy Group System (ChainGroup.cs)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-52 — Implement Phaser / Blinker Enemy Variant

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-53 — Implement Decoy Enemy Variant

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-54 — Implement Zigzagger and Healer Enemy Variants

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-68 — Implement Boss Encounter System (BossConfigSO + Phase Mechanics)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement the boss encounter system defined in GDD §2.4 and TDD §3.2. Each chapter ends with a boss enemy that has multiple phases and requires specific Baybayin character sequences to defeat.

**Acceptance Criteria:**
- `BossConfigSO` exists with fields: `bossName`, `phases` (list of BossPhase), `defeatSequence` (BaybayinCharacterSO[])
- Boss enters the scene at the end of the final wave of a chapter (triggered by WaveManager via `EventBus.OnFinalWaveComplete`)
- Each BossPhase has a distinct movement pattern and required character to advance to the next phase
- Drawing the correct character for the current phase advances the boss; drawing wrong triggers `OnDrawingFailed`
- Defeating the boss (all phases cleared) triggers `EventBus.OnBossDefeated` and initiates the chapter-complete sequence

**Req IDs:** GDD-REQ-003, TDD §3.2
