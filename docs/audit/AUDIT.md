# Salinlahi Code vs. Spec Audit

**Branch:** `dev` @ `cb41a966` (pulled 2026-09-11, already up to date)
**Spec:** `~/Downloads/SALINLAHI_Complete_User_Flow.xlsx` — all 10 sheets converted to text and read in full: Game Overview, Core Mechanics, Era 1 — Ugat, Era 2 — Ugnayan, Era 3 — Pamana, Character Mastery, Completion Rules, User Flow, Level Flow Details, Button and UI Rules.
**Method:** Read-only. Every C# file cited below was opened and read. ScriptableObject assets were parsed from their YAML with a scratchpad script (no repo writes). Scene wiring was checked by matching script GUIDs inside the `.unity` files. Nothing was run in Unity; nothing was edited.
**Status vocabulary:** DONE / PARTIAL / MISSING / DEVIATES / NOT-BUILT-YET. NOT-BUILT-YET is reserved for content the team has clearly not started (Levels 6 to 15 narrative, prologue, ending). DEVIATES means built but not matching the spec.

---

## 0. Top-line findings

1. **The revised campaign save system is dormant in the shipped scene.** `SaveManager._campaign` is `{fileID: 0}` in both `Assets/_Scenes/Bootstrap.unity:423` and `Assets/Prefabs/Managers/[Manager] SaveManager.prefab:47`, and no scene, prefab, or asset references `CampaignConfig_RevisedV1.asset`. `SaveManager.Initialize` therefore takes the `Legacy` branch (`Assets/Scripts/Core/SaveManager.cs:32-43`). Everything gated on `RevisedReady` never runs at runtime: the atomic outcome save, learning-evidence mastery, accuracy-based stars, reward/memory persistence, Reset Journey, tutorial-progress persistence, and the Tracing Dojo taught-symbol filter. Progress is PlayerPrefs stars and unlock flags (`ProgressManager.cs:491-543`).
2. **Wiring the campaign would currently block the game at boot.** `CampaignSaveService.InitializeInternal` refuses the campaign if any validator issue has `Error` severity (`CampaignSaveService.cs:62-68`). `CampaignConfigValidator.ValidateMedia` emits an Error when `contextImage` or `narrationClip` is null (`CampaignConfigValidator.cs:801-805`), and every focus word and level in all 15 configs has both null. Levels 6 to 15 also fail roster and pool checks, and Level 13 has no focus words at all. The test `RevisedCampaignAssetTests.LevelOneScope_ValidatesCleanExceptDeferredMedia` deliberately excludes the media errors, which is why the suite is green while the runtime gate would fail.
3. **Nine LF-CONTRACT-v2 phases exist, but only five have a surface.** `LevelFlowController.ExecutePhase` routes Story, FocusWords, SymbolLearning, Defense, ContextChallenge, MemoryReward, AtomicSave, Results; `RequiredPractice` falls to `ExecuteStubPhase` and auto-completes (`LevelFlowController.cs:244-269`). ContextChallenge and MemoryReward also auto-complete when the level has no `challengeSequence` or no `rewardIds` (`LevelPhasePlan.cs:51-58`; `LevelFlowController.cs:424-425, 459-461`). Levels 6, 7, 8, 10, and 13 have no challenge sequence and Levels 6 to 15 have no reward ids, so on those levels **clearing the wave alone completes the level**, contrary to the spec rule.
4. **The final-syllable rule is data only.** `LevelConfigSO.finalRestorationValue` is authored on 14 levels and validated for shape, but no runtime code reads it (`grep` finds only `LevelConfigSO.cs:35` and the validator). Restoration is word-level token choice, not syllable placement. Level 1 is authored as `NA` where the spec says `MA`; Level 15 is validator-forced to `PA` where the spec says `YA`.
5. **Active-clue combat is on for Level 1 only.** `activeClueCombatEnabled` is `1` in `Level1_Config.asset` and `0` in the other 14. Levels 2 to 15 use legacy combat where any matching on-screen enemy is hit and three matches trigger a mass clear (`CombatResolver.cs:127-199`). The spec requires "Respond to the active clue only" on every level.
6. **No hub, level preview, objective card, ready screen, wave-clear screen, memory card, archive, era completion, prologue, ending, or checkpoint recovery screens exist.** Fourteen of the 43 UF rows are MISSING or NOT-BUILT-YET.
7. **Accessibility settings are audio sliders only.** No captions, narration toggle, reduced motion, text speed, font size, high contrast, haptics, left-handed trace pad, or language setting exists anywhere in `Assets/Scripts` (keyword sweep returned no hits).
8. **No analytics tracking events exist.** `Assets/Scripts/Analytics/RecognitionLogger.cs` writes a recognition-attempt CSV only. None of the 25 `BTN-*` tracking events is emitted.

---

## 1. Runtime mode note (read this before the tables)

Many rows below say "revised path" or "legacy path". The code has two progress systems selected at boot:

| Mode | Selected when | Progress store | Stars | Unlock rule |
|---|---|---|---|---|
| Legacy (**current runtime**) | `SaveManager._campaign == null` (`SaveManager.cs:34`) | PlayerPrefs `salinlahi.progress.*` (`ProgressManager.cs:883-885`) | Hearts only: 3 at 100%, 2 at ≥50%, else 1 (`ProgressManager.cs:453-485`) | Completing level N sets `unlocked.N+1` (`ProgressManager.cs:528-533`) |
| Revised (**built, never wired**) | `_campaign` assigned and validator clean | `campaign-save.json` + journal in `persistentDataPath` (`ICampaignSaveStorage.cs:149-163`) | Accuracy formula (`LevelResultsCalculator.cs:43-50`) | `ApplyLevelProgression` flattened index+1, endless at last (`CampaignOutcomeCoordinator.cs:232-250`) |

Statuses in the tables describe what a player experiences today (legacy) and note where the revised path would change the answer.

---

## 2. Status table — User Flow (UF-01 to UF-43)

