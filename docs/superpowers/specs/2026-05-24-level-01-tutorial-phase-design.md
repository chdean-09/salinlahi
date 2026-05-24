# Level 1 Tutorial Phase Design

Project: Salinlahi  
Scope: Level 1 tutorial phase only  
Scene: `Level_01_Tutorial`  
Date: 2026-05-24  
Status: Design spec from user answers

## 0. Locked Decisions

- Learning goal: drawing syllable templates kills enemies; the first enemy proves "draw correctly to protect the base."
- Scene loading: `LevelManager` loads `Level_01_Tutorial` by scene name.
- Input: Unity Input System, mouse and touch, same validation path.
- Syllables: `BA -> SA -> LA -> HA`; first enemy is `BA`.
- Correctness: 10-15 px stroke-path tolerance plus direction matching, lenient for shaky mobile input.
- Tutorial pause: freeze gameplay and enemy movement, but keep drawing, UI, and guide animations active.
- Hants: 5s idle hint, 12s stronger hint, auto-complete after 3 failed attempts.
- Skip: locked until first successful drawing, then available from tutorial UI and pause menu.
- Protagonist: walks from bottom approach position to behind the wall, centered, over 1.5-2.0s.
- Camera: 2D side view, centered on the wall/base.
- Tutorial length: 90-120 seconds, 6-8 steps max.
- Audio/UI style was skipped by the user. This spec recommends minimal text/SFX but does not treat it as a fixed requirement.

## 1. Tutorial Flow Map

Target duration: 90-120 seconds, excluding player struggle time. Drawing gates pause enemy threat until correct or assisted.

| State | Target Time | Trigger | Player Sees | Player Action | Completion Condition |
|---|---:|---|---|---|---|
| `S00_Level1Gate` | 0s | Scene loaded | Gameplay view, base/wall visible | None | Active scene is `Level_01_Tutorial` and level ID is 1 |
| `S01_BaseIntro` | 0-8s | Gate passed | Camera centered on wall/base | Tap through short dialogue | Dialogue complete |
| `S02_ProtagonistWalkIn` | 8-12s | Base intro complete | Protagonist enters from bottom and stops behind wall | None | Walk animation reaches behind-wall midpoint |
| `S03_FirstEnemyBA` | 12-22s | Walk complete | First enemy enters and stops outside base range with `BA` marker | Watch | Enemy is spawned, targeted, and frozen |
| `S04_DrawBA` | 22-45s | First enemy frozen | `BA` guide, start dot, direction arrow, enemy target marker | Draw `BA` | Correct `BA` submission defeats first enemy |
| `S05_PracticeChain` | 45-90s | `BA` enemy defeated | One enemy at a time for `SA`, `LA`, `HA` | Draw prompted syllable | Each enemy defeated in order |
| `S06_ReleaseToLevel` | 90-105s | `HA` enemy defeated | Guide clears, normal HUD remains | Continue playing | Tutorial marked complete; normal Level 1 waves start |
| `S07_SkipPath` | Any time after `BA` success | Player taps Skip or pause-menu Skip | Confirmation prompt | Confirm skip | Tutorial marked skipped-complete; normal Level 1 waves start |

State rules:

- `S00_Level1Gate` must fail closed if scene name is not `Level_01_Tutorial`.
- Tutorial enemies are not normal wave enemies. They are controlled by the Level 1 tutorial controller.
- Normal `WaveManager.StartLevel()` runs only after `S06_ReleaseToLevel` or `S07_SkipPath`.
- Base damage is disabled for tutorial enemies. They cannot trigger hard fail.
- On app background or scene reload before completion, resume at the current tutorial state or restart Level 1 tutorial. Do not mark complete until `S06` or `S07`.

## 2. Dialogue Script

Text should stay short. Use typewriter or instant text, but do not block drawing prompts with long panels.

