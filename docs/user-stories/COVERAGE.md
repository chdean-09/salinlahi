# Coverage matrix — player behaviour already implemented

Every row is a player-observable outcome that is **implemented and reachable** on `dev`. These were extracted from the story files so those files carry only outstanding work.

**This is the manual regression checklist.** After a change, the rows touching that system are what should still be true. IDs are stable and unchanged; the task CSV cites them as `COVERAGE.md · <ID>`.

Verified against local `dev` @ `80aa29ec`, 2026-09-15; re-verified against `feature/ugat-2-5-realignment` @ `9198e8f4` (= `dev` @ `6fc34851` + 4), 2026-09-15. The 40 commits between the two are summarised in [`PROGRESS-DELTA-2026-09-15.md`](PROGRESS-DELTA-2026-09-15.md). Outstanding work lives in the numbered files listed in [`00-overview.md`](00-overview.md).

## 01 — App entry and Main Menu

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `MM-01` | Launch straight into the game | Bootstrap · `BootstrapLoader`, `SceneLoader` | `Assets/Scripts/Core/BootstrapLoader.cs`, `Assets/Scripts/Core/SceneLoader.cs` (`CreateFadeCanvas`), `docs/system/02_Architecture_and_Runtime_Flow.md` §2 |
| `MM-02` | Have my journey loaded before the menu appears | Core persistence · `SaveManager`, `CampaignSaveService` | `Assets/Scripts/Core/SaveManager.cs`, `docs/system/03_Core_Systems.md` §2.8, SALIN-219 |
| `MM-03` | Play offline | Whole app | `docs/capstone/GDD.md` §1.3, `docs/system/08_Mobile_Performance_and_Offline_Constraints.md` |
| `MM-04` | Play in portrait with one hand | Player settings · `AspectLockedCamera`, `SafeAreaHandler` | `Assets/Scripts/UI/SafeAreaHandler.cs`, `Assets/Scripts/Gameplay/Camera/AspectLockedCamera.cs` |
| `MM-05` | Start a brand-new journey | Main Menu · `MainMenuUI`, `JourneyEntryResolver` (`NewJourney`) | `Assets/Scripts/UI/MainMenuUI.cs`, `Assets/Scripts/Data/Persistence/JourneyEntryResolver.cs`, SALIN-136 |
| `MM-06` | Continue where I left off | Main Menu · `JourneyEntryResolver` (`ContinueLevel`, `CompletedJourney`) | `JourneyEntryResolver.cs`, `MainMenuUI.cs:89-99`, SALIN-255 (hub destination still To Do) |
| `MM-07` | Be stopped rather than silently reset when my save cannot be read | Core persistence · `JourneyEntryResolver`, `CampaignSaveRecoveryResolver` | `JourneyEntryResolver.cs`, `Assets/Scripts/Data/Persistence/CampaignSaveRecoveryResolver.cs` |
| `MM-09` | Open the Baybayin Codex from the menu | Main Menu → Almanac · `MainMenuUI.OnAlmanacPressed`, `AlmanacController` | `MainMenuUI.cs:184-188`, `Assets/_Scenes/Almanac.unity` |
| `MM-10` | Open the Memory Archive from the menu | Main Menu → Memory Archive · `MemoryArchiveController` | `Assets/Scripts/UI/MemoryArchiveController.cs`, `MainMenuUI.cs:19-20,192-198`, SALIN-240 |
| `MM-11` | Open Settings from the menu | Main Menu · `SettingsPanel` | `Assets/Scripts/UI/SettingsPanel.cs` |
| `MM-12` | Read the credits | Main Menu · `CreditsPanel` | `Assets/Scripts/UI/CreditsPanel.cs`, `docs/audio/audio-credits.md` |
| `MM-13` | Quit the game deliberately | Main Menu · `ExitConfirmationPanel` | `Assets/Scripts/UI/ExitConfirmationPanel.cs`, `MainMenuUI.cs:275-352`, SALIN-256 |
| `MM-14` | Start over without wiping my save by accident | Settings · `ResetJourneyFlow`, `ResetJourneyConfirmationPanel` | `Assets/Scripts/UI/ResetJourneyFlow.cs`, `SettingsPanel.cs:28-30` |
| `MM-15` | Know exactly what a reset destroys before I confirm it | Settings · `ResetJourneyConfirmationPanel` | `Assets/Scripts/UI/ResetJourneyConfirmationPanel.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §7 |
| `MM-16` | Get a clean journey after a reset, with the story from the start | Core persistence · `CampaignOutcomeCoordinator`, `SaveManager.ResetJourneyAtomically` | `Assets/Scripts/Core/SaveManager.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §7 |
| `MM-17` | Be told once when my old progress was migrated | Main Menu · `CampaignSaveNoticePanel`, `LegacyMigrationBuilder` | `Assets/Scripts/UI/CampaignSaveNoticePanel.cs`, `Assets/Scripts/Data/Persistence/LegacyMigrationBuilder.cs`, SALIN-272 |
| `MM-18` | Be told once when my save had to be recovered | Main Menu · `CampaignSaveNoticePanel`, `CampaignSaveNoticeCopy` | `Assets/Scripts/UI/CampaignSaveNoticeCopy.cs`, SALIN-290, SALIN-227 |
| `MM-19` | Be blocked, not reset, by a save from a newer version | Core persistence · `SaveManager`, `CampaignSaveValidator` | `SaveManager.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §7 |
| `MM-20` | Have a level completion that failed to save retried at next launch | Core persistence · `CampaignOutcomeCoordinator`, `SaveManager.RetryPendingOutcome` | `Assets/Scripts/Data/Persistence/CampaignOutcomeCoordinator.cs`, SALIN-174 |
| `MM-21` | Hear menu music — both clips are assigned on `[Manager] AudioManager.prefab`; the context is resolved from the scene name with short fade-out/fade-in. | Main Menu · `AudioManager` (`BgmContext`, `_homeScreenBgmClip`, `_gameplayBgmClip`) | `Assets/Scripts/Core/AudioManager.cs:8,182,261-268`, `Assets/Prefabs/Managers/[Manager] AudioManager.prefab:76-77` |
| `MM-23` | Hear the menu respond to my taps — both clips assigned on the AudioManager prefab. | Main Menu · `AudioManager.PlayMenuButtonClip`, `_menuButtonClickClip`, `_menuExitButtonClickClip` | `AudioManager.cs:22-23,692-697,993`, `[Manager] AudioManager.prefab:53-54` |

## 02 — Journey map and level entry

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `MAP-01` | See the levels of an era laid out | Level Select · `LevelSelectUI`, `LevelButton`, `EraConfigSO` | `Assets/Scripts/UI/LevelSelectUI.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §3.5 |
| `MAP-02` | Move between eras on the map | Level Select · `LevelSelectUI` | `LevelSelectUI.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §3.5 |
| `MAP-03` | Recognise each era by its own art | Level Select · `EraConfigSO` | `Assets/Scripts/Data/EraConfigSO.cs`, `LevelSelectUI.cs` |
| `MAP-05` | See which levels I have finished — pass 2: `_completionBadge` is assigned with a non-zero reference on all five `LevelButton` instances in `LevelSelect.unity` on `dev` (the audit's "0 occurrences" predates PR #126). Visual confirmation in play is still outstanding. | Level Select · `LevelButton` | `Assets/Scripts/UI/LevelButton.cs`, `Assets/_Scenes/LevelSelect.unity:313,906,1066,1787,2111`, SALIN-137 |
| `MAP-07` | See that a level is locked | Level Select · `LevelLockResolver`, `LevelButton` | `Assets/Scripts/Data/Persistence/LevelLockResolver.cs`, SALIN-137 |
| `MAP-10` | Have a whole era locked until I finish the era before it | Level Select · `LevelLockResolver` (cross-era requirements) | `LevelLockResolver.cs`, SALIN-137 |
| `MAP-11` | Read a level's title before I play it | Level content · all 15 `Level*_Config.asset` | `Assets/ScriptableObjects/Levels/Level*_Config.asset`, SALIN-218, SALIN-258 |
| `MAP-17` | Hear that a level is locked — clip assigned on the AudioManager prefab and pre-trimmed for immediate attack. | Level Select · `AudioManager` (`_levelLockedClip`) | `AudioManager.cs:61-62,544-551,998`, `[Manager] AudioManager.prefab:68` |

## 03 — Story, dialogue and cutscenes

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `STORY-01` | See the prologue that sets up the journey | Cutscenes · `CutscenePlayer`, `LevelCutsceneMappingSO`, `Level1_Opening.asset` | `Assets/Scripts/UI/CutscenePlayer.cs`, `Assets/ScriptableObjects/Cutscenes/Level1_Opening.asset`, `LevelCutsceneMapping.asset` (one entry, for level 1), SALIN-242 |
| `STORY-06` | Have the game pause itself while I read a story panel | Dialogue · `DialogueController`, `GameManager.EnterDialoguePause` | `Assets/Scripts/UI/DialogueController.cs`, `Assets/Scripts/Core/GameManager.cs`, `docs/system/03_Core_Systems.md` §8.1 |
| `STORY-07` | Read at my own pace, and skip ahead a line at a time | Dialogue · `DialogueController` | `DialogueController.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §5.5 |
| `STORY-08` | See who is speaking | Dialogue · `DialogueSO`, `DialogueController` | `Assets/Scripts/Data/DialogueSO.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §5.5 |
| `STORY-11` | See an era close before the next one opens — implemented as a self-building overlay; the Ugnayan and Pamana ending lines are unauthored content. | Era completion · `EraCompletionScreenUI`, `EraCompletionCopy` | `Assets/Scripts/UI/EraCompletionScreenUI.cs`, SALIN-253 |
| `STORY-12` | Not be offered "Next Level" at the end of an era | Results · `VictoryScreenUI`, `LevelFlowController` | `Assets/Scripts/UI/VictoryScreenUI.cs`, `LevelFlowController.cs:1440-1462`, SALIN-258 |
| `STORY-16` | Not have a cutscene hide a control I still need | UI layering · `CutscenePlayer`, `RenderOrder` | `Assets/Scripts/Gameplay/Rendering/RenderOrder.cs`, SALIN-292 |

## 04 — Level flow

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `FLOW-01` | Move through a level in a fixed, predictable order | Level flow · `LevelFlowMachine`, `LevelPhasePlan`, `LevelFlowController` | `Assets/Scripts/Gameplay/Flow/LevelFlowMachine.cs`, `LevelPhase.cs`, `LevelPhasePlan.cs`, SALIN-178 |
| `FLOW-02` | Skip phases a level does not use | Level flow · `LevelPhasePlan.FromConfig` | `LevelPhasePlan.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1 |
| `FLOW-03` | Never have a level quietly complete on combat alone | Level flow · `LevelPhasePlan` (`ContextChallengeContentMissing`, `MemoryRewardContentMissing`), `LevelContentMissingPanel` | `LevelPhasePlan.cs:168-180`, `LevelFlowController.cs:608-648`, `Assets/Scripts/UI/LevelContentMissingPanel.cs`, SALIN-223 |
| `FLOW-05` | Have the level's save committed before I see Victory | Level flow · `LevelFlowMachine`, `CampaignOutcomeCoordinator`, `VictoryScreenUI` | `LevelFlowMachine.cs`, `docs/system/04_Gameplay_Systems.md` §5.1.1, SALIN-174 |
| `FLOW-06` | Have malformed flow reports rejected instead of corrupting my run | Level flow · `LevelFlowMachine` | `LevelFlowMachine.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1 |
| `FLOW-07` | Have combat unable to hand me the level | Level flow · `LevelFlowMachine`, `WaveManager` | `LevelFlowMachine.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1 |
| `FLOW-11` | Get a beat to prepare before combat starts — the 2026-09-14 playtest reached the Ready screen in the live Level 1 route (Level Select → Level 1 → Ready → dialogue), which closes the audit's Master row 10 "MISSING". Two presentation defects are recorded: the panel is unstyled programmer-art with flat default buttons, and it does not cover the screen, so gameplay background and HP hearts bleed through before the level starts. Ownership remains open (U-5). | Level flow · `LevelReadyScreenController` | `Assets/Scripts/UI/LevelReadyScreenController.cs`, `progress/2026-09-14-level1-playtest.md` (Confirmed working; defects 3–4), SALIN-235 (closed obsolete) |
| `FLOW-13` | Not be able to draw during a phase that is not about drawing | Core · `GameManager`, `StrokeCapture` | `Assets/Scripts/Core/GameManager.cs` (`SuppressDrawingInput`, `AcceptsDrawingInput`), `docs/system/04_Gameplay_Systems.md` §9.4 |
| `FLOW-14` | Have the level's music start with the level | Level flow · `LevelFlowController`, `AudioManager` | `LevelFlowController.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §2 |
| `FLOW-15` | Have a level I abandon leave no trace on the next attempt | Core · `GameManager.AbortCurrentLevelAttempt`, `EventBus.OnLevelAttemptAborted` (9 subscribers) | `docs/system/03_Core_Systems.md` §8.2, SALIN-141 |
| `FLOW-16` | Be shown the level's stage art for its era — pass 2: the level configs still reference the legacy-named theme assets, but those assets resolve to the correct backgrounds: `EraTheme_Spanish → StageBackground_Ugat` (Levels 1–5), `EraTheme_Japanese → StageBackground_Ugnayan` (6–10), `EraTheme_American → StageBackground_Pamana` (11–15). `EnvironmentThemeSwapper` applies `level.eraTheme.stageBackground`. Only the asset *names* are stale. | Environment · `EnvironmentThemeSwapper`, `StageBackgroundBaker`, `StageBackgroundSO` | `Assets/Scripts/Gameplay/Environment/EnvironmentThemeSwapper.cs:34-43`, `Assets/ScriptableObjects/Themes/EraTheme_*.asset`, SALIN-218 |
| `FLOW-17` | Have the level tutorial run once and only once | Tutorial · `Level1OnboardingController`, `OnboardingPersistence`, `LevelTutorialProgress` | `Assets/Scripts/Gameplay/Tutorial/Onboarding/OnboardingPersistence.cs`, `Assets/Scripts/UI/LevelTutorialProgress.cs` |
| `FLOW-19` | Have the retired practice phase not waste my time — retired by ruling D-004 (SALIN-228 closed obsolete). The enum member remains for save/serialisation stability. | Level flow · `LevelFlowController.ExecutePhase` (no `RequiredPractice` case) | `LevelFlowController.cs:319-333`, `LevelPhase.cs`, SALIN-228 |
| `FLOW-20` | Have the level's phase not fight with the app's pause state | Core · `GameManager`, `LevelFlowMachine` | `docs/system/03_Core_Systems.md` §1.3, `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1 |

## 05 — Baybayin learning

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `LEARN-02` | Meet new characters gradually | Content · `BaybayinCharacterSO`, `LevelConfigSO.cumulativeSymbolPool`, `CampaignConfigValidator` | `Assets/Scripts/Data/BaybayinCharacterSO.cs`, `Assets/Scripts/Data/Validation/CampaignConfigValidator.cs`, SALIN-216, SALIN-217 |
| `LEARN-03` | Recognise a Baybayin character by sight | Learning · `SymbolLearningCardController`, `BaybayinCharacterSO.glyphOutlineSprite` | `Assets/Scripts/UI/HUD/SymbolLearningCardController.cs`, `Assets/Art/UI/GlyphOutlines/`, SALIN-157, SALIN-209 |
| `LEARN-04` | Hear a character's sound | Learning · `SymbolLearningCardController`, `AudioManager`, `SpokenValueResolver` | `SymbolLearningCardController.cs`, `Assets/Scripts/Data/Campaign/SpokenValueResolver.cs`, SALIN-157 |
| `LEARN-05` | Replay a character's sound | Learning · `SymbolLearningCardController` | `SymbolLearningCardController.cs`, SALIN-157 |
| `LEARN-08` | Be shown only the characters this level introduces | Content · all `Level*_Config.asset`, `SymbolLearningCardController` | SALIN-224, `docs/design/spec-rulings-2026-09.md` R3 |
| `LEARN-09` | Read a character with the reading this word needs | Learning · `SpokenValueResolver.ResolveLabel`, `SymbolLearningCardController`, `ActiveCluePresenter` | `Assets/Scripts/Data/Campaign/SpokenValueResolver.cs`, SALIN-221 |
| `LEARN-11` | See the words I am about to restore | Learning · `FocusWordPreviewController`, `FocusWordDefinition` | `Assets/Scripts/UI/HUD/FocusWordPreviewController.cs`, SALIN-138 |
| `LEARN-13` | See how a word breaks into syllables | Learning · `FocusWordDefinition.decomposition`, `FocusWordPreviewController` | `FocusWordDefinition.cs`, `FocusWordPreviewController.cs` |
| `PRAC-01` | Practise without pressure — but D-005 rules the Tracing Dojo deleted and free practice folded into the Codex. See U-4. | Tracing Dojo · `TracingDojo.unity`, `TracingDojoController` | `Assets/Scripts/UI/TracingDojo/TracingDojoController.cs`, SALIN-268 (To Do) |
| `PRAC-02` | Choose which character to practise | Tracing Dojo · `CharacterDropdown`, `CharacterListPopulator`, `CharacterListRow` | `Assets/Scripts/UI/TracingDojo/CharacterListPopulator.cs` |
| `PRAC-03` | Only be offered characters I have been taught | Tracing Dojo · `CharacterListPopulator`, `LearningStateSnapshot` | `docs/system/04_Gameplay_Systems.md` §12.2, SALIN-175 |
| `PRAC-04` | See a guide of the shape I am tracing | Tracing Dojo · `GhostStrokeRenderer` | `Assets/Scripts/UI/TracingDojo/GhostStrokeRenderer.cs` |
| `PRAC-05` | Get feedback on how close my trace was | Tracing Dojo · `FeedbackToast`, `TracingDojoEvidence` | `Assets/Scripts/UI/TracingDojo/FeedbackToast.cs`, `TracingDojoEvidence.cs` |
| `PRAC-06` | Have my practice count toward mastery | Learning evidence · `TracingDojoEvidence.Resolve`, `LearningEvidenceRecorder`, `ProgressManager.CommitPracticeSession` | `docs/system/04_Gameplay_Systems.md` §12.2, SALIN-175 |
| `PRAC-07` | Never have practice change my campaign progress | Learning evidence · `CampaignOutcomeCoordinator`, `CampaignOutcomeValidator` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7.1, SALIN-175 |
| `MAST-03` | Have "knowing it" require remembering it, not copying it | Learning · `LearningEvidenceRecorder`, `CampaignOutcomeCoordinator` | `docs/system/04_Gameplay_Systems.md` §12.1, SALIN-175 |
| `MAST-04` | Not be able to grind mastery in one sitting | Learning · `LearningEvidenceRecorder`, `MasteryEvaluator` | `docs/system/04_Gameplay_Systems.md` §12.1, `docs/capstone/GDD.md` §5.5 |
| `MAST-05` | Never lose mastery I have earned | Learning / persistence · `CampaignOutcomeCoordinator` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7, `docs/capstone/GDD.md` §5.5 |
| `MAST-07` | Have my learning saved with my progress, not separately | Persistence · `CampaignProgressOutcome.evidence`, `CampaignOutcomeCoordinator` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7.1, SALIN-175 |
| `MAST-08` | Have an abandoned attempt's learning discarded | Learning · `ProgressManager`, `LearningEvidenceRecorder` | `docs/system/03_Core_Systems.md` §2.8, SALIN-141 |
| `CODEX-01` | Browse the characters I have learned | Almanac · `AlmanacController`, `AlmanacCell`, `CharacterRegistrySO` | `Assets/Scripts/UI/Almanac/AlmanacController.cs` |
| `CODEX-03` | Not be spoiled by characters I have not met | Almanac · `AlmanacCell.ShouldBeInteractable` | `Assets/Scripts/UI/Almanac/AlmanacCell.cs` |
| `CODEX-04` | Open a character's full entry | Almanac · `AlmanacDetailScroll` | `Assets/Scripts/UI/Almanac/AlmanacDetailScroll.cs`, `AlmanacDetailScroll` reuse in level reveals |
| `CODEX-07` | Have a newly unlocked character appear in the Codex immediately — SFX clip assigned on the AudioManager prefab. | Almanac · `CharacterUnlockProgress`, `EventBus.OnCharacterUnlocked`, `AudioManager` (`_characterUnlockedClip`) | `Assets/Scripts/Core/CharacterUnlockProgress.cs`, `AudioManager.cs:65,535`, `docs/system/03_Core_Systems.md` §7.1 |
| `CODEX-08` | Have a reset clear the Codex too | Almanac · `CharacterUnlockProgress.ClearAllUnlocked` | `docs/system/03_Core_Systems.md` §7.1 |

## 06 — Drawing and recognition

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `DRAW-01` | Draw anywhere on the screen | Drawing · `DrawingCanvas`, `StrokeCapture` | `Assets/Scripts/Gameplay/Drawing/DrawingCanvas.cs`, ruling Q13 |
| `DRAW-02` | See my stroke appear under my finger immediately | Drawing · `StrokeCapture`, `DrawingCanvas.SetPoints` | `Assets/Scripts/Gameplay/Recognition/StrokeCapture.cs`, `docs/system/04_Gameplay_Systems.md` §4.3 |
| `DRAW-03` | See a smooth line, not a jagged polyline | Drawing · `StrokeGeometry.RebuildVisualCurve`, `RecognitionConfigSO` | `Assets/Scripts/Gameplay/Recognition/StrokeGeometry.cs`, `Assets/Scripts/Data/RecognitionConfigSO.cs` |
| `DRAW-04` | Submit a character by lifting my finger | Drawing · `StrokeCapture`, `RecognitionManager` | `StrokeCapture.cs`, `Assets/Scripts/Core/RecognitionManager.cs` |
| `DRAW-05` | Draw a character that needs more than one stroke | Drawing · `StrokeCapture`, `CapturedStroke`, `RecognitionConfigSO.multiStrokeWindowSeconds` | `RecognitionConfigSO.cs:18`, `docs/system/04_Gameplay_Systems.md` §4.3 |
| `DRAW-06` | Not have an accidental tap counted as an answer | Drawing · `StrokeValidation`, `RecognitionConfigSO` | `Assets/Scripts/Gameplay/Recognition/StrokeValidation.cs`, `RecognitionConfigSO.cs:32-35` |
| `DRAW-07` | Have my drawing matched against real Baybayin shapes | Recognition · `DollarPRecognizer`, `TemplateLoader`, `CapturedStroke` | `Assets/Scripts/Gameplay/Recognition/DollarPRecognizer.cs`, `TemplateLoader.cs` |
| `DRAW-08` | Have my drawing accepted when it is close enough | Recognition · `RecognitionManager`, `RecognitionConfigSO`, `LevelConfigSO.ResolveDrawingAccuracyThreshold` | `RecognitionConfigSO.cs:14`, `Assets/Scripts/Data/LevelConfigSO.cs:53-59,158` |
| `DRAW-09` | Not see a confidence score | Recognition · `RecognitionManager`, `FeedbackToast` | `docs/audit/BACKLOG.md` T59 ("hidden recognizer score" listed as code-is-better) |
| `DRAW-10` | Be told clearly when a drawing was rejected | Drawing / HUD · `EventBus.OnDrawingFailed`, `DrawingFeedback`, `DrawFeedbackPresenter` | `Assets/Scripts/UI/HUD/DrawingFeedback.cs`, `DrawFeedbackPresenter.cs` |
| `DRAW-11` | Be told *why* a drawing did not work | Combat feedback · `DrawFeedbackSignals`, `DrawFeedbackVocabulary`, `CombatResolver` | `Assets/Scripts/Gameplay/Combat/DrawFeedbackSignals.cs`, `Assets/Scripts/UI/HUD/DrawFeedbackVocabulary.cs` |
| `DRAW-13` | Have the same finger-lift not counted twice | Combat · `CombatResolver` echo guard | `Assets/Scripts/Gameplay/Combat/CombatResolver.cs:29-41,218-221`, SALIN-182, SALIN-135 |
| `DRAW-15` | Have the same character recognised the same way everywhere | Recognition · `RecognitionManager`, `EventBus.OnRecognitionResolved` | `Assets/Scripts/Core/RecognitionManager.cs`, `docs/system/03_Core_Systems.md` §4.3 |
| `DRAW-17` | Draw with the guide visible while I am still learning | Tutorial · `TraceHintPresenter`, `TutorialPathTrail`, `TutorialAssistAnimator` | `Assets/Scripts/UI/HUD/TraceHintPresenter.cs`, `Assets/Scripts/Gameplay/Tutorial/TutorialAssistAnimator.cs` |
| `DRAW-19` | Have my drawing ignored while the game is paused | Drawing · `StrokeCapture`, `GameManager` | `StrokeCapture.cs`, `docs/system/03_Core_Systems.md` §1.4 |
| `DRAW-20` | Have my part-drawn stroke thrown away if I leave the level | Drawing · `StrokeCapture` | `docs/system/03_Core_Systems.md` §8.2 |
| `DRAW-21` | Have my recognition attempts logged for the study | Analytics · `RecognitionLogger` | `Assets/Scripts/Analytics/RecognitionLogger.cs`, `TracingDojoController.cs` (disables logging on enable) |

## 07 — Combat and defense

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `CMB-01` | Face enemies in waves | Waves · `WaveManager`, `WaveDefinition` | `Assets/Scripts/Gameplay/Wave/WaveManager.cs`, `Assets/Scripts/Data/WaveDefinition.cs`, `docs/system/04_Gameplay_Systems.md` §6 |
| `CMB-02` | No wave indicator — the "Wave X" label was removed from every level by design decision; `OnWaveStarted` still drives wave progression | HUD · (was `WaveDisplay`, removed) | — |
| `CMB-03` | Only face enemies carrying symbols I have been taught | Waves / content · `WaveDefinition`, `LevelConfigSO`, `CampaignConfigValidator` | SALIN-216, `Assets/Scripts/Data/Validation/CampaignConfigValidator.cs` |
| `CMB-04` | Only face enemy types this level uses | Waves · `WaveDefinition`, `LevelConfigSO` | `WaveDefinition.cs`, `LevelConfigSO.cs:98` |
| `CMB-06` | Be given the symbol I need, not left waiting for it | Spawning · `SpawnAssignmentDirector`, `SpawnAssignmentCoordinator`, `SpawnAssignmentPolicy` | `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentDirector.cs`, `docs/design/spawn-assignment-system.md` |
| `CMB-07` | Not be asked for a symbol before its lesson has fired | Spawning · `SpawnSlotGate`, `SpawnGateRegistry` | `Assets/Scripts/Gameplay/Wave/SpawnAssignment/SpawnAssignmentPolicy.cs`, `SpawnGateRegistry.cs` |
| `CMB-10` | Not have the cue jump away while I am drawing | Combat · `ActiveClueDirector` | `ActiveClueDirector.cs` header |
| `CMB-12` | Still be able to play with the sound off — and currently load-bearing rather than belt-and-braces, because many pronunciation clips were unassigned at last audit. | Combat · `ClueChannels`, `LevelConfigSO.audioVisualFallback` | `docs/system/04_Gameplay_Systems.md` §13, `Level1AssetReadinessTests.ClueChannels_StayReadableWithoutPronunciationAudio` |
| `CMB-14` | Have a correct drawing count against any enemy carrying that symbol | Combat · `DrawTargetResolver`, `CombatResolver.ResolveActiveClueDraw` | `Assets/Scripts/Gameplay/Combat/DrawTargetResolver.cs`, SALIN-135 |
| `CMB-15` | Have the nearest threat die first | Combat · `DrawTargetResolver`, `ActiveEnemyTracker` | `DrawTargetResolver.cs`, `Assets/Scripts/Gameplay/Enemy/ActiveEnemyTracker.cs` |
| `CMB-16` | Clear a crowd with one drawing when the level allows it | Combat · `CombatResolver` AOE path, `EventBus.OnAOETriggered` | `CombatResolver.cs:140,187-193`, `LevelConfigSO.cs:109` |
| `CMB-17` | See a mass clear acknowledged | HUD / feedback · `MassClearBadge`, `ChainAttackHitVfxController` | `Assets/Scripts/UI/HUD/MassClearBadge.cs`, `Assets/Scripts/Feedback/ChainAttackHitVfxController.cs` |
| `CMB-18` | Not be able to hit an enemy that is not a legal target | Combat · `ActiveClueDirector.IsClueTargetable`, `DrawTargetResolver` | `DrawTargetResolver.cs` header, `CombatResolver.cs:256-260` |
| `CMB-21` | See Juan at a readable size | Protagonist · `ProtagonistManager` (`MinVisibleWorldHeight` clamp) | `ProtagonistManager.cs` |
| `CMB-22` | Defend a shrine that can be damaged | Base · `PlayerBase`, `EnemyMover.OnTriggerEnter2D` | `Assets/Scripts/Gameplay/Base/PlayerBase.cs`, `Assets/Scripts/Gameplay/Enemy/EnemyMover.cs` |
| `CMB-23` | Have three hearts | Base · `HeartSystem`, `DefenseRules` | `Assets/Scripts/Gameplay/Base/HeartSystem.cs`, `Assets/Scripts/Data/Campaign/FocusWordDefinition.cs:21-24` |
| `CMB-24` | Lose two hearts when two enemies land together — ruled Q11, no code change required; documented in Level 4 wave authoring. | Base · `HeartSystem` | ruling Q11 |
| `CMB-27` | Lose the level when the shrine falls | Base / core · `HeartSystem`, `GameManager.HandleGameOver`, `DefeatScreenUI` | `docs/system/02_Architecture_and_Runtime_Flow.md` §5.2 |
| `CMB-28` | Win by clearing the level's combat | Waves / flow · `WaveManager`, `LevelFlowMachine` | `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1 |
| `CMB-30` | Not be punished for drawing a symbol that is already restored | Combat restoration · `TargetTextSlotMap`, `DrawFeedbackVocabulary` | `TargetTextSlotMap.cs`, `Assets/Scripts/UI/HUD/DrawFeedbackVocabulary.cs` |

