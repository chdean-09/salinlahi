---
name: 05-implementation-review
description: Review one completed Salinlahi ticket implementation (ticket + plan + diff + validation evidence) before integration, returning PASS, PASS_WITH_NOTES, FIX_REQUIRED, or BLOCKED. Review only; never edits code or pushes.
---

# Implementation Review (Salinlahi)

Review one ticket's finished implementation in its worktree/branch. Prefer a reviewer context separate from the implementer (fresh subagent when available) so findings are evidence-based rather than assumed.

## Inputs

Ticket (acceptance criteria), its `<KEY>-implementation-plan.md` (lives in the **main checkout** root, not the worktree — the orchestrator supplies the absolute path), the full diff vs the branch's base, and the implementer's validation report (`PASS/FAIL/NOT RUN/BLOCKED/NOT APPLICABLE` per check).

## Review sequence

1. **Acceptance criteria** — each Given/When/Then satisfied by the diff, or explicitly deferred with reason.
2. **Plan conformance** — deviations exist? Justified and reported, or unexplained?
3. **Diff hygiene** — apply the AGENTS.md "Git Diff Review" checklist: unintended `.meta`/`.unity`/`.prefab`/`.asset`/Animator/input changes; `Packages/` or `ProjectSettings/` drift; generated files (`*.csproj`, `Library/`, `Temp/`, `Logs/`); secrets or absolute machine paths; unrelated formatting or line-ending churn; scope creep beyond the ticket.
4. **Unity safety** — serialized fields renamed without `FormerlySerializedAs`; lifecycle callbacks or Inspector-bound methods removed on static-reference evidence alone; `Resources.Load` path changes; `.meta`/GUID integrity.
5. **Tests** — new behavior has focused EditMode (`Assets/Tests/Editor/`) or PlayMode (`Assets/Tests/PlayMode/`) coverage per repo patterns; test names reference the ticket/backlog ID where they prove a criterion.
6. **Validation accuracy** — every claimed check actually ran; anything `NOT RUN`/`BLOCKED` is judged material or not. CI (`git-conventions.yml`) validates naming only — it is never evidence of compilation or tests.
7. **Integration risk** — flag touched high-collision files (see `03-parallel-ticket-safety` list) and serialized assets so the integrator can order and re-sync correctly.

## Verdict

- `PASS` — criteria met, clean diff, accurate validation.
- `PASS_WITH_NOTES` — mergeable; record non-blocking findings.
- `FIX_REQUIRED` — return numbered, actionable, blocking findings to the implementation stage. The review stage does not apply fixes itself.
- `BLOCKED` — review impossible (missing plan/diff/validation, or a dependency/product decision beyond the ticket); escalate to the orchestrator.

## Output contract

```
REVIEW SALIN-xxx: <verdict>
BLOCKING: 1) <finding + file:line> ...
NOTES:    - <non-blocking> ...
INTEGRATION RISK: <shared files / serialized assets touched, or none>
```
