# Progress delta — register cut at `80aa29ec`, re-verified at `9198e8f4`

The user-story register and [`../../jira-import-tasks.csv`](../../jira-import-tasks.csv) were written against local `dev` @ `80aa29ec` (2026-09-15 02:18). By 15:47 the same day `dev` had advanced **36 commits** to `6fc34851` — the Level 1 enemy-introduction lesson branch merged, plus the HUD restoration-rail pass and the Almanac revision — and `feature/ugat-2-5-realignment` added **4 more** on top.

This file records what that 40-commit window changed in the register, and the evidence for each call. Every claim below was checked against the tree at `9198e8f4`, not inferred from a commit subject.

---

> **Where this list now lives.** `jira-import-tasks.csv` was subsequently rebuilt from [`PLAYER-JOURNEY.md`](PLAYER-JOURNEY.md) (27 epics + 154 stories). The reconciled engineering task list this document describes is archived at [`../backlog/engineering-tasks-2026-09-15.csv`](../backlog/engineering-tasks-2026-09-15.csv).

## Tasks closed — removed from `jira-import-tasks.csv`

| Was | Evidence |
|---|---|
| Merge `feature/level1-enemy-introduction-lesson` into dev | `da754394`; `enemyLessons` present in `dev:Assets/Scripts/Data/LevelConfigSO.cs` |
| Delete the SoloTeachBeat teach loop | `f2c7fe60`; `SoloTeachBeat.cs` deleted (−233), no `basicTeachSteps` in the tree, no `SoloTeach` reference in `Level1OnboardingSequence.asset` |
| Hide enemy debug labels | `d4f98b0a`; `Enemy._showDebugLabels` defaults false and no prefab authors `_showDebugLabels: 1` |
| Assign `_slashVfxPrefab` on the ProtagonistManager prefab | `7395d66a`; `[Manager] ProtagonistManager.prefab:62` carries a non-zero guid |
| Re-author `Challenge_Ugat03_Context` with two blanks | `b35e2119` |
| Re-author `Challenge_Ugat04_Context` with two blanks | `b35e2119` |
| Rewrite the Level 3/4 context lines in the narrative doc | `b35e2119`; "Isang salita lamang ang kulang" is gone from both the assets and `docs/content/ugat-levels-2-5-narrative.md` |
| Merge `feature/SALIN-205-ugat-narrative-content` into dev | the content is already on `dev` as `a7399633`; all twenty `Dialogue_Ugat0*.asset` are present and `Level5_Config` is wired. `git cherry` shows the branch's own commit unmerged — the branch is a stale duplicate, not missing work |
| Produce glyph badge sprites for the symbols missing badge art | `e9b1a84d`; 18 sprites in `Assets/Art/UI/GlyphBadges`, `badgeSprite` non-null on all 18 `Char_*.asset` (negative-controlled: the field is present and matched on all 18) |
| Produce Almanac portrait sprites for the symbols missing them | `19825d0c`; 18 `*-Almanac.png`, `almanacSprite` non-null on all 18 |

Two of these — the badge and portrait art — were **already stale when the register was cut**: `e9b1a84d` predates `80aa29ec`. The "7 of 18 at last audit" figure was carried forward from an older audit rather than re-checked.

## Tasks whose premise changed — rewritten, not closed

- **Delete `EnemyDiscoveryOnboardingController` and `EnemyDiscoveryOverlay.prefab`.** The deletion ruling rested on both being dead code. `eea01436` corrected that: the controller is live in `Gameplay.unity` and `Level_01_Tutorial.unity` and is the sole writer of `EnemyDiscoveryProgress`, which the Almanac reads. The actual conflict — its `EnterDialoguePause` firing on the same spawn as `EnemyIntroductionBeat`, which promises live input — was fixed by `f41fc858` keeping the record and skipping the pause, spotlight and panel on introduction spawns. Rewritten as a scope decision.
- **Play Level 2 first-time entry and record the onboarding once-only behaviour.** `8131a6b8` cleared `Level2_Config.onboardingSequence`. Level 2 now carries no onboarding at all, so the acceptance criterion could never be met. Rewritten to record the level with no sequence, and its retuned spawn pacing.
- **Sequence wave-start overlays.** Three of four contributors addressed (`8debc76a`, `f41fc858`, and the beat adopting the scene's spotlight rather than creating a second). The wave banner and tutorial overlay still open in the same frame.
- **Play a fresh Level 1 and record the eight-beat lesson.** The lesson moved from Abo ng Simula to Iligaw in `d66d4db4`; `Level1_Config.enemyLessons` references `IligawLesson.asset` and `AboLesson.asset` stays on disk unreferenced.
- **Replace `EnemyDataSO.Era` values.** Recorded the hazard `428da0ef` named: `GeneralAura` reads `Era` to decide who gets its speed buff and all nineteen non-boss enemies sit at era 0, so every value written during the migration changes behaviour.

## Work the window created — added to the CSV

- Correct the `OnboardingSequenceSO` tooltip that still names Level 2 as the first chain-on level.
- Give the Ugat 3 and 4 two-blank units a second `evidenceContentId` — one id cannot evidence two words.
- Run the SALIN-188 language and cultural review on the re-authored Ugat 3/4 Filipino.
- Produce scrambled badge art for the eighteen characters (`EnemyGlyphBadge.cs:41` names its dim-and-tint fallback as interim).
- Decide whether the tutorial guide's Skip affordance ships — `6fc34851` recorded that no caller passes `canSkip: true`.

## Story statuses changed

| ID | Was | Now | Why |
|---|---|---|---|
| `ENM-29` | Partial | Existing | both suppressors gone (`f2c7fe60`, `5779ae84`); lesson on `dev` |
| `REST-12` | Unclear | Existing | `b35e2119` re-authored both assets and the doc line together |
| `HUD-11` | Missing | Partial | defect 7 fixed; defect 6 unchanged; defect 9 partly addressed |
| `FLOW-18` | Partial | Missing by ruling | Level 2's onboarding sequence was removed, not wired |

## Checked and deliberately **not** changed

- **In-combat tutorial text size (defect 6).** The 38–56 pt auto-size band in `Level1TutorialGuideUI.ConfigureTextBand` is from `0471d89c`, which **predates** the playtest that found the text unreadable. Nothing in the window changed it.
- **Main-menu button spacing (defect 10).** `3bbd9021` reworked `MainMenu.unity`, but the adjacent-button clear gaps still measure 28 / 2 / 30 / 35 / 30 px against an acceptance criterion of equal within 2 px. `SandboxModeButton` still exists.
- **Listen control styling, discovery card truncation, Level Select avatar, Full Rect warning.** No commit in the window touches them, and none is decidable from the serialised scene alone — they need a visual check.
