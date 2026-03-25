# 07 — Content Pipeline
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Chad Andrada (Product Owner / Designer)

---

## 1. Baybayin Characters

### 1.1 Character Set Scope

The game covers **17 Baybayin characters: 14 consonants (BA, KA, DA, GA, HA, LA, MA, NA, NGA, PA, SA, TA, WA, YA) and 3 vowels (A, E/I, O/U)**. Diacritical marks (kudlit) are explicitly out of MVP scope (Should Ship, may be deferred post-launch).

[EVIDENCE: docs/capstone/Salinlahi.md, §1.5.1 Scope — "17 Baybayin consonant characters"; §3.3.3]
[EVIDENCE: Assets/Scripts/Data/RecognitionConfigSO.cs — context implies 17 templates]

### 1.2 Each character requires these assets

| Asset | File Type | Location | Required By |
|-------|-----------|----------|-------------|
| `BaybayinCharacterSO` asset | `.asset` | `Assets/ScriptableObjects/Characters/Char_[ID].asset` | All gameplay systems |
| Display sprite (glyph) | `.png` | `Assets/Art/UI/` or `Assets/Art/Characters/` | Enemy renderer; Tracing Dojo |
| Pronunciation audio clip | `.wav` / `.mp3` | `Assets/Audio/` | AudioManager |
| Recognition template file | `.txt` (point coordinates) | `Assets/Resources/Templates/[ID]_template.txt` | DollarPRecognizer (PLANNED) |

### 1.3 Current Status

As of Sprint 1: **placeholder assets only**. No BaybayinCharacterSO assets have been confirmed in `Assets/ScriptableObjects/Characters/`. No template `.txt` files have been confirmed in `Assets/Resources/Templates/`.

[EVIDENCE: Assets/Art/Characters/ — placeholder sprites only confirmed]
[EVIDENCE: Assets/ScriptableObjects/Characters/ — folder exists, asset contents unverified]

---

## 2. Enemy Content

### 2.1 Enemy Types

| Enemy Type | `enemyID` | Era | Tier | First Appears | Priority | Prefab | Status |
|------------|-----------|-----|------|--------------|----------|--------|--------|
| Soldado | `"soldado"` | Spanish | Regular (32×32) | Level 1 | Must Ship | `Enemy_Standard.prefab` | Prefab exists; SO unverified |
| Fraile | `"fraile"` | Spanish | Variant (32×32) | Level 2 | Must Ship | (PLANNED) | NOT FOUND |
| Guardia | `"guardia"` | Spanish | Variant (32×32) | Level 3 | Must Ship | (PLANNED) | NOT FOUND |
| Capitan | `"capitan"` | Spanish | Elite (48×48) | Level 4 | Must Ship | (PLANNED) | NOT FOUND |
| Soldier | `"soldier"` | American | Regular (32×32) | Level 6 | Must Ship | (PLANNED) | NOT FOUND |
| Maestro | `"maestro"` | American | Variant (32×32) | Level 7 | Should Ship | (PLANNED) | NOT FOUND |
| Pensionado | `"pensionado"` | American | Variant (32×32) | Level 8 | Should Ship | (PLANNED) | NOT FOUND |
| General | `"general"` | American | Elite (48×48) | Level 9 | Should Ship | (PLANNED) | NOT FOUND |
| Heitai | `"heitai"` | Japanese | Regular (32×32) | Level 11 | Must Ship | (PLANNED) | NOT FOUND |
| Kisha | `"kisha"` | Japanese | Variant (32×32) | Level 12 | Should Ship | (PLANNED) | NOT FOUND |
| Kempei | `"kempei"` | Japanese | Variant (32×32) | Level 13 | Should Ship | (PLANNED) | NOT FOUND |
| Shokan | `"shokan"` | Japanese | Elite (48×48) | Level 14 | Should Ship | (PLANNED) | NOT FOUND |

[EVIDENCE: Assets/Prefabs/Enemies/[Enemy] Standard.prefab — confirmed]
[EVIDENCE: docs/capstone/GDD.md, §4.3 Enemies — full roster with priority]
[EVIDENCE: Team README §9 — Enemy Type Roster with introduction levels]

### 2.2 Walk Frame Animation Convention

- `walkFrames` field on `EnemyDataSO` is a `Sprite[]`.
- Sprite index 0 is the default/static frame. The animator overrides sprite at runtime if `animatorController` is set.
- Placeholder file: `Assets/Art/Characters/Enemies/placeholder_enemy_standard.png`

[EVIDENCE: Assets/Scripts/Data/EnemyDataSO.cs — walkFrames, animatorController]
[EVIDENCE: Assets/Art/Characters/Enemies/placeholder_enemy_standard.png — confirmed]

