# Integration verification — Ugat QA Phase 1 (all four branches together)

- **Date:** 2026-09-16
- **Base:** `dev` @ `c3f515d1`
- **Branch:** `integration/ugat-qa-phase1` — the combined state of all four PRs, because that is what
  actually reaches `dev`. The four were previously tested only in isolation.
- **Unity:** 6000.3.9f1 batchmode, detached worktrees. The Editor on the main checkout was never touched.

## Baselines

Both were pinned on an unmodified `c3f515d1` before any edit. The PlayMode baseline did not exist
until now — until it did, no PlayMode result could mean anything.

| Suite | Baseline @ `c3f515d1` | Integration | Verdict |
|---|---|---|---|
| EditMode | 1288 / 1281 passed / **7 failed** | 1309 / 1302 / **7 failed** | NEW none, GONE none |
| PlayMode | 208 / 205 passed / **3 failed** | 208 / 205 / **3 failed** | NEW none, GONE none |

EditMode total rises by 21 (new tests); PlayMode total is unchanged because no PlayMode test was
added. The 7 EditMode failures are the known pre-existing set; the 3 PlayMode failures are
pre-existing and were not previously recorded anywhere.

**28 new or changed tests, all passing**, verified by name in the results XML rather than inferred
from the totals.

## Two things this pass caught that per-branch testing had not

**The always-on scrim.** The first end-screen fix put one opaque scrim on `EndScreenCanvas`. It is
active from scene load and nothing toggled it, so it covered the gameplay HUD and swallowed every
tap for the whole level — with a fully green EditMode suite. A render of the HUD with both panels
closed showed a blank screen, and that render existed before the fix was declared done and was not
looked at. `EndScreenBackdropSceneTests` now pins the shape that cannot regress that way:
`EndScreenCanvas_HasNoAlwaysOnFullScreenGraphic`, plus a per-panel backdrop check.

**A review claim that was wrong.** A reviewer concluded the EnemyPool log test was dead coverage,
because `ENABLE_SALINLAHI_LOG` is declared for Standalone only while the active build target is iOS,
and the test was removed on that basis. Running it showed the log *does* fire on the iOS-target run:
EditMode tests execute inside the Editor, which uses the Standalone define set whatever
`-buildTarget` says. The test was restored, and was then confirmed to genuinely execute under both
`-buildTarget StandaloneOSX` and the default target. An `Assert.Ignore` that cannot be reached is
still worth removing, but the reasoning behind removing the assertion was not correct.

## Still not covered

- **No in-Editor play session.** The Level 5 gate (no wave starts after the last target restores;
  every glowing enemy carries an open slot's symbol; every enemy body matches its badge) and the
  instruction-versus-rail gap both need the game actually running. The rail is built at runtime and
  does not exist in the saved scene, so no static render can show that gap.
- **No player build.** Nothing here was compiled for iOS or Standalone as a player.
- **`OnEnable` clearing paths** on the three ability controllers are unexercised — EditMode does not
  fire lifecycle callbacks. The `Initialize` restatement, which is the path that must hold for a
  pooled respawn, is covered.
- **No test covers the star sprite's appearance**, only that a sprite is assigned. That it looks
  like a star was checked by eye in `QA/screenshots/`.
