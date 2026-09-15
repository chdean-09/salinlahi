# 14 — Accessibility and settings

Prefix **`ACC`**. Covers the settings surface and the accessibility affordances the design commits to.

Almost all of this area is **Missing**: `SettingsPanel` exposes three volume sliders, a close control, and an optional journey-reset entry, and nothing else. The accessibility tickets (SALIN-262 … SALIN-265, SALIN-269) are all To Do and were classed "nice-to-have" for the demo, while ruling §3 makes every content, flow and combat task demo-required and leaves only polish and accessibility optional. Behaviour that already works is in [`COVERAGE.md`](COVERAGE.md).

---

### ACC-03 — Set the voice volume separately from the sound effects
As a player, I want the spoken syllables on their own channel, so that I can raise the learning audio above the rest.
- AC: A Voice slider controls pronunciation clips independently of general SFX.
- AC: Moving the SFX slider does not change pronunciation volume.
- AC: The setting persists across a restart.
- System: Settings · `SettingsPanel`, `AudioManager`
- Status: Missing — the GDD specifies BGM/SFX/Voice; only master, BGM and SFX exist and pronunciation plays on the shared SFX source.
- Refs: `docs/capstone/GDD.md` §5.3, `SettingsPanel.cs:9-12`

### ACC-04 — Play with the sound off and lose nothing
As a player who cannot hear, or is playing muted, I want every spoken thing available as text, so that turning sound off never costs me information.
- AC: With Captions on, every pronunciation shows its syllable as text for the clip's duration.
- AC: Every hint has a text alternative.
- AC: With Narration off, dialogue still shows its text and no voice plays.
- AC: Both settings persist across a restart.
- System: Settings / audio · `SettingsPanel`, `AudioManager`, `ActiveCluePresenter`, `DialogueController`
- Status: Missing — neither a captions nor a narration toggle exists, and there is no caption surface. The composed audio→visual clue fallback (`COVERAGE.md · CMB-12`) partly covers combat clues but is not a caption system.
- Refs: SALIN-262 (To Do), `docs/audit/BACKLOG.md` T50
- Merged: absorbs ACC-05

### ACC-06 — Adjust how text is presented
As a player with limited vision or a different reading pace, I want control over text size, speed and contrast, so that I can read the game comfortably.
- AC: A font-size setting scales all body text.
- AC: A text-speed setting changes the dialogue typewriter immediately.
- AC: A high-contrast setting increases UI contrast across screens.
- AC: All three persist across a restart.
- System: Settings · `SettingsPanel`, `DialogueController`, `CutscenePlayer`, TMP style assets
- Status: Missing — none of the three exists. What "high contrast" changes is itself unspecified (open question).
- Refs: SALIN-264 (To Do), `docs/audit/BACKLOG.md` T52
- Merged: absorbs ACC-07, ACC-08

### ACC-09 — Turn off screen shake and flashing
As a player sensitive to motion or flashing, I want to disable those effects, so that the game is safe for me to play.
- AC: With Reduced Motion on, no camera shake and no full-screen flash plays on a base hit.
- AC: With it off, both play as before.
- AC: The setting persists across a restart.
- System: Settings · `SettingsPanel`, `CameraShakeController`, `DamageEdgeFlashController`, `DrawingFeedback`
- Status: Missing — no reduced-motion setting exists; the effects are unconditional.
- Refs: SALIN-263 (To Do), `docs/audit/BACKLOG.md` T51

### ACC-10 — Read my hearts without counting sprites
As a player, I want my heart count legible at a glance, so that I am not squinting at small icons under pressure.
- AC: The HUD shows "n/3" beside the hearts and updates on `OnHeartsChanged`.
- AC: A Large Hearts setting doubles the heart icon size and persists across a restart.
- System: HUD / Settings · `HeartDisplay`, `SettingsPanel`
- Status: Missing — neither the numeral nor the size setting exists.
- Refs: SALIN-263, `docs/audit/BACKLOG.md` T51
- Merged: absorbs ACC-11

### ACC-12 — Feel a correct trace
As a player, I want haptic confirmation on a correct trace, so that I get feedback without looking or listening.
- AC: With Haptics on, a correct trace vibrates the device once; with it off, it does not.
- AC: The setting persists across a restart.
- System: Settings · `SettingsPanel`, `CombatResolver`
- Status: Missing — no `Vibrate` call exists anywhere under `Assets/Scripts`.
- Refs: SALIN-265 (To Do), ruling Q13 (rescoped the trace-pad requirement to a haptics toggle only)

