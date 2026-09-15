# 03 — Story, dialogue and cutscenes

Prefix **`STORY`**. Covers the prologue, per-level intro/outro dialogue, per-word explanation lines, memory cutscenes, era completion, the ending, and the language policy.

**Language policy (ruling Q16).** English is **UI copy only**. Story dialogue, focus-word explanations and cutscenes stay **Filipino**. There is **no language setting**.

---

### STORY-02 — Skip the prologue
As a returning or impatient player, I want to skip a cutscene, so that I am not made to re-watch a story beat.
- AC: A visible Skip control is available while a cutscene plays.
- AC: Skipping ends the cutscene immediately and continues the level flow.
- System: Cutscenes · `CutscenePlayer`
- Status: Partial — a `_skipButton` field and `SkipCutscene()` exist, but `HideLegacySkipButton()` is called on enable and before each play, so no skip control is presented. Skip is reachable only through the tap-to-complete path.
- Refs: `CutscenePlayer.cs:20-21,74-77,342`, SALIN-242 (Done in Jira), `docs/audit/BACKLOG.md` T27

### STORY-03 — Not be shown the prologue again once I have seen it
As a returning player, I want the prologue to play only once, so that the second launch takes me straight to the game.
- AC: A viewed flag is persisted after the prologue completes or is skipped.
- AC: A later launch does not replay it.
- AC: Reset Journey clears the flag so the prologue plays again.
- System: Core persistence · `CampaignSaveDocument` (prologue flag)
- Status: Missing — no prologue/cutscene-viewed flag exists anywhere in `Assets/Scripts`. MM-16 restores the prologue only because a reset creates a new journey.
- Refs: grep for `prologue` in `Assets/Scripts` returns nothing, SALIN-242, `docs/audit/BACKLOG.md` T27

### STORY-04 — Read the story that opens each level
As a player, I want a short story beat before each level, so that the combat has a reason.
- AC: When `LevelConfigSO.introDialogue` is set, the `Story` phase plays it before anything else.
- AC: `Story` is always a planned phase, so a level cannot skip its story beat silently.
- System: Level flow · `LevelFlowController.ExecuteStory`, `DialogueController`
- Status: Partial — implemented and authored for Ugat Levels 1–5 (`Dialogue_Ugat01..05_Intro`); no `Dialogue_Ugnayan*` or `Dialogue_Pamana*` assets exist.
- Refs: `Assets/Scripts/Gameplay/LevelFlowController.cs:346`, `Assets/ScriptableObjects/Dialogue/`, SALIN-248 / SALIN-251 (To Do)

### STORY-05 — Read the story that closes each level
As a player, I want a closing beat after I restore a memory, so that the level lands emotionally.
- AC: When `LevelConfigSO.outroDialogue` is set it plays after the memory reward.
- System: Level flow · `LevelFlowController`, `DialogueController`
- Status: Partial — authored for Ugat Levels 1–5 only (`Dialogue_Ugat01..05_Outro`).
- Refs: `Assets/ScriptableObjects/Dialogue/`, SALIN-248 / SALIN-251

### STORY-09 — Read an explanation of each focus word in story voice
As a player, I want each word I am restoring explained in the story's own language, so that meaning and narrative arrive together.
- AC: Each focus word can carry its own `DialogueSO` (`FocusWordDefinition.media.dialogue`).
- AC: The explanation is in Filipino.
- System: Level content · `FocusWordDefinition`, `DialogueController`
- Status: Partial — authored for Ugat only (e.g. `Dialogue_Ugat01_Ina`, `Dialogue_Ugat01_Ama`); Eras 2–3 have none.
- Refs: `Assets/Scripts/Data/Campaign/FocusWordDefinition.cs`, `Assets/ScriptableObjects/Dialogue/`

### STORY-10 — Watch the memory I restored play out
As a player, I want a cutscene when I finish restoring a word set, so that my work visibly restores something.
- AC: `LevelConfigSO.contextMedia.cutscene` plays during the `MemoryReward` phase.
- AC: A level with `rewardIds` but no cutscene (or vice versa) is treated as half-authored and refuses to complete rather than skipping the phase.
- System: Level flow · `LevelFlowController.ExecuteMemoryReward`, `LevelPhasePlan`
- Status: Partial — `Cutscene_Ugat01..05_Memory` exist; Eras 2–3 have none, so Levels 6–15 hit the content-missing refusal.
- Refs: `Assets/ScriptableObjects/Cutscenes/`, `Assets/Scripts/Gameplay/Flow/LevelPhasePlan.cs`, SALIN-223

### STORY-13 — See the ending after the final level
As a player, I want an ending cinematic after Level 15, so that the journey resolves.
- AC: Completing Pamana Level 5 plays an ending cutscene.
- AC: A campaign-complete flag is set.
- System: Cutscenes · `Cutscene_Ending` (not present)
- Status: Missing — no `Cutscene_Ending` asset and no post-game state exist.
- Refs: SALIN-254 (To Do), `docs/audit/BACKLOG.md` T43

### STORY-14 — Have something to do after I finish the game
As a player who completed the campaign, I want the menu to offer me something, so that finishing is not a dead end.
- AC: After campaign completion the menu offers replay, the Archive, the Codex, and an ending gallery.
- System: Main Menu · post-game state (not present)
- Status: Missing — depends on STORY-13. Endless Mode, the previous answer to this, was cut by ruling Q15.
- Refs: SALIN-254, `docs/design/spec-rulings-2026-09.md` Q15

### STORY-15 — Read every story beat in Filipino
As a Filipino player, I want the narrative in Filipino, so that the heritage framing is authentic rather than translated.
- AC: All story dialogue, focus-word explanations and cutscene copy are Filipino.
- AC: UI chrome (buttons, labels, notices) is English.
- AC: No language setting is offered.
- System: All content · `docs/content/*`
- Status: Partial — Ugat content is authored in Filipino; Ugnayan and Pamana copy is drafted in `docs/content/` but not converted to assets.
- Refs: `docs/content/ugat-levels-2-5-narrative.md`, `ugnayan-levels-6-10-narrative.md`, `pamana-levels-11-15-narrative.md`, ruling Q16, SALIN-266

### STORY-17 — Meet Paglimot as the force behind the enemies
As a player, I want the enemies framed as fragments of one antagonist, so that the fight has a single villain rather than a bestiary.
- AC: Enemy lore presents each corrupted enemy as created by Paglimot, embodying the opposite of its symbol's lesson.
- System: Content · `EnemyDataSO` lore copy, cutscene and dialogue copy
- Status: Partial — the design is ruled and each enemy has description text, but the dedicated `corruptedMeaning` / `trueMeaning` / `lore` / `restoredLesson` fields do not exist on `EnemyDataSO`.
- Refs: `docs/design/spec-rulings-2026-09.md` §3, SALIN-260 (To Do)
