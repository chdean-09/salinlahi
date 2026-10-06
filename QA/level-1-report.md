---
# Level 1 QA Report
**Date:** 2026-09-28
**Tester:** Claude (computer use, mouse input)
**Design doc used:** level-01-narrative.md

## Spawn Log
QA Session live asset snapshot before Play Mode: `Level1_Config.asset`; configured enemy types Abo ng Simula, Hati, Iligaw, Mantsa, Nawalang Mukha; permitted glyphs EI, NA, A, MA; target text INA · AMA. Timestamps below are from the 2026-09-28 Play Mode trace and include retries after a game-over state.

| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| 1 | Hati | MA | 14:15:15.451Z (+775.519s) | Trace recorded this same spawn twice with identical world position and frame; actual instance count is unclear. |
| 2 | Hati | MA | 14:15:15.451Z (+775.519s) | Duplicate trace row; world=(-0.01, 6.00), frame=251968. |
| 3 | Abo ng Simula | A | 14:26:53.448Z (+1473.517s) | First logged Abo spawn in this trace. |
| 4 | Nawalang Mukha | NA | 14:29:42.605Z (+1642.674s) | |
| 5 | Abo ng Simula | A | 14:34:20.470Z (+1920.539s) | |
| 6 | Iligaw | EI | 14:34:25.473Z (+1925.542s) | |
| 7 | Iligaw | A | 14:34:25.481Z (+1925.551s) | Logged at world=(-9.19, 0.06); whether it entered the visible field was not confirmed. |
| 8 | Iligaw | EI | 14:37:56.050Z (+2136.120s) | |
| 9 | Iligaw | A | 14:37:56.054Z (+2136.124s) | Logged at world=(-9.19, 0.06); whether it entered the visible field was not confirmed. |
| 10 | Hati | MA | 14:51:42.851Z (+2962.855s) | Trace recorded this same spawn twice with identical world position and frame; actual instance count is unclear. |
| 11 | Hati | MA | 14:51:42.851Z (+2962.855s) | Duplicate trace row; world=(-0.01, 6.00), frame=1010713. |
| 12 | Abo ng Simula | A | 15:01:16.913Z (+3536.912s) | Introduction beat later emitted the 14s on-screen visibility timeout warning (Finding 1). |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|---------|----------------|-----------|---------------------|
| 1 | M3 Enemy Introduction Beats | MAJOR | At 15:01:16.913Z, Abo ng Simula spawned. About 14 seconds later, Unity logged: `[Salinlahi] EnemyIntroductionBeat: 'Abo ng Simula' was still behind the HUD or outside the camera's view after 14s, so the beat halts it where it stands and the lesson may play against an occluded or empty field. Check the wave spawn height against the camera's orthographic size, that the enemy is actually walking, and that the clue panel's rect is where you think it is.` Stack: `UnityEngine.Debug:LogWarning (object)`; `DebugLogger:LogWarning (string) (at Assets/Scripts/Utilities/DebugLogger.cs:15)`; `EnemyIntroductionBeat/<WaitUntilEnemyIsOnScreen>d__61:MoveNext () (at Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/EnemyIntroductionBeat.cs:803)`; `UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr) (at /Users/bokken/build/output/unity/unity/Runtime/Export/Scripting/Coroutines.cs:17)`. The screenshot shows the Abo sprite and lesson panel after the timeout; whether the full beat completes before movement was not verified. | level01-abo-intro-final-pass.png | Confirmed warning; beat completion not verified |
| 2 | M8 UI and Assets — Console | MINOR | Console showed 187 logs, 4 warnings, and 0 errors. Other warnings shown: `[22:02:47] Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.` Stack shown: `UnityEngine.Debug:LogWarning (object)`; `[22:56:10] Ignoring depth surface load action as it is memoryless`; `[22:56:10] Ignoring depth surface store action as it is memoryless` (each displayed with count 2; no stack trace was exposed). The Abo introduction warning and full stack are in Finding 1. The Screenshot utility could not be opened, so no Console-panel image was saved. | level01-abo-intro-final-pass.png (Game context only; Console image unavailable) | Confirmed |

