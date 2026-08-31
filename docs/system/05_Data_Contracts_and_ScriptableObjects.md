# 05 — Data Contracts and ScriptableObjects
**Project:** Salinlahi
**Version:** 3.0
**Date:** 2026-08-31
**Owner:** Chad Andrada (Product Owner / Designer)

---

## 1. Design Principle

All game content is defined in ScriptableObject assets. Level designers can create new levels, adjust enemy speeds, change wave compositions, and tune difficulty entirely through the Unity Inspector **without writing code or recompiling**. This separation of content from logic is a non-negotiable architectural constraint.

[EVIDENCE: docs/capstone/TDD.md, §5 Data Layer]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.5.1 — "data assets rather than in code"]

---

## 1.1 Revised campaign frozen core (SALIN-170)

`CampaignConfigSO` is the authoritative root for the revised campaign. It owns one
`CampaignIdentityManifest`, one `CampaignTuning`, the ordered canonical symbol catalog,
and the ordered era references. Its identity envelope is fixed at
`campaign.revised-v1`, content schema `1`, save schema `1`, source schemas `0` and `1`,
and source workbook SHA-256
`33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83`.
`CampaignTuning.defaultShrineHearts` defaults to `3`.

The canonical campaign contains `era.ugat`, `era.ugnayan`, and `era.pamana`, with five
levels per era. Level identity is stable (`level.<era>.<local-order>`); `levelNumber`
remains global presentation order and is not revised campaign identity. A level owns
exactly two inline `FocusWordDefinition` records, ordered decomposition references,
cumulative symbol pool data, learning/practice/mastery requirements, clue and defense
rules, context media, rewards, and a final restoration value.

`BaybayinCharacterSO.stableId` identifies one visual symbol. Contextual spoken values
are stored in `spokenValues` and referenced by `SymbolValueReference`; `symbol.dara`
therefore carries both `value.da` and `value.ra` rather than becoming two learned
visual symbols. Stable lookup methods fail when an ID is unknown or duplicated and do
not fall back to display text, filenames, list position, or legacy aliases.

Each canonical symbol declares a canonical `firstIntroductionLevelId`. The validator
derives every level's expected cumulative pool from that metadata instead of symbol-list
position, then requires focus decompositions and learning/practice/mastery requirements
to reference symbols already present in the current pool. A `SymbolValueReference` must
point to the canonical `BaybayinCharacterSO` configured in the campaign catalog; a
separate asset with the same stable ID is an orphan reference and remains invalid. In
the finale's ordered learning requirements, the first PA entry must be instruction
before PA appears in later learning entries or practice, mastery, and focus content.

`CampaignConfigValidator` is a pure, non-mutating traversal that reports structured
issue codes and canonical paths. `Assets/Editor/CampaignConfigValidationMenu.cs` is
the editor-only adapter and does not repair or save assets. SALIN-170 exposes
compatibility metadata but performs no save I/O or migration; save conversion remains
the SALIN-171 boundary. The complete production three-era/15-level asset set remains
SALIN-172 authoring work.

For a revised level, `challengePrototypeEnabled` is the authoring-validation gate. When
false, `challengeSequence` may be null or may contain dormant staged authoring and
`CampaignConfigValidator` does not inspect it. When true, `challengeSequence` is required
and must pass `ChallengeSequenceValidator`. Missing and invalid sequences fail with
`CHALLENGE_SEQUENCE_MISSING` and `CHALLENGE_SEQUENCE_INVALID` at the canonical level
path. SALIN-168 remains the owner of internal sequence rules and runtime behavior.

## 2. ScriptableObject Schemas

### 2.1 `BaybayinCharacterSO`

**Menu path:** `Salinlahi/Baybayin Character`
**File:** `Assets/Scripts/Data/BaybayinCharacterSO.cs`
**Asset folder:** `Assets/ScriptableObjects/Characters/`

