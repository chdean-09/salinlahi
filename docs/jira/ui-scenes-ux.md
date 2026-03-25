# Epic: UI, Scenes & UX (SALIN-6)

**Status:** ⚠️ Skeleton Only — Most Screens Not Built | **Priority:** Medium–High

All game screens and UI systems: Main Menu, HUD, Level Select, Victory/Defeat, Tutorial, Dialogue, Scene Transitions, Loading Screen, Tracing Dojo, and visual polish.

---

## SALIN-23 — Build Main Menu Scene with Navigation

| Field      | Value |
|------------|-------|
| **Status** | In Progress |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | — |
| **Blocked By** | SALIN-11, SALIN-14 |

`MainMenu.unity` exists with functional Play, Settings, and Credits buttons. **Remaining:** Remove the `"(stub)"` debug log from `MainMenuUI.OnPlayButtonPressed()` and wire `SceneLoader.LoadScene("LevelSelect")` properly (requires LevelSelect.unity to exist — see SALIN-11).

**Acceptance Criteria:**
- `MainMenu` scene loads from Bootstrap without errors
- Play button navigates to LevelSelect scene via `SceneLoader`
- Settings button opens a settings overlay with at least an audio volume slider
- Credits button opens a credits screen listing team member names
- No `"(stub)"` debug logs remain in any button handler
- All buttons respond to touch input on a physical Android device
- Layout designed for 16:9 portrait orientation

---

## SALIN-31 — Build In-Game HUD — Hearts, Wave Counter, Character Prompt, Pause

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-46 |
| **Blocked By** | SALIN-13, SALIN-17 |

HUD Canvas overlaid on the Gameplay scene. Four main components driven by EventBus events — no direct component references.

**Components:**
- **Heart icons:** Subscribe to `HeartsChangedEvent(current, max)` — update sprite states (full/empty)
- **Wave counter:** Subscribe to WaveManager events — display "Wave 2 / 4" format
- **Character prompt:** Display the Baybayin glyph of the front-most enemy's `assignedCharacter`; update as wave changes
- **Pause button:** Calls `GameManager.ChangeState(Paused)`, shows pause overlay with Resume and Quit buttons

**Acceptance Criteria:**
- Heart icons update correctly on each `HeartsChangedEvent`
- Wave counter updates on wave start and completion
- Character prompt shows the correct Baybayin character for the current target enemy
- Pause button correctly toggles game state and time scale
- All HUD elements visible and non-overlapping on a 16:9 portrait layout

---

## SALIN-43 — Build Level Select Screen with Lock/Unlock/Complete States

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-70 |
| **Blocked By** | SALIN-11, SALIN-48 |

Grid or scroll view of 15 level buttons inside `LevelSelect.unity`. Each button reads `ProgressManager.IsLevelUnlocked(id)` and completion data from SALIN-48. Three visual states: locked (greyed), available, completed (star count shown). Tapping an available level sets the target level in GameManager and calls `SceneLoader.LoadScene("Gameplay")`.

**Acceptance Criteria:**
- All 15 level buttons render in the correct visual state
- Locked levels are not interactive
- Tapping an available level loads Gameplay with the correct `LevelConfigSO` loaded
- Completed levels display 1–3 stars based on `ProgressManager.GetStars(id)`
- Back button returns to Main Menu

---

## SALIN-44 — Implement Scene Transition Manager — Fade In/Out

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-65 |
| **Blocked By** | SALIN-11 |

Completes the fade stub in `SceneLoader`. `TransitionManager.cs` manages a full-screen canvas-group alpha fade: transparent → black on load-out, black → transparent on load-in. Configurable duration. Wired into `SceneLoader.LoadScene()` so all scene transitions use it automatically.

**Acceptance Criteria:**
- All scene transitions fade through black (no abrupt cuts)
- Fade duration configurable (default 0.3s)
- Overlay canvas sits above all game UI in canvas sort order
- Fade plays correctly when triggered mid-gameplay (pause → quit)
- No audio pops or visual flickers during transition

