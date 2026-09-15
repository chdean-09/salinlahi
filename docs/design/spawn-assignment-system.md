# Spawn Assignment System — which syllable each enemy carries

**Status:** implemented. Core logic verified by 23 EditMode tests; Unity-side wiring NOT YET COMPILED (see Implementation below).

**Implementation:**
- [`SpawnAssignmentDirector.cs`](../../Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentDirector.cs) — all decision logic, free of UnityEngine types
- [`SpawnAssignmentPolicy.cs`](../../Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentPolicy.cs) — the parameter table below, serialized on `LevelConfigSO`
- [`SpawnAssignmentTypes.cs`](../../Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentTypes.cs) — slot/assignment/request types
- [`SpawnGateRegistry.cs`](../../Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnGateRegistry.cs) — beat gate tokens
- [`SpawnAssignmentCoordinator.cs`](../../Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentCoordinator.cs) — Unity wiring: slot building, pause-aware clock, symbol→enemy resolution
- [`SpawnAssignmentDirectorTests.cs`](../../Assets/Tests/Editor/Gameplay/SpawnAssignmentDirectorTests.cs) — 23 EditMode tests

**Replaces:** `WaveSpawner.SelectCharacterForSpawn` ([`WaveSpawner.cs:286-325`](../../Assets/Scripts/Gameplay/Wave/WaveSpawner.cs:286)), which draws uniformly with `Random.Range(0, validCharacters.Count)` and biases toward `EnemyDataSO.assignedCharacter`.

---

## The problem, stated precisely

Completing the target text wins the level immediately. So the sequence of syllables carried by spawning enemies *is* the level's length control and its content-delivery schedule. Uniform random assignment gives neither:

- **Level 1 as authored**, 4 slots (`I`, `NA`, `A`, `MA`) drawn uniformly from 4 characters. P(the first four spawns happen to be a permutation of the four slots in fillable order) is small, but P(the level ends inside 6 spawns) is not: with the anti-rush floor absent, roughly one run in five finishes before the third wave, i.e. before Iligaw has appeared at all.
- Conversely a run can go 10+ spawns without ever offering the one needed syllable, which reads as the game being broken rather than hard.

The fix is not "better randomness." It is to make the needed syllable a **scheduled resource** with a floor and a ceiling, and to make everything else deliberate filler.

---

## Grounding facts (verified 2026-09-14)

| Fact | Value | Source |
|---|---|---|
| Level 1 focus words | `INA`, `AMA` | `Level1_Config.asset:23,37` |
| Slots | 4 — `EI`(I), `NA`, `A`, `MA` | `allowedCharacters` |
| Waves | 5 | `Level1_Config.asset:141` |
| `enemyCount` per wave | 2, 4, 5, 6, 7 = **24** | ibid. |
| `spawnInterval` per wave | 6, 5, 4.5, 4, 3.5 (mean ≈ **4.3s**) | ibid. |
| `waveStartDelay` | 3, 2, 2, 2, 2 | ibid. |
| `enemySpeedMultiplier` | 0.6 | `Level1_Config.asset:105` |
| Enemy roster | Iligaw, NawalangMukha, AbongSimula, Mantsa | `enemyTypes` GUIDs |
| Authored spawn window | ≈ 114s + walk-down | computed from the above |

Every character is legal in every one of the five waves, so the wave list imposes **no** content schedule today. The schedule has to come from the assignment director.

---

## 1. PACING — how long is the level, and what ratio delivers it

### The model

Per slot, one **floor** and one **ceiling**:

- **Floor — `minSpawnsBeforeNeeded`.** Until this many enemies have spawned since the current slot became fillable, the needed syllable *cannot* be drawn. This is the sole mechanism preventing the 20-second win. It is a spawn count, not a timer, because the thing being rationed is *opportunities*, not seconds — a player who clears fast should not be punished with dead air.
- **Ceiling — `starvationTimeout`.** Covered in §4.
- Between them, a weighted coin: `neededWeight`.

Expected level length:

