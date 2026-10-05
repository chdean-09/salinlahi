# Localization Verification

**Run date:** 2026-10-05 (Unity Editor UTC test timestamps: 2026-10-04)

## UI shortening follow-up — 2026-10-05

- Approved compact labels were applied to runtime copy and serialized controls; matching existing test assertions and the translation inventory/glossary were updated. See `translation_shortening_review.md` for the 31 reviewed candidates and retained layout recommendations.
- **PASS:** focused text-occurrence review and `git diff --check`. Scene/prefab diffs change text values only; drawing-practice arrow escaping is preserved. No image, `.meta`, layout, or project-setting changes were made in this follow-up.
- **NOT RUN / NOT VERIFIED:** compilation, build, Unity tests, and live fit after shortening. The installed Editor launch timed out and Unity remained closed; Unity MCP was unavailable. The successful builds and test results below belong to the earlier localization pass and do not validate these follow-up changes.
- Nine PNG translations remain **DEFERRED**. Earlier discovery-test failures and the Play Mode AI Relay failure below remain unresolved.

## Environment and compilation

- Unity bridge verified against project `salinlahi`, Unity `6000.3.9f1`.
- `Unity_RunCommand` C# command compilation: **PASS** (`isCompilationSuccessful=true`, empty command compilation logs). Preflight read `EditorApplication.isCompiling=False`, `isUpdating=False`; active scene was `Assets/_Scenes/Bootstrap.unity`.
- Project compilation evidence: a player build completed via `BuildPipeline.BuildPlayer` for the already configured, supported `StandaloneOSX` target, including every enabled build scene. **PASS** — BuildReport `Succeeded`, 0 errors, 3 warnings, 416,333,721 bytes, 00:01:28.51. The build output was outside the repository. No platform switch or project setting change was made.
- Build warnings observed in Console: missing `RuntimePipelineConfig` disables that pipeline in Player builds; `Assets/Scripts/Gameplay/Enemy/KishaMover.cs:16` has assigned-but-unused `_state`; Unity Services returned HTTP 403 while uploading native symbols. The 403 warning did not fail the build. Unity Console reported 0 errors after the build.
- The Unity Test Framework source has a duplicate `using UnityEngine` warning in `Assets/Tests/Editor/Data/Batch2ProductionAssetContractTests.cs:6`; an unrelated AI Relay `connection.state_change` / WebSocket error also occurred in the earlier Play Mode run recorded below.

## Edit Mode

**Run:** Unity Test Framework `TestRunnerApi.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = … }))`. Selected existing fixtures: `DrawingFeedbackVocabularyTests`, `SettingsPanelTests`, `CampaignLevelLabelTests`, `CampaignEndingScreenTests`, `EnemyDiscoveryCopyProviderTests`, `EnemyDiscoveryOnboardingControllerTests`, `LevelLockNoticePanelTests`, `WaveClearedScreenTests`, `TutorialIntroPlayerTests`, `TutorialDrawHintAssetTests`, `CutsceneDataModelTests`, and `HintModalTests`.

**Initial result:** FAIL — 116 total, 90 passed, 26 failed, 0 skipped. After correcting stale translated expectations, the same named fixture selection was rerun through the installed Unity Test Framework API.

**Corrected broad rerun:** FAIL — 124 total, 115 passed, 9 failed, 0 skipped. No stale-English failures remained. The selection discovered 124 test cases; the earlier recorded run discovered 116. Eight failures were in `EnemyDiscoveryOnboardingControllerTests`; the ninth was a `HintModalTests` generated-button lookup assumption later corrected in a focused rerun. Unity wrote the broad-run results outside the repository; they are not project deliverables. The broad selection was not rerun after the focused HintModal fix.

The focused HintModal rerun passed 10/10 after correcting the generated-object lookup. That fix changed the test's lookup from `Card/Actions/Cancel` to the generated name from `HintModalCopy.CancelLabel`, now `Kanselahin`. The eight discovery-overlay failures remain unresolved. `Dismiss_WhileTypewriterRunning_HidesOverlayAndLeavesFullTextReady` and `EnemyDiscovered_StartsTypewriterThenRevealsText` expected non-null body text but received null (test lines 133 and 92). The other six tests expected overlay alpha 1 but received 0 (lines 172, 383, 515, 64, 418, and 281). The cause of these eight failures was not established (**NOT VERIFIED**).

For `EnemyDiscoveryOnboardingController.cs`, the diff contains translated presentation strings; the discovery event/reveal logic is unchanged. The failed assertions show that the overlay was not presented, but the cause of that skip was not established in this bounded investigation (**NOT VERIFIED**). The discovery failures remain **BLOCKED** for a separate fixture/runtime diagnosis; no behavior change was made to mask them.

## Play Mode

**Run:** Unity Test Framework `TestRunnerApi.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, groupNames = … }))`; existing `CutscenePlayerTests` and `LevelLockRuntimeSurfaceTests`.

**Result: FAIL — 27 total, 26 passed, 1 failed, 0 skipped.** `CutscenePlayerTests.ContinuePrompt_StaysHiddenWhileTypewriting_ThenAppears` failed because Unity AI Relay emitted an unhandled `connection.state_change` error (`WebSocketException: Unable to connect to the remote server (after 10 attempts)`). Unity reported writing its `TestResults.xml` to an Editor application-support location outside the repository.