---

## SALIN-45 — Implement DialogueController.cs with Typewriter Effect

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-46, SALIN-47, SALIN-50 |
| **Blocked By** | — |

`DialogueController.cs` MonoBehaviour. Displays a `DialogueSO` — a sequential list of `DialogueLine { string speakerName; Sprite portrait; string text; }`. Typewriter coroutine reveals text character-by-character at a configurable speed. Tap anywhere to: (a) skip the typewriter and show full text if still running, (b) advance to the next line if typewriter is complete. Fires `OnDialogueComplete` when the sequence ends.

**Acceptance Criteria:**
- `DialogueController.Play(DialogueSO)` initiates the sequence
- Character name and portrait displayed per line
- Typewriter reveals text at configurable chars/sec (default 30)
- Single tap skips typewriter; second tap advances to next line
- `OnDialogueComplete` fires after the last line is acknowledged
- `DialogueSO` and `DialogueLine` ScriptableObject classes created

---

## SALIN-46 — Implement LevelFlowController.cs — Intro/Gameplay/Outro Sequence

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-34 (BGM wiring), SALIN-58 |
| **Blocked By** | SALIN-45, SALIN-16 |

Orchestrates the full level lifecycle in the Gameplay scene:
1. `PlayDialogue(introDialogue)` → wait for `OnDialogueComplete`
2. `WaveManager.StartLevel()` → wait for `OnLevelComplete` or `OnGameOver`
3. On LevelComplete: `PlayDialogue(outroDialogue)` → wait for `OnDialogueComplete`
4. `GameManager.ChangeState(LevelComplete)` → show Victory screen

Also responsible for calling `AudioManager.PlayBGM(levelConfig.bgmClip)` at level start (SALIN-34 dependency).

**Acceptance Criteria:**
- Intro dialogue plays before wave starts; waves do not begin until intro is acknowledged
- On GameOver: no outro dialogue; go directly to Defeat screen
- On LevelComplete: outro dialogue plays, then Victory screen shown
- BGM loaded from `LevelConfigSO.bgmClip` on level start
- All transitions driven by EventBus events — no direct coroutine chains to UI components

---

## SALIN-47 — Write Dialogue Content for All 15 Levels

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-50 |
| **Blocked By** | SALIN-45 (DialogueSO format must be defined first) |

Script intro and outro dialogue for all 15 levels. Tutorial dialogue for Level 1 must integrate with SALIN-50. Format as `DialogueSO` assets or JSON files importable into `DialogueSO`.

**Note:** Draft dialogue in a shared document first, then import once `DialogueSO` format is confirmed by SALIN-45. Levels 1–5 (evaluation levels) must be complete before UAT. Levels 6–15 can be rougher drafts in Sprint 3 and polished in Sprint 4.

**Acceptance Criteria:**
- Intro and outro `DialogueSO` assets exist for all 15 levels
- Tutorial dialogue for Level 1 introduces drawing mechanics step-by-step
- Each level's dialogue is tonally consistent with the game's narrative setting
- All dialogue assets loadable by `DialogueController` without errors
- Levels 1–5 dialogue reviewed and approved before research UAT

---

## SALIN-50 — FTUE / Tutorial Overlay for Level 1

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-45, SALIN-46, SALIN-47 |

First-time user experience shown only on first launch of Level 1. A step-by-step overlay guides the player:
1. "An enemy approaches — it shows a Baybayin character"
2. "Draw the character on your screen with your finger"
3. "The enemy is defeated!"

Displayed once per install. Tracks first-launch state in PlayerPrefs. Non-blocking — player can dismiss each step. Integrates with `LevelFlowController` to pause wave start until tutorial is complete.

**Acceptance Criteria:**
- Tutorial shown only on first launch (PlayerPrefs flag)
- Three tutorial steps displayed in sequence with dismiss buttons
- Wave does not start until tutorial is completed or dismissed
- Tutorial does not appear on subsequent Level 1 plays

