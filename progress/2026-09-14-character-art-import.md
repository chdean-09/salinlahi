# 2026-09-14 — Replacement character art: import + wiring

Branch `feature/game-file-updates` (== local `dev` == `origin/dev` at `c3de3e60`). Nothing committed; see "Commit set" below.

## Done (serialized wiring verified in the live Unity 6000.3.9f1 Editor via the repo MCP relay)

| Character | Action | Identity path |
|---|---|---|
| Hati (HA) | Replaced `Assets/Art/Characters/Enemies/Corruption/sprite_enemy_hati_walk-Sheet.png` in place; `.meta` untouched | `EnemyData_Hati.walkFrames` resolves 4 sprites, same GUID `1fc0821d…`, same sprite IDs; Almanac uses `walkFrames[0]` (portrait null) |
| Iligaw (E/I) | same, `sprite_enemy_iligaw_walk-Sheet.png` | `EnemyData_Iligaw` resolves; GUID `b8fb579c…` |
| Uhaw (O/U) | same, `sprite_enemy_uhaw_walk-Sheet.png` | `EnemyData_Uhaw` resolves; GUID `e20bd411…` |
| Kadena (KA) | same, `sprite_enemy_kadena_walk-Sheet.png` | `EnemyData_Kadena` resolves; GUID `7cc5776d…` |
| Ragasa (RA) | **Import-only**: new `sprite_enemy_ragasa_walk-Sheet.png` + hand-authored meta (4 sprites `ragasa_walk_01..04`, PPU 192) | No `EnemyData_Ragasa`, no stats/ability/spawn — per handoff, needs a playable-enemy contract |
| Hati minions | **Import-only**: new `sprite_enemy_hati_minion_walk-Sheet.png` (+meta) | No split/minion runtime exists (`grep split` hits only UI copy); nothing to wire |
| Juan (protagonist) | **Attack only**: new `Assets/Art/Characters/Protagonist/sprite_prot_juan_attack-Sheet.png` (8 × 512×576, PPU 72, custom pivot 0.5/0.415, alignment 9) + meta; `Assets/Animations/Protagonist/ProtagonistDraw.anim` rewritten to 8 keys @ 12 fps (0.667 s, was 3 keys @ 4 fps / 0.75 s) | Shared `Protagonist.prefab` → `ProtagonistAnimator.controller` → `Draw` state → this clip; every map inherits. Idle untouched (`sprite_prot_japanese_idle_back-Sheet.png`, `ProtagonistIdle.anim`). |

Why in-place replacement works: every existing enemy walk sheet is already 2048×2048 = 2×2 cells of 1024×1024, row-major from the top, exactly the layout of the supplied `spritemotion-spritesheet-*.png` files (all RGBA with real transparency). Only the PNG bytes changed.

Juan scale: old idle is 32 px @ PPU 6 × prefab scale 0.2 = 1.07 u. Juan body ≈ 388 px @ PPU 72 × 0.2 = 1.08 u; pivot y 0.415 puts feet on the idle's feet line (offline overlay checked, `juan_scale_preview.png` in session scratchpad).

## Findings / discrepancies vs handoff

- **Frames zips in `~/Downloads` are mislabeled**: `Hati (HA)/…frames.zip` = Kadena chain golem; `Hati minions/…frames.zip` = the three-mask Hati; `Iligaw (I:E)/…frames.zip` = single-mask minion. Uhaw and Kadena zips match. Ragasa has no zip. The **spritesheet PNG in each folder matches the folder name** and was used instead. Owner should confirm the sheet identities (Hati = three-mask tendril figure, minion = single-mask smaller figure, Iligaw = spiked diamond mask, Ragasa = quadruped beast).
- Handoff says branch `dev`; checkout is `feature/game-file-updates` at the same commit. Local `dev` = `origin/dev`.
- Handoff blocker "Unity another project instance / stale process" is stale: Editor PID 96123 is live on this project and holds `Temp/UnityLockfile`; batch `-runTests` therefore cannot run, but the MCP relay (`node scripts/unity-mcp-launcher.js`) works. Note: `Unity_RunCommand` `Debug.Log` output is **not** returned by `Unity_GetConsoleLogs`; write results to a file from C# instead.
- Undocumented working-tree work not in the handoff's "Completed work" table: `LevelReadyScreenController.cs`, `NawalangMukhaNameLossController.cs`, `NameLossEffectRegistry.cs`, `LevelReadyAndNameLossTests.cs`, `EnemyData_NawalangMukha.asset`, `OnboardingSequenceSO.cs`, `Enemy.cs`, `EnemyDataSO.cs`, `DefeatScreenUI.cs`, `DialogueController.cs`, `SymbolLearningCardController.cs`, `FocusWordPreviewController.cs`, the four `Level1TutorialStep_*.asset`. Master row 10 (Ready/checkpoint screen, audited MISSING) may now be partly addressed by `LevelReadyScreenController` — unverified.
- An EditMode test run happened in the open Editor at 12:49 (console shows `VictoryScreenResultsTests`); it left `Assets/InitTestScene…unity(.meta)` untracked. Not mine; Unity normally removes them after the run.
- `docs/audit/STATUS-2026-09-13.md` is untracked (whole `docs/audit/` is), so the "canonical status file" is not in git either.

## NOT VERIFIED

- Runtime playback: enemies walking with the new frames in Play Mode, Almanac/discovery cards, Juan's Draw firing on attack, no Animator warnings. Editor is in use (test run), and Play Mode via relay would domain-reload the user's session.
- Unity Test Runner (EditMode/PlayMode) for this and for the pre-existing Levels 1–5 changes. `AlmanacEnemyRegistryTests.cs` is the only test touching `walkFrames`.
- Console error scan: relay returned only 53 pre-existing entries (test-run warnings); none referenced the new assets.

## Open decisions (owner)

1. `ProtagonistSlashVfx.prefab` still plays the **old** 3 draw frames (`sprite_prot_japanese_draw-Sheet.png`) rotated toward the target. Keep, or give it Juan-style brush-stroke frames?
2. Ragasa playable contract (stats, ability, spawn rules, roster, `EnemyData_Ragasa`, Almanac entry).
3. Hati split/minion runtime — no implementation exists; minion art is parked.
4. Juan idle art still pending; idle/attack currently mix art styles (old 25-px pixel figure vs new Juan).
5. Paragraph auto-fill design (unchanged from handoff).

## Commit set for this task (do not mix with the other working-tree areas)

```
Assets/Animations/Protagonist/ProtagonistDraw.anim
Assets/Art/Characters/Enemies/Corruption/sprite_enemy_{hati,iligaw,uhaw,kadena}_walk-Sheet.png
Assets/Art/Characters/Enemies/Corruption/sprite_enemy_ragasa_walk-Sheet.png(.meta)
Assets/Art/Characters/Enemies/Corruption/sprite_enemy_hati_minion_walk-Sheet.png(.meta)
Assets/Art/Characters/Protagonist/sprite_prot_juan_attack-Sheet.png(.meta)
progress/2026-09-14-character-art-import.md
```