```
E[duration] ≈ Σ_slots [ (minSpawnsBeforeNeeded + 1/neededWeight) × spawnInterval ] + fixedOverhead
```

`fixedOverhead` = intro dialogue + onboarding + the last enemy's walk-down + the Iligaw beat. Measure it; assume ≈ 25s for Level 1.

Inverted, to solve for the floor given a target X:

```
minSpawnsBeforeNeeded = (X − fixedOverhead) / (S × meanInterval) − 1/neededWeight
```

### Level 1: choosing X

Take **X = 130s**. That is not an arbitrary pick — it is the level *as already authored*: 114s of spawning plus overhead. The pacing model should reproduce the designer's intent, not overwrite it.

Solving: `(130 − 25) / (4 × 4.3) − 2 = 6.1 − 2 = 4.1` → **`minSpawnsBeforeNeeded = 4`**, `neededWeight = 0.5`.

Sanity check: expected spawns = `4 slots × (4 + 2) = 24`. **The authored `enemyCount` total is exactly 24.** The model lands on the hand-authored budget from the opposite direction, which is the strongest evidence available that the parameters are right.

Resulting ratio: **1 needed : 5 filler**, or 17% needed.

### The consequence you have to accept

If the player misses a needed carrier, 24 spawns is not enough. `enemyCount` must become a **pacing target, not a hard cap**: the final wave loops (re-emitting from its own roster) until the last slot fills. Without that, a missed final-slot enemy deadlocks the level with the spawn budget exhausted. This is a required change to `WaveSpawner`'s wave-completion condition, not an optional one.

---

## 2. FILLER — what a non-needed enemy carries

**Decision: a syllable needed *later* in the target, not next. With a duplicate-of-filled fallback when the later-needed set is too small for variety.**

Filler pool, in priority order:

1. **Later-needed** — unfilled slots after the cursor, excluding gated ones (§3).
2. **Filled-slot duplicates** — added when the later-needed pool has fewer than `minFillerVariety` (= 2) distinct symbols, weighted toward the least-recently-seen symbol.
3. **Off-target pool symbols** — Level 1 has none (the pool *is* the target); see §6.

### Why later-needed over duplicates

- **It is still a real recall rep.** Every filler enemy makes the player read a glyph that is part of the word they are restoring. Duplicates of an already-filled slot teach "this one is done, ignore it" — training the player to *stop* reading a glyph is precisely backwards for a literacy game.
- **It produces §5 for free.** Two on-screen enemies both carrying target syllables, one of which advances — that is the choice moment, and with duplicate-filler it cannot exist, because the HUD already shows which slot is crossed off. A choice you resolve by glancing at the HUD is a lookup, not a choice.
- **It previews the word.** The player meets `A` and `MA` while still working on `I`, so `AMA` is familiar before it is required.

### What it costs

Drawing a *later*-needed glyph must not read as a failure. It needs a distinct "not yet — that one comes later" response, visually separate from a miss. If it reads as a miss, option (b) actively punishes correct recall and should be abandoned for (a). **This feedback state is a hard prerequisite of this choice, not a nice-to-have.**

### Why the fallback is necessary

Worked through Level 1: while slot 2 (`NA`) is the cursor, later-needed = {`A`, `MA`}, but `MA` is gated (§3) — so the later-needed pool collapses to `{A}` and *every* filler for ~20 seconds is `A`. That is worse than duplicates. The `minFillerVariety` fallback restores `{A, EI}`. Rule (2) is load-bearing in Level 1 specifically, not a distant edge case.

---

## 3. GATING — where the Iligaw check lives

**It lives in the construction of the eligible-slot set, before any drawing happens — not inside the weighted draw.**

```
availableSlots = slots.Where(s => !s.Filled && GateSatisfied(s.gateToken))
```

Slot 4 (`MA`) carries `gateToken = "iligaw_beat_resolved"`; `GateSatisfied` queries the beat/onboarding system, which already owns that state (`OnboardingContext` / `OnboardingBeat`).

Three behaviours fall out of that placement, all of which are requirements and none of which survive putting the check in the draw:

