# 09 — The boss encounter

Prefix **`BOSS`**. Covers the single boss encounter at Era 3 Level 5 (global Level 15).

**Ruling Q5 + third round.** **Paglimot on Level 15 is the only boss.** El Inquisidor (Level 5) and The Superintendent (Level 10) are retired legacy mechanics. Level 15 is *both* a boss encounter *and* a mixed-wave paragraph level: one wave per phase (Ugat symbols, then Ugnayan, then all), one paragraph line restored per phase, **YA into MALAYA last**, and a per-phase checkpoint.

> **U-3 applies to this whole file.** The boss the level actually references is `BossConfig_Kadiliman`, the retired finale. `SALIN-273` ("retire Kadiliman from the campaign finale and reconcile the data, code and docs") is marked Done, but `Level15_Config.bossConfig` still points at `BossConfig_Kadiliman.asset` and `EnemyData_Boss_Kadiliman.asset` is still the boss enemy data. Whether this is naming debt or a live design mismatch is unresolved. Every story below describes the **required** behaviour and states the shipped state.

---

### BOSS-01 — Face one final boss at the end of the journey
As a player, I want a final antagonist to fight, so that the campaign has a climax.
- AC: Era 3 Level 5 runs a boss encounter instead of a plain wave level.
- AC: No other level has a boss.
- System: Boss · `WaveManager.RunBossEncounter`, `LevelConfigSO.bossConfig`
- Status: Partial — Level 15 has a boss and Level 5's has been cleared, but **Level 10 still references `BossConfig_Superintendent`** (U-2), and Level 15's boss is still the retired Kadiliman config (U-3).
- Refs: `Level15_Config.asset` (`bossConfig` → `BossConfig_Kadiliman`), `Level10_Config.asset` (`bossConfig` → `BossConfig_Superintendent`), ruling Q5

### BOSS-02 — Be taught the boss's rules before the fight
As a player, I want the boss's mechanics explained before it starts, so that I am not learning them while losing.
- AC: When the boss config has a tutorial, a paged scroll opens automatically after any character reveals and before the encounter.
- AC: Page 1 gives the boss name and lore; later pages explain the mechanics; arrows page and a close control starts the encounter from any page.
- AC: Drawing input is suppressed while the scroll is open.
- System: Boss · `BossTutorialController`, `BossTutorialScroll`, `BossTutorialSO`, `BossTutorialPaging`
- Status: Partial — the system is fully implemented and tested, but the only authored `BossTutorialSO` is `BossTutorial_ElInquisidor`, for a retired boss. Level 15's boss has no tutorial asset.
- Refs: `Assets/Scripts/Gameplay/Boss/BossTutorialController.cs`, `Assets/ScriptableObjects/Enemies/Boss Configs/`, SALIN-123

### BOSS-04 — Fight the boss's summoned enemies
As a player, I want the boss to send minions at me, so that the encounter is still a defense fight.
- AC: Each summon act plays a tell animation then streams 2–3 minions one at a time on a `delayBetweenMinions` cadence, clamped to `summonHorizontalBounds`.
- AC: The boss holds its cast pose from windup through the final spawn and briefly after, so the stream reads as a ritual rather than a burst.
- AC: Per the ruling, phase 1 summons Ugat-symbol enemies, phase 2 Ugnayan, phase 3 all three eras.
- System: Boss · `BossSummonTicker`, `BossPhase.summonEnemyTypes`
- Status: Partial — the summon streaming is implemented; the ruled per-phase era pools belong to a Paglimot config that does not exist. The shipped `BossConfig_Kadiliman` has four phases with its own (untuned) summon lists.
- Refs: `Assets/Scripts/Gameplay/Boss/BossSummonTicker.cs`, `docs/system/04_Gameplay_Systems.md` §7.4

### BOSS-10 — Restore a line of the era paragraph after each phase
As a player, I want the boss fight and the restoration to be one encounter, so that the finale combines everything I learned.
- AC: Clearing a boss phase opens one paragraph line to restore.
- AC: A correct restoration resumes the next phase.
- AC: Each phase boundary is a checkpoint.
- System: Boss / flow · `LevelFlowSegment`, `BossController`, `ChallengeFlowController`
- Status: Missing — Level 15 has no `flowSegments`, and the boss controller has no paragraph hook. The engine support for alternation exists (SALIN-226) but is unused at Level 15.
- Refs: `Level15_Config.asset` (no segments), SALIN-252 (To Do), `docs/audit/BACKLOG.md` T41

### BOSS-11 — Trace YA into MALAYA as the last act of the game
As a player, I want the final action of the campaign to be writing one symbol into one word, so that the ending is mine to complete.
- AC: The final action of Level 15 is tracing YA into MALAYA.
- AC: `Level15_Config.finalRestorationValue` is YA.
- AC: The finale symbol is the last entry of `ContentIdentity.RevisedSymbolIds`, and RA is not last.
- System: Boss / restoration · `ContentIdentity`, `CampaignConfigValidator.ValidateFinalRestoration`, `ChallengeSession`
- Status: Partial — the finale reorder and validator change were delivered with SALIN-217, but Level 15's challenge content and the final trace-and-place ceremony are unauthored.
- Refs: ruling Q1 + qualifier, SALIN-217, SALIN-252 (To Do)

### BOSS-12 — Restart only the phase I died in
As a player, I want dying in a late phase not to send me back to phase one, so that a long fight is not punishing to retry.
- AC: Dying in a phase restarts that phase only, with earlier paragraph lines still locked.
- System: Boss / flow · checkpoint recovery
- Status: Missing — depends on BOSS-10; no per-phase checkpoint exists.
- Refs: `docs/audit/BACKLOG.md` T41, ruling Q5 qualifier (b)

### BOSS-13 — Have a final boss I can see and hear
As a player, I want the climax to look and sound like a boss encounter, so that the finale has weight.
- AC: The boss config supplies a boss sprite, and collapse and stand-up frames are assigned.
- AC: The boss plays its own BGM (faded in), an intro growl, summon ticks, teleports, footsteps on pacing phases, hit and damage growls, a body fall on exhaustion, a laugh on a missed window, and a defeat sting.
- AC: Variant pools never repeat the same clip twice in a row.
- AC: Every audio handler is null-tolerant — a partly filled bank never breaks gameplay.
- System: Boss · `BossConfigSO.bossSprite`, `BossAudio`, `BossAudioBankSO`, `BossStateVisuals`
- Status: Missing — the audio subscription set, footstep cadence, no-repeat picker and per-category volume scaling are all implemented, but the finale boss config has **no `bossSprite` and no `audioBank`**, so the encounter would be a blank sprite in silence. Both are content gaps, not configuration ones.
- Refs: `Assets/Scripts/Gameplay/Boss/BossAudio.cs`, `docs/system/04_Gameplay_Systems.md` §7.4, §8.7
- Merged: absorbs BOSS-17

### BOSS-18 — Face a boss that is actually harder than the first enemies
As a player, I want the final boss to be the hardest thing in the game, so that finishing means something.
- AC: The finale's required draws and phase count exceed every earlier encounter.
- AC: Boss timers are tuned against playtest feedback rather than shipped at their first authored values.
- System: Boss content · `BossConfigSO.phases`
- Status: Unclear — the shipped finale config has 4 phases and 17 required draws, but the documentation explicitly records these as "a starting point, not tuned values" and that nobody has played the encounter. The ruled Paglimot shape (3 phases, one per era pool) differs.
- Refs: `docs/system/04_Gameplay_Systems.md` §7.4, RISK-14, ruling Q5 qualifier
