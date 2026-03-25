# 12 — Glossary and Naming Standard
**Project:** Salinlahi
**Version:** 1.2
**Date:** 2026-03-25
**Owner:** Jon Wayne Cabusbusan

---

## 1. Canonical Terms

| Term | Definition | Source |
|------|-----------|--------|
| **Baybayin** | The pre-colonial Filipino abugida writing system. Each character represents a consonant-vowel syllabic unit. The game uses 17 consonant base characters. **Correct spelling: Baybayin.** Not "Alibata." | Salinlahi.md §1; expert consultation |
| **Abugida** | A writing system in which each character represents a complete consonant-vowel syllabic unit (not individual phonemes). Baybayin is an abugida. | Salinlahi.md §2.1.3 |
| **$P Algorithm** | The $P Point-Cloud Gesture Recognizer. A 2D gesture recognition algorithm that treats a drawing as an unordered cloud of points, ignoring stroke number, order, and direction. The chosen recognition algorithm for Salinlahi. Also written as "Dollar P" in prose. | Salinlahi.md §1.7.4; §3.3.3 |
| **Template** | A pre-stored reference point cloud for one Baybayin character used by the $P algorithm to match player drawings. Stored as `.txt` files in `Assets/Resources/Templates/`. | Salinlahi.md §3.3.3 |
| **Confidence Score** | A value between 0 and 1 output by the $P algorithm indicating how closely a player's drawing matches the nearest template. A score ≥ 0.60 is accepted as a valid match. | RecognitionConfigSO.cs; Salinlahi.md §3.3.3 |
| **Shrine** | The player's base structure that enemies march toward. Losing all 3 Shrine hearts ends the game. | GDD §2.3 |
| **PlayerBase** | The Unity `GameObject` representing the Shrine's collision boundary. Must have Unity tag `"PlayerBase"`. Enemies use `OnTriggerEnter2D` with this tag. | EnemyMover.cs |
| **Heart** | One unit of the Shrine's health. The Shrine has 3 hearts by default. Each enemy base hit costs 1 heart. 0 hearts = Game Over. | GDD §2.3 |
| **Wave** | A single spawn sequence within a level, defined by `WaveConfigSO`. A wave has a fixed enemy count, spawn interval, and character pool. | WaveConfigSO.cs; GDD §2.4 |
| **Spawn Interval** | Seconds between consecutive enemy spawns within a wave. Defined in `WaveConfigSO.spawnInterval` (default: 3s). | WaveConfigSO.cs |
| **Bootstrap Scene** | The first scene loaded on app launch. It initializes all manager singletons and immediately transitions to MainMenu. It is never returned to after the initial load. | BootstrapLoader.cs; GDD §5.1 |
| **Manager** | A persistent `MonoBehaviour` Singleton that survives all scene loads. All managers are instantiated in the Bootstrap scene. | Singleton.cs; 02_Architecture |
| **EventBus** | The static C# event hub used for all cross-system communication. No direct inter-manager references except through `Instance` accessor and EventBus. | EventBus.cs |
| **Lite Build** | The free version of Salinlahi. Includes Story Mode levels 1–3 only. Endless Mode disabled. Separate app identifier. | TDD §7.2; Salinlahi.md §3.4 |
| **Full Build** | The premium version of Salinlahi (PHP 149). All 15 story levels, Endless Mode, all boss encounters. | TDD §7.2; Salinlahi.md §3.4 |
| **Tracing Dojo** | A pressure-free practice mode where players trace all 17 Baybayin characters with no enemies, no timer, and no penalty. | GDD §2.4 |
| **Intrinsic Integration** | A game design principle where the educational content (Baybayin drawing) is inseparable from the core gameplay mechanic (attacking enemies). The opposite of "chocolate-covered broccoli." | Salinlahi.md §2.1.2 |
| **Drawing Effect** | The cognitive science finding that drawing information to be learned produces superior memory retention compared to writing, visualizing, or viewing. The theoretical basis for Baybayin-as-attack-input. | Salinlahi.md §1.7.2; Fernandes et al., 2018 |
| **Multi-stroke Window** | The 1.5-second timer that begins after a finger lifts, during which additional strokes are accepted as part of the same drawing before recognition is submitted. | RecognitionConfigSO.cs |
| **Pool** | Unity `ObjectPool<T>` instance managing enemy `GameObject` recycling. Prevents runtime `Instantiate`/`Destroy` calls in the game loop. | EnemyPool.cs |
| **DontDestroyOnLoad** | Unity API call that marks a `GameObject` to persist across scene loads. Applied to all manager prefabs by `Singleton<T>.Awake()`. | Singleton.cs |
| **Protagonist** | The player character Salinlahi, visible on screen during gameplay as a 32×32 top-down sprite. Has 3 era-specific designs: Kuya (Village Boy, Spanish era), Laban (Fighter Youth, Japanese era), Manong (Young Adult, American era). Each has idle, draw gesture, victory, and collapse animations. | GDD §4.2 |
| **Dialogue Sequence** | A ScriptableObject asset containing an ordered list of dialogue lines (speaker, portrait, text, optional voice clip) for one story moment. Two types: Type A (gated, pauses gameplay) and Type B (in-wave popup, does not pause). | Team README §12 |
| **Era** | One of three historical periods represented in the game's chapters: Spanish Colonization (Chapter 1), American Occupation (Chapter 2), Japanese Occupation (Chapter 3). Each era has 4 unique enemy types, a unique shrine design, and a unique tileset. | GDD §4.1 |
| **Combo Streak** | A counter tracking consecutive correct drawings without a miss. At 5 consecutive successes, a combo reward triggers: all on-screen enemies slow down for 3 seconds. Resets on any miss or base hit. | GDD §3.2; Team README §9 |

