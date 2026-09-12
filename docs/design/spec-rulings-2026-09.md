# Spec Rulings — September 2026 Decision Record

> SALIN-213 / audit id T01. **Canonical, self-contained record** of the spec rulings the
> team made on **2026-09-11** and **2026-09-12**. Every ruling is restated here in full;
> this file is the authority a downstream ticket cites.
>
> **Provenance (not authority):** the rulings were captured during the code-vs-spec audit,
> in `docs/audit/AUDIT.md` §6 (first round), §6.1 (combat conflicts and the second/third
> rounds), §6.2 (plan review), and §6.4 (Jira reconciliation), with the consequence tables
> in `docs/audit/BACKLOG.md` and `docs/audit/JIRA-MERGE.md`. **That audit working set is
> untracked** — it is not part of the repository and may not exist in your checkout. Do not
> treat a pointer to it as a substitute for the text below. If this file and a copy of the
> audit ever disagree, this file is the one under version control and the one to fix.
>
> **Owner** of every ruling below: **Jon Wayne Cabusbusan (Systems Lead)** — see the
> [owner attribution note](#owner-attribution--requires-confirmation) before citing this
> file as authority for an individual decision.

## Reading order

Four rulings — **Q1, Q2, Q5 and Q12** — were amended by later rounds recorded in different
sections of the source. A one-line reading of any of them is **wrong**, and building against
the one-line reading produces work that does not fit the engine or the data model. Each has a
qualifier subsection in [§2](#2-qualifiers--read-these-before-implementing). Read the register
row and the qualifier together, never the row alone.

---

## 1. Ruling register

`BLOCKING` marks the six rulings named in SALIN-213's own summary as gating the sprint. All
sixteen are recorded because the acceptance criterion covers Q1–Q5 and Q12–Q16, and because
each of the remainder already owns downstream work.

| Q | Question | Ruling | Owner | Date | Owning tasks |
|---|---|---|---|---|---|
| **Q1** `BLOCKING` | Which syllable ends the campaign — YA (spec flow) or PA (code)? | **YA ends the campaign.** The spec flow wins over the code. Level Flow L15, UF-37 and Completion Rules ("YA completes MALAYA") are correct; the validator's enforcement of PA is wrong. → [qualifier](#q1--the-finale-symbol-is-derived-not-declared) | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-217 (T05), SALIN-229 (T14) |
| **Q2** `BLOCKING` | Are E, I, O, U separate spoken values (20), or not (18)? Is the character set 17 or 18? | **Firm on 18 characters.** DA and RA become **separate characters**, each with its own asset and its own enemy. E/I and O/U remain single spoken values resolved from word context — the total stays **18 spoken values**, now matched by **18 visual characters** rather than 17. → [qualifier](#q2--supersedes-the-2026-09-01-17-character-revert) | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-217 (T05), SALIN-221 (T09) |
| **Q3** `BLOCKING` | Is Level 1's final syllable MA or NA? | **MA.** The workbook is correct and **the asset is wrong** — `Level1_Config.asset` points `finalRestorationValue` at `Char_NA`. The educational matrix (`docs/technical/TW-SPK-004`) independently confirms that slot 2 (AMA) ends in MA. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-214 (T02) |
| **Q4** `BLOCKING` | Is the era paragraph a requirement, or do two final words satisfy Levels 5/10/15? | **Confirmed: the era paragraph is required** on Levels 5, 10 and 15. Earlier era words appear as **review blanks**; the level's **two new words are the taught slots**. Both are required — the paragraph does not replace the two-word structure. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-249 (T37), SALIN-252 (T40), SALIN-233 (T18) |
| **Q5** `BLOCKING` | Are Levels 5 and 10 boss fights, mixed waves, or both? | **Confirmed: follow the workbook.** El Inquisidor and Superintendent are **legacy mechanics to retire**. Levels 5 and 10 are **mixed, armored waves alternating with paragraph checkpoints**; word formation drives the level. **Paglimot on Level 15 is the only boss.** → [qualifier](#q5--level-15-keeps-its-boss-and-alternation-has-no-engine-support) | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-247 (T35), SALIN-226 (T66) |
| Q6 | Where does New Journey live — main menu, Settings, or both? | **Settings only**, behind the existing confirmation panel, to avoid accidental taps — as the code already does. The confirmation still lists what is reset and what is kept, then routes to the prologue. Spec rows UF-06/UF-07 are amended. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-256 (T45) |
| Q7 | Is the `A` listed among later uses of A in PAMANA a typo? | **Typo.** PAMANA decomposes PA + MA + NA and contains no standalone A. The Character Mastery cell is wrong. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-271 (T59) |
| Q8 | Does Continue open the hub, the map, or resume the level? | **Continue goes straight into the next incomplete level**, and once mid-level checkpoints persist, resumes *inside* it. UF-03's next screen is the level, not the hub. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-255 (T44) |
| Q9 | Are inflected forms ("SAMA-SAMA", "maUNA") restorable tokens or display-only? | **Restorable tokens are root words** (SAMA, UNA). Affixes and reduplication are **fixed text around the blank**, not player-placed. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-249 (T37), SALIN-252 (T40) |
| Q10 | Is per-word context art in scope for the demo? | **Yes — in scope for the demo.** | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-245 (T30), SALIN-257 (T47) |
| Q11 | Can a single event remove two hearts? | **Yes.** Paired enemies arriving together can remove two hearts in one event. No code change; document it in the Level 4 waves. | Jon Wayne Cabusbusan | 2026-09-11 | Level 4 wave authoring (no code change) |
| **Q12** `BLOCKING` | Wire the revised campaign save now (making the validator a boot gate), or downgrade media/roster validation to Warning until content lands? | **Yes — wire the revised save now.** → [qualifier](#q12--identity-blocks-content-warns-the-r1-split): the bare "yes" was **widened by plan review R1** into an identity/content split. Implementing the bare "yes" re-blocks boot. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-215 (T03), SALIN-219 (T07) |
| Q13 | Is the whole-screen drawing surface the intended trace pad, or is a bounded pad required? | **The whole-screen drawing surface *is* the trace pad.** The spec's "left-handed trace pad" row becomes **N/A**; no bounded pad and no pad-placement option are required. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-265 (T53), reduced to a haptics toggle |
| Q14 | The spec says Juan uses a bow and arrows; the built attack is a slash VFX. Change art or change spec? | **Withdrawn, not answered.** Q15 withdraws the bow/arrow requirement outright, so there is nothing left to reconcile. This is a closed item, **not an open question**. | Jon Wayne Cabusbusan | 2026-09-11 | — (closed under Q15) |
| Q15 | Keep or cut the built features absent from the spec (Endless Mode, Focus Mode, mass-clear, enemy abilities, Almanac Enemies tab)? | **The spec's "Combat and Archer System" and the Level Flow "Combat Flow" column are withdrawn**, replaced by the corrupted-enemy design in [§3](#3-combat-design-and-conflict-rulings-2026-09-11-second-and-third-round). Bow/arrow, lanes and forced single-active-clue targeting are **no longer requirements**. Remainder ruled in the second round: **cut combo powers, Focus Mode and Endless Mode; keep the Almanac Enemies tab**, updated to the 18 characters and their enemies. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-225 (T63), SALIN-268 (T56), SALIN-230 (T15) |
| Q16 | Who owns final Filipino copy, and is English acceptable for the demo? | **English is UI copy only.** Story dialogue, focus-word explanations and cutscenes stay **Filipino**. There is **no language setting**. (The first-round entry recorded only "English is acceptable"; the **third-round refinement above is the operative ruling** — SALIN-248 and SALIN-251 author narrative under it.) | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-266 (T54), SALIN-248 (T36), SALIN-251 (T39) |
| New | How are levels numbered for the player? | Levels are presented as **"Era N, Level 1 to 5"** everywhere, **never 1 to 15**. | Jon Wayne Cabusbusan | 2026-09-11 | SALIN-258 (T60) |

---

## 2. Qualifiers — read these before implementing

### Q1 — the finale symbol is derived, not declared

Making YA the finale is **not** a one-line value change.

`ContentIdentity.cs:31-32` derives the finale from position:

```
RevisedFinaleSymbolId = RevisedSymbolIds[RevisedSymbolIds.Count - 1]
```

and `RevisedSymbolIds` (`ContentIdentity.cs:20-26`) currently ends `…, "symbol.nga", "symbol.pa"`.
So the finale symbol is whatever sits **last in that array**. Delivering Q1 means **reordering
`RevisedSymbolIds` so `symbol.ya` is last** — not adding a field.

This collides with Q2. Q2 adds RA to the same array, and **plan review R8 ruled that RA must
not be appended last**, precisely because appending it would silently make RA the finale symbol
and overwrite Q1. RA's introduction level is **Level 13** (see OQ-6), and its array position must
reflect that, with YA remaining the final element.

Consequences to deliver together, owned by **SALIN-217 (T05)**:

- Reorder `RevisedSymbolIds` so `symbol.ya` is last and `symbol.ra` is not.
- `CampaignConfigValidator.cs:652-661` must **stop forcing** `symbol.pa` / `value.pa` as the finale
  (the `FinalRestorationInvalid` error reading *"The revised campaign finale must restore
  symbol.pa/value.pa"*).
- `Level15_Config.finalRestorationValue` → **YA**.

Plan review R9 noted that Q1 originally had **no owning task**; the code changes were folded
into T05 (SALIN-217) for this reason. If SALIN-217 is rescoped, Q1 loses its owner again.

### Q2 — supersedes the 2026-09-01 17-character revert

Q2 **reverses a deliberate, argued ruling that is eleven days older**, and anyone who finds that
older ruling first will reasonably conclude this document is the stale one. It is not. The
relationship is **supersession**, and the reason matters.

Commit **`935f2392`** — *"docs(system): SALIN-212 revert the character set to 17 taught
identities"*, 2026-09-01 — explicitly rejected an earlier 18-everywhere propagation, on the
grounds that "the character set" means two different things: the **curriculum teaches 17 visual
identities**, while the **recognizer distinguishes 18 glyph shapes**. Its load-bearing argument
was that an 18th taught identity *"would have demanded a glyph no level teaches"* — and on that
basis it also retuned `BossConfig_Kadiliman` from 18 to 17 required draws. The reasoning is
preserved at `docs/system/13_Document_Change_Log.md:15`.

**Q2 together with OQ-6 removes exactly that argument**, by giving RA a teaching slot at
**Level 13**. With RA taught, the "glyph no level teaches" objection no longer holds, and the
17-identity revert is superseded rather than contradicted.

**Documentation debt — recorded, not resolved here.** Seven tracked files still assert 17 and
are awaiting a sync pass:

| File | Lines |
|---|---|
| `docs/system/01_System_Overview.md` | 30 |
| `docs/system/04_Gameplay_Systems.md` | 304, 317-318 |
| `docs/system/07_Content_Pipeline.md` | 13 |
| `docs/system/09_Test_Strategy_and_Acceptance_Criteria.md` | 210, 298 |
| `docs/system/10_Requirements_Traceability_Matrix.md` | 67 |
| `docs/system/11_Risks_Dependencies_and_Mitigations.md` | 37 |
| `docs/system/13_Document_Change_Log.md` | 15 |

These are **deliberately not edited by SALIN-213.** `docs/system/` doc-sync is Systems Lead
governance under `docs/system/00_Documentation_Index.md` §Review and Update Rules, and needs its
own ticket. Until that pass lands, **this file is the current ruling and those seven are stale.**

Code surface the 18-character model touches (owned by SALIN-217 / SALIN-221, **not** by this
ticket): `ContentIdentity.RevisedSymbolIds` 17→18; `RevisedSpokenValueCount` stays 18 but
redistributes (DA and RA each take their own, `symbol.dara` retires); `RevisedDaraSymbolId`
retires; `CampaignConfigValidator.cs:220-233` (the "exactly eighteen contextual spoken values"
count and the `daraCount != 1` DA/RA rule); the `BaybayinIdCanonicalizer` RA→DA fold is removed;
`TemplateLoader` grouping; the Level 11 and Level 13 decompositions.

### Q5 — Level 15 keeps its boss, and "alternation" has no engine support

Q5 taken at face value is **wrong in two directions**, and both matter to different tickets.

**(a) The Level 15 boss is kept.** The first-round ruling reads "Paglimot on Level 15 is the only
boss", which is correct as stated — but the **third round went further** and ruled that Level 15
**keeps the Paglimot boss and folds the mixed waves and era paragraph into it**: one wave per
phase (Ugat, Ugnayan, all), one paragraph line restored per phase, **YA into MALAYA last**, and a
per-phase checkpoint. Level 15 is therefore *both* a boss encounter *and* a mixed-wave paragraph
level. Only Levels 5 and 10 lose their bosses.

**(b) "Combat alternates with paragraph blanks" is not implementable today.** Plan review R4
found this has **no engine support**: `LevelPhasePlan.cs:90-103` and `LevelFlowMachine.cs:41-66`
run Defense **once**, then ContextChallenge **once**. There is no alternation primitive. The
engine work is **SALIN-226 (T66)**, and **SALIN-247, SALIN-249, SALIN-252 and the Level 15 task
are blocked by it** — they each assumed alternation existed.

A reader who implements Q5 without R4 will plan level content the engine cannot run.

### Q12 — identity blocks, content warns (the R1 split)

The bare ruling is "yes, wire the revised save now". **Implemented literally, this breaks boot.**

Plan review R1 found that wiring the save makes `CampaignConfigValidator` a boot gate while
Level 13 is still unauthored — its empty focus words, pool, learning requirements and final value
are each a validator **Error** (`CampaignConfigValidator.cs:398-403, 501-505, 546-553, 644-650`;
`CampaignSaveService.cs:62-68`). R1 therefore **widened Q12** into a two-tier rule, and this
split — not the one-liner — is what SALIN-215 (T03) implements:

| Tier | Issues | Severity |
|---|---|---|
| **Identity** | manifest, era / level / symbol ids, counts, ordering, the DA/RA rule, workbook hash | **Error — blocks boot** |
| **Content completeness** | media, focus words, learning requirements, pools, rosters, final restoration value, challenge sequence, reward ids | **Warning** until a strict-mode switch in the release profile |

Related: plan review **R2** keeps SALIN-223 (T11) a **runtime guard only** — a missing
challenge or reward must not become an Error, or boot re-blocks until Levels 6–15 are authored,
contradicting this split. Plan review **R3** keeps review symbols in `learningRequirements` as
**Practice** kind rather than emptying the list on Levels 3, 4, 5, 10 and 14.

---

## 3. Combat design and conflict rulings (2026-09-11, second and third round)

The corrupted-enemy model replaces the withdrawn Combat and Archer System: every Baybayin symbol
has a corrupted enemy created by Paglimot embodying the opposite of that symbol's lesson, and Juan
defeats it by tracing the correct symbol. Four conflicts in that design were resolved in the
second round; six further topics in the third.

| # | Ruling | Owner | Date |
|---|---|---|---|
| **C1 / C3** | **DA and RA are separate characters, each with its own enemy.** Daan-Lihis stays with **DA**; an **RA enemy is to be designed** (appearance, ability, lore). The model is **18 characters, 18 enemies**. | Jon Wayne Cabusbusan | 2026-09-11 |
| **C2** | **Move the enemies to match the spec's era pools** — one syllable, one character; an enemy keeps the same asset and abilities everywhere. Corrected grouping: **Ugat** = A, E/I, BA, MA, NA, **TA**; **Ugnayan** = **O/U**, KA, GA, SA, WA, **YA**; **Pamana** = DA, RA, HA, LA, NGA, PA. **Symbol introduction levels are unchanged** — the enemies move, not the curriculum. Yapos ng Dilim keeps its final-stage role guarding YA in MALAYA, because YA is in the pool by Level 15. | Jon Wayne Cabusbusan | 2026-09-11 |
| **C4** | **Replace the enemy `era` enum** (`Spanish` / `American` / `Japanese`) with **Ugat / Ugnayan / Pamana**. | Jon Wayne Cabusbusan | 2026-09-11 |
| **Level 15** | **Keep the Paglimot boss** and fold the mixed waves and era paragraph into it — one wave per phase (Ugat, Ugnayan, all), one paragraph line per phase, YA into MALAYA last, per-phase checkpoint. (See the [Q5 qualifier](#q5--level-15-keeps-its-boss-and-alternation-has-no-engine-support).) | Jon Wayne Cabusbusan | 2026-09-11 |
| **Hub (UF-08)** | The hub is **still wanted**. | Jon Wayne Cabusbusan | 2026-09-11 |
| **Tracing Dojo** | **Fold free practice into the Codex**; remove the main-menu Tracing Dojo button. | Jon Wayne Cabusbusan | 2026-09-11 |
| **Memory art** | **In scope** — the team produces memory card and cutscene panel art. | Jon Wayne Cabusbusan | 2026-09-11 |
| **Existing progress** | Archive old PlayerPrefs progress and start a fresh journey with the migration notice (current code behaviour). Accepted by default. | Jon Wayne Cabusbusan | 2026-09-11 |
| **Demo target** | **All 15 levels.** Every content, flow and combat task is demo-required; only polish and accessibility tasks stay optional. | Jon Wayne Cabusbusan | 2026-09-11 |
| **RA recognition** | The team **confirms** the recognizer can separate RA from DA. **Recorded as a team confirmation, not a verified measurement** — no evaluation was run in that session. SALIN-217 (T05) keeps the evaluator check as an acceptance criterion; this row does not discharge it. | Jon Wayne Cabusbusan | 2026-09-11 |

Design decisions still flagged as open by the plan review, carried into their owning tickets
rather than resolved here: **R6** (seven designed enemy abilities act on things that do not exist
during combat — resolved into a combat-time rule sheet, owned by SALIN-259/T61) and **R7**
(paragraph restoration with syllable tiles puts 14+ tiles on a phone screen at Level 5 —
recommendation: **word tiles for paragraph units, syllable tiles for word units, one
`ChallengeUnit` per checkpoint line**).

---

## 4. Spec rulings from the Jira reconciliation (2026-09-12)

Four of the ten reconciliation answers are **spec** rulings and are recorded here. The other six
(**OQ-2, OQ-4, OQ-7, OQ-8, OQ-9, OQ-10**) are Jira-process decisions — ticket rescoping, link
types, import structure, board and sprint assignment — and are **not spec rulings**; they live in
the reconciliation notes and change no game behaviour.

| # | Ruling | Owner | Date |
|---|---|---|---|
| **OQ-1** | **Two blanks in Ugat Levels 3 and 4.** The workbook is the newest source (last modified **2026-09-11 12:02 UTC**) and **supersedes the 2026-09-01 one-blank amendment** (`793bcd85` "SALIN-144 restore one word", `df18c2a7`). Consequence: re-amend SALIN-144 and SALIN-146 back to two blanks; re-author `Challenge_Ugat03_Context` and `Challenge_Ugat04_Context` with two blanks; and rewrite the L3 context line at `docs/content/ugat-levels-2-5-narrative.md:137`, which still reads *"Isang salita lamang ang kulang"*, along with the matching L4 line. | Jon Wayne Cabusbusan | 2026-09-12 |
| **OQ-3** | **Checkpoint on every completed syllable.** Save-and-Exit followed by relaunch resumes at the **last completed syllable placement** in restoration, and at the **last cleared wave** in defense. SALIN-165's criterion that a level "restarts from a clean attempt rather than loading a partial checkpoint" must be amended to allow this. | Jon Wayne Cabusbusan | 2026-09-12 |
| **OQ-5** | **No lanes, powers, armour tiers, Focus Mode or Endless.** Restates and confirms Q15. | Jon Wayne Cabusbusan | 2026-09-12 |
| **OQ-6** | **DA and RA are separate**, on the 18-character model, with **RA introduced at Level 13**. Restates Q2 and adds the introduction level that R8 required. SALIN-155's second criterion ("DA/RA shares one basic character") is superseded. | Jon Wayne Cabusbusan | 2026-09-12 |

---

## 5. Workbook corrections required

The cells below disagree with the rulings above and need correcting in the spec workbook.
**Every row is `Pending`** — see the blocker note that follows for why none could be applied by
this ticket.

| # | Sheet | Cell / row | Current value | Corrected value | Ruling | Status |
|---|---|---|---|---|---|---|
| 1 | Game Overview | "Character set" | "18 spoken values but 17 visual characters" | 18 spoken values and **18 visual characters** | Q2 | `Pending` |
| 2 | Character Mastery | DA/RA row | one shared row | **split into DA and RA**, each with its own row and enemy | Q2, C1/C3 | `Pending` |
| 3 | Character Mastery | final-syllable row | PA "introduced in the final inheritance word" | **YA** ends the campaign | Q1 | `Pending` |
| 4 | Character Mastery | later uses of **A** | lists PAMANA | remove — PAMANA (PA+MA+NA) has no standalone A | Q7 | `Pending` |
| 5 | Level Flow Details | L1 final syllable | MA | **no workbook change** — the workbook is correct; the **asset** is wrong (`Level1_Config.asset` → `Char_NA`) | Q3 | `Pending` (asset-side, SALIN-214) |
| 6 | Level Flow Details | L5 / L10 combat | "boss" wording in Completion Rules L5 | **mixed armored waves** alternating with paragraph checkpoints; boss only on L15 | Q5 | `Pending` |
| 7 | Level Flow Details | L3 / L4 restoration | one blank | **two blanks** | OQ-1 | `Pending` |
| 8 | Completion Rules | "Words restored" | "all prior words plus 2 final words" | **era paragraph required** at L5/L10/L15, plus the two taught words | Q4 | `Pending` |
| 9 | Core Mechanics | "Combat and Archer System" | bow/arrow, lanes | **withdrawn**; replaced by the corrupted-enemy design (§3) | Q15 | `Pending` |
| 10 | Global UI Rules | "left-handed trace pad" | required | **N/A** — the whole screen is the trace pad | Q13 | `Pending` |
| 11 | UF-06 / UF-07 | New Journey placement | main menu | **Settings only**, behind the confirmation panel | Q6 | `Pending` |
| 12 | UF-03 | Continue destination | hub | **next incomplete level** | Q8 | `Pending` |

### Why these are Pending — the workbook cannot be corrected from this repository

Three findings, each verified against the working tree:

1. **The workbook is not in the repository.** `SALINLAHI_Complete_User_Flow.xlsx` — the file the
   audit names as its spec source — **does not exist anywhere in the working tree**. It was read
   from a location outside the repo. The only `.xlsx` present is
   `docs/technical/TW-SPK-004-educational-content-matrix.xlsx`, a different document.
2. **The code pins a *differently named* workbook, by name and by hash.**
   `docs/review/focus-word-checklist.md:7-8` identifies the approved workbook as
   **`CORE GAME MECHANICS.xlsx`**, with its checksum in `ContentIdentity.ApprovedWorkbookSha256`
   (`ContentIdentity.cs:14-15` = `33f7355f…c8e83`). `CampaignConfigValidator.cs:57-62` raises
   `WorkbookHashMismatch` as an **Error** when the campaign manifest's `sourceWorkbookSha256`
   disagrees — and under the [Q12 / R1 split](#q12--identity-blocks-content-warns-the-r1-split),
   workbook hash is an **identity** issue, so that Error **blocks boot**. Editing cells in the
   approved workbook changes its SHA and takes the game down until `ContentIdentity`,
   `CampaignIdentityManifest.cs:24,40` and `CampaignIdentityManifestTests.cs:22` are updated in
   the same change.
3. **A checksum discrepancy is already open and unowned.** `docs/backlog/technical-work.md:27`
   and `:824-829` record that SALIN-169's ticket specifies `34dad782…7eb7` while the code holds
   `33f7355f…c8e83`, with the action *"confirm which checksum is authoritative and update the
   other"* still outstanding.

Together these mean the correct file to edit is **ambiguous** (two differently named workbooks,
neither in the repo), and editing either one trips a **boot-blocking identity gate** whose
authoritative value is **itself disputed**. This is a product and ownership call, not an
implementation decision — it is escalated as [OI-1](#open-items). **No workbook was edited and
`ContentIdentity.ApprovedWorkbookSha256` was deliberately left untouched by SALIN-213.**

---

## 6. Downstream index — which ruling governs which ticket

SALIN-213 blocks eleven tickets. Each entry names the ruling to read **and its qualifier**, so a
developer picking up a ticket can find its governing decision in one lookup.

| Ticket | Work | Governing rulings |
|---|---|---|
| SALIN-214 (T02) | Correct Level 1 final restoration value to MA | **Q3** |
| SALIN-215 (T03) | Split the validator into blocking identity errors and content warnings | **Q12 + the R1 split** ([qualifier](#q12--identity-blocks-content-warns-the-r1-split)) — the bare Q12 is not implementable |
| SALIN-217 (T05) | Promote RA to a full 18th character | **Q2**, **C1/C3**, **OQ-6** (RA at Level 13), **R8** (RA must not be last), and the **Q1** finale reorder ([qualifier](#q1--the-finale-symbol-is-derived-not-declared)) |
| SALIN-221 (T09) | Author spoken values and labels | **Q2** — 18 characters, 18 spoken values, DA and RA distinct |
| SALIN-222 (T10) | Apply challenge tiers 1–5 to level data | No single ruling governs tier assignment; it waits on the ruling set as a whole, because Q4 and Q5 fix the shape of Levels 5, 10 and 15 |
| SALIN-229 (T14) | Final-syllable trace-and-place step | **Q1** (YA finale, with the derivation qualifier) and **Q3** (Level 1 = MA) |
| SALIN-247 (T35) | Redesign Level 5 and Level 10 combat | **Q5** — and its [qualifier](#q5--level-15-keeps-its-boss-and-alternation-has-no-engine-support): L15 keeps its boss, and alternation is **blocked on SALIN-226 (T66)** |
| SALIN-248 (T36) | Author Ugnayan narrative assets | **Q16** (narrative stays Filipino), **Q4** (era paragraph at L10) |
| SALIN-249 (T37) | Author challenge sequences for Levels 6, 7, 8, 10 | **Q4**, **Q9** (root-word tokens), **R7** (word tiles for paragraph units); alternation blocked on SALIN-226 |
| SALIN-251 (T39) | Author Pamana narrative assets | **Q16**, **Q4** (era paragraph at L15), plus the **Level 15** third-round ruling in §3 |
| SALIN-271 (T59) | Update the workbook with the code-is-better findings | **Q7**, and the whole of [§5](#5-workbook-corrections-required) — this ticket inherits the workbook blocker |

---

## Open items

These are recorded, not resolved. None is in scope for SALIN-213.

| # | Item | Why it is open | Where it stands |
|---|---|---|---|
| **OI-1** | **Workbook identity** — which file is the spec workbook (`SALINLAHI_Complete_User_Flow.xlsx` or `CORE GAME MECHANICS.xlsx`), where does it live, and who owns edits to it? | Neither file is in the repository; the code pins the second by SHA on a boot-blocking gate | **Escalated.** Blocks every row in §5 and blocks SALIN-271 (T59) |
| **OI-2** | **Checksum discrepancy** — SALIN-169's ticket says `34dad782…7eb7`, `ContentIdentity.ApprovedWorkbookSha256` says `33f7355f…c8e83` | Unowned since the spike; the action "confirm which is authoritative and update the other" is still outstanding | Recorded at `docs/backlog/technical-work.md:27, 824-829`. Must be settled before any workbook edit |
| **OI-3** | **Seven `docs/system/` files still assert 17 characters** against Q2 | Doc-sync is Systems Lead governance under `docs/system/00_Documentation_Index.md`; editing them from a Sprint 8 ticket would be scope creep | File list in the [Q2 qualifier](#q2--supersedes-the-2026-09-01-17-character-revert). **Needs its own ticket** |
| **OI-4** | **Per-ruling owner attribution** | The source records these as team rulings with no per-question attribution | See the note below |
| **OI-5** | **RA recognizer separation is a confirmation, not a measurement** | No evaluation was run when the team confirmed it | SALIN-217 (T05) keeps the evaluator check as acceptance |

### Owner attribution — requires confirmation

Every ruling in this document is attributed to **Jon Wayne Cabusbusan (Systems Lead)**. He is
SALIN-213's reporter, the documentation-governance owner under
`docs/system/00_Documentation_Index.md`, and the author of the `935f2392` ruling that Q2
supersedes.

**This attribution is a recording convention, not a source record.** The audit attributes these
rulings only to *"the team"*, with no per-question attribution anywhere. Owners were **not**
invented per question. **Confirm before this document is cited as authority for an individual
decision.**

---

## Scope note

SALIN-213's stated acceptance is that *each question in §5.1 Q1–Q5 and §6 Q12–Q16 has a one-line
ruling, an owner and a date, and the workbook cells that disagreed are corrected.* That is **ten**
questions, while the ticket summary names **six**. This document records the **superset** — all
sixteen plus the amending rulings — and marks the six from the summary `BLOCKING`, so both
readings are satisfied and reviewable. The workbook half is delivered as the §5 correction table
with every row `Pending`; see [OI-1](#open-items) for why it could not be closed.
