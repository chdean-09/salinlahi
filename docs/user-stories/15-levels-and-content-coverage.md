# 15 — Levels and content coverage

Prefix **`LVL`**. Outstanding work per level. Levels whose behaviour is fully implemented are rows in [`COVERAGE.md`](COVERAGE.md); the table below is the authoritative per-level status, so individual levels no longer need a story each.

Levels are named to the player as **"Era N · Level 1–5"**. The global numbering below is for file identification only.

> **Current asset-contract update (2026-09-22):** The older table and statuses are preserved as
> dated backlog evidence. The current branch now has production challenge/reward contracts for
> Levels 6–15, natural focus-symbol carriers in every non-intermission wave for Levels 6–14,
> Level 10 as a non-boss mixed-wave level, and Level 15 as the sole authored boss. This does not
> infer terminal playability: Unity compilation, Test Runner execution, and clean victory/results
> runs for Levels 1–15 are `BLOCKED`; see `docs/audit/IMPLEMENTATION_STATUS-2026-09-22.md`.

| Global | Player-facing | Title | Focus words | Challenge | Rewards | Tier | Combat restoration | Status |
|---:|---|---|---|:-:|:-:|:-:|:-:|---|
| 1 | Ugat · 1 | Ang Unang Tinig | INA, AMA | ✅ | ✅ | 1 | on | Partial (played to Wave 2; Juan not rendering) |
| 2 | Ugat · 2 | Mga Mata ng Bata | BATA, MATA | ✅ | ✅ | 2 | on | Existing |
| 3 | Ugat · 3 | Ang Tamang Gawa | BATA, TAMA | ✅ | ✅ | 3 | on | Partial (one blank, should be two) |
| 4 | Ugat · 4 | Unang Guro | INA, AMA | ✅ | ✅ | 4 | on | Partial (one blank, should be two) |
| 5 | Ugat · 5 | Larawan ng Tahanan | IBA, MANA | ✅ | ✅ | 5 | on | Partial (no era paragraph) |
| 6 | Ugnayan · 1 | Mula Awa sa Gawa | AWA, GAWA | ❌ | ❌ | 1 | off | Missing |
| 7 | Ugnayan · 2 | Sama-Samang Lakas | SAMA, KASAMA | ❌ | ❌ | 2 | off | Missing |
| 8 | Ugnayan · 3 | Gana at Kaya | GANA, KAYA | ❌ | ❌ | 3 | off | Missing |
| 9 | Ugnayan · 4 | Ang Unang Oo | OO, UNA | ✅ | ❌ | 4 | off | Missing |
| 10 | Ugnayan · 5 | Awit ng Pamayanan | SANA, SAYA | ❌ | ❌ | 5 | off | Missing (deviates) |
| 11 | Pamana · 1 | Dalang Alaala | DALA, DAMA | ✅ | ❌ | 1 | off | Missing |
| 12 | Pamana · 2 | Mga Tagapag-ingat | HANGA, HALAGA | ✅ | ❌ | 2 | off | Missing |
| 13 | Pamana · 3 | Sanga ng Hinaharap | *(none)* | ❌ | ❌ | 3 | off | Missing |
| 14 | Pamana · 4 | Halaga ng Alaala | ALAALA, MAHALAGA | ✅ | ❌ | 4 | off | Missing |
| 15 | Pamana · 5 | Ang Huling Pamana | PAMANA, MALAYA | ✅ | ❌ | 5 | off | Missing |

**Historical playable-slice note (2026-09-15):** Levels 1–5 were the only levels with both a challenge sequence and non-empty `rewardIds` at the last story-register audit. The current branch has since authored those contracts for Levels 6–15; runtime end-to-end playability remains `BLOCKED` pending Unity verification.

---

