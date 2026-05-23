# 05 — Data Contracts and ScriptableObjects
**Project:** Salinlahi
**Version:** 1.4
**Date:** 2026-05-23
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

**Multi-Template Note:**
TDD §2.2 specifies that multiple templates per character are supported (e.g., `BA_template_01.txt`, `BA_template_02.txt`) to handle handwriting variation. The current `templateFileName` field is a single `string`, which covers the base case of one template per character. If recognition accuracy tuning in Sprint 2 requires multiple templates per character, this field must either be changed to `List<string> templateFileNames` or the team must create multiple `BaybayinCharacterSO` assets per character. This decision is deferred to Sprint 2 integration.

[EVIDENCE: Assets/Scripts/Data/BaybayinCharacterSO.cs]
[EVIDENCE: docs/capstone/TDD.md, §5 Data Layer — BaybayinCharacterSO row]

---

### 2.2 `EnemyDataSO`

**Menu path:** `Salinlahi/Enemy Data`
**File:** `Assets/Scripts/Data/EnemyDataSO.cs`
**Asset folder:** `Assets/ScriptableObjects/`

| Field | Type | Header | Required | Notes |
|-------|------|--------|----------|-------|
| `enemyID` | `string` | Identity | YES | Lowercase ID. Confirmed values in `Assets/ScriptableObjects/`: `soldado, soldier, heitai, maestro, pensionado, general, kisha, kempei, shokan`, plus the boss type and legacy `shielded`/`sprinter` placeholders. |
| `moveSpeed` | `float` | Stats | YES | World units per second. Default `1.5f`. |
| `maxHealth` | `int` | Health | YES | Default `1`. `2` for shielded variants (Capitan, Shokan). Replaces the old `hitsRequired` field. |
| `walkFrames` | `Sprite[]` | Visuals | YES | At least 1 frame required. |
| `animatorController` | `RuntimeAnimatorController` | Visuals | NO | Optional animator override. |
| `assignedCharacter` | `BaybayinCharacterSO` | Character | YES | The character this enemy actually demands to be defeated. |
| `isDecoy` | `bool` | Decoy | NO | `true` for Maestro — drawing its character penalizes. |
| `dealsContactDamage` | `bool` | Contact Behavior | YES | Default `true`. `false` despawns the enemy without damaging the Shrine on contact. |
| `era` | `Era` | Variant Era | YES | `Era` enum: `Spanish, American, Japanese`. Used by `GeneralAura` to limit its buff to American-era allies. |
| `zigzagAmplitude` | `float` | Zigzag Mover (Pensionado) | NO | World-unit sine amplitude. `0` disables. |
| `zigzagFrequency` | `float` | Zigzag Mover (Pensionado) | NO | Hz. `0` disables. |
| `baseSpeedMultiplier` | `float` | Base Speed Modifier (General) | NO | Multiplier on top of `moveSpeed`. Default `1f`. |
| `auraRadius` | `float` | Aura (General) | NO | World-unit radius. `0` disables. |
| `auraSpeedMultiplier` | `float` | Aura (General) | NO | Multiplier applied to affected same-era non-boss enemies. Default `1.3f`. |
| `deathFrames` | `Sprite[]` | Death Animation | NO | Played in sequence on `Defeat()` before returning to pool. Empty = instant despawn. |
| `deathAnimationFps` | `float` | Death Animation | NO | Falls back to walk FPS when 0. Default `8f`. |
| `useHurtFeedback` | `bool` | Hurt Feedback | NO | Master toggle. HP=1 enemies never run hurt feedback regardless. |
| `hurtPausesMovement` | `bool` | Hurt Feedback — Movement Pause | NO | Freeze descent on non-lethal hit. |
| `hurtPauseDuration` | `float` | Hurt Feedback — Movement Pause | NO | Default `0.25f`. |
| `hurtShakesSprite` | `bool` | Hurt Feedback — Sprite Shake | NO | Jitter on hit. |
| `hurtShakeMagnitude` | `float` | Hurt Feedback — Sprite Shake | NO | Default `0.08f`. |
| `hurtShakeDuration` | `float` | Hurt Feedback — Sprite Shake | NO | Default `0.2f`. Should be ≤ `hurtPauseDuration`. |
| `hurtShakeFrequency` | `float` | Hurt Feedback — Sprite Shake | NO | Oscillations/sec. Default `30f`. |
| `hurtSwapsCharacter` | `bool` | Hurt Feedback — Character Swap | NO | On first non-lethal hit, swap to `postHurtCharacter`. |
| `postHurtCharacter` | `BaybayinCharacterSO` | Hurt Feedback — Character Swap | NO | Only consulted when `hurtSwapsCharacter == true`. |
| `hurtFrames` | `Sprite[]` | Hurt Feedback — Hurt Animation | NO | Frames played on non-lethal hit. |
| `hurtAnimationFps` | `float` | Hurt Feedback — Hurt Animation | NO | Default `12f`. |
| `chargeMultiplier` | `float` | Kisha Charge | NO | Variant-specific: KishaMover. Default `2.5f`. |
| `chargeTriggerYNormalized` | `float [0,1]` | Kisha Charge | NO | Viewport Y to start pause/charge. |
| `pauseDuration` | `float` | Kisha Charge | NO | Default `0.35f`. |
| `scrambleRadius` | `float` | Kempei Censor | NO | KempeiScrambleController radius. Default `3f`. |
| `scrambleMinGlitchInterval` | `float` | Kempei Censor | NO | Default `0.18f`. |
| `scrambleMaxGlitchInterval` | `float` | Kempei Censor | NO | Default `0.36f`. |

