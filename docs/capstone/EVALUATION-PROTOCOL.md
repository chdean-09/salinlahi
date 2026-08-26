# Capstone Evaluation Protocol

> **SALIN-191 / TW-RES-003.** Aligns the usability, experience, and learning-outcome measures with
> the revised 15-level, three-era experience, and defines privacy-safe local educational telemetry.
>
> **Status: DRAFT — not yet approved.** The ticket's completion condition is that this document is
> *documented and approved*. Drafting is done; approval is not. This protocol governs data
> collection from human participants, some of whom may be minors, so it must be signed off by the
> capstone adviser (and the institution's ethics process, if one applies) **before any participant
> is recruited**. Nothing here authorises collection on its own.

---

## 1. What changed, and why this document exists

The measures in the capstone manuscript were written against an earlier scope. Two concrete
mismatches were found while aligning them, both of which would have produced wrong instruments:

| Item | Manuscript / GDD says | Actual shipped data | Resolution |
|---|---|---|---|
| Base character count | Manuscript §2.1.8: **18**. GDD: **17**, in 5 places (`GDD.md` lines 104, 147, 194, 241, 259) | **18** distinct `characterID` values across 18 `BaybayinCharacterSO` assets: `A BA DA EI GA HA KA LA MA NA NGA OU PA RA SA TA WA YA` | **18 is correct.** The manuscript is right; the **GDD's 17 is stale**. Classic Baybayin has 17 (RA historically shared DA's glyph); this build treats `RA` as its own character, giving 3 vowels + 15 consonants. Instruments use **18**. The GDD needs a separate correction — see §11. |
| Era / chapter naming | GDD §Chapters: "Liwanag", "Pagbalik" | Canonical campaign is `era.ugat`, `era.ugnayan`, `era.pamana`, five levels each (`docs/system/05_Data_Contracts_and_ScriptableObjects.md`) | Instruments and reporting use the **canonical era ids**. Chapter display names are presentation, not identity. |

Level identity is `level.<era>.<local-order>`; `levelNumber` is global presentation order only. All
progression reporting in this protocol keys on **stable level identity**, never on `levelNumber`, so
that a re-ordering does not silently invalidate collected data.

---

## 2. Constructs and success measures

| # | Construct | Instrument | Success measure |
|---|---|---|---|
| M1 | Perceived usability of freehand drawing as the primary input | SUS (Brooke, 1996), 10 items, post-play | Mean SUS reported **with n, SD and the score distribution**, never as a bare number — Clark et al. (2021) on small-sample SUS uncertainty applies directly here |
| M2 | Input responsiveness and reliability in play | Local telemetry (§6) | First-attempt success rate; attempts per accepted character; redraw frequency; recognition latency (finger-lift → outcome) |
| M3 | Immediate recognition gain | Pre-test / immediate post-test (§5.1) | Change in correct glyph→syllable identifications across the 18 characters |
| M4 | **Delayed** retention | Delayed post-test at 7 (+/-2) days (§5.2) | Retention relative to immediate post-test |
| M5 | Progression through the revised campaign | Local telemetry (§6) | Levels completed by stable id; hearts remaining; assists used |
| M6 | Experience / motivation | Short structured interview (§5.4) | Thematic summary; not scored |

**M4 is new.** The manuscript's §2.1.8 currently has no delayed retention test and explicitly cautions
against claiming long-term retention without one. Adding it is what lets the study say anything about
retention at all; without it, M3 supports claims about *immediate* recognition only.

### Claim boundaries (unchanged, and binding)

The study may claim measurable improvement in **immediate visual recognition and syllable-value
association for the 18 supported base characters after gameplay exposure**, and — with M4 — short-term
retention at one week. It may **not** claim literacy, fluency, independent Baybayin writing, kudlit
handling, word transliteration, or sentence reading. Those are outside system scope and are not
instrumented.

---

## 3. Participant flow

Single session (~50–60 min), plus one short asynchronous follow-up.

| Step | Duration | Notes |
|---|---|---|
| 1. Consent / assent | 5 min | §7. Minors require guardian consent **and** participant assent. Nobody proceeds without it. |
| 2. Demographic + prior-exposure form | 3 min | Age band, prior Baybayin exposure, prior mobile-game frequency. No names. |
| 3. **Pre-test** (18 items) | 7 min | §5.1. Before any exposure to the game, including the Tracing Dojo. |
| 4. Guided orientation | 3 min | Controls only. No character instruction — that would contaminate M3. |
| 5. Gameplay | 25 min | Ugat Level 1 onward, natural progression. Participants stop where they stop; reaching a fixed level is **not** required and must not be coached. |
| 6. **Immediate post-test** (18 items, reordered) | 7 min | §5.1 |
| 7. SUS | 5 min | §5.3 |
| 8. Short interview | 5–8 min | §5.4 |
| 9. **Delayed post-test** | 7 min, day 7 (+/-2) | §5.2. Remote administration is acceptable and expected. |

