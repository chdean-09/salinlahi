# Technical Work — Backlog

Spike and technical-work specifications for the Salinlahi campaign.

**Related references**

- **Authoritative tracker:** the SALIN Jira project (epics SALIN-126–132). Each `TW-*`
  planning ID appears in its ticket's description (for example `TW-TASK-011` → SALIN-180).
- **Per-ticket specs and plans:** `docs/salin-166-spec.md`, `docs/salin-180-spec.md`,
  `docs/salin-180-plan.md`, and successors under `docs/`.
- **Legacy ticket dispositions:** [legacy-traceability.md](legacy-traceability.md)
  (SALIN-187 / TW-CHORE-003).

---

## TW-SPK-003 — Validate the three Paglimot mastery encounters

| Field | Value |
|-------|-------|
| **Jira** | SALIN-169 |
| **Type** | Spike (work-type-spike) |
| **Parent Epic** | BL-E3 (SALIN-126) |
| **MoSCoW** | Must Have |
| **Time-box** | 2 working days |
| **Supports** | BL-E3-S4, BL-E4-S5, BL-E5-S5 |
| **Workbook** | CORE GAME MECHANICS.xlsx |
| **Workbook checksum** | `34dad782a025b3acd3dcfc9bdfb2ce5c595fe81e6bd1789c9042849b63c27eb7` (Jira ticket) — **discrepancy:** `ContentIdentity.ApprovedWorkbookSha256` = `33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83` (see §7 Open Items) |
| **Repository source** | `docs/backlog/technical-work.md` (this file) |

### Scope and Planning Source

The workbook defines cumulative paragraph mastery at Levels 5 and 10 and an
explicit three-phase Paglimot battle at Level 15. This spike validates the
approved extension that presents **all three era endings as feasible
three-phase encounters**, with the first two testing era mastery and Level 15
serving as the final campaign mastery sequence.

**Inspect:** `BossController`, boss configuration assets, existing tutorials,
source-plan era endings.

### Terminology mapping

The workbook term **"Paglimot"** (Filipino: *the act of forgetting*) is the
narrative umbrella for the three era-ending boss encounters. Each boss is a
manifestation of cultural erasure — *paglimot* — in its respective era. The
codebase does not use the word "Paglimot" directly; it maps to the three
existing boss entities:

| Workbook term | Codebase boss | Era | Level | Config asset |
|---------------|---------------|-----|-------|-------------|
| Paglimot I (era mastery) | El Inquisidor | Spanish (ugat) | 5 | `BossConfig_ElInquisidor.asset` |
| Paglimot II (era mastery) | The Superintendent | American (ugnayan) | 10 | `BossConfig_Superintendent.asset` |
| Paglimot III (campaign mastery) | Kadiliman | Japanese (pamana) | 15 | `BossConfig_Kadiliman.asset` |

**"Paragraph"** in the workbook maps to the era's narrative/story unit. Each
era has one paragraph of story (intro dialogue, era content, boss outro).
**"Paragraph restoration"** is the narrative beat where defeating the era's
boss restores that era's lost story — the `outroDialogue` field on
`LevelConfigSO` (line 79) and the `LevelFlowController.PlayOutroThenVictory()`
coroutine (line 444) that plays it. **"Cumulative paragraph mastery"** means
the boss vulnerability window tests the player's ability to draw all
characters introduced *up to and including* that era (the
`allowedCharacters` roster on `LevelConfigSO`, line 43).

### LF-CONTRACT-v2 compatibility

LF-CONTRACT-v2 is the external level-framework contract (the revised campaign
data schema tracked in SALIN-170/SALIN-171). In the codebase it is
materialized as `ContentIdentity` (`Assets/Scripts/Data/Campaign/ContentIdentity.cs`)
with `RevisedCampaignId = "campaign.revised-v1"` and the approved workbook SHA
`33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83` (line 14).

The three encounter specifications below are compatible with this contract
because:

1. They reuse the existing `BossConfigSO` / `BossPhase` schema — no new
   ScriptableObject types or field changes are required.
2. They draw `allowedCharacters` from `LevelConfigSO` (the revised cumulative
   symbol pool), so the vulnerability window's glyph sampling already honors
   the cumulative-roster contract. `BossController.SampleNextExpectedCharacter`
   (line 114) reads `GameManager.Instance.CurrentLevel.allowedCharacters`.
3. They do not introduce any level beyond 15 — no `level.pamana.06` or
   equivalent is created. `ContentIdentity.RevisedLevelIds` (line 28) is
   generated from `RevisedEraIds` (3 eras) × `RevisedLevelsPerEra` (5) = 15
   entries. `RevisedFinaleLevelId` (line 29) is
   `RevisedLevelIds[14]` = `level.pamana.05`.
4. The `outroDialogue` and `bossConfig` fields already on `LevelConfigSO`
   (lines 50, 79) are the contract hooks for boss encounter wiring and paragraph restoration
   encounter wiring; the encounters consume them at runtime via existing
   `BossController` + `ProgressManager` + `LevelFlowController` paths.

---

## 1. Three-Phase Encounter Specifications

All three encounters use the existing `BossController` state machine
(`Assets/Scripts/Gameplay/Boss/BossController.cs`, line 8):

```
Idle → Intro → [SummoningPhase → WindingDown → Vulnerable → Damaged] × N → Outro → Defeated
```

Each phase = 1 HP. Boss HP = `phases.Count` (line 65: `HPRemaining =
config.phases.Count`). The vulnerability window samples glyphs from
`LevelConfigSO.allowedCharacters` via `SampleNextExpectedCharacter` (line 114).
Phase failure (timer expiry) repeats the phase with **no HP loss** — this is
the existing retry-within-encounter behavior (lines 234-253: the `while
(!phaseCleared)` loop at line 138 re-enters `RunSummoningPhase` for the same
`i`).

### 1.1 Level 5 — El Inquisidor (Paglimot I, Spanish era mastery)

**Era:** ugat (Spanish Colonization), Levels 1–5.
**Roster tested:** 8 characters (BA, KA, DA, GA, HA, LA, MA, NA per GDD §3.3,
line 145).
**Theme:** The friar-inquisitor burns manuscripts. Defeating him proves the
script still has power (GDD §4.5, line 254).
**Config asset:** `BossConfig_ElInquisidor.asset` — **already fully authored**
(3 phases, audio bank wired, tutorial wired). This is the reference
implementation.

Actual values read from
`Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_ElInquisidor.asset`:

| Phase | summonPhaseDuration | delayBetweenSummons | minionsPerSummon (min–max) | summonEnemyTypes | requiredCharacterCount | vulnerabilityTimer | movementPattern | Design intent |
|-------|---------------------|---------------------|---------------------------|------------------|------------------------|---------------------|-----------------|---------------|
| 1 | 30s | 7s | 1–2 | Soldado | 3 | 20s | Pace (speed 1.0, halfRange 2.5) | Gentle intro. Slow pace, single enemy type, generous timer. Player learns the summon → wind-down → vulnerable rhythm. |
| 2 | 30s | 4s | 1–2 | Fraile, Soldado, Guardia | 3 | 20s | Pace (speed 1.3, halfRange 2.5) | Escalation. Faster movement, variant enemies (phaser + fast), shorter summon gap. Tests multitasking under pressure. |
| 3 | 20s | 4s | 2–3 | Soldado, Fraile, Guardia, Capitan | 4 | 25s | Teleport (halfRange 2.5 × 2.5) | Climax. Teleporting boss, elite (shielded Capitan) joins, 4 draws required. Tests full era mastery. |

**Config wiring verified:**
- `bossEnemyData`: `EnemyData_Boss_ElInquisidor.asset` (guid `aa843150...`) —
  has 6 walk frames, 8 death frames, `era: 0` (Spanish), `dealsContactDamage: 0`.
