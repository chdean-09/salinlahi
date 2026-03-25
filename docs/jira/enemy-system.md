# Epic: Enemy System (SALIN-2)

**Status:** ⚠️ Base Complete — Variants Not Started | **Priority:** High

All enemy behaviour, pooling, wave spawning, and enemy variants (Sprinter, Shielded, Chain, Phaser, Decoy, Zigzagger, Healer, Boss).

---

## SALIN-15 — Implement Enemy.cs Core Component and EnemyMover.cs

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-16, SALIN-37, SALIN-38, SALIN-39 |
| **Blocked By** | SALIN-9, SALIN-10, SALIN-12 |

`Enemy.cs` base MonoBehaviour with `maxHealth`, current health, and `TakeDamage(int)`. On health reaching 0, publishes `EnemyDefeatedEvent` and calls `EnemyPool.Return(this)`. On reaching the shrine collider, publishes `EnemyShrineReachedEvent` and returns to pool. `EnemyMover.cs` moves the enemy downward toward the player shrine at a configurable speed each frame.

**Acceptance Criteria:**
- `Enemy` has `maxHealth`, current health, and `TakeDamage(int amount)` method
- On defeat: publishes `EnemyDefeatedEvent`, calls `EnemyPool.Return(this)`
- On shrine contact: publishes `EnemyShrineReachedEvent`, returns to pool
- `EnemyMover` moves toward a target transform at configurable `speed`
- Manual test: enemy walks toward shrine, takes damage, is defeated and returned to pool without errors

---

## SALIN-16 — Implement EnemyPool.cs, WaveSpawner.cs, and WaveManager.cs

| Field      | Value |
|------------|-------|
| **Status** | In Progress |
| **Assignee** | Clyde |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-30, SALIN-42, SALIN-55, SALIN-68 |
| **Blocked By** | SALIN-12, SALIN-15 |

`EnemyPool.cs` wraps `ObjectPool<Enemy>` and exposes `Spawn(Vector3)` and `Return(Enemy)`. `WaveSpawner.cs` spawns from `EnemyPool` at a configurable interval from designated spawn points. `WaveManager.cs` reads `LevelConfigSO`, sequences waves, and listens for all-enemies-cleared before advancing.

**Remaining work:** Code exists for all three. Needs a QA pass on wave transitions, edge-case where the last enemy is defeated exactly at the screen edge, and confirmation that `OnFinalWaveComplete` event fires correctly (required by SALIN-68).

**Acceptance Criteria:**
- `EnemyPool` exposes `Spawn(Vector3 position)` and `Return(Enemy e)`
- `WaveSpawner` spawns enemies at configurable interval from configurable spawn points
- `WaveManager` reads a `LevelConfigSO` and sequences all waves to completion
- `WaveManager` transitions to next wave only after all enemies of the current wave are cleared
- `OnFinalWaveComplete` event published after last wave of level is cleared
- Manual test: 3-wave Level 1 sequence completes with no null errors or stuck states

---

## SALIN-37 — Implement Sprinter Enemy Variant

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-30 (Level 3+ wave configs) |
| **Blocked By** | SALIN-15 |

Extends `Enemy.cs`. Higher `moveSpeed` driven by a separate `EnemyDataSO` asset. No new mechanics — purely a speed variant. Introduced in Level 3+ wave configs to ramp difficulty.

**Acceptance Criteria:**
- `SpeedEnemy.cs` (or `EnemyDataSO` speed override) makes the Sprinter visibly faster than the base enemy
- `EnemyDataSO` asset `EnemyData_Sprinter` created with elevated `moveSpeed`
- Sprinter correctly returns to pool on defeat and shrine contact
- Can be referenced in `WaveConfigSO` without errors

---

## SALIN-38 — Implement Shielded Enemy Variant (Multi-Hit)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-30 (Level 3+ wave configs) |
| **Blocked By** | SALIN-15 |

Requires 2 correct draws to defeat. First draw removes the shield (visual state change). Second draw defeats the enemy. `ShieldedEnemy.cs` extends `Enemy.cs` and overrides `TakeDamage()` to check shield state before applying damage.

**Acceptance Criteria:**
- First correct draw transitions enemy from `Shielded` to `Unshielded` state (visual change)
- Second correct draw defeats the enemy
- Drawing while shielded does not trigger `EnemyDefeatedEvent` — only the shield-break event
- `EnemyDataSO` asset `EnemyData_Shielded` created
- Shielded enemy works correctly in a wave without breaking pool behaviour