### LVL-01 — Play Ugat Level 1 end to end
As a new player, I want the first level to teach me the whole loop and let me finish it, so that I understand the game and want to continue.
- AC: Prologue → story → focus words INA and AMA → symbol lesson → onboarding beats → waves with active clues → combat restoration fills INA and AMA → final syllable **MA** completes AMA → memory cutscene → save → Results → Ugat Level 2 unlocks.
- AC: Every symbol asked for is in the Ugat pool; chain kills are off, so one correct draw is one kill.
- AC: The level is completable with 3 hearts.
- System: Level 1 · `Level1_Config.asset`, `Level1_ChallengeSequence.asset`, `Level1OnboardingSequence.asset`
- Status: Partial — the Level 1 fix pass is **committed on local `dev`** (`3469c8ca` symbol pairing, `2693cb75` slowed pacing — `enemySpeedMultiplier: 0.6`, threshold 0.45, spawn intervals 6 → 3.5 s, `80aa29ec` introduction cards, ash gust, draw feedback); `dev` is 3 ahead of `origin/dev`. The 2026-09-14 playtest reached **Wave 2** and confirmed Ready screen, narration, 4/4 symbol cards, Hati discovery card, wave advance, heart loss and the recognizer responding to real pointer input. **Not reached:** Victory, Retry, Defeat. **Blocking defects:** Juan does not render and the slash VFX prefab is unassigned (CMB-19, CMB-20). Pronunciation playback and wave pacing unverified.
- Refs: `Level1_Config.asset` (dev:125-127,177-233), `progress/2026-09-14-level1-playtest.md`, `PHASE3-PLAN.md`, SALIN-214

### LVL-03 — Play Ugat Levels 3 and 4 with their authored difficulty tiers
As a player, I want the middle Ugat levels to raise the stakes, so that the era has a curve rather than four identical levels.
- AC: Level 3 (tier 3) and Level 4 (tier 4): every three incorrect submissions costs a heart and resets to the current checkpoint.
- AC: Both levels present **two** blanks in their restoration sentence.
- AC: Level 4 sends paired arrivals that can remove two hearts in one event.
- AC: Some Level 4 clues appear as an incomplete word.
- System: Levels 3–4 · `Level3_Config.asset`, `Level4_Config.asset`, `Challenge_Ugat03/04_Context.asset`
- Status: Partial — both are statically completable at their tiers, but the two-blank ruling (OQ-1) is reflected in neither asset nor the narrative copy, and the `IncompleteWord` clue channel authoring for Level 4 is unconfirmed. See REST-12.
- Refs: `Level3_Config.asset`, `Level4_Config.asset`, ruling OQ-1, ruling Q11, SALIN-144, SALIN-146
- Merged: absorbs LVL-04

### LVL-05 — Play Ugat Level 5 as an era finale
As a player, I want the fifth level of the era to combine everything, so that the era ends with a real test.
- AC: There is **no boss**.
- AC: Mixed, armoured waves alternate with paragraph checkpoints across the level's flow segments.
- AC: The era paragraph is required in addition to the level's two new words IBA and MANA.
- AC: The final syllable is **NA**.
- AC: Completing it plays the Ugat era-completion screen and unlocks Ugnayan.
- System: Level 5 · `Level5_Config.asset`, `Challenge_Ugat05_Context.asset`
- Status: Partial — the boss is cleared, two `flowSegments` are authored and the armoured Walang-Awa is assigned to a middle wave; the **era paragraph is not authored**, paragraph auto-fill is incomplete, and the manual end-to-end gate has not been run.
- Refs: `Level5_Config.asset`, SALIN-283, SALIN-247 (In Progress), ruling Q4 / Q5

### LVL-06 — Complete the Ugnayan era
As a player, I want the second era's five levels to be playable to completion, so that the journey continues past Ugat.
- AC: **Level 1 (AWA, GAWA)** — restoration distinguishes the feeling from the action; awards `memory.ugnayan.01`.
- AC: **Level 2 (SAMA, KASAMA)** — restoration drags KASAMA to the group; the restorable token is the root SAMA and affixes are fixed text.
- AC: **Level 3 (GANA, KAYA)** — restoration presents cause/result blanks.
- AC: **Level 4 (OO, UNA)** — the focus preview reads "O · O" for OO and "U · NA" for UNA, teaching O/U as one character with two readings.
- AC: **Level 5 (SANA, SAYA)** — no boss; combat and restoration alternate across flow segments and the era paragraph is required alongside the two taught words.
- AC: Every level has intro/outro dialogue, per-word explanation lines, a memory cutscene and non-empty `rewardIds`, so none is refused by the content-missing guard.
- System: Levels 6–10 · `Level6-10_Config.asset`, `Challenge_Ugnayan*`, `Dialogue_Ugnayan*`, `Cutscene_Ugnayan*`
- Status: Missing — no `Dialogue_Ugnayan*` or `Cutscene_Ugnayan*` assets exist and every level has empty `rewardIds`. Levels 6, 7, 8 and 10 have no `challengeSequence`; only Level 9's exists. Level 10 additionally **deviates**: `bossConfig` still points at the retired `BossConfig_Superintendent` (U-2).
- Refs: `Level6-10_Config.asset`, SALIN-248 / SALIN-249 (To Do), SALIN-280, ruling Q4 / Q5 / Q9
- Merged: absorbs LVL-07, LVL-08, LVL-09, LVL-10

