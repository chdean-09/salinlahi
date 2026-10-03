# Git Delivery Quick Start

Jira linkage is optional. The required workflow is typed branches, Conventional Commit subjects, explicit verification, and a pull request to `dev`.

## 1. Know the Formats

```text
Branch:  docs/improve-ai-workflow
         feature/SALIN-123-improve-scene-loading

Commit:  docs(ai): improve delivery workflow
         fix(ui): SALIN-123 resolve null reference

PR:      Improve AI delivery workflow
         SALIN-123: Improve AI delivery workflow
```

- Branch types: `feature`, `bugfix`, `hotfix`, `chore`, `refactor`, `docs`, `test`, `spike`.
- Branch descriptions: lowercase kebab-case, 2–5 words, no more than 60 characters for the complete branch name.
- Commit types: `feat`, `fix`, `chore`, `refactor`, `docs`, `test`, `style`, `perf`, `ci`, `revert`; scope is optional.
- Jira keys: optional, but uppercase and well formed when present.

## 2. Install the Local Commit Hook

First confirm `.git/hooks/commit-msg` does not already exist. If it does, inspect and integrate it deliberately instead of overwriting it. When the path is absent, run from the repository root:

```bash
cp docs/jira/commit-msg-hook.sh .git/hooks/commit-msg
chmod +x .git/hooks/commit-msg
```

The hook resolves the repository root and delegates to `docs/jira/validate-git-conventions.sh`. Do not maintain a separate local regex.

## 3. Deliver a Change

1. Start from `dev` and create a typed task branch. A Jira key may be included but is not required.
2. Inspect the relevant code, tests, and Unity serialized consumers before editing.
3. Make the smallest coherent change and preserve unrelated worktree modifications.
4. Run the relevant verification, then review the complete diff before staging.
5. Stage only task-owned files and create a compliant Conventional Commit.
6. Push the task branch and open a pull request targeting `dev`.
7. Use a ready PR only when every required check is `PASS` or `NOT APPLICABLE`. Otherwise use a draft and document each `FAIL`, `NOT RUN`, or `BLOCKED` check.
8. Do not merge automatically.

For Codex, the exact request `ship this` authorizes the delivery actions above for the current task, including creating a compliant branch first when the current branch is `dev` or `main`. It does not authorize merging, force-pushing, bypassing validation, or staging unrelated files.

## 4. Validate Before Delivery

```bash
docs/jira/validate-git-conventions.sh branch "docs/improve-ai-workflow"
docs/jira/validate-git-conventions.sh commit "docs(ai): improve delivery workflow"
docs/jira/validate-git-conventions.sh pr "Improve AI delivery workflow"
```

`.github/workflows/git-conventions.yml` repeats these checks for the PR branch and title and for every commit subject in the PR's base-to-head range. It does not compile Unity or run Unity tests.

## 5. Fill In Verification and Impact

Use `.github/PULL_REQUEST_TEMPLATE.md` and report one of `PASS`, `FAIL`, `NOT RUN`, `BLOCKED`, or `NOT APPLICABLE` for:

- Unity compilation;
- Unity Console;
- Edit Mode tests;
- Play Mode tests;
- relevant manual regression.

Also state whether the diff affects `.meta` files/GUIDs, scenes, prefabs, ScriptableObjects or other `.asset` files, `Packages/`, or `ProjectSettings/`.

## Optional Jira Behavior

When a valid `SALIN-<number>` key is present, external Jira/GitHub integration may link the branch, commits, and PR and may run configured transitions. Ticketless work has no Jira transition. The external Jira automation and GitHub required-check settings are `NOT VERIFIED` by repository files.
