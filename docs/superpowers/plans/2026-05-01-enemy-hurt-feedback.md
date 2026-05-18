# Enemy Hurt Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a multi-HP enemy takes damage that does not kill it, play a configurable on-hit reaction (movement pause, sprite shake, optional carried-character swap, optional hurt animation), with every behaviour individually opt-out-able from the enemy's `EnemyDataSO`.

**Architecture:** Add a new lightweight sibling component `EnemyHurtFeedback` (mirrors the `PensionadoMover` / `GeneralAura` pattern from SALIN-54). All toggles and tuning live on `EnemyDataSO`, so designers configure per-variant via the existing `EnemyData_*.asset` assets. `Enemy.TakeDamage` notifies the feedback component on a non-lethal hit; the component owns the coroutine that combines pause + shake + character swap + (optional) hurt-frame playback. Hurt frames are an empty `Sprite[]` by default — when the artist delivers art, designers wire them in the Inspector with no code change.

**Tech Stack:** Unity 6 LTS (6000.3.9f1), C# 9.0, NUnit (Unity Test Framework 1.6.0), `UnityEngine.Pool`. No new packages.

**Ticket:** Replace `SALIN-XX` throughout with the actual Jira issue key once filed (per `CLAUDE.md` Git & Jira workflow). Suggested branch name: `feature/SALIN-XX-enemy-hurt-feedback`. **This work is independent of the in-progress SALIN-54 branch** — it should be filed as a separate ticket and merged after SALIN-54 lands, since it depends on the death-animation refactor and `EnemyHurtFeedback` will share the prefab roster with the Pensionado / General variants.

---

## Acceptance criteria → section map

