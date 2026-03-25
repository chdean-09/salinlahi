# Epic: Level Design & Progression (SALIN-5)

**Status:** ⚠️ Architecture Complete — Level 1 Only Configured | **Priority:** High

ScriptableObject data architecture, all 15 level configurations, level progress saving, difficulty tuning, and Endless Mode.

---

## SALIN-22 — Create All ScriptableObject Data Architecture Classes

| Field      | Value |
|------------|-------|
| **Status** | Done |
| **Assignee** | — |
| **Priority** | High |
| **Sprint** | Sprint 1 |
| **Blocks** | SALIN-30, SALIN-42, SALIN-55 |
| **Blocked By** | — |

All ScriptableObject data classes implemented: `LevelConfigSO`, `WaveConfigSO`, `EnemyDataSO`, `BaybayinCharacterSO`, `RecognitionConfigSO`, `GameConfigSO`. All under `Assets/ScriptableObjects/` with `CreateAssetMenu` attributes. One instance of each exists for testing. Level 1 is fully configured with 3 waves.

> ⚠️ Class is named `LevelConfigSO` in code (not `LevelDataSO` as the original Jira description said). All references should use the actual class name.

**Acceptance Criteria:**
- All SO classes exist under `Assets/Scripts/Data/`
- `CreateAssetMenu` path working for all classes
- `LevelConfigSO`: levelName, levelNumber, waves[], allowedCharacters[], isAvailableInLite
- `WaveConfigSO`: waveID, waveNumber, charactersInWave[], enemyCount, spawnInterval, waveStartDelay
- `EnemyDataSO`: enemyID, moveSpeed, walkFrames[], animatorController, assignedCharacter
- `BaybayinCharacterSO`: characterID, syllable, displaySprite, pronunciationClip, templateFileName
- `RecognitionConfigSO`: resamplePointCount (default 32), minimumConfidence (default 0.60), minimumPointCount (default 8)

---

## SALIN-30 — Design and Configure Levels 1–5 (Core Evaluation Levels)

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Jeff |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-43, SALIN-56 |
| **Blocked By** | SALIN-22, SALIN-27, SALIN-37, SALIN-38 |

Level 1 is done. Create `LevelConfigSO` and `WaveConfigSO` assets for Levels 2–5. These are the **core evaluation levels** used in the capstone research study — they must be balanced and playtested before UAT begins.

**Level guidance:**
- **Level 2:** 3 waves, add 1 new character, introduce Sprinter enemy (SALIN-37 must be done first)
- **Level 3:** 4 waves, mix 3 characters, introduce Shielded enemy (SALIN-38 must be done first)
- **Level 4:** 4 waves, 4 characters, mixed Soldado + Sprinter + Shielded enemy types
- **Level 5:** 5 waves, all unlocked characters, Chapter 1 Boss wave at end (SALIN-68 must be done)

**Acceptance Criteria:**
- `LevelConfigSO` assets created for Levels 2–5 in `Assets/ScriptableObjects/Levels/`
- `WaveConfigSO` assets created for each wave in each level
- Each level references only enemy types that are implemented and prefabbed
- All 5 levels playable without null reference errors
- Level 5 boss wave references a `BossConfigSO` (can be a stub until SALIN-68 lands)

---

## SALIN-42 — Design and Configure Levels 6–10

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Clyde |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-56 |
| **Blocked By** | SALIN-22, SALIN-39, SALIN-52, SALIN-53, SALIN-16 |

Chapter 2 levels. Introduce Chain, Phaser, and Decoy enemy variants. New Baybayin characters added per level. Chapter 2 boss at Level 10.

**Level guidance:**
- **Level 6:** 4 waves, introduces Chain enemies (SALIN-39)
- **Level 7:** 4 waves, introduces Phaser enemies (SALIN-52)
- **Level 8:** 5 waves, mixed Chain + Phaser
- **Level 9:** 5 waves, introduces Decoy enemies (SALIN-53)
- **Level 10:** 5 waves + Chapter 2 Boss (SALIN-68)

**Acceptance Criteria:**
- `LevelConfigSO` and `WaveConfigSO` assets created for Levels 6–10
- All enemy variant prefabs referenced in wave configs are implemented and functional
- Level 10 boss wave configured with a Chapter 2 `BossConfigSO`
- All 10 levels accessible and playable from Level Select (SALIN-43)

---

## SALIN-48 — Implement Level Progress Saving — PlayerPrefs Manager

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | High |
| **Sprint** | Sprint 2 |
| **Blocks** | SALIN-43, SALIN-70 |
| **Blocked By** | SALIN-22 |

