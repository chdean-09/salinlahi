# Salinlahi — general polish pass prompt

Paste the block below to start a polish session. It sequences the four workstreams so each one
gates the next, and front-loads the context that took a full session to discover last time.

---

I want to polish Salinlahi toward capstone completion. Work the four steps below **in order** —
each one gates the next, so do not jump ahead. After each step, stop and report before continuing.

**Step 1 — Play it before changing anything.**
Build and run Level 1 on the simulator. Play it end to end and report every visual or feel defect
you find, especially whether the enemy sizes and the fast-to-slow wave rhythm actually feel right.
Everything about enemy scale and wave pacing was validated by simulation and headless renders, never
by playing. Treat that as unverified. Do not fix anything yet — just report what you see, with
screenshots.

**Step 2 — Recover work that already exists.**
Audit the unmerged local branches whose remotes are gone. Tell me what is finished, what is
superseded by `dev`, and what is still valuable. `feature/SALIN-102-Write-Cutscene-Content-Between-Story-Levels`
has ~26 commits of cutscene content and `feature/SALIN-93-level-1-guided-onboarding` has ~13 of
guided onboarding; their remotes are deleted, so the reflog is the only copy. Land what is worth
landing. This is polish already paid for — do it before writing anything new.

**Step 3 — Finish the content.**
Land the unmerged SALIN-205 Ugat narrative branch, then author and validate the Level 2–5
configurations per SALIN-204. Fifteen open tickets are level configs and narrative
(SALIN-144–158, 172, 173, 204, 205); unfinished levels outweigh any amount of feature polish.

**Step 4 — Only then, depth.**
Implement the corrupted enemies' signature abilities from the `CorruptionEnemyBootstrap` roster,
starting with Mantsa, Takip, and Iligaw. Fifteen of the seventeen corrupted enemies currently have
no mechanic — only Labo (phaser) and Salungat (decoy) do — so they walk down and die identically.
The abilities are already designed in that file's roster comments. This is feature work, not polish:
flag it as such and do not start it if Steps 1–3 are still open.

## Ground rules

- **Verify by playing, not by simulating.** A headless render or a statistical model is evidence
  that something is *plausible*, not that it works. Where a claim can only be checked by playing,
  say so rather than implying it was verified.
- **Look at what actually ships before analysing it.** The prefab folder is not the enemy roster:
  the 17 corrupted enemies have no prefabs and resolve through `EnemyPool._enemyPrefab`. Resolve
  any roster from the level configs and the pool wiring, not from a file listing.
- **Both enemy rosters ship.** Colonial enemies appear in 11 of 15 levels; the corrupted set is the
  current design direction. Neither is dead.
- **Check sorting layers before trusting an occlusion or overlap metric.** Glyph badges render at
  `RenderOrder.EnemyGlyphBadge` (200) above enemies at `EnemyDefault` (0), so a badge is never
  hidden by a body.
- **Run `docs/jira/validate-git-conventions.sh` on the branch name, PR title, and every commit
  subject before opening a PR.** CI enforces it; PR titles must begin with an uppercase letter or
  number, so a conventional-commit-style title fails.
- **No Claude attribution** on commits or PR bodies.
- **Unity batchmode dirties the worktree** — `-runTests` reserializes `TutorialFont.asset` and
  deletes `PerformanceTestRun*.json`. Revert that dirt before committing. The PlayMode runner also
  exits `2` on any inconclusive result while the XML still says `Passed`; parse the XML, never the
  exit code. One test (`ElInquisidorTest`) is permanently inconclusive because
  `Resources/Test/Level5_ElInquisidor_TestRig.asset` has never existed.
- **Report honestly.** If a step is blocked, finish the others in full and say plainly what you left
  out and why. If a measurement contradicts an earlier conclusion of yours, say so and correct it.

## Definition of done for this pass

Level 1 plays end to end with no visual defects I have not accepted; every branch worth keeping is
either merged or explicitly written off; Levels 2–5 are authored, validated, and playable; and the
full suite is green apart from the one known-missing test fixture.