**Validation Rules:**
- `moveSpeed` must be > 0. Value ≤ 0 causes the enemy to never move (not crash-safe, but functionally broken).
- `maxHealth ≥ 1`.
- `assignedCharacter` must not be null. An enemy with a null character cannot be defeated by drawing.
- `walkFrames` must contain at least one entry.
- `era` must be one of `Spanish, American, Japanese`.
- `hurtShakeDuration` should be ≤ `hurtPauseDuration` so the shake ends inside the freeze window.
- Aura/decoy/zigzag/charge/scramble fields are variant-specific — leaving them at their default values disables the behavior.

#### 2.2.1 `Era` enum

Defined at the bottom of `EnemyDataSO.cs`:

| Value | Notes |
|-------|-------|
| `Spanish`, `American`, `Japanese` | Chapter/faction grouping. Drives same-era aura targeting (`GeneralAura`) and other era-scoped behaviors. |

[EVIDENCE: Assets/Scripts/Data/EnemyDataSO.cs]
[EVIDENCE: Assets/ScriptableObjects/EnemyData_*.asset]

---

### 2.3 `LevelConfigSO`

**Menu path:** `Salinlahi/Level Config`
**File:** `Assets/Scripts/Data/LevelConfigSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Levels/`

| Field | Type | Header | Required | Invariants |
|-------|------|--------|----------|------------|
| `levelName` | `string` | Identity | YES | Human-readable display name. Example: `"Chapter 1 - Level 1"`. |
| `levelNumber` | `int` | Identity | YES | 1-indexed. Story Mode range: 1–15. Must be globally unique. |
| `chapterNumber` | `int` | Identity | YES | Default `1`. Author-facing label for HUD/level-select grouping. |
| `chapterName` | `string` | Identity | YES | Default `"Chapter 1"`. Author-facing label for HUD/level-select grouping. |
| `eraTheme` | `EraThemeSO` | Identity | NO | Visual theme for this level's era (background, ground, shrine, decorations). Consumed by `EnvironmentThemeSwapper`. |
| `waves` | `List<WaveConfigSO>` | Waves | YES (non-boss) | Ordered list of waves played in index order. Ignored when `bossConfig != null`. |
| `allowedCharacters` | `List<BaybayinCharacterSO>` | Characters | YES | Master allowed-character list for this level. All `WaveConfigSO.charactersInWave` entries must be a subset of this list. |
| `bossConfig` | `BossConfigSO` | Boss | NO | If assigned, this level is a boss encounter. The level is treated as a boss level whenever this reference is non-null. |
| `isAvailableInLite` | `bool` | Build Flags | YES | `true` for levels 1–3 (Salinlahi Lite). `false` for levels 4–15 (Full only). Default `true`. |

