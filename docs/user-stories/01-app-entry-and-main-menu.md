# 01 — App entry and Main Menu

Prefix **`MM`**. Covers cold boot, the main menu, entering or restarting a journey, Settings, Credits, Exit, and the save notices the player meets at launch.

See [`00-overview.md`](00-overview.md) for the status legend and the Unclear register.

---

### MM-01 — Launch straight into the game
As a player, I want the app to finish loading on its own after I tap the icon, so that I reach the menu without touching anything.
- AC: `Bootstrap` loads, instantiates the manager singletons, waits one frame, then loads `MainMenu` with no player input.
- AC: The Bootstrap scene is never visible as a screen with controls.
- AC: A black fade covers the transition so the player never sees an unlit or half-loaded scene.
- System: Bootstrap · `BootstrapLoader`, `SceneLoader`
- Status: Existing
- Refs: `Assets/Scripts/Core/BootstrapLoader.cs`, `Assets/Scripts/Core/SceneLoader.cs` (`CreateFadeCanvas`), `docs/system/02_Architecture_and_Runtime_Flow.md` §2

### MM-02 — Have my journey loaded before the menu appears
As a player, I want my saved journey to be read before the menu is drawn, so that the menu already knows where I am.
- AC: `SaveManager.Initialize()` runs after the singletons awaken and before `SceneLoader.LoadMainMenu()`.
- AC: The save candidate is recovered, migrated to the current schema, and verified before the menu renders.
- AC: With no campaign asset assigned the game stays in Legacy mode and writes no revised save files.
- System: Core persistence · `SaveManager`, `CampaignSaveService`
- Status: Existing
- Refs: `Assets/Scripts/Core/SaveManager.cs`, `docs/system/03_Core_Systems.md` §2.8, SALIN-219

### MM-03 — Play offline
As a player, I want the whole game to work with no network connection, so that I can play anywhere and my data stays on my device.
- AC: No gameplay, save, or content path makes a network request.
- AC: Saves are written to `persistentDataPath` on the device only.
- System: Whole app
- Status: Existing
- Refs: `docs/capstone/GDD.md` §1.3, `docs/system/08_Mobile_Performance_and_Offline_Constraints.md`

### MM-04 — Play in portrait with one hand
As a player, I want to hold the phone in one hand in portrait, so that I can play comfortably on a phone.
- AC: Orientation is locked to portrait.
- AC: All interactive controls sit inside the 9:16 play column and inside the device safe area.
- System: Player settings · `AspectLockedCamera`, `SafeAreaHandler`
- Status: Existing
- Refs: `Assets/Scripts/UI/SafeAreaHandler.cs`, `Assets/Scripts/Gameplay/Camera/AspectLockedCamera.cs`

### MM-05 — Start a brand-new journey
As a first-time player, I want the menu to invite me to start, so that I know there is nothing to resume.
- AC: With no completed level the primary button reads **Start Journey**.
- AC: Pressing it routes to the prologue, then Era 1 Level 1.
- System: Main Menu · `MainMenuUI`, `JourneyEntryResolver` (`NewJourney`)
- Status: Existing
- Refs: `Assets/Scripts/UI/MainMenuUI.cs`, `Assets/Scripts/Data/Persistence/JourneyEntryResolver.cs`, SALIN-136

### MM-06 — Continue where I left off
As a returning player, I want one button that takes me back to where I stopped, so that I do not have to find my place.
- AC: With a journey in progress the primary button reads **Continue**.
- AC: Continue goes **straight into the next incomplete level**, not to a hub or a map (ruling Q8).
- AC: A journey with every configured level completed shows a review/replay destination and never a "next level" prompt.
- System: Main Menu · `JourneyEntryResolver` (`ContinueLevel`, `CompletedJourney`)
- Status: Existing
- Refs: `JourneyEntryResolver.cs`, `MainMenuUI.cs:89-99`, SALIN-255 (hub destination still To Do)

### MM-07 — Be stopped rather than silently reset when my save cannot be read
As a player whose save is damaged, I want the game to refuse to enter gameplay instead of restarting me at level 1, so that I do not mistake a broken save for lost progress.
- AC: An unreadable or incompatible save resolves to `JourneyEntryKind.Blocked`.
- AC: `Blocked` never falls back to level 1 and never enters gameplay.
- AC: The failed files are retained for diagnostics rather than deleted.
- System: Core persistence · `JourneyEntryResolver`, `CampaignSaveRecoveryResolver`
- Status: Existing
- Refs: `JourneyEntryResolver.cs`, `Assets/Scripts/Data/Persistence/CampaignSaveRecoveryResolver.cs`

