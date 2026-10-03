# Verification — WI-5 / WI-6 / WI-8 (scene half)

- **Branch:** `bugfix/end-screen-layering`, based on `dev` @ `c3f515d1`
- **Date:** 2026-09-16
- **Unity:** 6000.3.9f1 batchmode, detached worktree. The Editor open on the main checkout (PID 55965) was never touched.

## How the scene was edited

Through a one-shot Editor script driven by `-executeMethod`, not by hand-editing YAML. The script
has been deleted; the scene is the deliverable. Six changes to `Assets/_Scenes/Gameplay.unity`:

1. New root `EndScreenCanvas` — Overlay, **sortingOrder 100**, CanvasScaler copied from HUDCanvas.
2. `VictoryPanel` and `DefeatPanel` reparented onto it.
3. A `Backdrop` inside each panel as sibling 0 — opaque, stretched to the panel and pushed 2000px
   past it on every side, so it covers any screen while living and dying with its own panel.
4. Three authored star icons under `VictoryPanel`, wired into `VictoryScreenUI._starIcons`.
5. `DrawGlowingSymbolInstruction` re-anchored from the top edge to the bottom edge.
6. Deleted the root object named `GameObject`, which carried the editor-only `TemplatePreview`.

## Three corrections to the plan

**The plan's star approach would have rendered tofu.** It proposed authoring `★` (U+2605) as
TextMeshProUGUI glyphs in TutorialFont. No font in the project contains that codepoint —
TutorialFont has 63 glyphs, VT323 209, LiberationSans 250, none including 9733 — and TutorialFont's
dynamic atlas can only add glyphs its source TTF has. None of the three TTFs has it either. A
128×128 placeholder star sprite was generated instead and is authored on the icons.

**The defect was gold squares, not missing stars.** `VictoryScreenUI.EnsureRuntimeControls` already
built three icons, but set only `iconImage.color` and never a sprite, and a sprite-less `Image`
draws a filled rectangle. The new test pins the *sprite*, because "three icons exist" was never the
property that mattered.

**The first scrim was a critical regression, caught in review, not by me.** A single opaque scrim
owned by `EndScreenCanvas` is active from scene load and nothing ever toggled it, so it covered the
gameplay HUD and swallowed every tap for the whole level. The `hud-only` render showed a completely
blank screen and I did not look at it before declaring the gate met. Each panel now owns its own
`Backdrop` child, so it exists exactly when its panel does. Both directions are now rendered and
checked: the end screens cover the HUD, and the HUD renders normally when they are closed.

**A canvas alone did not cover the HUD.** The plan assumed layering the canvas was sufficient. Both
panels are centred, not full-screen, so the first render still showed hearts, the wave counter, the
pause button, the instruction, `0/4` and the restoration text. The scrim is what actually satisfies
the gate.

**sortingOrder is 100, not the plan's 200** — `SettingsPanel` builds at 200, so 200 would have tied
with it. The real stack is HUD 0, EndScreen 100, SettingsPanel 200, ChallengeMode 250, modals
300-320, cutscene 8500, pause 8800.

## Visual gate — PASS

The shot harness clears to BROWN, deliberately not the backdrop colour, so "covered" and "rendered
nothing" cannot be confused. Rendered headlessly at four configurations (`QA/screenshots/*-fixed.png`). At 1080×1920 and
1080×2400, Victory and Defeat both show **no** hearts, wave counter, pause button, glyph slots,
instruction or restoration text, and three stars that are visibly stars.

**The first render harness was wrong and said the fix had failed.** It converted every Overlay
canvas to ScreenSpaceCamera at an identical `planeDistance`, which throws sorting away — camera-mode
canvases sort by distance first. The scene was correct; the harness was not. Corrected by mapping
sortingOrder onto planeDistance. Worth recording: a rendering harness can manufacture a convincing
false negative.

## EditMode — PASS

| | Total | Passed | Failed |
|---|---|---|---|
| Baseline @ `c3f515d1` | 1288 | 1281 | 7 |
| This branch | 1288 | 1281 | 7 |

**NEW: none. GONE: none.**

One test did fail first: `VictoryScreenSceneWiringTests.Gameplay_StarDisplayIsUnauthored_WhichIsWhyItIsBuiltAtRuntime`,
which deliberately pinned the scene as unauthored. Its own failure message asked for reconciliation
rather than deletion, so it was rewritten as
`Gameplay_StarDisplayIsAuthored_WithARealSpriteOnEveryIcon` and its doc comment updated.

## Not done, and why

- **`EnvironmentThemeSwapper` renderer wiring (part of WI-8): BLOCKED.** There is no valid target.
  The scene contains exactly two SpriteRenderers — `Background` and `[Base] PlayerShrine` — and the
  latter is already consumed as `_baseZoneRenderer`. `Environment_Grid/Ground` is a Tilemap, and
  `Environment_Deco` has no children. Wiring the shrine would mean double-assigning the base-zone
  renderer. Left for SALIN-206 art.
- **CutsceneCanvas reference resolution (1920×1080 → 1080×1920): NOT DONE.** The plan flags a
  letterboxing risk needing a visual check on a real cutscene, which a static render cannot give.
- **LevelSelect.unity SafeAreaHandler: NOT DONE.** Different scene; deferred to keep this diff to
  one scene.
- **In-Editor play session: NOT RUN.** The instruction-versus-rail gap cannot be verified from a
  static render, because the rail is built at runtime and does not exist in the saved scene. The
  instruction's new anchor is computed from the presenter's own serialized rail values, but the
  constant gap is unverified until someone plays it.
