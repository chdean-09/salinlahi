---
# Level 12 QA Report
**Date:** 2026-09-28
**Tester:** Claude (computer use, mouse input)
**Design doc used:** No standalone Level 12 design document was found. I used `docs/content/pamana-levels-11-15-narrative.md` for the Level 12 story beat and approved focus words, `QA/level-11-report.md` and `Assets/ScriptableObjects/Levels/Level11_Config.asset` for the nearest-lower-level extension rule, and `Assets/ScriptableObjects/Levels/Level12_Config.asset` for the configured level title, glyph pool, target words, and authored waves. I also inspected `Assets/ScriptableObjects/Challenges/Challenge_Pamana12_Context.asset`, `Assets/ScriptableObjects/Campaign/IntroductionSchedule.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Hati.asset`, `EnemyData_Gapos.asset`, `EnemyData_Labo.asset`, `EnemyData_Ngatngat.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana12_Intro.asset`, `Dialogue_Pamana12_Hanga.asset`, `Dialogue_Pamana12_Halaga.asset`, `Dialogue_Pamana12_Outro.asset`, and `Assets/ScriptableObjects/Cutscenes/Cutscene_Pamana12_Memory.asset`. Assumption: Level 12 follows Level 11's established gameplay rules; the Level 12 asset supplies its actual title, roster, permitted glyph set, target words, and authored wave configuration. The grouped narrative document marks Level 12 dialogue and context copy `TO BE WRITTEN`, so the configured text is not treated as approved narrative copy.

## Spawn Log
| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | — | — | — | No runtime spawns were observed. The config asset lists Hati, Gapos, Labo, and Ngatngat across five authored waves with enemy counts 5, 5, 6, 6, and 7 (29 total). The configured permitted set is A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA, DA, HA, LA, and NGA. The focus words are HANGA and HALAGA. These are static asset values, not observed spawn assignments. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|----------|----------------|-----------|---------------------|
| 1 | QA execution | N/A | Unity was in Bootstrap Edit Mode when the Level 12 attempt began. I opened the scene picker, navigated to `Assets/_Scenes/Gameplay.unity`, and selected it. When I tried to activate the selected scene, Computer Use returned `Computer Use server error -10005: noWindowsAvailable`; later Unity app and screenshot requests returned `Computer Use server error -10005: timeoutReached`. The Unity app remained listed as running, so a crash was not confirmed. I could not verify that Gameplay loaded, capture a Level 12 baseline, or enter Play Mode. No scene, prefab, or asset was intentionally edited or saved. | No screenshot file saved; Computer Use could not capture Unity after the scene-picker attempt. | Confirmed test-session blocker; product behavior not verified |

## Needs Human Verification
- Level 12 was not entered. M1–M9 are NOT VERIFIED: instant-win with enemies alive; combat targeting and left-to-right slot order; runtime spawn assignments, timestamps, and glyph coverage; wrong-glyph miss feedback; base damage and reaction; enemy introduction timing and movement; UI/assets/audio/Console; target text; and era/level display.
- No glyph drawing was attempted. The configured permitted set is A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA, DA, HA, LA, and NGA. On a playable retry, give each glyph at least three mouse attempts; if all fail, report `NEEDS HUMAN VERIFICATION — mouse input may be the cause` rather than a confirmed recognition bug.
- The configured enemy assets describe Hati splitting into two carriers, Gapos binding two characters, Labo hiding its character, and Ngatngat removing clue syllables. No runtime appearance, glyph readiness, ability, introduction, or movement was observed. Introduction duration and repeat behavior remain NOT VERIFIED.
- The approved focus words are HANGA and HALAGA. The challenge asset contains one slot for each word, with prompts `Sa mga nag-ingat ng sulat at alaala, nararapat ang ating ______.` and `Hindi ginto ang sukatan; nasa kuwentong ipinasa ang tunay na ______.` The narrative document leaves Level 12 context and dialogue copy unwritten; runtime text and alignment with approved copy remain NOT VERIFIED.
- No Level 12 scene baseline or gameplay screenshot was saved. The earlier inline Unity view showed Bootstrap with the MainMenu visible in the Simulator; it was not a Level 12 baseline and no image file was created. Console state and error stack traces were not available.
- Play Mode was not entered during this attempt, so there was no Play Mode exit or save prompt to handle. The final Unity editor state could not be inspected after the Computer Use bridge timed out.

## Level Verdict
FAIL — the Level 12 playtest was blocked before gameplay, so no product verdict can be established and no game defect is confirmed.
---