### MM-08 — See my progress on the menu
As a player, I want the menu to tell me how far I have come, so that I can feel the journey accumulating.
- AC: The menu shows an era-relative progress line, e.g. "Ugat · Level 3".
- AC: The line never uses global level numbers 1–15.
- System: Main Menu · `MainMenuProgressLine`, `MainMenuProgressCopy`
- Status: Partial — the progress line is implemented; the percentage element of the spec line ("Ugat · Level 3 · 13%") is not confirmed and the ticket is still In Progress.
- Refs: `Assets/Scripts/UI/MainMenuProgressLine.cs`, SALIN-256 (In Progress), SALIN-258

### MM-09 — Open the Baybayin Codex from the menu
As a player, I want to browse the symbols and enemies I have met, so that I can study outside a level.
- AC: An **Almanac / Codex** button on the menu opens the Almanac scene.
- AC: A back control returns to the menu.
- System: Main Menu → Almanac · `MainMenuUI.OnAlmanacPressed`, `AlmanacController`
- Status: Existing
- Refs: `MainMenuUI.cs:184-188`, `Assets/_Scenes/Almanac.unity`

### MM-10 — Open the Memory Archive from the menu
As a player, I want to revisit the memories I have restored, so that the rewards stay meaningful after the level ends.
- AC: A **Memory Archive** button on the menu opens the archive overlay.
- AC: The button exists even though no archive scene does — it is cloned at runtime from the Settings button template.
- System: Main Menu → Memory Archive · `MemoryArchiveController`
- Status: Existing
- Refs: `Assets/Scripts/UI/MemoryArchiveController.cs`, `MainMenuUI.cs:19-20,192-198`, SALIN-240

### MM-11 — Open Settings from the menu
As a player, I want to reach Settings from the menu, so that I can adjust the game before I play.
- AC: A **Settings** button opens the settings overlay.
- AC: Closing it returns to the menu with the previous state intact.
- System: Main Menu · `SettingsPanel`
- Status: Existing
- Refs: `Assets/Scripts/UI/SettingsPanel.cs`

### MM-12 — Read the credits
As a player, I want to see who made the game, so that the team and asset authors are acknowledged.
- AC: A **Credits** control opens a credits panel listing the team and third-party asset credits.
- AC: The panel closes back to the menu.
- System: Main Menu · `CreditsPanel`
- Status: Existing
- Refs: `Assets/Scripts/UI/CreditsPanel.cs`, `docs/audio/audio-credits.md`

### MM-13 — Quit the game deliberately
As a player, I want an explicit way to leave the game, so that I am not forced to use the OS gesture.
- AC: An **Exit** button on the menu opens a confirmation.
- AC: Confirming quits the application on device; cancelling returns to the menu.
- AC: Exit never quits without confirmation.
- System: Main Menu · `ExitConfirmationPanel`
- Status: Existing
- Refs: `Assets/Scripts/UI/ExitConfirmationPanel.cs`, `MainMenuUI.cs:275-352`, SALIN-256

### MM-14 — Start over without wiping my save by accident
As a player, I want "New Journey" kept out of easy reach, so that I cannot destroy my progress with a stray tap.
- AC: New Journey / Reset Journey lives **only in Settings**, never on the main menu (ruling Q6).
- AC: It is gated behind a confirmation panel.
- System: Settings · `ResetJourneyFlow`, `ResetJourneyConfirmationPanel`
- Status: Existing
- Refs: `Assets/Scripts/UI/ResetJourneyFlow.cs`, `SettingsPanel.cs:28-30`

