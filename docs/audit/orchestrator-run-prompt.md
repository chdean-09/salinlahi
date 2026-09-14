# Prompt — first Jira parallel run after the 2026-09 audit import

Paste the block below as one message. It runs Sprint 8 only, dry run first, and stops for confirmation before touching code.

---

Use the **00-jira-parallel-orchestrator** skill for scope `sprint = 146` (SALIN Sprint 8). Merges are **not** authorized; stop every ticket at `READY_FOR_MERGE`.

**Pre-flight (Jira writes, do these before discovery):** add `Blocks` links so the pre-audit Ugat and Pamana stories wait for the foundation they now depend on (see `docs/audit/JIRA-MERGE.md`, Assignments and Import log):

- SALIN-233 blocks SALIN-144, SALIN-145, SALIN-146
- SALIN-226 blocks SALIN-147, SALIN-152, SALIN-207
- SALIN-247 blocks SALIN-147, SALIN-152
- SALIN-217 blocks SALIN-155
- SALIN-229 blocks SALIN-207

Skip any link that already exists. Report the links created.

**Then run steps 1–4 as a dry run** and stop. Expected wave 1 is SALIN-213, 216, 218, 224 only; SALIN-140 and SALIN-144–147 must show `BLOCKED` with the new links. If anything else is READY, show me why before continuing.

**Constraints for the real run (after I confirm):**

- Do not create a worktree for SALIN-205; `../salinlahi-worktrees/SALIN-205` already holds uncommitted work on an unpushed branch. Leave it untouched and exclude 205 from this run.
- Reused root plans for SALIN-140 and SALIN-182 predate the audit rulings (save-wiring split; combat-variety rescope). Revalidate both against the amended ticket descriptions before reuse; replan if they disagree.
- Plans without a **Model Routing** effort level run at Low unless revalidation says otherwise.
- Workers route by Jira assignee. Jeff owns every Sprint 8 foundation ticket except SALIN-224, so his worker will serialize; that is expected. Do not reassign to balance load unless I say so.
- Do not widen the scope to Sprints 9–10 or "all open" in this run.

After each wave emit the status block, and stop when Sprint 8 has no executable work.
