# 08 — Enemies

Prefix **`ENM`**. Covers the corrupted-enemy roster, movement, glyph badges, introduction and discovery, and one story per signature ability.

**Design (ruling §3, 2026-09-11).** Every Baybayin symbol has one corrupted enemy created by Paglimot, embodying the opposite of that symbol's lesson. Juan defeats it by tracing the correct symbol. The model is **18 characters, 18 enemies**. An enemy keeps the same asset and abilities everywhere it appears.

**Roster as authored** (`Assets/ScriptableObjects/Enemies/`, one `EnemyDataSO` per symbol):

| Enemy | Symbol | HP | Speed | Signature flag |
|---|---|---:|---:|---|
| Abo ng Simula | A | 1 | 1.60 | `ashesFirstSlot` |
| Iligaw | E/I | 1 | 1.50 | `spawnsMirrorDecoy` + zigzag |
| Bakod | BA | 1 | 0.85 | `blocksEnemiesBehind` |
| Mantsa | MA | 1 | 1.15 | `stainsNearbyGlyphs` |
| Nawalang Mukha | NA | 1 | 1.45 | `removesNames` |
| Takip | TA | 1 | 1.30 | `coversOwnGlyph` |
| Uhaw | O/U | 2 | 1.25 | — |
| Kadena | KA | 2 | 1.05 | `chainsNearestEnemy` |
| Gapos | GA | 2 | 1.00 | — |
| Salungat | SA | 1 | 1.50 | `isDecoy` |
| Walang-Awa | WA | 3 | 0.95 | — |
| Yapos ng Dilim | YA | 3 | 1.10 | — |
| Daan-Lihis | DA | 1 | 1.50 | zigzag only |
| Ragasa | RA | 1 | 1.50 | zigzag only |
| Hati | HA | 1 | 1.40 | `splitsOnDefeat` |
| Hati minion | HA | 1 | 1.40 | (support, spawned by Hati) |
| Labo | LA | 1 | 1.35 | `isPhaser` |
| Ngatngat | NGA | 1 | 1.90 | — |
| Punit | PA | 1 | 1.70 | — |

---

## Lifecycle and presentation

### ENM-03 — Read the symbol an enemy carries
As a player, I want to see the required character on the enemy, so that I know what to draw.
- AC: Every enemy prefab carries a world-space `EnemyGlyphBadge` rendering `BaybayinCharacterSO.badgeSprite`.
- AC: The badge reads `Enemy.VisualCharacter`, so every visual override (stain, cover, hurt-swap) drives it automatically.
- AC: Badge offset and scale can be overridden per enemy type.
- System: Enemies · `EnemyGlyphBadge`, `GlyphBadgeConfigSO`
- Status: Partial — the badge system is implemented; badge art existed for 7 of 18 symbols at last audit, so some enemies fall back to Latin text.
- Refs: `Assets/Scripts/Gameplay/Enemy/EnemyGlyphBadge.cs`, SALIN-257

### ENM-07 — Hear the symbol when the enemy dies
As a player, I want the syllable spoken at the moment of the kill, so that the reward and the sound arrive together.
- AC: `AudioManager` plays the character's `pronunciationClip` on `OnEnemyDefeated`.
- System: Audio · `AudioManager.PlayPronunciationClip`
- Status: Partial — the path is implemented; it is silent for any character whose clip is unassigned. See LEARN-06.
- Refs: `Assets/Scripts/Core/AudioManager.cs`, `docs/system/03_Core_Systems.md` §3.3

### ENM-10 — Have an enemy look like itself
As a player, I want each corrupted enemy to be visually distinct, so that I can learn to recognise them.
- AC: Each `EnemyDataSO` supplies its own walk frames, applied on initialize.
- AC: Replacement character art is wired on the shared assets so every level inherits it.
- System: Enemies · `Enemy.Initialize`, `EnemyDataSO.walkFrames`
- Status: Partial — all corruption enemies carry walk frames; replacement art was wired for 4 of 6 principals (Hati, Iligaw, Uhaw, Kadena) with Juan's idle, Ragasa and the Hati minions still import-only, and Play Mode / Almanac visual verification outstanding.
- Refs: `docs/audit/STATUS-2026-09-13.md` Continuation 2026-09-14, `Enemy.cs:229-236`

