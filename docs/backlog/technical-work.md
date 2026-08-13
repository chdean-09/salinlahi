# Technical Work — Backlog

Spike and technical-work specifications for the Salinlahi campaign.

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
| **Workbook checksum** | `34dad782a025b3acd3dcfc9bdfb2ce5c595fe81e6bd1789c9042849b63c27eb7` |
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
boss restores that era's lost story — the `finalRestorationValue` field on
`LevelConfigSO` and the `outroDialogue` / `CutsceneSO` that play on boss
defeat. **"Cumulative paragraph mastery"** means the boss vulnerability window
tests the player's ability to draw all characters introduced *up to and
including* that era (the `allowedCharacters` roster on the level config).

### LF-CONTRACT-v2 compatibility

LF-CONTRACT-v2 is the external level-framework contract (the revised campaign
data schema tracked in SALIN-170). In the codebase it is materialized as
`ContentIdentity.RevisedCampaignId = "campaign.revised-v1"` with the approved
workbook SHA `33f7355fce8c0154650bf18589879e75a6da51538d1b798769242bebe47c8e83`.

The three encounter specifications below are compatible with this contract
because:

1. They reuse the existing `BossConfigSO` / `BossPhase` schema — no new
   ScriptableObject types or field changes are required.
2. They draw `allowedCharacters` from `LevelConfigSO` (the revised
   `cumulativeSymbolPool`), so the vulnerability window's glyph sampling
   already honors the cumulative-roster contract.
3. They do not introduce any level beyond 15 — no `level.pamana.06` or
   equivalent is created. `ContentIdentity.RevisedLevelIds` remains 15 entries.
4. The `finalRestorationValue` and `masteryRequirements` fields already on
   `LevelConfigSO` are the contract hooks for paragraph restoration and
   mastery validation; the encounters consume them at runtime via existing
   `BossController` + `ProgressManager` paths.

---

## 1. Three-Phase Encounter Specifications

All three encounters use the existing `BossController` state machine:
`Intro → [SummoningPhase → WindingDown → Vulnerable → Damaged] × 3 → Outro → Defeated`.

Each phase = 1 HP. Boss HP = `phases.Count` = 3 for all encounters. The
vulnerability window samples glyphs from `LevelConfigSO.allowedCharacters`
(the cumulative roster up to that era). Phase failure (timer expiry) repeats
the phase with **no HP loss** — this is the existing retry-within-encounter
behavior.

### 1.1 Level 5 — El Inquisidor (Paglimot I, Spanish era mastery)

**Era:** ugat (Spanish Colonization), Levels 1–5.
**Roster tested:** 8 characters (BA, KA, DA, GA, HA, LA, MA, NA).
**Theme:** The friar-inquisitor burns manuscripts. Defeating him proves the
script still has power.
**Config asset:** `BossConfig_ElInquisidor.asset` — **already fully authored**
(3 phases, audio bank, tutorial). This is the reference implementation.

| Phase | Summon duration | Summon cadence | Minions/summon | Summon types | Required draws | Vulnerability timer | Movement | Design intent |
|-------|-----------------|----------------|----------------|--------------|----------------|---------------------|----------|---------------|
| 1 | 30s | 7s gap | 1–2 | Soldado | 3 | 20s | Pace (speed 1.0, range 2.5) | Gentle intro. Slow pace, single enemy type, generous timer. Player learns the summon → wind-down → vulnerable rhythm. |
| 2 | 30s | 4s gap | 1–2 | Soldado, Fraile, Guardia | 3 | 20s | Pace (speed 1.3, range 2.5) | Escalation. Faster movement, variant enemies (phaser + fast), shorter summon gap. Tests multitasking under pressure. |
| 3 | 20s | 4s gap | 2–3 | Soldado, Fraile, Guardia, Capitan | 4 | 25s | Teleport (range 2.5 × 2.5) | Climax. Teleporting boss, elite (shielded Capitan) joins, 4 draws required. Tests full era mastery. |

**Paragraph restoration:** On defeat, `outroDialogue` plays — the Spirit Guide
declares the script still has power. `finalRestorationValue` references the
era's focus word. `ProgressManager.HandleLevelComplete` saves stars and
unlocks Level 6.

**Status:** Fully implemented and wired. No code or asset changes needed.

### 1.2 Level 10 — The Superintendent (Paglimot II, American era mastery)

**Era:** ugnayan (American Occupation), Levels 6–10.
**Roster tested:** 14 characters (8 from era 1 + NGA, PA, SA, TA, WA, YA from
era 2).
**Theme:** The colonial education administrator wields institutional erasure.
His "Decree" ability scrambles Baybayin labels — the player must fight through
the scramble to preserve the script.
**Config asset:** `BossConfig_Superintendent.asset` — **placeholder, needs full
authoring** (currently 1 phase, defaults, no enemy types, no audio, no tutorial).