| Field | Type | Header | Required | Invariants |
|-------|------|--------|----------|------------|
| `characterID` | `string` | Identity | YES | Must match template filename prefix. Example: `"BA"` → template file `BA_template.txt` in `Assets/Resources/Templates/`. Case-sensitive. |
| `syllable` | `string` | Identity | YES | Lowercase Filipino syllable shown to player. Example: `"ba"`, `"ka"`, `"ga"`. Must not be empty. |
| `displaySprite` | `Sprite` | Visuals | YES | **Learning CARD, not a bare glyph** — `Resources/[ID].png` is a filled panel carrying the glyph *and its romanised syllable* (`BA-VA.png` reads "ba, va"). Consumed by the Tracing Dojo character list and `SymbolLearningCardController`. **Not** what appears on enemies; that is `badgeSprite`. |
| `almanacSprite` | `Sprite` | Visuals | NO | Stylized glyph shown in the Almanac character grid and detail view (`Art/UI/Almanac/[ID]-Almanac.png`). Falls back to `displaySprite` when null. |
| `badgeSprite` | `Sprite` | Visuals | NO | Scroll-framed glyph plate (`Art/UI/GlyphBadges/[ID].png`) used by `EnemyGlyphBadge` during gameplay. This — not `displaySprite` — is what appears on enemies. Carries no romanisation. |
| `scrambledBadgeSprite` | `Sprite` | Visuals | NO | Optional framed + glitched variant when a visual override is active (e.g. Kempei scramble). Falls back to `badgeSprite` when null. |
| `glyphOutlineSprite` | `Sprite` | Visuals | NO | **Bare glyph on a transparent background** (`Art/UI/GlyphOutlines/[ID].png`) — the only sprite here with no card, frame or romanisation. Generated from the recognition templates by `GlyphOutlineGenerator`, so it is exactly the shape `DollarPRecognizer` scores against. Used as the Tracing Dojo guide (`GhostStrokeRenderer`) and the gameplay trace hint (`TraceHintPresenter`). White, so consumers tint and fade it. Falls back to `displaySprite` / `badgeSprite` respectively when null. |
| `pronunciationClip` | `AudioClip` | Audio | YES | Played on every successful character recognition via `AudioManager`. Duration must be under 1 second to prevent overlap. Null triggers a silent defeat (no audio error). |
| `templateFileName` | `string` | Recognition | YES | Filename in `Assets/Resources/Templates/` without extension. Example: `"BA_template"`. Must match a file loadable via `Resources.Load<TextAsset>`. |
| `description` | `string` | Almanac | NO | Short player-facing description of the character shown in the Almanac detail panel. May be empty; Almanac UI falls back to an empty string gracefully. |

**Validation Rules:**
- `characterID` must be unique across all `BaybayinCharacterSO` assets in the project.
- `templateFileName` must reference a file that exists in `Assets/Resources/Templates/`.
- `pronunciationClip` must be assigned before Sprint 2 UAT.
- **18** total assets must exist at content-complete milestone: 15 consonants + 3 vowels. (Was stated as 17 "one per consonant"; corrected under the REQ-42 ruling that `RA` is its own glyph. All 18 exist today.)

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
| `levelName` | `string` | Identity | YES | Human-readable display name. Generic levels use `"Level N"`; authored encounters may use a title. |
| `levelNumber` | `int` | Identity | YES | 1-indexed. Story Mode range: 1–15. Must be globally unique. |
| `chapterNumber` | `int` | Identity | YES | Default `1`. Author-facing label for HUD/level-select grouping. |
| `chapterName` | `string` | Identity | YES | Default `"Ugat"`. Approved active campaign label for HUD/level-select grouping. |
| `eraTheme` | `EraThemeSO` | Identity | NO | Visual theme for this level's era (background, ground, shrine, decorations). Consumed by `EnvironmentThemeSwapper`. |
| `numberSprite` | `Sprite` | Identity | NO | Baked-in numbered scroll sprite displayed on this level's Level Select button. Null triggers a warning log from LevelButton; the scroll image is unchanged. |
| `waves` | `List<WaveDefinition>` | Waves | YES (non-boss) | Ordered list of embedded waves. Each `WaveDefinition`'s character/enemy subset is kept ⊆ the level rosters by `ReconcileWavesToRoster()`. Ignored when `bossConfig != null`. |
| `allowedCharacters` | `List<BaybayinCharacterSO>` | Characters | YES | Master allowed-character list for this level. All `WaveDefinition.characters` entries must be a subset of this list. |
| `allowedEnemyTypes` | `List<EnemyDataSO>` | Characters | YES | Master enemy-type roster for this level. All `WaveDefinition.enemyTypes` entries must be a subset of this list. |
| `bossConfig` | `BossConfigSO` | Boss | NO | If assigned, this level is a boss encounter. The level is treated as a boss level whenever this reference is non-null. |
| `challengePrototypeEnabled` | `bool` | Challenge | NO | Enables SALIN-168 challenge-sequence authoring validation for this revised level. Default `false`; does not require challenge content when disabled. |
| `challengeSequence` | `ChallengeSequenceSO` | Challenge | NO | Optional challenge sequence inspected only when `challengePrototypeEnabled` is `true`; dormant assigned content is allowed when disabled. |
| `isAvailableInLite` | `bool` | Build Flags | YES | `true` for levels 1–3 (Salinlahi Lite). `false` for levels 4–15 (Full only). Default `true`. |

