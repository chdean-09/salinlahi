# Test run — D1 challenge board re-enable (steps 1-2)

Baseline `origin/dev` @ `80ce76e9` (fresh worktree, APFS-cloned Library).
Head `654ef989` + working-tree changes. Unity 6000.3.9f1, Editor closed.
Diffed **by test fullname**, not by count.

## Results

| Mode | dev total | dev failed | head total | head failed | regressions |
|---|---|---|---|---|---|
| EditMode | 1304 | 11 | 1371 | 11 | **0** |
| PlayMode | 211 | 4 | 215 | 4 | **0** |

Every failure on head is failing on `origin/dev` too — 11 EditMode, 4 PlayMode, all
pre-existing. No test regressed and none was fixed.

Unity exited 2 on all four runs. The exit code is not the signal; the XML is.

## Vanished tests — not attributable to this change

Four EditMode cases exist on `dev` and are absent from head:

- `UgatWaveCurveMigrationTests.MigratedUgatLevel_ResolvesFiveWavesCarryingItsWholeRoster(2|3|4)`
- `UgatWaveCurveMigrationTests.ReferenceLevels_KeepAuthoredWaves(5)`

`git log origin/dev..HEAD` attributes the file to `f668377f` (four-wave curve + gated finale)
and `91d6ba62` (Level 5 onto Curve_Ugat). Parameterised cases were removed there. Nothing in
this change touches that file.

## Coverage added, and proved to bite

The first run closed nothing: 65 challenge/flow tests passed without one of them entering the
new branch. `grep` over the whole suite confirmed why — **no test set both `activeClue` flags**,
which is what `UsesCombatRestorationPath` requires, so that branch had never been executed by any
test, before or after this change.

Two fixtures now cover it, each verified with a negative control rather than assumed:

**`PostCombatChallengeBoardSelectionTests`** (EditMode, 15 cases) pins
`LevelFlowController.SelectPostCombatBoardUnitIds` — the rule and the shipped data. Negative
control: Level 3's mode flipped to `WordPlacement` →
`ShippedRestorationLevel_StillOpensItsBoardAfterCombat("level.ugat.03")` failed, alone, with the
intended message. Level 3 restored.

**`LevelFlowControllerPhaseTests.CombatRestoration_*`** (PlayMode, 2 cases) cover the routing:
a `SentenceRestoration` unit opens the board after combat and the phase commits once; a
`WordPlacement` unit completes without one. Negative control: the old
`yield return ExecuteCombatRestoration(); yield break;` reinstated →

```
total 39  passed 38  FAILED 1
 -> CombatRestoration_WithASentenceUnit_StillOpensTheBoard_AndCompletesOnce
    Expected: ContextChallenge
    But was:  Completed
```

Exactly one test caught it, and the 38 others stayed green — which measures what the suite would
have missed. Routing restored after the control run.

### Still not covered

Whether a board loss can defeat a level already won in combat. `heartPenalty: 1` is on every unit
in all ten sequences and was kept as authored. That is the in-Editor play session the plan calls
for, and it is a judgement about feel, not an assertion.

## Final numbers

| Mode | dev total | dev failed | head total | head failed | regressions |
|---|---|---|---|---|---|
| EditMode | 1304 | 11 | 1386 | 11 | **0** |
| PlayMode | 211 | 4 | 217 | 4 | **0** |

Re-run in full after the fixtures were added, so the new PlayMode tests are confirmed not to leak
scenes or singletons into the rest of the suite.

## Worktree hygiene

`-runTests` dirtied `TutorialFont.asset` (-1590 lines) and back-filled
`gateFinalSlotToFinalWave`, `maxOverflowBatches` and `waveCurve` defaults into 12
`Level*_Config.asset` files. All restored. Only the five task-owned files remain modified.

---

# Step 5 — the progression as an author-time check

`CampaignConfigValidator.ValidateChallengeModeProgression` reports a challenge unit whose mode
contradicts its era position: steps 1-2 restore words, 3-4 sentences, 5 the era's mastery
paragraph. Emitted at the profile's content severity — a Warning while authoring, an Error under
Strict — because it fires on real unresolved content rather than on a hypothetical mistake.

**Scoped to a wrong mode, never a missing sequence.** A level with no `challengeSequence` already
fails loudly at runtime: `ExecuteContextChallenge` refuses the phase and shows the content-missing
panel, which is what makes Levels 6, 7, 8, 10 and 13 uncompletable today. It announces itself the
first time anyone plays it. A *wrong* mode is the silent case — the level plays, clears and
completes, having assessed the wrong thing. Reporting missing sequences here would also flag every
level of `CampaignTestFixture`, which authors none, so the check would have been born failing its
own suite.

The plan's original framing keyed the rule on `activeClueCombatEnabled`. That would have covered
only Levels 1-5, which all already match — a rule that catches nothing and reads as a pass. Keying
on era position covers all fifteen.

## What it finds today

Exactly two levels, which is pinned by
`ChallengeModeEraProgressionTests.ShippedCampaign_DisagreesWithTheProgression_OnExactlyLevels14And15`:

| Level | Ships | Era position wants |
|---|---|---|
| 14 | `TimedMemory` | `SentenceRestoration` |
| 15 | `WordPlacement` | `ParagraphRestoration` |

Both were authored deliberately against ticket acceptance criteria (SALIN-156 AC3, SALIN-158 AC2),
so the test asserts the disagreement is *exactly these two* rather than asserting the campaign is
clean. Resolving either one fails the test, and so does a third level drifting. The reconciliation
backlog is now a named failing assertion instead of a line in a plan.

## Verification

| Mode | dev total | dev failed | head total | head failed | regressions | new failures |
|---|---|---|---|---|---|---|
| EditMode | 1304 | 11 | 1398 | 11 | **0** | **0** |
| PlayMode | 211 | 4 | 217 | 4 | **0** | **0** |

Diffed by name against the same fresh `origin/dev` baseline. Nothing vanished.
