# Epic: Research, Testing & Analytics (SALIN-51)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

In-game questionnaires (SUS, GEQ-S), recognition accuracy logging, confusion matrix export, internal playtest, formal UAT, and statistical analysis for the capstone paper.

---

## SALIN-61 — Build SUS (System Usability Scale) Questionnaire UI

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-62 — Build GEQ-S (Game Experience Questionnaire Short) UI

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-63 — Implement Confusion Matrix Export and Debug Tool

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-76 — Build GEQ Core Module Questionnaire UI (Full GEQ, not GEQ-S)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement the Game Experience Questionnaire (GEQ) core module in-app as required by Salinlahi.md §3.5.1. The capstone study requires the full GEQ core module (33 items), not just the short version. The questionnaire is shown after completing the 5 evaluation levels.

**Acceptance Criteria:**
- A GEQ questionnaire scene or overlay presents all 33 GEQ core module items (7-point Likert scale: 0=not at all, 6=extremely)
- Questionnaire is triggered automatically after the player completes Level 5 (the final evaluation level)
- Responses are saved to persistent storage as a CSV with columns: participantID, itemID, response, timestamp
- The questionnaire cannot be skipped during a study session (Submit button disabled until all 33 items answered)
- Completed response data is exportable alongside the recognition log (SALIN-35) for research analysis

**Req IDs:** Salinlahi.md §3.5.1
