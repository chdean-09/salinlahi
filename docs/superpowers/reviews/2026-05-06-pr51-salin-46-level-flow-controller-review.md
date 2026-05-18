# Code Review: SALIN-46 LevelFlowController

**Date:** 2026-05-06
**PR:** [#51](https://github.com/chdean-09/salinlahi/pull/51)
**Branch:** `feature/SALIN-46-level-flow-controller`
**Jira:** SALIN-46
**Reviewer:** superpowers:code-reviewer (dispatched via `/superpowers:requesting-code-review`)
**Scope:** SALIN-46 commits only (`9773833..5740c05`); upstream merged work ignored.

## Verdict

**Approve with required changes.** AC-5 is broken in the most consequential way: when `outroDialogue` is non-null, the victory screen pops up first and the outro dialogue plays on top of it. The commit message "resolve outro-before-victory race condition" (`2b58ea1`) is also actively misleading — the linked fix only changed `Show()` from `private` to `public` and did not solve the race.

---

## Acceptance Criteria Audit

| AC | Result | Notes |
|---|---|---|
| AC-1 (intro before waves) | ✅ Pass | [LevelFlowController.cs:50-58](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L50-L58) blocks on `OnDialogueComplete` before calling `_waveManager.StartLevel()` |
| AC-2 (BGM from config) | ✅ Pass | [LevelFlowController.cs:61-62](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L61-L62) |
| AC-3 (no `isBossLevel` branching in controller) | ✅ Pass | Controller calls `_waveManager.StartLevel()` only; boss vs wave routing stays in [WaveManager.cs:280-283](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L280-L283) |
| AC-4 (game over → defeat directly) | ⚠️ Partial | See **Important #1** — controller's handler does nothing meaningful |
| AC-5 (outro then victory) | ❌ **FAIL** | See **Critical #1** |
| AC-6 (event-driven, no direct UI coroutine chains) | ❌ **FAIL** | See **Critical #2** — `PlayOutroThenVictory()` directly calls `_victoryScreen.Show()` |
| AC-7 (`OnBossDefeated` subscription, event declared) | ✅ Pass / Stub | [EventBus.cs:43,70](../../../Assets/Scripts/Core/EventBus.cs#L43) declares it. Subscribed at [LevelFlowController.cs:27](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L27). Handler is a stub log line at [LevelFlowController.cs:122-126](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L122-L126). |
| AC-8 (OnEnable / OnDisable discipline) | ✅ Pass | [LevelFlowController.cs:23-37](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L23-L37) — clean symmetric subscribe/unsubscribe |

**Scene wiring** ([Gameplay.unity:646-649](../../../Assets/_Scenes/Gameplay.unity#L646-L649)): all three Inspector references (`_waveManager`, `_dialogueController`, `_victoryScreen`) resolve correctly. `_waitForExternalStart: 1` is set on the WaveManager ([Gameplay.unity:3694](../../../Assets/_Scenes/Gameplay.unity#L3694)).

---

## Critical (must fix before merge)

### Critical #1 — AC-5 is broken: outro plays *over* the victory screen, not before it

**Files:**
- [LevelFlowController.cs:89-97](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L89-L97)
- [VictoryScreenUI.cs:25](../../../Assets/Scripts/UI/VictoryScreenUI.cs#L25)

Walk the synchronous event flow when `outroDialogue` is non-null:

1. `WaveManager.CompleteRun()` → `EventBus.RaiseLevelComplete()` ([WaveManager.cs:549,554](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L549)).
2. `OnLevelComplete` invokes ALL subscribers synchronously, in delegate order:
   - `GameManager.HandleLevelComplete` ([GameManager.cs:48](../../../Assets/Scripts/Core/GameManager.cs#L48)) → state becomes `LevelComplete`.
   - `VictoryScreenUI.Show` ([VictoryScreenUI.cs:25](../../../Assets/Scripts/UI/VictoryScreenUI.cs#L25)) → **panel is set active immediately**, stars are populated.
   - `LevelFlowController.HandleLevelComplete` ([LevelFlowController.cs:25](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L25)) → starts the outro coroutine.
3. The outro coroutine calls `GameManager.StartGame()` (resetting state from `LevelComplete` back to `Playing`!), then `DialogueController.Play(...)`, which calls `EnterDialoguePause()`.
4. Dialogue plays *on top of* an already-visible victory panel.
5. After dialogue ends, `_victoryScreen.Show()` is called a second time, which redundantly re-activates the panel and re-runs star UI logic.

This violates AC-5 ("outro dialogue plays, THEN Victory screen is shown — in that order"). The commit message for `2b58ea1` claims to "resolve outro-before-victory race condition with direct Show() call", but the actual diff only widened `VictoryScreenUI.Show()` from `private` to `public`. The race was not resolved — it was just made callable.

**Fix options (pick one, in order of preference):**

**Option A (recommended)** — Remove `VictoryScreenUI`'s self-subscription and let `LevelFlowController` always drive it. This makes the controller the single source of truth for end-of-level UI:

```csharp
// VictoryScreenUI.cs — drop the subscription
private void OnEnable()
{
    // EventBus.OnLevelComplete += Show;  ← remove
    if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(OnNextLevelPressed);
    ...
}
```

Then `LevelFlowController.HandleLevelComplete` calls `_victoryScreen.Show()` in both branches (with and without outro). For consistency apply the same to DefeatScreenUI (see Important #1).

**Option B** — Use a deferred event raise. Have the controller swallow `OnLevelComplete` and re-raise a separate `OnVictoryScreenReady` event after the outro. Heavier, but keeps subscription style consistent.

Either way, also rewrite the commit narrative — the current `2b58ea1` message claims a fix that does not exist.

---

### Critical #2 — AC-6 violated: direct UI call from coroutine

**File:** [LevelFlowController.cs:109-111](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L109-L111)

The class-level XML doc claims "All transitions driven by EventBus events (SALIN-46 AC-6)", but `PlayOutroThenVictory()` does:

```csharp
if (_victoryScreen != null)
    _victoryScreen.Show();
```

That is exactly the "direct coroutine chain into a UI component" that AC-6 forbids. The inline comment "Show victory screen directly to avoid re-triggering OnLevelComplete handlers" acknowledges the violation but accepts it because raising `OnLevelComplete` again would double-fire `GameManager.HandleLevelComplete` (and any other one-shot subscribers).

The clean resolution falls out of Critical #1 Option A: if `VictoryScreenUI` is no longer self-subscribed, you have two equally valid choices that both honor AC-6:

- Keep the direct `_victoryScreen.Show()` call but update the AC text / docstring to acknowledge that ONE direct call (controller → owned UI reference) is allowed by design. Architecturally this is fine — the controller owns the UI.
- Or introduce `EventBus.OnLevelOutroComplete` and have `VictoryScreenUI` subscribe to *that* instead of `OnLevelComplete`.

Pick one and make the code, comments, and acceptance criteria agree.

---

## Important (should fix before merge)

### Important #1 — `HandleGameOver` doesn't satisfy AC-4 in spirit

**File:** [LevelFlowController.cs:115-120](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L115-L120)

```csharp
private void HandleGameOver()
{
    if (_levelEnded) return;
    _levelEnded = true;
    // DefeatScreenUI handles display via its own OnGameOver subscription
}
```

Setting `_levelEnded` is the only meaningful action, and it's only used for the `LevelComplete`/`GameOver` mutual exclusion guard. AC-4 says the controller should "route directly to Defeat screen". As written, the controller does not route anything — it relies on `DefeatScreenUI`'s own subscription. That is a perfectly reasonable design, but it contradicts the spec language and creates the same architectural inconsistency as Critical #2 (some UI is driven by the controller, some by direct event subscription).

Either:
- Update AC-4 wording to "ensure no further flow logic runs after GameOver" and document that DefeatScreenUI continues to self-subscribe; **or**
- Apply the same restructure as Critical #1 Option A — drop `DefeatScreenUI`'s self-subscription and let the controller call `_defeatScreen.Show()` from `HandleGameOver`.

Consistency is the issue; both UIs should be wired the same way.

### Important #2 — Race: `OnGameOver` during intro dialogue still starts waves

**File:** [LevelFlowController.cs:39-72](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L39-L72)

The `Start` coroutine does not check `_levelEnded` after the `WaitUntil` at line 57. If something raises `OnGameOver` while the player is reading the intro (e.g., a debug panel, an instant-death dev shortcut, or — in the future — a damaging cinematic), the coroutine resumes after dialogue completes and proceeds to:

1. Call `GameManager.StartGame()` → state moves from `GameOver` back to `Playing` (allowed because `StartGame()` has no guard).
2. Call `_waveManager.StartLevel()` → spawns waves on top of an already-shown defeat screen.

Today this is theoretical (HeartSystem is the only `OnGameOver` raiser and the player can't damage hearts during a paused dialogue), but the race is real and very cheap to close:

```csharp
yield return new WaitUntil(() => !_waitingForDialogue);
if (_levelEnded) yield break;   // ← add this
```

Also add the same guard before the `_waveManager.StartLevel()` call to be safe across both branches.

### Important #3 — `GameManager.StartGame()` is called from a `LevelComplete` state

**File:** [LevelFlowController.cs:99-103](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L99-L103)

```csharp
private IEnumerator PlayOutroThenVictory()
{
    if (GameManager.Instance != null)
        GameManager.Instance.StartGame();
    ...
}
```

`StartGame()` is unconditional — it sets `Time.timeScale = 1f` and forces `CurrentState = Playing`. After `OnLevelComplete` has already moved the state machine to `LevelComplete`, this rewinds it back to `Playing` solely to satisfy the `DialogueController.Play()` precondition ([DialogueController.cs:45-46](../../../Assets/Scripts/UI/DialogueController.cs#L45-L46)). Side effects you may not want during the outro:

- `WaveManager.CanContinueRun()` ([WaveManager.cs:516-525](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L516-L525)) treats `LevelComplete` as terminal — but here you've left that terminal state, so any in-flight wave coroutine could re-engage. (Practically `_running` is already false because `CompleteRun()` ran, so this is dormant — but it is a latent footgun if anything in the flow restarts a coroutine.)
- `EnemyPool` / `ActiveEnemyTracker` and any other systems that gate on `CurrentState == LevelComplete` to suppress activity will believe the level is live again.
- The state log noise (`GameState -> Playing` then `GameState -> Paused` then `GameState -> Playing` again) makes debugging harder.

Cleaner: introduce a dedicated `GameState.Dialogue` (or relax `DialogueController.Play()`'s precondition to also accept `LevelComplete` for outros), and use `EnterDialoguePause` / `ExitDialoguePause` semantics that preserve the originating state. The `_stateBeforeDialogue` field in `GameManager` already supports this — `Play()` is the only thing forcing the controller to lie about state.

### Important #4 — Misleading commit message for `2b58ea1`

**Commit:** `2b58ea121ceb51d3bda094edc28d41cfaf00a094`

The message says "resolve outro-before-victory race condition with direct Show() call". The actual diff is one access modifier change (`private Show()` → `public Show()`). It does not resolve any race; it merely makes the bug callable. This wastes future debugging time when someone bisects "this commit fixed the race" and finds nothing changed.

Recommend: either land the actual fix and amend the message, or rename to something like `chore(SALIN-46): expose VictoryScreenUI.Show() for external callers`. Per project guidelines, prefer a *new* commit over an amend.

### Important #5 — Boss outro / chapter hooks unimplemented despite AC-7

**File:** [LevelFlowController.cs:122-126](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L122-L126)

`HandleBossDefeated` is a single `DebugLogger.Log` call. AC-7 says "subscribes to `OnBossDefeated` for any boss-specific dialogue or chapter-complete hooks where applicable". The subscription exists, but per `WaveManager.RunBossEncounter` ([WaveManager.cs:420-422](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L420-L422)) `OnBossDefeated` is raised immediately followed by `OnLevelComplete`. So in the boss-defeat case, `OnLevelComplete`'s outro will already cover any chapter-complete dialogue need; consider documenting whether `OnBossDefeated` should ever do anything beyond logging, or remove the subscription entirely.

Per CLAUDE.md ("No leftover Debug.Log calls in committed code"), confirm that `DebugLogger.Log(...)` is allowed in shipped code or strip it.

---

## Minor / Follow-ups

### Minor #1 — Redundant warning in `ResolveLevelConfig`

**File:** [LevelFlowController.cs:74-86, 43-47](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L74-L86)

The warning at line 85 fires when `GameManager.CurrentLevel` is null AND the inspector fallback is null. Three lines later, `Start` logs an error and bails out. The warning is redundant noise.

### Minor #2 — Inspector fallback path is undocumented

**File:** [LevelFlowController.cs:74-86](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L74-L86)

If the GameManager has no current level but the inspector field is set, the controller will run with the inspector level — convenient for editor playtesting, but worth a tooltip or comment so a future engineer doesn't think it's dead code.

### Minor #3 — `_waitingForDialogue` is shared between intro and outro

**File:** [LevelFlowController.cs:21, 55, 105, 128-131](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L21)

The bool is reset by the global `OnDialogueComplete` event. If anything else in the scene plays a dialogue while the controller is mid-coroutine (e.g., a future tutorial popup raised by another system), `OnDialogueComplete` will wake the wrong waiter. Today no other system raises it, but consider tracking which dialogue you're waiting for or use `DialogueSO`-keyed dispatch.

### Minor #4 — `LevelConfigSO.cs` whitespace churn

**File:** [LevelConfigSO.cs](../../../Assets/Scripts/Data/LevelConfigSO.cs) (commit `9773833`)

The diff shows CRLF/LF or trailing-whitespace re-saves on every header line (`-` then `+` for unchanged content). Configure your editor to not rewrite line endings on save, or normalize via `.gitattributes`. Makes `git blame` noisy.

### Minor #5 — Class XML doc is currently aspirational, not descriptive

**File:** [LevelFlowController.cs:4-8](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L4-L8)

It says "All transitions driven by EventBus events (SALIN-46 AC-6)" but the implementation directly calls `_victoryScreen.Show()`. After Critical #1 / #2 are resolved, update the doc to reflect the chosen design.

### Minor #6 — Class is correctly *not* a singleton

Confirming: this controller is correctly *not* a `Singleton<T>` because each Gameplay scene wants a fresh flow. No action needed; just calling out that the instinct to make it a singleton would be wrong.

---

## Strengths

- **Clean OnEnable/OnDisable subscription discipline** ([LevelFlowController.cs:23-37](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L23-L37)) — exactly per CLAUDE.md.
- **`_waitForExternalStart` decouples cleanly** ([WaveManager.cs:62-65](../../../Assets/Scripts/Gameplay/Wave/WaveManager.cs#L62-L65)) — additive flag, defaults to false, fully backward compatible with existing scenes.
- **Idempotency guard via `_levelEnded`** ([LevelFlowController.cs:91, 117](../../../Assets/Scripts/Gameplay/LevelFlowController.cs#L91)) prevents duplicate routing if both `OnLevelComplete` and `OnGameOver` somehow fire.
- **`ResolveLevelConfig` fallback chain** (`GameManager.CurrentLevel` → inspector) is sensible for editor play and dev shortcuts.
- **Scene wiring is correct.** All three Inspector references resolve to the right MonoBehaviour, and `_waitForExternalStart: 1` is set on WaveManager.
- **AC-3 is honored cleanly** — controller stays oblivious to boss/wave distinction; the spec §4 contract is respected.
- **`OnBossDefeated` is genuinely declared in [EventBus.cs:43, 70](../../../Assets/Scripts/Core/EventBus.cs#L43)** — the audit ticket TICKET-05 flag is no longer accurate (or this PR resolved it).

---

## Suggested Resolution Order

1. **Fix AC-5** (Critical #1): pick the Option A path — drop `VictoryScreenUI`'s `OnLevelComplete` subscription; let the controller drive the show. Same for `DefeatScreenUI` (Important #1) for consistency.
2. **Add the `_levelEnded` guard after intro** (Important #2). One line.
3. **Decide on the dialogue-during-LevelComplete state model** (Important #3). Smallest fix: relax `DialogueController.Play()` to accept `LevelComplete` for outros, then drop the `StartGame()` call in `PlayOutroThenVictory`.
4. **Land a follow-up commit with an honest message** for the access-modifier change in `2b58ea1` (Important #4).
5. **Update class docstring and AC-6 wording** to match whichever design you pick (Critical #2).

Files needing changes:
- [Assets/Scripts/Gameplay/LevelFlowController.cs](../../../Assets/Scripts/Gameplay/LevelFlowController.cs)
- [Assets/Scripts/UI/VictoryScreenUI.cs](../../../Assets/Scripts/UI/VictoryScreenUI.cs)
- [Assets/Scripts/UI/DefeatScreenUI.cs](../../../Assets/Scripts/UI/DefeatScreenUI.cs) (for consistency)
- Possibly [Assets/Scripts/UI/DialogueController.cs](../../../Assets/Scripts/UI/DialogueController.cs) (to relax state precondition)
