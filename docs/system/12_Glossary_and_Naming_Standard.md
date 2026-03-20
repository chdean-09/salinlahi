# 12 — Glossary and Naming Standard
**Project:** Salinlahi
**Version:** 1.0
**Date:** 2026-03-19
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

---

## 2. Forbidden Synonyms

| Forbidden Term | Correct Term | Reason |
|----------------|-------------|--------|
| Alibata | Baybayin | "Alibata" is a 20th-century neologism rejected by scholars. The correct term is Baybayin. |
| Enemy spawn | Enemy get from pool | `Instantiate` is prohibited in the game loop. Correct phrasing: "retrieved from pool." |
| Enemy destroy | Enemy returned to pool / released | `Destroy` is not called on enemies during gameplay. |
| Create enemy | Get enemy from pool | Same as above. |
| Base health | Shrine hearts | The health mechanic uses discrete hearts, not a numeric health value. |
| Player character | Salinlahi (narrative presence only) | There is no on-screen player avatar during gameplay. The player's presence is their finger. |
| Drawing attack | Character drawing / stroke | Prefer "draw the Baybayin character" over "attack" in user-facing copy. |
| Dollar sign P | $P algorithm | Use `$P` in technical documents; spell out "Dollar-P" only in prose for non-technical readers. |

[EVIDENCE: docs/capstone/Salinlahi.md, §4.2 Characters — "no on-screen avatar during gameplay"]
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
| Manager prefabs | `[Manager] ClassName` | `[Manager] GameManager` |
| Enemy prefabs | `[Enemy] TypeName` | `[Enemy] Standard` |
| UI prefabs | `[UI] ScreenName` | `[UI] HUD` (planned) |

[EVIDENCE: Assets/Prefabs/ — bracket convention observed]

### 3.3 ScriptableObjects

| Rule | Pattern | Example |
|------|---------|---------|
| `BaybayanCharacterSO` assets | `Char_[ID]` | `Char_BA`, `Char_KA` |
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
| Enemy sprites | `enemy_[type]_[state]_[frame]` | `enemy_standard_walk_01` |
| UI sprites | `ui_[element]_[state]` | `ui_button_play_normal` |
| Background | `bg_[chapter]_[level]` | `bg_ch1_l1` |
| SFX clips | `sfx_[description]` | `sfx_base_hit`, `sfx_BA` |
| BGM tracks | `bgm_[context]` | `bgm_gameplay_ch1` |

### 3.6 Template Files

| Rule | Pattern | Example |
|------|---------|---------|
| Recognition templates | `[CharacterID]_template.txt` | `BA_template.txt` |
| CharacterID case must match `BaybayanCharacterSO.characterID` exactly | — | If SO has `"BA"`, file must be `BA_template.txt` |

---

## 4. Tag Conventions

| Tag | Applied To | Used By |
|-----|-----------|---------|
| `"PlayerBase"` | PlayerBase `GameObject` in Gameplay scene | `EnemyMover.OnTriggerEnter2D()` |

**Rule:** Never use magic strings for tags in code. Always use `CompareTag("TagName")`. Never use `gameObject.tag == "TagName"`.

[EVIDENCE: Assets/Scripts/Gameplay/Enemy/EnemyMover.cs — `CompareTag("PlayerBase")`]