## 08 — Enemies

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `ENM-01` | Face enemies that walk toward the shrine | Enemies · `EnemyMover` | `Assets/Scripts/Gameplay/Enemy/EnemyMover.cs`, `docs/system/04_Gameplay_Systems.md` §2 |
| `ENM-02` | Face enemies that move freely rather than in lanes — lanes were cut by ruling Q15 / OQ-5. | Enemies · `WaveSpawner`, `EnemyMover` | `docs/audit/STATUS-2026-09-13.md` Master row 14 |
| `ENM-04` | See the badge react to what I draw | Enemies · `EnemyGlyphBadge` | `docs/system/04_Gameplay_Systems.md` §1.1.1 |
| `ENM-05` | Have the badge stay readable even mid-animation | Enemies · `EnemyGlyphBadge` | `docs/system/04_Gameplay_Systems.md` §1.1.1 |
| `ENM-06` | Defeat an enemy by drawing its symbol | Enemies · `Enemy.Defeat`, `EnemyPool` | `Assets/Scripts/Gameplay/Enemy/Enemy.cs`, `docs/system/04_Gameplay_Systems.md` §1.2 |
| `ENM-08` | Have to draw twice for an armoured enemy — Uhaw and Gapos and Kadena are HP 2; Walang-Awa and Yapos ng Dilim are HP 3. | Enemies · `Enemy.TakeDamage`, `EnemyHurtFeedback` | `Assets/Scripts/Gameplay/Enemy/EnemyHurtFeedback.cs`, `Assets/ScriptableObjects/Enemies/` |
| `ENM-09` | See an enemy react when I hit it | Enemies / feedback · `EnemyHurtFeedback`, `SingleAttackHitVfxController` | `EnemyHurtFeedback.cs`, `Assets/Scripts/Feedback/SingleAttackHitVfxController.cs` |
| `ENM-11` | Abo ng Simula (A) buries the first slot in ash | Enemies · `ashesFirstSlot` → `AshFirstSlotController`, `AshGustController` | `Assets/Scripts/Gameplay/Enemy/AshFirstSlotController.cs`, SALIN-284 |
| `ENM-12` | Iligaw (E/I) spawns a false copy of itself | Enemies · `spawnsMirrorDecoy` → `MirrorDecoyController`, `ActiveClueDirector.IsClueTargetable` | `Assets/Scripts/Gameplay/Enemy/MirrorDecoyController.cs`, `CombatResolver.cs:256-260` |
| `ENM-14` | Mantsa (MA) stains nearby glyphs | Enemies · `stainsNearbyGlyphs` → `KempeiScrambleController`, `GlyphStainCycle` | `Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs`, `GlyphStainCycle.cs` |
| `ENM-16` | Takip (TA) covers its own glyph | Enemies · `coversOwnGlyph` → `GlyphCoverController` | `Assets/Scripts/Gameplay/Enemy/GlyphCoverController.cs` |
| `ENM-19` | Hati (HA) splits into minions when defeated | Enemies · `splitsOnDefeat` → `HatiSplitController`, `EnemyData_HatiMinion` | `Assets/Scripts/Gameplay/Enemy/HatiSplitController.cs`, `EnemyData_HatiMinion.asset` |
| `ENM-20` | Labo (LA) fades in and out | Enemies · `isPhaser` → `PhaserEnemy` | `Assets/Scripts/Gameplay/Enemy/PhaserEnemy.cs`, `EnemyDataSO.cs:44-67` |
| `ENM-31` | Have each enemy introduced only once | Tutorial · `EnemyIntroductionProgress`, `EnemyDiscoveryProgress` | `Assets/Scripts/Data/EnemyIntroductionProgress.cs`, `Assets/Scripts/Gameplay/Enemy/EnemyDiscoveryProgress.cs` |

