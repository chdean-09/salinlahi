# Mobile typography audit — in progress

Date: 2026-10-06 (Asia/Manila). Requested scope: Levels 1–15 and their story, learning, hint, challenge and scroll surfaces. **Not complete.**

## Observed findings and corrections

- **Font identity preserved:** The user clarified that readability polish must retain the original font style. The Liberation Sans replacement was reverted by restoring `Assets/Resources/Fonts/TutorialFont.asset` from the exact pre-task backup, including its VT323 metrics, atlas and material. Matching file hashes verified the restoration; the existing `.meta` was untouched. The temporary font-replacement utility was removed. The sans-serif screenshots below are superseded experiment evidence, not the current design.
- **P1 — crowded story strokes:** `CutscenePlayer` combined bold, 0.35–0.45 outline widths, 0.15 face dilation and a dark offset underlay. Narration now uses regular weight, a 0.08 outline and no face dilation. The shared underlay offset is reduced. Rendered cutscene verification remains pending.
- **P1 — tiny autosized copy:** `UITextScale` previously allowed 28-unit text and 40-unit body copy. The new scale uses Caption 44, Secondary 48, Body 54, Title 64 and Display 80. Body corresponds to approximately 16px on a 320px-wide 1080-reference canvas.
- **P1 — dialogue overflow after increasing size:** A fresh Level 5 capture at 320×568 exposed overflow after the reading floor increased. Dialogue and challenge-hint prose now use a masked reading viewport instead of shrinking. Dragging cancels the click eligibility so scrolling cannot also advance dialogue. This last correction has **not** been visually verified yet.
- Modal parchment has more room; Ready buttons have taller targets; hint labels retain their reading size. These need the complete viewport matrix before approval.

## Screenshot evidence

Local evidence is in `Temp/MobileReadability/`:

[Open the screenshot gallery with embedded images](/D:/projects/capstone/salinlahi/Temp/MobileReadability/gallery.md), or [open the HTML gallery with all 15 level selectors](/D:/projects/capstone/salinlahi/Temp/MobileReadability/gallery.html). These currently contain only the four Level 5 captures below. Missing levels are explicitly marked pending.

| Capture | What it verifies |
| --- | --- |
| `before-level05-dialogue.png` | Original font in the real QA gameplay scene, captured at 1284×2778 |
| `pass1-level05-dialogue-320.png` | Superseded font-replacement experiment at 320×568; replacement has been reverted |
| `pass1-level05-dialogue2-320.png` | Superseded experiment showing a second authored Level 5 dialogue line |
| `pass2-level05-dialogue-320.png` | Superseded experiment: larger reading floor exposed overflow, prompting the reading-viewport correction |

The pending gallery uses actual `LevelConfigSO` content and the existing UI controllers. It isolates presentation in the existing QA profile; it is not a complete gameplay walkthrough or a physical phone test.

## Verification status

- **PASS after font restoration:** the font asset matches the pre-task backup byte-for-byte, and its original VT323 family and `.meta` GUID remain intact. A fresh .NET build of the Editor test project and its runtime/editor dependencies finished with zero errors and 16 warnings. A temporary validation target excluded the removed font-experiment source from Unity's stale generated Editor project; generated projects were not edited. This checks source compilation only; Unity tests did not execute.
- **PASS:** .NET source builds of runtime/editor code and the existing Editor test project. An additional temporary MSBuild target also compiled `ReadingScrollRect.cs` and `MobileTypographyTests.cs`, which Unity had not imported yet. There were zero compiler errors, with warnings reported in the saved build output. This is **not** Unity Editor compilation or test execution.
- **PASS:** focused whitespace/diff check before the final pending visual pass.
- **Observed:** Unity rendered the first two experimental passes. Their replacement font is now reverted; fresh screenshots using the original VT323 font and current layout corrections remain pending.
- **BLOCKED:** Unity refresh/compilation of the latest reading-viewport changes, Unity Edit/Play Mode tests, and further screenshots. Desktop control returned `GetCursorPos failed: Access is denied (0x80070005)` after screenshots showed only the desktop background. An unlock/access-restoration request is pending.
- **NOT VERIFIED:** post-scroll dialogue, all cutscene panels, all hint states, all level/challenge variants, large-phone layouts, notched-phone safe areas and physical-device readability. No full audit health score is assigned without those observations.

## Remaining work

1. Restore desktop access, refresh Unity, inspect compilation and run the typography, responsive-layout, hint and font-rendering tests.
2. Capture and inspect each authored reading surface for Levels 1–15 at 320×568, 360×800 and 430×932; include a notched device in Simulator.
3. Check overflow, button wrapping, bold strokes, contrast and scroll-to-bottom behavior; iterate with new captures after each correction.
4. Exercise real dialogue clicks/drags and the level flow, then remove the temporary local probe and test-runner sources.
5. Review only this task's changes against the saved pre-task file copies. Preserve the substantial unrelated working-tree changes.
