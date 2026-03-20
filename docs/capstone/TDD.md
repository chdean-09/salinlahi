**SALINLAHI**

A 2D Pixel Art Defense Game

**TECHNICAL DESIGN DOCUMENT**

Version 1.0 | March 2026

**Engine: **Unity 6 LTS | URP 2D | C#

**Platforms: **Android, iOS (Portrait)

**Network: **Fully offline

**Team**

Andrada, Chad

Cabusbusan, Jon Wayne

Millan, Jeff Andre

Tejada, Ian Clyde

# 1. Architecture Overview

Salinlahi is a single-codebase Unity 6 LTS project targeting Android and iOS in portrait orientation. The rendering pipeline is Universal Render Pipeline (URP) configured for 2D. No 3D geometry exists anywhere in the project. The entire game runs offline with zero network calls.

The architecture follows three core principles:

- **Singleton Managers: **Core systems (GameManager, AudioManager, RecognitionManager) are singletons instantiated once in the Bootstrap scene and persist across all scene loads.

- **Event-Driven Communication: **Managers never reference each other directly. All inter-system communication flows through a static EventBus. This keeps systems testable in isolation and prevents circular dependencies.

- **Data-Driven Content: **All level pacing, enemy stats, wave composition, and recognition thresholds are defined in ScriptableObject assets editable in the Unity Inspector. Designers can tune the entire game without touching compiled scripts.

## 1.1 Scene Structure

The game uses five scenes loaded asynchronously through a SceneLoader utility. Bootstrap is the entry point, loads all manager prefabs, then auto-transitions to MainMenu. From there, the player navigates to LevelSelect (for story mode), TracingDojo, or EndlessMode. The Gameplay scene is shared by both story levels and endless mode, configured at load time by the LevelConfigSO or an endless-mode flag.

*Figure 1. Scene navigation flow*

## 1.2 Game State Machine

GameManager runs a simple state machine with four states: Loading (scene initialization), Playing (core loop active, enemies spawning, drawing enabled), Paused (overlay shown, time scale set to zero), and GameOver (hearts reached zero) or LevelComplete (all waves cleared). Every state transition fires an EventBus event so all subscribers (HUD, AudioManager, WaveManager) can react without polling.

*Figure 2. GameManager state machine*

# 2. Input and Recognition System

This is the most technically critical system in the game. The player's finger on the screen is the only input mechanism during gameplay. The system must capture strokes accurately, recognize them against Baybayin templates, and resolve combat outcomes, all within a 50ms budget from finger lift to visual result.

## 2.1 Touch Input Capture

The game uses Unity's new Input System package with the EnhancedTouch API instead of the legacy Input.touches array. The legacy system polls once per rendered frame, which means fast strokes can complete entirely within a frame gap and go unregistered. EnhancedTouch captures at the device's native input polling rate independent of frame rate, so fast strokes are never missed.

The capture system tracks three touch phases:

- **Began: **Creates a new empty coordinate list for the current stroke.

- **Moved: **Appends the current screen position to the list on each callback.

- **Ended: **Closes the stroke and starts a coroutine-based idle timer (1.5 seconds). If a new touch begins before the timer expires, it resets and appends to the same character attempt. If the timer expires, the full stroke list is sent to RecognitionManager as one character submission.

This idle-timer approach handles both single-stroke and multi-stroke Baybayin characters without needing to know the stroke count in advance. Input capture and recognition are fully separated so both can be tested independently.

## 2.2 The $P Recognition Pipeline

The $P Point-Cloud Recognizer treats a drawing as an unordered cloud of points rather than an ordered stroke sequence. This means it recognizes a character regardless of the order, direction, or number of strokes the player used, which is critical for a game introducing Baybayin to players with no knowledge of historical stroke conventions.

The algorithm normalizes each drawing in four steps before matching:

- **Resample **to exactly 32 evenly spaced points (removes speed dependency)

- **Scale **to a 250x250 unit square (removes size dependency)

