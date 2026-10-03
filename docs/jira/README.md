# Git Conventions and Optional Jira Linkage

This directory owns Salinlahi's shared branch, commit-subject, pull-request-title, and optional Jira-linkage rules. A Jira ticket is **not required** to create or push a branch, commit work, or open a pull request.

## Files

| File | Purpose |
|---|---|
| [`QUICK-START.md`](QUICK-START.md) | Short setup and delivery reference |
| [`TEAM-HANDBOOK.md`](TEAM-HANDBOOK.md) | Full conventions, verification, and PR workflow |
| [`validate-git-conventions.sh`](validate-git-conventions.sh) | Single validator used locally and in CI |
| [`commit-msg-hook.sh`](commit-msg-hook.sh) | Tracked commit hook that delegates to the validator |

Related repository files:

- `.github/workflows/git-conventions.yml` validates the task branch, PR title, and every commit subject in the PR range.
- `.github/PULL_REQUEST_TEMPLATE.md` records verification and Unity asset/configuration impact.
- `AGENTS.md` defines what Codex may do when told `ship this`.
- `docs/AI_WORKFLOW.md` explains the human and AI-assisted delivery workflow.

## Formats

| Item | Without Jira | With optional Jira |
|---|---|---|
| Branch | `docs/improve-ai-workflow` | `feature/SALIN-123-improve-scene-loading` |
| Commit | `docs(ai): improve delivery workflow` | `fix(ui): SALIN-123 resolve null reference` |
| Pull request | `Improve AI delivery workflow` | `SALIN-123: Improve AI delivery workflow` |

If a Jira-looking prefix is present, it must use uppercase `SALIN-<number>` syntax in the exact position shown. Malformed or lowercase keys fail validation. Ticketless work does not trigger Jira linking or transitions.

## Validator Interface

```bash
docs/jira/validate-git-conventions.sh <branch|commit|pr> <value>
```

Exit codes are `0` for valid input, `1` for a convention failure, and `2` for invalid invocation. Change convention rules in this validator first; do not duplicate regexes in hooks, workflows, or prompts.
