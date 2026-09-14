# Salinlahi — Audit backlog ↔ Jira SALIN reconciliation

Date: 2026-09-11, rulings added 2026-09-12 · Repo: `dev` @ `cb41a966` (2026-09-11 20:09:57 +0800) · Jira: jnwync.atlassian.net, project **SALIN** (id 10001, team-managed).
**Jira writes were authorised and executed on 2026-09-12; see the Import log near the end of this file.** The analysis sections below are kept as written before the import.

Inputs: `docs/audit/BACKLOG.md` (67 tasks, re-verified today), `docs/audit/jira-import.csv` (67 rows), and a read-only inventory of all 212 SALIN issues (summaries, statuses, parents, labels, sprints) plus the full descriptions of 81 candidate issues.

---

## Part A summary — repo refresh

| Item | Value |
|---|---|
| Branch | `dev` (`git checkout dev && git pull` → already up to date) |
| HEAD | `cb41a966` — "Merge pull request #198 from chdean-09/feature/stage-background-tiles", 2026-09-11 20:09:57 +0800 |
| Audit base | `cb41a966` (same commit) |
| Commits since audit | 0 (`git log cb41a966..HEAD` empty) |
| Re-verification result | 63 STILL-VALID · 1 PARTIALLY-DONE (T33, rescoped) · 3 OBSOLETE by ruling (T24, T48, T49) · 0 NOW-DONE · 0 NEW |

Per-task evidence is in `BACKLOG.md` (Changelog + Re-verification table + a status line under every task). The one material change is **T33**: at HEAD the colonial roster is deleted (`979db581`), the pool's default shell is `[Enemy] Corrupted.prefab` (`[Manager] EnemyPool.prefab:47`), only `elinquisidor` is registered (`:51`), and all 17 corruption `EnemyData_*.asset` carry 4 walk frames applied by `Enemy.cs:229-236`. Enemies already look distinct; the task shrinks to registration / fallback-warning cleanup.

---

## Part B.1 — Jira inventory (read-only)

| Item | Finding | How verified |
|---|---|---|
| Project | SALIN, id 10001, team-managed ("next-gen") | project metadata |
| Board | id 2. **Type: Scrum (inferred)** — sprints exist and are assigned via `customfield_10020`; the agile board endpoint is not exposed by the available tools, so the type is not directly read (OQ-8) | sprint field on issues |
| Active sprint | **none** | no issue has a sprint in state `active` |
| Future sprints | SALIN Sprint 5, 6, 7, 8, 9, 10 (67 issues assigned; most of Sprint 5–7 content is already Done — the sprints were never started/closed) | `customfield_10020` on issues |
| Closed sprints | SALIN Sprint 2 (16 issues), Sprint 3 (24), Sprint 4 (20) | same |
| Issues | 212 total: 144 Done · 36 To Do · 32 In Progress. Types: 152 Task, 37 Story, 18 Epic, 5 Bug. 4 explicit `[Duplicate - Do Not Use]` tasks (SALIN-194–197) | JQL inventory |
| Issue types available | Task, Bug, Story, Epic, Subtask | project issue-type metadata |
| Link types | Blocks, Cloners, Duplicate, Relates | link-type metadata |
| Components / versions / due dates | none in use | fields empty on all issues |
| Labels in use | `revised-backlog` (77), `workbook-core-mechanics` (68), `mvp` (19), `sprint-slice` (9), `code-health` (8), `level-1` (5), `ugat` (4), `work-type-research/qa/spike/chore`, `repo-bl-e*` / `repo-tw-*` traceability, `upload-duplicate`, `duplicate-of-salin-*`, `audit` (1), `qa`, `docs` | label counts |
| Parentless issues | 58 (incl. SALIN-207, 208, 210, 212 — the four most recent bug/task reports) | parent field |

### Epics and issue counts

| Epic | Status | Children | Notes |
|---|---|---|---|
| SALIN-1 Core Architecture & Infrastructure | Done | 7 | legacy, closed |
| SALIN-2 Enemy System | In Progress | 9 | legacy; only open epic that fits enemy work |
| SALIN-3 Baybayin Recognition System | In Progress | 10 | legacy |
| SALIN-4 Player & Combat System | In Progress | 5 | legacy |
| SALIN-5 Level Design & Progression | In Progress | 8 | legacy (SALIN-70 Endless lives here) |
| SALIN-6 UI, Scenes & UX | In Progress | 15 | legacy |
| SALIN-7 Audio | In Progress | 5 | legacy |
| SALIN-8 Release & Technical Quality | In Progress | 7 | legacy (SALIN-59 Lite flag) |
| SALIN-36 Art & Visual Assets | To Do | 2 | open, fits art batches |
| SALIN-51 Research, Testing & Analytics | To Do | 5 | open |
| SALIN-83 Code Health & Tech Debt | Done | 7 | closed |
| SALIN-126 Ugat Journey | In Progress | 10 | revised (BL-E3) |
| SALIN-127 Journey and Progression | To Do | 26 | revised (BL-E2): flow, save, results, QA |
| SALIN-128 Revised MVP Vertical Slice | To Do | 17 | revised (BL-E1): data schema, L1 slice |
| SALIN-129 Ugnayan Journey | To Do | 5 | revised (BL-E4) |
| SALIN-130 Pamana Journey | In Progress | 5 | revised (BL-E5) |
| SALIN-131 Learn and Review Baybayin | To Do | 7 | revised (BL-E6) |
| SALIN-132 Mobile Quality and Inclusive Play | To Do | 4 | revised (BL-E7) |

### Existing issues that matter for the merge (open or contradicted)

