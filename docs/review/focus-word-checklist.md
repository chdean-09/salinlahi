# Language and Cultural Review — Focus-Word Checklist

> SALIN-188 / TW-RES-001. One row per workbook focus-word slot (15 levels × 2 slots = 30).
> A qualified language/cultural adviser reviews each slot: word choice, meaning, decomposition,
> Baybayin labels, pronunciation, sentence/paragraph copy, learning sequence, and cultural
> framing. Status values: `Unreviewed` → `Flagged` (see findings log) → `Approved`.
> Words marked *(per approved matrix)* come from the external workbook
> (CORE GAME MECHANICS.xlsx, checksum in `ContentIdentity.ApprovedWorkbookSha256`) and must be
> filled in from the matrix before that row can be reviewed.

> **Provenance note (SALIN-204).** Words and decompositions for rows 7-10 are filled from the approved
> workbook matrix (`docs/technical/TW-SPK-004-educational-content-matrix.xlsx`, sheet "Verified 30
> Focus-Word Slots"). Rows 7 and 8 reuse the meanings approved at rows 1 and 2, since Level 4 repeats
> INA/AMA. Rows 9 and 10 are marked **(derived)**: no approved English gloss for IBA or MANA exists in
> this repository, so these were rendered from the SALIN-205 narrative copy — "ang naiiba, ang hindi
> katulad ng dati" and "ang minana mula sa nauna, ang ipinapasa sa susunod". They are the only two
> meanings shipped in the campaign that are not traceable to an approved source, and the validator only
> checks that a meaning is non-blank. **Adviser: please confirm or replace these two first.**

| # | Level | Slot | Word | Meaning (EN) | Decomposition | Intentional repeat | Status | Adviser | Date |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | level.ugat.01 | focus.01 | INA | mother | EI + NA | — | Unreviewed | | |
| 2 | level.ugat.01 | focus.02 | AMA | father | A + MA | — | Unreviewed | | |
| 3 | level.ugat.02 | focus.01 | BATA | child | BA + TA | — | Unreviewed | | |
| 4 | level.ugat.02 | focus.02 | MATA | eye | MA + TA | — | Unreviewed | | |
| 5 | level.ugat.03 | focus.01 | BATA | child | BA + TA | repeat of #3 | Unreviewed | | |
| 6 | level.ugat.03 | focus.02 | TAMA | correct | TA + MA | — | Unreviewed | | |
| 7 | level.ugat.04 | focus.01 | INA | mother | EI + NA | repeat of #1 | Unreviewed | | |
| 8 | level.ugat.04 | focus.02 | AMA | father | A + MA | repeat of #2 | Unreviewed | | |
| 9 | level.ugat.05 | focus.01 | IBA | different **(derived)** | EI + BA | — | Unreviewed | | |
| 10 | level.ugat.05 | focus.02 | MANA | inheritance **(derived)** | MA + NA | — | Unreviewed | | |
| 11 | level.ugnayan.01 | focus.01 | AWA | mercy/compassion | A + WA | — | Unreviewed | | |
| 12 | level.ugnayan.01 | focus.02 | GAWA | work/deed | GA + WA | — | Unreviewed | | |
| 13 | level.ugnayan.02 | focus.01 | SAMA | to accompany | SA + MA | — | Unreviewed | | |
| 14 | level.ugnayan.02 | focus.02 | KASAMA | companion | KA + SA + MA | builds on #13 | Unreviewed | | |
| 15 | level.ugnayan.03 | focus.01 | GANA | appetite/gusto | GA + NA | — | Unreviewed | | |
| 16 | level.ugnayan.03 | focus.02 | KAYA | ability/can | KA + YA | — | Unreviewed | | |
| 17 | level.ugnayan.04 | focus.01 | (per approved matrix) | | | | Unreviewed | | |
| 18 | level.ugnayan.04 | focus.02 | (per approved matrix) | | | | Unreviewed | | |
| 19 | level.ugnayan.05 | focus.01 | (per approved matrix) | | | | Unreviewed | | |
| 20 | level.ugnayan.05 | focus.02 | (per approved matrix) | | | | Unreviewed | | |
| 21 | level.pamana.01 | focus.01 | DALA | to carry | DA + LA | — | Unreviewed | | |
| 22 | level.pamana.01 | focus.02 | DAMA | to feel | DA + MA | — | Unreviewed | | |
| 23 | level.pamana.02 | focus.01 | HANGA | admiration | HA + NGA | — | Unreviewed | | |
| 24 | level.pamana.02 | focus.02 | HALAGA | value/worth | HA + LA + GA | — | Unreviewed | | |
| 25 | level.pamana.03 | focus.01 | SANGA | branch | SA + NGA | — | Unreviewed | | |
| 26 | level.pamana.03 | focus.02 | HARAYA | imagination | HA + RA + YA | — | Unreviewed | | |
| 27 | level.pamana.04 | focus.01 | (per approved matrix) | | | | Unreviewed | | |
| 28 | level.pamana.04 | focus.02 | (per approved matrix) | | | | Unreviewed | | |
| 29 | level.pamana.05 | focus.01 | PAMANA | heritage/legacy | PA + MA + NA | — | Unreviewed | | |
| 30 | level.pamana.05 | focus.02 | MALAYA | free | MA + LA + YA | — | Unreviewed | | |

Beyond the 30 slots, the adviser also reviews: intentional repeats and decomposition
progressions, sentence and paragraph challenge copy, Baybayin glyph labels, recorded
pronunciations, the era learning sequence, and the cultural framing of each era's
narrative (see `docs/content/` narrative files as they land).
