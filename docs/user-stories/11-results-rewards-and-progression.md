# 11 — Results, rewards and progression

Prefixes **`WAVE`** (Wave Cleared), **`RES`** (Results and stars), **`MEM`** (memory cards and archive), **`SAVE`** (persistence and unlocks).

---

## Wave Cleared

### WAVE-02 — See how the fight went
As a player, I want the Wave Cleared screen to show hearts left and my combat accuracy, so that I know how well I defended.
- AC: The banner shows surviving hearts and combat accuracy.
- AC: The continue control is labelled for what comes next ("Restore the Memory").
- System: Flow · `WaveClearedScreenUI`, `WaveClearedCopy`
- Status: Partial — the screen and gate are implemented; the audit records the ticket's four live-UI checks as unrun and two player-facing strings as needing content approval.
- Refs: `WaveClearedScreenUI.cs`, SALIN-232, SALIN-291

## Results and stars

### RES-07 — Be given advice on how to do better
As a player, I want a short tip after a level, so that I know what to practise.
- AC: The Results screen shows one improvement tip derived from the weakest metric.
- System: Results · `VictoryScreenUI`
- Status: Missing — no tip generation exists.
- Refs: SALIN-234, `docs/audit/BACKLOG.md` T19

### RES-12 — Learn something about Baybayin after a level
As a player, I want a short cultural or historical fact after a level, so that the heritage framing is reinforced.
- AC: A brief, visual trivia card appears after the level and never interrupts gameplay.
- System: Results · trivia card
- Status: Unclear — the GDD requires a post-level trivia card, but the revised flow replaces it with the Memory Card (MEM-01). No trivia-card implementation exists and no ruling retires the requirement.
- Refs: `docs/capstone/GDD.md` §3.3, §5.3

---

## Memory cards and archive

### MEM-06 — Have memory art to look at
As a player, I want the memory cards to be illustrated, so that the reward feels like a reward.
- AC: Each memory card and cutscene panel has authored art.
- System: Content · memory card art
- Status: Missing — memory-card art and "Hear Words" content are recorded as missing; memory art is ruled in scope for the team to produce.
- Refs: `docs/audit/STATUS-2026-09-13.md` (SALIN-240 gate), ruling §3 "Memory art"

---

## Saving and unlocking

### SAVE-06 — See that the game is saving
As a player, I want a small indicator at a save point, so that I know the write happened.
- AC: A scroll icon or "Saving…" then "Saved" appears briefly at each safe checkpoint.
- AC: A failure shows "Save failed — retrying" as text, not colour alone.
- System: Persistence · `SaveStatusIndicator` (not present)
- Status: Missing — no `SaveStatusIndicator` exists anywhere in `Assets/`. SALIN-239 is marked Done with no implementation.
- Refs: SALIN-239 (Done in Jira — unimplemented), `docs/audit/BACKLOG.md` T25
