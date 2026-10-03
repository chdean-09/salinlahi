# AI-Assisted Development Workflow

This document explains how the Salinlahi team should use Codex and optional supporting tools without losing Unity-specific context. It complements the concise agent rules in `AGENTS.md`; product and architecture references remain indexed by `docs/system/00_Documentation_Index.md`.

## Evidence and Status Language

This workflow separates facts from proposals:

- **Observed** — directly verified from this repository or the inspected tool session.
- **Inference** — a conclusion supported by observed evidence.
- **Recommendation** — a proposed team practice, not an installed capability.
- `UNKNOWN` / `NOT VERIFIED` — evidence or tool availability could not be confirmed.
- `NOT RUN` / `BLOCKED` — a verification step did not execute or could not execute.

Repository counts below were observed on 2026-08-17 and should be rechecked after substantial project changes.

## Observed Repository Baseline

| Area | Observed evidence |
|---|---|
| Unity | `ProjectSettings/ProjectVersion.txt` records `6000.3.9f1`. |
| Runtime code | `Assets/Scripts/` contains 197 C# files and is compiled by `Assets/Scripts/Salinlahi.Runtime.asmdef`. Major roots are `Core`, `Data`, `Gameplay`, `UI`, `Feedback`, `Analytics`, `Utilities`, and `Debug`. |
| Editor code | Eight C# files under `Assets/Editor/`, compiled by `Assets/Editor/Salinlahi.Editor.asmdef`. |
| Tests | 71 Edit Mode files under `Assets/Tests/Editor/` and 14 Play Mode files under `Assets/Tests/PlayMode/`, with dedicated test assembly definitions. |
| Serialized content | 11 `.unity` scenes, 39 `.prefab` files, and 73 project `.asset` files under `Assets/ScriptableObjects/`; primary project scenes are in `Assets/_Scenes/`. |
| Unity coupling | A repository-wide C# census under `Assets/` found 109 files mentioning `MonoBehaviour`, 87 files with `[SerializeField]`, 100 files with common Unity lifecycle callbacks, and eight files calling `Resources.Load`. |
| Packages | `Packages/manifest.json` includes the Unity Test Framework, Input System, and Universal Render Pipeline packages; the project assemblies also reference `Unity.TextMeshPro`. Exact resolved dependencies are in `Packages/packages-lock.json`. |
| Serialization | `ProjectSettings/EditorSettings.asset` uses Force Text serialization; `.gitattributes` assigns UnityYAMLMerge handling to `.unity`, `.prefab`, and `.asset` files. |
| CI | `.github/workflows/git-conventions.yml` validates task-branch names, pull-request titles, and every commit subject in each pull request. It does not compile Unity or run Unity tests. |
| Generated IDE files | Root `*.csproj` and `salinlahi.slnx` files identify themselves as Unity-generated and are ignored by `.gitignore`. |

The code surface also contains interfaces, abstract/generic base types, and 152 files with explicit base/interface lists. That is enough cross-file structure for semantic navigation to be useful, while the serialized-content volume makes C# analysis alone insufficient.

## What Codex Is Expected to Do

Codex should:

1. inspect requirements, source, assembly boundaries, tests, and serialized consumers before editing;
2. distinguish observed facts from inferences and recommendations;
3. use semantic C# tooling only for relationships that semantic tooling can actually see;
4. use Unity-aware inspection whenever scene, prefab, Inspector, ScriptableObject, Animator, Console, compilation, Play Mode, or Test Runner state is material;
5. make the smallest coherent change and preserve unrelated work;
6. report verification precisely, including `NOT RUN`, `BLOCKED`, and `NOT APPLICABLE` states;
7. review the final Git diff for Unity asset/GUID churn and local configuration leakage;
8. when explicitly told `ship this`, stage only task-owned files, commit, push, and open a pull request to `dev` without merging it.

Codex should not use the system documentation as a substitute for current repository inspection. Follow the requirements hierarchy in `docs/system/00_Documentation_Index.md`, but confirm implementation claims against `Assets/`, `Packages/`, and `ProjectSettings/`.