---

## SALIN-57 — Implement Safe Area Handler for Notched Devices

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-31, SALIN-43, SALIN-58 |

`SafeAreaHandler.cs` component adjusts Canvas panel RectTransform anchors at runtime using `Screen.safeArea`. Must be attached to all full-screen Canvas roots. Test with Unity Device Simulator using a notched device profile.

**Acceptance Criteria:**
- `SafeAreaHandler` applied to all main Canvas objects (HUD, MainMenu, LevelSelect, etc.)
- No interactive UI elements obscured by notch or home indicator area
- Tested with Device Simulator using a notched Android profile

---

## SALIN-58 — Build Victory, Defeat, Settings, and Credits Screens

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-46 |
| **Blocked By** | SALIN-13, SALIN-46 |

Four UI overlays or panels:
- **Victory:** 1–3 stars earned, "Next Level" button (loads next level), "Level Select" button
- **Defeat:** "Retry" button (reloads current level), "Level Select" button; shows heart count at time of defeat
- **Settings:** Master, BGM, SFX volume sliders wired to `AudioManager.SetVolume()`; values read/written via PlayerPrefs
- **Credits:** Scrollable or static list of team names and roles

**Acceptance Criteria:**
- Victory screen shows correct star count from `ProgressManager`
- Defeat screen "Retry" fully resets game state (hearts, waves, enemies)
- Settings sliders correctly update `AudioManager` volumes and persist via PlayerPrefs
- All four screens navigable from their appropriate trigger points
- GameOverUI stub log removed and replaced with functional screen

---

## SALIN-65 — Loading Screen During Scene Transitions

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-44 |

Spinner or animated loading indicator shown during async scene loads. Integrated into `TransitionManager`/`SceneLoader`. Minimum display time of 0.3 seconds to prevent a visual flash on fast devices.

**Acceptance Criteria:**
- Loading indicator visible during all async scene loads
- Minimum display duration of 0.3s regardless of actual load time
- Indicator hidden immediately when new scene is interactive
- Tested on a low-end device where load time is perceptible

---

## SALIN-66 — Visual Polish Pass — UI Animations, Feedback, and Drawing Trail

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-31, SALIN-43, SALIN-58, SALIN-19 |

Polish pass across all UI components and gameplay feedback:
- Button press: scale/colour pulse animation (Unity UI Animator or DOTween)
- Correct draw feedback: green flash on drawing canvas
- Wrong draw feedback: red flash or screen shake (subtle)
- Enemy defeat: visual flash or particle burst placeholder
- Drawing trail: upgrade `LineRenderer` to GPU-accelerated particle line if performance allows (flagged in code comment since Sprint 1)

**Acceptance Criteria:**
- All interactive buttons have press feedback animations
- Correct/incorrect draw events produce distinct visual feedback
- Drawing trail fade-out is smooth (no abrupt pop)
- All polish additions maintain 60fps target on mid-range Android

---

## SALIN-69 — Implement Tracing Dojo Scene and Practice Mode

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | — |
| **Blocked By** | SALIN-20, SALIN-74, SALIN-27 |

`TracingDojo.unity` scene accessible from Main Menu. Players practice drawing Baybayin characters without combat pressure. Scrollable list of unlocked characters. Selected character shows a ghost stroke overlay on the drawing canvas. Confidence score and pass/fail feedback shown after each attempt.

**Acceptance Criteria:**
- Tracing Dojo scene in Build Settings, navigable from Main Menu
- Scrollable character list shows all unlocked Baybayin characters
- Ghost overlay renders the template point cloud as a faded guide line
- After each draw: confidence score (as percentage) and pass/fail shown
- No combat mechanics — no enemies, no hearts, no wave timer
- Practice attempts are not written to the research recognition log (SALIN-35)

**Req IDs:** GDD-REQ-005
