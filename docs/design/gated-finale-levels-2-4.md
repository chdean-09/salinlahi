# Gated finale for Levels 2-4 — design

- **Date:** 2026-09-16
- **Status:** awaiting review
- **Applies to:** Levels 2, 3, 4 (combat-restoration, no special structure)

## Problem, restated

The request began "Level 2 has only very few waves; let's borrow enemies from Level 1". Two parts
of that premise do not hold, and the real problem is a third thing:

- **Level 2 already has Level 1's enemies.** Its roster is Iligaw, Nawalang Mukha, Abo ng Simula and
  Mantsa — every Era 1 enemy Level 1 carries — plus Bakod and Takip. The one Level 1 entry Level 2
  does not carry is Hati, which is a Pamana-era enemy whose presence in Level 1's roster is itself
  questionable (it is the enemy Level 1's heart-loss demo uses, carrying a symbol Level 1 never
  teaches). There is nothing worth importing.
- **Wave count is not what makes it short.** Level 2 already resolves five waves from `Curve_Ugat`.
- **The win rule is what makes it short.** `WaveManager.Update` calls `BeginInstantWin()` the moment
  `IsTargetTextRestored()` goes true. BATA/MATA is four slots (ba·ta, ma·ta), so a competent player
  finishes in wave 1-2 however many waves exist. Reducing the count to four would make the level
  *shorter*, not longer.

The mechanic actually wanted: **hold one syllable back so the level cannot end before its final
wave**, and keep pressure on until that syllable is resolved.

## Decisions taken

