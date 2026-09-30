---
# Level 9 QA Report
**Date:** 2026-09-28
**Tester:** Claude (computer use, mouse input)
**Design doc used:** No standalone Level 9 design document was found. I used `docs/content/ugnayan-levels-6-10-narrative.md` (Level 9 sentence and reduced-guidance requirement; its intro/focus/context/outro prose is largely marked `TO BE WRITTEN`), `QA/level-8-report.md` and `Assets/ScriptableObjects/Levels/Level8_Config.asset` for the nearest-lower-level extension rule, plus `Assets/ScriptableObjects/Levels/Level9_Config.asset`, `Assets/ScriptableObjects/Challenges/Challenge_Ugnayan09_Context.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan09_Intro.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan09_Oo.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan09_Una.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan09_Outro.asset`, `Assets/ScriptableObjects/Campaign/IntroductionSchedule.asset`, `Assets/ScriptableObjects/Characters/Char_OU.asset`, `Assets/ScriptableObjects/Enemies/EnemyData_Uhaw.asset`, and `Assets/ScriptableObjects/Enemies/EnemyData_NawalangMukha.asset`. Assumption: Level 9 follows Level 8's established gameplay rules; the Level 9 asset supplies its title, roster, permitted glyphs, target text, and authored waves. No gameplay behavior was inferred from these assets.

## Spawn Log
| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | — | — | — | No Level 9 spawns were observed because the scene could not be loaded or observed. The config lists Uhaw and Nawalang Mukha in its five authored waves (8, 9, 9, 10, and 12 enemies; 48 total). The permitted set is A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA. The target words are OO and UNA. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|-----------|----------------|-----------|---------------------|
| 1 | QA execution | N/A | Computer Use timed out while reconnecting to Unity by both app name and bundle ID. Unity remained listed as running, but its scene and Play Mode state could not be observed. Level 9 was not opened, so this is a test-session blocker, not a confirmed game defect. | `level09-computer-use-blocked.png` (not saved; Screenshot capture blocked by review) | Confirmed as a test-session blocker; product behavior not verified |

## Needs Human Verification
- Level 9 was not entered; no Level 9 scene baseline was captured. M1–M9 remain NOT VERIFIED: instant-win with enemies alive; combat targeting and slot order; spawn assignments/timestamps and glyph coverage; wrong-glyph miss feedback; base damage/reaction; introduction timing and movement; UI/assets/audio/Console; and narrative/era/level display.
- The permitted glyphs are A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, and YA. No glyph attempts were possible. Once the level opens, attempt each at least three times; if mouse input fails all three attempts, report `NEEDS HUMAN VERIFICATION — mouse input may be the cause`, not a confirmed recognition bug.
- The authored waves contain O/U, E/I, BA, MA, NA, TA, KA, GA, SA, WA, and YA. A is in `allowedCharacters` but absent from the authored wave character lists; verify at runtime whether it ever spawns. This is an asset-based observation only, not a confirmed M5 finding.
- The introduction schedule assigns Uhaw to Level 9, and O/U is introduced at `level.ugnayan.04`. Verify the Uhaw introduction completes before it walks, measure its duration, and confirm it does not appear before the relevant glyphs are learned. No introduction timing was observed.
- The configured enemies are Uhaw (absorbs the last restored character and carries that memory) and Nawalang Mukha (hides written names). No ability behavior or targeting was observed.
- The narrative specifies `Sinabi niyang OO at siya ang naging UNA sa pagtulong.` The challenge has one target slot for OO and one for UNA, with prompts for answering yes and being first to help. The dialogue assets contain intro/focus/outro copy, while the grouped narrative marks much of that copy `TO BE WRITTEN`; verify the runtime text against the available assets and confirm reduced clues do not reveal a full answer before an allowed help condition.
- `level09-gameplay-baseline.png`, the Uhaw introduction, first successful draw/kill, instant-win, miss, base-contact, and Console screenshots were not saved. Unity could not be observed, and Screenshot capture remains blocked by review.
- No scene, prefab, or asset edits were made by this QA attempt. Unity was still listed as running; exiting any prior Play Mode and discarding a save prompt could not be verified.

## Level Verdict
FAIL — QA was blocked before Level 9 opened, so no gameplay verdict can be established; no product defect was confirmed.
---
