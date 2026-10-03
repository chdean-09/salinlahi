# SALIN-166 Revised Era and Content Model Spike Completion Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish SALIN-166 as an approved, reviewable identity-and-migration contract and hand its exact decisions to SALIN-170, SALIN-171, and SALIN-185 without writing game code.

**Architecture:** SALIN-166 is a two-working-day decision spike, not a Unity implementation ticket. Its deliverable is the approved design document plus a Jira approval record; downstream tickets implement the content schema, save migration, and naming cleanup against that contract. Repository work stays documentation-only, while Jira comments and transitions occur only after the user explicitly authorizes those external writes.

**Tech Stack:** Markdown, Jira Cloud through the Atlassian MCP, Git, and PowerShell. Unity 6 LTS, C#, scenes, prefabs, ScriptableObjects, PlayerPrefs, and player save files are inspection evidence only and are not modified by this plan.

## Global Constraints

- SALIN-166/TW-SPK-001 is a time-boxed spike; it must not implement runtime C#, tests, Unity assets, scenes, prefabs, project settings, migration code, or player-data changes.
- The approved source baseline is `C:\Users\asus\Downloads\CORE GAME MECHANICS.xlsx` with SHA-256 `33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83`.
- The Jira checksum `34dad782a025b3acd3dcfc9bdfb2ce5c595fe81e6bd1789c9042849b63c27eb7` remains historical provenance, not the implementation baseline.
- The approved campaign is `campaign.revised-v1` with `era.ugat`, `era.ugnayan`, and `era.pamana`; Spanish, American, and Japanese are legacy faction/content semantics, not aliases for the revised eras.
- `identityManifestVersion`, `contentSchemaVersion`, and `saveSchemaVersion` are all `1`; unversioned legacy content/save is source version `0`; migration ID is `legacy-v0-to-revised-v1`.
- Historical campaign completion, mastery, discovery, tutorial, and selected-level data is archived and reset; approved audio and future accessibility preferences are preserved.
- The revised journey starts at `level.ugat.01`; old completion must never be converted into revised learning mastery.
- SALIN-170, SALIN-171, and SALIN-185 consume SALIN-166 and do not block completion of this spike. SALIN-167/SALIN-188 and SALIN-168 refine educational and challenge content later; they do not block approval of the identity and migration policy.
- When branch creation is executed, create `spike/SALIN-166-reconcile-revised-era-content-model` directly from the current local `dev` and carry every existing modified/untracked file unchanged. Do not stash, discard, reset, clean, move, stage, or commit those files.
- Do not create, stage, commit, amend, push, open a PR, or otherwise alter Git history unless the user explicitly requests that Git action. Branch creation is the only Git mutation already requested for this plan.
- Do not add Jira comments, edit issues, or transition statuses until the user explicitly authorizes those external writes in the execution session.
- Never modify `docs/capstone/Salinlahi.md`.
- No manual Unity configuration is required for SALIN-166. If Unity is opened for inspection, close it without saving scenes, assets, or project settings.

---

## File and System Map

**Repository files owned by this spike:**

- Modify: `docs/superpowers/specs/2026-08-09-salin-166-revised-era-content-model-design.md` — authoritative decision record, mappings, manifest, migration policy, approval state, and handoff.
- Create/maintain: `docs/superpowers/plans/2026-08-09-salin-166-revised-era-content-model.md` — exact completion, review, Jira, and downstream handoff procedure.

**External records touched only after explicit authorization:**

- Jira SALIN-166 — review request, approval evidence, final completion summary, and status.
- Jira SALIN-170 — content-schema handoff comment.
- Jira SALIN-171 — migration handoff comment.
- Jira SALIN-185 — naming/cleanup handoff comment.

**Files and systems explicitly not owned by this spike:**

- `Assets/Scripts/**`, `Assets/Tests/**`, `Assets/ScriptableObjects/**`, `Assets/_Scenes/**`, `ProjectSettings/**`, and player data under `Application.persistentDataPath`.
- `docs/system/**` and capstone diagrams, GDD, and TDD. Those change only with the downstream architectural implementation.
- SALIN-167/SALIN-188 educational approvals and SALIN-168 challenge-mode state design.

