# 07 — Content Pipeline
**Project:** Salinlahi
**Version:** 1.6
**Date:** 2026-08-31
**Owner:** Chad Andrada (Product Owner / Designer)

---

## 1. Baybayin Characters

### 1.1 Character Set Scope

The shipped game covers **18 Baybayin characters: 15 consonants (BA, DA, GA, HA, KA, LA, MA, NA, NGA, PA, RA, SA, TA, WA, YA) and 3 vowels (A, E/I, O/U)**. Diacritical marks (kudlit) are explicitly out of MVP scope (Should Ship, may be deferred post-launch).

> **Known discrepancy — open content decision.** The GDD states 17 characters (14 consonants) in five places. The authored set is 18 because **`RA` is authored as its own glyph**, where classic Baybayin folds RA into DA. The code and assets are correct at 18; the requirement text is the thing that is wrong. This is tracked as REQ-42 (⚠ Partial, P1) in doc 10 and discussed in doc 13 — it needs a product decision, not a code change. Do not "fix" the asset count down to 17 to match the GDD.

[EVIDENCE: `Assets/ScriptableObjects/Characters/` — 18 `Char_*.asset` files]
[EVIDENCE: docs/system/10_Requirements_Traceability_Matrix.md, REQ-12 and REQ-42]
[EVIDENCE: docs/capstone/Salinlahi.md, §1.5.1 Scope — "17 Baybayin consonant characters" (superseded by the authored set)]

### 1.2 Each character requires these assets

| Asset | File Type | Location | Required By |
|-------|-----------|----------|-------------|
| `BaybayinCharacterSO` asset | `.asset` | `Assets/ScriptableObjects/Characters/Char_[ID].asset` | All gameplay systems |
| Display sprite (glyph) | `.png` | `Assets/Art/UI/` or `Assets/Art/Characters/` | Enemy renderer; Tracing Dojo |
| Pronunciation audio clip | `.wav` / `.mp3` | `Assets/Audio/` | AudioManager |
| Recognition template file(s) | `.txt` (point coordinates) | `Assets/Resources/Templates/[ID]_template_[NN].txt` — **multiple numbered variants per character**, not one file | `DollarPRecognizer.cs` via `TemplateLoader.cs` |

### 1.3 Current Status

**Superseded — the Sprint 1 "placeholder assets only" note no longer holds.** Verified state on `dev` as of 2026-08-31:

| Asset class | Status | Detail |
|-------------|--------|--------|
| `BaybayinCharacterSO` assets | ✅ Complete | 18 `Char_*.asset` files, plus `CharacterRegistry_Default.asset` |
| Recognition templates | ✅ Complete | **121 `.txt` files** across all 18 characters (~6–7 variants each; HA carries 15, authored while resolving its recognition failure) |
| Display sprites | ✅ Present | Assigned on the character SOs |
| Pronunciation clips | ⚠️ **Partial — 7 of 18 assigned; 11 missing** | `BaybayinCharacterSO.pronunciationClip`. Recognition audio feedback is silent for the 11 unassigned characters. Tracked as DEP-03 in doc 11. |

Template variant counts are deliberately uneven: characters that proved harder to recognize received more authored variants. Adding templates is the first, cheapest lever for a character that recognizes poorly — before touching `minimumConfidence`, which is global and affects every character.

[EVIDENCE: `Assets/ScriptableObjects/Characters/`; `Assets/Resources/Templates/`]
[EVIDENCE: Assets/Scripts/Data/BaybayinCharacterSO.cs — `pronunciationClip`]

---

## 2. Enemy Content

### 2.1 Enemy Types

