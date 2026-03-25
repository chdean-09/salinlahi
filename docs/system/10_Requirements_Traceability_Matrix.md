# 10 — Requirements Traceability Matrix
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Jon Wayne Cabusbusan

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ Implemented | Evidence found in code |
| ⚠ Partial | Some evidence; not complete |
| ❌ NOT FOUND | No implementation evidence |
| 🔴 P0 | Blocks core gameplay — must be fixed this sprint |
| 🟠 P1 | Required before UAT — must be fixed by Sprint 4 |
| 🟡 P2 | Desirable — can be deferred to Sprint 5 |

---

## Requirements Traceability Matrix

| Req ID | Source Statement | Source Ref | Priority | Implementation Evidence | Test ID | Status | Gap Severity |
|--------|-----------------|-----------|----------|------------------------|---------|--------|-------------|
| REQ-01 | The game shall target Android and iOS devices in portrait orientation | GDD §1.3 | P0 | `ProjectSettings.asset` (portrait lock), git commit `ddc6ea3` | PL-02 | ✅ Implemented | None |
| REQ-02 | The game shall run fully offline with zero network calls | GDD §1.3; Salinlahi.md §1.5.1 | P0 | No network APIs in any script | PL-01 | ✅ Implemented | None |
| REQ-03 | The app shall cold-start to gameplay in under 5 seconds | TDD §7.3 | P1 | BootstrapLoader one-frame wait + async scene load | PF-02 | ⚠ Partial | Not measured yet |
| REQ-04 | The game shall maintain 60 fps consistently during wave gameplay | TDD §7.3 | P0 | ObjectPool eliminates GC; no alloc in Update loops | PF-01 | ⚠ Partial | Not profiled yet |
| REQ-05 | Recognition latency shall be under 50ms from finger lift to combat result | TDD §7.3; Salinlahi.md §3.3.3 | P0 | ❌ DollarPRecognizer not implemented | RC-04 | ❌ NOT FOUND | 🔴 P0 |
| REQ-06 | The APK/IPA size shall be under 100 MB | TDD §7.3 | P1 | Pixel art assets used; no large binaries confirmed | PF-03 | ⚠ Partial | Not measured yet |
| REQ-07 | Enemies shall spawn at the top of the screen and move downward toward the base | GDD §2 core loop; Salinlahi.md §3.5.1 | P0 | `EnemyMover.Update()` — `Vector2.down * _speed * Time.deltaTime` | EN-01 | ✅ Implemented | None |
| REQ-08 | The player shall defeat an enemy by drawing the Baybayin character displayed on it | GDD §2; Salinlahi.md §3.5.1 | P0 | `Enemy.Defeat()` implemented; ❌ RecognitionManager not implemented | RC-01 | ⚠ Partial | 🔴 P0 |
| REQ-09 | A correctly recognized drawing shall trigger `Enemy.Defeat()` within 50ms | TDD §3.3 | P0 | ❌ RecognitionManager not implemented | RC-01, RC-04 | ❌ NOT FOUND | 🔴 P0 |
| REQ-10 | The $P algorithm shall resample strokes to 32 points | Salinlahi.md §3.3.3; RecognitionConfigSO | P0 | `RecognitionConfigSO.resamplePointCount = 32` defined; ❌ DollarPRecognizer not implemented | RC-04 | ⚠ Partial | 🔴 P0 |
| REQ-11 | Recognition shall require minimum confidence score of 0.60 | Salinlahi.md §3.3.3; RecognitionConfigSO | P0 | `RecognitionConfigSO.minimumConfidence = 0.60f` defined; ❌ DollarPRecognizer not implemented | RC-03 | ⚠ Partial | 🔴 P0 |
| REQ-12 | Recognition shall cover 17 Baybayin consonant characters | Salinlahi.md §1.5.1; §3.3.3 | P0 | `RecognitionConfigSO` implies 17 templates; ❌ TemplateLoader not implemented | RC-07 | ❌ NOT FOUND | 🔴 P0 |
| REQ-13 | The multi-stroke window shall be 1.5 seconds after last finger lift | Salinlahi.md §3.3.3; RecognitionConfigSO | P1 | `RecognitionConfigSO.multiStrokeWindowSeconds = 1.5f` defined; ❌ StrokeCapture not implemented | RC-05 | ⚠ Partial | 🟠 P1 |
| REQ-14 | Strokes with fewer than 8 points shall be rejected as taps | RecognitionConfigSO | P1 | `RecognitionConfigSO.minimumPointCount = 8` defined; ❌ StrokeCapture not implemented | RC-06 | ⚠ Partial | 🟠 P1 |
| REQ-15 | An enemy reaching the PlayerBase shall decrement hearts by 1 | GDD §2.3; TDD §3.3 | P0 | `EnemyMover.OnTriggerEnter2D` fires `RaiseBaseHit()`; ❌ HeartSystem not implemented | WV-05 | ⚠ Partial | 🔴 P0 |
| REQ-16 | Hearts shall start at 3 per level | GDD §2.3 | P0 | ❌ HeartSystem not implemented | WV-05 | ❌ NOT FOUND | 🔴 P0 |
| REQ-17 | When hearts reach 0, GameOver state shall be triggered | GDD §2.3; TDD §3.3 | P0 | `GameManager.HandleGameOver()` listens to `OnGameOver`; ❌ HeartSystem does not fire it yet | WV-06 | ⚠ Partial | 🔴 P0 |
| REQ-18 | GameOver shall load the GameOver scene | GDD §5.1 | P0 | `GameManager.HandleGameOver()` → `SceneLoader.LoadGameOver()` | CS-01 | ✅ Implemented | None |
| REQ-19 | A pronunciation audio clip shall play on every correct enemy defeat | TDD §6; GDD §5.4 | P1 | `AudioManager.PlayPronunciationClip()` subscribed to `OnEnemyDefeated` | CS-05 | ⚠ Partial | Missing clips |
| REQ-20 | BGM shall loop during gameplay | TDD §6 | P2 | `AudioManager.PlayBGM()` sets `loop = true` | — | ⚠ Partial | Missing clip asset |
| REQ-21 | All manager singletons shall persist across scene loads via DontDestroyOnLoad | TDD §1 | P0 | `Singleton<T>.Awake()` — DontDestroyOnLoad confirmed | CS-01 | ✅ Implemented | None |
| REQ-22 | Only one instance of each Singleton type shall exist at runtime | TDD §1 | P0 | `Singleton<T>.Awake()` — duplicate destruction confirmed | CS-01 | ✅ Implemented | None |
| REQ-23 | Enemies shall be managed via Unity ObjectPool; no Instantiate/Destroy in game loop | TDD §1; ObjectPool.cs comment | P0 | `EnemyPool` + `ObjectPool<Enemy>` confirmed | EN-02 | ✅ Implemented | None |
| REQ-24 | Story Mode shall have 15 levels across 3 chapters | GDD §2.4 | P1 | `LevelConfigSO` supports structure; ❌ only 3 levels authored (Sprint target) | WV-01 | ⚠ Partial | 🟠 P1 |
| REQ-25 | Boss encounters shall occur at levels 5, 10, 15 | GDD §2.4; TDD §3.2 | P1 | ❌ BossConfigSO not implemented | — | ❌ NOT FOUND | 🟠 P1 |
| REQ-26 | WaveManager shall read LevelConfigSO and drive wave spawning | TDD §3.2; Salinlahi.md §3.5.1 | P0 | ❌ WaveManager not implemented | WV-01 | ❌ NOT FOUND | 🔴 P0 |
| REQ-27 | Wave spawning shall respect waveStartDelay and spawnInterval from WaveConfigSO | TDD §3.2 | P1 | ❌ WaveSpawner not implemented | WV-02, WV-03 | ❌ NOT FOUND | 🟠 P1 |
| REQ-28 | The Lite build shall restrict access to levels 1–3 only | TDD §7.2; Salinlahi.md §3.4 | P1 | `LevelConfigSO.isAvailableInLite` field defined; ❌ gate logic not implemented | — | ⚠ Partial | 🟠 P1 |
| REQ-29 | The game shall display a Main Menu with Play, Endless Mode, Tracing Dojo, Settings | GDD §5.1 | P1 | `MainMenuUI.Play()` exists; ❌ Endless, Dojo, Settings not implemented | — | ⚠ Partial | 🟠 P1 |
| REQ-30 | The HUD shall display current heart count and wave number | GDD §5.1; TDD §7.4 | P1 | ❌ HUD.cs not implemented | WV-05, WV-01 | ❌ NOT FOUND | 🟠 P1 |
| REQ-31 | Failed strokes shall show a red flash and X mark | GDD §5.4 | P1 | ❌ HUD.cs not implemented | RC-02 | ❌ NOT FOUND | 🟠 P1 |
| REQ-32 | The Tracing Dojo shall allow zero-pressure practice of all 17 characters | GDD §2.4; §5.4 | P2 | ❌ Tracing Dojo scene not implemented | — | ❌ NOT FOUND | 🟡 P2 |
| REQ-33 | Endless Mode shall activate after completing Story Mode or defeating the final boss, with high-score tracking (waves survived, enemies defeated, longest combo) | GDD §2.4; Team README §9 | P2 | ❌ Not implemented | — | ❌ NOT FOUND | 🟡 P2 |
| REQ-34 | Cross-system communication shall use EventBus exclusively | TDD §1; EventBus.cs comment | P0 | All systems use EventBus; no direct cross-manager calls observed | CS-03 | ✅ Implemented | None |
| REQ-35 | EventBus subscriptions shall be in OnEnable and unsubscribed in OnDisable | EventBus.cs comment | P0 | `GameManager`, `AudioManager` — OnEnable/OnDisable confirmed | CS-03 | ✅ Implemented | None |
| REQ-36 | Protagonist shall be visible on screen during gameplay as a 32×32 sprite with 3 era-specific designs | GDD §4.2 | P1 | ❌ Not implemented | — | ❌ NOT FOUND | 🟠 P1 |
| REQ-37 | 12 enemy types shall be era-themed (4 per era: Soldado/Fraile/Guardia/Capitan, Soldier/Maestro/Pensionado/General, Heitai/Kisha/Kempei/Shokan) | GDD §4.3 | P1 | ❌ Only standard enemy implemented | EN-07–EN-11 | ⚠ Partial | 🟠 P1 |
| REQ-38 | Combo system shall track consecutive correct draws; 5-streak triggers 3-second slow effect | GDD §3.2; Team README §9 | P1 | ❌ Not implemented | — | ❌ NOT FOUND | 🟠 P1 |
| REQ-39 | Dialogue panels (Type A) shall appear before/after levels with typewriter effect | GDD §4.5; Team README §12 | P1 | ❌ Not implemented | DL-01, DL-02 | ❌ NOT FOUND | 🟠 P1 |
| REQ-40 | Each era shall have a unique shrine design at 64×96 px with 4 damage states | GDD §4.1 | P2 | ❌ Not implemented | — | ❌ NOT FOUND | 🟡 P2 |
| REQ-41 | Boss encounters shall use phase-based system with BossConfigSO data | GDD §4.3; TDD §3.2 | P1 | ❌ Not implemented | BS-01–BS-04 | ❌ NOT FOUND | 🟠 P1 |
| REQ-42 | Baybayin character set shall include 14 consonants and 3 vowels (A, E/I, O/U) totaling 17 | GDD §3.3 | P0 | `RecognitionConfigSO` implies 17; character type not distinguished | RC-07 | ⚠ Partial | 🟠 P1 |

---

## Summary Counts

| Status | Count |
|--------|-------|
| ✅ Implemented | 9 |
| ⚠ Partial | 14 |
| ❌ NOT FOUND | 19 |
| **Total requirements** | **42** |

| Severity | Count |
|----------|-------|
| 🔴 P0 gaps | 9 |
| 🟠 P1 gaps | 16 |
| 🟡 P2 gaps | 4 |