---

### Task 0: Understand the starting gate — SALIN-166 is ready now

**Files:**

- Read: `docs/superpowers/specs/2026-08-09-salin-166-revised-era-content-model-design.md`
- Read: Jira SALIN-166

**Interfaces:**

- Consumes: the approved workbook export, the current `dev` repository inspection, and the SALIN-166 Jira description.
- Produces: an unambiguous `SPIKE READY` decision that permits review and handoff immediately.

- [ ] **Step 1: Read the plain-language scope**

Read Sections 1, 4, 5, 17, 18, 19, and 20 of the design document.

Expected conclusion: SALIN-166 decides how old data identities relate to the revised campaign. It does not implement the new schema, migration, level content, gameplay flow, or cleanup.

- [ ] **Step 2: Record why no other SALIN ticket blocks this spike**

Use this exact dependency interpretation:

| Ticket | Relationship to SALIN-166 | May SALIN-166 finish without it? |
|---|---|---|
| SALIN-167 / SALIN-188 | Later educational/cultural source for labels, decompositions, pronunciation, and media approval | Yes |
| SALIN-168 | Later challenge-mode state contract consumed by the final schema | Yes |
| SALIN-170 | Implements the versioned content schema defined by SALIN-166 | Yes; SALIN-170 waits for this decision |
| SALIN-171 | Implements the archive/migration/recovery policy defined by SALIN-166 | Yes; SALIN-171 waits for this decision |
| SALIN-185 | Applies approved naming and evidence-backed cleanup | Yes; SALIN-185 consumes this decision |

The SALIN-166 Jira body and planning IDs are authoritative for this delivery order. Do not use inverted Jira link direction as a reason to wait for downstream implementation.

- [ ] **Step 3: Confirm the written deliverable covers every completion criterion**

Check this exact trace:

1. field-by-field mapping — design Sections 9 and 10;
2. runtime era decision — Sections 6 and 7;
3. versioned manifest and compatibility — Section 8;
4. impact list — Section 13;
5. preserve/reset/backup/interruption/rollback — Sections 10 and 11;
6. explicit adapters/replacements — Sections 7 and 9;
7. follow-up representation — Sections 16 and 17;
8. rationale and approved direction — Sections 6 and 19.

Expected result: all eight checks are present. Mark Task 0 `SPIKE READY` and continue. Do not stop for SALIN-167, SALIN-168, SALIN-170, SALIN-171, SALIN-185, or SALIN-188.

---

### Task 1: Create the spike branch directly from local `dev`

**Files:** None. This task changes only the current branch reference; every working-tree file remains unchanged.

**Interfaces:**

- Consumes: current local branch `dev` and its existing dirty working tree.
- Produces: branch `spike/SALIN-166-reconcile-revised-era-content-model` with the same working-tree paths and statuses.

- [ ] **Step 1: Confirm the current branch is `dev`**

Run:

```powershell
git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi branch --show-current
```

Expected result: exactly `dev`. If another branch is active, stop and ask the user before switching; the user requested a branch directly from `dev` and existing changes must not be moved between branches silently.

- [ ] **Step 2: Capture the existing changes in memory**

Run in one PowerShell session and keep that session open through Step 6:

```powershell
$salin166Before = @(git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi status --porcelain=v1 --untracked-files=all)
$salin166Before
```

Expected result: the existing modified and untracked paths are printed. Do not edit, discard, reset, clean, move, stage, commit, or stash any path in this list.

- [ ] **Step 3: Confirm the branch does not already exist**

Run:

```powershell
git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi show-ref --verify --quiet refs/heads/spike/SALIN-166-reconcile-revised-era-content-model
$LASTEXITCODE
```

Expected result: `1`. If the output is `0`, stop and ask whether to reuse the existing branch; do not delete or overwrite it.

- [ ] **Step 4: Create the branch from the current local `dev`**

Run:

```powershell
git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi switch -c spike/SALIN-166-reconcile-revised-era-content-model
git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi branch --show-current
```

Expected result: exactly `spike/SALIN-166-reconcile-revised-era-content-model`.

