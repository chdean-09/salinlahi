# Epic: Baybayin Recognition System (SALIN-3)

**Status:** ⚠️ Pipeline Complete — Template Library Partial (3/17) | **Priority:** High

Touch input capture, LineRenderer stroke visualisation, $P recogniser implementation, Baybayin template library, edge-case handling, and accuracy logging.

---

## SALIN-18 — Configure Unity Input System and Implement StrokeCapture.cs

| Field      | Value |
|------------|-------|
| **Status** | Done *(Jira status stale — update to Done)* |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-19, SALIN-20, SALIN-28 |
| **Blocked By** | — |

`StrokeCapture.cs` (82 LOC) implemented using `EnhancedTouch` API. Listens for touch press, drag, and release. Fires `OnStrokeStarted`, `OnStrokeUpdated(List<Vector2>)`, and `OnStrokeCompleted(List<Vector2>)`. Validates minimum 8 points (configurable in `RecognitionConfigSO`) before `OnStrokeCompleted` is considered valid. Multi-stroke timeout logic prevents accidental multi-finger captures.

**Acceptance Criteria:**
- Unity Input System 1.19.0 installed; old Input Manager disabled
- `StrokeCapture` uses `EnhancedTouch` for single-touch stroke tracking
- All three events fire at correct touch lifecycle stages
- Stroke with fewer than `minimumPointCount` (8) points is discarded
- Test on device: drawing produces a non-empty point list in the console

> ⚠️ This ticket is Done in code. Update Jira status to **Done**.

---

## SALIN-19 — Implement DrawingCanvas.cs with LineRenderer Stroke Visualization

| Field      | Value |
|------------|-------|
| **Status** | Done *(Jira status stale — update to Done)* |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-29, SALIN-66 |
| **Blocked By** | SALIN-18 |

`DrawingCanvas.cs` subscribes to `StrokeCapture` events and renders the stroke as a `LineRenderer` in world space. Line is updated in real time during draw and cleared after a configurable duration when `OnStrokeCompleted` fires. A code comment marks the LineRenderer for a GPU upgrade in Sprint 4 (SALIN-66).

**Acceptance Criteria:**
- `DrawingCanvas` creates and configures a `LineRenderer` at runtime
- LineRenderer positions update in real time as `OnStrokeUpdated` fires
- On `OnStrokeCompleted`, line persists for a configurable duration then clears
- Line width, color, and material are configurable from the Inspector
- Test on device: visible line follows the finger and disappears after configured duration

> ⚠️ This ticket is Done in code. Update Jira status to **Done**.

---

## SALIN-20 — Implement DollarPRecognizer.cs — Full $P Algorithm

| Field      | Value |
|------------|-------|
| **Status** | Done *(Jira status stale — update to Done)* |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-75, SALIN-28 |
| **Blocked By** | — |

`DollarPRecognizer.cs` (120 LOC). Full C# port of the $P point-cloud gesture recogniser. Resamples input to 32 points (configurable via `RecognitionConfigSO`), applies greedy cloud matching against all loaded templates, and returns a `RecognitionResult { string templateName; float score; }`. Handles empty point lists gracefully (returns null, no exception).

**Acceptance Criteria:**
- `DollarPRecognizer.Recognize(List<Vector2>)` returns `RecognitionResult` or null
- Templates added via `AddTemplate(string name, List<Vector2> points)`
- Resampling, scaling, and origin translation match the $P reference algorithm
- Smoke test: circular template added, rough circle of 32 points passed → score > 0.8
- Empty point list returns null without throwing

> ⚠️ This ticket is Done in code. Update Jira status to **Done**.

---

## SALIN-21 — SPIKE: Investigate $P Recognizer Accuracy Issues *(Won't Do — Superseded)*

| Field      | Value |
|------------|-------|
| **Status** | Won't Do |
| **Assignee** | — |
| **Priority** | Low |
| **Sprint** | N/A |
| **Blocks** | — |
| **Blocked By** | — |

This spike was intended to run before `DollarPRecognizer.cs` was implemented. The implementation is now complete with confirmed working parameters. Spike findings are implicit in the final implementation choices.

**Closure note:** Final parameters — `resamplePointCount = 32`, `minimumConfidence = 0.60`, `minimumPointCount = 8` — are set in `RecognitionConfig_Default.asset`. No further investigation needed.

> ⚠️ Close this ticket in Jira as **Won't Do / Superseded**. Add the closure note above as a comment.

---

## SALIN-74 — Implement TemplateLoader.cs — Standalone Baybayin Template Loading

| Field      | Value |
|------------|-------|
| **Status** | Done *(Jira status stale — update to Done)* |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-75, SALIN-27 |
| **Blocked By** | — |

`TemplateLoader.cs` (51 LOC). Static utility class — no MonoBehaviour inheritance, no scene dependency. `Load(string fileName)` reads from `Resources/Templates/` and parses a point-cloud file into `List<Vector2>`, returning null (with a warning log) if the file does not exist. `LoadAll()` returns `Dictionary<string, List<Vector2>>` for all templates present. `DollarPRecognizer` exclusively uses `LoadAll()` at initialisation.

**Acceptance Criteria:**
- `TemplateLoader.Load(fileName)` returns non-null `List<Vector2>` for valid files
- `TemplateLoader.LoadAll()` returns a dictionary of all templates in `Resources/Templates/`
- Non-existent file → returns null + logs warning, no exception
- Static class only — no MonoBehaviour, no scene placement required
- `DollarPRecognizer` uses only `LoadAll()` for template initialisation

> ⚠️ This ticket is Done in code. Update Jira status to **Done**.

---

## SALIN-75 — Implement RecognitionManager.cs — Coordinate StrokeCapture and DollarPRecognizer

