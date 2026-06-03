# 05 — Data Contracts and ScriptableObjects
**Project:** Salinlahi
**Version:** 2.3
**Date:** 2026-06-03
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
| `almanacSprite` | `Sprite` | Visuals | NO | Stylized glyph shown in the Almanac character grid and detail view (`Art/UI/Almanac/[ID]-Almanac.png`). Falls back to `displaySprite` when null. |
| `badgeSprite` | `Sprite` | Visuals | NO | Framed glyph used by `EnemyGlyphBadge` during gameplay. Distinct from `displaySprite` (Tracing Dojo). |
| `scrambledBadgeSprite` | `Sprite` | Visuals | NO | Optional framed + glitched variant when a visual override is active (e.g. Kempei scramble). Falls back to `badgeSprite` when null. |
| `pronunciationClip` | `AudioClip` | Audio | YES | Played on every successful character recognition via `AudioManager`. Duration must be under 1 second to prevent overlap. Null triggers a silent defeat (no audio error). |
| `templateFileName` | `string` | Recognition | YES | Filename in `Assets/Resources/Templates/` without extension. Example: `"BA_template"`. Must match a file loadable via `Resources.Load<TextAsset>`. |
| `description` | `string` | Almanac | NO | Short player-facing description of the character shown in the Almanac detail panel. May be empty; Almanac UI falls back to an empty string gracefully. |

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
| `displayName` | `string` | Almanac | NO | Human-readable name shown in the Almanac enemy detail panel (e.g. `"Soldado"`). Falls back to `enemyID` when empty. |
| `description` | `string` | Almanac | NO | Short player-facing description of the enemy shown in the Almanac detail panel. May be empty. |
| `portraitSprite` | `Sprite` | Almanac | NO | Portrait sprite used in the Almanac detail panel. When null, `AlmanacEnemyEntry.ResolvePortrait()` falls back to `walkFrames[0]`. |
| `overrideBadgeOffset` | `bool` | Glyph Badge Override | NO | If true, `glyphBadgeOffsetOverride` replaces `GlyphBadgeConfigSO.defaultWorldOffset`. |
| `glyphBadgeOffsetOverride` | `Vector2` | Glyph Badge Override | NO | Per-enemy badge offset; consulted only when `overrideBadgeOffset` is true. |
| `overrideBadgeScale` | `bool` | Glyph Badge Override | NO | If true, `glyphBadgeScaleOverride` replaces `GlyphBadgeConfigSO.defaultWorldScale`. |
| `glyphBadgeScaleOverride` | `float` | Glyph Badge Override | NO | Per-enemy badge scale; consulted only when `overrideBadgeScale` is true. Default `1f`. |

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

### 2.2.2 `GlyphBadgeConfigSO`

**Menu path:** `Salinlahi/Glyph Badge Config`
**File:** `Assets/Scripts/Data/GlyphBadgeConfigSO.cs`
**Asset folder:** `Assets/ScriptableObjects/GlyphBadgeConfig_Default.asset` (single instance referenced by every `EnemyGlyphBadge` prefab instance)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `defaultWorldOffset` | `Vector2` | `(0, 1.2)` | Default local offset from enemy root. |
| `defaultWorldScale` | `float` | `1` | Default world-stable scale of the badge transform. |
| `swapSlideOffset` | `Vector2` | `(-0.8, 0)` | Slide direction + magnitude for swap animation. |
| `swapOutDuration` | `float` | `0.18` | Seconds for old sprite to slide out + fade. |
| `swapInDuration` | `float` | `0.18` | Seconds for new sprite to slide in + fade. |
| `finalDrawFlashColor` | `Color` | white | Tint applied during final-draw charge phase. |
| `finalDrawChargeDuration` | `float` | `0.08` | Seconds for scale-up + flash phase. |
| `finalDrawChargeScale` | `float` | `1.15` | Peak scale multiplier at end of charge phase. |
| `finalDrawReleaseDuration` | `float` | `0.18` | Seconds for shrink + drift phase. |
| `finalDrawReleaseRise` | `float` | `0.25` | Local-Y added during release phase. |
| `finalDrawReleaseRotation` | `float` | `10` | Degrees of rotation added during release phase. |
| `decoyRejectFlashColor` | `Color` | `(1, 0.3, 0.3, 1)` | Tint during decoy-reject flash. |
| `decoyRejectFlashDuration` | `float` | `0.1` | Seconds for the red flash. |
| `decoyRejectShakeMagnitude` | `float` | `0.1` | Peak shake offset (world units). |
| `decoyRejectShakeDuration` | `float` | `0.3` | Total shake duration. |
| `decoyRejectShakeFrequency` | `float` | `18` | Shake oscillations per second. |
| `failFlashColor` | `Color` | `(1, 0.3, 0.3, 1)` | Tint for boss draw-fail flash on the world badge. |
| `failFlashDuration` | `float` | `0.15` | Seconds for fail flash. |