## Tool Responsibilities

The recommended boundary is:

```text
Codex
├── repository tools  -> Markdown, JSON/YAML, packages, settings, literal search, Git diff
├── Serena (optional) -> C# symbols, references, implementations, inheritance, semantic edits
└── Unity MCP (optional) -> Editor, scenes, prefabs, GameObjects, serialized state, Console, tests
```

Unavailable optional tools must never block basic repository work when focused inspection or the Unity Editor can answer the question. They also must never be simulated: if a tool is not connected, record that fact.

### Serena

**Observed:** `.serena/project.yml` now configures Serena's `csharp` language server for Salinlahi. On the setup workstation, Serena `1.7.0` is installed, its Codex MCP entry is enabled, and `serena project health-check .` completed successfully after loading the Unity-generated solution and exercising symbol/reference queries. Those workstation versions are observations, not repository-pinned requirements. An already-running Codex task may need a restart before newly registered tools appear.

**Decision: ALREADY PRESENT as shared project configuration; local installation remains per developer.** The runtime, editor, and test assemblies span 290 C# files, including persistence interfaces in `Assets/Scripts/Data/Persistence/`, tutorial base classes in `Assets/Scripts/Gameplay/Tutorial/Onboarding/`, and generic utilities in `Assets/Scripts/Utilities/`. Serena can reduce broad file reads and improve symbol/reference/implementation navigation.

Use Serena, when operational, for:

- locating a type or member across the runtime/editor/test assemblies;
- finding references and implementations before changing a signature;
- navigating inheritance and interfaces during persistence, tutorial, or utility work;
- performing targeted C# edits with semantic context.

Before relying on Serena in a new Codex session, confirm its tools are visible, activate the current project when necessary, and run a project symbol or reference query. If it is absent or unhealthy, use focused `rg` searches and targeted reads, label unestablished semantic relationships `NOT VERIFIED`, and continue with the rest of the workflow.

Do not use Serena as evidence that a serialized field, Unity callback, Inspector binding, animation event, ScriptableObject, prefab, or scene object is unused. Unity-generated project files may be regenerated; the shared setup tolerates that rather than treating root `*.csproj` files as hand-maintained configuration.

### Unity MCP

**Observed:** no Unity MCP package appears in `Packages/manifest.json` or `Packages/packages-lock.json`; no repository MCP configuration or Unity MCP tool was found. Its implementation, version, and capabilities are `NOT VERIFIED`.

**Recommendation: RECOMMENDED, but not currently verified.** This project has material Unity-side coupling: project scenes in `Assets/_Scenes/`, manager/gameplay/UI prefabs under `Assets/Prefabs/`, authored data under `Assets/ScriptableObjects/`, hundreds of serialized fields, and many lifecycle callbacks. A Unity-aware bridge would materially improve inspection and verification.

Use a verified Unity MCP for:

- scene hierarchy and enabled build-scene inspection;
- prefab GameObjects, components, overrides, and serialized references;
- ScriptableObject values and asset-to-asset references;
- Inspector-assigned fields, event bindings, Animator/controller state, and input assets;
- current compilation state and Unity Console errors;
- entering/exiting Play Mode and running the relevant Edit Mode or Play Mode tests;
- build/editor context only when the installed implementation explicitly supports it.

Until Unity MCP is adopted, perform those checks in Unity `6000.3.9f1`. If neither the tool nor Editor inspection is possible, report the state as `BLOCKED` or `NOT VERIFIED` rather than inferring it from C#.

### CodeGraph

**Decision: DISABLED.** Do not initialize or use CodeGraph in this repository. Salinlahi uses the smaller boundary above: Serena when available for C# semantics, a verified Unity MCP or the Unity Editor for serialized/Editor state, and normal repository tools for other files.

### Optional Codex skills

Codex skills are procedural helpers installed in a developer's environment. They are not required to clone, build, or contribute to Salinlahi, and the repository workflow must remain usable without them.

The useful local set observed during this review is deliberately small:

