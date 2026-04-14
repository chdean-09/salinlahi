# 06 — UI/UX and Player Flow
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Jeff Andre Millan (UI/UX Developer)

---

## 1. Screen Inventory

| Screen | Scene | Script | Implementation Status |
|--------|-------|--------|-----------------------|
| Bootstrap (invisible) | `Bootstrap.unity` | `BootstrapLoader.cs` | Implemented |
| Main Menu | `MainMenu.unity` | `MainMenuUI.cs` | Partial (stub) |
| Level Select | (NOT FOUND — no scene) | `LevelSelect.cs` (PLANNED) | NOT FOUND |
| Gameplay HUD | `Gameplay.unity` | `HUD.cs` (PLANNED) | NOT FOUND |
| Pause Menu | (overlay) | `PauseMenu.cs` (PLANNED) | NOT FOUND |
| Level Complete | (NOT FOUND — no scene) | (PLANNED) | NOT FOUND |
| Game Over | `GameOver.unity` | `GameOverUI.cs` | Partial (stub) |
| Tracing Dojo | (NOT FOUND — no scene) | (PLANNED) | NOT FOUND |
| Settings | (NOT FOUND — no scene) | (PLANNED) | NOT FOUND |
| Dialogue Panel (Type A) | (overlay in Gameplay) | `DialogueController.cs` (PLANNED) | NOT FOUND |
| In-Wave Popup (Type B) | (overlay in Gameplay) | `InWavePopupController.cs` (PLANNED) | NOT FOUND |
| Level Complete | (overlay or separate scene) | (PLANNED) | NOT FOUND |
| Endless Mode | (shares Gameplay scene) | (PLANNED) | NOT FOUND |
| SUS/GEQ-S Questionnaire | (overlay or separate scene) | `QuestionnaireController.cs` (PLANNED) | NOT FOUND |

[EVIDENCE: Assets/_Scenes/ — only Bootstrap, MainMenu, Gameplay, GameOver scenes exist]
[EVIDENCE: docs/capstone/GDD.md, §5.1 Player Journey]

---

## 2. Player Journey Flow

```
App Launch
  └─ Bootstrap (invisible)
        └─ Auto → Main Menu
              ├─ [Play] → Level Select (PLANNED)
              │     └─ [Select Level] → Gameplay Scene
              │           ├─ Type A Intro Dialogue (if configured) → draws from DialogueSequence SO
              │           ├─ Waves begin after dialogue ends
              │           ├─ Pause → Pause Menu (PLANNED)
              │           │     ├─ [Resume] → Gameplay
              │           │     └─ [Quit] → Main Menu
              │           ├─ Win → Level Complete (PLANNED)
              │           │     └─ [Next Level] → Gameplay (next level)
              │           │     └─ [Menu] → Main Menu
              │           └─ Lose → Game Over Scene
              │                 ├─ [Retry] → Gameplay (same level)
              │                 └─ [Menu] → Main Menu
              ├─ [Endless Mode] → Gameplay Scene (endless config) (PLANNED)
              ├─ [Tracing Dojo] → Tracing Dojo Scene (PLANNED)
              └─ [Settings] → Settings Screen (PLANNED)
```

[EVIDENCE: docs/capstone/GDD.md, §5.1 Player Journey]

---

## 3. Main Menu — `MainMenuUI.cs`

### 3.1 Implemented Behavior
`MainMenuUI` contains a `Play()` method that calls `SceneLoader.Instance.LoadGameplay()`. It is wired to a button via the Unity Inspector.

[EVIDENCE: Assets/Scripts/UI/MainMenuUI.cs]

### 3.2 Required Menu Items (from GDD — partially not implemented)

| Menu Item | Expected Action | Status |
|-----------|----------------|--------|
| Play (Story Mode) | Navigate to Level Select | NOT FOUND (currently goes directly to Gameplay) |
| Endless Mode | Navigate to Endless Gameplay | NOT FOUND |
| Tracing Dojo | Navigate to Tracing Dojo scene | NOT FOUND |
| Settings | Open Settings screen | NOT FOUND |
| Credits | Display credits screen | NOT FOUND |

[EVIDENCE: docs/capstone/GDD.md, §5.3 — "Play, Endless Mode, Tracing Dojo, Settings, Credits"]

---

## 4. Gameplay HUD (PLANNED)

The HUD is specified in the GDD and TDD but **has no implementation file**. All items below are from source documents only.

| HUD Element | Description | EventBus Trigger |
|-------------|-------------|-----------------|
| Heart display | Shows current heart count (0–3 icons) | `OnHeartsChanged(int)` |
| Wave indicator | Shows "Wave X of Y" | `OnWaveStarted(int)` |
| Combo counter | Shows current streak count; appears only when active, fades when streak breaks | `OnComboChanged(int)` |
| Pause button | Top corner; opens Pause Menu overlay | (UI tap) |
| Drawing canvas | Full-screen transparent touch surface for drawing | `OnDrawingStarted`, `OnDrawingFailed` |
| Rejection feedback | Red flash + X mark on failed stroke | `OnDrawingFailed` |
| Success feedback | Visual burst on correct recognition | (PLANNED) |

