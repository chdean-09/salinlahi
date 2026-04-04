# Salin Lahi — Developer Workflow Handbook
**Jira × GitHub Integration Guide**

| Jira Project | GitHub Repo | Main Branch |
|--------------|-------------|-------------|
| `SALIN` | `salinlahi` | `main` |

---

## The Golden Rule

> **Every branch, every commit, and every pull request must reference a Jira issue key.**

No code should exist in GitHub that cannot be traced back to a Jira ticket. This enables automatic linking, keeps metrics accurate, and gives everyone full visibility into what changed and why.

---

## Quick Start (30-Second Checklist)

```
[ ] My branch name contains SALIN-XX
[ ] Every commit message contains SALIN-XX
[ ] My PR title starts with SALIN-XX:
[ ] The PR description template is filled in
[ ] CI is passing
```

---

## 1. Day-to-Day Workflow

### Before Writing Code

1. Check that a Jira issue exists (e.g., `SALIN-1`, `SALIN-11`)
2. Note the issue key
3. Ensure the issue is assigned to you with clear acceptance criteria
4. Verify story points are estimated

### Starting Work

```bash
# Pull latest main
git checkout main && git pull

# Create your branch
git checkout -b feature/SALIN-11-implement-scene-loader

# Jira will auto-detect the branch and show it in the Development panel
# Ticket auto-transitions to "In Progress" (if Automation is configured)
```

### During Development

```bash
# Make atomic commits — one logical change per commit
git commit -m "feat(scene-loader): SALIN-11 add async scene loading"
git commit -m "test(scene-loader): SALIN-11 add unit tests for LoadScene"

# Push regularly — at least daily
git push origin feature/SALIN-11-implement-scene-loader
```

### Opening a Pull Request

**Title format:** `SALIN-XX: Imperative description`

```
SALIN-11: Implement SceneLoader with async loading and fade stub
```

**Jira will auto-transition to "In Review"**

---

## 2. Branch Naming

### Pattern

```
{type}/{SALIN-XX}-{short-description}
```

### Branch Types

| Type | When to Use | Example |
|------|-------------|---------|
| `feature/` | New functionality | `feature/SALIN-11-implement-scene-loader` |
| `bugfix/` | Non-urgent bug fix | `bugfix/SALIN-102-fix-null-reference` |
| `hotfix/` | Critical production fix | `hotfix/SALIN-99-payment-timeout` |
| `chore/` | Deps, config, build | `chore/SALIN-78-upgrade-unity-version` |
| `refactor/` | Code restructuring | `refactor/SALIN-61-extract-auth-service` |
| `docs/` | Documentation only | `docs/SALIN-55-api-reference-guide` |
| `test/` | Test additions | `test/SALIN-88-unit-tests-player` |
| `spike/` | Research/exploration | `spike/SALIN-34-evaluate-shader-graph` |

### Rules

- **Lowercase only** — no uppercase letters in the branch name itself
- **Hyphens only** — no underscores, no spaces
- **Issue key is ALWAYS UPPERCASE** (`SALIN-11`, not `salin-11`)
- **Description:** 2–5 words, imperative, kebab-case
- **Max 60 characters** total

```
❌  main/SALIN-11
❌  SALIN11-feature
❌  feature/salin-11
❌  feature/SALIN-11_scene_loader

✅  feature/SALIN-11-implement-scene-loader
```

---

## 3. Commit Messages

### Pattern

```
{type}({scope}): {SALIN-XX} {description}
```

### Commit Types

| Type | Meaning |
|------|---------|
| `feat` | New feature or functionality |
| `fix` | Bug fix |
| `chore` | Build process, tooling, deps — no prod code change |
| `refactor` | Code restructuring — no behavior change |
| `docs` | Documentation changes only |
| `test` | Adding or correcting tests |
| `style` | Formatting, missing semicolons — no logic change |
| `perf` | Performance improvement |
| `ci` | CI/CD configuration changes |
| `revert` | Reverts a previous commit |

### Valid Examples (SALIN)

```
feat(scene-loader): SALIN-11 add async scene loading with LoadSceneAsync
fix(ui): SALIN-102 resolve null reference in MainMenuController
chore(deps): SALIN-78 upgrade Unity to 2022.3 LTS
refactor(core): SALIN-61 extract audio logic into AudioManager
docs(readme): SALIN-55 add build instructions for Android
test(scene-loader): SALIN-11 add unit tests for LoadScene method
```

### Atomic Commit Principle

Each commit = one logical change. A reviewer should understand the change entirely from commit message + diff.

```
❌  "work in progress"
❌  "more fixes"
❌  "stuff"

✅  "fix(ui): SALIN-102 resolve null reference on button click"
```

---

## 4. Pull Requests

### Title Format

```
{SALIN-XX}: {imperative short description}
```

### Examples

```
SALIN-11: Implement SceneLoader with async loading and fade stub
SALIN-102: Fix null reference exception in UI controller
SALIN-78: Upgrade Unity to 2022.3 LTS
```

