# Jira Ticket Index — Salinlahi (SALIN)

> **Project:** salinlahi (`SALIN`) — Jira site: jnwync.atlassian.net
> **Last rebuilt:** 2026-03-25
> **Total tickets:** 74 (10 Epics + 64 Tasks/Stories)

---

## Epics

| Epic | Title | Status | File |
|------|-------|--------|------|
| SALIN-1 | Core Architecture & Infrastructure | ✅ Done | [core-architecture.md](core-architecture.md) |
| SALIN-2 | Enemy System | ⚠️ In Progress | [enemy-system.md](enemy-system.md) |
| SALIN-3 | Baybayin Recognition System | ⚠️ Partially Done (Jira stale) | [baybayin-recognition.md](baybayin-recognition.md) |
| SALIN-4 | Player & Combat System | ⚠️ Partially Done (Jira stale) | [player-combat.md](player-combat.md) |
| SALIN-5 | Level Design & Progression | ⚠️ Architecture Done — Levels Not Configured | [level-design.md](level-design.md) |
| SALIN-6 | UI, Scenes & UX | ⚠️ Skeleton Only | [ui-scenes-ux.md](ui-scenes-ux.md) |
| SALIN-7 | Audio | ⚠️ Manager Built — No Audio Content | [audio.md](audio.md) |
| SALIN-8 | Release & Technical Quality | ⚠️ Build Config Done — Performance Not Started | [release-quality.md](release-quality.md) |
| SALIN-36 | Art & Visual Assets | ⚠️ Out of Scope for Dev Sprints | [art-visual.md](art-visual.md) |
| SALIN-51 | Research, Testing & Analytics | ❌ Not Started | [research-testing.md](research-testing.md) |

---

## Tickets Added by Audit

| ID | Title | Epic File | Sprint | Assignee |
|----|-------|-----------|--------|----------|
| SALIN-77 | Create BaybayinCharacterSO Assets — Remaining 14 Characters | [baybayin-recognition.md](baybayin-recognition.md) | Sprint 2 | Jeff |
| SALIN-78 | Internal Playtest & Bug Bash | [research-testing.md](research-testing.md) | Sprint 4 | Chad |
| SALIN-79 | Google Play Store Submission Prep | [release-quality.md](release-quality.md) | Sprint 4 | Clyde |
| SALIN-80 | Gameplay Integration Smoke Test | [player-combat.md](player-combat.md) | Sprint 2 | Chad |

---

## All Tickets by Epic

### SALIN-1 — Core Architecture & Infrastructure → [core-architecture.md](core-architecture.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-9 | Implement Singleton\<T\> Generic Base Class | Done | Sprint 1 | — |
| SALIN-10 | Implement EventBus.cs — Central Publish/Subscribe System | Done | Sprint 1 | — |
| SALIN-11 | Implement SceneLoader.cs with Async Loading and Fade Stub | In Progress | Sprint 2 | Wayne |
| SALIN-12 | Implement ObjectPool.cs — Generic Unity Object Pool Wrapper | Done | Sprint 1 | — |
| SALIN-13 | Implement GameManager.cs State Machine | Done | Sprint 1 | — |
| SALIN-14 | Configure Bootstrap.unity Scene and All Manager Prefab Shells | Done | Sprint 1 | — |
| SALIN-26 | Git Branching Strategy and PR Review Checklist | Done | Sprint 1 | — |

### SALIN-2 — Enemy System → [enemy-system.md](enemy-system.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-15 | Implement Enemy.cs Core Component and EnemyMover.cs | Done | Sprint 1 | — |
| SALIN-16 | Implement EnemyPool.cs, WaveSpawner.cs, and WaveManager.cs | In Progress | Sprint 2 | Clyde |
| SALIN-37 | Implement Sprinter Enemy Variant | To Do | Sprint 2 | Clyde |
| SALIN-38 | Implement Shielded Enemy Variant (Multi-Hit) | To Do | Sprint 2 | Clyde |
| SALIN-39 | Implement Chain Enemy Group System (ChainGroup.cs) | To Do | Sprint 3 | Clyde |
| SALIN-52 | Implement Phaser Enemy Variant (Visibility Toggle) | To Do | Sprint 3 | Clyde |
| SALIN-53 | Implement Decoy Enemy Variant | To Do | Sprint 3 | Clyde |
| SALIN-54 | Implement Zigzagger and Healer Enemy Variants | To Do | Sprint 3 | Clyde |
| SALIN-68 | Implement Boss Encounter System (BossConfigSO + Phase Mechanics) | To Do | Sprint 3 | Chad |

