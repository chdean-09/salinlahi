# Tracing Dojo UI — Character Dropdown & Expanded Canvas

**Date:** 2026-04-22
**Jira:** SALIN-69 (Tracing Dojo)
**Status:** Draft — pending review

## Summary

Replace the permanent left-side character list in the Tracing Dojo scene with a collapsible dropdown anchored in the top-right of the top bar (mirroring the Back button in the top-left). Expand the tracing canvas to fill nearly the full screen below the top bar, and move recognition feedback into a fading toast overlay on the canvas.

## Motivation

The current Tracing Dojo layout dedicates roughly half of the screen to a scrollable character list, which:

- Leaves the tracing area cramped — bad for accurate stroke input on mobile.
- Wastes space while the learner is focused on a single character (the common case).
- Makes the scene feel visually off-balance (Back button alone in the top-left; large list column beside a smaller canvas).

A collapsible dropdown reclaims the screen for tracing while keeping character switching one tap away.

## Goals

- Tracing canvas fills the area below the top bar edge-to-edge.
- Character selection is reachable in one tap from any state of the dojo.
- Currently selected character is visible at a glance (shown as the dropdown's header label).
- Feedback (verdict + confidence) appears as a brief toast over the canvas and dismisses itself.
- Reuse existing `CharacterListPopulator` and `CharacterListRow` with no script changes.

## Non-goals

- No search / filter / categorization of characters inside the dropdown.
- No animations beyond a simple `CanvasGroup` alpha fade for the feedback toast.
- No changes to recognition, registry, audio, or scene-navigation logic.
- No restyling of the Back button or existing canvas visuals beyond size.

## User-facing behavior

- **Closed dropdown:** top-right shows a button labeled with the current character's ID and a caret (e.g., `BA ▾`). Tapping it opens the panel.
- **Open dropdown:** a panel drops below the header, containing a scrollable list of rows. Each row shows the character's glyph icon and its ID. Max height ≈ 40% of screen — scrolls if content overflows. A transparent backdrop behind the panel closes it on outside-tap.
- **Selecting a row:** updates the ghost stroke, updates the dropdown header label, and closes the panel automatically.
- **Feedback toast:** on recognition result, the verdict (character ID) and confidence (percentage) fade in as an overlay centered in the canvas area, then fade out after ~1.5 s. Color stays green/red per pass/fail as today.

## Architecture

### New components

#### `CharacterDropdown` (`Assets/Scripts/UI/TracingDojo/CharacterDropdown.cs`)

Small MonoBehaviour that owns the dropdown's open/close state and the header label. Approx. 40 lines.

**Serialized fields:**
- `TMP_Text _headerLabel` — text on the header button.
- `Button _headerButton` — the toggle button in the top bar.
- `GameObject _panel` — the dropdown body (holds the ScrollRect).
- `GameObject _backdrop` — full-screen transparent image behind the panel.

**Public API:**
- `void SetCurrentCharacter(BaybayinCharacterSO character)` — updates `_headerLabel.text` to `"{character.characterID} ▾"`.
- `void Toggle()` — flips active state of `_panel` and `_backdrop`.
- `void Close()` — deactivates both.

**Wiring:**
- `_headerButton.onClick` → `Toggle()` (set in inspector or in `Awake()`).
- `_backdrop`'s `Button.onClick` → `Close()` (set in inspector).

#### `FeedbackToast` (`Assets/Scripts/UI/TracingDojo/FeedbackToast.cs`)

Small MonoBehaviour that renders the verdict/confidence pair and fades itself in and out. Approx. 30 lines.

**Serialized fields:**
- `CanvasGroup _group` — drives alpha.
- `TMP_Text _verdictLabel`
- `TMP_Text _confidenceLabel`
- `float _holdSeconds = 1.5f`
- `float _fadeSeconds = 0.2f`

**Public API:**
- `void Show(string characterID, float score, bool pass)` — sets text, sets color (green/red as today), starts a coroutine that fades in, holds, fades out.

**Implementation notes:**
- Cancel any running coroutine before starting a new one, so rapid successive strokes don't leave multiple fades overlapping.
- `_group.alpha = 0` on `Awake()` so it's hidden before the first result.

### Modified components

#### `TracingDojoController` (`Assets/Scripts/UI/TracingDojo/TracingDojoController.cs`)

- Remove fields: `_verdictLabel`, `_confidenceLabel`.
- Add fields: `[SerializeField] FeedbackToast _toast`, `[SerializeField] CharacterDropdown _dropdown`.
- `SelectCharacter(character)` additionally calls:
  - `_dropdown.SetCurrentCharacter(character)`
  - `_dropdown.Close()`
  - Clears the toast (either `_toast.Hide()` or relies on alpha already being 0).
- `RenderFeedback(characterID, score, pass)` delegates to `_toast.Show(characterID, score, pass)` instead of setting label text/color directly.

### Unchanged components

- `CharacterListPopulator.cs` — no code change. `_content` reference is re-parented in the scene to point into the dropdown panel's ScrollRect.
- `CharacterListRow.cs` — no change.
- `GhostStrokeRenderer.cs` — no change; its RectTransform is resized in the scene to fill the expanded canvas area.
- `DojoNavigator.cs` — no change; Back button behavior unaffected.
- `CharacterRegistrySO`, `BaybayinCharacterSO` — no change.

## Scene hierarchy (target)

```
Canvas (Screen Space - Overlay)
├── TopBar                        (new empty panel, top-stretch, ~120 px tall)
│   ├── BackButton                (moved into TopBar, top-left)
│   └── DropdownHeaderButton      (NEW, top-right)
│       └── HeaderLabel (TMP_Text)
├── TracingArea                   (anchors stretched, top offset = TopBar height)
│   ├── GhostStrokeRenderer       (resized to fill TracingArea)
│   └── FeedbackToast             (NEW, overlay anchored center or top-center)
│       ├── [CanvasGroup component]
│       ├── VerdictLabel
│       └── ConfidenceLabel
├── DropdownBackdrop              (NEW, full-stretch transparent Image + Button, disabled by default)
└── DropdownPanel                 (NEW, top-right under header, disabled by default, max height ≈ 40% screen)
    └── ScrollRect                (moved from the old left panel)
        └── Viewport
            └── Content           (unchanged, still referenced by CharacterListPopulator._content)
```

## Data flow

1. **Open dropdown** — user taps `DropdownHeaderButton` → `CharacterDropdown.Toggle()` → panel + backdrop activated.
2. **Select a character** — user taps a `CharacterListRow` → its bound action calls `TracingDojoController.SelectCharacter(character)` → controller calls `_ghost.Render(character)`, `_dropdown.SetCurrentCharacter(character)`, `_dropdown.Close()`.
3. **Outside-tap dismissal** — user taps the backdrop → backdrop's `Button.onClick` → `CharacterDropdown.Close()`.
4. **Recognition result** — `EventBus.OnRecognitionResolved` fires → `TracingDojoController.OnResolved` → `RenderFeedback` → `_toast.Show(...)` fades the toast in, holds, fades out.

## Error handling / edge cases

- **Rapid strokes producing overlapping toasts:** `FeedbackToast.Show` cancels the previous coroutine before starting a new one.
- **Dropdown open when a stroke is drawn:** strokes should be blocked by the backdrop (it sits above the canvas). No special handling needed — the backdrop catches the input.
- **No character selected yet (initial state):** the header label reads the default text set in the scene (e.g., `Select ▾`). Recognition still works regardless of selection, because feedback is driven by `OnRecognitionResolved`, not by `_selected`.
- **Registry empty:** existing `CharacterListPopulator` behavior (an empty Content) — out of scope for this change.

## Testing

Manual test plan (no automated tests — this is scene wiring + light scripting):

- Enter the Tracing Dojo from the main menu. Confirm:
  - Back button top-left, dropdown header top-right, tracing canvas fills the rest of the screen.
- Tap the header. Confirm the panel opens below it, the list is scrollable, and the backdrop is present but invisible.
- Tap outside the panel. Confirm it closes.
- Tap the header to re-open, then tap a row. Confirm:
  - The header label updates to the selected character's ID.
  - The panel closes.
  - The ghost stroke updates on the canvas.
- Draw a stroke. Confirm the feedback toast fades in with the verdict + confidence, then fades out automatically.
- Draw several strokes quickly. Confirm only one toast is ever visible at a time (no flicker or stacked fades).
- Tap Back. Confirm the scene returns to the main menu as today.

## Risks

- **Unity scene merge conflicts:** `TracingDojo.unity` is already dirty in the current working tree. Wiring steps should be committed in a single commit after verifying in Play mode to minimize rework if the scene needs reshuffling.
- **TopBar height on tall/short devices:** a fixed pixel height may feel wrong on unusual aspect ratios. Accept the risk for now; revisit if QA flags it.

## Out of scope (explicitly deferred)

- Dropdown search / filter.
- Animated transitions for the dropdown itself (scale/slide).
- Categorized sections (consonants vs. vowels).
- Restyling of `CharacterListRow` beyond its current appearance.
