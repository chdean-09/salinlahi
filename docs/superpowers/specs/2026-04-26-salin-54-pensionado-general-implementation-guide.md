# SALIN-54 — Implement Pensionado (Zigzagger) and General (Commander) Enemy Variants

Two American-era (Chapter 2) enemy variants. Both are **data-driven via `EnemyDataSO`** —
no Enemy subclasses, no per-variant inheritance trees. Movement quirks live on small
sibling components attached to the prefab:

- **Pensionado (Zigzagger)** descends toward the shrine with a non-linear sine-wave
  horizontal offset. Amplitude and frequency are read from `EnemyDataSO`.
- **General (Commander)** moves at a configurable base-speed multiplier (default 0.7×).
  While alive, it applies a configurable speed buff (default 1.3×) to every American-era
  non-boss enemy within an `auraRadius` proximity. On defeat, the buff is removed from
  every affected enemy in the same frame.

Each variant ships with a **walk animation** sprite sheet and a **defeat (death)
animation** sprite sheet. The death sheet plays once on `Enemy.Defeat()` before the
GameObject returns to the pool — a small refactor of `Enemy.cs` and two new
serialized fields on `EnemyDataSO` make this data-driven and reusable for future
variants.

The variants ship through edits to existing files (`Enemy.cs`, `EnemyDataSO.cs`,
`EnemyPool` Inspector, two prefab folders, four sprite-sheet imports) and three
new C# files (`PensionadoMover.cs`, `GeneralAura.cs`, plus the two SO assets, two
prefabs, four walk/death sprite sheets). Nothing is global state — the aura is a
proximity effect with an explicit per-enemy buff stack on `Enemy`.

Unity reference version: **Unity 6 LTS (6000.3.9f1)**. Every menu path below uses that
version's exact menu text.

---

## Acceptance criteria → section map

