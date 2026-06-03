# 04 — Gameplay Systems
**Project:** Salinlahi
**Version:** 2.3
**Date:** 2026-06-03

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
| `GlyphBadge` child | `EnemyGlyphBadge.cs` | World-space framed Baybayin badge; optional until prefabs are wired (null-safe on `Enemy`) |

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyGlyphBadge.cs]
[EVIDENCE: Assets/Prefabs/Enemies/[Enemy] Standard.prefab]

### 1.1.1 Glyph Badge

`EnemyGlyphBadge` is a world-space component on every enemy prefab (including the boss). It reads `Enemy.VisualCharacter` — the same source the developer debug label uses — so all visual overrides (Kempei scramble, Capitan/Shokan hurt-swap, `ApplyVisualCharacterOverride`) drive it for free. It renders the framed Baybayin glyph via `BaybayinCharacterSO.badgeSprite`.

Three animations live on the badge:

- **Swap** — fires for hurt-swap (`postHurtCharacter`) and the boss's intermediate correct draws within a vulnerable window.
- **Final-Draw** — fires on every `Enemy.Defeat()` and on the boss's terminal correct draw of a window.
- **Decoy Reject** — fires when the player draws a decoy's character (`Enemy.ApplyDecoyPenalty`).

Boss visibility is gated by `BossGlyphVisibilityBinder`, which subscribes to vulnerability events. The boss's `X / N` counter (`BossDrawCounterUI`) anchors its screen position to the badge transform, so per-enemy badge offsets propagate to the counter automatically. The binder ignores the initial `OnDrawnThisPhaseChanged` signal raised when a vulnerability window first samples its expected glyph (no swap before the first player draw), defers `Hide()` while the terminal final-draw animation is still playing (so the seal-broken animation runs to completion), and suppresses fail flashes when the boss is not currently targetable.

The defeat path also drives the badge final-draw for enemies without `deathFrames`: `Enemy.Defeat()` marks the enemy dying, kicks off `PlayFinalDraw()`, and waits for the badge animation to finish before returning to the pool. Decoy rejection (`Enemy.ApplyDecoyPenalty`) marks the decoy dying immediately and disables its contact collider so a second recognized draw of the same character cannot find it as an eligible target and re-trigger the penalty.

Visual tuning lives in `GlyphBadgeConfigSO`. Per-enemy offset/scale overrides live on `EnemyDataSO` (opt-in via `overrideBadgeOffset` / `overrideBadgeScale` toggles). The badge keeps its world-space offset/scale even when the parent's `localScale` changes mid-encounter (e.g. boss collapse squash) by recomputing the inverse-parent-scale compensation in `LateUpdate`.

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyGlyphBadge.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossGlyphVisibilityBinder.cs]
[EVIDENCE: Assets/Scripts/UI/BossDrawCounterUI.cs]

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

### 4.3 Mobile Drawing Capture

`StrokeCapture` uses Unity Input System `EnhancedTouch` callbacks as the authoritative input source. The finger-down sample is recorded and rendered immediately, so every stroke begins at the press position. While a finger is active, `Update()` only catches up unprocessed touch history for that same finger; it is not the primary drawing source.

Capture keeps two point streams:

- Raw points are accepted through `CapturedStroke.AddRawSample()` using `RecognitionConfigSO.rawSampleMinDistancePixels`. These cloned raw points are the only geometry submitted to `RecognitionManager`.
- Visual points are rebuilt from the raw points using `StrokeGeometry.RebuildVisualCurve()`. The curve is rendered through `DrawingCanvas.SetPoints()` and is never fed back into recognition.

Tap-like strokes are rejected by raw path length and raw bounds (`minimumStrokePathLengthPixels`, `minimumStrokeBoundsPixels`) instead of a fixed point-count threshold. Stroke timeout and multi-stroke submission timers use unscaled time so pause/timeScale changes do not stretch or freeze drawing completion.

[EVIDENCE: Assets/Scripts/Gameplay/Recognition/StrokeCapture.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Recognition/CapturedStroke.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Recognition/StrokeGeometry.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Drawing/DrawingCanvas.cs]

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

