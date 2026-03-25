# Epic: Player & Combat System (SALIN-4)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

Player base component, heart/life system, core combat resolution (drawing → enemy defeat), AOE burst, and combo/focus-mode mechanics.

---

## SALIN-17 — Implement PlayerBase.cs and HeartSystem.cs

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `PlayerBase.cs`, a MonoBehaviour that represents the player shrine, and `HeartSystem.cs`, which manages the player's life count. When an enemy reaches the shrine, the HeartSystem loses one heart and GameManager transitions to GameOver if hearts reach zero.

**Acceptance Criteria:**
- `HeartSystem` starts with a configurable `maxHearts` (default 3) and tracks current hearts
- `LoseHeart()` decrements hearts and publishes `HeartsChangedEvent(int current, int max)`
- At 0 hearts, publishes `PlayerDefeatedEvent` and calls `GameManager.ChangeState(GameOver)`
- `PlayerBase` listens for `EnemyShrineReachedEvent` and calls `HeartSystem.LoseHeart()`
- Manual test: 3 enemies reach shrine in sequence → HUD heart count decrements → GameOver triggered on 3rd

---

## SALIN-29 — Implement Core Combat Resolution (Drawing to Enemy Defeat)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-40 — Implement AOE Burst Mechanic (3+ Same Character Mass Defeat)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-41 — Implement ComboManager — Streak Counter and Focus Mode

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |
