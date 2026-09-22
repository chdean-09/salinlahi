# Salinlahi implementation status — campaign QA hardening

**Date:** 2026-09-22
**Branch:** `bugfix/campaign-qa-hardening`
**Starting commit:** `7149b175098c8baa79a14e49ec36f29c7b228f4d`
**Unity target:** `6000.3.9f1`

This is a current implementation record, not a replacement for dated audit evidence. It records
what was changed on this branch and what was or was not verified. A static check is labelled
`OBSERVED`; a Unity result is labelled `PASSED` or `FAILED` only when the Test Runner or a manual
run actually completed.

## Batch 2 reconciliation

| Regression | Test/contract present | Current verification | Status |
|---|---|---|---|
| Instant-victory versus Game Over race | `LevelFlowControllerTests.InstantWinCompletion_LateGameOverCannotReplaceVictory`, `LevelFlowControllerPhaseTests` | Focused Edit Mode group `LevelFlowControllerTests`: 14/14; Play Mode phase suite: 39/39 | `PASSED` |
| Level 5 final-TA gate and bounded overflow | `GatedFinaleCampaignOptInTests`, `Batch2ProductionAssetContractTests`, `FinalWaveIndexTests`, `OverflowBudgetTests` | Focused groups: 8/8, 7/7, 10/10, and 8/8; Level 5 contract included | `PASSED` |
| Exact Levels 2–5 production contracts | `Batch2ProductionAssetContractTests` | Unity Edit Mode run: 7/7; Level 10/13 carrier authoring corrections were required before green | `PASSED` |
| Recognizer-driven correct/wrong/miss | `StrokeReplayRecognitionPlayModeTests` | Play Mode fixture uses the real recognizer and downstream path: 3/3 | `PASSED` |
| Defeat → retry → pooled-enemy cleanup | `EnemyPoolLifecyclePlayModeTests.DefeatThenRetry_ReturnsPoolBeforeTheNextAttempt`, `Level1EndToEndTests` | Pool lifecycle: 4/4; Level 1 defeat→retry→complete: 2/2 | `PASSED` |
| Completed-level replay | `VictoryScreenResultsTests.ReplayPressed_DoesNotAdvanceTheSelectedLevel`, with `NextLevelPressed_StillAdvances_ProvingTheReplayGuardDiscriminates` as the control | Included in the full Edit Mode run: 1,469/1,469 passed. The test invokes the production replay handler with a completed level and verifies the selected level remains unchanged; the advancing control proves replay and next-level routing are distinct | `PASSED` (manual scene-button replay remains `BLOCKED`) |

The manual completed-level replay interaction is not counted as a pass. Historical XML and earlier
suite totals remain historical evidence only.

## Last repository-level verification

* `dotnet build Salinlahi.Runtime.csproj --no-restore -v:q` — `PASSED` (0 warnings, 0 errors;
  this is a generated non-Unity static check, not a Unity compilation or Test Runner result).
* `git diff --check` — `PASSED`.
* Unity full Edit Mode suite — `PASSED`: 1,469/1,469, 0 failed, 0 inconclusive.
* Unity full Play Mode suite — `PASSED`: 230/230, 0 failed, 0 inconclusive. The first complete run
  exposed one timing-sensitive `Phaser_PulsesBeforeBecomingInvisible` failure (229/230); its isolated
  rerun passed 1/1, and the subsequent complete rerun passed 230/230.
* After the proven-safe asset cleanup, one intermediate full Edit Mode run reported 1,461/1,469
  with eight onboarding failures while the focused onboarding fixture remained green (22/22). A
  clean-console full rerun completed 1,469/1,469; the intermediate result is retained as an
  order/timing observation, not a confirmed production defect.
* Branch/worktree review — `PASSED`: branch is `bugfix/campaign-qa-hardening`; no staged files;
  no `Packages/`, `ProjectSettings/`, generated Unity project, GUID, fileID, save-schema, or
  unrelated generated-file changes were detected.

## Current implementation changes

* Levels 6–14 wave rosters now carry every symbol used by their authored focus words, together with
  a natural enemy carrier for each symbol. Wave counts, delays, intermissions, and segment fields
  were preserved. The read-only contract in `Batch2ProductionAssetContractTests` and the full Edit
  Mode suite cover this invariant; the Level 10 NA and Level 13 YA carriers were corrected in the
  serialized assets before the 7/7 contract run.
* Levels 6–15 asset contracts now pin stable identity, era order, focus/pool/reward/challenge data,
  current challenge modes, boss topology, and the Level 10 non-boss / Level 15 sole-boss ruling.
* The current authored challenge mode map is explicit: Levels 6 and 8–9 use sentence restoration;
  Level 7 uses a word-placement unit followed by a sentence unit; Level 10 and 15 use paragraph
  units; Levels 11–12 use word units; Levels 13–14 use sentence units. This records live assets;
  the older D1 validator table remains covered by synthetic rule tests and is not silently treated as
  a product ruling.