1. **A gated symbol is excluded from filler too.** Put the gate in the needed-draw only, and `MA` leaks onto the board as filler before the beat — visible, drawable, and confusing. Gating the slot set gates both roles at once.
2. **A gated slot's timers do not run.** `slotArmedAt` is set when the gate *opens*, not when the cursor arrives. Otherwise a long Iligaw beat banks a starvation debt and dumps `MA` the instant the beat ends — the exact opposite of gating it.
3. **A closed gate cannot be won through.** If every unfilled slot is gated, `availableSlots` is empty, the director returns `HoldForGate`, and filler comes from rule (2). The level is structurally incapable of completing before the beat resolves. That is what "must not appear until resolved" actually demands.

Also pause the starvation clock during dialogue, intermission waves, and the beat itself — a timer that runs while nothing can spawn fires the moment gameplay resumes.

---

## 4. STARVATION — two timers, because there are two failure modes

| Timer | L1 value | Fires when | Effect |
|---|---|---|---|
| `starvationTimeout` | 30s | Needed syllable hasn't spawned since the slot armed | Force the next spawn's character to the needed syllable. Nothing else changes. |
| `hardStarvationTimeout` | 50s | Slot still unfilled after the syllable has been offered | Force needed **and** `neededWeight → 1.0` (suppress filler entirely) **and** escalate the clue channel (`Glyph` → `Glyph \| LatinText`, using the existing `audioVisualFallback` path). |

**Why two.** The soft timer fixes a *supply* problem: the glyph never showed up. The hard timer fixes a *comprehension* problem: it showed up and the player couldn't read it. Identical symptom (slot not filling), opposite remedy. One timeout would keep force-spawning a glyph the player has already demonstrated they cannot recognise, which is the frustrating failure this system exists to prevent.

**After a forced fill**, the next slot's `spawnsSinceSlotArmed` resets to 0 — the floor applies normally, so a forced fill doesn't cascade into a rushed finish.

---

## 5. THE CHOICE MOMENT — guaranteed by a directive, not by probability

`choiceMomentSlotIndex` (L1: **2**, the `NA` slot) marks one slot as the designated discrimination beat.

When the director is about to emit the needed carrier for that slot, it emits a **paired spawn** instead:

- **Enemy A** — the needed syllable for the current slot.
- **Enemy B** — a later-needed syllable, guaranteed distinct, emitted within `choicePairWindow` (1.5s) at a separated X.

Three things make this a guarantee rather than a strong probability:

1. **Budget precondition** — issued only when `activeEnemyCount + 2 ≤ maxConcurrent`. If unavailable, the directive **carries forward** to the next eligible slot; it is never dropped.
2. **Both must be live at emit.** If B fails to spawn, A is held back to the next tick with the directive still pending.
3. **The active clue must stand down for the pair window.** This is the coupling that will silently kill the feature if missed: [`ActiveClueSelector.SelectIndex`](../../Assets/Scripts/Gameplay/Combat/ActiveClueSelector.cs:47) always marks the closest eligible enemy. If it marks A, the game has answered the question for the player and the choice is cosmetic. For the pair window the clue policy must go word-level — mark both, or mark neither.

Iligaw's mirror decoy (the false glyph pinned to its source, `ae374aed`) is a *second* choice moment and a harder one. Keep both: the pair teaches **discrimination** (which of two real glyphs do I need?), the decoy teaches **deception** (which of two identical-looking glyphs is real?). They are different lessons.

---

## 6. SCALING — holds, with parameters plus two structural additions

The core loop — ordered slot list, cursor, floor/ceiling, filler policy — is independent of `S`. Two things must be added for sentence and paragraph targets:

### `activeSlotWindow` (structural)

With `S = 18` a single cursor means 18 sequential windows → a level over 10 minutes, and a restoration that must be completed strictly left-to-right, which is not how reading a sentence works. Later levels set `activeSlotWindow = k`: the next *k* unfilled slots are **all** simultaneously fillable, in any order. Level length becomes sub-linear in `S`, and the working set matches `ChallengeMode.SentenceRestoration` semantics. Level 1 sets it to 1 (strict order).

