---
name: 00-jira-parallel-orchestrator
description: Coordinate the full Salinlahi Jira parallel-development lifecycle — discovery, readiness, planning, safety analysis, isolated parallel implementation, review, and controlled integration into dev — by delegating to the specialist skills. Use for "run the Jira parallel workflow for <scope>" requests.
---

# Jira Parallel Orchestrator (Salinlahi)

Runtime brain: owns state, scheduling, worker allocation, dependency refresh, and failure isolation. Delegates everything else — never re-implements a specialist's job.

## Specialist skills

| Stage | Skill |
|---|---|
| Jira state + dependencies | `01-jira-ticket-discovery` |
| Per-ticket plan | `02-plan-salinlahi-ticket` (plan saved at repo root `<KEY>-implementation-plan.md`, uncommitted) |
| Concurrency decision | `03-parallel-ticket-safety` |
| Implementation | `04-implement-salinlahi-ticket` (in the ticket's worktree) |
| Review | `05-implementation-review` (independent subagent) |
| Commit/push/PR/merge | `06-ticket-integration` (serialized, one at a time) |

Config: `.claude/team-map.json` (Jira↔GitHub↔git identity, worker limit 4, worktree root).

## Ticket states

`BLOCKED → READY → PLANNING → PLANNED → RUNNING → REVIEW → (FIX_REQUIRED→RUNNING) → READY_FOR_INTEGRATION → PR_OPEN → READY_FOR_MERGE → MERGED`, plus `FAILED`, `UNKNOWN`. Keep state as a simple in-conversation table; no external store.

## Wave loop

1. **Discover** (skill) → READY/BLOCKED/GATES/UNBLOCKS for the scope.
2. **Verify code availability** — a Jira-`Done` blocker counts only if its code is actually in base: `git log -E origin/dev --oneline --grep "SALIN-<n>([^0-9]|$)"` (the boundary matters: plain `SALIN-20` would false-match SALIN-201…206), or its PR shows merged. Jira Done without merged code keeps the dependent `BLOCKED`.
3. **Plan** all READY tickets needing plans — parallel subagents, one ticket each; reuse an existing valid `<KEY>-implementation-plan.md` after revalidating it against current code, rather than replanning. Plans live in the **main checkout** root and are untracked — worktrees cannot see them, so always hand workers and reviewers the plan's absolute path.
4. **Safety** (skill) → parallel groups, SOFT_CONFLICTs, integration-order hint.
5. **Allocate workers** — max 4 concurrent (team-map `workerLimit`). Route each ticket to its Jira assignee's worker; per ticket create isolation using the plan's **Suggested Branch** (validate it first: `bash docs/jira/validate-git-conventions.sh branch "<branch>"`):
   ```bash
   git worktree add ../salinlahi-worktrees/SALIN-<n> -b <suggested-branch> origin/dev
   ```
   One worktree per ticket; workers never touch another worktree. **Scratch files must be namespaced per ticket** (e.g. `<scratchpad>/SALIN-<n>/…`) — concurrent workers sharing one scratchpad directory have silently corrupted each other's files (observed: one worker appended its source paths to another's `runtime.rsp`, breaking a compile run). Known repo quirk: fresh worktrees show ~11 phantom CRLF-modified files — workers ignore them and never `git add -A`. Unity `Library` import in worktrees is expensive: code+EditMode-testable work needs no Editor; defer Editor/PlayMode validation to the main checkout or a deliberate per-worktree Unity run.
6. **Implement in parallel** (skill, one subagent per ticket, plan attached). Jira: transition ticket to In Progress (id `21`) when its worker starts.
7. **Review** each finished ticket (skill, independent subagent). **Never run a Unity gate and a review concurrently on the same worktree** — batchmode mutates the working tree (regenerates `.meta`, deletes `InitTestScene<guid>.unity` and `PerformanceTestRun*.json`, reserializes assets), and a reviewer reading it mid-run sees phantom churn and misreads it as a live Editor session. Gate first, then review, and hand the reviewer the gate results so it does not re-report `NOT RUN`. `FIX_REQUIRED` → back to the same worker with findings, max 2 fix rounds, then mark `FAILED` and escalate. `BLOCKED` → orchestrator decision.
8. **Integrate serially** (skill) in this order: dependency-unlock value → shared-foundation first → conflict risk → freshness. After each merge: `git fetch origin`; re-check SOFT_CONFLICT branches (`git merge-tree --write-tree origin/dev <branch>`); revalidate/re-review affected branches; transition merged tickets to Done (id `31`) — **except gate-flagged tickets** (discovery `GATES`, e.g. SALIN-188 acceptance evidence): leave those In Progress and comment "merged; awaiting gate evidence" instead; refresh the discovery picture for newly unblocked tickets.
9. **Next wave** — return to step 1 until the scope has no executable work; then clean up merged worktrees (`git worktree remove`).

## Failure isolation

A failed/blocked ticket never stops unrelated workers. Its dependents stay `BLOCKED`; everything else proceeds. Preserve the failed worktree for inspection; report it.

## Identity

Workers and integration author commits as the ticket's Jira assignee per team-map `_policy` (team-approved). PR assignee/reviewer come from the same map. Pushes use this workstation's credentials only.

## Authorization boundaries

- Merging PRs: only when the run's invocation explicitly says to merge; default stop is `READY_FOR_MERGE`.
- Jira writes: status transitions for tickets this run works on; nothing else.
- Escalate, don't decide: contradictory acceptance criteria, behavioral merge conflicts, product/architecture calls beyond a ticket, all-work-blocked.

## Status block (emit after every state change batch)

```
READY: ...   PLANNING/PLANNED: ...   RUNNING: SALIN-x(worker/assignee) ...
REVIEW: ...  INTEGRATION QUEUE: 1. ... 2. ...
PR_OPEN/READY_FOR_MERGE: ...  MERGED: ...  BLOCKED: x ← y   FAILED: ...
```

## Dry run

On "dry run": execute steps 1–4 plus worker/integration-order planning only. No worktrees, no code changes, no commits, no pushes, no PRs, no Jira transitions.