| Phase | Summon duration | Summon cadence | Minions/summon | Summon types | Required draws | Vulnerability timer | Movement | Design intent |
|-------|-----------------|----------------|----------------|--------------|----------------|---------------------|----------|---------------|
| 1 | 28s | 6s gap | 1–2 | Soldier | 4 | 18s | Pace (speed 1.2, range 2.5) | Intro. American-era regulars only. 4 draws (up from 3 at L5 phase 1) reflects the larger cumulative roster. |
| 2 | 26s | 5s gap | 2–3 | Soldier, Pensionado, Maestro | 5 | 18s | Pace (speed 1.5, range 3.0) | Decree phase. Maestro (decoy) forces the player to *not* draw — a mastery test of restraint. Pensionado zigzag tests visual tracking. 5 draws required. |
| 3 | 22s | 4s gap | 2–3 | Soldier, Pensionado, General, Maestro | 6 | 22s | Teleport (range 3.0 × 2.0) | Climax. General (commander aura) speeds up nearby enemies. Teleporting boss. 6 draws required. Full era-2 mastery under maximum pressure. |

**Decree mechanic (scramble):** The Superintendent's signature ability is
label scrambling — the same mechanic Kempei uses (Level 13). During
SummoningPhase phases 2 and 3, the boss emits a "decree" that temporarily
scrambles labels on all on-screen non-boss enemies. This reuses the existing
`KempeiScrambleController` visual system, applied as a boss-phase effect rather
than an enemy-aura effect. **Implementation note:** this requires a new
`BossDecreeEffect` component (or a boss-scoped wrapper around the scramble
controller) — see §2 Reuse Assessment.

**Paragraph restoration:** On defeat, `outroDialogue` plays — the Spirit Guide
declares the script survives institutional erasure. `finalRestorationValue`
references the era's focus word. Level 11 unlocks.

**Art needs:** Boss sprite (64×64), walk frames, death frames, portrait
(`bossSprite`), era-themed shrine already exists (Ancestral Door).

**Audio needs:** `BossAudioBank_Superintendent.asset` — distinct BGM, growls,
footsteps, defeat clip. The `BossAudio` component is null-tolerant so the
encounter runs without audio, but shipping requires a full bank.

**Tutorial needs:** `BossTutorialSO` with 3 pages: (1) boss name + lore, (2)
Decree scramble explanation, (3) vulnerability window explanation. Reuses
existing `BossTutorialScroll` / `BossTutorialController` / `BossTutorialPaging`
— no new tutorial code needed.

### 1.3 Level 15 — Kadiliman (Paglimot III, final campaign mastery)

**Era:** pamana (Japanese Occupation), Levels 11–15.
**Roster tested:** All 17 characters (full campaign roster).
**Theme:** The Darkness itself — the embodiment of cultural forgetting. A
formless shadow entity combining all three eras of corruption. Drawing all 17
characters across the three phases restores Baybayin to the world.
**Config asset:** `BossConfig_Kadiliman.asset` — **placeholder, needs full
authoring** (currently 1 phase, defaults, no enemy types, no audio, no
tutorial).

| Phase | Summon duration | Summon cadence | Minions/summon | Summon types | Required draws | Vulnerability timer | Movement | Design intent |
|-------|-----------------|----------------|----------------|--------------|----------------|---------------------|----------|---------------|
| 1 | 25s | 5s gap | 2–3 | Heitai, Kisha | 6 | 20s | Pace (speed 1.3, range 3.0) | Era-3 opening. Japanese-era regulars + sprinter. 6 draws. Establishes the final boss's tempo. |
| 2 | 22s | 4s gap | 2–3 | Heitai, Kisha, Kempei, Shokan + cross-era (Soldado, Soldier) | 7 | 20s | Teleport (range 3.0 × 2.5) | Cross-era assault. Kadiliman summons enemies from *all three eras* (GDD §4.3). Kempei scrambles labels; Shokan is shielded + corruption veil. 7 draws. Tests full-campaign enemy knowledge. |
| 3 | 20s | 3s gap | 3–4 | All-era mix (Soldado, Soldier, Heitai, General, Shokan) | 8 | 25s | Teleport (range 3.5 × 3.0) | Final phase. Maximum summon rate, maximum minion count, cross-era elite mix. 8 draws required — the ultimate mastery check. The extended 25s timer compensates for the higher draw count. |

**17-character mastery:** The GDD states "Drawing all 17 characters defeats
it." The three-phase structure honors this cumulatively: 6 + 7 + 8 = 21 draws
across the encounter, with random sampling from the full 17-character roster
ensuring broad coverage. The *intent* (per the workbook) is that the player
demonstrates mastery of all 17 characters; the random sampler from
`allowedCharacters` (which is the full 17 at Level 15) achieves this
statistically. A deterministic 17-draw sequence is **not** required by the
existing `BossController` — the random-sampling vulnerability window is the
approved mechanism.

**Cross-era summons:** Kadiliman's `fallbackEnemyTypes` and per-phase
`summonEnemyTypes` include enemies from all three eras. This is a data-only
change — `BossSummonTicker` already spawns any `EnemyDataSO` passed to it. No
code change needed.