## 09 — Boss encounter

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `BOSS-03` | Fight the boss in phases | Boss · `BossController` state machine | `Assets/Scripts/Gameplay/Boss/BossController.cs`, `docs/system/04_Gameplay_Systems.md` §8.2, §8.6 |
| `BOSS-05` | Only be able to hurt the boss when it is open | Boss · `BossController.TryRouteDraw`, `BossStateVisuals`, `BossEnemy` | `docs/system/04_Gameplay_Systems.md` §8.3, §8.4, `Assets/Scripts/Gameplay/Enemy/BossEnemy.cs` |
| `BOSS-06` | See which glyph the boss demands next, and how many are left | Boss · `BossGlyphVisibilityBinder`, `BossDrawCounterUI`, `EnemyGlyphBadge` | `Assets/Scripts/UI/BossDrawCounterUI.cs`, `Assets/Scripts/Gameplay/Boss/BossGlyphVisibilityBinder.cs` |
| `BOSS-07` | See how long I have in the vulnerable window | Boss · `BossVulnerabilityTimerBar` | `Assets/Scripts/UI/BossVulnerabilityTimerBar.cs` |
| `BOSS-08` | See the boss's remaining health | Boss · `BossHealthBar` | `Assets/Scripts/UI/BossHealthBar.cs` |
| `BOSS-09` | Not lose progress when I miss a vulnerable window | Boss · `BossController` | `docs/system/04_Gameplay_Systems.md` §8.3, §8.4 |
| `BOSS-14` | See the boss move differently per phase | Boss · `PhaseBasedMovement` | `Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs` |
| `BOSS-15` | See the boss react to being hit | Boss · `BossDamageFeedback`, `BossStateVisuals` | `Assets/Scripts/Gameplay/Boss/BossDamageFeedback.cs` |
| `BOSS-16` | See the boss die and the level end properly | Boss · `BossController.RunOutro` | `docs/system/04_Gameplay_Systems.md` §8.5 |

