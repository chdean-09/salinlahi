# Ticket quality review — `jira-import-tasks.csv`

> **Resolved 2026-09-15.** All six findings below are fixed. `US-081` was split into four (`US-081`–`US-084`), which renumbered everything after it and took the set from 154 to **157 stories / 184 rows**. The file now reads as the record of what was found and what was done about it.

Checked before creation: 27 epics + 154 stories. There is no written Definition of Ready in this repo (`docs/jira/TEAM-HANDBOOK.md` covers git and PR conventions only), so this review applies a stated bar and shows the working. **Adjust the bar and I will re-run it.**

## The bar applied

1. **Role–want–why.** Every story reads *As a … I want … so that …*.
2. **Testable AC.** Each criterion names something a person or a test can observe and settle yes/no. No judgement words.
3. **One outcome per story.** A title covering several distinct behaviours is a placeholder, not a story.
4. **Grounded numbers.** Where the game already has a tuned value, the AC cites it rather than a word like "quick".
5. **Import hygiene.** Summary under 255 chars, description non-empty, every story parented to its stage epic.

## Result

| Check | Result |
|---|---|
| Role–want–why complete | **154 / 154 pass** |
| Title unique | **154 / 154 pass** — no duplicates |
| Summary length, description, parent link | **181 / 181 pass** |
| Testable AC | **148 pass, 6 need work** |
| One outcome per story | **153 pass, 1 needs work** (US-081) |

**6 stories need work.** The set is in better shape than the raw heuristics first suggested — 45 stories have a single acceptance criterion, but that is atomicity working as you asked for it, not thinness. I checked all 45 by hand; 38 are single-outcome and testable as written (`US-153` — "with haptics on, a correct drawing vibrates the device" — needs nothing added).

---

## The six

### US-040 — See my line follow my finger
- **Now:** "The rendered line is smooth, not visibly angular."
- **Why it fails:** "smooth" and "visibly angular" are judgement calls; two reviewers will disagree.
- **Proposed:** "Visual stroke points are subdivided to at most 8 screen px apart (`RecognitionConfig_Default.visualSampleSpacingPixels`), so no straight segment longer than 8 px is drawn between two real touch samples."

### US-042 — Draw a symbol that needs more than one stroke
- **Now:** "Strokes drawn in quick succession are treated as one symbol."
- **Why it fails:** "quick succession" has a tuned value in the build and the story does not cite it.
- **Proposed:** "A stroke begun within 0.6 s of the previous stroke ending joins the same symbol (`RecognitionConfig_Default.multiStrokeWindowSeconds`); after 0.6 s the drawing is submitted."
- **Note:** the C# default is `1.5f` but the shipped asset authors **0.6**. The story should cite the authored value.

### US-044 — Have a reasonable attempt accepted
- **Now:** "A drawing that clearly matches the intended symbol is accepted." / "Early levels accept a looser match than later ones."
- **Why it fails:** "clearly matches" is the whole question, restated. The second criterion promises per-level tuning with no values.
- **Proposed:** "A drawing scoring at or above 0.60 against the target template is accepted (`RecognitionConfig_Default.minimumConfidence`)." Plus, if the difficulty ramp is real: "Ugat levels accept ≥ X, Ugnayan ≥ Y, Pamana ≥ Z" — **those three values do not exist yet**; a single threshold ships today.

### US-045 — Know immediately when a drawing failed
- **Now:** "A rejected stroke produces a clear visual rejection and clears the canvas."
- **Why it fails:** "clear visual rejection" does not say what is drawn.
- **Proposed:** "A rejected stroke flashes the stroke in the rejection colour and clears the canvas within one frame of the decision, with enemies still moving."

### US-047 — Not be scored on my handwriting
- **Now:** "An accuracy figure **may** appear only in the pressure-free practice mode."
- **Why it fails:** "may" makes it unfalsifiable — nothing can violate it.
- **Proposed:** "An accuracy figure is shown only in practice mode, and nowhere during a level."

### US-081 — Gapos, Punit, Ngatngat and Uhaw each do something
- **Now:** one story for four enemies; "Uhaw drains something in play."
- **Why it fails:** the title is a placeholder and the Uhaw criterion names no effect. Four enemies cannot be closed as one ticket — the first three land and the fourth blocks it.
- **Proposed:** split into four stories, one per enemy, and specify Uhaw's drain (hearts? time? the hint budget?). **This takes the set from 154 to 157 — your call, since you set ~150 as the ceiling.**

---

## Checked and deliberately passed

`US-004` (offline), `US-046` (two distinct failure messages), `US-056` (resolves against any eligible carrier), `US-029`, `US-036`, `US-041`, `US-072`, `US-130`, `US-140`, `US-153`. A heuristic flagged these for a one-line AC restating the title; read in full, each states one observable outcome and needs nothing added.

`US-016`, `US-022`, `US-031`, `US-076`, `US-101`, `US-121`, `US-138`, `US-144`, `US-149`, `US-151` carry "and" in the title but describe one behaviour each (`US-076` "Labo fades in and out" is one cycle, not two features).