**Paragraph restoration:** On defeat, `outroDialogue` + `CutsceneSO` play —
"the script returns, the world remembers." `finalRestorationValue` references
the campaign's ultimate restoration symbol. Endless Mode unlocks (REQ-33).
This is the campaign finale — no Level 16 exists or is introduced.

**Art needs:** Boss sprite (64×64, formless shadow entity), walk frames, death
frames, portrait. This is the most demanding art asset — the "formless shadow"
needs to read as combining all three era corruption colors (per GDD §4.3).

**Audio needs:** `BossAudioBank_Kadiliman.asset` — the most dramatic audio
bank. Distinct BGM (final battle theme), growls, defeat fanfare. BGM fade-out
on defeat should feel conclusive.

**Tutorial needs:** `BossTutorialSO` with 3–4 pages: (1) boss name + lore
("Darkness itself"), (2) cross-era summon warning, (3) vulnerability window,
(4) optional "draw all 17 characters" encouragement. Reuses existing tutorial
system.

---

## 2. Reuse Assessment for the Existing Boss Framework

### 2.1 Fully reusable — no changes needed

| Component | Path | Reuse verdict |
|-----------|------|---------------|
| `BossController` | `Assets/Scripts/Gameplay/Boss/BossController.cs` | **100% reuse.** The state machine, phase loop, vulnerability window, draw routing, and outro are era-agnostic. All three encounters use it unchanged. |
| `BossEnemy` | `Assets/Scripts/Gameplay/Enemy/BossEnemy.cs` | **100% reuse.** `IsBoss => true`, `TakeDamage` no-op. Era-independent. |
| `BossSummonTicker` | `Assets/Scripts/Gameplay/Boss/BossSummonTicker.cs` | **100% reuse.** Spawns any `EnemyDataSO` from the phase config. Cross-era summons work automatically. |
| `PhaseBasedMovement` | `Assets/Scripts/Gameplay/Boss/PhaseBasedMovement.cs` | **100% reuse.** Hover/Pace/Teleport patterns are data-driven. |
| `BossStateVisuals` | `Assets/Scripts/Gameplay/Boss/BossStateVisuals.cs` | **100% reuse.** Panting, collapse, stand-up animations are generic. |
| `BossDamageFeedback` | `Assets/Scripts/Gameplay/Boss/BossDamageFeedback.cs` | **100% reuse.** Two-tier damage flash is era-independent. |
| `BossAudio` | `Assets/Scripts/Gameplay/Boss/BossAudio.cs` | **100% reuse.** Subscribes to 9 EventBus events, resolves bank from config. New bosses need only a new `BossAudioBankSO` asset. |
| `BossGlyphVisibilityBinder` | `Assets/Scripts/Gameplay/Boss/BossGlyphVisibilityBinder.cs` | **100% reuse.** Badge visibility during vulnerability is generic. |
| `BossHealthBar` | `Assets/Scripts/UI/BossHealthBar.cs` | **100% reuse.** HP-based, era-independent. |
| `BossVulnerabilityTimerBar` | `Assets/Scripts/UI/BossVulnerabilityTimerBar.cs` | **100% reuse.** Countdown is generic. |
| `BossDrawCounterUI` | `Assets/Scripts/UI/BossDrawCounterUI.cs` | **100% reuse.** "N / required" counter is generic. |
| `BossTutorialController` | `Assets/Scripts/Gameplay/Boss/BossTutorialController.cs` | **100% reuse.** Paged tutorial scroll, drawing-input suppression. |
| `BossTutorialScroll` / `BossTutorialPaging` | `Assets/Scripts/UI/Boss/BossTutorial*.cs` | **100% reuse.** Paging math and scroll UI are generic. |
| `BossConfigSO` / `BossPhase` | `Assets/Scripts/Data/BossConfigSO.cs`, `BossPhase.cs` | **100% reuse.** All phase fields (summon, vulnerability, movement) are data-driven. No schema changes needed. |
| `BossAudioBankSO` | `Assets/Scripts/Data/BossAudioBankSO.cs` | **100% reuse.** Per-boss audio bank. New asset per boss. |
| `BossTutorialSO` | `Assets/Scripts/Data/BossTutorialSO.cs` | **100% reuse.** Per-boss tutorial pages. New asset per boss. |
| `WaveManager.RunBossEncounter` | `Assets/Scripts/Gameplay/Wave/WaveManager.cs` | **100% reuse.** Activates boss when `bossConfig != null`. |
| `CombatResolver.TryRouteDraw` | `Assets/Scripts/Gameplay/CombatResolver.cs` | **100% reuse.** Routes draws to boss before AOE/closest-match. |
| EventBus boss events | `Assets/Scripts/Core/EventBus.cs` | **100% reuse.** 11 boss events already defined. |

### 2.2 Data-only work (no code changes)

