---
# Level 8 QA Report
**Date:** 2026-09-28
**Tester:** Claude (computer use, mouse input)
**Design doc used:** No standalone Level 8 design document was found. I used `docs/content/ugnayan-levels-6-10-narrative.md` (Level 8 focus words GANA and KAYA; much Level 8 prose is marked `TO BE WRITTEN`), `QA/level-7-report.md` and `Assets/ScriptableObjects/Levels/Level7_Config.asset` for the nearest-lower-level extension rule, plus `Assets/ScriptableObjects/Levels/Level8_Config.asset`, `Assets/ScriptableObjects/Challenges/Challenge_Ugnayan08_Context.asset`, `Assets/ScriptableObjects/Campaign/IntroductionSchedule.asset`, `Assets/ScriptableObjects/Characters/Char_YA.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Gapos.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_YaposngDilim.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Kadena.asset`, and `Assets/ScriptableObjects/Enemies/EnemyData_NawalangMukha.asset`. Assumption: Level 8 follows Level 7's established gameplay rules; the Level 8 asset supplies its title, roster, permitted glyphs, target text, and authored waves. No gameplay behavior was inferred from these assets.

## Spawn Log
| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | — | — | — | No Level 8 enemy spawns were observed. The level did not open; Unity remained at the Main Menu. The Level 8 config lists Gapos, Yapos ng Dilim, Kadena, and Nawalang Mukha across five authored waves (5, 6, 6, 7, and 8 enemies). The permitted set is A, E/I, BA, MA, NA, TA, KA, GA, SA, WA, YA. The target words are GANA and KAYA. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|-----------|----------------|-----------|---------------------|
| 1 | QA execution | N/A | The shared Gameplay scene was viewed before Play Mode, then Play Mode reached the Main Menu. Level Select was visible in the portrait Game view, but the docked Simulator clicks returned `windowNotFoundAtPosition`; clicks in the detached Game view moved the pointer without changing the menu. On this continuation, CUA timed out while reconnecting to the running Unity app. This prevented loading Level 8 and is an input/access problem, not a confirmed game defect. | `level08-level-select-blocked.png` (not saved; Screenshot capture blocked by review) | Confirmed as a test-session blocker; product behavior not verified |

## Needs Human Verification
- Level 8 gameplay was not entered. M1–M9 checks remain NOT VERIFIED: completion and instant-win with enemies alive; targeting and repeated-slot order; glyph coverage and spawn timestamps; miss feedback; base damage and reaction; narrative text; UI/assets/audio; and level era/number display.
- The permitted glyphs are A, E/I, BA, MA, NA, TA, KA, GA, SA, WA, and YA. No glyph attempts were possible. Once Level 8 opens, attempt each at least three times; do not call any glyph broken without those attempts. If mouse input fails all three attempts, record `NEEDS HUMAN VERIFICATION — mouse input may be the cause`.
- The asset introduces YA at `level.ugnayan.03`. The introduction schedule does not list a new enemy-type introduction for Level 8; runtime timing, movement, and whether any enemy intro appears remain unverified.
- The config roster is Gapos, Yapos ng Dilim, Kadena, and Nawalang Mukha. No ability-enemy timing against learned glyphs or individual spawn assignments/timestamps could be checked at runtime.
- The shared Gameplay edit-mode baseline showed generic placeholder labels and a blurred white image in the earlier view. It was not a Level 8 runtime screen and is not logged as a Level 8 UI defect. `level08-gameplay-baseline.png` was not saved because Screenshot capture remained blocked by review.
- No Level 8 introduction, first kill, instant-win, miss, base-contact, or Console screenshot was captured; those moments were not reached. Any expected screenshot file names are not saved because Screenshot capture remained blocked by review.
- Unity was still listed as running when CUA stopped responding. I could not verify exiting Play Mode or whether Unity displayed a save prompt. No scene, prefab, or asset edits were made by this QA attempt.

## Level Verdict
FAIL — QA was blocked before Level 8 opened, so no gameplay verdict can be established; this is not a confirmed Level 8 gameplay defect.
---