* Wave terminal-range, finale-gate, and overflow predicates are now delegated to the internal pure
  `WaveTerminalPolicy`; existing `WaveManager` seams remain as compatibility wrappers.
* Restoration-rail scaling values are computed by the internal pure `RestorationRailLayoutPolicy`;
  `ActiveCluePresenter` still owns Unity objects, lifecycle, and event wiring.
* Runtime-only test seams were narrowed from `public` to `internal`, using the existing friend test
  assemblies. `EnemyIntroductionBeat.ResetTestState` clears all static onboarding test state.
* The BossPhase migration test now imports a freshly rewritten copy of the checked-in one-phase
  BossConfig asset, so it exercises legacy field names without mutating a live ScriptableObject.
* The empty, unreferenced `Assets/Settings/InputActions.inputactions` fixture and its `.meta` were
  removed after checking all serialized/code/test/history references. The active input asset remains
  `Assets/InputSystem_Actions.inputactions`; no input bindings or project settings changed. The
  ignored `Assets/Resources/.DS_Store` artifact was also removed from the working directory.
* `Phaser_PulsesBeforeBecomingInvisible` now samples until the authored fade-out window (or until
  the pulse evidence is complete), eliminating the fixed short wall-clock sample that made the
  full Play Mode suite timing-sensitive. This is a test-only stability change; the production Phaser
  implementation is unchanged.

## Verification blockers and remaining work

The focused and complete Unity automated suites now have current results. The final post-suite Console
review after the green Play Mode rerun showed 589 logs, 98 warnings, and 11 errors; the visible errors
were missing fixture wiring (`WaveManager` config/fallback, `LevelConfigSO`, and `WaveSpawner`), not
assertion failures. The Console was then cleared.

Manual clean-progress Levels 1–15, defeat/retry/abort, replay through the real scene UI, terminal-result
screenshots, and physical touch capture remain `BLOCKED` or `NOT RUN`. Desktop automation has not
reliably reproduced physical touch capture or the real scene-button route. The completed-level replay
handler itself is covered by the passing Edit Mode regression above. The generated editor-test project
still cannot build without Unity-generated restore assets;
the generated runtime project did build as a non-Unity static C# check.

No package, project setting, GUID, fileID, save schema, or generated Unity project file was changed.
The approved changes are divided into focused commits on this branch; nothing was pushed, merged,
or opened as a pull request.

## Documentation disposition

* Current system, capstone, and boss user-story documentation is `UPDATE`: the index now points
  here and current-facing statements record the Level 10 non-boss / Level 15 sole-boss topology,
  closest-carrier policy, and evidence rules. Dated design history and acceptance claims remain
  explicitly historical or unresolved where the live implementation does not yet satisfy them.
* `docs/capstone/`, older system snapshots, dated handoffs, and audit CSV/MD files are `ARCHIVE` or
  `UPDATE` candidates, not deletion candidates. They contain historical requirements and QA evidence
  and were not rewritten in this batch.
* Generated/recovery documents and deletion candidates such as `TemplateRecorder`, legacy fallback
  paths, duplicate-looking input assets, and obsolete `Resources` content remain `UNKNOWN` until
  serialized, Editor, runtime, test, documentation, and history references are all negative.

## Remaining-batch disposition

| Batch | Result | Evidence and boundary |
|---|---|---|
| 1. Lock Batch 2 and restore baseline | `PASSED` | Corrected stale expectations and legacy-fixture coverage; full Edit Mode `1,469/1,469` and full Play Mode `230/230` are green. |
| 2. Confirmed campaign-authoring defects | `FIXED` / `PASSED` | Levels 6–14 roster repairs preserve authored wave timing/count fields; campaign contracts and full suites are green. Manual starvation/overflow behavior remains `BLOCKED`. |
| 3. Levels 6–15 contracts and scene-flow coverage | `BLOCKED` | Production asset contracts cover Levels 6–15 and pass. A real-scene clean completion for all 15 levels, save/load transitions, and terminal screenshots were not exercised. |
| 4. Low-risk cleanup | `PASSED` | Runtime test seams are `internal`, teardown reset was added, and the focused/full suites pass. The `Resources.Load` fallback was retained because serialized/build/editor dependencies were not disproven. |
| 5. Targeted responsibility separation | `PASSED` | `WaveTerminalPolicy` and `RestorationRailLayoutPolicy` are pure internal helpers; existing focused and complete suites pass. Manual visual/layout verification remains `BLOCKED`. |
| 6. Documentation updates/archives | `PASSED` | Current docs were reconciled; historical audits and handoffs were preserved. No document was deleted. |
| 7. Proven-safe deletion | `PASSED` for the empty input fixture; `UNKNOWN` for remaining candidates | `Assets/Settings/InputActions.inputactions` and its `.meta` had no code, serialized, test, documentation, or meaningful history dependency. `TemplateRecorder`, legacy fallback paths, and other generated/recovery content remain retained/unknown. |
| 8. Optional improvements | `NOT RUN` | No speculative optimization or broad usability change was applied before the manual release gate. |
| 9. Complete release QA | `BLOCKED` | Automated gates are green; manual clean-progress, touch, and terminal-result evidence are still unavailable. |