[EVIDENCE: Assets/Scripts/Data/GlyphBadgeConfigSO.cs]

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
| `numberSprite` | `Sprite` | Identity | NO | Baked-in numbered scroll sprite displayed on this level's Level Select button. Null triggers a warning log from LevelButton; the scroll image is unchanged. |
| `waves` | `List<WaveDefinition>` | Waves | YES (non-boss) | Ordered list of embedded waves. Each `WaveDefinition`'s character/enemy subset is kept ⊆ the level rosters by `ReconcileWavesToRoster()`. Ignored when `bossConfig != null`. |
| `allowedCharacters` | `List<BaybayinCharacterSO>` | Characters | YES | Master allowed-character list for this level. All `WaveDefinition.characters` entries must be a subset of this list. |
| `allowedEnemyTypes` | `List<EnemyDataSO>` | Characters | YES | Master enemy-type roster for this level. All `WaveDefinition.enemyTypes` entries must be a subset of this list. |
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

### 2.4 `WaveDefinition`

**Type:** Embedded `[System.Serializable]` class (not a ScriptableObject — no separate asset file)
**File:** `Assets/Scripts/Data/WaveDefinition.cs`
**Owner:** Stored inline inside `LevelConfigSO.waves`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `isIntermissionWave` | `bool` | NO | When `true`, marks a boss intermission wave. |
| `characters` | `List<BaybayinCharacterSO>` | YES (non-intermission) | Wave-subset of the level's `allowedCharacters` roster. Pruned to roster by `ReconcileWavesToRoster()`. |
| `enemyTypes` | `List<EnemyDataSO>` | YES (non-intermission) | Wave-subset of the level's `allowedEnemyTypes` roster. Pruned to roster by `ReconcileWavesToRoster()`. |
| `enemyCount` | `int` | YES | Total enemies spawned in this wave. Default `5`. |
| `spawnInterval` | `float` | YES | Seconds between enemy spawns. Default `3f`. |
| `waveStartDelay` | `float` | NO | Seconds before this wave begins. Default `1f`. |

**Invariants:**
- `characters` ⊆ `LevelConfigSO.allowedCharacters` (enforced by `ReconcileWavesToRoster` on `OnValidate`).
- `enemyTypes` ⊆ `LevelConfigSO.allowedEnemyTypes` (enforced by `ReconcileWavesToRoster` on `OnValidate`).
- Removing an entry from a level roster automatically removes it from all waves in the same `OnValidate` call.

[EVIDENCE: Assets/Scripts/Data/WaveDefinition.cs]
[EVIDENCE: Assets/Scripts/Data/LevelConfigSO.cs — ReconcileWavesToRoster]

---

### 2.5 `RecognitionConfigSO`

**Menu path:** `Salinlahi/Recognition Config`
**File:** `Assets/Scripts/Data/RecognitionConfigSO.cs`

