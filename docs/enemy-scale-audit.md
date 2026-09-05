# Enemy scale audit — Salinlahi

**Date:** 2026-09-06 · **Branch:** `dev` · **Verified in:** `Assets/_Scenes/Gameplay.unity`, rendered
at 1080×1920 (9:16 portrait) via Unity batchmode, with prefabs resolved the same way `EnemyPool` does.

---

## 0. Two rosters, one shared shell

The project ships **two enemy rosters** and they are sized by completely different mechanisms:

- **Corrupted** (17 creatures — Abo ng Simula, Iligaw, Mantsa, Bakod, Hati, Gapos, Kadena, Punit,
  Salungat, Takip, Uhaw, Ngatngat, Nawalang-Mukha, Walang-Awa, Yapos ng Dilim, Labo, Daan-Lihis).
  These are the current design direction. **They have no prefabs.** Each is an `EnemyDataSO` holding
  its own `walkFrames`, and `Enemy` overwrites the renderer's sprite from that array at spawn
  ([Enemy.cs:221](../Assets/Scripts/Gameplay/Enemy/Enemy.cs)).
- **Colonial** (Soldado, Guardia, Fraile, Soldier, Heitai, Kempei, Kisha, Maestro, Pensionado,
  General, Capitan, Shokan, + boss El Inquisidor). One prefab each, registered by `enemyID` in
  `[Manager] EnemyPool`.

**The trap:** an `EnemyDataSO` whose `enemyID` is not in `_registeredEnemyPrefabs` falls through to
the pool's `_enemyPrefab` — which was **`[Enemy] Soldado.prefab`**. So 14 of the 17 corrupted
creatures were being instantiated on the colonial Soldado shell and inherited *its* transform scale.
The corrupted roster had no size control of its own at all.

### Which levels use which

| Era | Levels | Corrupted | Colonial |
|---|---|---|---|
| Era 01 | 1–4 | L1, L2, L3 | L1, L2, L3, L4 |
| Era 02 | 6–9 | L6 | L6, L7, L8, L9 |
| Era 03 | 11–14 | — | L11–L14 |

Both rosters ship in `CampaignConfig_RevisedV1`; colonial enemies still appear in 11 of 15 levels.
The tutorial (`Level1TutorialStep_*`) uses corrupted only: Bakod, Hati, Uhaw.

---

## 1. Measured baseline

The play column is fixed by `AspectLockedCamera` at **11.25 × 20 world units**, so 1 world unit
≈ 96 px on a 1080×1920 phone.

Both rosters are deliberately normalized to the same authored frame size — colonial art is 32 px at
**PPU 6**, and `CorruptionEnemyBootstrap` imports the corrupted sheets at **PPU 192 so 1024 px spans
5.333 world units, identical to a 32×32 frame at PPU 6**. Everything sat at `localScale 0.30`
(boss 0.50), which put the entire cast at roughly **1.2–1.6 units tall — 6–8 % of screen height**.

### Why it read as "miniscule"

1. **The glyph badge is 1.70 × 1.86 world units** (113×124 px at PPU 100, `defaultWorldScale` 1.5).
   The label you read was **larger than the creature wearing it**, with its lower edge ~0.3 units
   inside the head. The enemy read as an accessory to its own UI.
2. **6–8 % of screen height** is roughly a fingernail on a phone — for a target you must identify
   *and* draw a glyph for under time pressure.
3. For the corrupted set specifically, the artwork is **painted at 1024², far denser than the 6-PPU
   colonial pixel art**. Detail that dense needs more screen area before it resolves at all — which
   is why the two rosters do not want the same scale.

It was *not* a runtime problem: `WaveSpawner` only sets position, never scale.

### Two layout facts worth recording

- Enemies do **not** walk in three fixed lanes. `WaveSpawner.SpawnEnemy` picks
  `Random.Range(minX, maxX)` across the outermost `SpawnPoint` transforms — a **continuous band
  x ∈ [−2.4, +2.4]**. Width is bounded by mutual clutter and by the dirt corridor (≈ ±3.9), not by
  lane pitch.
- The enemy `Collider2D` is **base-contact only** — the sole `OnTriggerEnter2D` in the codebase is
  `EnemyMover`'s `PlayerBase` check. Enemies are defeated by drawing a glyph, not by tapping, so
  collider width is not a hit target. Its height does shift *when* a base hit registers, and it
  scales with the transform, so a larger enemy connects marginally sooner — which is correct.

---

## 2. Applied sizes

