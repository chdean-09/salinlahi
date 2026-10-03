# EditMode verification — Phase 1a (Level 5 wave curve, resolver fallback, roster validator)

- **Branch:** `bugfix/level-5-wave-curve`, based on `dev` @ `c3f515d1`
- **Date:** 2026-09-16
- **Unity:** 6000.3.9f1, batchmode, detached worktree `salinlahi-worktrees/level5-curve`
  (Library warmed by APFS clone; the Editor open on the main checkout, PID 55965, was never touched)
- **Plan:** `docs/plans/ugat-qa-fix-plan.md` §4.1 steps 1–3. Step 4 (segment scoping) is **not** in
  this branch — it is gated on decision D3.

## Baseline pinned at the actual base commit

The plan cites a 7-failure baseline taken at `9ce1dc57`. That commit is an ancestor of `c3f515d1`
but not the base, so the baseline was re-run on an unmodified `c3f515d1` before any edit:

| Run | Total | Passed | Failed |
|---|---|---|---|
| Baseline @ `c3f515d1` (unmodified) | 1288 | 1281 | **7** |
| This branch | 1296 | 1289 | **7** |

Both XMLs were confirmed present and non-empty before being read; the batchmode exit code was
ignored (it returns 0 even when no results are produced).

**Failure-set diff: NEW none, GONE none.** All 7 are the known pre-existing failures
(4 × Level 2 onboarding / combo removal, 2 × Level 5 flow-segment count, 1 × Level 5 Walang-Awa).
The two Level 5 segment failures are expected to persist: this branch deliberately does not touch
`flowSegments`.

Total rises by 8 = 3 new coordinator tests + 4 new validator tests + 2 added `[TestCase(5)]`
instances − 1 removed `[TestCase(5)]`.

## New tests — all executed and passed

Confirmed by name in the results XML rather than inferred from the count:

- `CombatWaveRosterValidationTests` — 4/4 passed
- `SpawnAssignmentCoordinatorResolverTests` — 3/3 passed
- `UgatWaveCurveMigrationTests` — `MigratedUgatLevel_*(5)` passed on both cases;
  `ReferenceLevels_KeepAuthoredWaves(1)` passed with 5 removed

## Negative control

"0 issues" from a check that cannot fire is indistinguishable from a pass, so the validator was
run against the defect it exists to catch. With `Level5_Config.asset` reverted to its three
authored waves and nothing else changed:

`ShippedCampaign_HasNoCombatWaveThatNarrowsItsRestorationRoster` **FAILED**, reporting

```
WAVE_ROSTER_NARROWS_RESTORATION @ campaign.revised-v1.eras[0].levels[4].waves[0].characters:
  ... Missing: symbol.ba, symbol.ma, symbol.na.
WAVE_ROSTER_NARROWS_RESTORATION @ campaign.revised-v1.eras[0].levels[4].waves[0].enemyTypes: ...
```

The other three tests in the fixture still passed, so the failure is the shipped-campaign gate
alone. The asset was then restored to its migrated form.

## Scope check on the new validator

Before the check was added, every shipped level was resolved guid-by-guid to answer which would
fire. Only Levels 1–5 have `activeClueCombatEnabled: 1`. **Level 5 was the only level that fired**,
on all three of its waves. Level 1 keeps authored waves deliberately and does **not** fire — all
five of its waves carry the identical full roster, so no carve-out was needed. Levels 2–4 have
empty wave lists and inherit the roster. Levels 6–15 are on legacy combat and are skipped.

## Worktree hygiene

Batchmode reserialized `TutorialFont.asset` and wrote a default `waveCurve: {fileID: 0}` into the
11 level assets that had never serialized the field. That churn is out of scope and was reverted;
only `Level5_Config.asset` remains modified. Run artifacts (`*.xml`, `*.log`, stdout captures) were
deleted. The two new test scripts carry the `.meta` files Unity generated for them.

## Not run

- **PlayMode:** NOT RUN. No PlayMode test was added or touched.
- **In-Editor play session:** NOT RUN. The plan's visual gate for §4.1 — play Level 5 three times
  and confirm no wave starts after the last target restores, every glowing enemy carries an open
  slot's symbol, and every enemy body matches its badge — is **still outstanding**. It cannot be
  satisfied from a batchmode suite, and the segment half of that gate depends on D3 anyway.
