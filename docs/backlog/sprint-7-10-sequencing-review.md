# Sprint 7–10 Sequencing Review & Backlog Placement

Generated 2026-08-23, after Sprint 5 and Sprint 6 merged to `dev` (head `718b0c8`).
Source: SALIN Jira (`jnwync.atlassian.net`), Blocks-link graph + sprint/story-point fields.

---

## 1. Placement of the seven unsprinted items

| Issue | Pts | Epic | Gated on | Recommended sprint | Why |
|---|---|---|---|---|---|
| **SALIN-140** Persist completion, memories, unlocks atomically | 8 | 127 | 136 (S7), 142 (S7 ✅) | **Sprint 8** | Largest chokepoint in the remaining plan — blocks 139, 148, 160, 177, 190. SALIN-148 opens Sprint 9, so 140 cannot slip past S8. |
| **SALIN-177** Add revised-content and flow regression coverage | 13 | 127 | 140 only (170/171/174 ✅) | **Sprint 8** | Campaign-wide parent of SALIN-201 (S7 Level-1 slice). Coverage must exist before Ugat L2–5 (S8) and Ugnayan (S9) land. ⚠️ Its twin **SALIN-196 is marked Done and flagged "[Duplicate - Do Not Use]"** — don't read that as coverage existing. |
| **SALIN-164** Read and control the experience comfortably | 5 | 132 | 176 ✅ — **unblocked now** | **Sprint 9** | Blocks SALIN-193 (S10 release review). Must precede S10. |
| **SALIN-160** Review learned characters, restored words, memories | 8 | 131 | 140; 170/173/175 ✅ | **Sprint 9** | Only real gate is 140 (S8). |
| **SALIN-161** Review and recall complete restored words | 8 | 131 | 160, 188 (S5), 191 (S7) | **Sprint 9** (late) | Chains off 160. Same epic — keep them together. Move to S10 if 160 slips. |
| **SALIN-139** Replay completed levels without changing canonical journey | 3 | 127 | 140 | **Sprint 9** | Blocks nothing. Lowest-pressure item; defer to relieve S8. |
| **SALIN-190** Run per-era content and progression regression | 8 | 127 | 140, **158 (S10)** | **Sprint 10** | Cannot start until the final Pamana story (158) lands. Must precede 193. |

### Resulting load

| Sprint | Open pts now | + placements | New total |
|---|---|---|---|
| S7 | 31 | — | 31 |
| S8 | 52 | 140 (8), 177 (13) | **73 ⚠️** |
| S9 | 31 | 164 (5), 160 (8), 161 (8), 139 (3) | 55 |
| S10 | 52 | 190 (8) | 60 |

**Sprint 8 is overloaded at 73 pts** against a 52–57 pt norm. 140 is non-negotiable there. To make room, move **SALIN-182** (Align combat variety, 8 pts — blocked only by 180 ✅) or **SALIN-186** (Synchronize system documentation, 3 pts) to Sprint 9, which has slack.

---

## 2. Sequencing problems found in Sprints 7–10

### 2.1 🔴 Sprint 7 — acceptance is Done before its prerequisites

```
SALIN-192 "Accept the Level 1 vertical slice"   → Done
  is blocked by SALIN-135 (To Do)  ← unstarted
  is blocked by SALIN-201 (To Do)  ← unstarted

SALIN-189 "Playtest the Level 1 vertical slice" → Done
  is blocked by SALIN-192, 204, 205, 206  ← 205/206 still To Do (Sprint 8)
```

Level 1 was accepted and playtested in Jira while its combat-feedback story, its regression coverage, and two of its content tasks are unstarted — and while the Level 1 code itself only merged to `dev` today. Downstream, **SALIN-163** and **SALIN-191** both list 189 as their only blocker, so they now read as unblocked on a false premise.

**Fix:** reopen SALIN-192 and SALIN-189 until 135, 201, 205, 206 close.

### 2.2 Sprint 7 — correct internal order

Two independent chains, neither currently sequenced on the board:

```
A:  136 → 137                       (136 is unblocked now: 134/143/171/174 all ✅)
B:  135 → 141 → 201 → 192 → 189 → {163, 191}
```

PR #89 (SALIN-141) sits mid-chain B behind SALIN-135, which has no branch. Holding it remains correct.

### 2.3 🔴 Sprint 8 — SALIN-203 depends on a Sprint 9 story

```
SALIN-203 "Ugat slice: run content and progression regression"  [Sprint 8]
  is blocked by SALIN-147 (S8) ✓
  is blocked by SALIN-148 (S9) ✗  ← Ugnayan Level 6, next sprint
```

An **Ugat** regression task cannot legitimately require an **Ugnayan** story. Either the 148 link is wrong (most likely) or 203 belongs in Sprint 9. As written, Sprint 8 cannot close.

Correct Sprint 8 order otherwise:

```
{204 ✅, 205, 206} → 145 → 144 → 146 → 147 → 203
185 (after S7's 201) ·  182 (after 180 ✅) ·  186 (after 170/171 ✅)
```

Note SALIN-144 is Level **3** and SALIN-145 is Level **2** — the chain 145 → 144 → 146 → 147 is correct despite the numbering.

### 2.4 Sprint 9 — chain is correct, but the sprint is hollow

```
148 → 150 → 151 → 149 → 152      = Levels 6, 7, 8, 9, 10 ✓
```

The chain verifies cleanly. The problem is what's missing: **Sprint 9 has no config, narrative, asset, or regression tasks.** Sprint 8 carries SALIN-204/205/206 (author configs / produce narrative / integrate assets) plus 203 for Ugat Levels 2–5. Sprint 9 has five stories and nothing else — 31 pts against Sprint 8's 57.