Filler then means "needed later than the current window," and the §5 pair draws its two members from inside the window — which makes the choice moment *more* natural at scale, not less.

### `offTargetFillerWeight` (structural)

With an 18-symbol `cumulativeSymbolPool` and a 6-slot target, filler can draw from pool-minus-target. Without this, an 18-symbol level is still a 6-symbol shooting gallery. Level 1 sets it to 0 — its pool is exactly its target.

Read that pool from `LevelConfigSO.cumulativeSymbolPool`, which is **derived** from each symbol's first-introduction level — do not hand-author a parallel list; it will drift.

### Per-level parameters

All of the below live on `LevelConfigSO` (or a nested `SpawnAssignmentPolicy` block), not in the director.

| Parameter | Type | L1 value | Meaning |
|---|---|---|---|
| `targetDurationSeconds` | float | 130 | Design intent; drives the floor via the inverted formula. Not read at runtime. |
| `minSpawnsBeforeNeeded` | int | **4** | Anti-rush floor. The single most important pacing knob. |
| `neededWeight` | float | **0.5** | P(needed) once the floor is met. |
| `starvationTimeout` | float | 30 | Soft force. Supply fix. |
| `hardStarvationTimeout` | float | 50 | Hard force + clue escalation. Comprehension fix. |
| `minFillerVariety` | int | 2 | Below this many distinct later-needed symbols, admit filled duplicates. |
| `offTargetFillerWeight` | float | 0 | Share of filler drawn from pool-minus-target. |
| `activeSlotWindow` | int | 1 | How many slots are fillable at once. |
| `choiceMomentSlotIndex` | int | 2 | Slot whose needed carrier spawns as a pair. `-1` = none. |
| `choicePairWindow` | float | 1.5 | Seconds within which both pair members must spawn. |
| `maxConcurrentEnemies` | int | (existing) | Precondition for the pair. |
| `allowFinalWaveOverflow` | bool | true | Final wave re-emits past `enemyCount` until the last slot fills. |
| `assignmentSeed` | int | 0 | Seeded RNG. 0 = time-seeded. Non-zero makes a playtest replayable. |

Levels 2–5 should start by scaling `minSpawnsBeforeNeeded` **down** as `S` rises (more slots × the same floor = a very long level) and `activeSlotWindow` **up**.

---

## Pseudocode

### State (owned by `SpawnAssignmentDirector`, one per level run)

```
slots            : ordered [ { symbolId, filled, gateToken } ]   // from focusWords[].decomposition
cursor           : int                                            // first unfilled index
spawnsSinceArmed : int                                            // resets when the active window changes
armedAt          : float    // pause-aware game time; set when the slot becomes fillable AND ungated
softForced       : bool
hardForced       : bool
choicePending    : bool     // true once the level reaches choiceMomentSlotIndex
recentFiller     : ring buffer<symbolId>   // for least-recently-seen weighting
rng              : seeded
policy           : the parameter table above
```

### Entry point

```
function AssignCharacter(spawnRequest, now) -> Assignment

    # ---- 1. eligible slots. Gating lives HERE, before any draw. ----
    window   = slots.SkipWhile(filled).Where(!filled).Take(policy.activeSlotWindow)
    eligible = window.Where(s => GateSatisfied(s.gateToken))

    if eligible.isEmpty:
        # every remaining slot is gated — the level cannot be completed right now
        return Assignment{ symbol: PickFiller(eligible, spawnRequest), role: HoldForGate }

    ArmTimersIfNewlyEligible(eligible, now)     # armedAt set on gate OPEN, not on cursor arrival

    # ---- 2. ceiling: starvation overrides everything below ----
    elapsed = PausedAwareElapsed(now, armedAt)   # excludes dialogue, intermissions, the Iligaw beat

    if elapsed >= policy.hardStarvationTimeout:
        hardForced = true
        EscalateClueChannel()                    # Glyph -> Glyph | LatinText
        return Needed(eligible, spawnRequest)

    if elapsed >= policy.starvationTimeout and not softForced:
        softForced = true
        return Needed(eligible, spawnRequest)

    # ---- 3. floor: the anti-rush guard ----
    if spawnsSinceArmed < policy.minSpawnsBeforeNeeded:
        spawnsSinceArmed += 1
        return Filler(eligible, spawnRequest)

    # ---- 4. weighted draw ----
    spawnsSinceArmed += 1
    if hardForced or rng.NextFloat() < policy.neededWeight:
        return Needed(eligible, spawnRequest)
    return Filler(eligible, spawnRequest)
```

