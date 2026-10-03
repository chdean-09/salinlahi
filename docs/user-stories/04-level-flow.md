# 04 — Level flow (LF-CONTRACT-v2)

Prefix **`FLOW`**. Covers the ordered phases of a level attempt, the segment loop, and the screens that gate the transitions between them.

**Contract order:** `Story → FocusWords → SymbolLearning → RequiredPractice → Defense → ContextChallenge → MemoryReward → AtomicSave → Results`, with terminal states `Completed`, `Defeated`, `Exited`.

---

### FLOW-04 — Be told when a level is unfinished content rather than left confused
As a player who reaches an unauthored level, I want a clear message, so that I do not think the game is broken.
- AC: The content-missing panel names the level and states that it cannot be completed.
- AC: The panel holds the flow until the attempt goes terminal; it never advances.
- System: Level flow · `LevelContentMissingPanel`
- Status: Partial — the panel and refusal behaviour exist; the player-facing copy is still awaiting a language/product ruling.
- Refs: `LevelContentMissingPanel.cs`, SALIN-223 (manual/copy gate open)

### FLOW-08 — Alternate fighting and restoring within one level
As a player, I want later levels to switch between defending and restoring, so that the two halves of the game interleave instead of running once each.
- AC: `LevelConfigSO.flowSegments` declares an ordered list of wave groups and the challenge units that follow each.
- AC: Clearing a segment's waves opens that segment's restoration; a correct restoration resumes the next wave group.
- AC: `AtomicSave` and `Results` remain terminal and run once.
- System: Level flow · `LevelFlowSegment`, `LevelPhasePlan`, `LevelFlowMachine`, `WaveManager`
- Status: Partial — the engine support exists and Level 5 is authored with 2 segments; Levels 10 and 15 have no segments authored.
- Refs: `Assets/Scripts/Data/LevelFlowSegment.cs`, `Level5_Config.asset` (`flowSegments`), SALIN-226, SALIN-281, SALIN-247 (In Progress)

### FLOW-09 — Have a segment boundary act as a checkpoint
As a player, I want the work I finished in an earlier segment to stay done, so that dying later does not undo it.
- AC: A completed segment's restoration lines stay locked when a later wave is retried.
- System: Level flow · `LevelFlowMachine` (segment index), `ChallengeSession` (`CheckpointReset`)
- Status: Partial — the machine tracks a segment index and the challenge session has a checkpoint-reset state, but full state restoration across a retry is recorded as not established.
- Refs: `LevelFlowMachine.cs:46,90-95`, `docs/audit/STATUS-2026-09-13.md` Master row 19

### FLOW-10 — Read my objectives before the level starts
As a player, I want to be told what this level asks of me, so that I know what winning requires.
- AC: After the story beat, a mission objective card states the story goal, the language goal, the combat goal, the heart count, and the final restoration condition.
- AC: A control continues from the card into the word study.
- System: Level flow · `MissionObjectiveCard` (not present)
- Status: Missing — no `MissionObjectiveCard` exists anywhere in `Assets/`. SALIN-244 is marked Done with no implementation.
- Refs: SALIN-244 (Done in Jira — unimplemented), `docs/audit/BACKLOG.md` T29

### FLOW-12 — See what I am about to face on the Ready screen
As a player, I want the Ready screen to preview the enemy types and clue forms in this level, so that I can brace for them.
- AC: The Ready screen lists the level's enemy types and the clue channels it uses, plus the heart count.
- System: Level flow · `LevelReadyScreenController`
- Status: Missing — the controller renders a title and an objective line only; no enemy/clue preview.
- Refs: `LevelReadyScreenController.cs`, `docs/audit/BACKLOG.md` T20

### FLOW-18 — Learn something new in Level 2's onboarding
As a player, I want Level 2 to teach me its own thing, so that the second level is not a repeat of the first.
- AC: A first-time player entering Level 2 sees an onboarding sequence once; a second entry skips it.
- AC: The sequence does not teach a removed mechanic.
- System: Tutorial · `OnboardingSequenceSO`, Level 2 onboarding beats
- Status: Missing by ruling — `8131a6b8` cleared `Level2_Config.onboardingSequence`, so Level 2 now carries no onboarding at all and matches Levels 3–5. The `MassClearTeach` beat it ran taught the multi-kill chain, which is switched off (`multiKillChainEnabled: 0`) on every Ugat level, and a beat present at Level 2 but absent from Level 1 is guidance arriving after it was withdrawn. `Level2AdvancedOnboardingSequence.asset` and its copy are kept unreferenced for a chain-enabled level. This story stands as a design question — whether Level 2 should teach anything of its own — not as a wiring gap.
- Refs: SALIN-241, SALIN-225, `8131a6b8`, `Assets/ScriptableObjects/Levels/Level2_Config.asset`, `docs/audit/STATUS-2026-09-13.md` (SALIN-241 gate)