## Level-by-level terminal matrix

`PASSED` below means the named automated test actually completed. It does not infer a manual
victory/results state. No clean-progress run through the real scene UI reached a terminal result in
this session.

| Level | Automated evidence | Manual clean victory/results | Status |
|---:|---|---|---|
| 1 | `Level1EndToEndTests` 2/2, including defeat→retry→complete | `BLOCKED` — desktop input/navigation could not reliably drive the authored scene | `BLOCKED` |
| 2 | L2 production contract in `Batch2ProductionAssetContractTests` | `BLOCKED` | `BLOCKED` |
| 3 | L3 production contract in `Batch2ProductionAssetContractTests` | `BLOCKED` | `BLOCKED` |
| 4 | L4 production contract in `Batch2ProductionAssetContractTests` | `BLOCKED` | `BLOCKED` |
| 5 | L5 gate/overflow and production contract groups pass | `BLOCKED` | `BLOCKED` |
| 6 | L6–L15 contract suite passes; L6 roster correction included | `BLOCKED` | `BLOCKED` |
| 7 | L6–L15 contract suite passes; current word→sentence mode asserted | `BLOCKED` | `BLOCKED` |
| 8 | L6–L15 contract suite passes; sentence mode asserted | `BLOCKED` | `BLOCKED` |
| 9 | L6–L15 contract suite passes; sentence mode asserted | `BLOCKED` | `BLOCKED` |
| 10 | L6–L15 contract suite passes; non-boss paragraph topology asserted | `BLOCKED` | `BLOCKED` |
| 11 | L6–L15 contract suite passes; word mode asserted | `BLOCKED` | `BLOCKED` |
| 12 | L6–L15 contract suite passes; word mode asserted | `BLOCKED` | `BLOCKED` |
| 13 | L6–L15 contract suite passes; YA carrier correction included | `BLOCKED` | `BLOCKED` |
| 14 | L6–L15 contract suite passes; sentence mode asserted | `BLOCKED` | `BLOCKED` |
| 15 | L6–L15 contract suite passes; sole-boss/paragraph topology asserted | `BLOCKED` | `BLOCKED` |

## Observed Console findings and runtime coverage limits

The post-suite Console contained 11 errors and 98 warnings after the green Play Mode rerun. The
messages below were observed during test/fixture or Bootstrap execution; they are not being called
production gameplay bugs without a scene-authored reproduction.

| Observed message | Classification | Status / owner to verify |
|---|---|---|
| `WaveManager: Could not load Level 1 config and no fallback assigned.` / `StartLevel: No LevelConfigSO assigned.` | `OBSERVED` | `NOT REPRODUCED` as a production-scene defect; fixture wiring / `WaveManager` assignment needs a clean authored-scene check. |
| `WaveManager: WaveSpawner reference is missing.` | `OBSERVED` | `NOT REPRODUCED` in a clean authored scene; fixture wiring / `WaveSpawner` assignment. |
| `LevelFlowController: Level 1 tutorial is due, but Level10OnboardingController is not in the scene.` | `OBSERVED` | `UNKNOWN` until the tutorial scene is opened and its serialized onboarding reference is inspected. |
| `[ProtagonistManager] _protagonistPrefab not assigned.` | `OBSERVED` | `UNKNOWN`; inspect Bootstrap/Gameplay scene serialization before changing anything. |
| `VictoryScreenUI: SceneLoader not available.` | `OBSERVED` | `NOT REPRODUCED` as a production-scene defect; occurred on an isolated UI fixture path. |
| `AspectLockedCamera requires an orthographic camera.` and `[Salinlahi] Enemy.Initialize: EnemyDataSO is null.` | `OBSERVED` | `UNKNOWN`; requires clean authored-scene runtime reproduction. |

No gameplay bypass or duplicate-resolution exploit was confirmed. The pre-fix roster-narrowing
condition was confirmed by asset-contract failures and is now `FIXED`; its manual runtime proof is
still `BLOCKED`.

## Release readiness

Automated readiness is `PASSED`: Unity `6000.3.9f1` full Edit Mode is `1,469/1,469` and full Play
Mode is `230/230` after the isolated Phaser timing rerun. Repository hygiene is `PASSED` (`git
diff --check`; no package, project-setting, generated-project, GUID, fileID, or staged changes).
Release readiness overall is `BLOCKED` until a reliable input path permits clean Levels 1–15 runs,
defeat/retry/abort/replay and save-transition checks, terminal-result screenshots, physical-touch
capture, and a clean authored-scene pass for the observed wiring messages.
