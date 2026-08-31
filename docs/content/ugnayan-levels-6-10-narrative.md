# Ugnayan Levels 6–10 — Narrative and Memory Content

> **STRUCTURE ONLY — NO COPY WRITTEN YET.** Every Filipino line in this document is a
> `TO BE WRITTEN` placeholder. This file mirrors the structure of
> [`ugat-levels-2-5-narrative.md`](ugat-levels-2-5-narrative.md) so the Ugnayan authoring session has
> a shape to fill rather than a blank page. **Nothing here is approved, and nothing here is a
> suggestion of wording.**
>
> **Status: SCAFFOLD — awaiting authoring, then SALIN-188 language and cultural review.** As with
> Ugat, SALIN-188 is a final acceptance gate on every level Story in this era.
>
> Once the copy exists, **this file becomes the copy of record**: the `DialogueSO` assets are
> generated from it, so edit here first, then regenerate.

## Source of the words

Focus slots are taken verbatim from the **approved** matrix in
`docs/technical/TW-SPK-004-educational-content-matrix.md` §Verified focus slots. They are not invented
here.

| Level | Stable ID | Slot 1 | Slot 2 | Workbook last syllable | Ticket |
|---:|---|---|---|---|---|
| 6 | `level.ugnayan.01` | AWA | GAWA | WA | SALIN-148 |
| 7 | `level.ugnayan.02` | SAMA | KASAMA | MA | SALIN-150 |
| 8 | `level.ugnayan.03` | GANA | KAYA | YA | SALIN-151 |
| 9 | `level.ugnayan.04` | OO | UNA | NA | SALIN-149 |
| 10 | `level.ugnayan.05` | SANA | SAYA | YA | SALIN-152 |

Decompositions are stated in the level Stories' acceptance criteria and use basic symbols only — no
kudlit, per the matrix:

`AWA = A + WA` · `GAWA = GA + WA` · `SAMA = SA + MA` · `KASAMA = KA + SA + MA` ·
`GANA = GA + NA` · `KAYA = KA + YA` · `OO = O + O` · `UNA = U + NA` ·
`SANA = SA + NA` · `SAYA = SA + YA`

The matrix is explicit that occurrences are **never deduplicated**: `OO` is `O + O`, two occurrences
of the same symbol, not one.

### Characters this era introduces

Ugat closed with the pool `A, EI, BA, MA, NA, TA`. Ugnayan adds:

| Level | Introduces | Reused from Ugat | Source |
|---:|---|---|---|
| 6 | **WA**, **GA** | A | SALIN-148 AC1 — *"WA and GA are introduced while previously learned A remains available without being treated as new"* |
| 7 | **SA**, **KA** | MA | SALIN-150 AC2 — *"MA … is reused without being reported as a new character"* |
| 8 | **YA** | GA, NA, KA | KAYA = KA + YA; GANA = GA + NA |
| 9 | **O/U** | NA | SALIN-149 AC1 — *"using the basic O/U character defined by the plan"* |
| 10 | *(none)* | SA, NA, YA | SANA and SAYA are formed entirely from known syllables |

Level 10 introducing no new character is the same shape as Ugat 5: the culminating level tests
**forming new words from known syllables**, not learning new ones.

## Writing standard to follow

Carried from the Ugat document and the shipped Level 1 dialogue, which remains the only approved
sample. **Do not restate these rules per level — they apply throughout.**

- **In-world narrative is Filipino.** No English in dialogue lines. English appears only in the
  `meaning` field, which is developer- and matrix-facing.
- Two speakers only: **`Tagapagsalaysay`** (narrator) and **`Juan`**. The Paglimot is *narrated*,
  never given lines.
- Focus-word explanation template, held exactly:
  `WORD — <kahulugan>. Binubuo ito ng dalawang titik: X at Y.`
  followed by a line directing the player to trace (`Bakasin mo ...`).
  **KASAMA has three syllables**, so this template needs a three-part variant — the first place the
  Ugat template does not fit unchanged.
- Lines stay short. `DialogueLine.text` is `[TextArea(2, 6)]` rendered on a phone in portrait.

