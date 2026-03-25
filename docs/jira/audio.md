# Epic: Audio (SALIN-7)

**Status:** To Do | **Priority:** Medium | **Assignee:** Unassigned

AudioManager singleton, pronunciation recordings for all 17 Baybayin characters, SFX library, BGM with per-chapter track switching, and final mix pass.

---

## SALIN-24 — Implement AudioManager.cs — Singleton Audio Controller

| Field | Value |
|-------|-------|
| **Status** | Done |
| **Priority** | Medium |
| **Assignee** | Unassigned |

Implement `AudioManager.cs` as a Singleton that manages all game audio: BGM playback, SFX one-shots, and master/BGM/SFX volume control.

**Acceptance Criteria:**
- `AudioManager` exposes `PlayBGM(AudioClip clip)`, `StopBGM()`, `PlaySFX(AudioClip clip)`, and `SetVolume(float master, float bgm, float sfx)` methods
- BGM loops; calling `PlayBGM` while another clip is playing crossfades or stops and starts cleanly
- Volume settings are persisted to `PlayerPrefs` and restored on next session
- `PlaySFX` can be called from any script without causing AudioSource allocation per frame
- Test: play a BGM clip, play an SFX, adjust volumes — no errors, audio plays as expected

---

## SALIN-32 — Record / Source Pronunciation Audio for All 17 Baybayin Characters

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-33 — Implement SFX Library — All Core Game Event Sounds

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-34 — Implement BGM System with Per-Chapter Track Switching

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |

---

## SALIN-67 — Final Audio Mix Pass — Balance BGM, SFX, and Pronunciation

| Field | Value |
|-------|-------|
| **Status** | To Do |
| **Priority** | Medium |
| **Assignee** | Unassigned |