| Key | Status | Summary | Why it matters |
|---|---|---|---|
| SALIN-140 | In Progress | Persist completion, memories, and unlocks atomically | cannot pass while `SaveManager._campaign` is null (T07/T08) |
| SALIN-144–147 | To Do | Ugat Levels 2–5 stories | 144/146 amended 2026-09-01 to **one blank**, contradicting the workbook (T34) |
| SALIN-148–152 | In Progress | Ugnayan Levels 6–10 stories | code has no challenge/reward for 6, 7, 8, 10 (T11, T37) |
| SALIN-153–156, 158 | In Progress | Pamana Levels 11–15 stories | L13 empty; 155 assumes DA/RA share a glyph (T38) |
| SALIN-172 / 173 / 176 | In Progress | 15 configs / narrative / assets parents | expect era slices; T36–T40, T47 are those slices |
| SALIN-182 | In Progress | Align combat variety and five-trace powers | powers, lanes, armour — contradicted by ruling Q15 (T15, T61, T63) |
| SALIN-183 | In Progress | Metrics, rewards, mastery, Results | T19 is its campaign-wide Results slice |
| SALIN-184 / 169 | IP / Done | Three Paglimot encounters at 5, 10, 15 | contradicted by ruling Q5 (no boss at 5/10) — T35 |
| SALIN-207 | To Do | Kadiliman final boss to designed encounter | same objective as T41 |
| SALIN-208 | To Do | Record missing pronunciation clips | same objective as T32 + T46 |
| SALIN-210 | To Do (Bug) | 15 of 17 enemies spawn as Soldado | stale premise; same area as T33 |
| SALIN-212 | Done | Reconcile DA/RA identity | asked for the ruling; code unchanged (T05) |
| SALIN-133, 134, 138, 157, 178, 198 | Done | L1 / flow / preview / audio-label stories | Done claims contradicted by code at HEAD (T12, T13, T30, T09, T11, T02/T14) |
| SALIN-164, 165 | To Do | Read/control comfortably; responsive and stable | umbrella for T50–T52, T57; 165 forbids the partial-checkpoint resume T58 needs |
| SALIN-160, 161 | To Do | Review characters/words/memories; recall practice | umbrella for T26, T56 |
| SALIN-70 | To Do | Endless Mode unlock | cut by ruling Q15 (T63) |
| SALIN-59 | To Do | Lite/Full build flag | unrelated to the audit; leave |

---

## Part B.2 — Duplicate pass (all 67 audit tickets)

Verdict rule applied: DUPLICATE only when an existing open ticket has the same objective; OVERLAPS whenever an existing ticket's scope or acceptance criteria touch the audit task (even if Done and contradicted); GENUINELY NEW only when no SALIN issue mentions the surface at all.

Totals: **4 DUPLICATE · 51 OVERLAPS · 9 GENUINELY NEW · 3 dropped by ruling.** Imported in `jira-import-final.csv`: **59** (all OVERLAPS and NEW except T34, which folds into SALIN-144–147).

