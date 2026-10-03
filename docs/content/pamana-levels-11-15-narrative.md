# Pamana Levels 11–15 — Narrative and Memory Content

> **DRAFT COPY ADDED — NOT APPROVED.** The proposal at the end of this document is for language
> and cultural review only. Do not copy draft lines into dialogue or challenge assets until the
> user approves them.
> `TO BE WRITTEN` placeholder. Nothing here is authored copy, and nothing here should be
> pasted into an asset as-is.
>
> Two exceptions, both quoted verbatim from their tickets and therefore already the team's
> Filipino: the **Level 13 sentence** (SALIN-155 AC1) and the **Level 14 sentence** (SALIN-156 AC4).
> Use those exactly as written.

> **Status: DRAFT — awaiting SALIN-188 language and cultural review.** This
> mirrors `ugnayan-levels-6-10-narrative.md`, which mirrors the finished
> `ugat-levels-2-5-narrative.md`. Ugat is the model to match: it carries intro dialogue, per-focus-word
> dialogue, context copy, restored-memory text, and outro, all in finished Filipino.
>
> Level 11's two context prompts ([PR #177](https://github.com/chdean-09/salinlahi/pull/177)) were
> written during implementation before this narrative draft existed. Review them against the
> proposal below before replacing any asset copy.

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
| 11 | DA, LA | 14 |
| 12 | HA, NGA | 16 |
| 13 | RA | 17 |
| 14 | *(none)* | 17 |
| 15 | PA | **18** |

Level 15 is the first level whose pool is the **entire taught set of 18**. The current design ruling
requires a separate RA character introduced at Level 13; older matrix and narrative passages that
merge DA/RA or report a 17-character set are superseded by
`docs/design/spec-rulings-2026-09.md` Q2 and OQ-6.

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

> **Superseded DA/RA notes:** items 1 and 2 below reflect an older shared-character assumption.
> OQ-6 now makes DA and RA separate characters and introduces RA at Level 13. Do not use those two
> historical notes as current requirements; the proposals below preserve RA as a separate ID.

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
3. ~~**`ALAALA` decomposition.**~~ **Resolved 2026-09-01: `A + LA + A + LA`, four syllables.** This
   entry originally guessed five (`A + LA + A + LA + A`), which spells *alaalaa*. The trailing vowel
   of *alaala* is the inherent vowel of the second LA, not a further standalone A. Authored in
   SALIN-156.
4. **`MAHALAGA`, `PAMANA`, `MALAYA` decompositions** are likewise unstated. Presumed
   `MA + HA + LA + GA`, `PA + MA + NA`, `MA + LA + YA`.
5. **Level 14's timed memory.** SALIN-156 AC3 requires a timer whose expiry applies "the approved
   penalty defined by `LF-CONTRACT-v2`" while leaving the level recoverable. `ChallengeMode` has a
   `TimedMemory` member that no authored challenge uses yet. Level 14 would be the first.
6. ~~**The final paragraph does not exist.**~~ A three-line paragraph proposal is now included below
   for review. It remains unapproved and must not be wired into assets until the user accepts the
   Filipino copy. The configured three-checkpoint flow is authored separately from this draft.
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

> **Teaching note from SALIN-153 AC1:** this level introduces **DA and LA** while reusing A and
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

> **RA is introduced here as a separate character.** `HARAYA = HA + RA + YA` uses the RA character
> ID and enemy specified by OQ-6. The older SALIN-155 AC2 shared-character wording is superseded;
> do not describe RA as a second DA reading.

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
practice must occur **before** PAMANA assesses it. PA is the eighteenth character; the copy should
carry that weight. YA remains the final restored character in MALAYA.

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

## Draft copy proposal — review required

Every line in this section is **DRAFT — NOT APPROVED**. These Filipino proposals are documentation-only until language and cultural review is complete and the user approves them. The approved Level 13 and Level 14 sentences above remain verbatim.

### Level 11 — DALA, DAMA