- At level load: reads `LevelConfigSO` to get ordered `List<WaveDefinition>` (embedded waves).
- For each `WaveDefinition` in order:
  1. Wait `waveStartDelay` seconds.
  2. Fire `EventBus.RaiseWaveStarted(waveIndex)`.
  3. Spawn `enemyCount` enemies at intervals of `spawnInterval` seconds.
  4. Enemy type and character drawn from `WaveDefinition.characters` / `WaveDefinition.enemyTypes` (subsets of the level rosters).
  5. When all enemies in wave are defeated or return to pool: advance to next wave.
- After all waves complete: fire `EventBus.RaiseLevelComplete()`.
- Boss levels (5, 10, 15): when `LevelConfigSO.bossConfig != null`, `WaveManager.RunBossEncounter` activates the boss immediately and the level's `waves` list is ignored. `OnLevelComplete` is raised by `BossController` (not `WaveManager`) when the boss outro finishes.

### 6.2 WaveDefinition Fields Used by WaveManager

| Field | Type | Purpose |
|-------|------|---------|
| `characters` | `List<BaybayinCharacterSO>` | Pool of characters enemies can carry (subset of level roster) |
| `enemyTypes` | `List<EnemyDataSO>` | Pool of enemy types this wave may spawn (subset of level roster) |
| `enemyCount` | `int` | Total enemies spawned in this wave (default: 5) |
| `spawnInterval` | `float` | Seconds between spawns (default: 3f) |
| `waveStartDelay` | `float` | Seconds before first spawn in wave (default: 1f) |

[EVIDENCE: Assets/Scripts/Data/WaveDefinition.cs]
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
| `BossSummonTicker` | Stateless helper. Plays a summon-tell animation on the boss SpriteRenderer, then streams minions one at a time on a per-spawn `delayBetweenMinions` cadence (default 0.6s), applying `summonHorizontalBounds` clamp. Each summon act spawns 2–3 minions as a paced stream, not a single-frame burst. The boss holds its cast pose from the windup through the final spawn and for ~0.3s after, so the stream reads as a deliberate ritual. The number of acts per phase and the gap between acts (`delayBetweenSummons`) are unchanged. |
| `PhaseBasedMovement` | Drives the boss transform per phase movement pattern (Hover/Pace/Teleport). Imperative API: `StartPattern(phase)`, `StopPattern()`, `TeleportNow(phase)` (called by BossController on Teleport ticks). |
| `BossStateVisuals` | Panting bob + red tint during `WindingDown`/`Vulnerable`; collapse animation on entering `Vulnerable`; stand-up tween on exiting `Vulnerable` (`Damaged` or timeout). |
| `BossDamageFeedback` | Two-tier damage feedback: small-hit (per glyph) and emphasized (phase damage). Exposes `IsHurtPaused` and `CriticalColor` consumed by movement and state visuals. |
| `EnemyGlyphBadge` | World-space framed glyph above the boss; same component as regular enemies. |
| `BossGlyphVisibilityBinder` | Shows/hides the badge and drives swap/final-draw/fail-flash during vulnerability windows. |
| `BossAudio` | Subscribes to 9 EventBus boss events in `OnEnable`/`OnDisable`. Resolves `BossAudioBankSO` lazily from `BossConfigSO.audioBank` on `OnBossStarted`. Owns the footstep cadence coroutine (Pace phases only, gated by summon-animation and hurt-pause). Uses no-immediate-repeat random selection for `hitGrowls`, `damagedGrowls`, `footsteps`, and `teleports` pools. Null-tolerant: silently skips all audio if the bank or a specific clip is absent. |

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
| `BossRouteResult.Hit` | Correct glyph during `Vulnerable`; advances queue, samples next expected glyph. **Ordering invariant:** the controller samples the next glyph BEFORE raising `OnDrawnThisPhaseChanged`, so subscribers reading `CurrentExpectedCharacter` in the handler observe the newly sampled glyph (required by `BossDrawCounterUI`). |
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
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossAudio.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/BossEnemy.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Wave/WaveManager.cs — `RunBossEncounter`]

### 8.7 BossAudio Component