## Needs Human Verification
- M1: No human-drawn final slot fill was possible, so the instant-win condition with an enemy still alive was not verified.
- M2 and M6: The user reported they could not click/draw. Do not treat this as a gameplay recognition failure. A stored `A` fixture passed recognition (score 0.963, threshold 0.450) and the event trace recorded `combat drawing-missed`; a stored invalid `NA` sample failed recognition (score 0.409, threshold 0.450). These are QA fixtures, not manual mouse or touch strokes, and the miss feedback was not captured. A human should attempt EI, NA, A, and MA at least three times each, then verify matching-enemy kills, no-match miss behavior, no slot fill, and no crash.
- M2 and M5: The trace contains EI, NA, A, and MA spawns, all within the permitted set, but the trace spans retries and the level did not complete. Recheck coverage and left-to-right target filling during a completed run; verify multi-enemy targeting when duplicate glyphs are on screen.
- M3: Abo ng Simula, Nawalang Mukha, and Iligaw introduction panels were observed in the broader QA session. The latest Abo beat hit the 14s visibility timeout in Finding 1. Its completion and movement order were not verified because no physical click was available. Hati spawned, but its introduction timing/completion was not captured; Mantsa did not spawn in the recorded trace.
- M4: No out-of-set glyph was logged. Two Iligaw/A records were at world x=-9.19, y=0.06; confirm whether these are intentional off-screen entry points. Hati spawn telemetry contains exact duplicate event rows at identical frames/positions; confirm whether those represent double logging or duplicate instances.
- M7: The event trace recorded three base hits with damage=1 and hearts 3→2→1→0, followed by `game-over`; see `level01-game-over-base-overrun.png`. The per-hit reaction animation was not checked at the exact frame.
- M8: The current Console view showed 0 errors and the warnings listed above. No separate Console screenshot could be saved because the Screenshot utility timed out and the QA capture control saves the Game view only. Audio playback, every expected audio component, placeholder assets, and full layout across the complete level remain unverified. No magenta or blank sprite was observed in saved Game views.
- M9: The live target text and visible slots were INA · AMA and I, NA, A, MA; the title showed Ugat, Level 1, “Ang Unang Tinig.” The previous report's claimed NA/MA onboarding-card mismatch was not reproduced in this pass; the onboarding overview itself was not re-observed, so treat that prior note as not verified. Remaining dialogue and era presentation were not fully rechecked.

## Level Verdict
FAIL — The Abo ng Simula introduction hit a confirmed 14-second visibility timeout and emitted a warning that the lesson may play against an occluded or empty field; completion and most human-drawn gameplay checks remain unverified.
---

## QA Runtime Event Trace — 2026-09-28 19:54:41 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-09-28T11:48:19.6291460Z	41.482	session	begin level=1 asset=Assets/ScriptableObjects/Levels/Level1_Config.asset
2026-09-28T11:48:29.4765140Z	9.751	qa-entry	level=1; normal selection accepted=True
2026-09-28T11:48:30.6102820Z	10.885	base-hp	hearts=3
2026-09-28T11:48:30.6468900Z	10.921	cutscene	started
2026-09-28T11:49:03.2638330Z	43.538	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-28T11:53:59.0744950Z	339.351	qa-control	cutscene advance requested through OnTap callback; result=True
```

## QA Runtime Event Trace — 2026-09-28 21:21:03 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-09-28T13:21:03.8290990Z	35.409	session	begin level=1 asset=Assets/ScriptableObjects/Levels/Level1_Config.asset
```

