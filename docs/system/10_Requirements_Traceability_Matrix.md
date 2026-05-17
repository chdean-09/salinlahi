# 10 â€” Requirements Traceability Matrix
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Jon Wayne Cabusbusan

---

## Legend

| Symbol | Meaning |
|--------|---------|
| âœ… Implemented | Evidence found in code |
| âš  Partial | Some evidence; not complete |
| âŒ NOT FOUND | No implementation evidence |
| ðŸ”´ P0 | Blocks core gameplay â€” must be fixed this sprint |
| ðŸŸ  P1 | Required before UAT â€” must be fixed by Sprint 4 |
| ðŸŸ¡ P2 | Desirable â€” can be deferred to Sprint 5 |

---

## Requirements Traceability Matrix

| Req ID | Source Statement | Source Ref | Priority | Implementation Evidence | Test ID | Status | Gap Severity |
|--------|-----------------|-----------|----------|------------------------|---------|--------|-------------|
| REQ-01 | The game shall target Android and iOS devices in portrait orientation | GDD Â§1.3 | P0 | `ProjectSettings.asset` (portrait lock), git commit `ddc6ea3` | PL-02 | âœ… Implemented | None |
| REQ-02 | The game shall run fully offline with zero network calls | GDD Â§1.3; Salinlahi.md Â§1.5.1 | P0 | No network APIs in any script | PL-01 | âœ… Implemented | None |
| REQ-03 | The app shall cold-start to gameplay in under 5 seconds | TDD Â§7.3 | P1 | BootstrapLoader one-frame wait + async scene load | PF-02 | âš  Partial | Not measured yet |
| REQ-04 | The game shall maintain 60 fps consistently during wave gameplay | TDD Â§7.3 | P0 | ObjectPool eliminates GC; no alloc in Update loops | PF-01 | âš  Partial | Not profiled yet |
| REQ-05 | Recognition latency shall be under 50ms from finger lift to combat result | TDD Â§7.3; Salinlahi.md Â§3.3.3 | P0 | `DollarPRecognizer.cs` â€” latency not yet profiled on target device | RC-04 | âš  Partial | ðŸŸ  P1 |
| REQ-06 | The APK/IPA size shall be under 100 MB | TDD Â§7.3 | P1 | Pixel art assets used; no large binaries confirmed | PF-03 | âš  Partial | Not measured yet |
| REQ-07 | Enemies shall spawn at the top of the screen and move downward toward the base | GDD Â§2 core loop; Salinlahi.md Â§3.5.1 | P0 | `EnemyMover.Update()` â€” `Vector2.down * _speed * Time.deltaTime` | EN-01 | âœ… Implemented | None |
| REQ-08 | The player shall defeat an enemy by drawing the Baybayin character displayed on it | GDD Â§2; Salinlahi.md Â§3.5.1 | P0 | `Enemy.Defeat()` + `RecognitionManager.cs` + `WaveManager.cs` | RC-01 | âœ… Implemented | None |
| REQ-09 | A correctly recognized drawing shall trigger `Enemy.Defeat()` within 50ms | TDD Â§3.3 | P0 | `RecognitionManager.cs` â€” latency not profiled on device yet | RC-01, RC-04 | âš  Partial | ðŸŸ  P1 |
| REQ-10 | The $P algorithm shall resample strokes to 32 points | Salinlahi.md Â§3.3.3; RecognitionConfigSO | P0 | `RecognitionConfigSO.resamplePointCount = 32`; `DollarPRecognizer.cs` | RC-04 | âœ… Implemented | None |
| REQ-11 | Recognition shall require minimum confidence score of 0.60 | Salinlahi.md Â§3.3.3; RecognitionConfigSO | P0 | `RecognitionConfigSO.minimumConfidence = 0.60f`; `DollarPRecognizer.cs` | RC-03 | âœ… Implemented | None |
| REQ-12 | Recognition shall cover 17 Baybayin consonant characters | Salinlahi.md Â§1.5.1; Â§3.3.3 | P0 | `TemplateLoader.cs` loads from `Resources/Templates/`; template .txt files status unverified | RC-07 | âš  Partial | ðŸŸ  P1 |
| REQ-13 | The multi-stroke window shall be 1.5 seconds after last finger lift | Salinlahi.md Â§3.3.3; RecognitionConfigSO | P1 | `RecognitionConfigSO.multiStrokeWindowSeconds = 1.5f`; `StrokeCapture.cs` | RC-05 | âœ… Implemented | None |
| REQ-14 | Strokes with fewer than 8 points shall be rejected as taps | RecognitionConfigSO | P1 | `RecognitionConfigSO.minimumPointCount = 8`; `StrokeCapture.cs` | RC-06 | âœ… Implemented | None |
| REQ-15 | An enemy reaching the PlayerBase shall decrement hearts by 1 | GDD Â§2.3; TDD Â§3.3 | P0 | `EnemyMover.OnTriggerEnter2D` fires `RaiseBaseHit()`; `HeartSystem.cs` | WV-05 | âœ… Implemented | None |
| REQ-16 | Hearts shall start at 3 per level | GDD Â§2.3 | P0 | `HeartSystem.cs` | WV-05 | âœ… Implemented | None |
| REQ-17 | When hearts reach 0, GameOver state shall be triggered | GDD Â§2.3; TDD Â§3.3 | P0 | `HeartSystem.cs` fires `OnGameOver`; `GameManager.HandleGameOver()` responds | WV-06 | âœ… Implemented | None |
| REQ-18 | GameOver shall load the GameOver scene | GDD Â§5.1 | P0 | `GameManager.HandleGameOver()` â†’ `SceneLoader.LoadGameOver()` | CS-01 | âœ… Implemented | None |
| REQ-19 | A pronunciation audio clip shall play on every correct enemy defeat | TDD Â§6; GDD Â§5.4 | P1 | `AudioManager.PlayPronunciationClip()` subscribed to `OnEnemyDefeated` | CS-05 | âš  Partial | Missing clips |
| REQ-20 | BGM shall loop during gameplay | TDD Â§6 | P2 | `AudioManager.PlayBGM()` sets `loop = true` | â€” | âš  Partial | Missing clip asset |
| REQ-21 | All manager singletons shall persist across scene loads via DontDestroyOnLoad | TDD Â§1 | P0 | `Singleton<T>.Awake()` â€” DontDestroyOnLoad confirmed | CS-01 | âœ… Implemented | None |
| REQ-22 | Only one instance of each Singleton type shall exist at runtime | TDD Â§1 | P0 | `Singleton<T>.Awake()` â€” duplicate destruction confirmed | CS-01 | âœ… Implemented | None |
| REQ-23 | Enemies shall be managed via Unity ObjectPool; no Instantiate/Destroy in game loop | TDD Â§1; ObjectPool.cs comment | P0 | `EnemyPool` + `ObjectPool<Enemy>` confirmed | EN-02 | âœ… Implemented | None |
| REQ-24 | Story Mode shall have 15 levels across 3 chapters | GDD Â§2.4 | P1 | `LevelConfigSO` supports structure; âŒ only 3 levels authored (Sprint target) | WV-01 | âš  Partial | ðŸŸ  P1 |
| REQ-25 | Boss encounters shall occur at levels 5, 10, 15 | GDD Â§2.4; TDD Â§3.2 | P1 | âŒ BossConfigSO not implemented | â€” | âŒ NOT FOUND | ðŸŸ  P1 |
| REQ-26 | WaveManager shall read LevelConfigSO and drive wave spawning | TDD Â§3.2; Salinlahi.md Â§3.5.1 | P0 | `WaveManager.cs` + `WaveSpawner.cs` | WV-01 | âœ… Implemented | None |
| REQ-27 | Wave spawning shall respect waveStartDelay and spawnInterval from WaveConfigSO | TDD Â§3.2 | P1 | `WaveSpawner.cs` reads `WaveConfigSO` | WV-02, WV-03 | âœ… Implemented | None |
| REQ-28 | The Lite build shall restrict access to levels 1â€“3 only | TDD Â§7.2; Salinlahi.md Â§3.4 | P1 | `LevelConfigSO.isAvailableInLite` field defined; âŒ gate logic not implemented | â€” | âš  Partial | ðŸŸ  P1 |
| REQ-29 | The game shall display a Main Menu with Play, Endless Mode, Tracing Dojo, Settings | GDD Â§5.1 | P1 | `MainMenuUI.Play()` exists; âŒ Endless, Dojo, Settings not implemented | â€” | âš  Partial | ðŸŸ  P1 |
| REQ-30 | The HUD shall display current heart count and wave number | GDD Â§5.1; TDD Â§7.4 | P1 | `HUD.cs` | WV-05, WV-01 | âœ… Implemented | None |
| REQ-31 | Failed strokes shall show a red flash and X mark | GDD Â§5.4 | P1 | `HUD.cs` | RC-02 | âœ… Implemented | None |
| REQ-32 | The Tracing Dojo shall allow zero-pressure practice of all 17 characters | GDD Â§2.4; Â§5.4 | P2 | âŒ Tracing Dojo scene not implemented | â€” | âŒ NOT FOUND | ðŸŸ¡ P2 |
| REQ-33 | Endless Mode shall activate after completing Story Mode or defeating the final boss, with high-score tracking (waves survived, enemies defeated, longest combo) | GDD Â§2.4; Team README Â§9 | P2 | âŒ Not implemented | â€” | âŒ NOT FOUND | ðŸŸ¡ P2 |
| REQ-34 | Cross-system communication shall use EventBus exclusively | TDD Â§1; EventBus.cs comment | P0 | All systems use EventBus; no direct cross-manager calls observed | CS-03 | âœ… Implemented | None |
| REQ-35 | EventBus subscriptions shall be in OnEnable and unsubscribed in OnDisable | EventBus.cs comment | P0 | `GameManager`, `AudioManager` â€” OnEnable/OnDisable confirmed | CS-03 | âœ… Implemented | None |
| REQ-36 | Protagonist shall be visible on screen during gameplay as a 32Ã—32 sprite with 3 era-specific designs | GDD Â§4.2 | P1 | âŒ Not implemented | â€” | âŒ NOT FOUND | ðŸŸ  P1 |
| REQ-37 | 12 enemy types shall be era-themed (4 per era: Soldado/Fraile/Guardia/Capitan, Soldier/Maestro/Pensionado/General, Heitai/Kisha/Kempei/Shokan) | GDD Â§4.3 | P1 | âŒ Only standard enemy implemented | EN-07â€“EN-11 | âš  Partial | ðŸŸ  P1 |
| REQ-38 | Combo system shall track consecutive correct draws; 5-streak triggers focus mode slow effect | GDD Â§3.2; Team README Â§9 | P1 | `ComboManager.cs` | â€” | âœ… Implemented | None |
| REQ-39 | Dialogue panels (Type A) shall appear before/after levels with typewriter effect | GDD Â§4.5; Team README Â§12 | P1 | `DialogueController.cs` | DL-01, DL-02 | âœ… Implemented | None |
| REQ-40 | Each era shall have a unique shrine design at 64Ã—96 px with 4 damage states | GDD Â§4.1 | P2 | âŒ Not implemented | â€” | âŒ NOT FOUND | ðŸŸ¡ P2 |
| REQ-41 | Boss encounters shall use phase-based system with BossConfigSO data | GDD Â§4.3; TDD Â§3.2 | P1 | âŒ Not implemented | BS-01â€“BS-04 | âŒ NOT FOUND | ðŸŸ  P1 |
| REQ-42 | Baybayin character set shall include 14 consonants and 3 vowels (A, E/I, O/U) totaling 17 | GDD Â§3.3 | P0 | `RecognitionConfigSO` implies 17; character type not distinguished | RC-07 | âš  Partial | ðŸŸ  P1 |
| REQ-43 | A daily streak counter shall track consecutive days the player opens the game, stored locally via PlayerPrefs | Salinlahi.md Â§1.5.1; Sprint Timeline Sprint 3 | P2 | âŒ StreakManager not implemented | â€” | âŒ NOT FOUND | ðŸŸ¡ P2 |
| REQ-44 | The game shall include in-game SUS and GEQ-S questionnaire screens administered after gameplay during UAT | Sprint Timeline Sprint 4; Salinlahi.md Â§3.5.1, Â§3.5.2 | P0 | âŒ QuestionnaireController not implemented | â€” | âŒ NOT FOUND | ðŸ”´ P0 |
| REQ-45 | The game shall log recognition accuracy per character per level to a CSV file on the device for post-session analysis | Sprint Timeline Sprint 2; Salinlahi.md Â§3.5.3 | P0 | âŒ QuestionnaireLogger not implemented | â€” | âŒ NOT FOUND | ðŸ”´ P0 |
| REQ-46 | The recognition system shall support multiple templates per character to handle handwriting variation | TDD Â§2.2 | P1 | âš  RecognitionConfigSO exists; âŒ multi-template loading not implemented | RC-04 | âš  Partial | ðŸŸ  P1 |
| REQ-47 | First-time Level 1 shall guide the first enemy encounter with deterministic spawn, non-blocking drawing UI, and once-only tutorial progress | SALIN-93 | P1 | `WaveConfigSO` first-spawn override fields, `WaveManager` spawn suspension, `EventBus.OnEnemySpawned`, `LevelTutorialProgress` once-only keys; scene wiring pending | SALIN93-01â€“SALIN93-08, CS-03, WV-01 | âš  Partial | Scene and asset wiring pending |
| REQ-48 | First-time Level 1 shall establish the protagonist, Shrine objective, and progressive tracing assist before normal wave pacing | SALIN-93 supplemental flow | P1 | `Level1WorldIntroController`, `BaybayinTraceGuideController`, `TutorialOverlayController.ShowTraceAssist`, `GuidedLevel1TutorialController` assist fading; scene wiring pending | SALIN93-FLOW-00-SALIN93-FLOW-07, RC-01, RC-02 | âš  Partial | Scene and UI wiring pending |

---

## Summary Counts

| Status | Count |
|--------|-------|
| âœ… Implemented | 23 |
| âš  Partial | 20 |
| âŒ NOT FOUND | 5 |
| **Total requirements** | **48** |

| Severity | Count |
|----------|-------|
| ðŸ”´ P0 gaps | 0 |
| ðŸŸ  P1 gaps | 16 |
| ðŸŸ¡ P2 gaps | 5 |