---

## Signature abilities

Each story below is one enemy's combat-time ability. **U-8** applies to every `Missing` row: presentation, timing and accessibility cue are unspecified.

### ENM-13 — Bakod (BA) blocks the enemies behind it
As a player, I want Bakod's wall to force me to deal with it first, so that positioning matters.
- AC: While Bakod lives, the enemies it shields cannot be resolved by a draw.
- AC: A blocked resolution produces a readable "blocked" tell rather than a silent miss.
- System: Enemies · `blocksEnemiesBehind` → `BakodShieldController`, `CombatResolver`
- Status: Partial — the resolution block is implemented and exercised; a readable blocked tell and manual acceptance remain unverified.
- Refs: `Assets/Scripts/Gameplay/Enemy/BakodShieldController.cs`, SALIN-286

### ENM-15 — Nawalang Mukha (NA) strips names away
As a player, I want Nawalang Mukha to remove the romanised labels, so that I must read the Baybayin itself.
- AC: While it lives, romanised labels are stripped from enemy badges.
- AC: Defeating it restores them.
- System: Enemies · `removesNames` → `NawalangMukhaNameLossController`, `NameLossEffectRegistry`
- Status: Partial — implemented for enemy badges (SALIN-285); the audit records that the originally specified romanised-label surface did not exist as assumed, so the scope of the effect is narrower than the Matrix describes.
- Refs: `Assets/Scripts/Gameplay/Enemy/NawalangMukhaNameLossController.cs`, `docs/audit/STATUS-2026-09-13.md` Matrix (NA = MISSING)

### ENM-17 — Salungat (SA) is a decoy that punishes me
As a player, I want Salungat to be a trap I must learn to ignore, so that drawing without thinking has a cost.
- AC: Drawing Salungat's character costs a heart instead of defeating it.
- AC: Its badge glyph is mirrored so the decoy is readable as a decoy.
- AC: After the penalty it cannot be re-triggered by a second draw of the same character.
- System: Enemies · `isDecoy` → `CombatResolver` decoy path, `Enemy.ApplyDecoyPenalty`, `GlyphConfusionPairsSO`
- Status: Partial — the penalty and the badge mirroring are implemented; the exact false-target presentation and a non-mirroring accessibility cue are recorded as open.
- Refs: `CombatResolver.cs:347-357`, SALIN-288, `docs/audit/STATUS-2026-09-13.md` Gaps row 10

### ENM-18 — Kadena (KA) chains another enemy and makes it untouchable
As a player, I want Kadena to protect a neighbour, so that I have to break the chain before the target.
- AC: Kadena chains the nearest enemy and makes it invulnerable while the chain holds.
- AC: Defeating Kadena releases the chained enemy.
- System: Enemies · `chainsNearestEnemy` → `KadenaChainController`
- Status: Partial — the controller is implemented; the chain badge art is missing, and the spec's clause about which assist Kadena disables conflicts with the shipped invulnerability implementation.
- Refs: `Assets/Scripts/Gameplay/Enemy/KadenaChainController.cs`, SALIN-287, `docs/audit/STATUS-2026-09-13.md` Gaps row 9

### ENM-21 — Daan-Lihis (DA) takes a crooked path
As a player, I want Daan-Lihis to be hard to track, so that its lesson about wrong turnings shows in its movement.
- AC: Daan-Lihis descends in a lateral zigzag (`zigzagAmplitude`, `zigzagFrequency`).
- System: Enemies · `zigzagAmplitude` → `PensionadoMover`
- Status: Partial — the zigzag movement is authored and implemented, but no distinct signature ability beyond movement is specified or built.
- Refs: `Assets/Scripts/Gameplay/Enemy/PensionadoMover.cs`, `EnemyData_Daan-Lihis.asset`

### ENM-22 — Walang-Awa (WA) makes heart loss unavoidable while it lives
As a player, I want Walang-Awa to be genuinely merciless, so that its name means something in play.
- AC: While Walang-Awa lives, heart loss cannot be prevented.
- AC: The effect is visible to the player.
- System: Enemies · `HeartSystem`, heavy armour (HP 3)
- Status: Missing — Walang-Awa has only HP 3 and no ability flag. The original specification's healing clause is a confirmed no-op because no healing system exists, and its replacement is undecided. See U-9. SALIN-289 is marked Done with no flag or controller in the tree.
- Refs: `EnemyData_Walang-Awa.asset`, SALIN-289, `docs/audit/STATUS-2026-09-13.md` Gaps row 11

