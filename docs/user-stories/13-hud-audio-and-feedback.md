# 13 — HUD, audio and feedback

Prefixes **`HUD`**, **`AUD`** (audio), **`FX`** (visual feedback, camera, layout).

---

## HUD

### HUD-11 — Not see developer text in the play column
As a player, I want no debug strings on screen, so that the shipped view looks finished.
- AC: No `Draw: … (…)` or `Type: …` developer labels render during play.
- AC: In-combat tutorial text is legible at phone size.
- System: HUD · enemy debug labels (`RenderOrder.EnemyDebugLabel`), tutorial overlay
- Status: Missing — the 2026-09-14 playtest recorded `Draw: na (NA)` and `Type: hati` rendering tiny in the play column (defect 7), the "DRAW THE GLOWING SYMBOL" tutorial text far too small to read (defect 6), and four UI layers stacking at wave start with no reading order (defect 9).
- Refs: `progress/2026-09-14-level1-playtest.md` defects 6, 7, 9, `Assets/Scripts/Gameplay/Rendering/RenderOrder.cs`

## Audio

### AUD-01 — Hear the syllable when I succeed
As a player, I want the sound of the character I just drew, so that every kill teaches me its pronunciation.
- AC: `OnEnemyDefeated` plays the character's `pronunciationClip` as a one-shot.
- AC: Playback is null-safe — a missing clip never breaks the kill.
- System: Audio · `AudioManager.PlayPronunciationClip`
- Status: Partial — the path is implemented; silence for any character with an unassigned clip. See LEARN-06.
- Refs: `Assets/Scripts/Core/AudioManager.cs`, `docs/system/03_Core_Systems.md` §3.3

## Visual feedback and layout

### FX-04 — See the shrine's damage on the shrine itself
As a player, I want the shrine to change as it takes damage, so that I can read my state from the world.
- AC: A base hit plays feedback on the shrine, and the shrine's visual state reflects accumulated damage.
- System: Feedback · `BaseHitFeedbackController`, shrine art
- Status: Partial — hit feedback exists; the authored damage states are unconfirmed. See CMB-25.
- Refs: `Assets/Scripts/Feedback/BaseHitFeedbackController.cs`

### FX-09 — Read every piece of text on a phone
As a player, I want text large enough to read on a small screen, so that I am not squinting.
- AC: Body copy uses the project's readable font at a minimum legible size on the reference resolution.
- AC: Disabled buttons stay readable.
- System: UI · `TutorialFontProvider`, TMP style assets
- Status: Partial — a readable pixel font and disabled-button legibility were addressed by a dedicated pass, but font size is not a player setting. See ACC-06.
- Refs: `Assets/Scripts/UI/TutorialFontProvider.cs`, `docs/superpowers/plans/2026-05-26-readable-pixel-font-disabled-buttons.md`

### FX-11 — Have the game hold a steady frame rate
As a player, I want smooth play on a mid-range phone, so that drawing stays accurate.
- AC: No `Instantiate`/`Destroy` in the gameplay loop; enemies and VFX are pooled.
- AC: Performance targets and constraints are documented and measured.
- System: Performance · `EnemyPool`, VFX pools
- Status: Partial — the pooling discipline is enforced in code; on-device frame-rate measurement is not recorded as completed.
- Refs: `docs/system/08_Mobile_Performance_and_Offline_Constraints.md`