`ProgressManager.cs` Singleton. Stores per-level completion state and star rating (0–3 stars) to `PlayerPrefs`. Exposed API: `MarkLevelComplete(int levelID, int stars)`, `IsLevelUnlocked(int levelID)`, `GetStars(int levelID)`. Used by Level Select (SALIN-43) and Endless Mode unlock (SALIN-70).

**Acceptance Criteria:**
- `ProgressManager` is a Singleton instantiated from Bootstrap
- `MarkLevelComplete(levelID, stars)` persists completion and star count to PlayerPrefs
- `IsLevelUnlocked(levelID)` returns true if previous level is complete (or levelID == 1)
- `GetStars(levelID)` returns 0–3 for any level ID
- Progress persists across app restarts
- Resetting progress (`ClearAllProgress()`) clears all keys cleanly for research session resets

---

## SALIN-55 — Design and Configure Levels 11–15

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Wayne |
| **Priority** | Medium |
| **Sprint** | Sprint 3 |
| **Blocks** | SALIN-56 |
| **Blocked By** | SALIN-22, SALIN-54, SALIN-16 |

Chapter 3 levels. Introduce Zigzagger and Healer enemy variants. All 17 Baybayin characters must be available by this point. Chapter 3 final boss (Level 15) is the hardest encounter in the game.

**Level guidance:**
- **Level 11:** 4 waves, introduces Zigzagger enemies (SALIN-54)
- **Level 12:** 5 waves, introduces Healer enemies (SALIN-54)
- **Level 13:** 5 waves, mixed Zigzagger + Healer + previous variants
- **Level 14:** 5 waves, all 17 characters in rotation, all variant types
- **Level 15:** 5 waves + Chapter 3 Final Boss (most complex `BossConfigSO`)

**Acceptance Criteria:**
- `LevelConfigSO` and `WaveConfigSO` assets created for Levels 11–15
- Level 15 boss references the Chapter 3 `BossConfigSO`
- All 17 Baybayin characters appear across Levels 11–15 wave configs
- All levels playable and accessible from Level Select

---

## SALIN-56 — Full Difficulty Tuning Pass — All 15 Levels

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | High |
| **Sprint** | Sprint 4 |
| **Blocks** | SALIN-78 (internal playtest must use tuned levels) |
| **Blocked By** | SALIN-30, SALIN-42, SALIN-55, SALIN-78 |

After all 15 levels are configured and internal playtest data (SALIN-78) is collected, do a full balance pass. Adjust `enemyCount`, `spawnInterval`, `moveSpeed`, and wave timing across all 15 levels to achieve a smooth difficulty curve. Use recognition accuracy logs (SALIN-35) from the playtest to identify which characters are causing unintended difficulty spikes.

**Acceptance Criteria:**
- All 15 levels played through at least once during internal playtest (SALIN-78) before tuning begins
- Recognition accuracy log (SALIN-35) data reviewed for per-character difficulty outliers
- No single level feels like a large difficulty spike relative to adjacent levels
- Chapter bosses (Levels 5, 10, 15) feel climactic but beatable
- Tuning changes documented as comments on this ticket (before/after values)

---

## SALIN-70 — Implement Endless Mode Unlock Trigger (Post-Story) *(Stretch Goal)*

| Field      | Value |
|------------|-------|
| **Status** | To Do |
| **Assignee** | Chad |
| **Priority** | Low |
| **Sprint** | Sprint 4 *(stretch — first to cut if Sprint 3 slips)* |
| **Blocks** | — |
| **Blocked By** | SALIN-48, SALIN-55 |

Unlocked after all 15 story levels are complete (checked via `ProgressManager`). Endless Mode uses procedurally escalating waves with no level cap. Tracks and displays highest wave reached in PlayerPrefs. Pause menu exit at any time without data loss.

**Acceptance Criteria:**
- Given all 15 levels complete in PlayerPrefs, the Endless Mode button becomes interactive on Main Menu
- Tapping the button loads a new scene with infinite wave escalation
- Wave difficulty scales: `enemyCount +2` and `spawnInterval -0.1s` every 5 waves
- Highest wave reached persisted in PlayerPrefs and displayed on the Endless Mode button
- Pause menu exit works cleanly without data loss

**Req IDs:** GDD-REQ-006

> ⚠️ Stretch goal. If Sprint 3 slips by more than 2 days, this ticket is the first to be cut from Sprint 4.
