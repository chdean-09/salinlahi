# 09 — Test Strategy and Acceptance Criteria
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Whole Team (QA responsibility shared)

---

## 1. Testing Philosophy

Salinlahi has no automated unit test suite in its current implementation. All testing is manual, device-based, and sprint-end structured. Testing prioritizes:

1. **Core loop integrity** — draw → defeat → win/lose must be unbreakable.
2. **Recognition accuracy** — $P must pass the 60% confidence threshold for correctly shaped characters.
3. **Platform stability** — zero crashes on Android target hardware.
4. **Offline guarantee** — no network calls at any point.

---

## 2. Test Matrix by System Area

### 2.1 Core Systems

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| CS-01 | Only one GameManager instance exists at runtime | Load Bootstrap, play 2 full levels, open GameOver, reload | `GameManager.Instance` count == 1 throughout; no "Destroying duplicate" log spam | P0 |
| CS-02 | SceneLoader resets Time.timeScale before every scene load | Pause game (timeScale=0), trigger GameOver | Next scene runs at normal speed (1f) | P0 |
| CS-03 | EventBus subscriptions do not leak across scenes | Play Gameplay, go to GameOver, return to Gameplay | No duplicate event handler errors; `OnGameOver` fires exactly once per game-over event | P0 |
| CS-04 | BootstrapLoader auto-navigates to MainMenu after one frame | Launch app | MainMenu scene loads within 2 seconds of cold start | P0 |
| CS-05 | AudioManager plays pronunciation clip on enemy defeat | Defeat an enemy with correct drawing | Device audio emits pronunciation clip within 50ms of defeat | P1 |
| CS-06 | DebugLogger produces zero output in release build | Install release APK; monitor logcat | No `[Salinlahi]` or DebugLogger output in logcat | P1 |

### 2.2 Enemy System

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| EN-01 | Enemy moves top-to-bottom in portrait orientation | Spawn enemy; observe 5 seconds | Enemy `transform.position.y` decreases monotonically | P0 |
| EN-02 | Enemy returns to pool on defeat (no Destroy call) | Defeat 50 enemies across a wave | Unity Profiler shows 0 `Destroy` calls during wave; enemy count in pool increases | P0 |
| EN-03 | Enemy returns to pool on base hit | Allow 5 enemies to reach PlayerBase | Enemies deactivated after base hit; no null reference errors | P0 |
| EN-04 | EnemyMover stops on `OnDisable` | Force-deactivate an active enemy | `_active = false`; no further `transform.Translate` calls | P1 |
| EN-05 | EnemyPool uses lazy creation with no pre-warmed enemies | Inspect pool immediately after Bootstrap; then trigger first wave | Pool enemy count == 0 after Bootstrap; first `EnemyPool.Get()` call triggers `CreateEnemy()` and returns a valid enemy; `defaultCapacity` (10) only sets internal list allocation size, not pre-instantiated object count | P2 |
| EN-06 | `Enemy.Initialize` sets correct speed and sprite | Spawn Soldado with known EnemyDataSO | Enemy speed matches `EnemyDataSO.moveSpeed`; sprite matches `walkFrames[0]` | P1 |
| EN-07 | Fraile phaser label fades in/out on timer | Spawn Fraile enemy; observe 10 seconds | Baybayin label alternates between visible and hidden at `phaserInterval` rate | P1 |
| EN-08 | Maestro decoy penalizes player when drawn | Spawn Maestro; draw its displayed character | Player loses 1 heart; Maestro remains active | P1 |
| EN-09 | General commander aura buffs nearby American enemies | Spawn General with 3 Soldiers nearby | Soldiers move at 1.3× speed while General alive; normal speed after General defeated | P1 |
| EN-10 | Kempei censor scrambles nearby labels | Spawn Kempei with 3 enemies nearby | Nearby enemy labels show wrong characters while Kempei alive; correct labels restored after Kempei defeated | P1 |
| EN-11 | Capitan/Shokan require 2 hits to defeat | Spawn Capitan; draw correct character once | Capitan shows armor break but remains active; second correct draw defeats it | P1 |

