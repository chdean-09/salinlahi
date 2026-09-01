# 10 — Requirements Traceability Matrix
**Project:** Salinlahi
**Version:** 1.6
**Date:** 2026-08-27
**Owner:** Jon Wayne Cabusbusan

> **SALIN-186 revision.** This file was stored **double-encoded** — every `—`, `✅`, `⚠`, `❌` and
> severity emoji was mojibake (`â€"`, `âœ…`) across 55 of its 90 lines, and it carried a UTF-8 BOM.
> It was the only document in `docs/system/` affected. Encoding repaired, BOM removed.
>
> Seven rows were also **factually wrong** and are corrected below with evidence — most consequentially
> REQ-45 (recognition CSV logging), recorded as ❌ NOT FOUND at P0 severity while
> `RecognitionLogger.cs` has been writing `recognition_log.csv` all along, and REQ-32 (Tracing Dojo),
> recorded as NOT FOUND while `TracingDojo.unity` exists. The summary counts did not match the rows
> either. See §Backlog Linkage.

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
| REQ-05 | Recognition latency shall be under 50ms from finger lift to combat result | TDD §7.3; Salinlahi.md §3.3.3 | P0 | `DollarPRecognizer.cs` — latency not yet profiled on target device | RC-04 | ⚠ Partial | 🟠 P1 |
| REQ-06 | The APK/IPA size shall be under 100 MB | TDD §7.3 | P1 | Pixel art assets used; no large binaries confirmed | PF-03 | ⚠ Partial | Not measured yet |
| REQ-07 | Enemies shall spawn at the top of the screen and move downward toward the base | GDD §2 core loop; Salinlahi.md §3.5.1 | P0 | `EnemyMover.Update()` — `Vector2.down * _speed * Time.deltaTime` | EN-01 | ✅ Implemented | None |
| REQ-08 | The player shall defeat an enemy by drawing the Baybayin character displayed on it | GDD §2; Salinlahi.md §3.5.1 | P0 | `Enemy.Defeat()` + `RecognitionManager.cs` + `WaveManager.cs` | RC-01 | ✅ Implemented | None |
| REQ-09 | A correctly recognized drawing shall trigger `Enemy.Defeat()` within 50ms | TDD §3.3 | P0 | `RecognitionManager.cs` — latency not profiled on device yet | RC-01, RC-04 | ⚠ Partial | 🟠 P1 |
| REQ-10 | The $P algorithm shall resample strokes to 32 points | Salinlahi.md §3.3.3; RecognitionConfigSO | P0 | `RecognitionConfigSO.resamplePointCount = 32`; `DollarPRecognizer.cs` | RC-04 | ✅ Implemented | None |
| REQ-11 | Recognition shall require minimum confidence score of 0.60 | Salinlahi.md §3.3.3; RecognitionConfigSO | P0 | `RecognitionConfigSO.minimumConfidence = 0.60f`; `DollarPRecognizer.cs` | RC-03 | ✅ Implemented | None |
| REQ-12 | Recognition shall cover the Baybayin taught character set (**17**, matching REQ-42 since 2026-09-01) | Salinlahi.md §1.5.1; §3.3.3 | P0 | `TemplateLoader.cs` loads `Resources/Templates/` — **121 template files across 17 keys**. `DA` carries 17 variants: its own 12 plus RA's 5, folded by `BaybayinIdCanonicalizer` (SALIN-212) | RC-07 | ✅ Implemented | **Amended 2026-09-01.** This row previously read 18 and said the recognizer distinguishes RA from DA by shape. It no longer does, deliberately: RA folds into DA so a correctly drawn `ᜇ` reports the id the game actually matches against. No template was deleted — all 121 remain, and DA recognition gains 5 samples |
| REQ-13 | The multi-stroke window shall be 1.5 seconds after last finger lift | Salinlahi.md §3.3.3; RecognitionConfigSO | P1 | `RecognitionConfigSO.multiStrokeWindowSeconds = 1.5f`; `StrokeCapture.cs` | RC-05 | ✅ Implemented | None |
| REQ-14 | Tap-like strokes shall be rejected by raw path length and bounds | RecognitionConfigSO | P1 | `RecognitionConfigSO.minimumStrokePathLengthPixels`; `RecognitionConfigSO.minimumStrokeBoundsPixels`; `StrokeCapture.cs` | RC-06 | ✅ Implemented | None |
| REQ-15 | An enemy reaching the PlayerBase shall decrement hearts by 1 | GDD §2.3; TDD §3.3 | P0 | `EnemyMover.OnTriggerEnter2D` fires `RaiseBaseHit()`; `HeartSystem.cs` | WV-05 | ✅ Implemented | None |
| REQ-16 | Hearts shall start at 3 per level | GDD §2.3 | P0 | `HeartSystem.cs` | WV-05 | ✅ Implemented | None |
| REQ-17 | When hearts reach 0, GameOver state shall be triggered | GDD §2.3; TDD §3.3 | P0 | `HeartSystem.cs` fires `OnGameOver`; `GameManager.HandleGameOver()` responds | WV-06 | ✅ Implemented | None |
| REQ-18 | GameOver shall load the GameOver scene | GDD §5.1 | P0 | `GameManager.HandleGameOver()` → `SceneLoader.LoadGameOver()` | CS-01 | ✅ Implemented | None |
| REQ-19 | A pronunciation audio clip shall play on every correct enemy defeat | TDD §6; GDD §5.4 | P1 | `AudioManager.PlayPronunciationClip()` subscribed to `OnEnemyDefeated` | CS-05 | ⚠ Partial | Missing clips |
| REQ-20 | BGM shall loop during gameplay | TDD §6 | P2 | `AudioManager.PlayBGM()` sets `loop = true` | — | ⚠ Partial | Missing clip asset |
| REQ-21 | All manager singletons shall persist across scene loads via DontDestroyOnLoad | TDD §1 | P0 | `Singleton<T>.Awake()` — DontDestroyOnLoad confirmed | CS-01 | ✅ Implemented | None |
| REQ-22 | Only one instance of each Singleton type shall exist at runtime | TDD §1 | P0 | `Singleton<T>.Awake()` — duplicate destruction confirmed | CS-01 | ✅ Implemented | None |
| REQ-23 | Enemies shall be managed via Unity ObjectPool; no Instantiate/Destroy in game loop | TDD §1; EnemyPool.cs lifecycle | P0 | `EnemyPool` + Unity `ObjectPool<Enemy>` confirmed | EN-02 | ✅ Implemented | None |
| REQ-24 | Story Mode shall have 15 levels across 3 chapters | GDD §2.4 | P1 | **All 15 `Level<n>_Config.asset` authored**, plus `CampaignConfig_RevisedV1.asset` with `level.ugat.01`–`.05` (SALIN-204) | WV-01 | ⚠ Partial | 🟠 P1 (configs exist; Ugat L2–5 narrative and assets pending SALIN-205/206) |
| REQ-25 | Boss encounters shall occur at levels 5, 10, 15 | GDD §2.4; TDD §3.2 | P1 | Level 5 wired to fully-authored `BossConfig_ElInquisidor.asset`; Levels 10 and 15 wired to placeholder `BossConfig_Superintendent.asset` and `BossConfig_Kadiliman.asset` (single phase using legacy schema, both reuse El Inquisidor `bossEnemyData`). `WaveManager.RunBossEncounter` activates boss when `LevelConfigSO.bossConfig != null` | BS-01 | ⚠ Partial | 🟠 P1 (Levels 10 and 15 still need dedicated boss prefab/data and new-schema BossPhase values before they ship) |
| REQ-26 | WaveManager shall read LevelConfigSO and drive wave spawning | TDD §3.2; Salinlahi.md §3.5.1 | P0 | `WaveManager.cs` + `WaveSpawner.cs` | WV-01 | ✅ Implemented | None |
| REQ-27 | Wave spawning shall respect waveStartDelay and spawnInterval from WaveDefinition | TDD §3.2 | P1 | `WaveSpawner.cs` reads `WaveDefinition` embedded in `LevelConfigSO` | WV-02, WV-03 | ✅ Implemented | None |
| REQ-28 | The Lite build shall restrict access to levels 1–3 only | TDD §7.2; Salinlahi.md §3.4 | P1 | `LevelConfigSO.isAvailableInLite` field defined; ❌ gate logic not implemented | — | ⚠ Partial | 🟠 P1 |
| REQ-29 | The game shall display a Main Menu with Play, Endless Mode, Tracing Dojo, Settings | GDD §5.1 | P1 | `MainMenuUI.cs` references Play, **Endless, Dojo and Settings** | — | ✅ Implemented | None (SALIN-186: previously recorded as not implemented) |
| REQ-30 | The HUD shall display current heart count and wave number | GDD §5.1; TDD §7.4 | P1 | `HUD.cs` | WV-05, WV-01 | ✅ Implemented | None |
| REQ-31 | Failed strokes shall show a red flash and X mark | GDD §5.4 | P1 | `HUD.cs` | RC-02 | ✅ Implemented | None |
| REQ-32 | The Tracing Dojo shall allow zero-pressure practice of all 17 characters | GDD §2.4; §5.4 | P2 | **`Assets/_Scenes/TracingDojo.unity` exists**, with `TracingDojoController` + `FeedbackToast`; covers all **18** characters | — | ✅ Implemented | None (SALIN-186: previously recorded as NOT FOUND) |
| REQ-33 | Endless Mode shall activate after completing Story Mode or defeating the final boss, with high-score tracking (waves survived, enemies defeated, longest combo) | GDD §2.4; Team README §9 | P2 | ❌ Not implemented | — | ❌ NOT FOUND | 🟡 P2 |
| REQ-34 | Cross-system communication shall use EventBus exclusively | TDD §1; EventBus.cs comment | P0 | All systems use EventBus; no direct cross-manager calls observed | CS-03 | ✅ Implemented | None |
| REQ-35 | EventBus subscriptions shall be in OnEnable and unsubscribed in OnDisable | EventBus.cs comment | P0 | `GameManager`, `AudioManager` — OnEnable/OnDisable confirmed | CS-03 | ✅ Implemented | None |
| REQ-36 | Protagonist shall be visible on screen during gameplay as a 32×32 sprite with 3 era-specific designs | GDD §4.2 | P1 | ❌ Not implemented | — | ❌ NOT FOUND | 🟠 P1 |
| REQ-37 | 12 enemy types shall be era-themed (4 per era: Soldado/Fraile/Guardia/Capitan, Soldier/Maestro/Pensionado/General, Heitai/Kisha/Kempei/Shokan) | GDD §4.3 | P1 | 9 of 12 implemented (Soldado, Soldier, Heitai, Maestro, Pensionado, General, Kisha, Kempei, Shokan); 3 remain PLANNED (Fraile, Guardia, Capitan) | EN-07–EN-11 | ⚠ Partial | 🟠 P1 |
| REQ-38 | Combo system shall track consecutive correct draws; 5-streak triggers focus mode slow effect | GDD §3.2; Team README §9 | P1 | `ComboManager.cs` | — | ✅ Implemented | None |
| REQ-39 | Dialogue panels (Type A) shall appear before/after levels with typewriter effect | GDD §4.5; Team README §12 | P1 | `DialogueController.cs` | DL-01, DL-02 | ✅ Implemented | None |
| REQ-40 | Each era shall have a unique shrine design at 64×96 px with 4 damage states | GDD §4.1 | P2 | ❌ Not implemented | — | ❌ NOT FOUND | 🟡 P2 |
| REQ-41 | Boss encounters shall use phase-based system with BossConfigSO data | GDD §4.3; TDD §3.2 | P1 | `Assets/Scripts/Data/BossConfigSO.cs` + `BossPhase.cs`; full state machine in `Assets/Scripts/Gameplay/Boss/BossController.cs` (Idle -> Intro -> SummoningPhase -> WindingDown -> Vulnerable -> Damaged -> Outro) | BS-01, BS-02, BS-03, BS-05, BS-06 | ✅ Implemented | None |
| REQ-42 | Baybayin **taught** character set shall include **14 consonants and 3 vowels (A, E/I, O/U) totaling 17** (`DA` carries both the `da` and `ra` readings) | GDD §3.3 | P0 | `CampaignConfig_RevisedV1.symbols` holds **17** and excludes `Char_RA`; `Char_DA` carries both `value.da` and `value.ra`; spoken values total **18 across 17 symbols**, matching the approved matrix | RC-07 | ✅ Implemented | **RESOLVED at 17 (2026-09-01).** A 2026-08-31 ruling that `RA` is its own glyph was propagated as "every count is 18"; that was reverted. The tell: the GDD scaffolding table introduces 14 consonants + 3 vowels = **17 exactly**, and `RA` had nowhere to enter. Distinct from REQ-12, which is recognition scope and is correctly 18. `CharacterRegistry_Default.asset` was brought to 17 to match (SALIN-212, 2026-09-01) — at 18 the Almanac's "Learned _n_ / 18" counter could never reach 100%, since enemies carry only 17 distinct characters and none carries `RA` |
| REQ-43 | A daily streak counter shall track consecutive days the player opens the game, stored locally via PlayerPrefs | Salinlahi.md §1.5.1; Sprint Timeline Sprint 3 | P2 | ❌ StreakManager not implemented | — | ❌ NOT FOUND | 🟡 P2 |
| REQ-44 | The game shall include in-game SUS and GEQ-S questionnaire screens administered after gameplay during UAT | Sprint Timeline Sprint 4; Salinlahi.md §3.5.1, §3.5.2 | P0 | ❌ QuestionnaireController not implemented | — | ❌ NOT FOUND | 🔴 P0 |
| REQ-45 | The game shall log recognition accuracy per character per level to a CSV file on the device for post-session analysis | Sprint Timeline Sprint 2; Salinlahi.md §3.5.3 | P0 | **`Assets/Scripts/Analytics/RecognitionLogger.cs`** writes `recognition_log.csv` to `Application.persistentDataPath` on every attempt (pass or fail): `timestamp, recognizedCharacterID, confidence, secondBestCharacterID, secondBestConfidence, scoreGap, intendedCharacterID, outcome` | — | ✅ Implemented | None (SALIN-186: previously recorded as NOT FOUND at P0 severity; per-level keying is the remaining gap, see doc 13) |
| REQ-46 | The recognition system shall support multiple templates per character to handle handwriting variation | TDD §2.2 | P1 | `TemplateLoader.LoadAll()` returns `Dictionary<string, List<List<List<Vector2>>>>` — a **variant list per character**; 121 files give ~6–7 variants each | RC-04 | ✅ Implemented | None (SALIN-186: previously recorded as not implemented) |
| REQ-47 | The Gameplay HUD and stroke input shall constrain to a fixed 9:16 play column regardless of device aspect | `docs/superpowers/specs/2026-05-21-aspect-locked-play-column-design.md` | P2 | `Assets/Scripts/Gameplay/Camera/AspectLockedCamera.cs`, `Assets/Scripts/UI/PlayAreaContainer.cs`, `Assets/Scripts/Gameplay/Environment/BaseZoneScaler.cs`, `Assets/Scripts/Gameplay/Drawing/DrawingCanvas.cs` | PF-05 | ✅ Implemented | None |