- [ ] **Step 5: Prove the existing changes were carried over unchanged**

Run in the same PowerShell session:

```powershell
$salin166After = @(git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi status --porcelain=v1 --untracked-files=all)
Compare-Object $salin166Before $salin166After
```

Expected result: no output. Any output means a working-tree path/status changed; stop and investigate without resetting, cleaning, or overwriting files.

- [ ] **Step 6: Display the final branch/status without touching the changes**

Run:

```powershell
git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi status --short --branch --untracked-files=all
```

Expected result: the first line names the SALIN-166 spike branch and the remaining paths match Step 2. Leave everything unstaged and uncommitted.

---

### Task 2: Freeze the owner-approved review artifact

**Files:**

- Modify only if inaccurate: `docs/superpowers/specs/2026-08-09-salin-166-revised-era-content-model-design.md`
- Read: `C:\Users\asus\Downloads\CORE GAME MECHANICS.xlsx`

**Interfaces:**

- Consumes: Chad Denard Andrada's written approval on 2026-08-09 and the approved workbook SHA-256.
- Produces: a self-consistent owner-approved spike document ready for the second approver.

- [ ] **Step 1: Verify the source workbook is still the approved export**

Run:

```powershell
(Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\Users\asus\Downloads\CORE GAME MECHANICS.xlsx').Hash.ToLowerInvariant()
```

Expected result:

```text
33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83
```

If it differs, do not silently replace the baseline. Keep the current approved hash and report that a different export was found.

- [ ] **Step 2: Confirm the approval state is explicit**

The design header must say:

```markdown
**Status:** Approved by the SALIN-166 owner; Jira team sign-off pending
```

Section 20 must identify:

- Chad Denard Andrada — SALIN-166 assignee/owner; approved 2026-08-09.
- Jon Wayne Cabusbusan — Jira reporter/revised-backlog owner; second approval still required.

- [ ] **Step 3: Confirm all ten approved decisions remain unchanged**

Compare Section 19 against these non-negotiable decisions:

1. approved workbook hash `33f7355f...c8e83`;
2. new canonical revised IDs, not relabeled legacy IDs;
3. semantic IDs for campaign, eras, levels, focus slots, and symbols;
4. legacy identifiers only at import/migration boundaries;
5. `Char_DA` as the preferred `symbol.dara` carrier and `Char_RA` as alias/archive candidate;
6. reuse technical containers while replacing incompatible content meaning;
7. archive historical campaign progress and start at `level.ugat.01`;
8. preserve audio and approved accessibility preferences separately;
9. validated archive/temp/primary/backup, deterministic recovery, and idempotent migration;
10. reject unsupported schemas and invalid content rather than guessing.

Expected result: all ten remain present and semantically identical.

- [ ] **Step 4: Generate immutable review fingerprints**

Run:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath 'docs\superpowers\specs\2026-08-09-salin-166-revised-era-content-model-design.md'
Get-FileHash -Algorithm SHA256 -LiteralPath 'docs\superpowers\plans\2026-08-09-salin-166-revised-era-content-model.md'
```

Copy both 64-character hashes into the Jira review comment so approvers can identify the exact reviewed files. If either file changes after approval, generate new hashes and request approval again.

---

### Task 3: Request the second approval in Jira

**Files:** None locally after Task 2.

**Interfaces:**

- Consumes: owner-approved design and both review fingerprints.
- Produces: explicit Jira approval or actionable review feedback from Jon Wayne Cabusbusan.

- [ ] **Step 1: Obtain authorization for the Jira write**

Ask the user: `May I post the SALIN-166 review request comment to Jira?`

Expected result: an explicit yes in the current execution conversation. Without it, stop before the Jira mutation while leaving the local artifact complete.

- [ ] **Step 2: Post the review request to SALIN-166**

Assign the two exact Task 2 Step 4 outputs to `designSha256` and `planSha256`, then use the Atlassian Jira comment action on SALIN-166 with the following body. Interpolate the named values before the tool call; never post the variable names themselves:

```markdown
## SALIN-166 review request

The spike contract is ready for team sign-off. Chad Denard Andrada approved the technical direction on 2026-08-09.

