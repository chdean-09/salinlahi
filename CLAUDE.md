# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Salinlahi is a 2D pixel art mobile defense game (Android/iOS) built in **Unity 6 LTS (6000.3.9f1)** with **C#**. Players draw Baybayin (Filipino script) characters on a touchscreen to defeat approaching enemies. It is an educational game that teaches Baybayin through intrinsic gameplay integration.

## Build & Test

- **Unity Editor**: Open project in Unity 6, press Play to test
- **Build**: File > Build Settings > select Android/iOS > Build
- **Solution**: `Salinlahi.sln` (C# 9.0, .NET Framework 4.7.1)
- **Testing**: Unity Test Framework (`com.unity.test-framework@1.6.0`) is installed; tests go in `Assets/Tests/`
- **CI**: GitHub Actions validates PR titles and branch names against Jira format (`.github/workflows/jira-validation.yml`)

## Architecture

**Bootstrap-then-Singleton pattern**: App launches into `Bootstrap` scene, which initializes all manager singletons (marked `DontDestroyOnLoad`), then transitions to `MainMenu`.

**Scene flow**: Bootstrap → MainMenu → Gameplay → GameOver

**Core systems** (all in `Assets/Scripts/Core/`):
- `GameManager` — owns `GameState` enum (Idle, Playing, Paused, GameOver, LevelComplete)
- `EventBus` — static pub-sub for all cross-system communication (subscribe in `OnEnable`, unsubscribe in `OnDisable`)
- `SceneLoader` — async scene transitions
- `AudioManager` — BGM/SFX playback

**Gameplay systems** (`Assets/Scripts/Gameplay/`):
- `DollarPRecognizer` — $P point-cloud gesture recognition algorithm
- `CombatResolver` — maps recognized character to closest matching enemy
- `EnemyPool` — object pooling via Unity's `ObjectPool<Enemy>` (no runtime Instantiate/Destroy)
- `WaveManager`/`WaveSpawner` — wave progression and enemy spawning
- `ActiveEnemyTracker` — tracks live enemies on screen

**Data layer** (`Assets/Scripts/Data/`): All content is ScriptableObject-driven — characters, levels, waves, enemies, configs are inspector-editable assets in `Assets/ScriptableObjects/`.

**Base class**: All managers inherit from `Singleton<T>` (`Assets/Scripts/Utilities/Singleton.cs`).

## Naming Conventions

### C# Code
- Classes: `PascalCase` — `GameManager`, `EnemyPool`
- Private fields: `_camelCase` — `_bgmSource`, `_speed`
- Public properties: `PascalCase` — `CurrentState`, `Character`
- Constants: `ALL_CAPS`
- EventBus events: `On[Event]` (e.g., `OnGameOver`); raisers: `Raise[Event]()`
- Tags: always use `CompareTag()`, never `gameObject.tag ==`

### Assets
- Manager prefabs: `Manager_[Name].prefab`
- Enemy prefabs: `Enemy_[Type].prefab`
- ScriptableObjects: `Char_BA`, `Level_01`, `L1_W1`
- Scenes: PascalCase, no spaces
- Recognition templates: `[CharacterID]_template.txt` in `Assets/Resources/Templates/`

## Terminology

- Use **Baybayin**, never "Alibata"
- Say "get from pool" / "return to pool", never "spawn" / "destroy" for enemies
- Shrine health uses **hearts** (3 default), not generic "health"
- The player character is the **protagonist** (era-specific: Kuya, Laban, Manong)
- **$P algorithm** in technical docs; "Dollar-P" in prose

## Git & Jira Workflow

Every branch, commit, and PR must reference a Jira issue key (`SALIN-XX`).

- **Branch**: `{type}/SALIN-XX-description` where type is one of: feature, bugfix, hotfix, chore, refactor, docs, test, spike
- **Commit**: `{type}(scope): SALIN-XX description` (e.g., `feat(combat): SALIN-41 add combo system`)
- **PR title**: `SALIN-XX: Description`
- No leftover `Debug.Log` calls in committed code

## Documentation

Comprehensive system docs live in `docs/system/` (architecture, data contracts, test strategy, glossary). Academic project documents are in `docs/capstone/`. Jira workflow guides are in `docs/jira/`.
