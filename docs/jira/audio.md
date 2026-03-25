# Epic: Audio (SALIN-7)

**Status:** ⚠️ Manager Built — No Audio Content Yet | **Priority:** Medium

AudioManager singleton, pronunciation recordings for all 17 Baybayin characters, SFX library, BGM with per-chapter track switching, and final mix pass.

---

## SALIN-24 — Implement AudioManager.cs — Singleton Audio Controller

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-32, SALIN-33, SALIN-34 |
| **Blocked By** | SALIN-9 |

`AudioManager.cs` Singleton. Manages BGM playback, SFX one-shots, and master/BGM/SFX volume control. Wraps two `AudioSource` components (one for BGM, one for SFX). Volume settings persisted to `PlayerPrefs` and restored on next session. Listens to `OnEnemyDefeated` and `OnBaseHit` for automatic SFX hooks. No audio files are present yet — directories created, awaiting content.

**Acceptance Criteria:**
- `PlayBGM(AudioClip)`, `StopBGM()`, `PlaySFX(AudioClip)`, `SetVolume(float master, float bgm, float sfx)` all functional
- BGM loops; switching tracks does not produce audio pops
- Volume settings persist via PlayerPrefs
- `PlaySFX` does not allocate a new AudioSource per call
- All three volume channels independently controllable

---

## SALIN-32 — Record / Source Pronunciation Audio — All 17 Baybayin Characters

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-67 |
| **Blocked By** | SALIN-24, SALIN-77 (all 17 character SOs must exist) |

Locate or record clean `.wav` or `.ogg` pronunciation clips for all 17 Baybayin characters. One clip per character, ideally by a native Filipino speaker. Import into `Assets/Audio/Pronunciation/` and link each clip to the corresponding `BaybayinCharacterSO.pronunciationClip` field.

**Acceptance Criteria:**
- All 17 clips present in `Assets/Audio/Pronunciation/`
- Each `BaybayinCharacterSO.pronunciationClip` field populated
- Clips are clear (no background noise), under 2 seconds each
- `AudioManager.PlaySFX(clip)` plays pronunciation on character recognition
- Clips tested on device speaker — audible and intelligible

> ⚠️ If recording is not possible within Sprint 3, source royalty-free Filipino language audio or use TTS as a placeholder. Do not block the sprint on recording logistics.

---

## SALIN-33 — Implement SFX Library — All Core Game Event Sounds

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-67 |
| **Blocked By** | SALIN-24 |

Source or create SFX for all core game events. Import into `Assets/Audio/SFX/` and wire each sound to the corresponding EventBus event in `AudioManager`.

**Required SFX:**

| Event | Sound |
|-------|-------|
| `OnCharacterRecognized` (correct) | Positive chime / success |
| `OnDrawingFailed` (wrong draw) | Negative buzz |
| `EnemyDefeatedEvent` | Enemy defeat |
| `EnemyShrineReachedEvent` | Base hit / health loss |
| `OnWaveComplete` | Wave clear fanfare |
| `OnLevelComplete` | Level complete sting |
| `OnGameOver` | Defeat sting |
| `OnFocusModeActivated` | Focus mode swoosh |
| `OnBossDefeated` | Boss defeat fanfare |
| Combo streak (every 5) | Streak chime |

**Acceptance Criteria:**
- All 10 SFX clips present in `Assets/Audio/SFX/`
- Each clip wired to its EventBus event handler in `AudioManager`
- No SFX fires during `Paused` state
- All clips tested on device — no clipping or distortion

---

## SALIN-34 — Implement BGM System with Per-Chapter Track Switching

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-67 |
| **Blocked By** | SALIN-24, SALIN-46 |

Source 5 BGM tracks (Main Menu, Chapter 1, Chapter 2, Chapter 3, Boss) and import into `Assets/Audio/BGM/`. `LevelConfigSO` gets a `bgmClip` field. `LevelFlowController` (SALIN-46) calls `AudioManager.PlayBGM(levelConfig.bgmClip)` at level start. Boss wave switches to the Boss BGM track.

**Acceptance Criteria:**
- 5 BGM tracks present in `Assets/Audio/BGM/`
- `LevelConfigSO.bgmClip` field populated for all 15 levels
- BGM switches cleanly on level load (no audio pop)
- Boss wave triggers Boss BGM
- `AudioManager.PlayBGM()` called from `LevelFlowController`, not from scene directly

---

## SALIN-67 — Final Audio Mix Pass — Balance BGM, SFX, and Pronunciation

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | Medium |
| **Sprint** | Sprint 4 |
| **Blocks** | — |
| **Blocked By** | SALIN-32, SALIN-33, SALIN-34 |

After all audio assets are integrated, perform a full balance pass. BGM should sit below SFX in mix priority. Pronunciation clips must be clearly audible over both BGM and ambient SFX. Test on Android device speaker and headphones.

**Acceptance Criteria:**
- BGM volume sits at ~60% relative to SFX by default
- Pronunciation clips are clearly audible with BGM and SFX playing simultaneously
- No single SFX dominates or masks others during typical gameplay
- All three volume sliders in Settings (SALIN-58) adjust levels proportionally
- Tested on device speaker and 3.5mm headphones