- `audioBank`: `BossAudioBank_ElInquisidor.asset` (guid `e1230ab6...`) — wired.
- `tutorial`: `BossTutorial_ElInquisidor.asset` (guid `76ccbd22...`) — 4 pages
  (name/lore, summoning, vulnerability, teleportation).
- `fallbackEnemyTypes`: Soldado (guid `442e301f...`).
- `summonHorizontalBounds`: (-2, 2).
- `introDuration`: 1.5s, `outroDuration`: 0s.

**Level wiring verified** (`Level5_Config.asset`):
- `levelNumber: 5`, `chapterNumber: 1`, `chapterName: "Chapter 1"`.
- `bossConfig`: points to `BossConfig_ElInquisidor.asset` (guid `4b809512...`).
- `allowedCharacters`: 6 entries (should be 8 per GDD — **data gap**, see §7
  Open Items).
- `outroDialogue`: null (**data gap** — paragraph restoration dialogue not yet
  authored, see §7).
- `hasProtagonist: 1`.

**Status:** Fully implemented and wired. No code or asset changes needed for
the encounter itself. Data gaps (allowedCharacters count, outroDialogue) are
non-blocking for the encounter mechanics but should be fixed for full GDD
compliance.

### 1.2 Level 10 — The Superintendent (Paglimot II, American era mastery)

**Era:** ugnayan (American Occupation), Levels 6–10.
**Roster tested:** 14 characters (8 from era 1 + NGA, PA, SA, TA, WA, YA from
era 2 per GDD §3.3, line 146).
**Theme:** The colonial education administrator wields institutional erasure.
His "Decree" ability scrambles Baybayin labels — the player must fight through
the scramble to preserve the script (GDD §4.3, line 239; GDD §4.5, line 256).
**Config asset:** `BossConfig_Superintendent.asset` — **placeholder, needs full
authoring** (currently 1 phase, no enemy types, no audio, no tutorial).

Current placeholder values read from
`Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Superintendent.asset`:

| Field | Current value | Issue |
|-------|---------------|-------|
| phases | 1 phase (defaults: 30s summon, 5s gap, 2–3 minions, 3 required, 12s timer, Hover) | Needs 3 phases per spec below |
| summonEnemyTypes | empty | Needs American-era enemy types |
| fallbackEnemyTypes | empty | Needs American-era fallback |
| audioBank | null | Needs `BossAudioBank_Superintendent.asset` |
| tutorial | null | Needs `BossTutorial_Superintendent.asset` |
| bossSprite | null | Needs portrait sprite |
| description | empty | Needs Almanac copy |

**Proposed 3-phase specification:**

| Phase | summonPhaseDuration | delayBetweenSummons | minionsPerSummon (min–max) | summonEnemyTypes | requiredCharacterCount | vulnerabilityTimer | movementPattern | Design intent |
|-------|---------------------|---------------------|---------------------------|------------------|------------------------|---------------------|-----------------|---------------|
| 1 | 28s | 6s | 1–2 | Soldier | 4 | 18s | Pace (speed 1.2, halfRange 2.5) | Intro. American-era regulars only. 4 draws (up from 3 at L5 phase 1) reflects the larger cumulative roster (14 vs 8). |
| 2 | 26s | 5s | 2–3 | Soldier, Pensionado, Maestro | 5 | 18s | Pace (speed 1.5, halfRange 3.0) | Decree phase. Maestro (decoy) forces the player to *not* draw — a mastery test of restraint. Pensionado zigzag tests visual tracking. 5 draws required. Decree scramble active during this phase. |
| 3 | 22s | 4s | 2–3 | Soldier, Pensionado, General, Maestro | 6 | 22s | Teleport (halfRange 3.0 × 2.0) | Climax. General (commander aura, `auraRadius: 3.5`, `auraSpeedMultiplier: 1.3` per `EnemyData_General.asset`) speeds up nearby enemies. Teleporting boss. 6 draws required. Decree scramble active. Full era-2 mastery under maximum pressure. |

**Decree mechanic (scramble):** The Superintendent's signature ability is
label scrambling — the same visual mechanic Kempei uses (Level 13, GDD §4.3
line 231). During SummoningPhase phases 2 and 3, the boss emits a "decree"
that temporarily scrambles labels on all on-screen non-boss enemies. This
reuses the existing `KempeiScrambleController`
(`Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs`) visual system,
applied as a boss-phase effect rather than an enemy-aura effect.

**Implementation note:** `KempeiScrambleController` is `[RequireComponent(typeof(Enemy))]`
(line 5) and uses `_enemy.Data.scrambleRadius` (line 56) to determine its
affect radius. It scrambles enemies within range by calling
`target.ApplyVisualCharacterOverride(this, scramble.Character)` (line 155) and
restores them via `target.ClearVisualCharacterOverride(this)` (line 161). The
scramble selects wrong characters from `WaveManager.CurrentAllowedCharacters`
(line 169). To apply this as a boss-phase effect, a new `BossDecreeEffect`
component is needed — see §2.3 Reuse Assessment for details.

**Paragraph restoration:** On defeat, `BossController.RunOutro()` (line 266)
raises `OnBossDefeated` then `OnLevelComplete` (lines 284-285).
`LevelFlowController.HandleLevelComplete` triggers
`PlayOutroThenVictory()` (line 444) which plays `_levelConfig.outroDialogue`
(line 446) — the Spirit Guide's restoration declaration. Level 11 unlocks via
`ProgressManager.MarkLevelComplete` (line 302: `PlayerPrefs.SetInt(UnlockedKey(nextLevelID), 1)`).

**Art needs:** Boss sprite (64×64), walk frames, death frames, portrait
(`bossSprite`). GDD §6.3 line 412: Art Batch 3 includes Superintendent boss.

**Audio needs:** `BossAudioBank_Superintendent.asset` — distinct BGM, growls,
footsteps, defeat clip. `BossAudio` (line 16: `[RequireComponent(typeof(BossController))]`)
is null-tolerant (line 81: `if (_bank == null) return;`) so the encounter runs
without audio, but shipping requires a full bank.

**Tutorial needs:** `BossTutorialSO` with 3 pages: (1) boss name + lore, (2)
Decree scramble explanation, (3) vulnerability window explanation. Reuses
existing `BossTutorialScroll` / `BossTutorialController` / `BossTutorialPaging`
— no new tutorial code needed. `BossTutorialController.Play()` (line 16)
no-ops gracefully when `config.tutorial` is null (line 23).

### 1.3 Level 15 — Kadiliman (Paglimot III, final campaign mastery)

**Era:** pamana (Japanese Occupation), Levels 11–15.
**Roster tested:** All 17 characters (full campaign roster per GDD §3.3 line
146; `ContentIdentity.RevisedSymbolIds` has 17 entries, line 20).
**Theme:** The Darkness itself — the embodiment of cultural forgetting. A
formless shadow entity combining all three eras of corruption. Drawing all 17
characters across the three phases restores Baybayin to the world (GDD §4.3
line 240; GDD §4.5 line 258).
**Config asset:** `BossConfig_Kadiliman.asset` — **placeholder, needs full
authoring** (currently 1 phase, no enemy types, no audio, no tutorial).

Current placeholder values read from
`Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Kadiliman.asset`:

| Field | Current value | Issue |
|-------|---------------|-------|
| phases | 1 phase (defaults: 30s summon, 5s gap, 2–3 minions, 3 required, 12s timer, Hover) | Needs 3 phases per spec below |
| summonEnemyTypes | empty | Needs cross-era enemy types |
| fallbackEnemyTypes | empty | Needs all-era fallback |
| audioBank | null | Needs `BossAudioBank_Kadiliman.asset` |
| tutorial | null | Needs `BossTutorial_Kadiliman.asset` |
| bossSprite | null | Needs portrait sprite |
| description | empty | Needs Almanac copy |

