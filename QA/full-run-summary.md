# Manual Unity QA — Full Run Summary

**Date:** 2026-09-28
**Scope:** Levels 1–15, one level at a time
**Source reports:** `QA/level-1-report.md` through `QA/level-15-report.md`

Severity counts below are counts of findings explicitly marked BLOCKING, MAJOR, or MINOR in each level report. `N/A` execution notes are not counted as severity findings. Verdicts are copied from the individual reports: a FAIL may mean the level could not be reached or completed during QA and does not by itself confirm a product defect.

## Level Results

| Level | Verdict | Blocking | Major | Minor |
|---:|---|---:|---:|---:|
| 1 | PASS | 0 | 0 | 3 |
| 2 | PASS | 0 | 0 | 7 |
| 3 | FAIL | 0 | 1 | 1 |
| 4 | FAIL | 1 | 0 | 1 |
| 5 | FAIL | 1 | 0 | 2 |
| 6 | PASS | 0 | 0 | 3 |
| 7 | PASS | 0 | 0 | 3 |
| 8 | FAIL | 0 | 0 | 0 |
| 9 | FAIL | 0 | 0 | 0 |
| 10 | FAIL | 0 | 0 | 0 |
| 11 | FAIL | 0 | 0 | 0 |
| 12 | FAIL | 0 | 0 | 0 |
| 13 | FAIL | 0 | 1 | 0 |
| 14 | FAIL | 0 | 2 | 0 |
| 15 | FAIL | 0 | 3 | 1 |
| **Total** |  | **2** | **7** | **21** |

## Blocking Issues

These two BLOCKING findings concern the ability to execute QA, not confirmed gameplay defects.

1. **Level 4 — Level entry blocked by computer-use input routing.** Play Mode opened to MainMenu. Clicks on Start Journey and Level Select did not change the screen; Simulator clicks returned `windowNotFoundAtPosition`. Level 4 gameplay was never entered. See `QA/level-4-report.md`.
2. **Level 5 — Narrative screen could not be advanced.** Play Mode showed “Tap anywhere to continue.” Space and Return did not advance it, and game-view mouse clicks returned `windowNotFoundAtPosition`. Level 5 gameplay was never entered. See `QA/level-5-report.md`.

## Systemic Patterns

- **QA access and input limited campaign coverage.** No level completed the full requested M1–M9 procedure. Some runs stopped in tutorials or ended before completion; Level 4 and 5 input routing blocked entry; Levels 8–14 could not be opened or reached through the available Unity window; and Level 15 started the Level 1 opening instead. Treat the campaign results as incomplete runtime coverage and repeat the unverified checks with reliable Unity access and touch input. This is a test-environment limitation, not proof of a game defect.
- **Safe-area/notch clipping recurred in Levels 2, 6, and 7.** Enemy glyph badges or their upper edges were partly obscured by the simulated device notch. Review shared safe-area layout for enemy lanes and badges.
- **PillarFill Sprite Tiling warnings recurred in Levels 1, 2, 3, 5, 6, 7, and 15.** The Console message says the Sprite used for tiling was not generated with Full Rect; the recorded stack points to `PillarFill`. This is a repeated Editor/asset configuration warning. Reports that inspected the Console recorded zero errors.
- **Unity AI Toolkit Account API timeout warnings recurred across multiple sessions.** They were reported in Levels 1–6 and 15, often alongside Editor startup. These are Editor/tooling warnings and were not established as gameplay failures.
- **Late-campaign authored content and configuration diverge from current rulings in Levels 13–15.** Level 13's DA/RA teaching and sentence source conflict with the approved shared-glyph content; Level 14's permitted RA is absent from wave whitelists and its wired challenge asset differs from current authoring source; Level 15's boss phase roster, glyph coverage, and target order conflict with current rulings. Some runtime effects remain unverified because those levels were not reached.
- **Approved narrative copy is incomplete across the Ugnayan and Pamana groups (Levels 6–15).** Their grouped narrative documents mark substantial per-level copy as `TO BE WRITTEN`, limiting verification of in-game narrative against approved text.

## Blocking Findings Across the Run

There are no confirmed blocking gameplay defects in these reports. The two recorded BLOCKING findings are the Level 4 and Level 5 test-entry blocks listed above.

## Recommended Fix Order

1. **Unblock and repeat QA.** Restore reliable Unity/game-view interaction and direct level entry for Levels 4–15; then rerun all M1–M9 checks that could not be observed. Use touch input for glyph-recognition confirmation.
2. **Address repeated campaign and presentation issues.** Correct shared safe-area placement for enemy glyph badges; review the repeated `PillarFill` Full Rect warning; investigate the recurring Unity AI Toolkit Account API timeout as an Editor/tooling issue. Align the Level 13–15 serialized assets and authoring sources with current rulings, and complete approved Ugnayan/Pamana narrative copy.
3. **Resolve per-level major findings.** Level 3: reproduce the rapid base-overrun result on touch and verify spawn timing and damage. Level 13: settle the DA/RA representation and required sentence. Level 14: reconcile RA spawn eligibility and the wired challenge asset. Level 15: reconcile Paglimot phases/guard, glyph coverage, and final target ordering.
4. **Clean up minor findings.** Correct the Level 1 onboarding label discrepancy; improve introduction prompt legibility and safe-area spacing; then review the remaining per-level minor visual and Console findings in the individual reports.

