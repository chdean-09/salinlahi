---
name: 01-jira-ticket-discovery
description: Retrieve Salinlahi (SALIN) Jira tickets, interpret Blocks-link dependencies and acceptance gates, and classify Jira-level readiness as READY/BLOCKED/UNKNOWN for the parallel orchestrator. Read-only against Jira; makes no code, git, or readiness-of-code claims.
---

# SALIN Jira Ticket Discovery

Query live Jira state and return a compact ticket + dependency report. This skill never implements, branches, commits, or judges code availability — Jira `Done` does not mean the code is merged; that check belongs to orchestration/integration.

## Stable configuration (verified 2026-08-23)

- Atlassian MCP tools; `cloudId: 4b895a89-9e37-44f7-b69a-0b0a0bdee4b1` (jnwync.atlassian.net), project `SALIN`.
- Statuses: `To Do` / `In Progress` / `Done`. Transition IDs (all issue types): To Do=`11`, In Progress=`21`, Done=`31`.
- Sprint field: `customfield_10020` (board 2). Known sprint IDs: S5=143, S6=144, S7=145, S8=146, S9=147, S10=148. Story points: `customfield_10016`.
- Dependency link type: `Blocks` — `inwardIssue` blocks the ticket ("is blocked by"); `outwardIssue` is blocked by it. No other dependency link types are in use; `Relates` is informational only.
- Epics are parents (SALIN-126–132); epic status is unreliable (epics stay To Do) — never derive readiness from a parent epic.

## Query pattern

Large results overflow to a file — always request minimal `fields` and extract with `jq`:

```
searchJiraIssuesUsingJql(cloudId, jql, fields: ["summary","status","assignee","issuelinks","customfield_10020","customfield_10016","parent"], responseContentFormat: "markdown")
```

Typical scopes: `project = SALIN AND sprint IN ("SALIN Sprint 7")`, or explicit `key IN (...)`. When the result is saved to a file, extract with `jq '.issues.nodes[] | ...'`.

## Repository-specific interpretation rules

1. **Acceptance gates are not start blockers.** Ticket descriptions carry `## Final acceptance gates` (typically SALIN-188 language/cultural review) stating it "is not a hard implementation start gate". Treat gate links as `GATE`, not `BLOCKED`.
2. **Hard dependencies** come from `Blocks` links plus the description's `## Dependencies` section (TW-* IDs map to SALIN keys via ticket descriptions).
3. **Known bad link data:** SALIN-178↔180 and SALIN-188↔192 Blocks links are recorded backwards (evidence in SALIN-180 comments). Until fixed in Jira, invert them.
4. **Duplicates:** SALIN-194, 195, 196, 197 are titled "[Duplicate - Do Not Use]" and marked Done. Never treat them as satisfying anything; live equivalents are 168, 171, 177, 193.
5. **Board automation:** editing the sprint field resets status to To Do. After any sprint move, re-read and restore status.
6. **Campaign parents vs slices:** parent tasks (e.g. 172/173/176/183/184, 177, 190) explicitly delegate closable scope to per-era slice tickets (198–206 pattern). A parent being In Progress does not block its slices.

## Readiness classification

For each ticket in scope:

- `READY` — status not Done, and every hard blocker is status `Done` (code-in-dev verification is the orchestrator's job; report blocker keys so it can check).
- `BLOCKED` — at least one hard blocker not Done; name each `key ← blocker (status)`.
- `UNKNOWN` — links/description contradict each other or required fields are unreadable; state why.

## Output contract

Return only this compact block (plus optional one-line notes):

```
TICKETS <scope>
READY:   SALIN-xxx (assignee, pts) ...
BLOCKED: SALIN-yyy ← SALIN-zzz (To Do) ...
GATES:   SALIN-xxx gated-by SALIN-188 (acceptance only)
UNKNOWN: ...
UNBLOCKS: SALIN-zzz → [SALIN-a, SALIN-b]   # what completing each READY/blocker frees
```