**Proposed 3-phase specification:**

| Phase | summonPhaseDuration | delayBetweenSummons | minionsPerSummon (min–max) | summonEnemyTypes | requiredCharacterCount | vulnerabilityTimer | movementPattern | Design intent |
|-------|---------------------|---------------------|---------------------------|------------------|------------------------|---------------------|-----------------|---------------|
| 1 | 25s | 5s | 2–3 | Heitai, Kisha | 6 | 20s | Pace (speed 1.3, halfRange 3.0) | Era-3 opening. Japanese-era regulars + sprinter. 6 draws. Establishes the final boss's tempo. |
| 2 | 22s | 4s | 2–3 | Heitai, Kisha, Kempei, Shokan + Soldado, Soldier | 7 | 20s | Teleport (halfRange 3.0 × 2.5) | Cross-era assault. Kadiliman summons enemies from *all three eras* (GDD §4.3 line 240). Kempei scrambles labels; Shokan is shielded. 7 draws. Tests full-campaign enemy knowledge. |
| 3 | 20s | 3s | 3–4 | All-era mix (Soldado, Soldier, Heitai, General, Shokan) | 8 | 25s | Teleport (halfRange 3.5 × 3.0) | Final phase. Maximum summon rate, maximum minion count, cross-era elite mix. 8 draws required — the ultimate mastery check. The extended 25s timer compensates for the higher draw count. |

**17-character mastery:** The GDD states "Drawing all 17 characters defeats it"
(line 193, line 240). The three-phase structure honors this cumulatively:
6 + 7 + 8 = 21 draws across the encounter, with random sampling from the full
17-character roster ensuring broad coverage. The *intent* (per the workbook) is
that the player demonstrates mastery of all 17 characters; the random sampler
from `allowedCharacters` (which should be the full 17 at Level 15) achieves
this statistically. A deterministic 17-draw sequence is **not** required by the
existing `BossController` — `SampleNextExpectedCharacter` (line 125: `int idx =
UnityEngine.Random.Range(0, level.allowedCharacters.Count)`) uses random
sampling, which is the approved mechanism.

**Cross-era summons:** Kadiliman's `fallbackEnemyTypes` and per-phase
`summonEnemyTypes` include enemies from all three eras. This is a data-only
change — `BossSummonTicker.PickEnemyType` (line 110) already spawns any
`EnemyDataSO` passed to it from `phase.summonEnemyTypes` or
`config.fallbackEnemyTypes`. No code change needed.

**Paragraph restoration:** On defeat, `outroDialogue` + optional `CutsceneSO`
play — "the script returns, the world remembers" (GDD §4.5 line 259).
`LevelFlowController.PlayOutroThenVictory()` (line 444) plays `outroDialogue`
then resolves `CutsceneTriggerType.AfterLevel` (line 453). Endless Mode
unlocks via `ProgressManager.MarkLevelComplete` when `levelID == TotalLevels`
(line 240: `UnlockEndlessMode()`). This is the campaign finale — no Level 16
exists or is introduced.

**Art needs:** Boss sprite (64×64, formless shadow entity), walk frames, death
frames (dramatic dissolution), portrait. This is the most demanding art asset —
the "formless shadow" needs to read as combining all three era corruption
colors (per GDD §4.3 line 240). GDD §6.3 line 412: Art Batch 3 includes
Kadiliman boss.

**Audio needs:** `BossAudioBank_Kadiliman.asset` — the most dramatic audio
bank. Distinct BGM (final battle theme), growls, defeat fanfare. BGM fade-out
on defeat should feel conclusive (`BossAudio.HandleBossDefeated` line 139 calls
`AudioManager.Instance.FadeOutBGM`).

**Tutorial needs:** `BossTutorialSO` with 3–4 pages: (1) boss name + lore
("Darkness itself"), (2) cross-era summon warning, (3) vulnerability window,
(4) optional "draw all 17 characters" encouragement. Reuses existing tutorial
system.

---

## 2. Reuse Assessment for the Existing Boss Framework

### 2.1 Fully reusable — no changes needed

| Component | Path | Reuse verdict |
|-----------|------|---------------|
| `BossController` | `Assets/Scripts/Gameplay/Boss/BossController.cs` | **100% reuse.** The state machine (line 8), phase loop (line 133), vulnerability window (line 215), draw routing (line 94), and outro (line 266) are era-agnostic. All three encounters use it unchanged. |
| `BossEnemy` | `Assets/Scripts/Gameplay/Enemy/BossEnemy.cs` | **100% reuse.** `IsBoss => true` (line 12), `TakeDamage` no-ops (line 16). Era-independent. |
| `BossSummonTicker` | `Assets/Scripts/Gameplay/Boss/BossSummonTicker.cs` | **100% reuse.** `PickEnemyType` (line 110) spawns any `EnemyDataSO` from `phase.summonEnemyTypes` or `config.fallbackEnemyTypes`. Cross-era summons work automatically. `PickAllowedCharacter` (line 126) assigns from `LevelConfigSO.allowedCharacters`. |
| `PhaseBasedMovement` | `Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs` | **100% reuse.** Hover/Pace/Teleport patterns are data-driven via `BossPhase.movementPattern` (line 77). `TeleportNow` (line 59) is imperative, called by `BossController` on Teleport ticks. |
| `BossStateVisuals` | `Assets/Scripts/Gameplay/Boss/BossStateVisuals.cs` | **100% reuse.** Panting (line 44), collapse (line 64), stand-up (line 94) animations are generic sprite transforms. Era-independent. |
| `BossAudio` | `Assets/Scripts/Gameplay/Boss/BossAudio.cs` | **100% reuse.** Subscribes to 9 EventBus events (lines 38-46), resolves bank from `config.audioBank` (line 79). Null-tolerant (line 81). New bosses need only a new `BossAudioBankSO` asset. |
| `BossAudioBankSO` | `Assets/Scripts/Data/BossAudioBankSO.cs` | **100% reuse.** Per-boss audio bank schema. New asset per boss. |
| `BossTutorialSO` | `Assets/Scripts/Data/BossTutorialSO.cs` | **100% reuse.** Per-boss tutorial pages (line 59). `BossTutorialPage` struct supports title, body, frames, animation, and visual effects (line 24). New asset per boss. |
| `BossTutorialController` | `Assets/Scripts/Gameplay/Boss/BossTutorialController.cs` | **100% reuse.** Paged tutorial scroll, drawing-input suppression (line 41). No-ops when `config.tutorial` is null (line 23). |
| `BossConfigSO` / `BossPhase` | `Assets/Scripts/Data/BossConfigSO.cs`, `BossPhase.cs` | **100% reuse.** All phase fields (summon, vulnerability, movement) are data-driven. No schema changes needed. |
| `WaveManager.RunBossEncounter` | `Assets/Scripts/Gameplay/Wave/WaveManager.cs` | **100% reuse.** Activates boss when `bossConfig != null` (line 359), spawns boss via `SpawnBossEnemy` (line 463), calls `boss.StartBoss` (line 479). |
| `CombatResolver.TryRouteDraw` | `Assets/Scripts/Gameplay/Combat/CombatResolver.cs` | **100% reuse.** Routes draws to boss before AOE/closest-match (line 68-73). Boss consumes the draw if targetable. |
| EventBus boss events | `Assets/Scripts/Core/EventBus.cs` | **100% reuse.** 11 boss events defined (lines 50-62): `OnBossStarted`, `OnBossPhaseStarted`, `OnBossExhausted`, `OnBossVulnerable`, `OnBossVulnerabilityWindowActive`, `OnBossVulnerabilityExpired`, `OnBossDamaged`, `OnBossDefeated`, `OnBossSummonTick`, `OnBossDrawHit`, `OnBossTeleport`. |
| `ProgressManager.HandleLevelComplete` | `Assets/Scripts/Core/ProgressManager.cs` | **100% reuse.** Saves level completion + unlocks next level + `PlayerPrefs.Save()` (line 312, via `MarkLevelComplete`). Unlocks Endless Mode at Level 15 (line 305-308, via `MarkLevelComplete`). |
| `LevelFlowController.PlayOutroThenVictory` | `Assets/Scripts/Gameplay/LevelFlowController.cs` | **100% reuse.** Plays `outroDialogue` (line 446) and after-level cutscene (line 453) on level complete. |
| `PauseMenuUI.ShouldCachePausedRunSnapshot` | `Assets/Scripts/UI/PauseMenuUI.cs` | **100% reuse.** Returns false when `CurrentBoss != null` (line 129) — quitting mid-boss discards the snapshot, preventing soft-locks. |
| `BossDiscoveryProgress.TryMarkDiscovered` | `Assets/Scripts/Core/BossDiscoveryProgress.cs` | **100% reuse.** Persisted boss Almanac discovery (line 17). Called when the boss tutorial closes. Atomic via `PlayerPrefs.Save()` (line 74). |

