# 04 — Level flow (LF-CONTRACT-v2)

Prefix **`FLOW`**. Covers the ordered phases of a level attempt, the segment loop, and the screens that gate the transitions between them.

**Contract order:** `Story → FocusWords → SymbolLearning → RequiredPractice → Defense → ContextChallenge → MemoryReward → AtomicSave → Results`, with terminal states `Completed`, `Defeated`, `Exited`.

---

### FLOW-01 — Move through a level in a fixed, predictable order
As a player, I want each level to follow the same shape, so that I always know where I am in it.
- AC: Every level runs its planned phases in the contract order above.
- AC: The current phase is the single source of truth for what the player can do.
- AC: Every phase change raises `PhaseChanged(previous, next)`.
- System: Level flow · `LevelFlowMachine`, `LevelPhasePlan`, `LevelFlowController`
- Status: Existing
- Refs: `Assets/Scripts/Gameplay/Flow/LevelFlowMachine.cs`, `LevelPhase.cs`, `LevelPhasePlan.cs`, SALIN-178

### FLOW-02 — Skip phases a level does not use
As a player, I want a level with no new symbols to go straight to the part that matters, so that I am not shown empty screens.
- AC: `FocusWords`, `SymbolLearning` and `RequiredPractice` are planned only when the level config has the matching content.
- AC: An unplanned phase is skipped with no screen and no executor.
- AC: `Story`, `Defense`, `ContextChallenge`, `MemoryReward`, `AtomicSave` and `Results` are always planned.
- System: Level flow · `LevelPhasePlan.FromConfig`
- Status: Existing
- Refs: `LevelPhasePlan.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1

### FLOW-03 — Never have a level quietly complete on combat alone
As a player, I want clearing the waves not to be enough, so that finishing a level means I actually did the language work.
- AC: `ContextChallenge` and `MemoryReward` are planned on every level, including for a null config.
- AC: A level missing a `challengeSequence`, or missing `rewardIds` **or** a `contextMedia.cutscene`, shows a content-missing panel and refuses to complete the phase.
- AC: The flow then never reaches `AtomicSave`, never raises `LevelComplete`, and the next level does not unlock.
- System: Level flow · `LevelPhasePlan` (`ContextChallengeContentMissing`, `MemoryRewardContentMissing`), `LevelContentMissingPanel`
- Status: Existing
- Refs: `LevelPhasePlan.cs:168-180`, `LevelFlowController.cs:608-648`, `Assets/Scripts/UI/LevelContentMissingPanel.cs`, SALIN-223

### FLOW-04 — Be told when a level is unfinished content rather than left confused
As a player who reaches an unauthored level, I want a clear message, so that I do not think the game is broken.
- AC: The content-missing panel names the level and states that it cannot be completed.
- AC: The panel holds the flow until the attempt goes terminal; it never advances.
- System: Level flow · `LevelContentMissingPanel`
- Status: Partial — the panel and refusal behaviour exist; the player-facing copy is still awaiting a language/product ruling.
- Refs: `LevelContentMissingPanel.cs`, SALIN-223 (manual/copy gate open)

### FLOW-05 — Have the level's save committed before I see Victory
As a player, I want Victory to appear only once my progress is safely written, so that a crash cannot take away a level I was told I finished.
- AC: `ReportPhaseComplete(AtomicSave)` is rejected; `AtomicSave` advances only through `ReportSaveResult(true)`.
- AC: `ReportSaveResult(false)` holds the machine in `AtomicSave` for the retry loop rather than failing the level.
- AC: Victory and Next are shown only for outcome status `Committed` or `AlreadyCommitted`.
- System: Level flow · `LevelFlowMachine`, `CampaignOutcomeCoordinator`, `VictoryScreenUI`
- Status: Existing
- Refs: `LevelFlowMachine.cs`, `docs/system/04_Gameplay_Systems.md` §5.1.1, SALIN-174

### FLOW-06 — Have malformed flow reports rejected instead of corrupting my run
As a player, I want a double-tap or a stray system report not to break the level, so that the run stays coherent.
- AC: A completion report for a phase other than the current one is rejected with no state change.
- AC: A duplicate report for a phase already left is rejected.
- AC: Any report after a terminal state is rejected.
- System: Level flow · `LevelFlowMachine`
- Status: Existing
- Refs: `LevelFlowMachine.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1