- `unity-project-scout` or `unity-asmdef` for repository and assembly discovery;
- `unity-csharp-scripting` or `unity-script` for C# implementation;
- `unity-scene`, `unity-prefab`, and `unity-scriptableobject` for serialized work, but only with an operational Unity bridge;
- `unity-test-runner` for test execution, but only when its required Unity tooling is connected;
- `superpowers:verification-before-completion` for evidence-based handoff checks.

Use only the skills relevant to the task. If a skill is missing, follow the equivalent steps in `AGENTS.md` manually; do not block normal work and do not pretend the skill ran. Do not require teammates to install all of them. Their exact distribution source and package version are `NOT VERIFIED`, so this repository does not publish a guessed installation command. A developer may use Codex's skill installer only after selecting and reviewing a trusted source.

## Repository Configuration vs Local Configuration

### Shared through Git

The repository shares these AI and delivery files through Git:

- `AGENTS.md` — concise operational rules for Codex sessions;
- `docs/AI_WORKFLOW.md` — this human-facing workflow;
- `.serena/project.yml` — portable Salinlahi C# language-server configuration;
- `.serena/.gitignore` — excludes Serena's local override, cache, and logs;
- `docs/jira/validate-git-conventions.sh` — the single source of truth for branch, commit-subject, and pull-request-title formats;
- `docs/jira/commit-msg-hook.sh` — the tracked hook source that delegates to the validator;
- `.github/workflows/git-conventions.yml` — pull-request convention validation;
- `.github/PULL_REQUEST_TEMPLATE.md` — verification and Unity asset-impact reporting.

The project context those files reference is already shared through `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/`, `.gitattributes`, `.gitignore`, and `docs/system/`.

There is no shared Unity MCP, `.codex/`, `.cursor/`, or root `.mcp*` configuration. If the team adopts one later, add shared configuration only after its source, compatibility, and non-secret contents are reviewed. CodeGraph is deliberately excluded and must remain uninitialized.

### Local only

Keep these machine-specific:

- the Serena executable, global `~/.serena/` configuration, downloaded language-server/runtime caches, and user-level Codex MCP registration;
- `.serena/project.local.yml`, which is reserved for per-developer overrides and ignored by `.serena/.gitignore`;
- Unity MCP and Codex-skill installations, user-level connections, and tool caches;
- the installed `.git/hooks/commit-msg` copy; its tracked source remains `docs/jira/commit-msg-hook.sh`;
- API keys, credentials, tokens, and user-specific server endpoints;
- absolute machine paths and per-user editor/MCP overrides;
- Unity-generated/cache state such as `Library/`, `Temp/`, `Logs/`, `UserSettings/`, root `*.csproj`, and solution files, all already covered by `.gitignore`.

Never commit a local tool configuration merely to make one workstation work. First decide whether it is portable, secret-free, and required by the team.

### Serena setup for another developer

Serena is optional. The following commands are from its current upstream Codex setup and were successfully exercised on the setup workstation:

```sh
uv tool install -p 3.13 serena-agent
serena init
serena setup codex
serena project health-check .
```

The shared `.serena/project.yml` already selects C#. Serena's current Roslyn backend requires a .NET 10 runtime; install it through Microsoft's supported instructions for the developer's operating system. The setup workstation used .NET SDK `10.0.400`, but that exact SDK version is not a project requirement.

Restart Codex after registration. In the new session, confirm Serena tools are exposed, activate the current directory/project if necessary, and verify a symbol/reference query before relying on semantic results. If setup is unavailable or the health check fails, use the documented focused-search fallback and report Serena as `NOT VERIFIED` or `BLOCKED`; do not hold unrelated work hostage to an optional tool.