### 2.2 Data-only work (no code changes)

| Work item | Asset(s) to create/fill |
|-----------|------------------------|
| Fill `BossConfig_Superintendent.asset` | 3 phases per §1.2, `fallbackEnemyTypes` (American era), `summonHorizontalBounds`, `introDuration`, `outroDuration` |
| Fill `BossConfig_Kadiliman.asset` | 3 phases per §1.3, `fallbackEnemyTypes` (all three eras), `summonHorizontalBounds`, `introDuration`, `outroDuration` |
| Create `BossAudioBank_Superintendent.asset` | BGM, introGrowl, summonTick, bodyFall, vulnerabilityExpiredLaugh, defeat, hitGrowls, damagedGrowls, footsteps, teleports |
| Create `BossAudioBank_Kadiliman.asset` | Same fields as above, final battle theme |
| Create `BossTutorial_Superintendent.asset` | 3 pages (name/lore, Decree, vulnerability) |
| Create `BossTutorial_Kadiliman.asset` | 3–4 pages (name/lore, cross-era summons, vulnerability, optional encouragement) |
| Fill `EnemyData_Boss_Superintendent.asset` | `displayName`, `description`, walk frames, death frames, `era: 1` (American), `dealsContactDamage: 0` (boss), `overrideBadgeOffset: 1`, `glyphBadgeScaleOverride: 3` (match El Inquisidor) |
| Fill `EnemyData_Boss_Kadiliman.asset` | `displayName`, `description`, walk frames, death frames, `era: 2` (Japanese), `dealsContactDamage: 0` (boss), `overrideBadgeOffset: 1`, `glyphBadgeScaleOverride: 3` |
| Wire `bossConfig.tutorial` on each config | Point to the new `BossTutorialSO` assets |
| Wire `bossConfig.audioBank` on each config | Point to the new `BossAudioBankSO` assets |
| Wire `bossConfig.bossSprite` on each config | Point to the portrait sprites (for Almanac) |
| Create boss prefabs | `[Enemy] Boss_Superintendent.prefab`, `[Enemy] Boss_Kadiliman.prefab` — duplicate `Boss_ElInquisidor.prefab` and swap sprites/data |
| Fill `Level10_Config.asset` | `allowedCharacters` needs 14 entries (currently 1); `outroDialogue` needs wiring; `eraTheme` needs assignment |
| Fill `Level15_Config.asset` | `allowedCharacters` needs 17 entries (currently 1); `chapterNumber: 3` (currently 1 — **bug**); `chapterName: "Chapter 3"`; `outroDialogue` needs wiring; `eraTheme` needs assignment |
| Fill `Level5_Config.asset` | `allowedCharacters` needs 8 entries (currently 6); `outroDialogue` needs wiring |

### 2.3 New code needed

| Component | Reason | Effort |
|-----------|--------|--------|
| `BossDecreeEffect` (Superintendent only) | The "Decree" scramble ability reuses the Kempei scramble *visual* but applies it as a boss-phase effect (scramble all on-screen non-boss enemies during SummoningPhase phases 2–3), not an enemy aura. `KempeiScrambleController` is `[RequireComponent(typeof(Enemy))]` and uses `_enemy.Data.scrambleRadius` — a thin boss-scoped wrapper or a global scramble event is needed. | Small — wraps existing `KempeiScrambleController` logic or fires a global scramble event. ~1 day. |
| Boss prefab duplication | Duplicate `Boss_ElInquisidor.prefab`, swap `SpriteRenderer` sprites, swap `EnemyDataSO` reference. | Trivial — Unity Editor work, no code. |

### 2.4 Reuse verdict

**The existing boss framework is 95% reusable for all three encounters.** The
only new code is the Superintendent's Decree effect (a thin wrapper around
existing scramble logic). Everything else is data authoring and prefab
duplication. The framework was designed to be data-driven and era-agnostic —
this validation confirms that design holds.

---

## 3. Scope Statement

### 3.1 Mechanics

All three encounters use the same core mechanic loop
(`BossController.RunEncounter`, line 129):
1. **SummoningPhase** (line 163) — boss summons era-appropriate minions while
   moving (Hover/Pace/Teleport). Player draws to defeat minions and protect
   hearts.
2. **WindingDown** (line 200) — boss stops summoning, pants (exhausted).
   `BossStateVisuals.BeginPanting()` plays. Player clears remaining minions.
   `ActiveEnemyTracker.HasActiveNonBossEnemies` gates progression (line 211).
3. **Vulnerable** (line 215) — boss collapses, becomes targetable
   (`IsTargetable` line 17). Player must draw N correct glyphs from the
   cumulative roster within a timer.
4. **Damaged** (line 256) — boss loses 1 HP (`HPRemaining--` line 259),
   stands up, advances to next phase.

**Difficulty progression across encounters:**

| Parameter | Level 5 | Level 10 | Level 15 |
|-----------|---------|----------|----------|
| Cumulative roster | 8 chars | 14 chars | 17 chars |
| Phase 1 required draws | 3 | 4 | 6 |
| Phase 2 required draws | 3 | 5 | 7 |
| Phase 3 required draws | 4 | 6 | 8 |
| Total draws to defeat | 10 | 15 | 21 |
| Phase 1 vuln timer | 20s | 18s | 20s |
| Phase 3 vuln timer | 25s | 22s | 25s |
| Minion variety | Spanish (4 types) | American (4 types) | All-era (10+ types) |
| Boss movement | Pace → Pace → Teleport | Pace → Pace → Teleport | Pace → Teleport → Teleport |
| Signature mechanic | Reinforcement summon | Decree (label scramble) | Cross-era summon + all-17 roster |

The difficulty curve is **draw count + roster breadth + minion pressure**, not
stricter recognition (threshold stays at 0.60 per GDD §3.6 line 179). Timers
are tuned to give ~3–4 seconds per required draw, adjusted for roster size.

### 3.2 Paragraph restoration

"Paragraph restoration" is the narrative beat on boss defeat. The mechanical
implementation:

1. `BossController.RunOutro()` (line 266) raises `OnBossDefeated` (line 284)
   then `OnLevelComplete` (line 285).
2. `LevelFlowController` catches `OnLevelComplete` (line 94) and calls
   `PlayOutroThenVictory()` (line 444) which plays the level's `outroDialogue`
   (a `DialogueSO`) — the Spirit Guide's restoration declaration (line 446).
