# Tracing Dojo Dropdown Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Tracing Dojo's permanent left-side character list with a top-right collapsible dropdown, expand the tracing canvas to near-full-screen, and turn recognition feedback into a fading toast overlay.

**Architecture:** Two new focused MonoBehaviours (`CharacterDropdown`, `FeedbackToast`) plus small edits to `TracingDojoController`. The existing `CharacterListPopulator` and `CharacterListRow` are reused unchanged. The scene `TracingDojo.unity` is restructured: new TopBar with Back (left) and dropdown header (right), an expanded TracingArea, and a hidden DropdownPanel + full-screen Backdrop for outside-tap dismissal.

**Tech Stack:** Unity 6 LTS (6000.3.9f1), C#, TextMeshPro, Unity UI (uGUI).

**Reference spec:** [docs/superpowers/specs/2026-04-22-tracing-dojo-dropdown-design.md](../specs/2026-04-22-tracing-dojo-dropdown-design.md)

**Testing note:** Per spec, there are no automated tests for this change — it is scene wiring plus light UI scripting. Each task has a manual verification step in the Unity Editor.

---

## File Structure

**Create:**
- `Assets/Scripts/UI/TracingDojo/CharacterDropdown.cs` — header button + panel + backdrop controller (~45 lines).
- `Assets/Scripts/UI/TracingDojo/FeedbackToast.cs` — verdict/confidence display with alpha fade (~40 lines).

**Modify:**
- `Assets/Scripts/UI/TracingDojo/TracingDojoController.cs` — swap label fields for `_toast` + `_dropdown`, update `SelectCharacter` and `RenderFeedback`.
- `Assets/_Scenes/TracingDojo.unity` — hierarchy restructure + component wiring.

**Unchanged:**
- `Assets/Scripts/UI/TracingDojo/CharacterListPopulator.cs`
- `Assets/Scripts/UI/TracingDojo/CharacterListRow.cs`
- `Assets/Scripts/UI/TracingDojo/GhostStrokeRenderer.cs`
- `Assets/Scripts/UI/TracingDojo/DojoNavigator.cs`

---

## Task 1: Create `CharacterDropdown` script

**Files:**
- Create: `Assets/Scripts/UI/TracingDojo/CharacterDropdown.cs`

- [ ] **Step 1: Write `CharacterDropdown.cs`**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Text _headerLabel;
    [SerializeField] private Button _headerButton;
    [SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _backdrop;
    [SerializeField] private Button _backdropButton;
    [SerializeField] private string _placeholderText = "Select \u25BE";

    private void Awake()
    {
        _headerButton.onClick.AddListener(Toggle);
        _backdropButton.onClick.AddListener(Close);
        _headerLabel.text = _placeholderText;
        SetOpen(false);
    }

    public void SetCurrentCharacter(BaybayinCharacterSO character)
    {
        _headerLabel.text = character != null
            ? $"{character.characterID} \u25BE"
            : _placeholderText;
    }

    public void Toggle() => SetOpen(!_panel.activeSelf);

    public void Close() => SetOpen(false);

    private void SetOpen(bool open)
    {
        _panel.SetActive(open);
        _backdrop.SetActive(open);
    }
}
```

- [ ] **Step 2: Let Unity import the script**

Switch focus to Unity Editor. Wait for the spinner to finish and the console to stay clear. Expected: no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/TracingDojo/CharacterDropdown.cs Assets/Scripts/UI/TracingDojo/CharacterDropdown.cs.meta
git commit -m "feat(dojo): SALIN-69 add CharacterDropdown controller"
```

---

## Task 2: Create `FeedbackToast` script

**Files:**
- Create: `Assets/Scripts/UI/TracingDojo/FeedbackToast.cs`

- [ ] **Step 1: Write `FeedbackToast.cs`**

```csharp
using System.Collections;
using TMPro;
using UnityEngine;

public class FeedbackToast : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _verdictLabel;
    [SerializeField] private TMP_Text _confidenceLabel;
    [SerializeField] private float _holdSeconds = 1.5f;
    [SerializeField] private float _fadeSeconds = 0.2f;

    private static readonly Color PassColor = new(0.20f, 0.55f, 0.25f);
    private static readonly Color FailColor = new(0.70f, 0.20f, 0.20f);

    private Coroutine _running;

    private void Awake()
    {
        _group.alpha = 0f;
    }

    public void Show(string characterID, float score, bool pass)
    {
        _verdictLabel.text = characterID;
        _verdictLabel.color = pass ? PassColor : FailColor;
        _confidenceLabel.text = $"{score * 100f:F0}%";

        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(FadeCycle());
    }

    private IEnumerator FadeCycle()
    {
        yield return Fade(0f, 1f, _fadeSeconds);
        yield return new WaitForSeconds(_holdSeconds);
        yield return Fade(1f, 0f, _fadeSeconds);
        _running = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        _group.alpha = to;
    }
}
```

