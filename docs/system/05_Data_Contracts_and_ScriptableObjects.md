# 05 — Data Contracts and ScriptableObjects
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Chad Andrada (Product Owner / Designer)

---

## 1. Design Principle

All game content is defined in ScriptableObject assets. Level designers can create new levels, adjust enemy speeds, change wave compositions, and tune difficulty entirely through the Unity Inspector **without writing code or recompiling**. This separation of content from logic is a non-negotiable architectural constraint.

[EVIDENCE: docs/capstone/TDD.md, §5 Data Layer]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.5.1 — "data assets rather than in code"]

---

## 2. ScriptableObject Schemas

### 2.1 `BaybayinCharacterSO`

**Menu path:** `Salinlahi/Baybayin Character`
**File:** `Assets/Scripts/Data/BaybayinCharacterSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Characters/`

| Field | Type | Header | Required | Invariants |
|-------|------|--------|----------|------------|
| `characterID` | `string` | Identity | YES | Must match template filename prefix. Example: `"BA"` → template file `BA_template.txt` in `Assets/Resources/Templates/`. Case-sensitive. |
| `syllable` | `string` | Identity | YES | Lowercase Filipino syllable shown to player. Example: `"ba"`, `"ka"`, `"ga"`. Must not be empty. |
| `displaySprite` | `Sprite` | Visuals | YES | The Baybayin glyph sprite rendered on the enemy body. Must not be null at runtime. |
| `pronunciationClip` | `AudioClip` | Audio | YES | Played on every successful character recognition via `AudioManager`. Duration must be under 1 second to prevent overlap. Null triggers a silent defeat (no audio error). |
| `templateFileName` | `string` | Recognition | YES | Filename in `Assets/Resources/Templates/` without extension. Example: `"BA_template"`. Must match a file loadable via `Resources.Load<TextAsset>`. |

**Validation Rules:**
- `characterID` must be unique across all `BaybayinCharacterSO` assets in the project.
- `templateFileName` must reference a file that exists in `Assets/Resources/Templates/`.
- `pronunciationClip` must be assigned before Sprint 2 UAT.
- 17 total assets must exist at content-complete milestone (one per Baybayin consonant).

[EVIDENCE: Assets/Scripts/Data/BaybayinCharacterSO.cs]
[EVIDENCE: docs/capstone/TDD.md, §5 Data Layer — BaybayinCharacterSO row]

---

### 2.2 `EnemyDataSO`

**Menu path:** `Salinlahi/Enemy Data`
**File:** `Assets/Scripts/Data/EnemyDataSO.cs`
**Asset folder:** `Assets/ScriptableObjects/` (implied; not confirmed in repo)

| Field | Type | Header | Required | Invariants |
|-------|------|--------|----------|------------|
| `enemyID` | `string` | Identity | YES | Unique type identifier. Canonical values: `"soldado"`, `"fraile"`, `"guardia"`, `"capitan"`, `"soldier"`, `"maestro"`, `"pensionado"`, `"general"`, `"heitai"`, `"kisha"`, `"kempei"`, `"shokan"`. Must be lowercase. |
| `moveSpeed` | `float` | Stats | YES | World units per second toward the base. Default `1.5f`. Must be > 0. |
| `hitsRequired` | `int` | Stats | YES | Number of correct drawings needed to defeat. Default `1`. Shielded enemies use `2`. |
| `walkFrames` | `Sprite[]` | Visuals | YES | Animation frames for walking. At least 1 frame required (index 0 set as initial sprite). Null array or zero length is tolerated by code but produces invisible enemy — authoring error. |
| `animatorController` | `RuntimeAnimatorController` | Visuals | NO | Optional animator override. May be null for static sprite enemies. |
| `assignedCharacter` | `BaybayinCharacterSO` | Character | YES | The Baybayin character this enemy carries and requires drawn to be defeated. Must not be null at runtime. |
| `isDecoy` | `bool` | Special | NO | `true` for Maestro enemies. Drawing their character penalizes the player. Default `false`. |
| `isPhaser` | `bool` | Special | NO | `true` for Fraile enemies. Baybayin label fades in/out on timer. Default `false`. |
| `phaserInterval` | `float` | Special | NO | Seconds between phaser label visibility toggles. Only used when `isPhaser = true`. |
| `hasCorruptionVeil` | `bool` | Special | NO | `true` for Shokan enemies. All three era corruption colors swirl around sprite. Default `false`. |
| `commanderSpeedBuff` | `float` | Special | NO | Speed multiplier applied to nearby same-era enemies while this enemy is alive. Only used for General (`1.3f`). Default `0f` (inactive). |