### 2.3 Recognition System (PLANNED — verify in Sprint 2)

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| RC-01 | Correct character drawing defeats matched enemy | Draw the character shown on an active enemy | Matched enemy calls `Defeat()` within 50ms of finger lift | P0 |
| RC-02 | Incorrect drawing does not defeat enemy | Draw a different character from what enemy displays | Enemy remains active; `OnDrawingFailed` fires; red flash shown | P0 |
| RC-03 | Minimum confidence 0.60 enforced | Draw near-correct character (deliberately sloppy) | Recognition rejects at confidence < 0.60 | P0 |
| RC-04 | Recognition latency < 50ms | Use `Stopwatch` around DollarPRecognizer call; run 100 iterations | 95th percentile latency ≤ 50ms on mid-range Android | P0 |
| RC-05 | Multi-stroke window (1.5s) waits correctly | Draw a multi-stroke character with pauses between strokes | Recognition triggered 1.5s after last finger lift, not after each stroke | P1 |
| RC-06 | Minimum point count 8 filters out taps | Tap screen (no drag) | No recognition attempt; `OnDrawingFailed` fires or tap is silently ignored | P1 |
| RC-07 | All 17 templates load without error at startup | Launch app with all template files present | No `NullReferenceException` in logcat; `TemplateLoader` reports 17 loaded | P0 |

### 2.4 Wave System (PLANNED — verify in Sprint 2/3)

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| WV-01 | Waves play in order from LevelConfigSO | Play Level 1 to completion | Waves fire in index order 0→N; `OnWaveStarted` fires with correct index | P0 |
| WV-02 | waveStartDelay respected | Observe first enemy spawn time after wave start | First enemy spawns exactly `waveStartDelay` seconds after `OnWaveStarted` | P1 |
| WV-03 | spawnInterval respected between enemies | Time consecutive enemy spawns | Interval between spawns matches `WaveConfigSO.spawnInterval` ± 100ms | P1 |
| WV-04 | Level completes after all waves and enemies cleared | Clear all enemies in all waves | `OnLevelComplete` fires; `GameState.LevelComplete` set | P0 |
| WV-05 | Hearts decrement on base hit | Allow 1 enemy to reach base | `OnHeartsChanged(2)` fires; HUD shows 2 hearts | P0 |
| WV-06 | 3 base hits trigger GameOver | Allow 3 enemies to reach base | `OnGameOver` fires; `GameState.GameOver`; GameOver scene loads | P0 |

### 2.5 Performance

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| PF-01 | 60 fps during wave gameplay | Run Unity Profiler during 5-enemy wave on target Android | Frame time ≤ 16.7ms for ≥ 95% of frames; no GC spikes > 1ms | P0 |
| PF-02 | Cold start < 5 seconds | Time from tap on app icon to MainMenu visible | ≤ 5 seconds on target device | P1 |
| PF-03 | APK size < 100 MB | Check final build size | APK ≤ 100 MB | P1 |
| PF-04 | Zero runtime Instantiate/Destroy in game loop | Profile wave gameplay | Unity Profiler shows 0 `Instantiate`/`Destroy` calls during active wave | P0 |

### 2.7 Boss System (PLANNED — verify in Sprint 3)

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| BS-01 | Boss spawns at boss level (5, 10, 15) | Play Level 5 to final wave completion | Boss encounter activates after waves; `OnBossSpawned` fires | P0 |
| BS-02 | Boss phase transitions work | Defeat boss phase 1 requirement | `OnBossPhaseCleared` fires; boss transitions to phase 2 | P0 |
| BS-03 | Boss defeat triggers level complete | Clear all boss phases | `OnBossDefeated` fires; `OnLevelComplete` fires | P0 |
| BS-04 | Kadiliman requires all 17 characters | Play Level 15 boss | Player must draw all 17 characters to defeat Kadiliman | P1 |