| Work item | Asset(s) to create/fill |
|-----------|------------------------|
| Fill `BossConfig_Superintendent.asset` | 3 phases per §1.2, `fallbackEnemyTypes` (American era), `summonHorizontalBounds`, `introDuration`, `outroDuration` |
| Fill `BossConfig_Kadiliman.asset` | 3 phases per §1.3, `fallbackEnemyTypes` (all three eras), `summonHorizontalBounds`, `introDuration`, `outroDuration` |
| Create `BossAudioBank_Superintendent.asset` | BGM, growls, footsteps, teleports, defeat clip |
| Create `BossAudioBank_Kadiliman.asset` | BGM, growls, footsteps, teleports, defeat clip |
| Create `BossTutorial_Superintendent.asset` | 3 pages (name/lore, Decree, vulnerability) |
| Create `BossTutorial_Kadiliman.asset` | 3–4 pages (name/lore, cross-era summons, vulnerability, optional encouragement) |
| Fill `EnemyData_Boss_Superintendent.asset` | Walk frames, death frames, era = American |
| Fill `EnemyData_Boss_Kadiliman.asset` | Walk frames, death frames, era = Japanese (or a new "Final" era if desired — but `Era` enum has no "Final"; use Japanese) |
| Wire `bossConfig.tutorial` on each config | Point to the new `BossTutorialSO` assets |
| Wire `bossConfig.audioBank` on each config | Point to the new `BossAudioBankSO` assets |
| Create boss prefabs | `[Enemy] Boss_Superintendent.prefab`, `[Enemy] Boss_Kadiliman.prefab` — duplicate `Boss_ElInquisidor.prefab` and swap sprites/data |
| Wire `Level10_Config.asset` / `Level15_Config.asset` | Ensure `bossConfig` points to the filled configs; fix `chapterNumber` on Level 15 (currently 1, should be 3) |

### 2.3 New code needed

| Component | Reason | Effort |
|-----------|--------|--------|
| `BossDecreeEffect` (Superintendent only) | The "Decree" scramble ability reuses the Kempei scramble *visual* but applies it as a boss-phase effect (scramble all on-screen labels during SummoningPhase phases 2–3), not an enemy aura. A thin wrapper or boss-scoped trigger is needed. | Small — wraps existing `KempeiScrambleController` logic or fires a global scramble event. ~1 day. |
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

All three encounters use the same core mechanic loop:
1. **SummoningPhase** — boss summons era-appropriate minions while moving
   (Hover/Pace/Teleport). Player draws to defeat minions and protect hearts.
2. **WindingDown** — boss stops summoning, pants (exhausted). Player clears
   remaining minions.
3. **Vulnerable** — boss collapses, becomes targetable. Player must draw N
   correct glyphs from the cumulative roster within a timer.
4. **Damaged** — boss loses 1 HP, stands up, advances to next phase.

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
stricter recognition (threshold stays at 0.60 per GDD §3.6). Timers are tuned
to give ~3–4 seconds per required draw, adjusted for roster size.

### 3.2 Paragraph restoration

"Paragraph restoration" is the narrative beat on boss defeat. The mechanical
implementation:

1. `BossController.RunOutro()` raises `OnBossDefeated` then `OnLevelComplete`.
2. `LevelFlowController` catches `OnLevelComplete` and plays the level's
   `outroDialogue` (a `DialogueSO`) — the Spirit Guide's restoration
   declaration.
3. For Level 15, a `CutsceneSO` may also play (the "script returns" ending).
4. `LevelConfigSO.finalRestorationValue` (a `SymbolValueReference`) identifies
   the era's restored focus word — this is a data contract field, not a
   runtime mechanic. It is consumed by the validation layer
   (`CampaignConfigValidator`) and available for future UI display.
5. `ProgressManager.HandleLevelComplete()` saves level completion (stars,
   unlock next level) via `PlayerPrefs.Save()` — the atomic save.

**No new mechanic is needed.** Paragraph restoration is dialogue/cutscene
playback + progress save, both of which are existing systems.

### 3.3 Active clues

The `ClueMode` enum on `LevelConfigSO` controls how much glyph information is
shown to the player during gameplay:

| ClueMode | Behavior | Use in boss encounters |
|----------|----------|------------------------|
| `FullGlyph` | Badge shows the full Baybayin glyph | Levels 5–10 (default). Player sees what to draw. |
| `SpokenAndLatin` | Badge shows Latin spelling + audio cue | Optional for Level 15 phase 3 (hardcore mode). |
| `LatinOnly` | Badge shows only Latin spelling | Optional difficulty modifier. |
| `None` | No badge — player must recognize from memory | Not recommended for boss encounters (frustrating under time pressure). |

**Recommendation:** All three boss encounters use `FullGlyph` (the existing
default). The Superintendent's Decree *temporarily* overrides this by
scrambling labels (a Kempei-style effect), which is the era-specific clue
challenge. No permanent clue-mode change is needed for bosses.

### 3.4 Art

