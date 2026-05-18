# PR #50 Code Review — SALIN-39: Chapter 3 Enemy Variants

- **Date:** 2026-04-25
- **PR:** [chdean-09/salinlahi#50](https://github.com/chdean-09/salinlahi/pull/50)
- **Jira:** SALIN-39
- **Branch:** `feature/SALIN-39-kempei-sandbox-validation`
- **Range:** `0c43558..69aa11b` (origin/main → branch HEAD)
- **Reviewer:** superpowers:code-reviewer (dispatched via `/superpowers:requesting-code-review`)

## Scope Reviewed

Chapter 3 (Japanese-era) enemy variant set:

- **Kisha (Sprinter)** — data-driven Walk → Pause → Charge via `KishaMover` reading `chargeMultiplier`, `chargeTriggerYNormalized`, and `pauseDuration` from `EnemyDataSO`.
- **Kempei (Censor)** — visual-only label scrambling for enemies inside `scrambleRadius`, sourced from the level's `allowedCharacters`, restored on Kempei defeat or pool return.
- **Shokan (Elite)** — shielded multi-hit enemy reusing the existing `EnemyDataSO.maxHealth = 2` Capitan path, plus a corruption veil overlay removed on the first valid hit.
- Shared enemy runtime hooks: `Enemy.ApplyVisualCharacterOverride` / `ClearVisualCharacterOverride`, `HealthChanged` event, mover extensibility.
- Sandbox + wave wiring: sandbox validation flow, level-character discovery, enemy asset discovery.
- Three new SO assets, three prefabs, scene/pool wiring, and supporting animation/meta assets.

## AC Coverage

| AC | Status | Evidence |
|---|---|---|
| **AC-1** KishaMover Walk → Pause → Charge from EnemyDataSO | Met | [KishaMover.cs:59-89](../../../Assets/Scripts/Gameplay/Enemy/KishaMover.cs#L59-L89) — `ChargeRoutine` reads `chargeTriggerYNormalized`, `pauseDuration`, `chargeMultiplier` from `_enemy.Data`. |
| **AC-2** Kisha charge reaches shrine; mid-charge defeat returns to pool | Not verifiable from diff | Code path is sound on inspection — `ChargeRoutine` only sets speed; defeat goes through shared `Enemy.Defeat → ReturnToPool`. PR explicitly notes no device validation. No automated coverage. |
| **AC-3** Scramble pulls only from `allowedCharacters`, never matches real | **Partial** | [KempeiScrambleController.cs:172-176](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs#L172-L176) falls back to other active enemies' real characters when `allowedCharacters` produces no candidates. Strict reading of AC-3 forbids this. Harmless when `allowedCharacters.Count >= 2`, but a degenerate single-character config exposes it. |
| **AC-4** CombatResolver matches by real `assignedCharacter` | Met | [CombatResolver.cs:25,67](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs#L25) and [ActiveEnemyTracker.cs:72,98](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs#L72) read `e.Character`, never `VisualCharacter`. |
| **AC-5** Revert on defeat OR pool return, same frame | Met | [KempeiScrambleController.cs:30-33](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs#L30-L33) — `OnDisable → ClearAffectedEnemies()` clears every override synchronously. Pool-return path triggers `OnDisable` via `gameObject.SetActive(false)`; defeat also routes through pool return. |
| **AC-6** Scramble does not affect boss icons or HUD tray | Met (implicit) | Architecturally only `Enemy` instances receive overrides via `ApplyVisualCharacterOverride`; HUD/boss icon rows aren't `Enemy`. Brittle without an explicit comment — any future feature mirroring `VisualCharacter` to HUD would silently break this. |
| **AC-7** Shokan uses `maxHealth = 2` shielded path, no new health field | Met | [EnemyData_Shokan.asset:17](../../../Assets/ScriptableObjects/EnemyData_Shokan.asset#L17) → `maxHealth: 2`. Reuses existing `ShouldTriggerShieldBreak` / `TakeDamage` path on `Enemy`. |
| **AC-8** First hit removes veil; second hit defeats | Met | [ShokanCorruptionVeil.cs:45-53](../../../Assets/Scripts/Gameplay/Enemy/ShokanCorruptionVeil.cs#L45-L53) — `RefreshVeil` shows veil only when `currentHealth >= maxHealth`. Driven by `HealthChanged`. Veil resets on next pool get because `Enemy.Initialize` fires `HealthChanged` at full HP ([Enemy.cs:138](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L138)). |
| **AC-9** Three SO assets with correct configs | Met | `EnemyData_Kisha.asset`, `EnemyData_Kempei.asset`, `EnemyData_Shokan.asset` all present with correct `enemyID`, `maxHealth`, walk frames. `assignedCharacter: {fileID: 0}` is intentional — wired at runtime by `WaveSpawner`. |
| **AC-10** Pool return on defeat AND base-hit, no Instantiate/Destroy | Met | `Enemy.Defeat → ReturnToPool → pool.Return`. `EnemyMover.OnTriggerEnter2D(PlayerBase) → pool.Return`. `EnemyPool` only `Instantiate`s on prefab create (`createFunc`), never per-spawn. |
| **AC-11** Register on pool get, unregister on pool return | Met | `Enemy.Initialize → ActiveEnemyTracker.Register` ([Enemy.cs:135](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L135)). `EnemyPool.Return → ActiveEnemyTracker.Unregister` ([EnemyPool.cs:160](../../../Assets/Scripts/Gameplay/Enemy/EnemyPool.cs#L160)). Single Register / single Unregister. |
| **AC-12** Single Unregister path (TICKET-16 fix) | Met | [Enemy.cs:243-248](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L243-L248) — `Defeat()` no longer calls `Unregister` directly. Only call site is `EnemyPool.Return:160`. The double-call from TICKET-16 is gone. |

**Summary:** 11 of 12 ACs cleanly met. AC-3 partial (fallback violates "from `allowedCharacters`"). AC-2 not automated.

## Strengths

- **Variant logic isolated cleanly.** `KishaMover`, `KempeiScrambleController`, `ShokanCorruptionVeil` keep variant-specific behavior off the shared `Enemy` while `Enemy` remains the single combat/health/label authority.
- **Source-keyed override map.** [Enemy.cs:38](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L38) — `_labelOverrides : Dictionary<object, BaybayinCharacterSO>` keys by source so overlapping Kempei sources cannot stomp each other.
- **Real vs visual character cleanly separated.** [Enemy.cs:42-43](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L42-L43) — `Character` (real) vs `VisualCharacter` (display).
- **AC-12 fix landed.** `Enemy.Defeat()` no longer double-calls `Unregister`; `EnemyPool.Return` is the single tracker-unregister site.
- **KishaMover coroutine lifecycle is correct.** Started in `OnEnable`, cancelled in `OnDisable`, restarted on `SetSpeed`. `yield return null` defers reading `_enemy.Data` until after `Initialize` populates it.
- **Shokan veil reset on pool reuse.** `Enemy.Initialize` fires `HealthChanged(currentHealth, currentHealth)` so the veil reapplies on every fresh pool get without extra wiring.
- **Sandbox is build-gated.** Every sandbox path wraps in `#if UNITY_EDITOR || SALINLAHI_SANDBOX`. No leakage into production builds.
- **No leftover `Debug.Log`.** Variant scripts use `DebugLogger.*`. Raw `Debug.Log` calls are confined to editor-only `OnValidate` and pre-existing dev tools.
- **Prefab GUIDs match SO assets.** No SALIN-55-style placeholder regression.
- **Stability test exists.** `SandboxModeTests.KempeiKeepsScrambledCharacterStableWhileTargetRemainsAffected` ([SandboxModeTests.cs:441](../../../Assets/Editor/SandboxModeTests.cs#L441)) directly exercises the override-key + `IsWrongCharacter` path.

## Issues

### Critical (Must Fix)

None. Tracker symmetry, EventBus symmetry, pooling, and AC-12 paths are correct.

### Important (Should Fix)

1. **AC-3 fallback can leak non-allowed characters.** [KempeiScrambleController.cs:172-176](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs#L172-L176). When `allowedCharacters` produces no "wrong" candidate, the controller falls back to other enemies' real characters. Strict reading of AC-3 forbids this. Either drop the fallback (skip scrambling that target this frame) or explicitly document the fallback as accepted scope.

2. **No automated tests for `KishaMover` or `ShokanCorruptionVeil`.** Kempei has one happy-path test; Kisha and Shokan state machines are entirely unverified. The `#if UNITY_INCLUDE_TESTS` hook at [KishaMover.cs:111-113](../../../Assets/Scripts/Gameplay/Enemy/KishaMover.cs#L111-L113) is wired but unused. Suggest at minimum:
   - `KishaMover.ChargeStateForTests` advances Walking → Paused → Charging when the trigger Y is reached.
   - Shokan: `Initialize → TakeDamage(1)` → veil disabled; second `TakeDamage(1)` → `EnemyPool.Return` called and veil reset on next `Get`.
   - Kempei: scramble a target → return Kempei to pool → assert target's `VisualCharacter == Character` in the same frame (covers the AC-5 base-hit pool-return path explicitly).

3. **AC-6 (boss/HUD exclusion) is met only implicitly.** `KempeiScrambleController` only iterates `ActiveEnemyTracker` and writes through `Enemy.ApplyVisualCharacterOverride`, so HUD UI and boss icon rows are never touched today. Brittle — any future feature mirroring `VisualCharacter` to HUD would silently break AC-6. Add a doc comment on `Enemy.ApplyVisualCharacterOverride` clarifying that overrides are per-`Enemy`-only.

4. **`KishaMover` instant-charge fallback on missing camera.** [KishaMover.cs:96-105](../../../Assets/Scripts/Gameplay/Enemy/KishaMover.cs#L96-L105) — `HasReachedTriggerY` returns `true` if `Camera.main` is null. During a brief frame when `Camera.main` is still resolving (scene load edge), Kisha skips Walk/Pause and charges instantly. The "fail-instant-charge" behavior is the worst-possible fail-mode (makes the enemy harder). Safer to return `false` so Kisha keeps walking, or hold Walk until the camera resolves with a retry cap.

5. **`WaveManager.CurrentAllowedCharacters` is a static mutable property.** [WaveManager.cs:14](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L14). Already typed as `IReadOnlyList<BaybayinCharacterSO>` (good — consumers can't mutate). Two latent concerns:
   - Two simultaneous `WaveManager`s (additive scene load, domain reload) would clobber each other. Not a current scenario but worth a note.
   - `CurrentAllowedCharacters = _levelConfig.allowedCharacters` exposes the SO's live list reference; if anything mutates that list, `KempeiScrambleController` sees it live. The `IReadOnlyList` property type prevents consumer-side mutation, so this is mostly hypothetical.

### Minor (Nice to Have)

1. **Stale buffers in `KempeiScrambleController.OnDisable`.** [KempeiScrambleController.cs:30-33](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs#L30-L33) — `_activeSnapshot` and `_candidateCharacters` aren't cleared. Next reuse is safe (the first `Update` overwrites), but worth clearing for hygiene/debuggability.

2. **Magic numbers in glitch interval.** `MinGlitchInterval = 0.18f`, `MaxGlitchInterval = 0.36f` ([KempeiScrambleController.cs:7-8](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs#L7-L8)). Per CLAUDE.md, gameplay tuning is data-driven via SO. Consider promoting to `EnemyDataSO` so designers can tune censor cadence per level.

3. **Cross-variant default values on SOs.** `EnemyData_Kempei.asset` carries Kisha-only fields (`chargeMultiplier`, `chargeTriggerYNormalized`, `pauseDuration`); `EnemyData_Kisha.asset` carries `scrambleRadius`; `EnemyData_Shokan.asset` carries both. Cosmetic — runtime ignores them. If keeping `EnemyDataSO` shared, fine; alternatively warn in `OnValidate` on misuse.

4. **Per-frame allocation in `RemoveUnaffectedEnemies`.** [KempeiScrambleController.cs:91](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs#L91) — allocates a `List<Enemy>` only when there's something to clear, but could be eliminated by reusing a member buffer. Per-frame GC pressure if many targets leave the radius simultaneously.

5. **`SandboxController` uses `FindFirstObjectByType` at startup.** [SandboxController.cs:50,71,253](../../../Assets/Scripts/Debug/Sandbox/SandboxController.cs#L50). Sandbox-only path, doesn't hit production budgets, but the SALIN-89 trend is registration patterns. Consider a static `SandboxController.Instance`.

6. **Naming drift on `_legacyDefaultEnemyData`.** [WaveManager.cs:14](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L14). `[FormerlySerializedAs]` is correct for migration, but the field name reads "deprecated, do not assign" while `OnValidate` still warns when missing. Either retire the field and remove the validator, or rename to `_fallbackEnemyData`.

## Recommendations

- Add EditMode tests for `KishaMover` charge state transitions and `ShokanCorruptionVeil` veil-on-pool-reuse before merge — both are state machines with subtle Initialize/OnEnable ordering. The `#if UNITY_INCLUDE_TESTS` hook is already wired.
- Document on `Enemy.ApplyVisualCharacterOverride` that overrides are per-`Enemy`-only and must never be propagated to HUD/boss UI consumers, to harden AC-6 against future regressions.
- Decide on the AC-3 fallback at [KempeiScrambleController.cs:172-176](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs#L172-L176): drop it (strict AC compliance) or document and bound it.
- Promote Kempei glitch interval constants to `EnemyDataSO` for designer tunability.
- Resolve the `_legacyDefaultEnemyData` naming/validator dissonance in `WaveManager`.

## Assessment

**Ready to merge: With one fix.**

**Reasoning:** Variant code is well-isolated, AC-12 is correctly fixed, pooling/tracker symmetry is preserved, and sandbox paths are properly build-gated. The one strict AC deviation (AC-3 fallback) needs a decision — fix or document. The missing Kisha/Shokan automation tests leave non-trivial state machines unverified, but the PR's sandbox-mode validation is acceptable interim coverage; a follow-up ticket for automated tests is recommended.

## Files Reviewed

- [Assets/Scripts/Gameplay/Enemy/Enemy.cs](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs)
- [Assets/Scripts/Gameplay/Enemy/KishaMover.cs](../../../Assets/Scripts/Gameplay/Enemy/KishaMover.cs)
- [Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs](../../../Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs)
- [Assets/Scripts/Gameplay/Enemy/ShokanCorruptionVeil.cs](../../../Assets/Scripts/Gameplay/Enemy/ShokanCorruptionVeil.cs)
- [Assets/Scripts/Gameplay/Enemy/EnemyMover.cs](../../../Assets/Scripts/Gameplay/Enemy/EnemyMover.cs)
- [Assets/Scripts/Gameplay/Enemy/EnemyPool.cs](../../../Assets/Scripts/Gameplay/Enemy/EnemyPool.cs)
- [Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs)
- [Assets/Scripts/Gameplay/Wave/WaveManager.cs](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs)
- [Assets/Scripts/Gameplay/Wave/WaveSpawner.cs](../../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs)
- [Assets/Scripts/Gameplay/Combat/CombatResolver.cs](../../../Assets/Scripts/Gameplay/Combat/CombatResolver.cs)
- [Assets/Scripts/Data/EnemyDataSO.cs](../../../Assets/Scripts/Data/EnemyDataSO.cs)
- [Assets/Scripts/Data/LevelConfigSO.cs](../../../Assets/Scripts/Data/LevelConfigSO.cs)
- [Assets/Scripts/Debug/Sandbox/SandboxController.cs](../../../Assets/Scripts/Debug/Sandbox/SandboxController.cs)
- [Assets/Editor/SandboxModeTests.cs](../../../Assets/Editor/SandboxModeTests.cs)
- [Assets/ScriptableObjects/EnemyData_Kisha.asset](../../../Assets/ScriptableObjects/EnemyData_Kisha.asset)
- [Assets/ScriptableObjects/EnemyData_Kempei.asset](../../../Assets/ScriptableObjects/EnemyData_Kempei.asset)
- [Assets/ScriptableObjects/EnemyData_Shokan.asset](../../../Assets/ScriptableObjects/EnemyData_Shokan.asset)
- `Assets/Prefabs/Enemies/[Enemy] Kisha.prefab`
- `Assets/Prefabs/Enemies/[Enemy] Kempei.prefab`
- `Assets/Prefabs/Enemies/[Enemy] Shokan.prefab`