| Field      | Value |
|------------|-------|
| **Status** | Done *(Jira status stale — update to Done)* |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-29 |
| **Blocked By** | SALIN-20, SALIN-74 |

`RecognitionManager.cs` in `Core/`. Subscribes to `EventBus.OnStrokeCompleted` on Awake, unsubscribes on OnDestroy. Passes the stroke to `DollarPRecognizer.Recognize()` within the same frame. If confidence ≥ 0.60: publishes `EventBus.OnCharacterRecognized(result)`. If confidence < 0.60: publishes `EventBus.OnDrawingFailed`. Not a Singleton — placed as a MonoBehaviour in the Gameplay scene.

**Acceptance Criteria:**
- Subscribes to `EventBus.OnStrokeCompleted` on Awake; unsubscribes on OnDestroy
- `Recognize()` called in the same frame as stroke completion
- Confidence ≥ 0.60 → publish `OnCharacterRecognized`
- Confidence < 0.60 → publish `OnDrawingFailed`
- Not a Singleton; lives in Gameplay scene only

> ⚠️ This ticket is Done in code. Update Jira status to **Done**.

---

## SALIN-27 — Build Baybayin Template Library — All 17 Characters (3–5 Variants Each)

| Field      | Value |
|------------|-------|
| **Status** | In Progress (3 of 17 done) |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-29, SALIN-30, SALIN-80 |
| **Blocked By** | SALIN-74 |

Point-cloud template files and `BaybayinCharacterSO` assets for all 17 Baybayin characters. **Done:** BA, KA, GA. **Remaining:** A, DA/RA, E, HA, I, LA, MA, NA, NGA, O, PA, SA, TA, U, WA, YA.

For each remaining character:
1. Use `TemplateRecorder.cs` (Debug tool) to record 3–5 stroke variants
2. Save point cloud to `Resources/Templates/<CHAR>_template.txt`
3. Create `BaybayinCharacterSO` asset at `Assets/ScriptableObjects/Characters/`
4. Set: `characterID`, `syllable`, `templateFileName`, `pronunciationClip` (null placeholder OK)

**Acceptance Criteria:**
- All 17 `BaybayinCharacterSO` assets exist under `Assets/ScriptableObjects/Characters/`
- All 17 `.txt` template files exist in `Resources/Templates/`
- `TemplateLoader.LoadAll()` returns exactly 17 entries
- Each SO has `characterID` and `syllable` populated; `templateFileName` matches the file
- `DollarPRecognizer` can recognise each of the 17 characters with confidence ≥ 0.60

> ⚠️ Critical path ticket. SALIN-29 (combat) and all level configs beyond Level 1 are blocked until this is complete.

---

## SALIN-28 — Recognition Edge Cases and Input Validation

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-29 |
| **Blocked By** | SALIN-18, SALIN-20 |

Handles all invalid or ambiguous input scenarios at the recognition and combat layers. Ensures the game never enters a stuck or undefined state due to bad touch input.

**Acceptance Criteria:**
- Stroke with fewer than `minimumPointCount` (8) points is silently discarded — no recognition attempt
- Multitouch stroke (more than one simultaneous touch) is rejected — only single-finger strokes processed
- Stroke duration < 100ms is treated as an accidental tap and discarded
- No input received mid-stroke for > 2 seconds: stroke auto-completes and is passed to recogniser
- Drawing during `GameOver` or `Paused` state: silently ignored (no events published)
- All rejection cases log a debug message at `DebugLogger` verbosity level only

---

## SALIN-35 — Implement Recognition Accuracy Logging System

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-63, SALIN-76 |
| **Blocked By** | SALIN-75, SALIN-29 |

`RecognitionLogger.cs` Singleton. Appends per-draw-attempt records to `recognition_log.csv` in `Application.persistentDataPath`. Append-only — survives app restarts. A `participantID` field (set via a study-session setup screen before evaluation begins) is included in every row.

**CSV schema:**
```
participantID, sessionID, levelID, waveNumber, characterExpected,
characterRecognized, confidenceScore, correct, timestamp
```

**Acceptance Criteria:**
- Logger initialised from Bootstrap scene alongside other managers
- Appends (never overwrites) on each recognition attempt
- `participantID` and `sessionID` set via PlayerPrefs input before session begins
- `RecognitionLogger.ExportLog()` returns the absolute file path
- File persists across app restarts in append mode

> ⚠️ Moved to Sprint 3 (was unscheduled). Required by SALIN-63 and SALIN-76 in Sprint 4 — must land before Apr 24.

---

## SALIN-77 — Baybayin Character SOs: Remaining 14 Characters

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-27, SALIN-32 |
| **Blocked By** | SALIN-74 |

Companion task to SALIN-27. While SALIN-27 covers recording the stroke template `.txt` files, this task explicitly covers creating the 14 remaining `BaybayinCharacterSO` ScriptableObject assets. Each SO must be fully populated with `characterID`, `syllable`, `templateFileName`, and a null placeholder for `pronunciationClip` (to be filled in by SALIN-32).

**Characters:** A, DA/RA, E, HA, I, LA, MA, NA, NGA, O, PA, SA, TA, U, WA, YA

**Acceptance Criteria:**
- 14 new `BaybayinCharacterSO` assets created at `Assets/ScriptableObjects/Characters/`
- Each SO has `characterID` (e.g. `"HA"`), `syllable` (e.g. `"ha"`), and `templateFileName` set
- `pronunciationClip` field is null (placeholder) — will be filled by SALIN-32
- All 14 assets visible in the Assets > Create menu path
- `TemplateLoader.LoadAll()` returns 17 entries after these SOs are created