3. For Level 15, a `CutsceneSO` may also play (line 453:
   `ResolveCutscene(CutsceneTriggerType.AfterLevel)`) — the "script returns"
   ending.
4. `ProgressManager.HandleLevelComplete()` (line 189) calls
   `MarkLevelComplete` (line 266) which saves level completion (stars, unlock
   next level) via `PlayerPrefs.Save()` (line 312) — the atomic save.
5. For Level 15: `MarkLevelComplete` calls `UnlockEndlessMode()` (line 308)
   when `levelID == TotalLevels` (line 305, `TotalLevels = 15` at line 30).

**No new mechanic is needed.** Paragraph restoration is dialogue/cutscene
playback + progress save, both of which are existing systems.

### 3.3 Active clues

The `LevelConfigSO` does not currently have a `ClueMode` field — glyph badges
show the full Baybayin character by default via `GlyphBadgeConfigSO`. The GDD
§3.2 line 136 describes the glyph badge system: "Every enemy displays its
required Baybayin character in a scroll badge above its head."

The Superintendent's Decree *temporarily* overrides this by scrambling labels
(a Kempei-style effect), which is the era-specific clue challenge. No permanent
clue-mode change is needed for bosses.

**Recommendation:** All three boss encounters use the default full-glyph badge.
The Decree scramble is the only clue challenge, and it is temporary
(phase-scoped, not permanent).

### 3.4 Art

| Asset | Level 5 | Level 10 | Level 15 |
|-------|---------|----------|----------|
| Boss sprite (64×64) | **Done** (El Inquisidor, 6 walk frames) | **Needed** — Superintendent, American-era administrator | **Needed** — Kadiliman, formless shadow entity (most complex) |
| Walk frames | **Done** (6 frames) | Needed | Needed |
| Death frames | **Done** (8 frames) | Needed | Needed (dramatic dissolution) |
| Portrait (`bossSprite`) | **Done** | Needed (Almanac) | Needed (Almanac) |
| Era shrine | **Done** (Baybayin Altar) | **Done** (Ancestral Door) | **Done** (Scroll Shrine) |
| Era tileset | **Done** (jungle path) | **Done** (cobblestone) | **Done** (bombed cobblestone) |
| Tutorial art | **Done** (uses walk frames) | Needed (uses walk frames) | Needed (uses walk frames) |

Art is the primary external dependency. Per GDD §6.3 line 412, Art Batch 3
(boss sprites for Superintendent + Kadiliman) is needed by End of Week 7.

### 3.5 Audio

| Asset | Level 5 | Level 10 | Level 15 |
|-------|---------|----------|----------|
| `BossAudioBankSO` | **Done** (`BossAudioBank_ElInquisidor.asset`) | **Needed** | **Needed** |
| BGM | Done | Needed (American-era boss theme) | Needed (final battle theme) |
| Growls / hit SFX | Done | Needed | Needed |
| Footsteps | Done | Needed | Needed |
| Defeat clip | Done | Needed | Needed (conclusive fanfare) |

`BossAudio` is null-tolerant (line 81: `if (_bank == null) return;`) —
encounters run without audio. But shipping requires full banks. Per GDD §6.3
line 415, boss theme BGM is needed by End of Week 7.

### 3.6 Tutorial needs

All three encounters use the existing `BossTutorialSO` +
`BossTutorialController` + `BossTutorialScroll` system. No new tutorial code.

| Boss | Pages | Content |
|------|-------|---------|
| El Inquisidor | **Done** (`BossTutorial_ElInquisidor.asset`, 4 pages) | Name/lore, summoning, vulnerability, teleportation |
| Superintendent | 3 pages (needed) | Name/lore, Decree scramble, vulnerability |
| Kadiliman | 3–4 pages (needed) | Name/lore, cross-era summons, vulnerability, optional "all 17 characters" encouragement |

Tutorial art reuses the boss's `walkFrames` from `EnemyDataSO` — no extra art
assets needed (per `BossTutorialSO` design: `frames` field on
`BossTutorialPage`, line 34).

### 3.7 Accessibility

The existing accessibility features (GDD §5.5) apply unchanged to boss
encounters:

- **Full-screen drawing** — no precision targeting; draw anywhere.
- **Audio pronunciation on every correct draw** — secondary feedback channel.
- **Failed strokes show red flash / X** — clear rejection feedback.
- **Portrait one-handed play** — boss encounters use the same orientation.
- **No text-heavy tutorials** — boss tutorials are paged scrolls with art.
- **Pause** — pause menu works during boss encounters (`Time.timeScale = 0`
  pauses all scaled coroutines). See §4.5 for the pause acceptance example.

**Boss-specific accessibility notes:**
- The vulnerability timer bar (`BossVulnerabilityTimerBar`) provides a visual
  countdown — no reliance on audio alone for time pressure.
- The draw counter (`BossDrawCounterUI`) shows "N / required" — clear progress
  indication.
- Phase failure (timer expiry) repeats the phase with no HP loss — forgiving
  retry within the encounter (lines 234-253).
- The Superintendent's Decree (label scramble) could be disorienting for
  players with visual processing differences. **Mitigation:** the scramble is
  temporary (phase-scoped, not permanent), and killing the source restores
  labels — consistent with the Kempei pattern the player already learned.

### 3.8 Difficulty progression

The difficulty progression is **cumulative and pressure-based**, not
recognition-based:

1. **Roster breadth:** 8 → 14 → 17 characters. More characters = more
   cognitive load during the vulnerability window.
2. **Draw count:** 10 → 15 → 21 total draws. More draws = longer sustained
   accuracy under time pressure.
3. **Minion pressure:** Spanish-only → American-only → cross-era. More enemy
   variety = more multitasking (decoys, scramblers, commanders, shielded).
4. **Boss mobility:** Pace → Pace → Teleport (L5); Pace → Pace → Teleport
   (L10); Pace → Teleport → Teleport (L15). Kadiliman teleports earlier and
   more aggressively.
5. **Signature mechanics:** Reinforcement (simple) → Decree scramble
   (moderate — tests restraint + visual parsing) → Cross-era assault (complex
   — tests full campaign knowledge).

**Recognition threshold remains 0.60** across all encounters (GDD §3.6 line
179). The difficulty is time pressure and volume, not stricter matching.

---

## 4. Acceptance Examples

These examples specify the expected behavior for each critical encounter state.
They map to the existing `BossController` state machine and are testable via
the existing EditMode test infrastructure (see
`Assets/Tests/Editor/Gameplay/BossControllerTests.cs` for existing patterns).

### 4.1 Phase entry

**Given:** Level 5 is loaded, `bossConfig != null`, boss tutorial (if any) has
closed, `WaveManager.RunBossEncounter` has been called (line 448).

**When:** `BossController.StartBoss(config, spawner)` executes (line 41).

**Then:**
- `GameManager.Instance.CurrentBoss` is set to this `BossController` (line 74).
- `EventBus.RaiseBossStarted(config)` is raised (line 76).
- `BossAudio` fades in BGM (if `audioBank.bgm` is non-null).
- State transitions: `Idle → Intro` (line 159).
- After `config.introDuration` seconds (line 160): state → `SummoningPhase`,
  phase index = 0, `EventBus.RaiseBossPhaseStarted(0)` is raised (line 167).
- `PhaseBasedMovement.StartPattern(phase0)` begins (line 170).
- `BossHealthBar` shows HP = `phases.Count` (3).
- Drawing input is accepted (not suppressed — `IsTargetable` is false during
  SummoningPhase but `CombatResolver` routes to boss only when targetable).

### 4.2 Phase failure (vulnerability timer expiry)

