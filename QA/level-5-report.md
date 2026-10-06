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

## QA Runtime Event Trace — 2026-10-06 01:47:36 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-05T17:47:11.8569627Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-05T17:47:13.8700264Z	2.014	qa-entry	level=5; normal selection accepted=True
2026-10-05T17:47:15.5585150Z	3.703	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 01:50:23 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-05T17:48:23.4497640Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-05T17:48:25.7373176Z	2.289	qa-entry	level=5; normal selection accepted=True
2026-10-05T17:48:26.7657150Z	3.317	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 01:53:17 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-05T17:51:44.4234030Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-05T17:51:46.5693589Z	2.147	qa-entry	level=5; normal selection accepted=True
2026-10-05T17:51:47.7235643Z	3.301	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 09:12:33 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T01:11:36.5063951Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T01:11:39.3943421Z	2.889	qa-entry	level=5; normal selection accepted=True
2026-10-06T01:11:41.0480222Z	4.543	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 09:26:12 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T01:23:42.0898638Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T01:23:44.0223028Z	1.935	qa-entry	level=5; normal selection accepted=True
2026-10-06T01:23:45.0463637Z	2.958	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 09:43:04 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T01:42:06.1342164Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T01:42:09.7690726Z	3.636	qa-entry	level=5; normal selection accepted=True
2026-10-06T01:42:11.1013586Z	4.968	base-hp	hearts=3
2026-10-06T01:42:21.2290632Z	15.095	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 09:44:07 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T01:43:22.1102542Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T01:43:24.1888172Z	2.079	qa-entry	level=5; normal selection accepted=True
2026-10-06T01:43:25.2380302Z	3.128	base-hp	hearts=3
2026-10-06T01:44:06.3820327Z	44.272	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 09:47:33 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T01:45:28.1678195Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T01:45:30.6113336Z	2.445	qa-entry	level=5; normal selection accepted=True
2026-10-06T01:45:31.6497564Z	3.483	base-hp	hearts=3
2026-10-06T01:45:43.2868866Z	15.121	cutscene	started
2026-10-06T01:45:49.5764287Z	21.410	cutscene	started
2026-10-06T01:45:55.3273099Z	27.161	cutscene	started
2026-10-06T01:46:00.9815171Z	32.815	cutscene	started
2026-10-06T01:46:06.6980459Z	38.531	cutscene	started
2026-10-06T01:46:13.0755181Z	44.909	cutscene	started
2026-10-06T01:46:17.4903749Z	49.324	cutscene	started
2026-10-06T01:46:21.9599085Z	53.793	cutscene	started
2026-10-06T01:46:26.3839730Z	58.218	cutscene	started
2026-10-06T01:46:30.7931290Z	62.627	cutscene	started
2026-10-06T01:46:35.2064335Z	67.039	cutscene	started
2026-10-06T01:46:39.6476217Z	71.481	cutscene	started
2026-10-06T01:46:44.1149913Z	75.949	cutscene	started
2026-10-06T01:46:48.5521458Z	80.386	cutscene	started
2026-10-06T01:46:52.3802396Z	84.213	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 10:04:09 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:01:10.6638351Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:01:12.6844575Z	2.022	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:01:13.8920492Z	3.230	base-hp	hearts=3
2026-10-06T02:01:24.7061224Z	14.044	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 10:11:59 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:09:50.0964595Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:09:52.3415093Z	2.247	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:09:53.6746254Z	3.580	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 10:14:13 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:13:03.6962234Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:13:05.7556564Z	2.062	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:13:06.8796807Z	3.185	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 10:16:47 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:15:15.6990750Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:15:17.9808325Z	2.283	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:15:19.1958917Z	3.498	base-hp	hearts=3
```

## QA Runtime Event Trace — 2026-10-06 10:21:19 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:17:53.1089289Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:17:55.5155849Z	2.407	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:17:56.7409909Z	3.633	base-hp	hearts=3
2026-10-06T02:18:34.6346191Z	41.526	cutscene	started
2026-10-06T02:18:35.3269205Z	42.219	cutscene	started
2026-10-06T02:18:36.0250952Z	42.917	cutscene	started
2026-10-06T02:18:36.7128843Z	43.604	cutscene	started
2026-10-06T02:18:37.0905231Z	43.982	cutscene	started
2026-10-06T02:18:37.4618058Z	44.353	cutscene	started
2026-10-06T02:18:53.5033059Z	60.395	cutscene	started
2026-10-06T02:19:05.6209876Z	72.512	cutscene	started
2026-10-06T02:19:15.6848196Z	82.577	cutscene	started
2026-10-06T02:19:27.0148392Z	93.907	cutscene	started
2026-10-06T02:19:40.2667122Z	107.158	cutscene	started
2026-10-06T02:19:40.9618191Z	107.853	cutscene	started
2026-10-06T02:19:41.6504808Z	108.542	cutscene	started
2026-10-06T02:19:42.3421463Z	109.233	cutscene	started
2026-10-06T02:19:42.7232751Z	109.615	cutscene	started
2026-10-06T02:19:53.4755845Z	120.367	cutscene	started
2026-10-06T02:19:53.8568184Z	120.748	cutscene	started
2026-10-06T02:20:03.6582499Z	130.550	cutscene	started
2026-10-06T02:20:04.0297812Z	130.921	cutscene	started
2026-10-06T02:20:13.3335855Z	140.225	cutscene	started
2026-10-06T02:20:13.7242143Z	140.616	cutscene	started
2026-10-06T02:20:22.9900719Z	149.882	cutscene	started
2026-10-06T02:20:23.3856467Z	150.277	cutscene	started
2026-10-06T02:20:35.5216139Z	162.413	cutscene	started
2026-10-06T02:20:36.1981418Z	163.090	cutscene	started
2026-10-06T02:20:36.8822956Z	163.773	cutscene	started
2026-10-06T02:20:37.5753989Z	164.467	cutscene	started
2026-10-06T02:20:37.9542151Z	164.846	cutscene	started
2026-10-06T02:20:48.6180697Z	175.510	cutscene	started
2026-10-06T02:20:48.9963477Z	175.888	cutscene	started
2026-10-06T02:20:58.6962416Z	185.587	cutscene	started
2026-10-06T02:20:59.0769427Z	185.968	cutscene	started
2026-10-06T02:21:08.2543649Z	195.146	cutscene	started
2026-10-06T02:21:08.6374715Z	195.529	cutscene	started
2026-10-06T02:21:16.6406805Z	203.533	cutscene	started
2026-10-06T02:21:17.0180804Z	203.910	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 10:29:04 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:26:51.9945162Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:26:54.2287469Z	2.235	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:26:55.4190921Z	3.425	base-hp	hearts=3
2026-10-06T02:27:19.6968628Z	27.703	cutscene	started
2026-10-06T02:27:22.2186540Z	30.225	cutscene	started
2026-10-06T02:27:24.7503979Z	32.757	cutscene	started
2026-10-06T02:27:27.2542621Z	35.260	cutscene	started
2026-10-06T02:27:29.7592917Z	37.766	cutscene	started
2026-10-06T02:27:33.2223811Z	41.229	cutscene	started
2026-10-06T02:27:35.1092588Z	43.115	cutscene	started
2026-10-06T02:27:37.0054618Z	45.012	cutscene	started
2026-10-06T02:27:38.9167687Z	46.923	cutscene	started
2026-10-06T02:27:40.7914532Z	48.798	cutscene	started
2026-10-06T02:27:43.6255163Z	51.632	cutscene	started
2026-10-06T02:27:45.5064022Z	53.513	cutscene	started
2026-10-06T02:27:47.3992851Z	55.406	cutscene	started
2026-10-06T02:27:49.3034264Z	57.310	cutscene	started
2026-10-06T02:27:51.1748937Z	59.181	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 10:39:05 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:33:14.4130482Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:33:16.8099330Z	2.399	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:33:17.9480351Z	3.537	base-hp	hearts=3
2026-10-06T02:33:51.8345808Z	37.424	cutscene	started
2026-10-06T02:33:52.4648752Z	38.054	cutscene	started
2026-10-06T02:33:53.0942683Z	38.683	cutscene	started
2026-10-06T02:33:53.7147312Z	39.304	cutscene	started
2026-10-06T02:33:54.3394319Z	39.928	cutscene	started
2026-10-06T02:33:54.9668975Z	40.556	cutscene	started
2026-10-06T02:34:17.3420653Z	62.931	cutscene	started
2026-10-06T02:34:33.1516102Z	78.740	cutscene	started
2026-10-06T02:34:46.6334817Z	92.222	cutscene	started
2026-10-06T02:35:00.7513232Z	106.340	cutscene	started
2026-10-06T02:35:21.1387106Z	126.728	cutscene	started
2026-10-06T02:35:21.7711006Z	127.360	cutscene	started
2026-10-06T02:35:22.3922430Z	127.981	cutscene	started
2026-10-06T02:35:23.0227887Z	128.612	cutscene	started
2026-10-06T02:35:23.6507232Z	129.239	cutscene	started
2026-10-06T02:35:40.6287149Z	146.218	cutscene	started
2026-10-06T02:35:41.2623329Z	146.852	cutscene	started
2026-10-06T02:35:56.9306867Z	162.520	cutscene	started
2026-10-06T02:35:57.5554123Z	163.144	cutscene	started
2026-10-06T02:36:12.3593926Z	177.949	cutscene	started
2026-10-06T02:36:12.9833409Z	178.572	cutscene	started
2026-10-06T02:36:27.1222237Z	192.711	cutscene	started
2026-10-06T02:36:27.7508721Z	193.340	cutscene	started
2026-10-06T02:36:45.9744785Z	211.563	cutscene	started
2026-10-06T02:36:46.5994882Z	212.188	cutscene	started
2026-10-06T02:36:47.2246167Z	212.813	cutscene	started
2026-10-06T02:36:47.8552515Z	213.444	cutscene	started
2026-10-06T02:36:48.4762301Z	214.065	cutscene	started
2026-10-06T02:37:05.0896630Z	230.678	cutscene	started
2026-10-06T02:37:05.7262081Z	231.315	cutscene	started
2026-10-06T02:37:21.3303407Z	246.919	cutscene	started
2026-10-06T02:37:21.9572744Z	247.546	cutscene	started
2026-10-06T02:37:35.7473492Z	261.336	cutscene	started
2026-10-06T02:37:36.3813885Z	261.970	cutscene	started
2026-10-06T02:37:47.6570289Z	273.246	cutscene	started
2026-10-06T02:37:48.2814564Z	273.871	cutscene	started
2026-10-06T02:38:58.2773357Z	343.866	cutscene	started
2026-10-06T02:38:58.9087757Z	344.497	cutscene	started
2026-10-06T02:38:59.5297460Z	345.119	cutscene	started
2026-10-06T02:39:00.1606977Z	345.749	cutscene	started
2026-10-06T02:39:00.7915500Z	346.380	cutscene	started
2026-10-06T02:39:01.4216925Z	347.010	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 10:45:08 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T02:42:04.3645797Z	0.001	session	begin level=5 asset=Assets/ScriptableObjects/Levels/Level5_Config.asset
2026-10-06T02:42:06.8406338Z	2.477	qa-entry	level=5; normal selection accepted=True
2026-10-06T02:42:08.0076365Z	3.646	base-hp	hearts=3
2026-10-06T02:42:52.7302740Z	48.367	cutscene	started
2026-10-06T02:42:53.3602807Z	48.997	cutscene	started
2026-10-06T02:42:53.9905147Z	49.627	cutscene	started
2026-10-06T02:42:54.6175379Z	50.255	cutscene	started
2026-10-06T02:42:55.2386602Z	50.875	cutscene	started
2026-10-06T02:42:55.8646633Z	51.501	cutscene	started
2026-10-06T02:43:18.1799552Z	73.817	cutscene	started
2026-10-06T02:43:33.9045965Z	89.541	cutscene	started
2026-10-06T02:43:47.4288354Z	103.065	cutscene	started
2026-10-06T02:44:01.5689430Z	117.206	cutscene	started
2026-10-06T02:44:22.0257292Z	137.662	cutscene	started
2026-10-06T02:44:22.6552613Z	138.292	cutscene	started
2026-10-06T02:44:23.2823531Z	138.919	cutscene	started
2026-10-06T02:44:23.9075271Z	139.545	cutscene	started
2026-10-06T02:44:24.5326662Z	140.170	cutscene	started
2026-10-06T02:44:41.4688170Z	157.106	cutscene	started
2026-10-06T02:44:42.1070492Z	157.744	cutscene	started
2026-10-06T02:44:57.8292651Z	173.466	cutscene	started
2026-10-06T02:44:58.4522774Z	174.089	cutscene	started
```