**Validation Rules:**
- Levels 1–3: `isAvailableInLite = true`.
- Levels 4–15: `isAvailableInLite = false`.
- When `bossConfig != null`, the level runs the boss encounter and the `waves` list is ignored (per the LevelConfigSO inspector tooltip and `WaveManager.RunAllWavesRoutine`).
- For non-boss levels, the `waves` list must not be empty. An empty wave list causes immediate level-complete with no gameplay.
- `chapterNumber` and `chapterName` are author-facing labels for HUD/level-select grouping.

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
| `waveID` | `string` | Identity | YES (non-intermission) | Unique string identifier. Example: `"L1_W1"`. Used for debug logging and potential save-state keying. |
| `waveNumber` | `int` | Identity | YES (non-intermission) | 1-indexed within the level. Used for HUD display. |
| `isIntermissionWave` | `bool` | Identity | NO | When `true`, `OnValidate` skips `waveID`/`waveNumber` checks. Used for boss intermission waves. |
| `charactersInWave` | `List<BaybayinCharacterSO>` | Spawn Settings | YES | Baybayin characters that can appear on enemies in this wave. WaveManager draws from this list when assigning characters to spawned enemies. Must not be empty. |
| `enemyTypesInWave` | `List<EnemyDataSO>` | Spawn Settings | NO | Pool of enemy types this wave may spawn. `WaveSpawner.SelectEnemyDataForSpawn` picks at random; falls back to the spawner's `_fallbackEnemyData` if this list is empty. |
| `enemyCount` | `int` | Spawn Settings | YES | Total enemies spawned in this wave. Default `5`. Must be ≥ 1. |
| `spawnInterval` | `float` | Spawn Settings | YES | Seconds between consecutive enemy spawns. Default `3f`. Must be > 0. |
| `waveStartDelay` | `float` | Spawn Settings | YES | Seconds of delay before first enemy spawns in this wave. Default `1f`. May be 0. |

**Validation Rules:**
- `charactersInWave` must be a non-empty subset of the parent `LevelConfigSO.allowedCharacters`.
- When `isIntermissionWave` is `false`, `waveID` must be non-empty and `waveNumber > 0` (enforced by `OnValidate`).
- `enemyCount` ≥ 1.
- `spawnInterval` > 0 (zero causes instantaneous spawn of all enemies simultaneously — gameplay-breaking).
- Missing entries in `enemyTypesInWave` / `charactersInWave` are flagged by `OnValidate`.

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

### 2.6 `BossConfigSO`

**Menu path:** `Salinlahi/Boss Config`
**File:** `Assets/Scripts/Data/BossConfigSO.cs`
**Asset folder:** `Assets/ScriptableObjects/` (existing assets include `BossConfig_ElInquisidor.asset`)

| Field | Type | Header | Required | Notes |
|-------|------|--------|----------|-------|
| `bossName` | `string` | Identity | YES | Display name. |
| `bossID` | `string` | Identity | YES | Internal id (e.g. `el_inquisidor`). |
| `bossSprite` | `Sprite` | Visuals | NO | HUD/portrait sprite, distinct from the in-world enemy sprite. |
| `bossEnemyData` | `EnemyDataSO` | Spawning | YES | Defines the boss's prefab, base sprite, animator, collision. Its `assignedCharacter` MUST be null so the boss is invisible to `FindClosestToBase`. |
| `phases` | `List<BossPhase>` | Phases | YES | Ordered. Phase count = boss's effective HP. Last phase clear ends the encounter. |
| `fallbackEnemyTypes` | `List<EnemyDataSO>` | Summon Fallback | NO | Used when a phase's `summonEnemyTypes` is empty. |
| `summonHorizontalBounds` | `Vector2` | Summon Bounds | NO | Hard world-space horizontal cap on every minion spawn (`x = minX, y = maxX`). Set `x ≥ y` to disable. |
| `introDuration` | `float` | Intro / Outro | YES | Seconds boss is invulnerable on entry. Default `2.0f`. |
| `outroDuration` | `float` | Intro / Outro | YES | Seconds before `OnLevelComplete` after the last phase is cleared. Default `2.5f`. |

**Validation Rules:**
- `phases.Count ≥ 1` (zero phases → `BossController.StartBoss` aborts with a logged error).
- `bossEnemyData.assignedCharacter` must be null so the boss is excluded from `CombatResolver` closest-match.
- If a phase's `summonEnemyTypes` is empty, `fallbackEnemyTypes` must provide at least one entry — otherwise `BossSummonTicker` skips the spawn and logs a warning.

[EVIDENCE: Assets/Scripts/Data/BossConfigSO.cs]
[EVIDENCE: Assets/ScriptableObjects/BossConfig_ElInquisidor.asset]

---

### 2.7 `BossPhase`

**File:** `Assets/Scripts/Data/BossPhase.cs`
**Embedding:** `[System.Serializable]` class stored in `BossConfigSO.phases`. Not a `ScriptableObject` and has no menu path.