### MM-15 — Know exactly what a reset destroys before I confirm it
As a player, I want the reset confirmation to list what is erased and what is kept, so that I can decide with full information.
- AC: The confirmation names the progression that will be cleared (levels, symbols, memories, rewards).
- AC: It names what survives (audio preferences and other settings outside the campaign document).
- AC: Cancelling changes nothing on disk.
- System: Settings · `ResetJourneyConfirmationPanel`
- Status: Existing
- Refs: `Assets/Scripts/UI/ResetJourneyConfirmationPanel.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §7

### MM-16 — Get a clean journey after a reset, with the story from the start
As a player who resets, I want the game to behave like a fresh install, so that I can experience the whole journey again.
- AC: Reset issues a new `journeyGenerationId` and clears level, symbol, memory, reward and receipt progress.
- AC: The prologue plays again.
- AC: A pending outcome journal from the previous generation is quarantined as stale and can never be applied to the new journey.
- System: Core persistence · `CampaignOutcomeCoordinator`, `SaveManager.ResetJourneyAtomically`
- Status: Existing
- Refs: `Assets/Scripts/Core/SaveManager.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §7

### MM-17 — Be told once when my old progress was migrated
As a long-time player, I want a one-time notice explaining that my old progress was archived, so that a changed save format does not look like data loss.
- AC: An existing legacy journey is archived, a fresh revised journey is created, and a one-time migration notice is shown.
- AC: Audio preferences are preserved across the migration.
- AC: The notice does not reappear on later launches.
- System: Main Menu · `CampaignSaveNoticePanel`, `LegacyMigrationBuilder`
- Status: Existing
- Refs: `Assets/Scripts/UI/CampaignSaveNoticePanel.cs`, `Assets/Scripts/Data/Persistence/LegacyMigrationBuilder.cs`, SALIN-272

### MM-18 — Be told once when my save had to be recovered
As a player whose save was corrupt, I want a notice that recovery happened, so that I understand why my progress changed.
- AC: When no save candidate can be recovered the game creates a clean journey, retains the failed files, and shows a one-time recovery notice.
- AC: The notice text distinguishes a **superseded / newer** save from a **corrupt** one.
- System: Main Menu · `CampaignSaveNoticePanel`, `CampaignSaveNoticeCopy`
- Status: Existing
- Refs: `Assets/Scripts/UI/CampaignSaveNoticeCopy.cs`, SALIN-290, SALIN-227

### MM-19 — Be blocked, not reset, by a save from a newer version
As a player who downgraded the app, I want the game to refuse to load rather than overwrite my newer save, so that my progress survives.
- AC: A higher save or journal schema, an identity mismatch, or unresolved I/O leaves the data in place and enters `RevisedBlocked`.
- AC: The game never falls back to legacy campaign keys from `RevisedBlocked`.
- System: Core persistence · `SaveManager`, `CampaignSaveValidator`
- Status: Existing
- Refs: `SaveManager.cs`, `docs/system/02_Architecture_and_Runtime_Flow.md` §7

### MM-20 — Have a level completion that failed to save retried at next launch
As a player whose game was killed after a level, I want that completion applied next time I open the game, so that I do not lose the level I just finished.
- AC: A valid pending outcome journal is replayed once during initialization, before `RevisedReady` is published.
- AC: Replay is exact and idempotent — a duplicate returns `AlreadyCommitted` without incrementing the save revision.
- System: Core persistence · `CampaignOutcomeCoordinator`, `SaveManager.RetryPendingOutcome`
- Status: Existing
- Refs: `Assets/Scripts/Data/Persistence/CampaignOutcomeCoordinator.cs`, SALIN-174

### MM-21 — Hear menu music
As a player, I want music on the menu, so that the game has a mood before I press Play.
- AC: The menu plays a looping BGM track through `AudioManager`.
- AC: The track respects the master and BGM volume sliders.
- System: Main Menu · `AudioManager`
- Status: Partial — the BGM API and volume stack exist; per-scene menu track assignment is authored content and not verified in the current tree.
- Refs: `Assets/Scripts/Core/AudioManager.cs`, `docs/audio/audio-audit-2026-09-05.md`

### MM-22 — Not be offered modes that no longer exist
As a player, I want the menu to only offer things the game actually has, so that I am not sent to a dead end.
- AC: The menu has no Endless Mode button.
- AC: No menu control leads to a removed mechanic.
- System: Main Menu · `MainMenuUI`
- Status: Partial — Endless Mode is removed (SALIN-225) but the **Tracing Dojo** button is still live although D-005 deletes it. See U-4.
- Refs: `MainMenuUI.cs:13,33,178-181`, `Assets/_Scenes/MainMenu.unity:3148`, SALIN-268