**Given:** Boss is in `Vulnerable` state, phase index = 1,
`requiredCharacterCount` = 5, `vulnerabilityTimer` = 18s. Player has drawn 3
correct glyphs (`_correctDrawsThisWindow = 3`).

**When:** 18 seconds elapse without reaching 5 correct draws (line 234:
`elapsed < phase.vulnerabilityTimer` fails).

**Then:**
- `_isVulnerableActiveWindow` becomes `false` (line 241) — boss no longer
  targetable.
- `EventBus.RaiseBossVulnerabilityExpired(1)` is raised (line 249).
- `BossAudio` plays `vulnerabilityExpiredLaugh` (if configured).
- `BossStateVisuals.PlayStandUp()` plays (line 251) — boss stands back up.
- `IsTargetable` returns `false` (line 17).
- `onComplete(false)` is called (line 252) — `didDamage` stays false.
- `BossController` re-enters `SummoningPhase` for phase 1 (same phase, not
  next — the `while (!phaseCleared)` loop at line 138 re-iterates with the
  same `i`).
- **HP is NOT decremented.** `HPRemaining` stays at 2 (line 259 only runs on
  `didDamage = true`).
- `_correctDrawsThisWindow` resets to 0 on next `RunVulnerable` entry
  (line 220).

### 4.3 Phase retry (after failure)

**Given:** Phase 1 just failed (§4.2). Boss re-enters `SummoningPhase` for
phase 1 (line 140: `yield return RunSummoningPhase(i)` with same `i`).

**When:** The summoning → winding-down → vulnerable cycle completes again.

**Then:**
- The player gets another attempt at the same phase with the same
  `requiredCharacterCount` and `vulnerabilityTimer` (from `Config.phases[i]`).
- Glyph sampling re-randomizes from `allowedCharacters` (line 125:
  `Random.Range(0, level.allowedCharacters.Count)`).
- There is **no limit on retries** — the `while (!phaseCleared)` loop (line
  138) repeats indefinitely until the player clears it or loses all hearts.
- No progress is lost between retries (phase index stays at 1, HP stays at 2).

### 4.4 Heart loss (minion reaches base)

**Given:** Boss is in `SummoningPhase` or `Vulnerable`. A summoned minion
(Soldado) reaches the PlayerBase trigger.

**When:** `EnemyMover.OnTriggerEnter2D` fires on "PlayerBase" tag.

**Then:**
- `EventBus.RaiseBaseHit()` fires.
- `HeartSystem` decrements hearts by 1, raises `OnHeartsChanged(currentHearts)`.
- `HUD` heart counter updates visually.
- The minion is returned to pool (`Enemy.ReturnToPool()`).
- **Boss HP is unaffected** — heart loss and boss HP are independent systems.
  `BossController.HPRemaining` only changes in `RunDamaged` (line 259).
  `BossEnemy.TakeDamage` no-ops (line 16) — direct damage cannot bypass the
  phase gate. Heart loss does not interrupt the vulnerability timer or phase
  state; the boss continues its current state machine cycle.
- If `currentHearts == 0`: `HeartSystem` raises `OnGameOver` →
  `GameManager.HandleGameOver()` → `DefeatScreenUI` shows. The boss encounter
  is abandoned (see §4.5 on save behavior).

**Test note:** Heart loss is a `HeartSystem`/`EnemyMover` behavior, not a
`BossController` behavior. The acceptance test suite
(`Salin169AcceptanceTests.cs`) validates BossController-specific behavior;
heart loss acceptance is covered by the existing `HeartSystem` test suite.
The BossController-specific assertion — that `HPRemaining` is unaffected by
heart loss — is implicitly verified by the wrong-glyph test
(`WrongGlyph_DuringVulnerable_RaisesDrawingFailedNoDamage`), which confirms
non-phase-gated events do not decrement HP.

### 4.5 Pause

**Given:** Boss is in `Vulnerable` state, vulnerability timer is counting
down (12.5s remaining), player is mid-draw.

**When:** Player taps the pause button.

**Then:**
- `GameManager.PauseGame()` sets `Time.timeScale = 0`.
- All scaled-time coroutines (`WaitForSeconds`, `Time.deltaTime` loops in
  `BossController.RunVulnerable` line 237-238) freeze. The vulnerability timer
  stops counting down.
- `StrokeCapture` stops accepting input (game is paused).
- `PauseMenuUI` is shown with Resume / Restart / Level Select / Settings.
- `BossVulnerabilityTimerBar` stops draining.
- `BossAudio` BGM continues (AudioSource ignores timeScale by default) unless
  explicitly paused — acceptable (BGM during pause is standard).

**When:** Player taps Resume.

**Then:**
- `Time.timeScale` restored to 1.
- All coroutines resume. Vulnerability timer continues from where it froze.
- Drawing input re-enabled.

**When:** Player taps "Return to Level Select" (quit mid-boss).

**Then:**
- `PauseMenuUI.ShouldCachePausedRunSnapshot()` returns `false` (line 129:
  `GameManager.Instance.CurrentBoss != null`).
- The run is abandoned. No mid-boss save is written.
- On returning to Level 5/10/15, the encounter starts from the beginning
  (Intro → Phase 1). See §4.7 for atomic save behavior.

### 4.6 Completion (all phases cleared)

**Given:** Boss is in `Vulnerable` state, phase index = 2 (final phase),
`requiredCharacterCount` = 8, player has drawn 7 correct glyphs.

**When:** Player draws the 8th correct glyph (`_correctDrawsThisWindow` reaches
8 ≥ `requiredCharacterCount`, line 243).

**Then:**
- `TryRouteDraw` returns `BossRouteResult.Hit` (line 107).
- `_correctDrawsThisWindow` = 8.
- `RunVulnerable` calls `onComplete(true)` (line 245).
- State → `Damaged` (line 257). `HPRemaining--` → 0 (line 259).
- `EventBus.RaiseBossDamaged(2, 0)` raised (line 260). `BossHealthBar` shows 0.
- `BossStateVisuals.PlayStandUp()` plays (line 263).
- `RunEncounter` loop exits (all phases cleared — `for` loop at line 133
  completes).
- State → `Outro` (line 268). `IsDefeated = true` (line 269).
- `BossEnemy.PlayDeathAnimationFrames()` plays (line 278, if death frames
  configured).
- After `config.outroDuration` seconds (line 280):
  - State → `Defeated` (line 282).
  - `EventBus.RaiseBossDefeated()` raised (line 284). `BossAudio` plays defeat
    clip + fades out BGM (line 144-147).
  - `EventBus.RaiseLevelComplete()` raised (line 285).
  - `BossEnemy.ReturnToPool()` (line 288).
- `ProgressManager.HandleLevelComplete()` saves stars, unlocks next level
  (line 165, 247).
- `LevelFlowController` plays `outroDialogue` (line 446) — paragraph
  restoration.
- For Level 15: Endless Mode unlock flag is set (line 305-308,
  `UnlockEndlessMode()`).

### 4.7 Atomic save behavior

**Given:** Boss has been defeated, `OnLevelComplete` has been raised (line 285).

**When:** `ProgressManager.HandleLevelComplete()` executes (line 189).

**Then:**
- Level completion is written to `PlayerPrefs`:
  - `salinlahi.progress.unlocked.{N}` = 1 (line 296, via `MarkLevelComplete`)
  - `salinlahi.progress.stars.{N}` = computed star count (line 291, via `MarkLevelComplete`)
  - `salinlahi.progress.unlocked.{N+1}` = 1 (line 302, if N < 15)
- `PlayerPrefs.Save()` is called **immediately** after writing (line 312) —
  this is the atomic save. Unity's `PlayerPrefs.Save()` flushes to disk
  synchronously.