### Decision

The revised Ugat/Ugnayan/Pamana campaign receives new canonical stable IDs. Spanish/American/Japanese remain legacy faction/content semantics and are not aliases for the revised eras. Runtime implementation belongs to SALIN-170; save migration belongs to SALIN-171; naming/cleanup belongs to SALIN-185.

### Approved versions and migration

- Campaign: `campaign.revised-v1`
- Identity manifest: `1`
- Content schema: `1`
- Save schema: `1`
- Supported legacy source: unversioned `0`
- Migration: `legacy-v0-to-revised-v1`
- Preserve: audio and approved accessibility preferences
- Archive/reset: old campaign completion, mastery, discovery, tutorial state, and selected level
- Revised start: `level.ugat.01`
- Recovery: validated legacy archive plus temporary, primary, and backup revised files; deterministic and idempotent

### Review artifact

- Design: `docs/superpowers/specs/2026-08-09-salin-166-revised-era-content-model-design.md`
- Design SHA-256: `${designSha256}`
- Plan: `docs/superpowers/plans/2026-08-09-salin-166-revised-era-content-model.md`
- Plan SHA-256: `${planSha256}`
- Workbook SHA-256: `33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83`

Jon Wayne Cabusbusan: please reply with `Approved for SALIN-170/171/185 handoff` or list a specific requested change. This approval covers identity, version, compatibility, preserve/reset, backup, interruption-recovery, and rollback policy. Educational decompositions/media remain SALIN-167/SALIN-188; challenge behavior remains SALIN-168.
```

- [ ] **Step 3: Evaluate the reply precisely**

Accepted approval text is an unambiguous statement equivalent to `Approved for SALIN-170/171/185 handoff` from Jon Wayne Cabusbusan.

If the reply requests a change:

1. map the request to the owning design section;
2. reject it from SALIN-166 if it attempts to author exact decompositions/media (SALIN-167/SALIN-188) or challenge behavior (SALIN-168);
3. otherwise update the design consistently, including the completion trace and handoff;
4. rerun Task 2 Steps 3–4;
5. post the changed decisions and new fingerprints;
6. obtain fresh approval from Chad and Jon.

Do not treat silence, an emoji, Jira status, or a verbal message without a recorded decision as team approval.

- [ ] **Step 4: Record team sign-off in the design**

After Jon approves, change the design header to:

```markdown
**Status:** Team approved; ready for downstream implementation
```

In Section 20, record Jon's approval date and the SALIN-166 Jira comment URL/identifier. Recompute both fingerprints and post one final comment noting that only approval metadata changed after the reviewed technical content.

---

### Task 4: Hand the content contract to SALIN-170

**Files:** None locally unless review feedback changes the design.

**Interfaces:**

- Consumes: team-approved Sections 7–9, 12, 14, and 15.1.
- Produces: one explicit SALIN-170 implementation boundary with no SALIN-166 runtime work.

- [ ] **Step 1: Obtain authorization for the downstream Jira comment**

Ask the user: `May I post the approved SALIN-166 schema handoff to SALIN-170?`

Continue only after an explicit yes in the execution conversation.

- [ ] **Step 2: Post this exact SALIN-170 handoff**

```markdown
## Approved SALIN-166 contract for SALIN-170

Implement the versioned revised campaign schema against these frozen decisions:

- root `CampaignConfigSO` for `campaign.revised-v1`;
- exactly three ordered eras: `era.ugat`, `era.ugnayan`, `era.pamana`;
- exactly 15 stable level IDs and 30 inline focus slots;
- exactly 17 canonical visual symbol IDs, including one `symbol.dara` with contextual DA/RA values;
- `levelNumber` is presentation/global order only, never save/content identity;
- Spanish/American/Japanese remain isolated legacy faction/content semantics;
- manifest/content/save versions are all `1`; supported source content/save includes legacy `0` and revised `1`;
- no kudlit or modified consonant forms in current campaign scope;
- validation fails closed on missing/duplicate/unsupported identities and invalid campaign counts;
- preserve deserialization/GUIDs only where meaning remains compatible; use explicit legacy aliases/adapters otherwise.