| Beat | Trigger | Speaker | Timing | Line |
|---|---|---|---:|---|
| Base shown | `S01_BaseIntro` start | Guide | 0.0s | This is the base. |
| Threat setup | After tap | Guide | 1.5s | Keep enemies away from it. |
| Player arrives | `S02_ProtagonistWalkIn` start | Protagonist | 0.0s | I will defend it. |
| First enemy | `S03_FirstEnemyBA` enemy appears | Guide | 0.0s | Oh no, an enemy is coming. |
| Draw purpose | Enemy freezes | Guide | 1.0s | Draw its syllable to defeat it. |
| BA instruction | `S04_DrawBA` start | Guide | 0.0s | Draw BA. Start at the dot. |
| BA success | `BA` defeated | Guide | 0.0s | Great job. Drawing protects the base. |
| SA prompt | `SA` enemy freezes | Guide | 0.0s | Now draw SA. |
| LA prompt | `LA` enemy freezes | Guide | 0.0s | Draw LA next. |
| HA prompt | `HA` enemy freezes | Guide | 0.0s | Last one. Draw HA. |
| Release | `HA` defeated | Guide | 0.0s | You are ready. Defend the base. |

Wrong-draw feedback lines:

- Wrong syllable: `That was {recognized}. Draw {target}.`
- Bad direction: `Follow the arrow direction.`
- Too short: `Draw the full shape.`
- Idle at 5s: `Trace the glowing guide.`
- Idle at 12s: `Start at the dot, then follow the arrow.`
- Assist after 3 failures: `Watch this once.`

## 3. Animation Notes

Protagonist walk-in:

- Trigger: `S02_ProtagonistWalkIn`.
- Duration: 1.75s target; acceptable range 1.5-2.0s.
- Start position: `(wallCenter.x - 3.0 world units, groundY, 0)`.
- End position: `(wallCenter.x, groundY, 0)`, visually behind the wall.
- Sorting: protagonist behind wall foreground but above far background. Use sorting order below wall front layer.
- Movement curve: ease-out during final 20% so the stop feels deliberate.
- End pose: idle/ready stance facing enemy lane.
- Camera: 2D side view centered on `wallCenter`; no pan unless current composition hides the wall.
- Framing: wall/base should occupy the center third; enemy lane visible above/right enough to show the first incoming enemy.
- Input: drawing disabled during walk-in, enabled at `S04_DrawBA`.

Enemy animation during tutorial:

- Tutorial enemy enters from normal spawn direction until it reaches a safe tutorial stop point.
- Stop point: at least 1.5 world units before base trigger or any damage zone.
- On prompt start, enemy idle/walk animation may continue, but transform movement must stop.
- On correct draw, enemy receives the normal single-hit/defeat feedback if health is 1. If tutorial enemy has more health, force tutorial damage to defeat it in one correct draw.

## 4. Syllable Template SVG Asset

Purpose: a 1024x1024 SVG source template that can be shown to the player or exported as PNG. Safe margin is 64 px. The guide is split into four copy zones for `BA`, `SA`, `LA`, and `HA`.

