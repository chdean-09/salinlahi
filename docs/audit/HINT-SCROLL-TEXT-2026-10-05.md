# Campaign hint-scroll text audit

Date: 2026-10-05. Scope: all 15 levels referenced by `Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset`, resolved through its three era assets. Updated after the requested heading change: focus-word headings now show one spaced underscore per letter of the authored answer; clue bodies and sentence context are unchanged.

## Evidence and limits

**Observed:** campaign level and focus-word dialogue GUIDs resolve uniquely. The actual `SentenceHintContent.Build` source was compiled and executed outside Unity using minimal data-only stand-ins populated from authored YAML. All 15 levels produced content; all 30 focus words produced exactly one clue with an answer-length blank heading. The actual SentenceHintContentTests ran with NUnit assertions and data/lifetime stand-ins: 44 cases passed, zero failed. No complete focus-word answer leaked into the sentence-scroll headings or lines. This is an isolated content check, not Unity compilation or a Unity Test Framework run.

**Observed:** `Assets/Scripts/Gameplay/LevelFlowController.cs` applies each level to `SentenceHintController`. `Assets/Scripts/UI/HUD/SentenceHintController.cs` composes each entry as a bold heading followed by its lines, with a blank line between entries. The inventory removes TMP bold markup only; authored line breaks and blanks are preserved. Default title: `Hint sa Pangungusap`; close: `Isara`. No scene/prefab overrides of these copy fields were found.

**Observed:** `Assets/Scripts/UI/HUD/SentenceHintContent.cs` selects explicit `hintText`, otherwise a matching word-mode objective clue, otherwise the first surviving dialogue line. It strips spelling recipes and blanks whole focus words. Context-mode objectives contribute `Konteksto ng pangungusap`; literal fragments remain and targets become `__`. The chip is suppressed during challenges, introductory modals and cutscenes. Opening it pauses combat.

**NOT VERIFIED:** imported text, actual layout, wrapping, clipping and scrolling in Unity. Unity MCP and native Editor control were unavailable. Unity compilation, Console inspection and Edit/Play Mode tests: **BLOCKED**.

## Findings

1. **Implemented:** all 30 sentence-scroll headings now use one spaced underscore per answer letter, from the authored Latin spelling with a display-label fallback. INA is `_ _ _`, BATA is `_ _ _ _`. The shared meaning data remains available to other UI.
2. **Observed:** the title still contains the English word `Hint`. All focus-word `hintText` fields are empty except Level 3 BATA. Levels 1-2 use objective clues; other word clues use dialogue.
3. **Observed:** Levels 3-5, 10 and 15 show syllable/partial-word blanks, such as `__buting`, `gu__gawa` and `taha__n`, following the authored tokenization. **Recommendation:** review readability against the intended sentence exercise.
4. **Observed:** Level 15 joins sentences without spaces: `__ __ __nan.__ __` and `kultura.Bawat`. `AddObjectiveContext` concatenates units without separators, and those authored units supply no boundary spaces. **Recommendation:** review sentence separation before changing authored spacing or the renderer.
5. **Observed:** Levels 6-9 and 11-14 have no restoration objective units, so they show two word definitions without sentence context. Levels 10 and 15 do have context. Comments in `SentenceHintContent` and `SentenceHintController` claiming Levels 6-15 have no units are stale.
6. **Implemented:** challenge hints now show only authored Filipino synonyms, with no sentence, answer label or English meaning. KASAMA reveals `kapiling, kaagapay`. The modal option/title is `Ipakita ang kasingkahulugan`. The same synonyms persist after closing the modal; hint limits and score penalties are unchanged.

## Sentence-scroll inventory

### Level 1: Ang Unang Tinig

Source: `Assets/ScriptableObjects/Levels/Level1_Config.asset`.

- `INA` clue: matched objective `clue`.
- `AMA` clue: matched objective `clue`.

```text
_ _ _
ilaw ng tahanan

_ _ _
ang haligi ng tahanan
```

### Level 2: Mga Mata ng Bata

Source: `Assets/ScriptableObjects/Levels/Level2_Config.asset`.

- `BATA` clue: matched objective `clue`.
- `MATA` clue: matched objective `clue`.

```text
_ _ _ _
bunga ng pagmamahalan

_ _ _ _
dungawan ng kaluluwa
```

### Level 3: Ang Tamang Gawa

Source: `Assets/ScriptableObjects/Levels/Level3_Config.asset`.

- `BATA` clue: explicit `hintText`.
- `TAMA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugat03_Tama.asset`.

```text
Konteksto ng pangungusap
Ang __buting __ __ ay gu__gawa ng __ __.

_ _ _ _
ang musmos na sumisibol, ang simula ng bawat alaala.

_ _ _ _
ang wasto, ang nararapat.
```

### Level 4: Unang Guro

Source: `Assets/ScriptableObjects/Levels/Level4_Config.asset`.

