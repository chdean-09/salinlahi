# Level 1 Enemy Introduction Lesson — Design

**Date:** 2026-09-14
**Status:** Approved for planning
**Supersedes:** the `SoloTeachBeat` four-step teach loop

---

## 1. Problem

Three systems currently claim the job of telling a player what an enemy is.

| System | Where | State |
|---|---|---|
| `SoloTeachBeat` + `basicTeachSteps` | pre-combat, `Level1OnboardingController` | Live. Runs four frozen teach steps back-to-back. |
| `EnemyIntroductionBeat` | mid-combat, from `Enemy.Initialize` | Live and wired in `Gameplay.unity`. |
| `EnemyDiscoveryOnboardingController` | — | Dead. Unwired since a bad merge; see `docs/handoff-enemy-discovery-overlay.md`. |

The first two conflict structurally rather than cosmetically. `SoloTeachBeat` sets
`TutorialRuntimeState.SetCombatOverrideActive(true)`, and
`EnemyIntroductionBeat.IsIntroducibleSpawn` declines every claim while that flag is set. So on
Level 1 the player draws four glyphs carried by four enemies that are never named, never
explained, and never attributed an ability. Each of those types is then introduced later,
mid-wave, as a stranger the player has already killed.

Two further defects follow from the same cause:

- **Four introductions at once.** The teach loop runs EI, NA, A, MA consecutively. Nothing
  separates one enemy's lesson from the next, so no single ability can be revealed, reacted to,
  and explained before the next enemy arrives.
- **Abilities never fire during teaching.** Teach-step enemies are frozen and short-lived, so the
  player's first exposure to every signature ability happens with no framing at all.

## 2. Ruling

**`EnemyIntroductionBeat` is the single introduction system.** `SoloTeachBeat`'s teach loop is
deleted. `EnemyDiscoveryOnboardingController` and `EnemyDiscoveryOverlay.prefab` are deleted.

There is exactly one place a player learns what an enemy is, and its trigger is that enemy's first
spawn.

`EnemyIntroductionBeat` keeps its four-step shape (Halt, Name, Ability, Release) as the universal
default and gains an optional, level-scoped **lesson profile** that extends it to eight beats.
Level 1 authors exactly one profile, on Abo ng Simula. Levels 2 through 15 author none and are
behaviourally unchanged.

## 3. The eight beats

```
Beat 1  Appear      halt + vignette, ability ARMED (inverts today's suppression)
Beat 2  Ability     ash gust fires; the word clue visibly crumbles
Beat 3  React       one line — the player is told something just happened   [once per campaign]
Beat 4  Generalize  every enemy has its own ability                          [once per campaign]
Beat 5  Name        "Abo ng Simula"
Beat 6  Explain     abilityLine — what it does, never how to counter it
Beat 7  Glyph       badge reveal on the enemy
Beat 8  Draw        gated trace of that glyph; the enemy dies, the ash lifts
```

Beats 1, 5 and 6 are the existing card steps. Beats 3 and 4 are new copy. Beats 7 and 8 reuse the
surviving `Level1TutorialStepSO` guide machinery.

Beat 8 pays off beat 2 directly: the clue the player lost in beat 2 returns when they complete the
draw. That closure is the lesson.

## 4. Why Abo, and why he is second on screen

### 4.1 Abo is permanently bound to A

`EnemyData_AbongSimula.assignedCharacter` is `Char_A`, and ruling C2
(`docs/design/spec-rulings-2026-09.md`) fixes that binding: one syllable, one character, one
enemy, everywhere. Abo carries A in every level. He cannot be reassigned for the lesson.

### 4.2 His ability is inert on the first needed slot

`ActiveCluePresenter.BuildMaskedSpellingWithRestoration` masks a slot when

```
(isTargetSlot && !isRestoredSlot) || isAshedSlot        where isAshedSlot = ash && emittedSlots == 0
```

The ash always masks emitted slot 0. The target slot is already masked. When the needed symbol
*is* its word's first symbol, both conditions land on the same slot and the ashed and readable
strings are byte-identical. `ActiveCluePresenter` detects this and cancels the crumble animation:

> Equal strings mean the ash bit nothing — the needed slot already was the word's first.

