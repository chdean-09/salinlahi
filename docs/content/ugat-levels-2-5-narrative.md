# Ugat Levels 2–5 — Narrative and Memory Content

> **SALIN-205.** Source of truth for the intros, focus-word explanations, context copy, restored
> memories, and the Ugat ending for Levels 2–5. The `DialogueSO` assets are generated from this
> document, so **this file is the copy of record** — edit here first, then regenerate.
>
> **Status: DRAFT — awaiting SALIN-188 language and cultural review.** The ticket names that review as
> a final acceptance gate. This copy was drafted against the approved focus-word matrix and the Level 1
> voice, but **it has not been reviewed by a Filipino language or cultural authority and must not be
> treated as approved.** Wording, register, and cultural framing are all open to correction.

## Source of the words

Focus slots are taken verbatim from the **approved** matrix in
`docs/technical/TW-SPK-004-educational-content-matrix.md` §Verified focus slots. They are not invented
here.

| Level | Slot 1 | Slot 2 | Workbook last syllable | Ticket |
|---:|---|---|---|---|
| 2 | BATA | MATA | TA | SALIN-145 |
| 3 | BATA *(repeat)* | TAMA | MA | SALIN-144 |
| 4 | INA *(repeat)* | AMA *(repeat)* | MA | SALIN-146 |
| 5 | IBA | MANA | NA | SALIN-147 |

Decompositions use basic symbols only — no kudlit, per the matrix:
`BATA = BA + TA` · `MATA = MA + TA` · `TAMA = TA + MA` · `INA = I + NA` · `AMA = A + MA` ·
`IBA = I + BA` · `MANA = MA + NA`

The matrix flags BATA at Level 3 and INA/AMA at Level 4 as **intentional repeats**. The copy leans into
that rather than hiding it: Level 3 reuses BATA inside a full sentence, and Level 4 asks for INA and AMA
back with fewer clues. A repeat that reads as an accident would look like a content bug.

## Writing standard followed

Taken from the shipped Level 1 dialogue, which is the only approved sample:

- **In-world narrative is Filipino.** No English in dialogue lines. English appears only in the
  `meaning` field, which is developer- and matrix-facing (Level 1 uses `mother` / `father`).
- Two speakers only: **`Tagapagsalaysay`** (narrator) and **`Juan`**. Level 1 introduces no others, so
  none are introduced here — the Paglimot is *narrated*, never given lines. Giving it a voice would be
  a design decision this ticket does not own.
- Focus-word explanation template, held exactly:
  `WORD — <kahulugan>. Binubuo ito ng dalawang titik: X at Y.`
  followed by a line directing the player to trace (`Bakasin mo ...`).
- Lines stay short. `DialogueLine.text` is a `[TextArea(2, 6)]` field rendered on a phone in portrait,
  so each line is kept to roughly one or two breaths of speech.

## Story arc

Ugat means *root*. The four levels move from what Juan lost, through seeing himself, to what he can
still pass on.

| Level | Purpose |
|---:|---|
| 2 | Juan sees the child he was. The eye that remembers. |
| 3 | Affirmation — the words become a whole sentence, and his father's praise returns. |
| 4 | Recall without help. The memory is his, not borrowed. |
| 5 | What is inherited. The Ugat era closes and hands off to Ugnayan. |

---

## Level 2 — `level.ugat.02` — BATA, MATA

### Intro — `Dialogue_Ugat02_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | Bumalik si Juan sa lumang tahanan. May naaninag siyang anino ng isang batang naglalaro sa bakuran. |
| Juan | Kilala ko ang batang iyan... ngunit hindi ko makita ang kanyang mukha. |
| Tagapagsalaysay | Ang Paglimot ay unang kumukuha sa mga mata. Ibalik mo ang dalawang salitang magpapakita muli sa iyo: BATA at MATA. |

### Focus word — BATA — `Dialogue_Ugat02_Bata`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | BATA — ang musmos na sumisibol, ang simula ng bawat alaala. Binubuo ito ng dalawang titik: BA at TA. |
| Tagapagsalaysay | Bakasin mo ang bawat titik upang maibalik ang alaala ng batang si Juan. |

`meaning`: `child`

### Focus word — MATA — `Dialogue_Ugat02_Mata`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | MATA — ang nakakikita at nakaaalala. Binubuo ito ng dalawang titik: MA at TA. |
| Tagapagsalaysay | Bakasin mo ang bawat titik upang muling makita ang nakaraan. |