| Audit | Summary | Matching Jira (status) | Verdict | What to do |
|---|---|---|---|---|
| T01 | Record the six blocking spec rulings | SALIN-212 (Done), SALIN-188 (IP), SALIN-166 (Done) | OVERLAPS | Import. Relates SALIN-212 (it asked for the DA/RA ruling T01 records), SALIN-188 (language review consumes the rulings). |
| T02 | Correct Level 1 final restoration value to MA | SALIN-198 (Done: 'final syllable match the approved matrix') | OVERLAPS | Import as Bug. Relates SALIN-198; its Done claim is contradicted by `Level1_Config.asset:113` (OQ-4). |
| T03 | Downgrade missing context media from Error to Warning in the campaign validator | none (SALIN-170 Done built the validator; no ticket for the Error/Warning split) | GENUINELY NEW | Import. |
| T04 | Make Levels 6-15 combat rosters equal their cumulative pools | SALIN-172 (IP, parent of the 15 configs), SALIN-204 (IP, Ugat slice) | OVERLAPS | Import as Bug; relates SALIN-172. Do not fold: 172 is a long-lived parent and this is a closable defect. |
| T05 | Promote RA to a full 18th character | SALIN-212 (Done, escalated the 17-vs-18 question), SALIN-170 (Done, criteria say 17 visual), SALIN-177 (Done, tests assert 17 visual) | OVERLAPS | Import. Relates SALIN-212, SALIN-170. SALIN-212 should be reopened or referenced in a comment when writes are allowed (OQ-4). |
| T06 | Rename levels to spec titles and switch to era stage backgrounds | SALIN-185 (Done, naming), SALIN-172 (IP) | OVERLAPS | Import; relates SALIN-185. |
| T07 | Wire the revised campaign into SaveManager and boot in RevisedReady | SALIN-140 (IP), SALIN-174 (Done), SALIN-171 (Done) | OVERLAPS | Import; this is the concrete engineering step 140 cannot pass without. Link **blocks SALIN-140**. |
| T08 | Persist per-objective completion flags and gate unlock on them | SALIN-140 (IP: 'saved once as one versioned outcome') | OVERLAPS | Import; link **blocks SALIN-140**. |
| T09 | Author spoken values and labels per the Q2 ruling | SALIN-157 (Done: 'audio and visible label follow the approved level context'), SALIN-155 (IP, RA convention) | OVERLAPS | Import; relates SALIN-157 (Done claim contradicted by `FocusWordPreviewController.cs:90-98`, OQ-4) and SALIN-155. |
| T10 | Apply challenge tiers 1-5 to level data | SALIN-181 (Done, tier engine), SALIN-172 (IP, config authoring) | OVERLAPS | Import; relates SALIN-172. |
| T11 | Plan ContextChallenge and MemoryReward on every level and refuse completion when content is missing | SALIN-178 (Done: 'Defense systems … cannot mark a level complete') | OVERLAPS | Import as Bug; relates SALIN-178 (contradicted for levels with empty `challengeSequence`, OQ-4). |
| T12 | Make learning cards show only the level's new symbols | SALIN-133 (Done: 'only the required new syllables … are introduced') | OVERLAPS | Import as Bug; relates SALIN-133 (OQ-4). |
| T13 | Build the Required Practice phase (in-level guided tracing) | SALIN-178 (Done, nine-phase flow), SALIN-134 (Done: 'trace each required character before using it in defense') | OVERLAPS | Import; relates SALIN-178, SALIN-134. RequiredPractice is a stub at `LevelFlowController.cs:259-262` (OQ-4). |
| T14 | Add the final-syllable trace-and-place step to restoration | SALIN-198 (Done), SALIN-181 (Done) | OVERLAPS | Import; relates SALIN-181. |
| T15 | Set per-level combat mode and clue channels | SALIN-180 (Done, engine), SALIN-182 (IP, combat variety incl. lanes/powers) | OVERLAPS | Import; relates SALIN-182, which must be rescoped after Q15 (OQ-5). |
| T16 | Replace the free hint with the spec's hint modal and penalties | SALIN-181 (Done, emergency hint), SALIN-163 (IP, hint after failures) | OVERLAPS | Import; relates SALIN-163. |
| T17 | Add the Wave Cleared screen | none | GENUINELY NEW | Import. |
| T18 | Rebuild the restoration board with Baybayin tiles, Submit, Undo, and Hear | SALIN-181 (Done, challenge engine), SALIN-209 (Done, glyph outlines) | OVERLAPS | Import; relates SALIN-181, SALIN-209. |
| T19 | Complete the Level Results screen | SALIN-183 (IP, metrics/Results parent), SALIN-202 (Done, L1 Results slice) | OVERLAPS | Import as the campaign-wide Results slice; relates SALIN-183. Alternative: fold as new AC on 183. |
| T20 | Add the Ready for Defense screen and a practice checkpoint | none | GENUINELY NEW | Import. |
| T21 | Rebuild the Level Failed screen with checkpoint recovery | SALIN-135 (Done, clean retry), SALIN-181 (checkpoint reset) | OVERLAPS | Import; relates SALIN-135. |
| T22 | Pause menu: Restart Wave and objective quick view | SALIN-141 (Done, pause/restart/leave) | OVERLAPS | Import; relates SALIN-141. |
| T23 | Heart-loss beat: freeze, show the missed clue, queue it for review | none | GENUINELY NEW | Import. |
| T24 | DROPPED: combo rewards | SALIN-182 (IP, five-trace powers) | DROPPED (ruling) | Do not import (ruling Q15). SALIN-182 must drop its power tiers (OQ-5). |
| T25 | Save-status indicator at safe checkpoints | none (SALIN-140 is the write, not the indicator) | GENUINELY NEW | Import. |
| T26 | Memory Card screen and Memory Archive | SALIN-160 (To Do, review characters/words/memories) | OVERLAPS | Import; relates SALIN-160. Card + archive UI is not in 160's AC. |
| T27 | Wire the prologue cinematic with Skip and a viewed flag | SALIN-102 (Done, cutscene content), SALIN-114 (Done, tap-anywhere) | OVERLAPS | Import; relates SALIN-102. |
| T28 | Level Preview screen | SALIN-138 (Done, in-level intro preview) | OVERLAPS | Import; relates SALIN-138. T28 is the pre-level screen from the map, 138 is the in-level intro. |
| T29 | Mission Objective card | SALIN-138 (Done) | OVERLAPS | Import; relates SALIN-138. |
| T30 | Focus-word preview: images, tap-to-hear, Baybayin slots, inspection gating | SALIN-138 (Done: 'both words and their decompositions are readable') | OVERLAPS | Import; relates SALIN-138 (its AC is met; spec extras are not, OQ-4). |
| T31 | Stroke-order animation on the symbol lesson | none (SALIN-209 Done supplies the outline art) | GENUINELY NEW | Import; relates SALIN-209. |
| T32 | Record and wire pronunciation clips for the Level 1 symbols | SALIN-208 (To Do: record the missing clips, lists A, EI, MA, NA) | DUPLICATE | Do not import. Use SALIN-208; when writes are allowed, comment that A, EI, MA, NA are the Level 1 priority. |
| T33 | Author prefabs for the 13 corruption enemies | SALIN-210 (To Do, Bug: 'no prefab, spawn as Soldado') | DUPLICATE | Do not import. SALIN-210's premise is stale at HEAD (Soldado deleted in `979db581`); refresh its description to the rescoped T33 text in BACKLOG.md. |
| T34 | Align Ugat Levels 2-5 restoration with the spec | SALIN-145, SALIN-144, SALIN-146, SALIN-147 (To Do, Ugat L2–5 stories) | OVERLAPS | Do not import a separate ticket. Fold T34's deltas into 144–147 AC **after OQ-1** (one blank vs two) is decided. |
| T35 | Redesign Level 5 and Level 10 combat per the Q5 ruling | SALIN-147, SALIN-152 (three-phase Paglimot at L5/L10), SALIN-184 (IP), SALIN-169 (Done) | OVERLAPS | Import as the rescope task; relates SALIN-184, SALIN-147, SALIN-152. Jira currently specifies three-phase Paglimot at L5/L10; ruling Q5 says waves + paragraph, no boss (OQ-2). |
| T36 | Author Ugnayan (Levels 6-10) narrative assets | SALIN-173 (IP parent: 'later eras require additional slices'), SALIN-205 (Ugat slice pattern) | OVERLAPS | Import as 'Ugnayan slice' of 173; relates SALIN-173. |
| T37 | Author challenge sequences for Levels 6, 7, 8, 10 | SALIN-172 (IP parent), SALIN-148–152 (IP stories) | OVERLAPS | Import as 'Ugnayan slice: configurations and challenges' of 172; relates SALIN-172, SALIN-152. |
| T38 | Author Level 13 (SANGA, HARAYA) with a context-selected RA | SALIN-155 (IP, L13 story; AC assumes DA/RA share one glyph) | OVERLAPS | Import (tool change + data); relates SALIN-155 (AC needs the 18 ruling, OQ-6). |
| T39 | Author Pamana (Levels 11-15) narrative assets | SALIN-173 (IP parent) | OVERLAPS | Import as 'Pamana slice' of 173; relates SALIN-173. |
| T40 | Author Level 13 challenge and the Level 15 final text | SALIN-155, SALIN-158 (IP) | OVERLAPS | Import as 'Pamana slice: L13 challenge and L15 final text'; relates SALIN-158, SALIN-155. |
| T41 | Build the Paglimot three-phase boss for Level 15 (waves per phase, paragraph line per phase) | SALIN-207 (To Do: bring Kadiliman up to its designed encounter), SALIN-184 (IP), SALIN-158 (IP) | DUPLICATE | Do not import. Update SALIN-207 with the ruled design (waves per era per phase, paragraph line per phase, YA into MALAYA, phase-only restart) when writes are allowed. |
| T42 | Era Completion scene and Next Era Unlock | SALIN-147/152 ('era ending is shown'), SALIN-137 (Done, next-era unlock) | OVERLAPS | Import; relates SALIN-147. |
| T43 | Ending cinematic and post-game hub; decide Endless Mode | SALIN-158 (IP: completed-journey state, review/replay/Credits, Endless clause) | OVERLAPS | Import; relates SALIN-158. Endless is cut by T63, which satisfies 158's 'no enabled control' clause. |
| T44 | Living Scroll hub | none | GENUINELY NEW | Import. |
| T45 | Main menu completeness: progress display, Exit, archive entry | SALIN-136 (Done, routing), SALIN-142 (Done, reset) | OVERLAPS | Import; relates SALIN-136. |
| T46 | Record the remaining pronunciation clips | SALIN-208 (To Do) | DUPLICATE | Do not import; SALIN-208 already lists every missing clip including RA. |
| T47 | Glyph badge, almanac, and level-number art for remaining symbols and levels | SALIN-176 (IP, asset parent), SALIN-206 (IP, Ugat art slice) | OVERLAPS | Import; relates SALIN-176. |
| T48 | DROPPED: bow-and-arrow presentation | none | DROPPED (ruling) | Do not import (ruling Q15). |
| T49 | DROPPED: multi-lane battlefield | SALIN-182 (lanes) | DROPPED (ruling) | Do not import (ruling Q15). SALIN-182 must drop lanes (OQ-5). |
| T50 | Narration toggle and captions for audio clues | SALIN-164 (To Do: 'equivalent readable visual cue or caption') | OVERLAPS | Import; relates SALIN-164. |
| T51 | Reduced motion / reduced flash, larger hearts, hearts numeral | SALIN-164 ('reduced-effects … enabled') | OVERLAPS | Import; relates SALIN-164. |
| T52 | Text speed, font size, high contrast, Restore Defaults, Apply | SALIN-164 ('readable … smallest supported screen') | OVERLAPS | Import; relates SALIN-164. |
| T53 | Haptics toggle | none | GENUINELY NEW | Import. |
| T54 | Finalize English player-facing copy | SALIN-188 (IP, language review), SALIN-173 (IP) | OVERLAPS | Import; relates SALIN-188. |
| T55 | Emit the BTN-* tracking events to a local offline event log | SALIN-191 (IP: 'privacy-safe local educational telemetry'), SALIN-35 (Done) | OVERLAPS | Import; relates SALIN-191. |
| T56 | Character Codex upgrade and fold free practice into it | SALIN-160 (To Do, review screen), SALIN-159 (Done, practice), SALIN-118 (Done, Almanac) | OVERLAPS | Import; relates SALIN-160, SALIN-159. Alternative: fold into 160. |
| T57 | Difficulty Assist settings | SALIN-164 ('learning-speed assistance') | OVERLAPS | Import; relates SALIN-164. |
| T58 | Persist the mid-level checkpoint and implement Save and Exit | SALIN-165 (To Do: relaunch restarts 'from a clean attempt rather than loading a partial checkpoint'), SALIN-141 (Done) | OVERLAPS | Import; relates SALIN-165. **Conflict**: 165's AC forbids what the spec's Save and Exit requires (OQ-3). |
| T59 | Update the workbook with the code-is-better findings | SALIN-186 (Done, doc sync), SALIN-167 (Done, matrix) | OVERLAPS | Import; relates SALIN-186. |
| T60 | Present levels as Era N Level 1-5 everywhere | SALIN-185 (Done, naming) | OVERLAPS | Import; relates SALIN-185. |
| T61 | Implement the designed abilities for the corrupted enemies | SALIN-182 (IP, combat variety) | OVERLAPS | Import; relates SALIN-182 (replaces its variety list with the enemy rule sheet, OQ-5). |
| T62 | Enemy era, lore, and lesson data; retire the colonial era enum | SALIN-185 (Done: 'historical names are archived'), SALIN-176 ('Paglimot manifestations') | OVERLAPS | Import; relates SALIN-185. |
| T63 | Remove combo powers, Focus Mode, and Endless Mode | SALIN-182 (IP, five-trace powers), SALIN-70 (To Do, Endless unlock), SALIN-117 (Done, L2 focus tutorial) | OVERLAPS | Import; relates SALIN-182, SALIN-70. Recommend closing SALIN-70 as Won't Do and rescoping 182 (OQ-5). |
| T64 | Design and author the RA enemy | none (SALIN-212 covers RA identity, not an enemy) | GENUINELY NEW | Import; relates SALIN-212. |
| T65 | Design and build the Level 2 onboarding replacement | SALIN-116, SALIN-117 (Done, L2 tutorials) | OVERLAPS | Import; relates SALIN-117. |
| T66 | Support alternating defense and restoration segments in the level flow | SALIN-178 (Done, nine-phase flow), SALIN-184 (IP, continuous restoration) | OVERLAPS | Import; relates SALIN-178, SALIN-184. |
| T67 | Bump the save schema and migrate or reset development saves | SALIN-171 (Done, migration), SALIN-143 (Done) | OVERLAPS | Import; relates SALIN-171. |

