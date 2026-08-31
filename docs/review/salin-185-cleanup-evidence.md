# SALIN-185 — Cleanup Evidence

> The ticket's completion criteria require that **"each removed or moved candidate has reference and
> regression evidence"**. This is that evidence, gathered against `dev` @ `1a4f28a` on 2026-08-27.

## Why the evidence was re-gathered

An earlier attempt exists as PR #92, opened 2026-08-06 and last touched 2026-08-09. It is now **96
commits behind `dev`**. Its reference scan was correct when written and is no longer.

Re-scanning found one candidate whose status had changed:

| Candidate | PR #92 | Now | Why it changed |
|---|---|---|---|
| `Assets/Scripts/Debug/ProgressManagerTester.cs` | deleted as unused | **must be kept** | `Assets/Tests/Editor/ReleaseProfile/ReleaseProfileGuardTests.cs` asserts `Salinlahi.Debug.ProgressManagerTester` compiles under `UNITY_EDITOR`. That file was added **2026-08-17 by SALIN-179**, eleven days after PR #92 was cut, and does not exist at its merge-base. |

Deleting it would have broken `ProgressManagerTesterIsCompiledInEditor`. **No amount of rebasing would
have surfaced this** — the deletion merges cleanly and simply makes a newer test fail. That is the
specific hazard of landing an evidence-based cleanup on stale evidence.

## Removed — with evidence

Reference scan: `grep -rn "\bSYMBOL\b" Assets/Scripts Assets/Editor Assets/Tests --include="*.cs"`,
excluding each symbol's own defining file. Asset candidates were scanned by **GUID** across every
`.unity`, `.prefab` and `.asset`.

| File | Evidence | Verdict |
|---|---|---|
| `Assets/Editor/LevelWaveMigrator.cs` | 0 references | superseded — SALIN-109 replaced standalone `WaveConfigSO` with embedded `WaveDefinition`, so the migrator has nothing left to migrate |
| `Assets/Scripts/Debug/TestDialogueTrigger.cs` | 0 references | dev-only trigger, unused |
| `Assets/Scripts/Gameplay/Tutorial/Level1TutorialGlyphValidator.cs` | 0 external references | the trio below reference only each other, so they are removed together |
| `…/Level1TutorialValidationFailure.cs` | referenced only by the two siblings | " |
| `…/Level1TutorialValidationResult.cs` | referenced only by `GlyphValidator` | " |
| `Assets/Scripts/Utilities/ObjectPool.cs` | defines `PooledObject<T>`; **0 references** | superseded by Unity's built-in `UnityEngine.Pool`. `EnemyPool.cs` already uses `UnityEngine.Pool.ObjectPool<Enemy>`, and the only `PooledObject` hit in the repo is the fully-qualified `UnityEngine.Pool.PooledObject<Enemy>` in `SandboxModeTests`. `Enemy` does **not** inherit the custom type. |
| `Assets/ScriptableObjects/Dialogue/TestDialogue.asset` | 0 GUID references | test fixture in release content |
| `Assets/_Scenes/CreateBaybayinTemplate.unity` | 0 GUID references; **not in `EditorBuildSettings`** | authoring scene, never shipped |
| `Assets/Resources/baybayin_eval_latest.txt` | 0 references | output artifact of `BaybayinRecognitionEvaluator`, regenerated on demand |
| `Assets/Resources/PerformanceTestRunInfo.json` | generated | written by the Unity Performance Testing package on every batchmode run — see below |
| `Assets/Resources/PerformanceTestRunSettings.json` | generated | " |

### Kept, against PR #92

`Assets/Scripts/Debug/ProgressManagerTester.cs` — **2 live references**, one of them a guard test that
requires the type to exist. Kept.

### The performance files were causing real friction

`PerformanceTestRunInfo.json` and `PerformanceTestRunSettings.json` were committed but are **written by
the Unity Performance Testing package on every batchmode run**. Every test run therefore dirtied the
working tree with deletions, which is noise every contributor has had to step around when staging. They
are removed and added to `.gitignore` so they stop coming back.