**Validation Rules:**
- Levels 1–3: `isAvailableInLite = true`.
- Levels 4–15: `isAvailableInLite = false`.
- When `bossConfig != null`, the level runs the boss encounter and the `waves` list is ignored (per the LevelConfigSO inspector tooltip and `WaveManager.RunAllWavesRoutine`).
- For non-boss levels, the `waves` list must not be empty. An empty wave list causes immediate level-complete with no gameplay.
- `chapterNumber` and `chapterName` are author-facing labels for HUD/level-select grouping.
- When `challengePrototypeEnabled` is `false`, `challengeSequence` is ignored and may be null or dormant staged authoring. When enabled, the reference is required and must pass `ChallengeSequenceValidator`; campaign issues use `CHALLENGE_SEQUENCE_MISSING` or `CHALLENGE_SEQUENCE_INVALID`.

[EVIDENCE: Assets/Scripts/Data/LevelConfigSO.cs]
[EVIDENCE: docs/capstone/TDD.md, §5 Data Layer — LevelConfigSO row]
[EVIDENCE: docs/capstone/Salinlahi.md, §3.4 Business Model — Lite = levels 1–3]

---

### 2.3.1 `FocusWordDefinition.meaning`

Each inline focus word carries a required `meaning` — the approved plain-language meaning of the
whole word. The word `Meaning` mastery dimension matches on this field, so it is unimplementable
without it. A blank or whitespace-only value raises `FOCUS_MEANING_MISSING`. Copy is authored by
SALIN-172 against the SALIN-167/SALIN-188 matrix.

[EVIDENCE: Assets/Scripts/Data/Campaign/FocusWordDefinition.cs]
[EVIDENCE: Assets/Scripts/Data/Validation/CampaignConfigValidator.cs, ValidateFocusWords]

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

**Not every recognition tuning lever lives here.** These are hardcoded constants in `DollarPRecognizer.cs`, deliberately not exposed on the SO, and changing them affects every character at once:

| Constant | Value | Effect |
|----------|-------|--------|
| `CLEAR_WIN_GAP` | `0.08f` | Margin by which the shape-score leader must beat the runner-up to be returned without semantic penalties. |
| `DISAMBIGUATION_TOP_K` | `3` | How many close candidates get re-ranked by the composite (stroke-count + aspect) score. |
| `ONE_D_ASPECT_THRESHOLD` | `4.5f` | Bounding-box aspect at or above which `ScaleToSquare` scales **uniformly** instead of per-axis, preserving the aspect of essentially one-dimensional characters (HA). Lowering it toward 1 approaches always-uniform scaling, which was measured to be worse overall — it regresses RA toward KA. |

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
| `eraName` | `string` | Identity | YES | Human-readable active campaign name for logs/debugging (e.g. `"Ugat"`). |
| `backgroundSprite` | `Sprite` | Visuals | YES | Full-screen background sprite shown when this era is active. Assigned to `_eraBackgroundImage.sprite` by `LevelSelectUI.ShowEra`. |
| `bannerSprite` | `Sprite` | Visuals | YES | Baked-in campaign banner sprite assigned to `_eraBannerImage.sprite` by `LevelSelectUI.ShowEra`. |
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