`meaning`: `eye`

### Context copy

> Hindi makikita ang mukha ng bata hangga't walang MATA. Buuin mo ang dalawang salita upang mabuo ang larawan.

### Restored memory — `memory.ugat.02`

> Ang batang si Juan, naglalaro sa bakuran ng kanilang tahanan, habang minamasdan siya ni Ina mula sa bintana.

### Outro — `Dialogue_Ugat02_Outro`

| Speaker | Line |
|---|---|
| Juan | Nakikita ko na siya. Ako pala iyon noong bata pa ako. |
| Tagapagsalaysay | Ang matang nakakikita sa sarili ang unang hakbang sa pag-alala, Juan. |

---

## Level 3 — `level.ugat.03` — BATA (repeat), TAMA

### Intro — `Dialogue_Ugat03_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | Sa lilim ng punong mangga, may naririnig si Juan na tinig — ang tinig ng kanyang ama. |
| Juan | May sinasabi siya tungkol sa akin. Ngunit hindi ko na maalala ang buong pangungusap. |
| Tagapagsalaysay | Hindi sapat ang isang salita ngayon. Buuin mo ang buong pangungusap: balikan ang BATA, at hanapin ang TAMA. |

### Focus word — BATA (repeat) — `Dialogue_Ugat03_Bata`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | BATA — nabakas mo na ito noon. Ngayon, gagamitin mo ito sa loob ng isang buong pangungusap. Binubuo pa rin ito ng dalawang titik: BA at TA. |
| Tagapagsalaysay | Bakasin mo itong muli — hindi bilang bagong salita, kundi bilang bahagi ng isang buong diwa. |

`meaning`: `child`

### Focus word — TAMA — `Dialogue_Ugat03_Tama`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | TAMA — ang wasto, ang nararapat. Binubuo ito ng dalawang titik: TA at MA. |
| Tagapagsalaysay | Bakasin mo ang bawat titik upang mabuo ang sinabi ni Ama. |

`meaning`: `correct`

### Context copy

> Isang salita lamang ang kulang sa pangungusap ni Ama. Ilagay mo ang tamang salita sa tamang puwang.

### Restored memory — `memory.ugat.03`

> Ang tinig ni Ama sa lilim ng punong mangga: "Tama ang bata." Isang papuring dala ni Juan hanggang sa kanyang paglaki.

### Outro — `Dialogue_Ugat03_Outro`

| Speaker | Line |
|---|---|
| Juan | "Tama ang bata." Iyon ang sinabi ni Ama sa akin. |
| Tagapagsalaysay | Hindi lamang salita ang naibalik mo, Juan. Isang buong pangungusap — at ang tiwala ng iyong ama. |

---

## Level 4 — `level.ugat.04` — INA, AMA (both repeats, fewer clues)

### Intro — `Dialogue_Ugat04_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | Humina ang liwanag sa daan pauwi. Kakaunti na lamang ang palatandaan. |
| Juan | Alam ko ang mga salitang ito. Nabakas ko na sila noon. |
| Tagapagsalaysay | Kaya nga inaalis ko na ang ilang gabay. Kung tunay mong naaalala sina INA at AMA, mababakas mo sila kahit walang tulong. |

### Focus word — INA (repeat) — `Dialogue_Ugat04_Ina`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | INA — ang nagluwal at nag-aruga. Binubuo ito ng dalawang titik: I at NA. |
| Tagapagsalaysay | Walang gabay sa pagkakataong ito. Bakasin mo mula sa alaala. |

`meaning`: `mother`

### Focus word — AMA (repeat) — `Dialogue_Ugat04_Ama`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | AMA — ang haligi ng tahanan. Binubuo ito ng dalawang titik: A at MA. |
| Tagapagsalaysay | Walang gabay sa pagkakataong ito. Bakasin mo mula sa alaala. |

`meaning`: `father`

### Context copy

> Wala nang larawang gagabay sa iyo. Piliin mo ang salitang nararapat, mula lamang sa iyong alaala.

### Restored memory — `memory.ugat.04`

> Ang mukha nina Ina at Ama, malinaw na sa isip ni Juan — hindi na larawang hiniram, kundi alaalang tunay nang kanya.

### Outro — `Dialogue_Ugat04_Outro`

| Speaker | Line |
|---|---|
| Juan | Hindi ko na kailangan ng palatandaan. Nasa akin na sila. |
| Tagapagsalaysay | Ang alaalang nabakas nang walang gabay ay alaalang tunay nang naibalik. |

