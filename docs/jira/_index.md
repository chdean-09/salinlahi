# Jira Ticket Index — Salinlahi (SALIN)

> **Project:** salinlahi (`SALIN`) — Jira site: jnwync.atlassian.net
> **Last exported:** 2026-03-25
> **Total tickets:** 76 (10 Epics, 66 Tasks/Stories)

---

## Epics

| Epic | Title | Status | File |
|------|-------|--------|------|
| SALIN-1 | Core Architecture & Infrastructure | To Do | [core-architecture.md](core-architecture.md) |
| SALIN-2 | Enemy System | To Do | [enemy-system.md](enemy-system.md) |
| SALIN-3 | Baybayin Recognition System | To Do | [baybayin-recognition.md](baybayin-recognition.md) |
| SALIN-4 | Player & Combat System | To Do | [player-combat.md](player-combat.md) |
| SALIN-5 | Level Design & Progression | To Do | [level-design.md](level-design.md) |
| SALIN-6 | UI, Scenes & UX | To Do | [ui-scenes-ux.md](ui-scenes-ux.md) |
| SALIN-7 | Audio | To Do | [audio.md](audio.md) |
| SALIN-8 | Release & Technical Quality | To Do | [release-quality.md](release-quality.md) |
| SALIN-36 | Art & Visual Integration | To Do | [art-visual.md](art-visual.md) |
| SALIN-51 | Research, Testing & Analytics | To Do | [research-testing.md](research-testing.md) |

---

## All Tickets by Epic

### SALIN-1 — Core Architecture & Infrastructure → [core-architecture.md](core-architecture.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-9 | Implement Singleton\<T\> Generic Base Class | Done |
| SALIN-10 | Implement EventBus.cs — Central Publish/Subscribe System | Done |
| SALIN-11 | Implement SceneLoader.cs with Async Loading and Fade Stub | In Progress |
| SALIN-12 | Implement ObjectPool.cs — Generic Unity Object Pool Wrapper | Done |
| SALIN-13 | Implement GameManager.cs State Machine | Done |
| SALIN-14 | Configure Bootstrap.unity Scene and All Manager Prefab Shells | Done |
| SALIN-26 | Git Branching Strategy and PR Review Checklist | Done |

### SALIN-2 — Enemy System → [enemy-system.md](enemy-system.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-15 | Implement Enemy.cs Core Component and EnemyMover.cs | Done |
| SALIN-16 | Implement EnemyPool.cs, WaveSpawner.cs, and WaveManager.cs | In Progress |
| SALIN-37 | Implement Sprinter Enemy Variant | To Do |
| SALIN-38 | Implement Shielded Enemy Variant (Multi-Hit) | To Do |
| SALIN-39 | Implement Chain Enemy Group System (ChainGroup.cs) | To Do |
| SALIN-52 | Implement Phaser / Blinker Enemy Variant | To Do |
| SALIN-53 | Implement Decoy Enemy Variant | To Do |
| SALIN-54 | Implement Zigzagger and Healer Enemy Variants | To Do |
| SALIN-68 | Implement Boss Encounter System (BossConfigSO + Phase Mechanics) | To Do |

### SALIN-3 — Baybayin Recognition System → [baybayin-recognition.md](baybayin-recognition.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-18 | Configure Unity Input System and Implement StrokeCapture.cs | To Do |
| SALIN-19 | Implement DrawingCanvas.cs with LineRenderer Stroke Visualization | To Do |
| SALIN-20 | Implement DollarPRecognizer.cs — Full $P Algorithm | To Do |
| SALIN-21 | SPIKE: Investigate $P Recognizer Accuracy Issues | To Do |
| SALIN-27 | Build Baybayin Template Library — All 17 Characters (3-5 Variants Each) | To Do |
| SALIN-28 | Recognition Edge Cases and Input Validation | To Do |
| SALIN-35 | Implement Recognition Accuracy Logging System | To Do |
| SALIN-74 | Implement TemplateLoader.cs — Standalone Baybayin Template Loading | To Do |
| SALIN-75 | Implement RecognitionManager.cs — Coordinate StrokeCapture and DollarPRecognizer | To Do |

### SALIN-4 — Player & Combat System → [player-combat.md](player-combat.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-17 | Implement PlayerBase.cs and HeartSystem.cs | To Do |
| SALIN-29 | Implement Core Combat Resolution (Drawing to Enemy Defeat) | To Do |
| SALIN-40 | Implement AOE Burst Mechanic (3+ Same Character Mass Defeat) | To Do |
| SALIN-41 | Implement ComboManager — Streak Counter and Focus Mode | To Do |

