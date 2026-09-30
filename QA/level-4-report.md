---
# Level 4 QA Report
**Date:** 2026-09-27
**Tester:** Claude (computer use, mouse input)
**Design doc used:** `ugat-levels-2-5-narrative.md` (grouped Levels 2–5; no standalone Level 4 design document)

**Design assumption:** For Level 4 details without a standalone design document, extend the nearest lower level's established rules. Use `Level4_Config.asset` for its actual roster allow-list, permitted glyphs, and target text. The grouped narrative document is the authored source for Level 4 dialogue.

## Spawn Log
**Pre-play scene:** The shared `Gameplay.unity` scene was viewed before Play Mode. Its editor preview showed generic placeholder labels (`TITLE`, `Description`, `0/4`, `Restoration text`) rather than loaded Level 4 content. Baseline view was not saved as `level04-baseline.png` because Screenshot capture was blocked by computer-use review.

**Configured roster and glyphs:** `Level4_Config.asset` has no authored wave list and references the wave curve. Its allowed enemy types are Abo ng Simula, Iligaw, Bakod, Mantsa, Nawalang Mukha, and Takip. Its permitted glyph set is `a`, `e/i`, `ba`, `ma`, `na`, and `ta`. This is the configured allow-list; actual spawns were not observed. The asset target is `Ang INA at AMA ang uNAng guro sa tahaNAn.` with slots `i`, `na`, `a`, `ma`, `na`, `na`.

| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | None observed | — | Not started | Level 4 was not entered; no Level 4 spawn occurred during this run. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|----------|----------|----------------|------------|----------------------|
| 1 | QA execution / M1–M9 coverage | BLOCKING | Play Mode opened to MainMenu. Computer-use clicks on the visible `Start Journey` and `Level Select` controls did not change the game screen. Clicks aimed at the embedded Simulator instead returned `windowNotFoundAtPosition((265.0,575.0))`. I exited Play Mode with the editor shortcut and Unity returned to Bootstrap. Level 4 was never entered, so this is a test-execution block and does not confirm a Level 4 defect.<br><br>**Access recheck — 2026-09-28:** In a detached Unity Game window, a coordinate click no longer raised `windowNotFoundAtPosition`, but the Level 1 opening prompt remained unchanged. A coordinate click in the main Editor Simulator returned `noWindowsAvailable`. The Salinlahi Dev `Click Named Button` helper requires a separate `click-name.txt` input file; that file was absent and I did not create it because the QA file whitelist excludes it. Level 4 remains unentered.<br><br>**Final input-path recheck — 2026-09-28:** A click on Unity's detached Game toolbar toggled the Stats overlay, confirming that CUA clicks reached the Unity window. Clicks on the in-game `Tap anywhere to continue` prompt, a click elsewhere in the game surface, and a short press-and-release drag left the prompt unchanged. I stopped Play Mode with `Cmd+P`; the Game view returned to the generic editor preview. The Screenshot utility timed out, so this recheck has no saved capture. Level 4 remains unentered. <br><br>**Simulator focus-mode check — 2026-09-28:** I opened the main Editor's Device Simulator from `Window > Panels > 6 Simulator`; its toolbar showed `Play Unfocused`. With Play Mode stopped, the Game view showed the generic editor preview (`TITLE`, `Description`, `0/4`, and `Restoration text`). Keyboard input changed Simulator scale from 24 to 25, and `Cmd+P` stopped Play Mode, but coordinate clicks on both the Simulator and Game view returned `windowNotFoundAtPosition`; `Return` did not advance the opening prompt. Reopening the macOS Screenshot utility timed out, so no capture was saved. Level 4 remains unentered and no level-specific behavior was tested.<br><br>**Post-user-update input recheck — 2026-09-28:** After the user confirmed completing the suggested access steps, I reconnected to Unity and opened the detached Game view through `Window > General > Game`. Its toolbar still showed `Play Unfocused`. `Cmd+P` entered Play Mode and displayed the Level 1 opening prompt. A CUA click on that prompt failed with `windowNotFoundAtPosition((725.0,193.5))`; `Cmd+P` exited Play Mode and returned the Game view to the generic editor preview. The authorized Screenshot app still timed out to launch, so no capture was saved. Level 4 remains unentered; this is a continued test-session access block, not a confirmed gameplay defect.<br><br>**Play Focused retry — 2026-09-28:** The Game toolbar now showed `Play Focused`, and the Level 1 opening screen was visible in Play Mode. CUA clicks on the prompt and on the game background returned without a routing error, with the pointer visible at each target, but neither advanced the opening. I stopped Play Mode with `Cmd+P`; the Game view returned to the generic editor preview. Level 4 remains unentered; the behavior of the opening screen under these CUA clicks is unresolved and is not reported as a Level 4 defect. No capture was saved.| `level04-level-select-input.png` (not saved; capture blocked); `level04-access-recheck.png` (not captured; Screenshot destination was inaccessible); `level04-play-unfocused.png` (not captured; Screenshot utility launch timed out); post-user-update recheck (not saved; Screenshot app timed out); Play Focused retry (not saved)| The toolbar response and unchanged game content were observed; the cause inside the Unity input path is not verified. ; The visible focus mode and keyboard responses are confirmed; pointer delivery to runtime remains blocked in CUA.|
| 2 | M8 UI and Assets — Console | MINOR | After exiting Play Mode, Console showed 131 info messages, 1 warning, and 0 errors. The warning was an AI Toolkit Account API timeout while Unity was on MainMenu/Bootstrap, so it is not attributed to Level 4. Full warning and stack trace are recorded below. | `level04-console-warning.png` (not saved; capture blocked) | Confirmed in the Editor Console; not level-specific. |