### Needed — and the choice-moment directive

```
function Needed(eligible, spawnRequest) -> Assignment
    target = eligible.First()        # with activeSlotWindow > 1, the least-recently-offered instead

    if choicePending and cursor == policy.choiceMomentSlotIndex:
        if activeEnemyCount + 2 > policy.maxConcurrentEnemies:
            return Filler(eligible, spawnRequest)      # directive CARRIES FORWARD, never drops

        decoy = PickDistinctLaterNeeded(target)
        if decoy == null:
            return Filler(eligible, spawnRequest)      # carries forward

        choicePending = false
        SuspendActiveClueMarking(policy.choicePairWindow)   # or mark both — never mark only `target`
        EnqueuePairedSpawn(decoy, withinSeconds: policy.choicePairWindow, atSeparatedX: true)
        return Assignment{ symbol: target.symbolId, role: Needed, slot: target.index, paired: true }

    return Assignment{ symbol: target.symbolId, role: Needed, slot: target.index }
```

### Filler

```
function PickFiller(eligible, spawnRequest) -> symbolId
    laterNeeded = slots.Where(!filled)
                       .Skip(activeSlotWindow)
                       .Where(s => GateSatisfied(s.gateToken))     # gated symbols are NOT filler
                       .Select(symbolId).Distinct()

    pool = laterNeeded
    if pool.Count < policy.minFillerVariety:
        pool += slots.Where(filled).Select(symbolId)                # duplicate fallback

    if policy.offTargetFillerWeight > 0 and rng.NextFloat() < policy.offTargetFillerWeight:
        offTarget = levelConfig.cumulativeSymbolPool - slots.Select(symbolId)
        if offTarget.any: pool = offTarget

    if pool.isEmpty:
        pool = slots.Where(filled).Select(symbolId)                 # last resort: any learned symbol

    choice = WeightedLeastRecentlySeen(pool, recentFiller)
    recentFiller.Push(choice)
    return choice
```

### On a slot filling

```
function OnSlotFilled(slotIndex, now)
    slots[slotIndex].filled = true
    cursor = slots.FirstIndexWhere(!filled)
    spawnsSinceArmed = 0            # the floor applies to the new slot, even after a forced fill
    softForced = hardForced = false
    ResetClueChannel()
    ArmTimersIfNewlyEligible(CurrentWindow(), now)
```

### Where it hooks in

`WaveSpawner.SelectCharacterForSpawn` becomes a thin call to `AssignCharacter`. The current `assignedCharacter` preference (`WaveSpawner.cs:304-309`) is **dropped** for active-clue levels — an enemy's authored character cannot be allowed to override the schedule. Keep it as the fallback path for levels where `activeClueCombatEnabled == false`, so the 11 colonial-enemy levels are untouched.

---

## Worked example - one full playthrough of Level 1

**Generated, not hand-computed.** Produced by replaying the real `SpawnAssignmentDirector` against
Level 1's authored wave timings (`waveStartDelay` 3/2/2/2/2, `enemyCount` 2/4/5/6/7,
`spawnInterval` 6/5/4.5/4/3.5) with `assignmentSeed = 20260914`.

Player model: converts a needed carrier 5s after it spawns, with one scripted miss on the first MA
offer. Beat model: the Iligaw mirror-decoy beat resolves 10s after the second Iligaw spawns. Enemies
occupy the field for 10s. `t` is seconds from the first wave's start.