---

## Part B.3 — Merge strategies

Constraints common to all three: labels are space-separated in the CSV importer; `Blocks` links between new tickets use the `Issue Id` column; links to existing issues use their key; sub-tasks in a team-managed project inherit their parent's sprint and cannot be planned independently; the four `[Duplicate - Do Not Use]` tasks show that a previous import already created duplicates once, so linking to existing keys matters more than speed.

### Strategy A — Dedicated epic "Spec Audit 2026-09"
- **Epic:** one new epic holding all imported tickets; `Relates` links out to the SALIN issues each one overlaps.
- **Sprint:** everything to backlog; pull Phase A into the next sprint at planning.
- **Types:** as in the CSV (Story/Task/Bug).
- **Linking:** `Blocks` for T-dependencies; `Relates` to existing keys.
- **Labels:** `audit-2026-09`.
- **Pros:** one place to see audit progress; no edits to existing epics. **Cons:** creates a second taxonomy beside BL-E1…E7 — the same work (e.g. T07 vs SALIN-140) ends up under two epics, and era slices (T36–T40) sit apart from their level stories in 129/130.

### Strategy B — Distribute into existing epics, tag with a label *(recommended)*
- **Epic:** each ticket gets the revised epic that already owns its area (SALIN-127 flow/save/UI, SALIN-128 data/validation, SALIN-126/129/130 era slices, SALIN-131 learning/review, SALIN-132 accessibility, SALIN-2 enemies, SALIN-36 art, SALIN-51 analytics). Distribution in the final CSV:

