---
name: 06-ticket-integration
description: Safely integrate one reviewed Salinlahi ticket branch into dev — sync, conflict check, validate, commit with the assigned developer's git identity, push, open the PR, and stop at READY_FOR_MERGE unless merging is explicitly authorized. One ticket at a time.
---

# Ticket Integration (Salinlahi)

Integrate exactly one ticket whose review verdict is `PASS` or `PASS_WITH_NOTES`. Base branch: `dev` (repo `chdean-09/salinlahi`). GitHub via `gh` CLI (verified working).

## Preconditions

Implementation complete; review verdict recorded; branch/worktree path known; no other integration in flight (integrations are serialized).

## Identity (team-approved policy — see `.claude/team-map.json` `_policy`)

Commits are authored as the ticket's assigned developer. Look up the assignee in `.claude/team-map.json` and commit with:

```bash
git -c user.name="<git.name>" -c user.email="<git.email>" commit -m "<subject>"
```

Pushes always use this workstation's own authenticated credentials. Never ask for a teammate's keys, passwords, or tokens. If the assignee is not in the map, stop and ask.

## Sequence

Run every step from the ticket's worktree. The branch arrives with **uncommitted** work (workers never commit), so commit comes first — a dirty tree blocks `git merge`.

1. **Validate conventions** before creating anything:
   ```bash
   bash docs/jira/validate-git-conventions.sh branch "<branch>"
   bash docs/jira/validate-git-conventions.sh commit "<subject>"
   bash docs/jira/validate-git-conventions.sh pr "<title>"
   ```
   Branch: `type/SALIN-123-kebab-desc` (2–5 words, ≤60 chars). Commit: `feat(scope): SALIN-123 description`. PR title: `SALIN-123: Sentence case title`.
2. **Stage explicitly.** Only task-owned paths — never `git add -A` (phantom CRLF files, plans, scratch). Excluded always: `*-implementation-plan.md`, `.DS_Store`, `Library/ Temp/ Logs/ UserSettings/`, generated `*.csproj`/`*.slnx`, unrelated dirty files.
3. **Commit** with the assignee identity (`git -c user.name=... -c user.email=... commit`).
4. **Sync base:** `git fetch origin`, then pre-check conflicts without touching the worktree:
   `git merge-tree --write-tree origin/dev <branch>` — a conflict header means resolution is needed.
5. **Classify conflicts:** `CLEAN` → continue. `MECHANICAL` (adjacent lines, imports, both-added docs) → resolve, revalidate. `BEHAVIORAL` (same logic changed both sides, or any `.unity`/`.prefab`/`.asset` conflict — local UnityYAMLMerge tooling is broken, so serialized conflicts are behavioral by default) → return the ticket to review/implementation; do not resolve blind.
6. **Merge dev into the branch** (merge, not rebase — pushed branches), then **revalidate** if the sync pulled in changes touching the ticket's files: re-run the ticket's focused tests; if behavior-adjacent, send back for re-review.
7. **Push** → **PR** to `dev` with `gh pr create`, body generated from `.github/PULL_REQUEST_TEMPLATE.md` and filled honestly: verification table uses only `PASS/FAIL/NOT RUN/BLOCKED/NOT APPLICABLE`; Unity-impact table lists every touched serialized path. Draft PR if any relevant check is `FAIL`/`NOT RUN`/`BLOCKED`. Set assignee/reviewer from team-map GitHub usernames.
8. **Comment the PR link on the Jira ticket** (existing team convention): `addCommentToJiraIssue` — "Implementation is complete and up for review: <PR url>" plus a one-line verification summary.
9. **CI:** only `validate-conventions` exists (branch/PR-title/commit-subject lint). Wait for it; a green check proves naming only — say so, never imply tested code.
10. **Merge rule:** merge only when the current invocation explicitly authorizes merging. Use `gh pr merge <n> --merge` (merge commit — never squash; squash breaks stacked branches). Otherwise stop and report `READY_FOR_MERGE`.

## Post-merge report (for the orchestrator)

```
INTEGRATED SALIN-xxx: MERGED <sha> | READY_FOR_MERGE PR#<n>
BASE: refresh required (origin/dev advanced)
UNBLOCK CANDIDATES: <keys whose blocker this was>
RESYNC: <open branches touching the same files, or none>
```