- [ ] **Step 2: Let Unity import the script**

Switch to Unity. Expected: no compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/TracingDojo/FeedbackToast.cs Assets/Scripts/UI/TracingDojo/FeedbackToast.cs.meta
git commit -m "feat(dojo): SALIN-69 add FeedbackToast overlay"
```

---

## Task 3: Update `TracingDojoController`

**Files:**
- Modify: `Assets/Scripts/UI/TracingDojo/TracingDojoController.cs`

- [ ] **Step 1: Replace file contents**

Overwrite the entire file with:

```csharp
using UnityEngine;

public class TracingDojoController : MonoBehaviour
{
    [SerializeField] private GhostStrokeRenderer _ghost;
    [SerializeField] private FeedbackToast _toast;
    [SerializeField] private CharacterDropdown _dropdown;
    [SerializeField] private CharacterRegistrySO _registry;

    private BaybayinCharacterSO _selected;

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += OnResolved;
        RecognitionLogger.LoggingEnabled = false;
        if (GameManager.Instance != null) GameManager.Instance.EnterPractice();
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= OnResolved;
        RecognitionLogger.LoggingEnabled = true;
        if (GameManager.Instance != null) GameManager.Instance.ExitPractice();
    }

    public void SelectCharacter(BaybayinCharacterSO character)
    {
        _selected = character;
        _ghost.Render(character);
        _dropdown.SetCurrentCharacter(character);
        _dropdown.Close();
    }

    private void OnResolved(RecognitionResult result, bool passedThreshold, float threshold)
    {
        _toast.Show(result.characterID, result.score, passedThreshold);

        if (passedThreshold
            && _selected != null
            && result.characterID == _selected.characterID
            && _selected.pronunciationClip != null)
        {
            AudioManager.Instance.PlaySFX(_selected.pronunciationClip);
        }
    }
}
```

- [ ] **Step 2: Let Unity import**

Expected: no compile errors. The inspector for whatever GameObject holds `TracingDojoController` will show two "Missing" field references (`_toast` and `_dropdown`) and the removed fields will be gone. That is expected — we wire them in later tasks.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/TracingDojo/TracingDojoController.cs
git commit -m "refactor(dojo): SALIN-69 route feedback through FeedbackToast and update dropdown on select"
```

---

## Task 4: Scene — build the new TopBar

**Files:**
- Modify: `Assets/_Scenes/TracingDojo.unity`

All of Task 4 happens inside the Unity Editor.

- [ ] **Step 1: Open the scene and back up**