## Coverage Limitations

Many requested screenshots could not be saved because computer-use screenshot or Unity-window access failed; the individual reports identify captures that were unavailable. Gameplay behavior not directly observed is marked unverified or inferred in those reports. No scene, prefab, or asset fixes were made.

## QA Access Blocker Recheck — 2026-09-28

I brought the main Unity Editor and Device Simulator forward and closed the detached Game panel. Play Mode still began at the Level 1 opening from the shared Gameplay scene. Clicking the simulated device failed in the computer-use layer with `Computer Use server error -10005: windowNotFoundAtPosition((400.0,273.0))`; a second coordinate probe failed at `(1250.0,783.0)`. Space did not advance the opening screen. Play Mode was exited without saving scene or asset changes. The `Gameplay.unity` EventSystem uses `InputSystemUIInputModule` with Unity's default UI actions; that action asset includes `<Mouse>/leftButton` and `<Mouse>/position` bindings. `CutscenePlayer` advances through its `_tapCatcher.onClick` listener. This evidence does not justify a repository input-code change because the computer-use click did not reach Unity. Level 4 and 5 entry remain unverified until mouse events can be delivered to the Simulator/Game view.

In a follow-up recheck, I opened a dedicated Unity `Game` window from `Window > General > Game`. Coordinate clicks no longer raised `windowNotFoundAtPosition` there and the cursor appeared at the requested position, but clicks and a short press-and-release drag left the `Tap anywhere to continue` screen unchanged. A coordinate probe in Preview likewise moved the cursor without activating its folder row. The main Editor Simulator remains inaccessible to coordinate clicks. No Level 4 or Level 5 gameplay was entered, and no code, scene, prefab, or asset was changed. The session was left at the Level 1 opening prompt at that stage; a later recheck stopped Play Mode as recorded below.

Final access recheck on 2026-09-28: the main Unity Simulator coordinate action returned `noWindowsAvailable`. In the detached Unity Game window, CUA clicks on the toolbar successfully toggled the Stats overlay, while clicks on the game surface and a short press-and-release drag left the Level 1 “Tap anywhere to continue” prompt unchanged. This confirms the CUA action can click Unity's Editor UI but does not establish that a pointer event reaches the game's runtime UI. `Cmd+P` returned the Game view to the generic editor preview, indicating Play Mode was stopped. The Screenshot utility timed out, so the recheck has no saved capture. The Salinlahi Dev `Click Named Button` helper requires an external `click-name.txt` input file; it was absent and remains unused because the QA file whitelist excludes it. Level 4 and Level 5 remain unentered. No scene, prefab, asset, or source change was made; the access blocker remains unresolved pending a working runtime input path or a human mouse interaction.

In the next recheck, I opened the main Editor's Device Simulator from `Window > Panels > 6 Simulator`. Its toolbar showed `Play Unfocused`. Keyboard input changed the Simulator scale from 24 to 25, and `Cmd+P` stopped Play Mode; CUA coordinate clicks on both the Simulator and detached Game view continued to fail with `windowNotFoundAtPosition`. `Return` did not advance the Level 1 opening prompt. Launching the authorized macOS Screenshot utility timed out, so no screenshot was saved. Unity is now out of Play Mode. Level 4 and Level 5 remain unentered. No project source, scene, prefab, or asset was changed. A working runtime mouse event is still required before QA can proceed.

After the user confirmed completing the suggested access steps, I reconnected to Unity and opened the detached Game view through `Window > General > Game`. Its toolbar still showed `Play Unfocused`. `Cmd+P` entered Play Mode and displayed the Level 1 opening prompt. A CUA click on the prompt failed with `windowNotFoundAtPosition((725.0,193.5))`; `Cmd+P` exited Play Mode and returned the Game view to the generic editor preview. The authorized Screenshot app still timed out to launch, so no capture was saved. Level 4 remains unentered; this is a continued test-session access block, not a confirmed gameplay defect.

After the user selected `Play Focused`, the detached Game view showed the Level 1 opening prompt in Play Mode. CUA clicks on the prompt and game background returned without a routing error, and the pointer appeared at both targets, but neither advanced the opening. I stopped Play Mode with `Cmd+P`; the Game view returned to the generic editor preview. Level 4 remains unentered; the opening response under CUA input is unresolved and no Level 4 behavior was tested. No capture was saved.
