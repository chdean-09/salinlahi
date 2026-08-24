# Jira Parallel Development Workflow

Runtime entry point for executing SALIN tickets in parallel with AI workers. Complements `AGENTS.md` (repo rules) and `docs/AI_WORKFLOW.md` (tooling). The detailed procedures live in the skills; this file is sequence, handoffs, and authorization.

## Prerequisites

Skills `01`, `03`, `05`, `06` and `00` live in `.claude/skills/` and ship with this repository. **`02-plan-salinlahi-ticket` and `04-implement-salinlahi-ticket` are user-level skills** in `~/.claude/skills/` and are *not* in the repo — without them the pipeline has holes at the planning and implementation stages. Copy them from a teammate who has them before running the workflow.

Also required: `gh` authenticated, the Atlassian MCP connector authorized, and Unity `6000.3.9f1` installed for the integration gate.

## Invocation

Normal operation is one instruction to the agent:

> Use the **00-jira-parallel-orchestrator** skill for `<scope>`.

where `<scope>` is a sprint (`SALIN Sprint 7`), a ticket list, or a JQL filter. Add **"dry run"** to plan without touching code, git, GitHub, or Jira. Add **"merges authorized"** to allow the run to merge green PRs; otherwise every ticket stops at `READY_FOR_MERGE`.

## Sequence

```
1. 01-jira-ticket-discovery    → READY / BLOCKED / GATES / UNBLOCKS
2. verify code-in-dev          (Jira Done ≠ merged; git log origin/dev --grep)
3. 02-plan-salinlahi-ticket    → <KEY>-implementation-plan.md per READY ticket (parallel)
4. 03-parallel-ticket-safety   → SAFE / SOFT_CONFLICT / BLOCKED + integration order
5. worker allocation           → 1 worktree per ticket under ../salinlahi-worktrees/
6. 04-implement-salinlahi-ticket→ parallel, one worker per ticket (≤4)
7. 05-implementation-review    → PASS / PASS_WITH_NOTES / FIX_REQUIRED / BLOCKED
8. 06-ticket-integration       → serialized: validate → commit (assignee identity)
                                 → sync dev → revalidate → push → PR → Jira comment
                                 → merge (only if authorized) or READY_FOR_MERGE
9. refresh                     → fetch dev, re-sync soft-conflict branches,
                                 Jira transition Done, unlock next wave → repeat 1
```

Parallel phase: 3, 6, 7. Controlled (serialized) phase: 8–9.

## Handoffs

| Producer | Consumer receives |
|---|---|
| Discovery | ticket table + blocker keys + unblock map |
| Planner | plan file with Parallel Orchestration Metadata (files/systems/risk) |
| Safety | runnable groups, conflicts with reasons, order hint |
| Implementer | diff + validation statuses + deviations/discoveries |
| Reviewer | verdict + blocking findings + integration risk |
| Integrator | PR/merge state + unblock candidates + resync list |

## Fixed conventions (do not rediscover)

Base branch `dev`; merge commits only (never squash). Branch/commit/PR formats validated by `bash docs/jira/validate-git-conventions.sh {branch|commit|pr} "<value>"`. PR body = `.github/PULL_REQUEST_TEMPLATE.md`, honestly filled. CI = naming lint only — it never proves compilation or tests. Plans stay at repo root, uncommitted. Commits authored as the ticket's assignee per `.claude/team-map.json` (team-approved policy); pushes use the operator's own credentials.

## Stop and escalate to a human

- contradictory or unimplementable acceptance criteria
- behavioral merge conflict, or any conflict inside `.unity`/`.prefab`/`.asset`
- product/architecture decision not covered by the ticket
- assignee missing from team-map, or authorization missing for a required action
- two fix-review rounds without a PASS
- everything remaining in scope is BLOCKED

## Known environment caveats

- Fresh worktrees show ~11 phantom CRLF-modified files: stage explicit paths only, never `git add -A`.
- Local UnityYAMLMerge mergespec is broken: serialized-asset conflicts are resolved in Unity or escalated, never blind-merged.
- Jira board automation resets status to To Do when a sprint field changes — restore status after sprint moves.
- SALIN-194–197 are Done duplicates ("Do Not Use"); SALIN-178/180 and 188/192 Blocks links are recorded backwards until fixed.