### 2.15 `LearningTuningSO`

**Menu path:** `Salinlahi/Learning Tuning`
**File:** `Assets/Scripts/Data/Learning/LearningTuningSO.cs`
**Asset folder:** `Assets/ScriptableObjects/`

Referenced from `CampaignConfigSO.learningTuning`. Required on the revised path — a campaign with no
tuning asset fails validation with `LEARNING_TUNING_MISSING`. Hanging tuning off the campaign root
makes it reachable everywhere the campaign already is, without widening a constructor.

| Field | Type | Header | Required | Invariants |
|-------|------|--------|----------|------------|
| `immediateSuccessesForPracticed` | `int` | Mastery thresholds | YES | `[Min(1)]`, default `2`. Immediate successes in one dimension required to reach Practiced. |
| `delayedSuccessesForRecalled` | `int` | Mastery thresholds | YES | `[Min(1)]`, default `1`. Delayed retrieval successes required to reach Recalled. |
| `delayedSuccessesForMastered` | `int` | Mastery thresholds | YES | `[Min(1)]`, default `2`. Delayed retrieval successes required to reach Mastered. |
| `delayedSessionsForMastered` | `int` | Mastery thresholds | YES | `[Min(1)]`, default `2`. Distinct committed sessions carrying delayed successes. Sessions rather than levels, so finale content stays reachable. |
| `nextLevelOffset` | `int` | Review offsets | YES | `[Min(1)]`, default `1`. |
| `laterLevelOffset` | `int` | Review offsets | YES | `[Min(1)]`, default `3`. |
| `accuracyWeight` | `float` | Priority weights | YES | Default `1`. Weight on `(1 - accuracy)`. |
| `stateGapWeight` | `float` | Priority weights | YES | Default `1`. Weight on distance below Mastered. |
| `overdueWeight` | `float` | Priority weights | YES | Default `2`. Weight on overdue review checkpoint count. |

[EVIDENCE: Assets/Scripts/Data/Learning/LearningTuningSO.cs]
[EVIDENCE: Assets/Scripts/Data/Validation/CampaignConfigValidator.cs, ValidateLearningTuning]

---

### 2.16 Mastery evidence records (SALIN-175)

**File:** `Assets/Scripts/Data/Learning/LearningEvidence.cs`

Serializable records persisted inside `CampaignProgressData` and inside `CampaignProgressOutcome`.

- `MasteryDimension` — `Form`, `Sound`, `Assembly`, `Meaning`.
- `MasteryState` — `None`, `Introduced`, `Practiced`, `Recalled`, `Mastered`. `None = 0`.
- `LearningContentKind` — `Symbol`, `Word`. `MasteryDimensions.For(kind)` gives the applicable
  dimensions: symbols carry three (no `Meaning`), words carry all four.
- `LearningSessionKind` — `LevelAttempt`, `FreePractice`, `ScheduledReview`. `LevelAttempt` is `0`
  so a record written before this field existed deserializes with correct semantics. The bare value
  `Practice` is deliberately avoided because `ContentRequirementKind.Practice` already exists on a
  different axis.
- `DimensionEvidence` — per-dimension counters (`immediateSuccesses`/`immediateAttempts`,
  `delayedSuccesses`/`delayedAttempts`, `delayedSessionCount`), plus `highWaterState` and
  `lastEvidenceLevelId`. Mastery never regresses: `highWaterState` is a high-water mark.
