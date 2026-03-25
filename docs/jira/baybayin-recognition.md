# Epic: Baybayin Recognition System (SALIN-3)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

Touch input capture, LineRenderer stroke visualisation, $P recogniser implementation, Baybayin template library, edge-case handling, and accuracy logging.

---

## SALIN-18 — Configure Unity Input System and Implement StrokeCapture.cs

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Install and configure Unity's Input System package for touch input. Implement `StrokeCapture.cs`, which listens for touch press, drag, and release events and outputs an ordered list of 2D points representing the player's drawn stroke.

**Acceptance Criteria:**
- Unity Input System package is installed and the old Input Manager is disabled
- `StrokeCapture` uses `EnhancedTouch` or an `InputAction` asset to track a single touch stroke
- `OnStrokeStarted`, `OnStrokeUpdated(List<Vector2> points)`, and `OnStrokeCompleted(List<Vector2> points)` events are fired at appropriate times
- Stroke captures minimum 5 points before `OnStrokeCompleted` is considered valid
- Test on device or simulator: drawing a shape produces a non-empty point list in the console

---

## SALIN-19 — Implement DrawingCanvas.cs with LineRenderer Stroke Visualization

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `DrawingCanvas.cs`, which subscribes to `StrokeCapture` events and renders the stroke as a `LineRenderer` in world space. The trail must be visible during the draw and cleared after recognition completes.

**Acceptance Criteria:**
- `DrawingCanvas` creates and configures a `LineRenderer` component at runtime
- As `OnStrokeUpdated` fires, the LineRenderer positions are updated in real time
- On `OnStrokeCompleted`, the line remains visible for a short configurable duration then fades out or clears
- Line width, color, and material are configurable from the Inspector
- Test: draw a stroke on device — a visible line follows the finger and disappears after the configured duration

---

## SALIN-20 — Implement DollarPRecognizer.cs — Full $P Algorithm

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `DollarPRecognizer.cs`, a full C# port of the $P point-cloud gesture recogniser. It takes a list of 2D points as input and returns the best-matching template name and a confidence score (0.0–1.0).

**Acceptance Criteria:**
- `DollarPRecognizer.Recognize(List<Vector2> points)` returns a `RecognitionResult { string templateName; float score; }`
- Templates can be added via `AddTemplate(string name, List<Vector2> points)`
- Resampling, scaling, and origin translation match the reference $P paper algorithm
- At least one smoke test: add a circular template, pass a rough circle of 32 points, score > 0.8
- Recogniser handles an empty point list gracefully (returns null result, no exception)

---

## SALIN-21 — SPIKE: Investigate $P Recognizer Accuracy Issues (Time-Boxed: 4 hours)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Time-boxed (4-hour) spike to investigate known and potential accuracy issues with the $P recogniser on Baybayin character strokes.

**Acceptance Criteria:**
- Spike is completed within 4 hours of wall-clock time
- A findings comment covers: tested characters, observed failure cases, candidate fixes
- At least 5 distinct Baybayin characters are tested
- A recommended set of parameter values (e.g. resample count, score threshold) is documented
- Any code changes are committed to a `spike/dollar-p-accuracy` branch, not merged

---

## SALIN-27 — Build Baybayin Template Library — All 17 Characters (3-5 Variants Each)

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-28 — Recognition Edge Cases and Input Validation

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-35 — Implement Recognition Accuracy Logging System

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-74 — Implement TemplateLoader.cs — Standalone Baybayin Template Loading

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `TemplateLoader.cs` as a standalone utility class that loads Baybayin stroke templates from `Resources/Templates/` and parses them into `List<Vector2>` point clouds. `DollarPRecognizer` depends on `TemplateLoader` — it must NOT load templates itself.

**Acceptance Criteria:**
- `TemplateLoader.Load(templateFileName)` returns a non-null `List<Vector2>` for each of the 17 valid template files
- `TemplateLoader.LoadAll()` returns a `Dictionary<string, List<Vector2>>` with exactly 17 entries
- Given a fileName that does not exist, `Load()` returns null and logs a warning — no exception thrown
- `TemplateLoader` is a static utility class — no MonoBehaviour inheritance, no scene dependency
- `DollarPRecognizer` obtains all templates exclusively via `TemplateLoader.LoadAll()` at initialisation

**Req IDs:** TDD-REQ-004, SAL-REQ-002

---

## SALIN-75 — Implement RecognitionManager.cs — Coordinate StrokeCapture and DollarPRecognizer

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `RecognitionManager.cs` as the coordinator between `StrokeCapture` and `DollarPRecognizer`. It subscribes to `EventBus.OnStrokeCompleted`, passes the stroke to the recogniser, and publishes `EventBus.OnCharacterRecognized` or `EventBus.OnDrawingFailed` based on the result.

**Acceptance Criteria:**
- `RecognitionManager` subscribes to `EventBus.OnStrokeCompleted` on Awake and unsubscribes on OnDestroy
- Given `OnStrokeCompleted` fires with a valid point list, `DollarPRecognizer.Recognize()` is called within the same frame
- Given `Recognize()` returns confidence >= 0.60, `EventBus.OnCharacterRecognized(result)` is published
- Given `Recognize()` returns confidence < 0.60, `EventBus.OnDrawingFailed` is published
- `RecognitionManager` is a MonoBehaviour placed in the Gameplay scene — it is NOT a Singleton

**Req IDs:** TDD-REQ-004, SAL-REQ-002, SAL-REQ-003