---

## Summary Counts

Recounted from the rows above on 2026-08-27 (SALIN-186). **The previous figures did not match the
table they summarised** — they read ✅ 25 / ⚠ 19 / ❌ 3 and claimed *zero* P0 gaps while two rows were
themselves marked 🔴 P0.

| Status | Count |
|--------|-------|
| ✅ Implemented | 30 |
| ⚠ Partial | 12 |
| ❌ NOT FOUND | 5 |
| **Total requirements** | **47** |

| Severity | Count |
|----------|-------|
| 🔴 P0 gaps | 1 |
| 🟠 P1 gaps | 8 |
| 🟡 P2 gaps | 3 |

**The single remaining P0 gap is REQ-44** — in-game SUS and GEQ-S questionnaire screens, still
unimplemented (`QuestionnaireController` does not exist). This is the instrument the capstone
evaluation depends on: see `docs/capstone/EVALUATION-PROTOCOL.md` (SALIN-191), which assumes SUS is
administered and is therefore blocked on this or on an out-of-game equivalent.

The five ❌ NOT FOUND rows are REQ-33 (Endless Mode), REQ-36 (protagonist sprite), REQ-40 (era shrine
designs), REQ-43 (daily streak) and REQ-44 (questionnaire screens).

---

## Backlog Linkage (SALIN)