The Ugnayan equivalents of 204/205/206 do not exist as tickets. Sprint 10 has the same gap for Pamana Levels 11–15.

### 2.5 🔴 Sprint 10 — SALIN-157 is scheduled far too late

```
SALIN-157 "Hear each required syllable at the moment of learning"  [Sprint 10]
  is blocked by 175 ✅, 199 ✅, 188 (S5, In Progress)
```

Its blockers are effectively Sprint 5 work. This is a core learning-loop affordance — audio at the moment a syllable is taught — and it is currently scheduled after Level 14. Levels 1–14 would ship without it. It belongs in **Sprint 7**, not Sprint 10.

### 2.6 Sprint 10 — otherwise correct

```
153 → 154 → 155 → 156 → 158      = Levels 11, 12, 13, 14, 15 ✓
158 → 190 → 193                  (once 190 is placed here)
193 also needs 164 (place in S9), and 162/165/179 ✅
```

---

## 3. 🔴 Systemic issue: campaign parents closed while their scope is unbuilt

These are all **Done** and in **no sprint**:

| Issue | Pts | Scope |
|---|---|---|
| SALIN-172 | 13 | Author and validate the 15 level configurations |
| SALIN-173 | 13 | Produce revised narrative and memory content |
| SALIN-176 | 13 | Integrate revised art, audio, and UI assets |
| SALIN-183 | 8 | Implement learning metrics, rewards, mastery, Results |
| SALIN-184 | 13 | Author the three Paglimot mastery encounters |

Every Level-1 slice ticket says explicitly: *"Closably scoped Level 1 delivery slice of campaign parent SALIN-172 … **The parent remains open for Levels 2–15**."* The parents are closed anyway.

Consequence: **SALIN-148, 149, 150, 151, 152, 154, 155, 156, 158 all list SALIN-172 as a blocker.** Because 172 reads Done, the graph reports every Sprint 9 and Sprint 10 story as content-ready when configurations for Levels 6–15 have not been authored. This is the same class of error as the Sprint 9/10 missing-slice-tasks gap in §2.4 — and it is why that gap is invisible on the board.

**Fix:** reopen 172, 173, 176, 183, 184, or split per-era child tasks (Ugnayan / Pamana equivalents of 204/205/206) into Sprints 9 and 10.

Separately, **SALIN-194/195/196/197** are Done and titled "[Duplicate - Do Not Use]". Live versions: 168, 171, **177**, **193**.

---

## 4. Actions

### ✅ Applied 2026-08-23 (verified in Jira)

| Change | Issues |
|---|---|
| Reopened to **To Do** | SALIN-192, SALIN-189 |
| Reopened to **In Progress**, then placed in **Sprint 7** at the team's request | SALIN-172, SALIN-173, SALIN-176, SALIN-183, SALIN-184 |
| Moved Sprint 10 → **Sprint 7** | SALIN-157 |
| Placed in **Sprint 8** | SALIN-140, SALIN-177 |
| Placed in **Sprint 9** | SALIN-164, SALIN-160, SALIN-161, SALIN-139 |
| Placed in **Sprint 10** | SALIN-190 |

Confirmation that the campaign parents were closed prematurely, from their own
descriptions (read after reopening): SALIN-172 — *"Parent campaign task … SALIN-198 covers
Level 1 and SALIN-204 covers Ugat Levels 2–5. Continue later eras in additional slices."*
SALIN-190 — *"Parent per-era QA task. SALIN-203 owns the closable Ugat run; **create later
slices for Ugnayan and Pamana**."* The Ugnayan and Pamana slices do not exist as tickets.

**Board automation warning.** Setting the Sprint field on these five silently reset their status
from In Progress back to **To Do**. The In Progress state was re-applied afterwards and verified.
Anyone moving an issue between sprints on this board should re-check its status afterwards — the
sprint edit and the status are not independent.

**Sprint 7 is now ~96 open points** (≈36 before these five, +60). The five are campaign parents
whose remaining scope runs to Level 15, and whose delivery slices sit in Sprints 8–10, so Sprint 7
cannot close on them. They are placed here as visible owners of the Levels 2–15 gap rather than as
sprint-closable work; per-era slices (§3) remain the way to make that scope closable.

Also noted while applying: SALIN-140's description states that *"Regression and QA coverage
(SALIN-177 and SALIN-201) consumes this behavior as validation evidence; it is **not** an
implementation prerequisite."* So 177 may start before 140 completes — same-sprint placement
in S8 is safe, and 177 could move to S9 if S8 capacity forces it.

### ☐ Still outstanding — need a human decision

1. **Remove the SALIN-203 → SALIN-148 link, or move 203 to Sprint 9** (§2.3). Left alone: it
   is unclear whether the link is wrong or the sprint is, and either edit changes scope.
2. **Rebalance Sprint 8** — now 73 pts. Move SALIN-182 (8) or SALIN-186 (3) to Sprint 9 (§1).
3. **Create the Ugnayan and Pamana slice tasks** for Sprints 9 and 10 — the equivalents of
   SALIN-204/205/206 (config, narrative, assets) and SALIN-203 (regression) (§2.4, §3).
4. **SALIN-204 is Done but its blocker SALIN-192 is now To Do.** Reopening 192 exposed this.
   Either 204's Ugat L2–5 configuration work genuinely predates acceptance, or it should be
   reopened too.
5. **Correct two reversed Blocks links** — SALIN-180→178 and SALIN-188→192. Not applicable via
   API: the Jira MCP connector exposes `createIssueLink` but no delete operation, so adding the
   correct link would leave a Blocks cycle. Evidence is recorded in a comment on SALIN-180.
6. **Start Sprints 5–10** — all six are still in state `future` with no dates. No board or
   sprint tools in this connector; must be done from the Jira board.