| Field | Type | Header | Notes |
|-------|------|--------|-------|
| `summonDuration` | `float` | Summoning Phase | Seconds the boss summons minions. Default `30f`. |
| `summonInterval` | `float` | Summoning Phase | Seconds between summon ticks. In Teleport movement, also the teleport cadence. Default `5f`. |
| `summonBurstMin` | `int` | Summoning Phase | Min minions per tick (inclusive). Default `2`. |
| `summonBurstMax` | `int` | Summoning Phase | Max minions per tick (inclusive, `Random.Range(min, max+1)`). Default `3`. |
| `summonEnemyTypes` | `List<EnemyDataSO>` | Summoning Phase | Pool for this phase. Empty falls back to `BossConfigSO.fallbackEnemyTypes`. |
| `summonSpawnRange` | `Vector2` | Summoning Phase | Half-range around the boss's CURRENT position for each minion's spawn origin. Default `(2, 0)`. |
| `requiredCharacterCount` | `int` | Vulnerability Window | Correct random glyphs needed during the Vulnerable window. Default `3`. |
| `vulnerabilityTimer` | `float` | Vulnerability Window | Seconds the Vulnerable window lasts (timer starts after the collapse animation). Default `12f`. |
| `movementPattern` | `BossMovementPattern` | Movement | `Hover, Pace, Teleport`. Default `Pace`. |
| `movementSpeed` | `float` | Movement | World units/sec. Used by Pace; ignored by Hover/Teleport. Default `1f`. |
| `paceHalfRange` | `float` | Movement | Pace only: horizontal half-range around spawn. Default `1.5f`. |
| `teleportHalfRange` | `Vector2` | Movement | Teleport only: half-range around base position. `Y > 0` enables vertical teleport. Default `(2, 0)`. |

`BossMovementPattern` enum (defined in the same file): `Hover` / `Pace` / `Teleport`.

[EVIDENCE: Assets/Scripts/Data/BossPhase.cs]

---

### 2.8 `EraThemeSO`

**Menu path:** `Salinlahi/Era Theme`
**File:** `Assets/Scripts/Data/EraThemeSO.cs`
**Asset folder:** `Assets/ScriptableObjects/`

Per-era visual theme referenced by `LevelConfigSO.eraTheme` and consumed by `EnvironmentThemeSwapper` to swap background, ground, shrine, and base-zone visuals per level era. Inferred fields (matching the ERD in `docs/capstone/SystemDiagrams.md`):

| Field | Notes |
|-------|-------|
| `eraName` | Identity label. |
| `backgroundSprite` | Main background sprite. |
| `groundSprite` | Tiled ground sprite. |
| `shrineSprite` | Era-specific shrine sprite. |
| `baseZoneSprite` | Fence/barrier sprite at the shrine defense line. |

[EVIDENCE: Assets/Scripts/Data/EraThemeSO.cs]
[EVIDENCE: docs/capstone/SystemDiagrams.md — ERD]

---

### 2.9 `GameConfigSO`

**Menu path:** `Salinlahi/Game Config`
**File:** `Assets/Scripts/Data/GameConfigSO.cs`

Single tuning asset (one project-wide instance) that holds runtime tuning knobs. ERD-visible fields:

| Field | Notes |
|-------|-------|
| `focusModeThreshold` | Consecutive correct draws needed to trigger Focus Mode. |
| `focusModeDuration` | Seconds Focus Mode stays active. |
| `focusModeSpeedMultiplier` | Enemy speed multiplier while Focus Mode is active (e.g. `0.5` = half speed). |

[EVIDENCE: Assets/Scripts/Data/GameConfigSO.cs]
[EVIDENCE: docs/capstone/SystemDiagrams.md — ERD]

---

### 2.10 `CharacterRegistrySO`

**Menu path:** `Salinlahi/Character Registry`
**File:** `Assets/Scripts/Data/CharacterRegistrySO.cs`

Master registry of all `BaybayinCharacterSO` assets, exposed via the `All` list. Used by sandbox mode (`WaveManager` sandbox code path) and visual scramble checks that need to enumerate every available character.

[EVIDENCE: Assets/Scripts/Data/CharacterRegistrySO.cs]

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
| `EnemyDataSO` | `Assets/ScriptableObjects/` |
| `BossConfigSO` | `Assets/ScriptableObjects/` (e.g. `BossConfig_ElInquisidor.asset`, alongside other top-level configs) |
| Templates (text files) | `Assets/Resources/Templates/` |

[EVIDENCE: Assets/ScriptableObjects/ directory listing — Characters/, Levels/, Waves/ subdirs confirmed]
[EVIDENCE: docs/capstone/TDD.md, §7.4 Folder Structure]

### 3.3 Template File Format

Each `BaybayinCharacterSO.templateFileName` references a plain-text coordinate file in `Assets/Resources/Templates/`. Format is determined by the `TemplateLoader.cs` implementation. Expected content per `Salinlahi.md §3.3.3`: comma-separated 2D point coordinates representing the resampled $P point cloud for that character.

Authoring rule: Template files must be validated against `RecognitionConfigSO.resamplePointCount` (default 32 points). A template with a different point count will cause a recognition error.