- **Translate **the centroid to the origin (removes position dependency)

- **Match **against all 17 stored Baybayin templates using a greedy approximation of the Hungarian algorithm to find the minimum total pairwise Euclidean distance

The template with the lowest distance wins. A confidence score at or above 0.60 is required. Templates are stored as plain text coordinate files in Resources/Templates/ and loaded at startup via TemplateLoader. They can be revised or expanded without recompiling the project. Multiple templates per character are supported (e.g., BA_template_01.txt, BA_template_02.txt) to handle handwriting variation.

*Figure 3. $P recognition pipeline*

## 2.3 Drawing Canvas

DrawingCanvas renders the player's stroke in real time using a LineRenderer component. The stroke is visible as a trail following the player's finger. After recognition fires (success or failure), the canvas clears. On failure, a brief red flash or X mark appears before clearing. On success, a brief green flash confirms the hit. The canvas occupies the full screen, ensuring there is no restrictive drawing box.

# 3. Combat and Wave System

## 3.1 Enemy Lifecycle

Enemies are managed through Unity's built-in ObjectPool<T> class. At scene load, a fixed pool of enemy GameObjects is pre-instantiated and held inactive. When WaveSpawner needs an enemy, it pulls one from the pool, assigns its BaybayinCharacterSO and movement configuration, and activates it. When an enemy is defeated or reaches the base, it is deactivated and returned to the pool rather than destroyed. This eliminates garbage collection spikes from Instantiate/Destroy during wave gameplay.

*Figure 4. Enemy lifecycle and object pooling*

## 3.2 Wave Management

WaveManager reads a LevelConfigSO at level load, which defines the sequence of WaveConfigSO assets for that level. Each WaveConfigSO specifies the enemy types, spawn count, spawn delay, and spawn positions for one wave. WaveManager triggers WaveSpawner per wave and fires OnWaveStarted and OnWaveCleared events so the HUD and GameManager can track progress.

For boss levels (5, 10, 15), a BossConfigSO defines the boss encounter that activates after the final wave clears. Bosses use a phase-based system with distinct mini-game mechanics per phase.

## 3.3 Combat Resolution

When RecognitionManager fires OnCharacterRecognized, WaveManager listens and finds the closest active enemy on screen carrying that character ID. That enemy is immediately defeated. If multiple enemies carry the same character and three or more are on screen simultaneously, the AOE burst mechanic (if implemented) defeats all of them at once. A combo counter tracks consecutive correct drawings without misses. The streak resets on a failed stroke.

If an enemy reaches the PlayerBase collider, it is deactivated, HeartSystem decrements the heart count, and OnHeartsChanged fires. When hearts reach zero, GameManager transitions to the GameOver state.

# 4. EventBus Architecture

The EventBus is a static class that acts as the central messaging hub. No manager holds a direct reference to any other manager. Publishers fire events with relevant data, and subscribers react independently. This pattern was chosen for three reasons: it eliminates circular dependencies, it makes each system independently testable, and it allows new systems to be added by subscribing to existing events without modifying the publisher.

*Figure 5. EventBus publish/subscribe architecture*

# 5. Data Layer

All game content is defined in ScriptableObject assets. This means that level designers (in this case, Chad) can create new levels, adjust enemy speeds, change wave compositions, and tune difficulty entirely through the Unity Inspector without writing code or recompiling.

*Figure 6. ScriptableObject data architecture*

| **Asset Type** | **Defines** |
| --- | --- |
| LevelConfigSO | Chapter assignment, background theme, ordered list of WaveConfigSO references for that level. |
| WaveConfigSO | Enemy types to spawn, count per type, spawn delay between enemies, spawn position columns. |
| EnemyDataSO | Movement speed, movement pattern (straight, zigzag, chain), health (for shielded types), reference to a BaybayinCharacterSO. |
| BaybayinCharacterSO | Character ID string, display name, template file references, AudioClip for pronunciation. |
| BossConfigSO | Boss health pool, number of phases, mini-game type per phase, timing windows. |
| RecognitionConfigSO | Confidence threshold (0.60), resample point count (32), scale square size (250), idle timer duration (1.5s). |