| Field | Type | Range | Default | Invariant |
|-------|------|-------|---------|-----------|
| `resamplePointCount` | `int` | 16–64 (`[Range]`) | `32` | Number of points $P resamples each stroke to. Reducing below 16 degrades accuracy. Increasing above 64 increases recognition latency beyond 50ms budget. |
| `minimumConfidence` | `float` | 0–1 (`[Range]`) | `0.60` | Minimum score to accept a recognition result. Lowering increases false positives. Raising increases false negatives. Do not change without UAT re-validation. |
| `multiStrokeWindowSeconds` | `float` | — | `1.5f` | Seconds after finger lift before recognition submits. Allows multi-stroke Baybayin characters. |
| `rawSampleMinDistancePixels` | `float` | — | `1.5f` | Minimum movement before a new raw sample is accepted. Keeps recognition geometry dense enough for mobile while filtering duplicate points. |
| `visualSampleSpacingPixels` | `float` | — | `4f` | Target spacing for visual-only curve samples used by `DrawingCanvas`. Does not affect recognition input. |
| `maxVisualSamplesPerSegment` | `int` | — | `8` | Safety cap for visual interpolation samples inserted per raw segment. Does not affect recognition input. |
| `minimumStrokePathLengthPixels` | `float` | — | `18f` | Rejects tap-like strokes whose total raw path length is too short. |
| `minimumStrokeBoundsPixels` | `float` | — | `10f` | Rejects tap-like strokes whose raw bounding box is too small. |

**Validation Rules:**
- `minimumConfidence` must not be changed from `0.60` without a documented UAT re-validation run.
- `resamplePointCount` must not exceed 64 (latency constraint: <50ms).
- Tap rejection is based on raw path length and raw bounds, not raw point count.
- Visual interpolation is for rendering only. Recognition receives cloned raw stroke points captured before visual smoothing.

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
| `description` | `string` | Almanac | NO | Short player-facing description of the boss shown in the Almanac detail panel. May be empty. |
| `bossSprite` | `Sprite` | Visuals | NO | HUD/portrait sprite, distinct from the in-world enemy sprite. |
| `bossEnemyData` | `EnemyDataSO` | Spawning | YES | Defines the boss's prefab, base sprite, animator, collision. Its `assignedCharacter` MUST be null so the boss is invisible to `FindClosestToBase`. |
| `phases` | `List<BossPhase>` | Phases | YES | Ordered. Phase count = boss's effective HP. Last phase clear ends the encounter. |
| `fallbackEnemyTypes` | `List<EnemyDataSO>` | Summon Fallback | NO | Used when a phase's `summonEnemyTypes` is empty. |
| `summonHorizontalBounds` | `Vector2` | Summon Bounds | NO | Hard world-space horizontal cap on every minion spawn (`x = minX, y = maxX`). Set `x ≥ y` to disable. |
| `introDuration` | `float` | Intro / Outro | YES | Seconds boss is invulnerable on entry. Default `2.0f`. |
| `outroDuration` | `float` | Intro / Outro | YES | Seconds before `OnLevelComplete` after the last phase is cleared. Default `2.5f`. |
| `audioBank` | `BossAudioBankSO` | Audio | NO | Per-boss audio bank. May be null — `BossAudio` no-ops cleanly if absent. |
| `tutorial` | `BossTutorialSO` | Tutorial | NO | Optional upfront tutorial shown at level start before the encounter begins. `null` = no tutorial; `LevelFlowController` gates on this field. |

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
| `summonPhaseDuration` | `float` | Summoning Phase | Total phase length in seconds. No NEW summon acts may start after this elapses; an act already in progress always runs to completion. Default `30f`. Renamed from `summonDuration` (legacy assets migrate via `[FormerlySerializedAs]`). |
| `delayBetweenSummons` | `float` | Summoning Phase | Seconds BETWEEN summon acts. Boss movement (teleport / pace) fires during this gap. Default `5f`. Renamed from `summonInterval`. |
| `minionsPerSummonMin` | `int` | Summoning Phase | Min minions per summon act (inclusive). Default `2`. Renamed from `summonBurstMin`. |
| `minionsPerSummonMax` | `int` | Summoning Phase | Max minions per summon act (inclusive, `Random.Range(min, max+1)`). Default `3`. Renamed from `summonBurstMax`. |
| `delayBetweenMinions` | `float` | Summoning Phase | Seconds WITHIN a summon act between consecutive minion spawns (NEW). Total in-act duration ≈ `count × delayBetweenMinions`. Default `0.6f`. Set to `0` to disable stagger — discouraged. |
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

### 2.11 `BossAudioBankSO`