- `INA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugat04_Ina.asset`.
- `AMA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugat04_Ama.asset`.

```text
Konteksto ng pangungusap
Ang __ __ at __ __ ang u__ng guro sa taha__n.

_ _ _
ang nagluwal at nag-aruga.

_ _ _
ang haligi ng tahanan.
```

### Level 5: Larawan ng Tahanan

Source: `Assets/ScriptableObjects/Levels/Level5_Config.asset`.

- `IBA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugat05_Iba.asset`.
- `MANA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugat05_Mana.asset`.

```text
Konteksto ng pangungusap
Kapag __y ga__ at __sip, 
__s __raming __gay ang kaya, __s __raming pangarap ang __ __abot, 
at __s __layo ang __pupun__han.

_ _ _
ang naiiba, ang hindi katulad ng dati.

_ _ _ _
ang minana mula sa nauna, ang ipinapasa sa susunod.
```

### Level 6: Mula Awa sa Gawa

Source: `Assets/ScriptableObjects/Levels/Level6_Config.asset`.

- `AWA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan06_Awa.asset`.
- `GAWA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan06_Gawa.asset`.

```text
_ _ _
malasakit na nadarama para sa kapwa.

_ _ _ _
kilos na isinasagawa, hindi lamang iniisip.
```

### Level 7: Sama-Samang Lakas

Source: `Assets/ScriptableObjects/Levels/Level7_Config.asset`.

- `SAMA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan07_Sama.asset`.
- `KASAMA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan07_Kasama.asset`.

```text
_ _ _ _
ang paglapit at pakikiisa sa iba.

_ _ _ _ _ _
taong kapiling sa gawain at paglalakbay.
```

### Level 8: Gana at Kaya

Source: `Assets/ScriptableObjects/Levels/Level8_Config.asset`.

- `GANA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan08_Gana.asset`.
- `KAYA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan08_Kaya.asset`.

```text
_ _ _ _
siglang nagtutulak upang magsimula at magpatuloy.

_ _ _ _
kakayahang harapin at tapusin ang gawain.
```

### Level 9: Ang Unang Oo

Source: `Assets/ScriptableObjects/Levels/Level9_Config.asset`.

- `OO` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan09_Oo.asset`.
- `UNA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan09_Una.asset`.

```text
_ _
pagsang-ayon at kusang pagtanggap.

_ _ _
nauuna sa pagkilos o pagkakasunod.
```

### Level 10: Awit ng Pamayanan

Source: `Assets/ScriptableObjects/Levels/Level10_Config.asset`.

- `SANA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan10_Sana.asset`.
- `SAYA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Ugnayan10_Saya.asset`.

```text
Konteksto ng pangungusap
__ __ __ at ______, may ga__ ang __ __ __.
__ng wika ay ilaw ng ba__t ta__.
__ bagong araw, __y pag-asa ang pamaya__n.

_ _ _ _
pag-asang hinihiling at pinipiling buhayin.

_ _ _ _
galak na lumalago kapag ibinabahagi.
```

### Level 11: Dalang Alaala

Source: `Assets/ScriptableObjects/Levels/Level11_Config.asset`.

- `DALA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana11_Dala.asset`.
- `DAMA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana11_Dama.asset`.

```text
_ _ _ _
ang bagay o aral na bitbit sa paglalakbay.

_ _ _ _
ang pag-unawa sa pamamagitan ng pakiramdam.
```

### Level 12: Mga Tagapag-ingat

Source: `Assets/ScriptableObjects/Levels/Level12_Config.asset`.

- `HANGA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana12_Hanga.asset`.
- `HALAGA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana12_Halaga.asset`.

```text
_ _ _ _ _
pagkilala at paghanga sa mabuting ginawa ng iba.

_ _ _ _ _ _
sukat ng saysay at kabuluhan.
```

### Level 13: Sanga ng Hinaharap

Source: `Assets/ScriptableObjects/Levels/Level13_Config.asset`.

- `SANGA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana13_Sanga.asset`.
- `HARAYA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana13_Haraya.asset`.

```text
_ _ _ _ _
bahaging tumutubo mula sa puno at umaabot sa bagong direksiyon.

_ _ _ _ _ _
malayang guniguni at larawang binubuo ng isip.
```

### Level 14: Halaga ng Alaala

Source: `Assets/ScriptableObjects/Levels/Level14_Config.asset`.

- `ALAALA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana14_Alaala.asset`.
- `MAHALAGA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana14_Mahalaga.asset`.

```text
_ _ _ _ _ _
gunita sa tao, pangyayari, o aral na lumipas.