### Corrupted roster — one shared scale, by design

`CorruptionEnemyBootstrap` normalizes every sheet into the same 5.333-unit frame, and the artists
drew each creature at its intended relative size *within* that frame. The size hierarchy is
therefore already authored in-frame (visible content ranges from Bakod at 4.10 units to Hati/Uhaw at
5.33 — a 30 % spread), and **one shared scale is the correct answer**: it preserves that hierarchy
exactly and gives per-creature variation for free.

| | Scale | Spawn band | New world size | % screen h |
|---|---|---|---|---|
| All 17 corrupted creatures | 0.30 → **0.68** | ±2.4 → **±2.14** | 2.55–3.52 w × 2.79–3.63 h | 14–18 % |

**How 0.68 was reached — and why not 0.80.** The team first chose 0.80 from a rendered
0.56 / 0.68 / 0.80 comparison. That comparison used *hand-spaced* enemies and was therefore
misleading. Re-tested against a simulated real wave — Level 6's tightest (6 enemies, 2.1 s interval),
real per-enemy `moveSpeed`, real `Random.Range` x, 400 trials per config, counting an enemy lost when
≥ 50 % of its sprite **area** is covered by enemies above it:

| scale | band | avg enemies hidden (of ~5.8) | % of waves affected |
|---|---|---|---|
| 0.48 | ±2.40 | 0.26 | 23 % |
| 0.56 | ±2.45 | 0.39 | 32 % |
| **0.68** | **±2.14** | **0.68** | **51 %** |
| 0.74 | ±1.98 | 0.83 | 59 % |
| 0.80 | ±1.83 | 1.07 | 69 % |
| 0.80 | ±1.70 | 1.12 | 71 % |

At 0.80 roughly one on-screen enemy in five was more than half covered — and each one is a glyph the
player cannot read. 0.68 halves that while still being 2.3× the shipped size.

**Two traps this exposed.**

1. **Narrowing the spawn band to fix treeline clipping made occlusion worse** (0.90 → 1.12 hidden).
   Lateral separation is what keeps vertically-overlapping enemies readable, so the band and the
   scale pull in opposite directions. The band is now set so the widest creature (Bakod, 3.52 units
   at 0.68) just touches the dirt edge at ±3.9 — **±2.14**. That is the maximum legal band at this
   scale, i.e. the most lateral separation available.
2. **No band rescues 0.80.** Widening for separation pushes creatures into the treeline; even the
   widest legal band for 0.80 (±1.83) still leaves 69 % of waves with a lost enemy.

**Root cause of the clumping is pre-existing, not scale.** Level 6 mixes `moveSpeed` from 0.85 to
1.9 in a single wave at a 2.1 s interval, so fast enemies catch slow ones and pile up. Scale only
decides whether the pile is "overlapping but legible" or "one mass". Raising that interval
2.1 → 3.0 s would bring even 0.80 down to 0.54 hidden / 44 % — but that is a difficulty and pacing
change across the level configs and should be playtested, not simulated. See §7.

**Badge offset 2.5** on all 17 `EnemyDataSO` assets, via the existing `overrideBadgeOffset`
mechanism. The global default (1.9) stays tuned for the colonial tier at 0.48. 2.5 reproduces the
shipped badge-tucked-on-the-head look (~0.28 units of overlap) at 0.68 — A/B'd at 0.80 against a
value that fully clears the silhouette, which read worse because the badge-to-creature pairing
became ambiguous with several on screen. All 17 share one value because every sheet's content
reaches the top of its 1024 frame (measured: bbox top = 0 for all 17).

**Structural change this required:** `[Enemy] Corrupted.prefab` (new, copied from Soldado) is now the
pool's `_enemyPrefab`, so the corrupted roster is sized independently of the colonial Soldado.
`[Enemy] Labo` and `[Enemy] Daan-Lihis` are corrupted creatures that *do* have registered pools
(Soldado variants), so they carry an explicit 0.68 override.

### Colonial roster — per-tier, because their art encodes a hierarchy across sheets

Unlike the corrupted set, the colonial sheets vary in authored size *between* files (grunt 30–31 px →
officer 46 px → elite 48 px). A flat ×1.6 preserves that, trimmed where it costs width or crowds the boss.