## Story arc

Ugnayan means *connection*. The stated purpose of each level is taken from its Story's acceptance
criteria, not invented here.

| Level | Purpose (from the ticket) |
|---:|---|
| 6 | *"care becomes meaningful through action"* — AWA into GAWA |
| 7 | *"how added syllables change a word while reinforcing cooperation"* — SAMA into KASAMA |
| 8 | *"connect persistence with the ability to help my community"* — GANA and KAYA |
| 9 | *"recall of vowel characters and NA"*; the helping-first memory |
| 10 | *"hope and joy return when the village works together"*; closes Era 2, hands off to Pamana |

---

## ⚠️ Open decisions before authoring

These are **not** wording questions and should be settled first.

1. **Asset naming.** Ugat used `Dialogue_Ugat02_*` where the global level number and the era-local
   order happened to coincide. They do not coincide here: Level 6 is `level.ugnayan.01`. Memory IDs
   follow the stable ID (`memory.ugat.02`), which argues for `Dialogue_Ugnayan01_*` — but that reads
   oddly beside "Level 6". **Pick one convention before any asset is generated.** This document uses
   the stable-ID form in the headings below; change it there once and it propagates.
2. **Level 10's canonical paragraph.** SALIN-152 AC2 requires the player to restore *"the canonical
   paragraph"*. The equivalent text for Ugat 5 was never written, which is why SALIN-147's AC2 remains
   unmet. If Ugnayan's paragraph is not authored here it will block Level 10 the same way.
3. **Level 10's Paglimot encounter.** SALIN-152 AC2 also references the *"approved three-phase Paglimot
   extension"*. That is SALIN-184's deliverable, not content — noted so it is not mistaken for a
   writing task.

---

## Level 6 — `level.ugnayan.01` — AWA, GAWA

### Intro — `Dialogue_Ugnayan01_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN* |
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

### Focus word — AWA — `Dialogue_Ugnayan01_Awa`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — template: `AWA — <kahulugan>. Binubuo ito ng dalawang titik: A at WA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN (English, developer-facing)*

### Focus word — GAWA — `Dialogue_Ugnayan01_Gawa`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — template: `GAWA — <kahulugan>. Binubuo ito ng dalawang titik: GA at WA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

### Context copy

> *TO BE WRITTEN.* This is the **challenge prompt** — the text shown during the Context Challenge
> beat. Ugat's equivalents frame the task in-world and state how many words are missing; the count
> stated here is binding on the challenge asset, so be explicit.

### Restored memory — `memory.ugnayan.01`

> *TO BE WRITTEN.* Per SALIN-148 AC3, the memory explains that **care becomes meaningful through
> action**.

### Outro — `Dialogue_Ugnayan01_Outro`

| Speaker | Line |
|---|---|
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

---

## Level 7 — `level.ugnayan.02` — SAMA, KASAMA

### Intro — `Dialogue_Ugnayan02_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN* |
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

### Focus word — SAMA — `Dialogue_Ugnayan02_Sama`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — `SAMA — <kahulugan>. Binubuo ito ng dalawang titik: SA at MA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

### Focus word — KASAMA — `Dialogue_Ugnayan02_Kasama`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — **three** syllables: KA + SA + MA. The Ugat template says "dalawang titik" and does not fit; a three-part variant is needed.* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

> **Teaching note from SALIN-150:** the level's point is *"how added syllables change a word"* —
> KASAMA is SAMA with KA in front. Copy that makes that relationship visible is doing the level's job.

### Context copy

> *TO BE WRITTEN.* State the missing-word count explicitly.

### Restored memory — `memory.ugnayan.02`

> *TO BE WRITTEN.* Per SALIN-150 AC3, the **cooperation** memory.

### Outro — `Dialogue_Ugnayan02_Outro`

| Speaker | Line |
|---|---|
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

---

## Level 8 — `level.ugnayan.03` — GANA, KAYA

**Sentence, given verbatim in SALIN-151 AC1:**

> `Kapag may GANA, mas maraming bagay ang KAYA.`

