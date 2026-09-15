# 10 — Restoration and context challenges

Prefix **`REST`**. Covers the `ContextChallenge` phase: how the player puts words back into text, the combat auto-fill path, the difficulty tiers, hints, the final syllable, and the era paragraph.

**Two paths ship today, and that is the conflict in U-7.**
1. **Combat auto-fill** — on Levels 1–5 (`activeClueRestorationEnabled: 1`) the target text fills as the player defeats the enemies carrying the needed syllables; completing the text wins the encounter.
2. **The separate challenge board** — `ChallengeModeUI` / `ChallengeSession` still present candidate buttons and accept placement submissions, which D-003 rules should be replaced by the auto-fill.

**Challenge modes available:** `GuidedTracing`, `WordPlacement`, `SentenceRestoration`, `ParagraphRestoration`, `TimedMemory`.

---

## Filling the target text

### REST-02 — Have a syllable fill itself when I defeat the enemy carrying it
As a player, I want a correct draw to restore the word directly, so that I never have to switch from fighting to a separate puzzle screen.
- AC: A correct draw that resolves against an enemy carrying a needed syllable fills that slot.
- AC: Slots fill in reading order within a word.
- System: Restoration · `ActiveCluePresenter`, `TargetTextSlotMap`, `CombatResolver`
- Status: Partial — implemented on Levels 1–5. SALIN-277 is recorded as a **DRAFT (unapproved)** design, and D-003 auto-fill is recorded as not fully implemented because the separate board survives. See U-7.
- Refs: SALIN-277, `docs/audit/STATUS-2026-09-13.md` Master row 21

### REST-03 — See restoration progress clearly on a phone
As a player, I want a readable beat when a slot fills and a board that fits my screen, so that restoration feels like an achievement rather than a puzzle I cannot parse.
- AC: Filling a slot plays a distinct visual and audio acknowledgement.
- AC: A rejected placement has its own distinct tell.
- AC: Paragraph units use **word** tiles; word units use **syllable** tiles, so the tile count per screen stays small.
- AC: One `ChallengeUnit` per checkpoint line.
- System: Restoration · `ActiveCluePresenter`, `ChallengeModeUI`, challenge authoring
- Status: Unclear — the word-tile recommendation (R7) is accepted, but auto-fill animation, timing, the rejection tell, tile sizing and overflow behaviour are all unspecified. `ChallengeModeUI` currently routes `ParagraphRestoration` through the same Latin candidate-button path as `SentenceRestoration`, which is exactly what R7 warned puts 14+ tokens on a phone screen at Level 5. See U-6.
- Refs: `ChallengeModeUI.cs:166-167`, `ChallengeSession.cs:235-236,262`, ruling R7, `docs/audit/STATUS-2026-09-13.md` "Requirements too vague"
- Merged: absorbs REST-26

## The challenge board

### REST-08 — Place Baybayin tiles rather than Latin words
As a player, I want to assemble words from Baybayin glyph tiles, so that I am handling the script rather than its transliteration.
- AC: Word-level units use syllable tiles; paragraph units use word tiles.
- AC: A wrong tile shakes and returns; Undo returns the last tile; Submit validates all slots at once and reopens only the wrong ones.
- AC: A Hear control plays the word or sentence.
- System: Challenges · `ChallengeModeUI`, `BaybayinCharacterSO.glyphOutlineSprite`
- Status: Missing — `ChallengeModeUI` builds Latin word buttons validated on tap; there is no tile board, no Undo, no batch Submit and no Hear control. SALIN-233 was closed **obsolete under D-003** in favour of combat auto-fill, so this story is superseded rather than merely unbuilt — recorded here because the Latin board it was meant to replace still ships.
- Refs: `Assets/Scripts/Gameplay/ChallengeModeUI.cs:124-159`, SALIN-233 (closed obsolete), ruling R7

### REST-11 — Restore inflected words by their root
As a player, I want affixes and reduplication to stay as fixed text, so that I am only asked to place real root words.
- AC: Restorable tokens are root words (e.g. SAMA, UNA).
- AC: Affixes and reduplication ("SAMA-SAMA", "maUNA") are fixed text around the blank, not player-placed.
- System: Challenge content · `ChallengeSequenceSO` token authoring
- Status: Partial — ruled (Q9) and authored for Ugat; Ugnayan and Pamana challenge content is unauthored.
- Refs: ruling Q9, SALIN-249 / SALIN-252 (To Do)