Level 1's flattened slots are `0=I 1=NA 2=A 3=MA`. At level start the needed slot is 0, which is
`INA`'s first symbol. **An ash fired on the opening spawn is a guaranteed no-op.** This is
arithmetic, not tuning — no authoring of Abo avoids it, because A is permanently word-position-1
of `AMA`.

This is also why `AshFirstSlotController` arms per spawn today, and why its docstring records the
earlier version being reverted: the HUD changed before the player had ever read it unobscured, so
there was no baseline against which the change could register as an enemy doing something.

### 4.3 Consequence: one enemy precedes him

The lesson requires one restored slot. Level 1's opening becomes:

```
Spawn 1   Iligaw carrying I
          ability suppressed (existing introduction-spawn rule)
          introduction card DEFERRED — no name, no card
          non-blocking first-draw trace guide
          kill -> clue "_NA" becomes "i_"      <- the baseline, and a real restoration

Spawn 2+  Abo carrying A, ARMED — his first spawn once slot 0 is restored
          all eight beats
          beat 2: "i_" becomes "__"            <- the ash now has something to take
          beat 8: trace A -> Abo dies, ash lifts, "i_" returns
```

Level 1's wave table draws from four types, so Abo is not guaranteed to be literally the second
enemy: his lesson runs on his first spawn after the precondition is met, which may be spawn 2, 3
or 4. This is acceptable because the deferral rule in 7.1 keeps every other type anonymous until
the lesson lands, so whatever arrives in between is still an unnamed corrupted thing. No spawn
directive pins Abo, and none is added — pinning an enemy *type* is machinery
`SpawnAssignmentPolicy` does not have today, and the deferral already buys what it would.

Abo is not a carrier in his own lesson. He is interference: the player needs NA, Abo makes NA's
word unreadable, and the counter is written on Abo's own badge. That is the "player derives the
counter" rule `EnemyDataSO.abilityLine` is authored to.

Iligaw stays nameless for roughly twenty seconds. This is deliberate — it reads as an anonymous
corrupted thing, and beat 4 is what converts it retroactively into an instance of a rule.

### 4.4 Options considered and rejected

- **Abo first with the ash silent.** Beats 2-4 move to his second arrival. Rejected: splits one
  lesson across two encounters, which is the defect this design exists to remove.
- **Abo first with ashed slots re-rendered** as a burnt mark distinct from an unrestored `_`.
  Rejected for now: it changes a shipped ability's presentation, and beat 2 would still take
  nothing from the player, so beat 3 would react to menace rather than loss. Recorded as a viable
  fallback if the twenty-second delay playtests badly.

## 5. Data model

### 5.1 New — `EnemyLessonSO`

```csharp
[CreateAssetMenu(menuName = "Salinlahi/Enemy Lesson")]
public sealed class EnemyLessonSO : ScriptableObject
{
    public EnemyDataSO          enemy;                     // whose first spawn triggers this
    public int                  requiredRestoredSlots = 1; // precondition; see 4.2
    public bool                 armAbilityOnIntroduction;  // inverts suppression (Abo: true)
    public OnboardingBeatCopy   reactLine;                 // beat 3, once per campaign
    public OnboardingBeatCopy   ruleLine;                  // beat 4, once per campaign
    public bool                 revealGlyphLate;           // beat 7; badge hidden through 1-6
    public Level1TutorialStepSO drawStep;                  // beat 8; null = no draw gate
    public float                abilityBeatSeconds = 2.5f; // beat 2 hold, wall-clock
    public string               opensGateTokenOnComplete;  // see section 6
}
```

Referenced from `LevelConfigSO`, **not** from `EnemyDataSO`: the lesson is level-scoped, the enemy
is not. Putting it on the enemy would make Abo teach his lesson on every level he appears in, and
would contradict C2's "an enemy keeps the same asset and abilities everywhere."

```csharp
// LevelConfigSO
public EnemyLessonSO[] enemyLessons;   // Level 1: one entry (Abo). All other levels: empty.
```

`requiredRestoredSlots` keeps the arithmetic of 4.2 visible to a designer in the Inspector rather
than buried as a Level 1 special case inside the beat.

### 5.2 Retired