SALIN-167/SALIN-188 still own exact educational labels, decompositions, pronunciation, and media approval. SALIN-168 still owns the challenge-mode state contract. Those inputs may refine authored fields and validators, but they must not replace the approved identity/version policy.

Source: SALIN-166 design Sections 7–9, 12, 14, and 15.1.
```

- [ ] **Step 3: Verify SALIN-170 did not inherit migration implementation**

Read the posted comment and confirm it does not ask SALIN-170 to write save files, migrate PlayerPrefs, show the migration notice, rotate backups, or recover interrupted writes. Those belong to SALIN-171.

---

### Task 5: Hand the migration contract to SALIN-171

**Files:** None locally unless review feedback changes the design.

**Interfaces:**

- Consumes: team-approved Sections 8, 10, 11, 12, 14, and 15.2.
- Produces: one deterministic SALIN-171 migration/recovery boundary.

- [ ] **Step 1: Obtain authorization for the downstream Jira comment**

Ask the user: `May I post the approved SALIN-166 migration handoff to SALIN-171?`

Continue only after an explicit yes in the execution conversation.

- [ ] **Step 2: Post this exact SALIN-171 handoff**

```markdown
## Approved SALIN-166 contract for SALIN-171

Implement migration `legacy-v0-to-revised-v1` against these frozen decisions:

- content/save source `0` means the current unversioned legacy build; revised save schema is `1`;
- archive all known legacy campaign keys before the first revised save becomes active;
- preserve audio and approved accessibility preferences separately;
- reset old level completion, mastery/character unlocks, discovery, tutorial checkpoints, endless unlock, selected level, memories, and rewards;
- initialize the revised journey at `level.ugat.01` with no revised mastery/completion claims;
- use logical files `legacy-progress-v0.json`, `campaign-save.tmp`, `campaign-save.json`, and `campaign-save.bak`;
- validate archive read-back before activation and validate the complete temporary revised document before promotion;
- recovery order is valid primary, promotable newer valid temp, valid backup, valid legacy source and idempotent rerun, clean initialization, then documented safe reset while retaining corrupt inputs;
- migration state lives in the v1 save envelope and is idempotent; do not infer success from one PlayerPrefs key;
- reject unknown higher schemas; never merge old completion into revised mastery;
- show one migration notice only after a valid revised save is active.

SALIN-171 may finish its design immediately. Runtime implementation begins after SALIN-170 freezes the concrete v1 save/content envelope it must serialize and validate.

Source: SALIN-166 design Sections 8, 10–12, 14, and 15.2.
```

- [ ] **Step 3: Verify migration ownership remains coherent**

Confirm the handoff preserves the SALIN-174 atomic persistence boundary: SALIN-171 defines migration behavior and uses the shared atomic boundary; it must not create a second competing normal-progress writer.

---

### Task 6: Hand naming and cleanup boundaries to SALIN-185

**Files:** None locally unless review feedback changes the design.

**Interfaces:**

- Consumes: team-approved Sections 7, 9, 13, 16, and 17.
- Produces: a SALIN-185 cleanup boundary that preserves evidence and avoids broad deletion.

- [ ] **Step 1: Obtain authorization for the downstream Jira comment**

Ask the user: `May I post the approved SALIN-166 naming/cleanup handoff to SALIN-185?`

Continue only after an explicit yes in the execution conversation.

- [ ] **Step 2: Post this exact SALIN-185 handoff**

```markdown
## Approved SALIN-166 contract for SALIN-185

Apply these naming and cleanup boundaries:

- active revised campaign names are Ugat, Ugnayan, Pamana, and the approved Paglimot scope;
- Spanish/American/Japanese are not aliases for revised eras and may remain only in explicit legacy faction/content adapters or archives;
- do not rename Unity assets merely for cosmetic consistency when retaining the filename/GUID is safer;
- runtime/UI/test/save identity must use canonical stable IDs, never asset filenames, Unity GUIDs, display text, list position, or `levelNumber`;
- `Char_DA` is the preferred carrier for `symbol.dara`; validate every reference before archiving or removing `Char_RA`;
- replace the Spanish-only Almanac reveal gate for revised discovery;
- archive historical content and remove/move only candidates with reference scans and regression evidence;
- preserve `.meta` files for approved Unity moves;
- do not perform broad cleanup just to reduce file count.

