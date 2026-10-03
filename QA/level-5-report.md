---
# Level 5 QA Report
**Date:** 2026-09-27
**Tester:** Claude (computer use, mouse input)
**Design doc used:** `ugat-levels-2-5-narrative.md` (grouped Levels 2–5; no standalone Level 5 design document)

**Design assumption:** For Level 5 details without a standalone design document, extend the nearest lower level's established rules. Use `Level5_Config.asset` for its actual roster allow-list, permitted glyphs, and target text. The grouped narrative document is the authored source for Level 5 dialogue.

## Spawn Log
**Pre-play scene:** The shared `Gameplay.unity` scene was loaded before Play Mode. Its editor preview showed generic placeholder labels (`TITLE`, `Description`, `0/4`, `Restoration text`) rather than loaded Level 5 content. Baseline view was not saved as `level05-baseline.png` because Screenshot capture was blocked by computer-use review.

**Configured roster and glyphs:** `Level5_Config.asset` has no authored wave list and references the wave curve. Its allowed enemy types are Abo ng Simula, Iligaw, Bakod, Mantsa, Nawalang Mukha, Takip, and Walang-Awa. This is the configured allow-list; actual spawns were not observed. The permitted glyph set is `a`, `e/i`, `ba`, `ma`, `na`, and `ta`. The target paragraph is `Kapag may gana at isip, mas maraming bagay ang kaya, mas maraming pangarap ang naaabot, at mas malayo ang napupuntahan.` with 14 completion slots in order: `ma`, `na`, `i`, `ma`, `ma`, `ba`, `ma`, `ma`, `na`, `a`, `ma`, `ma`, `na`, `ta`.

| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | None observed | — | Not started | Play Mode began in a narrative screen, but Level 5 was not entered; no Level 5 spawn occurred during this run. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|----------|----------|----------------|-----------|----------------------|
| 1 | QA execution / M1–M9 coverage | BLOCKING | Play Mode opened a narrative screen showing “Tap anywhere to continue.” Space and Return did not advance it. Mouse clicks aimed at the Unity game view returned `windowNotFoundAtPosition` errors, so the level flow could not be operated. I exited Play Mode with the editor shortcut; no scene or asset changes were saved. Level 5 gameplay was never entered, so this is a test-execution block and does not confirm a Level 5 defect.<br><br>**Access recheck — 2026-09-28:** In a detached Unity Game window, a coordinate click no longer raised `windowNotFoundAtPosition`, but the Level 1 opening prompt remained unchanged. A coordinate click in the main Editor Simulator returned `noWindowsAvailable`. The Salinlahi Dev `Click Named Button` helper requires a separate `click-name.txt` input file; that file was absent and I did not create it because the QA file whitelist excludes it. Level 5 remains unentered.<br><br>**Final input-path recheck — 2026-09-28:** A click on Unity's detached Game toolbar toggled the Stats overlay, confirming that CUA clicks reached the Unity window. Clicks on the in-game `Tap anywhere to continue` prompt, a click elsewhere in the game surface, and a short press-and-release drag left the prompt unchanged. I stopped Play Mode with `Cmd+P`; the Game view returned to the generic editor preview. The Screenshot utility timed out, so this recheck has no saved capture. Level 5 remains unentered. <br><br>**Simulator focus-mode check — 2026-09-28:** I opened the main Editor's Device Simulator from `Window > Panels > 6 Simulator`; its toolbar showed `Play Unfocused`. With Play Mode stopped, the Game view showed the generic editor preview (`TITLE`, `Description`, `0/4`, and `Restoration text`). Keyboard input changed Simulator scale from 24 to 25, and `Cmd+P` stopped Play Mode, but coordinate clicks on both the Simulator and Game view returned `windowNotFoundAtPosition`; `Return` did not advance the opening prompt. Reopening the macOS Screenshot utility timed out, so no capture was saved. Level 5 remains unentered and no level-specific behavior was tested.| `level05-intro-input-blocked.png` (not saved; capture blocked); `level05-access-recheck.png` (not captured; Screenshot destination was inaccessible); `level05-play-unfocused.png` (not captured; Screenshot utility launch timed out)| The toolbar response and unchanged game content were observed; the cause inside the Unity input path is not verified. ; The visible focus mode and keyboard responses are confirmed; pointer delivery to runtime remains blocked in CUA.|
| 2 | M8 UI and Assets — Console | MINOR | After the Gameplay scene Play attempt, Console showed 133 info messages, 3 warnings, and 0 errors. Two warnings at 22:28:26 repeated the Sprite Tiling message below while `PillarFill` assigned sprites during scene initialization. | `level05-console-sprite-tiling.png` (not saved; capture blocked) | Confirmed in the Editor Console; scene-level warning, not tied to a Level 5 enemy or combat action. |
| 3 | M8 UI and Assets — Console | MINOR | Console also showed one Account API timeout warning at 22:28:53. It is an Editor/AI Toolkit warning and is not attributed to Level 5. | `level05-console-account-api.png` (not saved; capture blocked) | Confirmed in the Editor Console; not level-specific. |