**Menu path:** `Salinlahi/Audio/Boss Audio Bank`
**File:** `Assets/Scripts/Data/BossAudioBankSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Audio/` (e.g. `BossAudioBank_ElInquisidor.asset`)

Holds all per-boss audio clip references and tuning fields for one boss encounter. Referenced by `BossConfigSO.audioBank` and consumed by `BossAudio` on the boss prefab. Designers create a new asset for each new boss to give it a distinct sonic identity without code changes.

| Field | Type | Header | Required | Notes |
|-------|------|--------|----------|-------|
| `bgm` | `AudioClip` | BGM | NO | Looping BGM played for the duration of the boss encounter. |
| `bgmVolume` | `float` | BGM | NO | `[0..1]` scale for this boss's BGM. Stacks multiplicatively on top of Master & BGM user sliders. Default `1f`. |
| `introGrowl` | `AudioClip` | One-Shots | NO | Plays once on `OnBossStarted`. |
| `introGrowlVolume` | `float` | One-Shots | NO | `[0..1]` per-clip volume scale. Default `1f`. |
| `summonTick` | `AudioClip` | One-Shots | NO | Plays each time the boss begins a summon tick (`OnBossSummonTick`). |
| `summonTickVolume` | `float` | One-Shots | NO | `[0..1]` per-clip volume scale. Default `1f`. |
| `bodyFall` | `AudioClip` | One-Shots | NO | Plays on `OnBossExhausted` (winding-down state). |
| `bodyFallVolume` | `float` | One-Shots | NO | `[0..1]` per-clip volume scale. Default `1f`. |
| `vulnerabilityExpiredLaugh` | `AudioClip` | One-Shots | NO | Plays on `OnBossVulnerabilityExpired` (player failed to break the boss). |
| `vulnerabilityExpiredLaughVolume` | `float` | One-Shots | NO | `[0..1]` per-clip volume scale. Default `1f`. |
| `defeat` | `AudioClip` | One-Shots | NO | Plays on `OnBossDefeated` (outro start). |
| `defeatVolume` | `float` | One-Shots | NO | `[0..1]` per-clip volume scale. Default `1f`. |
| `hitGrowls` | `AudioClip[]` | Variant Pools | NO | Short growls cycled on `OnBossDrawHit` (correct glyph during vulnerable window). No-immediate-repeat. |
| `hitGrowlsVolume` | `float` | Variant Pools | NO | `[0..1]` volume scale applied to every clip in the pool. Default `1f`. |
| `damagedGrowls` | `AudioClip[]` | Variant Pools | NO | Long growls cycled on `OnBossDamaged` (HP lost). No-immediate-repeat. |
| `damagedGrowlsVolume` | `float` | Variant Pools | NO | `[0..1]` volume scale applied to every clip in the pool. Default `1f`. |
| `footsteps` | `AudioClip[]` | Variant Pools | NO | Footstep variants played at `footstepInterval` during Pace-pattern phases. No-immediate-repeat. |
| `footstepsVolume` | `float` | Variant Pools | NO | `[0..1]` volume scale applied to every clip in the pool. Default `1f`. |
| `teleports` | `AudioClip[]` | Variant Pools | NO | Teleport variants played on `OnBossTeleport` (Teleport-pattern snap). No-immediate-repeat. |
| `teleportsVolume` | `float` | Variant Pools | NO | `[0..1]` volume scale applied to every clip in the pool. Default `1f`. |
| `footstepInterval` | `float` | Footstep Cadence | NO | Seconds between footstep SFX while in a Pace phase. Default `0.45f`. Min `0.05f`. |
| `bgmFadeInSeconds` | `float` | BGM Fade | NO | Seconds to fade BGM in on `OnBossStarted`. Default `1f`. |
| `bgmFadeOutSeconds` | `float` | BGM Fade | NO | Seconds to fade BGM out on `OnBossDefeated`. Default `1.5f`. |

**Null-tolerance:** All clip fields are optional. `BossAudio` silently skips any clip that is null, so partially-filled banks do not break gameplay. A new boss with a completely different sonic identity requires only a new `BossAudioBankSO` asset and a reference update on `BossConfigSO.audioBank` — no code change.