### SALIN-3 — Baybayin Recognition System → [baybayin-recognition.md](baybayin-recognition.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-18 | Configure Unity Input System and Implement StrokeCapture.cs | Done *(Jira stale)* | Sprint 1 | — |
| SALIN-19 | Implement DrawingCanvas.cs with LineRenderer Stroke Visualization | Done *(Jira stale)* | Sprint 1 | — |
| SALIN-20 | Implement DollarPRecognizer.cs — Full $P Algorithm | Done *(Jira stale)* | Sprint 1 | — |
| SALIN-21 | SPIKE: Investigate $P Recognizer Accuracy Issues | Won't Do — Superseded | — | — |
| SALIN-74 | Implement TemplateLoader.cs — Standalone Template Loading | Done *(Jira stale)* | Sprint 1 | — |
| SALIN-75 | Implement RecognitionManager.cs — Coordinate StrokeCapture and $P | Done *(Jira stale)* | Sprint 1 | — |
| SALIN-27 | Build Baybayin Template Library — All 17 Characters (3–5 Variants Each) | In Progress | Sprint 2 | Jeff |
| SALIN-28 | Recognition Edge Cases and Input Validation | To Do | Sprint 3 | Jeff |
| SALIN-35 | Implement Recognition Accuracy Logging System | To Do | Sprint 3 | Jeff |
| SALIN-77 | Create BaybayinCharacterSO Assets — Remaining 14 Characters | To Do | Sprint 2 | Jeff |

### SALIN-4 — Player & Combat System → [player-combat.md](player-combat.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-17 | Implement PlayerBase.cs and HeartSystem.cs | Done *(Jira stale)* | Sprint 1 | — |
| SALIN-29 | Implement Core Combat Resolution (Drawing to Enemy Defeat) | To Do | Sprint 2 | Chad |
| SALIN-40 | Implement AOE Burst Mechanic (3+ Same Character Mass Defeat) | To Do | Sprint 3 | Chad |
| SALIN-41 | Implement ComboManager — Streak Counter and Focus Mode | To Do | Sprint 2 | Chad |
| SALIN-80 | Gameplay Integration Smoke Test | To Do | Sprint 2 | Chad |

### SALIN-5 — Level Design & Progression → [level-design.md](level-design.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-22 | Create All ScriptableObject Data Architecture Classes | Done | Sprint 1 | — |
| SALIN-30 | Design and Configure Levels 1–5 (Core Evaluation Levels) | To Do | Sprint 2 | Jeff |
| SALIN-42 | Design and Configure Levels 6–10 | To Do | Sprint 3 | Clyde |
| SALIN-48 | Implement Level Progress Saving — PlayerPrefs Manager | To Do | Sprint 2 | Wayne |
| SALIN-55 | Design and Configure Levels 11–15 | To Do | Sprint 3 | Wayne |
| SALIN-56 | Full Difficulty Tuning Pass — All 15 Levels | To Do | Sprint 4 | Chad |
| SALIN-70 | Implement Endless Mode Unlock Trigger (Post-Story) *(Stretch)* | To Do | Sprint 4 | Chad |

### SALIN-6 — UI, Scenes & UX → [ui-scenes-ux.md](ui-scenes-ux.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-23 | Build Main Menu Scene with Navigation | In Progress | Sprint 2 | Jeff |
| SALIN-31 | Build In-Game HUD — Hearts, Wave Counter, Character Prompt, Pause | To Do | Sprint 2 | Wayne |
| SALIN-43 | Build Level Select Screen with Lock/Unlock/Complete States | To Do | Sprint 2 | Wayne |
| SALIN-44 | Implement Scene Transition Manager — Fade In/Out | To Do | Sprint 3 | Wayne |
| SALIN-45 | Implement DialogueController.cs with Typewriter Effect | To Do | Sprint 3 | Wayne |
| SALIN-46 | Implement LevelFlowController.cs — Intro/Gameplay/Outro Sequence | To Do | Sprint 3 | Wayne |
| SALIN-47 | Write Dialogue Content for All 15 Levels | To Do | Sprint 3 | Jeff |
| SALIN-50 | FTUE / Tutorial Overlay for Level 1 | To Do | Sprint 4 | Wayne |
| SALIN-57 | Implement Safe Area Handler for Notched Devices | To Do | Sprint 4 | Wayne |
| SALIN-58 | Build Victory, Defeat, Settings, and Credits Screens | To Do | Sprint 3 | Wayne |
| SALIN-65 | Loading Screen During Scene Transitions | To Do | Sprint 4 | Wayne |
| SALIN-66 | Visual Polish Pass — UI Animations, Feedback, and Drawing Trail | To Do | Sprint 4 | Wayne |
| SALIN-69 | Implement Tracing Dojo Scene and Practice Mode | To Do | Sprint 3 | Chad |