| Epic | Tickets | Ids |
|---|---|---|
| SALIN-127 Journey and Progression | 32 | T01, T06, T07, T08, T11, T13, T14, T15, T16, T17, T18, T19, T20, T21, T22, T23, T25, T27, T28, T29, T30, T42, T43, T44, T45, T54, T58, T59, T60, T63, T66, T67 |
| SALIN-128 Revised MVP Vertical Slice | 6 | T02, T03, T04, T05, T10, T12 |
| SALIN-132 Mobile Quality and Inclusive Play | 5 | T50, T51, T52, T53, T57 |
| SALIN-131 Learn and Review Baybayin | 4 | T09, T26, T31, T56 |
| SALIN-130 Pamana Journey | 3 | T38, T39, T40 |
| SALIN-2 Enemy System | 3 | T61, T62, T64 |
| SALIN-126 Ugat Journey | 2 | T35, T65 |
| SALIN-129 Ugnayan Journey | 2 | T36, T37 |
| SALIN-36 Art & Visual Assets | 1 | T47 |
| SALIN-51 Research, Testing & Analytics | 1 | T55 |

- **Sprint:** Phase A plus the four engine blockers (T01–T12, T63, T66, T67) into **SALIN Sprint 8**, because that unstarted sprint already contains SALIN-140 and the Ugat slice (144–147, 203–206) which these tickets unblock; everything else to the backlog (no sprint) for planning by era.
- **Types:** Bug where a Done Jira story is contradicted by code (T02, T04, T11, T12); Story for player-facing screens; Task for data, tooling, cleanup. No sub-tasks (they cannot be sprinted separately and the CSV importer needs parent ids for them).
- **Linking:** `Blocks` between audit tickets from the dependency columns; `Relates` to the matching existing key; T07 and T08 additionally `Blocks` SALIN-140.
- **Labels:** `audit-2026-09` + `revised-backlog` (existing convention) + `phase-a/b/c/d` + the area labels already in the CSV; `demo-required` retained.
- **Pros:** one taxonomy; a JQL of `labels = audit-2026-09` still gives the audit view; slices land where the era stories already live; SALIN-140/172/173/176 parents gain their missing children. **Cons:** 18 epics get touched; epic "Journey and Progression" grows by 32.

### Strategy C — Fold into existing tickets wherever possible, import only the remainder
- **Epic:** as B for what is imported; the ~25 OVERLAPS that map to an open In Progress/To Do parent (SALIN-140, 160, 164, 172, 173, 176, 182, 183, 184) become **sub-tasks** of those issues instead of standalone tickets; Done-but-contradicted stories are reopened and the audit AC appended.
- **Sprint:** sub-tasks inherit; reopened stories go back to their original future sprint.
- **Pros:** smallest issue count; keeps the BL-E story numbering intact. **Cons:** reopening 6+ Done stories and rewriting 8 parents is a large **write** operation that this session cannot perform; sub-tasks hide demo-critical engineering (T07, T13, T66) inside stories that are already In Progress; dependency `Blocks` links across sub-tasks are awkward on the board; and reopening Done items conflicts with the project's own "Historical SALIN issues remain unchanged and are evidence only" convention.

---

## Recommendation — Strategy B

Distribute the 59 surviving tickets into the existing revised epics, label everything `audit-2026-09`, put the fifteen foundation tickets into SALIN Sprint 8, link with `Blocks`/`Relates`, and handle the eight non-imported tasks by editing the four existing tickets they duplicate (SALIN-207, 208, 210, and the 144–147 set) in a separate, authorised write step.

### Step-by-step import order

