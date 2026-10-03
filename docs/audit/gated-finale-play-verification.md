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

---

## Post-fix addendum — 2026-09-17

The whole-branch review found two Critical defects after the play run above, and the fixes changed
which slot is gated. What the original run verified therefore no longer describes the shipped rule,
so this records the post-fix position honestly.

**What changed.** Level 2's gate moved from `ta@MATA` to `ma@MATA`. The old rule gated the last
flattened slot; Level 2's slots are `[ba, ta, ma, ta]`, and `ActiveClueRestorationState.Apply`
restores *every* slot matching a symbol across all words — so restoring TA for BATA filled the gated
slot for free and Level 2 was never gated at all. The rule is now "the last slot whose symbol occurs
exactly once in the level".

**Post-fix play — PARTIAL.** A replay resumed a paused run rather than starting fresh, so the
temporary heart raise did not apply and the run ended in the early waves again. What it does
establish, on the resume path specifically — which is where Critical 2 lived:

| Observation | Count |
|---|---|
| Exceptions / NullReferenceExceptions | **0** |
| Gate-stuck errors ("cannot be completed") | **0** |
| Restoration-overflow give-up warnings | **0** |
| Softlock (run neither ending nor progressing) | **none — the run ended normally** |

**NOT verified post-fix:** that the moved gate (`ma@MATA`) withholds and opens across a full
four-wave Level 2 run. The pre-fix run proved the gate opens and escorts continue, but under the
superseded rule. The new rule is covered by EditMode tests including a shipped-data pin
(`GatedLevel_WithholdsASymbolThatOccursExactlyOnce`) whose negative control was demonstrated —
restoring the old rule fails it for Level 2 while Levels 3 and 4 still pass — but no play session
has exercised it end to end.

Anyone playing Level 2 before merge should confirm: the level does not complete before wave 4, and
the MA carrier only becomes available once wave 4 begins.

**Method note.** `HeartSystem._maxHearts` was again temporarily raised and restored;
`git diff --exit-code` on `Assets/_Scenes/Gameplay.unity` is silent.