**Sprite Tiling warning — 22:28:26 (two occurrences)**

`Sprite Tiling might not appear correctly because the Sprite used is not generated with Full Rect. To fix this, change the Mesh Type in the Sprite's import setting to Full Rect.`

```text
UnityEngine.SpriteRenderer:set_drawMode (UnityEngine.SpriteDrawMode)
PillarFill:AssignSprite (UnityEngine.Sprite) (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:204)
PillarFill:ApplyInternal (PillarFillMode,UnityEngine.Color,UnityEngine.Sprite) (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:129)
PillarFill:Apply () (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:91)
PillarFill:OnEnable () (at Assets/Scripts/Gameplay/Environment/PillarFill.cs:55)
```

**Account API warning — 22:28:53**

`Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.`

```text
UnityEngine.Debug:LogWarning (object)
Unity.AI.Toolkit.Accounts.Services.States.ApiAccessibleState/<WaitForCloudProjectSettings>d__4:MoveNext () (at ./Library/PackageCache/com.unity.ai.assistant@284c75a8d208/Modules/Unity.AI.Toolkit.Accounts/Services/States/ApiAccessibleState.cs:46)
System.Threading.Tasks.TaskCompletionSource`1<bool>:TrySetResult (bool)
Unity.AI.Toolkit.EditorTask/<>c__DisplayClass22_0:<WaitForCondition>b__0 () (at ./Library/PackageCache/com.unity.ai.assistant@284c75a8d208/Modules/Unity.AI.Toolkit.Async/EditorTask.cs:420)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /Users/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:392)
```

## Needs Human Verification
- **M1/M2/M5/M6 and glyph recognition:** No Level 5 gameplay input was made because the level could not be opened. No glyph received mouse attempts, so no recognition failure is claimed. With working mouse or touch input, test all six glyphs (`a`, `e/i`, `ba`, `ma`, `na`, `ta`), each at least three times; verify matching-enemy kills (including multiple matching enemies), wrong-glyph miss feedback, left-to-right slot fills, and immediate win with an enemy alive.
- **M3/M4:** `Walang-Awa` is the new type in Level 5's configured allow-list; actual spawns, timestamps, glyph coverage, out-of-set glyphs, introduction beat, duration, movement timing, and ability timing were not observed. Verify its authored introduction completes before it walks and that its ability follows glyph learning.
- **M7:** Base damage amount, heart change, and reaction were not tested. Let one enemy reach the base and record the before/after heart count and response.
- **M8/M9:** Level-specific UI, assets, audio, target text rendering, era/level label, and narrative playback were not reached. The authored focus words are IBA and MANA; the intended target paragraph is `Kapag may gana at isip, mas maraming bagay ang kaya, mas maraming pangarap ang naaabot, at mas malayo ang napupuntahan.` Compare the Level 5 introduction and ending to the grouped narrative document.
- **Screenshots:** `level05-baseline.png`, `level05-first-enemy-intro.png`, `level05-first-draw.png`, `level05-instant-win.png`, `level05-miss.png`, `level05-base-hit.png`, `level05-intro-input-blocked.png`, `level05-console-sprite-tiling.png`, and `level05-console-account-api.png` were not saved. The user authorized Screenshot for QA, but computer-use review rejected opening it; no workaround was used. Capture these during a human rerun, plus any issue at the moment it occurs.

## Level Verdict
FAIL — The Level 5 run was blocked before level entry by computer-use input routing; no Level 5 gameplay defect was confirmed.
---