**Volume layering:** Per-clip `*Volume` fields and `bgmVolume` are designer-side balance knobs that stack multiplicatively on top of the player-facing master/BGM/SFX sliders managed by `AudioManager`. Setting any `*Volume` to `0` silences that category without breaking the rest of the bank.

[EVIDENCE: Assets/Scripts/Data/BossAudioBankSO.cs]
[EVIDENCE: Assets/ScriptableObjects/Audio/BossAudioBank_ElInquisidor.asset]

---

### 2.12 `EraConfigSO`

**Menu path:** `Salinlahi/Era Config`
**File:** `Assets/Scripts/Data/EraConfigSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Eras/`

Per-era visual + content bundle for the Level Select screen. `LevelSelectUI` holds a `List<EraConfigSO>` and iterates its `levels` list to populate the five fixed `LevelButton` scene instances.

| Field | Type | Header | Required | Notes |
|-------|------|--------|----------|-------|
| `eraName` | `string` | Identity | YES | Human-readable name for logs/debugging (e.g. `"Era One"`). |
| `backgroundSprite` | `Sprite` | Visuals | YES | Full-screen background sprite shown when this era is active. Assigned to `_eraBackgroundImage.sprite` by `LevelSelectUI.ShowEra`. |
| `bannerSprite` | `Sprite` | Visuals | YES | Baked-in banner sprite for this era (e.g. the ERA ONE scroll). Assigned to `_eraBannerImage.sprite` by `LevelSelectUI.ShowEra`. |
| `levels` | `List<LevelConfigSO>` | Levels | YES | Ordered list of levels in this era. Expected length matches `LevelSelectUI._levelButtons.Count` (5). Shorter lists cause the surplus `LevelButton` slots to be hidden (`SetActive(false)`). |

**Validation Rules:**
- `levels.Count` should equal `LevelSelectUI._levelButtons.Count` (currently 5). Shorter is handled; longer entries beyond slot count are silently ignored.
- All three visual fields (`eraName`, `backgroundSprite`, `bannerSprite`) must be non-null for the era to render correctly.

[EVIDENCE: Assets/Scripts/Data/EraConfigSO.cs]
[EVIDENCE: Assets/Scripts/UI/LevelSelectUI.cs]

---

### 2.13 `AlmanacEnemyRegistrySO`

**Menu path:** `Salinlahi/Almanac Enemy Registry`
**File:** `Assets/Scripts/Data/AlmanacEnemyRegistrySO.cs`
**Asset folder:** `Assets/ScriptableObjects/` (single project-wide instance)

Master list of all enemy entries surfaced in the Almanac. Holds a list of `AlmanacEnemyEntry` records. `OnValidate` automatically syncs each boss entry's `enemyData` field from `bossConfig.bossEnemyData`, so designer only needs to assign `bossConfig` — the enemy data reference is kept consistent by the Editor.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `entries` | `List<AlmanacEnemyEntry>` | YES | Ordered list of all enemy entries. Empty = Almanac Enemies tab shows nothing. |

[EVIDENCE: Assets/Scripts/Data/AlmanacEnemyRegistrySO.cs]

---

### 2.13.1 `AlmanacEnemyEntry`

**Type:** `[System.Serializable]` class (not a ScriptableObject — no separate asset file)
**File:** `Assets/Scripts/Data/AlmanacEnemyRegistrySO.cs` (same file as `AlmanacEnemyRegistrySO`)

One row in the Almanac enemy registry. Represents either a regular enemy or a boss.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `enemyData` | `EnemyDataSO` | YES (regular); auto-filled (boss) | The enemy's data asset. For boss entries, this field is auto-populated by `AlmanacEnemyRegistrySO.OnValidate` from `bossConfig.bossEnemyData`. |
| `bossConfig` | `BossConfigSO` | NO | Assign for boss entries. Leaving null marks the entry as a regular enemy. |

**Derived property:**

- `IsBoss` → `bossConfig != null`

**Methods:**

