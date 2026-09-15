# 12 — Pause, failure, retry and edge cases

Prefix **`FAIL`**. Covers pausing, restarting, quitting, losing, checkpoints, and the awkward states in between.

---

## Pause

### FAIL-05 — See my objectives from the pause menu
As a player, I want to check what I am supposed to be doing without leaving the level, so that I can re-orient after a break.
- AC: The pause panel lists the level's target words and the symbols in play.
- System: Pause · `PauseMenuUI`
- Status: Missing — pass 2 re-confirmed: `PauseMenuUI` contains no objective, word or symbol view and no Restart Wave control. SALIN-237 is marked Done with no implementation.
- Refs: `Assets/Scripts/UI/PauseMenuUI.cs`, SALIN-237, `docs/audit/BACKLOG.md` T22

### FAIL-07 — Resume from a checkpoint instead of the very start
As a player, I want to retry the wave I am on rather than the whole level, so that a late mistake does not cost me the lesson screens again.
- AC: Restart Wave in the pause menu (after confirmation) restarts the current wave only, keeping earlier progress.
- AC: Retry Checkpoint on the defeat screen restarts at the last cleared wave (or the last restoration checkpoint) with full hearts and no lesson cards.
- System: Pause / Defeat · `PauseMenuUI`, `DefeatScreenUI`, `WaveManager` (start at a wave index)
- Status: Missing — Restart reloads the whole level, and `DefeatScreenUI` exposes Retry, Review Lesson and Level Select with no checkpoint option. `WaveManager` has no start-at-wave-index entry point. SALIN-236 is marked Done with no implementation. Whether a wave restart restores full hearts is unspecified (open question).
- Refs: `PauseMenuUI.cs:126-131`, `Assets/Scripts/UI/DefeatScreenUI.cs:33-43,86-89,138-141`, SALIN-236, `docs/audit/BACKLOG.md` T21, T22
- Merged: absorbs FAIL-14

### FAIL-09 — Leave mid-level and come back where I was
As a player, I want to stop a long level and resume at the same place, so that I can play in short sessions.
- AC: A checkpoint is written after every cleared wave and after every completed syllable placement in restoration.
- AC: A Save and Exit control in the pause menu writes the checkpoint and returns to the menu without committing an outcome.
- AC: Pausing on wave 3, exiting, killing the app and relaunching resumes at wave 3 with the same hearts.
- AC: Placing two of a word's syllables, exiting and relaunching reopens the board with those two locked and the third empty.
- System: Pause / persistence · `PauseMenuUI`, `CampaignSaveDocument`, `GameManager` snapshot
- Status: Missing — `GameManager`'s paused-run snapshot is in-memory only and there is no Save and Exit control. Ruled OQ-3, which supersedes SALIN-165's "clean attempt" criterion.
- Refs: `GameManager.cs:216-290`, SALIN-270 (To Do), ruling OQ-3, `docs/audit/BACKLOG.md` T58
- Merged: absorbs FAIL-10

## Losing

### FAIL-12 — Be told why I lost
As a player, I want the defeat screen to name the cause, so that I learn from it.
- AC: The screen shows hearts at defeat and an explanation line.
- AC: It names the symbols that reached the Shrine.
- System: Defeat · `DefeatScreenUI`
- Status: Partial — narrowed in pass 2: commit `78070b70` ("cutscene fix") **removed the explanation sentence** and now hides `_explanationText` (`SetActive(false)`), so the defeat screen shows the heart count only. No cause, no missed symbols, no stats.
- Refs: `DefeatScreenUI.cs:67-68`, commit `78070b70`, `docs/audit/BACKLOG.md` T21

### FAIL-15 — Review the symbols I got wrong before retrying
As a player, I want to re-study what beat me, so that the retry is a better attempt rather than the same one.
- AC: A Review Lesson / Review Symbols control opens practice for the symbols that reached the Shrine.
- System: Defeat · `DefeatScreenUI._reviewLessonButton`
- Status: Partial — a `_reviewLessonButton` field exists on `DefeatScreenUI`; whether it targets the missed symbols specifically is not established.
- Refs: `DefeatScreenUI.cs`

### FAIL-16 — Leave after losing
As a player, I want to stop after a defeat, so that I am not forced to retry.
- AC: A Level Select control returns out of the level.
- System: Defeat · `DefeatScreenUI`
- Status: Partial — the control exists but returns to Main Menu where the spec asks for Level Select. See MAP-15.
- Refs: `docs/system/06_UI_UX_and_Player_Flow.md` §5.2

## Edge cases

### FAIL-23 — Never lose a level because the game was in the background
As a player, I want the app not to run the fight while I am not looking, so that I do not come back to a defeat.
- AC: Losing focus pauses or suspends gameplay rather than continuing to spawn and advance enemies.
- System: Core · application focus handling
- Status: Unclear — pass 2 confirmed there is **no `OnApplicationPause` or `OnApplicationFocus` handler anywhere** under `Assets/Scripts`, so nothing in the game reacts to backgrounding; whether the OS suspends the loop or the level keeps running depends on platform defaults. The requirement itself is unwritten. See U-15.
- Refs: grep of `Assets/Scripts` for `OnApplicationPause|OnApplicationFocus` returns nothing

### FAIL-25 — Have the retired El Inquisidor test content not affect my game
As a player, I want cut boss content not to appear, so that the game does not contain stray old encounters.
- AC: No level references `BossConfig_ElInquisidor` or `BossConfig_Superintendent`.
- System: Content · `Level*_Config.asset`
- Status: Partial — Level 5's boss reference is cleared (SALIN-283), but `Level10_Config.bossConfig` still points at `BossConfig_Superintendent`. See U-2.
- Refs: `Level10_Config.asset`, `Level5_Config.asset`, SALIN-280