| Enemy | Role | Scale | New size | Why |
|---|---|---|---|---|
| Soldado, Guardia, Soldier, Heitai, Kempei, Kisha | rank & file | 0.30 → **0.48** | ~1.5 × 2.48 | ×1.6 baseline; verified in a 1.0/1.4/1.6/1.8 sweep — 1.8 crowded |
| Fraile, Pensionado, Maestro | civilian silhouettes | 0.30 → **0.48** | 1.4–1.9 × 2.3–2.5 | Same tier; their sheets are authored 1–2 px shorter/narrower, so they stay the slighter figures without a special case |
| General | officer | 0.30 → **0.45** | 2.18 × 3.45 | Trimmed below baseline — at 0.48 it hits 3.68 and competes with the boss's 4.08 |
| Capitan, Shokan | elite | 0.30 → **0.42** | 2.4–2.6 × 3.36 | Capped hardest: widest sheets (34/37 px on 48-cells). A flat ×1.6 puts Shokan at 2.96 wide inside a 4.8-unit spawn band — two would occlude each other's badges |
| Boss — El Inquisidor | boss | 0.50 → **0.62** | 5.89 × 5.06 | Kept from the 0.80 pass. At 0.68 the tallest corrupted creature is 3.63, so an unbumped boss (4.08) would lead by only 12 % — too little for a boss silhouette. At 0.62 it leads by 39 %. Badge override moved −3.5 → −4.3 to track the taller body. Safe to revert to 0.50 if the team prefers |
| Protagonist | hero | 0.30 → **0.48** | 1.84 × 2.56 | **Required, not extra.** Hero and grunts were both 0.30; raising only enemies would leave the hero at 64 % of a footsoldier. Same factor preserves the hero-to-grunt ratio (1.03×) and lifts its head clear of the shrine fence. Feet land at y −9.78 against a floor of −10, and `AspectLockedCamera` never shrinks the view below 20 units tall |

## 3. Supporting change

**`GlyphBadgeConfig_Default.defaultWorldOffset.y`: 1.4 → 1.9.** The badge is anchored in *world*
space and does not follow a taller sprite, so without this it would cover the head and torso at every
new size. 1.9 reproduces the existing badge-on-the-head overlap (~0.28 units) for the grunt tier and
needs no per-enemy overrides — the General and the elites land within 0.08 units of their present
relationship, and the boss keeps its own override.

`_labelBaseWorldOffset.y` went −1.4 → −1.9 on the enemy prefabs and the `Enemy.cs` default, mirrored
below the sprite. Those labels are compiled out of release builds (`ShouldShowDebugLabels`) — this
only keeps the editor view legible.

Nothing else needed touching: hit VFX derive position from `sr.bounds`, and the glyph badge and debug
labels already recompute a world-stable local scale from the parent every `LateUpdate`.

## 4. Verification

- **Pool-accurate renders** of the Level 1, Level 3 and Level 6 rosters — each enemy resolved through
  the live `EnemyPool` wiring (registered `enemyID` → its prefab, else `_enemyPrefab`), with real
  glyph badges laid out using the shipped config and the same math as
  `EnemyGlyphBadge.RecomputeBaseFromParentScale`.
- **Scale sweeps** rendered for both rosters before committing to a value.
- **Boss frame** rendered against 0.48 grunts.
- Test results recorded in §6.

## 5. Roster decision (settled 2026-09-06)

**Both rosters ship. The colonial values in §2 stand.** Confirmed with the team — the colonial
enemies are not being retired, and they remain the roster for Level 4 and all of Eras 02–03
(levels 7–9, 11–14).

This is what makes the split default-pool shell load-bearing rather than incidental: the two sets
are authored differently (colonial art varies in size *between* sheets at PPU 6; corrupted art is
normalized into one 5.333-unit frame at PPU 192 and varies *within* it), so they need independent
scales. Before this change they shared `[Enemy] Soldado.prefab` and could not be tuned apart.

If a future migration does retire the colonial set, the thing to remove is the per-tier tuning in
§2 plus the colonial enemy references in the level configs — not the shell split, which the
corrupted roster needs regardless.

## 6. Test results

Run after every change above, against the live prefab/scene/data values:

- **EditMode: 850 / 850 passed** — includes `EnemyPoolRegistrationTests` and the level-to-`EnemyPool`
  enemy-id contract guard, so re-pointing `_enemyPrefab` did not break pool registration.
- **PlayMode: 172 passed, 0 failed, 1 inconclusive.** The inconclusive `ElInquisidorTest` needs
  `Resources/Test/Level5_ElInquisidor_TestRig.asset`, a fixture that has never existed in the repo —
  pre-existing and unrelated to scale. Note the PlayMode runner exits **2** when anything is
  inconclusive; the XML result is `Passed`. Parse the XML, not the exit code.