`BossAudio` is a `MonoBehaviour` on the boss prefab (sibling of `BossController`). It has no Inspector fields — the audio bank is resolved lazily at runtime.

**EventBus subscriptions (subscribe in `OnEnable`, unsubscribe in `OnDisable`):**

| Event | Handler | Audio Played |
|-------|---------|-------------|
| `OnBossStarted(BossConfigSO)` | `HandleBossStarted` | `FadeInBGM(bank.bgm, bank.bgmFadeInSeconds)` + `PlaySFX(bank.introGrowl)` |
| `OnBossPhaseStarted(int)` | `HandleBossPhaseStarted` | Starts/stops footstep coroutine based on `BossMovementPattern.Pace` |
| `OnBossSummonTick` | `HandleBossSummonTick` | `PlaySFX(bank.summonTick)` |
| `OnBossTeleport` | `HandleBossTeleport` | `PlaySFX(PickNoRepeat(bank.teleports))` |
| `OnBossExhausted(int)` | `HandleBossExhausted` | Stops footsteps + `PlaySFX(bank.bodyFall)` |
| `OnBossDrawHit` | `HandleBossDrawHit` | `PlaySFX(PickNoRepeat(bank.hitGrowls))` |
| `OnBossDamaged(int, int)` | `HandleBossDamaged` | `PlaySFX(PickNoRepeat(bank.damagedGrowls))` |
| `OnBossVulnerabilityExpired(int)` | `HandleBossVulnerabilityExpired` | `PlaySFX(bank.vulnerabilityExpiredLaugh)` |
| `OnBossDefeated` | `HandleBossDefeated` | `PlaySFX(bank.defeat)` + `FadeOutBGM(bank.bgmFadeOutSeconds)` |

**Footstep coroutine:** Active only during `BossMovementPattern.Pace` phases. Fires at `bank.footstepInterval` (default 0.45s). Gated by `BossSummonTicker.IsPlayingSummonAnimation` and `BossDamageFeedback.IsHurtPaused` — no footsteps while the boss is visually still. First step is delayed by one interval after phase start (by design).

**No-immediate-repeat picker:** Variant pools (`hitGrowls`, `damagedGrowls`, `footsteps`, `teleports`) use `Random.Range` with a per-pool `lastIdx` guard so the same clip never plays twice in a row.

**Null-tolerance:** Every handler silently returns if `_bank` is null or the targeted clip field is null. A partially-filled `BossAudioBankSO` does not break gameplay.

**Per-category volume scaling:** Each `PlaySFX`/`FadeInBGM` call passes the matching `*Volume` field from the bank (`bgmVolume`, `introGrowlVolume`, `summonTickVolume`, `bodyFallVolume`, `vulnerabilityExpiredLaughVolume`, `defeatVolume`, `hitGrowlsVolume`, `damagedGrowlsVolume`, `footstepsVolume`, `teleportsVolume`). `AudioManager.PlaySFX(clip, volumeScale)` forwards the scale to `AudioSource.PlayOneShot`, and `AudioManager.FadeInBGM(clip, seconds, volumeScale)` stores it as `_bgmScale` so the BGM volume is computed as `master * bgm * _bgmScale` for the duration of the encounter. `_bgmScale` resets to `1f` on `FadeOutBGM`, `StopBGM`, or `PlayBGM` so non-boss tracks resume at normal level.

[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossAudio.cs]
[EVIDENCE: Assets/Scripts/Data/BossAudioBankSO.cs]

---

## 9. Level Start — Character Unlock Reveal

### 9.1 Overview

When a level begins, any Baybayin characters that are **newly introduced by that level** (i.e., listed in `LevelConfigSO.allowedCharacters` but not yet persisted in `CharacterUnlockProgress`) are revealed to the player one at a time through an animated scroll overlay before waves start. This ensures every character a player may face in combat has been explicitly shown to them.

On replay the entire reveal is skipped: `BuildRevealQueue` filters out already-unlocked characters, producing an empty list, so `Play` is a no-op.

### 9.2 `CharacterUnlockRevealController` (MonoBehaviour)

**Location:** `Assets/Scripts/Gameplay/CharacterUnlockRevealController.cs`

