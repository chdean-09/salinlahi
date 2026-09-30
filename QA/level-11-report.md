---
# Level 11 QA Report
**Date:** 2026-09-28
**Tester:** Claude (computer use, mouse input)
**Design doc used:** No standalone Level 11 design document was found. I used `docs/content/pamana-levels-11-15-narrative.md` (Level 11 focus words and teaching note; intro, focus, context, memory, and outro copy are marked `TO BE WRITTEN`), `QA/level-10-report.md` and `Assets/ScriptableObjects/Levels/Level10_Config.asset` for the nearest-lower-level extension rule, plus `Assets/ScriptableObjects/Levels/Level11_Config.asset`, `Assets/ScriptableObjects/Challenges/Challenge_Pamana11_Context.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana11_Intro.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana11_Dala.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana11_Dama.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana11_Outro.asset`, `Assets/ScriptableObjects/Campaign/IntroductionSchedule.asset`, and the configured enemy assets `EnemyData_Daan-Lihis.asset`, `EnemyData_Labo.asset`, and `EnemyData_Mantsa.asset`. Assumption: Level 11 follows Level 10's established gameplay rules; the Level 11 asset supplies its title, roster, permitted glyphs, target words, and authored waves. The grouped narrative has no approved Level 11 context prompts, so the configured prompts are not treated as approved narrative copy.

## Spawn Log
| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | — | — | — | No Level 11 spawns were observed. The replay reached the MainMenu scene in Play Mode, but the Level Select UI could not be activated. The Level 11 config lists Daan-Lihis, Labo, and Mantsa in four authored waves (4, 5, 6, and 7 enemies; 22 total). The permitted set is A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA, DA, LA. The target words are DALA and DAMA. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|-----------|----------------|-----------|---------------------|
| 1 | QA execution | N/A | Replay was blocked before Level 11 opened. Unity reached the MainMenu scene in Play Mode and showed the main menu in the iPhone 12 Pro Max Simulator. Mouse coordinates over the detached Game view only moved the pointer; clicks did not open Level Select. In the main Editor window, mouse clicks and a zero-distance drag returned `Computer Use server error -10005: noWindowsAvailable`. This confirms an input/tooling blocker, not a broken game button or Level 11 defect. I stopped Play Mode with Cmd+P; Unity returned to Bootstrap in Edit Mode with no save prompt. No scene, prefab, or asset changes were made. | `level11-replay-blocked.png` (not saved; Screenshot app binding timed out; Unity screenshot was visible inline) | Confirmed as a test-session blocker; product behavior not verified |

## Needs Human Verification
- Level 11 was not entered, so no Level 11 scene baseline was captured. M1–M9 remain NOT VERIFIED: instant-win with enemies alive; combat targeting and slot order; spawn assignments/timestamps and glyph coverage; wrong-glyph miss feedback; base damage/reaction; introduction timing and movement; UI/assets/audio/Console; target text; and era/level display.
- No glyph draw attempts were possible. The permitted glyphs are A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA, DA, and LA. Once Level 11 opens, attempt each at least three times; if mouse input fails all three attempts, report `NEEDS HUMAN VERIFICATION — mouse input may be the cause`, not a confirmed recognition bug.
- The introduction schedule assigns Daan-Lihis to Level 11. Verify its introduction finishes before it walks, measure its duration, and confirm the character-path ability does not appear before the relevant glyphs are learned. No introduction timing was observed. Labo and Mantsa are also configured; their runtime behavior and any repeated introductions remain unverified.
- The challenge has two one-slot units in order: DALA, then DAMA. Its current prompts are `May ______ kang alaala mula sa mga naunang panahon.` and `Hindi sapat na dalhin ang alaala; kailangan din itong ______.` The narrative document says these implementation-time prompts should be replaced when approved context copy exists; verify runtime text against the approved copy when authored.
- The editor status bar displayed: `Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.` The Console was not inspected, so Console errors/warnings and their stack traces remain NOT VERIFIED.
- The main-menu screenshot was visible inline but could not be saved. `cua.getApp("Screenshot")` timed out; no screenshot file was written. No screenshot of a Level 11 introduction, draw/kill, instant-win, miss, base contact, or Console state exists.

## Level Verdict
FAIL — the Level 11 replay was blocked before gameplay, so no product verdict can be established and no product defect was confirmed.
---