**Validation Rules:**
- `moveSpeed` must be > 0. Value ≤ 0 causes the enemy to never move (not crash-safe, but functionally broken).
- `assignedCharacter` must not be null. An enemy with a null character cannot be defeated by drawing.
- `walkFrames` must contain at least one entry.

[EVIDENCE: Assets/Scripts/Data/EnemyDataSO.cs]

---

### 2.3 `LevelConfigSO`

**Menu path:** `Salinlahi/Level Config`
**File:** `Assets/Scripts/Data/LevelConfigSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Levels/`

| Field | Type | Header | Required | Invariants |
|-------|------|--------|----------|------------|
| `levelName` | `string` | Identity | YES | Human-readable display name. Example: `"Chapter 1 - Level 1"`. |
| `levelNumber` | `int` | Identity | YES | 1-indexed. Story Mode range: 1–15. Must be globally unique. |
| `waves` | `List<WaveConfigSO>` | Waves | YES | Ordered list of waves played in index order. Must not be empty. |
| `allowedCharacters` | `List<BaybayinCharacterSO>` | Characters | YES | Master allowed-character list for this level. All `WaveConfigSO.charactersInWave` entries must be a subset of this list. |
| `baseSpawnDelay` | `float` | Spawn Settings | YES | Base delay between enemy spawns for this level. |
| `isBossLevel` | `bool` | Boss | YES | `true` for levels 5, 10, 15. When true, `LevelFlowController` activates `BossController` instead of `WaveManager` after final wave. |
| `bossConfig` | `BossConfigSO` | Boss | NO | Reference to boss configuration. Null for non-boss levels. Required for boss levels (5, 10, 15). |
| `isAvailableInLite` | `bool` | Build Flags | YES | `true` for levels 1–3 (Salinlahi Lite). `false` for levels 4–15 (Full only). |

**Validation Rules:**
- Levels 1–3: `isAvailableInLite = true`.
- Levels 4–15: `isAvailableInLite = false`.
- Boss levels (5, 10, 15): must reference a `BossConfigSO` (field NOT FOUND in current implementation — planned Sprint 3).
- `waves` list must not be empty. An empty wave list causes immediate level-complete with no gameplay.

[EVIDENCE: Assets/Scripts/Data/LevelConfigSO.cs]
[EVIDENCE: docs/capstone/TDD.md, §5 Data Layer — LevelConfigSO row]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.4 Business Model — Lite = levels 1–3]

---

### 2.4 `WaveConfigSO`

**Menu path:** `Salinlahi/Wave Config`
**File:** `Assets/Scripts/Data/WaveConfigSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Waves/`

| Field | Type | Header | Required | Invariants |
|-------|------|--------|----------|------------|
| `waveID` | `string` | Identity | YES | Unique string identifier. Example: `"L1_W1"`. Used for debug logging and potential save-state keying. |
| `waveNumber` | `int` | Identity | YES | 1-indexed within the level. Used for HUD display. |
| `charactersInWave` | `List<BaybayinCharacterSO>` | Spawn Settings | YES | Baybayin characters that can appear on enemies in this wave. WaveManager draws from this list when assigning characters to spawned enemies. Must not be empty. |
| `enemyCount` | `int` | Spawn Settings | YES | Total enemies spawned in this wave. Default `5`. Must be ≥ 1. |
| `spawnInterval` | `float` | Spawn Settings | YES | Seconds between consecutive enemy spawns. Default `3f`. Must be > 0. |
| `waveStartDelay` | `float` | Spawn Settings | YES | Seconds of delay before first enemy spawns in this wave. Default `1f`. May be 0. |

**Validation Rules:**
- `charactersInWave` must be a non-empty subset of the parent `LevelConfigSO.allowedCharacters`.
- `enemyCount` ≥ 1.
- `spawnInterval` > 0 (zero causes instantaneous spawn of all enemies simultaneously — gameplay-breaking).

[EVIDENCE: Assets/Scripts/Data/WaveConfigSO.cs]

---