Scene MonoBehaviour placed in the Gameplay scene. Orchestrates the reveal sequence.

| Member | Description |
|--------|-------------|
| `[SerializeField] AlmanacDetailScroll _scroll` | The reused `AlmanacDetailScroll` overlay from the Almanac scene; repurposed here to show the character card. |
| `static List<BaybayinCharacterSO> BuildRevealQueue(IReadOnlyList<BaybayinCharacterSO> allowed, Func<BaybayinCharacterSO, bool> isUnlocked)` | Pure static. Filters `allowed` to characters that are non-null and not yet unlocked (according to `isUnlocked`). Null arguments return an empty list. |
| `IEnumerator Play(IReadOnlyList<BaybayinCharacterSO> toReveal)` | No-op if `_scroll` is null or `toReveal` is empty. Calls `GameManager.Instance.SuppressDrawingInput(true)` at start (finally-guarded). For each character: shows the card via `_scroll.Show(glyph, characterID, description)`, waits for the player to press ✕ (detected via `OnHidden` lambda), calls `CharacterUnlockProgress.TryMarkUnlocked` + `EventBus.RaiseCharacterUnlocked`, waits for the close animation to finish, then proceeds to the next. Calls `SuppressDrawingInput(false)` in `finally`. |

[EVIDENCE: Assets/Scripts/Gameplay/CharacterUnlockRevealController.cs]

### 9.3 Integration into `LevelFlowController`

`LevelFlowController` owns a `[SerializeField] CharacterUnlockRevealController _revealController` reference (resolved via `FindFirstObjectByType` in `EnsureRuntimeReferences` if not wired in the Inspector).

A `private enum RevealTiming { BeforeTutorial, AfterTutorial }` field (Inspector-configurable, default `AfterTutorial`) controls when reveals fire relative to the level tutorial:

```
RunLevelFlow():
  intro dialogue
  GameManager.StartGame()
  ├─ RevealTiming.BeforeTutorial → PlayRevealsIfAny()
  PlayLevelTutorialIfNeeded()
  ├─ RevealTiming.AfterTutorial  → PlayRevealsIfAny()   ← default
  AudioManager plays BGM
  WaveManager.StartLevel()
```

`PlayRevealsIfAny()` is a no-op if `_levelConfig == null` or `_revealController == null`. It calls `BuildRevealQueue(_levelConfig.allowedCharacters, CharacterUnlockProgress.HasUnlocked)` and yields `_revealController.Play(queue)` only when the queue is non-empty.

For non-tutorial levels the tutorial step is itself a no-op, so reveals fire at level start before waves for both timing values.

[EVIDENCE: Assets/Scripts/Gameplay/LevelFlowController.cs]

### 9.4 Drawing Input Suppression

While a reveal scroll is open, `GameManager.SuppressDrawingInput(true)` is called, which makes `AcceptsDrawingInput` return `false` even when `GameState` is `Playing`. This prevents `StrokeCapture` from accepting drawing input during the overlay. `SuppressDrawingInput(false)` is called in a `finally` block so suppression is always lifted, even if the coroutine is interrupted. As an additional safety measure, `GameManager.SetState` resets `_drawingSuppressed = false` whenever state transitions to `GameOver` or `LevelComplete`, preventing a permanently locked input state if the level ends while a reveal is in progress.

[EVIDENCE: Assets/Scripts/Core/GameManager.cs — SuppressDrawingInput, AcceptsDrawingInput]
[EVIDENCE: Assets/Scripts/Gameplay/CharacterUnlockRevealController.cs — Play() finally block]

---

## 10. Aspect-Locked Play Column

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

---

## 11. Boss Tutorial System (SALIN-123)

### 11.1 Overview

When a boss level begins, if `LevelConfigSO.bossConfig.tutorial != null`, a paged "boss tutorial" scroll opens automatically **after** any character-unlock reveals and **before** the boss encounter (before `WaveManager.StartLevel()`). The player reads the boss name + lore on page 1, then pages through mechanic explanations. The red X closes the scroll from any page and starts the encounter. Drawing input is suppressed while the scroll is open.

Non-boss levels and boss levels without a `tutorial` reference are entirely unaffected — no scroll, no errors.

