# 06 — Drawing and recognition

Prefix **`DRAW`**. Covers the touch surface, stroke capture, the `$P` point-cloud recognizer, thresholds, and accept/reject feedback. What a recognised glyph then *does* is in [`07-combat-and-defense.md`](07-combat-and-defense.md).

**Controls, in full:** touch and drag to draw; lift to submit; tap UI buttons to navigate. There is no joystick, no attack button and no gesture shortcut. Drawing is the only combat input.

---

### DRAW-12 — Be told clearly when a drawing worked
As a player, I want positive confirmation on a correct stroke, so that success reads as success.
- AC: A successful recognition produces a distinct visual acknowledgement.
- AC: The correct character's pronunciation plays on the defeat it causes.
- System: Drawing / feedback · `DrawFeedbackPresenter`, `AudioManager.PlayPronunciationClip`
- Status: Partial — audio and the attack/hit VFX are implemented; the GDD's dedicated "success burst" on the drawing surface is recorded as planned.
- Refs: `docs/system/06_UI_UX_and_Player_Flow.md` §4 (Success feedback: PLANNED)

### DRAW-14 — Not lose a stroke to the screen edge
As a player, I want to be able to draw near the edge of the screen, so that large characters are not clipped by the OS.
- AC: Input screen positions are clamped to the play-column screen rect so strokes cannot drift into the pillared margins.
- AC: The known limitation of OS edge gestures is documented.
- System: Drawing · `DrawingCanvas`, `AspectLockedCamera.PlayColumnScreenRect`
- Status: Partial — clamping to the play column is implemented; a deliberate edge dead-zone for OS gestures is a documented risk (RISK), not an implemented feature.
- Refs: `DrawingCanvas.cs`, `docs/capstone/GDD.md` §6.2, `docs/system/04_Gameplay_Systems.md` §10

### DRAW-16 — Have visually similar characters told apart
As a player, I want the game to distinguish characters that look alike, so that a correct drawing is not read as the wrong character.
- AC: Each character has multiple recorded templates.
- AC: DA and RA are separated by the recogniser; RA has its own templates and its own art.
- System: Recognition · `TemplateLoader`, `Resources/Templates/*`, `GlyphConfusionPairsSO`
- Status: Unclear — the team **confirmed** that the recogniser can separate RA from DA, but explicitly recorded that as a confirmation and not a measurement; no evaluation has been run. The `BaybayinIdCanonicalizer` RA→DA fold was to be removed as part of SALIN-217.
- Refs: `docs/design/spec-rulings-2026-09.md` §3 "RA recognition", OI-5, SALIN-217

### DRAW-18 — Have the guide withdrawn as I get better
As a player, I want the training wheels to come off gradually, so that I end up drawing from memory.
- AC: Guidance is withdrawn in stages within the encounter as the player demonstrates the symbol.
- System: Combat guidance · staged withdrawal
- Status: Missing — staged in-encounter guidance withdrawal is not implemented; D-004/D-014 are recorded as MISSING/DEVIATES and SALIN-279 is a draft.
- Refs: `docs/audit/STATUS-2026-09-13.md` Master row 9, SALIN-279 (draft, unapproved)
