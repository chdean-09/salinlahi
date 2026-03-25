# Epic: Level Design & Progression (SALIN-5)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

ScriptableObject data architecture, all 15 level configurations, level progress saving, and full difficulty tuning pass.

---

## SALIN-22 — Create All ScriptableObject Data Architecture Classes

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Design and implement all ScriptableObject data classes that drive the game's data-driven architecture: `LevelDataSO`, `WaveDataSO`, `EnemyDataSO`, `BaybayinCharacterSO`, and `GameConfigSO`.

**Acceptance Criteria:**
- `LevelDataSO` holds level ID, display name, list of `WaveDataSO`, required characters, and unlock condition
- `WaveDataSO` holds enemy prefab reference, enemy count, spawn interval, and difficulty modifiers
- `EnemyDataSO` holds max health, move speed, and enemy type enum
- `BaybayinCharacterSO` holds character name, phonetic label, reference sprite, and template point cloud
- `GameConfigSO` holds global settings (heart count, recognition threshold, etc.)
- All SOs are under `Assets/ScriptableObjects/` and can be created via the Assets > Create menu
- At least one instance of each SO is created for testing

---

## SALIN-30 — Design and Configure Levels 1-5 (Core Evaluation Levels)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-42 — Design and Configure Levels 6-10

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-48 — Implement Level Progress Saving — PlayerPrefs Manager

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-55 — Design and Configure Levels 11-15

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-56 — Full Difficulty Tuning Pass — All 15 Levels

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-70 — Implement Endless Mode Unlock Trigger (Post-Story Completion)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement the Endless Mode unlock that activates after the player completes all 15 story levels (GDD §2.4). Endless Mode uses procedurally escalating waves with no level cap.

**Acceptance Criteria:**
- Given all 15 levels are marked complete in PlayerPrefs, the Endless Mode button becomes interactive on Main Menu
- Given Endless Mode is unlocked, tapping the button loads a new scene with infinite wave escalation
- Endless Mode wave difficulty scales by increasing `enemyCount` and decreasing `spawnInterval` every 5 waves
- Endless Mode tracks and displays the player's highest wave reached (persisted in PlayerPrefs)
- Endless Mode can be exited at any time via a pause menu without data loss

**Req IDs:** GDD-REQ-006