### 11.2 Components

| Script | Location | Role |
|--------|----------|------|
| `BossTutorialSO` | `Assets/Scripts/Data/BossTutorialSO.cs` | Content asset. Holds an ordered `List<BossTutorialPage>`. One asset per boss. |
| `BossTutorialPage` | Same file | `[Serializable]` struct: `title`, `body`, `art` (Sprite, optional). |
| `BossTutorialPaging` | `Assets/Scripts/UI/Boss/BossTutorialPaging.cs` | Pure struct (no Unity deps). Tracks `Index` over a fixed `Count`, exposes `CanGoLeft` / `CanGoRight`, clamps `Next()`/`Prev()`. Fully EditMode-testable. |
| `BossTutorialScroll` | `Assets/Scripts/UI/Boss/BossTutorialScroll.cs` | MonoBehaviour overlay. Wires left/right arrow buttons and close button in `Awake`. `Show(pages)` activates + animates in; close button calls `Close()` (animates out, raises `OnClosed`). Mirrors `AlmanacDetailScroll`'s expand/fade animation using `Time.unscaledDeltaTime`. |
| `BossTutorialController` | `Assets/Scripts/Gameplay/Boss/BossTutorialController.cs` | Scene MonoBehaviour. `Play(BossTutorialSO)` coroutine: shows the scroll, suppresses drawing input, waits for `OnClosed`, waits for close animation, restores drawing input. No-ops gracefully on null / empty / unwired. |

### 11.3 Integration into `LevelFlowController`

`LevelFlowController` owns a `[SerializeField] BossTutorialController _bossTutorialController` reference (resolved via `FindFirstObjectByType<BossTutorialController>` in `EnsureRuntimeReferences` if not wired in the Inspector).

`PlayBossTutorialIfNeeded()` is called after `PlayRevealsIfAny()` and before the BGM / `WaveManager.StartLevel()` block:

```csharp
private IEnumerator PlayBossTutorialIfNeeded()
{
    if (_levelConfig == null
        || _levelConfig.bossConfig == null
        || _levelConfig.bossConfig.tutorial == null)
        yield break;  // Not a boss level or no tutorial configured.

    if (_bossTutorialController == null)
    {
        DebugLogger.LogWarning("...");  // Waves still start.
        yield break;
    }

    yield return _bossTutorialController.Play(_levelConfig.bossConfig.tutorial);
}
```

### 11.4 Scene Wiring (Gameplay.unity)

1. Duplicate `CharacterUnlockRevealScroll` → rename `BossTutorialScroll` → swap `AlmanacDetailScroll` for `BossTutorialScroll`; wire `_art`, `_title`, `_body`, `_canvasGroup`, `_panel`, `_closeButton`; add `LeftArrow`/`RightArrow` buttons and `PageIndicator` text.
2. Create empty GameObject `BossTutorialController` → add `BossTutorialController` component → wire `_scroll` → `BossTutorialScroll`.
3. On `LevelFlowController`, wire `Boss Tutorial Controller` → the `BossTutorialController` object.
4. On each `BossConfigSO`, set the `Tutorial` field to the matching `BossTutorialSO` asset.

### 11.5 Drawing Input Suppression

Same mechanism as `CharacterUnlockRevealController` (§9.4): `GameManager.SuppressDrawingInput(true/false)` wrapped in a `finally` block ensures suppression is always lifted even if the coroutine is interrupted.

[EVIDENCE: Assets/Scripts/Data/BossTutorialSO.cs]
[EVIDENCE: Assets/Scripts/UI/Boss/BossTutorialPaging.cs]
[EVIDENCE: Assets/Scripts/UI/Boss/BossTutorialScroll.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossTutorialController.cs]
[EVIDENCE: Assets/Scripts/Gameplay/LevelFlowController.cs — PlayBossTutorialIfNeeded]
[EVIDENCE: Assets/Tests/Editor/Boss/BossTutorialPagingTests.cs]
[EVIDENCE: Assets/Tests/Editor/Boss/BossTutorialSOTests.cs]
[EVIDENCE: Assets/Tests/Editor/Boss/BossTutorialControllerTests.cs]