### PR Checklist (Author + Reviewer)

- [ ] PR title starts with a Jira issue key (`SALIN-XX:`)
- [ ] Branch name follows the naming convention
- [ ] All commits reference the issue key
- [ ] PR description is fully filled in (use template)
- [ ] Jira Development panel shows the linked branch and PR
- [ ] Tests pass and coverage is not reduced
- [ ] No commented-out code, no debug logs left in
- [ ] Author has self-reviewed their own diff

### Scope and Size

- **One PR per Jira issue**
- Aim for **under 400 lines** changed per PR
- Anything **over 800 lines** should be split

---

## 5. Jira Ticket Structure

### Issue Type Hierarchy

| Level | Type | Scope |
|-------|------|-------|
| 1 | Epic | Major feature area or milestone |
| 2 | Story | Deliverable unit of user value. One branch + one PR |
| 3 | Task | Concrete technical step |
| 3 | Bug | Defect. Always gets `bugfix/` or `hotfix/` branch |
| 4 | Subtask | Granular work inside a Story or Task |

### Required Fields

| Field | Requirement |
|-------|-------------|
| Summary | Imperative verb phrase, max 60 chars |
| Description | Context, background, acceptance criteria |
| Acceptance Criteria | Bulleted testable conditions (required for Stories/Bugs) |
| Story Points | Fibonacci: 1/2/3/5/8/13 only |
| Assignee | Set when moved to In Progress |
| Epic Link | Required for all Stories and Tasks |
| Priority | Blocker / Critical / High / Medium / Low |

### Example: SALIN-11

```
Summary: Implement SceneLoader.cs with Async Loading and Fade Stub

Description:
SceneLoader.cs Singleton that loads scenes asynchronously via LoadSceneAsync. 
Exposes LoadScene(string sceneName) as core internal method, with convenience 
wrappers as public API. Includes fade-in/fade-out canvas group stub.

Acceptance Criteria:
- SceneLoader inherits Singleton<T> and persists via DontDestroyOnLoad
- LoadScene(string sceneName) internally, plus convenience wrappers (LoadMainMenu, etc.)
- Async loading via LoadSceneAsync, no main thread freeze
- Concurrent load calls are no-ops with logged warning
- CanvasGroup fade stub exists (alpha 0→1 on load, 1→0 on complete)
- LevelSelect.unity created and added to Build Settings
```

---

## 6. Workflow Status Transitions

| Status | Trigger | Developer Action |
|--------|---------|------------------|
| To Do | Issue created/groomed | No branch yet |
| In Progress | Branch created with issue key | `feature/SALIN-XX-...` pushed |
| In Review | PR opened with issue key | PR title starts with `SALIN-XX:` |
| QA / Testing | PR approved | Merged to staging/test env |
| Done | PR merged to main | Automated via Jira Automation |
| Blocked | Manual | Add comment explaining blocker |

### Automation Rules (Jira)

Configure in **Project Settings → Automation**:

1. Branch created with issue key → **Transition to In Progress**
2. Pull request opened with issue key → **Transition to In Review**
3. Pull request merged to `main` → **Transition to Done**
4. Issue transitioned to Done → **Post comment with merged PR link**

---

## 7. Metrics

| Metric | Measures | Shows 0 When... |
|--------|----------|-----------------|
| PR Cycle Time | PR open → merged | No PRs linked to issues |
| Lead Time | First commit → deploy | No GitHub deployment events |
| Deploy Frequency | Deployments per day/week | CI/CD not emitting events |
| Work Item Age | Ticket open → Done | No issue keys in commits/PRs |

**Note:** Metrics are never backfilled — they build from integration forward.

---

## Quick Reference Card

| Task | Correct Convention |
|------|-------------------|
| Feature branch | `feature/SALIN-XX-short-description` |
| Bug branch | `bugfix/SALIN-XX-short-description` |
| Hotfix branch | `hotfix/SALIN-XX-short-description` |
| Feature commit | `feat(scope): SALIN-XX description` |
| Fix commit | `fix(scope): SALIN-XX description` |
| PR title | `SALIN-XX: Imperative description` |
| Jira summary | Imperative verb + outcome, max 60 chars |

---

## FAQ

**Q: What if I'm working on something small that doesn't need a ticket?**
A: Create a ticket anyway. Every code change needs traceability. Use `chore/` type for small tasks.

**Q: Can I have multiple tickets in one branch?**
A: No. One branch = one ticket. Split the work if needed.

**Q: What if I forgot to include the ticket key in my commit?**
A: For unpushed commits: `git commit --amend -m "feat(ui): SALIN-11 add button"`
For pushed commits: Leave it, but ensure future commits include it.

**Q: The ticket is SALIN-1, but my branch is `feature/salin-1-...`. Is that ok?**
A: No — always uppercase. Use `feature/SALIN-1-...`. Jira keys are case-sensitive.

---

*Team Workflow Handbook v1.0 — Jira × GitHub Integration*