| Asset | Level 5 | Level 10 | Level 15 |
|-------|---------|----------|----------|
| Boss sprite (64×64) | **Done** (El Inquisidor) | **Needed** — Superintendent, American-era administrator | **Needed** — Kadiliman, formless shadow entity (most complex) |
| Walk frames | **Done** | Needed | Needed |
| Death frames | **Done** | Needed | Needed (dramatic dissolution) |
| Portrait (`bossSprite`) | **Done** | Needed (Almanac) | Needed (Almanac) |
| Era shrine | **Done** (Baybayin Altar) | **Done** (Ancestral Door) | **Done** (Scroll Shrine) |
| Era tileset | **Done** (jungle path) | **Done** (cobblestone) | **Done** (bombed cobblestone) |
| Tutorial art | **Done** (uses walk frames) | Needed (uses walk frames) | Needed (uses walk frames) |

Art is the primary external dependency. Per GDD §6.3, Art Batch 3 (boss
sprites for Superintendent + Kadiliman) is needed by End of Week 7.

### 3.5 Audio

| Asset | Level 5 | Level 10 | Level 15 |
|-------|---------|----------|----------|
| `BossAudioBankSO` | **Done** (`BossAudioBank_ElInquisidor.asset`) | **Needed** | **Needed** |
| BGM | Done | Needed (American-era boss theme) | Needed (final battle theme) |
| Growls / hit SFX | Done | Needed | Needed |
| Footsteps | Done | Needed | Needed |
| Defeat clip | Done | Needed | Needed (conclusive fanfare) |

`BossAudio` is null-tolerant — encounters run without audio. But shipping
requires full banks. Per GDD §6.3, boss theme BGM is needed by End of Week 7.

### 3.6 Tutorial needs

All three encounters use the existing `BossTutorialSO` + `BossTutorialController`
+ `BossTutorialScroll` system. No new tutorial code.

| Boss | Pages | Content |
|------|-------|---------|
| El Inquisidor | **Done** (`BossTutorial_ElInquisidor.asset`) | Name/lore, summoning, vulnerability |
| Superintendent | 3 pages (needed) | Name/lore, Decree scramble, vulnerability |
| Kadiliman | 3–4 pages (needed) | Name/lore, cross-era summons, vulnerability, optional "all 17 characters" encouragement |

Tutorial art reuses the boss's `walkFrames` from `EnemyDataSO` — no extra art
assets needed (per `BossTutorialSO` design guidance in §05 Data Contracts).

### 3.7 Accessibility

The existing accessibility features (GDD §5.5) apply unchanged to boss
encounters:

- **Full-screen drawing** — no precision targeting; draw anywhere.
- **Audio pronunciation on every correct draw** — secondary feedback channel.
- **Failed strokes show red flash / X** — clear rejection feedback.
- **Portrait one-handed play** — boss encounters use the same orientation.
- **No text-heavy tutorials** — boss tutorials are paged scrolls with art.
- **Pause** — pause menu works during boss encounters (timeScale = 0 pauses
  all scaled coroutines). See §4.5 for the pause acceptance example.

**Boss-specific accessibility notes:**
- The vulnerability timer bar (`BossVulnerabilityTimerBar`) provides a visual
  countdown — no reliance on audio alone for time pressure.
- The draw counter (`BossDrawCounterUI`) shows "N / required" — clear progress
  indication.
- Phase failure (timer expiry) repeats the phase with no HP loss — forgiving
  retry within the encounter. No permadeath of progress until hearts reach 0.
- The Superintendent's Decree (label scramble) could be disorienting for
  players with visual processing differences. **Mitigation:** the scramble is
  temporary (phase-scoped, not permanent), and killing the source restores
  labels — consistent with the Kempei pattern the player already learned.

### 3.8 Difficulty progression

The difficulty progression is **cumulative and pressure-based**, not
recognition-based:

1. **Roster breadth:** 8 → 14 → 17 characters. More characters = more
   cognitive load during the vulnerability window (must recognize and draw
   the sampled glyph from a larger mental library).
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

**Recognition threshold remains 0.60** across all encounters (GDD §3.6). The
difficulty is time pressure and volume, not stricter matching.

---

## 4. Acceptance Examples

These examples specify the expected behavior for each critical encounter state.
They map to the existing `BossController` state machine and are testable via
the existing EditMode/PlayMode test infrastructure.

### 4.1 Phase entry

**Given:** Level 5 is loaded, `bossConfig != null`, boss tutorial (if any) has
closed, `WaveManager.RunBossEncounter` has been called.

**When:** `BossController.StartBoss(config, spawner)` executes.

**Then:**
- `GameManager.Instance.CurrentBoss` is set to this `BossController`.
- `EventBus.OnBossStarted(config)` is raised.
- `BossAudio` fades in BGM (if `audioBank.bgm` is non-null).
- State transitions: `Idle → Intro`.
- After `config.introDuration` seconds: state → `SummoningPhase`, phase index
  = 0, `EventBus.OnBossPhaseStarted(0)` is raised.
- `PhaseBasedMovement.StartPattern(phase0)` begins boss movement.
- `BossHealthBar` shows HP = `phases.Count` (3).
- Drawing input is accepted (not suppressed).

### 4.2 Phase failure (vulnerability timer expiry)

**Given:** Boss is in `Vulnerable` state, phase index = 1, `requiredCharacterCount`
= 5, `vulnerabilityTimer` = 18s. Player has drawn 3 correct glyphs
(`_correctDrawsThisWindow = 3`).