---

## SALIN-39 — Implement Chain Enemy Group System (ChainGroup.cs)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-42 (Level 6+ wave configs) |
| **Blocked By** | SALIN-15, SALIN-16 |

`ChainGroup.cs` manages a linked list of enemies. Defeating one in the chain starts a short countdown (configurable) on the adjacent enemy. The player must draw that enemy's character before the timer expires, or the chain resets. Adds a sequential combo-reading challenge.

**Acceptance Criteria:**
- `ChainGroup` holds an ordered list of `Enemy` references
- Defeating the first enemy in the chain starts a visible countdown timer on the next
- Drawing the next character in sequence before timer expires continues the chain
- Timer expiry resets chain progress (no damage to remaining enemies)
- Chain does not break the pool — all enemies still return to pool on defeat

---

## SALIN-52 — Implement Phaser / Blinker Enemy Variant

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-42 |
| **Blocked By** | SALIN-15 |

Phases in and out of visibility at a configurable interval. Can only be damaged while visible. Drawing while the enemy is invisible publishes `OnDrawingFailed` (no damage applied). A visibility state flag and a timer-based toggle govern behaviour.

**Acceptance Criteria:**
- `PhaserEnemy.cs` toggles between visible and invisible states at configurable interval
- `TakeDamage()` is ignored (no damage, no feedback) when the enemy is invisible
- Drawing while invisible fires `OnDrawingFailed`
- Visibility toggle is driven by a coroutine, not Update (mobile-friendly)
- Enemy still returns to pool correctly on defeat

---

## SALIN-53 — Implement Decoy Enemy Variant

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-42 |
| **Blocked By** | SALIN-15, SALIN-29 |

Displays an incorrect Baybayin character label. Drawing the displayed character has no effect. The player must identify the mismatch and draw the enemy's actual `assignedCharacter` to defeat it. `DecoyEnemy.cs` holds a `fakeCharacter` (`BaybayinCharacterSO`) field separate from the real `assignedCharacter`.

**Acceptance Criteria:**
- `DecoyEnemy` has both `assignedCharacter` (real) and `fakeCharacter` (displayed) fields
- HUD character prompt shows `fakeCharacter` when a Decoy is the target
- Drawing the `fakeCharacter` fires `OnDrawingFailed`
- Drawing the `assignedCharacter` defeats the enemy normally
- `CombatResolver` correctly uses `assignedCharacter` for matching, not `fakeCharacter`

---

## SALIN-54 — Implement Zigzagger and Healer Enemy Variants

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-55 (Level 11+ wave configs) |
| **Blocked By** | SALIN-15 |

**Zigzagger:** Non-linear movement path using a sine-wave horizontal offset. `ZigzagMover.cs` extends `EnemyMover.cs` with a configurable amplitude and frequency.

**Healer:** Periodically restores health to the lowest-health enemy in range. `HealerEnemy.cs` runs a heal coroutine at a configurable interval. Healing stops when the Healer is defeated.

**Acceptance Criteria:**
- `ZigzagMover` produces visible non-linear movement; enemy still reaches shrine if not defeated
- `HealerEnemy` heals the nearest low-health enemy every N seconds (configurable)
- Healer heal coroutine stops cleanly on `TakeDamage` → defeat
- Both enemies return to pool correctly
- Separate `EnemyDataSO` assets created for each variant

---

## SALIN-68 — Implement Boss Encounter System (BossConfigSO + Phase Mechanics)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | High |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-42, SALIN-55 (chapter boss wave configs) |
| **Blocked By** | SALIN-16, SALIN-29, SALIN-27 |

Boss encounter system per GDD §2.4 and TDD §3.2. Each chapter ends with a boss enemy that has multiple phases, each requiring a specific Baybayin character to advance. `BossConfigSO` drives configuration. Boss triggered by `EventBus.OnFinalWaveComplete`.

**Acceptance Criteria:**
- `BossConfigSO` has: `bossName`, `phases` (list of `BossPhase`), `defeatSequence` (array of `BaybayinCharacterSO`)
- Boss enters scene when `EventBus.OnFinalWaveComplete` fires
- Each `BossPhase` has a distinct movement pattern and required character to advance
- Drawing the correct character for the current phase advances the boss to the next phase
- Drawing the wrong character fires `OnDrawingFailed` (no phase advance)
- All phases cleared → `EventBus.OnBossDefeated` → chapter-complete sequence begins

**Req IDs:** GDD-REQ-003, TDD §3.2
