# 06 — UI/UX and Player Flow
**Project:** Salinlahi
**Version:** 1.5
**Date:** 2026-05-24
**Owner:** Jeff Andre Millan (UI/UX Developer)

---

## 1. Screen Inventory

| Screen | Scene | Script | Implementation Status |
|--------|-------|--------|-----------------------|
| Bootstrap (invisible) | `Bootstrap.unity` | `BootstrapLoader.cs` | Implemented |
| Main Menu | `MainMenu.unity` | `MainMenuUI.cs` | Partial (stub) |
| Level Select | `LevelSelect.unity` | `LevelSelectUI.cs` | Implemented |
| Gameplay HUD | `Gameplay.unity` | `HUD.cs` | Implemented |
| Pause Menu | (overlay) | `PauseMenuUI.cs` | Implemented |
| Level Complete | (overlay or separate scene) | `VictoryScreenUI.cs` | Implemented |
| Game Over | `GameOver.unity` | `GameOverUI.cs` / `DefeatScreenUI.cs` | Deprecated — replaced by `DefeatScreenUI` overlay in Gameplay scene (SALIN-58) |
| Tracing Dojo | `TracingDojo.unity` | `TracingDojoController.cs` (+ `CharacterDropdown`, `CharacterListPopulator`, `CharacterListRow`, `DojoNavigator`, `FeedbackToast`, `GhostStrokeRenderer`) | Implemented |
| Create Baybayin Template (editor-only) | `CreateBaybayinTemplate.unity` | — | Editor tooling |
| Settings | (overlay) | `SettingsPanel.cs` | Implemented |
| Credits | (overlay) | `CreditsPanel.cs` | Implemented |
| Dialogue Panel (Type A) | (overlay in Gameplay) | `DialogueController.cs` | Implemented |
| In-Wave Popup (Type B) | (overlay in Gameplay) | `InWavePopupController.cs` (PLANNED) | PLANNED |
| Endless Mode | (shares Gameplay scene) | (PLANNED) | PLANNED |
| SUS/GEQ-S Questionnaire | (overlay or separate scene) | `QuestionnaireController.cs` (PLANNED) | PLANNED |

[EVIDENCE: Assets/_Scenes/ — Bootstrap, MainMenu, LevelSelect, TracingDojo, Gameplay, GameOver, CreateBaybayinTemplate scenes confirmed]
[EVIDENCE: Assets/Scripts/UI/TracingDojo/ — TracingDojoController.cs and supporting scripts]
[EVIDENCE: Assets/Scripts/UI/DefeatScreenUI.cs; Assets/Scripts/Core/SceneLoader.cs — `LoadGameOver()` marked `[System.Obsolete]`]
[EVIDENCE: docs/capstone/GDD.md, §5.1 Player Journey]

---

## 2. Player Journey Flow

