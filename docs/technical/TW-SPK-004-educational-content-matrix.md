# TW-SPK-004 — Educational Content Matrix Validation

**Jira:** SALIN-167  
**Type:** Spike  
**Priority:** Must Have  
**Status:** Pending TW-RES-001/SALIN-188 Review  
**Authoritative matrix:** `docs/technical/TW-SPK-004-educational-content-matrix.xlsx`

## Outcome

The draft matrix is complete and internally checked. It defines 17 canonical visual symbols, 18 contextual spoken values, and 30 ordered focus-word slots across 15 levels. It preserves intentional word and syllable repetition, treats E/I and O/U as context-dependent vowels, treats DA/RA as one visual identity, and introduces PA at the start of Level 15 before PAMANA is assessed.

This spike records repository discrepancies but does not modify gameplay code, ScriptableObjects, templates, audio, glyphs, or level configurations. Final language and cultural approval remains owned by TW-RES-001, represented by SALIN-188.

## Sources and precedence

Evidence was applied in this order:

1. `files/backlog/CORE GAME MECHANICS.xlsx`, explicitly approved by the user on 2026-08-13.
2. The approved SALIN-167 resolutions in the supplied Jira body.
3. Current repository assets and code data.
4. Historical Jira issues as supporting evidence only.

The approved workbook SHA-256 is `4a2996ef4f4d3102b4657b1e8bf2b54b2183bc7748d3931a6bb674d2f92ecc9d`. The Jira upload note instead records `34dad782a025b3acd3dcfc9bdfb2ce5c595fe81e6bd1789c9042849b63c27eb7`; this is retained as discrepancy D-001 rather than silently rewritten.

`docs/backlog/technical-work.md` is absent from this checkout and its Git history. The supplied Jira body is therefore the planning-source substitute for this draft.

## Matrix rules

- The canonical visual order is A, E/I, BA, MA, NA, TA, O/U, KA, GA, SA, WA, YA, DA/RA, HA, LA, NGA, and PA.
- The 18 contextual spoken values arise because DA and RA share `ᜇ`; E/I and O/U remain shared visual vowels rather than separate symbols.
- A focus-word decomposition is an ordered list of syllable occurrences. Occurrences are never deduplicated: OO is `O + O`, and ALAALA is `A + LA + A + LA`.
- Each level contributes exactly two focus slots. For Levels 5, 10, and 15, the slots are the two final/new words from the workbook paragraph lists; previously introduced paragraph words remain usage evidence rather than extra slots.
- The second slot's final syllable must equal the workbook's `Last Required Syllable`. The first slot is retained in full and marked not applicable for that level-completion check.
- Every spoken syllable maps to a canonical visual identity. All current-scope decompositions use basic symbols only; no row requires kudlit or an unsupported modified consonant.
- Cumulative pools are monotonic. PA is added at Level 15 before slot 1, PAMANA (`PA + MA + NA`), and remains available for slot 2, MALAYA.

## Verified focus slots

| Level | Era | Slot 1 | Slot 2 | Workbook last syllable |
|---:|---|---|---|---|
| 1 | Ugat | INA | AMA | MA |
| 2 | Ugat | BATA | MATA | TA |
| 3 | Ugat | BATA | TAMA | MA |
| 4 | Ugat | INA | AMA | MA |
| 5 | Ugat | IBA | MANA | NA |
| 6 | Ugnayan | AWA | GAWA | WA |
| 7 | Ugnayan | SAMA | KASAMA | MA |
| 8 | Ugnayan | GANA | KAYA | YA |
| 9 | Ugnayan | OO | UNA | NA |
| 10 | Ugnayan | SANA | SAYA | YA |
| 11 | Pamana | DALA | DAMA | MA |
| 12 | Pamana | HANGA | HALAGA | GA |
| 13 | Pamana | SANGA | HARAYA | YA |
| 14 | Pamana | ALAALA | MAHALAGA | GA |
| 15 | Pamana | PAMANA | MALAYA | YA |