Attrition at step 9 is the main threat to M4. Record the completed-vs-invited count and report it;
do not silently drop non-responders from M3 as well.

---

## 4. Assignment and controls

This is a **single-group pre-test/post-test design** with no control group and no random assignment —
a quasi-experimental design in the sense of Shadish et al. (2002). This is a real limitation, not a
formality: maturation, testing effects, and self-selection are all uncontrolled, and the delayed
post-test shares the testing-effect problem with the immediate one.

Mitigations actually applied: pre-test precedes all exposure; post-test items are reordered; no
character instruction is given during orientation. Everything else is reported as a limitation.

---

## 5. Instruments

### 5.1 Recognition test — immediate (pre / post)

- 18 items, one per supported character. Glyph shown; participant selects or writes the syllable value.
- **Post-test uses the same 18 items in a different order** to blunt item-order recall.
- Scope limited to base characters and their syllable values. **No** kudlit, transliteration, sentence
  reading, or independent writing.
- Scored 0–18. Report total change and per-character gain, so characters with the highest and lowest
  gain can be identified.

### 5.2 Recognition test — delayed (new)

- Same 18 items, a **third** ordering.
- Administered day 7 (+/-2) with no intervening play. Confirm and record whether the participant
  played between sessions; a participant who did is reported separately, not silently pooled.

### 5.3 SUS

Standard 10-item SUS, 5-point scale, administered after play and before the interview. Report mean,
SD, n, and distribution. Interpret against Bangor et al. (2008), and per Speicher (2015) frame the
question as *"is freehand drawing usable as the primary input under wave pressure"* — not general
ease of use.

### 5.4 Structured interview

Four fixed prompts, audio recorded **only with explicit separate consent**; otherwise notes only.

1. What did drawing under time pressure feel like?
2. When a drawing was not accepted, did you know what to change?
3. Did anything about the story or characters stay with you?
4. Would you keep playing outside a study? Why?

Prompt 2 exists to evaluate SALIN-163's supportive feedback wording from the player's side, which no
automated test can reach.

### 5.5 Researcher observation sheet (telemetry fallback)

If telemetry is unavailable or declined, the observer records per level: attempts before first
acceptance, visible frustration/hesitation events, assists used, and completion. This is the
"equivalent researcher-observation method" the ticket permits, and it keeps M2/M5 answerable without
any file collection at all.

---

## 6. Telemetry — privacy-safe, local, opt-in

### 6.1 What already exists

`Assets/Scripts/Analytics/RecognitionLogger.cs` already writes `recognition_log.csv` to
`Application.persistentDataPath`. It is **local-only** — there is no network transmission anywhere in
the logger — and its columns are:

```
timestamp, recognizedCharacterID, confidence, secondBestCharacterID,
secondBestConfidence, scoreGap, intendedCharacterID, outcome
```

No name, device id, account, advertising id, or location is recorded. `LoggingEnabled` is a public
static, so it is switchable at runtime.

**`confidence` is a researcher-facing field and stays.** SALIN-163 removed the recognizer score from
the *player-facing UI*, which is a different question: the player should not be graded by a raw
metric, but M2 depends on it. The two are not in conflict, and this distinction should survive
review.

### 6.2 Fields used for this study

| Field | Serves | Notes |
|---|---|---|
| `timestamp` | M2 latency | Session-relative offset is sufficient; absolute wall-clock is not needed |
| `intendedCharacterID`, `recognizedCharacterID`, `outcome` | M2, M3 per-character gain | |
| `confidence`, `secondBestConfidence`, `scoreGap` | M2 reliability | Researcher-only |
| Level stable id, completion, hearts, assists | M5 | **Not currently logged — see §11** |

### 6.3 Rules

1. **Local only.** No network transmission. Files leave the device only by deliberate researcher
   export, in the participant's presence.
2. **Opt-in, and separable.** A participant may complete the study with telemetry declined; §5.5 is
   the fallback. Declining must cost them nothing.