## 10 — Restoration and challenges

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `REST-01` | See the text I am restoring while I fight (Levels 1–5) | Restoration · `ActiveCluePresenter`, `TargetTextSlotMap` | `Assets/Scripts/UI/HUD/ActiveCluePresenter.cs`, `Assets/Scripts/Gameplay/Combat/TargetTextSlotMap.cs` |
| `REST-04` | Win the encounter by completing the text (Levels 1–5) | Restoration · `LevelFlowController.ExecuteCombatRestoration`, `InstantWinPresenter`, `InstantWinCopy` | `LevelFlowController.cs:696-730`, `Assets/Scripts/Gameplay/Flow/InstantWinPresenter.cs` |
| `REST-05` | Have a wave-only segment not ask me for a second board | Restoration · `LevelFlowController.ExecuteCombatRestoration` | `LevelFlowController.cs:718-727` |
| `REST-06` | Place words into a sentence | Challenges · `ChallengeSession`, `ChallengeModeUI`, `ChallengeSequenceSO` | `Assets/Scripts/Gameplay/ChallengeSession.cs`, `Assets/Scripts/Data/ChallengeSequenceSO.cs` |
| `REST-07` | Match a word to what it means | Challenges · `ChallengeMode.WordPlacement` | `ChallengeSequenceSO.cs`, `Challenge_Ugat02_Context.asset` |
| `REST-09` | Trace a symbol as a guided step inside a challenge | Challenges · `ChallengeMode.GuidedTracing`, `Level1TutorialStepSO` | `ChallengeSequenceSO.cs:46`, `Assets/ScriptableObjects/Tutorial/Level1TutorialStep_*.asset` |
| `REST-10` | Recall a word from memory under a short timer | Challenges · `ChallengeMode.TimedMemory`, `ChallengeSession` | `ChallengeSequenceSO.cs:47,50` |
| `REST-13` | Be given a gentle first attempt at restoration — Levels 1 and 2 are tier 1 and 2. | Challenges · `ChallengeTierPolicy.ForTier`, `ChallengeSessionState.SupportiveRetry` | `Assets/Scripts/Gameplay/ChallengeTierPolicy.cs`, SALIN-222 |
| `REST-14` | Pay for mistakes in later levels — Levels 3, 4, 5 are tiers 3, 4, 5. | Challenges · `ChallengeTierPolicy`, `ChallengeSessionState.Penalty`, `CheckpointReset` | `ChallengeTierPolicy.cs`, SALIN-222 |
| `REST-15` | Have each level's tier match its place in the era — verified: L1–L5 = 1–5, L6–L10 = 1–5, L11–L15 = 1–5. | Content · all 15 `Level*_Config.asset` | `Assets/ScriptableObjects/Levels/`, SALIN-222 |
| `REST-16` | Ask for help when I am stuck | Challenges · `ChallengeSession.RequestHint`, `ChallengeModeUI` | `ChallengeSession.cs`, `ChallengeModeUI.cs:105-112` |
| `REST-17` | Know what a hint costs before I spend it | Challenges · `HintModal`, `HintModalCopy` | `Assets/Scripts/UI/HUD/HintModal.cs`, SALIN-231 |
| `REST-18` | Not have a hint solve the level for me — the pre-SALIN-231 behaviour (apply the answer directly, free and unlimited) is replaced. | Challenges · `ChallengeSession`, `HintModal` | `HintModal.cs` header, SALIN-231 |
| `REST-19` | Be told when I have run out of hints | Challenges · `ChallengeModeUI`, `ChallengeTierPolicy.emergencyHintEnabled` | `ChallengeModeUI.cs:315-344`, `ChallengeTierPolicy.cs:24-28` |
| `REST-20` | See the hint penalty on my results | Results · `LevelResultsCalculator`, `ChallengeSession.EmergencyHintScorePenalty` | `docs/design/scoring-and-stars.md`, SALIN-181, SALIN-202 |
| `REST-22` | Have Level 1 end on MA, not NA | Content · `Level1_Config.asset` | ruling Q3, SALIN-214, `docs/technical/TW-SPK-004-educational-content-matrix.md` |
| `REST-23` | Have the campaign end on YA | Content identity · `ContentIdentity`, `CampaignConfigValidator` | ruling Q1 + qualifier, SALIN-217 |
| `REST-27` | Have my challenge answers recorded as learning | Learning · `ChallengeSession`, `LearningEvidenceRecorder` | `docs/design/scoring-and-stars.md`, `ChallengeSequenceSO.cs:54,66` |
| `REST-28` | Have the challenge pause with the game | Challenges · `ChallengeSessionState` | `ChallengeSession.cs:1-20` |
| `REST-29` | Have the game not ask me for content that was never written — currently active on Levels 6, 7, 8, 10 and 13, which have no challenge sequence. | Level flow · `LevelPhasePlan.ContextChallengeContentMissing` | `LevelPhasePlan.cs:168-180`, SALIN-223 |
| `REST-30` | See the restoration rail below the play field instead of drawn on the shrine fence | HUD · `ActiveCluePresenter` (rail band), `AspectLockedCamera` (`SetBottomBandPixels`) | `Assets/Scripts/UI/HUD/ActiveCluePresenter.cs`, `Assets/Scripts/Gameplay/Camera/AspectLockedCamera.cs`, `c3cf9612`, `d2b10574` |
| `REST-31` | Read every restoration slot by its romanised syllable, earned or not | HUD · `ActiveCluePresenter` (per-slot labels, plated label row) | `ActiveCluePresenter.cs`, `8debc76a`, `97f4bf7b` |
| `REST-32` | See a defeated enemy's glyph fly into its box in the finished Almanac art | HUD · `ActiveCluePresenter` (slot flight, `almanacSprite`) | `ActiveCluePresenter.cs`, `862262c6` |