| AC | Where it is satisfied |
|---|---|
| AC-1 Damaging an enemy with `currentHealth > 1` triggers hurt feedback (the kill stroke does not) | [§1.3](#13-modify-enemycs--invoke-hurt-feedback-on-non-lethal-damage) `TakeDamage` branch + [§3.4](#34-test-3--lethal-damage-does-not-trigger-hurt-feedback) test |
| AC-2 Movement pause is configurable (toggle + duration) | [§1.1](#11-extend-enemydataso-with-hurt-feedback-fields) `hurtPausesMovement` / `hurtPauseDuration` + [§1.2](#12-create-enemyhurtfeedbackcs) pause branch + [§3.5](#35-test-4--pause-toggle-stops-the-mover-and-restores-it) test |
| AC-3 Sprite shake is configurable (toggle + magnitude + duration + frequency) | [§1.1](#11-extend-enemydataso-with-hurt-feedback-fields) shake fields + [§1.2](#12-create-enemyhurtfeedbackcs) shake loop + [§3.6](#36-test-5--shake-restores-position-cleanly) test |
| AC-4 Carried character swap is configurable (toggle + replacement character); General opts out | [§1.1](#11-extend-enemydataso-with-hurt-feedback-fields) `hurtSwapsCharacter` / `postHurtCharacter` + [§1.2](#12-create-enemyhurtfeedbackcs) swap branch + [§3.7](#37-test-6--character-swap-fires-once-when-enabled) test + [§2.3](#23-update-enemydata_general) (General leaves swap off) |
| AC-5 Hurt animation is data-driven and optional (empty `hurtFrames` = no animation) | [§1.1](#11-extend-enemydataso-with-hurt-feedback-fields) `hurtFrames` array + [§1.2](#12-create-enemyhurtfeedbackcs) frame stepper + [§3.8](#38-test-7--hurt-animation-plays-when-frames-are-set) test |
| AC-6 Master toggle on data lets a designer fully opt-out a variant without prefab edits | [§1.1](#11-extend-enemydataso-with-hurt-feedback-fields) `useHurtFeedback` + [§1.2](#12-create-enemyhurtfeedbackcs) early-return + [§3.3](#33-test-2--master-toggle-disables-all-feedback) test |
| AC-7 Pool reuse cleans hurt state — no stale swap, no leftover shake offset | [§1.3](#13-modify-enemycs--invoke-hurt-feedback-on-non-lethal-damage) `ResetForPool` integration + [§1.2](#12-create-enemyhurtfeedbackcs) `ResetState` + [§3.9](#39-test-8--resetforpool-clears-hurt-state) test |
| AC-8 Walk animation does not fight hurt animation | [§1.3](#13-modify-enemycs--invoke-hurt-feedback-on-non-lethal-damage) `AdvanceWalkAnimation` guard + [§3.8](#38-test-7--hurt-animation-plays-when-frames-are-set) verification |

---

## Phase 0 audit snapshot

Recorded against the working tree at `feature/SALIN-54-pensionado-and-general-enemies` (commit `7b69acc`). Re-run the audit if this plan is started after SALIN-54 has merged into `main` — line numbers may shift slightly but the symbols and shape will hold.

| Symbol | Location | Status |
|---|---|---|
| `EnemyDataSO` fields | [Assets/Scripts/Data/EnemyDataSO.cs](../../../Assets/Scripts/Data/EnemyDataSO.cs) | currently `enemyID`, `moveSpeed`, `maxHealth`, `walkFrames`, `animatorController`, `assignedCharacter`, `isDecoy`, `dealsContactDamage`, `era`, `zigzagAmplitude`, `zigzagFrequency`, `baseSpeedMultiplier`, `auraRadius`, `auraSpeedMultiplier`, `deathFrames`, `deathAnimationFps`. **None** of the §1.1 hurt-feedback fields are present. |
| `Enemy.TakeDamage(int)` | [Assets/Scripts/Gameplay/Enemy/Enemy.cs:215](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L215) | exists. Already has the `_isDying` re-entry guard from SALIN-54. **Edited** in §1.3 to call `_hurtFeedback?.OnHurt()` after `TriggerShieldBreakVisual()`. |
| `Enemy.AssignCharacter(BaybayinCharacterSO)` | [Assets/Scripts/Gameplay/Enemy/Enemy.cs:101](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L101) | exists. Reused as-is by `EnemyHurtFeedback` to swap the carried character. |
| `Enemy.AdvanceWalkAnimation()` | [Assets/Scripts/Gameplay/Enemy/Enemy.cs:376](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L376) | exists. **Edited** in §1.3 with a single early-return so it does not overwrite the hurt animation's sprite. |
| `Enemy.ResetForPool()` | [Assets/Scripts/Gameplay/Enemy/Enemy.cs:181](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L181) | exists. **Edited** in §1.3 with a single line: `_hurtFeedback?.ResetState();`. |
| `EnemyMover.SetSpeed(float)` / `Stop()` | [Assets/Scripts/Gameplay/Enemy/EnemyMover.cs:15](../../../Assets/Scripts/Gameplay/Enemy/EnemyMover.cs#L15) | exists. `Stop()` sets `_active = false`; `SetSpeed(x)` sets `_active = true` and writes `_speed`. `EnemyHurtFeedback` uses both to pause and resume movement without touching `_speed` directly (so Focus Mode and the General's aura buff remain composable on resume). |
| `Enemy.EffectiveSpeed` | [Assets/Scripts/Gameplay/Enemy/Enemy.cs:55](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs#L55) | exists. `EnemyHurtFeedback` reads this to push the buff-aware speed back into the mover when the pause ends. |
| Existing HP > 1 SOs | [Assets/ScriptableObjects/](../../../Assets/ScriptableObjects/) | `EnemyData_Shielded` (HP 2), `EnemyData_Shokan` (HP 2), `EnemyData_General` (HP 3, authored in SALIN-54). All three need their hurt-feedback fields filled in §2. `EnemyData_Boss` is excluded (boss work is SALIN-68). |
| Existing HP > 1 prefabs | [Assets/Prefabs/Enemies/](../../../Assets/Prefabs/Enemies/) | `[Enemy] Shielded.prefab`, `[Enemy] Shokan.prefab`, `Enemy_General.prefab`. All three get the `EnemyHurtFeedback` sibling in §2.5. The other nine prefabs are HP=1 (kill stroke = first hit) and do not need the component, though attaching it is harmless. |
| `DecoyEnemyTests` fixture pattern | [Assets/Tests/Editor/Gameplay/DecoyEnemyTests.cs](../../../Assets/Tests/Editor/Gameplay/DecoyEnemyTests.cs) | reference for §3 — uses `ScriptableObject.CreateInstance<EnemyDataSO>()` + `AddComponent<Enemy>()` + reflection to drive non-public state. Same pattern reused. |
| `BaybayinCharacterSO.characterID` | [Assets/Scripts/Data/BaybayinCharacterSO.cs:7](../../../Assets/Scripts/Data/BaybayinCharacterSO.cs#L7) | exists. Used by `Enemy.Character` via `_runtimeCharacter ?? _data.assignedCharacter`. The swap on hurt writes `_runtimeCharacter` and is observable through `Enemy.Character`. |

> **NOTE on shake interaction with `PensionadoMover`** `PensionadoMover.Update` overwrites `transform.position.x` absolutely each frame (it reads `baseX + sin(...)`). When a Pensionado is shaken, the X component of the shake offset will be lost on the next `PensionadoMover` update. **This is acceptable** because Pensionado has `maxHealth = 1` — it dies on the first hit and the hurt path never runs. The other HP > 1 enemies in this codebase do not use absolute-position movers, so the additive root-transform shake (§1.2) is correct for them. If a future variant ever has both a `>1` HP and absolute X writes, refactor §1.2 to shake a `Visual` child node instead of the root.

---

## Task 1: Code implementation

### §1.1 Extend `EnemyDataSO` with hurt-feedback fields

**Files:**
- Modify: [Assets/Scripts/Data/EnemyDataSO.cs](../../../Assets/Scripts/Data/EnemyDataSO.cs)

Defaults are deliberately neutral so existing assets that have not been re-saved still behave correctly: `useHurtFeedback = true` with `hurtFrames` empty means HP > 1 enemies will pause and shake on hit (the new default behaviour) while HP = 1 enemies are unaffected (their kill stroke skips the path). `hurtSwapsCharacter = false` means the character only swaps for variants explicitly authored to do so.

- [ ] **Step 1.1.1: Open the file**

  Project window → `Assets/Scripts/Data/EnemyDataSO.cs` → double-click. Confirm the file currently ends at the `Era` enum block (the `deathAnimationFps` field is the last serialized field, `Era` is the last declaration in the file).

- [ ] **Step 1.1.2: Add the field block**

  Position the cursor on a new blank line **immediately after the `deathAnimationFps` field** and **before the closing `}` of the class** (i.e. inside the `EnemyDataSO` class body). Paste:

  ```csharp
      [Header("Hurt Feedback (multi-HP enemies)")]
      [Tooltip("Master toggle. If false, no hurt feedback runs even if EnemyHurtFeedback is on the prefab. HP=1 enemies never trigger hurt feedback regardless of this value (they die on the first hit).")]
      public bool useHurtFeedback = true;

      [Header("Hurt Feedback — Movement Pause")]
      [Tooltip("If true, the enemy stops descending for hurtPauseDuration seconds after a non-lethal hit.")]
      public bool hurtPausesMovement = true;
      [Tooltip("Seconds the enemy stays frozen on hit. 0 disables the pause without touching the toggle.")]
      public float hurtPauseDuration = 0.25f;

      [Header("Hurt Feedback — Sprite Shake")]
      [Tooltip("If true, the sprite jitters around its current position for hurtShakeDuration seconds after a non-lethal hit.")]
      public bool hurtShakesSprite = true;
      [Tooltip("Maximum shake offset per axis in world units. 0.08 ~= 1/12th of a 1x1 sprite.")]
      public float hurtShakeMagnitude = 0.08f;
      [Tooltip("Total seconds the shake plays. Should usually be <= hurtPauseDuration so the shake ends inside the freeze window.")]
      public float hurtShakeDuration = 0.2f;
      [Tooltip("Shake oscillations per second. Higher = more frantic. 30 reads as a sharp jolt; 10 reads as a softer wobble.")]
      public float hurtShakeFrequency = 30f;

      [Header("Hurt Feedback — Character Swap")]
      [Tooltip("If true, the carried character changes to postHurtCharacter on the first non-lethal hit. Leave off for variants that should keep their original glyph (e.g. General).")]
      public bool hurtSwapsCharacter = false;
      [Tooltip("The character the enemy demands after the first non-lethal hit. Only consulted when hurtSwapsCharacter is true.")]
      public BaybayinCharacterSO postHurtCharacter;

      [Header("Hurt Feedback — Hurt Animation (optional)")]
      [Tooltip("Frames played in sequence on a non-lethal hit. Empty = no animation; the sprite stays on the current walk frame. Plug in the artist's hurt sheet here when it arrives — no code change required.")]
      public Sprite[] hurtFrames;
      [Tooltip("Playback FPS for hurtFrames. 0 falls back to the walk animation FPS on Enemy.cs (default 8).")]
      public float hurtAnimationFps = 12f;
  ```

- [ ] **Step 1.1.3: Save the file**

  Save (Ctrl+S).

- [ ] **Step 1.1.4: Verify the compile**

  Switch focus to Unity. Wait for the bottom-status `Compiling...` to clear. Open the Console (`Window → General → Console`, Ctrl+Shift+C). Confirm no red rows.

- [ ] **Step 1.1.5: Sanity check on an existing SO**

  Project window → click any existing `EnemyData_*.asset` (e.g. `EnemyData_Soldado`). Inspector → confirm the new headers appear:
  - `Hurt Feedback (multi-HP enemies)` with `Use Hurt Feedback` checked.
  - `Hurt Feedback — Movement Pause` with toggle on, duration `0.25`.
  - `Hurt Feedback — Sprite Shake` with toggle on, magnitude `0.08`, duration `0.2`, frequency `30`.
  - `Hurt Feedback — Character Swap` with toggle off, `Post Hurt Character` empty.
  - `Hurt Feedback — Hurt Animation (optional)` with `Hurt Frames` size `0`, fps `12`.

  Hover each numeric field — the tooltip text matches the strings in §1.1.2.

- [ ] **Step 1.1.6: Commit**

  ```bash
  git add Assets/Scripts/Data/EnemyDataSO.cs
  git commit -m "feat(data): SALIN-XX add hurt-feedback fields to EnemyDataSO"
  ```

---

### §1.2 Create `EnemyHurtFeedback.cs`

**Files:**
- Create: [Assets/Scripts/Gameplay/Enemy/EnemyHurtFeedback.cs](../../../Assets/Scripts/Gameplay/Enemy/EnemyHurtFeedback.cs)

A sibling component that owns the on-hit reaction. The single coroutine drives all three concurrent beats (pause, shake, hurt-frame stepping) so that timing stays in sync; sub-coroutines would re-introduce the multi-coroutine cleanup bug we already wrote off in `Enemy.PlayDeathAnimationThenReturn`.

- [ ] **Step 1.2.1: Create the script file**

  Project window → `Assets/Scripts/Gameplay/Enemy/`. Right-click the `Enemy` folder → `Create → Scripting → MonoBehaviour Script`. Type `EnemyHurtFeedback` → Enter. The file becomes `EnemyHurtFeedback.cs` and sits next to `Enemy.cs`, `EnemyMover.cs`, `PensionadoMover.cs`, and `GeneralAura.cs`.

- [ ] **Step 1.2.2: Paste the body**

  Double-click `EnemyHurtFeedback.cs` to open it. Select all (Ctrl+A), delete, and paste the body verbatim:

  ```csharp
  using System.Collections;
  using UnityEngine;

  // Sibling component on an Enemy prefab. Plays a configurable on-hit reaction
  // (movement pause, sprite shake, optional character swap, optional hurt-frame
  // animation) when the Enemy takes non-lethal damage. All toggles and tuning
  // come from EnemyDataSO so designers configure per variant.
  [RequireComponent(typeof(Enemy))]
  [RequireComponent(typeof(EnemyMover))]
  public class EnemyHurtFeedback : MonoBehaviour
  {
      private Enemy _enemy;
      private EnemyMover _mover;
      private SpriteRenderer _renderer;

      private bool _hasSwappedCharacter;
      private Coroutine _hurtRoutine;

      public bool IsPlayingHurtAnimation => _hurtRoutine != null;

      private void Awake()
      {
          _enemy = GetComponent<Enemy>();
          _mover = GetComponent<EnemyMover>();
          _renderer = GetComponent<SpriteRenderer>();
      }

      private void OnDisable()
      {
          // Pool return / scene unload — drop hurt state silently so the next
          // spawn starts clean.
          ResetState();
      }

      // Called by Enemy.ResetForPool so the next reuse from the pool starts clean.
      public void ResetState()
      {
          if (_hurtRoutine != null)
          {
              StopCoroutine(_hurtRoutine);
              _hurtRoutine = null;
          }
          _hasSwappedCharacter = false;
      }

      // Called by Enemy.TakeDamage on a non-lethal hit (currentHealth > 0).
      public void OnHurt()
      {
          if (_enemy == null) return;
          EnemyDataSO data = _enemy.Data;
          if (data == null || !data.useHurtFeedback) return;

          // If a hurt routine is already in flight, do not stack — the existing
          // one will see the latest data on its next tick. This keeps timing stable
          // when an enemy is hit twice in rapid succession.
          if (_hurtRoutine != null) return;

          if (data.hurtSwapsCharacter
              && !_hasSwappedCharacter
              && data.postHurtCharacter != null)
          {
              _enemy.AssignCharacter(data.postHurtCharacter);
              _hasSwappedCharacter = true;
          }

          _hurtRoutine = StartCoroutine(PlayHurt(data));
      }

      private IEnumerator PlayHurt(EnemyDataSO data)
      {
          bool pause = data.hurtPausesMovement && data.hurtPauseDuration > 0f;
          bool shake = data.hurtShakesSprite
                       && data.hurtShakeDuration > 0f
                       && data.hurtShakeMagnitude > 0f;
          bool anim = data.hurtFrames != null
                      && data.hurtFrames.Length > 0
                      && _renderer != null;

          float pauseDur = pause ? data.hurtPauseDuration : 0f;
          float shakeDur = shake ? data.hurtShakeDuration : 0f;
          float animFps = data.hurtAnimationFps > 0f ? data.hurtAnimationFps : 8f;
          float animFrameDur = anim ? (1f / animFps) : 0f;
          float animTotalDur = anim ? (data.hurtFrames.Length * animFrameDur) : 0f;
          float totalDur = Mathf.Max(pauseDur, shakeDur, animTotalDur);

          if (pause) _mover?.Stop();

          Vector3 appliedShake = Vector3.zero;
          int animFrameIndex = -1;
          bool resumed = !pause;
          float t = 0f;

          while (t < totalDur)
          {
              if (shake)
              {
                  // Subtract last frame's offset so the mover's contribution is
                  // preserved on the root transform. Then add this frame's offset.
                  transform.position -= appliedShake;

                  if (t < shakeDur)
                  {
                      float angle = t * data.hurtShakeFrequency * Mathf.PI * 2f;
                      float decay = 1f - Mathf.Clamp01(t / shakeDur);
                      Vector3 next = new Vector3(
                          Mathf.Sin(angle) * data.hurtShakeMagnitude * decay,
                          Mathf.Cos(angle * 1.7f) * data.hurtShakeMagnitude * decay,
                          0f);
                      transform.position += next;
                      appliedShake = next;
                  }
                  else
                  {
                      appliedShake = Vector3.zero;
                  }
              }

              if (anim && t < animTotalDur)
              {
                  int wantIdx = Mathf.Min(
                      (int)(t / animFrameDur),
                      data.hurtFrames.Length - 1);
                  if (wantIdx != animFrameIndex)
                  {
                      animFrameIndex = wantIdx;
                      Sprite frame = data.hurtFrames[animFrameIndex];
                      if (frame != null) _renderer.sprite = frame;
                  }
              }

              if (!resumed && t >= pauseDur)
              {
                  // Pause window has elapsed — re-apply EffectiveSpeed (which
                  // includes any aura buffs and Focus Mode) to the mover.
                  if (_mover != null && _enemy != null)
                      _mover.SetSpeed(_enemy.EffectiveSpeed);
                  resumed = true;
              }

              yield return null;
              t += Time.deltaTime;
          }

          // Cleanup: remove any leftover shake offset from the root transform.
          if (shake) transform.position -= appliedShake;

          // Belt-and-suspenders: if the loop exited before the resume branch ran,
          // make sure movement resumes.
          if (!resumed && _mover != null && _enemy != null)
              _mover.SetSpeed(_enemy.EffectiveSpeed);

          _hurtRoutine = null;
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 1.2.3: Verify compile**

  Switch to Unity, wait for `Compiling...` to clear, confirm Console is clean.

  > **NOTE on `[RequireComponent(typeof(EnemyMover))]`** Adding this attribute means Unity will refuse to remove the `EnemyMover` from a prefab that has `EnemyHurtFeedback`. That is intentional — the pause branch calls `_mover.Stop()` and `_mover.SetSpeed(...)`. If a future variant has no mover (e.g. a stationary turret), drop this attribute and add a null guard to the call sites.

  > **NOTE on the `IsPlayingHurtAnimation` property** This is the single contract `Enemy.cs` uses (§1.3) to suppress walk-frame stepping while a hurt frame is showing. It returns `_hurtRoutine != null`, which is true for the entire hurt window — even if `hurtFrames` is empty. That is harmless: when no frames are configured the property is true but the routine never writes `_renderer.sprite`, so `Enemy.AdvanceWalkAnimation`'s skip merely freezes the walk frame for the duration of the pause. Players read this as "the enemy stopped walking", which is exactly the visual we want.

- [ ] **Step 1.2.4: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Enemy/EnemyHurtFeedback.cs
  git commit -m "feat(enemy): SALIN-XX add EnemyHurtFeedback sibling component"
  ```

---

### §1.3 Modify `Enemy.cs` — invoke hurt feedback on non-lethal damage

**Files:**
- Modify: [Assets/Scripts/Gameplay/Enemy/Enemy.cs](../../../Assets/Scripts/Gameplay/Enemy/Enemy.cs)

Three edits:
1. Cache an `EnemyHurtFeedback` reference in `Awake`.
2. In `TakeDamage`, after the non-lethal branch, call `_hurtFeedback?.OnHurt()`.
3. In `AdvanceWalkAnimation`, return early if a hurt animation is playing.
4. In `ResetForPool`, call `_hurtFeedback?.ResetState()`.

- [ ] **Step 1.3.1: Open the file and add the cached field**

  Open `Assets/Scripts/Gameplay/Enemy/Enemy.cs`. Use Ctrl+F to jump to the existing `private EnemyMover _mover;` field declaration (currently around line 31). Position the cursor at the end of that line and press Enter. On the new line, type:

  ```csharp
      private EnemyHurtFeedback _hurtFeedback;
  ```

  Save (Ctrl+S).

- [ ] **Step 1.3.2: Cache the reference in `Awake`**

  Use Ctrl+F to jump to `private void Awake()` (currently around line 83). The current body is:

  ```csharp
      private void Awake()
      {
          _mover = GetComponent<EnemyMover>();
          _renderer = GetComponent<SpriteRenderer>();

          if (_renderer != null)
              _baseRendererColor = _renderer.color;

          EnsureDebugLabels();
          RefreshDebugLabels();
      }
  ```

  Position the cursor at the end of the `_renderer = GetComponent<SpriteRenderer>();` line, press Enter, and add:

  ```csharp
          _hurtFeedback = GetComponent<EnemyHurtFeedback>();
  ```

  The opening of the method should now read:

  ```csharp
      private void Awake()
      {
          _mover = GetComponent<EnemyMover>();
          _renderer = GetComponent<SpriteRenderer>();
          _hurtFeedback = GetComponent<EnemyHurtFeedback>();
  ```

  Save (Ctrl+S).

  > **NOTE** `GetComponent<EnemyHurtFeedback>()` returns `null` on prefabs that do not have the component (most enemies). All call sites use `?.` so a null cache is harmless.

- [ ] **Step 1.3.3: Wire the hurt call in `TakeDamage`**

  Use Ctrl+F to jump to `public void TakeDamage(int amount)` (currently line 215). The current body of the post-damage block is:

  ```csharp
          if (_currentHealth <= 0)
          {
              Defeat();
          }
          else if (ShouldTriggerShieldBreak(previousHealth))
          {
              TriggerShieldBreakVisual();
          }
  ```

  Replace the entire `if/else if` block above with:

  ```csharp
          if (_currentHealth <= 0)
          {
              Defeat();
          }
          else
          {
              if (ShouldTriggerShieldBreak(previousHealth))
                  TriggerShieldBreakVisual();

              _hurtFeedback?.OnHurt();
          }
  ```

  Save (Ctrl+S).

  > **NOTE on order vs. `TriggerShieldBreakVisual`** The shield-break placeholder writes `_renderer.color` once. The hurt routine never touches `_renderer.color`, only `_renderer.sprite`. The two are orthogonal so call order does not matter; the structure above is chosen for readability — visual color first, then the larger reaction routine.

- [ ] **Step 1.3.4: Suppress walk animation while hurt animation plays**

  Use Ctrl+F to jump to `private void AdvanceWalkAnimation()` (currently line 376). The current first lines after the `{` are:

  ```csharp
      private void AdvanceWalkAnimation()
      {
          if (_renderer == null || _data == null || _data.walkFrames == null)
              return;
  ```

  Add a single guard immediately after the opening `{` and before the existing null check:

  ```csharp
      private void AdvanceWalkAnimation()
      {
          if (_hurtFeedback != null && _hurtFeedback.IsPlayingHurtAnimation)
              return;

          if (_renderer == null || _data == null || _data.walkFrames == null)
              return;
  ```

  Save (Ctrl+S).

- [ ] **Step 1.3.5: Reset hurt state on pool return**

  Use Ctrl+F to jump to `public void ResetForPool()` (currently line 181). Inside the `try` block, find the line `_speedBuffs.Clear();` (added by SALIN-54). Position the cursor at the end of that line, press Enter, and add:

  ```csharp
              _hurtFeedback?.ResetState();
  ```

  The opening of the `try` block should now read:

  ```csharp
          try
          {
              _runtimeCharacter = null;
              _speedBuffs.Clear();
              _hurtFeedback?.ResetState();
              _isDying = false;
  ```

  Save (Ctrl+S).

  > **NOTE** `EnemyHurtFeedback.OnDisable` also calls `ResetState()` when the GameObject deactivates inside `EnemyPool.OnRelease`. The explicit call from `ResetForPool` is belt-and-suspenders for two cases: (1) `Initialize` failing mid-way and short-circuiting the pool flow; (2) tests that drive the enemy directly without going through the pool. Calling `ResetState` twice is idempotent.

- [ ] **Step 1.3.6: Verify compile**

  Switch to Unity, wait for `Compiling...` to clear, confirm Console is clean. If you see `CS0246: 'EnemyHurtFeedback' could not be found`, §1.2 has not been completed — finish it first and return.

- [ ] **Step 1.3.7: Commit**

  ```bash
  git add Assets/Scripts/Gameplay/Enemy/Enemy.cs
  git commit -m "feat(enemy): SALIN-XX wire hurt feedback into Enemy lifecycle"
  ```

---

## Task 2: Manual Unity configuration — data and prefabs

Code is now functional but every existing variant has the new fields at their defaults (which means the new HP > 1 enemies will pause + shake, but no character swap and no hurt animation). This task wires the design intent per variant.

### §2.1 Update `EnemyData_Shielded`

**Files:**
- Modify (in Unity Inspector, written to disk on save): [Assets/ScriptableObjects/EnemyData_Shielded.asset](../../../Assets/ScriptableObjects/EnemyData_Shielded.asset)

Shielded is the basic two-hit "mash through the shield" variant. It pauses + shakes on shield break; no character swap (the assigned glyph is the one the player solved for and we want them to see it land twice).

- [ ] **Step 2.1.1: Open the asset**

  Project window → `Assets/ScriptableObjects/EnemyData_Shielded.asset` → click once. Inspector shows every field, including the new `Hurt Feedback` headers.

- [ ] **Step 2.1.2: Configure the values**

  Set the following values exactly. Leave everything else (the existing fields above the new headers) untouched.

  | Field | Value |
  |---|---|
  | Hurt Feedback (multi-HP enemies) → Use Hurt Feedback | checked |
  | Hurt Feedback — Movement Pause → Hurt Pauses Movement | checked |
  | Hurt Feedback — Movement Pause → Hurt Pause Duration | `0.25` |
  | Hurt Feedback — Sprite Shake → Hurt Shakes Sprite | checked |
  | Hurt Feedback — Sprite Shake → Hurt Shake Magnitude | `0.08` |
  | Hurt Feedback — Sprite Shake → Hurt Shake Duration | `0.2` |
  | Hurt Feedback — Sprite Shake → Hurt Shake Frequency | `30` |
  | Hurt Feedback — Character Swap → Hurt Swaps Character | unchecked |
  | Hurt Feedback — Character Swap → Post Hurt Character | leave `None` |
  | Hurt Feedback — Hurt Animation (optional) → Hurt Frames | leave Size `0` |
  | Hurt Feedback — Hurt Animation (optional) → Hurt Animation Fps | `12` |

- [ ] **Step 2.1.3: Save the asset**

  `File → Save` (Ctrl+S). The on-disk YAML for `EnemyData_Shielded.asset` now includes the new fields explicitly (Unity writes them on first save after the SO schema changed in §1.1).

### §2.2 Update `EnemyData_Shokan`

Shokan is the Japanese-era equivalent of Shielded with the same two-hit pacing. Use the same configuration as §2.1.

- [ ] **Step 2.2.1: Open the asset**

  Project window → `Assets/ScriptableObjects/EnemyData_Shokan.asset` → click once.

- [ ] **Step 2.2.2: Apply the same values as §2.1.2**

  Identical table — pause + shake on, character swap off, hurt frames empty.

- [ ] **Step 2.2.3: Save**

  Ctrl+S.

### §2.3 Update `EnemyData_General`

The General is the "boss-aura commander" with HP=3. Per the user request, it **opts out of the character swap** (its assigned character should remain its tactical identifier even after damage) but keeps the pause and shake so the player gets clear hit feedback during the three-hit kill sequence.

- [ ] **Step 2.3.1: Open the asset**

  Project window → `Assets/ScriptableObjects/EnemyData_General.asset` → click once.

- [ ] **Step 2.3.2: Configure the values**

  | Field | Value |
  |---|---|
  | Hurt Feedback (multi-HP enemies) → Use Hurt Feedback | checked |
  | Hurt Feedback — Movement Pause → Hurt Pauses Movement | checked |
  | Hurt Feedback — Movement Pause → Hurt Pause Duration | `0.3` (slightly longer than the rank-and-file — General is a heavier visual presence) |
  | Hurt Feedback — Sprite Shake → Hurt Shakes Sprite | checked |
  | Hurt Feedback — Sprite Shake → Hurt Shake Magnitude | `0.1` (slightly stronger than rank-and-file) |
  | Hurt Feedback — Sprite Shake → Hurt Shake Duration | `0.25` |
  | Hurt Feedback — Sprite Shake → Hurt Shake Frequency | `30` |
  | Hurt Feedback — Character Swap → **Hurt Swaps Character** | **unchecked** ← General opts out per design |
  | Hurt Feedback — Character Swap → Post Hurt Character | leave `None` |
  | Hurt Feedback — Hurt Animation (optional) → Hurt Frames | leave Size `0` |
  | Hurt Feedback — Hurt Animation (optional) → Hurt Animation Fps | `12` |

- [ ] **Step 2.3.3: Save**

  Ctrl+S.

  > **NOTE on aura interaction** While the General is paused, its `GeneralAura` keeps ticking (the aura coroutine does not check the mover's `_active` flag). Buffs on nearby American-era enemies stay applied for the full pause window. This is correct — the General is still alive and present, just temporarily stunned by the player's hit.

### §2.4 Optional: configure character swap on a single example variant

To exercise the swap path end-to-end during playtest, configure **one** existing variant to swap. This is the cheapest way to confirm the wiring works without waiting for designer decisions on every glyph. **This step is optional** — skip it if you'd rather author the swap behaviour later in a design pass.

- [ ] **Step 2.4.1: Pick a candidate**

  Use `EnemyData_Shielded` (already configured in §2.1) for the example. The existing `assignedCharacter` is the Shielded's first-hit glyph; the swap glyph should be a Chapter-1 character that is unlocked by the time Shielded variants appear.

- [ ] **Step 2.4.2: Toggle on and wire**

  Re-open `EnemyData_Shielded.asset`. In the `Hurt Feedback — Character Swap` section:
  - Check `Hurt Swaps Character`.
  - Click the small ⊙ target picker icon next to `Post Hurt Character`. In the picker search, type a character ID different from the asset's existing `assignedCharacter` (e.g. if `assignedCharacter` is `Char_BA`, choose `Char_KA`). Double-click the result. The slot binds.
  - Save (Ctrl+S).

  > **NOTE** Revert this for the merged build if it conflicts with intended design — it is purely a test fixture for §4 playtest. Better long-term: leave Shielded with `hurtSwapsCharacter = false` and add a new SO `EnemyData_TwoFaced` that ships with the swap on as its identity.

### §2.5 Add `EnemyHurtFeedback` to HP > 1 prefabs

**Files:**
- Modify (Unity Prefab Mode):
  - [Assets/Prefabs/Enemies/[Enemy] Shielded.prefab](../../../Assets/Prefabs/Enemies/%5BEnemy%5D%20Shielded.prefab)
  - [Assets/Prefabs/Enemies/[Enemy] Shokan.prefab](../../../Assets/Prefabs/Enemies/%5BEnemy%5D%20Shokan.prefab)
  - [Assets/Prefabs/Enemies/Enemy_General.prefab](../../../Assets/Prefabs/Enemies/Enemy_General.prefab)

`Enemy.Awake` calls `GetComponent<EnemyHurtFeedback>()`. Without the component on the prefab, the cached reference is `null` and all hurt-feedback calls become no-ops. So this step is what actually turns the feature on for these three variants.

- [ ] **Step 2.5.1: Add to `[Enemy] Shielded`**

  - (a) Project window → `Assets/Prefabs/Enemies/[Enemy] Shielded.prefab` → double-click. Scene view enters Prefab Mode (breadcrumb bar shows `< Scenes / [Enemy] Shielded`).
  - (b) Hierarchy → select the root `[Enemy] Shielded` GameObject. Inspector shows `Enemy`, `EnemyMover`, `Collider2D`, `SpriteRenderer`.
  - (c) Inspector → bottom → click `Add Component`. In the search field, type `EnemyHurtFeedback`. A single result appears.
  - (d) Click the result. The component is added to the root with no serialized fields (it auto-grabs `Enemy`, `EnemyMover`, and `SpriteRenderer` siblings at runtime via `RequireComponent` and `Awake`).
  - (e) Breadcrumb bar `<` to exit Prefab Mode. Click `Save` if prompted.
  - (f) `File → Save Project` so the prefab change writes to disk.

- [ ] **Step 2.5.2: Add to `[Enemy] Shokan`**

  Repeat §2.5.1 steps (a)–(f) using `Assets/Prefabs/Enemies/[Enemy] Shokan.prefab`.

- [ ] **Step 2.5.3: Add to `Enemy_General`**

  Repeat §2.5.1 steps (a)–(f) using `Assets/Prefabs/Enemies/Enemy_General.prefab`. The General prefab also has `GeneralAura` and (optionally) `PensionadoMover` siblings — `EnemyHurtFeedback` sits alongside them on the same root GameObject.

- [ ] **Step 2.5.4: Commit the prefab changes**

  ```bash
  git add "Assets/Prefabs/Enemies/[Enemy] Shielded.prefab" "Assets/Prefabs/Enemies/[Enemy] Shokan.prefab" "Assets/Prefabs/Enemies/Enemy_General.prefab" Assets/ScriptableObjects/EnemyData_Shielded.asset Assets/ScriptableObjects/EnemyData_Shokan.asset Assets/ScriptableObjects/EnemyData_General.asset
  git commit -m "chore(enemy): SALIN-XX attach EnemyHurtFeedback to multi-HP variants"
  ```

  > **NOTE on the other nine prefabs** `[Enemy] Soldado`, `[Enemy] Sprinter`, `[Enemy] Heitai`, `[Enemy] Maestro`, `[Enemy] Soldier`, `[Enemy] Kempei`, `[Enemy] Kisha`, `[Enemy] Pensionado`, `[Enemy] Boss` are HP=1 and never enter the non-lethal branch, so they do **not** need `EnemyHurtFeedback`. Adding it is harmless but pointless; skipping the work is the right call. (`[Enemy] Boss` becomes a multi-HP variant in SALIN-68; address hurt feedback there, not here.)

---

## Task 3: Tests

**Files:**
- Create: `Assets/Tests/Editor/Gameplay/EnemyHurtFeedbackTests.cs`

EditMode tests using the `DecoyEnemyTests` fixture pattern (`ScriptableObject.CreateInstance` + `AddComponent` + reflection for non-public state). Tests run synchronously in the editor — coroutines that yield `null` advance one Unity frame per `yield return`, so we use `[UnityTest]` + `IEnumerator` and the `EditorApplication.QueuePlayerLoopUpdate()`-style frame stepping that NUnit's Unity integration handles automatically.

The tests cover the eight ACs end-to-end through `Enemy.TakeDamage` so the wiring in §1.3 is also exercised.

- [ ] **Step 3.1: Create the test file**

  Project window → `Assets/Tests/Editor/Gameplay/` → right-click empty space → `Create → C# Script`. Name it `EnemyHurtFeedbackTests`. Open the file.

- [ ] **Step 3.2: Test 1 — non-lethal hit calls `OnHurt`**

  Replace the entire file with the body below. Each `[Test]` block corresponds to one AC; running the file gives you the full row.

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
      public class EnemyHurtFeedbackTests
      {
          private readonly List<Object> _objectsToDestroy = new();

          [TearDown]
          public void TearDown()
          {
              for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
              {
                  if (_objectsToDestroy[i] != null)
                      Object.DestroyImmediate(_objectsToDestroy[i]);
              }
              _objectsToDestroy.Clear();
          }

          [Test]
          public void NonLethalHit_StartsHurtRoutine()
          {
              EnemyDataSO data = CreateData(maxHealth: 2);
              Enemy enemy = CreateEnemyWithFeedback(data);
              EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

              enemy.TakeDamage(1);

              Assert.IsTrue(feedback.IsPlayingHurtAnimation,
                  "Expected hurt routine to start after a non-lethal hit.");
              Assert.AreEqual(1, enemy.CurrentHealth);
          }

          [Test]
          public void MasterToggleOff_DoesNothing()
          {
              EnemyDataSO data = CreateData(maxHealth: 2);
              data.useHurtFeedback = false;
              Enemy enemy = CreateEnemyWithFeedback(data);
              EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

              enemy.TakeDamage(1);

              Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                  "Expected hurt routine to stay idle when useHurtFeedback is false.");
          }

          [Test]
          public void LethalHit_DoesNotStartHurtRoutine()
          {
              EnemyDataSO data = CreateData(maxHealth: 1);
              Enemy enemy = CreateEnemyWithFeedback(data);
              EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

              enemy.TakeDamage(1);

              Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                  "Expected hurt routine to stay idle when the hit is lethal.");
              Assert.AreEqual(0, enemy.CurrentHealth);
          }

          [UnityTest]
          public IEnumerator PauseToggle_StopsAndResumesMover()
          {
              EnemyDataSO data = CreateData(maxHealth: 2);
              data.hurtPauseDuration = 0.05f;
              data.hurtShakesSprite = false;       // isolate to pause behaviour
              data.hurtPausesMovement = true;
              Enemy enemy = CreateEnemyWithFeedback(data);
              EnemyMover mover = enemy.GetComponent<EnemyMover>();

              enemy.TakeDamage(1);

              // After the hit, mover should be stopped.
              Assert.IsFalse(mover.IsMoving, "Expected mover to be stopped during pause.");

              // Wait long enough for the pause to elapse.
              float waited = 0f;
              while (waited < 0.2f)
              {
                  yield return null;
                  waited += Time.deltaTime;
              }

              Assert.IsTrue(mover.IsMoving, "Expected mover to resume after pause window.");
          }

          [UnityTest]
          public IEnumerator Shake_RestoresPositionOnExit()
          {
              EnemyDataSO data = CreateData(maxHealth: 2);
              data.hurtPausesMovement = false;     // isolate shake (so mover does not translate Y)
              data.hurtShakesSprite = true;
              data.hurtShakeMagnitude = 0.5f;
              data.hurtShakeDuration = 0.05f;
              data.hurtShakeFrequency = 20f;
              Enemy enemy = CreateEnemyWithFeedback(data);
              Vector3 before = enemy.transform.position;

              enemy.TakeDamage(1);

              // Let the shake play out.
              float waited = 0f;
              while (waited < 0.2f)
              {
                  yield return null;
                  waited += Time.deltaTime;
              }

              Vector3 after = enemy.transform.position;
              Assert.That((after - before).magnitude, Is.LessThan(0.001f),
                  "Expected shake to leave the root position unchanged on exit.");
          }

          [Test]
          public void CharacterSwap_FiresOnceWhenEnabled()
          {
              BaybayinCharacterSO original = CreateCharacter("BA", "ba");
              BaybayinCharacterSO replacement = CreateCharacter("KA", "ka");
              EnemyDataSO data = CreateData(maxHealth: 3);
              data.assignedCharacter = original;
              data.hurtSwapsCharacter = true;
              data.postHurtCharacter = replacement;
              Enemy enemy = CreateEnemyWithFeedback(data);

              enemy.TakeDamage(1);
              Assert.AreSame(replacement, enemy.Character,
                  "Expected character to swap after first non-lethal hit.");

              // Second hit: still alive (HP 3 -> 1). Character should not flip again.
              enemy.TakeDamage(1);
              Assert.AreSame(replacement, enemy.Character,
                  "Expected character to stay swapped on subsequent hits.");
          }

          [Test]
          public void CharacterSwap_StaysOriginalWhenDisabled()
          {
              BaybayinCharacterSO original = CreateCharacter("BA", "ba");
              BaybayinCharacterSO replacement = CreateCharacter("KA", "ka");
              EnemyDataSO data = CreateData(maxHealth: 2);
              data.assignedCharacter = original;
              data.hurtSwapsCharacter = false;
              data.postHurtCharacter = replacement;
              Enemy enemy = CreateEnemyWithFeedback(data);

              enemy.TakeDamage(1);

              Assert.AreSame(original, enemy.Character,
                  "Expected character to remain original when hurtSwapsCharacter is false.");
          }

          [UnityTest]
          public IEnumerator HurtFrames_PlayWhenSet()
          {
              Sprite frame0 = CreateSolidSprite(Color.red);
              Sprite frame1 = CreateSolidSprite(Color.yellow);
              EnemyDataSO data = CreateData(maxHealth: 2);
              data.hurtPausesMovement = false;
              data.hurtShakesSprite = false;
              data.hurtFrames = new[] { frame0, frame1 };
              data.hurtAnimationFps = 10f;     // 0.1s per frame, 0.2s total
              Enemy enemy = CreateEnemyWithFeedback(data);
              SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

              enemy.TakeDamage(1);

              // First frame should be visible immediately (or by the next frame).
              yield return null;
              Assert.AreSame(frame0, renderer.sprite,
                  "Expected first hurt frame to be applied.");

              // Wait long enough to advance into the second frame.
              float waited = 0f;
              while (waited < 0.15f)
              {
                  yield return null;
                  waited += Time.deltaTime;
              }
              Assert.AreSame(frame1, renderer.sprite,
                  "Expected second hurt frame after one frame duration elapsed.");
          }

          [Test]
          public void ResetForPool_ClearsHurtState()
          {
              EnemyDataSO data = CreateData(maxHealth: 2);
              Enemy enemy = CreateEnemyWithFeedback(data);
              EnemyHurtFeedback feedback = enemy.GetComponent<EnemyHurtFeedback>();

              enemy.TakeDamage(1);
              Assert.IsTrue(feedback.IsPlayingHurtAnimation);

              enemy.ResetForPool();

              Assert.IsFalse(feedback.IsPlayingHurtAnimation,
                  "Expected hurt routine to be cleared on ResetForPool.");
          }

          // ----- helpers -----

          private EnemyDataSO CreateData(int maxHealth)
          {
              EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
              data.enemyID = "test";
              data.moveSpeed = 1f;
              data.maxHealth = maxHealth;
              data.assignedCharacter = CreateCharacter("BA", "ba");
              data.dealsContactDamage = true;
              data.useHurtFeedback = true;
              data.hurtPausesMovement = true;
              data.hurtPauseDuration = 0.05f;
              data.hurtShakesSprite = true;
              data.hurtShakeMagnitude = 0.05f;
              data.hurtShakeDuration = 0.05f;
              data.hurtShakeFrequency = 20f;
              data.hurtSwapsCharacter = false;
              _objectsToDestroy.Add(data);
              return data;
          }

          private BaybayinCharacterSO CreateCharacter(string id, string syllable)
          {
              BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
              character.characterID = id;
              character.syllable = syllable;
              _objectsToDestroy.Add(character);
              return character;
          }

          private Sprite CreateSolidSprite(Color color)
          {
              Texture2D tex = new Texture2D(2, 2);
              Color[] pixels = new Color[4];
              for (int i = 0; i < 4; i++) pixels[i] = color;
              tex.SetPixels(pixels);
              tex.Apply();
              Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
              _objectsToDestroy.Add(tex);
              _objectsToDestroy.Add(sprite);
              return sprite;
          }

          private Enemy CreateEnemyWithFeedback(EnemyDataSO data)
          {
              GameObject go = new GameObject("Enemy_Test");
              go.SetActive(false);
              go.AddComponent<SpriteRenderer>();
              go.AddComponent<BoxCollider2D>();
              go.AddComponent<EnemyMover>();
              Enemy enemy = go.AddComponent<Enemy>();
              go.AddComponent<EnemyHurtFeedback>();
              SetPrivateField(enemy, "_showDebugLabels", false);
              go.SetActive(true);
              _objectsToDestroy.Add(go);

              Assert.IsTrue(enemy.Initialize(data));
              return enemy;
          }

          private static void SetPrivateField(object target, string fieldName, object value)
          {
              FieldInfo field = target.GetType().GetField(
                  fieldName,
                  BindingFlags.Instance | BindingFlags.NonPublic);
              Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
              field.SetValue(target, value);
          }
      }
  }
  ```

  Save (Ctrl+S).

- [ ] **Step 3.3: Run all tests once and confirm they fail in the expected way**

  Unity menu bar → `Window → General → Test Runner`. Switch to the `EditMode` tab. Click `Run All`.

  > **WARNING** If you're running tests **before** §1.1–§1.3 are implemented (true TDD ordering), every test should fail with one of:
  > - `CS0117: 'EnemyDataSO' does not contain a definition for 'useHurtFeedback'` (means §1.1 not done)
  > - `CS0246: 'EnemyHurtFeedback' could not be found` (means §1.2 not done)
  > - `Expected hurt routine to start after a non-lethal hit` (means §1.3 not done — the most likely intermediate failure)
  >
  > If you're running tests **after** §1.1–§1.3, every test should pass on the first run. If any fail, do **not** edit the test to make it pass — debug the production code instead (see superpowers:systematic-debugging).

- [ ] **Step 3.4: Test 3 — lethal damage does not trigger hurt feedback**

  Already covered by `LethalHit_DoesNotStartHurtRoutine` in §3.2. Confirm the green check on this row in Test Runner.

- [ ] **Step 3.5: Test 4 — pause toggle stops the mover and restores it**

  Already covered by `PauseToggle_StopsAndResumesMover`. Confirm the green check.

- [ ] **Step 3.6: Test 5 — shake restores position cleanly**

  Already covered by `Shake_RestoresPositionOnExit`. Confirm the green check.

- [ ] **Step 3.7: Test 6 — character swap fires once when enabled**

  Already covered by `CharacterSwap_FiresOnceWhenEnabled` and `CharacterSwap_StaysOriginalWhenDisabled`. Confirm both green.

- [ ] **Step 3.8: Test 7 — hurt animation plays when frames are set**

  Already covered by `HurtFrames_PlayWhenSet`. Confirm the green check.

- [ ] **Step 3.9: Test 8 — `ResetForPool` clears hurt state**

  Already covered by `ResetForPool_ClearsHurtState`. Confirm the green check.

- [ ] **Step 3.10: Commit**

  ```bash
  git add Assets/Tests/Editor/Gameplay/EnemyHurtFeedbackTests.cs
  git commit -m "test(enemy): SALIN-XX cover EnemyHurtFeedback behaviour"
  ```

---

## Task 4: Manual playtest verification

This is the final smoke test in the editor — exercise the feature in a real scene and confirm the visual feel matches the design intent.

- [ ] **Step 4.1: Open the gameplay scene**

  Unity menu bar → `File → Open Scene` → `Assets/_Scenes/Bootstrap.unity`. Press Play. The Bootstrap → MainMenu → Gameplay flow runs as normal; choose a level that contains a Shielded variant (any Chapter-1 level with the shielded soldier wave).

- [ ] **Step 4.2: Verify Shielded pause + shake**

  When a `[Enemy] Shielded` appears, draw its assigned character once. Expected:
  - The enemy's HP drops from 2 → 1.
  - The sprite jitters around its position for ~0.2s (shake).
  - The enemy stops descending for ~0.25s (pause).
  - The walk animation freezes during the pause and resumes when the pause ends.
  - The Console shows `Enemy [BA] took 1 damage. HP: 1` (or whatever character).

  Draw the character a second time. The kill stroke fires the existing death animation (or instant pool-return if `deathFrames` is empty), and the hurt routine does **not** play (because `_currentHealth <= 0` short-circuits before `_hurtFeedback?.OnHurt()`).

- [ ] **Step 4.3: Verify General pause + shake + no swap**

  Advance to a wave that contains a General. Draw its assigned character three times. Each non-lethal hit pauses + shakes; the carried-character label stays the same across all three hits (because `hurtSwapsCharacter` is unchecked on `EnemyData_General`). The third hit kills as normal.

- [ ] **Step 4.4: (Optional) Verify Shielded character swap**

  Only if §2.4 is configured. Spawn a Shielded, hit it once. The on-screen Baybayin label flips from the original glyph (e.g. `Draw: ba (BA)`) to the swap glyph (e.g. `Draw: ka (KA)`). The player must now draw the new glyph to land the second hit.

- [ ] **Step 4.5: Confirm pool reuse is clean**

  Let the wave finish (kill all enemies or let them reach the shrine). Trigger a new wave. The next Shielded that spawns from the pool should:
  - Show the original `assignedCharacter` (not the swap glyph from the previous instance).
  - Have full `maxHealth` (the shield-break visual is reset).
  - Not be in a pre-paused or pre-shaken state — first-frame movement should be normal.

  If any of those is wrong, regress to §1.3.5 — `ResetForPool` is not calling `ResetState`.

- [ ] **Step 4.6: Stop play, write up the verification result**

  Stop play. The plan's ACs are satisfied. Move on to the future-asset preparation (Task 5) or close out the ticket.

---

## Task 5: Future — wire hurt-frame art when the artist delivers it

When the artist eventually supplies hurt-animation sprite sheets per variant, no code change is needed — only data wiring. This task is a **forward-looking checklist**, not part of the current implementation.

- [ ] **Step 5.1: Receive the hurt sheet PNG(s) from the artist**

  Naming convention (mirrors the SALIN-54 walk/death sheets):
  - `Assets/Animations/Enemy/Shielded/sprite_enemy_shielded_hurt-Sheet.png`
  - `Assets/Animations/Enemy/Shokan/sprite_enemy_shokan_hurt-Sheet.png`
  - `Assets/Animations/Enemy/General/sprite_enemy_general_hurt-Sheet.png`

  3–4 horizontal frames per sheet is typical (a quick "wince" — flash of pain, contorted pose, recovery).

- [ ] **Step 5.2: Import each sheet using the SALIN-54 walk-sheet procedure**

  Follow §2.7.1.3 and §2.7.1.4 of [docs/superpowers/specs/2026-04-26-salin-54-pensionado-general-implementation-guide.md](../specs/2026-04-26-salin-54-pensionado-general-implementation-guide.md):
  - `Texture Type = Sprite (2D and UI)`, `Sprite Mode = Multiple`, `Pixels Per Unit = 6`, `Filter Mode = Bilinear`.
  - `Sprite Editor → Slice → Method = Automatic → Slice` (or `Grid By Cell Count` if Automatic miscounts).

- [ ] **Step 5.3: Wire the slices into the SO's `Hurt Frames` array**

  Per variant:
  - Project window → click `EnemyData_Shielded.asset` (or Shokan, or General).
  - Inspector → `Hurt Feedback — Hurt Animation (optional) → Hurt Frames` field.
  - Lock the Inspector (top-right padlock).
  - Project window → expand the disclosure triangle on the imported hurt sheet.
  - Drag-select all sub-sprites (Shift+click first and last) → drop on the `Hurt Frames` array header label. Unity sizes the array and assigns each slice in order.
  - Set `Hurt Animation Fps` to `12` (or higher for snappier wince — 16 reads tighter; do not go below 8 or the wince feels laggy).
  - Unlock the Inspector.
  - Save (Ctrl+S).

- [ ] **Step 5.4: Verify in editor**

  Press Play, hit a Shielded once. The hurt sheet plays in place during the pause window. The walk animation resumes after the wince finishes.

- [ ] **Step 5.5: Commit**

  ```bash
  git add Assets/Animations/Enemy/Shielded/ Assets/Animations/Enemy/Shokan/ Assets/Animations/Enemy/General/ Assets/ScriptableObjects/EnemyData_Shielded.asset Assets/ScriptableObjects/EnemyData_Shokan.asset Assets/ScriptableObjects/EnemyData_General.asset
  git commit -m "feat(art): SALIN-XX wire hurt animation sprites for Shielded, Shokan, General"
  ```

---

## Manual Unity configuration — at-a-glance summary

The list below is a quick checklist of every step that requires action **inside the Unity Editor** (i.e. clicks the user must perform that no Edit / Write tool can do for them). Each item links back to the section that covers it.

1. **[§1.1.4] Compile-check after `EnemyDataSO` edit** — wait for `Compiling...` to clear; confirm Console clean.
2. **[§1.1.5] Inspector sanity check** — open any `EnemyData_*.asset`, confirm five new headers and tooltips appear.
3. **[§1.2.3] Compile-check after `EnemyHurtFeedback.cs` create** — wait for `Compiling...` to clear; confirm Console clean.
4. **[§1.3.6] Compile-check after `Enemy.cs` edit** — wait for `Compiling...` to clear; confirm Console clean.
5. **[§2.1] Configure `EnemyData_Shielded`** — set the eleven `Hurt Feedback*` field values per the §2.1.2 table; save.
6. **[§2.2] Configure `EnemyData_Shokan`** — same values as Shielded; save.
7. **[§2.3] Configure `EnemyData_General`** — same shape as Shielded but `hurtSwapsCharacter = unchecked`, slightly tuned numeric values per the §2.3.2 table; save.
8. **[§2.4 — optional] Wire example swap on `EnemyData_Shielded`** — toggle on `Hurt Swaps Character`, point `Post Hurt Character` at a different Chapter-1 glyph; save.
9. **[§2.5.1] Add `EnemyHurtFeedback` to `[Enemy] Shielded.prefab`** — open in Prefab Mode → Add Component → search → click. Save Project.
10. **[§2.5.2] Add `EnemyHurtFeedback` to `[Enemy] Shokan.prefab`** — same procedure. Save Project.
11. **[§2.5.3] Add `EnemyHurtFeedback` to `Enemy_General.prefab`** — same procedure. Save Project.
12. **[§3.3] Run EditMode tests** — Test Runner → EditMode tab → Run All. Confirm all eight `EnemyHurtFeedbackTests` rows are green.
13. **[§4.1–§4.6] Playtest in Bootstrap scene** — verify Shielded pause + shake; General pause + shake without swap; (optional) Shielded character swap; pool-reuse cleanliness.
14. **[§5 — future]** Receive hurt-animation art from artist → import per SALIN-54 sheet workflow → wire slices into each SO's `Hurt Frames` array → save → playtest.

---

## Notes on architecture and trade-offs

- **Why a sibling component, not a method on `Enemy`?** The Pensionado / General / Aura pattern from SALIN-54 establishes that variant-specific behaviour lives on small sibling components, not Enemy subclasses or expanded `Enemy.cs`. Hurt feedback is the same shape: optional, per-variant, opt-in via prefab attachment. Keeping it out of `Enemy.cs` means: (1) no extra runtime cost on HP=1 prefabs that do not need it; (2) one focused file to debug if hurt feedback regresses; (3) parallel ergonomics with how a future "stagger on hit" or "bleed on hit" feature would live.
- **Why a single coroutine, not three?** A single coroutine driving all three beats means time advances in lockstep — the shake decay, the pause window, and the hurt-frame stepper all read the same `t` and finish on the same exit. Separate coroutines would need a coordinator to handle "what happens if shake ends before pause" and "what if the enemy is force-returned to the pool mid-shake". The current shape sidesteps both.
- **Why additive root-transform shake instead of a `Visual` child node?** A `Visual` child would isolate the shake from the mover writers, but it requires re-authoring every prefab in `Assets/Prefabs/Enemies/` to move the `SpriteRenderer` to a child node — and the existing `Enemy.cs` `GetComponent<SpriteRenderer>()` would need to switch to `GetComponentInChildren`. That is a 12-prefab + cross-cutting source-file change for a small visual benefit. The additive approach is good enough for every variant we ship today (HP > 1 enemies do not have absolute-position movers) and stays cheap. If a future variant needs both, refactor at that point — the contract on `EnemyHurtFeedback` does not change.
- **Why `useHurtFeedback` (master toggle) when every other field is already toggled?** It is the single field a designer touches to A/B-test "feature on" vs "feature off" without editing seven sub-toggles. Every variant ships with it `true`; flipping it `false` is the surgical opt-out for a variant that should never react to non-lethal damage (e.g. a future "stoic" enemy archetype). The other toggles (`hurtPausesMovement`, `hurtShakesSprite`, `hurtSwapsCharacter`) are designer dials within the on state.
- **Why `_hurtRoutine != null` as the "is playing" check, not a separate bool?** One source of truth. A separate bool would have to be flipped in three places (start, end, reset) and a mismatch leaks into `Enemy.AdvanceWalkAnimation`'s suppression check. The coroutine reference is the canonical state.

---

## Plan complete. Two execution options:

1. **Subagent-Driven (recommended)** — Dispatch a fresh subagent per task with review between tasks. Use [superpowers:subagent-driven-development](../../../C:/Users/asus/.claude/plugins/cache/superpowers-marketplace/superpowers/5.0.5/skills/subagent-driven-development/SKILL.md) to run.
2. **Inline Execution** — Execute tasks in the current session using [superpowers:executing-plans](../../../C:/Users/asus/.claude/plugins/cache/superpowers-marketplace/superpowers/5.0.5/skills/executing-plans/SKILL.md) with checkpoints between tasks.

Reply with the chosen approach to start.
