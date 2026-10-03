# 01 — App entry and Main Menu

Prefix **`MM`**. Covers cold boot, the main menu, entering or restarting a journey, Settings, Credits, Exit, and the save notices the player meets at launch.

See [`00-overview.md`](00-overview.md) for the status legend and the Unclear register.

---

### MM-08 — See my progress on the menu
As a player, I want the menu to tell me how far I have come, so that I can feel the journey accumulating.
- AC: The menu shows an era-relative progress line, e.g. "Ugat · Level 3".
- AC: The line never uses global level numbers 1–15.
- System: Main Menu · `MainMenuProgressLine`, `MainMenuProgressCopy`
- Status: Partial — the progress line is implemented; the percentage element of the spec line ("Ugat · Level 3 · 13%") is not confirmed and the ticket is still In Progress.
- Refs: `Assets/Scripts/UI/MainMenuProgressLine.cs`, SALIN-256 (In Progress), SALIN-258

### MM-22 — Not be offered modes that no longer exist
As a player, I want the menu to only offer things the game actually has, so that I am not sent to a dead end.
- AC: The menu has no Endless Mode button.
- AC: No menu control leads to a removed mechanic.
- System: Main Menu · `MainMenuUI`
- Status: Partial — Endless Mode is removed (SALIN-225) but the **Tracing Dojo** button is still live although D-005 deletes it. See U-4.
- Refs: `MainMenuUI.cs:13,33,178-181`, `Assets/_Scenes/MainMenu.unity:3148`, SALIN-268