Added by SALIN-186 to satisfy *"links to this backlog"*, which was previously unmet outright: this
document contained **zero** `SALIN-` keys.

| Requirement area | REQ | Delivered by |
|---|---|---|
| Level phase flow (LF-CONTRACT-v2) | REQ-26, REQ-27 | **SALIN-178** `LevelFlowMachine` — see doc 02 §6.1 |
| Combat feedback and retry safety | REQ-31 | **SALIN-135** |
| Journey entry routing | — | **SALIN-136** `JourneyEntryResolver` |
| Level locks and prerequisites | REQ-28 (partial) | **SALIN-137** `LevelLockResolver` |
| Pause / restart / leave lifecycle | REQ-18 | **SALIN-141** `AbortCurrentLevelAttempt`, `OnLevelAttemptAborted` |
| Syllable audio at learning time | REQ-19 | **SALIN-157** `SpokenValueResolver` — visual-only fallback ships; clips missing |
| Active-clue combat | REQ-08 | **SALIN-180** `ActiveClueDirector`, `ClueChannels` |
| Context-challenge engine | — | **SALIN-181** |
| Atomic outcome commit | — | **SALIN-174** — acceptance in doc 09 §1.4 |
| Persistence and migration | — | **SALIN-171** — acceptance in doc 09 §1.2 |
| Resume safety after revised update | — | **SALIN-143** — acceptance in doc 09 §1.3 |
| Frozen-core acceptance | — | **SALIN-170** — acceptance in doc 09 §1.1 |
| Learning / mastery evidence | REQ-45 | **SALIN-175** — see doc 02 §7.1 |
| Ugat Levels 2–5 configurations | REQ-24 | **SALIN-204** (`level.ugat.01`–`.05` authored) |
| Level 1 regression coverage | — | **SALIN-201** |
| Supportive recognition feedback | REQ-31 | **SALIN-163** — removes the raw confidence score from player-facing UI |
| Capstone evaluation protocol | REQ-44 | **SALIN-191** — `docs/capstone/EVALUATION-PROTOCOL.md` |
| This documentation sync | — | **SALIN-186** |

**Jira data caveats.** SALIN-194–197 are `Done` duplicates titled "[Duplicate - Do Not Use]"; the live
equivalents are 168, 171, 177 and 193. The SALIN-178/180 and SALIN-188/192 *Blocks* links are recorded
backwards in Jira and have not been corrected — the connector exposes no link-delete operation.