### ENM-23 — Yapos ng Dilim (YA) traps the final symbol
As a player, I want the last enemy of the campaign to guard the last symbol, so that the finale has a gatekeeper.
- AC: While Yapos ng Dilim lives, the final symbol slot (YA into MALAYA) cannot be filled.
- AC: It keeps this role in the Level 15 finale.
- System: Enemies · `ChallengeSession` / final restoration hook
- Status: Missing — Yapos ng Dilim has HP 3 and no ability flag or controller.
- Refs: `EnemyData_YaposngDilim.asset`, `docs/design/spec-rulings-2026-09.md` C2, SALIN-259 (To Do)

### ENM-24 — Face a signature ability on Gapos, Punit, Ngatngat and Uhaw
As a player, I want these four enemies to do something I can see in combat, so that they are threats rather than reskins with more health.
- AC: **Gapos (GA)** — while it lives, the tracing window or stroke responsiveness is constrained.
- AC: **Punit (PA)** — on its arrival, part of the restored sentence is torn away.
- AC: **Ngatngat (NGA)** — a combat-time effect matching its "gnawing" description, threatening restored progress.
- AC: **Uhaw (O/U)** — a combat-time effect that drains something in play.
- AC: Each has an ability flag on `EnemyDataSO`, a controller attached through `Enemy.EnsureAbilityComponent`, and coverage in `CorruptionSignatureAbilityTests`.
- System: Enemies · `EnemyDataSO` ability flags, `StrokeCapture` hook, `ChallengeSession` hook
- Status: Missing — all four carry description text and stats only (Gapos HP 2, Uhaw HP 2, Punit speed 1.7, Ngatngat speed 1.9) with no ability flag and no controller. **Blocked:** the R6 rule sheet that maps each to an existing hook lives in the untracked `AUDIT.md §6.3` and is not in the repository, so none of the four has an implementable rule yet. See U-8.
- Refs: `EnemyData_Gapos.asset`, `EnemyData_Punit.asset`, `EnemyData_Ngatngat.asset`, `EnemyData_Uhaw.asset`, SALIN-259 (To Do), `docs/audit/BACKLOG.md` T61
- Merged: absorbs ENM-25, ENM-26, ENM-27

### ENM-28 — Ragasa (RA) rushes the shrine
As a player, I want the RA enemy to be a distinct threat, so that RA is a real character and not a footnote.
- AC: Ragasa spawns in Level 13 and Level 15 waves carrying `Char_RA`.
- AC: Drawing RA defeats it; drawing DA does not.
- AC: It is registered in the enemy pool and the Codex enemy registry, in Pamana.
- System: Enemies · `EnemyData_Ragasa`, `EnemyPool`, `AlmanacEnemyRegistrySO`
- Status: Partial — `EnemyData_Ragasa.asset` now exists with `enemyID: ragasa`, `Char_RA`, HP 1, speed 1.5 and zigzag movement, but it is authored with `era: 0` (Spanish/Ugat), has no signature ability, no pool registration, and no wave placement. See U-10.
- Refs: `EnemyData_Ragasa.asset`, SALIN-261 (To Do), `docs/audit/STATUS-2026-09-13.md` (Ragasa scope blocker)

---

## Introduction and discovery