### 2.8 Dialogue System (PLANNED — verify in Sprint 3)

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| DL-01 | Type A dialogue pauses gameplay | Trigger pre-level dialogue | Time.timeScale == 0; enemies do not move; typewriter text plays | P1 |
| DL-02 | Tap skips typewriter then advances line | During Type A dialogue, tap once then again | First tap completes line; second tap advances to next line | P1 |
| DL-03 | Type B popup does not pause gameplay | Trigger in-wave popup | Enemies continue moving; popup shows for 3-4 seconds then fades | P2 |

### 2.6 Platform / Offline

| Test ID | Requirement | Test Procedure | Pass Criterion | Priority |
|---------|-------------|---------------|---------------|----------|
| PL-01 | App runs fully offline | Disable WiFi and cellular; play full level | All features functional; no network error dialogs | P0 |
| PL-02 | Portrait-only orientation enforced | Rotate device to landscape during gameplay | Screen does not rotate; game remains in portrait | P0 |
| PL-03 | Zero crashes in 15-level playthrough | UAT participant plays all 15 levels | 0 crash reports via Play Store/TestFlight | P0 |
| PL-04 | Keystore-signed Android build installs | Install release APK on physical device | App installs and launches without error | P1 |

---

## 3. Regression Checklist (Run Before Each Sprint Sign-Off)

- [ ] Bootstrap → MainMenu auto-transition works
- [ ] Play button navigates to Level Select (Sprint 2+) or Gameplay (Sprint 1)
- [ ] Enemy spawns and moves down screen
- [ ] Enemy returns to pool on defeat (not destroyed)
- [ ] Enemy returns to pool on base hit
- [ ] GameOver fires when hearts reach 0
- [ ] GameOver scene loads after game over state
- [ ] Retry button reloads Gameplay
- [ ] Menu button returns to MainMenu
- [ ] No duplicate Singleton warnings in console
- [ ] No null reference exceptions in console
- [ ] Audio plays on enemy defeat (Sprint 2+)
- [ ] Time.timeScale = 1f on scene load
- [ ] App runs offline (airplane mode test)
- [ ] Combo counter increments on consecutive correct draws (Sprint 2+)
- [ ] Combo resets on miss or base hit (Sprint 2+)
- [ ] Type A dialogue panels pause and resume correctly (Sprint 3+)

---

## 4. UAT Readiness Criteria (Sprint 5)

User Acceptance Testing targets 50–100 participants per `GDD.md §6.1`.

### 4.1 Technical Readiness Gate

All the following must be true before UAT begins:

| Gate | Check |
|------|-------|
| Levels 1–10 are playable end-to-end | Verified by internal playthrough |
| Recognition accuracy ≥ 80% for correctly shaped draws on device | Measured across 10 players × 5 characters each |
| 0 crashes in 2-hour internal session | Logged via Unity Cloud Diagnostics or manual log review |
| Audio plays on all 17 character defeats | All `pronunciationClip` fields assigned |
| HUD shows correct heart count and wave number | Functional HUD with EventBus integration |
| Game Over screen shows stats | Final stats display implemented |

### 4.2 UAT Instruments

| Tool | Purpose | Source |
|------|---------|--------|
| System Usability Scale (SUS) | Measures whether drawing input feels natural and usable under real gameplay conditions. Benchmark: a score of 68 or above indicates acceptable usability. | Salinlahi.md §3.5.2 |
| Game Experience Questionnaire (GEQ) core module | Measures player enjoyment and engagement | Salinlahi.md §3.5.1 |
| Pre/post Baybayin character recall test | Measures learning outcome | Salinlahi.md — educational objective |
| Session completion rate | % of participants who finish Level 1 | Engagement proxy |
| Crash / error log review | Technical stability | Sprint 5 QA |

[EVIDENCE: docs/capstone/Salinlahi.md, §3.5.2 — "participants complete the System Usability Scale (SUS) after playing through the first two levels"]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.5.1 — "Game Experience Questionnaire core module during user testing"]
[EVIDENCE: docs/capstone/GDD.md, §6.1 Sprint 5 — "User Acceptance Testing with 50–100 participants"]