## QA Runtime Event Trace — 2026-09-28 22:00:42 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-09-28T13:25:48.5190340Z	0.001	session	begin level=1 asset=Assets/ScriptableObjects/Levels/Level1_Config.asset
2026-09-28T13:25:50.7718750Z	2.254	qa-entry	level=1; normal selection accepted=True
2026-09-28T13:25:52.3071650Z	3.789	base-hp	hearts=3
2026-09-28T13:25:52.3423050Z	3.824	cutscene	started
2026-09-28T13:27:47.6310990Z	119.057	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-28T13:29:05.6693110Z	197.095	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-28T13:31:48.9458640Z	360.371	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-28T13:31:49.4721830Z	360.897	cutscene	completed
2026-09-28T13:39:11.5699660Z	802.995	qa-input-snapshot	mouse=Mouse; position=(0,825); leftDown=False; pointerOverUI=False; raycasters=6; hits=DialogueTapCatcher
2026-09-28T13:41:33.2313680Z	944.656	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T13:43:41.5910520Z	1073.020	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T13:44:39.3931510Z	1130.825	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T13:47:31.0762110Z	1302.508	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T13:52:05.5063830Z	1576.938	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] LevelReadyOverlay/[Runtime] LevelReadyPanel/StartButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T13:53:47.2328120Z	1678.664	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] FocusWordPreviewOverlay/[Runtime] FocusWordPreview/[Runtime] FocusWordContinue; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T13:55:46.3056340Z	1797.737	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] SymbolLearningCardOverlay/[Runtime] SymbolLearningCard/[Runtime] SymbolLearningReplay; programmatic pointer click; handled=True; hardware input not tested
```

## QA Runtime Event Trace — 2026-09-28 23:06:32 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-09-28T14:02:19.9336950Z	0.000	session	begin level=1 asset=Assets/ScriptableObjects/Levels/Level1_Config.asset
2026-09-28T14:02:21.5245610Z	1.591	qa-entry	level=1; normal selection accepted=True
2026-09-28T14:02:22.1268700Z	2.194	base-hp	hearts=3
2026-09-28T14:02:22.1478740Z	2.215	cutscene	started
2026-09-28T14:03:09.6140530Z	49.681	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-28T14:03:24.1262660Z	64.193	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-28T14:04:05.4081790Z	105.475	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-28T14:04:05.9424770Z	106.009	cutscene	completed
2026-09-28T14:09:31.0936820Z	431.161	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:10:04.4312240Z	464.498	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:10:44.1574270Z	504.225	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:11:47.4567630Z	567.524	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:12:21.1391860Z	601.207	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] LevelReadyOverlay/[Runtime] LevelReadyPanel/StartButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:12:50.8309010Z	630.898	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] FocusWordPreviewOverlay/[Runtime] FocusWordPreview/[Runtime] FocusWordContinue; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:13:15.1334950Z	655.201	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] SymbolLearningCardOverlay/[Runtime] SymbolLearningCard/[Runtime] SymbolLearningContinue; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:13:39.5410460Z	679.609	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] SymbolLearningCardOverlay/[Runtime] SymbolLearningCard/[Runtime] SymbolLearningContinue; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:13:57.6489300Z	697.716	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] SymbolLearningCardOverlay/[Runtime] SymbolLearningCard/[Runtime] SymbolLearningContinue; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:14:16.3472160Z	716.415	qa-ui-callback	button=TutorialSpotlightOverlay/[Runtime] SymbolLearningCardOverlay/[Runtime] SymbolLearningCard/[Runtime] SymbolLearningContinue; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:14:51.6490200Z	751.717	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:15:15.1115750Z	775.179	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:15:15.4513400Z	775.519	spawn	type=Hati; glyph=MA; world=(-0.01,6.00); frame=251968
2026-09-28T14:15:15.4514290Z	775.519	spawn	type=Hati; glyph=MA; world=(-0.01,6.00); frame=251968
2026-09-28T14:17:34.1524190Z	914.220	recognition	glyph=NA; score=0.409; threshold=0.450; passed=False
2026-09-28T14:17:34.1547430Z	914.223	recognition	drawing-failed
2026-09-28T14:17:34.1555110Z	914.223	qa-control	submitted deliberate invalid stroke sample
2026-09-28T14:23:19.9358910Z	1260.004	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:23:43.6781870Z	1283.746	qa-ui-callback	button=HUDCanvas/HUDRoot/HUDLayer/PauseButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:24:32.9952300Z	1333.064	qa-ui-callback	button=HUDCanvas/HUDRoot/HUDLayer/PauseButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:26:50.1177190Z	1470.186	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:26:50.4373380Z	1470.506	wave-start	index=0
2026-09-28T14:26:53.4482030Z	1473.517	spawn	type=Abo ng Simula; glyph=A; world=(1.29,5.85); frame=500664
2026-09-28T14:29:40.5983370Z	1640.667	qa-ui-callback	button=HUDCanvas/EnemyDiscoveryOnboarding/MessagePanel/DismissButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:29:42.6050910Z	1642.674	spawn	type=Nawalang Mukha; glyph=NA; world=(-1.61,5.85); frame=559535
2026-09-28T14:34:08.4281570Z	1908.497	qa-ui-callback	button=HUDCanvas/EnemyDiscoveryOnboarding/MessagePanel/DismissButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:34:11.1279380Z	1911.197	base-hit	announced damage=1
2026-09-28T14:34:11.1286210Z	1911.198	base-damage	applied=1
2026-09-28T14:34:11.1286580Z	1911.198	base-hp	hearts=2
2026-09-28T14:34:18.4626550Z	1918.532	base-hit	announced damage=1
2026-09-28T14:34:18.4627300Z	1918.532	base-damage	applied=1
2026-09-28T14:34:18.4627700Z	1918.532	base-hp	hearts=1
2026-09-28T14:34:18.4636210Z	1918.533	wave-clear	index=0
2026-09-28T14:34:18.4637340Z	1918.533	wave-start	index=1
2026-09-28T14:34:20.4703390Z	1920.539	spawn	type=Abo ng Simula; glyph=A; world=(1.73,5.85); frame=655502
2026-09-28T14:34:25.4734690Z	1925.542	spawn	type=Iligaw; glyph=EI; world=(-0.99,5.85); frame=657182
2026-09-28T14:34:25.4815970Z	1925.551	spawn	type=Iligaw; glyph=A; world=(-9.19,0.06); frame=657183
2026-09-28T14:37:55.0582650Z	2135.127	qa-ui-callback	button=HUDCanvas/EnemyDiscoveryOnboarding/MessagePanel/DismissButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:37:56.0502720Z	2136.120	spawn	type=Iligaw; glyph=EI; world=(0.97,5.85); frame=726210
2026-09-28T14:37:56.0545470Z	2136.124	spawn	type=Iligaw; glyph=A; world=(-9.19,0.06); frame=726211
2026-09-28T14:37:58.7985680Z	2138.868	base-hit	announced damage=1
2026-09-28T14:37:58.7986540Z	2138.868	base-damage	applied=1
2026-09-28T14:37:58.7986790Z	2138.868	base-hp	hearts=0
2026-09-28T14:37:58.8154400Z	2138.885	outcome	game-over
2026-09-28T14:42:04.2815940Z	2384.351	qa-ui-callback	button=HUDCanvas/DefeatPanel/RetryButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:42:04.8165300Z	2384.886	base-hp	hearts=3
2026-09-28T14:44:59.0182430Z	2559.088	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:45:35.1860850Z	2595.256	qa-ui-callback	button=HUDCanvas/HUDRoot/HUDLayer/PauseButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:47:28.2166170Z	2708.222	qa-ui-callback	button=HUDCanvas/HUDRoot/HUDLayer/PauseButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:51:42.5458660Z	2962.549	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T14:51:42.8513430Z	2962.855	spawn	type=Hati; glyph=MA; world=(-0.01,6.00); frame=1010713
2026-09-28T14:51:42.8513990Z	2962.855	spawn	type=Hati; glyph=MA; world=(-0.01,6.00); frame=1010713
2026-09-28T14:54:58.2969290Z	3158.299	qa-input-snapshot	mouse=Mouse; position=(0,838); leftDown=False; pointerOverUI=False; raycasters=7; hits=DialogueTapCatcher
2026-09-28T14:56:42.6741830Z	3262.675	recognition	glyph=A; score=0.963; threshold=0.450; passed=True
2026-09-28T14:56:42.6779740Z	3262.679	combat	drawing-missed
2026-09-28T14:56:42.6854810Z	3262.686	qa-control	replayed fixture glyph=A
2026-09-28T15:00:04.0140050Z	3464.013	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T15:01:13.5911940Z	3533.590	qa-ui-callback	button=TutorialCanvas/RuntimeDialogueController/DialogueTapCatcher; programmatic pointer click; handled=True; hardware input not tested
2026-09-28T15:01:13.9027800Z	3533.901	wave-start	index=0
2026-09-28T15:01:16.9134290Z	3536.912	spawn	type=Abo ng Simula; glyph=A; world=(1.60,5.85); frame=1167544
```

## QA Runtime Event Trace — 2026-10-06 09:07:36 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T01:06:56.8231673Z	0.001	session	begin level=1 asset=Assets/ScriptableObjects/Levels/Level1_Config.asset
2026-10-06T01:06:59.2087710Z	2.386	qa-entry	level=1; normal selection accepted=True
2026-10-06T01:07:00.3069631Z	3.485	base-hp	hearts=3
2026-10-06T01:07:00.3491442Z	3.528	cutscene	started
```

## QA Runtime Event Trace — 2026-10-06 09:14:00 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-10-06T01:13:34.0813621Z	0.001	session	begin level=1 asset=Assets/ScriptableObjects/Levels/Level1_Config.asset
2026-10-06T01:13:36.4372391Z	2.356	qa-entry	level=1; normal selection accepted=True
2026-10-06T01:13:37.5357953Z	3.455	base-hp	hearts=3
2026-10-06T01:13:37.5771515Z	3.496	cutscene	started
```
