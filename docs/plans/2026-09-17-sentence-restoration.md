# Sentence restoration — plan

**Finding (Level 3 playtest, 2026-09-17).** The combat target text is a list of isolated words —
`BATA`, `TAMA` — where it should be a sentence restored in order:

> Ang **MA**buting **BATA** ay gu**MA**gawa ng **TAMA**.

split into two phrases, the second opening only once the first is finished, and the level won when
the second completes.

**The request underneath it:** *"one syllable kill does not fill up 2 or more slots, only one at a
time."* That is not a detail. It is the root cause of several things we have already worked around,
and fixing it is what makes the rest of this simple instead of intricate.

## The root cause, and the band-aids it has produced

`ActiveClueRestorationState.Apply(symbolStableId)` fills **every** slot whose symbol matches,
across every focus word, from one defeated carrier. Restoration is by symbol, not by slot.

Everything below is a workaround for that one decision:

| Workaround | What it is really compensating for |
|---|---|
| `DerivedFinaleGate` picking "the last slot whose symbol occurs **exactly once**" | A gate on a repeated symbol withholds nothing, because the duplicate fills it for free |
| `ContentValidationCode.GatedFinaleUnwinnable` | Detecting levels where *no* symbol occurs once, so no slot can be withheld at all |
| The authored finale gating **every** slot carrying its symbol (`74fac901`, mine, yesterday) | Making TA gateable on Level 2 despite occurring twice |
| Level 2's derived finale landing on `MA` rather than its last slot | `ta@MATA` was silently ungateable |

Four mechanisms exist to route around one rule. Per-slot restoration deletes the need for all four:
if a kill fills exactly one slot, the last slot is always withholdable, and "the finale" is just
"the last slot of the last phrase". No derivation, no uniqueness analysis, no symbol-wide gate.

**This supersedes my own recent work.** `4aa0e675` and `74fac901` should be reverted as part of
this, not built on.

## What "a sentence" needs that focus words cannot express

`FocusWordDefinition` is `latinSpelling` + `decomposition` — a word and its syllables. A sentence
needs three things it has no room for:

1. **Literal text that is never restored.** "Ang", "ay", "ng", and the unmarked parts of
   "MAbuting" / "guMAgawa".
2. **Slots embedded mid-word.** The restorable `MA` inside `MAbuting` is one slot; the rest of the
   word is scenery.
3. **Order.** Phrase 1 before phrase 2.

**Do not invent a fourth target-text model.** `ChallengeUnitDefinition` already carries exactly this
shape — `prompt` plus `slots` plus `tokens` — and Level 5's paragraph units already use it for
gapped text (`"______ na ang panahon, ngunit ang ______ ni Juan ay nasa kanya pa"`). The combat rail
should adopt that shape rather than grow a parallel one. This is the same argument the challenge
reveal plan made in September and it still holds.

## Proposed design

**1. Phrases replace the flat focus-word list.**
A level's target text becomes an ordered list of phrases. Each phrase carries display text and its
restorable slots, in the `ChallengeUnitDefinition` shape. Level 3:

| Phrase | Text | Slots |
|---|---|---|
| 1 | Ang **MA**buting **BATA** | MA, BA, TA |
| 2 | ay gu**MA**gawa ng **TAMA**. | MA, TA, MA |

**2. Only the active phrase is live.** Its slots are the spawner's targets and the only ones the
rail accepts. Earlier phrases are shown restored; later phrases are shown as scenery or hidden — a
presentation choice worth deciding explicitly rather than by default.

**3. One kill, one slot.** `Apply` fills the first unrestored slot of the active phrase matching
that symbol, and nothing else. Level 3's phrase 2 needs two separate MA kills.

**4. A phrase completes, the next opens. The last completes, the level is won.** This replaces
`IsTargetComplete` as the win condition and makes `gateFinalSlotToFinalWave` redundant.

**5. Waves are authored per phrase, in the asset we already have.**
`IntroductionScheduleSO` already holds each level's introductions, fillers and finale. Phrase wave
budgets belong in the same entry — Level 3 phrase 1 gets 3 waves; phrase 2 spawns until filled, which
`WantsOverflow` already does. Filler behaviour is unchanged: some earlier-level syllables as padding,
mostly the phrase's own.

## What this breaks, honestly

This is a core rule. Everything below reads it today and must be revisited, not merely recompiled:

- **Abilities.** `AshFirstSlotController` covers "the first slot"; `KempeiScrambleController` stains
  symbols; `GlyphCoverController` hides a carrier's own. Each assumes symbol-wide restoration.
- **`RestoredSlotCount`.** Abo's lesson gates on "the player has used the clue at least once".
- **The clue rail.** `ActiveCluePresenter` builds one flat rail from all focus words; phrases need
  either a rail per phrase or a rail that scrolls.
- **`SpawnAssignmentCoordinator`.** Slots, gates, whitelist and `IsTargetComplete` all assume one
  flat slot list for the level.
- **`LevelFlowController.TryBuildRestorationTarget`** maps challenge units to focus words by
  `evidenceContentId`; phrases change what a "target" is.
- **Combat-restoration levels 1–5** all carry `activeClueRestorationEnabled`.
- **Tests:** `GatedFinaleSlotTests`, `GatedFinaleValidationTests`, `GatedFinaleCampaignOptInTests`,
  `IntroductionScheduleTests`, the withholding tests, and the D1 board tests.

**Estimated shape:** this is a multi-session change touching data model, spawner, presenter,
abilities and validator. It is not a level-3 fix.

## Order of work

1. **Per-slot restoration, alone.** Change `Apply`, fix the abilities and `RestoredSlotCount`, keep
   the flat word list. Levels play the same except a repeated symbol now needs two kills. Ship and
   playtest this by itself — it is the highest-risk step and the one most likely to surface
   surprises in the abilities.
2. **Retire the finale machinery.** With per-slot restoration, delete `DerivedFinaleGate`, the
   authored finale field, `GatedFinaleUnwinnable`, and `gateFinalSlotToFinalWave`. The last slot is
   the finale.
3. **Phrases.** Introduce the phrase model on the `ChallengeUnitDefinition` shape, migrate Levels
   1–5, teach the rail and the spawner about the active phrase.
4. **Author Level 3's sentence** and its 3 + overflow wave split.
5. **Extend the validator** so a phrase whose slots the level's roster cannot spawn fails at author
   time, as the roster gate does now.

## Testing

Per-slot restoration needs a negative control before anything else: a level whose target text
repeats a symbol, asserting that one kill leaves the second slot unrestored. Without it, symbol-wide
restoration passes every test we have.

Each step diffed by name against a fresh `origin/dev` baseline, and step 1 needs a play session —
whether the abilities still read correctly cannot be seen from batchmode.

## My assessment

**The phrase split is right and worth doing.** A sentence restored in order teaches reading in a way
two isolated words cannot, and "finish the thought to win" is a better win condition than "fill four
slots".

**Per-slot restoration is right and is the real fix.** It removes four separate mechanisms rather
than adding a fifth.

**Two things I would decide before starting, not during.**

- *Does a phrase's completed text stay on screen?* Level 3's full sentence is long; a rail holding
  phrase 1 while phrase 2 fills may not fit the portrait layout. Measure before authoring.
- *Does a wrong draw still cost anything?* With one kill per slot, phrase 2 needs six kills rather
  than four. The level gets longer; whether it also gets harder is a tuning decision.

**One caution.** Step 1 changes how every ability behaves, on every level, including the three that
already ship. It should go in on its own, with a playtest, before phrases are built on top of it.
