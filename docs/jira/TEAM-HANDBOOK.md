# Git and Optional Jira Team Handbook

This handbook defines Salinlahi's repository-side delivery conventions. It applies whether or not a task has a Jira ticket.

## Source of Truth

`docs/jira/validate-git-conventions.sh` is the single executable source of truth. It is called by:

- `docs/jira/commit-msg-hook.sh` for local commit subjects;
- `.github/workflows/git-conventions.yml` for PR branch names, PR titles, and every commit subject in the PR range.

The interface is:

```bash
docs/jira/validate-git-conventions.sh <branch|commit|pr> <value>
```

Exit `0` means valid, `1` means the value violates a convention, and `2` means the invocation is invalid. Update the shared validator before changing examples or enforcement; do not copy its regexes elsewhere.

## Branch Names

Accepted forms:

```text
type/description
type/SALIN-123-description
```

Rules:

- Allowed types: `feature`, `bugfix`, `hotfix`, `chore`, `refactor`, `docs`, `test`, `spike`.
- The description is lowercase kebab-case with 2–5 words.
- The complete branch name is at most 60 characters.
- `dev` and `main` are exempt long-lived branches.
- A Jira key is optional. A Jira-looking prefix must be uppercase `SALIN-<number>`; lowercase or malformed variants are rejected.

Examples:

```text
feature/add-save-recovery
bugfix/SALIN-123-fix-menu-focus
docs/improve-ai-workflow
```

## Commit Subjects

Accepted forms:

```text
type: description
type(scope): description
type: SALIN-123 description
type(scope): SALIN-123 description
```

Allowed types are `feat`, `fix`, `chore`, `refactor`, `docs`, `test`, `style`, `perf`, `ci`, and `revert`. Scope is optional and uses lowercase letters, numbers, or hyphens. The description must not be empty.

Examples:

```text
feat: add save recovery
docs(ai): improve delivery workflow
fix(ui): SALIN-123 resolve null reference
```

Git-generated `Merge ...` and `Revert "..."` subjects are accepted. A conventional `revert(scope): description` subject is also accepted.

## Pull-Request Titles

Accepted forms:

```text
Improve AI delivery workflow
SALIN-123: Improve AI delivery workflow
```

The descriptive title starts with an uppercase letter or number. A Jira prefix is optional; when supplied, it must be uppercase `SALIN-<number>: ` followed by the title.

Pull requests target `dev` by default. Use another base only when the task explicitly requires it.

## Delivery Sequence

Use this order for every change:

1. **Verify** — run the checks relevant to the changed code and Unity state.
2. **Review** — inspect the complete diff and confirm only task-owned files are intended.
3. **Commit** — stage only task-owned files and use a validated subject.
4. **Push** — push the typed task branch. A Jira ticket is not a prerequisite.
5. **Pull request** — open against `dev`, fill out the template, and stop before merge.

If work is still on `dev` or `main`, create a compliant task branch before committing. Do not mix unrelated dirty-worktree changes into the delivery commit.

### Ready versus draft

Create a ready pull request only when all required checks are `PASS` or `NOT APPLICABLE`. Create a draft when any relevant check is:

- `FAIL` — the check ran and failed;
- `NOT RUN` — the check was not executed;
- `BLOCKED` — the environment prevented execution.

List every unresolved check in the PR. A draft is not permission to hide known failures.

### `ship this` contract for Codex

The exact instruction `ship this` authorizes Codex to verify, review, create a branch when needed, stage only task-owned files, commit, push, and open a pull request to `dev`. It never authorizes Codex to:

- merge the pull request;
- force-push or rewrite published history;
- bypass the shared validator or required checks;
- stage unrelated or pre-existing worktree changes;
- call a draft ready while relevant checks remain unresolved.

## Unity Verification and Impact Reporting

The PR template requires explicit status for:

- Unity compilation;
- Unity Console review;
- Edit Mode tests under `Assets/Tests/Editor/`;
- Play Mode tests under `Assets/Tests/PlayMode/`;
- relevant manual regression from `docs/system/09_Test_Strategy_and_Acceptance_Criteria.md`.

Use only `PASS`, `FAIL`, `NOT RUN`, `BLOCKED`, or `NOT APPLICABLE`. Never write “tests pass” unless the named suite actually ran and passed. No repository-owned command-line Unity test or build command has been verified; do not invent one.

Review and report whether the PR changes:

- `.meta` files or GUID relationships;
- scenes (`.unity`);
- prefabs (`.prefab`);
- ScriptableObjects or other `.asset` files;
- `Packages/manifest.json` or `Packages/packages-lock.json`;
- anything in `ProjectSettings/`.

Serialized content requires Unity-aware review. Static C# references cannot reveal all Inspector assignments, UnityEvents, animation events, Resources paths, or asset references.

## Optional Jira Linkage

Jira keys are optional in branches, commits, and pull-request titles. No Jira issue is required before pushing a branch or opening a PR.

When a valid key is present, external Jira/GitHub integration may link development activity and run configured status transitions. Jira-dependent automation must run only for work carrying a valid optional key. Ticketless work does not link to a Jira issue and does not trigger Jira transitions.

The repository does not prove the current external Jira automation or GitHub branch-protection settings; both are `NOT VERIFIED`. If the team requires the convention workflow before merge, configure `Git Convention Validation` as a required GitHub check outside the repository.

## Local Hook

Install the tracked hook source into the current checkout:

```bash
cp docs/jira/commit-msg-hook.sh .git/hooks/commit-msg
chmod +x .git/hooks/commit-msg
```

The installed `.git/hooks/commit-msg` file is local and is not committed. The tracked hook resolves the repository root, reads the first subject line, and delegates to `docs/jira/validate-git-conventions.sh`.

If a hook already exists, inspect and integrate it deliberately; do not overwrite it blindly.

## CI Scope

`.github/workflows/git-conventions.yml` runs for pull-request open, edit, synchronize, and reopen events. It checks:

- the head branch (`dev` and `main` are exempt long-lived names);
- the pull-request title, passed through an environment variable;
- every commit subject in the base-to-head range.

The workflow validates naming only. It does **not** compile Unity, inspect the Console, run Edit Mode or Play Mode tests, validate builds, or prove scene/prefab/asset integrity.

## Review Checklist

Before requesting review, confirm:

- the branch, commit subjects, and PR title pass the centralized validator;
- any Jira key is valid and consistently placed, or Jira is marked `NOT APPLICABLE`;
- the PR targets `dev` unless another base was explicitly required;
- verification statuses and evidence are complete;
- Unity serialized/configuration impact is stated explicitly;
- unrelated files and local credentials/configuration are absent;
- the PR is draft when relevant checks are unresolved;
- no automatic merge is requested or performed.

## Troubleshooting

### A ticketless name fails

Jira is optional, but the surrounding convention is not. Check the allowed branch/commit type, branch word count and length, commit colon/spacing, and PR title capitalization.

### A Jira-prefixed name fails

Use uppercase `SALIN-<number>` and the exact separator for the item:

- branch: `SALIN-123-`;
- commit: `SALIN-123 `;
- PR: `SALIN-123: `.

Lowercase keys and malformed prefixes are intentionally rejected rather than treated as ordinary description text.

### Local and CI validation disagree

Confirm the checkout contains the current `docs/jira/validate-git-conventions.sh`, then reinstall the tracked hook source. The hook and workflow must delegate to the validator; neither should contain an independent convention regex.

### A convention workflow passes but Unity is broken

That is outside this workflow's scope. Inspect Unity compilation and the Console, run the relevant Edit Mode/Play Mode tests, and report the result separately in the PR template.