| Method | Returns | Behavior |
|--------|---------|----------|
| `ResolveDisplayName()` | `string` | Returns `bossConfig.bossName` for bosses; otherwise `enemyData.displayName` (falling back to `enemyData.enemyID` if empty). |
| `ResolveDescription()` | `string` | Returns `bossConfig.description` for bosses; otherwise `enemyData.description`. May return empty string. |
| `ResolvePortrait()` | `Sprite` | Returns `bossConfig.bossSprite` for bosses; otherwise `enemyData.portraitSprite`. Falls back to `enemyData.walkFrames[0]` when both portrait fields are null. |

[EVIDENCE: Assets/Scripts/Data/AlmanacEnemyRegistrySO.cs]

---

### 2.14 `BossTutorialSO` + `BossTutorialPage`

**Menu path:** `Salinlahi/Boss Tutorial`
**File:** `Assets/Scripts/Data/BossTutorialSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Enemies/Boss Configs/Boss Tutorials/` (recommended sibling folder)

Holds an ordered list of `BossTutorialPage` structs displayed to the player before a boss encounter begins. `LevelFlowController` gates on `BossConfigSO.tutorial != null`; pages are shown in `BossTutorialScroll` and closeable at any time via the red X.

#### `BossTutorialSO` fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `pages` | `List<BossTutorialPage>` | NO | Ordered pages. Empty list → `HasPages` is `false` → tutorial is skipped. Page 0 is conventionally the boss name + lore; subsequent pages cover mechanics. |

**Computed properties:**

| Property | Type | Behavior |
|----------|------|----------|
| `PageCount` | `int` | `pages?.Count ?? 0` |
| `HasPages` | `bool` | `PageCount > 0` |

#### `BossTutorialPage` struct fields (serializable)

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `title` | `string` | YES | Page heading shown in the scroll title label. Page 0 is the boss name; later pages are mechanic names (e.g. `"Summoning"`, `"Vulnerability"`). |
| `body` | `string` | YES | Lore (page 0) or mechanic explanation text. `[TextArea(2,6)]` in the Inspector. Empty hides the body GameObject. |
| `frames` | `Sprite[]` | NO | Sprite frames for the page art. Single frame = static; multiple frames = animated at `animationFps`. Empty/null hides the art `Image`. Use the boss's `walkFrames` from `EnemyDataSO` directly — no extra assets needed. |
| `animationFps` | `float` | NO | Frames per second for the art animation. `0` or negative = static (shows `frames[0]` only). Typical value: `8` (matching the boss’s walk animation). |
| `effect` | `BossTutorialArtEffect` | NO | Visual effect applied to the art, mimicking actual boss battle state visuals. Default `None`. |

**`BossTutorialArtEffect` enum:**

| Value | Visual | Matches |
|-------|--------|---------|
| `None` | No special effect — static or frame-animated art only. | Normal boss walk/idle. |
| `Panting` | Sinusoidal Y-bob (asymmetric, down-stroke 30% slower) + red tint lerp. | `BossStateVisuals.PantLoop` (WindingDown / exhausted state). |
| `Collapsed` | Y-scale squash to 85% + downward offset + half-amplitude bob + red tint. | `BossStateVisuals.PlayCollapse` + half-amplitude panting (Vulnerable state). |
| `Teleporting` | Scales down to 60% and teleports to random offsets on a timer. | Desperation / teleportation mechanics. |

**Design guidance:**
- A `BossTutorialSO` asset is shared across all plays of the same boss — page wording is not personalized.
- For art, use the boss’s existing `walkFrames` from `EnemyDataSO` — no new art assets are required. The `Panting` and `Collapsed` effects mimic the runtime boss visuals.
- Leave `frames` empty on any page that does not benefit from a visual; the art frame hides itself.
- Add/remove pages freely. The paging math (`BossTutorialPaging`) and arrow disable logic handle any count ≥ 1 automatically.
- If a future requirement adds "show once," add a `LevelTutorialProgress`-style PlayerPrefs gate in `LevelFlowController.PlayBossTutorialIfNeeded` — explicitly out of scope here (every-entry by design).

[EVIDENCE: Assets/Scripts/Data/BossTutorialSO.cs]
[EVIDENCE: Assets/Scripts/UI/Boss/BossTutorialPaging.cs]
[EVIDENCE: Assets/Scripts/UI/Boss/BossTutorialScroll.cs]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossTutorialController.cs]

