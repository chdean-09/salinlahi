---
# Level 6 QA Report
**Date:** 2026-09-27
**Tester:** Claude (computer use, mouse input)
**Design doc used:** No standalone Level 6 design document was found. Per the established rule, extended the nearest lower level's gameplay rules and used `docs/content/ugnayan-levels-6-10-narrative.md` (structure only; no approved story copy), `Assets/ScriptableObjects/Levels/Level6_Config.asset`, `Assets/ScriptableObjects/Challenges/Challenge_Ugnayan06_Context.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan06_Intro.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan06_Awa.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan06_Gawa.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Gapos.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Hati.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Kadena.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Labo.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Ngatngat.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Punit.asset`, and `Assets/ScriptableObjects/Enemies/EnemyData_Salungat.asset`. Mouse references inspected: `Assets/Resources/Templates/A_template_01.txt`, `Assets/Resources/Templates/EI_template_01.txt`, `Assets/Resources/Templates/BA_template_01.txt`, `Assets/Resources/Templates/MA_template_01.txt`, `Assets/Resources/Templates/NA_template_01.txt`, `Assets/Resources/Templates/TA_template_01.txt`, `Assets/Resources/Templates/GA_template_01.txt`, `Assets/Resources/Templates/WA_template_01.txt`, `Assets/Art/UI/GlyphBadges/A.png`, `Assets/Art/UI/GlyphBadges/EI.png`, `Assets/Art/UI/GlyphBadges/BA.png`, `Assets/Art/UI/GlyphBadges/MA.png`, `Assets/Art/UI/GlyphBadges/NA.png`, `Assets/Art/UI/GlyphBadges/TA.png`, `Assets/Art/UI/GlyphBadges/GA.png`, and `Assets/Art/UI/GlyphBadges/WA.png`. QA comparison reports read: `QA/level-4-report.md` and `QA/level-5-report.md`.

## Spawn Log
| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| 1 | Walang-Awa | Not readable at first view; its badge was partly under the phone notch | First enemy-introduction screen; run elapsed time not recorded | First intro card: “Each broken armor layer reveals a different remembered character beneath it.” `level06-walang-awa-intro.png` (not saved). |
| 2 | Gapos | ga | First run, after Walang-Awa card; exact elapsed time not displayed | Intro card said it binds two characters and only the current-word character breaks free. `level06-gapos-intro.png` (not saved). |
| 3 | Walang-Awa | wa | First gameplay lane view; exact elapsed time not displayed | Visible below Gapos after its intro card was dismissed. `level06-first-spawn-notch-clipped.png` (not saved). |
| 4 | Unidentified armored enemy | Not readable | First gameplay lane view; exact elapsed time not displayed | Top spawn and badge partly obscured by the iPhone notch; type and syllable could not be identified. `level06-first-spawn-notch-clipped.png` (not saved). |
| 5 | Gapos | ga | Retry Combat; exact elapsed time not displayed | Gapos introduction card appeared again on a same-level retry. |
| — | Configured pool, not all observed | — | — | `Level6_Config.asset` permits A, E/I, BA, MA, NA, TA, GA, WA. Authored pools list Wave 1: Bakod, Gapos, Mantsa, Abong Simula, Walang-Awa; Wave 2 adds Labo and Hati; Wave 3 adds Kadena, Nawalang Mukha, and Ngatngat; Wave 4 adds Punit, Salungat, and Takip. Only Gapos, Walang-Awa, and one unidentified sprite were observed before the base overrun. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|----------|----------------|-----------|---------------------|
| 1 | M8 UI and assets | MINOR | In the live lane, the topmost enemy's glyph badge was partly hidden behind the iPhone simulator notch, preventing its syllable from being read at first appearance. | `level06-first-spawn-notch-clipped.png` (not saved) | Confirmed |
| 2 | M8 UI and assets | MINOR | A story screen ended mid-line at “trust, car…” at the lower edge. The capture may have occurred while its text was still typing, so persistent clipping is not established. | `level06-story-copy-bottom.png` (not saved) | Inferred |
| 3 | M8 Console | MINOR | Unity Console showed 0 errors, 3 warnings, and 165 log entries: one Account API timeout and two repeated Sprite Tiling warnings. Visible call lines were `UnityEngine.Debug:LogWarning (object)` and `UnityEngine.SpriteRenderer:set_drawMode (UnityEngine.SpriteDrawMode)`. The Console row clipped the end of the Sprite Tiling message; there were no error stack traces. | `level06-console-warnings.png` (not saved) | Confirmed |

