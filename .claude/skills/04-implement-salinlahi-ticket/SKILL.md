---
name: 04-implement-salinlahi-ticket
description: Implement one Salinlahi (SALIN) ticket inside its own git worktree from that ticket's implementation plan, leaving the work uncommitted with an honest per-check validation report for review and integration. Never commits, pushes, opens PRs, or touches another ticket's worktree.
---

# Implement One Salinlahi Ticket

Execute exactly one ticket's plan in exactly one worktree. Dispatched by `00-jira-parallel-orchestrator` as `salinlahi-worker-low|medium|high` — the effort is already pinned by the agent definition; do not re-argue it.

## Inputs

Ticket key, its worktree path (`../salinlahi-worktrees/SALIN-<n>`), and the **absolute path** to `<KEY>-implementation-plan.md` in the main checkout. The plan is not inside your worktree and never will be — read it from the path given. If the plan is missing or unreadable, stop and report `BLOCKED`; do not plan for yourself.

## Parallel Worker Mode — hard boundaries

Other workers are running right now in sibling worktrees. Violating any of these corrupts their work, not just yours.

- **Stay in your worktree.** Never read-modify-write in another `../salinlahi-worktrees/SALIN-*`, and never in the main checkout. The main checkout is the orchestrator's.
- **Never commit, push, open a PR, or merge.** `06-ticket-integration` commits with the assignee's identity. You hand over a dirty tree — that is the expected state.
- **Never `git add -A`.** Fresh worktrees show ~11 phantom CRLF-modified files. Stage nothing; if you must inspect, use explicit paths.
- **After any Unity run in a worktree, audit the tree before reporting.** Observed 2026-09-12 on SALIN-224: a batch-mode gate left **32** tracked files modified — far past the ~11 the line above warns about — spanning `Char_*`, `Dialogue_*`, `Era_*`, `CampaignConfig_RevisedV1.asset`, untouched level configs, and three `ProjectSettings/*.asset`. Thirty-one were line-ending-only, but **one was a genuine 528-line regeneration of `Assets/Resources/Fonts/TutorialFont.asset`** (TextMeshPro glyph mark-positioning records) that would have ridden silently into the commit. Separate real changes from churn with `git diff --numstat` — churn reports `0 0` — revert anything you did not intend, and list the exact paths the integrator should stage.
- **Namespace your scratch files** under `<scratchpad>/SALIN-<n>/`. Workers sharing one scratch directory have silently corrupted each other's files — an observed incident appended one worker's source paths into another's `runtime.rsp` and broke a compile run.
- **No Jira writes.** The orchestrator owns transitions.

## Sequence

1. **Read the plan fully before editing anything.** Re-read the live Jira ticket too; if the plan contradicts the current description, stop and report — do not reconcile it yourself.
2. **Confirm the starting state** the plan asserts. If the plan's `Current state` no longer matches the worktree (base moved, another ticket landed), report the drift before proceeding.
3. **Execute the plan's steps in order**, smallest coherent change, correct assembly. Honour every "explicitly not touched" entry in the plan's file list.
4. **Respect a `⚠️ CONTENT-BLOCKED` step.** Implement everything around it; leave it undone and report it. Never substitute invented content — titles, spoken values, ruling text, spec strings — for a missing source.
5. **Add the focused test the plan names**, under `Assets/Tests/Editor/` (EditMode) or `Assets/Tests/PlayMode/`. Name it so it references the ticket or backlog ID when it proves a criterion.
6. **Validate** (below), then **review your own diff** against the AGENTS.md "Git Diff Review" checklist: stray `.meta`/`.unity`/`.prefab`/`.asset` churn, `Packages/`/`ProjectSettings/` drift, generated files, secrets or absolute machine paths, line-ending churn, anything outside scope.

## Unity safety (non-negotiable)

