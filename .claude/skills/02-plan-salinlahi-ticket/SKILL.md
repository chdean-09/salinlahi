---
name: 02-plan-salinlahi-ticket
description: Plan one Salinlahi (SALIN) Jira ticket into an implementation plan at the main-checkout root, naming the files it will touch, the implementation effort, and the parallel-orchestration metadata the safety stage consumes. Planning only; never edits source, creates worktrees, commits, or writes to Jira.
---

# Plan One Salinlahi Ticket

Produce `<KEY>-implementation-plan.md` for exactly one ticket. Dispatched by `00-jira-parallel-orchestrator` as `salinlahi-planner` (Opus 5 High). The plan is the sole handoff to the implementer, the reviewer, and `03-parallel-ticket-safety` — everything they need must be in the file, not in your reply.

## Inputs

Ticket key + the orchestrator's dispatch (repo path, audit/source doc paths). Read the **live** Jira description via the Atlassian MCP (`cloudId 4b895a89-9e37-44f7-b69a-0b0a0bdee4b1`, project `SALIN`); ticket text is amended in place, so never plan from a cached summary.

## Write the plan to

`<repo-root>/<KEY>-implementation-plan.md` — the **main checkout** root. `.gitignore` has `/SALIN-*-implementation-plan.md`; it stays untracked. Worktrees cannot see it, so the orchestrator passes its absolute path onward.

## Sequence

1. **Restate acceptance criteria** from the live ticket, one line each, numbered. These are what the reviewer checks.
2. **Verify the premise against the code before planning the fix.** Ticket descriptions go stale. Establish, with `file:line`, what is actually true on `origin/dev` today. Report any criterion already satisfied, and any that is broken more or less widely than the ticket claims.
3. **Check whether the work already landed:** `git log -E origin/dev --oneline --grep "SALIN-<n>([^0-9]|$)"` and inspect adjacent merged PRs touching the same systems. Scope the plan to what genuinely remains; never re-plan merged work.
4. **Confirm every acceptance gate you propose actually passes on base today.** A gate already failing on `dev` for unrelated reasons (e.g. the campaign validator) is not an available gate — substitute record-baseline-then-diff and say why, or the reviewer reads the pre-existing failure as this ticket's regression.
5. **Plan the smallest coherent change** per AGENTS.md "Change Scope", in the correct assembly. Name each step with exact paths.
6. **Name the boundary against neighbouring tickets.** If another ticket owns a file or a field you would otherwise touch, say where this ticket stops and cite the other key.
7. **Validate the branch name** and record the exact command and exit code:
   ```bash
   bash docs/jira/validate-git-conventions.sh branch "<branch>"
   ```
   `type/SALIN-123-kebab-desc`, 2–5 slug words, ≤60 chars. Type must match the Jira issue type — a Bug takes `bugfix/`. Validate the commit subject and PR title forms too.

## Never invent missing content

Titles, ruling texts, spoken values, ruling owners, workbook cells, spec strings: if the source is not in the repo, **do not supply it from inference**. Mark that step `⚠️ CONTENT-BLOCKED`, name the exact evidence you searched, and escalate it in Risks. A plan that guesses content ships wrong content silently. Label conclusions `Observed` / `Inference` per AGENTS.md; cite `file:line` for anything load-bearing.

## Required plan sections

All of these, by these names. `03-parallel-ticket-safety` and `05-implementation-review` parse them; a missing metadata section stalls the wave.

| Section | Must contain |
|---|---|
| Acceptance criteria | numbered, from the live ticket |
| Current state | what is true on `dev` now, with `file:line` |
| Remaining scope | ordered steps, exact paths, `⚠️ CONTENT-BLOCKED` where applicable |
| Files the implementation will touch | modified / added / explicitly-not-touched |
| Validation | per-check, each labelled `PASS`/`FAIL`/`NOT RUN`/`BLOCKED`/`NOT APPLICABLE` when run |
| **Suggested Branch** | name + the validation command and its exit code |
| **Model Routing** | exactly one of `Low`, `Medium`, `High` + justification |
| **Parallel Orchestration Metadata** | expected files, expected systems, high-collision files touched, serialized assets touched, shared-foundation yes/no, integration risk, integration-order note |
| Risks and escalations | numbered, each with a disposition |
| Suggested commit / PR title | validated forms |

## Model Routing

Default **Low**. Choose higher only with a concrete, repo-grounded reason, and state it:

- **Low** — declarative data or doc edits, no new types, no control flow, hazards narrow and named.
- **Medium** — a wrong edit fails *silently* (no compile error, no validator error, no failing test); or the fix requires a judgement call the ticket does not settle; or the change must shrink as well as grow a serialized field.
- **High** — an algorithm to design, cross-system debugging, or a contract change with many consumers.

Ticket "size" estimates do not decide this; failure mode does.

## Escalate, do not decide

Contradictory or unimplementable acceptance criteria, a required source absent from the repo, a product or architecture call beyond the ticket, or a conflict with another ticket's ownership. Record it in Risks with a disposition and surface it in your reply so the orchestrator sees it without opening the file.

## Output contract (your reply, not the file)

```
PLANNED SALIN-xxx → <absolute plan path>
BRANCH:   <name> (validate exit 0)
EFFORT:   Low | Medium | High — <one-line reason>
FILES:    <count + the headline paths>
CONFLICTS: <serialized assets / high-collision files, or none>
ESCALATIONS: 1) ... 2) ...   (or: none)
```

Planning only: no worktree, no source edit, no commit, no push, no Jira transition.