### LVL-11 — Complete Pamana Levels 1, 2 and 4
As a player, I want the third era's build-up levels playable to completion, so that the finale is earned rather than jumped to.
- AC: **Level 1 (DALA, DAMA)** — DA is taught here with DA-only content; RA does not appear until Level 3.
- AC: **Level 2 (HANGA, HALAGA)** — introduces HA and LA.
- AC: **Level 4 (ALAALA, MAHALAGA)** — lands the era's theme of value before the finale.
- AC: Each has intro/outro dialogue, per-word explanation lines, a memory cutscene and non-empty `rewardIds`.
- System: Levels 11, 12, 14 · `Level11/12/14_Config.asset`, `Challenge_Pamana11/12/14_Context.asset`
- Status: Missing — all three challenge sequences exist, but `rewardIds` is empty on each and there are no Pamana narrative assets, so completion is refused.
- Refs: `Level11_Config.asset`, `Level12_Config.asset`, `Level14_Config.asset`, SALIN-251 (To Do), ruling R8
- Merged: absorbs LVL-12, LVL-14

### LVL-13 — Play Pamana Level 3 and meet RA
As a player, I want Level 13 to introduce RA as its own character, so that DA and RA are finally distinguished.
- AC: The level's focus words are SANGA and HARAYA.
- AC: HARAYA decomposes to HA · RA · YA using `Char_RA`, and the preview reads "ha · ra · ya".
- AC: An RA enemy spawns carrying `Char_RA`; drawing RA defeats it and drawing DA does not.
- AC: The campaign validator reports Level 13 clean.
- System: Level 13 · `Level13_Config.asset`, `EnemyData_Ragasa.asset`
- Status: Missing — Level 13 has **no focus words, no pool, no learning requirements and no challenge sequence**. `EnemyData_Ragasa.asset` exists but has no ability, no pool registration, no wave placement and is authored in the wrong era. See U-10.
- Refs: `Level13_Config.asset`, SALIN-250 / SALIN-252 / SALIN-261 (To Do), ruling OQ-6

### LVL-15 — Play Pamana Level 5 as the campaign finale
As a player, I want the last level to be a boss fight, a paragraph restoration and a final trace all at once, so that the ending uses everything I learned.
- AC: The Paglimot boss fight runs one wave per phase: Ugat symbols, then Ugnayan, then all three pools.
- AC: One paragraph line is restored after each phase, with a per-phase checkpoint.
- AC: PAMANA and MALAYA are the taught words; the final action is tracing **YA** into MALAYA.
- AC: Completing it plays the ending cinematic and marks the campaign complete.
- System: Level 15 · `Level15_Config.asset`, `Challenge_Pamana15_Context.asset`
- Status: Missing — a challenge sequence and a boss reference exist, but `rewardIds` is empty, there are **no flow segments**, the alternating structure is unbuilt, the era paragraph and final ceremony are unauthored, the boss is still the retired `BossConfig_Kadiliman` with no art or audio bank, and there is no ending cutscene. See U-3.
- Refs: `Level15_Config.asset`, SALIN-252 / SALIN-254 (To Do), ruling Q1 / Q5 third round

### LVL-18 — Have the content the game validates against be identifiable
As a player, I want the game's content to be traceable to an approved source, so that what I play is what was designed.
- AC: The campaign manifest's `sourceWorkbookSha256` matches `ContentIdentity.ApprovedWorkbookSha256`.
- System: Content validation · `CampaignIdentityManifest`, `ContentIdentity`
- Status: Unclear — the approved workbook is not in the repository, two differently named workbooks are in play, and the pinned checksum is itself disputed (`34dad782…` vs `33f7355f…`). Escalated as OI-1 / OI-2.
- Refs: `docs/design/spec-rulings-2026-09.md` §5, `docs/backlog/technical-work.md:27,824-829`