3. **Pseudonymous.** Files are renamed to a participant code (`P01`…) at export. The code↔identity
   key is held separately from the data (§7.3) and is the only linkage.
4. **No new PII fields may be added** to the schema without re-approval of this protocol.
5. **Inspectable.** A participant may ask to see their own file before it is taken.

---

## 7. Consent and privacy

### 7.1 Consent

- Written informed consent before any data collection.
- **Minors: guardian consent plus participant assent.** Both, or the participant does not take part.
- Consent covers, separately and severably: (a) test responses, (b) SUS and interview notes,
  (c) audio recording, (d) telemetry export. Any may be declined individually.
- Withdrawal is permitted at any time, including after the session, up to the anonymisation point
  (§8). After anonymisation, withdrawal is no longer technically possible — participants must be told
  this **before** they consent, not after they ask.

### 7.2 Data minimisation

Collect age **band**, not date of birth. No names on instruments — participant code only. No school
or class identifiers. No device identifiers. No screen recording of anything except, with consent,
the drawing surface.

### 7.3 Storage

- Instruments and exports: encrypted volume, access limited to the named research team.
- The code↔identity key: stored **separately** from the data, and destroyed at anonymisation (§8).
- No cloud sync, no shared drives, no messaging apps for transfer of raw data.

---

## 8. Retention

| Data | Retention | Disposal |
|---|---|---|
| Code↔identity key | Until the delayed post-test is complete and matched, then **destroy** | Secure delete |
| Consent forms | Per institutional requirement (typically the longer of 3 years or programme policy) | Per institution |
| Anonymised instrument responses, SUS, telemetry | Until capstone defence + 1 year | Secure delete |
| Audio recordings | Transcribe, then **destroy the audio within 30 days** | Secure delete |

**Anonymisation point:** destruction of the code↔identity key, immediately after delayed post-tests
are matched. This is the moment withdrawal stops being possible, and is the date to state on the
consent form.

---

## 9. Sample plan

- **Target: 15–20 participants.** Enough for descriptive statistics and thematic saturation on the
  interview; the design does not support inferential claims requiring a powered sample, and none are
  made.
- **Inclusion:** able to use a touchscreen phone; no prior formal Baybayin instruction beyond
  incidental school exposure (recorded in the demographic form regardless).
- **Recruitment:** convenience sampling. This is a real limitation and is reported as one.
- **Stopping rule:** stop at 20, or at target n with interview saturation, whichever is first.
- Report **invited / consented / completed session / completed delayed post-test** as four separate
  counts. Attrition between the last two is the headline threat to M4.

---

## 10. Analysis ownership

| Area | Owner |
|---|---|
| Protocol integrity, consent, ethics correspondence | Capstone adviser (approval authority) |
| Recruitment, session administration, delayed follow-up | Named researcher, TBD by the team |
| Telemetry export, pseudonymisation, secure storage | Named data custodian, TBD — **must not be the same person as the session administrator** |
| Recognition-test scoring and per-character analysis | TBD |
| SUS scoring and interpretation | TBD |
| Interview coding | Two coders, TBD; report agreement |
| Write-up | Full team |

Names are deliberately left `TBD`: assigning them is part of approving this protocol, not part of
drafting it. **The custodian/administrator separation is not a TBD** — it is a requirement.

---

## 11. Open items — required before approval

1. **The GDD's "17 characters" is wrong in 5 places** (`GDD.md` lines 104, 147, 194, 241, 259). The
   data has 18. Not corrected here because SALIN-191 owns evaluation measures, not the GDD; this
   needs its own ticket, and the Kadiliman final-boss sequence ("draw all 17") may be a real content
   bug rather than a documentation typo. **Worth checking before it ships.**
2. **Level progression fields are not logged** (§6.2). M5 currently depends on the observation sheet.
   Adding stable level id, completion, hearts and assists to local telemetry is a small code change
   and needs its own ticket.
3. **Ethics route unconfirmed** — whether the institution requires formal review for minors must be
   settled before recruitment.
4. **SOP mapping** — M1/M2 map to the manuscript's SOP 2 and M3/M4 to SOP 3. Mapping for SOP 1 and
   Objective 4 (§3.5.4) should be confirmed against the current manuscript.
5. **Named owners** for §10.

---

## References

Bangor, Kortum & Miller (2008); Brooke (1996); Clark et al. (2021); Shadish, Cook & Campbell (2002);
Speicher (2015); Tadayon & Pottie (2021) — as cited in the capstone manuscript §2.1.7–2.1.8.