- **Intro:** Tagapagsalaysay: “Sa bagong yugto ng paglalakbay, dala ni Juan ang mga aral ng naunang mga salinlahi.” Juan: “Paano ko iingatan ang mga ito?” Tagapagsalaysay: “Unawain muna natin ang dinadala at nadarama mo.”
- **DALA:** “DALA — bagay o aral na bitbit mula sa nakaraan. Binubuo ito ng dalawang titik: DA at LA.” / “Bakasin mo ang DALA.” Meaning: *carry; bring*.
- **DAMA:** “DAMA — pakiramdam o pag-unawang nadarama sa sarili. Binubuo ito ng dalawang titik: DA at MA.” / “Bakasin mo ang DAMA.” Meaning: *feel; sense*.
- **Context:** “DALA ko ang aral ng mga nauna, at DAMA ko ang bigat ng kanilang pinagdaanan.”
- **Memory:** “Kapag nauunawaan natin ang mga aral noon, naaalagaan natin ang mga ito ngayon.”
- **Outro:** Juan: “Hindi ko lamang dala ang mga aral; nauunawaan ko rin ang kanilang pinagmulan.” Tagapagsalaysay: “Sa susunod, kikilalanin natin ang mga nag-ingat sa mga ito.”

### Level 12 — HANGA, HALAGA

- **Intro:** Tagapagsalaysay: “May mga taong nag-ingat sa mga titik at nagbahagi nito sa iba.” Juan: “Nais kong makilala ang kanilang ginawa.” Tagapagsalaysay: “Pag-isipan natin kung bakit sila hinahangaan at kung ano ang halaga ng kanilang pag-iingat.”
- **HANGA:** “HANGA — paghanga sa kabutihan o husay ng iba. Binubuo ito ng dalawang titik: HA at NGA.” / “Bakasin mo ang HANGA.” Meaning: *admiration*.
- **HALAGA:** “HALAGA — kabuluhan o saysay ng isang bagay. Binubuo ito ng tatlong titik: HA, LA, at GA.” / “Bakasin mo ang HALAGA.” Meaning: *value; worth*.
- **Context:** “HANGA ako sa mga nag-ingat sa mga titik; mahalaga ang HALAGA ng kanilang ambag.”
- **Memory:** “Ang pag-iingat sa mga titik ay nagpanatili ng mahalagang bahagi ng ating kultura.”
- **Outro:** Juan: “Mahalaga ang ginawa ng mga nauna sa atin.” Tagapagsalaysay: “Bawat salinlahi ay may sariling ambag sa kinabukasan.”

### Level 13 — SANGA, HARAYA

- **Intro:** Tagapagsalaysay: “Bawat salinlahi ay dugtong sa punong nag-ugat sa mga nauna.” Juan: “May maidaragdag din ba ako sa susunod?” Tagapagsalaysay: “Oo. Bawat isa ay maaaring mangarap at mag-iwan ng bagong sanga.”
- **SANGA:** “SANGA — bahagi ng punong tumutubo mula sa katawan o ibang sanga nito. Binubuo ito ng dalawang titik: SA at NGA.” / “Bakasin mo ang SANGA.” Meaning: *branch*.
- **HARAYA:** “HARAYA — imaheng nabubuo sa isip at nagbibigay-hugis sa pangarap. Binubuo ito ng tatlong titik: HA, RA, at YA.” / “Bakasin mo ang HARAYA.” Meaning: *imagination*.
- **RA teaching note:** RA is a separate character from DA and is introduced at this level under OQ-6. Review player-facing teaching copy for RA; do not describe it as a shared character or a second DA reading.
- **Context:** Use the approved sentence verbatim: “Bawat salinlahi ay isang SANGA na may sariling HARAYA para sa kinabukasan.” Draft instruction: “Ibalik ang SANGA at HARAYA sa pangungusap.”
- **Memory:** “Bawat salinlahi ay may maiaambag na pangarap sa kinabukasan.”
- **Outro:** Juan: “May sarili akong pangarap, at bahagi ako ng mas mahabang salinlahi.” Tagapagsalaysay: “Sa susunod, alalahanin natin kung saan nagsisimula ang pagkilala sa ating pinagmulan.”