**When:** 18 seconds elapse without reaching 5 correct draws.

**Then:**
- `_isVulnerableActiveWindow` becomes `false` (boss no longer targetable).
- `EventBus.OnBossVulnerabilityExpired(1)` is raised.
- `BossAudio` plays `vulnerabilityExpiredLaugh` (if configured).
- `BossStateVisuals.PlayStandUp()` plays — boss stands back up.
- `IsTargetable` returns `false`.
- `BossController` re-enters `SummoningPhase` for phase 1 (same phase, not
  next).
- **HP is NOT decremented.** `HPRemaining` stays at 2.
- `BossHealthBar` unchanged.
- `_correctDrawsThisWindow` resets to 0 on next `RunVulnerable` entry.

### 4.3 Phase retry (after failure)

**Given:** Phase 1 just failed (§4.2). Boss re-enters `SummoningPhase` for
phase 1.

**When:** The summoning → winding-down → vulnerable cycle completes again.

**Then:**
- The player gets another attempt at the same phase with the same
  `requiredCharacterCount` and `vulnerabilityTimer`.
- Glyph sampling re-randomizes from `allowedCharacters`.
- There is **no limit on retries** — the phase repeats indefinitely until the
  player clears it or loses all hearts.
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
- If `currentHearts == 0`: `HeartSystem` raises `OnGameOver` →
  `GameManager.HandleGameOver()` → `DefeatScreenUI` shows. The boss encounter
  is abandoned (see §4.6 on save behavior).

### 4.5 Pause

**Given:** Boss is in `Vulnerable` state, vulnerability timer is counting
down (12.5s remaining), player is mid-draw.

**When:** Player taps the pause button.

**Then:**
- `GameManager.PauseGame()` sets `Time.timeScale = 0`.
- All scaled-time coroutines (`WaitForSeconds`, `Time.deltaTime` loops in
  `BossController`) freeze. The vulnerability timer stops counting down.
- `StrokeCapture` stops accepting input (game is paused).
- `PauseMenuUI` is shown with Resume / Restart / Level Select / Settings.
- `BossVulnerabilityTimerBar` stops draining.
- `BossAudio` BGM continues (AudioSource ignores timeScale by default) unless
  explicitly paused — this is acceptable (BGM during pause is standard).

**When:** Player taps Resume.

**Then:**
- `Time.timeScale` restored to 1.
- All coroutines resume. Vulnerability timer continues from where it froze.
- Drawing input re-enabled.

**When:** Player taps "Return to Level Select" (quit mid-boss).

**Then:**
- `PauseMenuUI.ShouldCachePausedRunSnapshot()` returns `false` (boss
  encounter active → no snapshot cached).
- The run is abandoned. No mid-boss save is written.
- On returning to Level 5/10/15, the encounter starts from the beginning
  (Intro → Phase 1). See §4.7 for atomic save behavior.

### 4.6 Completion (all phases cleared)

**Given:** Boss is in `Vulnerable` state, phase index = 2 (final phase),
`requiredCharacterCount` = 8, player has drawn 7 correct glyphs.

**When:** Player draws the 8th correct glyph (`_correctDrawsThisWindow` reaches
8 ≥ `requiredCharacterCount`).

**Then:**
- `TryRouteDraw` returns `BossRouteResult.Hit`.
- `_correctDrawsThisWindow` = 8.
- `RunVulnerable` calls `onComplete(true)`.
- State → `Damaged`. `HPRemaining--` → 0.
- `EventBus.OnBossDamaged(2, 0)` raised. `BossHealthBar` shows 0.
- `BossStateVisuals.PlayStandUp()` plays.
- `RunEncounter` loop exits (all phases cleared).
- State → `Outro`. `IsDefeated = true`.
- `BossEnemy.PlayDeathAnimationFrames()` plays (if death frames configured).
- After `config.outroDuration` seconds:
  - State → `Defeated`.
  - `EventBus.OnBossDefeated()` raised. `BossAudio` plays defeat clip + fades
    out BGM.
  - `EventBus.OnLevelComplete()` raised.
  - `BossEnemy.ReturnToPool()`.
- `ProgressManager.HandleLevelComplete()` saves stars, unlocks next level.
- `LevelFlowController` plays `outroDialogue` (paragraph restoration).
- For Level 15: Endless Mode unlock flag is set (REQ-33, when implemented).

### 4.7 Atomic save behavior

**Given:** Boss has been defeated, `OnLevelComplete` has been raised.

**When:** `ProgressManager.HandleLevelComplete()` executes.

**Then:**
- Level completion is written to `PlayerPrefs`:
  - `salinlahi.progress.level_{N}_complete` = 1
  - `salinlahi.progress.level_{N}_stars` = computed star count
  - `salinlahi.progress.level_{N+1}_unlocked` = 1 (if N < 15)
- `PlayerPrefs.Save()` is called **immediately** after writing — this is the
  atomic save. Unity's `PlayerPrefs.Save()` flushes to disk synchronously on
  the calling thread.
