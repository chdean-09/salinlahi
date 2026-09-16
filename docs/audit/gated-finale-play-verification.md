# Play verification — gated finale, Level 2

- **Date:** 2026-09-16
- **Branch:** `integration/ugat-qa-phase1` @ `fb812e13` (merge of `feature/gated-finale-levels-2-4`)
- **Method:** played in the live Unity Editor; evidence read from `Editor.log`, which records the
  gate resolution and every spawn, rather than from screenshots alone.

## Verified

| Gate | Evidence | Verdict |
|---|---|---|
| The finale gate opens on the last wave | `SpawnAssignmentCoordinator: gate 'final_wave_reached' resolved.` — logged exactly **once** | **PASS** |
| Levels 2-4 run the four-wave curve | **19 spawn calls**, exactly `2+4+6+7`, the computed `Curve_Ugat_Short` counts. HUD showed "Wave 4". | **PASS** |
| Escorts keep coming until resolved | **25 spawns against a 19-enemy wave budget — 6 beyond it**, with **0** "restoration overflow ran … without finishing" warnings | **PASS** |
| The level is still loseable | Hearts drained normally (`Hearts remaining: 18/40`); an earlier 3-heart run reached `0/3` and `GameState -> GameOver` | **PASS** |
| No runtime damage | **0** exceptions, **0** NullReferenceExceptions across both runs | **PASS** |
| Focus preview shows no English | Level 2's card read `BATA / ba · ta` and `MATA / ma · ta` | **PASS** |
| Instruction clear of the slot rail | Visible gap above the four slots on Level 2 | **PASS** |

## NOT verified, and why

**"The level cannot end before wave 4" is unproven.** Ending early requires *restoring three
syllables quickly*, which requires drawing Baybayin glyphs accurately enough for the recognizer.
Driving the mouse, I could not. The first run died in **wave 0** with nothing restored.

This is the one gate a non-player cannot reach: with zero syllables restored, a level cannot end
early for trivial reasons, so the run proves nothing about the gate's *withholding*. What was proven
is that the gate opens at the right moment — the other half of the mechanic.

It needs someone who can play the level. The cheaper alternative is a PlayMode test that drives the
coordinator's restoration state directly and asserts `IsTargetTextRestored` stays false while the
gated slot is closed; that does not need drawing skill and would close this properly.

## Method note — a temporary change, reverted

The first run died in wave 0, so the gate was never reached. To get the run as far as the final
wave, `HeartSystem._maxHearts` in `Gameplay.unity` was temporarily raised from **3 to 40**, the
level replayed, and the scene then restored from a backup. `git diff --exit-code` on
`Assets/_Scenes/Gameplay.unity` is silent — byte-identical to its committed state, no residue.

Recording it because the numbers above (`18/40`) only make sense with it, and because an
undisclosed test-only edit is indistinguishable from an accident.

## Incidental observation

Replaying Level 2 played its symbol introductions again ("Symbol 1 of 2 — ba", "Symbol 2 of 2 —
ta"). Whether that is the campaign-wide claim being reset by the Editor session or genuine per-run
behaviour was **not** established here, and it bears directly on the separate introductions change
(Section 4 of `docs/design/gated-finale-levels-2-4.md`). Worth settling before that work starts.