| Enemy Type | `enemyID` | Era | Tier | First Appears | Priority | Prefab | Status |
|------------|-----------|-----|------|--------------|----------|--------|--------|
| Soldado | `"soldado"` | Spanish | Regular (32×32) | Level 1 | Must Ship | `[Enemy] Soldado.prefab` | Implemented (`[Enemy] Soldado.prefab` + `EnemyData_Soldado.asset`) |
| Fraile | `"fraile"` | Spanish | Variant (32×32) | Level 2 | Must Ship | `[Enemy] Fraile.prefab` | Implemented (`[Enemy] Fraile.prefab` + `EnemyData_Fraile.asset`) |
| Guardia | `"guardia"` | Spanish | Variant (32×32) | Level 3 | Must Ship | `[Enemy] Guardia.prefab` | Implemented (`[Enemy] Guardia.prefab` + `EnemyData_Guardia.asset`) |
| Capitan | `"capitan"` | Spanish | Elite (48×48) | Level 4 | Must Ship | `[Enemy] Capitan.prefab` | Implemented (`[Enemy] Capitan.prefab` + `EnemyData_Capitan.asset`) |
| Soldier | `"soldier"` | American | Regular (32×32) | Level 6 | Must Ship | `[Enemy] Soldier.prefab` | Implemented (`[Enemy] Soldier.prefab` + `EnemyData_Soldier.asset`) |
| Maestro | `"maestro"` | American | Variant (32×32) | Level 7 | Should Ship | `[Enemy] Maestro.prefab` | Implemented (`[Enemy] Maestro.prefab` + `EnemyData_Maestro.asset`) |
| Pensionado | `"pensionado"` | American | Variant (32×32) | Level 8 | Should Ship | `[Enemy] Pensionado.prefab` | Implemented (`[Enemy] Pensionado.prefab` + `EnemyData_Pensionado.asset`) |
| General | `"general"` | American | Elite (48×48) | Level 9 | Should Ship | `[Enemy] General.prefab` | Implemented (`[Enemy] General.prefab` + `EnemyData_General.asset`) |
| Heitai | `"heitai"` | Japanese | Regular (32×32) | Level 11 | Must Ship | `[Enemy] Heitai.prefab` | Implemented (`[Enemy] Heitai.prefab` + `EnemyData_Heitai.asset`) |
| Kisha | `"kisha"` | Japanese | Variant (32×32) | Level 12 | Should Ship | `[Enemy] Kisha.prefab` | Implemented (`[Enemy] Kisha.prefab` + `EnemyData_Kisha.asset`) |
| Kempei | `"kempei"` | Japanese | Variant (32×32) | Level 13 | Should Ship | `[Enemy] Kempei.prefab` | Implemented (`[Enemy] Kempei.prefab` + `EnemyData_Kempei.asset`) |
| Shokan | `"shokan"` | Japanese | Elite (48×48) | Level 14 | Should Ship | `[Enemy] Shokan.prefab` | Implemented (`[Enemy] Shokan.prefab` + `EnemyData_Shokan.asset`) |

**Note:** `[Enemy] Shielded.prefab` and `[Enemy] Sprinter.prefab` were removed from the repo. The matching `EnemyData_Shielded.asset` / `EnemyData_Sprinter.asset` SOs may still exist as legacy placeholders and are not referenced by any current `LevelConfigSO` or its embedded `WaveDefinition` waves.

### 2.2 Corrupted-Enemy Roster (in transition)

The era-themed roster in §2.1 is **being superseded** by a corrupted-enemy roster in which each enemy is bound to the specific Baybayin syllable that defeats it, via `EnemyDataSO.assignedCharacter`.

| Aspect | State on `dev` |
|--------|----------------|
| Enemy data assets | 32 `EnemyData_*.asset` total |
| With `assignedCharacter` set | **19** (AbongSimula, Bakod, Daan-Lihis, Fraile, Gapos, Hati, Iligaw, Kadena, Labo, Maestro, Mantsa, NawalangMukha, Ngatngat, Punit, Salungat, Takip, Uhaw, Walang-Awa, YaposngDilim) |
| Per-enemy stats and abilities | **Not on `dev`** — authored in draft PR #144 |
| `[Enemy] Labo` / `[Enemy] Daan-Lihis` prefab variants | **Not on `dev`** — added by draft PR #144 |

Two consequences worth stating plainly, because both have already cost debugging time:

1. **The two rosters currently coexist.** The era-themed enemies (Soldado, Maestro, …) and the corrupted enemies are both live, which is why the sandbox catalog lists both. Retiring the era-themed roster is a **product decision that has not been made** — do not delete those assets on the assumption that the corruption roster replaced them.
2. **Registering a new enemy prefab takes two steps, not one.** Adding the prefab to the `[Manager] EnemyPool` prefab's `_registeredEnemyPrefabs` is not sufficient: **scene instances of the pool override the array**, including its size. A prefab registered only at the prefab level fails at runtime with `EnemyPool: Unknown enemyID '<id>'. Falling back to default pool.` The scene instances in `Bootstrap`, `Gameplay`, and `Level_01_Tutorial` must be updated too.

[EVIDENCE: Assets/Scripts/Data/EnemyDataSO.cs — `assignedCharacter`]
[EVIDENCE: `Assets/ScriptableObjects/Enemies/` — 32 assets, 19 with `assignedCharacter`]
[EVIDENCE: Assets/Prefabs/Managers/[Manager] EnemyPool.prefab — `_registeredEnemyPrefabs`]

[EVIDENCE: Assets/Prefabs/Enemies/ — Soldado, Soldier, Heitai, Maestro, Pensionado, General, Kisha, Kempei, Shokan, Boss_ElInquisidor prefabs confirmed]
[EVIDENCE: Assets/ScriptableObjects/ — matching `EnemyData_*.asset` files confirmed]
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

### 2.4 Bosses

