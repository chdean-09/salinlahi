# 02 — Journey map and level entry

Prefix **`MAP`**. Covers the era map (Level Select), level locks and their reasons, the level preview, and the Living Scroll hub.

---

### MAP-01 — See the levels of an era laid out
As a player, I want to see the five levels of the era I am in, so that I can tell where I am and what is next.
- AC: Level Select shows up to five level scroll buttons, one per era-local slot, sourced from the active `EraConfigSO.levels`.
- AC: Slots beyond the era's level count are hidden, not shown empty.
- System: Level Select · `LevelSelectUI`, `LevelButton`, `EraConfigSO`
- Status: Existing
- Refs: `Assets/Scripts/UI/LevelSelectUI.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §3.5

### MAP-02 — Move between eras on the map
As a player, I want to page between eras, so that I can look ahead and look back.
- AC: Prev / Next arrows are always visible.
- AC: At an era edge the arrow becomes non-interactable and tints grey in place rather than disappearing.
- System: Level Select · `LevelSelectUI`
- Status: Existing
- Refs: `LevelSelectUI.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §3.5

### MAP-03 — Recognise each era by its own art
As a player, I want each era to look different on the map, so that the three eras feel like distinct places.
- AC: The era background image comes from `EraConfigSO.backgroundSprite`.
- AC: The era banner image comes from `EraConfigSO.bannerSprite`.
- System: Level Select · `EraConfigSO`
- Status: Existing
- Refs: `Assets/Scripts/Data/EraConfigSO.cs`, `LevelSelectUI.cs`

### MAP-04 — Read each level's number on its scroll
As a player, I want each level scroll to show which level it is, so that I can pick the right one.
- AC: Each `LevelButton` renders `LevelConfigSO.numberSprite`.
- AC: All text is baked into the sprite; no TMP overlay is used.
- System: Level Select · `LevelButton`
- Status: Partial — `numberSprite` is assigned for Levels 1–5 only; Levels 6–15 render blank scrolls.
- Refs: `docs/system/06_UI_UX_and_Player_Flow.md` §10, SALIN-257 (To Do)

### MAP-05 — See which levels I have finished
As a player, I want completed levels marked on the map, so that I can see my progress at a glance.
- AC: `LevelButton.Setup(config, unlocked, completed)` renders a completion badge on a completed level.
- System: Level Select · `LevelButton`
- Status: Partial — the `_completionBadge` field exists but was recorded as unwired on every `LevelButton` in `LevelSelect.unity`; PR #126 was in flight at the time of the audit. Needs a visual re-check.
- Refs: `Assets/Scripts/UI/LevelButton.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §10, SALIN-137

### MAP-06 — See my best stars on a level
As a player, I want each finished level to show my best star rating, so that I know which levels I could do better on.
- AC: The level's best star count is displayed on or next to its scroll.
- AC: Replaying a level never lowers a previously earned star count.
- System: Level Select · `LevelButton`, `CampaignProgressRepository`
- Status: Partial — monotonic best-stars is guaranteed in the save layer; on-map display of the star count is not confirmed in the current scene.
- Refs: `docs/system/02_Architecture_and_Runtime_Flow.md` §7, SALIN-174

### MAP-07 — See that a level is locked
As a player, I want locked levels to look locked, so that I do not try to enter them.
- AC: A locked level renders in a visibly locked state and does not start gameplay when tapped.
- System: Level Select · `LevelLockResolver`, `LevelButton`
- Status: Existing
- Refs: `Assets/Scripts/Data/Persistence/LevelLockResolver.cs`, SALIN-137

### MAP-08 — Be told why a level is locked
As a player, I want the game to name what I still have to do, so that I know how to unlock the level.
- AC: Tapping a locked level shows a notice naming the specific prerequisite.
- AC: The notice uses era-relative wording, e.g. "Finish Ugat Level 5", never "Finish Level 5".
- System: Level Select · `LevelLockNoticePanel`, `LevelLockResolver`
- Status: Partial — the resolver supplies the reason and the panel exists, but the surface is a runtime-built placeholder rather than authored art.
- Refs: `Assets/Scripts/UI/LevelLockNoticePanel.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §10, SALIN-258

