---
# Level 10 QA Report
**Date:** 2026-09-28
**Tester:** Claude (computer use, mouse input)
**Design doc used:** No standalone Level 10 design document was found. I used `docs/content/ugnayan-levels-6-10-narrative.md` (Level 10 focus words and the canonical-paragraph requirement, whose copy is marked `TO BE WRITTEN`), `QA/level-9-report.md` and `Assets/ScriptableObjects/Levels/Level9_Config.asset` for the nearest-lower-level extension rule, plus `Assets/ScriptableObjects/Levels/Level10_Config.asset`, `Assets/ScriptableObjects/Challenges/Challenge_Ugnayan10_Context.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan10_Intro.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan10_Sana.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan10_Saya.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan10_Outro.asset`, `Assets/ScriptableObjects/Campaign/IntroductionSchedule.asset`, and the configured enemy assets `EnemyData_Gapos.asset`, `EnemyData_Walang-Awa.asset`, `EnemyData_Kadena.asset`, `EnemyData_Salungat.asset`, `EnemyData_NawalangMukha.asset`, `EnemyData_YaposngDilim.asset`, and `EnemyData_Uhaw.asset`. Assumption: Level 10 follows Level 9's established gameplay rules; the Level 10 asset supplies its title, roster, permitted glyphs, target words, and authored waves. No gameplay behavior was inferred from these assets.

## Spawn Log
| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | — | — | — | No Level 10 spawns were observed because Unity could not be opened through Computer Use. The config lists Gapos, Walang-Awa, Kadena, Salungat, Nawalang Mukha, Yapos ng Dilim, and Uhaw across five authored waves (6, 7, 8, 9, and 10 enemies; 40 total). The permitted set is A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|-----------|----------------|-----------|---------------------|
| 1 | QA execution | N/A | A fresh Computer Use session and Unity app binding both timed out before exposing the Unity window. Unity is listed as running, but its scene and Play Mode state could not be observed; Level 10 was not opened. This is a test-session blocker, not a confirmed game defect. | `level10-computer-use-blocked.png` (not saved; Screenshot capture blocked by review) | Confirmed as a test-session blocker; product behavior not verified |

## Needs Human Verification
- Level 10 was not entered and no scene baseline was captured. M1–M9 remain NOT VERIFIED: instant-win with enemies alive; combat targeting and slot order; spawn assignments/timestamps and glyph coverage; wrong-glyph miss feedback; base damage/reaction; UI/assets/audio/Console; target text; and era/level display.
- The permitted glyphs are A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, and YA. The authored waves include all 12 across the run, but runtime glyph appearances were not checked. No glyph attempts were possible. Once the level opens, attempt each at least three times; if mouse input fails all three attempts, report `NEEDS HUMAN VERIFICATION — mouse input may be the cause`, not a confirmed recognition bug.
- The challenge has three one-slot units in order: SANA, SAYA, then SANA again. Verify left-to-right completion, repeated SANA handling, and the instant-win when the third slot fills while an enemy remains alive.
- `IntroductionSchedule.asset` has no Level 10 introduction entry, and the configured enemy types were introduced in earlier levels. Verify no introduction repeats in Level 10; no introduction timing was observed.
- The grouped narrative explicitly leaves the required canonical paragraph `TO BE WRITTEN`, while the challenge asset contains three separate prompts: `Sa bawat pagsubok, nanatili ang ______ na muling mabubuo ang pamayanan.`, `Nang ang awa ay naging gawa at ang lahat ay kumilos, bumalik ang ______.`, and `Ang ______ ay hindi tahimik na paghihintay; ito ang tinig na sabay-sabay nating isinasabuhay.` The approved paragraph and the runtime target content could not be compared.
- `level10-gameplay-baseline.png`, first successful draw/kill, instant-win, miss, base-contact, and Console screenshots were not saved. Unity could not be observed, and Screenshot capture remains blocked by review.
- No scene, prefab, or asset edits were made by this QA attempt. Unity was still listed as running; exiting any prior Play Mode and discarding a save prompt could not be verified.

## Level Verdict
FAIL — QA was blocked before Level 10 opened, so no gameplay verdict can be established; no product defect was confirmed.
---