### SALIN-5 — Level Design & Progression → [level-design.md](level-design.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-22 | Create All ScriptableObject Data Architecture Classes | Done |
| SALIN-30 | Design and Configure Levels 1-5 (Core Evaluation Levels) | To Do |
| SALIN-42 | Design and Configure Levels 6-10 | To Do |
| SALIN-48 | Implement Level Progress Saving — PlayerPrefs Manager | To Do |
| SALIN-55 | Design and Configure Levels 11-15 | To Do |
| SALIN-56 | Full Difficulty Tuning Pass — All 15 Levels | To Do |
| SALIN-70 | Implement Endless Mode Unlock Trigger (Post-Story Completion) | To Do |

### SALIN-6 — UI, Scenes & UX → [ui-scenes-ux.md](ui-scenes-ux.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-23 | Build Main Menu Scene with Navigation | In Progress |
| SALIN-31 | Build In-Game HUD — Hearts, Wave Counter, Character Prompt, Pause | To Do |
| SALIN-43 | Build Level Select Screen with Lock/Unlock/Complete States | To Do |
| SALIN-44 | Implement Scene Transition Manager — Fade In/Out Between Scenes | To Do |
| SALIN-45 | Implement DialogueController.cs with Typewriter Effect | To Do |
| SALIN-46 | Implement LevelFlowController.cs — Intro/Gameplay/Outro Sequence | To Do |
| SALIN-47 | Write Dialogue Content for All 15 Levels | To Do |
| SALIN-50 | FTUE / Tutorial Overlay for Level 1 | To Do |
| SALIN-57 | Implement Safe Area Handler for Notched Android/iOS Devices | To Do |
| SALIN-58 | Build Victory, Defeat, Settings, and Credits Screens | To Do |
| SALIN-65 | Loading Screen During Scene Transitions | To Do |
| SALIN-66 | Visual Polish Pass — UI Animations, Feedback, and Drawing Trail | To Do |
| SALIN-69 | Implement Tracing Dojo Scene and Practice Mode | To Do |

### SALIN-7 — Audio → [audio.md](audio.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-24 | Implement AudioManager.cs — Singleton Audio Controller | Done |
| SALIN-32 | Record / Source Pronunciation Audio for All 17 Baybayin Characters | To Do |
| SALIN-33 | Implement SFX Library — All Core Game Event Sounds | To Do |
| SALIN-34 | Implement BGM System with Per-Chapter Track Switching | To Do |
| SALIN-67 | Final Audio Mix Pass — Balance BGM, SFX, and Pronunciation | To Do |

### SALIN-8 — Release & Technical Quality → [release-quality.md](release-quality.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-25 | Configure Android Build Settings and Apply for Developer Account | Done |
| SALIN-59 | Implement Lite/Full Build Flag and Level Filtering | To Do |
| SALIN-64 | Android Permissions Handling (Storage, Network) | To Do |
| SALIN-71 | Frame Rate Profiling — Verify 60fps Target on Mid-Range Android | To Do |
| SALIN-72 | Cold-Start Benchmark — Verify App Launches in Under 5 Seconds | To Do |
| SALIN-73 | APK Size Verification — Confirm Release Build Under 100 MB | To Do |

### SALIN-36 — Art & Visual Integration → [art-visual.md](art-visual.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-49 | Art Batch 1 Integration — Standard Enemy, Shrine, Baybayin Overlays, Forest BG | To Do |
| SALIN-60 | Art Batches 2, 3 & 4 Integration | To Do |

### SALIN-51 — Research, Testing & Analytics → [research-testing.md](research-testing.md)
| Ticket | Title | Status |
|--------|-------|--------|
| SALIN-61 | Build SUS (System Usability Scale) Questionnaire UI | To Do |
| SALIN-62 | Build GEQ-S (Game Experience Questionnaire Short) UI | To Do |
| SALIN-63 | Implement Confusion Matrix Export and Debug Tool | To Do |
| SALIN-76 | Build GEQ Core Module Questionnaire UI (Full GEQ, not GEQ-S) | To Do |

---

## Status Summary

| Status | Count |
|--------|-------|
| Done | 11 |
| In Progress | 3 |
| To Do | 52 |
| **Total** | **66** |