### ACC-13 — Turn on an assist that makes the game easier without cheapening it
As a player who finds the pace too fast, I want an assist mode I can reach when I need it, so that I can still complete the game honestly.
- AC: Assist slows enemies, extends the trace window and raises guide opacity.
- AC: All mandatory objectives remain required with Assist on.
- AC: The Results details show an assist icon when Assist was on at any point in the attempt, and a replay cannot hide it.
- AC: The defeat screen offers a route to change Difficulty Assist.
- System: Settings · `SettingsPanel`, `EnemyMover`, `StrokeCapture`, `TraceHintPresenter`, `VictoryScreenUI`, `DefeatScreenUI`
- Status: Missing — no assist settings exist and `EnemyMover` has no assist speed factor. The three numeric values are undecided (open question).
- Refs: SALIN-269 (To Do), `docs/audit/BACKLOG.md` T57, T57 (UF-32)
- Merged: absorbs ACC-14

### ACC-15 — Restore all settings to their defaults
As a player who changed too much, I want a one-tap reset, so that I can get back to a known state.
- AC: Restore Defaults resets every `SettingsPanel` value to its default and writes them to `PlayerPrefs`.
- AC: The restored values survive a restart.
- System: Settings · `SettingsPanel`
- Status: Missing
- Refs: SALIN-264, `docs/audit/BACKLOG.md` T52

### ACC-19 — Learn the game by playing it, not by reading instructions
As a player, I want the first level to teach through play, so that I am not made to read a manual.
- AC: Level 1 teaches drawing, the shrine, heart loss and the clue system through in-play beats rather than text screens.
- AC: Every in-play guidance string is legible at 360×640.
- System: Tutorial · `Level1OnboardingController`, onboarding beats
- Status: Partial — beats exist for protagonist intro, base intro, heart-loss demo, mass clear, release and enemy introduction; the solo-teach loop that suppressed the enemy introductions is deleted (`f2c7fe60`) and ENM-29 is now Existing. What remains from the playtest is presentational: the in-combat guidance text unreadable at phone size and the symbol card's `Listen` control near-illegible (defects 5–6). `6fc34851` also recorded that no caller passes `canSkip: true`, so the guide's Skip affordance is unreachable today.
- Refs: `Assets/Scripts/Gameplay/Tutorial/Onboarding/Beats/`, `progress/2026-09-14-level1-playtest.md` defects 5–6, `docs/capstone/GDD.md` §5.5

### ACC-20 — Read every player-facing string in approved copy
As a player, I want no placeholder text, so that the game reads as finished.
- AC: No player-facing string is marked as a placeholder or "NOT PRODUCT-APPROVED".
- AC: The approval is recorded in `docs/review/approval-log.md`.
- System: All UI copy
- Status: Partial — SALIN-291 covered the approval of copy shipped as not-approved, but placeholder markers were recorded in `LevelLockNoticePanel`, `ChallengeModeUI` and `PauseMenuUI`, and several strings still await content approval.
- Refs: SALIN-291, SALIN-266 (To Do), `docs/audit/BACKLOG.md` T54

---

### EVAL-01 — Give feedback on the game as a study participant
As a participant in the evaluation, I want an in-game questionnaire, so that my experience can be recorded.
- AC: A SUS / GEQ-S questionnaire is presented at the agreed point in the session.
- AC: Responses are stored locally; nothing leaves the device.
- System: Analytics · questionnaire controller (not present)
- Status: Unclear — no questionnaire controller exists, and the external instrument's ownership, delivery medium and acceptance are unspecified. See U-11.
- Refs: `docs/capstone/EVALUATION-PROTOCOL.md`, `docs/audit/STATUS-2026-09-13.md` Master row 36

### EVAL-02 — Have my session recorded for the study without leaving the device
As a participant, I want my interactions logged locally, so that the study has data and my privacy is preserved.
- AC: Recognition attempts are logged locally (`RecognitionLogger`).
- AC: UI/button tracking events are emitted to a local offline event log.
- System: Analytics · `RecognitionLogger`, `UiEventLogger` (not present)
- Status: Partial — `RecognitionLogger` exists; no `UiEventLogger` exists, so the specified `BTN-*` tracking events are not emitted. The full 25-event list lives in the workbook, not the repo.
- Refs: `Assets/Scripts/Analytics/RecognitionLogger.cs`, SALIN-267 (To Do), `docs/audit/BACKLOG.md` T55