- `SymbolMasteryRecord` / `WordMasteryRecord` — one record per content ID, each holding its
  `DimensionEvidence` list. Word records also carry `satisfiedReviewCheckpoints`.
- `LearningEvidenceEntry` — a **session summary for one `(contentId, dimension)` pair**, not one
  entry per attempt. Invariant: `0 <= retrievalSuccessCount <= successCount <= attemptCount`. Counts
  and a per-attempt `answerWasVisible` boolean cannot coexist, because one entry folding three
  attempts cannot say which of them showed the answer; `LearningEvidenceRecorder` performs that fold.
- `LearningEvidenceBatch` — `levelId`, `sessionKind`, `instructedContentIds`, `entries`.

**Rule: evidence alone never creates a record.** Only `instructedContentIds` may introduce content,
and instruction seeds every applicable dimension at `Introduced` without recording an attempt.
Otherwise practice on never-taught content would silently self-introduce it and corrupt
`IntroducedSymbolIds`.

[EVIDENCE: Assets/Scripts/Data/Learning/LearningEvidence.cs]
[EVIDENCE: Assets/Scripts/Data/Learning/LearningProgressWriter.cs]

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

Template files live in `Assets/Resources/Templates/` and are named `[ID]_template_[NN].txt` — **each character has multiple numbered variants**, not a single file (121 files across 18 characters as of 2026-08-31).

**Format**, per `TemplateLoader.cs` → `StrokeTextParser.ParseStrokes`:

- **One point per line**, as `x, y`. It is *not* one comma-separated list of all points.
- Coordinates are **normalized floats**, not pixels — e.g. `0.1353, 0.0590`.
- **A blank line separates strokes.** A file with no blank lines is a single-stroke character.
- Empty/whitespace lines at the end are harmless; a file that parses to zero points logs `TemplateLoader: Template '<id>' had no valid points after parsing strokes.`

**Authoring rules** (corrected — the previous version of this section was wrong):

- ~~Templates must contain exactly `resamplePointCount` (32) points.~~ **False, and actively misleading.** Raw templates vary widely in point count — measured on `dev`: HA 251, A 537, RA 586, BA 670. `DollarPRecognizer.Normalize` calls `ResampleStrokes(strokes, _n)` to bring every cloud to `resamplePointCount` before scoring. **Author at whatever density the capture produces; do not hand-decimate to 32.**
- **Stroke count is semantically meaningful.** It feeds the stroke-count mismatch penalty in top-K disambiguation, so blank-line placement must reflect how the character is genuinely drawn.
- **Aspect ratio is meaningful and must be preserved.** `ScaleToSquare` scales uniformly for clouds whose bounding box exceeds `ONE_D_ASPECT_THRESHOLD` (4.5), which is what keeps a near-one-dimensional character such as HA distinguishable. A template captured with a distorted aspect will not normalize back to the right shape.
- **Y is up.** Templates are authored in a y-up frame; feeding y-down screen coordinates in directly produces a vertically mirrored cloud that scores poorly.
- Adding more variants is the **first and cheapest** lever for a character that recognizes poorly. Prefer it to changing `minimumConfidence`, which is global and needs UAT re-validation.

[EVIDENCE: Assets/Scripts/Gameplay/Recognition/TemplateLoader.cs — "Each non-empty line is an x,y point and blank lines separate strokes"]
[EVIDENCE: Assets/Scripts/Gameplay/Recognition/DollarPRecognizer.cs — `ResampleStrokes`, `ScaleToSquare`]

### 4.4 CampaignSaveDocument (schema v3)

Revised progression is stored as a versioned CampaignSaveDocument serialized with Unity JsonUtility.
Schema v3 contains campaign identity, content/save schema versions, a monotonic revision, committed
transaction metadata, migration/recovery receipts, a lowercase SHA-256 integrity field, stable-ID
progress records, `progress.journeyGenerationId`, and the lifetime
`progress.appliedOutcomeReceipts` ledger. A receipt stores canonical outcome ID, level ID, and UTC
application time. The clean journey starts at level.ugat.01; v1 saves migrate atomically and receive
a new generation plus an empty receipt ledger.