### SALIN-7 — Audio → [audio.md](audio.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-24 | Implement AudioManager.cs — Singleton Audio Controller | Done | Sprint 1 | — |
| SALIN-32 | Record / Source Pronunciation Audio — All 17 Baybayin Characters | To Do | Sprint 3 | Jeff |
| SALIN-33 | Implement SFX Library — All Core Game Event Sounds | To Do | Sprint 3 | Jeff |
| SALIN-34 | Implement BGM System with Per-Chapter Track Switching | To Do | Sprint 3 | Jeff |
| SALIN-67 | Final Audio Mix Pass — Balance BGM, SFX, and Pronunciation | To Do | Sprint 4 | Jeff |

### SALIN-8 — Release & Technical Quality → [release-quality.md](release-quality.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-25 | Configure Android Build Settings and Apply for Developer Account | Done | Sprint 1 | — |
| SALIN-59 | Implement Lite/Full Build Flag and Level Filtering | To Do | Sprint 4 | Clyde |
| SALIN-64 | Android Permissions Handling (Storage, Network) | To Do | Sprint 4 | Clyde |
| SALIN-71 | Frame Rate Profiling — Verify 60fps on Mid-Range Android | To Do | Sprint 4 | Clyde |
| SALIN-72 | Cold-Start Benchmark — Verify App Launches Under 5 Seconds | To Do | Sprint 4 | Clyde |
| SALIN-73 | APK Size Verification — Confirm Release Build Under 100 MB | To Do | Sprint 4 | Clyde |
| SALIN-79 | Google Play Store Submission Prep | To Do | Sprint 4 | Clyde |

### SALIN-36 — Art & Visual Assets → [art-visual.md](art-visual.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-49 | Art Batch 1: Protagonist + Soldado Sprites and Animations | To Do | — | Art team |
| SALIN-60 | Art Batches 2–4: Enemy Variants, Backgrounds, and UI Icons | To Do | — | Art team |

### SALIN-51 — Research, Testing & Analytics → [research-testing.md](research-testing.md)

| Ticket | Title | Status | Sprint | Assignee |
|--------|-------|--------|--------|----------|
| SALIN-35 | Implement Recognition Accuracy Logging System | To Do | Sprint 3 | Jeff |
| SALIN-61 | Build SUS Questionnaire UI (10 Items) | To Do | Sprint 4 | Jeff |
| SALIN-62 | Build GEQ-S Questionnaire UI (14 Items) | To Do | Sprint 4 | Jeff |
| SALIN-63 | Implement Confusion Matrix Export and Debug Tool | To Do | Sprint 4 | Jeff |
| SALIN-76 | Build Full GEQ Core Module Questionnaire UI (33 Items) | To Do | Sprint 4 | Jeff |
| SALIN-78 | Internal Playtest & Bug Bash | To Do | Sprint 4 | Chad |

---

## Sprint Overview

| Sprint | Dates | Focus |
|--------|-------|-------|
| Sprint 1 | Mar 16–27 | Core infrastructure (Done) |
| Sprint 2 | Mar 30–Apr 10 | Combat loop, level data, UI skeleton |
| Sprint 3 | Apr 13–24 | Enemy variants, dialogue, audio content, research logging |
| Sprint 4 | Apr 27–May 8 | Polish, questionnaires, performance, store submission |

### Sprint 2 — Tickets in Scope

