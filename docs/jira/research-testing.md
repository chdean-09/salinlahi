# Epic: Research, Testing & Analytics (SALIN-51)

**Status:** ❌ Not Started | **Priority:** High

In-game questionnaires (SUS, GEQ-S, Full GEQ), recognition accuracy logging, confusion matrix export, and internal playtest gate.

---

## SALIN-35 — Implement Recognition Accuracy Logging System

> *See also: [baybayin-recognition.md](baybayin-recognition.md#salin-35) — this ticket spans both epics.*

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-63, SALIN-76 |
| **Blocked By** | SALIN-75, SALIN-29 |

`RecognitionLogger.cs` Singleton. Appends per-draw-attempt records to `recognition_log.csv` in `Application.persistentDataPath`. Append-only — survives app restarts. Requires a study-session setup screen (sub-task) where a facilitator enters the `participantID` before each research session begins.

**CSV schema:**
```
participantID, sessionID, levelID, waveNumber, characterExpected,
characterRecognized, confidenceScore, correct, timestamp
```

**Sub-task — Participant ID Setup Screen:** A simple UI screen (shown once at research session start) where a facilitator enters the participant ID. Value stored in PlayerPrefs for the session. Screen accessible from a hidden button or study mode toggle in Settings.

**Acceptance Criteria:**
- Logger initialised from Bootstrap scene alongside other managers
- Appends (never overwrites) on each recognition attempt
- `participantID` and `sessionID` set from study setup screen before session
- `RecognitionLogger.ExportLog()` returns the absolute file path
- File persists across app restarts; separate sessions produce sequential rows

> ⚠️ Moved to Sprint 3 from unscheduled. SALIN-63 and SALIN-76 in Sprint 4 are blocked until this is done. Must land by Apr 24.

---

## SALIN-61 — Build SUS (System Usability Scale) Questionnaire UI

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-35 (participant ID mechanism reused) |

10-item SUS questionnaire. 5-point Likert scale (1 = Strongly Disagree, 5 = Strongly Agree). Triggered at the end of a designated study session (after the evaluation levels). Submit button disabled until all 10 items answered.

**Note:** Build this as a reusable `QuestionnaireController` MonoBehaviour — SALIN-62 and SALIN-76 extend the same pattern. Define the controller interface first, then implement SUS as the first concrete questionnaire.

**Responses CSV:**
```
participantID, questionnaireType, itemID, response, timestamp
```

**Acceptance Criteria:**
- All 10 SUS items displayed in canonical order
- 5-point Likert input per item; Submit disabled until all answered
- Responses saved to `sus_responses.csv` in persistent data path
- `participantID` pulled from `RecognitionLogger` / PlayerPrefs (same session ID)
- Questionnaire cannot be reopened after submission in the same session

---

## SALIN-62 — Build GEQ-S (Game Experience Questionnaire Short) UI

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-61 (reuses QuestionnaireController from SUS) |

14-item GEQ Short version. 5-point scale (0 = Not at all, 4 = Extremely). Shown after each gameplay session or at the end of evaluation levels. Extends the `QuestionnaireController` pattern established by SALIN-61.

**Responses CSV:**
```
participantID, questionnaireType, itemID, response, timestamp
```

**Acceptance Criteria:**
- All 14 GEQ-S items displayed in canonical order
- 5-point (0–4) scale per item; Submit disabled until all answered
- Responses saved to `geq_short_responses.csv`
- Consistent presentation with SUS (same QuestionnaireController base)

---

## SALIN-76 — Build Full GEQ Core Module Questionnaire UI (33 Items)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-35, SALIN-61 |

33-item full GEQ core module. 7-point Likert scale (0 = Not at all, 6 = Extremely). Triggered automatically after the player completes Level 5 (the final evaluation level). Cannot be skipped during a study session. Responses exportable alongside the recognition log.

**Responses CSV:**
```
participantID, questionnaireType, itemID, response, timestamp
```

**Acceptance Criteria:**
- All 33 GEQ core module items displayed in canonical order
- 7-point (0–6) scale per item; Submit disabled until all 33 answered
- Triggered automatically after Level 5 completion — not manually accessible
- Responses saved to `geq_core_responses.csv`
- Export function callable alongside `RecognitionLogger.ExportLog()` for combined research output

**Req IDs:** Salinlahi.md §3.5.1

---

## SALIN-63 — Implement Confusion Matrix Export and Debug Tool

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-35 |

Reads `recognition_log.csv` (from SALIN-35). Computes per-character confusion: for each expected character, shows what was recognised instead and how often. Exports confusion matrix as `confusion_matrix.csv`. In-Editor debug overlay visualises confusion data for testing during development.

**Confusion matrix CSV:**
```
expectedCharacter, recognizedCharacter, count, percentage
```

**Acceptance Criteria:**
- `ConfusionMatrixExporter.Export()` reads `recognition_log.csv` and writes `confusion_matrix.csv`
- All 17×17 character pairs represented (0 if no confusion occurred)
- In-Editor debug overlay renders the matrix as a simple table or heatmap
- Export callable from a debug menu or study export screen
- Tool excluded from release builds (`#if UNITY_EDITOR` guard or debug-menu-only)

---

## SALIN-78 — Internal Playtest & Bug Bash

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad (lead) |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | SALIN-56 |
| **Blocked By** | Sprint 3 feature completion (all levels, enemies, UI) |

Structured internal playtest session before research UAT. Each team member plays all 15 levels independently on a physical Android device. Bugs logged to Jira with reproduction steps. Session produces a prioritised bug list. Results feed directly into SALIN-56 (difficulty tuning) and SALIN-66 (visual polish).

**Acceptance Criteria:**
- All 4 team members participate in the playtest session
- Every level (1–15) played through to completion or GameOver at least once
- All bugs logged to Jira with: title, steps to reproduce, expected vs actual behaviour, device tested
- Bug list triaged and prioritised before Sprint 4 polish work begins
- Recognition accuracy logs exported via SALIN-35 and reviewed for per-character accuracy outliers

> ⚠️ This is a gate for research UAT. No participant testing should begin until SALIN-78 is complete and critical bugs are resolved.
