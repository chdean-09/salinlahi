# Jira × GitHub Integration

This directory contains all documentation and tools for the SALIN Jira-GitHub workflow integration.

## 📚 Documentation

| File | Purpose | Read Time |
|------|---------|-----------|
| [`QUICK-START.md`](QUICK-START.md) | **Start here!** Setup guide and quick reference | 5 min |
| [`TEAM-HANDBOOK.md`](TEAM-HANDBOOK.md) | Complete workflow guide with examples | 15 min |

## 🛠️ Tools

| File | Purpose |
|------|---------|
| [`commit-msg-hook.sh`](commit-msg-hook.sh) | Git hook for validating commit messages locally |

## ⚡ Quick Reference

```
Branch:  feature/SALIN-11-implement-scene-loader
Commit:  feat(scene-loader): SALIN-11 add async loading
PR:      SALIN-11: Implement scene loader with fade stub
```

## 🔗 Related Files

- `.github/workflows/jira-validation.yml` — GitHub Actions PR validation
- `.github/PULL_REQUEST_TEMPLATE.md` — PR template with Jira fields

## 🎯 The Golden Rule

> **Every branch, every commit, and every pull request must reference a Jira issue key.**

No exceptions. This enables automatic linking, keeps metrics accurate, and gives everyone full visibility.

---

*Last updated: 2024*