### REST-12 — Face two blanks in Ugat Levels 3 and 4
As a player, I want the later Ugat levels to ask for two words, so that difficulty rises within the era.
- AC: `Challenge_Ugat03_Context` and `Challenge_Ugat04_Context` each present two blanks.
- AC: The narrative copy matches two blanks, not one.
- System: Challenge content · `Challenge_Ugat03/04_Context.asset`
- Status: Unclear — ruled OQ-1 (two blanks stand, superseding the 2026-09-01 one-blank amendment), but the narrative doc line `docs/content/ugat-levels-2-5-narrative.md:137` still reads "Isang salita lamang ang kulang" and the assets were last verified unchanged.
- Refs: ruling OQ-1, SALIN-144 / SALIN-146, `docs/audit/BACKLOG.md` T34

---



## The final syllable

### REST-21 — Finish a level by writing its last syllable myself
As a player, I want the last act of a level to be one deliberate trace, so that finishing has a ceremony.
- AC: After both words are placed, the board shows the word with its last slot empty and a short focus moment.
- AC: `LevelConfigSO.finalRestorationValue` names the required symbol; any other symbol is rejected.
- AC: Completing it plays the memory cutscene and sets the persisted `finalSyllableRestored` flag.
- AC: The step cannot be skipped.
- System: Restoration · `ChallengeFlowController`, `ChallengeSession`, `LevelConfigSO.finalRestorationValue`
- Status: Missing — pass 2 re-confirmed on `dev`: the only non-validator reference to `finalRestorationValue` is a `TODO(SALIN-229)` in `LevelObjectiveFlagResolver` stating it "is authored but has no runtime reader, so there is nothing to check", with `finalSyllableRestored = true` hard-coded. No trace-and-place step, no focus moment, no ceremony. SALIN-229 is marked Done with no implementation — the fifth such ticket.
- Refs: `Assets/Scripts/Gameplay/Flow/LevelObjectiveFlagResolver.cs:62-66`, SALIN-229, `docs/audit/BACKLOG.md` T14

## The era paragraph

### REST-24 — Restore an era paragraph line by line
As a player, I want the fifth level of each era to ask for a paragraph in checkpointed lines, so that the era's five levels come together without one mistake costing me all of it.
- AC: Levels 5, 10 and 15 each require an era paragraph **in addition to** that level's two new focus words.
- AC: Words from earlier levels in the era appear as review blanks; the level's two new words are the taught slots.
- AC: Each paragraph line is its own `ChallengeUnit` with `checkpointOnSuccess` true.
- AC: A penalty resets only the current line.
- System: Challenge content · `Challenge_*05/10/15_Context.asset`, `ChallengeMode.ParagraphRestoration`, `ChallengeTierPolicy.checkpointResetOnPenalty`
- Status: Missing — `ParagraphRestoration` and the unit-level checkpoint mechanism both exist, but no paragraph content is authored anywhere. Level 5's challenge uses two syllable units; Levels 10 and 15 have no paragraph at all.
- Refs: `ChallengeSequenceSO.cs:49`, ruling Q4, ruling R7, SALIN-247 (In Progress), SALIN-249 / SALIN-252 (To Do)
- Merged: absorbs REST-25

### REST-25 — Have each paragraph line checkpointed
As a player, I want a completed paragraph line to stay completed, so that a later mistake does not cost me the whole paragraph.
- AC: Each paragraph checkpoint line is its own `ChallengeUnit` with `checkpointOnSuccess` true.
- AC: A penalty resets only the current line.
- System: Challenges · `ChallengeUnitDefinition.checkpointOnSuccess`, `ChallengeTierPolicy.checkpointResetOnPenalty`
- Status: Partial — the unit-level checkpoint mechanism exists; the per-line paragraph authoring it depends on does not.
- Refs: `ChallengeSequenceSO.cs:49`, ruling R7

### REST-26 — Not be handed fourteen tiles on a phone screen
As a player, I want the paragraph board to stay usable on a phone, so that the hardest level is not the least playable.
- AC: Paragraph units use **word** tiles; word units use **syllable** tiles.
- AC: One `ChallengeUnit` per checkpoint line, so the tile count per screen stays small.
- System: Challenges · challenge authoring
- Status: Unclear — the recommendation (R7) is accepted but the layout, tile sizing and overflow behaviour are unspecified. See U-6.
- Refs: ruling R7, `docs/design/spec-rulings-2026-09.md` §3