| AC | Where it is satisfied |
|---|---|
| AC-1 Pensionado visibly zigzags and still reaches the shrine if undefeated | [§2.4](#24-create-pensionadomover) (mover) + [§2.6](#26-author-the-enemy_pensionado-and-enemy_general-prefabs) (prefab) + [§2.10.2](#2102--test-1--pensionado-zigzag-and-shrine-reach) verification |
| AC-2 `zigzagAmplitude` and `zigzagFrequency` configurable on `EnemyDataSO` | [§2.3](#23-extend-enemydataso-with-variant-fields) field block |
| AC-3 General base-speed multiplier (default 0.7×) read from `EnemyDataSO` | [§2.2](#22-add-speed-buff-api-effectivespeed-and-death-animation-playback-to-enemy) (`EffectiveSpeed` includes `baseSpeedMultiplier`) + [§2.3](#23-extend-enemydataso-with-variant-fields) (`baseSpeedMultiplier`) + [§2.8](#28-author-the-enemydata_pensionado-and-enemydata_general-scriptableobjects) (asset value `0.7`) |
| AC-4 American-era enemies in radius move at buffed multiplier (default 1.3×) | [§2.5](#25-create-generalaura) tick body + [§2.8](#28-author-the-enemydata_pensionado-and-enemydata_general-scriptableobjects) (`auraSpeedMultiplier = 1.3`) + [§2.10.3](#2103--test-2--general-aura-applies-buff-to-american-era-enemies) verification |
| AC-5 On General defeat, buff is removed in the same frame | [§2.5](#25-create-generalaura) `OnDisable` clears every entry in `_affected` + [§2.10.4](#2104--test-3--general-defeat-removes-the-buff-immediately) verification |
| AC-6 Aura does not affect non-American enemies or bosses | [§2.5](#25-create-generalaura) `era` and `IsBoss` skips + [§2.10.5](#2105--test-4--era-isolation) and [§2.10.6](#2106--test-5--bosses-are-skipped) |
| AC-7 Both variants return to pool on defeat / base-hit and register with `ActiveEnemyTracker` | Inherited from existing `Enemy.Defeat → ReturnToPool` + `EnemyMover.OnTriggerEnter2D` paths. Verified in [§2.1](#21-verify-enemydefeat-single-unregister-already-correct) and [§2.10.7](#2107--test-6--pool-and-tracker-cleanup-for-both-variants) |
| AC-8 Separate SO assets exist: `EnemyData_Pensionado`, `EnemyData_General` | [§2.8](#28-author-the-enemydata_pensionado-and-enemydata_general-scriptableobjects) |
| Death animation plays before pool return (no AC; design feature) | [§2.2](#22-add-speed-buff-api-effectivespeed-and-death-animation-playback-to-enemy) Defeat refactor + [§2.10.8](#2108--test-7--death-animation-plays-before-pool-return) verification |

---

## Phase 0 audit snapshot

Recorded against the current working tree (branch `dev`). Do not re-confirm each step;
the table below is the authoritative API surface this guide depends on.

| Symbol | Location | Status |
|---|---|---|
| `Enemy.Defeat()` | [Assets/Scripts/Gameplay/Enemy/Enemy.cs](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L233) | exists, lines 233–238. **Already single-unregister** via `ReturnToPool → EnemyPool.Return → ActiveEnemyTracker.Unregister`. No bug to fix in §2.1. **Refactored** in §2.2.4 to optionally play `_data.deathFrames` before the pool return. |
| `Enemy.TakeDamage(int)` | same, line 167 | exists. **Edited** in §2.2.5 with a single-line `if (_isDying) return;` re-entry guard. |
| `Enemy.Data` (returns `EnemyDataSO`) | same, line 41 | exists |
| `Enemy.Character` | same, line 39 | exists |
| `Enemy.IsBoss` | same, line 45 | exists as `public virtual bool IsBoss => false;` (placeholder; SALIN-68 replaces with `BossController`-backed override). **Do not modify.** |
| `Enemy.MaxHealth` | same | **NOT declared** — added by §2.2 |
| `Enemy.EffectiveSpeed` / `ApplySpeedBuff` / `ClearSpeedBuff` / `_speedBuffs` | same | **NOT declared** — added by §2.2 |
| `Enemy.Initialize(EnemyDataSO)` line 115 calls `_mover.SetSpeed(_data.moveSpeed);` | same | exists. **Replaced** by §2.2 with `_mover.SetSpeed(EffectiveSpeed)` so `baseSpeedMultiplier` applies on spawn. |
| `Enemy.ResetForPool()` | same, lines 145–165 | exists. **Edited** by §2.2 to add `_speedBuffs.Clear()`, reset `_isDying`, stop the death coroutine, and re-enable the contact collider. |
| `Enemy.Update()` line 263–266 | same | calls `AdvanceWalkAnimation()` only. **Does not write `transform.position`.** PensionadoMover can write X without conflict. |
| `Enemy.AdvanceWalkAnimation()` line 274–302 | same | reads `_data.walkFrames` (the `Sprite[]` you assign in §2.8) and steps through them at `_walkAnimationFps` (default 8). No Animator Controller is needed. |
| `EnemyMover.Update()` line 37–42 | [Assets/Scripts/Gameplay/Enemy/EnemyMover.cs](../../../Assets/Scripts/Gameplay/Enemy/EnemyMover.cs#L37) | uses `transform.Translate(Vector2.down * finalSpeed * Time.deltaTime, Space.World)`. Translates **Y only**; X is free for PensionadoMover. |
| `EnemyMover.SetSpeed(float)` | same, line 15 | exists. Reused by §2.2 to push `EffectiveSpeed` after every buff change. |
| `EnemyMover.OnTriggerEnter2D` | same, line 44 | reads `Collider2D` on the same GameObject. §2.2.4 disables the collider on Defeat to silence base-hit triggers during the death animation. |
| `ActiveEnemyTracker.GetActiveEnemiesSnapshot()` | [Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs](../../../Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs#L44) | exists, returns a fresh `List<Enemy>` copy — safe for `GeneralAura` to iterate while it mutates buffs. |
| `ActiveEnemyTracker.Register / Unregister` | same, lines 26–42 | exists. Pool path wires both. §2.2.4 also calls `Unregister` directly at the start of the death animation so the corpse cannot be re-targeted. |
| `EnemyPool._registeredEnemyPrefabs` | [Assets/Scripts/Gameplay/Enemy/EnemyPool.cs](../../../Assets/Scripts/Gameplay/Enemy/EnemyPool.cs#L35) | serialized `List<EnemyPrefabRegistration>` with fields `enemyID`, `prefab`, `defaultCapacity`, `maxSize`. New variants are registered via the **EnemyPool component Inspector** in the Bootstrap scene. |
| `EnemyPool.ResolvePoolState` matches on `enemyID.Trim().ToLowerInvariant()` | same, lines 184–204 | the `enemyID` string on each `EnemyDataSO` must match an entry's `enemyID` field, case-insensitive. Falls back to default pool with a warning if unmatched. |
| `EnemyDataSO` fields | [Assets/Scripts/Data/EnemyDataSO.cs](../../../Assets/Scripts/Data/EnemyDataSO.cs) | currently `enemyID`, `moveSpeed`, `maxHealth`, `walkFrames`, `animatorController`, `assignedCharacter`, `isDecoy`, `dealsContactDamage`. **None** of the §2.3 additions are present. |
| `EnemyDataSO` CreateAssetMenu | same, line 3 | `[CreateAssetMenu(fileName = "EnemyData", menuName = "Salinlahi/Enemy Data")]`. New SO assets are authored via `Create → Salinlahi → Enemy Data`. |
| Existing `EnemyData_*.asset` files | [Assets/ScriptableObjects/](../../../Assets/ScriptableObjects/) | flat directory (no `Enemies/` subfolder). Files: `EnemyData_Soldado`, `EnemyData_Sprinter`, `EnemyData_Shielded`, `EnemyData_Heitai`, `EnemyData_Kempei`, `EnemyData_Kisha`, `EnemyData_Maestro`, `EnemyData_Soldier`, `EnemyData_Shokan`, `EnemyData_Boss`. New assets sit alongside these. |
| Existing enemy prefabs | [Assets/Prefabs/Enemies/](../../../Assets/Prefabs/Enemies/) | nine `[Enemy] X.prefab` files. American-era infantry: `[Enemy] Soldier.prefab` (sister to `EnemyData_Soldier`). New prefabs follow the **CLAUDE.md `Enemy_[Type].prefab` convention**: `Enemy_Pensionado.prefab`, `Enemy_General.prefab`. |
| Existing walk-animation sprite sheet (Maestro) | [Assets/Animations/Enemy/Maestro/sprite_enemy_maestro_walk-Sheet.png](../../../Assets/Animations/Enemy/Maestro/sprite_enemy_maestro_walk-Sheet.png) | reference convention for §2.7. 4 horizontal frames, ~26×32 each, **Pixels Per Unit = 6**, **Filter Mode = Bilinear**, **Sprite Mode = Multiple**. |
| `EnemyData_Maestro.walkFrames` array | [Assets/ScriptableObjects/EnemyData_Maestro.asset](../../../Assets/ScriptableObjects/EnemyData_Maestro.asset) | reference wiring for §2.8. Four entries, each pointing at one slice of the Maestro sheet via sub-asset reference. `animatorController` is null — sprite cycling is code-driven by `Enemy.AdvanceWalkAnimation`. |
| `GameManager.Instance.CurrentState` and `GameState.Playing` | [Assets/Scripts/Core/GameManager.cs](../../../Assets/Scripts/Core/GameManager.cs) | exists and is the gating signal used by both new movers (so they pause in `Paused` / `GameOver`). |

> **NOTE** The legacy bracket naming `[Enemy] X.prefab` predates the
> `Enemy_[Type].prefab` convention documented in `CLAUDE.md`. New prefabs follow the
> documented form. A future cleanup ticket can rename the legacy nine.

> **NOTE on the death animation** Until §2.2 lands, `Enemy.Defeat()` deactivates the
> GameObject the same frame — the enemy disappears instantly. After §2.2, if
> `EnemyDataSO.deathFrames` is empty the behaviour is unchanged (no animation,
> immediate pool return), so existing variants without death art keep working. The
> animation is opt-in per variant via the SO's `deathFrames` array.

---

## §2.1  Verify `Enemy.Defeat` single-Unregister (already correct)

Earlier drafts of this guide said `Enemy.Defeat()` "double-unregisters" by calling
`ActiveEnemyTracker.Unregister` directly **and** through `ReturnToPool`. The current
file does not. This is a **verify-only** step — no edit, no commit.

### §2.1.1  Open the file and confirm the body

- (a) **Project** window → navigate to `Assets/Scripts/Gameplay/Enemy/`. Double-click
  `Enemy.cs` to open it in your external editor.
- (b) Use Ctrl+F to jump to `public void Defeat(`. Confirm lines 233–238 read exactly:

```csharp
public void Defeat()
{
    BaybayinCharacterSO capturedCharacter = Character;
    ReturnToPool();
    EventBus.RaiseEnemyDefeated(capturedCharacter);
}
```

- (c) Confirm there is no direct `ActiveEnemyTracker.Instance.Unregister(this)` call
  inside `Defeat()`. Use Ctrl+F for `Unregister` — the only matches in `Enemy.cs`
  appear inside `Initialize` error branches (lines 79, 90, 100), which run when init
  fails and the enemy is being abandoned. They are correct.
- (d) Open `EnemyPool.cs` (`Assets/Scripts/Gameplay/Enemy/EnemyPool.cs`). Confirm
  `EnemyPool.Return(Enemy)` calls `ActiveEnemyTracker.Instance?.Unregister(enemy);`
  exactly once (line 160). This is the canonical unregister path used by
  `Defeat → ReturnToPool → EnemyPool.Return`.
- (e) Close both files without saving.

> **NOTE** No commit. Any future code path that ends an enemy's life should call
> `ReturnToPool()` (not `Unregister` directly). If you grep for new direct
> `Unregister` calls in life-ending paths, treat that as a bug and remove them.

---

## §2.2  Add speed-buff API, `EffectiveSpeed`, and death-animation playback to `Enemy`

`GeneralAura` (§2.5) needs a way to apply and remove a speed multiplier to nearby
enemies without clobbering buffs from other sources (e.g. a future second General).
A small `Dictionary<object, float>` keyed by the source component lets multiple buffs
coexist; `EffectiveSpeed` multiplies them together with the data-side
`baseSpeedMultiplier` (used by the General to be intrinsically slow).

`EnemyMover` reads from its own cached `_speed` field set via `SetSpeed(float)`.
So whenever the buff dictionary changes, `Enemy` must push the new `EffectiveSpeed`
into the mover.

This section also introduces a **death-animation playback path** in `Enemy.Defeat()`:
when `EnemyDataSO.deathFrames` is non-empty, the enemy unregisters from the tracker
immediately, freezes in place, plays the death frames once, and only then returns to
the pool. `EventBus.RaiseEnemyDefeated` fires on the kill stroke (so combo / SFX
respond instantly); only the GameObject deactivation is delayed.

### §2.2.1  Open the file and add the imports

- (a) **Project** window → `Assets/Scripts/Gameplay/Enemy/Enemy.cs` → double-click.
- (b) Confirm line 1 reads `using UnityEngine;`. Lines 5–6 import `TMPro` and
  `UnityEngine.Pool`. There is no `System.Collections` or
  `System.Collections.Generic` import yet.
- (c) Position the cursor at the end of line 1 and press Enter. On the new line 2
  type:

```csharp
using System.Collections;
using System.Collections.Generic;
```

- (d) Save (Ctrl+S). Lines 1–3 now read:

```csharp
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
```

> **NOTE** `System.Collections` is needed for the non-generic `IEnumerator` type
> used by Unity coroutines. `System.Collections.Generic` is needed for the
> `Dictionary<object, float>` speed-buff field. Without them the file will not
> compile and the Console will flag `CS0246: 'IEnumerator' could not be found` /
> `CS0246: 'Dictionary<,>' could not be found` after the next save.

### §2.2.2  Add the speed-buff fields and the public API

- (a) Use Ctrl+F to jump to the field declaration cluster around lines 29–37 (the
  block ending in `private float _walkFrameTimer;`). The next visible declaration
  on line 39 is the `Character` property.
- (b) Position the cursor at the end of line 37 (the `_walkFrameTimer` line) and
  press Enter twice to leave a blank line.
- (c) On the new blank line type:

```csharp
private readonly Dictionary<object, float> _speedBuffs = new Dictionary<object, float>();
```

- (d) Use Ctrl+F to jump to the public-property cluster on lines 39–45. After the
  `IsBoss` placeholder on line 45 (`public virtual bool IsBoss => false;`) press
  Enter and paste:

```csharp
public int MaxHealth => _data != null ? _data.maxHealth : 0;

public float EffectiveSpeed
{
    get
    {
        if (_data == null) return 0f;
        float speed = _data.moveSpeed * _data.baseSpeedMultiplier;
        foreach (var kv in _speedBuffs) speed *= kv.Value;
        return speed;
    }
}

public void ApplySpeedBuff(object source, float multiplier)
{
    _speedBuffs[source] = multiplier;
    PushSpeedToMover();
}

public void ClearSpeedBuff(object source)
{
    if (_speedBuffs.Remove(source))
        PushSpeedToMover();
}

private void PushSpeedToMover()
{
    if (_mover != null) _mover.SetSpeed(EffectiveSpeed);
}
```

- (e) Save (Ctrl+S).

> **NOTE on the dictionary key** Using `object` as the key (typically `this` from a
> caller's perspective) lets every buff source own its own slot. `GeneralAura` calls
> `enemy.ApplySpeedBuff(this, multiplier)` so each General has a unique slot keyed by
> its own component instance. Two Generals' buffs compose multiplicatively, which
> matches the design intent — twice the influence around two Generals.

### §2.2.3  Add the death-animation fields

The death pipeline needs two new private fields: an `_isDying` flag (re-entry
guard) and a `_deathRoutine` reference (so `ResetForPool` can stop it if the pool
force-clears the enemy mid-animation).

- (a) Ctrl+F to the speed-buff field you just added in §2.2.2(c)
  (`private readonly Dictionary<object, float> _speedBuffs ...`).
- (b) Press Enter at the end of that line to leave a fresh line beneath it.
- (c) Type the two new fields:

```csharp
private bool _isDying;
private Coroutine _deathRoutine;
```

- (d) The four-line group should now read:

```csharp
private readonly Dictionary<object, float> _speedBuffs = new Dictionary<object, float>();
private bool _isDying;
private Coroutine _deathRoutine;
```

- (e) Save (Ctrl+S).

### §2.2.4  Refactor `Defeat()` to play the death animation before returning to pool

- (a) Ctrl+F to `public void Defeat()`. The current body is the four-line block
  shown in §2.1.1 step (b).
- (b) **Replace the entire `Defeat()` method** with the new body below. Triple-click
  the opening brace line to select the line, then shift-down-arrow to select the
  whole method through the closing `}`, then paste:

```csharp
public void Defeat()
{
    if (_isDying) return;

    BaybayinCharacterSO capturedCharacter = Character;
    bool hasDeathAnimation = _data != null
        && _data.deathFrames != null
        && _data.deathFrames.Length > 0;

    if (hasDeathAnimation)
    {
        // Slow path: freeze, unregister, fire the event immediately, then play frames.
        _isDying = true;
        ActiveEnemyTracker.Instance?.Unregister(this);
        _mover?.Stop();
        DisableContactCollider();
        EventBus.RaiseEnemyDefeated(capturedCharacter);
        _deathRoutine = StartCoroutine(PlayDeathAnimationThenReturn());
    }
    else
    {
        // Fast path (no death sheet): unchanged from the pre-SALIN-54 behaviour.
        ReturnToPool();
        EventBus.RaiseEnemyDefeated(capturedCharacter);
    }
}

private IEnumerator PlayDeathAnimationThenReturn()
{
    Sprite[] frames = _data != null ? _data.deathFrames : null;
    if (_renderer != null && frames != null && frames.Length > 0)
    {
        float fps = _data.deathAnimationFps > 0f
            ? _data.deathAnimationFps
            : _walkAnimationFps;
        if (fps <= 0f) fps = 8f;
        float frameDuration = 1f / fps;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) _renderer.sprite = frames[i];
            float elapsed = 0f;
            while (elapsed < frameDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    _deathRoutine = null;
    ReturnToPool();
}

private void DisableContactCollider()
{
    Collider2D col = GetComponent<Collider2D>();
    if (col != null) col.enabled = false;
}
```

- (c) Save (Ctrl+S).

> **NOTE on why the event fires immediately** The original `Defeat()` raised
> `OnEnemyDefeated` after `ReturnToPool` deactivated the GameObject. The slow path
> here preserves that ordering at the cost of a per-frame deactivation delay
> (~0.5s for a 4-frame, 8-fps death sheet): unregister + freeze + fire the event
> first, then animate, then deactivate. Subscribers (combo manager, score, audio)
> see the kill on the kill stroke; only the visual cleanup is deferred.

> **NOTE on collider disable** The `Collider2D` on the enemy is what triggers
> `OnTriggerEnter2D` in `EnemyMover` at the shrine. Disabling it on Defeat ensures
> a dying enemy that happens to be inside the shrine zone does not register a
> base hit during its death frames. `ResetForPool` re-enables it (§2.2.7) before
> the next spawn.

> **NOTE on coroutine cancellation** If the pool is force-cleared
> (`ReturnAllCheckedOut`) mid-animation, Unity deactivates the GameObject which
> implicitly cancels the coroutine. `ResetForPool` calls `StopCoroutine` and
> nulls `_deathRoutine` defensively (§2.2.7) so the next reuse starts clean.

### §2.2.5  Add a re-entry guard to `TakeDamage`

While the death animation is playing, the enemy is unregistered from the tracker,
so neither `CombatResolver.FindClosestToBase` nor
`ActiveEnemyTracker.FindAllWithCharacter` will find it. As belt-and-suspenders,
guard `TakeDamage` against re-entry too — this protects any code path that holds
a stale `Enemy` reference (test fixtures, sandbox tools).

- (a) Ctrl+F to `public void TakeDamage(int amount)` (currently line 167).
- (b) The first line inside the method is currently:

```csharp
        if (_data == null)
```

- (c) Add a single line **above** it (so it becomes the first statement of the
  method):

```csharp
        if (_isDying) return;
```

- (d) The opening of `TakeDamage` should now read:

```csharp
public void TakeDamage(int amount)
{
    if (_isDying) return;

    if (_data == null)
    {
        DebugLogger.LogWarning($"Enemy.TakeDamage: Enemy '{name}' has no data and cannot take damage.");
        return;
    }
```

- (e) Save (Ctrl+S).

### §2.2.6  Wire the buff push into `Initialize`

`Initialize` currently writes the raw `_data.moveSpeed` into the mover. After §2.2.2
that is a bug: the General's `baseSpeedMultiplier = 0.7` would be ignored on spawn
and the General would move at full speed for the first frame.

- (a) In `Enemy.cs`, use Ctrl+F to jump to the `_mover.SetSpeed(_data.moveSpeed);`
  call (currently line 115, inside `Initialize(EnemyDataSO)`).
- (b) Replace the line **in place** with:

```csharp
        _mover.SetSpeed(EffectiveSpeed);
```

- (c) The surrounding lines 113–115 should now read:

```csharp
        _mover.Stop();
        _mover.SetSpeed(EffectiveSpeed);
```

- (d) Save (Ctrl+S).

> **NOTE** At the moment `Initialize` runs, `_speedBuffs` is empty (cleared in
> `ResetForPool` — see §2.2.7) and `_data` is freshly assigned, so `EffectiveSpeed`
> reduces to `_data.moveSpeed * _data.baseSpeedMultiplier`. The General's `0.7`
> multiplier therefore takes effect on the very first frame after spawn.

### §2.2.7  Clear buff and death state on pool return

If an affected American-era enemy is defeated while a General's aura is still
buffing it, the buff entry stays in `_speedBuffs` after the enemy returns to the
pool. The death animation also leaves `_isDying = true` and a non-null
`_deathRoutine` reference if the enemy was force-returned mid-animation. Clean
both up here.

- (a) Ctrl+F to `public void ResetForPool()` (currently line 145).
- (b) Inside the `try` block, locate the line `_runtimeCharacter = null;` (currently
  line 149). Position the cursor at the end of that line and press Enter.
- (c) On the new line, paste the four cleanup statements (matching the existing
  12-space indent — the `try` block's body, not the method body):

```csharp
            _speedBuffs.Clear();
            _isDying = false;
            if (_deathRoutine != null)
            {
                StopCoroutine(_deathRoutine);
                _deathRoutine = null;
            }
            Collider2D contactCollider = GetComponent<Collider2D>();
            if (contactCollider != null) contactCollider.enabled = true;
```

- (d) The opening of the `try` block should now read:

```csharp
        try
        {
            _runtimeCharacter = null;
            _speedBuffs.Clear();
            _isDying = false;
            if (_deathRoutine != null)
            {
                StopCoroutine(_deathRoutine);
                _deathRoutine = null;
            }
            Collider2D contactCollider = GetComponent<Collider2D>();
            if (contactCollider != null) contactCollider.enabled = true;
            _data = null;
            _currentHealth = 0;
```

- (e) Save (Ctrl+S).

### §2.2.8  Return to Unity and verify compile

- (a) Switch focus back to the Unity editor.
- (b) The status bar at the bottom shows `Compiling...` briefly, then clears.
- (c) Open the **Console** window (`Window → General → Console`, or Ctrl+Shift+C).
  Confirm there are no red error rows.
- (d) If the Console flags `CS0103: 'baseSpeedMultiplier' does not exist on
  EnemyDataSO`, that means §2.3 has not been done yet. Skip ahead to §2.3, run it,
  and return — the compile clears once `EnemyDataSO` exposes `baseSpeedMultiplier`,
  `deathFrames`, and `deathAnimationFps`.

> **WARNING** Do not write a `transform.Translate` or any movement call inside
> `Enemy.cs`. Movement remains owned by `EnemyMover.cs`, which reads from `_speed`.
> The contract added here is: `Enemy` is the buff-stack book-keeper, and it pushes
> the resolved `EffectiveSpeed` into `_mover.SetSpeed(...)` on every change.
> Bypassing the mover would break Focus Mode (which composes
> `_focusSpeedMultiplier` on top of `_speed`) and the sandbox movement-pause flag.

> **Commit**
>
> `feat(enemy): SALIN-54 add speed-buff API, EffectiveSpeed, and death-animation playback to Enemy`

---

## §2.3  Extend `EnemyDataSO` with variant fields

Five movement / aura fields plus an `Era` enum, **plus** two death-animation fields.
Existing fields are left unchanged. The new fields default to values that are
**no-ops** on existing enemies — the existing `EnemyData_*.asset` files remain valid
without re-authoring (they will simply have an empty `deathFrames` array, which
keeps the fast-path defeat behaviour from §2.2.4).

### §2.3.1  Open the file

- (a) **Project** window → `Assets/Scripts/Data/EnemyDataSO.cs` → double-click.
- (b) Confirm the file currently ends at line 33 with a closing `}`. Lines 5–32
  declare the existing eight serialized fields under their `[Header]` groups.

### §2.3.2  Add the variant fields and `Era` enum

- (a) Position the cursor at the end of line 32 (the last `public bool
  dealsContactDamage = true;` line, **inside** the class).
- (b) Press Enter twice to leave a blank line, then paste the following block. Each
  field appears in the Inspector under its own header, in the order shown:

```csharp
    [Header("Variant Era")]
    [Tooltip("Chapter / faction grouping. Used by GeneralAura to limit its buff to American-era allies.")]
    public Era era = Era.Spanish;

    [Header("Zigzag Mover (Pensionado)")]
    [Tooltip("Horizontal sine amplitude in world units. 0 disables zigzag.")]
    public float zigzagAmplitude = 0f;
    [Tooltip("Sine frequency in Hz. 0 disables zigzag.")]
    public float zigzagFrequency = 0f;

    [Header("Base Speed Modifier (General)")]
    [Tooltip("Multiplier applied on top of moveSpeed. 1.0 = default.")]
    public float baseSpeedMultiplier = 1f;

    [Header("Aura (General)")]
    [Tooltip("Radius in world units. 0 disables aura.")]
    public float auraRadius = 0f;
    [Tooltip("Speed multiplier applied to affected same-era non-boss enemies.")]
    public float auraSpeedMultiplier = 1.3f;

    [Header("Death Animation (optional)")]
    [Tooltip("Frames played in sequence on Defeat() before the enemy returns to the pool. Empty = no death animation; the enemy disappears immediately (existing fast-path behaviour).")]
    public Sprite[] deathFrames;
    [Tooltip("Playback FPS for deathFrames. 0 falls back to the walk animation FPS on Enemy.cs (default 8).")]
    public float deathAnimationFps = 8f;
```

- (c) Confirm the closing `}` of the class is still present immediately after the
  block you just pasted.
- (d) After the class's closing `}` (i.e. at the very bottom of the file, **outside**
  the class), add the enum declaration:

```csharp
public enum Era
{
    Spanish,
    American,
    Japanese
}
```

- (e) The final shape of the file is: the existing eight-field class extended with
  the new fields **inside** the class braces, followed by the `Era` enum **after**
  the class's closing brace.
- (f) Save (Ctrl+S).

### §2.3.3  Return to Unity and verify

- (a) Wait for `Compiling...` to clear.
- (b) Open the **Console** (`Window → General → Console`). Confirm no red errors.
- (c) Select any existing `EnemyData_*.asset` (e.g. `Assets/ScriptableObjects/EnemyData_Soldado.asset`)
  and look at the Inspector. The new headers `Variant Era`, `Zigzag Mover (Pensionado)`,
  `Base Speed Modifier (General)`, `Aura (General)`, and `Death Animation (optional)`
  appear with the defaults shown above. Hover each numeric field to confirm the
  tooltip text matches.
- (d) Confirm the existing assets' `Death Animation (optional) → Death Frames` array
  is empty (Size = 0). They will use the fast path in `Enemy.Defeat()` — no
  re-authoring required.

> **NOTE** Defaults are deliberately neutral: `era = Spanish` matches the existing
> Chapter-1 enemies, `zigzagAmplitude = 0` and `auraRadius = 0` make the new
> behaviour silent on existing assets, `baseSpeedMultiplier = 1` preserves their
> current speed, `auraSpeedMultiplier = 1.3` is the design value (only consumed
> when `auraRadius > 0`), and `deathFrames` defaults to empty so no death animation
> is played unless an artist supplies one. Re-author existing assets only if their
> faction is no longer Spanish or you are adding death frames to them.

> **Commit**
>
> `feat(data): SALIN-54 extend EnemyDataSO with era, zigzag, base-speed, aura, and death-animation fields`

---

## §2.4  Create `PensionadoMover`

A small sibling component on the Pensionado prefab. It applies a horizontal sine
offset every frame around the Pensionado's spawn-X. Vertical descent is still owned
by `EnemyMover`, which translates only Y — so there is **no conflict** between the
two writers and **no override flag** is needed.

### §2.4.1  Create the script file

- (a) **Project** window → navigate to `Assets/Scripts/Gameplay/Enemy/`.
- (b) Right-click the `Enemy` folder → `Create → Scripting → MonoBehaviour Script`.
  A new asset appears with the name field highlighted.
- (c) Type `PensionadoMover` → Enter. The file becomes `PensionadoMover.cs` and
  sits next to `Enemy.cs` and `EnemyMover.cs`.
- (d) Double-click `PensionadoMover.cs` to open it in your external editor.

### §2.4.2  Paste the body

- (a) Select all (Ctrl+A) and delete the scaffolded `Start` / `Update`.
- (b) Paste the body verbatim:

```csharp
using UnityEngine;

// Sibling component on the Pensionado prefab. Applies a horizontal sine offset
// around the spawn-X every frame. EnemyMover continues to drive vertical descent;
// it translates only Y, so the two writers do not collide.
[RequireComponent(typeof(Enemy))]
public class PensionadoMover : MonoBehaviour
{
    private Enemy _enemy;
    private float _baseX;
    private float _spawnTime;

    private void OnEnable()
    {
        _enemy = GetComponent<Enemy>();
        _baseX = transform.position.x;
        _spawnTime = Time.time;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        if (data == null || data.zigzagAmplitude <= 0f) return;

        float t = Time.time - _spawnTime;
        float offset = Mathf.Sin(t * Mathf.PI * 2f * data.zigzagFrequency)
                       * data.zigzagAmplitude;

        Vector3 pos = transform.position;
        pos.x = _baseX + offset;
        transform.position = pos;
    }
}
```

- (c) Save (Ctrl+S).
- (d) Return to Unity. Wait for `Compiling...` to clear and confirm the Console is
  clean.

> **NOTE on co-existence with `EnemyMover`** `EnemyMover.Update` (lines 37–42)
> calls `transform.Translate(Vector2.down * finalSpeed * Time.deltaTime, Space.World)`
> — that translates Y, not X. `PensionadoMover.Update` reads the current position,
> overwrites X, and writes back, preserving whatever Y the mover produced this
> frame. There is no script execution order requirement: regardless of which
> component runs first, each frame's final position has the descended Y from
> `EnemyMover` and the sine-offset X from `PensionadoMover`.

> **NOTE on the `OnEnable` capture** `_baseX = transform.position.x` is captured
> when the pool re-enables the GameObject. The pool sets the spawn position
> **before** activating the GameObject (`EnemyPool.OnGet → SetActive(true)`
> sequence), so `_baseX` reflects the actual spawn lane each time, not stale state
> from a previous spawn.

> **NOTE on death animation interaction** When `Enemy.Defeat()` runs the slow
> path (§2.2.4), it calls `_mover?.Stop()` which sets `EnemyMover._active = false`.
> `PensionadoMover.Update` does not check the mover's `_active` flag, so the
> Pensionado would technically keep wiggling sideways during its death animation.
> If you want it to freeze fully, change `PensionadoMover.Update`'s gating to
> also early-return when `_enemy != null && _enemy.IsDying` — add a public
> `bool IsDying => _isDying;` getter on `Enemy.cs` first. The current behaviour
> (slight wiggle during the death frames) is acceptable for the feel; treat the
> freeze as a polish ticket if it bothers playtesters.

> **Commit**
>
> `feat(enemy): SALIN-54 add PensionadoMover for sine-wave zigzag descent`

---

## §2.5  Create `GeneralAura`

A sibling component on the General prefab. Ticks every 0.25s, snapshots
`ActiveEnemyTracker.GetActiveEnemiesSnapshot()`, and applies the data-driven
speed buff to every American-era non-boss enemy within `auraRadius`. Tracks
the previous tick's affected set so it can clear the buff on enemies that just
left the radius. Clears all buffs on disable, which fires whenever the General
is defeated, returns to pool, or the scene unloads.

### §2.5.1  Create the script file

- (a) **Project** window → `Assets/Scripts/Gameplay/Enemy/`.
- (b) Right-click the `Enemy` folder → `Create → Scripting → MonoBehaviour Script`.
- (c) Name the new file `GeneralAura` → Enter. The file becomes `GeneralAura.cs`.
- (d) Double-click to open it in your external editor.

### §2.5.2  Paste the body

- (a) Select all (Ctrl+A) and delete the scaffold.
- (b) Paste:

```csharp
using System.Collections.Generic;
using UnityEngine;

// Sibling component on the General prefab. Every TICK seconds, applies the
// data-driven speed buff to every American-era non-boss enemy within auraRadius
// and removes the buff from anything that just left the radius. OnDisable
// (defeat / scene unload / pool return) clears the buff from every still-affected
// enemy in the same frame, satisfying AC-5.
[RequireComponent(typeof(Enemy))]
public class GeneralAura : MonoBehaviour
{
    private const float TICK = 0.25f;

    private Enemy _self;
    private readonly HashSet<Enemy> _affected = new HashSet<Enemy>();
    private readonly HashSet<Enemy> _stillAffectedBuffer = new HashSet<Enemy>();
    private float _nextTick;

    private void OnEnable()
    {
        _self = GetComponent<Enemy>();
        _nextTick = 0f;
        _affected.Clear();
        _stillAffectedBuffer.Clear();
    }

    private void OnDisable()
    {
        // AC-5: drop buffs from every enemy we were buffing last tick, in this frame.
        foreach (Enemy e in _affected)
        {
            if (e != null) e.ClearSpeedBuff(this);
        }
        _affected.Clear();
        _stillAffectedBuffer.Clear();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;
        if (Time.time < _nextTick) return;
        _nextTick = Time.time + TICK;

        EnemyDataSO data = _self != null ? _self.Data : null;
        if (data == null || data.auraRadius <= 0f) return;

        if (ActiveEnemyTracker.Instance == null) return;
        List<Enemy> active = ActiveEnemyTracker.Instance.GetActiveEnemiesSnapshot();

        float radiusSq = data.auraRadius * data.auraRadius;
        Vector3 myPos = transform.position;

        _stillAffectedBuffer.Clear();

        for (int i = 0; i < active.Count; i++)
        {
            Enemy other = active[i];
            if (other == null || other == _self) continue;          // do not self-buff
            if (other.IsBoss) continue;                              // AC-6
            EnemyDataSO otherData = other.Data;
            if (otherData == null) continue;
            if (otherData.era != Era.American) continue;             // AC-6

            float distSq = (other.transform.position - myPos).sqrMagnitude;
            if (distSq > radiusSq) continue;

            other.ApplySpeedBuff(this, data.auraSpeedMultiplier);
            _stillAffectedBuffer.Add(other);
        }

        // Anything in last tick's set that is no longer in radius loses the buff now.
        foreach (Enemy prev in _affected)
        {
            if (prev != null && !_stillAffectedBuffer.Contains(prev))
                prev.ClearSpeedBuff(this);
        }

        _affected.Clear();
        foreach (Enemy e in _stillAffectedBuffer) _affected.Add(e);
    }
}
```

- (c) Save (Ctrl+S).
- (d) Return to Unity. Wait for `Compiling...` to clear and confirm the Console is
  clean.

> **NOTE** `Era.American` resolves because the `Era` enum lives at file scope
> (declared **outside** the `EnemyDataSO` class in §2.3.2). If your team prefers
> the enum nested inside `EnemyDataSO`, change the reference to
> `EnemyDataSO.Era.American`.

> **NOTE on the snapshot allocation** `GetActiveEnemiesSnapshot` returns a fresh
> `List<Enemy>` copy (`ActiveEnemyTracker.cs:47`). At a 0.25s tick that is one
> small alloc per General per quarter-second — well below the per-frame budget.
> **Do not** swap to `FindAllWithCharacter`'s shared buffer — that buffer is a
> character-filtered view reused across calls, and the aura needs every active
> enemy regardless of character.

> **NOTE on `OnDisable`** Returning the General to the pool calls
> `gameObject.SetActive(false)` (in `EnemyPool.OnRelease`). Unity invokes
> `OnDisable` synchronously inside the same frame, so the buff is removed
> from every affected enemy in the same `TakeDamage → Defeat → ReturnToPool`
> stack frame. AC-5 is satisfied without an explicit "on defeat" event.
>
> When the General has a death animation (§2.2.4 slow path), `OnDisable` does
> **not** fire on the kill stroke — it fires when the death coroutine ends and
> `ReturnToPool` deactivates the GameObject. The buff therefore lingers for the
> ~0.5s duration of the death animation. If a follow-up ticket needs the buff to
> drop on the kill stroke instead, call `_aura?.ClearAllBuffs()` from
> `Enemy.Defeat()`'s slow-path branch and have `GeneralAura` expose that method.
> For SALIN-54 the lingering 0.5s is acceptable — by then the General's corpse
> is visibly dying and players read the buff drop as a single beat with the
> animation.

> **WARNING** Never call `enemy.ApplySpeedBuff(null, x)` or
> `enemy.ClearSpeedBuff(null)` from this component — every General must use
> `this` as the source so two Generals' buffs occupy distinct dictionary slots.
> Using `null` would alias them and the second General's buff would replace the
> first's, leading to surprises like the buff disappearing when the second General
> dies even though the first is still alive.

> **Commit**
>
> `feat(enemy): SALIN-54 add GeneralAura proximity speed-buff component`

---

## §2.6  Author the `Enemy_Pensionado` and `Enemy_General` prefabs

Both prefabs are duplicated from `[Enemy] Soldier.prefab` (the existing American-era
infantry baseline). New names follow the documented convention from `CLAUDE.md`:
`Enemy_[Type].prefab`. The Soldier baseline already has a `SpriteRenderer`, an
`Enemy` component, an `EnemyMover`, a `Collider2D` (for the shrine trigger), and a
working set of placeholder walk frames — duplication carries all of that across.

§2.7 then replaces the placeholder walk frames on each variant and adds death
sheets so the in-scene visuals are correct.

### §2.6.1  Duplicate the baseline for Pensionado

- (a) **Project** window → expand `Assets/Prefabs/Enemies/`. You will see nine
  `[Enemy] X.prefab` files.
- (b) Click `[Enemy] Soldier.prefab` once to select it. Press Ctrl+D (Duplicate). A
  copy named `[Enemy] Soldier 1.prefab` appears with the name field highlighted.
- (c) Type `Enemy_Pensionado` → Enter. The file is now `Enemy_Pensionado.prefab`.

### §2.6.2  Add `PensionadoMover` to the Pensionado prefab

- (a) Double-click `Enemy_Pensionado.prefab`. The Scene view enters **Prefab Mode**
  — a breadcrumb bar `< Scenes / Enemy_Pensionado` appears at the top of the Scene
  view, and the Hierarchy now shows only the prefab's contents.
- (b) Hierarchy → select the root `Enemy_Pensionado` GameObject (the one with the
  `Enemy` and `EnemyMover` components in the Inspector).
- (c) Inspector → scroll to the bottom → click `Add Component`. In the search
  field, type `PensionadoMover`. A single result appears.
- (d) Click the result. The `PensionadoMover` component is added to the root with no
  serialized fields (it auto-grabs the `Enemy` sibling at runtime via
  `RequireComponent`).
- (e) Breadcrumb bar at top of Scene view → click the `<` back arrow (or the
  `Scenes` crumb on the far left) to exit Prefab Mode. If prompted to save, click
  `Save`.
- (f) Save the project (`File → Save Project`) so the prefab change writes to disk.

### §2.6.3  Duplicate the baseline for General

- (a) **Project** window → click `[Enemy] Soldier.prefab` again → Ctrl+D.
- (b) Rename the new copy `Enemy_General` → Enter.

### §2.6.4  Add `GeneralAura` to the General prefab

- (a) Double-click `Enemy_General.prefab` to enter Prefab Mode.
- (b) Hierarchy → select the root `Enemy_General` GameObject.
- (c) Inspector → `Add Component` → type `GeneralAura` → click the result. The
  component is added with no serialized fields.
- (d) Breadcrumb bar `<` to exit Prefab Mode. Save when prompted.
- (e) `File → Save Project`.

> **TIP (optional aura gizmo)** To eyeball the aura radius while authoring, add a
> disabled `SpriteRenderer` child to `Enemy_General` scaled to `2 × auraRadius`
> with a 25%-alpha ring sprite. Disable the renderer's GameObject in builds; only
> enable it when checking the radius in Sandbox Mode. Skip if you do not have a
> ring sprite handy — the verification tests in §2.10 do not require it.

> **Commit**
>
> `feat(enemy): SALIN-54 author Enemy_Pensionado and Enemy_General prefabs`

---

## §2.7  Author the walk and death sprite sheets

Each variant gets its own multi-frame **walk** animation (used by
`Enemy.AdvanceWalkAnimation` while the enemy is descending) and a multi-frame
**death** animation (used by `Enemy.PlayDeathAnimationThenReturn` from §2.2.4
when `EnemyDataSO.deathFrames` is populated).

`Enemy.cs` cycles through both arrays via `SpriteRenderer.sprite` swaps — **no
Animator Controller is needed**, and every existing animated enemy in this project
leaves `EnemyDataSO.animatorController` set to `None`. The two new variants do
the same.

This section is the part that takes the longest if you have not imported a sprite
sheet in Unity before. Read all of §2.7.1 once before clicking — the import
settings are clustered into a single Inspector view, so you can fill them all in
one pass.

The reference asset for the convention is the existing
`Assets/Animations/Enemy/Maestro/sprite_enemy_maestro_walk-Sheet.png`, sliced into
4 frames at ~26×32 px. Open it once in the Inspector before starting so you have
the values in front of you (`Pixels Per Unit = 6`, `Filter Mode = Bilinear`,
`Sprite Mode = Multiple`).

### §2.7.0  Decide your source-art shape

Your artist may have supplied either:

- **Option A (recommended, matches Maestro):** one PNG per animation laid out
  horizontally in a row. Two files per variant — one for walk, one for death.
  File names like `sprite_enemy_pensionado_walk-Sheet.png` and
  `sprite_enemy_pensionado_death-Sheet.png`.
- **Option B (fallback):** separate PNGs per frame
  (`pensionado_walk_0.png`, `pensionado_walk_1.png`, ...,
  `pensionado_death_0.png`, ...).

If you have neither yet, copy the Maestro walk sheet temporarily so the prefab
still has plausible visuals while you wait for the real art:

- (a) Project window → `Assets/Animations/Enemy/Maestro/sprite_enemy_maestro_walk-Sheet.png`
  → Ctrl+D. A copy named `sprite_enemy_maestro_walk-Sheet 1.png` appears.
- (b) Rename it `sprite_enemy_pensionado_walk-Sheet.png` and proceed with §2.7.1.
  Repeat for the General. For the death sheet you can either skip it (leave
  `deathFrames` empty in §2.8 — the fast path runs and the enemy disappears
  immediately) or duplicate the Maestro walk sheet again as a stand-in. Replace
  with real art when the artist delivers — re-running §2.7.1 on the new file
  rewires the slices automatically as long as the frame count matches.

### §2.7.1  Option A — import a single sprite sheet (recommended)

#### §2.7.1.1  Create the destination folder for the Pensionado

- (a) Project window → expand `Assets/Animations/Enemy/`. You should see one
  existing folder, `Maestro/`.
- (b) Right-click the `Enemy` folder → `Create → Folder`. A new folder named
  `New Folder` appears.
- (c) Press F2 (rename) and type `Pensionado` → Enter. Confirm the path is
  `Assets/Animations/Enemy/Pensionado/`.

#### §2.7.1.2  Import the source PNGs

- (a) Open Windows Explorer (or your OS file browser) at the location where your
  artist saved the source PNGs (e.g. a Dropbox folder, a Discord download, the
  desktop).
- (b) **Drag both PNG files** (the walk sheet and the death sheet) from the OS
  file browser **into** the `Assets/Animations/Enemy/Pensionado/` folder in
  Unity's Project window. Unity imports them and creates sibling `.meta` files.
  Both PNGs appear in the Project window with small image thumbnails.
- (c) Click the imported walk sheet once to select it. Confirm the file name is
  `sprite_enemy_pensionado_walk-Sheet.png` (rename if needed: F2 → type the new
  name → Enter; Unity rewrites the meta automatically and preserves any references).
- (d) Click the imported death sheet. Confirm the file name is
  `sprite_enemy_pensionado_death-Sheet.png`. Rename if needed.

#### §2.7.1.3  Configure texture import settings (one sheet at a time)

The settings below apply to both the walk sheet and the death sheet. Apply them
once per file. If you select both PNGs at the same time before editing the
Inspector, the settings change applies to both at once (multi-edit mode).

- (a) With the PNG (or both PNGs multi-selected) selected, look at the
  **Inspector**. The top section shows a row of platform tabs and import settings.
  Below the preview, six grouped fields control how Unity treats this image as a
  sprite.
- (b) `Texture Type` dropdown — currently `Default`. Click → choose
  `Sprite (2D and UI)`. The Inspector below changes to show sprite-specific
  fields.
- (c) `Texture Shape` — leave `2D`.
- (d) `Sprite Mode` dropdown — currently `Single`. Click → choose `Multiple`. New
  fields appear below it (`Pixels Per Unit`, `Mesh Type`, `Extrude Edges`,
  `Pivot`, etc.).
- (e) `Pixels Per Unit` — type `6`. (The Maestro sheet uses 6. This makes one
  Unity world unit ~6 source pixels, so a 26×32 sprite is ~4.3×5.3 world units —
  consistent with the existing scene's enemy scale.)
- (f) `Mesh Type` — leave `Tight` (the default).
- (g) `Extrude Edges` — leave `1`.
- (h) `Pivot` — leave `Center` (the per-slice pivot can be overridden in the
  Sprite Editor; the slice grid usually matches Center).
- (i) `Generate Physics Shape` — leave **on** (the existing convention).
- (j) Scroll down to the **Advanced** group. Click the disclosure triangle to
  expand it.
- (k) `Filter Mode` dropdown — choose `Bilinear` (matches Maestro). If your art
  is strict pixel-art and you want no smoothing on upscale, choose `Point (no
  filter)` instead — this is a per-team preference; the Maestro convention is
  Bilinear.
- (l) `Aniso Level` — leave at `1`.
- (m) Scroll to the bottom. Click the `Apply` button (it lights up only when
  pending changes need to be written). Unity reimports the texture(s); the
  thumbnails and preview refresh.

> **WARNING** Do **not** click `Revert` after `Apply` is enabled — that discards
> your edits. If you accidentally clicked `Revert`, simply re-enter the values
> above and `Apply` again.

#### §2.7.1.4  Slice each sheet in the Sprite Editor

Slicing is done one sheet at a time (the Sprite Editor only opens for a single
asset).

- (a) With the **walk sheet** selected (single-selection, not multi), click the
  `Sprite Editor` button just above `Apply` (or `Window → 2D → Sprite Editor`).
  The Sprite Editor window opens with the texture visible in a checkered preview
  canvas.
- (b) The toolbar shows `Sprite Editor`, a `Slice` dropdown, an `Apply` /
  `Revert` row, and a few view controls. Click the `Slice` dropdown to expand
  it.
- (c) **Method** dropdown → choose `Automatic` (Unity auto-detects sprite
  bounding boxes by scanning for transparent gaps). For a clean horizontal sheet
  with transparent gutters, this gives you N rectangles in one click.
- (d) `Pivot` — choose `Center` (matches §2.7.1.3 step h).
- (e) `Method` lower row — leave `Smart`.
- (f) Click the `Slice` button at the bottom of the dropdown. The preview
  canvas shows green rectangles around each detected frame. For a 4-frame walk
  sheet you should see exactly 4 rectangles.
- (g) **If automatic slicing detects the wrong number of frames** (e.g. it joins
  two frames or splits one): switch the `Method` dropdown to `Grid By Cell Count`,
  set `Column & Row` to `4 × 1` (for 4 horizontal frames; use whatever count your
  artist provided), set `Pixel Size` alternatively to the per-frame width if you
  know it (e.g. 26 px), then click `Slice` again.
- (h) Hover each green rectangle to see its auto-generated name in the bottom-left
  status bar (e.g. `sprite_enemy_pensionado_walk-Sheet_0`,
  `..._1`, ...). The Maestro convention names slices `<sheetname>_0`, `_1`, ...
  in left-to-right order.
- (i) Click `Apply` at the top of the Sprite Editor toolbar. The window can stay
  open or be closed (X) — the slices are now baked into the texture asset.
- (j) **Repeat (a)–(i) for the death sheet.** Select
  `sprite_enemy_pensionado_death-Sheet.png` in the Project window, open the
  Sprite Editor, slice it. The frame count for death is usually 3–6 (a quick
  collapse / poof / flash); whatever Automatic mode detects is what you wire in
  §2.8.

#### §2.7.1.5  Confirm the slices in the Project window

- (a) Project window → click the disclosure triangle (▶) on the left side of the
  walk sheet's row. It expands to reveal the sliced sub-sprites:
  `sprite_enemy_pensionado_walk-Sheet_0`, `..._1`, `..._2`, `..._3`.
- (b) Repeat for the death sheet — disclosure triangle on
  `sprite_enemy_pensionado_death-Sheet.png` reveals
  `sprite_enemy_pensionado_death-Sheet_0`, `..._1`, ..., `..._N-1` where N is
  whatever you sliced in §2.7.1.4(j).
- (c) Click each sub-sprite to confirm it shows just one frame in the Inspector
  preview (not the whole sheet).

#### §2.7.1.6  Repeat for the General

- (a) Project window → right-click `Assets/Animations/Enemy/` → `Create →
  Folder` → name `General` → Enter.
- (b) Drag the General's walk-sheet PNG and death-sheet PNG into
  `Assets/Animations/Enemy/General/`. Rename to
  `sprite_enemy_general_walk-Sheet.png` and
  `sprite_enemy_general_death-Sheet.png` if needed.
- (c) Repeat §2.7.1.3 (texture settings, multi-select both at once for speed) and
  §2.7.1.4 (slicing, one at a time) on the General's two sheets.
- (d) Confirm the slices via §2.7.1.5.

### §2.7.2  Option B — import individual PNGs (fallback)

Use this only if your artist supplied per-frame PNGs and not sheets.

- (a) Create the destination folder as in §2.7.1.1.
- (b) Drag all walk-frame and death-frame PNGs into the folder at once
  (Ctrl+select them in Explorer, then drag).
- (c) Select all PNGs in the Project window (Ctrl+click each, or
  shift-select a range). In the Inspector you can edit the import settings for
  **all** of them at the same time.
- (d) Set `Texture Type = Sprite (2D and UI)`, `Sprite Mode = Single` (since each
  PNG is one frame), `Pixels Per Unit = 6`, Advanced → `Filter Mode = Bilinear`.
  Click `Apply`.
- (e) The PNGs are now top-level Sprite assets. They have no sub-sprites; in §2.8
  you will drag the PNG assets directly (not sub-asset slices) into the SO's
  `walkFrames` / `deathFrames` arrays in their natural numeric order
  (`..._0`, `..._1`, …).

### §2.7.3  Sanity-check the assets against the Maestro convention

- (a) Project window → `Assets/Animations/Enemy/Maestro/sprite_enemy_maestro_walk-Sheet.png`
  → expand. Confirm: 4 sub-sprites, named `..._0` through `..._3`.
- (b) Project window → `Assets/Animations/Enemy/Pensionado/sprite_enemy_pensionado_walk-Sheet.png`
  → expand. Confirm the same shape: 4 sub-sprites, indexed 0–3.
- (c) Project window → `Assets/Animations/Enemy/Pensionado/sprite_enemy_pensionado_death-Sheet.png`
  → expand. Confirm the death sheet has whatever frame count you sliced
  (typically 3–6), indexed 0 onwards.
- (d) Same for the General's walk and death sheets in
  `Assets/Animations/Enemy/General/`.
- (e) If any sheet shows a different frame count than expected, that is fine —
  both `walkFrames` and `deathFrames` arrays adapt to whatever array length you
  wire in §2.8 — but record the count so you size the SO arrays correctly.

> **NOTE on the prefab `SpriteRenderer.sprite` field** The duplicated prefabs
> from §2.6 still show their original Soldier-baseline sprite in the Scene view
> until you wire the new sheet. That is harmless — at runtime, `Enemy.Initialize`
> overwrites `_renderer.sprite` with `_data.walkFrames[0]` (line 123), so the
> prefab's authoring-time sprite is only seen in the Editor preview. If you want
> the prefab to look right in the Editor too, double-click the prefab to enter
> Prefab Mode → select the root → Inspector → `SpriteRenderer` → `Sprite` field
> → click the ⊙ target picker → search for `sprite_enemy_pensionado_walk-Sheet_0`
> → double-click. Repeat for the General prefab using its `_0` walk slice.

> **NOTE on death animation duration** A 4-frame death sheet at 8 fps is 0.5s
> on screen. If your artist provides 6+ frames and you want a snappier death,
> bump `deathAnimationFps` on the SO to 12 or 16 (set in §2.8). Avoid going below
> 6 fps — the death feels laggy and the wave-clear logic does not wait for the
> animation, so the next wave may already be spawning by the time the corpse
> finishes.

> **Commit**
>
> `feat(art): SALIN-54 import and slice Pensionado and General walk and death sprite sheets`

---

## §2.8  Author the `EnemyData_Pensionado` and `EnemyData_General` ScriptableObjects

The new SO assets live alongside the existing ten `EnemyData_*.asset` files at the
**flat** path `Assets/ScriptableObjects/`. Do not create a `Enemies/` subfolder —
that would diverge from the existing layout.

### §2.8.1  Create `EnemyData_Pensionado`

- (a) **Project** window → click `Assets/ScriptableObjects/` to select the folder
  (so the new asset is created here, not in a sibling).
- (b) Right-click empty space inside the folder view → `Create → Salinlahi → Enemy
  Data`. A new asset appears with the placeholder name `EnemyData` highlighted.
  (The menu label comes from the `[CreateAssetMenu(menuName = "Salinlahi/Enemy
  Data")]` attribute on `EnemyDataSO.cs:3`.)
- (c) Type `EnemyData_Pensionado` → Enter.
- (d) Click the asset once to select it. The Inspector shows every `EnemyDataSO`
  field grouped under its `[Header]`.
- (e) Fill in the values cell-by-cell, matching the table below. Where the table
  shows `Char_LA`, click the small ⊙ target picker icon next to the field, type
  `Char_LA` in the picker search, and double-click the result. Walk-Frames and
  Death-Frames wiring is covered in the dedicated steps §2.8.2 and §2.8.3 — leave
  those two arrays empty for now.

| Field | Value |
|---|---|
| Identity → Enemy ID | `pensionado` |
| Stats → Move Speed | `0.9` |
| Health → Max Health | `1` |
| Visuals → Animator Controller | leave `None` (sprite cycling is code-driven; matches the Maestro convention) |
| Character → Assigned Character | `Char_LA` (or any unlocked Chapter-2 glyph for the test wave) |
| Decoy → Is Decoy | unchecked |
| Contact Behavior → Deals Contact Damage | checked |
| Variant Era → Era | `American` |
| Zigzag Mover (Pensionado) → Zigzag Amplitude | `1.2` |
| Zigzag Mover (Pensionado) → Zigzag Frequency | `2` |
| Base Speed Modifier (General) → Base Speed Multiplier | `1` |
| Aura (General) → Aura Radius | `0` |
| Aura (General) → Aura Speed Multiplier | `1.3` (left at default; ignored because aura radius is 0) |
| Death Animation (optional) → Death Animation Fps | `8` |

### §2.8.2  Wire the Pensionado walk frames

- (a) With `EnemyData_Pensionado.asset` still selected, find the `Visuals → Walk
  Frames` field. Click the disclosure triangle (▶) to expand the array.
- (b) Click the `Size` numeric field. Type `4` (or whatever count you confirmed in
  §2.7.3 for the walk sheet). Press Enter. Four empty `Element 0` … `Element 3`
  rows appear, each a Sprite-typed object slot.
- (c) **Lock the Inspector** so the SO stays selected while you click around the
  Project window. Top-right corner of the Inspector → click the small padlock
  icon (it shifts from open to closed). The Inspector now stays on
  `EnemyData_Pensionado` even when you click other assets.
- (d) Project window → expand `Assets/Animations/Enemy/Pensionado/sprite_enemy_pensionado_walk-Sheet.png`
  to reveal the four sub-sprites.
- (e) **Drag the `_0` sub-sprite** from the Project window into `Element 0` on the
  locked Inspector. The slot binds to
  `sprite_enemy_pensionado_walk-Sheet_0 (Sprite)`.
- (f) Drag `_1` → `Element 1`. Drag `_2` → `Element 2`. Drag `_3` → `Element 3`.
- (g) Confirm the SO's `Walk Frames` array now lists all four sprites in order.
  Leave the Inspector locked — you will use it again for the death frames in
  §2.8.3.

> **TIP (multi-drag shortcut)** With the Inspector locked, expand the sprite
> sheet, then drag-select all four sub-sprites in the Project window
> (Shift+click the first and last) and **drop the whole selection onto the array
> header label** (`Walk Frames`). Unity sizes the array to 4 and assigns each
> slice in order automatically. This is the fastest way once you have done it
> a few times; the per-element drag in steps (e)–(f) is the explicit version.

### §2.8.3  Wire the Pensionado death frames

- (a) With the Inspector still locked on `EnemyData_Pensionado.asset`, scroll down
  to the `Death Animation (optional) → Death Frames` field. Click its disclosure
  triangle to expand the array.
- (b) Click the `Size` numeric field. Type the death-sheet frame count from
  §2.7.3(c) (e.g. `4` for a 4-frame collapse). Press Enter. The corresponding
  number of empty `Element X` rows appears.
- (c) Project window → click on
  `Assets/Animations/Enemy/Pensionado/sprite_enemy_pensionado_death-Sheet.png`
  once (it does **not** unselect the SO because the Inspector is locked).
- (d) Expand the death-sheet's disclosure triangle to reveal its sub-sprites
  (`..._0`, `..._1`, …).
- (e) Drag each sub-sprite into the matching Death-Frames `Element` slot in
  numeric order. Or use the multi-drag shortcut from the §2.8.2 TIP — drag-select
  all death sub-sprites and drop them on the `Death Frames` array header label.
- (f) Top-right corner of the Inspector → click the padlock icon again
  (closed → open) so it stops following `EnemyData_Pensionado` exclusively.
- (g) Confirm both arrays are populated: `Walk Frames` has 4 entries from the
  walk sheet, `Death Frames` has N entries from the death sheet. The
  `Death Animation Fps` field above is `8`.
- (h) `File → Save` (Ctrl+S).

### §2.8.4  Create `EnemyData_General`

- (a) Project window → still inside `Assets/ScriptableObjects/` → right-click empty
  space → `Create → Salinlahi → Enemy Data`.
- (b) Type `EnemyData_General` → Enter.
- (c) Fill in the values:

| Field | Value |
|---|---|
| Identity → Enemy ID | `general` |
| Stats → Move Speed | `1.0` |
| Health → Max Health | `3` |
| Visuals → Animator Controller | leave `None` |
| Character → Assigned Character | `Char_GA` (or any unlocked Chapter-2 glyph) |
| Decoy → Is Decoy | unchecked |
| Contact Behavior → Deals Contact Damage | checked |
| Variant Era → Era | `American` |
| Zigzag Mover (Pensionado) → Zigzag Amplitude | `0` |
| Zigzag Mover (Pensionado) → Zigzag Frequency | `0` |
| Base Speed Modifier (General) → Base Speed Multiplier | `0.7` |
| Aura (General) → Aura Radius | `3.5` |
| Aura (General) → Aura Speed Multiplier | `1.3` |
| Death Animation (optional) → Death Animation Fps | `8` |

### §2.8.5  Wire the General walk and death frames

- (a) Repeat §2.8.2's drag procedure using the General's walk sheet at
  `Assets/Animations/Enemy/General/sprite_enemy_general_walk-Sheet.png`.
- (b) Repeat §2.8.3's drag procedure using the General's death sheet at
  `Assets/Animations/Enemy/General/sprite_enemy_general_death-Sheet.png`.
- (c) `File → Save` (Ctrl+S) so both new SOs are flushed to disk with all four
  arrays populated.

> **NOTE** `EnemyDataSO` does not carry a prefab reference. The data-to-prefab
> mapping lives on `EnemyPool` (§2.9) keyed by the `enemyID` string.
> `enemyID = "pensionado"` and `enemyID = "general"` (lowercase, no spaces) are the
> keys `EnemyPool.ResolvePoolState` will look up — case-insensitive
> (`enemyID.Trim().ToLowerInvariant()`).

> **NOTE on leaving deathFrames empty** If your artist has not delivered a death
> sheet yet, leave `Death Frames` Size = `0`. `Enemy.Defeat()`'s fast-path branch
> (§2.2.4) runs and the enemy disappears immediately — the variant is still
> shippable, just without the death visual. Wire the death sheet later by
> repeating §2.8.3 / §2.8.5 and re-saving the SO. No prefab or code change is
> required.

> **Commit**
>
> `feat(data): SALIN-54 author EnemyData_Pensionado and EnemyData_General assets`

---

## §2.9  Register the variants with `EnemyPool`

`EnemyPool` is a `Singleton<T>` instantiated by Bootstrap and persisted via
`DontDestroyOnLoad`. Variant prefabs are registered through the Inspector on the
Bootstrap-scene `Manager_EnemyPool` prefab via the serialized
`Registered Enemy Prefabs` list (`EnemyPool.cs:35`, type
`List<EnemyPrefabRegistration>`).

### §2.9.1  Open Bootstrap and locate `Manager_EnemyPool`

- (a) Unity menu bar → `File → Open Scene` (Ctrl+O). Navigate to
  `Assets/_Scenes/Bootstrap.unity` (note the leading underscore on `_Scenes`).
  Double-click `Bootstrap.unity` to open it.
- (b) Hierarchy → locate the GameObject that holds the `EnemyPool` component.
  Most likely named `Manager_EnemyPool` (per the `Manager_[Name].prefab` convention
  in `CLAUDE.md`). If the scene root has a parent grouping object (e.g.
  `[Managers]`), expand it first.
- (c) Click the GameObject. The Inspector shows the `EnemyPool` component with three
  `[Header]` groups: `Prefab`, `Pool Size`, and `Registered Enemy Prefabs`.

### §2.9.2  Add a registration row for Pensionado

- (a) Inspector → `Registered Enemy Prefabs` group → click the disclosure triangle
  to expand the list. Note the current `Size` value (e.g. `9` if every existing
  variant has a row).
- (b) Click the `Size` numeric field, type `Size + 2` (e.g. if it shows `9`, type
  `11`), and press Enter. Two empty `Element X` rows appear at the bottom of the
  list.
- (c) Expand the **first** new row (`Element [Size-2]`):
  - **Enemy ID**: type `pensionado` (lowercase, matching the SO's
    `EnemyData_Pensionado.enemyID` value from §2.8.1).
  - **Prefab**: drag `Assets/Prefabs/Enemies/Enemy_Pensionado.prefab` from the
    Project window into the slot. The slot binds it as
    `Enemy_Pensionado (Enemy)`.
  - **Default Capacity**: `4` (two waves' worth of typical Pensionado throughput).
  - **Max Size**: `8`.

### §2.9.3  Add a registration row for General

- (a) Expand the **second** new row (`Element [Size-1]`):
  - **Enemy ID**: type `general` (lowercase, matching `EnemyData_General.enemyID`).
  - **Prefab**: drag `Assets/Prefabs/Enemies/Enemy_General.prefab` into the slot.
  - **Default Capacity**: `2` (Generals are rare).
  - **Max Size**: `4`.

### §2.9.4  Save the Bootstrap scene

- (a) `File → Save` (Ctrl+S). The asterisk on the `Bootstrap` scene tab disappears
  once the save completes.

> **NOTE** `EnemyPool.ResolvePoolState` (lines 184–204) matches `enemyID` via
> `Trim().ToLowerInvariant()`, so `Pensionado`, `pensionado`, and ` Pensionado `
> all resolve. Sticking with lowercase keeps the Inspector list scannable. If a
> wave config references an unknown `enemyID`, the pool logs
> `EnemyPool: Unknown enemyID 'X'. Falling back to default pool.` — search the
> Console for that warning if a Pensionado or General fails to spawn.

> **Commit**
>
> `chore(enemy): SALIN-54 register Pensionado and General variants with EnemyPool`

---

## §2.10  Verification

All tests run in the Unity editor with `Bootstrap.unity` as the entry point and
**Sandbox Mode** active. Sandbox is reached from the Main Menu's `Sandbox` button
and lets you spawn arbitrary enemy/character combinations without authoring a wave.

### §2.10.1  Open Sandbox Mode

- (a) Unity menu bar → `File → Open Scene` → `Assets/_Scenes/Bootstrap.unity` →
  Open.
- (b) Press Play (top toolbar, ▶). Bootstrap transitions to the Main Menu.
- (c) In the Game view Main Menu, tap the `Sandbox` button. The Gameplay scene
  loads with the `[Sandbox] Overlay` Canvas on top — a vertical panel anchored to
  the top of the screen with `SANDBOX MODE`, enemy/character selectors, a spawn
  button, movement controls, and a recognition readout.

### §2.10.2  Test 1 — Pensionado zigzag and shrine reach (AC-1, AC-2, AC-7)

- (a) Sandbox overlay → `Enemy:` selector → tap `Next Enemy` until the label reads
  `Enemy: Pensionado`.
- (b) `Character mode:` → if it reads `Random`, tap `Toggle Character Mode` so it
  reads `Specific`.
- (c) `Specific character:` → tap `Next Character` until it reads `LA (la)`.
- (d) Tap `Spawn Selected Enemy` three times (with ~0.5s between taps so they spread
  vertically). Three Pensionado sprites appear at the top lane and start
  descending.
- (e) **Expected**
  - Each Pensionado oscillates left/right around its spawn lane while descending.
    The horizontal travel is visibly non-linear (a sine wave at 2 Hz with ±1.2 m
    amplitude, per §2.8.1). Vertical descent is steady.
  - The walk-cycle sprite swaps every ~0.125s (8 fps). All four frames from
    §2.7's sliced walk sheet appear in sequence.
  - If undefeated, each one passes through the shrine collision zone and triggers
    `EventBus.RaiseBaseHit(1)`. Console shows `Heart lost. Hearts left: N` rows
    from `HeartManager`.
  - The Pensionado GameObject becomes inactive after the base hit (visible by
    expanding `Manager_EnemyPool` in the Hierarchy during Play mode and watching
    the `Enemy_Pensionado` instance toggle off).

### §2.10.3  Test 2 — General aura applies buff to American-era enemies (AC-3, AC-4)

- (a) Stop Play (■). Press Play again and re-enter Sandbox Mode.
- (b) `Enemy:` → `Soldier` (the existing American-era infantry; era should be
  `American` if its SO has been re-authored — if not, set
  `EnemyData_Soldier.era = American` first).
- (c) `Specific character:` → `BA`.
- (d) Spawn 4× Soldier. Note their descent pace.
- (e) Pause briefly. `Enemy:` → `General`. Spawn 1× General **near** the Soldiers
  (within ~3.5 m — the Sandbox spawns at a fixed top-lane position, so use the
  movement-pause toggle if the Soldiers have already passed the radius).
- (f) **Expected**
  - The General descends at ~0.7× the Soldiers' base pace before any aura kicks in
    (`baseSpeedMultiplier = 0.7`).
  - Within ~0.25s of the General spawn, every Soldier inside the 3.5 m aura
    visibly accelerates to ~1.3× its previous descent. They cover ground faster
    than the unspawned Soldiers from before the General appeared.
  - If you temporarily attach a `Debug.Log` inside `Enemy.ApplySpeedBuff` printing
    `EffectiveSpeed`, the per-tick Console output for buffed Soldiers shows
    `(moveSpeed × 1) × 1.3` while the General is alive.

### §2.10.4  Test 3 — General defeat removes the buff immediately (AC-5)

- (a) Continue from Test 2 with the General still alive and at least one Soldier
  inside the radius.
- (b) Sandbox → set `Specific character:` to the General's assigned character
  (`GA` per §2.8.4). In the Game view drawing area, draw `ᜄ` (the GA glyph).
  `CombatResolver` routes a single hit to the closest GA-tagged enemy — the
  General. Repeat the draw twice more (General has `maxHealth = 3`) to defeat it.
- (c) **Expected**
  - On the third successful draw, the General enters the slow-path Defeat
    branch (§2.2.4): unregisters from the tracker, freezes in place, fires
    `OnEnemyDefeated`, and starts the death animation.
  - Every previously-buffed Soldier visibly slows back to its base pace within
    ~0.5s of the kill stroke (when `OnDisable` on `GeneralAura` finally runs as
    the death coroutine ends and `ReturnToPool` deactivates the General). If you
    log `Enemy.EffectiveSpeed` per-frame on a buffed Soldier, the value drops
    back to `moveSpeed × 1` at that moment.
  - **NOTE on the 0.5s lag** with a death animation present, the buff drop is
    delayed by the death duration (the `OnDisable` clear in `GeneralAura` does
    not fire until the GameObject deactivates at the end of the coroutine). For
    AC-5 to be "the buff is removed in the same frame" with strict reading, the
    follow-up enhancement noted in §2.5's `OnDisable` callout is required —
    otherwise this is the design trade-off baked into shipping the death
    animation. For SALIN-54, treat the lingering 0.5s as acceptable.

### §2.10.5  Test 4 — era isolation (AC-6 part 1)

- (a) Stop Play; re-enter Sandbox Mode.
- (b) Spawn 1× General. Spawn 2× `Heitai` (Japanese-era infantry —
  `EnemyData_Heitai.era = Japanese`) within the General's radius.
- (c) **Expected**
  - The two Heitai descend at their unbuffed base speed throughout. The General's
    aura tick skips them at the `era != American` check.
  - If a `Debug.Log` is added inside the era-skip branch, the Console prints
    `aura: skip Heitai (era=Japanese)` once per tick per Heitai while in radius.

### §2.10.6  Test 5 — bosses are skipped (AC-6 part 2)

SALIN-68 is not yet merged, so `Enemy.IsBoss` always returns `false` (the
placeholder at `Enemy.cs:45`). This test verifies the aura's `IsBoss` skip still
runs. Re-run after SALIN-68 ships to validate the real boss exclusion.

- (a) Stop Play; re-enter Sandbox Mode.
- (b) Spawn 1× General. Inside `GeneralAura.Update`, temporarily inject a single
  `Debug.Log($"aura check: {other.name} IsBoss={other.IsBoss}");` line above the
  `if (other.IsBoss) continue;` check. Save and re-enter Play.
- (c) Spawn 3× Soldier in radius. Watch the Console — every per-tick log shows
  `IsBoss=False`. Every Soldier is buffed.
- (d) Stop Play and **remove the Debug.Log** before committing — `CLAUDE.md`
  forbids `Debug.Log` in committed code.
- (e) **Re-test post-SALIN-68:** the `Enemy.IsBoss` override on `BossController`
  will return `true`. The aura skip will then exclude bosses. Open this test row
  again on the SALIN-68 branch to confirm.

### §2.10.7  Test 6 — pool and tracker cleanup for both variants (AC-7)

- (a) Stop Play; re-enter Sandbox Mode.
- (b) Spawn 2× Pensionado and 2× General (4 enemies total).
- (c) During Play, expand `Manager_EnemyPool` in the Hierarchy. Confirm the four
  enemies appear as active children. Confirm
  `ActiveEnemyTracker.Instance.ActiveCount == 4` (add a temporary
  `Debug.Log(ActiveEnemyTracker.Instance.ActiveCount)` to a sandbox handler if you
  do not have the debugger attached).
- (d) Defeat all four (draw their assigned characters).
- (e) **Expected**
  - All four enter the slow-path Defeat (`deathFrames` non-empty per §2.8). They
    unregister from the tracker on the kill stroke, so `ActiveCount` drops to 0
    immediately even though the death animations are still playing.
  - All four GameObjects deactivate at the end of their respective death
    coroutines (~0.5s) and re-parent under `Manager_EnemyPool`.
  - No `Enemy.Defeat: '...' has no data` warnings in the Console. No
    `EnemyPool.Return: '...' was already returned` warnings (which would indicate
    a regression of the §2.1 single-Unregister property).
- (f) Remove any temporary log calls before committing.

### §2.10.8  Test 7 — death animation plays before pool return

This is the dedicated check for the §2.2.4 refactor. Run it once with
`deathFrames` populated and once with `deathFrames` cleared (Size = 0) to confirm
both paths.

- (a) Stop Play; re-enter Sandbox Mode.
- (b) Confirm `EnemyData_Pensionado.deathFrames` is **populated** (Size = N from
  §2.8.3). Spawn 1× Pensionado + draw `LA` to defeat it on the first hit
  (Pensionado has `maxHealth = 1`).
- (c) **Expected (slow path)**
  - On the kill stroke, the Pensionado freezes in place (no more sine wiggle, no
    more descent). The walk sprite swaps to the death sheet's frame `_0`. Each
    frame plays for ~0.125s (8 fps) until all N frames have been shown.
  - During the animation, expanding `Manager_EnemyPool` in the Hierarchy shows
    the Pensionado is still active (not yet returned to pool). Its
    `Collider2D.enabled` is `false` during this window.
  - At the end of the animation, the GameObject deactivates and re-parents under
    `Manager_EnemyPool`.
  - `EventBus.OnEnemyDefeated` fires on the kill stroke (combo bumps, defeat SFX
    plays immediately) — confirmable by adding a temporary `Debug.Log` to any
    handler that subscribes to `OnEnemyDefeated`.
- (d) Stop Play. Select `EnemyData_Pensionado.asset` in the Project window.
  Inspector → `Death Animation (optional) → Death Frames` → Size = `0` (clear
  the array). Save (Ctrl+S).
- (e) Re-enter Sandbox Mode. Spawn 1× Pensionado and defeat it.
- (f) **Expected (fast path, deathFrames empty)**
  - The Pensionado disappears in a single frame on the kill stroke — no death
    animation. The behaviour matches the pre-SALIN-54 fast path.
  - `EventBus.OnEnemyDefeated` still fires on the kill stroke.
- (g) Restore `EnemyData_Pensionado.deathFrames` by repeating §2.8.3 (or `Ctrl+Z`
  on the SO if you have not done other edits since). Confirm the array is
  populated again.

### §2.10.9  Acceptance matrix summary

| Check | Source | Pass criterion |
|---|---|---|
| AC-1 | §2.4 mover + §2.10.2 test 1 | Visible non-linear horizontal sine + reaches shrine if undefeated |
| AC-2 | §2.3 + §2.8.1 | Inspector exposes both fields with the values authored above |
| AC-3 | §2.2 EffectiveSpeed + §2.8.4 | General descends at 0.7× the Soldier's base pace (visual) |
| AC-4 | §2.5 + §2.10.3 test 2 | Soldier in radius accelerates to 1.3× while General alive |
| AC-5 | §2.5 OnDisable + §2.10.4 test 3 | Soldier slows back to base when the General's GameObject deactivates (immediate if `deathFrames` empty; delayed by death duration if populated — see §2.5 callout) |
| AC-6 | §2.5 era + IsBoss skips + §2.10.5/§2.10.6 | Heitai unaffected; bosses unaffected (post-SALIN-68 retest) |
| AC-7 | §2.1 verify + §2.10.7 test 6 | Both variants tracker-unregister on kill stroke; pool return at end of death animation |
| AC-8 | §2.8 | Both `EnemyData_*.asset` files exist at `Assets/ScriptableObjects/` |
| Death anim | §2.2.4 + §2.10.8 test 7 | Slow path plays frames then returns; fast path (empty array) returns immediately; event fires on kill stroke either way |

> **Commit**
>
> `test(enemy): SALIN-54 verify Pensionado zigzag, General aura, and death-animation playback`

---

## Files created or modified

| Path | Action |
|---|---|
| [Assets/Scripts/Gameplay/Enemy/Enemy.cs](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs) | Modified — add `using System.Collections;` + `using System.Collections.Generic;`; add `_speedBuffs`, `_isDying`, `_deathRoutine`, `MaxHealth`, `EffectiveSpeed`, `ApplySpeedBuff`, `ClearSpeedBuff`, `PushSpeedToMover`; refactor `Defeat()` to slow / fast paths; add `PlayDeathAnimationThenReturn` and `DisableContactCollider`; add `if (_isDying) return;` to `TakeDamage`; replace `_mover.SetSpeed(_data.moveSpeed)` with `_mover.SetSpeed(EffectiveSpeed)` in `Initialize`; clear buff/death state and re-enable collider in `ResetForPool` |
| [Assets/Scripts/Data/EnemyDataSO.cs](../../../Assets/Scripts/Data/EnemyDataSO.cs) | Modified — add `Era` enum, `era`, `zigzagAmplitude`, `zigzagFrequency`, `baseSpeedMultiplier`, `auraRadius`, `auraSpeedMultiplier`, `deathFrames`, `deathAnimationFps` |
| Assets/Scripts/Gameplay/Enemy/PensionadoMover.cs | New |
| Assets/Scripts/Gameplay/Enemy/GeneralAura.cs | New |
| Assets/Prefabs/Enemies/Enemy_Pensionado.prefab | New (duplicated from `[Enemy] Soldier.prefab`) |
| Assets/Prefabs/Enemies/Enemy_General.prefab | New (duplicated from `[Enemy] Soldier.prefab`) |
| Assets/Animations/Enemy/Pensionado/sprite_enemy_pensionado_walk-Sheet.png | New (imported as Sprite, sliced into 4 frames per Maestro convention) |
| Assets/Animations/Enemy/Pensionado/sprite_enemy_pensionado_death-Sheet.png | New (imported as Sprite, sliced into N frames per the §2.7 convention) |
| Assets/Animations/Enemy/General/sprite_enemy_general_walk-Sheet.png | New (imported as Sprite, sliced into 4 frames) |
| Assets/Animations/Enemy/General/sprite_enemy_general_death-Sheet.png | New (imported as Sprite, sliced into N frames) |
| Assets/ScriptableObjects/EnemyData_Pensionado.asset | New |
| Assets/ScriptableObjects/EnemyData_General.asset | New |
| [Assets/_Scenes/Bootstrap.unity](../../../Assets/_Scenes/Bootstrap.unity) | Modified — add two `EnemyPrefabRegistration` rows on `Manager_EnemyPool.EnemyPool._registeredEnemyPrefabs` |

## Commit plan (chronological)

1. `feat(enemy): SALIN-54 add speed-buff API, EffectiveSpeed, and death-animation playback to Enemy`
2. `feat(data): SALIN-54 extend EnemyDataSO with era, zigzag, base-speed, aura, and death-animation fields`
3. `feat(enemy): SALIN-54 add PensionadoMover for sine-wave zigzag descent`
4. `feat(enemy): SALIN-54 add GeneralAura proximity speed-buff component`
5. `feat(enemy): SALIN-54 author Enemy_Pensionado and Enemy_General prefabs`
6. `feat(art): SALIN-54 import and slice Pensionado and General walk and death sprite sheets`
7. `feat(data): SALIN-54 author EnemyData_Pensionado and EnemyData_General assets`
8. `chore(enemy): SALIN-54 register Pensionado and General variants with EnemyPool`
9. `test(enemy): SALIN-54 verify Pensionado zigzag, General aura, and death-animation playback`

§2.1 is verify-only and produces no commit. Commits 1 and 2 must land in this order
(commit 1 references `baseSpeedMultiplier`, `deathFrames`, and `deathAnimationFps`,
which commit 2 introduces — the staging order above is the safe one). Commits 3 and
4 are independent. Commits 5–7 must run after 1–4 because the SO authoring uses the
new fields and the new sliced sprites. Commit 8 is the integration commit that
makes the variants spawnable from waves.