### FLOW-07 — Have combat unable to hand me the level
As a player, I want the defence systems not to be able to declare the level complete, so that the learning phases cannot be bypassed by a combat bug.
- AC: Defense systems can only call `ReportDefenseComplete()`; they cannot mark the level complete or write campaign rewards.
- System: Level flow · `LevelFlowMachine`, `WaveManager`
- Status: Existing
- Refs: `LevelFlowMachine.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1

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

### FLOW-11 — Get a beat to prepare before combat starts
As a player, I want a moment between the lesson and the fight, so that combat does not start the instant I finish reading.
- AC: A Ready screen shows the level title and objective and holds until the player presses Start.
- AC: The screen offers a way back out of the level that preserves what has been done.
- System: Level flow · `LevelReadyScreenController`
- Status: Partial — `LevelReadyScreenController` exists with `Present`, `StartLevel`, `Back` and builds its title from the level config, but the audit records no Ready screen at all in the production flow and SALIN-235 was closed as obsolete. See U-5.
- Refs: `Assets/Scripts/UI/LevelReadyScreenController.cs`, SALIN-235 (closed obsolete), `docs/audit/STATUS-2026-09-13.md` Master row 10

### FLOW-12 — See what I am about to face on the Ready screen
As a player, I want the Ready screen to preview the enemy types and clue forms in this level, so that I can brace for them.
- AC: The Ready screen lists the level's enemy types and the clue channels it uses, plus the heart count.
- System: Level flow · `LevelReadyScreenController`
- Status: Missing — the controller renders a title and an objective line only; no enemy/clue preview.
- Refs: `LevelReadyScreenController.cs`, `docs/audit/BACKLOG.md` T20

### FLOW-13 — Not be able to draw during a phase that is not about drawing
As a player, I want my finger ignored while a card or overlay is open, so that I do not waste a stroke or trigger something by accident.
- AC: `GameManager.SuppressDrawingInput(true)` makes `AcceptsDrawingInput` false even while `Playing`.
- AC: Suppression is lifted in a `finally` block so an interrupted coroutine cannot leave input locked.
- AC: Suppression is force-cleared when the state becomes `GameOver` or `LevelComplete`.
- System: Core · `GameManager`, `StrokeCapture`
- Status: Existing
- Refs: `Assets/Scripts/Core/GameManager.cs` (`SuppressDrawingInput`, `AcceptsDrawingInput`), `docs/system/04_Gameplay_Systems.md` §9.4

### FLOW-14 — Have the level's music start with the level
As a player, I want the level's music to begin when the level begins, so that the atmosphere is set.
- AC: `LevelConfigSO.bgmClip` is played after the pre-combat beats and before waves start.
- System: Level flow · `LevelFlowController`, `AudioManager`
- Status: Existing
- Refs: `LevelFlowController.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §2

### FLOW-15 — Have a level I abandon leave no trace on the next attempt
As a player, I want restarting or quitting a level to give me a genuinely clean attempt, so that nothing carries over.
- AC: `AbortCurrentLevelAttempt()` is the single entry point for leaving an attempt incomplete, and is idempotent.
- AC: On abort: no outcome is committed, spawning stops, the boss state machine exits, partial stroke input is discarded, no progress is recorded, both pause latches clear, and the pause menu closes.
- System: Core · `GameManager.AbortCurrentLevelAttempt`, `EventBus.OnLevelAttemptAborted` (9 subscribers)
- Status: Existing
- Refs: `docs/system/03_Core_Systems.md` §8.2, SALIN-141

### FLOW-16 — Be shown the level's stage art for its era
As a player, I want the battlefield to look like the era I am in, so that the three eras feel different to play in.
- AC: The level applies its era stage background (`StageBackground_Ugat/Ugnayan/Pamana`).
- System: Environment · `EnvironmentThemeSwapper`, `StageBackgroundBaker`, `StageBackgroundSO`
- Status: Partial — the era stage background assets and the swapper exist and level names/eras are migrated, but `eraTheme` on the level configs was last recorded pointing at legacy colonial theme assets; needs a visual re-check.
- Refs: `Assets/Scripts/Gameplay/Environment/EnvironmentThemeSwapper.cs`, SALIN-218

### FLOW-17 — Have the level tutorial run once and only once
As a first-time player, I want in-level guidance the first time and not after, so that a replay is not slowed down.
- AC: A level's tutorial / onboarding sequence runs on a first-time entry and is skipped on later entries.
- AC: `LevelConfigSO.alwaysShowTutorial` can force it for authoring and testing.
- AC: Reset Journey brings it back.
- System: Tutorial · `Level1OnboardingController`, `OnboardingPersistence`, `LevelTutorialProgress`
- Status: Existing
- Refs: `Assets/Scripts/Gameplay/Tutorial/Onboarding/OnboardingPersistence.cs`, `Assets/Scripts/UI/LevelTutorialProgress.cs`

### FLOW-18 — Learn something new in Level 2's onboarding
As a player, I want Level 2 to teach me its own thing, so that the second level is not a repeat of the first.
- AC: A first-time player entering Level 2 sees an onboarding sequence once; a second entry skips it.
- AC: The sequence does not teach a removed mechanic.
- System: Tutorial · `OnboardingSequenceSO`, Level 2 onboarding beats
- Status: Partial — the combo/Focus teach beats were removed with SALIN-225 and SALIN-241 is marked Done, but the Level 2 on-screen onboarding and mass-clear check are recorded as manually unverified.
- Refs: SALIN-241, SALIN-225, `docs/audit/STATUS-2026-09-13.md` (SALIN-241 gate)

### FLOW-19 — Have the retired practice phase not waste my time
As a player, I want no empty "required practice" screen, so that the flow does not stall on a stub.
- AC: `RequiredPractice` has no executor and does not present a screen.
- AC: The phase is either unplanned or auto-completes without player input.
- System: Level flow · `LevelFlowController.ExecutePhase` (no `RequiredPractice` case)
- Status: Existing — retired by ruling D-004 (SALIN-228 closed obsolete). The enum member remains for save/serialisation stability.
- Refs: `LevelFlowController.cs:319-333`, `LevelPhase.cs`, SALIN-228

### FLOW-20 — Have the level's phase not fight with the app's pause state
As a player, I want pausing mid-phase to be safe, so that resuming puts me back exactly where I was.
- AC: App-level `GameState` (Idle/Playing/Paused/GameOver/LevelComplete/Practicing) is independent of the level phase.
- AC: A terminal phase transition clears `IsPaused`.
- System: Core · `GameManager`, `LevelFlowMachine`
- Status: Existing
- Refs: `docs/system/03_Core_Systems.md` §1.3, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1