---

## Level 5 — `level.ugat.05` — IBA, MANA — closes the Ugat arc

### Intro — `Dialogue_Ugat05_Intro`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | Sa dulo ng Ugat, hinarap ni Juan ang Paglimot — ang anino na kumain sa mga alaala ng kanyang pamilya. |
| Juan | Iba na ang panahon. Iba na ang mundo. Ngunit hindi ibig sabihin niyon ay wala na akong natira. |
| Tagapagsalaysay | Tama ka, Juan. May mana kang hindi kayang kainin ng Paglimot. Bakasin mo ang IBA at ang MANA. |

### Focus word — IBA — `Dialogue_Ugat05_Iba`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | IBA — ang naiiba, ang hindi katulad ng dati. Binubuo ito ng dalawang titik: I at BA. |
| Tagapagsalaysay | Bakasin mo ito upang tanggapin na nagbabago ang panahon. |

`meaning`: `different`

### Focus word — MANA — `Dialogue_Ugat05_Mana`

| Speaker | Line |
|---|---|
| Tagapagsalaysay | MANA — ang minana mula sa nauna, ang ipinapasa sa susunod. Binubuo ito ng dalawang titik: MA at NA. |
| Tagapagsalaysay | Bakasin mo ito upang angkinin ang iyong pamana. |

`meaning`: `inheritance`

### Context copy

> Nagbago man ang panahon, may naiwan pa ring hindi kayang kunin ng Paglimot. Buuin mo kung ano iyon.

### Restored memory — `memory.ugat.05`

> Ang mana ni Juan — hindi lupa, hindi ginto, kundi ang kakayahang bumasa at sumulat ng Baybayin, ipinasa mula kina Ina at Ama.

### Ugat ending — `Dialogue_Ugat05_Outro`

| Speaker | Line |
|---|---|
| Juan | Iba na nga ang panahon. Ngunit ang mana ko ay nasa akin pa rin — nasa mga titik na natutunan kong bakasin. |
| Tagapagsalaysay | Ito ang Ugat, Juan: ang pinanggalingan. Malalim na ang iyong ugat ngayon. |
| Tagapagsalaysay | Sa susunod na yugto, ang Ugnayan — kung paano nagkakaugnay-ugnay ang isa't isa. |

---

## Integration status

| Item | State |
|---|---|
| `DialogueSO` assets for the 16 blocks above | **Generated** by `Assets/Editor/Campaign/UgatNarrativeContentTool.cs` |
| `introDialogue` and `outroDialogue` on Levels 2–5 | **Wired** by the same tool, matching how Level 1 wires both ends. At Level 5 the outro *is* the Ugat ending. |
| `rewardIds` = `memory.ugat.02`…`.05` | **Written** by the same tool — closes 4 validator errors |
| Focus-word `meaning` and per-word dialogue | **Blocked.** Levels 2–5 have **zero focus-word slots**, so there is nothing to attach the explanations to. See below. |
| Context images / narration audio | **Blocked on SALIN-206** (art and audio production) |

### Blocked on SALIN-204, which is marked Done but is not

Running `CampaignConfigValidator` against the shipped `CampaignConfig_RevisedV1.asset` reports
**134 errors**, including **`FOCUS_SLOT_COUNT_INVALID` on 14 of the 15 levels** — every level except
Level 1. Per Ugat level 2–5 the errors are:

| Code | Owner |
|---|---|
| `FOCUS_SLOT_COUNT_INVALID` (focusWords) | SALIN-204 / SALIN-172 |
| `REQUIREMENT_INVALID` × 3 (learning, practice, mastery) | SALIN-204 |
| `CUMULATIVE_POOL_INVALID` | SALIN-204 |
| `FINAL_RESTORATION_INVALID` | SALIN-204 |
| `REQUIRED_MEDIA_MISSING`, `REQUIRED_REFERENCE_MISSING` (contextMedia) | SALIN-206 |
| `REQUIRED_REFERENCE_MISSING` (rewardIds) | **SALIN-205 — closed by this work** |

The per-word explanations above are written and ready. They cannot be attached until the focus-word
slots exist, because `FocusWordDefinition.media.dialogue` is the field they hang on. **Authoring those
slots is not this ticket's scope** — the `meaning` field's own tooltip assigns it to SALIN-172, and the
slot structure to SALIN-204.