### ENM-29 — Be properly introduced to an enemy the first time it appears
As a player, I want to be shown what an enemy is and what it does at its first spawn, so that I am never fighting a stranger and never see an ability without an explanation.
- AC: An enemy's first spawn halts the field and presents the four-beat default introduction: Halt, Name, Ability, Release.
- AC: `EnemyIntroductionBeat` is the single introduction system; the pre-combat teach loop and the enemy discovery overlay are deleted.
- AC: Drawing input is suppressed while the card is open.
- AC: Level 1's Abo lesson extends this to eight beats — Appear (ability armed), Ability (it fires), React, Generalize, Name, Explain, Glyph, Draw — and the clue lost in the Ability beat returns when the player completes the gated draw.
- AC: Levels 2–15 author no lesson profile and keep the four-beat default.
- AC: All four of Level 1's taught-glyph enemies are introduced on their first spawn.
- System: Tutorial · `EnemyIntroductionBeat`, `EnemyIntroductionCardView`, `EnemyIntroductionProgress`, `EnemyLessonSO`
- Status: Partial — the four-beat path is on `dev` (`80aa29ec`) and the playtest saw the card fire on first contact with Hati. Two gaps: `SoloTeachBeat` still calls `SetCombatOverrideActive(true)` and `IsIntroducibleSpawn` declines while that override holds, so Level 1's four taught-glyph enemies are never introduced; and the eight-beat lesson (`EnemyLessonSO`, `AboLesson.asset`, `LevelConfigSO.enemyLessons`) is **implemented only on the unmerged branch** `feature/level1-enemy-introduction-lesson` — `dev` has no `enemyLessons` field. Runtime verification of the lesson has not been recorded, and `EnemyDiscoveryOnboardingController` is still present on `dev`.
- Refs: `SoloTeachBeat.cs:53`, `EnemyIntroductionBeat.cs:205-215`, branch `feature/level1-enemy-introduction-lesson` @ `247c9dcb` (`d5df114c`, `ae7ed0ba`, `d979be75`), `docs/design/2026-09-14-level1-enemy-introduction-lesson-design.md`, `progress/2026-09-14-level1-playtest.md`
- Merged: absorbs ENM-30

### ENM-32 — Have enemies grouped by the era they belong to
As a player, I want the Codex to group enemies by era, so that the roster maps onto the journey.
- AC: `EnemyDataSO.era` is `Ugat` / `Ugnayan` / `Pamana`.
- AC: Grouping follows the ruled pools: Ugat = A, E/I, BA, MA, NA, TA; Ugnayan = O/U, KA, GA, SA, WA, YA; Pamana = DA, RA, HA, LA, NGA, PA.
- System: Enemies / Codex · `EnemyDataSO.era`, `AlmanacController`
- Status: Missing — the enum is still `Spanish / American / Japanese` and every corruption asset is authored `era: 0`. The Almanac gates enemy reveals on `Era.Spanish`. See U-12.
- Refs: `Assets/Scripts/Data/EnemyDataSO.cs:274-279`, ruling C2 / C4, SALIN-260 (To Do)

### ENM-33 — Read an enemy's corrupted meaning and the lesson it hides
As a player, I want each enemy's entry to explain what it corrupts and what restoring the symbol teaches, so that the enemies carry the curriculum.
- AC: `EnemyDataSO` carries `corruptedMeaning`, `trueMeaning`, `lore` and `restoredLesson`.
- AC: Defeating an enemy for the first time shows a discovery card with its ability, corrupted meaning, true meaning and restored lesson.
- System: Enemies / Codex · `EnemyDataSO`, `EnemyDiscoveryCopyProvider`, `AlmanacDetailScroll`
- Status: Missing — none of the four fields exist; a single `description` string is split on "Power:" as an interim. The playtest also found the Hati discovery card's text truncated mid-word with garbled overlap on the panel's right edge (defect 8).
- Refs: `EnemyDataSO.cs:16-17`, `Assets/Scripts/UI/EnemyDiscoveryCopyProvider.cs`, SALIN-260, `progress/2026-09-14-level1-playtest.md` defect 8

### ENM-34 — Not be shown retired enemies in the Codex
As a player, I want the Codex to list only enemies the game contains, so that it is not a museum of cut content.
- AC: El Inquisidor, The Superintendent and Kadiliman are absent from the enemy registry and the discovery flows.
- System: Codex · `AlmanacEnemyRegistrySO`
- Status: Partial — pass 2 resolved the registry's actual contents: it lists all 18 corruption enemies (including Ragasa) plus **`EnemyData_Boss_Superintendent` and `EnemyData_Boss_Kadiliman`**; El Inquisidor has been removed. Two retired bosses remain visible as Codex entries.
- Refs: `Assets/ScriptableObjects/Almanac/AlmanacEnemyRegistry_Default.asset`, SALIN-247 / SALIN-273, `docs/audit/BACKLOG.md` T35