1. **Open Questions are ruled (2026-09-12, table below); nothing blocks the import.** The rulings change the wording of five existing tickets (SALIN-144, 146, 147, 152, 155, 165, 182, 184, 70) — those edits belong to step 8.
2. **Dry run the CSV importer** on `docs/audit/jira-import-final.csv` with field mapping: `Issue Id` → Issue Id; `Parent` → Parent (epic key); `Sprint` → Sprint; `Blocked By n` → link type *Blocks* (inward, "is blocked by"); `Relates n` → link type *Relates*; `Blocks Existing` → link type *Blocks* (outward). Confirm the importer resolves existing keys in link columns.
3. **Import Phase A (T01–T10) + T12** first — these are data/validation tasks under SALIN-128/127/131 and carry no inbound links from existing tickets.
4. **Import Phase B (T11, T13–T23, T25, T26, T63, T65, T66, T67)** — the flow/engine tickets; T07/T08 gain the outbound `Blocks SALIN-140` link here.
5. **Import Phase C (T27–T31, T35–T40, T42–T45, T47, T60–T62, T64)** — content, era slices, enemies.
6. **Import Phase D (T50–T59)** — accessibility, analytics, docs.
7. **Verify links**: every `Blocked By` resolved to a `Blocks` link; spot-check T07 → SALIN-140, T35 → SALIN-184, T36 → SALIN-173.
8. **Authorised write step (not this session):** re-amend SALIN-144/146 to two blanks and SALIN-147/152 to waves + paragraph (OQ-1, OQ-2); amend SALIN-165 (syllable checkpoint, OQ-3), SALIN-155 (DA/RA separate, OQ-6), SALIN-184 (Level 15 only); refresh SALIN-210 (stale Soldado premise → rescoped T33), append the ruled Level 15 design to SALIN-207 (from T41), comment on SALIN-208 that A/EI/MA/NA are the Level 1 priority (T32), amend SALIN-144–147 per OQ-1 (T34), rescope SALIN-182 and close SALIN-70 as Won't Do per OQ-5, and add a comment to SALIN-212 pointing at T05.
9. **Sprint 8 planning:** confirm the fifteen foundation tickets fit alongside SALIN-140/144–147/203–206; move SALIN-203 (Ugat regression) behind T35.

---

## Open Questions — all ruled 2026-09-12

The team answered every question on 2026-09-12 ("go with what we discussed earlier and what you recommend"). Rulings first, original questions kept below for the record.

| # | Ruling (2026-09-12) | Consequence |
|---|---|---|
| OQ-1 | **(b) Two blanks in Ugat Levels 3 and 4.** The workbook is the newest source: `SALINLAHI_Complete_User_Flow.xlsx` was last modified 2026-09-11 12:02 UTC; the one-blank change landed 2026-09-01 (`793bcd85` "SALIN-144 restore one word", `df18c2a7`), and the narrative doc it cites was last edited 2026-09-06 (`9ca01475`) and still reads "Isang salita lamang ang kulang" at `docs/content/ugat-levels-2-5-narrative.md:137`. | T34 stays folded into SALIN-144–147 but its delta is now: re-amend SALIN-144 and SALIN-146 back to two blanks, rewrite the L3 context line at `ugat-levels-2-5-narrative.md:137` (and the matching L4 line) so the copy no longer says one word is missing, and re-author `Challenge_Ugat03_Context` / `Challenge_Ugat04_Context` with two blanks. The 2026-09-01 amendment on SALIN-144/146 is superseded. |
| OQ-2 | **(a)** Rescope SALIN-184 to Level 15 only; edit SALIN-147 and SALIN-152 AC from "three-phase Paglimot extension" to mixed waves alternating with paragraph lines (ruling Q5). SALIN-169 (Done) stays as history. | T35 unchanged; SALIN-203/190 regression checklists drop "Paglimot phases" for Levels 5 and 10. |
| OQ-3 | **Checkpoint on every completed syllable.** Save and Exit and relaunch resume at the last completed syllable placement in restoration (and at the last cleared wave in defense). | T58 acceptance amended below; SALIN-165's AC "restarts that level from a clean attempt rather than loading a partial checkpoint" must be amended to allow the saved syllable/wave checkpoint. T20/T21 checkpoint boundaries align to the same rule. |
| OQ-4 | **(a)** Leave Done stories closed; audit tickets carry `Relates` links (as in the CSV). Four contradicted items import as Bug (T02, T04, T11, T12). | No reopen writes needed. |
| OQ-5 | **(a)** No lanes, powers, armour tiers, Focus Mode, or Endless (ruling Q15). Rescope SALIN-182 to the enemy rule sheet (AUDIT.md §6.3) and close SALIN-70 as Won't Do. | T15, T61, T63 unchanged. |
| OQ-6 | **DA and RA are separate.** Amend SALIN-155's second AC ("DA/RA shares one basic character") to the 18-character model with RA introduced at Level 13. | T38, T05 unchanged. |
| OQ-7 | **(a)** Era slices import as standalone Tasks linked to SALIN-172/173 (the SALIN-204–206 precedent). | CSV unchanged. |
| OQ-8 | Informational only: the question was whether board 2 is a Scrum board (has sprints) or a Kanban board (no sprints). Sprints exist on issues, so Scrum is assumed; nothing changes unless the board is Kanban, in which case the Sprint column is ignored on import. | None. |
| OQ-9 | **(a)** Foundation tickets go to SALIN Sprint 8. | CSV unchanged. |
| OQ-10 | **(a)** T19 imports as a sibling Results slice linked to SALIN-183. | CSV unchanged. |

### Original questions (for the record)