Implementation note: current resources include `BA-VA.png`, `SA-ZA.png`, and `LA.png`. `HA` should be authored as the final project glyph before implementation sign-off. The SVG below uses guide paths for layout and interaction design, not final art authority.

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024" role="img" aria-labelledby="title desc">
  <title id="title">Level 1 Syllable Drawing Template</title>
  <desc id="desc">A square 1024 by 1024 tutorial guide with 64 pixel safe margins and four syllable guide zones: BA, SA, LA, and HA.</desc>

  <defs>
    <marker id="arrow" markerWidth="14" markerHeight="14" refX="10" refY="7" orient="auto" markerUnits="strokeWidth">
      <path d="M2,2 L12,7 L2,12 Z" fill="#168a4a"/>
    </marker>
    <style>
      .bg { fill: #f7f7f2; }
      .safe { fill: none; stroke: #d33; stroke-width: 3; stroke-dasharray: 12 10; }
      .zone { fill: #ffffff; stroke: #333333; stroke-width: 4; rx: 16; }
      .guide { fill: none; stroke: #7d168f; stroke-width: 18; stroke-linecap: round; stroke-linejoin: round; }
      .ghost { fill: none; stroke: #7d168f; stroke-width: 34; stroke-linecap: round; stroke-linejoin: round; opacity: 0.14; }
      .dir { fill: none; stroke: #168a4a; stroke-width: 5; stroke-linecap: round; marker-end: url(#arrow); }
      .start { fill: #168a4a; stroke: #ffffff; stroke-width: 5; }
      .label { font-family: Arial, sans-serif; font-size: 38px; font-weight: 700; fill: #202020; text-anchor: middle; }
      .small { font-family: Arial, sans-serif; font-size: 21px; fill: #404040; text-anchor: middle; }
    </style>
  </defs>

  <rect class="bg" x="0" y="0" width="1024" height="1024"/>
  <rect class="safe" x="64" y="64" width="896" height="896"/>
  <text class="small" x="512" y="45">1024 x 1024 source - 64 px safe margin</text>

  <rect class="zone" x="96" y="104" width="384" height="368"/>
  <rect class="zone" x="544" y="104" width="384" height="368"/>
  <rect class="zone" x="96" y="552" width="384" height="368"/>
  <rect class="zone" x="544" y="552" width="384" height="368"/>

  <text class="label" x="288" y="155">BA</text>
  <text class="small" x="288" y="435">Start at dot. Loop clockwise.</text>
  <path class="ghost" d="M250 290 C230 238 281 208 319 239 C350 268 340 328 293 330 C259 332 237 316 250 290"/>
  <path class="guide" d="M250 290 C230 238 281 208 319 239 C350 268 340 328 293 330 C259 332 237 316 250 290"/>
  <circle class="start" cx="250" cy="290" r="13"/>
  <path class="dir" d="M238 262 C245 225 285 210 317 236"/>

  <text class="label" x="736" y="155">SA</text>
  <text class="small" x="736" y="435">Down stroke, then curve right.</text>
  <path class="ghost" d="M674 220 L730 220 L730 332 C770 240 840 217 849 273 C858 328 793 333 760 318"/>
  <path class="guide" d="M674 220 L730 220 L730 332 C770 240 840 217 849 273 C858 328 793 333 760 318"/>
  <circle class="start" cx="674" cy="220" r="13"/>
  <path class="dir" d="M674 220 L730 220 L730 292"/>

  <text class="label" x="288" y="603">LA</text>
  <text class="small" x="288" y="883">Wave first, then center curl.</text>
  <path class="ghost" d="M210 708 C252 738 286 712 319 702 C350 691 384 699 401 711 M302 713 C280 736 284 752 309 760 C334 768 335 790 306 799 C283 806 286 829 315 839"/>
  <path class="guide" d="M210 708 C252 738 286 712 319 702 C350 691 384 699 401 711 M302 713 C280 736 284 752 309 760 C334 768 335 790 306 799 C283 806 286 829 315 839"/>
  <circle class="start" cx="210" cy="708" r="13"/>
  <path class="dir" d="M210 708 C252 738 286 712 319 702"/>

  <text class="label" x="736" y="603">HA</text>
  <text class="small" x="736" y="883">HA guide plus upper vowel mark.</text>
  <path class="ghost" d="M662 732 C705 762 754 716 808 742"/>
  <path class="guide" d="M662 732 C705 762 754 716 808 742"/>
  <circle class="guide" cx="736" cy="681" r="10"/>
  <circle class="start" cx="662" cy="732" r="13"/>
  <path class="dir" d="M662 732 C705 762 754 716 808 742"/>
</svg>
```

## 5. Instruction Pause Logic

Definition of "right":

- The active prompt has a target syllable ID: `BA`, `SA`, `LA`, or `HA`.
- The submitted stroke set passes minimum point count and stroke duration checks.
- Recognition result passes the configured confidence threshold.
- `RecognitionResult.characterID == targetSyllableID`.
- Normalized stroke path stays within 10-15 px of the target guide path after fitting to the guide bounds.
- Stroke direction and stroke order match the template. Direction can be lenient for shaky points but not reversed.

Pause behavior:

- Do not use `GameManager.PauseGame()` or `EnterDialoguePause()` during drawing gates, because current drawing input accepts only `Playing` or `Practicing`.
- Keep `GameManager.CurrentState == Playing` during drawing gates.
- Freeze tutorial threat locally: stop tutorial enemy movement, prevent base damage, and keep normal waves unstarted.
- UI guide animations, hint timers, and assist demo use unscaled time or their own timers.
- Dialogue-only panels may use the existing dialogue pause because drawing is disabled during those panels.

Feedback loop:

- On wrong draw, keep the enemy frozen and keep the same prompt active.
- Clear the player's stroke after feedback delay.
- Show one cause-specific hint, not a generic failure.
- Do not reduce hearts, advance waves, or damage the base.
- After a correct draw, clear the guide, play hit/defeat feedback, then advance to the next state.

Anti-frustration safeguards:

- Idle 5s: glow the full guide path and pulse the start dot.
- Idle 12s: animate the correct stroke path once.
- Failure 1: short text hint.
- Failure 2: stronger visual hint; widen path tolerance toward 15 px.
- Failure 3: auto-complete with an assist animation and kill the tutorial enemy.
- Assisted completion counts as tutorial progress but logs `assisted=true`.
- Skip remains unavailable until the first manual `BA` success.

## 6. What More?

Extra tutorial enhancements:

- Hant cadence: vary idle hints by failure cause, not only by elapsed time.
- Skip/assist: add "Practice later" prompt after first success; route to Tracing Dojo from pause menu.
- Accessibility: high contrast guide colors, large labels, left-handed safe UI placement, reduced motion toggle.
- Fail states: no hard fail in tutorial; if the base is somehow hit, restore heart and log a tutorial safety error.
- Audio cues: optional light success/error SFX; avoid voice-over unless localization scope expands.
- Analytics: log prompt ID, attempts, idle time, success/assist/skip, recognized ID, confidence, and duration.
- Localization: keep all strings under 60 characters; store in data, not hardcoded controller logic.
- Replay: allow replaying Level 1 tutorial from settings or level select without resetting campaign progress.
- QA hooks: editor button to reset Level 1 tutorial progress and jump to each tutorial state.
- Visual polish: target marker over enemy, start dot, direction arrow, and green success flash on the guide.

## 7. Unity Implementation Notes

Recommended new scene object:

- `Level1InteractiveTutorialController`
  - Lives only in `Level_01_Tutorial`.
  - Owns the state machine from `S00` to `S07`.
  - Serialized guard: `_requiredSceneName = "Level_01_Tutorial"`.
  - Serialized guard: `_requiredLevelNumber = 1`.
  - Early returns and disables itself unless both guards pass.
  - References: `WaveManager`, `WaveSpawner`, `DrawingCanvas`, `DialogueController`, tutorial overlay UI, protagonist animator, camera target, base/wall transforms, tutorial enemy data, and `BaybayinCharacterSO` entries for `BA`, `SA`, `LA`, `HA`.

Recommended new supporting scripts:

- `Level1TutorialState`
  - Enum: `Gate`, `BaseIntro`, `WalkIn`, `EnemyIntro`, `DrawPrompt`, `PracticeChain`, `Release`, `Skipped`.
- `Level1TutorialStep`
  - Serializable data for prompt ID, target character, dialogue line, guide sprite/path, enemy data, hint copy, and stop position.
- `Level1TutorialGuideUI`
  - Shows guide path, start dot, direction arrow, feedback text, skip button, and assist animation.
- `Level1TutorialGlyphValidator`
  - Compares submitted strokes against current target using recognition result, 10-15 px path tolerance, and direction matching.
- `Level1TutorialEnemyController`
  - Wraps the spawned tutorial enemy, freezes movement, disables base damage, applies target marker, and forces one-correct-draw defeat.
- `Level1TutorialAnalytics`
  - Optional local logger for attempt counts and assist/skip outcomes.

Shared-system touch points, kept safe:

- `LevelFlowController`
  - Current project already calls a tutorial before starting BGM and waves.
  - For Level 1, route that tutorial call to `Level1InteractiveTutorialController.Play()` when scene and level guards match.
  - For every other level, preserve current flow.
- `LevelTutorialProgress`
  - Reuse existing Level 1 progress key, but mark complete only after `S06_ReleaseToLevel` or confirmed `S07_SkipPath`.
  - Do not mark seen on first panel display.
- `StrokeCapture`
  - Preferred small shared addition: expose a passive `OnStrokesSubmitted` event with submitted stroke points.
  - This event must not change recognition behavior.
  - `Level1TutorialGlyphValidator` subscribes only while `Level1InteractiveTutorialController` is active.
- `RecognitionManager`
  - Do not change global thresholds for all levels.
  - Use `OnRecognitionResolved` and `OnCharacterRecognized` for Level 1 prompts.
  - Any tutorial-only tolerance/direction check stays inside `Level1TutorialGlyphValidator`.
- `CombatResolver`
  - Do not alter global combat routing.
  - Tutorial enemies should be handled by tutorial state logic or flagged as tutorial-owned, so normal combat remains unchanged for other levels.
- `WaveManager`
  - Normal waves remain unstarted until the tutorial releases.
  - Do not insert tutorial enemies into regular wave configs unless the Level 1 config references tutorial-only waves.

Level 1 isolation checklist:

- Scene guard: `SceneManager.GetActiveScene().name == "Level_01_Tutorial"`.
- Data guard: `GameManager.CurrentLevel.levelNumber == 1`.
- Controller is scene-local, not a global singleton.
- New tutorial data asset is referenced only by `Level_01_Tutorial`.
- Skip/progress keys are Level 1-specific.
- Any shared script change is passive by default and covered by non-Level-1 regression tests.
- No changes to enemy health, recognition thresholds, wave pacing, or combat behavior outside guarded tutorial code.

Suggested data assets:

- `Assets/ScriptableObjects/Tutorial/Level1TutorialSequence.asset`
- `Assets/ScriptableObjects/Tutorial/Level1TutorialStep_BA.asset`
- `Assets/ScriptableObjects/Tutorial/Level1TutorialStep_SA.asset`
- `Assets/ScriptableObjects/Tutorial/Level1TutorialStep_LA.asset`
- `Assets/ScriptableObjects/Tutorial/Level1TutorialStep_HA.asset`
- `Assets/Resources/Templates/HA_template_01.txt` or equivalent final template source, if missing.

Suggested acceptance tests:

- Loading any scene except `Level_01_Tutorial` does not instantiate or run the interactive tutorial.
- In `Level_01_Tutorial`, normal waves do not start until tutorial completion or skip.
- Drawing `BA` correctly defeats only the first tutorial enemy and unlocks Skip.
- Drawing another syllable during `BA` prompt does not damage enemies or base.
- Three failed attempts trigger assist and advance the prompt.
- Tutorial enemy cannot hit the base while a prompt is active.
- Completing or skipping the tutorial marks Level 1 tutorial progress.
- Replaying later levels does not show Level 1 tutorial UI.

## 8. Notes For Current Repo

The current repo already contains these relevant systems:

- `Assets/Scripts/Gameplay/LevelFlowController.cs`
- `Assets/Scripts/UI/TutorialOverlayController.cs`
- `Assets/Scripts/UI/LevelTutorialProgress.cs`
- `Assets/Scripts/Gameplay/Recognition/StrokeCapture.cs`
- `Assets/Scripts/Core/RecognitionManager.cs`
- `Assets/Scripts/Gameplay/Combat/CombatResolver.cs`
- `Assets/Scripts/Gameplay/Wave/WaveManager.cs`
- `Assets/Scripts/Gameplay/Wave/WaveSpawner.cs`
- `Assets/Scripts/Gameplay/Enemy/Enemy.cs`
- `Assets/Scripts/Gameplay/Enemy/EnemyMover.cs`

Current `TutorialOverlayController` is a simple 3-step dismissible overlay. This design replaces the Level 1 experience with an interactive state machine, but it should not remove the simple overlay unless the Level 1 scene has been migrated and tested.