Note the enemy column: because each Level 1 enemy embodies exactly one symbol, choosing the symbol
chooses the enemy. Iligaw is always E/I, Nawalang Mukha always NA, Abo ng Simula always A, Mantsa
always MA.

| # | t | Wave | Enemy | Syl | Role | Slot | Note |
|---|---|---|---|---|---|---|---|
| 1 | 3 | W1 | NawalangMukha | `NA` | Filler | - |  |
| 2 | 9 | W1 | AbongSimula | `A` | Filler | - |  |
| 3 | 17 | W2 | NawalangMukha | `NA` | Filler | - |  |
| 4 | 22 | W2 | AbongSimula | `A` | Filler | - |  |
| 5 | 27 | W2 | Iligaw | `EI` | Needed | 1 |  |
| 6 | 32 | W2 | Iligaw | `EI` | Filler | - |  |
| 7 | 39 | W3 | AbongSimula | `A` | Filler | - |  |
|  | 42 |  |  |  |  |  | **Iligaw beat resolves** - MA ungated |
| 8 | 43.5 | W3 | Mantsa | `MA` | Filler | - |  |
| 9 | 48 | W3 | AbongSimula | `A` | Filler | - |  |
| 10 | 52.5 | W3 | NawalangMukha | `NA` | Needed | 2 |  |
| 11 | 54 | W3 | AbongSimula | `A` | PairDecoy | - | **CHOICE MOMENT** - clue marking suspended |
| 12 | 57 | W3 | NawalangMukha | `NA` | Needed | 2 |  |
| 13 | 63.5 | W4 | NawalangMukha | `NA` | Filler | - |  |
| 14 | 67.5 | W4 | Iligaw | `EI` | Filler | - |  |
| 15 | 71.5 | W4 | Mantsa | `MA` | Filler | - |  |
| 16 | 75.5 | W4 | NawalangMukha | `NA` | Filler | - |  |
| 17 | 79.5 | W4 | AbongSimula | `A` | Needed | 3 |  |
| 18 | 83.5 | W4 | Iligaw | `EI` | Filler | - |  |
| 19 | 89.5 | W5 | AbongSimula | `A` | Filler | - |  |
| 20 | 93 | W5 | NawalangMukha | `NA` | Filler | - |  |
| 21 | 96.5 | W5 | Iligaw | `EI` | Filler | - |  |
| 22 | 100 | W5 | AbongSimula | `A` | Filler | - |  |
| 23 | 103.5 | W5 | NawalangMukha | `NA` | Filler | - |  |
| 24 | 107 | W5 | Mantsa | `MA` | Needed | 4 | **player misses it** |
| 25 | 110.5 | W5 | Iligaw | `EI` | Filler | - |  |
| 26 | 114 | (ovf) | Mantsa | `MA` | Needed | 4 | **OVERFLOW past the 24-enemy budget** |
| 27 | 117.5 | (ovf) | AbongSimula | `A` | Filler | - | **OVERFLOW past the 24-enemy budget** |
Total spawns: 27   Level completes at t = 126s

### What this run demonstrates

- **The floor made an instant win impossible.** The first needed symbol arrives on spawn 5 at
  t=27; the four-draw win the old uniform draw allowed cannot occur.
- **The gate held.** MA appears nowhere - needed or filler - before t=42, when the beat resolves.
  It then becomes legal filler (rows 8 and 15), which is the design's "preview the word" behaviour
  working as intended.
- **The choice moment fired** at t=52.5/54: Nawalang Mukha carrying the needed NA alongside Abo ng
  Simula carrying A, one advancing and one not.
- **A real miss was recovered.** The scripted MA miss at t=107 did not stall the level.
- **Overflow was required by an ordinary run**, confirming that `enemyCount` must become a pacing
  target rather than a hard cap.
- **Level completes at t=126s** against the 130s target.

### Two corrections this exercise forced on the design above

