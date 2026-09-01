# Pamana Levels 11–15 — Narrative and Memory Content

> **STRUCTURE ONLY — NO COPY WRITTEN YET.** Every Filipino line in this document is a
> `TO BE WRITTEN` placeholder. Nothing here is authored copy, and nothing here should be
> pasted into an asset as-is.
>
> Two exceptions, both quoted verbatim from their tickets and therefore already the team's
> Filipino: the **Level 13 sentence** (SALIN-155 AC1) and the **Level 14 sentence** (SALIN-156 AC4).
> Use those exactly as written.

> **Status: SCAFFOLD — awaiting authoring, then SALIN-188 language and cultural review.** This
> mirrors `ugnayan-levels-6-10-narrative.md`, which mirrors the finished
> `ugat-levels-2-5-narrative.md`. Ugat is the model to match: it carries intro dialogue, per-focus-word
> dialogue, context copy, restored-memory text, and outro, all in finished Filipino.
>
> Until this document is authored, **Pamana level work will keep producing prompts written by whoever
> implements the ticket.** That has already happened once: Level 11's two context prompts
> ([PR #177](https://github.com/chdean-09/salinlahi/pull/177)) are placeholder-quality and were
> written during implementation precisely because this document did not exist. They should be
> replaced from here.

## Source of the words

The 30 focus-word slots come from the approved matrix,
[`docs/technical/TW-SPK-004-educational-content-matrix.md`](../technical/TW-SPK-004-educational-content-matrix.md).
Pamana is rows 11–15:

| Level | Stable ID | Slot 1 | Slot 2 | Final syllable | Ticket |
|---:|---|---|---|---|---|
| 11 | `level.pamana.01` | DALA | DAMA | MA | SALIN-153 |
| 12 | `level.pamana.02` | HANGA | HALAGA | GA | SALIN-154 |
| 13 | `level.pamana.03` | SANGA | HARAYA | YA | SALIN-155 |
| 14 | `level.pamana.04` | ALAALA | MAHALAGA | GA | SALIN-156 |
| 15 | `level.pamana.05` | PAMANA | MALAYA | YA | **SALIN-158** |

> **Ticket numbering does not run straight.** Level 15 is **SALIN-158**, not SALIN-157 —
> SALIN-157 is "Hear each required syllable at the moment of learning" (BL-E6-S1) and is not a level
> at all. The same trap exists in Ugnayan, where SALIN-149 is Level 9 rather than Level 7. Check the
> summary before assuming a key.

### Decompositions, from the tickets

Quoted from acceptance criteria wherever the ticket states them:

- `DALA = DA + LA` · `DAMA = DA + MA` — SALIN-153 AC2
- `HANGA = HA + NGA` · `HALAGA = HA + LA + GA` — SALIN-154 AC1
- `SANGA = SA + NGA` · **`HARAYA = HA + RA + YA`** — SALIN-155 AC1
- `PAMANA` · `MALAYA` — SALIN-158 states none; **derive and confirm**
- `ALAALA` · `MAHALAGA` — SALIN-156 states none, only that *"repeated syllables are represented
  accurately and in order"*; **derive and confirm**

### Characters this era introduces

| Level | Introduces | Cumulative pool |
|---:|---|---:|
| 11 | DA/RA, LA | 14 |
| 12 | HA, NGA | 16 |
| 13 | *(none)* | 16 |
| 14 | *(none)* | 16 |
| 15 | PA | **17** |

Level 15 is the first level whose pool is the **entire taught set of 17**, which is also the number
of draws `BossConfig_Kadiliman` requires. Those two numbers agreeing is not a coincidence and should
stay that way — see SALIN-212.

## Writing standard to follow

Match `ugat-levels-2-5-narrative.md`. Concretely, from the shipped Ugat context copy:

> *"Isang salita lamang ang kulang sa pangungusap ni Ama. Ilagay mo ang tamang salita sa tamang puwang."*
> *"Wala nang larawang gagabay sa iyo. Piliin mo ang salitang nararapat, mula lamang sa iyong alaala."*

The pattern is **one thematic sentence, then one instruction sentence**. Second person, addressed to
the player. No romanised syllables in player-facing copy — the glyph is the thing being taught.

## Story arc

Pamana is the inheritance era: what was carried (11), what is valued (12), what each generation
imagines (13), why memory matters (14), and what is finally passed on (15). Juan's journey ends here.

| Level | Beat | Ticket's own words for the memory |
|---:|---|---|
| 11 | Carrying the past forward | *"carrying and understanding past lessons"* (AC3) |
| 12 | Recognising who protected it | *"the cultural-protection memory"* (AC3) |
| 13 | Each generation as a branch | *"the future-generation memory"* (AC3) |
| 14 | Memory as the origin of identity | AC4's sentence carries it |
| 15 | Memory becomes inheritance | *"the message about memory becoming inheritance"* (AC4) |

## ⚠️ Open decisions before authoring

1. **`HARAYA` needs `value.ra`, and the tooling cannot currently express that.** SALIN-155 AC1
   requires `HA + RA + YA`. RA is not a separate symbol — it is `Char_DA` read with its **second**
   spoken value, `value.ra`. `CampaignLevelDataTool.SpokenValueId()` always takes
   `spokenValues[0]`, which is `value.da`. **Level 13 is the first and only place in the campaign
   where the second spoken value is load-bearing**, and the tool needs per-syllable value selection
   before it can author that level. This is the DA/RA design working exactly as intended, not a
   defect — see SALIN-212.
2. **SALIN-155 AC2 names a convention that does not exist in writing.** *"its display and
   pronunciation follow the approved educational convention"* — no document defines what the player
   sees or hears when the shared `ᜇ` glyph is read as RA rather than DA. This needs a ruling, and it
   is a language-and-culture question, not an engineering one.
3. **`ALAALA` decomposition.** `A + LA + A + LA + A`? The matrix is explicit that occurrences are
   never deduplicated, and SALIN-156 AC1 asks for repeated syllables "accurately and in order", but
   neither states the split. Confirm before authoring.
4. **`MAHALAGA`, `PAMANA`, `MALAYA` decompositions** are likewise unstated. Presumed
   `MA + HA + LA + GA`, `PA + MA + NA`, `MA + LA + YA`.
5. **Level 14's timed memory.** SALIN-156 AC3 requires a timer whose expiry applies "the approved
   penalty defined by `LF-CONTRACT-v2`" while leaving the level recoverable. `ChallengeMode` has a
   `TimedMemory` member that no authored challenge uses yet. Level 14 would be the first.
6. **The final paragraph does not exist.** SALIN-158 AC3 requires restoring "the configured final
   paragraph across all three phases". The same missing paragraph already blocks SALIN-147 AC2 and
   SALIN-152 AC2. One paragraph, three tickets waiting on it.
7. **Clue policy for Levels 11–15.** The shipped escalation is Full (Ugat 1–3) → Reduced (Ugat 4,
   Ugat 5, Ugnayan 9). Level 11 shipped as **Reduced** because it introduces DA/RA and LA and its
   story asks for guided instruction; SALIN-156 explicitly says reduced for Level 14. Levels 12, 13
   and 15 are unruled. Minimal has never been used.
8. **SALIN-156 has an empty User Story section.** Worth filling in, since it is the only level story
   without one.

---

## Level 11 — `level.pamana.01` — DALA, DAMA

> **Data and challenge already authored** ([PR #177](https://github.com/chdean-09/salinlahi/pull/177)).
> The two context prompts currently in `Challenge_Pamana11_Context` were written during
> implementation because this document did not exist. **Replace them from the Context copy below
> once authored.**

### Intro — `Dialogue_Pamana01_Intro`

TO BE WRITTEN — Juan enters the final era carrying everything from Ugat and Ugnayan.

### Focus word — DALA — `Dialogue_Pamana01_Dala`

TO BE WRITTEN — what it means to carry something forward.

### Focus word — DAMA — `Dialogue_Pamana01_Dama`

TO BE WRITTEN — carrying is not enough without feeling and understanding.

> **Teaching note from SALIN-153 AC1:** this level introduces **DA/RA and LA** while reusing A and
> MA. The copy should let DA feel new without implying the player has never seen A or MA.

### Context copy

TO BE WRITTEN — thematic sentence, then instruction. Two prompts, one per focus word.

### Restored memory — `memory.pamana.01`

TO BE WRITTEN — AC3: "carrying and understanding past lessons".

### Outro — `Dialogue_Pamana01_Outro`

TO BE WRITTEN — Level 12 unlocks.

---

## Level 12 — `level.pamana.02` — HANGA, HALAGA

### Intro — `Dialogue_Pamana02_Intro`

TO BE WRITTEN — the people who protected the script.

### Focus word — HANGA — `Dialogue_Pamana02_Hanga`

TO BE WRITTEN — admiration for those who kept it alive.

### Focus word — HALAGA — `Dialogue_Pamana02_Halaga`

TO BE WRITTEN — what that protection was worth.

> **Teaching note from SALIN-154 AC2:** GA was learned in Ugnayan. When HALAGA is practised the game
> must reuse it "without a duplicate introduction" — the copy should not reintroduce GA as new.

### Context copy

TO BE WRITTEN.

### Restored memory — `memory.pamana.02`

TO BE WRITTEN — AC3: "the cultural-protection memory".

### Outro — `Dialogue_Pamana02_Outro`

TO BE WRITTEN — Level 13 unlocks.

---

## Level 13 — `level.pamana.03` — SANGA, HARAYA

### Sentence — **verbatim from SALIN-155 AC1, do not rewrite**

> `Bawat salinlahi ay isang SANGA na may sariling HARAYA para sa kinabukasan.`

This is the only place in the game where the title word *salinlahi* appears in player-facing copy.

### Intro — `Dialogue_Pamana03_Intro`

TO BE WRITTEN — each generation as a branch of one tree.

### Focus word — SANGA — `Dialogue_Pamana03_Sanga`

TO BE WRITTEN.

### Focus word — HARAYA — `Dialogue_Pamana03_Haraya`

TO BE WRITTEN — imagination as what each branch adds.

> **⚠️ This is the RA level.** `HARAYA = HA + RA + YA` is the single point in the whole campaign
> where the shared `ᜇ` glyph is read as **RA** rather than DA. SALIN-155 AC2 requires its display and
> pronunciation to follow "the approved educational convention" — **which is not written down
> anywhere.** See open decision 2. The copy here may need to acknowledge the shared glyph explicitly;
> that is a language-review call.

### Context copy

TO BE WRITTEN — the sentence above supplies the frame; this is the instruction around it.

### Restored memory — `memory.pamana.03`

TO BE WRITTEN — AC3: "the future-generation memory".

### Outro — `Dialogue_Pamana03_Outro`

TO BE WRITTEN — Level 14 unlocks.

---

## Level 14 — `level.pamana.04` — ALAALA, MAHALAGA *(reduced clues, timed)*

### Sentence — **verbatim from SALIN-156 AC4, do not rewrite**

> `Ang ALAALA ay MAHALAGA dahil dito nagsisimula ang pagkilala sa ating pinagmulan.`

### Intro — `Dialogue_Pamana04_Intro`

TO BE WRITTEN — the longest words in the game, under time pressure.

### Focus word — ALAALA — `Dialogue_Pamana04_Alaala`

TO BE WRITTEN.

### Focus word — MAHALAGA — `Dialogue_Pamana04_Mahalaga`

TO BE WRITTEN.

> **Teaching note from SALIN-156 AC2:** guidance is reduced, and progress must stay visible "without
> revealing every remaining character". The copy should reassure without giving the answer.

### Timer failure copy

TO BE WRITTEN — AC3: when the timer expires the checkpoint penalty applies and the level stays
recoverable. The player needs to understand they have not lost the level. **No existing level has
this state**, so there is no precedent copy to borrow.

### Context copy

TO BE WRITTEN.

### Restored memory — `memory.pamana.04`

TO BE WRITTEN — the sentence above already carries the idea; the memory should extend rather than
repeat it.

### Outro — `Dialogue_Pamana04_Outro`

TO BE WRITTEN — Level 15 unlocks.

---

## Level 15 — `level.pamana.05` — PAMANA, MALAYA — ends the game

### Intro — `Dialogue_Pamana05_Intro`

TO BE WRITTEN — the last level; the Living Scroll is nearly whole.

### PA instruction — **required before assessment**

TO BE WRITTEN — SALIN-158 AC1: PA is required for the first time here, and guided instruction and
practice must occur **before** PAMANA assesses it. PA is the seventeenth and final symbol; the copy
should carry that weight.

### Focus word — PAMANA — `Dialogue_Pamana05_Pamana`

TO BE WRITTEN — the title of the era and the name of what is being passed on.

### Focus word — MALAYA — `Dialogue_Pamana05_Malaya`

TO BE WRITTEN — freedom as the result of remembering.

### Final paragraph — **required by SALIN-158 AC3**

TO BE WRITTEN — **and it does not exist.** Restored across all three phases of the final Paglimot
encounter. The same paragraph is required by SALIN-147 AC2 and SALIN-152 AC2. Authoring it once
unblocks three tickets. See open decision 6.

### Context copy

TO BE WRITTEN.

### Restored memory — `memory.pamana.05`

TO BE WRITTEN.

### Final message — `Dialogue_Pamana05_Outro`

TO BE WRITTEN — SALIN-158 AC4: "the message about memory becoming inheritance". This is the last
thing the player reads. It closes Juan's journey and the game.

### Completed-journey state

TO BE WRITTEN — AC5 and AC6: review, replay and Credits are available, and the state survives
reopening the app. AC7: **no enabled control may promise Endless Mode**, which has no approved story.
Any copy here must not gesture at content that does not exist.