AC1 states GANA and KAYA are *"the two valid target words"*, so this level restores **two** words
unless that AC is amended. Compare Ugat 3 and 4, where the authored copy said one and the ACs were
amended to match — settle the count here **before** the challenge asset is built, not after.

### Intro — `Dialogue_Ugnayan03_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN* |
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

### Focus word — GANA — `Dialogue_Ugnayan03_Gana`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — `GANA — <kahulugan>. Binubuo ito ng dalawang titik: GA at NA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

### Focus word — KAYA — `Dialogue_Ugnayan03_Kaya`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — `KAYA — <kahulugan>. Binubuo ito ng dalawang titik: KA at YA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

### Context copy

> *TO BE WRITTEN.* Must agree with the blank count above.

### Restored memory — `memory.ugnayan.03`

> *TO BE WRITTEN.* Per SALIN-151 AC3, the intended **community lesson**.

### Outro — `Dialogue_Ugnayan03_Outro`

| Speaker | Line |
|---|---|
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

---

## Level 9 — `level.ugnayan.04` — OO, UNA *(reduced clues)*

**Sentence, given verbatim in SALIN-149 AC3:**

> `Sinabi niyang OO at siya ang naging UNA sa pagtulong.`

This is the era's **reduced-guidance** level, the counterpart to Ugat 4. AC2 requires that the game
*"does not reveal the full answer before an allowed help condition is met"*.

### Intro — `Dialogue_Ugnayan04_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN* |
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

### Focus word — OO — `Dialogue_Ugnayan04_Oo`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — `OO` is `O + O`, the **same symbol twice**. Copy should not imply two different characters.* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

### Focus word — UNA — `Dialogue_Ugnayan04_Una`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — `UNA — <kahulugan>. Binubuo ito ng dalawang titik: U at NA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

> **Note:** O and U are the same canonical symbol (`OU`), as E and I are (`EI`). The copy for OO and
> UNA should read naturally while both spoken values map to one glyph.

### Context copy

> *TO BE WRITTEN.* Ugat 4's equivalent leaned on the absence of guidance — *"Wala nang larawang
> gagabay sa iyo"*. This level has the same job.

### Restored memory — `memory.ugnayan.04`

> *TO BE WRITTEN.* Per SALIN-149 AC3, the **helping-first** memory.

### Outro — `Dialogue_Ugnayan04_Outro`

| Speaker | Line |
|---|---|
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

---

## Level 10 — `level.ugnayan.05` — SANA, SAYA — closes the Ugnayan arc

### Intro — `Dialogue_Ugnayan05_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN* |
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |

### Focus word — SANA — `Dialogue_Ugnayan05_Sana`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — `SANA — <kahulugan>. Binubuo ito ng dalawang titik: SA at NA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

### Focus word — SAYA — `Dialogue_Ugnayan05_Saya`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | *TO BE WRITTEN — `SAYA — <kahulugan>. Binubuo ito ng dalawang titik: SA at YA.`* |
| Tagapagsalaysay | *TO BE WRITTEN — `Bakasin mo ...`* |

`meaning`: *TO BE WRITTEN*

### Canonical paragraph — **required by SALIN-152 AC2**

> *TO BE WRITTEN.* ⚠️ **This is the gap that blocked Ugat 5.** SALIN-147 AC2 requires a canonical
> paragraph that was never written, so that acceptance criterion is still unmet with the level
> otherwise authored. Writing Ugnayan's paragraph **here** avoids repeating that.
>
> Per AC1 it restores SANA and SAYA *"alongside all prior Ugnayan words"* — AWA, GAWA, SAMA, KASAMA,
> GANA, KAYA, OO, UNA.

### Context copy

> *TO BE WRITTEN.*

### Restored memory — `memory.ugnayan.05`

> *TO BE WRITTEN.* Per SALIN-152, **hope and joy return when the village works together**.

### Ugnayan ending — `Dialogue_Ugnayan05_Outro`

| Speaker | Line |
|---|---|
| Juan | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN* |
| Tagapagsalaysay | *TO BE WRITTEN — hands off to Pamana, as Ugat's outro hands off to Ugnayan.* |

---