## Moved — test fixtures out of release content

`Assets/Resources/TestDraws/` → `Assets/Tests/Fixtures/TestDraws/` — 20 `.txt` fixtures plus their
`.meta` files, moved with `git mv` so **every `.meta` travels with its asset and no GUID changes**.

Anything under `Assets/Resources/` is built into the player. These are recognition-regression fixtures
and have no business shipping to a device, which is the ticket's *"test fixtures and development tooling
are excluded from release content"* criterion.

The move is **not** a pure rename — three files referenced the old path and all three were updated:

| File | Change |
|---|---|
| `Assets/Editor/BaybayinRecognitionEvaluator.cs` | `TestDrawsFolder` constant plus two user-facing messages |
| `Assets/Scripts/Debug/TemplateRecorder.cs` | `BuildOutputDirectory()` writes recorded draws to the new path, so recorder and evaluator still agree |
| `Assets/Tests/Editor/Gameplay/DollarPRecognizerTests.cs` | `Resources.Load` → `AssetDatabase.LoadAssetAtPath`. **Required**: `Resources.Load` only resolves under `Assets/Resources/`, so the move would otherwise have silently failed the fixture lookup. Safe here because this is an Edit Mode test that never runs in a player. |

`Assets/Resources/Templates/` deliberately **stays** — `TemplateLoader.LoadAll()` reads it at runtime
through `Resources.LoadAll`, so those 121 files are release content, not fixtures.

## Build settings

Removed a stale entry for `Assets/Scenes/SampleScene.unity`, which **does not exist on disk**. The
seven real scenes are untouched.

## Regression evidence

Unity 6000.3.9f1 batchmode, against `dev` @ `1a4f28a`:

| Suite | This branch | Baseline | Delta |
|---|---|---|---|
| Edit Mode | 782 / 713 / **69** | 782 / 713 / **69** | none |
| Play Mode | 132 / 117 / **14** | 132 / 117 / **14** | none |

**The total count is identical, not just the failure count** — that matters for a deletion PR, because
a dropped test file would show up as a lower total while the failure count stayed flat and looked green.

Named confirmation of the two things most at risk:

- All five `DollarPRecognizerTests.Recognize_ResourceDrawRegression_ReturnsExpectedCharacter` cases
  (`KA_draw_01`, `RA_draw_01..03`, `HA_draw_01`) **Passed** — the fixtures resolve from their new home.
- `ReleaseProfileGuardTests.ProgressManagerTesterIsCompiledInEditor` **Passed** — the file kept against
  PR #92 is genuinely required.

## Deliberately not done

**The naming-alignment half of PR #92 is not ported here.** It rewrites all 15 `Level*_Config.asset`
files and eight `docs/system/*.md` files, which collide directly with unmerged work:

| Conflict | With |
|---|---|
| `docs/system/04, 05, 06, 07, 08, 10, 11, 12` | **PR #130** (SALIN-186) rewrote seven of those, including a full repair of doc 10 |
| `Level2–5_Config.asset` | **PR #134** (SALIN-205) wired intro/outro dialogue and rewards into exactly those files |

Landing both would mean hand-resolving serialized-asset and documentation conflicts across four open
PRs, in the area this repo explicitly flags as dangerous (`UnityYAMLMerge` is broken locally). The
naming alignment should be redone **after #130 and #134 merge**, against whatever `dev` looks like then
— and re-scanned again, for the same reason this document exists.

Also untouched, per the ticket's own *"no broad refactor merely to reduce file count"*: PR #92 changed
`Enemy.Initialize`'s signature as part of removing the pooling abstraction. That is a behavioural
refactor, `dev` has since moved on independently, and deleting `ObjectPool.cs` does not require it.

## Recommendation

**Close PR #92 as superseded.** Its verified-still-valid content is here with fresh evidence; its one
now-unsafe deletion is dropped; and its naming half is deferred rather than force-merged. Reopening a
new SALIN-185 slice for the naming alignment once #130 and #134 land is cleaner than rebasing 5 commits
over 96.