| # | Decision | Chosen |
|---|---|---|
| 1 | How the finale works | Gate the last syllable so it cannot be restored before the final wave |
| 2 | Scope | Levels 2-4 (Level 1 is tutorial-paced; Level 5's segments await the D3 ruling) |
| 3 | Introduction cadence | Replay every run, for both symbol and enemy |
| 4 | Respawn behaviour | Escort waves — pressure keeps building until resolved |
| 5 | Wave count | Four waves across Levels 2-4 |

## What already exists

Almost all of this is shipped machinery; the new code is small.

- **Slot gating.** `SpawnSlotGate` (SpawnAssignmentPolicy.cs:8) binds a flattened slot to a token
  that must open before the slot is fillable. `SpawnSlot.GateToken` carries it
  (SpawnAssignmentTypes.cs:19-33), `SpawnAssignmentDirector.IsGateOpen` (:611) enforces it, and
  `SpawnAssignmentCoordinator.OpenGate(token)` (:202) opens one. **Level 1 already uses this**, gating
  the MA of AMA behind `SpawnGateRegistry.AboAshShown`.
- **Respawn with escorts.** `WaveManager.RunRestorationOverflow` (:726) already runs after the last
  wave, spawning batches of `OverflowBatchSize = 3` while `coordinator.WantsOverflow` — i.e. while
  the target text is incomplete. `allowFinalWaveOverflow` is already `1` on all five levels.

## Section 1 — the gated finale

**The slot is derived, never authored.** `slotGates` entries carry a literal `slotIndex`. Authoring
`slotIndex: 3` on three level assets couples the gate to content position: re-author MATA as MATAAS
and the gate silently lands mid-word. This codebase has been bitten by that class of drift before
(the derived `cumulativeSymbolPool` that must be edited in two places; Level 5's narrowed wave roster
that no test caught). So the mechanism is reused, the index is not authored.

1. **New token.** `SpawnGateRegistry.FinalWaveReached = "final_wave_reached"`.
2. **New opt-in.** One bool on `SpawnAssignmentPolicy`, `gateFinalSlotToFinalWave`, default `false`.
   It lives on the policy — which already owns `slotGates`, `slotFloors` and
   `allowFinalWaveOverflow` — so spawn concerns stay in the spawn policy rather than leaking into
   `LevelConfigSO`. Set `true` on Levels 2, 3, 4.
3. **Derive the slot.** At the end of `SpawnAssignmentCoordinator.BuildSlots` (:173-199), when the
   policy opts in and `_slots.Count > 1`, replace the target entry with an identical `SpawnSlot`
   carrying `FinalWaveReached`. `GateToken` is readonly, so this is a replace rather than a mutate.
   An authored gate on the target always wins, so Level 1's hand-authoring is untouched by
   construction.

   **The target is the last slot whose symbol occurs EXACTLY ONCE in the flattened list, not simply
   the last slot** (`DerivedFinaleGate.LastUniquelyOccurringIndex`). Restoration is by SYMBOL:
   `ActiveCluePresenter.ActiveClueRestorationState.Apply` fills *every* slot matching a defeated
   carrier's symbol across *every* focus word, so a gate on a repeated symbol withholds nothing.
   Level 2 was exactly that shape — BATA + MATA flatten to `[ba, ta, ma, ta]`, the last slot is
   `ta@MATA`, and any TA carrier taken for BATA's slot 1 filled it for free, so the level this
   feature was built for completed in wave 2 with the gate nominally on. It now gates `ma@MATA`
   (slot 2). Levels 3 (`[ba, ta, ta, ma]`) and 4 (`[i, na, a, ma]`) end on a unique symbol, so
   their gate is unchanged — which is why the defect survived every per-level review.

   A level whose every symbol repeats cannot support a gated finale at all: it is left ungated at
   runtime and reported at author time (see 5).
4. **Open it on the final wave.** In `WaveManager`'s wave loop, immediately before
   `_spawner.SpawnWave` for the last wave in the run, call `OpenFinaleGate()`
   (`AssignmentCoordinator.OpenGate(SpawnGateRegistry.FinalWaveReached)`).

   **And unconditionally immediately before `RunRestorationOverflow`.** Reaching overflow means the
   wave list is exhausted by definition, so the gate must be open by then. The per-wave opening
   alone is not enough: `ResolveResumeWaveIndex` returns `waves.Count` when the player paused or
   quit during the final wave after its last enemy had spawned, the resumed loop body then never
   executes, and the run falls through to overflow with the gate still closed. With Levels 2-4's
   `maxOverflowBatches: 0`, `ShouldContinueOverflow` is permanently true and the director emits
   `HoldForGate` forever: the run becomes unwinnable *and* unloseable. `OpenGate` is idempotent, so
   both openings coexist.
5. **Guard it in the validator.** A level opting in must have at least two slots, at least one
   wave, and at least one symbol that occurs exactly once. A one-slot level that gated its only
   slot would be unwinnable; a level with no uniquely-occurring symbol gates nothing at all. Both
   must fail at author time, not in the player's hands.

## Section 2 — respawn pressure

`MaxOverflowBatches` is a hardcoded `const int = 12` (WaveManager.cs:49), so overflow gives up after
roughly 36 enemies. The requested behaviour is "until the game is won or lost".

Make it a policy field defaulting to the current 12, and treat a non-positive value as unbounded.
Levels 2-4 opt into unbounded. A hard cap stays available for any level that wants one, and the
default keeps every other level on exactly today's behaviour.

Deliberately **not** an unconditional `while(true)`: the loop already exits on `CanContinueRun()` and
on `WantsOverflow`, and the existing warning at :763 is a genuine diagnostic for a starved director.
Unbounded is opt-in, per level, and visible in data.

## Section 3 — four waves

A second curve asset, `Curve_Ugat_Short`, with `waveCount: 4` and the existing ramp fields, which
Levels 2-4 reference in place of `Curve_Ugat`. This is what curve assets are for — shared shape
definitions — so it needs no new field and no per-level override.

Re-pins `UgatWaveCurveMigrationTests.MigratedUgatLevel_ResolvesFiveWavesCarryingItsWholeRoster`,
which currently asserts five waves and `enemyCount [2,4,5,6,7]` for levels 2,3,4,5. Levels 2-4 move
to the four-wave expectation; Level 5 stays on `Curve_Ugat`.

**Recorded disagreement:** five waves reads better with the gate, because the held-back syllable has
more room before the finale. This is a feel judgement and it is the product owner's call; it is one
field to flip either way.

## Section 4 — introductions: the model already exists

**Superseded 2026-09-17. The redesign this section used to specify should NOT be built.**

The original ask was to stop introductions depending on a complicated campaign-wide mechanic and
instead assign, per level, which characters that level introduces. That is already how it works.

`LevelConfigSO.learningRequirements` is authored per level, and
`SymbolLearningCardController.HasPresentableRequirement` (:137-156) presents every entry whose
`kind` is `ContentRequirementKind.Instruction`. That list *is* "the characters this level
introduces", and it is authored across the whole campaign today:

| Level | Introduces (Instruction) |
|---|---|
| `level.ugat.01` | EI, NA, A, MA |
| `level.ugat.02` | BA, TA |
| `level.ugnayan.01` | GA, WA |
| `level.ugnayan.02` | KA, SA |
| `level.ugnayan.03` | YA |
| `level.ugnayan.04` | OU |
| `level.pamana.01` | DA, LA |
| `level.pamana.02` | HA, NGA |
| `level.pamana.05` | PA |

Verified in play: Level 2 presented "Symbol 1 of 2 — ba" and "Symbol 2 of 2 — ta", matching its two
`Instruction` entries exactly.

**It already replays every run.** `SymbolLearningCardController` performs no save lookup, no claim,
and no persistence check of any kind — every "Progress" reference in it is the *"Symbol 1 of 2"*
label. Replaying a level re-presents its introductions. The original ask is already satisfied.

**Two corrections to what this document previously claimed:**

- The campaign-wide, permanent `EnemyIntroductionProgress.TryClaimIntroduction` governs **enemy**
  introductions only. It has nothing to do with symbol introductions.
- This document claimed `SpawnGateRegistry.Level1RosterMet` is raised *because an introduction
  fired*, and that cadence and completion were therefore tangled. That was **wrong**.
  `LevelRoster.TryOpenRosterGate` already derives the gate from roster *state* — whether every
  introducible type has been met — and its own comment says both call sites share that helper "so
  the level-start and post-introduction evaluations can never drift". The coupling described here
  was the historical bug, and it is already fixed.

Building the redesign would have re-implemented a working system and migrated live data —
`MirrorDecoyController` and `SpawnAssignmentCoordinator` both read `cumulativeSymbolPool` — for no
behavioural gain.

### The one real gap: two sources of truth

`BaybayinCharacterSO.firstIntroductionLevelId` records the same fact from the other direction, and
nothing enforces that the two agree. That is a silent-drift hazard: a bootstrap tool rewrites one
and the other goes stale with no symptom.

**It has already drifted.** A sweep of all 18 symbols found 17 agreeing and one not:

> `Char_RA` declares `firstIntroductionLevelId: level.pamana.03`, but **no level anywhere carries an
> `Instruction` requirement for RA**. Level 13 (`level.pamana.03`) has `learningRequirements: []`
> and `focusWords: []` — it teaches nothing — while RA sits in the `cumulativeSymbolPool` of Levels
> 13, 14 and 15. A player can meet RA as a spawnable symbol having never been introduced to it.

This is consistent with the known Ugnayan/Pamana content gap rather than a regression, but it is a
live defect and no test or validator sees it.

**The work, therefore, is a validator rule — not a redesign:**

- Every symbol in the registry is introduced by **exactly one** level, i.e. exactly one level carries
  an `Instruction` requirement naming it.
- A symbol's `firstIntroductionLevelId` names **that** level.
- A symbol appearing in a level's `cumulativeSymbolPool` was introduced by that level or an earlier
  one — the property that actually protects the player from an untaught glyph.

Content severity (Warning in Authoring, Error in Strict), matching the other content rules, because
the RA case is unauthored content rather than a code fault and must not fail an authoring run.

## Testing

- **EditMode.** Coordinator: the derived slot is gated when the policy opts in, ungated when it does
  not, and an authored gate is never overwritten; a level whose last symbol repeats gates the last
  *unique* slot instead, and a level with no unique symbol is left ungated. Validator: a one-slot
  level and a no-unique-symbol level opting in are both rejected. Shipped data: each of Levels 2-4
  gates a symbol that occurs exactly once. Resume: `ResolveResumeWaveIndex` returns `waves.Count`
  for a pause after the final wave's last spawn, `IsFinalWaveIndex` is false there, unbounded
  overflow never self-terminates, and `OpenFinaleGate` releases the slot with no wave having run.
  Migration tests re-pinned to four waves for Levels 2-4.
- **PlayMode.** The gated slot cannot be filled before the final wave; it becomes fillable once the
  final wave starts; overflow keeps spawning while it is unresolved.
- **Negative control.** Point the gate at a level that has not opted in and confirm the finale test
  fails — a gate that never closes is indistinguishable from a passing test otherwise.
- **In-Editor.** Play Level 2 end to end. Batchmode cannot see pacing, and the last two defects in
  this area were both found by playing, not by the suite.

## Out of scope

- Level 1 and Level 5 keep today's behaviour.
- The three dead ability controllers found while investigating — `ShokanCorruptionVeil`,
  `GeneralAura`, `KishaMover` — have no enabling field and no asset. Deleting them is its own change.
- `Kadena` is functioning but first reachable at Level 6; nothing to do in Era 1.

## Open question

Whether `gateFinalSlotToFinalWave` should also require the final wave to actually *contain* a carrier
for the gated symbol. Today the director falls back rather than blocking when a wave's roster cannot
supply a needed symbol, so the gate opening is sufficient — but on a level whose final wave narrowed
its roster, the syllable could open and still not arrive. The `WAVE_ROSTER_NARROWS_RESTORATION`
validator rule added earlier already rejects that shape, so this is believed covered; worth
confirming during implementation.