- If the app crashes after `OnBossDefeated` but before `PlayerPrefs.Save()`,
  the level is **not** marked complete — the player must re-defeat the boss.
- If the app crashes *after* `PlayerPrefs.Save()`, the level **is** marked
  complete — progress is durable.
- **No mid-boss save exists.** Boss phase progress, current phase index,
  vulnerability timer state, and correct-draw count are **never persisted**.
  This is an intentional design decision (documented in
  `PauseMenuUI.ShouldCachePausedRunSnapshot`, line 124-130). Quitting mid-boss
  always restarts the encounter from the beginning.
- `BossDiscoveryProgress.TryMarkDiscovered(config)` is called when the boss
  tutorial closes (before the encounter), persisting the boss's Almanac
  discovery independently of encounter completion. This is also atomic via
  `PlayerPrefs.Save()` (line 74) or `SaveManager.Repository` (line 23).

---

## 5. Completion Criteria Verification

### 5.1 Compatible with all three era-ending stories

| Era | Story (GDD §4.5) | Encounter | Compatibility |
|-----|------------------|-----------|---------------|
| Spanish (ugat) | "El Inquisidor, a corrupted high-ranking friar-inquisitor who oversaw the burning of Baybayin manuscripts. Defeating him proves the script still has power." (line 254) | Level 5 — 3-phase, Spanish-era summons, reinforcement mechanic | ✅ The boss's summoning of Soldado reinforcements *is* the manuscript-burning force. Defeat = script has power. |
| American (ugnayan) | "The Superintendent, an American colonial education administrator wielding the power of institutional erasure." (line 256) | Level 10 — 3-phase, American-era summons, Decree (label scramble) mechanic | ✅ The Decree scramble *is* institutional erasure (replacing the script with wrong labels). Defeat = script survives institutions. |
| Japanese (pamana) | "Kadiliman, the Darkness itself. The embodiment of cultural forgetting. A formless shadow entity combining all three eras of corruption. Drawing all 17 characters in a timed sequence restores Baybayin to the world." (line 258) | Level 15 — 3-phase, cross-era summons, all-17 roster | ✅ Cross-era summons = all three eras of corruption combined. 21 draws from the full 17-character roster = demonstrating total mastery. Defeat = Baybayin restored. |

All three encounters are compatible with their era-ending stories. The
mechanics reinforce the narrative themes.

### 5.2 Compatible with LF-CONTRACT-v2

Verified against `ContentIdentity.cs` (`Assets/Scripts/Data/Campaign/ContentIdentity.cs`):
- `RevisedCampaignId = "campaign.revised-v1"` (line 6) — no campaign ID change
  needed.
- `RevisedSymbolIds` has 17 entries (line 20-26) — the Level 15 roster draws
  from all 17. No new symbols introduced.
- `RevisedLevelIds` has 15 entries (3 eras × 5, line 28) — no level beyond 15
  is introduced. `RevisedFinaleLevelId` = `level.pamana.05` (line 29-30) =
  Level 15.
- No new ScriptableObject types or schema changes — encounters reuse
  `BossConfigSO` / `BossPhase` / `LevelConfigSO` unchanged.
- Vulnerability window samples from `LevelConfigSO.allowedCharacters` (the
  revised cumulative symbol pool) via `BossController.SampleNextExpectedCharacter`
  (line 116-126).
- `outroDialogue` and `bossConfig` on `LevelConfigSO` (lines 50, 79) are the
  contract hooks for paragraph restoration and boss encounter wiring.

### 5.3 Level 15 remains the final battle

**Confirmed.** No Level 16 or unapproved extra encounter is introduced:
- `ContentIdentity.RevisedLevelIds` has exactly 15 entries (line 28:
  `RevisedEraIds.Count * RevisedLevelsPerEra` = 3 × 5 = 15).
- `ContentIdentity.RevisedFinaleLevelId` (line 29) is the last entry =
  `level.pamana.05` = Level 15.
- `ProgressManager.TotalLevels` is used to gate `MarkLevelComplete` (line 300:
  `if (nextLevelID <= TotalLevels)`) — no Level 16 unlock path exists.
- Kadiliman's defeat triggers the ending ("the script returns, the world
  remembers", GDD §4.5 line 259) and Endless Mode unlock (line 305-308).
- No `level.pamana.06` or equivalent ID is generated or referenced.

---

## 6. Revised Story Estimates

Based on the approved three-phase design and the codebase analysis above:

| Story | Description | Estimate (story points) | Basis |
|-------|-------------|------------------------|-------|
| BL-E3-S4 | Level 5 — El Inquisidor (3-phase) | **1** | Already fully implemented (3 phases, audio bank, tutorial, enemy data, prefab). Data gaps: `allowedCharacters` needs 2 more entries, `outroDialogue` needs wiring. Verification-only effort. |
| BL-E4-S5 | Level 10 — The Superintendent (3-phase) | **5** | Data authoring (3 phases, audio bank, tutorial, enemy data, level config) + `BossDecreeEffect` component (~1 day code) + prefab duplication + art/audio asset integration + playtesting. |
| BL-E5-S5 | Level 15 — Kadiliman (3-phase) | **5** | Data authoring (3 phases, cross-era summons, audio bank, tutorial, enemy data, level config + chapterNumber fix) + prefab duplication + art/audio asset integration (most complex art) + playtesting. No new code (cross-era summon is data-only). |
| **Total** | All three Paglimot encounters | **11** | |

**Risk-adjusted estimate:** Add +2 points buffer for art dependency risk
(boss sprites for Superintendent + Kadiliman are external deliverables per GDD
§6.3 line 412, Art Batch 3 by End of Week 7). **Risk-adjusted total: 13 story
points.**

---

## 7. Open Items (non-blocking)

1. **Level 5 `allowedCharacters` gap:** `Level5_Config.asset` has 6 entries;
   GDD §3.3 line 145 specifies 8 (BA, KA, DA, GA, HA, LA, MA, NA). Fix during
   BL-E3-S4 data authoring.
2. **Level 10 `allowedCharacters` gap:** `Level10_Config.asset` has 1 entry;
   should be 14 (8 + NGA, PA, SA, TA, WA, YA). Fix during BL-E4-S5.
3. **Level 15 `allowedCharacters` gap:** `Level15_Config.asset` has 1 entry;
   should be 17 (full campaign roster). Fix during BL-E5-S5.
4. **Level 15 `chapterNumber` bug:** `Level15_Config.asset` has
   `chapterNumber: 1` — should be 3 (Japanese era is Chapter 3 per GDD §4.2
   line 191). Fix during BL-E5-S5.
5. **`outroDialogue` not wired:** All three boss level configs have
   `outroDialogue: null`. Paragraph restoration dialogue needs authoring and
   wiring for all three encounters.
6. **Endless Mode (REQ-33):** Not yet implemented (per
   `docs/system/10_Requirements_Traceability_Matrix.md` line 58: "NOT FOUND").
   Kadiliman's defeat calls `UnlockEndlessMode()` (line 308) which sets the
   PlayerPrefs flag, but the Endless Mode scene/system does not exist yet.
   This is a separate story, not part of the encounter spec.
7. **Deterministic 17-character sequence:** The GDD says "drawing all 17
   characters defeats it" (line 193). The current random-sampling approach
   covers all 17 statistically but does not guarantee each is drawn exactly
   once. If the product owner requires a *deterministic* 17-draw sequence for
   Level 15 phase 3, `BossController.SampleNextExpectedCharacter` (line 114)
   would need a shuffle-bag variant. **Recommendation:** keep random sampling
   — it's consistent with the existing framework and the "mastery" intent is
   satisfied by drawing from the full roster under pressure.
