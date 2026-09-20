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

## What it found, and what closed it

It flagged exactly two levels:

| Level | Shipped | Era position wants |
|---|---|---|
| 14 | `TimedMemory` | `SentenceRestoration` |
| 15 | `WordPlacement` | `ParagraphRestoration` |

Both had been authored against ticket acceptance criteria — SALIN-156 AC3 calls Level 14 "the
timed memory sentence", SALIN-158 AC2 is word forming — so I first reverted them and left the
check asserting the disagreement was exactly those two.

That was the wrong call. The plan sets the precedence rule in its own second line: *where this
plan and the table disagree, the table wins.* A ticket AC is not an exemption from it. Both levels
are now on the table's modes, their authoring tools carry a SUPERSEDED note over the original
reasoning, and the test asserts the campaign matches the progression outright
(`ShippedCampaign_MatchesTheEraProgression`).

Levels 6, 7, 8, 10 and 13 are not covered: they have no sequence at all, which this rule leaves to
the runtime. They fall under the assertion automatically once step 3 authors them.

**Level 15's copy is still not a paragraph.** Its two units are single-slot with instructional
prompts ("Buuin mo ang pangalan..."), so the mode is now correct and the writing is not. That is
SALIN-158 AC3, which the authoring tool already records as blocked on copy that unblocks three
tickets at once.

## Verification

| Mode | dev total | dev failed | head total | head failed | regressions | new failures |
|---|---|---|---|---|---|---|
| EditMode | 1304 | 11 | 1398 | 11 | **0** | **0** |
| PlayMode | 211 | 4 | 217 | 4 | **0** | **0** |

Diffed by name against the same fresh `origin/dev` baseline. Nothing vanished.

---

# Step 4 — focus words: closed as already satisfied

Neither change the plan describes is needed. Both of its step-4 claims describe the shipped state
inaccurately, which is different from the Level 14/15 case — there the plan and a ticket AC
disagreed about what *should* be, and the plan's own precedence rule settled it.

## Level 5 already covers all six Era 1 characters

Its three `ParagraphRestoration` units answer:

| Unit | Answers |
|---|---|
| `ugat05-restore-line-01` | IBA, MANA |
| `ugat05-restore-line-02` | INA, AMA |
| `ugat05-restore-line-03` | TAMA |

Spelling out to **A, BA, EI, MA, NA, TA** — the complete Era 1 set, with nothing missing.

The plan read the `focusWords` list (IBA, MANA) and concluded A and TA were absent.
`docs/content/ugat-levels-2-5-narrative.md` is explicit that these are different axes: *"IBA and
MANA are the level's taught words; INA, AMA and TAMA are review blanks from Levels 2-4"*, and
*"every blanked word is spellable from the six Era 1 characters only"*.

Worth noting why this was easy to miss: the coverage lives in the challenge, and until step 1 of
this plan that challenge had never run. The data was right and invisible at the same time.

Adding A and TA to `focusWords` would also demote IBA and MANA from being the level's taught
words, and each new focus word carries its own dialogue and restored-memory cutscene — authoring,
not reconciliation.

## PAMANA is Level 15's word

The narrative roster assigns `level.pamana.05 | PAMANA | MALAYA`, and Level 15 ships exactly
those. Level 11 ships DALA and DAMA, matching SALIN-153 AC2 (`DALA = DA + LA`, `DAMA = DA + MA`).

`Char_PA.firstIntroductionLevelId` is `level.pamana.05`, so spelling PAMANA on Level 11 would use
PA four levels before it is introduced — what `SYMBOL_NOT_INTRODUCED` and
`PA_INSTRUCTION_ORDER_INVALID` exist to catch — and would duplicate Level 15's own focus word.

Confirmed 2026-09-17: Level 11 keeps DALA and DAMA.

## Remaining

Step 3 only — the five missing sequences on Levels 6, 7, 8, 10 and 13, which need Filipino copy
the team writes. Level 13 has no focus words either, which is why `Char_RA` is introduced by no
level. Level 15's paragraph copy (SALIN-158 AC3) is part of the same writing task.