Intentional repeated slots are BATA at Level 3 and INA/AMA at Level 4. Repeated syllable occurrences inside OO and ALAALA are preserved independently of repeated whole-word slots.

## Repository reconciliation

The XLSX records 24 discrepancies: nine source/schema/asset/runtime discrepancies and one cumulative-roster discrepancy for each of the 15 current level assets. Key findings are:

- `CharacterRegistry_Default.asset` and `BaybayinCharacterSO` data contain 18 entries because DA and RA are separate, conflicting with the approved 17-visual model.
- DA and RA have separate templates and different display PNG bytes. The validator also expects 18 character IDs and separate DA/RA coverage.
- ~~The runtime canonicalizer has no DA/RA alias, and `TemplateLoader` groups DA and RA template IDs independently.~~ **Resolved 2026-09-01 (SALIN-212).** `BaybayinIdCanonicalizer` now folds `RA` into `DA`, joining the existing E/I, O/U, PA/FA, BA/VA and SA/ZA groups, so `TemplateLoader` loads `RA_template_01..05` under `DA` alongside `DA_template_01..12` — one key, 17 variants, 121 templates total and none lost. This was a live gameplay bug, not bookkeeping: every consumer of a recognition result compares raw ids (`ActiveEnemyTracker.FindAllWithCharacter`, the active-clue check in `CombatResolver`, `BossController.TryRouteDraw`) and nothing in the game carries RA, so an unfolded `RA` matched nothing and scored a correct draw as a miss. Measured with the project's own recognizer: the three RA-shaped regression draws now return `DA` at 0.916–0.921 against a 0.60 confidence floor.
- The template folder contains 121 variants. DA, HA, KA, and SA exceed the validator's stated 3–5 variants per identity and require recognition-owner review.
- Seven pronunciation clips are linked: BA, DA, HA, KA, O, SA, and WA. Missing contextual pronunciations are explicitly classified as planned; O/U and DA/RA coverage is partial pending reviewer-approved contextual audio.
- Many character records lack dedicated almanac and badge art and currently depend on display-only or fallback behavior.
- Every existing Level 1–15 `allowedCharacters` roster differs from the workbook-derived cumulative pool. Notably, Level 15 currently contains only NGA and does not introduce PA before PAMANA.

Each discrepancy row names its evidence, expected resolution, severity, affected scope, disposition, and owning follow-up. Proposed implementation ownership is assigned to SALIN-170/171 for identity/schema and migration, SALIN-172 for level rosters, SALIN-176 for media, and the recognition owner for template policy.

## Internal validation

The workbook's formula-driven checks report:

- 17 canonical visual rows and 18 contextual value records.
- 30 focus slots, with exactly two slots for every level.
- 30 validated decompositions and character/audio mappings to existing or planned data.
- Three intentional repeated whole-word slots.
- Zero kudlit requirements and zero unsupported modified consonants.
- Zero workbook final-syllable mismatches and zero cumulative-pool coverage failures.
- No `#REF!`, `#DIV/0!`, `#VALUE!`, `#NAME?`, or `#N/A` formula errors.

Pool coverage, decomposition, kudlit/modified-consonant detection, character/audio mapping, final-symbol checks, and final validation are calculated from the visible row data. The QC formulas validate token counts, the ordered spoken/canonical sequence, the final canonical symbol against the workbook requirement, retained symbols across cumulative pools, required character/glyph/template evidence, and existing-or-planned audio references without treating planned clips as silently existing. The four output tables expose Excel filter controls for review.

All four output sheets—Character Matrix, Word Slots, Discrepancies, and Review Log—were rendered and visually checked for legibility. The generated workbook is the authoritative row-level artifact; this document summarizes its rules, evidence, and handoff state.

## Review and approval

The draft is handed off as **Pending TW-RES-001/SALIN-188 Review**. Reviewer findings, dispositions, incorporated evidence, reviewer identity, date, and final approval must be recorded in the XLSX Review Log and reflected here when applicable.

No language/cultural approval is claimed by SALIN-167. Final approval remains pending an explicit TW-RES-001/SALIN-188 decision.