| Ticket | Title | Assignee |
|--------|-------|----------|
| SALIN-11 | SceneLoader.cs — finish async + fade | Wayne |
| SALIN-16 | EnemyPool, WaveSpawner, WaveManager | Clyde |
| SALIN-23 | Main Menu — wire Play button to LevelSelect | Jeff |
| SALIN-29 | Core Combat Resolution (CombatResolver.cs) | Chad |
| SALIN-30 | Configure Levels 1–5 LevelConfigSO + WaveConfigSO | Jeff |
| SALIN-31 | In-Game HUD | Wayne |
| SALIN-37 | Sprinter Enemy Variant | Clyde |
| SALIN-38 | Shielded Enemy Variant | Clyde |
| SALIN-41 | ComboManager — Streak + Focus Mode | Chad |
| SALIN-43 | Level Select Screen | Wayne |
| SALIN-48 | ProgressManager.cs — Level Progress Saving | Wayne |
| SALIN-77 | BaybayinCharacterSO Assets — Remaining 14 Characters | Jeff |
| SALIN-80 | Gameplay Integration Smoke Test | Chad |

### Sprint 3 — Tickets in Scope

| Ticket | Title | Assignee |
|--------|-------|----------|
| SALIN-28 | Recognition Edge Cases and Input Validation | Jeff |
| SALIN-32 | Pronunciation Audio — All 17 Characters | Jeff |
| SALIN-33 | SFX Library — All Core Game Events | Jeff |
| SALIN-34 | BGM System with Per-Chapter Switching | Jeff |
| SALIN-35 | Recognition Accuracy Logging System | Jeff |
| SALIN-39 | Chain Enemy Group System | Clyde |
| SALIN-40 | AOE Burst Mechanic | Chad |
| SALIN-42 | Configure Levels 6–10 | Clyde |
| SALIN-44 | Scene Transition Manager — Fade In/Out | Wayne |
| SALIN-45 | DialogueController.cs — Typewriter Effect | Wayne |
| SALIN-46 | LevelFlowController.cs — Intro/Gameplay/Outro | Wayne |
| SALIN-47 | Dialogue Content — All 15 Levels | Jeff |
| SALIN-52 | Phaser Enemy Variant | Clyde |
| SALIN-53 | Decoy Enemy Variant | Clyde |
| SALIN-54 | Zigzagger and Healer Enemy Variants | Clyde |
| SALIN-55 | Configure Levels 11–15 | Wayne |
| SALIN-58 | Victory, Defeat, Settings, Credits Screens | Wayne |
| SALIN-68 | Boss Encounter System | Chad |
| SALIN-69 | Tracing Dojo Scene and Practice Mode | Chad |

### Sprint 4 — Tickets in Scope

| Ticket | Title | Assignee |
|--------|-------|----------|
| SALIN-50 | FTUE / Tutorial Overlay for Level 1 | Wayne |
| SALIN-56 | Full Difficulty Tuning Pass — All 15 Levels | Chad |
| SALIN-57 | Safe Area Handler for Notched Devices | Wayne |
| SALIN-59 | Lite/Full Build Flag and Level Filtering | Clyde |
| SALIN-61 | SUS Questionnaire UI | Jeff |
| SALIN-62 | GEQ-S Questionnaire UI | Jeff |
| SALIN-63 | Confusion Matrix Export and Debug Tool | Jeff |
| SALIN-64 | Android Permissions Handling | Clyde |
| SALIN-65 | Loading Screen During Scene Transitions | Wayne |
| SALIN-66 | Visual Polish Pass | Wayne |
| SALIN-67 | Final Audio Mix Pass | Jeff |
| SALIN-70 | Endless Mode Unlock Trigger *(Stretch)* | Chad |
| SALIN-71 | Frame Rate Profiling — 60fps Verification | Clyde |
| SALIN-72 | Cold-Start Benchmark | Clyde |
| SALIN-73 | APK Size Verification | Clyde |
| SALIN-76 | Full GEQ Core Module Questionnaire UI | Jeff |
| SALIN-78 | Internal Playtest & Bug Bash | Chad |
| SALIN-79 | Google Play Store Submission Prep | Clyde |

---

## Status Summary

| Status | Count |
|--------|-------|
| Done | 12 |
| In Progress | 3 |
| Won't Do | 1 |
| To Do | 54 |
| **Total** | **70** |

> ⚠️ **Jira stale statuses:** SALIN-17, 18, 19, 20, 74, 75 are fully implemented in code but still show "To Do" in Jira. Update these tickets to **Done** in Jira at the start of Sprint 2.
>
> ⚠️ **Critical path:** SALIN-27 (Jeff) + SALIN-77 (Jeff) must land by end of Sprint 2 — all template-dependent work (SALIN-29, SALIN-30) blocks on them. SALIN-29 (Chad) is the single highest-priority unimplemented ticket; the recognition-to-combat pipeline is incomplete without it.