- If the app crashes after `OnBossDefeated` but before `PlayerPrefs.Save()`,
  the level is **not** marked complete — the player must re-defeat the boss.
  This is acceptable (boss defeat → save is a single-frame operation).
- If the app crashes *after* `PlayerPrefs.Save()`, the level **is** marked
  complete — progress is durable.
- **No mid-boss save exists.** Boss phase progress, current phase index,
  vulnerability timer state, and correct-draw count are **never persisted**.
  This is an intentional design decision (documented in
  `PauseMenuUI.ShouldCachePausedRunSnapshot`). Quitting mid-boss always
  restarts the encounter from the beginning.
- `BossDiscoveryProgress.TryMarkDiscovered(config)` is called when the boss
  tutorial closes (before the encounter), persisting the boss's Almanac
  discovery independently of encounter completion. This is also atomic via
  `PlayerPrefs.Save()`.

---

## 5. Completion Criteria Verification

### 5.1 Compatible with all three era-ending stories

| Era | Story (GDD §4.5) | Encounter | Compatibility |
|-----|------------------|-----------|---------------|
| Spanish (ugat) | "El Inquisidor oversaw the burning of Baybayin manuscripts. Defeating him proves the script still has power." | Level 5 — 3-phase, Spanish-era summons, reinforcement mechanic | ✅ The boss's summoning of Soldado reinforcements *is* the manuscript-burning force. Defeat = script has power. |
| American (ugnayan) | "The Superintendent, an American colonial education administrator wielding institutional erasure." | Level 10 — 3-phase, American-era summons, Decree (label scramble) mechanic | ✅ The Decree scramble *is* institutional erasure (replacing the script with wrong labels). Defeat = script survives institutions. |
| Japanese (pamana) | "Kadiliman, the Darkness itself. The embodiment of cultural forgetting. Drawing all 17 characters restores Baybayin to the world." | Level 15 — 3-phase, cross-era summons, all-17 roster | ✅ Cross-era summons = all three eras of corruption combined. 21 draws from the full 17-character roster = demonstrating total mastery. Defeat = Baybayin restored. |

All three encounters are compatible with their era-ending stories. The
mechanics reinforce the narrative themes.

### 5.2 Compatible with LF-CONTRACT-v2

Verified in the "LF-CONTRACT-v2 compatibility" section above. Summary:
- No new ScriptableObject types or schema changes.
- Vulnerability window samples from `LevelConfigSO.allowedCharacters` (the
  revised `cumulativeSymbolPool`).
- No level beyond 15 is introduced.
- `finalRestorationValue` and `masteryRequirements` are the contract hooks
  for paragraph restoration and mastery validation.

### 5.3 Level 15 remains the final battle

**Confirmed.** No Level 16 or unapproved extra encounter is introduced. The
specification explicitly states Level 15 is the campaign finale:
- `ContentIdentity.RevisedLevelIds` has exactly 15 entries (3 eras × 5 levels).
- `LevelConfigSO.levelNumber` range is 1–15 (GDD §2.4, REQ-24).
- Kadiliman's defeat triggers the ending ("the script returns, the world
  remembers") and Endless Mode unlock (REQ-33).
- No `level.pamana.06` or equivalent ID is generated or referenced.

### 5.4 Final story estimates (revised)

Based on the approved three-phase design, the revised effort estimates for
the three boss encounters:

| Story | Description | Estimate (story points) | Basis |
|-------|-------------|------------------------|-------|
| BL-E3-S4 | Level 5 — El Inquisidor (3-phase) | **1** | Already fully implemented. No work needed. Estimate is verification-only. |
| BL-E4-S5 | Level 10 — The Superintendent (3-phase) | **5** | Data authoring (3 phases, audio bank, tutorial, enemy data) + `BossDecreeEffect` component (~1 day code) + prefab duplication + art/audio asset integration + playtesting. |
| BL-E5-S5 | Level 15 — Kadiliman (3-phase) | **5** | Data authoring (3 phases, cross-era summons, audio bank, tutorial, enemy data) + prefab duplication + art/audio asset integration (most complex art) + playtesting. No new code (cross-era summon is data-only). |
| **Total** | All three Paglimot encounters | **11** | |

**Comparison to pre-spike estimates:** The pre-spike workbook estimate
treated Level 15 as a unique single-phase battle and Levels 5/10 as
non-encounter mastery checks. The approved extension (all three as
three-phase encounters) adds scope to Levels 5 and 10 but reuses the existing
framework so heavily that the net estimate is lower than a from-scratch
three-boss implementation would be. Level 5 drops to 1 (already done). The
new code surface is minimal (one small component for the Decree effect).

**Risk-adjusted estimate:** Add +2 points buffer for art dependency risk
(boss sprites for Superintendent + Kadiliman are external deliverables per
GDD §6.3). **Risk-adjusted total: 13 story points.**

---

## 6. Evidence Index

