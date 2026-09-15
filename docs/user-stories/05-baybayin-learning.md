# 05 — Baybayin learning

Prefixes **`LEARN`** (symbols and words), **`PRAC`** (free practice), **`MAST`** (mastery and review), **`CODEX`** (Codex / Almanac).

This is the curriculum half of the game. Combat-side drawing lives in [`06-drawing-and-recognition.md`](06-drawing-and-recognition.md); restoring words lives in [`10-restoration-and-challenges.md`](10-restoration-and-challenges.md).

---

## Symbols

### LEARN-01 — Be taught a fixed set of Baybayin characters
As a player, I want a defined set of characters to learn, so that the game has a finishable curriculum.
- AC: The campaign teaches 18 visual characters with 18 spoken values.
- AC: DA and RA are separate characters with separate assets and separate enemies.
- AC: E/I is one spoken value and O/U is one spoken value, each resolved from word context.
- System: Content identity · `ContentIdentity.RevisedSymbolIds`, `Char_*.asset`
- Status: Unclear — the code and assets hold 18 (`18 Char_*.asset`, `RevisedSymbolIds` reordered per Q1/R8), but the GDD and seven `docs/system/` files still assert 17 taught. See U-1.
- Refs: `Assets/Scripts/Data/Campaign/ContentIdentity.cs`, ruling Q2 / OQ-6, SALIN-217, SALIN-276

### LEARN-06 — Have every character I am taught actually make a sound
As a player, I want the audio to exist for the characters I am learning, so that the sound channel is not silently missing.
- AC: Every spoken value the campaign teaches has an assigned, correct `pronunciationClip`.
- AC: DAMA and HARAYA play different middle syllables (DA vs RA).
- System: Content · `Assets/Audio/Pronunciation/`, `Char_*.asset`
- Status: Partial — pass 2, verified on `dev`: `Assets/Audio/Pronunciation/Voice/` holds `Voice_A … Voice_YA` for all 18 identities (plus `Voice_HA_Alt`), and 39 of 40 `pronunciationClip` fields are assigned to them (A→`Voice_A`, E/I→`Voice_EI`, RA→`Voice_RA`, DA→`Voice_DA`, MA/NA/TA likewise). The one gap is `Char_OU.value.u`, which is unassigned because **no `Voice_U` clip exists** — `value.o` and `value.ou` both use `Voice_O`. Audible playback is still unverified: the 2026-09-14 playtest pressed `Listen` without error but could not hear output.
- Refs: `Assets/ScriptableObjects/Characters/Char_*.asset`, `Assets/Audio/Pronunciation/Voice/`, SALIN-208, SALIN-275, `progress/2026-09-14-level1-playtest.md` (NOT VERIFIED)

### LEARN-07 — Watch a character being written
As a player, I want to see the stroke order animated, so that I learn how to draw it, not just what it looks like.
- AC: Each learning card animates the glyph stroke by stroke from the recorded template data.
- AC: Replay Stroke and Replay Sound controls are available on the card.
- System: Learning · `SymbolLearningCardController`, `TutorialAssistAnimator`, `Resources/Templates/*`
- Status: Missing — pass 2 re-confirmed: `SymbolLearningCardController` contains only the audio replay path (`ReplayAudio`, `IsReplayAvailable`); no stroke animator, template playback or Replay Stroke control. SALIN-246 is marked Done with no implementation — a fourth instance of the Jira-automation pattern.
- Refs: `Assets/Scripts/UI/HUD/SymbolLearningCardController.cs:53,120-121,198-234`, SALIN-246, `docs/audit/BACKLOG.md` T31

### LEARN-10 — Not be shown a character the game never teaches
As a player, I want the counters and grids to only contain characters in the curriculum, so that "learned all" is reachable.
- AC: The character registry contains exactly the taught set.
- AC: Every character in the registry is carried by at least one enemy somewhere in the campaign.
- System: Content · `CharacterRegistrySO`, `AlmanacController`
- Status: Partial — pass 2: `CharacterRegistry_Default.asset` holds **18** entries, so the registry matches the ruling. The second criterion fails: RA is carried only by `EnemyData_Ragasa`, which is in no wave and no pool, so RA cannot be met in play and the counter cannot reach 18/18 until Level 13 is authored. See U-10.
- Refs: `Assets/ScriptableObjects/Characters/CharacterRegistry_Default.asset`, SALIN-217, SALIN-261

---

## Words

### LEARN-12 — Understand what a word means
As a player, I want each target word's meaning in plain language, so that restoring it means something.
- AC: `FocusWordDefinition.meaning` is authored for every focus word and displayed on the preview.
- System: Content · `FocusWordDefinition`, `CampaignConfigValidator`
- Status: Partial — authored and required for Ugat; Levels 6–15 focus-word content is incomplete and Level 13 has none.
- Refs: `Assets/Scripts/Data/Campaign/FocusWordDefinition.cs`, `docs/review/focus-word-checklist.md`, SALIN-250

### LEARN-14 — See a word's syllables as Baybayin, not Latin
As a player, I want the decomposition drawn in Baybayin, so that I am reading the script rather than a transliteration.
- AC: Each syllable in the decomposition renders its Baybayin glyph badge art.
- AC: Latin syllables are the documented fallback only while badge art is missing.
- System: Learning · `FocusWordPreviewController`, `BaybayinCharacterSO.badgeSprite`
- Status: Partial — the fallback is what ships; glyph badge art existed for 7 of 18 symbols at last audit.
- Refs: `FocusWordPreviewController.cs` header, SALIN-257 (To Do)