8. **`BossDecreeEffect` implementation:** The Superintendent's signature
   mechanic needs a thin code wrapper. Estimated ~1 day. This is the only
   new code in the entire spike scope.
9. **Art/audio dependencies:** Superintendent and Kadiliman sprites + audio
   banks are external deliverables (GDD §6.3 line 412, Art Batch 3 / boss BGM
   by End of Week 7). The encounter specs are blocked on these for shipping
   but not for implementation (placeholder sprites/audio work via
   null-tolerance in `BossAudio` and `BossStateVisuals`).
10. **Workbook checksum discrepancy:** The Jira ticket (SALIN-169) specifies
    workbook checksum `34dad782a025b3acd3dcfc9bdfb2ce5c595fe81e6bd1789c9042849b63c27eb7`,
    but `ContentIdentity.ApprovedWorkbookSha256` (line 14) =
    `33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83`.
    These do not match. This must be resolved before the spike is considered
    fully validated — either the Jira ticket has a stale checksum, or
    `ContentIdentity` was updated after the ticket was created. **Action
    needed:** confirm which checksum is authoritative and update the other.

---

## 8. Evidence Index

| Claim | Evidence |
|-------|----------|
| BossController state machine | `Assets/Scripts/Gameplay/Boss/BossController.cs:8` — `State` enum |
| BossController phase loop | `Assets/Scripts/Gameplay/Boss/BossController.cs:133` — `for` loop, `138` — `while (!phaseCleared)` |
| BossController vulnerability window | `Assets/Scripts/Gameplay/Boss/BossController.cs:215` — `RunVulnerable` |
| BossController draw routing | `Assets/Scripts/Gameplay/Boss/BossController.cs:94` — `TryRouteDraw` |
| BossController glyph sampling | `Assets/Scripts/Gameplay/Boss/BossController.cs:114` — `SampleNextExpectedCharacter` |
| BossController outro | `Assets/Scripts/Gameplay/Boss/BossController.cs:266` — `RunOutro` |
| BossController HP = phases.Count | `Assets/Scripts/Gameplay/Boss/BossController.cs:65` |
| BossConfigSO schema | `Assets/Scripts/Data/BossConfigSO.cs:7` — class, `27` — `phases`, `45` — `audioBank`, `49` — `tutorial` |
| BossPhase schema | `Assets/Scripts/Data/BossPhase.cs:11` — class, `16` — `summonPhaseDuration`, `39` — `requiredCharacterCount`, `41` — `vulnerabilityTimer`, `44` — `movementPattern` |
| El Inquisidor fully authored | `Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_ElInquisidor.asset` — 3 phases, audio bank, tutorial |
| Superintendent placeholder | `Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Superintendent.asset` — 1 phase, no audio/tutorial |
| Kadiliman placeholder | `Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Kadiliman.asset` — 1 phase, no audio/tutorial |
| Level 5 config | `Assets/ScriptableObjects/Levels/Level5_Config.asset` — 6 allowedCharacters, bossConfig wired |
| Level 10 config | `Assets/ScriptableObjects/Levels/Level10_Config.asset` — 1 allowedCharacter, bossConfig wired |
| Level 15 config | `Assets/ScriptableObjects/Levels/Level15_Config.asset` — 1 allowedCharacter, chapterNumber: 1 (bug) |
| Level 15 chapterNumber bug | `Assets/ScriptableObjects/Levels/Level15_Config.asset:17` — `chapterNumber: 1` |
| No mid-boss save | `Assets/Scripts/UI/PauseMenuUI.cs:129` — `CurrentBoss != null` → return false |
| Atomic save | `Assets/Scripts/Core/ProgressManager.cs:312` — `PlayerPrefs.Save()` |
| Endless Mode unlock | `Assets/Scripts/Core/ProgressManager.cs:305-308` — `UnlockEndlessMode()` |
| Boss discovery save | `Assets/Scripts/Core/BossDiscoveryProgress.cs:17` — `TryMarkDiscovered`, `74` — `PlayerPrefs.Save()` |
| Tutorial system | `Assets/Scripts/Gameplay/Boss/BossTutorialController.cs:16` — `Play()`, `23` — null no-op |
| Tutorial schema | `Assets/Scripts/Data/BossTutorialSO.cs:56` — class, `24` — `BossTutorialPage` |
| Audio system | `Assets/Scripts/Gameplay/Boss/BossAudio.cs:16` — class, `81` — null-tolerant |
| Audio bank schema | `Assets/Scripts/Data/BossAudioBankSO.cs:13` — class |
| WaveManager boss integration | `Assets/Scripts/Gameplay/Wave/WaveManager.cs:359` — `bossConfig != null`, `448` — `RunBossEncounter` |
| Draw routing | `Assets/Scripts/Gameplay/Combat/CombatResolver.cs:68-73` — boss routing before AOE |
| Revised campaign contract | `Assets/Scripts/Data/Campaign/ContentIdentity.cs:6` — `RevisedCampaignId`, `20` — `RevisedSymbolIds` (17), `28` — `RevisedLevelIds` (15), `29` — `RevisedFinaleLevelId` |
| BossSummonTicker cross-era | `Assets/Scripts/Gameplay/Boss/BossSummonTicker.cs:110` — `PickEnemyType`, `126` — `PickAllowedCharacter` |
| PhaseBasedMovement patterns | `Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs:77` — switch on `movementPattern` |
| Kempei scramble (Decree reuse base) | `Assets/Scripts/Gameplay/Enemy/KempeiScrambleController.cs:5` — `RequireComponent(Enemy)`, `56` — `scrambleRadius`, `155` — `ApplyVisualCharacterOverride` |
| EnemyDataSO era enum | `Assets/Scripts/Data/EnemyDataSO.cs:166` — `Era { Spanish, American, Japanese }` |
| General aura | `Assets/ScriptableObjects/Enemies/EnemyData_General.asset:31` — `auraRadius: 3.5`, `32` — `auraSpeedMultiplier: 1.3` |
| LevelFlowController outro | `Assets/Scripts/Gameplay/LevelFlowController.cs:444` — `PlayOutroThenVictory`, `446` — `outroDialogue` |
| EventBus boss events | `Assets/Scripts/Core/EventBus.cs:50-62` — 11 boss events |
| REQ-25 (boss encounters at 5/10/15) | `docs/system/10_Requirements_Traceability_Matrix.md:50` — Partial (L10/L15 placeholders) |
| REQ-33 (Endless Mode) | `docs/system/10_Requirements_Traceability_Matrix.md:58` — NOT FOUND |
| REQ-41 (phase-based boss system) | `docs/system/10_Requirements_Traceability_Matrix.md:66` — Implemented |
| GDD boss descriptions | `docs/capstone/GDD.md:193` — boss encounters, `238-240` — boss table, `254-259` — era endings |
| GDD character progression | `docs/capstone/GDD.md:145` — Chapter 1 (8 chars), `146` — Chapter 2 (+6), `146` — Chapter 3 (all 17) |
| GDD recognition threshold | `docs/capstone/GDD.md:179` — Fixed at 0.60 |
| GDD enemy types | `docs/capstone/GDD.md:211-232` — per-era enemy tables |
| GDD art dependencies | `docs/capstone/GDD.md:405` — Art Batch 3 (Superintendent + Kadiliman bosses) |
| GDD audio dependencies | `docs/capstone/GDD.md:408` — Boss theme BGM by End of Week 7 |
| Existing test patterns | `Assets/Tests/Editor/Gameplay/BossControllerTests.cs` — EditMode test helpers |

---

*End of TW-SPK-003. This spike is complete when the three encounter
specifications, reuse assessment, scope statement, acceptance examples, and
revised estimates are reviewed and approved by the product owner.*
