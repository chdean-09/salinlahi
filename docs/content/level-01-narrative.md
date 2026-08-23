# Level 1 Narrative — Unang Alaala (INA at AMA)

> SALIN-200 / TW-TASK-004 Level 1 slice. Every row is **PENDING SALIN-188 REVIEW** —
> Filipino usage, cultural framing, and Baybayin claims must be approved by the
> language and cultural adviser before Level 1 acceptance (SALIN-192). English is a
> support translation, not display copy, unless the matrix says otherwise.

## 1. Level intro dialogue (`Dialogue_Ugat01_Intro`)

| # | Speaker | Filipino | English (support) | Status |
| --- | --- | --- | --- | --- |
| 1 | Tagapagsalaysay | Noong unang panahon, isinusulat ng ating mga ninuno ang kanilang mga alaala sa Baybayin. | Long ago, our ancestors wrote their memories in Baybayin. | PENDING SALIN-188 REVIEW |
| 2 | Tagapagsalaysay | Ngunit dumating ang Paglimot, at unti-unting kinain nito ang mga alaala ng pamilya ni Juan. | But the Forgetting came, and little by little it devoured the memories of Juan's family. | PENDING SALIN-188 REVIEW |
| 3 | Juan | Hindi ko na maalala ang mukha nina Ina at Ama... | I can no longer remember the faces of Mother and Father... | PENDING SALIN-188 REVIEW |
| 4 | Tagapagsalaysay | Sa bawat titik ng Baybayin na matututunan mo, isang alaala ang maibabalik. Simulan natin sa dalawang salitang pinakamalapit sa puso: INA at AMA. | With every Baybayin character you learn, a memory can be restored. Let us begin with the two words closest to the heart: INA and AMA. | PENDING SALIN-188 REVIEW |

## 2. Focus-word explanations

Each focus slot also carries authored label and meaning copy (`Level1_Config.focusWords`,
authored by SALIN-198). The meaning is what the Meaning mastery dimension matches on, so
the adviser approves it as content, not as a support gloss.

| Slot | Display label | Meaning | Status |
| --- | --- | --- | --- |
| `level.ugat.01.focus.01` | INA | mother | PENDING SALIN-188 REVIEW |
| `level.ugat.01.focus.02` | AMA | father | PENDING SALIN-188 REVIEW |

### INA (`Dialogue_Ugat01_Ina`, attached to focus slot 01)

| # | Speaker | Filipino | English (support) | Status |
| --- | --- | --- | --- | --- |
| 1 | Tagapagsalaysay | INA — ang nagluwal at nag-aruga. Binubuo ito ng dalawang titik: I at NA. | INA — mother, the one who bore and nurtured. It is built from two characters: I and NA. | PENDING SALIN-188 REVIEW |
| 2 | Tagapagsalaysay | Bakasin mo ang bawat titik upang maibalik ang alaala ni Ina. | Trace each character to restore the memory of Mother. | PENDING SALIN-188 REVIEW |

### AMA (`Dialogue_Ugat01_Ama`, attached to focus slot 02)

| # | Speaker | Filipino | English (support) | Status |
| --- | --- | --- | --- | --- |
| 1 | Tagapagsalaysay | AMA — ang haligi ng tahanan. Binubuo ito ng dalawang titik: A at MA. | AMA — father, the pillar of the home. It is built from two characters: A and MA. | PENDING SALIN-188 REVIEW |
| 2 | Tagapagsalaysay | Bakasin mo ang bawat titik upang maibalik ang alaala ni Ama. | Trace each character to restore the memory of Father. | PENDING SALIN-188 REVIEW |

## 3. Context-challenge copy (`Challenge_Ugat01_Context`, authored by SALIN-198)

| Element | Filipino | English (support) | Status |
| --- | --- | --- | --- |
| `displayName` (sequence title) | Unang Alaala | First Memory | PENDING SALIN-188 REVIEW |
| ugat01-place-ina prompt | Ibalik ang INA sa alaala. | Restore INA to the memory. | PENDING SALIN-188 REVIEW |
| ugat01-place-ama prompt | Ibalik ang AMA sa alaala. | Restore AMA to the memory. | PENDING SALIN-188 REVIEW |

## 4. Restored memory cutscene (`Cutscene_Ugat01_Memory`, era memory reward)

| Panel | Filipino | English (support) | Status |
| --- | --- | --- | --- |
| 1 | Sa liwanag ng gabing iyon, muling nabuo ang mga mukha. | In the light of that evening, the faces took shape once more. | PENDING SALIN-188 REVIEW |
| 2 | Naalala ni Juan ang init ng yakap ni Ina at ang tawa ni Ama sa hapag. | Juan remembered the warmth of Mother's embrace and Father's laughter at the table. | PENDING SALIN-188 REVIEW |
| 3 | Dalawang salita, dalawang alaalang naibalik: INA at AMA. | Two words, two memories restored: INA and AMA. | PENDING SALIN-188 REVIEW |

## 5. Completion / outro dialogue (`Dialogue_Ugat01_Outro`)

| # | Speaker | Filipino | English (support) | Status |
| --- | --- | --- | --- | --- |
| 1 | Juan | Naaalala ko na sila. Sina Ina at Ama. | I remember them now. Mother and Father. | PENDING SALIN-188 REVIEW |
| 2 | Tagapagsalaysay | Ito pa lamang ang simula, Juan. Marami pang alaala ang naghihintay na maibalik. | This is only the beginning, Juan. Many more memories wait to be restored. | PENDING SALIN-188 REVIEW |

## 6. Era story reference (`Era_01.storyReference` → `Dialogue_Ugat01_Intro`)

The Ugat era's framing story is the Level 1 intro (family roots — ugat — begin the
journey); a dedicated era-wide story asset can replace this reference when SALIN-173
authors the campaign-wide narrative.

## Asset attachment map

| Reference | Asset |
| --- | --- |
| `Level1_Config.introDialogue` | `Dialogue_Ugat01_Intro` |
| `Level1_Config.outroDialogue` | `Dialogue_Ugat01_Outro` |
| `focusWords[0].media.dialogue` | `Dialogue_Ugat01_Ina` |
| `focusWords[1].media.dialogue` | `Dialogue_Ugat01_Ama` |
| `focusWords[*].media.cutscene`, `contextMedia.dialogue/cutscene` | `Cutscene_Ugat01_Memory` / `Dialogue_Ugat01_Intro` |
| `Era_01.storyReference` | `Dialogue_Ugat01_Intro` |
| `Era_01.memoryReference` | `Cutscene_Ugat01_Memory` |

## Superseded assets

`Level1_Opening.asset` is the original Level 1 opening cutscene — an English-language
colonial-era origin story. It contradicts the pre-colonial Ugat framing authored here,
so its `LevelCutsceneMapping` entry (level 1, BeforeLevel) is removed and Level 1 now
opens on `Dialogue_Ugat01_Intro`. The cutscene asset stays in the project, unreferenced,
as legacy evidence per SALIN-187.