---

## 3. Static Data Helpers

### 3.0 `CharacterUnlockProgress`

**File:** `Assets/Scripts/Core/CharacterUnlockProgress.cs`
**Type:** `static` class (no MonoBehaviour, no Singleton)
**Persistence:** Unity `PlayerPrefs` — key `salinlahi.almanac.character_ids`

Stores the set of unlocked `BaybayinCharacterSO` character IDs as a pipe-separated (`|`) string in PlayerPrefs. All IDs are normalized via `Trim().ToLowerInvariant()` before storage and lookup, so case differences do not produce duplicate entries.

**Public API:**

| Method | Signature | Behavior |
|--------|-----------|----------|
| `HasUnlocked` | `bool HasUnlocked(BaybayinCharacterSO data)` | Returns `true` if `data.characterID` (normalized) is in the persisted set. Returns `false` if `data` is null. |
| `TryMarkUnlocked` | `bool TryMarkUnlocked(BaybayinCharacterSO data, out string normalizedID)` | If the normalized ID is not already in the set, adds it, persists via `PlayerPrefs.Save()`, and returns `true`. Returns `false` (already unlocked or null input). |
| `ClearAllUnlocked` | `void ClearAllUnlocked()` | Deletes the PlayerPrefs key and clears the in-memory cache. Called by `ProgressManager.ClearAllProgress()`. |

**Design notes:**
- This class is **pure** — it raises no EventBus events. The caller (e.g., a wave-clear handler) is responsible for raising `EventBus.RaiseCharacterUnlocked(character)` after `TryMarkUnlocked` returns `true`.
- `ProgressManager.ClearAllProgress()` calls `ClearAllUnlocked()` so that a full progress reset also wipes the Almanac character unlock state.

[EVIDENCE: Assets/Scripts/Core/CharacterUnlockProgress.cs]
[EVIDENCE: Assets/Scripts/Core/ProgressManager.cs — ClearAllProgress()]

---

## 4. Asset Authoring Guidelines

### 4.1 Naming Convention

| Asset Type | Pattern | Example |
|------------|---------|---------|
| `BaybayinCharacterSO` | `Char_[ID]` | `Char_BA`, `Char_KA` |
| `EnemyDataSO` | `Enemy_[type]` | `Enemy_Standard`, `Enemy_Fast` |
| `LevelConfigSO` | `Level_[number]` | `Level_01`, `Level_10` |
| `RecognitionConfigSO` | `RecognitionConfig` | (singleton asset) |
| `EraConfigSO` | `Era_[number]` | `Era_01`, `Era_02` |

### 4.2 Asset Folder Map

| Asset Type | Folder |
|------------|--------|
| `BaybayinCharacterSO` | `Assets/ScriptableObjects/Characters/` |
| `LevelConfigSO` | `Assets/ScriptableObjects/Levels/` |
| `EnemyDataSO` | `Assets/ScriptableObjects/` |
| `BossConfigSO` | `Assets/ScriptableObjects/` (e.g. `BossConfig_ElInquisidor.asset`, alongside other top-level configs) |
| `BossAudioBankSO` | `Assets/ScriptableObjects/Audio/` (e.g. `BossAudioBank_ElInquisidor.asset`) |
| `EraConfigSO` | `Assets/ScriptableObjects/Eras/` |
| Templates (text files) | `Assets/Resources/Templates/` |

[EVIDENCE: Assets/ScriptableObjects/ directory listing — Characters/, Levels/, Waves/ subdirs confirmed]
[EVIDENCE: docs/capstone/TDD.md, §7.4 Folder Structure]

### 4.3 Template File Format

Each `BaybayinCharacterSO.templateFileName` references a plain-text coordinate file in `Assets/Resources/Templates/`. Format is determined by the `TemplateLoader.cs` implementation. Expected content per `Salinlahi.md §3.3.3`: comma-separated 2D point coordinates representing the resampled $P point cloud for that character.

Authoring rule: Template files must be validated against `RecognitionConfigSO.resamplePointCount` (default 32 points). A template with a different point count will cause a recognition error.