### Level 14 — ALAALA, MAHALAGA

- **Intro:** Tagapagsalaysay: “May mga alaala ng pinagmulan na kailangang ingatan.” Juan: “Paano ko malalaman kung alin ang dapat kong tandaan?” Tagapagsalaysay: “Unawain ang pangungusap at isaayos ang dalawang nawawalang salita. Makikita mo ang natapos mo habang nagpapatuloy.”
- **ALAALA:** “ALAALA — bagay o pangyayaring nananatili sa isip. Binubuo ito ng apat na titik: A, LA, A, at LA.” / “Bakasin mo ang ALAALA.” Meaning: *memory*.
- **MAHALAGA:** “MAHALAGA — may malaking saysay o kabuluhan. Binubuo ito ng apat na titik: MA, HA, LA, at GA.” / “Bakasin mo ang MAHALAGA.” Meaning: *important; valuable*.
- **Context:** Use the approved sentence verbatim: “Ang ALAALA ay MAHALAGA dahil dito nagsisimula ang pagkilala sa ating pinagmulan.” Draft instruction: “Ibalik ang ALAALA at MAHALAGA sa tamang puwang, ayon sa pagkakasunod.”
- **Timer expiry:** “Naubos ang oras sa bahaging ito. Nananatili ang iyong naibalik; subukan muli ang bahaging ito.” Confirm the penalty wording against LF-CONTRACT-v2 before asset use.
- **Memory:** “Sa pag-alala sa ating pinagmulan, mas nauunawaan natin kung sino tayo.”
- **Outro:** Juan: “Mas malinaw na sa akin kung bakit mahalaga ang mga alaala.” Tagapagsalaysay: “Huling bahagi na lamang ang natitira: ang ipapasa natin sa susunod na salinlahi.”

### Level 15 — PAMANA, MALAYA

- **Intro:** Tagapagsalaysay: “Halos buo na ang buhay na balumbon. Narito si Paglimot upang burahin ang mga naingatan.” Juan: “Hindi ko hahayaang mawala ang mga alaala.” Tagapagsalaysay: “Harapin natin ito nang yugto-yugto, dala ang mga aral ng bawat kapanahunan.”
- **PA instruction:** “Ito ang PA, ang bagong titik sa huling yugto. Bakasin mo muna ito at sanayin bago buuin ang PAMANA.” Meaning: *guided introduction to PA*. Review its wording and placement against SALIN-158 AC1.
- **PAMANA:** “PAMANA — aral o bagay na ipinasa ng mga nauna. Binubuo ito ng tatlong titik: PA, MA, at NA.” / “Bakasin mo ang PAMANA.” Meaning: *inheritance; legacy*.
- **MALAYA:** “MALAYA — hindi nakagapos o napipigilan. Binubuo ito ng tatlong titik: MA, LA, at YA.” / “Bakasin mo ang MALAYA.” Meaning: *free*.
- **Three-checkpoint paragraph proposal (Ugat → Ugnayan → Lahat):** 1. “Ang alaala ng mga nauna ay PAMANA.” 2. “Sa ugnayan, pinangangalagaan natin ang PAMANA.” 3. “Kapag iningatan natin ang alaala, MALAYA ang kinabukasan.” The third line places MALAYA last; YA remains its last restored character. Review wording and token placement before using these lines in the three authored segments.
- **Context instruction:** “Ibalik ang mga salita sa bawat bahagi ng pangungusap. Dalhin ang natutuhan mo hanggang sa huling puwang.”
- **Memory:** “Ang alaalang iningatan ay nagiging pamana para sa susunod na salinlahi.”
- **Final message:** “Ang alaala ay naging pamana. Mula sa mga nauna, ipinasa natin ito sa susunod na salinlahi.” Review before authoring the final cutscene.
- **Completed-journey prompt:** “Maaari mong balikan ang mga natapos na aral o panoorin muli ang wakas.” Do not promise any unapproved game mode.