A static C# search is not proof anything is unused — Unity invokes lifecycle callbacks, and `[SerializeField]` fields, UnityEvents, Animator events, and `Resources.Load` paths are all invisible to it. Preserve every `.meta` file and its GUID. Before renaming a serialized field, check existing assets and add `UnityEngine.Serialization.FormerlySerializedAs`. Do not change GUIDs, file IDs, execution order, tags/layers, input bindings, or build-scene membership as incidental cleanup.

Serialized-asset edits (`.asset`, `.prefab`, `.unity`) are serialized *object* changes, not text edits — the local UnityYAMLMerge mergespec is broken, so keep the edit minimal and exactly as scoped.

## Verification — report honestly

**No repository-owned command-line Unity test or build command has ever been verified. Do not fabricate one.** Unity `Library` import in a fresh worktree is expensive, so a pure data or documentation change may legitimately defer Editor work.

**But never hand over C# you have not compiled.** If your change adds or edits any `.cs` file, a Unity batch-mode run in your worktree is part of your job, not the integrator's — it is supported and has been done successfully here. Compile, run the EditMode suite, and report real numbers. Deferring the gate on new code ships unverified compilation into review, and the reviewer cannot gate what you did not run. The tree-audit rule above is how you clean up after that run; it is not a reason to skip it.

**Two Unity traps that fake a pass. Both have bitten this repo.**

- **`-runTests` combined with `-quit` exits 0 after the asset refresh without running a single test, and writes no results XML.** On exit code alone that reads as a green suite. Drop `-quit` when running tests; it is fine with `-executeMethod`. Always confirm you have a real results file with a `<test-run>` total before reporting a number.
- **Generators rewrite assets during the test run.** `RevisedCampaignAssetTests.LoadCampaign()` calls `RevisedCampaignBootstrap.Run()` in every test, and that bootstrap writes `Assets/ScriptableObjects/**` back to disk — `BackfillSymbolCatalog()` replaces whole `spokenValues` lists, `AuthorLevelOne()` rewrites Level 1's focus words. So a data-only edit can be **silently reverted by the very suite meant to prove it**, with everything green and the Inspector showing the old value. If your change edits data that any generator also authors, fix the generator too, and **re-read the affected files off disk after the suite finishes**. A passing suite is not evidence here; the post-run file contents are.

Label every check with exactly one of:

| Label | Means |
|---|---|
| `PASS` | the check ran here and passed |
| `FAIL` | the check ran here and failed |
| `NOT RUN` | it was not executed |
| `BLOCKED` | the environment prevented it |
| `NOT APPLICABLE` | e.g. gameplay tests for a docs-only change |

Never write `PASS` for a suite you did not actually run. CI (`git-conventions.yml`) lints naming only and is never evidence of compilation or tests. If the plan substituted a record-baseline-then-diff gate because a validator already fails on `dev`, report both the baseline and the diff so the reviewer does not read a pre-existing failure as your regression.

## Deviations and discoveries

Any departure from the plan gets reported with its reason — a plan step that turns out wrong is a finding, not a failure. Report anything you learn that changes another ticket's picture (an unrecorded file overlap, a stale premise, a hazard the plan missed).

## Fix rounds

`FIX_REQUIRED` comes back with numbered findings; address each one and say what changed per finding. Two rounds is the cap — if a finding survives a second round, stop and report `FAILED` with the state of the worktree rather than continuing.

## Stop and report instead of deciding

Contradictory acceptance criteria, a required source absent from the repo, a product or architecture call beyond the ticket, a conflict inside a serialized asset, or work that would require touching another ticket's files.

## Output contract

```
IMPLEMENTED SALIN-xxx (worktree <path>, effort <level>)
CHANGED:   <explicit paths, grouped modified / added>
VALIDATION: <check> — PASS | FAIL | NOT RUN | BLOCKED | NOT APPLICABLE   (one line each)
DEVIATIONS: <plan step + what differed + why>   (or: none)
DISCOVERIES: <facts affecting other tickets>    (or: none)
INCOMPLETE: <criteria not met + why>            (or: none)
STATE: uncommitted in worktree, ready for review
```

Work left **uncommitted**. No push, no PR, no merge, no Jira transition.
