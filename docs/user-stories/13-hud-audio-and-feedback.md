# 13 — HUD, audio and feedback

Prefixes **`HUD`**, **`AUD`** (audio), **`FX`** (visual feedback, camera, layout).

---

## HUD

### HUD-11 — Not see developer text in the play column
As a player, I want no debug strings on screen, so that the shipped view looks finished.
- AC: No `Draw: … (…)` or `Type: …` developer labels render during play.
- AC: In-combat tutorial text is legible at phone size.
- System: HUD · enemy debug labels (`RenderOrder.EnemyDebugLabel`), tutorial overlay
- Status: Partial — defect 7 is fixed: `d4f98b0a` defaulted `Enemy._showDebugLabels` off and removed the `1` authored on the shared corruption shell, so no prefab opts in and the ids no longer render. Defect 6 (tutorial text size) is unchanged — the 38–56 pt auto-size band predates the playtest. Defect 9 is partly addressed: `8debc76a` cut the miss message and two backing overlays and `f41fc858` stopped the discovery overlay pausing and spotlighting on introduction spawns, leaving the wave banner and the tutorial overlay still opening in the same frame.
- Refs: `progress/2026-09-14-level1-playtest.md` defects 6, 7, 9, `Assets/Scripts/Gameplay/Enemy/Enemy.cs` (`_showDebugLabels`), `d4f98b0a`, `8debc76a`, `f41fc858`, `Assets/Scripts/Gameplay/Rendering/RenderOrder.cs`

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