## 11 — Results, rewards and progression

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `WAVE-01` | Have the fight acknowledged before the puzzle starts | Flow · `WaveClearedScreenUI`, `WaveClearedCopy` | `Assets/Scripts/UI/WaveClearedScreenUI.cs`, SALIN-232 |
| `WAVE-03` | Not be stopped by a screen that has nothing to say | Flow · `WaveClearedScreenUI.Present` contract | `WaveClearedScreenUI.cs` header ("CONTRACT") |
| `RES-01` | See a results screen when I finish a level | Results · `VictoryScreenUI`, `LevelResultsCopy` | `Assets/Scripts/UI/VictoryScreenUI.cs`, SALIN-234 |
| `RES-02` | Be rated out of three stars | Results · `LevelResultsCalculator` | `Assets/Scripts/Data/Learning/LevelResultsCalculator.cs`, `docs/design/scoring-and-stars.md` |
| `RES-03` | Be rated on my language accuracy, not just on surviving | Results · `LevelResultsCalculator` | `docs/design/scoring-and-stars.md` |
| `RES-04` | Have my drawing accuracy count toward my rating — pass 2 corrected: the pass-1 wording said the accuracy was "shown"; it is computed and hidden by ruling. | Results · `LevelResultsCalculator`, `LevelResultsCopy` | `Assets/Scripts/UI/LevelResultsCopy.cs:22-31`, `docs/design/scoring-and-stars.md` |
| `RES-05` | Have my understanding of the words count toward my rating — pass 2 corrected as for RES-04. | Results · `LevelResultsCalculator`, `LevelResultsCopy` | `LevelResultsCopy.cs:22-31`, `docs/design/scoring-and-stars.md` |
| `RES-06` | See my hearts and hints on the results — pass 2: `LevelResultsCopy` exposes exactly `Stars`, `StarCount`, `Score`, `Hearts`, `Hints`, `HintPenalty`, `NewSymbols`, `Restored`. "Best combo" is retired with combos; the improvement tip and View Details are RES-07 (Missing). | Results · `VictoryScreenUI`, `LevelResultsCopy`, `LevelResultsCalculator` | `Assets/Scripts/UI/LevelResultsCopy.cs:106-136`, SALIN-234 |
| `RES-08` | Have the results and my saved record agree | Results / persistence · `LevelResultsCalculator`, `CampaignProgressOutcome` | `docs/design/scoring-and-stars.md` |
| `RES-09` | Continue to the next level from the results | Results · `VictoryScreenUI`, `LevelFlowController` | `VictoryScreenUI.cs`, `docs/system/04_Gameplay_Systems.md` §5.1.1 |
| `RES-10` | Replay a level without losing what I earned | Results / persistence · `VictoryScreenUI`, `CampaignOutcomeCoordinator` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7, SALIN-174 |
| `RES-11` | Return to the map from the results | Results · `VictoryScreenUI` | `VictoryScreenUI.cs` |
| `MEM-01` | Claim the memory I restored — implemented as self-building overlays; memory-card art is recorded as missing. | Rewards · `MemoryClaimPanel`, `MemoryCardUI`, `MemoryCardCopy` | `Assets/Scripts/UI/MemoryClaimPanel.cs`, `Assets/Scripts/UI/MemoryCardUI.cs`, SALIN-240 |
| `MEM-02` | Browse every memory I have restored | Rewards · `MemoryArchiveController`, `MemoryArchiveModel` | `Assets/Scripts/UI/MemoryArchiveController.cs`, `Assets/Scripts/Data/Learning/MemoryArchiveModel.cs` |
| `MEM-03` | See which memories I have yet to earn | Rewards · `MemoryArchiveController` | `MemoryArchiveController.cs` header, SALIN-240, SALIN-258 |
| `MEM-04` | See the memories of an era together when the era ends | Rewards · `EraCompletionScreenUI` | `Assets/Scripts/UI/EraCompletionScreenUI.cs`, SALIN-253 |
| `MEM-05` | Not be able to claim the same memory twice | Rewards / persistence · `LevelRewardResolver`, `CampaignOutcomeCoordinator` | `Assets/Scripts/Data/Learning/LevelRewardResolver.cs`, `docs/design/scoring-and-stars.md` "Rewards" |
| `SAVE-01` | Have my progress saved automatically | Persistence · `LevelFlowController`, `CampaignOutcomeCoordinator`, `ProgressManager` | `docs/system/04_Gameplay_Systems.md` §5.1.1 |
| `SAVE-02` | Never have a half-written save | Persistence · `CampaignSaveCommitter`, `CampaignOutcomeCoordinator` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7 |
| `SAVE-03` | Have my progress only ever go forward | Persistence · `CampaignOutcomeCoordinator` | `docs/system/04_Gameplay_Systems.md` §5.1.1 |
| `SAVE-04` | Be told when my level could not be saved | Persistence · `CampaignOutcomeSaveFailurePanel` | `Assets/Scripts/UI/CampaignOutcomeSaveFailurePanel.cs`, `docs/system/04_Gameplay_Systems.md` §5.1.1 |
| `SAVE-05` | Keep a recoverable completion when I leave after a save failure | Persistence · `CampaignOutcomeJournal`, `SaveManager.RetryPendingOutcome` | `docs/capstone/GDD.md` §5.4, SALIN-174 |
| `SAVE-07` | Unlock the next level by completing this one | Persistence · `CampaignOutcomeCoordinator`, `LevelLockResolver` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7 |
| `SAVE-08` | Unlock a symbol by being taught it | Persistence · `LevelRewardResolver` | `docs/design/scoring-and-stars.md` "Rewards" |
| `SAVE-09` | Have my save survive an app update | Persistence · `CampaignSaveMigrator`, `CampaignSaveDocument.CurrentSaveSchemaVersion` | `Assets/Scripts/Data/Persistence/CampaignSaveMigrator.cs`, SALIN-227 |
| `SAVE-10` | Have my settings survive a journey reset | Persistence · `AudioManager`, `CampaignOutcomeCoordinator` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7 |
| `SAVE-11` | Not have an abandoned attempt recorded | Persistence · `ProgressManager` | `docs/system/03_Core_Systems.md` §8.2 |