# 6. Audio Feedback System

Audio feedback is not polish. It is a core part of the learning mechanism. Every time a player successfully draws a character, a voice pronunciation clip for that Baybayin syllable plays at the exact moment the enemy is defeated. This pairs the positive combat reward with the phonetic identity of the character. Over hundreds of repetitions, this builds an automatic association between the character's shape and its sound.

Each BaybayinCharacterSO holds a reference to its AudioClip. AudioManager maintains a dictionary mapping character IDs to clips, populated at startup. When OnCharacterRecognized fires, AudioManager retrieves and plays the corresponding clip via an AudioSource component. Clips are kept short (under 1 second) to avoid overlapping with the next enemy's appearance. All audio assets are bundled with the application and play fully offline.

Background music and sound effects (enemy spawn, base hit, game over) are handled through separate AudioSource channels with independent volume controls exposed in Settings.

# 7. Build and Deployment

## 7.1 Version Control

The project uses Git on GitHub. Asset serialization is set to Force Text in Unity project settings so scene and prefab files are stored in human-readable YAML, which Git can diff and merge more reliably than binary formats. To reduce scene merge conflicts, the team splits content across additive scenes and avoids simultaneous edits to the same scene file.

## 7.2 Lite/Full Build Split

Both Salinlahi Lite and Salinlahi Full are built from the same Unity codebase. A build configuration flag (a scripting define symbol) controls which content is accessible. Lite locks the level select to Levels 1 through 3 and disables Endless Mode. Full unlocks everything. Both builds use separate app identifiers and are submitted independently to the Google Play Store and Apple App Store.

## 7.3 Performance Targets

| **Metric** | **Target** |
| --- | --- |
| Recognition latency (finger lift to combat result) | Under 50ms on target hardware |
| Frame rate during wave gameplay | Consistent 60fps on mid-range devices (no GC spikes) |
| App launch to gameplay | Under 5 seconds cold start |
| APK size | Under 100MB (pixel art assets are lightweight) |
| Crash rate | Zero crashes during a full 15-level playthrough |

## 7.4 Folder Structure

| **Folder** | **Contents** |
| --- | --- |
| Assets/Scripts/Core/ | Singleton.cs, EventBus.cs, SceneLoader.cs, GameManager.cs |
| Assets/Scripts/Input/ | StrokeCapture.cs, DrawingCanvas.cs |
| Assets/Scripts/Recognition/ | DollarPRecognizer.cs, TemplateLoader.cs, RecognitionManager.cs |
| Assets/Scripts/Gameplay/ | Enemy.cs, EnemyMover.cs, EnemyPool.cs, WaveSpawner.cs, WaveManager.cs, PlayerBase.cs, HeartSystem.cs |
| Assets/Scripts/UI/ | HUD.cs, MainMenu.cs, LevelSelect.cs, PauseMenu.cs |
| Assets/Scripts/Audio/ | AudioManager.cs |
| Assets/Data/ | All ScriptableObject assets (LevelConfigSO, WaveConfigSO, etc.) |
| Assets/Resources/Templates/ | Baybayin character template .txt coordinate files |
| Assets/Prefabs/Managers/ | All manager prefabs instantiated by Bootstrap |
| Assets/Art/ | Sprite sheets, backgrounds, UI elements |
| Assets/Audio/ | Pronunciation clips, BGM, SFX |

# Changelog

| **Version** | **Changes** |
| --- | --- |
| v1.0 (March 2026) | Initial TDD. Covers system architecture, recognition pipeline, combat system, EventBus pattern, data layer, audio system, build configuration, and folder structure. Six architecture diagrams included. |

*This document is a living reference. Update it whenever a system**'**s design changes. Track every change in the changelog above.*