| # | Question | Where it bites | Options |
|---|---|---|---|
| OQ-1 | **One blank or two in Ugat Levels 3 and 4?** The workbook (Level Flow L3/L4) says two words are restored; SALIN-144 and SALIN-146 were amended on 2026-09-01 to **one** blank citing `docs/content/ugat-levels-2-5-narrative.md`, and `Challenge_Ugat03_Context` ships one blank. | T34 (folded into 144–147), AUDIT.md §4 L3/L4 rows | (a) keep one blank and mark the workbook cells as corrected; (b) restore two blanks and re-amend 144/146 |
| OQ-2 | **Levels 5 and 10: waves + paragraph (ruling Q5) or three-phase Paglimot (SALIN-147, 152, 184, 169 Done)?** The Jira stories and the Done spike specify a three-phase Paglimot extension at 5 and 10; the team ruled on 2026-09-11 that only Level 15 keeps the boss. | T35, T41/SALIN-207, SALIN-184 scope, SALIN-203/190 regression checklists | (a) rescope 184 to Level 15 only and edit 147/152 AC; (b) reverse Q5 |
| OQ-3 | **Mid-level resume after relaunch.** Spec UF-43 / Global "Safe Saving" (T58) requires Save and Exit to resume at the checkpoint; SALIN-165 AC says a relaunch "restarts that level from a clean attempt rather than loading a partial checkpoint". | T58, SALIN-165 | (a) amend 165 to allow the saved checkpoint; (b) drop T58's resume and keep the spec's "restoration checkpoint" as in-session only |
| OQ-4 | **Done stories contradicted by code at HEAD** — SALIN-133 (T12), 134 & 178 (T13 stub, T11 guard), 138 (T30), 157 (T09), 198 (T02, T14), 212 (T05), 170/177 (17 visual symbols vs the 18 ruling). Automation may have closed some of these (see memory: Done is a claim to verify). | which issue type the audit tickets get; whether to reopen | (a) leave Done, import audit tickets with `Relates` (assumed in the CSV); (b) reopen and append AC |
| OQ-5 | **SALIN-182 and SALIN-70 after ruling Q15.** 182 (In Progress) specifies five-trace powers, lanes, armour; 70 (To Do) specifies Endless Mode; SALIN-190/203 regression lists include "five-trace powers". | T15, T61, T63, T24/T48/T49 | (a) rescope 182 to the enemy rule sheet and close 70 Won't Do; (b) keep 182 open and let T63 remove the code — conflicting tickets |
| OQ-6 | **SALIN-155 wording** ("DA/RA shares one basic character") vs the 18-character ruling and RA introduced at Level 13. | T38, T05 | amend 155's second AC after import |
| OQ-7 | **Era slices as standalone tickets or as children of 172/173?** The Ugat precedent (SALIN-204–206) made slices standalone Tasks linked to the parent. The CSV follows that precedent; the parents stay open for later eras. | T36, T37, T39, T40 | (a) standalone (assumed); (b) sub-tasks |
| OQ-8 | **Board type** — Scrum is inferred from sprint usage; not read directly. | inventory only | confirm in Jira board settings |
| OQ-9 | **Sprint 8 vs a new sprint** for the fifteen foundation tickets. Sprint 8 already holds 12 issues incl. the Ugat slice; capacity unknown. | import step 9 | (a) Sprint 8 (assumed in the CSV); (b) create "Sprint 8a – Foundations" |
| OQ-10 | **T19 vs SALIN-183** — import the Results screen as a sibling slice (assumed) or append its AC to 183? | T19 | either; the CSV imports it |

---

## Import log — executed 2026-09-12 (authorised by the team)

All writes below were performed through the Atlassian MCP connector as Jon Wayne Cabusbusan.