### 2.5 `RecognitionConfigSO`

**Menu path:** `Salinlahi/Recognition Config`
**File:** `Assets/Scripts/Data/RecognitionConfigSO.cs`

| Field | Type | Range | Default | Invariant |
|-------|------|-------|---------|-----------|
| `resamplePointCount` | `int` | 16–64 (`[Range]`) | `32` | Number of points $P resamples each stroke to. Reducing below 16 degrades accuracy. Increasing above 64 increases recognition latency beyond 50ms budget. |
| `minimumConfidence` | `float` | 0–1 (`[Range]`) | `0.60` | Minimum score to accept a recognition result. Lowering increases false positives. Raising increases false negatives. Do not change without UAT re-validation. |
| `multiStrokeWindowSeconds` | `float` | — | `1.5f` | Seconds after finger lift before recognition submits. Allows multi-stroke Baybayin characters. |
| `minimumPointCount` | `int` | — | `8` | Minimum screen points in a stroke to be considered valid. Prevents taps from being interpreted as drawing attempts. |

**Validation Rules:**
- `minimumConfidence` must not be changed from `0.60` without a documented UAT re-validation run.
- `resamplePointCount` must not exceed 64 (latency constraint: <50ms).

[EVIDENCE: Assets/Scripts/Data/RecognitionConfigSO.cs]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.3.3 — 32 points, 0.60 threshold, 1.5s window]

---

### 2.6 `BossConfigSO` — NOT FOUND

`BossConfigSO` is specified in `TDD.md §5 Data Layer` and is referenced in `LevelConfigSO` design for boss levels (5, 10, 15). **No implementation file exists in `Assets/Scripts/Data/`.**

Required fields (from TDD spec and Team README §4):

| Field | Type | Description |
|-------|------|-------------|
| `bossName` | `string` | Display name of the boss |
| `totalPhases` | `int` | Number of distinct phases |
| `phaseCharacters` | `List<BaybayinCharacterSO>` | Characters required per phase |
| `timeLimitPerPhase` | `float` | Seconds allowed per phase |
| `bossSprites` | `Sprite[]` | Visual sprites for boss states |
| `bossThemeClip` | `AudioClip` | Boss encounter background music |

Status: **NOT FOUND** — P1 gap, required Sprint 3.

[EVIDENCE: docs/capstone/TDD.md, §5 Data Layer — BossConfigSO row]
[EVIDENCE: Team README §4 — BossConfigSO code sample]

---

## 3. Asset Authoring Guidelines

### 3.1 Naming Convention

| Asset Type | Pattern | Example |
|------------|---------|---------|
| `BaybayinCharacterSO` | `Char_[ID]` | `Char_BA`, `Char_KA` |
| `EnemyDataSO` | `Enemy_[type]` | `Enemy_Standard`, `Enemy_Fast` |
| `LevelConfigSO` | `Level_[number]` | `Level_01`, `Level_10` |
| `WaveConfigSO` | `L[level]_W[wave]` | `L1_W1`, `L3_W2` |
| `RecognitionConfigSO` | `RecognitionConfig` | (singleton asset) |

### 3.2 Asset Folder Map

| Asset Type | Folder |
|------------|--------|
| `BaybayinCharacterSO` | `Assets/ScriptableObjects/Characters/` |
| `LevelConfigSO` | `Assets/ScriptableObjects/Levels/` |
| `WaveConfigSO` | `Assets/ScriptableObjects/Waves/` |
| `EnemyDataSO` | `Assets/ScriptableObjects/` (confirm with Designer) |
| Templates (text files) | `Assets/Resources/Templates/` |

[EVIDENCE: Assets/ScriptableObjects/ directory listing — Characters/, Levels/, Waves/ subdirs confirmed]
[EVIDENCE: docs/capstone/TDD.md, §7.4 Folder Structure]

### 3.3 Template File Format

Each `BaybayinCharacterSO.templateFileName` references a plain-text coordinate file in `Assets/Resources/Templates/`. Format is determined by the `TemplateLoader.cs` implementation (NOT FOUND). Expected content per `Salinlahi.md §3.3.3`: comma-separated 2D point coordinates representing the resampled $P point cloud for that character.

Authoring rule: Template files must be validated against `RecognitionConfigSO.resamplePointCount` (default 32 points). A template with a different point count will cause a recognition error.