| ID | Spec requirement (one line, quoted) | Status | Evidence (file:line) | Gap |
|---|---|---|---|---|
| UF-01 | App Launch: "Black screen, studio logo, then a faint Baybayin symbol"; "Tap to Continue"; "Loads local save data and accessibility settings"; damaged data → "offer a safe reset without deleting settings". | PARTIAL | `Core/BootstrapLoader.cs:6-13` (init save, load menu); `Core/SceneLoader.cs:274-332` (runtime black loading canvas + progress bar); `Data/Persistence/CampaignSaveService.cs:120-166` (quarantine corrupt files, `safe-reset` receipt); `UI/CampaignSaveNoticePanel.cs:66-83` (recovery notice copy); `Core/AudioManager.cs:156-158,823-869` (audio prefs kept in PlayerPrefs, untouched by reset) | No logo/symbol splash, no Tap to Continue. Safe reset exists only on the dormant revised path. Language setting does not exist. |
| UF-02 | First-Time Save Check: "Begin Journey" + "Settings"; "Creates a new local save"; storage unavailable → "continue with temporary session and show offline save warning". | PARTIAL | `UI/MainMenuUI.cs:62-93` (Play label becomes "Start Journey" for `NewJourney`); `Data/Persistence/JourneyEntryResolver.cs:99-109`; `Core/SaveManager.cs:64-74` (I/O failure → `RevisedBlocked`, blocking notice) | No dedicated first-time screen. On storage failure the revised path **blocks Play** (`MainMenuUI.cs:124-128`) instead of a temporary session. Legacy path has no failure handling at all. |
| UF-03 | Returning Player Check: menu "shows current era, level, completion rate"; "Continue Journey" resumes "from the latest safe checkpoint"; "New Journey, Memory Archive, Settings". | PARTIAL | `UI/MainMenuUI.cs:107-150` (routes to first unlocked incomplete level); `Data/Persistence/JourneyEntryResolver.cs:67-117`; `Core/ProgressManager.cs:134-163` (legacy equivalent) | No era/level/completion display on menu. "Checkpoint" is level granularity only. No New Journey or Memory Archive button. |
| UF-04 | Opening Prologue: "Pixel-art cinematic"; "Next", "Skip Cinematic, Auto-Play"; "Prologue viewed flag". | NOT-BUILT-YET (wiring) | `Assets/ScriptableObjects/Cutscenes/Level1_Opening.asset` (6 English panels exist); `LevelCutsceneMapping.asset` has `entries: []` (asset dump); `LevelFlowController.cs:288-299, 1013-1023` (would play a mapped BeforeLevel cutscene); `UI/CutscenePlayer.cs:526-531` (`SkipCutscene` exists), `:725-729` (skip button root is force-hidden) | Prologue asset never plays because the mapping is empty. No Auto-Play. No viewed flag persisted. Text is English; spec lore is Filipino narrator. |
| UF-05 | Juan Accepts the Role: "Enter the Scroll"; "Unlocks Era 1 and opens the Living Scroll hub"; saves "Era 1 unlocked, tutorial active". | MISSING | Era 1 / Level 1 unlocked by construction (`Data/Persistence/LevelLockResolver.cs:148-154` comment; `ProgressManager.cs:563-566`) | No acceptance screen, no hub. |
| UF-06 | Main Menu: "Continue Journey"; "Era Map, Memory Archive, Character Codex, Settings, Exit"; "Adaptive background based on latest completed era". | PARTIAL | `UI/MainMenuUI.cs:15-23` (buttons: Play, LevelSelect, EndlessMode, TracingDojo, Almanac, Settings), `:195-201` (Credits handler, no menu button per QA) | No Memory Archive, no Exit. Endless Mode and Tracing Dojo are not in spec. No adaptive background. |
| UF-07 | New Journey Confirmation: panel "lists what will be reset and what settings will remain"; "Start New Journey" / "Cancel"; next "UF-04 or UF-06". | PARTIAL | `UI/ResetJourneyFlow.cs:16-34` (copy lists reset/kept items); `UI/ResetJourneyConfirmationPanel.cs:53-90`; `UI/SettingsPanel.cs:112-122, 170-177`; `Core/ProgressManager.cs:646-688` | Reachable only from Settings, not a New Journey button. `CanOfferReset` requires `RevisedReady` (`ResetJourneyFlow.cs:36-39`) so the button is **hidden in the current legacy runtime**. Success routes to Main Menu, never to the prologue. |
| UF-08 | Living Scroll Hub: "Three chambers"; "progress bar, and quick links"; "Locked eras show the exact unlock requirement". | MISSING | Not found. Nearest: `UI/LevelSelectUI.cs` era paging | No hub scene. |
| UF-09 | Era Map: "Five level nodes connected by a glowing path. Locked nodes show chains. Completed nodes show stars and a memory icon"; "Locked node explains the prior level required"; "Back to Scroll, View Era Lore". | PARTIAL | `UI/LevelSelectUI.cs:91-164` (five buttons per era, prev/next); `UI/LevelButton.cs:72-119` (lock icon, completion badge, grey tint); `UI/LevelLockNoticePanel.cs:35-44, 103-107` (prerequisite copy, placeholder); `Assets/Art/UI/level1..5.png` only | No stars on nodes, no memory icon, no path, no era lore. Numbered scroll art exists for Levels 1 to 5 only; 6 to 15 render blank placeholders (`LevelButton.cs:85-96`). Lock copy is flagged "NOT PRODUCT-APPROVED" in code. |
| UF-10 | Level Preview: "Level title, story summary, target words, new symbols, best stars"; "Enter Memory"; "Hear Target Words, View Symbols, Back". | MISSING | `UI/LevelButton.cs:197-232` loads Gameplay directly on press | No preview screen. |
| UF-11 | Story Scene: "pixel-art scene with dialogue"; "Next"; "Skip Scene, Auto-Play, Replay Voice"; "Story viewed flag". | PARTIAL (Levels 1-5) / NOT-BUILT-YET (6-15) | `Gameplay/LevelFlowController.cs:271-311` (`ExecuteStory`: cutscene then `introDialogue`); `UI/DialogueController.cs:210-242, 461-481` (tap advances, tap skips typewriter); Level configs 1-5 have `introDialogue` = `Dialogue_UgatNN_Intro`; 6-15 have `None` (asset dump) | No Skip Scene, no Auto-Play, no voice, no persisted viewed flag. Story is dialogue box over the battlefield, not a scene. |
| UF-12 | Mission Objective: "Objective card with story goal, language goal, combat goal, hearts, and final restoration condition"; "Study Words". | MISSING | Not found | — |
| UF-13 | Target Word Preview: "Each word includes image, meaning, syllable breakdown, and empty Baybayin slots"; "Tap each word and syllable to hear it"; "Learn Symbols"; "Continue stays disabled until required words are opened in tutorial levels". | PARTIAL | `UI/HUD/FocusWordPreviewController.cs:31-50, 58-98` (one text panel: label, meaning, Latin syllables; single Continue) | No image, no Baybayin slots, no per-word/syllable audio, no inspected-gating. Decomposition uses `symbol.syllable`, so DA/RA shows "da" regardless of context (`:90-98`). |
| UF-14 | Symbol Lesson: "Large Baybayin symbol, Roman sound, word example, stroke order"; "Practice Symbol"; "Replay Stroke, Replay Sound, Next Symbol"; "Older review symbols may be skipped in later levels". | PARTIAL / DEVIATES | `UI/HUD/SymbolLearningCardController.cs:88-110, 170-203` (glyph = `displaySprite` learning card, context label via `SpokenValueResolver`, replay audio, Continue) | No stroke-order animation, no word example, no skip for review symbols. Every level's `learningRequirements` lists the whole cumulative pool as `Instruction` (asset dump: Level 2 = 6 cards, Level 15 = 17 cards), so the player re-watches every card every level. Audio exists for 7 of 17 symbols; all four Level 1 symbols are silent. |
| UF-15 | Tracing Practice: "Tracing canvas, start point, stroke path, accuracy ring, attempts"; "Check Trace"; "Reset Trace, Hear Sound, Show Guide"; "Accuracy meets the level threshold for every required symbol"; "No heart is lost". | MISSING (in-level) / PARTIAL (elsewhere) | `Gameplay/LevelFlowController.cs:259-262` (RequiredPractice is a stub, auto-completes); `practiceRequirements` authored on all 14 levels (asset dump); `UI/TracingDojo/TracingDojoController.cs` (free practice, menu only); `Gameplay/Tutorial/Onboarding/Level1OnboardingController.cs:142-190` (Level 1 tutorial SoloTeach beats); `Data/RecognitionConfigSO.cs:14` (single global 0.60 threshold); `Gameplay/Recognition/StrokeCapture.cs:215, 233-238` (auto-submit after 1.5 s, no Check button) | The mandatory practice phase does not exist in-level. No per-level threshold, no accuracy ring, no Reset Trace, no attempt counter. |
| UF-16 | Ready for Defense: "Battle preview shows enemy types, active clue forms, hearts"; "Defend the Scroll"; "Creates combat checkpoint". | MISSING | `LevelFlowController.cs:364-420` (Defense starts immediately after learning cards) | No ready screen, no combat checkpoint. |
| UF-17 | Combat HUD: "Juan at the bottom, Living Scroll behind him, enemy lanes above, three hearts, combo, active clue, trace pad, pause button, wave meter"; "Level 5 boss may restart the current phase instead of the whole level". | PARTIAL / DEVIATES | `UI/HUD/HeartDisplay.cs`, `UI/HUD/ComboDisplay.cs:33-46`, `UI/HUD/WaveDisplay.cs:35-41`, `UI/HUD.cs:32-35` (pause), `UI/HUD/ActiveCluePresenter.cs:492-544` (clue panel + replay); `Gameplay/Enemy/EnemyMover.cs:59-64` (enemies descend); `Gameplay/Combat/CombatResolver.cs:433-435` (single column, no lanes); `Gameplay/Protagonist/ProtagonistSlashVfx.cs` (slash frames, no arrow); `Gameplay/Boss/BossController.cs:165-190` (phase loops until damaged) but defeat reloads whole level (`UI/DefeatScreenUI.cs:63-72`) | No lanes. Attack is a melee slash VFX, not a bow/arrow. Active clue only on Level 1. Boss defeat restarts whole level. Protagonist spawns only where `hasProtagonist` is true (Levels 1-5). |
| UF-18 | Correct Trace Response: "Correct path glows, Juan fires, target is hit, combo rises"; "Armored targets remain until required hits are completed". | DONE (Level 1) / DEVIATES (2-15) | `CombatResolver.cs:233-286` (active-clue hit, pronunciation, evidence); `Gameplay/Combat/ComboManager.cs:63-89`; `Data/EnemyDataSO.cs:23-25` + `Gameplay/Enemy/Enemy.cs:332-354` (multi-HP); `ActiveCluePresenter.cs:564-580` ("Restored: WORD" cue) | On Levels 2-15 legacy resolution hits the closest matching enemy and mass-clears three or more (`CombatResolver.cs:127-199`). |
| UF-19 | Combo Reward: "rapid shot, piercing arrow, or temporary shield"; "A wrong answer resets combo and removes unused reward". | PARTIAL / DEVIATES | `ComboManager.cs:118-148` (tier-based power grant), `:201-213` (reset on miss keeps shields by design), `Gameplay/Combat/ComboPower.cs:31-41` (tier map); `Data/GameConfigSO.cs:10` (threshold 5) | Power tier comes from `challengePolicy.tier`; only Level 1 sets a tier (1 → None). All levels grant `None` today. Shield survives a miss, contrary to spec. Focus Mode slow-time is not in spec. |
| UF-20 | Wrong or Weak Trace: "Red outline, missed arrow, correction hint"; "Resets combo and shortens the time before the target advances"; "Some levels apply a score penalty". | PARTIAL | `UI/HUD/DrawingFeedback.cs:81-99` (flash, message, help offer after 3); `UI/HUD/DrawingFeedbackVocabulary.cs`; `CombatResolver.cs:242-260` (miss + false evidence) | No time-shortening. No score penalty (legacy stars are hearts-only). |
| UF-21 | Heart Lost: "Scroll cracks, one heart empties, battlefield pauses briefly, and the missed clue is shown"; "returns the clue to the review queue". | PARTIAL | `EnemyMover.cs:66-101` → `Gameplay/Base/PlayerBase.cs:24-27` → `Gameplay/Base/HeartSystem.cs:47-86`; `Feedback/BaseHitFeedbackController.cs`, `Feedback/DamageEdgeFlashController.cs` | No pause, no missed-clue display, no review queue. |
| UF-22 | Pause Menu: "Resume"; "Restart Wave, Controls, Audio, Exit to Map"; shows "current objective, current words, symbol quick view"; "Exit requires confirmation. Restart resets the current wave". | PARTIAL / DEVIATES | `UI/PauseMenuUI.cs:56-71, 118-150` (Resume, Restart, Quit, Settings), `:156-220` (confirmation for both), `:222-237` (Restart = whole level via `SceneLoader.RestartCurrentLevel`), `:239-250, 268-298` (leave caches in-memory snapshot) | Restart is whole-level, not wave. No objective/words/symbol view. Paused-run snapshot is memory only (`Core/GameManager.cs:216-290`), lost on app kill. |
| UF-23 | Wave Cleared: "Wave-clear banner, surviving hearts, combat accuracy"; "Restore the Memory"; "Combat victory alone does not complete the level". | DONE (rule) / MISSING (screen) | `Gameplay/Wave/WaveManager.cs:639-655` (raises `OnDefenseComplete`); `Gameplay/Flow/LevelFlowMachine.cs:41-66` (defense can only advance the phase); `Core/EventBus.cs:19-22` | No banner or button; flow moves straight to the challenge. Rule is undermined by data on Levels 6, 7, 8, 10, 13 (see §5). |
| UF-24 | Restoration board: "Story scene on one side, missing word slots on the other, movable Baybayin tiles"; "Submit Restoration"; "Hear Sentence, Use Hint, Undo, Reset"; wrong placement "shakes and returns". | PARTIAL / DEVIATES | `Gameplay/ChallengeFlowController.cs:43-94`; `Gameplay/ChallengeModeUI.cs:46-105, 107-142` (runtime buttons, whole-word token choices); `Gameplay/ChallengeSession.cs:168-217` (per-slot validation, sentence restoration) | No Baybayin tiles; choices are whole Latin words with decoys. Validation is immediate per tap, no Submit. Retry = checkpoint reset; no Undo, no Hear Sentence, no shake. Assets exist for Levels 1-5, 9, 11, 12, 14, 15 only. |
| UF-25 | Hint Modal: "Hint choices show image meaning, replayed audio, first symbol, or correct blank glow. Cost is shown before use"; "Use This Hint" / "Cancel"; exhausted → "explains how to retry or review". | DEVIATES | `ChallengeSession.cs:219-246` (`RequestHint` reveals the exact answer token, degrades clue policy); `ChallengeModeUI.cs:234-245` ("Hint: WORD"); `Gameplay/ChallengeTierPolicy.cs:25-31` (penalty only when `emergencyHintEnabled`, tier 5) | No choice, no cost display, no confirm, no exhausted-state copy (button silently no-ops). No level is tier 5, so hints are free and unlimited (except tier 5 budget). |
| UF-26 | Incorrect Placement: "Incorrect slot shakes, misplaced tile returns, and a context reminder appears"; "Preserves correct placements"; "Too many errors may reduce score or remove a heart based on the level rules". | PARTIAL | `ChallengeSession.cs:410-451` (error count, penalty after `maxErrors`, checkpoint reset); `ChallengeModeUI.cs:213-214` ("Try again. Correct progress is safe.") | Text only, no shake/return animation. Score reduction only on the dormant revised path. |
| UF-27 | Memory Restored: "The missing text appears in Baybayin and Roman form. The environment regains color"; validation "Combat, tracing, word, context, and final syllable requirements all pass". | PARTIAL / MISSING (rule) | `LevelFlowController.cs:454-466` (plays `contextMedia.cutscene` when `rewardIds` non-empty); cutscenes for Levels 1-5 are text-only, `image: None` (asset dump) | No Baybayin rendering, no colour return. Final-syllable validation not implemented. Levels 6-15 skip the phase (`rewardIds: []`). |
| UF-28 | Level Results: "Score summary, one to three stars, trace accuracy, combat accuracy, context accuracy, best combo, hearts, hints, and improvement tip"; "Claim Memory"; "Replay Level, View Details". | PARTIAL | `UI/VictoryScreenUI.cs:40-67` (stars from ProgressManager), `:75-105` (runtime summary: score, tracing %, context %, restored words, new symbol count); `Data/Learning/LevelResultsCalculator.cs:30-62`; `LevelFlowController.cs:503-555` | Stars shown are legacy hearts-only today. No combat accuracy, best combo, hint count, tip, Claim, Replay, or Details. |
| UF-29 | Memory Card: "card front and back, target words, Baybayin forms, lore text, and collectible number"; "Flip Card, Hear Words, Add to Favorites"; "Stores the card in the archive". | MISSING | `Data/Learning/LevelRewardResolver.cs:51-63` (`memory.*` ids computed); `Data/Persistence/CampaignSaveDocument.cs:33` (`unlockedMemoryIds` stored, revised path only) | No card UI, no archive. |
| UF-30 | Return to Era Map: "Updated map, completed node, new node glow, stars, and era completion meter"; "Next Level"; "If validation fails, the next node remains locked and the missing requirement is shown". | PARTIAL | `VictoryScreenUI.cs:106-135` (Next Level loads gameplay directly), `:137-146` (Level Select); `LevelSelectUI.cs:129-159` (highlight next level); `CampaignOutcomeCoordinator.cs:232-250, 300-325` (unlock + verify, revised) | No return-to-map step after victory; no stars or completion meter on the map. |
| UF-31 | Level Failed: "Failure scene, cause of failure, missed symbols, current checkpoint, and recommended review"; "Retry Checkpoint"; "Review Symbols, Restart Level, Exit to Map"; "earlier completed memories remain safe". | PARTIAL | `UI/DefeatScreenUI.cs:41-83` (hearts text, Retry = full reload, Level Select); no commit on defeat (`LevelFlowController.cs:977-995`) | No cause, missed symbols, checkpoint, or review. Retry replays every learning card (also QA 5.8). |
| UF-32 | Checkpoint Recovery: "Checkpoint summary shows what stays complete and what restarts"; "Retry Now"; "Change Difficulty Assist". | MISSING | `ChallengeSession.cs:299-317` (checkpoint reset exists inside the challenge only) | No cross-defeat checkpoint. No difficulty assist. |
| UF-33 | Era Completion Scene: "Longer cinematic, completed era paragraph, all five memory cards, mastery summary, and the era ending line"; "Complete Era"; "Combat victory without paragraph completion keeps the era locked". | MISSING / DEVIATES | Not found. Era 5/10/15 configs are boss levels with two word units (asset dump); `Era_01.memoryReference` = `Cutscene_Ugat01_Memory` (Level 1's memory, reused); Era_02/03 have none | No era scene, no paragraph, no ending line. Level 5 outro dialogue mentions Ugnayan (`Dialogue_Ugat05_Outro`), that is the only era-end beat. |
| UF-34 | Next Era Unlock: "Next era door changes from gray to color. New symbols appear around it. A short lore preview plays"; "Enter Next Era". | PARTIAL | `CampaignOutcomeCoordinator.cs:239-242` (flattened index+1 crosses the era implicitly); `LevelLockNoticePanel.cs:40-41` (era-crossing copy); `LevelSelectUI.cs:175-184` (arrow enable) | No unlock scene or preview. |
| UF-35 | Paglimot Boss Introduction: "Three-phase boss arena"; "Face Paglimot"; "Review All Symbols, Back to Map"; "Starts boss Phase 1 with three hearts and one phase checkpoint". | DEVIATES | `Level15_Config.asset` → `BossConfig_Kadiliman` with **4** phases, `allowedCharacters = [NGA]` only (asset dump); `BossController.cs:150-162` (samples random glyph from `allowedCharacters`); `Gameplay/Boss/BossTutorialController.cs` (only El Inquisidor has a tutorial asset) | Boss is the legacy "Kadiliman", not Paglimot. Phases test random single glyphs, not era recall or word restoration. Level 15 asks only NGA. |
| UF-36 | Boss Phase Transition: "recovered words fill part of the final paragraph"; "Saves the phase checkpoint"; "Failure restarts the current phase, not completed phases". | DEVIATES | `BossController.cs:165-190` (phase repeats until damaged within one attempt); defeat → whole level (`DefeatScreenUI.cs:63-72`) | No paragraph lines, no persisted phase checkpoint. |
| UF-37 | Final Word Restoration: "the word MALAYA missing its last syllable"; "Trace YA and place it into MALAYA"; "Restore MALAYA". | DEVIATES | `Challenge_Pamana15_Context.asset` (choose whole word MALAYA among decoys SAYA, MAHALAGA); `Level15_Config.finalRestorationValue = PA`; `CampaignConfigValidator.cs:652-661` (forces `symbol.pa/value.pa` on the finale) | Spec says YA; code enforces PA. No syllable trace-and-place. See Open Question Q1. |
| UF-38 | Final Ending Cinematic: "Continue"; "Completes the campaign and unlocks post-game review"; "Campaign complete flag, ending viewed". | MISSING | `ProgressManager.cs:535-538` and `CampaignOutcomeCoordinator.cs:243-244` (completing Level 15 unlocks **Endless Mode**) | No ending. Endless Mode is not in spec. |
| UF-39 | Memory Archive: "Cards grouped by era, filters, favorites, locked silhouettes". | MISSING | Not found | — |
| UF-40 | Character Codex: "Grid of 17 visual symbols, spoken values, mastery bars, first use, later uses, and challenge mode"; "Practice Symbol"; "Locked symbols show the era in which they appear". | PARTIAL / DEVIATES | `UI/Almanac/AlmanacController.cs:121-145` (grid from `CharacterRegistry_Default`, locked '?' cells, detail = ID + description), `:176-181` ("Learned X/Y"); `CharacterRegistry_Default.asset` has **18** entries incl. `Char_RA` (no `stableId`) | 18 cells, not 17. No spoken values, mastery bars, uses, practice entry, or era hint on locked cells. Practice lives in the separate Tracing Dojo (`UI/TracingDojo/*`). |
| UF-41 | Completed Scroll Hub: "Fully colored hub, final completion percentage, mastery challenges, replay access, and ending gallery". | MISSING | `MainMenuUI.cs:43-48, 159-169` (Endless Mode unlock instead) | — |
| UF-42 | Settings: "Audio sliders, narration, text speed, font size, high contrast, reduced motion, haptics, left-handed trace pad, language, reset controls"; "Apply Settings"; "Restore Defaults, Back". | PARTIAL | `UI/SettingsPanel.cs:9-30` (master/BGM/SFX sliders, Back, optional Reset Journey); `AudioManager.cs:823-869` (persisted) | Only audio. No narration, text speed, font size, contrast, motion, haptics, handedness, language, Restore Defaults, or Apply. |
| UF-43 | Exit Confirmation: "states what has been saved and what current progress will restart"; "Save and Exit"; save failure → "Retry Save and Continue Without Saving". | PARTIAL / DEVIATES | `PauseMenuUI.cs:30-33` ("Leave this level? Your progress in this attempt will not be saved."), `:239-250` | Nothing is saved on exit (attempt aborted). No app-exit path. Retry-save UI exists only for level completion (`UI/CampaignOutcomeSaveFailurePanel.cs`). |

---

## 3. Status table — Button and UI Rules

### 3.1 Button dictionary (BTN-*)

Tracking events: none of the 25 events is emitted anywhere. `Analytics/RecognitionLogger.cs:43-122` logs recognition attempts to `recognition_log.csv` only. The "Tracking" column is therefore MISSING for every row and is not repeated.

| ID | Spec (label / enabled-when / confirm / destination) | Status | Evidence | Gap |
|---|---|---|---|---|
| BTN-START | "Begin Journey" on first-time menu; enabled when "No active journey exists"; → Opening Prologue | PARTIAL | `MainMenuUI.cs:76-78` ("Start Journey"), `:107-150` (→ Gameplay Level 1) | Label differs; skips prologue. |
| BTN-CONTINUE | "Continue Journey"; enabled when "A valid save exists"; disabled shows "Loading or Repair Save" | PARTIAL | `MainMenuUI.cs:79-81, 124-128`; `CampaignSaveNoticePanel.cs:23-37` (blocking notice with "Retry") | Label "Continue". Blocked state shown via notice panel, button not relabelled. |
| BTN-NEXT | "Next" on dialogue panels; enabled when "Current panel is fully displayed" | PARTIAL | `DialogueController.cs:461-481` (tap anywhere; first tap completes typewriter); `CutscenePlayer.cs:497-513` | Tap-catcher, no labelled button; tap during typewriter completes the line instead of being disabled. |
| BTN-SKIP | "Skip Scene"; "Yes for final ending only" confirm | MISSING | `CutscenePlayer.cs:75-76, 725-729` (skip listener removed, root hidden) | Method exists, control hidden. |
| BTN-HEAR | "Hear Word" on word preview and restoration | PARTIAL | `SymbolLearningCardController.cs:119-128` (per-symbol replay); `ActiveCluePresenter.cs:296-300` (combat clue replay) | No word/sentence replay on preview or restoration board. |
| BTN-PRACTICE | "Practice Symbol" on lesson and codex → Tracing Practice | MISSING | Not on `SymbolLearningCardController` or `AlmanacController` | Practice only via main-menu Tracing Dojo. |
| BTN-CHECK | "Check Trace"; "Dimmed before the minimum coverage is reached" | DEVIATES | `StrokeCapture.cs:187-216, 233-238` (auto-submit 1.5 s after last stroke; tap-like strokes discarded `:194-206`) | No explicit check; auto-submit. |
| BTN-RESET | "Reset Trace" | MISSING | Not found | — |
| BTN-READY | "Defend the Scroll"; "Shows missing symbol count" | MISSING | No ready screen | — |
| BTN-PAUSE | "Pause" on HUD | DONE | `HUD.cs:32-35` → `GameManager.cs:105-113` | Not hidden during result animations (not verified either way). |
| BTN-RESUME | "Resume" | DONE | `PauseMenuUI.cs:118-124`; `GameManager.cs:115-124` | — |
| BTN-RESTART-WAVE | "Restart Wave" resets "current wave or boss phase"; confirm Yes | DEVIATES | `PauseMenuUI.cs:126-132, 222-237` (confirm, then whole-level restart) | Whole level, not wave. |
| BTN-HINT | "Use Hint" shows score cost; "Yes, after selecting a hint type"; exhausted → "No Hints Left" | DEVIATES | `ChallengeModeUI.cs:102`; `ChallengeSession.cs:219-246` | No cost, no confirm, no exhausted copy. |
| BTN-UNDO | "Undo" returns most recent tile | MISSING | `ChallengeModeUI.cs:103` has "Retry" (checkpoint reset) | — |
| BTN-SUBMIT | "Submit Restoration" validates all slots; "Shows remaining blank count" | DEVIATES | `ChallengeSession.cs:168-194` (validated per tap); `ChallengeModeUI.cs:210` ("Slots: n/m") | No submit step. |
| BTN-CLAIM | "Claim Memory" stores card; disabled shows "Complete Objective" | MISSING | Save happens automatically in `ExecuteAtomicSave` (`LevelFlowController.cs:468-492`) | — |
| BTN-NEXT-LEVEL | "Next Level" → Next Level Preview; "Shows unlock requirement" | PARTIAL | `VictoryScreenUI.cs:106-135` (→ Gameplay directly) | No preview; no requirement copy. |
| BTN-REPLAY | "Replay Level"; "Never disabled for completed levels" | PARTIAL | `LevelButton.cs:197-232` (completed levels stay pressable) | No Replay on results/archive. |
| BTN-NEXT-ERA | "Enter Next Era" | MISSING | No era completion screen | — |
| BTN-ARCHIVE | "Memory Archive" | MISSING | Not found | — |
| BTN-CODEX | "Character Codex" | PARTIAL | `MainMenuUI.cs:177-182` (Almanac) | Almanac also holds an Enemies tab not in spec. |
| BTN-SETTINGS | "Settings" on global screens | PARTIAL | `MainMenuUI.cs:184-193`; `PauseMenuUI.cs:142-150` | Not on Level Select, Almanac, or Dojo. |
| BTN-BACK | "Back" returns to previous safe screen | PARTIAL | `LevelSelectUI.cs:339-348`; `AlmanacController.cs:206-213`; `UI/TracingDojo/DojoNavigator.cs`; `SettingsPanel.cs:102-106` | Present on all non-combat scenes that exist. |
| BTN-EXIT | "Save and Exit"; confirm Yes; "Shows Retry Save if writing fails" | DEVIATES | `PauseMenuUI.cs:134-140, 239-250` | Leave without save; no app exit. |
| BTN-FINAL | "Restore MALAYA" validates YA | DEVIATES | See UF-37 | — |

### 3.2 Global UI and Interaction Rules

| Rule | Spec wording | Status | Evidence | Gap |
|---|---|---|---|---|
| Safe Saving | "Save only at safe checkpoints: after practice, wave clear, restoration checkpoint, result, and map"; "Checkpoint includes screen, level, hearts, phase, completed placements"; "Never corrupt prior completed progress"; "Show text, not color alone, for save status" | PARTIAL | Revised: `LevelFlowController.cs:468-492` (save only at AtomicSave), `Data/Persistence/CampaignSaveCommitter.cs:60-123` (temp → read-back → backup → promote → verify → rollback), `CampaignOutcomeJournal` + receipts (`CampaignOutcomeCoordinator.cs:23-58`). Legacy: `ProgressManager.cs:491-543` at level complete only. In-memory snapshot on leave: `GameManager.cs:216-290` | Saves happen at fewer points than the spec (level end only); no mid-level checkpoint survives app restart. Save-status indicator: none (no scroll icon or text). Prior-progress safety is stronger than spec on the revised path. |
| Three Hearts | "Combat begins with three hearts"; "At zero hearts, freeze enemy movement before opening failure state"; "Remaining hearts persist only inside the current safe checkpoint"; "optional larger heart icons and reduced flash" | PARTIAL | `HeartSystem.cs:9, 47-86`; `WaveManager.cs:248-257` (returns enemies on GameOver); `DefenseRules.shrineHearts = 3` on all configs but unread at runtime (`HeartSystem._maxHearts` serialized) | No larger-icon or reduced-flash option. `defenseRules.shrineHearts` is not consumed. |
| Hints | "Hints explain, reveal, or replay. They do not automatically finish an entire required word"; "Cost is shown before confirmation"; "When exhausted, route to review"; "Narration and text alternative for every hint" | DEVIATES | `ChallengeSession.cs:219-246`; `ChallengeModeUI.cs:234-245` | Hint reveals the exact word token for the current slot (the unit's whole answer). No cost, no exhausted routing. |
| Locked Content | "Locked levels, cards, and symbols remain visible with exact requirements"; "Unlock checks use completed objective flags, not combat victory alone"; "Do not rely only on lock color" | PARTIAL | `LevelButton.cs:101-103` (lock icon), `LevelLockNoticePanel.cs:103-107` (requirement text on press); `AlmanacCell.cs:28-47` (locked '?' cell, non-interactable, no requirement); `LevelFlowMachine.cs:41-66` (objective gating) | Almanac locked cells show no requirement. Gating undermined by empty content on Levels 6, 7, 8, 10, 13 (§5.2). |
| Touch Targets | "Primary buttons are large and placed in the lower thumb area. Destructive actions stay separated"; "Mis-taps never delete progress without confirmation"; "Left-handed trace pad option and scalable UI" | PARTIAL | `PauseMenuUI.cs:156-220` (confirm restart/leave); `ResetJourneyConfirmationPanel.cs`; `UI/SafeAreaHandler.cs`; drawing surface is full screen (`StrokeCapture.cs:353-361`) | No left-handed option, no UI scale setting. Button sizing not verified visually. |
| Audio Clues | "Every required audio clue also has a replay control and an optional visual indicator"; "Audio asset ID must match the symbol and context value, including O/U and DA/RA"; "Missing audio falls back to text and symbol"; "Captions and narration toggle" | PARTIAL / DEVIATES | `ActiveCluePresenter.cs:527-544` (replay button when SpokenAudio channel); `Data/Campaign/ClueChannelResolver.cs:21-30` (visual fallback); `Data/Campaign/SpokenValueResolver.cs:19-31`; `Char_DA` has `value.da` and `value.ra` both → `DA.wav`; `Char_OU` has one `value.ou` → `O.wav` (asset dump) | No RA audio, no U audio, no captions, no narration toggle. Only 7 of 17 symbols have any clip. |
| Difficulty Assist | "Options may slow enemies, extend trace time, or show one stroke"; "Reduced motion, longer timing, and guide opacity controls"; "Assist icon appears in results details" | MISSING | `ChallengeTierPolicy.cs` (authoring-time tiers, not a player setting); `UI/HUD/TraceHintPresenter.cs` (ghost after 3 failures, not a setting) | No player-facing assist settings. |
| Final Syllable Rule | "Every level has a final required syllable that triggers the restoration scene"; "Validate exact symbol, trace accuracy, word order, and context slot"; "Combat completion cannot bypass this rule"; "Final syllable flag is part of completion data" | MISSING | `LevelConfigSO.cs:35` (field), `CampaignConfigValidator.cs:638-662` (shape check only); no runtime reader; `CampaignProgressOutcome.cs` has no final-syllable flag | Not implemented. |

---

## 4. Status table — Level Flow Details, Completion Rules, Character Mastery, Core Mechanics

### 4.1 Level Flow Details (Levels 1 to 15)

Columns compared: target words, final required syllable, story/dialogue, combat shape, restoration mode. Source for code values: the 15 `Assets/ScriptableObjects/Levels/LevelN_Config.asset` files and the `Assets/ScriptableObjects/Challenges/*` assets (parsed dump).

| Lvl | Spec words / final syllable | Code words / final | Story | Combat | Restoration | Status | Gap |
|---|---|---|---|---|---|---|---|
| 1 | INA, AMA / **MA** ("MA completes AMA") | INA, AMA / **NA** | Intro+outro dialogue, per-word dialogue, memory cutscene (text) | Active clue, 5 waves, 4 corruption enemies, protagonist | `Challenge_Ugat01`: 2 word-placement units | PARTIAL / DEVIATES | Final syllable authored NA, spec MA. All 4 Level 1 enemy types fall back to a placeholder prefab (QA B-02). No tracing practice phase. |
| 2 | BATA, MATA / TA | BATA, MATA / TA | Dialogue + cutscene (text) | Legacy combat, 3 waves | 2 units choosing missing syllable (BA, TA) | PARTIAL | Spec wants "Image clues begin replacing Roman text" and "Player selects which word matches the child and which matches the eyes". Code chooses the missing syllable token. |
| 3 | BATA, TAMA / MA; sentence "Ang mabuting BATA ay gumagawa ng TAMA" | BATA, TAMA / MA | Dialogue + cutscene (text) | Legacy, 4 waves | Sentence restoration, 1 blank (TAMA) | PARTIAL | Spec places both BATA and TAMA in blanks; code has one blank. No audio-only clues. |
| 4 | INA, AMA / MA; sentence "Ang INA at AMA ang unang guro sa tahanan" | INA, AMA / MA | Dialogue + cutscene (text) | Legacy, 4 waves ("fast paired" not verified) | Sentence, 1 blank (AMA) | PARTIAL | Spec: restore both words in order; code has one blank. |
| 5 | Paragraph of 7 words incl. IBA, MANA / NA; "Mixed enemies... one armored"; "alternates with paragraph blanks" | IBA, MANA / NA; boss **El Inquisidor** (3 phases), no waves | Dialogue + cutscene (text); outro mentions Ugnayan | Boss encounter (random glyph draws) | 2 syllable-choice units (EI, NA) | DEVIATES | No paragraph. Level is a legacy boss fight, not the spec's mixed wave. |
| 6 | AWA, GAWA / WA | AWA, GAWA / WA | None | Legacy, 4 waves, 12 enemy types; `allowedCharacters` includes untaught KA, HA, LA, NGA, PA, SA | **None** | NOT-BUILT-YET / DEVIATES | Wave clear completes the level. Roster fails validator. |
| 7 | SAMA, KASAMA / MA | SAMA, KASAMA / MA | None | Legacy, 4 waves (Salungat decoy + Kadena) | **None** | NOT-BUILT-YET | Same. |
| 8 | GANA, KAYA / YA | GANA, KAYA / YA | None | Legacy, 5 waves | **None** | NOT-BUILT-YET | Same. |
| 9 | OO, UNA / NA; sentence | OO, UNA / NA | None | Legacy, 5 waves | Sentence, 2 blanks | PARTIAL | No story. O and U share one spoken value (`value.ou`). |
| 10 | Paragraph of 10 words incl. SANA, SAYA / YA; "Armored enemies need two correct traces" | SANA, SAYA / YA; boss **Superintendent** (1 phase); `allowedCharacters = [A]` | None | Boss, no waves | **None** | DEVIATES / NOT-BUILT-YET | No paragraph, no challenge; boss asks only "A". |
| 11 | DALA, DAMA / MA | DALA, DAMA / MA | None | Legacy, 4 waves (Daan-Lihis only) | 2 word-placement units | PARTIAL | No story. "Context decides DA or RA" not surfaced. |
| 12 | HANGA, HALAGA / GA; "Shielded enemies require two correct traces" | HANGA, HALAGA / GA | None | Legacy, 5 waves (Hati hp1, Ngatngat hp1) | 2 word-placement units | PARTIAL | No shielded enemies in waves. |
| 13 | SANGA, HARAYA / YA; "Use ᜇ as RA inside HARAYA"; "Three-lane combat" | **No focus words, pool, requirements, or final value** | None | Legacy, 5 waves | **None** | NOT-BUILT-YET | Blocked on per-syllable spoken-value authoring (`docs/content/pamana-levels-11-15-narrative.md` decision 1). |
| 14 | ALAALA, MAHALAGA / GA; "memory meter" | ALAALA (A+LA+A+LA), MAHALAGA / GA | None | Legacy, 5 waves | TimedMemory unit, 45 s, 2 slots | PARTIAL | Timed recall exists (`ChallengeSession.cs:248-273`). No story. |
| 15 | 10-word paragraph; PAMANA, MALAYA / **YA**; three boss phases recalling eras; "Restore MALAYA" traces YA | PAMANA, MALAYA / **PA**; boss **Kadiliman** (4 phases); `allowedCharacters = [NGA]` | None | Boss, no waves | 2 word-placement units | DEVIATES | See UF-35 to UF-37. |

Additional level-wide deviations: level names are legacy colonial titles (`El Inquisidor`, `Superintendent`, `Bagong Pananakop`, `Karahawan`, `Kempei Patrol`, `Gauntlet`, `Kadiliman`) rather than the spec titles; `eraTheme` on every level is a legacy colonial theme (`EraTheme_Spanish/Japanese/American`) while `StageBackground_Ugat/Ugnayan/Pamana` assets exist; Levels 11 to 15 have `chapterNumber = 1`; `hasProtagonist` is false on Levels 6 to 15 so Juan does not appear.

### 4.2 Completion Rules

| Requirement | Spec pass condition | Status | Evidence | Gap |
|---|---|---|---|---|
| Story viewed | "Player reaches the objective screen." | PARTIAL | `LevelFlowController.cs:271-311` (dialogue must finish before Defense) | No objective screen; no persisted flag; Levels 6-15 have no story. |
| Symbols practiced | "All required symbols pass" the accuracy threshold; Level 5 "All era symbols" | MISSING | `LevelFlowController.cs:259-262` (RequiredPractice stub) | Not enforced. |
| Words restored | "Every word is complete and ordered correctly." | PARTIAL | `ChallengeSession.cs:379-408` (unit completion), `LevelPhasePlan.cs:51-52` | Enforced only where a challenge asset exists (10 of 15 levels). |
| Combat wave | "Required wave or boss phase is cleared." Fail: "Three hearts reach zero." | DONE | `WaveManager.cs:373-463, 639-655`; `HeartSystem.cs:81-85`; `BossController.cs:300-325` | Enemy mix per level (spec: slow → mixed → medium → fast paired → armored/boss) does not match authored waves (see 4.1). |
| Context challenge | "All blanks match the story context." Fail: "A correct word is placed in the wrong blank." | PARTIAL | `ChallengeSession.cs:168-194` (slot id + occurrence must match) | Same coverage gap as Words restored. |
| Last syllable | "Final syllable triggers the restoration scene." | MISSING | No runtime reader of `finalRestorationValue` | — |
| Unlock result | "Next node lights up and memory card is awarded." | PARTIAL | `ProgressManager.cs:528-533` (legacy unlock); `CampaignOutcomeCoordinator.cs:239-248` (revised unlock + memory ids) | No memory card UI. Level 5/10 unlock the next era only because the level list is flattened; no "every Level 5 objective" check beyond the phase machine. |

### 4.3 Character Mastery (17 visual symbols)

Data source: `Assets/ScriptableObjects/Characters/Char_*.asset` dump. Spec "First Level" vs code `firstIntroductionLevelId`.

| Symbol | Spec first level | Code first level | Audio clip | Badge art | Status | Note |
|---|---|---|---|---|---|---|
| A | 1 | level.ugat.01 | none | none | PARTIAL | Silent in Level 1. |
| E/I | 1 | level.ugat.01 | none | none | PARTIAL | One spoken value `value.ei`; spec: "Use the sound required by the word context". |
| BA | 2 | level.ugat.02 | BA.wav | BA.png | DONE (data) | — |
| MA | 1 | level.ugat.01 | none | none | PARTIAL | — |
| NA | 1 | level.ugat.01 | none | none | PARTIAL | — |
| TA | 2 | level.ugat.02 | none | none | PARTIAL | — |
| O/U | 9 | level.ugnayan.04 | O.wav | O.png | PARTIAL / DEVIATES | Single `value.ou`; no U audio. |
| KA | 7 | level.ugnayan.02 | KA.wav | KA.png | DONE (data) | — |
| GA | 6 | level.ugnayan.01 | none | none | PARTIAL | — |
| SA | 7 | level.ugnayan.02 | SA.wav | SA.png | DONE (data) | — |
| WA | 6 | level.ugnayan.01 | WA.wav | WA.png | DONE (data) | — |
| YA | 8 | level.ugnayan.03 | none | none | PARTIAL | — |
| DA/RA | 11 | level.pamana.01 | DA.wav for both values | DA.png | PARTIAL | `value.ra` reuses DA.wav; UI label uses `symbol.syllable` "da" (`FocusWordPreviewController.cs:90-98`, `ActiveCluePresenter.cs:781-799`). Recognizer folds RA→DA (`Data/BaybayinIdCanonicalizer.cs`, per `docs/technical/TW-SPK-004`). |
| HA | 12 | level.pamana.02 | HA.wav | HA.png | DONE (data) | — |
| LA | 11 | level.pamana.01 | none | none | PARTIAL | — |
| NGA | 12 | level.pamana.02 | none | none | PARTIAL | — |
| PA | 15 | level.pamana.05 | none | none | PARTIAL | Validator enforces PA instruction before PAMANA (`CampaignConfigValidator.cs:684-703`). |
| (RA) | not a symbol | `Char_RA.asset` exists with no `stableId`, listed in `CharacterRegistry_Default` | — | — | DEVIATES | Registry has 18 entries; Almanac shows 18 cells. |

Mastery rule ("Trace, recognize by sound, and place without a guide", etc.): the code has a four-dimension mastery model (Form, Sound, Assembly, Meaning) with states Introduced → Practiced → Recalled → Mastered (`Data/Learning/MasteryEvaluator.cs:8-28`, `Data/Learning/MasteryDimensions.cs`), driven by evidence recorded during combat, challenge, and Dojo. It is richer than the spec but only persists on the dormant revised path and is not shown in any UI (no mastery bars). Status: PARTIAL.

Templates: all 17 identities plus RA have recognition templates (`Assets/Resources/Templates`, 10 to 30 variants each). Glyph outline art exists for all 18 IDs (`Assets/Art/UI/GlyphOutlines`). Badge art exists for 7 (`Assets/Art/UI/GlyphBadges`). Almanac art exists for 7.

### 4.4 Core Mechanics

**Player loop steps**

| Step | Spec | Status | Evidence | Gap |
|---|---|---|---|---|
| 1.0 Era map | "Locked stages show the required prior level." | PARTIAL | `LevelSelectUI.cs`, `LevelLockNoticePanel.cs` | See UF-09. |
| 2.0 Story scene | "People, objects, or memories fade because of Paglimot." | PARTIAL | `LevelFlowController.cs:271-311` | Dialogue only, Levels 1-5. |
| 3.0 Two target words | "Tap each word to hear it. The word breaks into syllable tiles." | PARTIAL | `FocusWordPreviewController.cs` | Static text, no tap/tiles. |
| 4.0 One large symbol | "Observe stroke animation. The symbol lights in sequence." | PARTIAL | `SymbolLearningCardController.cs` | Static card, no stroke animation. |
| 5.0 Guided tracing | "Accuracy is measured... Pass every required symbol." | MISSING (in-level) | `LevelFlowController.cs:259-262` | — |
| 6.0 Battlefield | "Identify the active enemy clue and trace the matching symbol. Juan automatically fires." | PARTIAL | `CombatResolver.cs`, `ActiveClueDirector.cs` | Active clue only on Level 1; slash not arrow. |
| 7.0 Restoration | "Correct answers lock into place. Wrong ones shake and return." | PARTIAL | `ChallengeSession.cs` | Word tokens, no tiles/shake. |
| 8.0 Restored memory | "Score, stars, accuracy, and memory card appear." | PARTIAL | `VictoryScreenUI.cs` | No memory card. |
| 9.0 Level map | "Level 5 unlocks the next era. Progress does not advance if objectives are incomplete." | PARTIAL | `ProgressManager.cs:528-533`; `LevelFlowMachine.cs` | Objective gate hollow on content-less levels. |

**Combat and Archer System**

| Mechanic | Spec | Status | Evidence | Gap |
|---|---|---|---|---|
| Player position | "Juan remains near the bottom... does not manually aim." | PARTIAL | `Gameplay/Protagonist/ProtagonistManager.cs`; `hasProtagonist` true on 1-5 only | Absent on 10 levels. |
| Enemy target | "One enemy is marked as active... Respond to the active clue only." | DEVIATES | `ActiveClueDirector.cs:126-167` (Level 1 only) | — |
| Correct answer | "A valid trace matching the clue makes Juan fire one arrow." | PARTIAL | `CombatResolver.cs:233-286`; slash VFX | Visual differs. |
| Wrong answer | "arrow miss... Correction window becomes shorter." | PARTIAL | `CombatResolver.cs:242-260`; `GameConfigSO.cs:45` (echo window is a debounce, not a difficulty timer) | No shrinking window. |
| Lives | "Three hearts. An enemy reaching the Scroll removes one heart." | DONE | `HeartSystem.cs`; `EnemyMover.cs:66-101` | — |
| Combo | "Consecutive correct traces increase the combo. A mistake resets it." Rewards "Rapid shot, piercing arrow, or shield". | PARTIAL | `ComboManager.cs` | Rewards inert (tier data). Extra Focus Mode slow-time. |
| Victory | "Combat alone does not complete the level." | DONE (structure) / DEVIATES (data) | `LevelFlowMachine.cs:56-66` | See §5.2. |

**Level Difficulty Pattern** (Level type 1 to 5 per era): guidance ("Full tracing guides" → "No guide"), hints ("Image, sound, Roman, outline" → "One emergency hint with score penalty"), combat speed. Status: PARTIAL. Evidence: `ChallengeCluePolicy` Full/Reduced/Minimal on units (asset dump: Ugat 1-3 Full, Ugat 4-5 and all Pamana Reduced); `ChallengeTierPolicy.ForTier` (`ChallengeTierPolicy.cs:43-56`) defines the 1-5 escalation including tier-5 emergency hint with 10% penalty, but only Level 1 sets `tier` (1). Combat clue channels (`ClueChannels`) can express image/audio/incomplete-word clues but every level except 1 uses `Glyph` only with active clue off. Gap: the escalation is authored in code but not applied to level data.

---

## 5. Cross-checks

### 5.1 Contradictions inside the spreadsheet (questions for the team, not fixed here)

| # | Where | Contradiction | Question |
|---|---|---|---|
| Q1 | Level Flow Details L15, UF-37, Completion Rules "Last syllable: Completes final era word" say the final syllable is **YA** ("YA completes MALAYA"). Character Mastery says **PA** is "Introduced in the final inheritance word" with mastery "Trace correctly during the boss phase". Code enforces PA (`CampaignConfigValidator.cs:652-661`). | Which syllable ends the campaign: YA (spec flow) or PA (code)? |
| Q2 | Game Overview: "18 spoken values but 17 visual characters" (only DA/RA doubled). Character Mastery E/I: "Use the sound required by the word context"; O/U: "Interpret vowel from context". Global UI Rules: "Audio asset ID must match the symbol and context value, including O/U and DA/RA." Code: exactly 18 values, one each for E/I and O/U (`CampaignConfigValidator.cs:220-224`). | Are E, I, O, U separate spoken values (20 total) or not (18)? This decides audio clips and labels. |
| Q3 | Level Flow Details L1 final syllable **MA**. Code Level 1 `finalRestorationValue` = **NA**. The educational matrix (`docs/technical/TW-SPK-004`) says slot 2 (AMA) ends in MA. | Confirm MA; the asset is wrong. |
| Q4 | Core Mechanics and Completion Rules describe Level 5/10/15 as "Restore a paragraph using all era symbols" / "All prior words plus 2 final words". Level Flow Details L5 lists 7 words, L10 lists 10. The team's matrix and all authored assets use exactly **two focus words per level**. | Is the era paragraph a requirement, or do two final words satisfy Level 5/10/15? |
| Q5 | UF-17 failure state: "Level 5 boss may restart the current phase". Level Flow L5 combat: "Mixed enemies... one armored enemy... Combat alternates with paragraph blanks" (no boss). Completion Rules L5: "Mixed, armored, boss enemies". Code: Levels 5 and 10 are full boss encounters with no waves. | Are Levels 5 and 10 boss fights, mixed waves, or both? |
| Q6 | UF-06 lists "Exit" on the main menu and UF-43 covers app exit; UF-42 says "Reset journey requires a separate confirmation screen" while UF-07 makes New Journey a main-menu action. Code has reset in Settings only. | Where does New Journey live: main menu, Settings, or both? |
| Q7 | Character Mastery lists PAMANA among later uses of **A** (PAMANA = PA + MA + NA has no standalone A). | Typo, or is a different decomposition intended? |
| Q8 | UF-03 "Continue Journey" → UF-08 (hub); BTN-CONTINUE destination "Latest hub, map, or checkpoint"; UF-01 next screen "UF-02 or UF-03". | Does Continue open the hub, the map, or resume the level? |
| Q9 | Era 2 sheet paragraph uses inflected forms "SAMA-SAMA" and "maUNA"; Level Flow L10 lists tokens "SAMA, KASAMA... UNA". | Are inflected forms restorable tokens or display-only? |
| Q10 | Level Flow L2 restoration: "Player selects which word matches the child and which matches the eyes" (word-to-image). Core Mechanics Level 2 hints: "Image and sound only". No image assets exist and the spec never lists image assets per word. | Is per-word context art in scope for the demo? |
| Q11 | Completion Rules "Combat wave: Level 1 Slow basic enemies... Level 5 Mixed, armored, boss enemies" and Level Flow L4 "two hearts can be lost" if paired enemies arrive together. Core Mechanics "Lives: An enemy reaching the Scroll removes one heart." | Can a single event remove two hearts? |

### 5.2 Rules that are easy to break silently (checked one by one)

| Rule | Verdict | Evidence |
|---|---|---|
| **Final-syllable rule** | **Not enforced.** No runtime code reads `finalRestorationValue`; restoration units are whole words; Level 1 value disagrees with spec; Level 15 value disagrees with spec. | `LevelConfigSO.cs:35`; `CampaignConfigValidator.cs:638-662`; `Challenge_*` dump |
| **"Combat victory alone never completes a level"** | **Holds structurally, broken by data.** `LevelFlowMachine.ReportDefenseComplete` only advances to the next planned phase, and Results is reachable only via an accepted save. But `LevelPhasePlan.FromConfig` plans ContextChallenge only when `challengeSequence != null` and MemoryReward only when `rewardIds` is non-empty; RequiredPractice always auto-completes. On Levels 6, 7, 8, 10, 13 the plan is Story → FocusWords → SymbolLearning → Defense → AtomicSave → Results, so clearing the wave (or the boss) completes the level. | `LevelFlowMachine.cs:41-66`; `LevelPhasePlan.cs:43-59, 90-103`; `LevelFlowController.cs:224-228, 259-262, 424-425, 459-461`; asset dump |
| **Unlocks validated by objective flags rather than combat** | **Same as above.** Unlock is written only in AtomicSave (`CommitCompletion`), which the machine reaches only after every *planned* phase. There are no per-objective flags (story viewed, symbols practiced, words restored, final syllable) in the save document. | `LevelFlowController.cs:468-492`; `CampaignSaveDocument.cs:65-88`; `ProgressManager.cs:491-543` |
| **Saving only at safe checkpoints** | **Safer than spec, but fewer checkpoints.** Revised path commits once per level after all phases, plus `TrySetActiveLevel`, tutorial beat progress, and Dojo practice batches. Legacy path writes stars at completion. Nothing mid-level is persisted; pause-leave keeps an in-memory snapshot only (lost on app kill). No visible save indicator. | `CampaignSaveCommitter.cs:60-123`; `CampaignProgressRepository.cs:19-25, 85-102`; `GameManager.cs:216-290`; `PauseMenuUI.cs:268-298` |
| **Full offline play** | **Holds.** No network API appears anywhere in `Assets/Scripts` (grep for `UnityWebRequest`, `HttpClient`, `System.Net`, `internetReachability` returns nothing). Saves are `Application.persistentDataPath` files and PlayerPrefs. | `ICampaignSaveStorage.cs:32`; `ProgressManager.cs:883-885` |
| **Hint penalties** | **Not applied.** Score penalty exists only for tier-5 emergency hints (`emergencyHintScorePenalty` 0.10) and no level is tier 5. Hint count reaches the results metric only on the dormant revised path. On the legacy path hints have no consequence and are unlimited. | `ChallengeSession.cs:92-95, 219-246`; `ChallengeTierPolicy.cs:43-56`; `LevelResultsCalculator.cs:42-44`; asset dump (`tier` values) |
| **DA/RA and O/U read from context** | **Partially.** The learning card resolves label and clip from the requirement's `spokenValueId` (`SymbolLearningCardController.cs:170-203`, `SpokenValueResolver.cs`). But (a) `value.ra` has no distinct clip, (b) O/U has a single value, (c) focus-word preview and combat clue text use `symbol.syllable`, (d) Level 13 (the only RA word) is unauthored, and (e) `Char_RA` still exists as an 18th registry entry. | `Char_DA.asset`, `Char_OU.asset`, `Char_RA.asset` dump; `FocusWordPreviewController.cs:90-98`; `ActiveCluePresenter.cs:781-799`; `AlmanacController.cs:131` |

### 5.3 Accessibility items from Global UI Rules

| Item | Status | Evidence |
|---|---|---|
| Captions for audio clues / narration toggle | MISSING | Keyword sweep of `Assets/Scripts` for caption, subtitle, narration: no runtime hits. Clue text exists only when a level authors `LatinText`/`IncompleteWord` channels (`ActiveCluePresenter.cs:498-507`). |
| Reduced motion / reduced flash | MISSING | No setting; flashes and shakes are always on (`DrawingFeedback.cs`, `Feedback/CameraShakeController.cs`, `Feedback/DamageEdgeFlashController.cs`). |
| Left-handed trace pad | MISSING (arguably N/A) | Drawing surface is the whole screen (`StrokeCapture.cs:353-361`); no pad placement option. |
| Status shown by text, not colour alone | PARTIAL | Hearts: colour only, red vs faded white (`HeartDisplay.cs:208`), no numeral during play; defeat screen shows "n/3" text (`DefeatScreenUI.cs:57-58`). Locked levels: lock icon + grey tint + text on press (`LevelButton.cs:78-103`, `LevelLockNoticePanel.cs`). Locked Almanac cells: '?' frame (`AlmanacCell.cs:38`). Dojo pass/fail: colour plus text (`FeedbackToast.cs:38-44`). Save status: no indicator at all. |
| Larger heart icons / scalable UI | MISSING | No setting; `SafeAreaHandler` and `CanvasScaler` handle aspect only. |
| Font size / high contrast / text speed / haptics / language | MISSING | Not found. Typewriter speed is a serialized constant (`DialogueController.cs:27`). |

### 5.4 Code that exists but appears nowhere in the spec

- **Endless Mode** button and unlock flag (`MainMenuUI.cs:43-48, 159-169`; `ProgressManager.cs:627-640`). Button is inert (QA 2.3).
- **Tracing Dojo** free-practice scene (`UI/TracingDojo/*`). Closest spec analogue is UF-40 "Practice Symbol" from the codex.
- **Sandbox Mode** developer surface (`Debug/Sandbox/*`, `MainMenuUI.cs:203-288`), editor/sandbox builds only.
- **Almanac Enemies tab**, enemy discovery overlays and progress (`UI/Almanac/*`, `UI/EnemyDiscoveryOnboardingController.cs`, `Gameplay/Enemy/EnemyDiscoveryProgress.cs`, `Core/BossDiscoveryProgress.cs`).
- **Focus Mode** slow-time on combo threshold (`ComboManager.cs:229-261`; `Data/GameConfigSO.cs:12-19`) and the Level 2 combo/focus onboarding beats (`Gameplay/Tutorial/Onboarding/Beats/ComboTeachBeat.cs`, `FocusModeTeachBeat.cs`).
- **Mass-clear / AOE chain attack** when three or more matching enemies are on screen (`CombatResolver.cs:147-187`; `UI/HUD/MassClearBadge.cs`).
- **Enemy signature abilities**: decoy enemies that cost a heart when drawn (`CombatResolver.cs:339-349`, `EnemyDataSO.cs:40`), phaser invisibility, glyph cover, badge scrambling, mirror decoys, zigzag movers, speed auras (`Gameplay/Enemy/*`).
- **Boss vulnerability-window mechanic** with summon phases, draw counters, and timer bars (`Gameplay/Boss/*`, `UI/Boss*`), and three legacy bosses (El Inquisidor, Superintendent, Kadiliman).
- **Legacy colonial era themes** and enemy `Era` enum (`Data/EraThemeSO.cs`, `EnemyDataSO.cs:192-197`).
- **Character-unlock reveal** modal at level start (`Gameplay/CharacterUnlockRevealController.cs`).
- **Level 1 onboarding beats** (protagonist intro, base intro, solo teach, heart-loss demo, release) with skip and resume (`Gameplay/Tutorial/Onboarding/*`, `OnboardingPersistence.cs`).
- **Learning evidence, mastery states, review scheduler, practice priority** (`Data/Learning/*`).
- **Atomic save journal, migration from legacy PlayerPrefs, quarantine, recovery notices** (`Data/Persistence/*`, `UI/CampaignSaveNoticePanel.cs`, `UI/CampaignOutcomeSaveFailurePanel.cs`).
- **Credits panel** (`UI/CreditsPanel.cs`), no menu entry.
- **Recognition attempt CSV logger** (`Analytics/RecognitionLogger.cs`).

### 5.5 Where the code is better than the spec (recommend spec updates)

1. **Save integrity.** The revised path writes to a temp file, reads it back, backs up the primary, promotes, re-validates, and rolls back on failure; level outcomes are journaled and replayed on next launch if the commit fails (`CampaignSaveCommitter.cs:60-123`; `CampaignOutcomeCoordinator.cs:23-58, 81-84`). The spec's "Safe Saving" row should adopt this as the required behaviour and add the recovery-notice screens (`CampaignSaveNoticePanel`, `CampaignOutcomeSaveFailurePanel`) as UF rows.
2. **Flow machine.** `LevelFlowMachine` makes "combat alone cannot complete" a structural property rather than a rule to remember. The spec should name the nine phases (LF-CONTRACT-v2) and state that Results is reachable only via an accepted save.
3. **Deterministic active-clue selection** that freezes during a trace so a faster enemy cannot steal the mark mid-draw (`ActiveClueDirector.cs:98-167`, `ActiveClueSelector.cs`). Worth writing into Core Mechanics "Enemy target".
4. **Recognizer confidence is hidden from the player** and a rejected stroke is never named as a character (`DrawingFeedbackVocabulary.cs:12-24`). The spec's UF-20 should state this explicitly.
5. **Four-dimension mastery model with delayed-retrieval states** (`MasteryEvaluator.cs`) is a stronger definition of "mastery" than the Character Mastery sheet's one-line rules. Recommend the codex spec (UF-40) adopt Form/Sound/Assembly/Meaning bars.
6. **Reset Journey copy** lists exactly what is cleared and kept (`ResetJourneyFlow.cs:16-23`), matching UF-07 well; keep it as the canonical wording.
7. **Learning-first stars**: three stars require tracing and context accuracy, not just hearts (`LevelResultsCalculator.cs:46-50`, `docs/design/scoring-and-stars.md`). The spec's UF-28 lists metrics but no formula; recommend adopting this one.

---

## 6. Open Questions (consolidated)

### Rulings received 2026-09-11 (from the team)

| Q | Ruling | Effect on backlog |
|---|---|---|
| Q1 | **YA** ends the campaign (spec flow wins). | T14/T41 validate YA into MALAYA; `CampaignConfigValidator.cs:652-661` must stop forcing PA; `Level15_Config.finalRestorationValue` → YA. |
| Q2 | **Firm on 18 characters**: DA and RA become separate characters, each with its own asset and enemy ability (to be produced). Spec Game Overview ("17 visual characters"), the content matrix, and the code's 17-symbol model all change. | T05 flips: keep `Char_RA`, give it a `stableId`, `firstIntroductionLevelId`, spoken value, badge art. Code impact: `ContentIdentity.RevisedSymbolIds` (17→18) and `RevisedDaraSymbolId`, `CampaignConfigValidator.cs:157-233` (symbol count, DARA rule), `BaybayinIdCanonicalizer` RA→DA fold (remove), `TemplateLoader` grouping, Level 11/13 decompositions. T09 no longer needs a context-selected RA. |
| Q3 | Level 1 final syllable is **MA**; fix the asset. | T02 confirmed. |
| Q4 | **Confirmed.** Era paragraph required on Levels 5, 10, 15; earlier era words are review blanks, the two new words are the taught slots. | T34, T37, T40 author full paragraphs; T18 must support ParagraphRestoration with checkpoints. |
| Q5 | **Confirmed: follow the workbook.** El Inquisidor and Superintendent are legacy mechanics to retire. Levels 5 and 10 are mixed, armored waves alternating with paragraph checkpoints; word formation drives the level; Paglimot on Level 15 is the only boss. | T35 removes the boss configs from Levels 5 and 10; legacy boss assets become unused. |
| Q6 | **Confirmed: New Journey lives in Settings** behind the confirmation panel (as the code already does), not on the main menu, to avoid accidental taps. Spec rows UF-06/UF-07 to be updated; the confirmation still lists what is reset and kept, then routes to the prologue. | T45 drops the main-menu New Journey button; keeps progress display, Exit, Archive. |
| Q7 | Typo in Character Mastery (A in PAMANA). | T59 fixes the cell. |
| Q8 | **Confirmed.** Continue goes straight into the next incomplete level, and once mid-level checkpoints persist, resumes inside it. | UF-03 next screen = level, not hub. T44 (hub) drops in priority. |
| Q9 | **Confirmed.** Restorable tokens are root words (SAMA, UNA); affixes and reduplication are fixed text around the blank. | T37, T40. |
| Q10 | Per-word context images are **in scope for the demo**. | T30 and T47 become demo-required. |
| Q11 | Yes, paired enemies can remove two hearts in one event. | No code change; document in Level 4 waves. |
| New | Levels are presented as **Era N, Level 1 to 5** everywhere, never 1 to 15. | New task T60. |

| Q12 | **Yes**, wire the revised save now. | T03 + T07 already carry it (demo-required). |
| Q13 | The **whole-screen drawing surface is the trace pad**. | T53 shrinks to a haptics toggle; the spec's "left-handed trace pad" row is amended to N/A. |
| Q15 | **The spec's "Combat and Archer System" and the Level Flow "Combat Flow" column are withdrawn.** Replaced by the corrupted-enemy design in §6.1. Bow/arrow, lanes, and forced single-active-clue targeting are no longer requirements. Combo rewards and Focus Mode: **still pending a keep-or-cut call** (in neither version). Endless Mode and the Almanac Enemies tab: **pending**. | T48, T49 dropped; T15 reframed as a per-level choice; T24 held; new T61, T62. |
| Q16 | **English** copy is acceptable. | T54 becomes "finalize English copy"; no language setting. |

Still open: Q14 is moot under Q15; Q15 remainder (combo/Focus Mode, Endless Mode, Almanac Enemies tab); the two conflicts in §6.1.

### 6.1 Combat design received 2026-09-11 (supersedes the workbook's combat rows)

**Model.** Every Baybayin symbol has a corrupted enemy created by Paglimot that embodies the opposite of the symbol's lesson. Juan defeats it by tracing the correct symbol; it dissolves into ink and the meaning returns to the Scroll. Era grouping: Ugat = A, E/I, O/U, BA, MA, NA; Ugnayan = KA, GA, SA, TA, WA; Pamana = DA/RA, HA, LA, NGA, PA, YA (YA on the final stage).

**What the code already has.** All 17 corruption enemies exist as `Assets/ScriptableObjects/Enemies/EnemyData_*.asset` with the exact appearance and ability text from this design in their `description` field, and `Enemy.Initialize` attaches ability components from data flags (`Assets/Scripts/Gameplay/Enemy/Enemy.cs:218-222`). The legacy combat path (draw the glyph of any on-screen enemy, `CombatResolver.cs:127-199`) already matches "defeat the enemy by tracing the correct symbol". Prefab art is missing for 13 of the 17 (QA B-02, backlog T33).

| Symbol | Enemy | Asset | Designed ability | Code today | Status |
|---|---|---|---|---|---|
| A | Abo ng Simula | `EnemyData_AbongSimula` | Covers the first symbol of a word with ash | hp 1, no ability flag | MISSING ability |
| E/I | Iligaw | `EnemyData_Iligaw` | Changes direction, creates false copies of the correct symbol | `spawnsMirrorDecoy` (`Gameplay/Enemy/MirrorDecoyController.cs`) | DONE (decoy copy); direction change via zigzag fields, not set |
| O/U | Uhaw | `EnemyData_Uhaw` | Drains sound from nearby words | hp 2 | MISSING ability |
| BA | Bakod | `EnemyData_Bakod` | Blocks paths, separates | hp 1, slow (0.85) | MISSING ability |
| KA | Kadena | `EnemyData_Kadena` | Binds villagers | hp 2 | MISSING ability |
| DA/RA | Daan-Lihis | `EnemyData_Daan-Lihis` | Redirects paths, sends Juan back | hp 1 | MISSING ability |
| GA | Gapos | `EnemyData_Gapos` | Traps hands, slows tracing | hp 2 | MISSING ability (a longer stroke window is the natural hook: `StrokeCapture.cs:215`) |
| HA | Hati | `EnemyData_Hati` | Splits into two smaller enemies | hp 1 | MISSING ability |
| LA | Labo | `EnemyData_Labo` | Hides symbols, faces, paths | `isPhaser` (`Gameplay/Enemy/PhaserEnemy.cs`) | DONE (self-hiding) |
| MA | Mantsa | `EnemyData_Mantsa` | Stains correct symbols into incorrect forms | `stainsNearbyGlyphs` (`Gameplay/Enemy/KempeiScrambleController.cs`) | DONE |
| NA | Nawalang Mukha | `EnemyData_NawalangMukha` | Removes names from characters and dialogue | hp 1 | MISSING ability |
| NGA | Ngatngat | `EnemyData_Ngatngat` | Eats letters and inscriptions | hp 1, fast (1.9) | MISSING ability |
| PA | Punit | `EnemyData_Punit` | Tears sentences into pieces | hp 1, fast (1.7) | MISSING ability |
| SA | Salungat | `EnemyData_Salungat` | Reverses commands, allies hit the wrong target | `isDecoy` (drawing it costs a heart, `CombatResolver.cs:339-349`) | PARTIAL |
| TA | Takip | `EnemyData_Takip` | Hides correct answers, covers objects | `coversOwnGlyph` (`Gameplay/Enemy/GlyphCoverController.cs`) | DONE |
| WA | Walang-Awa | `EnemyData_Walang-Awa` | Armored, prevents healing/helping | hp 3 | PARTIAL (armor only) |
| YA | Yapos ng Dilim | `EnemyData_YaposngDilim` | Traps the final symbol of SAYA, HARAYA, MALAYA; reveals the next Salinlahi | hp 3 | MISSING ability and story beat |

**Conflicts to resolve (not decided here):**

| # | Conflict | Question |
|---|---|---|
| C1 | This roster has one enemy for "Da or Ra" and its lore says the two "share one ancestral Baybayin form". Ruling Q2 says 18 separate characters. | Does RA get its own enemy, or does Q2 revert to 17 characters? |
| C2 | Roster eras: O/U in Ugat, TA in Ugnayan, YA in Pamana. Spec era pools and all level configs: TA taught at Ugat Level 2, YA at Ugnayan Level 8, O/U at Ugnayan Level 9. An enemy cannot appear before its symbol is taught. | Move the symbols' first-introduction levels, or move the enemies' "best era"? |
| C3 | The roster covers 17 symbols but the design says "18 characters" (Q2). | Same as C1. |
| C4 | Enemy `era` field is the legacy `Spanish/American/Japanese` enum (`EnemyDataSO.cs:73-75, 192-197`) and the Almanac only reveals Spanish-era entries (`AlmanacController.cs:159-162, 252`). | Replace with Ugat/Ugnayan/Pamana. |

**Rulings on the conflicts (2026-09-11, second round):**

| # | Ruling | Effect |
|---|---|---|
| C1 / C3 | **DA and RA are separate characters, each with its own enemy.** Daan-Lihis stays with DA; an RA enemy is to be designed (appearance, ability, lore). 18 characters, 18 enemies. | T05 (RA character), new T64 (RA enemy design and asset). |
| C2 | **Move the enemies to match the spec's era pools.** One syllable, one character: an enemy keeps the same asset and abilities everywhere. Corrected grouping: Ugat = A, E/I, BA, MA, NA, **TA (Takip)**; Ugnayan = **O/U (Uhaw)**, KA, GA, SA, WA, **YA (Yapos ng Dilim)**; Pamana = DA, RA, HA, LA, NGA, PA. Yapos ng Dilim's final-stage role (guarding YA in MALAYA) can still be used in Level 15 because YA is in the pool by then. | T62 authors `era` per this grouping; symbol introduction levels unchanged. |
| C4 | **Replace the enemy `era` enum with Ugat/Ugnayan/Pamana.** | T62. |
| Q15 remainder | **Cut combo powers, Focus Mode, and Endless Mode.** Keep the Almanac Enemies tab, updated to the 18 characters and their enemies. | T24 dropped; new T63 (removal); T56 gains the Enemies tab refresh. |

**Third-round rulings (2026-09-11):**

| Topic | Ruling | Effect |
|---|---|---|
| Language | English is **UI copy only**; story dialogue, focus-word explanations, and cutscenes stay Filipino. | T54 scope; T36/T39 author Filipino. |
| Level 15 | **Keep the Paglimot boss** and fold the mixed waves and era paragraph into it: one wave per phase (Ugat, Ugnayan, all), one paragraph line restored per phase, YA into MALAYA last, per-phase checkpoint. | T41 rewritten. |
| Hub (UF-08) | **Still wanted.** | T44 kept. |
| Tracing Dojo | **Fold free practice into the Codex**; remove the main-menu button. | T56. |
| Memory art | **In scope**; the team will produce memory card and cutscene panel art. | T26, T47 required. |
| Existing progress | Recommendation accepted by default: archive old PlayerPrefs progress and start a fresh journey with the migration notice (current code behaviour). | T07 unchanged. |
| Demo target | **All 15 levels.** | Every content, flow, and combat task is demo-required; only polish/accessibility tasks stay optional. |
| RA recognition | Team confirms the recognizer can separate RA from DA. Recorded as a confirmation, not verified in this session; T05 keeps the evaluator check as acceptance. | T05. |

### 6.2 Plan review before ticket creation (2026-09-11)

| # | Issue found | Where | Fix applied |
|---|---|---|---|
| R1 | T07 (wire the save) was silently blocked by Level 13: empty focus words, pool, requirements, and final value are each a validator Error, and T38 depends on the RA split. | `CampaignConfigValidator.cs:398-403, 501-505, 546-553, 644-650`; `CampaignSaveService.cs:62-68` | T03 widened: identity errors block, content-completeness issues are Warnings until a strict-mode switch. |
| R2 | T11 asked the validator to make a missing challenge/reward an Error, which re-blocks boot until Levels 6 to 15 are authored, contradicting T03. | same | T11 is runtime guard only; Error deferred to the strict switch. |
| R3 | T12 ("Instruction only for new symbols") empties `learningRequirements` on Levels 3, 4, 5, 10, 14, which the validator rejects. | `CampaignConfigValidator.cs:501-505` | Review symbols stay in the list as Practice kind. |
| R4 | "Combat alternates with paragraph blanks" (L5, L10, L15 phase lines) has no engine support; the flow runs Defense once then ContextChallenge once. T35, T37, T40, T41 assumed it. | `LevelPhasePlan.cs:90-103`; `LevelFlowMachine.cs:41-66` | New T66; T35, T37, T40, T41 blocked by it. |
| R5 | T05, T08, T63 change the save schema; `CampaignSaveValidator` rejects unknown symbol ids, so existing dev saves would block boot. No migration task existed. | `CampaignSaveDocument.cs:7`; `CampaignSaveMigrator.cs` | New T67. |
| R6 | Seven designed enemy abilities act on things that do not exist during combat (paths, villagers, dialogue names, documents, sentences). | §6.1 table | T61 gains a design prerequisite: per-enemy combat-time rule sheet. **Design decision needed.** |
| R7 | Paragraph restoration with Baybayin syllable tiles puts 14+ tiles on a phone screen for Level 5. | T18/T34 | Recommend word tiles for paragraph units, syllable tiles for word units, one `ChallengeUnit` per checkpoint line. **Design decision needed.** |
| R8 | RA introduction level undecided (Level 11 with DA, or Level 13 with HARAYA). `RevisedSymbolIds` last element is the finale symbol, so RA must not be appended last. | `ContentIdentity.cs:20-34` | Added to T05; recommend Level 13. **Design decision needed.** |
| R9 | Ruling Q1 (YA finale) had no owning task; T56 depends on the higher-numbered T62. | `CampaignConfigValidator.cs:652-661` | Q1 code changes added to T05; T56 exception documented. |
| CSV | Jira's importer cannot read `T02;T03` from one cell, and semicolon-joined labels import as one label. | `jira-import.csv` | Restructured: `Issue Id` column plus `Blocked By 1..3` (map to the Blocks link type), labels space-separated. |

### 6.3 Combat-time rule sheet for the enemy abilities (R6, accepted 2026-09-11)

Each designed ability is translated into an effect on one of the four things that exist during combat: the active clue, the wave, the trace, or the restoration that follows. Hooks name the code that already exposes the seam.

| Enemy | Designed ability | Combat-time rule | Hook | Note |
|---|---|---|---|---|
| Abo ng Simula (A) | Covers the first symbol of a word with ash | While alive, the clue's incomplete-word text masks the **first** syllable as well as the target one, and the "Restored: WORD" cue shows the word with its first letter ashed for 1 s | `ActiveCluePresenter.BuildMaskedSpelling` (781-799), `ShowWordRestoredCue` | Clue stays solvable via glyph badge. |
| Uhaw (O/U) | Drains sound from nearby words | While alive, spoken clues and the replay button are muted; the visual fallback channel is forced on | `ActiveCluePresenter.UpdateCluePanel` (527-544), `ClueChannelResolver.Resolve` | Also mutes pronunciation on hit until it dies. |
| Bakod (BA) | Blocks paths, separates | Spawns front-and-centre and shields every enemy behind it: they cannot be marked or damaged until Bakod falls; Bakod itself moves at 0.85 speed | `ActiveClueDirector.IsEligibleClue` (196-215), `CombatResolver.IsEligibleCombatTarget` (311-328) | Enemies queue behind it (spawn order sorted fastest-first already). |
| Kadena (KA) | Binds villagers | On spawn, chains the nearest other enemy; the chained enemy is invulnerable (badge shows a chain) until Kadena is defeated | `Enemy.TakeDamage` guard, `EnemyGlyphBadge.SetCovered`-style overlay | Kadena keeps hp 2. |
| Daan-Lihis (DA) | Redirects paths, sends Juan back | Reaching the base costs no heart; instead the current wave's spawn count rewinds by two (two more enemies respawn) | `EnemyDataSO.dealsContactDamage=false`, `WaveManager._currentWaveSpawnedCount` (465-468), `WaveSpawner.SpawnWave` offset | Only enemy whose breach is not a heart. |
| Gapos (GA) | Traps hands, slows tracing | While alive, recognition submits 0.6 s later than normal (multi-stroke window extended) and the stroke line renders with a short lag | `StrokeCapture.StartMultiStrokeTimer` (363-368), `RecognitionConfigSO.multiStrokeWindowSeconds` | Same trace accuracy; less time. |
| Hati (HA) | Splits into two smaller enemies | On first hit it splits into two half-scale enemies, each carrying a **different** symbol from the level pool; both must be traced | `Enemy.TakeDamage` non-lethal branch (356-370), `hurtSwapsCharacter/postHurtCharacter`, `WaveSpawner.RestoreEnemy` | Replaces hp 2. |
| Labo (LA) | Hides symbols, faces, paths | Existing: phases invisible; while invisible it is not targetable | `PhaserEnemy` | Built. |
| Mantsa (MA) | Stains correct symbols | Existing: nearby badges show scrambled glyphs | `KempeiScrambleController` | Built. |
| Nawalang Mukha (NA) | Removes names | While alive, the clue panel's Latin text and the "Restored: WORD" label are blanked ("______"); only the glyph and audio remain | `ActiveCluePresenter.SetClueText` (735-753), `HandleActiveClueResolved` (564-580) | Removes the "name" of the word. |
| Ngatngat (NGA) | Eats letters | Every 3 s it eats one more letter from the incomplete-word clue until only the glyph badge is left; letters return when it dies | `ActiveCluePresenter.SetClueText`, a per-enemy timer | Fast (1.9). |
| Punit (PA) | Tears sentences apart | If it reaches the base, in addition to the heart, the next restoration unit opens with **Reduced** clue policy and one already-locked paragraph line reopens | `ChallengeSession._cluePolicy`, `ChallengeSession.ResetToCheckpoint` (299-317) | Ties combat to restoration; fits Levels 12 to 15 only. |
| Salungat (SA) | Reverses commands | Existing decoy: drawing its glyph costs a heart. **Add:** its badge shows the glyph mirrored so a careful player can tell | `CombatResolver.ResolveMatchedEnemy` (339-349), `EnemyDataSO.isDecoy` | Built plus tell. |
| Takip (TA) | Hides correct answers | Existing: covers its own badge, reveals briefly | `GlyphCoverController` | Built. |
| Walang-Awa (WA) | Armored, prevents helping | Existing hp 3. **Add:** while alive, heart loss cannot be prevented by any effect and the base-hit flash is stronger | `HeartSystem.LoseHeart` | Armor built; add the "no mercy" tell. |
| Yapos ng Dilim (YA) | Traps the final symbol | Carries the level's final syllable (slot-2 word) and must be the last enemy standing in its wave; the final-syllable step (T14) is locked until it is defeated; on death the trapped child is revealed | `LevelConfigSO.finalRestorationValue`, `ActiveClueSelector` (force lowest priority), T14 gate | Level 8 onward; story reveal at Level 15. |
| Iligaw (E/I) | Direction change, false copies | Existing mirror decoy; **add** zigzag movement via `zigzagAmplitude/Frequency` already on `EnemyDataSO` | `MirrorDecoyController`, `PensionadoMover` | Data only. |
| RA enemy | To be designed (T64) | Should follow the same pattern: one clue, wave, trace, or restoration effect | | Ruling C1. |

**Effect on earlier statuses.** UF-17 to UF-20 and Core Mechanics "Combat and Archer System" rows are no longer measured against bow/arrow, lanes, or single-active-clue. Under the new design the legacy combat path is DONE for targeting; active-clue mode stays available as an optional per-level difficulty tool. Lore, "restored lesson", and "corrupted meaning" text are new content requirements for the enemy discovery overlay and Almanac (`UI/EnemyDiscoveryOnboardingController.cs`, `AlmanacDetailScroll.cs`); only appearance and ability text is authored today.

Q1 to Q11 above, plus:

- **Q12** Should the revised campaign save be wired now (making `CampaignConfigValidator` a boot gate), or should media/roster validation be downgraded to Warning until content lands? The current test suite treats media as "deferred" but the runtime does not.
- **Q13** Is the whole-screen drawing surface the intended "trace pad", making the left-handed option unnecessary, or is a bounded pad required?
- **Q14** The spec says Juan uses a bow and arrows; the built attack is a slash VFX. Change art, or change spec?
- **Q15** Endless Mode, Focus Mode, mass-clear, enemy signature abilities, and the Almanac Enemies tab are substantial built features absent from the spec. Keep (and document) or cut?
- **Q16** The lock-notice and hint copy are marked placeholder / not product-approved in code. Who owns final Filipino copy, and is English acceptable for the demo?

---

## 6.4 Jira reconciliation rulings (2026-09-12)

Recorded from the team's answers to the ten Open Questions in `docs/audit/JIRA-MERGE.md`. Full consequence table there.

| # | Ruling (2026-09-12) | Consequence |
|---|---|---|
| OQ-1 | **(b) Two blanks in Ugat Levels 3 and 4.** The workbook is the newest source: `SALINLAHI_Complete_User_Flow.xlsx` was last modified 2026-09-11 12:02 UTC; the one-blank change landed 2026-09-01 (`793bcd85` "SALIN-144 restore one word", `df18c2a7`), and the narrative doc it cites was last edited 2026-09-06 (`9ca01475`) and still reads "Isang salita lamang ang kulang" at `docs/content/ugat-levels-2-5-narrative.md:137`. | T34 stays folded into SALIN-144–147 but its delta is now: re-amend SALIN-144 and SALIN-146 back to two blanks, rewrite the L3 context line at `ugat-levels-2-5-narrative.md:137` (and the matching L4 line) so the copy no longer says one word is missing, and re-author `Challenge_Ugat03_Context` / `Challenge_Ugat04_Context` with two blanks. The 2026-09-01 amendment on SALIN-144/146 is superseded. |
| OQ-2 | **(a)** Rescope SALIN-184 to Level 15 only; edit SALIN-147 and SALIN-152 AC from "three-phase Paglimot extension" to mixed waves alternating with paragraph lines (ruling Q5). SALIN-169 (Done) stays as history. | T35 unchanged; SALIN-203/190 regression checklists drop "Paglimot phases" for Levels 5 and 10. |
| OQ-3 | **Checkpoint on every completed syllable.** Save and Exit and relaunch resume at the last completed syllable placement in restoration (and at the last cleared wave in defense). | T58 acceptance amended below; SALIN-165's AC "restarts that level from a clean attempt rather than loading a partial checkpoint" must be amended to allow the saved syllable/wave checkpoint. T20/T21 checkpoint boundaries align to the same rule. |
| OQ-4 | **(a)** Leave Done stories closed; audit tickets carry `Relates` links (as in the CSV). Four contradicted items import as Bug (T02, T04, T11, T12). | No reopen writes needed. |
| OQ-5 | **(a)** No lanes, powers, armour tiers, Focus Mode, or Endless (ruling Q15). Rescope SALIN-182 to the enemy rule sheet (AUDIT.md §6.3) and close SALIN-70 as Won't Do. | T15, T61, T63 unchanged. |
| OQ-6 | **DA and RA are separate.** Amend SALIN-155's second AC ("DA/RA shares one basic character") to the 18-character model with RA introduced at Level 13. | T38, T05 unchanged. |
| OQ-7 | **(a)** Era slices import as standalone Tasks linked to SALIN-172/173 (the SALIN-204–206 precedent). | CSV unchanged. |
| OQ-8 | Informational only: the question was whether board 2 is a Scrum board (has sprints) or a Kanban board (no sprints). Sprints exist on issues, so Scrum is assumed; nothing changes unless the board is Kanban, in which case the Sprint column is ignored on import. | None. |
| OQ-9 | **(a)** Foundation tickets go to SALIN Sprint 8. | CSV unchanged. |
| OQ-10 | **(a)** T19 imports as a sibling Results slice linked to SALIN-183. | CSV unchanged. |

---

## 7. Files opened for this audit

**Spec:** all 10 sheets of `SALINLAHI_Complete_User_Flow.xlsx`.

**Runtime C# (read in full unless noted):**
`Assets/Scripts/Core/BootstrapLoader.cs`, `GameManager.cs`, `SceneLoader.cs`, `SaveManager.cs`, `ProgressManager.cs`, `EventBus.cs`, `RecognitionManager.cs`, `CharacterUnlockProgress.cs`, `AudioManager.cs` (grep only);
`Assets/Scripts/Data/LevelConfigSO.cs`, `EraConfigSO.cs`, `BaybayinCharacterSO.cs`, `ChallengeSequenceSO.cs`, `EnemyDataSO.cs`, `BossConfigSO.cs`, `BossPhase.cs`, `CutsceneSO.cs`, `DialogueSO.cs`, `LevelCutsceneMappingSO.cs`, `WaveDefinition.cs`, `GameConfigSO.cs`, `RecognitionConfigSO.cs`, `OnboardingSequenceSO.cs`, `Level1TutorialSequenceSO.cs`;
`Assets/Scripts/Data/Campaign/CampaignConfigSO.cs`, `FocusWordDefinition.cs`, `SpokenValueDefinition.cs`, `SpokenValueResolver.cs`, `ClueChannels.cs`, `ClueChannelResolver.cs`, `CampaignTuning.cs`, `ContentIdentity.cs`;
`Assets/Scripts/Data/Learning/LevelResultsCalculator.cs`, `LevelRewardResolver.cs`, `MasteryEvaluator.cs`, `MasteryDimensions.cs`, `LearningTuningSO.cs`;
`Assets/Scripts/Data/Persistence/LevelLockResolver.cs`, `CampaignOutcomeCoordinator.cs`, `JourneyEntryResolver.cs`, `CampaignSaveDocument.cs`, `CampaignProgressOutcome.cs`, `CampaignSaveService.cs`, `CampaignSaveCommitter.cs`, `CampaignSaveRecoveryResolver.cs`, `CampaignProgressRepository.cs`, `ICampaignSaveStorage.cs`;
`Assets/Scripts/Data/Validation/CampaignConfigValidator.cs`, `ContentValidationIssue.cs`;
`Assets/Scripts/Gameplay/LevelFlowController.cs`, `ChallengeFlowController.cs`, `ChallengeSession.cs`, `ChallengeModeUI.cs`, `ChallengeTierPolicy.cs`, `CharacterUnlockRevealController.cs`;
`Assets/Scripts/Gameplay/Flow/LevelFlowMachine.cs`, `LevelPhase.cs`, `LevelPhasePlan.cs`;
`Assets/Scripts/Gameplay/Wave/WaveManager.cs`, `WaveSpawner.cs` (grep);
`Assets/Scripts/Gameplay/Combat/CombatResolver.cs`, `ActiveClueDirector.cs`, `ActiveClueSelector.cs`, `LevelConfigClueObjectiveSource.cs`, `ComboManager.cs`, `ComboPower.cs`;
`Assets/Scripts/Gameplay/Base/HeartSystem.cs`, `PlayerBase.cs`;
`Assets/Scripts/Gameplay/Enemy/EnemyMover.cs` (55-110), `Enemy.cs` (grep), `EnemyGlyphBadge.cs` (grep);
`Assets/Scripts/Gameplay/Boss/BossController.cs` (125-200, 300-337, grep);
`Assets/Scripts/Gameplay/Protagonist/ProtagonistAttackController.cs`, `ProtagonistSlashVfx.cs` (grep);
`Assets/Scripts/Gameplay/Recognition/StrokeCapture.cs`, `TemplateLoader.cs`, `StrokeValidation.cs`, `DollarPRecognizer.cs` (1-120);
`Assets/Scripts/Gameplay/Drawing/DrawingCanvas.cs`;
`Assets/Scripts/Gameplay/Tutorial/ChallengeRuntimeState.cs`, `TutorialRuntimeState.cs`, `Level1TutorialStep.cs`, `Level1TutorialEnemyController.cs`, `Onboarding/OnboardingPersistence.cs`, `Onboarding/OnboardingContext.cs`, `Onboarding/Level1OnboardingController.cs` (140-232, grep);
`Assets/Scripts/UI/MainMenuUI.cs`, `LevelSelectUI.cs`, `LevelButton.cs`, `LevelLockNoticePanel.cs`, `PauseMenuUI.cs`, `SettingsPanel.cs`, `VictoryScreenUI.cs`, `DefeatScreenUI.cs`, `GameOverUI.cs`, `ResetJourneyFlow.cs`, `ResetJourneyConfirmationPanel.cs`, `CampaignSaveNoticePanel.cs`, `CampaignOutcomeSaveFailurePanel.cs`, `CutscenePlayer.cs`, `DialogueController.cs`, `LevelTutorialProgress.cs`, `HUD.cs`;
`Assets/Scripts/UI/HUD/ActiveCluePresenter.cs`, `TraceHintPresenter.cs`, `SymbolLearningCardController.cs`, `FocusWordPreviewController.cs`, `ComboDisplay.cs`, `WaveDisplay.cs`, `DrawingFeedback.cs`, `DrawingFeedbackVocabulary.cs`, `FocusModeIndicator.cs`, `MassClearBadge.cs`, `HeartDisplay.cs` (grep);
`Assets/Scripts/UI/Almanac/AlmanacController.cs`, `AlmanacCell.cs`, `AlmanacDetailScroll.cs`;
`Assets/Scripts/UI/TracingDojo/TracingDojoController.cs`, `TracingDojoEvidence.cs`, `GhostStrokeRenderer.cs`, `DojoNavigator.cs`, `FeedbackToast.cs`, `CharacterListPopulator.cs`;
`Assets/Scripts/Analytics/RecognitionLogger.cs` (grep).

**Assets (parsed):** all 15 `Assets/ScriptableObjects/Levels/*.asset`; all 11 `Assets/ScriptableObjects/Challenges/*.asset`; all 18 `Assets/ScriptableObjects/Characters/Char_*.asset` and `CharacterRegistry_Default.asset`; `Campaign/CampaignConfig_RevisedV1.asset`; `Themes/Era_01..03.asset`; all `Dialogue/*.asset`; all `Cutscenes/*.asset`; `Enemies/Boss Configs/*.asset`; all `Enemies/EnemyData_*.asset`; `Prefabs/Managers/[Manager] SaveManager.prefab` (grep).

**Scenes:** `Assets/_Scenes/*.unity` (script-GUID wiring scan); `Bootstrap.unity` (SaveManager block); `LevelSelect.unity` (era GUIDs); `ProjectSettings/EditorBuildSettings.asset`.

**Art / audio / templates (directory listings):** `Assets/Resources/Templates`, `Assets/Audio/Pronunciation`, `Assets/Art/UI/GlyphBadges`, `GlyphOutlines`, `Almanac`, `level*.png`.

**Docs:** `AGENTS.md`, `docs/design/scoring-and-stars.md`, `docs/system/06_UI_UX_and_Player_Flow.md` (1-200), `docs/backlog/current-user-stories.md`, `docs/jira/README.md`, `docs/jira/TEAM-HANDBOOK.md` (1-120), `docs/content/level-01-asset-manifest.md`, `level-01-narrative.md`, `ugat-levels-2-5-narrative.md`, `ugnayan-levels-6-10-narrative.md`, `pamana-levels-11-15-narrative.md` (headers and open-decision sections), `docs/technical/TW-SPK-004-educational-content-matrix.md` (1-140), `docs/review/manual-qa-2026-09-05.md` (1-330).

**Tests (names and selected asserts):** full listing of `Assets/Tests`; `Editor/Data/RevisedCampaignAssetTests.cs`, `Editor/Data/Level1AssetReadinessTests.cs` (grep).