| Boss | Asset | Status |
|------|-------|--------|
| El Inquisidor (Spanish) | `[Enemy] Boss_ElInquisidor.prefab` + `BossConfig_ElInquisidor.asset` | Implemented |
| The Superintendent (American) | — | PLANNED |
| Kadiliman (Final) | — | PLANNED |

[EVIDENCE: Assets/Prefabs/Enemies/[Enemy] Boss_ElInquisidor.prefab]
[EVIDENCE: Assets/ScriptableObjects/BossConfig_ElInquisidor.asset]

---

## 3. Levels and Waves

### 3.1 Level Structure

| Campaign | Levels | Historical Era | Gameplay Theme | Boss Level |
|----------|--------|----------------|-----------------|------------|
| Ugat | 1–5 | Spanish Colonization | Drawing mastery | Level 5 |
| Ugnayan | 6–10 | American Occupation | Tactical thinking | Level 10 |
| Pamana | 11–15 | Japanese Occupation | Mastery and chaos | Level 15 |

Paglimot is a separate set of three mastery encounters after the story campaigns, not a fourth five-level campaign. The historical campaign names in the capstone and Jira evidence remain unchanged.

[EVIDENCE: docs/capstone/GDD.md, §4.1 Levels/Maps — chapter names and historical eras]
[EVIDENCE: Team README §9 — chapter gameplay themes]

### 3.2 Level Asset Naming

| Asset | Pattern | Example |
|-------|---------|---------|
| `LevelConfigSO` | `Level_[##]` | `Level_01.asset`, `Level_10.asset` |

**Note:** Authored on-disk pattern is `Level[N]_Config.asset` (e.g. `Level1_Config.asset`); the doc's `Level_##` example is the planned convention. Levels 1–3 are populated; the remaining 12 are PLANNED. Wave data is now embedded inside each `LevelConfigSO` as `List<WaveDefinition>` — there are no separate wave `.asset` files.

**Current status:** `Assets/ScriptableObjects/Levels/` contains `Level1_Config.asset` through `Level15_Config.asset`; all active assets use the campaign mapping above.

[EVIDENCE: Assets/ScriptableObjects/Levels/Level1_Config.asset, Level2_Config.asset, Level3_Config.asset]

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

Templates are loaded via `Resources.Load<TextAsset>` at startup by `TemplateLoader.cs`. Each file represents one Baybayin character's point-cloud template.

### 4.2 File Naming

```
[characterID]_template.txt
```

Example: `BA_template.txt`, `KA_template.txt`

The `characterID` must match `BaybayinCharacterSO.characterID` exactly (case-sensitive).

### 4.3 File Content Format

Plain text coordinate pairs. Format per `TemplateLoader.cs` implementation. Format based on $P algorithm:
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
  └─ List<WaveDefinition> (embedded, no asset files)
        ├─ List<BaybayinCharacterSO> (characters — subset of level allowedCharacters)
        │     ├─ displaySprite (Sprite)
        │     ├─ pronunciationClip (AudioClip)
        │     └─ templateFileName → Resources/Templates/[file].txt
        └─ List<EnemyDataSO> (enemyTypes — subset of level allowedEnemyTypes)

LevelConfigSO
  └─ List<BaybayinCharacterSO> (allowedCharacters)

LevelConfigSO
  └─ List<EnemyDataSO> (allowedEnemyTypes)

LevelConfigSO
  └─ bossConfig (optional) → BossConfigSO
                               ├─ bossEnemyData → EnemyDataSO (assignedCharacter MUST be null)
                               ├─ phases (List<BossPhase>) — each phase may reference summonEnemyTypes → EnemyDataSO
                               └─ fallbackEnemyTypes → List<EnemyDataSO>

LevelConfigSO
  └─ eraTheme → EraThemeSO

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
| Template file missing | `TemplateLoader` will throw `NullReferenceException` | Critical |
| `LevelConfigSO.waves` empty | Immediate level-complete, no gameplay | High |
| `BossConfigSO.phases` empty | `BossController.StartBoss` logs an error and aborts the encounter | Critical |
| `BossConfigSO.bossEnemyData.assignedCharacter` non-null | Boss enters `CombatResolver.FindClosestToBase` results, breaking the boss damage gate | High |
| `BossPhase.summonEnemyTypes` empty AND `BossConfigSO.fallbackEnemyTypes` empty | `BossSummonTicker` skips spawns with a warning; vulnerability windows still resolve normally | Medium |

[EVIDENCE: Assets/Scripts/Core/AudioManager.cs — null check on pronunciationClip]
[EVIDENCE: Assets/Scripts/Gameplay/Enemy/Enemy.cs — null checks on walkFrames]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossController.cs — `StartBoss` phase guard]
[EVIDENCE: Assets/Scripts/Gameplay/Combat/CombatResolver.cs — `FindClosestToBase`]
[EVIDENCE: Assets/Scripts/Gameplay/Boss/BossSummonTicker.cs — summon-type fallback]