The Level Lock Play Mode tests emitted `[Salinlahi] LevelButton: Selected level could not be persisted.` The test's attempted selected-level write failed; no selected-level persistence was recorded. No progress reset was performed.

## Manual UI and build

- **Observed:** Unity Simulator rendered Main Menu after Play from Bootstrap. Visible buttons included `Simulan ang Laro`, `Piliin ang Antas`, `Talaan`, and `Mga Setting`. The `Piliin ang Antas` label visibly crowds/touches both button edges at the captured Simulator scale; clipping is **NOT VERIFIED** (screenshot alone does not establish glyph overflow). No screenshot was saved to disk; evidence was observed live in the Unity Simulator window.
- **Observed before this final edit:** Settings opened from Main Menu; title rendered as `Mga Setting`. The earlier Simulator run showed English labels/descriptions and `Sound controls unavailable` at `Assets/Scripts/UI/SettingsPanel.cs:124-126` and `:652-656`, with warning `[Salinlahi] SettingsPanel: AudioManager.Instance not available.` The authored runtime copy is now Filipino (inventory at `translation_list.md`); post-edit visual rendering and normal-build audio availability are **NOT VERIFIED**.
- **Observed:** Talaan opened the Almanac view. The heading `MGA KALABAN` is serialized TMP text in `Assets/_Scenes/Almanac.unity:1979` (not part of the unchanged PNG art); the counter showed `Natuklasan 18/18`. The bottom row of enemy cards was partly below the visible content viewport, consistent with a scroll area, but intended scroll behavior is **NOT VERIFIED**. This reflects existing progress and was not modified.
- Main Menu credits panel source data is serialized in `Assets/_Scenes/MainMenu.unity:3287` as `Ginawa ng` and the four approved contributors (Chad Andrada, Ian Clyde, Jeff Andre Millan, Jon Wayne Cabusbusan). The Simulator showed no visible Credits button, and the scene has no serialized `OnCreditsPressed` binding, so the credits panel is unreachable through the observed Main Menu. The panel was **NOT RUN / NOT VERIFIED** rendered. Roles are not assigned or inferred.
- **Observed:** Starting Play from Bootstrap successfully traversed Main Menu into `Assets/_Scenes/Gameplay.unity`; the Simulator rendered the opening cutscene with the typewriter line `Umuwi si Juan sa b…`. This confirms a gameplay-scene presentation surface and cutscene artwork rendered. The line was mid-typewriter, so clipping is **NOT VERIFIED**. Gameplay HUD/tutorial, discovery overlay, later cutscenes/results, and the Hati Minion lesson remain **NOT RUN / NOT VERIFIED**.
- Direct Play from `MainMenu.unity` and Home navigation from `Almanac.unity` each emitted `[Salinlahi] ... SceneLoader not available` and took a direct-load fallback or failed to return. These were artificial entry paths created to inspect isolated scenes; they are **not evidence of a Bootstrap launch failure**. The supported Bootstrap entry path did reach gameplay.
- **Observed:** Play Mode was stopped and `Assets/_Scenes/Bootstrap.unity` restored as the active Edit Mode scene. Scene manager query confirmed it loaded and not dirty. No user progress reset was performed.
- Player build: **PASS** as detailed above; output was outside the repository and is not a project artifact.

## Final focused check and bounded audit

- Focused Edit Mode fixture `Salinlahi.Tests.Editor.UI.SettingsPanelTests` was requested through `TestRunnerApi`; the callback returned 0 passed, 0 failed, 0 skipped, so no cases ran (**NOT RUN**, not a pass). The initial short name and the discovered fully qualified fixture name both yielded an empty selection. No broad test discovery or full suite was run to chase the runner filter.
- Final StandaloneOSX `BuildPipeline.BuildPlayer`, after the final Settings copy change, used the existing enabled scene list and Unity `6000.3.9f1`: **PASS**, BuildReport `Succeeded`, 0 errors, 3 warnings, 416,333,721 bytes, 00:00:23.92. The warnings were the missing `RuntimePipelineConfig`, assigned-but-unused `KishaMover._state`, and Unity Services HTTP 403 for native symbol upload. The build output was outside the repository. This build did not intentionally change the target or enabled-scene configuration.
- The final text audit found and translated the remaining straightforward Settings literals, including the conditional automatic-save status. `translation_list.md` records the applied wording and current source content. Remaining bounds: the SA-ZA blue raster caption is clipped and unreadable (UNKNOWN); Filipino proposals for nine PNGs are **DEFERRED**, with original images and `.meta` files unchanged and no further image generation planned; Hati Minion curriculum copy is authored in Filipino but its runtime presentation remains **NOT VERIFIED**; `The Superintendent` remains excluded by the recorded user decision. No claim of exhaustive runtime presentation or clipping verification is made.
- `git diff --check`: **PASS**. The report does not claim visual validation of the edited Settings labels after the source change.

## Worktree

The final Settings copy and inventory/report corrections are part of the localization task changes. No pre-existing user changes were restored or overwritten during this final pass.