---

## 2. Forbidden Synonyms

| Forbidden Term | Correct Term | Reason |
|----------------|-------------|--------|
| Alibata | Baybayin | "Alibata" is a 20th-century neologism rejected by scholars. The correct term is Baybayin. |
| Enemy spawn | Enemy get from pool | `Instantiate` is prohibited in the game loop. Correct phrasing: "retrieved from pool." |
| Enemy destroy | Enemy returned to pool / released | `Destroy` is not called on enemies during gameplay. |
| Create enemy | Get enemy from pool | Same as above. |
| Base health | Shrine hearts | The health mechanic uses discrete hearts, not a numeric health value. |
| Player character (generic) | Protagonist / Salinlahi | Use 'protagonist' or the era-specific name (Kuya, Laban, Manong) when referring to the on-screen character. The protagonist IS visible during gameplay as a 32×32 sprite. |
| Drawing attack | Character drawing / stroke | Prefer "draw the Baybayin character" over "attack" in user-facing copy. |
| Dollar sign P | $P algorithm | Use `$P` in technical documents; spell out "Dollar-P" only in prose for non-technical readers. |

[EVIDENCE: GDD §4.2 — protagonist is visible as 32×32 sprite during gameplay]
[EVIDENCE: Expert consultation noted in Salinlahi.md — Alibata is not the correct term]

---

## 3. Naming Rules

### 3.1 C# Scripts

| Rule | Pattern | Example |
|------|---------|---------|
| Classes: PascalCase | `ClassName` | `GameManager`, `EnemyPool` |
| Private fields: `_camelCase` | `_fieldName` | `_bgmSource`, `_speed` |
| Public properties: PascalCase | `PropertyName` | `CurrentState`, `Character` |
| Methods: PascalCase | `MethodName()` | `Initialize()`, `Defeat()` |
| Constants: ALL_CAPS | `CONSTANT_NAME` | `SCENE_MAIN_MENU` |
| EventBus events: `On[Event]` | `OnEventName` | `OnGameOver`, `OnBaseHit` |
| EventBus raisers: `Raise[Event]` | `RaiseEventName()` | `RaiseGameOver()`, `RaiseBaseHit()` |

[EVIDENCE: All C# scripts — consistent pattern observed]

### 3.2 Prefabs

| Rule | Pattern | Example |
|------|---------|---------|
| Manager prefabs | `Manager_[Name]` | `Manager_Audio`, `Manager_GameManager` |
| Enemy prefabs | `Enemy_[Type]` | `Enemy_Standard`, `Enemy_Fast` |
| Boss prefabs | `Boss_[Chapter]` | `Boss_Chapter1` |
| UI prefabs | `UI_[Name]` | `UI_HUD`, `UI_HeartIcon` |

[EVIDENCE: Team README §5 — underscore convention is the target standard]

### 3.3 ScriptableObjects

| Rule | Pattern | Example |
|------|---------|---------|
| `BaybayinCharacterSO` assets | `Char_[ID]` | `Char_BA`, `Char_KA` |
| `EnemyDataSO` assets | `Enemy_[Type]` | `Enemy_Standard` |
| `LevelConfigSO` assets | `Level_[##]` (zero-padded) | `Level_01`, `Level_10` |
| `WaveConfigSO` assets | `L[level]_W[wave]` | `L1_W1`, `L10_W3` |

### 3.4 Scenes

| Rule | Pattern | Example |
|------|---------|---------|
| Scenes: PascalCase, no spaces | `SceneName` | `Bootstrap`, `MainMenu`, `GameOver` |

[EVIDENCE: Assets/_Scenes/ — confirmed naming]

### 3.5 Art Assets

| Asset Type | Pattern | Example |
|------------|---------|---------|
| Enemy sprites | `enemy_[type]_[state].png` | `enemy_standard_walk.png`, `enemy_standard_death.png` |
| Boss sprites | `boss_[chapter]_[state].png` | `boss_chapter1_idle.png` |
| UI sprites | `ui_[element]_[state].png` | `ui_button_normal.png` |
| Background | `bg_[scene]_[layer].png` | `bg_gameplay_layer1.png` |
| SFX clips | `sfx_[description].wav` | `sfx_enemy_defeat.wav`, `sfx_base_hit.wav` |
| BGM tracks | `bgm_[context].ogg` | `bgm_gameplay.ogg`, `bgm_boss.ogg` |
| Pronunciation | `pronunciation_[syllable].wav` | `pronunciation_ba.wav`, `pronunciation_ka.wav` |

### 3.6 Template Files

| Rule | Pattern | Example |
|------|---------|---------|
| Recognition templates | `[CharacterID]_template.txt` | `BA_template.txt` |
| CharacterID case must match `BaybayinCharacterSO.characterID` exactly | — | If SO has `"BA"`, file must be `BA_template.txt` |

### 3.7 Animations

| Rule | Pattern | Example |
|------|---------|---------|
| Enemy animations | `enemy_[type]_[state].anim` | `enemy_standard_walk.anim`, `enemy_standard_death.anim` |
| Boss animations | `boss_[chapter]_[phase].anim` | `boss_chapter1_phase1.anim` |
| UI animations | `ui_[element]_[state].anim` | `ui_heart_break.anim` |

---

## 4. Tag Conventions

| Tag | Applied To | Used By |
|-----|-----------|---------|
| `"PlayerBase"` | PlayerBase `GameObject` in Gameplay scene | `EnemyMover.OnTriggerEnter2D()` |

**Rule:** Never use magic strings for tags in code. Always use `CompareTag("TagName")`. Never use `gameObject.tag == "TagName"`.

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs — `CompareTag("PlayerBase")`]