## 12 — Pause, failure, retry and edge cases

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `FAIL-01` | Pause the game | Pause · `PauseMenuUI`, `GameManager.PauseGame` | `Assets/Scripts/UI/PauseMenuUI.cs`, `docs/system/03_Core_Systems.md` §1.4 |
| `FAIL-02` | Resume exactly where I stopped | Pause · `GameManager.ResumeGame` | `docs/system/03_Core_Systems.md` §1.4 |
| `FAIL-03` | Not have a dialogue steal my pause | Pause · `GameManager` | `docs/system/03_Core_Systems.md` §8.1, SALIN-141 |
| `FAIL-04` | Change settings while paused | Pause · `PauseMenuUI`, `SettingsPanel` | `PauseMenuUI.cs` |
| `FAIL-06` | Restart the level from the pause menu — pass 2: `_restartButton` is assigned (`fileID: 1217944618`) on the pause menu in `Gameplay.unity` on `dev`; the audit's "absent" predates PR #127. | Pause · `PauseMenuUI`, `GameManager.AbortCurrentLevelAttempt` | `PauseMenuUI.cs:30-31`, `Assets/_Scenes/Gameplay.unity:1112`, SALIN-141 |
| `FAIL-08` | Quit to the menu from the pause menu | Pause · `PauseMenuUI`, `AbortCurrentLevelAttempt` | `docs/system/03_Core_Systems.md` §8.2 |
| `FAIL-11` | Be shown a defeat screen when I lose | Defeat · `DefeatScreenUI`, `GameManager.HandleGameOver` | `Assets/Scripts/UI/DefeatScreenUI.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §5.1, SALIN-58 |
| `FAIL-13` | Retry immediately after losing | Defeat · `DefeatScreenUI` | `DefeatScreenUI.cs` |
| `FAIL-17` | Keep the rewards of levels I already finished after a defeat | Defeat / persistence · `LevelFlowMachine` (`Defeated` terminal), `ProgressManager` | `docs/system/02_Architecture_and_Runtime_Flow.md` §6.1 |
| `FAIL-18` | Have the HUD get out of the way when I lose | Defeat · `DefeatScreenUI._hudRoot` | `DefeatScreenUI.cs` |
| `FAIL-19` | Never be left with the game frozen after a scene change | Core · `SceneLoader.LoadRoutine` | `docs/system/03_Core_Systems.md` §2.5, §2.6 |
| `FAIL-20` | Never have two scene loads fight each other | Core · `SceneLoader` | `docs/system/03_Core_Systems.md` §2.4 |
| `FAIL-21` | Never see an enemy from the previous level | Core · `SceneLoader.CleanupGameplayRun`, `EnemyPool.ReturnAllCheckedOut` | `docs/system/03_Core_Systems.md` §2.7 |
| `FAIL-22` | Never get stuck unable to draw | Core · `GameManager` | `docs/system/04_Gameplay_Systems.md` §9.4 |
| `FAIL-24` | Not be able to finish a level that has no content — intentionally active on Levels 6–15. | Level flow · `LevelPhasePlan`, `LevelContentMissingPanel` | `LevelPhasePlan.cs:168-180`, SALIN-223 |
| `FAIL-26` | Have a duplicated manager not break the game | Core · `Singleton<T>` | `Assets/Scripts/Utilities/Singleton.cs`, `docs/system/03_Core_Systems.md` §5 |
| `FAIL-27` | Not have debug logging slow down the shipped game | Core · `DebugLogger` | `Assets/Scripts/Utilities/DebugLogger.cs`, `docs/system/03_Core_Systems.md` §6 |
| `FAIL-28` | Not be able to reach developer tools | Debug · `DevBuildGuard`, `SandboxMode`, `#if UNITY_EDITOR \|\| SALINLAHI_SANDBOX` | `Assets/Scripts/Debug/DevBuildGuard.cs`, `docs/sandbox-mode.md` |