1. **The enemy roster is a bijection, not a free choice.** `EnemyDataSO.assignedCharacter` pins each
   Level 1 enemy to one symbol, and `WaveSpawner` already preferred it so the glyph badge never
   contradicts the body beneath it. The integration seam therefore inverts: the director picks the
   **symbol**, and the enemy type follows. The earlier draft's "drop the assignedCharacter
   preference" was wrong and would have reintroduced the Mantsa-carrying-an-E/I defect.
2. **The choice moment needed a stronger guarantee than "one designated slot".** The first
   simulation run never produced a pair: the concurrency budget was full for both of the designated
   slot's needed spawns, the player filled the slot anyway, and the directive died with it. It now
   arms when the level *reaches* that slot and stays armed until spent. Covered by
   `ChoiceMoment_SurvivesTheDesignatedSlotBeingFilledWhileBlocked`.

## Applying this to all five levels

The defense now runs **until the target words are finished**, not until the wave list is exhausted.

Before this, a run that spent its authored enemy budget with a slot still empty hit
`LevelFlowController.RefuseCompletionForMissingContent` — *"combat ended before restoring focus word
slots"* — and dead-ended. The wave budget ran out, not the player's skill.
`WaveManager.RunRestorationOverflow` now keeps emitting from the final wave's roster and cadence,
through the ordinary `SpawnWave` path, until every slot is restored. The schedule's starvation
timers are what terminate that loop; it is bounded at 12 batches of 3 so a mis-authored gate
degrades into the existing refuse-completion diagnostic instead of spinning forever.

### Per-level tuning

All five authored levels target two two-syllable words, so `S = 4` throughout and one schedule
drives all of them. Only the anti-rush floor changes, and it is **derived from each level's own
enemy budget** rather than chosen by feel:

```
minSpawnsBeforeNeeded = round(budget / S - 1/neededWeight)
```

| Level | Focus words | Budget | Mean interval | Authored + overhead | Floor | Predicted |
|---|---|---|---|---|---|---|
| 1 | INA + AMA | 24 | 4.29s | 128s | **4** | 128s |
| 2 | BATA + MATA | 18 | 3.33s | 85s | **2** | 78s |
| 3 | BATA + TAMA | 22 | 2.98s | 91s | **4** | 96s |
| 4 | INA + AMA | 30 | 3.93s | 143s | **6** | 151s |
| 5 | IBA + MANA | 21 | 3.12s | 91s | **3** | 87s |

Every predicted duration lands within a few seconds of the length that level was already authored
for, which is the same cross-check that validated Level 1: the model arrives at the designers'
numbers from the opposite direction.

Leaving every level on the default floor of 4 would not break anything now that overflow exists, but
it would stretch Level 2 from ~85s to ~105s and compress Level 4. Author the table with
**Salinlahi → Campaign → Author Spawn Assignment Policies**; **Validate Spawn Assignment Policies**
reports any level whose floor has drifted from its budget, which is what happens the moment someone
retunes `enemyCount`. `AllLevels_FinishTheirTargetInsideTheirAuthoredBudgetPlusOverflow` asserts the
same derivation in EditMode.

### What still needs sentences

`activeSlotWindow` and `offTargetFillerWeight` are implemented and tested but sit at 1 and 0,
because no authored level has a sentence or paragraph target yet — all five are two short words.
They become the load-bearing parameters the moment one does; see §6.

---

## Open questions for the owner

1. **Is 130s the right X?** It is the level as currently authored. If first-time-player testing wants 150s, the 24-enemy budget cannot supply it — that needs `enemyCount` raised by ~6 across W4/W5, or `minSpawnsBeforeNeeded = 5` with overflow doing more work. Pick one before implementation.
2. **The "not yet, that comes later" feedback state** (§2) is a prerequisite of the chosen filler policy. If it isn't affordable this pass, filler should fall back to duplicates-only and `minFillerVariety` becomes moot.
3. **Suspending the active clue for the pair window** (§5) changes `ActiveClueDirector` behaviour that currently has EditMode tests asserting determinism. Confirm the suspension is acceptable before those tests are touched.
