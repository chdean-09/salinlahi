# Epic: Player & Combat System (SALIN-4)

**Status:** ⚠️ Base Complete — Combat Wiring Missing | **Priority:** High

Player base component, heart/life system, core combat resolution (drawing → enemy defeat), AOE burst, and combo/focus-mode mechanics.

---

## SALIN-17 — Implement PlayerBase.cs and HeartSystem.cs

| Field      | Value |
|------------|-------|
| **Status** | Done *(Jira status stale — update to Done)* |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-29, SALIN-31 |
| **Blocked By** | SALIN-10, SALIN-13 |

`PlayerBase.cs` MonoBehaviour represents the player shrine. `HeartSystem.cs` manages the player's life count. When an enemy reaches the shrine, `HeartSystem.LoseHeart()` is called. At 0 hearts, `PlayerDefeatedEvent` is published and `GameManager.ChangeState(GameOver)` is triggered.

**Acceptance Criteria:**
- `HeartSystem` starts with configurable `maxHearts` (default 3) and tracks current hearts
- `LoseHeart()` decrements hearts and publishes `HeartsChangedEvent(int current, int max)`
- At 0 hearts: publishes `PlayerDefeatedEvent`, calls `GameManager.ChangeState(GameOver)`
- `PlayerBase` listens for `EnemyShrineReachedEvent` and calls `HeartSystem.LoseHeart()`
- Manual test: 3 enemies reach shrine → heart count decrements → GameOver triggered on 3rd

> ⚠️ This ticket is Done in code. Update Jira status to **Done**.

---

## SALIN-29 — Implement Core Combat Resolution (Drawing → Enemy Defeat)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-40, SALIN-41, SALIN-56, SALIN-80 |
| **Blocked By** | SALIN-27, SALIN-28, SALIN-75 |

`CombatResolver.cs` MonoBehaviour placed in the Gameplay scene. The critical missing link between the recognition pipeline and the enemy system — drawing currently does nothing to enemies. Listens for `EventBus.OnCharacterRecognized(RecognitionResult)`, finds the front-most enemy whose `assignedCharacter.characterID` matches the result, and calls `enemy.TakeDamage(1)`. No match → publish `OnDrawingFailed`.

**Acceptance Criteria:**
- `CombatResolver` subscribes to `EventBus.OnCharacterRecognized` on Awake
- Correct character draw: finds front-most matching enemy and calls `TakeDamage(1)`
- If multiple enemies share the same character, the one closest to the shrine is targeted first
- No matching enemy on screen → publish `OnDrawingFailed`
- `CombatResolver` is a MonoBehaviour in the Gameplay scene — not a Singleton
- End-to-end test: drawing the correct Baybayin character defeats the corresponding enemy

> ⚠️ Highest-priority unimplemented ticket in the project. Sprint 2 must not close without this merged.

---

## SALIN-40 — Implement AOE Burst Mechanic (3+ Same Character Mass Defeat)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-56 |
| **Blocked By** | SALIN-29, SALIN-41 |

When the player draws the same character 3 or more consecutive times and defeats an enemy each time, the 3rd defeat triggers an AOE burst: all enemies on screen with the same `assignedCharacter` are instantly defeated. Provides a high-skill reward mechanic for players who recognise the same character pattern quickly.

**Acceptance Criteria:**
- `ComboManager` (SALIN-41) tracks consecutive same-character defeats
- At streak count ≥ 3 with the same character, `AOEBurst()` fires
- AOE calls `TakeDamage(maxHealth)` on all enemies with the matching `assignedCharacter`
- `EventBus.OnBurstTriggered` published for VFX and audio feedback
- AOE does not affect enemies with a different `assignedCharacter`

---

## SALIN-41 — Implement ComboManager — Streak Counter and Focus Mode

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | Medium |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-40 |
| **Blocked By** | SALIN-29 |

`ComboManager.cs` Singleton. Tracks consecutive correct draws (any character). At streak of 5+, activates Focus Mode: enemy movement speed reduced to 50% for 5 seconds (configurable in `GameConfigSO`). Streak resets on `OnDrawingFailed` or any `HeartsChangedEvent`. HUD displays current streak count (wired in SALIN-31).

**Acceptance Criteria:**
- `currentStreak` increments on every `OnCharacterRecognized` event
- `currentStreak` resets to 0 on `OnDrawingFailed` or `HeartsChangedEvent`
- At streak ≥ 5: `EventBus.OnFocusModeActivated` published; enemy `moveSpeed` multiplied by 0.5
- Focus Mode lasts 5 seconds (configurable); `EventBus.OnFocusModeDeactivated` fires on expiry
- HUD streak count subscribes to `ComboManager` events and displays current streak

---

## SALIN-80 — Gameplay Integration Smoke Test

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | Sprint 3 feature work |
| **Blocked By** | SALIN-11, SALIN-29 |

Full end-to-end validation pass after SALIN-29 (combat resolution) merges. Walks through the complete game loop: Bootstrap → MainMenu → LevelSelect → Gameplay Level 1 → draw 3 enemies down → trigger GameOver → Retry → back to MainMenu. All event mismatches, scene transition errors, and null references are logged as Jira bugs.

**Acceptance Criteria:**
- Full scene-to-scene flow completes without errors on a physical Android device
- Drawing correct characters defeats matching enemies
- GameOver → Retry resets all game state cleanly (hearts, wave, enemies)
- MainMenu → LevelSelect → back to MainMenu works without errors
- Results (pass/fail per step, any bugs found) documented as a comment on this ticket

> ⚠️ This is a gate check. Sprint 3 feature work should not begin until SALIN-80 passes.