### 2.3 Sprite Size Specifications

| Entity Type | Size | PPU | Examples |
|-------------|------|-----|----------|
| Regular enemies | 32×32 px | 32 | Soldado, Soldier, Heitai |
| Variant enemies | 32×32 px | 32 | Fraile, Guardia, Maestro, Pensionado, Kisha, Kempei |
| Elite enemies | 48×48 px | 32 | Capitan, General, Shokan |
| Bosses | 64×64 px | 32 | El Inquisidor, The Superintendent, Kadiliman |
| Protagonist | 32×32 px | 32 | Kuya, Laban, Manong |
| Shrines | 64×96 px | 32 | Baybayin Altar (Spanish), Ancestral Door (American), Scroll Shrine (Japanese) |
| Dialogue portraits | 96×96 px | 32 | All speaking characters |

[EVIDENCE: docs/capstone/GDD.md, §4.2 Characters; §4.3 Enemies]
[EVIDENCE: Team README §6 — Technical Specifications for pixel artist]

---

## 3. Levels and Waves

### 3.1 Level Structure

| Chapter | Name | Levels | Era | Gameplay Theme | Boss Level |
|---------|------|--------|-----|---------------|------------|
| Chapter 1 | Liwanag (Light) | 1–5 | Spanish Colonization | Drawing mastery | Level 5 |
| Chapter 2 | Paglaban (Resistance) | 6–10 | American Occupation | Tactical thinking | Level 10 |
| Chapter 3 | Pagbalik (Reclamation) | 11–15 | Japanese Occupation | Mastery and chaos | Level 15 |

[EVIDENCE: docs/capstone/GDD.md, §4.1 Levels/Maps — chapter names and historical eras]
[EVIDENCE: Team README §9 — chapter gameplay themes]

### 3.2 Level Asset Naming

| Asset | Pattern | Example |
|-------|---------|---------|
| `LevelConfigSO` | `Level_[##]` | `Level_01.asset`, `Level_10.asset` |
| `WaveConfigSO` | `L[level]_W[wave]` | `L1_W1.asset`, `L3_W2.asset` |

**Current status:** `Assets/ScriptableObjects/Levels/` and `Assets/ScriptableObjects/Waves/` folders exist. Asset population is NOT verified beyond folder existence.

[EVIDENCE: Assets/ScriptableObjects/Levels/ and Waves/ — folders confirmed]

### 3.3 Build Flag

`LevelConfigSO.isAvailableInLite`:
- `true` → accessible in both Salinlahi Lite and Salinlahi Full (levels 1–3).
- `false` → accessible in Salinlahi Full only (levels 4–15).

[EVIDENCE: Assets/Scripts/Data/LevelConfigSO.cs — isAvailableInLite field]
[EVIDENCE: docs/capstone/TDD.md, §7.2 Lite/Full Build Split]

---

## 4. Recognition Templates

### 4.1 Location

`Assets/Resources/Templates/`

Templates are loaded via `Resources.Load<TextAsset>` at startup by `TemplateLoader.cs` (PLANNED). Each file represents one Baybayin character's point-cloud template.

### 4.2 File Naming

```
[characterID]_template.txt
```

Example: `BA_template.txt`, `KA_template.txt`

The `characterID` must match `BaybayinCharacterSO.characterID` exactly (case-sensitive).

### 4.3 File Content Format

Plain text coordinate pairs. Format determined by `TemplateLoader.cs` implementation (NOT FOUND). Expected format based on $P algorithm:
```
x1,y1
x2,y2
...
x32,y32
```
(32 points per `RecognitionConfigSO.resamplePointCount` default)

### 4.4 Current Status

`Assets/Resources/Templates/` folder confirmed. No `.txt` files verified inside it.

[EVIDENCE: docs/capstone/Salinlahi.md, §3.3.3 — "Templates stored as plain text coordinate files"]
[EVIDENCE: docs/capstone/TDD.md, §7.4 — Assets/Resources/Templates/]

---

## 5. Art Assets

### 5.1 Folder Map

| Folder | Contents |
|--------|----------|
| `Assets/Art/Characters/Enemies/` | Enemy sprites (placeholder: `placeholder_enemy_standard.png`) |
| `Assets/Art/Characters/Player/` | Protagonist sprites: Kuya (Spanish era), Laban (Japanese era), Manong (American era) at 32×32. Idle, draw gesture, victory, collapse animations. |
| `Assets/Art/Environment/Background/` | Scene backgrounds |
| `Assets/Art/Environment/` | `placeholder_shrine.png` — PlayerBase visual |
| `Assets/Art/Environment/Tileset/` | Tile art |
| `Assets/Art/FX/` | Visual effects |
| `Assets/Art/UI/Buttons/` | Button sprites |
| `Assets/Art/UI/Fonts/` | Font assets |
| `Assets/Art/UI/Frames/` | UI frame/border assets |
| `Assets/Art/UI/Icons/` | Icon sprites |
| `Assets/Animations/Enemy/` | Enemy animation clips |
| `Assets/Animations/UI/` | UI animation clips |

