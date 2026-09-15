# 07 — Combat and defense

Prefix **`CMB`**. Covers waves, spawn scheduling, active clues, target resolution, chain kills, hearts, the Shrine, and Juan.

**Retired by ruling:** lanes, bow-and-arrow projectiles, combo powers, Focus Mode, Endless Mode, armour tiers (Q15 / OQ-5). See [`../decisions/retired-scope.md`](../decisions/retired-scope.md).

---

## Waves

### CMB-05 — Face faster enemies in later levels
As a player, I want the pressure to rise across the campaign, so that my improving skill is tested.
- AC: `LevelConfigSO.enemySpeedMultiplier` scales enemy speed per level.
- AC: Spawn rate increases per wave within a level and per level within an era.
- System: Waves / content · `LevelConfigSO.enemySpeedMultiplier`, `WaveDefinition.spawnInterval`
- Status: Partial — the levers exist and are authored for Ugat; Era 2–3 wave pacing is not fully authored and exact values are untuned.
- Refs: `LevelConfigSO.cs:46`, `docs/capstone/GDD.md` §3.6

### CMB-08 — Have enemies recycled rather than spawned from scratch
As a player, I want no stutter when enemies appear, so that the game stays smooth on a phone.
- AC: All enemy creation and destruction goes through `EnemyPool`; no `Instantiate`/`Destroy` in the gameplay loop.
- AC: The pool has a default capacity and a maximum size; overflow destroys only above the maximum.
- System: Pooling · `EnemyPool`
- Status: Partial — pooling is implemented, but the corruption enemy ids are not registered per type, so they fall back to a shared default pool and log `Unknown enemyID` warnings.
- Refs: `Assets/Scripts/Gameplay/Enemy/EnemyPool.cs`, `docs/audit/BACKLOG.md` T33, SALIN-210

---

## Clues and targeting

### CMB-09 — Be told which symbol to draw next
As a player, I want a clear cue for what to draw, so that I am never guessing what the game wants.
- AC: On an active-clue level exactly one enemy carries the mark at a time.
- AC: The mark is selected from eligible on-screen carriers, favouring the one closest to the Shrine.
- System: Combat · `ActiveClueDirector`, `ActiveClueSelector`, `ActiveCluePresenter`
- Status: Partial — implemented and enabled on Levels 1–5 (`activeClueCombatEnabled: 1`); Levels 6–15 have it off.
- Refs: `Assets/Scripts/Gameplay/Combat/ActiveClueDirector.cs`, `Level*_Config.asset`

### CMB-11 — Be cued in more than one way
As a player, I want different kinds of clue, so that the difficulty comes from how the symbol is asked for, not just from speed.
- AC: `ClueChannels` composes `Glyph`, `SpokenAudio`, `LatinText`, `ContextImage` and `IncompleteWord`.
- AC: A level declares which channels it may use.
- System: Combat · `ClueChannels`, `ActiveClueSelector`, `ActiveCluePresenter`
- Status: Partial — the channel model and presenter are implemented; the per-level channel ladder (Roman+outline → image+sound → audio-only → incomplete word) is authored for Ugat and not for Levels 6–15.
- Refs: `Assets/Scripts/Data/Campaign/ClueChannels.cs`, SALIN-230

### CMB-13 — Replay an audio clue
As a player, I want to hear a spoken clue again, so that I am not punished for missing it once.
- AC: An audio-only clue offers a replay control.
- System: Combat · `ActiveCluePresenter`
- Status: Missing — the replay-button requirement is part of the unimplemented per-level clue ladder.
- Refs: SALIN-230, `docs/audit/BACKLOG.md` T15

## Juan and the Shrine

### CMB-19 — See Juan defend the shrine
As a player, I want a visible protagonist who reacts when I draw, so that the fight has a character and my drawing visibly becomes an attack.
- AC: When `LevelConfigSO.hasProtagonist` is set, Juan is present in the play column, and walks in at level start when `protagonistWalksIn` is set.
- AC: A correct recognition triggers Juan's attack animation and a slash VFX toward the resolved target.
- AC: The slash VFX is pooled, not instantiated per hit.
- System: Protagonist · `ProtagonistManager`, `ProtagonistAttackController`, `ProtagonistSlashVfx`
- Status: Partial — both controllers exist, but the 2026-09-14 playtest recorded **no protagonist visible anywhere in the play column** during dialogue, Wave 1 or Wave 2, and logged `[ProtagonistAttackController] _slashVfxPrefab not assigned on ProtagonistManager prefab`. The new Juan idle/attack art landed in `f35de96d` and was never seen on screen. Playtest defects 1 and 2, both High, likely one root cause. The handoff listed "restored slash feedback" as completed.
- Refs: `ProtagonistManager.cs`, `ProtagonistAttackController.cs`, `LevelConfigSO.cs:142-145`, `progress/2026-09-14-level1-playtest.md` defects 1–2
- Merged: absorbs CMB-20

### CMB-25 — See the shrine take visible damage
As a player, I want the shrine to look hurt, so that I can feel how close I am to losing.
- AC: A base hit plays a hit feedback beat on the shrine.
- AC: The shrine has distinct visual damage states.
- System: Feedback · `BaseHitFeedbackController`, shrine art
- Status: Partial — hit feedback is implemented; pass 2 found **no damage-state logic anywhere** under `Feedback/`, `Gameplay/Base/` or `Gameplay/Environment/` (no crack/destroyed/damage-state symbols), so the GDD's four visual states are unbuilt, not merely unconfirmed.
- Refs: `Assets/Scripts/Feedback/BaseHitFeedbackController.cs`, `docs/capstone/GDD.md` §4.1

### CMB-26 — Get a beat to register a breach
As a player, I want the game to pause briefly and show me what got through, so that I learn from the loss instead of just seeing a heart vanish.
- AC: A breach freezes the field for about a second.
- AC: The missed glyph and its syllable are shown.
- AC: That symbol is returned to the review queue and reappears as a later clue in the same wave.
- System: Combat · `HeartSystem`, `ActiveClueDirector`, `HeartDisplay`
- Status: Missing — pass 2 re-confirmed on `dev`. The only "freeze" in the combat code is `ActiveClueDirector`'s mark-freeze during a trace (CMB-10), which is unrelated; `HeartSystem`, `CombatResolver` and `HeartDisplay` have no breach pause or missed-clue surface. SALIN-238 is marked Done with no implementation. The playtest confirmed hearts simply decrement (3 → 1) as enemies arrive.
- Refs: `Assets/Scripts/Gameplay/Combat/ActiveClueDirector.cs:22-29,102-113` (mark freeze only), SALIN-238, `docs/audit/BACKLOG.md` T23

### CMB-29 — Win immediately when the target text is complete
As a player, I want filling the target text to end the encounter, so that the language goal — not an enemy counter — is what wins the level.
- AC: On a combat-restoration level, completing every required target slot resolves the encounter.
- AC: The spawn schedule treats the target text as the level's length control.
- System: Combat restoration · `LevelFlowController.ExecuteCombatRestoration`, `TargetTextSlotMap`, `SpawnAssignmentCoordinator`, `InstantWinPresenter`
- Status: Partial — implemented and enabled on Levels 1–5 (`activeClueRestorationEnabled: 1`); off for Levels 6–15. SALIN-277 is recorded as a DRAFT (unapproved) design.
- Refs: `Assets/Scripts/Gameplay/Flow/InstantWinPresenter.cs`, `Assets/Scripts/Gameplay/Combat/TargetTextSlotMap.cs`, SALIN-277