### MAP-09 — Have a level stay locked until every objective of the previous one is done
As a player, I want unlocking to require actually finishing a level, so that clearing waves alone does not skip the learning.
- AC: Unlock checks read persisted per-objective flags (`storyViewed`, `symbolsPracticed`, `wordsRestored`, `contextPassed`, `finalSyllableRestored`), not just a `completed` boolean.
- AC: Any false flag leaves the next level locked and the lock notice names the missing objective.
- System: Core persistence · `LevelObjectiveGate`, `LevelLockResolver`, `CampaignSaveDocument`
- Status: Existing
- Refs: `Assets/Scripts/Data/Persistence/LevelObjectiveGate.cs`, `Assets/Scripts/Gameplay/Flow/LevelObjectiveFlagResolver.cs`, SALIN-220

### MAP-10 — Have a whole era locked until I finish the era before it
As a player, I want the next era to open only when I complete the current one, so that the curriculum stays in order.
- AC: Era 2 Level 1 is locked until Ugat Level 5 is completed; Era 3 Level 1 until Ugnayan Level 5.
- AC: The lock notice names the cross-era prerequisite.
- System: Level Select · `LevelLockResolver` (cross-era requirements)
- Status: Existing
- Refs: `LevelLockResolver.cs`, SALIN-137

### MAP-11 — Read a level's title before I play it
As a player, I want each level to have a real name, so that the levels feel like chapters rather than numbers.
- AC: Each `LevelConfigSO.levelName` is its authored Filipino title (e.g. "Ang Unang Tinig", "Mga Mata ng Bata").
- AC: No player-facing surface shows "Level 7"; it shows "Ugnayan · Level 2".
- System: Level content · all 15 `Level*_Config.asset`
- Status: Existing
- Refs: `Assets/ScriptableObjects/Levels/Level*_Config.asset`, SALIN-218, SALIN-258

### MAP-12 — Preview a level before committing to it
As a player, I want to see what a level is about before I enter, so that I can prepare rather than being dropped in.
- AC: Tapping an unlocked level opens a preview showing the level title, a story summary, the target words, the new symbols, and the best stars earned.
- AC: The preview offers "Enter Memory" to start, "Hear Target Words" to play the words, and "View Symbols".
- AC: Gameplay only loads after the player confirms from the preview.
- System: Level Select · `LevelPreviewPanel` (not present)
- Status: Missing — no `LevelPreviewPanel` exists anywhere in `Assets/`; `LevelButton` loads gameplay directly. SALIN-243 is marked Done in Jira with no implementation.
- Refs: `LevelButton.cs`, SALIN-243 (Done in Jira — unimplemented), `docs/audit/BACKLOG.md` T28

### MAP-13 — Hear the target words from the preview
As a player, I want to tap and hear the words I will restore, so that I know what they sound like before I meet them.
- AC: Each target word on the preview is tappable and plays its spoken form.
- System: Level Select · `LevelPreviewPanel` (not present)
- Status: Missing — depends on MAP-12.
- Refs: SALIN-243, `docs/audit/BACKLOG.md` T28

### MAP-14 — Reach the era map through a single hub
As a player, I want one place that represents the whole journey, so that the three eras feel like parts of one scroll.
- AC: Continue (or a hub entry) opens a Living Scroll hub with three chambers, one per era.
- AC: Locked chambers show their prerequisite, e.g. "Complete Ugat Level 5".
- AC: Tapping an unlocked chamber opens that era's map.
- System: Hub · `Hub.unity` (not present)
- Status: Missing — the hub is explicitly kept by ruling but no `Hub.unity` or hub UI exists.
- Refs: SALIN-255 (To Do), `docs/design/spec-rulings-2026-09.md` §3 "Hub (UF-08)", `docs/audit/BACKLOG.md` T44

### MAP-15 — Get back to the map from anywhere I can leave a level
As a player, I want a reliable way back to the map, so that I am never stuck inside a level.
- AC: The pause menu and the defeat overlay both offer a route out of the level.
- AC: Leaving a level in progress commits no outcome.
- System: Level Select / Gameplay · `PauseMenuUI`, `DefeatScreenUI`, `GameManager.AbortCurrentLevelAttempt`
- Status: Partial — a route out exists from both surfaces, but `DefeatScreenUI` currently returns to Main Menu where the spec asks for Level Select.
- Refs: `Assets/Scripts/UI/DefeatScreenUI.cs`, `docs/system/06_UI_UX_and_Player_Flow.md` §5.2

### MAP-16 — Not lose my place when I leave the map
As a player, I want the map to reopen on the era I was last looking at, so that I do not have to page back every time.
- AC: Re-entering Level Select opens on the era containing the player's current level.
- System: Level Select · `LevelSelectUI`
- Status: Unclear — no requirement or implementation found for remembering the viewed era; behaviour is unspecified.
- Refs: `LevelSelectUI.cs`
