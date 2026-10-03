# Phase 3 — General Polish Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Take Salinlahi from "merged but unverified" to "Level 1 plays clean, every stranded branch is landed or written off, Levels 2–5 are authored and playable, suite green."

**Architecture:** Four gated workstreams executed strictly in order — verify by playing, then recover paid-for work, then finish content, then (only if the first three close) add depth. Each step stops and reports before the next begins. No step may start while an earlier one is open.

**Tech Stack:** Unity `6000.3.9f1`, .NET SDK `9.0.306`, C#, Unity Test Framework (EditMode + PlayMode), iOS Simulator, Unity MCP relay (`scripts/unity-mcp-launcher.js`).

**Spec:** [`docs/polish-pass-prompt.md`](docs/polish-pass-prompt.md) — the authoritative statement of this pass. Read it alongside this plan.

**Supporting context:** [`docs/audit/STATUS-2026-09-13.md`](docs/audit/STATUS-2026-09-13.md) (state audit), [`progress/2026-09-14-character-art-import.md`](progress/2026-09-14-character-art-import.md) (last session's delivery + open decisions), [`docs/audit/BACKLOG.md`](docs/audit/BACKLOG.md), `docs/system/09_Test_Strategy_and_Acceptance_Criteria.md`.

---

## Baseline (verified 2026-09-14)

| Fact | Value |
|---|---|
| Checkout | `dev` @ `90410457` == `origin/dev`, 0 commits behind |
| Working tree | Clean except untracked `docs/audit/`, `docs/polish-pass-prompt.md`, `progress/` |
| Open PRs | None |
| Other worktrees | `salinlahi-worktrees/SALIN-205`, `.claude/worktrees/bold-bardeen-b60246` (`test/deterministic-boss-summon-timing`) |
| Existing iOS build | `Builds/iOS-Sim/` — generated Xcode source from 2026-09-06, **stale**, no `.app` present |

Phases 1–2 delivered and merged: the read-only audit; the Levels 1–5 fix pass, pronunciation audio and shared active-clue path (`f35de96d`); replacement character art with Juan attack **and idle** (`f35de96d`); Hati/Ragasa test coverage (`debe7bf9`) and controller metadata (`c912bed6`).

## Corrections to the spec's premises

These were verified before planning. The executor must not act on the superseded versions.

- **C1 — SALIN-102 is not stranded.** `git cherry dev feature/SALIN-102-Write-Cutscene-Content-Between-Story-Levels` reports **26 `-`, 0 `+`**: every commit has a patch-equivalent already in `dev`. The spec's "the reflog is the only copy" is false for this branch. Prior session notes also record that its orphan cutscene was left unwired deliberately.
- **C2 — SALIN-93 is genuinely unapplied but high-risk.** 13 commits, 0 patch-equivalent in `dev`, 54 files, and it rewrites `Assets/_Scenes/Gameplay.unity` by 6357 lines. `dev` has since heavily modified that same scene (Level 1 fix pass, active clue, Ready screen). A blind merge will conflict destructively.
- **C3 — SALIN-205 overlaps dev's active-clue work.** 1 commit, 39 files, and it modifies `Level2_Config.asset`–`Level5_Config.asset`, which `f35de96d` also modified. Expect a real conflict in those four assets.
- **C4 — Step 4's three named targets are already done.** [`Assets/Editor/Art/CorruptionEnemyBootstrap.cs:24-25`](Assets/Editor/Art/CorruptionEnemyBootstrap.cs:24) states: "Implemented so far: Labo (isPhaser), Salungat (isDecoy), Mantsa (stainsNearbyGlyphs), Takip (coversOwnGlyph) and Iligaw (spawnsMirrorDecoy + zigzag). The other twelve still walk". The spec's "only Labo and Salungat" and its Mantsa/Takip/Iligaw starting set are stale. **Twelve** enemies lack a mechanic.
- **C5 — Juan idle is no longer pending.** `sprite_prot_juan_idle-Sheet.png` landed in `f35de96d`. Open decision #4 in the progress file ("idle/attack mix art styles") is closed; confirm visually in Task 1.
- **C6 — The status docs are not in git.** `docs/audit/`, `docs/polish-pass-prompt.md` and `progress/` are all untracked. Task 0 fixes this before anything else can reference them durably.

## Global Constraints

Every task's requirements implicitly include this section.

- **Verify by playing, not by simulating.** A headless render or a statistical model shows something is *plausible*, not that it works. Where a claim can only be checked by playing, say so rather than implying it was verified.
- **Resolve rosters from level configs and `EnemyPool._enemyPrefab` wiring, never from a file listing.** The 17 corrupted enemies have no prefabs. The prefab folder is not the roster.
- **Both enemy rosters ship.** Colonial enemies appear in 11 of 15 levels; the corrupted set is the current design direction. Neither is dead.
- **Check sorting layers before trusting an occlusion metric.** Glyph badges render at `RenderOrder.EnemyGlyphBadge` (200), above enemies at `EnemyDefault` (0). A badge is never hidden by a body.
- **Run `docs/jira/validate-git-conventions.sh` on the branch name, PR title, and every commit subject before opening a PR.** CI enforces it. PR titles must begin with an uppercase letter or number — a conventional-commit-style title fails. Usage is `validate-git-conventions.sh <branch|commit|pr> <value>`; with no arguments it only prints usage, which is not a pass.
- **The convention validator exits `0` even when it fails** (verified 2026-09-14 with a negative control: `branch "Bad_Branch Name!!"` printed `Convention error: …` and still exited `0`). **Grep its output for `Convention error`; never branch on its exit code.** This is the same trap as the test runner's exit code — two of the project's three verification tools lie in their status codes.
- **No Claude attribution** on commits or PR bodies. No `Co-Authored-By` trailer, no "Generated with Claude Code" footer.
- **Unity batchmode dirties the worktree.** `-runTests` reserializes `Assets/.../TutorialFont.asset` and deletes `PerformanceTestRun*.json`. Revert that dirt before committing.
- **Never trust the test runner's exit code.** The PlayMode runner exits `2` on any inconclusive result while the XML still says `Passed`. Parse the XML. Also: batchmode exits `0` with no results XML when the Editor is already open — check for the XML's existence, not the code.
- **`ElInquisidorTest` is permanently inconclusive** because `Resources/Test/Level5_ElInquisidor_TestRig.asset` has never existed. That one failure is the accepted exception in the definition of done.
- **The Unity Editor being open blocks batch `-runTests` but enables the MCP relay.** Pick one per task; do not report "another instance" as a blocker without checking whether the Editor is deliberately in use.
- **Do not commit across workstreams.** Each task owns its own branch and its own file set.

## Definition of done for Phase 3

Level 1 plays end to end with no visual defect the owner has not accepted; every branch worth keeping is merged or explicitly written off; Levels 2–5 are authored, validated and playable; the full suite is green apart from `ElInquisidorTest`.

---

## Task 0: Put the status documents under version control

Nothing else in this plan can cite a durable path until this lands. Three untracked paths currently hold the project's entire status record.

**Files:**
- Add: `docs/audit/` (7 files incl. `STATUS-2026-09-13.md`, `AUDIT.md`, `BACKLOG.md`, `JIRA-MERGE.md`, two CSVs, `orchestrator-run-prompt.md`)
- Add: `docs/polish-pass-prompt.md`
- Add: `progress/2026-09-14-character-art-import.md`
- Add: `PHASE3-PLAN.md` (this file)

- [ ] **Step 1: Confirm nothing in these paths is secret or machine-local**

```bash
grep -rniE 'api[_-]?key|token|password|secret|Bearer ' docs/audit/ progress/ docs/polish-pass-prompt.md PHASE3-PLAN.md
```

Expected: no matches. If a match appears, stop and report it — do not commit that file.

- [ ] **Step 2: Check the CSVs are worth tracking**

`docs/audit/jira-import.csv` and `jira-import-final.csv` total ~86 KB of one-time Jira import data. If the owner considers them disposable, exclude them and say so in the commit body. Default: track them — they are the evidence behind `JIRA-MERGE.md`.

- [ ] **Step 3: Branch and validate the name**

```bash
git checkout -b docs/phase3-status-tracking
./docs/jira/validate-git-conventions.sh
```

- [ ] **Step 4: Stage and commit**

```bash
git add docs/audit/ docs/polish-pass-prompt.md progress/ PHASE3-PLAN.md
git commit -m "docs: track audit, polish-pass and phase 3 planning records"
```

- [ ] **Step 5: Verify the tree is clean and the commit subject passes**

```bash
git status --short && ./docs/jira/validate-git-conventions.sh
```

Expected: empty status output; validator passes.

**Done when:** `git status --short` is empty and the four paths are tracked on a branch that passes the convention validator.

---

## Task 1: Play Level 1 on the simulator and report defects (spec Step 1)

**This task changes no game code.** Its only deliverable is an evidence report. The spec is explicit: everything about enemy scale and wave pacing was validated by simulation and headless renders, never by playing. Treat all of it as unverified.

**Files:**
- Create: `progress/2026-09-14-level1-playtest.md`
- Modify: none

**Interfaces:**
- Consumes: `dev` @ `90410457` with Task 0 landed.
- Produces: a defect list that Task 1b (spun off only if the owner approves fixes) and the Phase 3 done-criteria both read.

- [ ] **Step 1: Open the simulator panel before building**

Call the iOS simulator control tool with `action: "attach"` first. It is cheap, opens instantly on a booted device, and surfaces the device-access prompt while the user is present. If it errors because nothing is booted, boot a device, then retry.

- [ ] **Step 2: Produce a current build**

`Builds/iOS-Sim/` is Xcode *source* generated 2026-09-06 — it predates every Phase 1–2 change and contains no `.app`. Do not launch it. Either rebuild from Unity for the iOS Simulator target, or build the existing Xcode project and confirm its `Data/` timestamp is newer than `f35de96d`. Record which path you took.

- [ ] **Step 3: Set `runInBackground` before measuring anything timing-related**

Play Mode stalls or crawls when unfocused unless `PlayerSettings.runInBackground` is set on the **macOS** target (the runtime flag alone is not enough; the iOS target hides the setting). Measure and record actual fps before trusting any judgement about wave pacing or input timing.

- [ ] **Step 4: Play Level 1 end to end and capture screenshots**

Cover, at minimum: the Ready screen and its ownership behaviour; dialogue; the tutorial steps; the active clue; slash feedback and VFX; Retry; Defeat; victory. Screenshot each.

- [ ] **Step 5: Answer the spec's two named questions explicitly**

Do enemy sizes feel right in a real wave, and does the fast-to-slow wave rhythm feel right? Answer from play, not from the occlusion model. Note that a prior conclusion — that hand-spaced mockups overstate viability — was itself derived by simulation; this is the first chance to check it by playing.

- [ ] **Step 6: Check the Phase 1–2 items still marked NOT VERIFIED**

Enemies walking with the new frames; Almanac/discovery cards; Juan's `Draw` firing on attack without disturbing idle (C5 — idle art has landed, so confirm the two no longer mix styles); `ProtagonistSlashVfx.prefab` still playing the **old** 3 draw frames (open decision #1); no new Animator warnings; Unity Console clean.

- [ ] **Step 7: Write the report**

Write `progress/2026-09-14-level1-playtest.md` with a defect table (severity, what you saw, screenshot, repro), a "verified working" list, and a "still not verified" list. Attach screenshots. **Fix nothing.**

- [ ] **Step 8: Commit and stop**

```bash
git add progress/2026-09-14-level1-playtest.md
git commit -m "docs: record Level 1 simulator playtest findings"
```

Then **stop and report to the owner.** Step 2 of the spec does not begin until they have seen this list and said which defects they accept.

**Done when:** a screenshot-backed defect report exists, the two named feel questions are answered from play, and the owner has been asked which defects to fix. Not when the defects are fixed.

---

## Task 2: Triage the stranded branches (spec Step 2, part 1)

Decision work only. No merges in this task.

**Files:**
- Create: `progress/2026-09-14-branch-recovery-triage.md`
- Modify: none

**Interfaces:**
- Consumes: corrections C1–C3 above.
- Produces: a per-branch verdict (`LAND` / `WRITE OFF` / `PARTIAL`) that Task 3 executes.

- [ ] **Step 1: Record the SALIN-102 write-off with evidence**

```bash
git cherry dev feature/SALIN-102-Write-Cutscene-Content-Between-Story-Levels | sort | uniq -c
```

Expected: `26 -`, zero `+`. Paste the output. Per C1, the spec's premise for this branch is wrong; the verdict is **WRITE OFF**, not "land it". Spot-check two of its cutscene assets by content against `dev` to confirm patch-id equivalence held, then state the write-off plainly.

- [ ] **Step 2: Enumerate every other branch whose remote is gone**

```bash
git branch -vv | grep ': gone\]'
```

For each, record commits ahead of `dev` and the `git cherry` split:

```bash
for b in $(git branch --format='%(refname:short)' | grep -v '^dev$'); do
  echo "$b: ahead=$(git rev-list --count dev..$b) new=$(git cherry dev $b 2>/dev/null | grep -c '^+')"
done
```

- [ ] **Step 3: Triage SALIN-93 by content, not by patch-id**

Patch-id says 13 commits are unapplied, but squashed or re-authored work would not show. Compare its new scripts against `dev`:

```bash
git diff --stat dev...feature/SALIN-93-level-1-guided-onboarding
git diff dev...feature/SALIN-93-level-1-guided-onboarding -- Assets/Scripts/UI/Level1WorldIntroController.cs
```

For each new file (`Level1WorldIntroController.cs`, the six new test files), ask: does `dev` already have an equivalent under another name? `dev` now ships `LevelReadyScreenController.cs`, `Level1TutorialStep_*.asset` and an onboarding sequence — much of SALIN-93's intent may already be live under different names.

- [ ] **Step 4: Assess the `Gameplay.unity` conflict cost honestly**

SALIN-93 rewrites `Assets/_Scenes/Gameplay.unity` by 6357 lines; `dev` has since rewritten the same scene. Do not plan to merge the scene. State whether the *scripts and tests* can be cherry-picked without the scene, and whether the scene changes are recoverable at all.

- [ ] **Step 5: Triage SALIN-205 and the boss-timing worktree**

SALIN-205 is 1 commit / 39 files and collides with `dev` on `Level2_Config.asset`–`Level5_Config.asset` (C3). It is content the project needs — provisional verdict **LAND**, executed in Task 4, not here. Also check `test/deterministic-boss-summon-timing` in `.claude/worktrees/bold-bardeen-b60246`.

- [ ] **Step 6: Write the triage document and stop**

One row per branch: commits ahead, new-vs-duplicate split, files that matter, conflict cost, verdict, and one line of reasoning. Commit it:

```bash
git add progress/2026-09-14-branch-recovery-triage.md
git commit -m "docs: triage stranded branches against dev"
```

Report the verdicts to the owner before Task 3.

**Done when:** every branch has a verdict backed by a `git cherry` or content comparison, and no branch is called "the reflog is the only copy" without that being shown.

---

## Task 3: Land what the triage says to land (spec Step 2, part 2)

Executes Task 2's verdicts. Scope is whatever Task 2 marked `LAND` or `PARTIAL` — most likely a cherry-pick of SALIN-93's scripts and tests without its scene.

**Files:** determined by Task 2. Do not widen beyond its verdict list.

**Interfaces:**
- Consumes: `progress/2026-09-14-branch-recovery-triage.md`.
- Produces: merged branches, and a written-off list recorded in the triage doc.

- [ ] **Step 1: Branch per recovered unit**

```bash
git checkout -b feature/recover-salin-93-onboarding-scripts
./docs/jira/validate-git-conventions.sh
```

- [ ] **Step 2: Cherry-pick only the approved paths**

```bash
git checkout feature/SALIN-93-level-1-guided-onboarding -- <exact paths from triage>
```

Use path-scoped checkout rather than a merge, so `Gameplay.unity` cannot come along by accident.

- [ ] **Step 3: Compile before testing**

Let Unity import and compile. Confirm the Console shows 0 errors. A recovered script referencing a type `dev` has since renamed will fail here, not in tests.

- [ ] **Step 4: Run the affected tests and parse the XML**

Run the EditMode suite. Parse the results XML — never the exit code (global constraints). Expected: green apart from `ElInquisidorTest`.

- [ ] **Step 5: Revert batchmode dirt, then commit**

```bash
git checkout -- Assets/Resources/Fonts/TutorialFont.asset 2>/dev/null
git status --short
git add <approved paths>
git commit -m "feat(onboarding): recover SALIN-93 world intro and tutorial progress"
```

- [ ] **Step 6: Open the PR**

Validate the branch name, PR title (must start uppercase) and every commit subject with `docs/jira/validate-git-conventions.sh` first. No Claude attribution in the body.

**Done when:** each `LAND` verdict is merged with a green targeted suite, each `WRITE OFF` is recorded with its evidence, and no branch is left in limbo.

---

## Task 4: Land the SALIN-205 Ugat narrative (spec Step 3, part 1)

**Files:**
- Merge from: `feature/SALIN-205-ugat-narrative-content` (worktree `salinlahi-worktrees/SALIN-205`, 1 commit, 39 files)
- Expect conflicts in: `Assets/ScriptableObjects/Levels/Level2_Config.asset` … `Level5_Config.asset`
- Adds: `docs/content/ugat-levels-2-5-narrative.md`, `Dialogue_Ugat02..05_*.asset` + metas

**Interfaces:**
- Consumes: `dev` with Task 3 landed.
- Produces: `Dialogue_Ugat*` assets and level-config dialogue references that Task 5 authors against.

- [ ] **Step 1: Sync the worktree and preview the conflict**

```bash
git -C ../salinlahi-worktrees/SALIN-205 fetch origin
git merge-tree $(git merge-base dev feature/SALIN-205-ugat-narrative-content) dev feature/SALIN-205-ugat-narrative-content | grep -c '<<<<<<<'
```

- [ ] **Step 2: Resolve the four level configs by field, not by hunk**

`dev` added active-clue fields to these assets; SALIN-205 added dialogue references. Both belong in the result. Merging by hunk will silently drop one side. Open each asset and confirm the merged YAML carries **both** sets of fields.

- [ ] **Step 3: Confirm no GUID was reissued**

Re-running an authoring tool reissues asset GUIDs and silently unwires references. Verify each `Dialogue_Ugat*.asset.meta` GUID matches the branch's, and that the level configs reference those exact GUIDs.

- [ ] **Step 4: Check against already-authored content first**

The team's Filipino content lives in `docs/content/`. Before accepting or editing any narrative copy, read what is already authored there — do not write replacement copy for text that already exists.

- [ ] **Step 5: Compile, run the suite, parse the XML, commit**

Revert batchmode dirt. Validate conventions. Open the PR.

**Done when:** SALIN-205 is merged, all four level configs carry both the active-clue and dialogue fields, no GUID changed, and the suite is green apart from `ElInquisidorTest`.

---

## Task 5: Author and validate the Level 2–5 configurations (spec Step 3, part 2)

Per SALIN-204. Fifteen open tickets are level configs and narrative (SALIN-144–158, 172, 173, 204, 205); the spec is explicit that unfinished levels outweigh any amount of feature polish.

**Files:**
- Modify: `Assets/ScriptableObjects/Levels/Level2_Config.asset` … `Level5_Config.asset`
- Modify: `Assets/ScriptableObjects/Levels/Level*_ChallengeSequence.asset`
- Reference: `docs/content/`, `docs/system/09_Test_Strategy_and_Acceptance_Criteria.md`

- [ ] **Step 1: Read SALIN-204 and the existing Level 1 config as the pattern**

Level 1 is the only fully authored level. Derive the required field set from it, not from the ScriptableObject class definition — the class permits fields the design does not use.

- [ ] **Step 2: Check precedent before declaring any field blocked**

If a value looks missing for Levels 2–5, check whether Level 1 has shipped it since day one before reporting it as a blocker.

- [ ] **Step 3: Respect the symbol-pool derivation**

`cumulativeSymbolPool` is derived from each symbol's `firstIntroductionLevelId`, and a bootstrap rewrites that field. Edit **both**, or the pool will silently revert.

- [ ] **Step 4: Mutate existing assets — never re-run an authoring tool that calls `CreateAsset`**

`CreateAsset` reissues the GUID and unwires every reference to it.

- [ ] **Step 5: Author each level, one commit per level**

Level 2, then 3, 4, 5. After each: compile, run the validator, play that level on the simulator, commit.

- [ ] **Step 6: Play Levels 2–5 end to end**

Per the global constraints, configuration that validates is not a level that plays. Screenshot each level's completion.

- [ ] **Step 7: Run the full suite and parse the XML**

Expected: green apart from `ElInquisidorTest`.

**Done when:** Levels 2–5 are authored, validated, and each has been played end to end on the simulator with a screenshot.

---

## Task 6: Corrupted-enemy signature abilities (spec Step 4) — GATED

**Do not start this task if Tasks 1–5 are open.** The spec says so explicitly, and requires it be flagged as feature work rather than polish. Flagging it here satisfies that requirement.

**Scope correction (C4):** the spec's starting set — Mantsa, Takip, Iligaw — is already implemented, along with Labo and Salungat. Per [`CorruptionEnemyBootstrap.cs:24-25`](Assets/Editor/Art/CorruptionEnemyBootstrap.cs:24), **five** of seventeen have mechanics and **twelve** do not. Re-derive the target list from that file's roster comments before writing anything, and ask the owner which twelve to prioritise — the spec's priority order no longer applies.

**Files:**
- Reference: `Assets/Editor/Art/CorruptionEnemyBootstrap.cs` (roster + ability designs in comments)
- Modify: per-enemy `EnemyDataSO` assets and the ability implementation path used by `isPhaser` / `isDecoy` / `stainsNearbyGlyphs` / `coversOwnGlyph` / `spawnsMirrorDecoy`

- [ ] **Step 1: Confirm Tasks 1–5 are all closed, and report to the owner before proceeding**
- [ ] **Step 2: Re-derive the twelve mechanic-less enemies from the roster, resolving membership from level configs and `EnemyPool._enemyPrefab`, not from the prefab folder**
- [ ] **Step 3: Get the owner's priority order for the twelve**
- [ ] **Step 4: For each enemy — write the failing EditMode test first, then the minimal implementation, then the play check**

**Done when:** the owner has approved a priority order and each implemented ability has a passing test plus a simulator play check. Not when the flags are set in data.

---

## Open decisions still owned by the project owner

Carried forward from `progress/2026-09-14-character-art-import.md`. None of them block Tasks 0–3.

1. `ProtagonistSlashVfx.prefab` still plays the old 3 draw frames rotated toward the target — keep, or author Juan-style brush-stroke frames? (Task 1 Step 6 will show what it looks like in play.)
2. Ragasa playable contract — stats, ability, spawn rules, roster registration, `EnemyData_Ragasa`, Almanac entry. Art is imported; nothing is wired.
3. Hati split/minion runtime — no implementation exists; the minion art is parked.
4. ~~Juan idle art~~ — **closed**, landed in `f35de96d` (C5). Confirm visually in Task 1.
5. Paragraph auto-fill — exact text progression, error behaviour, persistence and completion transition need one authoritative design before implementation.
6. Sprite-sheet identities — the owner should confirm Hati = three-mask tendril figure, minion = single-mask smaller figure, Iligaw = spiked diamond mask, Ragasa = quadruped beast. The `~/Downloads` frames zips are mislabeled across Hati/minions/Iligaw; the spritesheet PNG in each folder is the true identity.

## Risks

| Risk | Task | Mitigation |
|---|---|---|
| `Gameplay.unity` merge destroys `dev`'s Level 1 fix pass | 3 | Path-scoped checkout of scripts/tests only; never merge the scene |
| Level 2–5 config merge silently drops the active-clue fields | 4 | Resolve by field and inspect merged YAML, not by accepting hunks |
| A green suite hides UI wiring defects | 1, 5 | Tests assert strings, not pixels — the simulator play check is the gate |
| "0 issues" from a filter that matches nothing reads as a pass | 2, 3 | Use a negative control or a before/after delta on any scan |
| Batchmode reports exit 0 with no results | 3, 4, 5 | Assert the results XML exists and parse it |
| Re-running an authoring tool unwires references | 4, 5 | Mutate existing assets; never `CreateAsset` over a live one |