- `OnboardingBeatType.SoloTeach` — the enum value `2` stays burned, per the file's own convention
  ("a stale serialized `beatOrder` blob still carrying 3 or 6 would otherwise bind silently to the
  new beat").
- `OnboardingSequenceSO.soloTeachStep`, `basicTeachSteps`, `basicTeachVideos`,
  `soloTeachPreVideo`, `soloTeachVideo`, `soloTeachPostSuccess`.
- `SoloTeachBeat.cs`, `EnemyDiscoveryOnboardingController.cs`, `EnemyDiscoveryOverlay.prefab`.

### 5.3 Kept

`Level1TutorialStepSO`, `Level1TutorialGuideUI`, `TutorialAssistAnimator`, `TutorialIntroPlayer`
and `TutorialSpotlightOverlay` all survive and are reused by beats 7 and 8. Only the four-in-a-row
loop dies, not the teaching machinery.

`Level1TutorialSequenceSO` remains as a legacy adapter behind `LevelConfigSO.tutorialSequence`.
Removing it is out of scope for this change and is recorded as separate cleanup.

## 6. Spawn gating

Today `Level1_Config` gates flattened slot 3 (the MA of AMA) on `SpawnGateRegistry.AboAshShown`,
which opens when Abo's ash arms on a later spawn. Its stated purpose is that the level be
"structurally incapable of completing early" — that is, before the player has seen an ability at
all.

Under this design the ash arms in beat 2, near the start of the level, so that token opens almost
immediately and the gate stops doing its job.

**Ruling: retarget, do not remove.** A new token replaces it on Level 1's slot 3:

```csharp
// SpawnGateRegistry
public const string Level1RosterMet = "level1_roster_met";
```

It opens when `EnemyIntroductionProgress` has recorded an introduction for every non-decoy,
non-boss type in the level's wave roster — for Level 1, Abo's eight-beat lesson plus the three
standard cards. The roster is read from `LevelConfigSO.waves[*].enemyTypes` rather than authored
separately, so a wave-table edit cannot leave the gate pointing at a type the level no longer
spawns. The check runs when any introduction completes. This preserves the gate's real purpose, that the level
cannot end before the player has met every enemy in it, and it stays inside the existing
one-token-per-slot model.

`AboAshShown` stays in the registry, unused by Level 1, available to any level that wants it.

## 7. The other three Level 1 enemies

### 7.1 Deferral must also suppress

`EnemyIntroductionBeat` today treats a declined claim as safe precisely because it leaves the
ability armed: "the safe failure is an ability with no card, never a card's worth of silence with
the ability switched off." That inverts here. While a level's lesson is pending, other types are
declined *deliberately*, and under the existing rule Iligaw would spawn a mirror decoy on spawn 1
— an unexplained duplicate enemy before the player has been told abilities exist, which is the
exact failure beat 4 is meant to prevent.

So deferral is a third state, not a decline. A type deferred by a pending lesson spawns with its
ability **suppressed**, and its introduction and ability both arm on its next spawn after the
lesson completes. `Enemy.Initialize` therefore asks for a tri-state — *introduce and arm*,
*introduce and suppress*, *defer and suppress* — rather than the current boolean.

This is the single most likely thing to be broken by a well-meaning edit, because the existing
comment in `Enemy.Initialize` argues the opposite for the ordinary case, and it is correct for the
ordinary case. Both rules must be stated at that call site.

### 7.2 What each receives

Iligaw, Nawalang Mukha and Mantsa each receive:

1. **The existing four-step card** on their own first spawn. This now actually fires during
   Level 1, because there is no combat override left to decline against. Their abilities remain
   suppressed on the introduction spawn — the inversion in 5.1 is Abo-only — and, per 7.1, while
   Abo's lesson is still pending.
2. **A non-blocking first-draw trace guide** the first time each one's glyph is the needed symbol.
   It does not halt, gate or pause; it is the guide overlay from `Level1TutorialStepSO` shown
   alongside live combat and dismissed on the first success or after a timeout.

No glyph in Level 1 is drawn cold, and no enemy after Abo halts the level for a gated lesson.

Beats 3 and 4 do not repeat. They latch once per campaign in `EnemyIntroductionProgress`, which
`ProgressManager` already clears on New Journey, so replaying Level 1 re-teaches the rule and a
later level never does.

## 8. Code changes

| File | Change |
|---|---|
| `EnemyIntroductionBeat.cs` | Lesson-profile branch in `TryClaim`/`PlayIntroduction`. Precondition check declines **without** spending the type's one-shot. Defers other types' claims while a level's lesson is still pending. |
| `Enemy.cs` | `ApplyIntroductionSpawnSuppression(_isIntroductionSpawn && !lessonArmsAbility)`. New `SetGlyphBadgeVisible(bool)` seam for beat 7. |
| `Level1OnboardingController.cs` | Drop the `SoloTeach` case and its GIF-override resolution. |
| `SpawnAssignmentPolicy.cs` | No new fields. `openingSpawnSpokenValueId` and `slotFloors` already express the opening. |
| `SpawnGateRegistry.cs` | Add `Level1RosterMet`. |
| `LevelConfigSO.cs` | Add `enemyLessons`. |
| `Level1TeachingBeatSceneWiringTool.cs` | Docstring is stale — it claims none of the teaching components are placed; all six are present in `Gameplay.unity`. Correct it, and place the first-draw guide surface. |

## 9. Authoring changes

| Asset | Field | From | To |
|---|---|---|---|
| `Level1_Config` | `spawnAssignmentPolicy.openingSpawnSpokenValueId` | `value.a` | `value.ei` |
| `Level1_Config` | `slotFloors[0].minSpawnsBeforeNeeded` | `1` | `0` |
| `Level1_Config` | `slotGates[0].gateToken` | `abo_ash_shown` | `level1_roster_met` |
| `Level1_Config` | `enemyLessons` | — | `[AboLesson]` |
| `Level1OnboardingSequence` | `beatOrder` | `0,1,2,5` | `0,1,5` |
| `Level1OnboardingSequence` | `basicTeachSteps` | 4 entries | cleared |
| new | `AboLesson.asset` | — | `enemy=Abo, requiredRestoredSlots=1, armAbilityOnIntroduction=true, revealGlyphLate=true, drawStep=Level1TutorialStep_A`; `reactLine` and `ruleLine` pending copy (§11); `opensGateTokenOnComplete` left empty — the token is raised by the roster check in §6, not by this lesson |

`Level1TutorialStep_A` is reused unchanged for beat 8 — it already targets `Char_A` and
`EnemyData_AbongSimula`. Its `promptText` should be re-authored from "Draw A. Follow the guide."
to copy that names the ash being lifted.

Copy for beats 3 and 4 is Filipino per ruling Q16 (story lines stay Filipino; English is UI copy
only). Draft before authoring against `docs/content/`, which holds the team's authored Filipino.

## 10. Testing

**EditMode, no scene:**
- `EnemyLessonSO` precondition: a claim below `requiredRestoredSlots` is declined *and* the type's
  one-shot is not spent. This is the regression that would otherwise silently disable Abo's
  introduction for the whole campaign.
- `SpawnGateRegistry`: slot 3 stays withheld until `level1_roster_met` opens; `AboAshShown` no
  longer gates anything on Level 1.
- `BuildMaskedSpellingWithRestoration`: with slot 0 restored, ashed and readable strings differ —
  the assertion that beat 2 is visible at all. Negative control: with nothing restored they are
  identical.
- Beat 3/4 once-per-campaign latch across a simulated Level 1 replay.
- Deferral suppresses: a type declined by a pending lesson reports ability-suppressed, not armed
  (7.1). Negative control — the same type with no lesson pending reports armed.
- `level1_roster_met` opens only after every roster type has an introduction recorded, and the
  roster is derived from the wave table.

**PlayMode** (lifecycle callbacks are required, so these cannot be EditMode):
- Full opening: Iligaw spawns first carrying I, no card shown; after its defeat Abo's claim is
  accepted and all eight beats run in order.
- Abo's ability is armed on the introduction spawn; the other three types' are not.
- Every exit path restores `Time.timeScale`, the vignette, the glyph badge and enemy movement —
  including an abort mid-lesson.

**Visual check is mandatory, not optional.** A green suite has passed five UI wiring defects on
this project before. Run the level and look at it: the crumble in beat 2, the badge reveal in
beat 7, and the ash lifting on beat 8's success.

## 11. Open items

- Filipino copy for beats 3 and 4 — author against `docs/content/` before implementation.
- Whether the first-draw guide for enemies 2-4 should time out or persist until first success.
- `Level1TutorialSequenceSO` legacy adapter removal — separate cleanup, not this change.