## Needs Human Verification
- **NEEDS HUMAN VERIFICATION — mouse input may be the cause.** The available computer-use drag action produces a straight line, so it could not trace the curved Baybayin shapes. One straight-line drag was issued after an extended wait, but the view displayed defeat and no recognition result could be attributed to it. A human should use freehand mouse input to attempt each permitted glyph at least three times: A, E/I, BA, MA, NA, TA, GA, and WA. No glyph is being called broken.
- M1 and M5: the four lower target tiles remained empty; the final-slot instant win, left-to-right progression, full glyph coverage, and all-wave spawn list were not tested. The visible syllables were `ga` and `wa`; the notch-obscured spawn was unreadable.
- M2 and M6: no valid matching or wrong-syllable draw was recognized, so targeting, miss feedback, kill behavior, slot effects, and the documented Gapos ability conflict with the literal M2 rule remain unverified.
- M3: Walang-Awa and Gapos cards appeared fully rendered with “Tap to continue.” The cards stayed until manually dismissed (about 41 seconds for Walang-Awa and at least 71 seconds for Gapos in this tester-controlled hold); these are dwell times, not intrinsic animation durations. The Gapos card repeated on Retry Combat. Verify the intended 2–5 second introduction timing, that each enemy remains stationary until its intro finishes, and that introductions do not recur in later levels.
- M4: exact spawn times were not displayed and the run clock was not captured. Verify every spawn and glyph across all four waves, including whether each syllable falls inside the configured set and whether ability enemies appear only after their glyph lesson.
- M7: the first run ended with “The base was overrun.” On Retry Combat, the display changed from three red hearts to two, then reached zero after an observed 0.8-second idle interval. Individual base contacts and the reaction animation were not captured, so per-enemy damage and effect remain unverified.
- M8: the pre-Play Gameplay scene showed the generic labels “TITLE,” “Description,” “0/4,” and “Restoration text”; the runtime Level 6 card and glyph lesson displayed the expected level and focus words. No magenta materials or blank renderers were seen in brief live views. Audio expectations on individual objects were not audited. Screenshot files were not saved: the previously authorized Screenshot app action remained blocked by computer-use review. Required captures still needed are `level06-gameplay-baseline.png`, `level06-walang-awa-intro.png`, `level06-gapos-intro.png`, `level06-first-draw.png`, `level06-instant-win.png`, `level06-base-overrun-defeat.png`, and the issue screenshots referenced above.
- M9: the live start card correctly displayed “Ugnayan - Level 6: Mula Awa sa Gawa,” and the lesson displayed AWA = a + wa and GAWA = ga + wa. The challenge asset contains “Ang ______ ay malasakit na nadarama para sa kapwa.” and “Sa ______ naipapakita kung tunay ang malasakit.” The grouped narrative document has no approved copy, so runtime story-text agreement and context-cloze display remain NOT VERIFIED.
- Play Mode was exited without a save prompt. Git status remained on `dev` with no scene or asset changes.

## Level Verdict
PASS — No confirmed blocking or major defect was found; multiple gameplay checks remain unverified because freehand glyph input and a completed run were unavailable.
---

## QA Runtime Event Trace — 2026-09-30 01:27:48 +08:00

Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.

```text
2026-09-29T16:47:00.1915880Z	0.001	session	begin level=6 asset=Assets/ScriptableObjects/Levels/Level6_Config.asset
2026-09-29T16:47:02.4607210Z	2.270	qa-entry	level=6; normal selection accepted=True
2026-09-29T16:47:03.1813720Z	2.990	base-hp	hearts=3
2026-09-29T16:47:03.2015340Z	3.011	cutscene	started
2026-09-29T16:47:26.9763330Z	26.785	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-29T16:53:36.6423360Z	396.448	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-29T16:55:05.5900290Z	485.395	qa-control	cutscene advance requested through OnTap callback; result=True
2026-09-29T16:55:05.5932250Z	485.398	cutscene	completed
2026-09-29T16:56:25.2689550Z	565.074	qa-ui-callback	button=HUDCanvas/HUDRoot/HUDLayer/PauseButton; programmatic pointer click; handled=True; hardware input not tested
2026-09-29T17:03:21.7949960Z	981.667	wave-start	index=0
2026-09-29T17:03:23.3333420Z	983.206	spawn	type=Walang-Awa; glyph=WA; world=(0.20,6.80); frame=127527
2026-09-29T17:03:41.5447530Z	1001.417	spawn	type=Gapos; glyph=GA; world=(-1.37,6.80); frame=129747
2026-09-29T17:04:02.6837340Z	1022.556	spawn	type=Walang-Awa; glyph=WA; world=(1.00,6.80); frame=132333
2026-09-29T17:04:06.6379640Z	1026.510	base-hit	announced damage=1
2026-09-29T17:04:06.6385690Z	1026.511	base-damage	applied=1
2026-09-29T17:04:06.6386100Z	1026.511	base-hp	hearts=2
2026-09-29T17:04:11.6151690Z	1031.488	base-hit	announced damage=1
2026-09-29T17:04:11.6152240Z	1031.488	base-damage	applied=1
2026-09-29T17:04:11.6152480Z	1031.488	base-hp	hearts=1
2026-09-29T17:04:16.5838770Z	1036.456	base-hit	announced damage=1
2026-09-29T17:04:16.5839360Z	1036.456	base-damage	applied=1
2026-09-29T17:04:16.5839500Z	1036.456	base-hp	hearts=0
2026-09-29T17:04:16.6081570Z	1036.481	outcome	game-over
2026-09-29T17:04:31.0502480Z	1050.923	base-hp	hearts=3
2026-09-29T17:04:31.1800840Z	1051.053	wave-start	index=0
2026-09-29T17:04:32.6844870Z	1052.557	spawn	type=Walang-Awa; glyph=WA; world=(1.24,6.80); frame=135900
2026-09-29T17:04:35.4987680Z	1055.371	spawn	type=Gapos; glyph=GA; world=(-0.64,6.80); frame=136240
```
