# Epic: UI, Scenes & UX (SALIN-6)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

All game screens and UI systems: Main Menu, HUD, Level Select, Victory/Defeat, Tutorial, Dialogue, Scene Transitions, Loading Screen, and visual polish.

---

## SALIN-23 — Build Main Menu Scene with Navigation

| Field | Value |
|-------|-------|
| **Status** | In Progress |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Build the `MainMenu` Unity scene with a functional navigation structure: Play → Level Select, Settings → Settings overlay, Credits → Credits screen. Use placeholder UI art until art assets are delivered.

**Acceptance Criteria:**
- `MainMenu` scene loads from Bootstrap without errors
- 'Play' button navigates to `LevelSelect` scene via `SceneLoader`
- 'Settings' button opens a settings overlay (audio volume slider at minimum)
- 'Credits' button opens a credits screen listing team member names
- All buttons respond to touch input on a physical Android device
- Scene uses Unity UI (Canvas, Buttons) and is laid out for a 16:9 portrait aspect ratio

---

## SALIN-31 — Build In-Game HUD — Hearts, Wave Counter, Character Prompt, Pause

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-43 — Build Level Select Screen with Lock/Unlock/Complete States

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-44 — Implement Scene Transition Manager — Fade In/Out Between Scenes

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-45 — Implement DialogueController.cs with Typewriter Effect

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-46 — Implement LevelFlowController.cs — Intro/Gameplay/Outro Sequence

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-47 — Write Dialogue Content for All 15 Levels

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-50 — FTUE / Tutorial Overlay for Level 1 (First-Time User Experience)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-57 — Implement Safe Area Handler for Notched Android/iOS Devices

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-58 — Build Victory, Defeat, Settings, and Credits Screens

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-65 — Loading Screen During Scene Transitions

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-66 — Visual Polish Pass — UI Animations, Feedback, and Drawing Trail

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-69 — Implement Tracing Dojo Scene and Practice Mode

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Build the Tracing Dojo scene (GDD §2.4, §5.4) where players practice drawing Baybayin characters without combat pressure. Shows a character to trace, overlays a ghost template, and gives accuracy feedback after each attempt.

**Acceptance Criteria:**
- Tracing Dojo scene exists in Build Settings and is navigable from Main Menu
- Player can select any unlocked Baybayin character from a scrollable list
- A ghost/overlay of the selected character's stroke path is displayed on the drawing canvas
- After each draw attempt, confidence score and pass/fail feedback are shown on screen
- Tracing Dojo has no combat mechanics — no enemies, no hearts, no wave timer

**Req IDs:** GDD-REQ-005