## 7. Wave bunching — fixed

The clumping that capped the scale at 0.68 is a spawn problem, not a scale problem: a wave rolls its
enemy type independently per spawn (`SelectEnemyDataForSpawn`), and Level 6's roster spans
`moveSpeed` **0.85–1.9**, so a fast enemy rolled late catches a slow one ahead and the pair stacks
into one unreadable silhouette. 23 % of waves lost an enemy to this even at the colonial 0.48.

Measured levers (Level 6's tightest wave, 6 enemies at 2.1 s, 600 trials each):

| lever | avg hidden | % waves | wave duration |
|---|---|---|---|
| baseline | 0.75 | 56 % | 30.6 s |
| raise interval to 3.0 s | 0.44 | 36 % | 35.1 s |
| minimum lateral spawn separation, 1.8 u | 0.27 | 24 % | 30.7 s |
| speed-ordered spawning (fastest first) | 0.00 | 0 % | 30.6 s |
| **both** | **0.00** | **0 %** | **30.6 s** |

Both are applied. Neither costs wave duration, and neither changes a difficulty input — same
enemies, same counts, same intervals.

**1. Speed-ordered spawning** (`BuildSpawnOrder`, toggle `_spawnFastestFirst`). The wave's types are
rolled up front and sorted fastest-first, so a later spawn is never faster than the one ahead of it.
Their gap can then only grow, and by the time the follower descends into view it has already
separated. This is what drives occlusion to exactly zero — it removes catch-up rather than
compensating for it.

The sort is **stable**, so equal-speed enemies keep the order they were rolled in. That matters:
Eras 02–03 run one or two speeds, where ordering is correctly a no-op rather than a reshuffle, and
even on Level 6 ties preserve variety.

*Trade-off, accepted deliberately:* every wave's speed profile is now monotonically fast→slow rather
than random. That is a consistent rhythm across all 15 levels. `_spawnFastestFirst` turns it off
without a code change if playtesting disagrees.

**2. Minimum lateral spawn separation, 1.8 u** (`PickSpawnX`, toggles
`_minLateralSpawnSeparation` / `_lateralSeparationAttempts`). A spawn re-rolls its X a bounded
number of times to land clear of the previous spawn, falling back to the last roll so a band
narrower than the separation can never stall a spawn. Ordering already removes catch-up, so this is
now defence in depth — it still covers same-speed pairs and anything that reintroduces mixed
ordering (a resumed wave, or the toggle being turned off).

1.8 u is a tuned optimum, not a floor: **larger is worse**. Past roughly half the spawn band, spawns
ping-pong between the two edges and every second pair lines up again (3.0 u measures 0.46 / 39 %).

Effect across every corrupted-bearing wave (500 trials each):

| wave | original | + separation | + ordering (shipped) |
|---|---|---|---|
| L1 w5 (10 @ 2.5 s) | 0.34 (32 %) | 0.04 (4 %) | **0.00 (0 %)** |
| L2 w2 (6 @ 2.8 s) | 0.21 (21 %) | 0.18 (18 %) | **0.00 (0 %)** |
| L3 w1 (5 @ 2.0 s) | 0.30 (26 %) | 0.10 (10 %) | **0.00 (0 %)** |
| L6 w4 (6 @ 2.1 s) | 0.67 (52 %) | 0.29 (25 %) | **0.00 (0 %)** |

With catch-up removed, the corridor could now carry enemies larger than 0.68 — the 0.80 that was
rejected in §2 was rejected on occlusion, and that constraint is gone. Worth revisiting against
playtesting rather than simulation, since the remaining limits (treeline clipping at the band edge,
boss silhouette lead) are judgement calls.

## 8. Other follow-ups (not applied)

- **Art-style gap.** The corrupted set is painted at 1024², the environment and protagonist are
  6-PPU pixel art. Enlarging the creatures makes that contrast more visible.
- **Boss scale is optional.** 0.62 was introduced when the corrupted set was at 0.80. At 0.68 the
  boss would still be largest at its original 0.50, just by a slimmer margin.

## 9. Method note

The size sweeps that informed the first two rounds placed enemies at hand-picked, well-separated
positions and made every scale look viable. They were wrong about density. Anything that changes
on-screen entity size should be checked against a **simulated real wave** — real spawn interval, real
per-entity `moveSpeed`, real random x, measuring 2D area occlusion — before a value is chosen.