_ _ _ _ _ _ _ _
may malaking saysay at dapat ingatan.
```

### Level 15: Ang Huling Pamana

Source: `Assets/ScriptableObjects/Levels/Level15_Config.asset`.

- `PAMANA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana15_Pamana.asset`.
- `MALAYA` clue: `Assets/ScriptableObjects/Dialogue/Dialogue_Pamana15_Malaya.asset`.

```text
Konteksto ng pangungusap
__ __ ni Juan ang mga aral na kaniyang __ __ mula sa pamilya at __ __ __nan.__ __ siya sa __ __ __ ng mga taong nag-ingat ng kultura.Bawat salinlahi ay __ __ ng nakaraan at may sariling __ __ __.

_ _ _ _ _ _
kaalaman, alaala, at pagpapahalagang ipinapasa sa susunod.

_ _ _ _ _ _
may kakayahang pumili at kumilos nang hindi nakakulong.
```

## Challenge hint modal

**Observed source behavior:** `Assets/Scripts/UI/HUD/HintModal.cs`, `HintModalCopy.cs`, `Assets/Scripts/Gameplay/ChallengeModeUI.cs` and `ChallengeFlowController.cs` define this separate scroll. `SetLevelHintWords` searches current-level focus words first, then earlier campaign levels. Partial-word WordPlacement/SentenceRestoration units retain the evidenced whole-word context. SentenceHintContent.BuildChallengeHint reads the separate hintSynonyms field and removes answer mentions. It never falls back to sentence clues or English meanings.

**Inference from authored slots and the resolver:** below are possible revealed synonyms for each level sequence, assuming the slot is active and budget available. This is not an executed challenge-session check. Where no word resolves, the panel says `Walang pahiwatig para sa hakbang na ito.` and disables confirmation.

| Level | Possible synonym hints | Slots/tokens with no resolvable meaning |
|---|---|---|
| 1 | `nanay`; `tatay` | None |
| 2 | `musmos`; `paningin` | None |
| 3 | `musmos`; `wasto` | None |
| 4 | `nanay`; `tatay` | None |
| 5 | `naiiba, kakaiba`; `pamana`; `nanay`; `tatay`; `wasto` | None |
| 6 | `habag`; `kilos` | None |
| 7 | `pakikiisa`; `kapiling, kaagapay` | None |
| 8 | `sigla`; `kakayahan` | None |
| 9 | `sige, sang-ayon`; `nauna, nangunguna` | None |
| 10 | `nawa`; `tuwa, galak` | None |
| 11 | `bitbit`; `ramdam` | None |
| 12 | `paghanga`; `saysay, kabuluhan` | None |
| 13 | `sangay`; `guniguni, imahinasyon` | None |
| 14 | `gunita`; `makabuluhan` | None |
| 15 | `mana, minana`; `nakalaya, nagsasarili` | None |

Shared modal text:

- Confirmation: `Gumamit ng hint?`; body `Ipakita ang kasingkahulugan`; buttons `Ipakita`, `Kansela`.
- Budget: `Walang bayad sa antas na ito.` or `Natitirang pahiwatig: N`.
- Revealed: title `Ipakita ang kasingkahulugan`; body from the table; close `Isara`.
- Exhausted: title/button `Wala nang Pahiwatig`; body `Nagamit mo na ang pahiwatig para sa antas na ito.`; close `Isara`.
- Persistent challenge status: `Pahiwatig: ` followed by the same Filipino synonyms.
- `CostLine` can format `May halagang N puntos.`, but `HintModal.Open` currently renders the remaining-budget/free line instead.

## Rename and validation

**Observed:** all 30 former-name text occurrences in 16 tracked files were changed to `Buhay na Kasulatan`, including Level 1/6 cutscenes, Iligaw/Salungat copy, documentation and the translation inventory. The name substitution changed no runtime logic, hint data, asset GUIDs or file IDs. The later hint requests change the shared builder, modal, tests and the dedicated hintSynonyms field on all 30 focus words. Existing sentence clues, meanings, GUIDs and file IDs are preserved. Historical documentation received only the terminology substitution.

- **PASS:** tracked-text search for the former name returned no matches.
- **PASS:** changed Unity YAML parsed; comparison with Git HEAD after line-ending normalization verified only requested substitutions in the 16 existing files.
- **PASS:** campaign level and focus-word dialogue GUID references resolved uniquely.
- **PASS:** actual hint builder/content tests executed in isolation with NUnit assertions and Unity data/lifetime stand-ins: 44 cases passed; all 15 levels/30 words use answer-length blank headings. All clue and sentence-context lines match the previous inventory.
- **PASS:** diff review and whitespace checks.
- **BLOCKED:** Unity compilation/Console, Edit/Play Mode tests and visual rendering.
- Gameplay checks for documentation-only substitutions: **NOT APPLICABLE**.

Manual verification in Unity 6000.3.9f1: inspect the Salungat ability and Iligaw description in the almanac, and play Level 1/6 openings to check the longer name fits. Open every level’s `?` scroll during combat and compare against this inventory. Check portrait wrapping, long-body scrolling and Level 15 sentence boundaries. Open challenge hints on the corresponding slots to verify the separate modal text and budget behavior.