Source: SALIN-166 design Sections 7, 9, 13, 16, and 17.
```

- [ ] **Step 3: Check the currently In Progress SALIN-185 work**

Because SALIN-185 is already In Progress, compare its current changes against the handoff before it removes or renames anything. If it has already treated colonial identifiers as revised aliases or deleted assets without reference evidence, pause that specific cleanup and reconcile it with SALIN-166; do not roll back unrelated valid work.

---

### Task 7: Confirm there is no SALIN-166 Unity configuration

**Files:** None.

**Interfaces:**

- Consumes: the spike's documentation-only scope.
- Produces: a verified `NOT APPLICABLE` Unity configuration record.

- [ ] **Step 1: Do not open Unity for implementation**

SALIN-166 creates no `CampaignConfigSO`, era assets, level assets, inspectors, scenes, prefabs, save files, or player settings. Therefore there are no Unity buttons, menus, object assignments, or scene-save operations for the user to perform.

- [ ] **Step 2: If Unity was opened only to inspect evidence, exit without saving**

Use these exact UI steps only if Unity is already open:

1. Do not press the main **Play** button.
2. Do not choose **File > Save** or **File > Save Project**.
3. If Unity shows a save prompt while closing, read the listed scene/assets and choose **Don't Save** for SALIN-166 inspection changes.
4. Close the Editor.
5. Run `git status --short --untracked-files=all` and compare it with the Task 1 baseline.

Expected result: no new or changed `Assets/**` or `ProjectSettings/**` path is attributable to SALIN-166. Existing user changes remain untouched.

- [ ] **Step 3: Route future manual Unity work to the owning ticket**

- Campaign/era/level fields and validators: SALIN-170.
- Revised level assets and inspector assignments: SALIN-172 and the level stories.
- Save-upgrade test profiles and recovery cases: SALIN-171.
- Renames/moves/reference cleanup: SALIN-185.

Do not copy those instructions into SALIN-166 as implementation steps.

---

### Task 8: Verify the artifact and close SALIN-166

**Files:**

- Verify: `docs/superpowers/specs/2026-08-09-salin-166-revised-era-content-model-design.md`
- Verify: `docs/superpowers/plans/2026-08-09-salin-166-revised-era-content-model.md`
- Verify externally: Jira SALIN-166, SALIN-170, SALIN-171, and SALIN-185

**Interfaces:**

- Consumes: team approval and all three posted handoffs.
- Produces: evidence-backed SALIN-166 completion without runtime changes.

- [ ] **Step 1: Run the document completeness scan**

Run:

```powershell
$design = Get-Content -Raw -LiteralPath 'docs\superpowers\specs\2026-08-09-salin-166-revised-era-content-model-design.md'
$required = @('field-by-field','campaign.revised-v1','era.ugat','era.ugnayan','era.pamana','identityManifestVersion','contentSchemaVersion','saveSchemaVersion','legacy-v0-to-revised-v1','campaign-save.tmp','campaign-save.json','campaign-save.bak','legacy-progress-v0.json','SALIN-170','SALIN-171','SALIN-185')
$missing = @($required | Where-Object { $design -notmatch [regex]::Escape($_) })
$missing
```

Expected result: no output.

- [ ] **Step 2: Run the red-flag and formatting scan**

Run:

```powershell
$ownedDocs = @(
    'docs\superpowers\specs\2026-08-09-salin-166-revised-era-content-model-design.md',
    'docs\superpowers\plans\2026-08-09-salin-166-revised-era-content-model.md'
)
$redFlagPattern = 'T[B]D|T[O]DO|implement la' + 'ter|fill in de' + 'tails|Similar to Ta' + 'sk|appropriate error hand' + 'ling'
rg -n $redFlagPattern @ownedDocs
$ownedDocs | ForEach-Object { Select-String -LiteralPath $_ -Pattern '[ \t]+$' }
```

Expected result: both commands print nothing. This checks only the two SALIN-166 documents; existing unrelated diffs and whitespace errors remain user-owned and must not be modified.

- [ ] **Step 3: Verify the Jira evidence**

Read SALIN-166 and confirm all of the following:

1. Chad's approval date is recorded.
2. Jon's explicit approval is recorded.
3. the design/plan fingerprints are recorded.
4. the approved workbook hash is recorded.
5. content/save/manifest version `1` and source `0` are recorded.
6. preserve/reset/archive/recovery policy is summarized.
7. SALIN-170, SALIN-171, and SALIN-185 each contain the approved handoff.

Expected result: seven checks pass. If any check is absent, add only the missing evidence after explicit authorization; do not mark SALIN-166 Done.

- [ ] **Step 4: Confirm repository scope integrity**

Run:

```powershell
git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi status --short --untracked-files=all
git -c safe.directory=C:/Users/asus/Documents/CODING/Salinlahi diff --name-only -- Assets ProjectSettings
```

Compare the output with the Task 1 baseline. Existing user changes may remain, including a pre-existing `ProjectSettings/ProjectSettings.asset` modification; the requirement is that SALIN-166 added no runtime/Unity change and did not modify any existing user-owned path.

- [ ] **Step 5: Obtain authorization for the final Jira comment and transition**

Ask the user: `The SALIN-166 design is team-approved and all handoffs are recorded. May I post the completion summary and transition SALIN-166 to Done?`

Continue only after an explicit yes in the execution conversation.

- [ ] **Step 6: Post the final SALIN-166 completion comment**

```markdown
## SALIN-166 complete

Team approval is recorded for the revised identity and migration contract.

- New canonical revised IDs; legacy colonial-era semantics remain isolated
- Identity manifest/content schema/save schema: `1`
- Supported source: legacy unversioned `0` and revised `1`
- Migration: `legacy-v0-to-revised-v1`
- Old campaign progress archived/reset; audio/accessibility preferences preserved
- Deterministic archive/temp/primary/backup recovery and rollback policy approved
- SALIN-170 schema handoff posted
- SALIN-171 migration handoff posted
- SALIN-185 naming/cleanup handoff posted
- No C#, Unity asset, scene, prefab, ProjectSettings, or player-data change belongs to this spike

SALIN-167/SALIN-188 remain responsible for educational/cultural approval, and SALIN-168 remains responsible for challenge-mode behavior. Those follow-ups do not change the approved stable-ID/version/migration policy without a new recorded decision.
```

- [ ] **Step 7: Transition SALIN-166 to Done and verify**

Re-read available transitions, then use the available `Done` transition. At plan authoring time the transition ID is `31`, but select by the live transition named `Done` rather than assuming the ID cannot change.

Fetch SALIN-166 again and confirm:

- status is `Done`;
- assignee remains Chad Denard Andrada;
- final comment exists;
- approval comments remain visible.

If the status or evidence is missing, report the exact mismatch and leave the repository unchanged.

- [ ] **Step 8: Report completion without Git side effects**

Provide links to SALIN-166 and both local documents. State that SALIN-166 completed as a spike and that coding begins in SALIN-170/171 after their own gates. State that no Unity configuration, stage, commit, push, PR, or runtime change was performed unless separately authorized and verified.

---

## Execution Order Summary

1. Task 0 passes immediately: SALIN-166 does not wait for downstream tickets.
2. Create the requested spike branch from local `dev` while preserving the dirty tree exactly.
3. Freeze and fingerprint the owner-approved design.
4. Obtain Jon Wayne Cabusbusan's explicit Jira approval.
5. Post the approved contracts to SALIN-170, SALIN-171, and SALIN-185 after user authorization.
6. Perform no Unity configuration for this spike.
7. Verify documentation/Jira evidence, request final Jira authorization, and transition SALIN-166 to Done.

The next coding ticket is SALIN-170 for the schema. SALIN-171 can proceed with detailed migration design immediately, but its implementation waits for SALIN-170 to freeze the concrete v1 envelope. SALIN-185 must align any active cleanup with this approved contract.
