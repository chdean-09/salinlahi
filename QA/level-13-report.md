---
# Level 13 QA Report
**Date:** 2026-09-28
**Tester:** Claude (computer use, mouse input)
**Design doc used:** No standalone Level 13 design document was found. I used `docs/content/pamana-levels-11-15-narrative.md` for the required Level 13 sentence, focus words, and DA/RA teaching note, `docs/technical/TW-SPK-004-educational-content-matrix.md` for the approved 17-visual-symbol model, and `QA/level-12-report.md` for the nearest-lower-level context. Level 12 could not be played, so no runtime behavior was carried forward; I applied the user's M1–M9 acceptance rules and used `Assets/ScriptableObjects/Levels/Level13_Config.asset` for the configured roster, glyph set, focus words, and waves. I also inspected `Assets/ScriptableObjects/Challenges/Challenge_Pamana13_Context.asset`, `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana13_Intro.asset`, `Dialogue_Pamana13_Sanga.asset`, `Dialogue_Pamana13_Haraya.asset`, `Dialogue_Pamana13_Outro.asset`, `Assets/ScriptableObjects/Cutscenes/Cutscene_Pamana13_Memory.asset`, `Assets/ScriptableObjects/Characters/Char_RA.asset`, `Char_DA.asset`, and the configured enemy assets `EnemyData_Hati.asset`, `EnemyData_Salungat.asset`, `EnemyData_Ngatngat.asset`, `EnemyData_Ragasa.asset`, and `EnemyData_YaposngDilim.asset`. Assumption: Level 13 extends the established campaign rules and has no new enemy-type introduction, as the grouped narrative states; because Level 12 was blocked, that assumption has not been tested at runtime.

## Spawn Log
| # | Enemy Type | Syllable | Timestamp | Notes |
|---|-----------|---------|-----------|-------|
| — | — | — | — | No runtime spawns were observed. The config lists Hati, Salungat, Ngatngat, Ragasa, and Yapos ng Dilim across five authored waves with enemy counts 5, 6, 6, 7, and 8 (32 total). Its configured `allowedCharacters` set is A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA, DA, HA, LA, NGA, and RA. The focus words are SANGA and HARAYA. These are static asset values, not observed runtime spawns. |

## Findings
| # | Category | Severity | What I observed | Screenshot | Confirmed / Inferred |
|---|---------|----------|----------------|-----------|---------------------|
| 1 | QA execution | N/A | Unity remained listed as running, but Computer Use could not connect to its window: `cua.getApp("Unity")` returned `Computer Use server error -10005: timeoutReached`. I could not inspect the current scene, load Level 13, capture a baseline, or enter Play Mode. No Unity crash was confirmed. No scene, prefab, or asset was intentionally edited or saved. | No screenshot file saved; Unity window access timed out. | Confirmed test-session blocker; product behavior not verified |
| 2 | M9 narrative and glyph content | MAJOR | The approved narrative requires the exact sentence `Bawat salinlahi ay isang SANGA na may sariling HARAYA para sa kinabukasan.` and describes DA/RA as one shared visual glyph. That sentence was not found anywhere under `Assets`. The Level 13 config and `Char_RA.asset` represent RA as a separate character, while `Dialogue_Pamana13_Haraya.asset` says RA has its own form, separate from DA. This conflicts with the approved 17-visual-symbol model. The matrix also notes that RA is canonicalized to DA at runtime, so this source mismatch does not establish a recognition failure or prove exactly what the player sees. | No screenshot file saved; the Unity window was unavailable. | Confirmed asset/document text mismatch; runtime effect inferred |

## Needs Human Verification
- Level 13 was not entered. M1–M9 remain NOT VERIFIED at runtime: instant-win with enemies alive; combat targeting and left-to-right slot order; spawn assignments, timestamps, and glyph coverage; wrong-glyph miss feedback; base damage and reaction; introduction timing and movement; UI/assets/audio/Console; target text; and era/level display.
- No glyph drawing was attempted. The config's allowed set includes DA and RA separately. Test every configured glyph with at least three mouse attempts; if a glyph fails all three, report `NEEDS HUMAN VERIFICATION — mouse input may be the cause`. Do not call a recognition failure confirmed without repeated failures across runs.
- The challenge config has one slot for SANGA and one for HARAYA. Its prompts are `Ang bawat bagong ______ ay nakaugnay pa rin sa pinagmulan nitong puno.` and `Sa ating ______, nabubuo ang kinabukasang may puwang sa alaala at pagbabago.` Verify runtime slot order and whether the required verbatim sentence is shown. The exact sentence is absent from the inspected game assets.
- The grouped narrative lists no new enemy type for Level 13. The configured roster includes Hati (splits), Salungat (decoy character), Ngatngat (removes clue syllables), Ragasa (hides its character), and Yapos ng Dilim (seals an ordered character chain). Runtime behavior, repeated introductions, glyph readiness, and movement were not observed.
- No Level 13 baseline, first introduction, successful draw/kill, instant-win, miss, base-contact, or Console screenshot was saved. Console messages and stack traces were not available.
- Play Mode was not entered, so no Play Mode exit or save prompt occurred. The final Unity scene and editor state could not be inspected.

## Level Verdict
FAIL — the Level 13 playtest was blocked before gameplay, and the configured RA teaching conflicts with the approved shared-glyph rule; no runtime defect was confirmed.
---