Upstream references: [Serena installation](https://oraios.github.io/serena/02-usage/010_installation.html), [Codex client setup](https://oraios.github.io/serena/02-usage/030_clients.html), [C# language-server requirements](https://oraios.github.io/serena/01-about/020_programming-languages.html), and [Microsoft .NET installation](https://learn.microsoft.com/dotnet/core/install/).

## Recommended Development Flow

### C# feature or bug work

1. Read the relevant requirement/architecture section under `docs/system/` or `docs/capstone/`.
2. Inspect the live implementation, its `.asmdef`, nearby tests, and any ScriptableObject/prefab/scene consumers.
3. If Serena is visible, this project is active, and a project query succeeds, use it for definitions, references, implementations, and inheritance. Otherwise use focused repository search and targeted reads.
4. Make the smallest change in the existing assembly boundary.
5. Let Unity compile and inspect the Console.
6. Run the narrowest relevant tests from `Assets/Tests/Editor/` or `Assets/Tests/PlayMode/`.
7. Review the complete diff, including serialized and `.meta` files.

Examples of test proximity are visible in the repository: gameplay behavior has focused tests under `Assets/Tests/Editor/Gameplay/`, data contracts under `Assets/Tests/Editor/Data/`, persistence under `Assets/Tests/Editor/Persistence/`, and runtime interactions under `Assets/Tests/PlayMode/`.

### Scene, prefab, or ScriptableObject work

1. Start with a verified Unity MCP or the matching Unity Editor, not C# alone.
2. Identify the exact scene/prefab/asset, GameObject/component, prefab instance/override, and serialized field involved.
3. Trace the referenced script and data contract semantically when possible.
4. Edit through Unity-aware tooling so `.meta`, GUID, file ID, and prefab relationships are preserved.
5. Reopen/reinspect the object, wait for compilation, and check the Console.
6. Review Force Text YAML diffs for unexpected object churn, lost references, or override changes.

### Refactors and renames

Refactors require both relationship layers:

- semantic C# references/implementations/inheritance; and
- Unity serialized references in scenes, prefabs, ScriptableObjects, Animator assets, Resources paths, and Inspector bindings.

For serialized field renames, inspect existing assets and use `FormerlySerializedAs` when it is the appropriate compatibility mechanism. Current examples exist in `Assets/Scripts/Data/BossPhase.cs`, `Assets/Scripts/Data/LevelConfigSO.cs`, and `Assets/Scripts/Gameplay/Wave/WaveManager.cs`.

Do not use or initialize CodeGraph for refactors. Combine semantic C# navigation with Unity serialized-state inspection and focused repository search.

### Verification and handoff

The verified project options are:

- Unity `6000.3.9f1` compilation and Console review;
- Unity Test Framework Edit Mode suites in `Assets/Tests/Editor/`;
- Unity Test Framework Play Mode suites in `Assets/Tests/PlayMode/`;
- relevant manual regression checks in `docs/system/09_Test_Strategy_and_Acceptance_Criteria.md`;
- Git convention validation in `.github/workflows/git-conventions.yml` (branch, pull-request title, and commit subjects only).

No repository-owned command-line Unity test/build invocation was verified during this review. Do not add one to an AI prompt or claim it ran without separately validating it.

A handoff should name every check and give it one of these outcomes: `PASS`, `FAIL`, `NOT RUN`, `BLOCKED`, or `NOT APPLICABLE`. “Tests pass” is acceptable only when the named suite executed and passed.

### Commit-to-PR delivery

The repository delivery sequence is **verify → review → commit → push → pull request**. The exact instruction `ship this` grants Codex permission to perform that sequence for the current task. It does not grant permission to merge, force-push, rewrite published history, bypass checks, or include unrelated worktree changes.

Before delivery:

1. run the relevant Unity/project checks and assign an explicit status to each;
2. review the complete diff, including `.meta`, scene, prefab, ScriptableObject/`.asset`, package, and `ProjectSettings` impact;
3. when currently on `dev` or `main`, create a compliant task branch; otherwise validate the current branch;
4. stage only files owned by the task and validate the proposed commit subject;
5. push the task branch and open a pull request to `dev` by default.

Open a ready pull request only if all required checks are `PASS` or `NOT APPLICABLE`. If any relevant check is `FAIL`, `NOT RUN`, or `BLOCKED`, open a draft and list the unresolved checks in the pull-request body. Never merge automatically.

The centralized formats are:

| Item | Without Jira | With optional Jira |
|---|---|---|
| Branch | `docs/improve-ai-workflow` | `feature/SALIN-123-improve-scene-loading` |
| Commit | `docs(ai): improve delivery workflow` | `fix(ui): SALIN-123 resolve null reference` |
| Pull request | `Improve AI delivery workflow` | `SALIN-123: Improve AI delivery workflow` |

Branch types are `feature`, `bugfix`, `hotfix`, `chore`, `refactor`, `docs`, `test`, and `spike`. Branch descriptions are lowercase kebab-case with 2–5 words, and the full branch name is at most 60 characters. Commit types are `feat`, `fix`, `chore`, `refactor`, `docs`, `test`, `style`, `perf`, `ci`, and `revert`; scope is optional. Git-generated `Merge ...` and `Revert "..."` subjects are accepted.

Jira keys are optional. A supplied key must use uppercase `SALIN-<number>` syntax in the exact position shown above; malformed or lowercase Jira-looking prefixes fail validation. Ticketless branches, commits, and pull requests do not trigger Jira linking or transitions. `docs/jira/validate-git-conventions.sh` owns these rules; the local hook and CI workflow delegate to it.

## Unity-Specific AI Failure Modes

These risks are directly relevant to the observed repository:

- **Deleting an engine callback as “unused.”** Common lifecycle callbacks occur in 100 project files; Unity invokes them without ordinary callers.
- **Breaking serialized fields.** `[SerializeField]` appears broadly, while scenes, prefabs, and ScriptableObjects carry the actual values.
- **Trusting static references as the full dependency graph.** Inspector bindings, ScriptableObjects, prefab instances, scene components, Animator behavior, and runtime loading sit outside normal C# references.
- **Breaking resource paths.** `Assets/Resources/` is active and `Resources.Load` is used by recognition/template and UI code.
- **Regenerating GUIDs.** Moving/deleting an asset without its `.meta` breaks references even when code still compiles.
- **Creating noisy serialized diffs.** Scenes, prefabs, and `.asset` files use Force Text; unrelated reserialization can obscure the intended change.
- **Ignoring Unity Console errors.** A C# edit is not verified merely because a text editor shows no diagnostics.
- **Editing generated IDE files.** Root project/solution files are Unity-generated and ignored; changes will be overwritten.
- **Overstating CI coverage.** The current GitHub workflow checks Git naming conventions, not Unity compilation, tests, builds, or asset validity.
- **Claiming unavailable tools worked.** Shared Serena configuration does not prove a particular Codex session is connected, and Unity MCP remains unverified. Check live capability before citing tool results.
- **Shipping unrelated work.** `ship this` applies only to task-owned files; pre-existing or unrelated worktree changes stay unstaged.
- **Opening a ready PR with unresolved checks.** `FAIL`, `NOT RUN`, or `BLOCKED` verification belongs in a draft pull request with the status documented.

## Adoption and Evaluation Guidance

Adopt tools incrementally:

1. Keep Codex plus repository inspection as the baseline.
2. Trial the configured Serena integration on reference-heavy C# work. Keep it only if it reliably understands the four assembly boundaries and reduces broad searches/full-file reads.
3. Trial one Unity MCP implementation on a scene/prefab task and a test/Console task. Keep it only if it reports live Editor state accurately and does not create asset churn.
4. Keep CodeGraph disabled; do not initialize or use it for this repository.

Evaluate the tools using outcomes rather than novelty:

- Were symbol/refactor relationships found with fewer irrelevant file reads?
- Were serialized or Inspector-only dependencies discovered before a breakage?
- Did Unity compilation, Console, and test status become easier to report accurately?
- Did scene/prefab/`.meta` churn decrease rather than increase?
- Did the tool introduce configuration, version, credential, or maintenance burden disproportionate to its value?

Any Unity MCP or Codex-skill adoption proposal must separately verify the implementation source, version, Unity compatibility, installation steps, and shared-vs-local configuration. Those details are currently `NOT VERIFIED` and are intentionally not invented here.