| Claim | Evidence |
|-------|----------|
| BossController state machine | `Assets/Scripts/Gameplay/Boss/BossController.cs` — `State` enum, `RunEncounter()`, `RunVulnerable()`, `RunOutro()` |
| BossConfigSO schema | `Assets/Scripts/Data/BossConfigSO.cs` — `phases`, `audioBank`, `tutorial`, `bossEnemyData` |
| BossPhase schema | `Assets/Scripts/Data/BossPhase.cs` — `summonPhaseDuration`, `requiredCharacterCount`, `vulnerabilityTimer`, `movementPattern` |
| El Inquisidor fully authored | `Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_ElInquisidor.asset` — 3 phases, audio bank, tutorial |
| Superintendent placeholder | `Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Superintendent.asset` — 1 phase, defaults, no audio/tutorial |
| Kadiliman placeholder | `Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Kadiliman.asset` — 1 phase, defaults, no audio/tutorial |
| Level 15 chapterNumber bug | `Assets/ScriptableObjects/Levels/Level15_Config.asset` — `chapterNumber: 1` (should be 3) |
| No mid-boss save | `Assets/Scripts/UI/PauseMenuUI.cs` — `ShouldCachePausedRunSnapshot()` returns false when `CurrentBoss != null` |
| Atomic save | `Assets/Scripts/Core/ProgressManager.cs` — `HandleLevelComplete()` → `PlayerPrefs.Save()` |
| Boss discovery save | `Assets/Scripts/Core/BossDiscoveryProgress.cs` — `TryMarkDiscovered()` → `PlayerPrefs.Save()` |
| Tutorial system | `Assets/Scripts/Gameplay/Boss/BossTutorialController.cs`, `Assets/Scripts/Data/BossTutorialSO.cs` |
| Audio system | `Assets/Scripts/Gameplay/Boss/BossAudio.cs`, `Assets/Scripts/Data/BossAudioBankSO.cs` |
| WaveManager boss integration | `Assets/Scripts/Gameplay/Wave/WaveManager.cs` — `RunBossEncounter()` |
| Draw routing | `Assets/Scripts/Gameplay/Boss/BossController.cs` — `TryRouteDraw()`, `BossRouteResult` enum |
| Revised campaign contract | `Assets/Scripts/Data/Campaign/ContentIdentity.cs` — `RevisedCampaignId`, `RevisedLevelIds` (15 entries) |
| Paragraph restoration field | `Assets/Scripts/Data/LevelConfigSO.cs:26` — `finalRestorationValue` |
| Mastery requirements field | `Assets/Scripts/Data/LevelConfigSO.cs:28` — `masteryRequirements` |
| ClueMode enum | `Assets/Scripts/Data/Campaign/FocusWordDefinition.cs` — `ClueMode` |
| REQ-25 (boss encounters at 5/10/15) | `docs/system/10_Requirements_Traceability_Matrix.md:50` — Partial (L10/L15 placeholders) |
| REQ-41 (phase-based boss system) | `docs/system/10_Requirements_Traceability_Matrix.md:66` — Implemented |
| GDD boss descriptions | `docs/capstone/GDD.md:193,236-240,254-259` |
| Boss system documentation | `docs/system/04_Gameplay_Systems.md:299-384` — §8 Boss Encounter System |
| Data contracts documentation | `docs/system/05_Data_Contracts_and_ScriptableObjects.md:239-294` — §2.6–2.7 BossConfigSO/BossPhase |
| Kempei scramble (Decree reuse base) | `Assets/Scripts/Data/EnemyDataSO.cs` — `scrambleRadius`, `scrambleMinGlitchInterval`, `scrambleMaxGlitchInterval` |

---

## 7. Open Items (non-blocking)

1. **Level 15 `chapterNumber` bug:** `Level15_Config.asset` has
   `chapterNumber: 1` — should be 3. Fix during data authoring for BL-E5-S5.
2. **Endless Mode unlock (REQ-33):** Not yet implemented. Kadiliman's defeat
   should set the unlock flag, but the Endless Mode scene/system does not
   exist yet. This is a separate story, not part of the encounter spec.
3. **Deterministic 17-character sequence:** The GDD says "drawing all 17
   characters defeats it." The current random-sampling approach covers all 17
   statistically but does not guarantee each is drawn exactly once. If the
   product owner requires a *deterministic* 17-draw sequence for Level 15
   phase 3, `BossController.SampleNextExpectedCharacter` would need a
   shuffle-bag variant. **Recommendation:** keep random sampling — it's
   consistent with the existing framework and the "mastery" intent is
   satisfied by drawing from the full roster under pressure.
4. **`BossDecreeEffect` implementation:** The Superintendent's signature
   mechanic needs a thin code wrapper. Estimated ~1 day. This is the only
   new code in the entire spike scope.
5. **Art/audio dependencies:** Superintendent and Kadiliman sprites + audio
   banks are external deliverables (GDD §6.3, Art Batch 3 / boss BGM by End
   of Week 7). The encounter specs are blocked on these for shipping but not
   for implementation (placeholder sprites/audio work via null-tolerance).

---

*End of TW-SPK-003. This spike is complete when the three encounter
specifications, reuse assessment, scope statement, acceptance examples, and
revised estimates are reviewed and approved by the product owner.*