Schema v3 (SALIN-175) adds `progress.symbolMastery` and `progress.wordMastery`, and adds
`sessionKind` to `AppliedOutcomeReceipt`. Both mastery lists are null-defended by
`CampaignSaveSerializer.Normalize` and required non-null by `CampaignSaveValidator`. Records are
sorted by content ID on every write: the journal's `SameOutcome` compares serialized JSON, so
unstable ordering would make an identical replay look like a different outcome.

`CampaignSaveMigrator.TryUpgradeToCurrent` (renamed from `TryUpgradeV1`) is now a step chain — 1→2
fills the journey generation and empties the receipt ledger, 2→3 moves the version — guarded by a
range check rather than an equality check, so adding schema 4 later needs one more step block and no
guard edit. Every save the shipped build has written is a v2 save, and it upgrades rather than being
discarded.

`LevelAttempt` receipts are the durable idempotency record and are kept for the lifetime of the
journey. Practice and review receipts are bounded at 32, because the journal only ever holds one
pending outcome so the deduplication window is one outcome deep. The receipt just written is never a
pruning candidate — evicting it would fail the coordinator's `HasReceipt` check and wedge the
journal.

The campaign file roles are fixed below Application.persistentDataPath: campaign-save.json
(published primary), campaign-save.tmp (flushed candidate), campaign-save.bak (validated prior
primary), and legacy-progress-v0.json (immutable typed archive). Integrity is computed over a clone
with an empty checksum, encoded as UTF-8, and formatted as lowercase hexadecimal. A higher save
schema is a blocking condition and is never reset or overwritten.

The optional campaign root is the activation gate. Null retains legacy compatibility; an assigned
root with validation errors blocks revised progress. Once revised mode is active, campaign consumers
use CampaignProgressRepository and CampaignOutcomeCoordinator and do not dual-write PlayerPrefs.
Audio preferences remain outside this document and continue to use their existing PlayerPrefs keys.

### 4.5 CampaignProgressOutcome and outcome journal (SALIN-174, SALIN-175)

`CampaignProgressOutcome` is the immutable session-end payload: outcome schema, outcome ID, journey
generation, campaign/content identity, level ID, stars, unlocked symbol IDs, unlocked memory IDs,
claimed reward IDs, UTC completion time, and — at outcome schema 2 — `sessionKind` and an
`evidence` batch. The journal wrapper uses schema 1 and the file format
`salinlahi-campaign-outcome-journal`.

Outcome schema 2 accepts a range (`MinimumOutcomeSchemaVersion`..`CurrentOutcomeSchemaVersion`)
rather than an exact match. `CampaignOutcomeValidator.UpgradeToCurrent` stamps a v1 outcome loaded
from a journal written by an older build; without it the version check would silently discard an
in-flight level completion on upgrade. The upgrade runs at the journal's single parse boundary
(`ReadCandidate`), so all five `SameOutcome` comparison sites see a v2 outcome.

Validation is session-kind aware. A `LevelAttempt` requires 1–3 stars. Any other kind must carry
zero stars and empty unlock/reward lists — **practice is structurally unable to alter level
completion**. Evidence entries are rejected when the dimension does not apply to the content kind,
when a `(contentId, dimension)` pair repeats, when the count invariant is violated, when the content
is unknown to the campaign, or when a symbol is neither already unlocked nor instructed in the same
batch.

The two journal roles are `campaign-outcome.pending.tmp` and
`campaign-outcome.pending.json`. The temporary file is flushed and read back before promotion; the
published file is read back again before the coordinator applies it. A lowercase SHA-256 checksum
covers the UTF-8 JSON with `integritySha256` empty. Unknown higher journal schemas remain in place
and block startup. Valid pending outcomes replay monotonically, exact receipt duplicates return
`AlreadyCommitted`, and journal files are cleared only after the campaign publication is verified.
Reset creates a new generation and quarantines any stale-generation pending outcome.