**Design constraint:** The entire screen is the drawing surface during gameplay. No precision targeting required. The player draws anywhere on the screen.

[EVIDENCE: docs/capstone/GDD.md, §2.2 Controls Summary; §5.4 Accessibility]
[EVIDENCE: docs/capstone/TDD.md, §7.4 — HUD.cs]

---

## 5. Game Over Screen — `GameOverUI.cs`

### 5.1 Implemented Behavior
`GameOverUI` contains wired button handlers. Implementation is a stub with button calls to `SceneLoader.Instance.LoadGameplay()` (Retry) and `SceneLoader.Instance.LoadMainMenu()` (Menu).

[EVIDENCE: Assets/Scripts/UI/GameOverUI.cs]

### 5.2 Required Content (from GDD — partially not implemented)

| Element | Description | Status |
|---------|-------------|--------|
| Final stats display | Waves survived, enemies defeated, accuracy % | NOT FOUND |
| Retry button | Reloads current level gameplay scene | Implemented (LoadGameplay stub) |
| Return to Level Select | Returns to level select | Partial (returns to MainMenu currently) |

[EVIDENCE: docs/capstone/GDD.md, §5.1 — "Game Over: Shows final stats. Retry button. Return to Level Select button."]

---

## 5.5 Dialogue System (PLANNED)

### Type A — Gated Story Panels

Appear at fixed moments: before a level starts, after a boss is defeated, or when a chapter ends. Player reads text, then taps to continue. These carry the main plot beats.

- Time.timeScale set to 0 during Type A panels
- Typewriter text effect with punctuation-aware pauses (0.12s for `.?!`, 0.06s for `,`, 0.03s for other characters)
- First tap completes the current line instantly; second tap advances to next line
- Character portraits displayed on left or right side of panel
- Uses `WaitForSecondsRealtime` (not `WaitForSeconds`) so typewriter runs correctly at timeScale 0

### Type B — In-Wave Popups

Small atmospheric flavor lines that appear during gameplay. Do not pause gameplay.

- Appear at top or bottom edge of screen, away from drawing area
- Show for 3–4 seconds, fade out automatically
- Triggered by EventBus subscriptions, not by LevelFlowController
- If a new popup triggers while one is showing, old one fades immediately and new one replaces it
- Optional per wave; can be safely cut if behind schedule

### Data Architecture

All dialogue content stored in `DialogueSequence` ScriptableObjects containing ordered lists of `DialogueLine` entries (speaker name, portrait, portrait side, text, optional voice clip).

[EVIDENCE: Team README §12 — Cutscenes, Dialogue, and Story Bits]
[EVIDENCE: docs/capstone/GDD.md, §4.5 Narrative Beats]

---

## 6. Level Complete Screen (PLANNED)

No scene or script for Level Complete currently exists. Required content per GDD:

| Element | Description |
|---------|-------------|
| Stats summary | Enemies killed, drawing accuracy %, waves cleared |
| Trivia card | Cultural/historical fact about the Baybayin character learned |
| Next Level button | Advances to next `LevelConfigSO` in sequence |
| Menu button | Returns to Level Select |

[EVIDENCE: docs/capstone/GDD.md, §5.1 Player Journey — "Level Complete: Brief stats screen...Trivia card. Next Level button."]

---

## 7. Tracing Dojo (PLANNED)

No scene or script currently exists. Required behavior per GDD:

- Accessible from Main Menu at any time.
- Shows all 17 Baybayin characters in a practice grid.
- Player can select any character and trace it freely.
- No enemies, no timer, no penalty for incorrect strokes.
- Provides visual guide overlay for each character's expected shape.
- Runs recognition system in passive mode to show confidence score as visual feedback.

[EVIDENCE: docs/capstone/GDD.md, §2.4 Game Modes — "Tracing Dojo (Tutorial)"]

---

## 8. Accessibility Requirements

| Requirement | Implementation Target | Source |
|-------------|----------------------|--------|
| Full-screen drawing area — no precision targeting required | Drawing canvas = entire screen | GDD §5.4 |
| Audio pronunciation on every correct defeat | `AudioManager.PlayPronunciationClip()` | GDD §5.4; AudioManager.cs |
| Visual rejection feedback (red flash + X mark) on failed stroke | `HUD.cs` (PLANNED) | GDD §5.4 |
| Tracing Dojo zero-pressure practice space | Tracing Dojo scene (PLANNED) | GDD §5.4 |
| Portrait-mode one-handed play design | Unity Player Settings: portrait lock | GDD §5.4 |
| No text-heavy tutorials — first level teaches via play | Level 1 design constraint | GDD §5.4 |

[EVIDENCE: docs/capstone/GDD.md, §5.4 Accessibility]

---

## 9. Input Summary

| Input | Action |
|-------|--------|
| Touch and drag on screen | Draw a Baybayin character stroke |
| Lift finger | Submit the drawn stroke for recognition |
| Tap UI buttons | Navigate menus, pause, retry |

**Constraint:** No virtual joystick, no attack buttons, no gesture shortcuts. Drawing is the only combat input.

[EVIDENCE: docs/capstone/GDD.md, §2.2 Controls Summary]