**Console warning — 21:55:18**

`Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.`

```text
UnityEngine.Debug:LogWarning (object)
Unity.AI.Toolkit.Accounts.Services.States.ApiAccessibleState/<WaitForCloudProjectSettings>d__4:MoveNext () (at ./Library/PackageCache/com.unity.ai.assistant@284c75a8d208/Modules/Unity.AI.Toolkit.Accounts/Services/States/ApiAccessibleState.cs:46)
System.Threading.Tasks.TaskCompletionSource`1<bool>:TrySetResult (bool)
Unity.AI.Toolkit.EditorTask/<>c__DisplayClass22_0:<WaitForCondition>b__0 () (at ./Library/PackageCache/com.unity.ai.assistant@284c75a8d208/Modules/Unity.AI.Toolkit.Async/EditorTask.cs:420)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /Users/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:392)
```

## Needs Human Verification
- **M1/M2/M5/M6 and glyph recognition:** No Level 4 gameplay input was made because the level could not be opened. No glyph received any mouse attempts, so no recognition failure is claimed. With touch or working mouse input, test all six glyphs (`a`, `e/i`, `ba`, `ma`, `na`, `ta`), each at least three times; verify matching-enemy kills, multiple matching targets, wrong-glyph miss feedback, left-to-right slot fills, and immediate win with an enemy alive.
- **M3/M4:** The configured allow-list matches the six types listed for Level 3, so no new enemy type is evident in this asset. Actual spawns, spawn timestamps, glyph coverage, out-of-set glyphs, introduction beats, and introduction durations remain unverified. Inspect each spawn and compare the Level 4 intro to the grouped narrative.
- **M7:** Base damage amount, heart change, and reaction were not tested. Let one enemy reach the base and record the before/after heart count and response.
- **M8/M9:** Level-specific UI, assets, audio, target text rendering, era/level label, and narrative playback were not reached. The grouped document describes Level 4 as a reduced-clue repeat of INA/AMA. Verify that authored introduction and context copy appear as written and that the target reads `Ang INA at AMA ang uNAng guro sa tahaNAn.`
- **Screenshots:** `level04-baseline.png`, `level04-level-intro.png`, `level04-first-draw.png`, `level04-instant-win.png`, `level04-miss.png`, `level04-base-hit.png`, and `level04-console-warning.png` were not saved. The user authorized Screenshot for QA, but computer-use review rejected opening it; no workaround was used. Capture these during a human rerun, plus any issue at the moment it occurs.

## Level Verdict
FAIL — The Level 4 run was blocked before level entry by computer-use input routing; no Level 4 gameplay defect was confirmed.
---