## 13 — HUD, audio and feedback

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `HUD-01` | Have the HUD stay out of my way | HUD · `HUD`, `PlayAreaContainer` | `Assets/Scripts/UI/HUD.cs`, `docs/capstone/GDD.md` §5.2 |
| `HUD-02` | See how many hearts I have left | HUD · `HeartDisplay` | `Assets/Scripts/UI/HUD/HeartDisplay.cs` |
| `HUD-03` | No wave indicator — same removal as `CMB-02`; the wave label is gone on levels 1–15 | HUD · (was `WaveDisplay`, removed) | — |
| `HUD-04` | See the symbol I am being asked for (Levels 1–5) | HUD · `ActiveCluePresenter` | `Assets/Scripts/UI/HUD/ActiveCluePresenter.cs` |
| `HUD-05` | See the words I am restoring on the HUD (Levels 1–5) | HUD · `ActiveCluePresenter`, `FocusWordPreviewController` | `ActiveCluePresenter.cs` |
| `HUD-06` | Reach the pause button easily | HUD · `HUD`, `SafeAreaHandler` | `Assets/Scripts/UI/SafeAreaHandler.cs`, `docs/capstone/GDD.md` §5.2 |
| `HUD-07` | See my drawing feedback in words as well as colour | HUD · `DrawFeedbackPresenter`, `DrawFeedbackVocabulary`, `DrawingFeedbackVocabulary` | `Assets/Scripts/UI/HUD/DrawFeedbackPresenter.cs`, `DrawFeedbackVocabulary.cs` |
| `HUD-08` | See a mass clear announced | HUD · `MassClearBadge` | `Assets/Scripts/UI/HUD/MassClearBadge.cs` |
| `HUD-09` | Not see a combo counter that no longer exists — removed by SALIN-225. Documentation cleanup and a manual HUD check are still recorded as outstanding. | HUD · `HUD` | SALIN-225, `docs/audit/STATUS-2026-09-13.md` Master row 27 |
| `HUD-10` | Have the HUD anchored to the play area on any device | Layout · `PlayAreaContainer`, `AspectLockedCamera` | `docs/system/06_UI_UX_and_Player_Flow.md` §4.2 |
| `AUD-02` | Hear the shrine being hit — pass 2 corrected: doc 03 §3.5's "STUB" note is stale. Two clips are assigned on the AudioManager prefab with volume 0.92–1.0 and pitch 0.96–1.04 variation. | Audio · `AudioManager.PlayBaseHitSound`, `_baseHitClips` | `Assets/Scripts/Core/AudioManager.cs:24,84-87,206,597`, `[Manager] AudioManager.prefab:55-57` |
| `AUD-03` | Hear level music | Audio · `AudioManager.PlayBGM` | `AudioManager.cs`, `docs/system/03_Core_Systems.md` §3.4 |
| `AUD-04` | Have music change smoothly | Audio · `AudioManager` | `docs/system/03_Core_Systems.md` §3.4 |
| `AUD-05` | Have one sound not drown out another | Audio · `AudioManager`, `BossAudioBankSO` | `docs/system/04_Gameplay_Systems.md` §8.7 |
| `AUD-06` | Not hear the same variant twice in a row — the same policy is not applied to general enemy SFX. | Audio · `BossAudio` no-repeat picker | `docs/system/04_Gameplay_Systems.md` §8.7 |
| `AUD-07` | Not hear pronunciation clips stack on top of each other | Audio · `SymbolLearningCardController`, `AudioManager` | `Assets/Scripts/UI/HUD/SymbolLearningCardController.cs` |
| `AUD-08` | Have the game remember my volume settings | Audio · `AudioManager`, `SettingsPanel` | `docs/system/02_Architecture_and_Runtime_Flow.md` §7 |
| `AUD-09` | Hear a correct drawing land — clip assigned on the prefab. | Audio · `AudioManager.PlayCorrectGlyphSfx`, `_correctGlyphClip` | `AudioManager.cs:30-34,208,468`, `[Manager] AudioManager.prefab:58` |
| `AUD-10` | Hear a wrong drawing rejected — clip assigned on the prefab. | Audio · `AudioManager.PlayWrongGlyphSfx`, `_wrongGlyphClip` | `AudioManager.cs:38-40,209,480`, `[Manager] AudioManager.prefab:60` |
| `AUD-11` | Hear each enemy fall without a wall of noise — clips assigned on the prefab. | Audio · `AudioManager.PlayEnemyDefeatedSfx`, `PlayChainLightningSfx` | `AudioManager.cs:20-21,45-57,207,212,501,620`, `[Manager] AudioManager.prefab:51-52,62` |
| `AUD-12` | Hear victory and defeat stings — both clips assigned; ducking enabled on the prefab. | Audio · `AudioManager.PlayVictorySting`, `PlayDefeatSting` | `AudioManager.cs:69-76,210-211,554-559`, `[Manager] AudioManager.prefab:72-75` |
| `AUD-13` | Hear sounds land on the tap, not after it | Audio · `AudioManager.PrepareClipForImmediateAttack` | `AudioManager.cs:90-91,993-999` |
| `FX-01` | See my drawing rejected unmistakably | Feedback · `DrawingFeedback`, `BorderPulse` | `Assets/Scripts/UI/HUD/DrawingFeedback.cs`, `Assets/Scripts/UI/BorderPulse.cs` |
| `FX-02` | See my attack connect | Feedback · `SingleAttackHitVfxController`, `SingleAttackHitSpriteVfx`, `ChainAttackHitVfxController` | `Assets/Scripts/Feedback/SingleAttackHitVfxController.cs`, `ChainAttackHitVfxController.cs` |
| `FX-03` | Feel a hit on the shrine | Feedback · `CameraShakeController`, `CameraShakeData`, `DamageEdgeFlashController`, `EdgeGradient` | `Assets/Scripts/Feedback/CameraShakeController.cs`, `DamageEdgeFlashController.cs` |
| `FX-05` | Play the same game on any phone or tablet | Layout · `AspectLockedCamera` | `Assets/Scripts/Gameplay/Camera/AspectLockedCamera.cs`, `docs/system/04_Gameplay_Systems.md` §10 |
| `FX-06` | Never see the base fence not reach the edges | Layout · `BaseZoneScaler` | `Assets/Scripts/Gameplay/Environment/BaseZoneScaler.cs` |
| `FX-07` | Have things drawn in the right order | Rendering · `RenderOrder` | `Assets/Scripts/Gameplay/Rendering/RenderOrder.cs` |
| `FX-08` | Have the tutorial point at things clearly | Tutorial feedback · `TutorialSpotlightOverlay`, `SpotlightOverlayGraphic`, `TutorialPathTrail` | `Assets/Scripts/UI/TutorialSpotlightOverlay.cs`, `TutorialPathTrail.cs` |
| `FX-10` | Not have UI cut off by a notch or a rounded corner | UI · `SafeAreaHandler` | `Assets/Scripts/UI/SafeAreaHandler.cs` |