### LEARN-15 — Study the target words properly before combat
As a player, I want to see, hear and understand both target words before I have to fight for them, so that the restoration means something.
- AC: The preview offers tappable syllable buttons that speak, and a Hear Word control that plays the whole word.
- AC: Each focus word's `contextImage` is displayed when assigned; the slot is hidden when null.
- AC: The Continue control is disabled until both word cards have been inspected.
- System: Learning · `FocusWordPreviewController`, `ContentMediaReferences.contextImage`
- Status: Missing — the preview is a single readable text block: no image slot, no tap-to-hear, no inspection gating. Per-word context art is ruled in scope for the demo (Q10) but is not authored; missing media is a validator Warning, not an Error.
- Refs: `FocusWordPreviewController.cs`, SALIN-245 (To Do), SALIN-257, ruling Q10, `docs/audit/BACKLOG.md` T30
- Merged: absorbs LEARN-16, LEARN-17

## Free practice

### PRAC-08 — Reach practice from a symbol's Codex entry
As a player, I want to jump from a symbol I am reading about straight into practising it, so that study and practice are one action.
- AC: A **Practice Symbol** control on a Codex entry opens the tracing surface with that symbol preselected.
- System: Codex · `AlmanacController` → tracing surface
- Status: Missing — no link from the Codex to the tracing surface exists; the Dojo is reached only from the main menu.
- Refs: SALIN-268 (To Do), `docs/audit/BACKLOG.md` T56

---

## Mastery and review

### MAST-01 — See how well I know each character, across more than one skill
As a player, I want a visible per-character state covering shape and sound, so that I can tell what I have merely seen from what I actually know.
- AC: Each symbol and focus word carries a mastery state of `Introduced`, `Practiced`, `Recalled` or `Mastered`.
- AC: Symbols track **Form** and **Sound**; words track **Assembly** and **Meaning**.
- AC: A dimension with no applicable evidence is not counted against the player.
- AC: The state is surfaced on Results and review screens.
- System: Learning · `MasteryEvaluator`, `MasteryDimensions`, `LearningStateSnapshot`, `LevelResultsCalculator`
- Status: Partial — the model, dimensions, evaluator and snapshot are implemented and committed with the save. The Codex mastery bars that display them per symbol are not built (CODEX-06), and Sound records only pronunciation exposures.
- Refs: `Assets/Scripts/Data/Learning/MasteryEvaluator.cs`, `MasteryDimensions.cs`, `docs/design/scoring-and-stars.md`, SALIN-175, SALIN-268
- Merged: absorbs MAST-02

### MAST-06 — Be brought back to characters I am forgetting
As a player, I want the game to resurface characters I have not practised recently, so that I do not quietly lose them.
- AC: A review scheduler determines which symbols and words are due.
- AC: The player is offered a review session containing the due items.
- AC: A review session records evidence only and never changes level progression.
- System: Learning · `ReviewScheduler`, `ReviewDueItem`, `LearningSessionKind.ScheduledReview`
- Status: Partial — `ReviewScheduler` and the `ScheduledReview` session kind are implemented and unit-tested, and the intake path accepts them, but nothing in the runtime consumes the scheduler: its only non-test referents are `ReviewDueItem` and `LearningStateSnapshot`. There is no review screen or entry point.
- Refs: `Assets/Scripts/Data/Learning/ReviewScheduler.cs`, `docs/system/04_Gameplay_Systems.md` §12.1

## Codex / Almanac

### CODEX-02 — See how much of the script I have collected
As a player, I want a "learned x / y" counter, so that I can see the collection filling up.
- AC: The Baybayin tab shows `Learned x/y` where `y` is the taught-set size.
- AC: The counter can reach 100% — every registry entry is obtainable in play.
- System: Almanac · `AlmanacController.FormatCounter`, `CountUnlockedCharacters`
- Status: Partial — the counter renders against a registry of 18, but 18/18 is unreachable in play until an RA enemy actually spawns (LEARN-10, U-10). The 17-vs-18 documentation conflict (U-1) no longer affects the code.
- Refs: `AlmanacController.cs`, `CharacterRegistry_Default.asset`, SALIN-217, SALIN-261

### CODEX-05 — Browse the enemies I have met
As a player, I want an enemy tab, so that I can look up what a corrupted enemy does.
- AC: An Enemies tab shows one cell per enemy and boss type.
- AC: A cell reveals once the player has encountered that enemy.
- AC: The Enemies tab is kept (ruling Q15) and lists the 18 corrupted enemies keyed to their symbols, with no legacy colonial enemies.
- System: Almanac · `AlmanacController`, `AlmanacEnemyRegistrySO`, `AlmanacEnemyDiscovery`
- Status: Partial — the tab exists, but enemy reveals are gated on `Era.Spanish`, the era enum is still colonial, and the registry still lists the retired El Inquisidor, Superintendent and Kadiliman. See U-12.
- Refs: `AlmanacController.cs:262`, `Assets/Scripts/UI/Almanac/AlmanacEnemyDiscovery.cs`, SALIN-260 (To Do)

### CODEX-06 — See a symbol's readings, first use and later uses
As a player, I want a character's entry to show where it appears in the campaign, so that the Codex is a study tool.
- AC: An unlocked symbol entry shows its spoken value(s), its first use, and its later uses.
- AC: It shows four mastery bars, one per dimension.
- AC: A locked cell reads e.g. "Appears in Ugnayan".
- System: Codex · `AlmanacController`, `AlmanacDetailScroll`, `LearningStateSnapshot`
- Status: Missing — the detail scroll shows portrait, name and description only.
- Refs: SALIN-268 (To Do), `docs/audit/BACKLOG.md` T56