```
App Launch
  └─ Bootstrap (invisible)
        └─ Auto → Main Menu
              ├─ [Play] → Level Select
              │     └─ [Select Level] → Gameplay Scene
              │           ├─ Type A Intro Dialogue (if configured) → draws from DialogueSequence SO
              │           ├─ Waves begin after dialogue ends
              │           ├─ Pause → Pause Menu (PLANNED)
              │           │     ├─ [Resume] → Gameplay
              │           │     └─ [Quit] → Main Menu
              │           ├─ Win → Level Complete (PLANNED)
              │           │     └─ [Next Level] → Gameplay (next level)
              │           │     └─ [Menu] → Main Menu
              │           └─ Lose → Defeat Overlay (in Gameplay) — `DefeatScreenUI`
              │                 ├─ [Retry] → Gameplay (same level)
              │                 └─ [Menu] → Main Menu
              ├─ [Endless Mode] → Gameplay Scene (endless config) (PLANNED)
              ├─ [Tracing Dojo] → Tracing Dojo Scene (Implemented)
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
| Play (Story Mode) | Navigate to Level Select | Partial (navigates to Gameplay directly; LevelSelect wiring unverified) |
| Endless Mode | Navigate to Endless Gameplay | PLANNED |
| Tracing Dojo | Navigate to Tracing Dojo scene | PLANNED |
| Settings | Open Settings screen | Partial (`SettingsPanel.cs` exists; menu wiring unverified) |
| Credits | Display credits screen | Partial (`CreditsPanel.cs` exists; menu wiring unverified) |

[EVIDENCE: docs/capstone/GDD.md, §5.3 — "Play, Endless Mode, Tracing Dojo, Settings, Credits"]

---

## 4. Gameplay HUD — `HUD.cs`

The HUD is implemented in `Assets/Scripts/UI/HUD.cs`. Elements below reflect current implementation and GDD specification.

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

### 4.1 Boss HUD Elements

The Gameplay scene wires three boss-only UI elements alongside the regular HUD. Each is a separate `MonoBehaviour` on the gameplay Canvas and is active only during a boss encounter.

| UI Element | Script | Behavior |
|------------|--------|----------|
| Boss health bar | `Assets/Scripts/UI/BossHealthBar.cs` | Filled Image type. Fills at `HPRemaining / phases.Count` and tweens via `Mathf.Lerp` on `OnBossDamaged`. Subscribes to `OnBossStarted` (acquires `GameManager.CurrentBoss`), `OnBossDamaged`, `OnBossDefeated`. Follows the boss world-space position with `_bossWorldOffset` via `WorldToScreenPoint` then `ScreenPointToWorldPointInRectangle`. Fades via unscaled time. |
| Boss glyph queue | `Assets/Scripts/UI/BossGlyphQueueUI.cs` | Shows one Baybayin icon plus an `X / N` progress counter above the boss during the Vulnerable window. Subscribes to `OnBossStarted`, `OnBossVulnerabilityWindowActive` (shown only after the collapse animation finishes, so the icon never displays a stale glyph mid-collapse), `OnBossDamaged`, `OnBossVulnerabilityExpired`, `OnBossDefeated`, `OnDrawingFailed` (red flash on wrong glyph). Listens to `BossController.OnDrawnThisPhaseChanged` to refresh the counter and the next expected glyph. Replaces the legacy `BossLabelIconRow`. |
| Boss vulnerability timer | `Assets/Scripts/UI/BossVulnerabilityTimerBar.cs` | Countdown bar under the boss that drains during the Vulnerable window. Driven by `OnBossVulnerabilityWindowActive` (countdown starts after collapse finishes so the on-screen time matches the actual targetable window) / `OnBossVulnerabilityExpired` / `OnBossDamaged`. |

[EVIDENCE: Assets/Scripts/UI/BossHealthBar.cs]
[EVIDENCE: Assets/Scripts/UI/BossGlyphQueueUI.cs]
[EVIDENCE: Assets/Scripts/UI/BossVulnerabilityTimerBar.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossController.cs — `OnDrawnThisPhaseChanged`]

### 4.2 PlayAreaContainer / AspectLockedCamera

The Gameplay HUD now lives under a `PlayAreaContainer` RectTransform that sizes itself to `AspectLockedCamera.PlayColumnScreenRect`. This keeps HUD corner anchors pinned to the 9:16 play column on tablets and ultra-wide phones, instead of the device viewport. `PlayAreaContainer` subscribes to `AspectLockedCamera.OnPlayAreaChanged` to re-anchor whenever the device aspect changes (e.g., editor Game-view aspect switch).

[EVIDENCE: Assets/Scripts/UI/PlayAreaContainer.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Camera/AspectLockedCamera.cs — `PlayColumnScreenRect`, `OnPlayAreaChanged`]

---

## 5. Defeat Screen — `DefeatScreenUI.cs`

### 5.1 Implemented Behavior

The standalone `GameOver` scene is **deprecated**. The current defeat flow runs entirely inside the Gameplay scene via `DefeatScreenUI`, a `CanvasGroup` overlay. `GameManager.HandleGameOver` no longer calls `SceneLoader.LoadGameOver` (the loader method itself is marked `[System.Obsolete]`); it instead snapshots `GameManager.LastDefeatHearts` (the hearts count at the moment of defeat, which the overlay reads to render its summary) and toggles the overlay's `CanvasGroup`.

[EVIDENCE: Assets/Scripts/UI/DefeatScreenUI.cs]
[EVIDENCE: Assets/Scripts/Core/GameManager.cs — `HandleGameOver`, `LastDefeatHearts`]
[EVIDENCE: Assets/Scripts/Core/SceneLoader.cs — `LoadGameOver()` carries `[System.Obsolete]`]

### 5.2 Required Content (from GDD — partially not implemented)

| Element | Description | Status |
|---------|-------------|--------|
| Final stats display | Waves survived, enemies defeated, accuracy % | Partial (`LastDefeatHearts` snapshot wired; full stats NOT FOUND) |
| Retry button | Reloads current level gameplay scene | Implemented (overlay calls `SceneLoader.LoadGameplay`) |
| Return to Level Select | Returns to level select | Partial (returns to MainMenu currently) |

[EVIDENCE: docs/capstone/GDD.md, §5.1 — "Game Over: Shows final stats. Retry button. Return to Level Select button."]

---

## 5.5 Dialogue System — `DialogueController.cs`

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

## 7. Tracing Dojo — Implemented

Scene: `Assets/_Scenes/TracingDojo.unity`. Script suite: `Assets/Scripts/UI/TracingDojo/` (`TracingDojoController.cs`, `CharacterDropdown.cs`, `CharacterListPopulator.cs`, `CharacterListRow.cs`, `DojoNavigator.cs`, `FeedbackToast.cs`, `GhostStrokeRenderer.cs`).

User-facing behavior per GDD:

- Accessible from Main Menu at any time.
- Shows all 17 Baybayin characters in a practice grid.
- Player can select any character and trace it freely.
- No enemies, no timer, no penalty for incorrect strokes.
- Provides visual guide overlay for each character's expected shape (`GhostStrokeRenderer`).
- Runs recognition system in passive mode to show confidence score as visual feedback (`FeedbackToast`).

[EVIDENCE: Assets/_Scenes/TracingDojo.unity]
[EVIDENCE: Assets/Scripts/UI/TracingDojo/TracingDojoController.cs and supporting scripts]
[EVIDENCE: docs/capstone/GDD.md, §2.4 Game Modes — "Tracing Dojo (Tutorial)"]

---

## 8. Accessibility Requirements

| Requirement | Implementation Target | Source |
|-------------|----------------------|--------|
| Full-screen drawing area — no precision targeting required | Drawing canvas = entire screen | GDD §5.4 |
| Audio pronunciation on every correct defeat | `AudioManager.PlayPronunciationClip()` | GDD §5.4; AudioManager.cs |
| Visual rejection feedback (red flash + X mark) on failed stroke | `HUD.cs` | GDD §5.4 |
| Tracing Dojo zero-pressure practice space | `TracingDojo.unity` (Implemented) | GDD §5.4 |
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