## 14 — Accessibility and settings

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `ACC-01` | Set the overall volume | Settings · `SettingsPanel`, `AudioManager` | `Assets/Scripts/UI/SettingsPanel.cs:10,15` |
| `ACC-02` | Set music and effects separately | Settings · `SettingsPanel`, `AudioManager` | `SettingsPanel.cs:11-12,16-17` |
| `ACC-16` | Draw anywhere rather than in a fixed pad — ruling Q13 made the "left-handed trace pad" requirement N/A. | Drawing · `DrawingCanvas` | ruling Q13, `DrawingCanvas.cs` |
| `ACC-17` | Not need precise aim to play | Combat · `DrawTargetResolver` | `docs/capstone/GDD.md` §5.5, `DrawTargetResolver.cs` |
| `ACC-18` | Have two channels of feedback for every outcome — pass 2: the audio channel is now complete for every outcome (AUD-02, AUD-09–AUD-12) and pronunciation clips exist for 17 of 18 identities (LEARN-06). Audible playback on device is still unverified. | Feedback · `AudioManager`, `DrawFeedbackPresenter` | `docs/capstone/GDD.md` §5.5, `Assets/Scripts/Core/AudioManager.cs:206-212` |

## 15 — Levels and content coverage

| ID | Implemented behaviour | System | Evidence |
|---|---|---|---|
| `LVL-02` | Play Ugat Level 2 end to end — statically completable. The on-screen Level 2 onboarding replacement and the mass-clear check are recorded as manually unverified. | Level 2 · `Level2_Config.asset`, `Challenge_Ugat02_Context.asset` | `Level2_Config.asset`, SALIN-241, SALIN-224 |
| `LVL-16` | Have every level's roster match what I have been taught | Content validation · `CampaignConfigValidator` | SALIN-216 |
| `LVL-17` | Have the game refuse to boot on a broken campaign identity | Content validation · `CampaignConfigValidator`, `CampaignSaveService` | SALIN-215, ruling Q12 + R1 split |

---

**238 implemented behaviours.**
