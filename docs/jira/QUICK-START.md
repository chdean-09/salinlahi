# 🚀 SALIN Jira-GitHub Workflow — Setup Guide

## For Team Members: Quick Setup (2 minutes)

### 1. Read the Handbook
📖 **Start here:** `docs/jira/TEAM-HANDBOOK.md`

This contains everything you need to know about branches, commits, and PRs.

### 2. Install Git Hook (Recommended)

This will validate your commit messages locally before pushing:

```bash
# From repo root
cp docs/jira/commit-msg-hook.sh .git/hooks/commit-msg
chmod +x .git/hooks/commit-msg
```

### 3. Use the Quick Reference

**Print this and keep it handy:**

```
┌─────────────────────────────────────────────────────────────┐
│                    SALIN QUICK REFERENCE                     │
├─────────────────────────────────────────────────────────────┤
│  BRANCH:  feature/SALIN-11-implement-scene-loader           │
│  COMMIT:  feat(scope): SALIN-11 add async loading           │
│  PR:      SALIN-11: Implement scene loader                  │
├─────────────────────────────────────────────────────────────┤
│  TYPES: feature/ bugfix/ hotfix/ chore/ refactor/ docs/     │
│         test/ spike/                                        │
├─────────────────────────────────────────────────────────────┤
│  30-SEC CHECKLIST:                                          │
│  [ ] Branch has SALIN-XX                                    │
│  [ ] Commits have SALIN-XX                                  │
│  [ ] PR title starts with SALIN-XX:                         │
│  [ ] PR description filled in                               │
│  [ ] CI passing                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## For Repo Admins: Enable Automation (5 minutes)

### 1. Enable GitHub Actions

The validation workflow is already in `.github/workflows/jira-validation.yml`. 

Just push it to main:
```bash
git add .github/workflows/jira-validation.yml
git commit -m "chore(ci): SALIN-X add PR validation workflow"
git push origin main
```

This will automatically check every PR for:
- ✅ Title starts with `SALIN-XX:`
- ✅ Branch follows naming convention

### 2. Configure Jira Automation

In Jira, go to **Project Settings → Automation** and create these rules:

**Rule 1: Branch Created → In Progress**
- **Trigger:** Branch created
- **Condition:** Branch name contains `SALIN-`
- **Action:** Transition issue to "In Progress"

**Rule 2: PR Opened → In Review**
- **Trigger:** Pull request opened
- **Condition:** PR title contains `SALIN-`
- **Action:** Transition issue to "In Review"

**Rule 3: PR Merged → Done**
- **Trigger:** Pull request merged to `main`
- **Condition:** PR title contains `SALIN-`
- **Action:** Transition issue to "Done"

**Rule 4: Done → Comment with PR Link**
- **Trigger:** Issue transitioned to Done
- **Action:** Add comment: "Merged in {triggerIssue.pr.url}"

### 3. Verify GitHub-Jira Integration

1. Go to Jira → Project Settings → GitHub
2. Connect your repository
3. Verify the integration is active

---

## Troubleshooting

### PR validation is failing

Check the PR title format:
```
❌ "Implement scene loader"
❌ "SALIN-11 implement scene loader"
✅ "SALIN-11: Implement scene loader"
```

### Commits are being rejected

Check your commit message:
```
❌ "work in progress"
❌ "SALIN-11"
✅ "feat(scene-loader): SALIN-11 add async loading"
✅ "SALIN-11: Add scene loader"
```

### Jira isn't showing my branch/PR

- Ensure your branch name contains `SALIN-XX` in UPPERCASE
- Ensure your PR title starts with `SALIN-XX:`
- Wait 1-2 minutes for sync (sometimes delayed)
- Check the Development panel on the right side of the Jira issue

### I need to bypass validation (emergency hotfix)

Repo admins can override by adding the label `skip-validation` to the PR.

---

## File Structure

```
.github/
├── workflows/
│   └── jira-validation.yml    # PR validation automation
├── PULL_REQUEST_TEMPLATE.md   # Updated with Jira format
docs/
├── jira/
│   ├── TEAM-HANDBOOK.md       # Complete workflow guide
│   ├── QUICK-START.md         # This file
│   └── commit-msg-hook.sh     # Local commit validation
```

---

## Need Help?

1. Check `docs/jira/TEAM-HANDBOOK.md` for detailed examples
2. Look at recent PRs for examples of correct formatting
3. Ask in team chat or ping a maintainer

---

*Setup Guide v1.0*