[EVIDENCE: Assets/Art/ and Assets/Animations/ directory listings]

### 5.2 Sprite Import Settings (Required)

All gameplay sprites must use:
- Filter Mode: Point (no filter) — enforces pixel art fidelity
- PPU (Pixels Per Unit): **32** — consistent across the project (base pixel resolution 32×32 per tile/character unit)
- Compression: None or lossless for pixel art

[EVIDENCE: git commit `d718060` — "art(placeholders): import placeholder sprites with correct PPU and filter settings"]

### 5.3 Asset Status Summary

| Category | Status |
|----------|--------|
| Enemy sprite (standard) | Placeholder exists |
| Shrine (base) sprite | Placeholder exists |
| Baybayin glyph sprites (17) | NOT CONFIRMED in repo |
| UI art (buttons, frames, icons) | Folders exist; content NOT CONFIRMED |
| Audio — pronunciation clips (17) | NOT CONFIRMED in repo |
| Audio — BGM | NOT CONFIRMED |
| Audio — SFX (base hit, game over, spawn) | NOT CONFIRMED |
| Baybayin template .txt files (17) | NOT CONFIRMED |

### 5.4 Shrine Variants

Each era has its own shrine/base structure at 64×96 px with 4 visual damage states (full, crack 1, crack 2, destroyed):

| Shrine | Era | Chapter |
|--------|-----|---------|
| Baybayin Altar | Spanish | 1 |
| Ancestral Door | American | 2 |
| Scroll Shrine | Japanese | 3 |

[EVIDENCE: docs/capstone/GDD.md, §4.1 Levels/Maps]

---

## 6. Naming Conventions Summary

| Asset Category | Pattern | Notes |
|----------------|---------|-------|
| Baybayin Character SO | `Char_[ID]` | ID uppercase, 2 chars: BA, KA, GA |
| Enemy Data SO | `Enemy_[Type]` | Type title-case: Standard, Fast |
| Level Config SO | `Level_[##]` | Zero-padded number: 01, 10 |
| Wave Config SO | `L[level]_W[wave]` | No padding: L1_W1, L10_W3 |
| Enemy prefab | `[Enemy] [Type]` | Brackets denote prefab: `[Enemy] Standard` |
| Manager prefab | `[Manager] [Name]` | Brackets: `[Manager] GameManager` |
| Recognition template | `[ID]_template.txt` | Lowercase ID: `ba_template.txt` OR uppercase per SO |
| Sprites | `[category]_[description]_[variant]` | Example: `enemy_standard_walk_01` |
| Audio clips | `[category]_[id]` | Example: `sfx_BA`, `bgm_chapter1` |

[EVIDENCE: Assets/Prefabs/Managers/ and Assets/Prefabs/Enemies/ — bracket convention observed]

---

## 7. Asset Dependency Graph

```
LevelConfigSO
  └─ List<WaveConfigSO>
        └─ List<BaybayinCharacterSO>
              ├─ displaySprite (Sprite)
              ├─ pronunciationClip (AudioClip)
              └─ templateFileName → Resources/Templates/[file].txt

LevelConfigSO
  └─ List<BaybayinCharacterSO> (allowedCharacters)

EnemyDataSO
  ├─ walkFrames (Sprite[])
  ├─ animatorController (RuntimeAnimatorController)
  └─ assignedCharacter → BaybayinCharacterSO

EnemyPool
  └─ _enemyPrefab → [Enemy] Standard.prefab
        └─ Enemy.cs
              └─ EnemyMover.cs
```

---

## 8. Fallback Strategy

| Missing Asset | Runtime Behavior | Risk Level |
|---------------|-----------------|------------|
| `pronunciationClip == null` | Silent defeat (no audio error, `AudioManager` is null-safe) | Low |
| `walkFrames` empty | Enemy spawns with no visible sprite (invisible) | Medium |
| `assignedCharacter == null` | Enemy cannot be recognized or defeated; gameplay blocks | High |
| Template file missing | `TemplateLoader` will throw `NullReferenceException` (PLANNED implementation) | Critical |
| `LevelConfigSO.waves` empty | Immediate level-complete, no gameplay | High |

[EVIDENCE: Assets/Scripts/Core/AudioManager.cs — null check on pronunciationClip]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs — null checks on walkFrames]