Open `Assets/_Scenes/TracingDojo.unity`. Confirm you are on the `feature/SALIN-69-tracing-dojo` branch. (The scene is already dirty from prior work in the branch — don't discard those changes.)

- [ ] **Step 2: Create `TopBar`**

Right-click the main `Canvas` → Create Empty → rename `TopBar`.

Inspector → Rect Transform:
- Anchor preset: top, stretch (hold Alt to also set position/size).
- Left = 0, Right = 0, Pos Y = 0, Height = 120.

- [ ] **Step 3: Move `BackButton` under `TopBar`**

Drag the existing Back button into `TopBar`. Its anchors will break — re-anchor to top-left. Set Pos X / Pos Y so it sits where it does in the mockup (roughly 24 px inset from the left and top).

- [ ] **Step 4: Create `DropdownHeaderButton`**

Right-click `TopBar` → UI → Button - TextMeshPro → rename `DropdownHeaderButton`. Re-anchor top-right. Size ~200×80. Pos X / Pos Y so it mirrors the Back button (roughly 24 px inset from the right and top).

Rename the child `Text (TMP)` to `HeaderLabel`. Set its default text to `Select ▾`.

- [ ] **Step 5: Verify in Play mode**

Press Play. Expected: Back button in top-left, `Select ▾` button in top-right, nothing else visually changed yet. Stop Play. Do not save if anything looks off — fix positions first.

- [ ] **Step 6: Commit** (scene only — components not wired yet)

```bash
git add Assets/_Scenes/TracingDojo.unity
git commit -m "feat(dojo): SALIN-69 add TopBar with Back and dropdown header button"
```

---

## Task 5: Scene — expand the tracing canvas

**Files:**
- Modify: `Assets/_Scenes/TracingDojo.unity`

- [ ] **Step 1: Rename/find the tracing area container**

Identify the parent GameObject holding `GhostStrokeRenderer` and the stroke input. If it isn't already named, rename to `TracingArea`.

- [ ] **Step 2: Re-anchor `TracingArea`**

Rect Transform:
- Anchor preset: stretch, stretch.
- Left = 0, Right = 0, Top = 120 (matches `TopBar` height), Bottom = 0.

- [ ] **Step 3: Resize `GhostStrokeRenderer`**

Make its RectTransform fill `TracingArea` (anchors stretch/stretch, offsets 0).

- [ ] **Step 4: Verify in Play mode**

Press Play. Expected: the dark tracing area now fills the screen below the top bar, edge-to-edge. The old left-side list is still present (we delete that in Task 7). Stop Play.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Scenes/TracingDojo.unity
git commit -m "feat(dojo): SALIN-69 expand tracing canvas to fill below top bar"
```

---

## Task 6: Scene — build the `FeedbackToast`

**Files:**
- Modify: `Assets/_Scenes/TracingDojo.unity`

- [ ] **Step 1: Create `FeedbackToast` GameObject**

Right-click `TracingArea` → Create Empty → rename `FeedbackToast`. Re-anchor top-center of `TracingArea`, Pos Y offset ~80 from top, width ~400, height ~120.

Add components:
- `CanvasGroup` (Alpha = 0, Interactable off, Blocks Raycasts off).
- `FeedbackToast` (the script from Task 2).

- [ ] **Step 2: Create two child TMP_Text objects**

Right-click `FeedbackToast` → UI → Text - TextMeshPro. Rename `VerdictLabel`. Repeat for `ConfidenceLabel`. Stack them vertically inside the toast (e.g., verdict on top, confidence below).

- [ ] **Step 3: Find and delete the old standalone verdict/confidence labels**

Locate the pre-existing TMP labels that the old `TracingDojoController` used for `_verdictLabel` and `_confidenceLabel`. Delete them (they are superseded by the new toast children).

- [ ] **Step 4: Wire `FeedbackToast` component**

Select the `FeedbackToast` GameObject. In the inspector:
- `_group` → drag the same GameObject's `CanvasGroup`.
- `_verdictLabel` → `VerdictLabel` child.
- `_confidenceLabel` → `ConfidenceLabel` child.
- Leave `_holdSeconds` (1.5) and `_fadeSeconds` (0.2) at defaults.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Scenes/TracingDojo.unity
git commit -m "feat(dojo): SALIN-69 add FeedbackToast overlay to tracing area"
```

---

## Task 7: Scene — build the dropdown backdrop, panel, and move the list

**Files:**
- Modify: `Assets/_Scenes/TracingDojo.unity`

- [ ] **Step 1: Create `DropdownBackdrop`**

Right-click `Canvas` → UI → Image → rename `DropdownBackdrop`. Place it as a sibling of `TopBar` and `TracingArea`, **above** them in hierarchy order so it renders on top when active.

Inspector:
- Rect Transform: anchor stretch/stretch, all offsets 0.
- Image → Color → Alpha = 0 (fully transparent). Raycast Target = on.
- Add a `Button` component. Leave transition set to `None`.
- Set the GameObject **inactive**.

- [ ] **Step 2: Create `DropdownPanel`**

Right-click `Canvas` → UI → Panel → rename `DropdownPanel`. Place it **below** `DropdownBackdrop` in hierarchy (so it renders above the backdrop).

Inspector → Rect Transform:
- Anchor top-right.
- Size: Width ~360, Height = 40% of reference resolution height (e.g., ~720 px on a 1920×1080 reference).
- Position: Pos X = -24 (inset from right), Pos Y = -140 (just below the top bar).
- Set the GameObject **inactive**.

- [ ] **Step 3: Move the existing list into `DropdownPanel`**

Locate the existing `ScrollRect` (currently the character list on the left). Drag it into `DropdownPanel`. Re-anchor stretch/stretch inside the panel with small padding (e.g., 8 px all sides).

- [ ] **Step 4: Delete the old left-panel container**

The old container for the character list (the grey left column in the screenshot) is now empty — delete it.

- [ ] **Step 5: Verify `CharacterListPopulator` wiring still points at `Content`**

Select the GameObject with `CharacterListPopulator`. Inspector should show:
- `_content` → the `Content` transform inside the moved ScrollRect (usually `ScrollRect/Viewport/Content`).
- `_rowPrefab`, `_registry`, `_controller` — unchanged.

If `_content` is now null, re-assign it.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Scenes/TracingDojo.unity
git commit -m "feat(dojo): SALIN-69 move character list into hidden dropdown panel with backdrop"
```

---

## Task 8: Scene — wire the `CharacterDropdown` component

**Files:**
- Modify: `Assets/_Scenes/TracingDojo.unity`

- [ ] **Step 1: Add `CharacterDropdown` component**

Select `DropdownPanel`. Add Component → `CharacterDropdown` (the script from Task 1).

- [ ] **Step 2: Wire serialized fields**

- `_headerLabel` → `HeaderLabel` (TMP_Text child of `DropdownHeaderButton`).
- `_headerButton` → `DropdownHeaderButton` (its `Button` component).
- `_panel` → `DropdownPanel` GameObject (itself).
- `_backdrop` → `DropdownBackdrop` GameObject.
- `_backdropButton` → `DropdownBackdrop`'s `Button` component.
- `_placeholderText` — leave default (`Select ▾`).

- [ ] **Step 3: Do NOT wire onClick in the inspector**

`Awake()` adds the listeners in code, so the inspector `onClick()` lists on both buttons should remain empty. If any lingering `()` entries exist from earlier experiments, remove them.

- [ ] **Step 4: Verify in Play mode**

Press Play. Expected:
- Tap `Select ▾` → `DropdownPanel` and `DropdownBackdrop` both become visible; the character list is shown inside the panel.
- Tap the backdrop (anywhere outside the panel) → both hide.
- Tap `Select ▾` again → opens again.

Stop Play.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Scenes/TracingDojo.unity
git commit -m "feat(dojo): SALIN-69 wire CharacterDropdown toggle and backdrop dismissal"
```

---

## Task 9: Scene — wire `TracingDojoController` references

**Files:**
- Modify: `Assets/_Scenes/TracingDojo.unity`

- [ ] **Step 1: Select the controller GameObject**

Find the GameObject with `TracingDojoController` (likely a root-level `DojoController` or similar). Its inspector now shows unresolved `_toast` and `_dropdown` fields (and the old `_verdictLabel` / `_confidenceLabel` fields are gone).

- [ ] **Step 2: Wire new references**

- `_ghost` → unchanged.
- `_toast` → `FeedbackToast` GameObject's `FeedbackToast` component.
- `_dropdown` → `DropdownPanel`'s `CharacterDropdown` component.
- `_registry` → unchanged.

- [ ] **Step 3: Verify in Play mode — golden path**

Press Play:
1. Tap `Select ▾`.
2. Tap any character row (e.g., `BA`).
3. Expect: the panel closes, the header updates to `BA ▾`, the ghost stroke shows the BA template.
4. Draw the BA stroke on the canvas.
5. Expect: the feedback toast fades in showing `BA` and a percentage (green if pass, red if fail), then fades out.

- [ ] **Step 4: Verify edge cases**

Still in Play:
1. Draw several strokes in quick succession — only one toast should be visible at any time (no flicker, no stacked fades).
2. Open the dropdown, then tap outside the panel (anywhere on the backdrop) — panel closes.
3. Tap Back — scene returns to the main menu as before.

Stop Play.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Scenes/TracingDojo.unity
git commit -m "feat(dojo): SALIN-69 wire TracingDojoController to toast and dropdown"
```

---

## Task 10: Final cleanup pass

**Files:**
- Various — visual polish only.

- [ ] **Step 1: Layout pass on mobile aspect ratios**

In Game view, switch aspect ratios (e.g., 9:16 portrait, 9:19.5 portrait) and confirm:
- Back button and dropdown header stay anchored to their corners.
- Dropdown panel fits within the screen on narrow devices.
- Feedback toast is visible and not clipped.

If something breaks on a common aspect, adjust offsets and commit.

- [ ] **Step 2: Check console**

Confirm no warnings or errors during a full play cycle (enter scene → select → draw → back).

- [ ] **Step 3: Final commit if any tweaks were made**

```bash
git add Assets/_Scenes/TracingDojo.unity
git commit -m "polish(dojo): SALIN-69 finalize dropdown UI layout on mobile aspects"
```

---

## Verification checklist (complete before PR)

- [ ] Play mode: full golden path works (open dropdown → select → ghost renders → draw → feedback toast → back).
- [ ] Play mode: outside-tap dismisses the dropdown.
- [ ] Play mode: rapid strokes show one toast at a time.
- [ ] Console: no errors or new warnings during a play cycle.
- [ ] Scene: no orphaned/old GameObjects from the previous left-panel layout.
- [ ] Scripts: no leftover `Debug.Log` calls.
- [ ] Branch: commits follow `feat/refactor/polish(dojo): SALIN-69 ...` format.