| Step | Result |
|---|---|
| Issues created | 59 (SALIN-213 … SALIN-271), all `To Do`, labelled `audit-2026-09`, parented to the recommended epics, 15 foundation tickets in SALIN Sprint 8 (id 146) |
| Blocks links | 67 (65 between audit tickets from the dependency columns; SALIN-219 and SALIN-220 block SALIN-140) |
| Relates links | 64 (audit ticket → matching existing issue) |
| Descriptions amended | SALIN-144, 146 (two blanks, OQ-1); SALIN-147, 152 (waves + paragraph, OQ-2); SALIN-155 (DA/RA separate, OQ-6); SALIN-165 (syllable checkpoint, OQ-3); SALIN-182 (rescoped to the enemy rule sheet, OQ-5); SALIN-184 (Level 15 only, OQ-2); SALIN-207 (ruled Level 15 design from T41); SALIN-210 (stale Soldado premise refreshed from T33). Each keeps the original text under an "Original / superseded" heading. |
| Comments | SALIN-208 (T32/T46 folded in; A, EI, MA, NA first), SALIN-212 (ruling: 18; implementation on SALIN-217), SALIN-70 (Won't Do note) |
| Transitions | SALIN-70 → Done (workflow has no Won't Do status; the comment is the resolution note) |
| Not done | Priority could not be set on create (field is not on the team-managed create screen); all new tickets carry Jira's default priority. Set it in bulk from the board if needed. The CSV importer was not used; the MCP connector created the issues directly, so `jira-import-final.csv` is now a record, not an input. |

### Audit id → Jira key

| Audit | Jira |
|---|---|
| T01 | SALIN-213 |
| T02 | SALIN-214 |
| T03 | SALIN-215 |
| T04 | SALIN-216 |
| T05 | SALIN-217 |
| T06 | SALIN-218 |
| T07 | SALIN-219 |
| T08 | SALIN-220 |
| T09 | SALIN-221 |
| T10 | SALIN-222 |
| T11 | SALIN-223 |
| T12 | SALIN-224 |
| T63 | SALIN-225 |
| T66 | SALIN-226 |
| T67 | SALIN-227 |
| T13 | SALIN-228 |
| T14 | SALIN-229 |
| T15 | SALIN-230 |
| T16 | SALIN-231 |
| T17 | SALIN-232 |
| T18 | SALIN-233 |
| T19 | SALIN-234 |
| T20 | SALIN-235 |
| T21 | SALIN-236 |
| T22 | SALIN-237 |
| T23 | SALIN-238 |
| T25 | SALIN-239 |
| T26 | SALIN-240 |
| T65 | SALIN-241 |
| T27 | SALIN-242 |
| T28 | SALIN-243 |
| T29 | SALIN-244 |
| T30 | SALIN-245 |
| T31 | SALIN-246 |
| T35 | SALIN-247 |
| T36 | SALIN-248 |
| T37 | SALIN-249 |
| T38 | SALIN-250 |
| T39 | SALIN-251 |
| T40 | SALIN-252 |
| T42 | SALIN-253 |
| T43 | SALIN-254 |
| T44 | SALIN-255 |
| T45 | SALIN-256 |
| T47 | SALIN-257 |
| T60 | SALIN-258 |
| T61 | SALIN-259 |
| T62 | SALIN-260 |
| T64 | SALIN-261 |
| T50 | SALIN-262 |
| T51 | SALIN-263 |
| T52 | SALIN-264 |
| T53 | SALIN-265 |
| T54 | SALIN-266 |
| T55 | SALIN-267 |
| T56 | SALIN-268 |
| T57 | SALIN-269 |
| T58 | SALIN-270 |
| T59 | SALIN-271 |

Not imported: T24, T48, T49 (dropped by ruling); T32, T46 → SALIN-208; T33 → SALIN-210; T41 → SALIN-207; T34 → folded into SALIN-144/145/146/147.

---

## Assignments — 2026-09-12

Split requested by the team: 40% Jeff, 20% each Chad, Jon, Ian Clyde. Assignment follows dependency chains so each person owns a lane end to end; cross-person blockers are almost all on Jeff's foundation tickets, which sit first in Sprint 8.

| Assignee | Count | Lane | Tickets |
|---|---|---|---|
| Jeff Andre Millan | 24 | Foundation and core loop: rulings, data fixes, validator, save wiring, objective flags, flow engine (Required Practice, final syllable, alternating segments), restoration board, hint/wave/ready/failed/results screens, checkpoints, combo/Endless removal, schema bump. | SALIN-213 (T01), SALIN-214 (T02), SALIN-215 (T03), SALIN-216 (T04), SALIN-219 (T07), SALIN-220 (T08), SALIN-222 (T10), SALIN-223 (T11), SALIN-228 (T13), SALIN-229 (T14), SALIN-231 (T16), SALIN-232 (T17), SALIN-233 (T18), SALIN-234 (T19), SALIN-235 (T20), SALIN-236 (T21), SALIN-237 (T22), SALIN-239 (T25), SALIN-240 (T26), SALIN-270 (T58), SALIN-225 (T63), SALIN-241 (T65), SALIN-226 (T66), SALIN-227 (T67) |
| Chad Denard Andrada | 12 | Characters and enemies: RA promotion, spoken values, learning cards, stroke animation, focus-word preview, Level 13 authoring, combat mode/clue channels, enemy abilities, enemy era/lore data, RA enemy, Codex. | SALIN-217 (T05), SALIN-221 (T09), SALIN-224 (T12), SALIN-230 (T15), SALIN-245 (T30), SALIN-246 (T31), SALIN-250 (T38), SALIN-252 (T40), SALIN-268 (T56), SALIN-259 (T61), SALIN-260 (T62), SALIN-261 (T64) |
| Jon Wayne Cabusbusan | 12 | Navigation and menus: level titles, era-local numbering, level preview, objective card, prologue, main menu, hub, ending, heart-loss beat, English copy, event log, workbook update. | SALIN-218 (T06), SALIN-238 (T23), SALIN-242 (T27), SALIN-243 (T28), SALIN-244 (T29), SALIN-254 (T43), SALIN-255 (T44), SALIN-256 (T45), SALIN-266 (T54), SALIN-267 (T55), SALIN-271 (T59), SALIN-258 (T60) |
| Ian Clyde Tejada | 11 | Era content and accessibility: Level 5/10 redesign, Ugnayan and Pamana narrative slices, Ugnayan challenges, era completion scene, badge/almanac/level art, captions, reduced motion, text settings, haptics, difficulty assist. | SALIN-247 (T35), SALIN-248 (T36), SALIN-249 (T37), SALIN-251 (T39), SALIN-253 (T42), SALIN-257 (T47), SALIN-262 (T50), SALIN-263 (T51), SALIN-264 (T52), SALIN-265 (T53), SALIN-269 (T57) |

Cross-person blockers: 22 of 67 Blocks links. All but three point at Jeff's foundation (T01 rulings, T04 rosters, T07 save wiring, T08 flags, T10 tiers, T11 guard, T26 memory card, T66 segments). The three that do not: SALIN-252 (T40, Chad) waits on SALIN-249 (T37, Ian); SALIN-227 (T67, Jeff) waits on SALIN-217 (T05, Chad); SALIN-253 (T42, Ian) waits on SALIN-240 (T26, Jeff) and its own SALIN-248. Do SALIN-213 (T01) first: it unblocks nine tickets across all four people.

---

## Files

- `docs/audit/BACKLOG.md` — rewritten with Changelog, Re-verification table, and a status line per task.
- `docs/audit/jira-import-final.csv` — 59 rows; columns `Issue Id, Summary, Description, Issue Type, Priority, Labels, Parent, Sprint, Phase, Blocked By 1-3, Relates 1-3, Blocks Existing`.
- `docs/audit/jira-import.csv` — original 67-row export, unchanged (kept for traceability).
- `docs/audit/AUDIT.md` — unchanged this round.
