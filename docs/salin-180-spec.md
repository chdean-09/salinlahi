# SALIN-180 — Active-Clue Combat and Configurable Clue Modes

- **Jira:** SALIN-180 (Task, High, 8 SP) — parent epic BL-E1 / SALIN-128
- **Repository planning ID:** TW-TASK-011
- **Date:** 2026-08-21
- **Status:** Design approved; implementation not started

## 1. Context

### 1.1 Observed repository state

| Capability the ticket needs | State in `dev` at design time |
| --- | --- |
| `ClueMode` enum | Exists, `Assets/Scripts/Data/Campaign/FocusWordDefinition.cs:12`. Values `FullGlyph`, `SpokenAndLatin`, `LatinOnly`, `None`. |
| `LevelConfigSO.clueMode` | Exists, `Assets/Scripts/Data/LevelConfigSO.cs:23`. **No runtime consumer, no validator rule, not serialized into any level asset.** |
| Active-enemy concept | `ActiveEnemyTracker` means "alive and on screen", not "the designated clue target". |
| Targeting | `CombatResolver` resolves by closest matching character, plus an AOE burst at `_aoeThreshold` (3). No designated target. |
| Visible marking | `EnemyGlyphBadge` renders per-enemy glyph sprites; has `Show`, `Hide`, `PlayFailFlash`, `PlayDecoyReject`. |
| Retrieval-strength hook | `LearningEvidenceRecorder.RecordAttempt(..., bool answerWasVisible)` exists (`Assets/Scripts/Data/Learning/LearningEvidenceRecorder.cs:29`). |
| Session evidence owner | `ProgressManager.LevelEvidence` (`Assets/Scripts/Core/ProgressManager.cs:544`), created on demand and discarded when the level is left. |

**Observed:** `clueMode` is entirely inert. Reshaping it is a rename of dead weight, not a breaking change to SALIN-170's delivered contract.

### 1.2 Dependency reality

SALIN-180 is blocked by SALIN-178 (`TW-TASK-010`, "Implement the reusable nine-phase level flow", `LF-CONTRACT-v2`, 13 SP). SALIN-178 is **In Progress, assigned to another engineer, and has no commits or branch in this repository**. The Defense-phase boundary that SALIN-180's acceptance criteria reference does not exist yet.

**Decision:** build SALIN-180 standalone behind a narrow adapter interface, so it lands without waiting and integrates with SALIN-178 by implementing one interface. See §3.3.

### 1.3 Requirement discrepancy

The Jira scope asks for "glyph, sound, image, and incomplete-word prompts". The merged `ClueMode` enum is a *how-much-is-revealed* ladder, not a *which-modality* selector: it has no image value, no incomplete-word value, and welds sound to Latin text. The criterion "required sound clues have an approved readable visual equivalent when audio is unavailable" further implies a clue carries more than one channel at once, which a single-select enum cannot express.

**Decision:** replace `ClueMode` with a composable `[Flags]` channel set. See §3.1.

## 2. Scope

### 2.1 In scope

- Composable clue-channel data contract on `LevelConfigSO`, with validation.
- Deterministic active-clue selection with a Unity-free policy core.
- Strict active-clue targeting: only the marked enemy is drawable while clue combat is armed.
- Clue presentation across glyph, spoken audio, Latin text, context image, and incomplete-word channels, with a guaranteed readable fallback when audio is unavailable.
- Objective crediting through the existing learning-evidence path, exactly once per clue.
- EditMode and PlayMode test coverage for every determinism criterion.

### 2.2 Non-goals

- Implementing SALIN-178's nine-phase flow or any part of `LF-CONTRACT-v2`.
- Authoring Paglimot encounter content (SALIN-184).
- Language and cultural review sign-off (SALIN-188).
- Refactoring `CombatResolver` boss routing, decoy penalty, or AOE staggering beyond the single gate described in §3.3.
- Changing behaviour of any existing level. All 15 level assets keep today's combat until explicitly opted in.

## 3. Architecture

### 3.1 Data contract

New file `Assets/Scripts/Data/Campaign/ClueChannels.cs`:

```csharp
[System.Flags]
public enum ClueChannels
{
    None           = 0,
    Glyph          = 1 << 0,
    SpokenAudio    = 1 << 1,
    LatinText      = 1 << 2,
    ContextImage   = 1 << 3,
    IncompleteWord = 1 << 4,
}
```

`LevelConfigSO.clueMode` is replaced by:

```csharp
[Header("Active-Clue Combat")]
public bool activeClueCombatEnabled;                          // arms the subsystem; default false
public ClueChannels clueChannels = ClueChannels.Glyph;
public ClueChannels audioVisualFallback = ClueChannels.LatinText;
```

`activeClueCombatEnabled` defaults to `false`. Every existing level keeps today's combat path untouched, which is what makes this landable ahead of SALIN-178 instead of a big-bang switch.

`ClueChannelResolver` is a pure static class:

```csharp
public static ClueChannels Resolve(ClueChannels channels, ClueChannels audioVisualFallback);
public static bool HasReadableVisual(ClueChannels channels);
```

`Resolve` returns `channels` unchanged when it already carries a visual channel. When `SpokenAudio` is set with no visual channel, it adds `audioVisualFallback`. This turns the audio-fallback acceptance criterion into a testable invariant rather than a QA checklist item.

**Validation.** Add a `CampaignConfigValidator` rule: when `activeClueCombatEnabled` is true, `Resolve(clueChannels, audioVisualFallback)` must satisfy `HasReadableVisual`. Bad authoring then fails at validation rather than at play.

**Migration.** No `Level*_Config.asset` has serialized `clueMode`, no validator references it, and no runtime code reads it. Removal requires no `FormerlySerializedAs` shim and no asset reserialization.

### 3.2 Selection core — ActiveClueSelector

Plain C#, no `UnityEngine` types in the signature. This is the single most important structural decision: it makes every determinism criterion an EditMode test against a pure function.

```csharp
public readonly struct ClueCandidate
{
    public string CharacterId;      // canonical combat character id
    public float  DistanceToBase;   // Y-derived; lower is closer
    public long   SpawnSequence;    // monotonic, assigned at Enemy.Initialize
    public bool   IsEligible;
}

public static int SelectIndex(IReadOnlyList<ClueCandidate> candidates);
```

Policy, in order: filter to `IsEligible`, take minimum `DistanceToBase`, break ties on lowest `SpawnSequence`, return `-1` when no candidate qualifies.

Eligibility reuses the existing predicate set from `CombatResolver.IsEligibleCombatTarget` — not dying, has `Data`, phaser currently visible — plus two additions specific to clue selection:

- **A decoy is never eligible to be the active clue.** A decoy's displayed glyph is deliberately wrong; marking it as the language objective would teach the wrong symbol.
- **A boss is never eligible to be the active clue.** Note that the existing predicate deliberately *keeps* bosses, because `CombatResolver` still routes single-target resolution to them; the exclusion here applies only to clue selection, leaving `BossController.TryRouteDraw` untouched.

### 3.3 Director and lifecycle — ActiveClueDirector

A `MonoBehaviour` holding exactly two pieces of state: the current clue and a freeze flag.

**Why the mark latches.** `EventBus` exposes no `OnEnemySpawned` event, and enemies move continuously, so "closest to base" changes every frame. Enemy speeds differ (the sprinter variant), so a faster enemy can overtake the marked one mid-trace. Without a latch, an in-flight correct trace lands on a mark that no longer exists and scores as a miss.

Rules:

- Re-evaluate in `LateUpdate` while unfrozen, and immediately on invalidation (defeat, base hit, despawn).
- `EventBus.OnDrawingStarted` freezes the mark. `EventBus.OnRecognitionResolved` unfreezes it, then re-evaluates.
- `GameManager.CurrentState == GameState.Paused` suppresses both re-evaluation and clearing.
- Raises `OnActiveClueChanged(Enemy previous, Enemy current)`.

Net effect: the mark tracks threat but never moves while the player is drawing.

**Adapter seam.** The interface that decouples this ticket from SALIN-178:

```csharp
public interface IClueObjectiveSource
{
    bool IsClueCombatActive { get; }
    IReadOnlyCollection<string> CurrentObjectiveContentIds { get; }
}
```

Today this is backed by `LevelConfigSO.activeClueCombatEnabled` combined with `GameState.Playing`. When SALIN-178 lands, its Defense phase implements the same interface; `ActiveClueDirector` is unchanged. That is the entire integration cost.

**CombatResolver change.** One gate in `HandleCharacterRecognized`, placed **after** the existing tutorial/challenge override check **and after boss routing**:

Gate placement matters. Because §3.2 makes bosses ineligible as clues, gating *before* `BossController.TryRouteDraw` would leave a boss unattackable on any clue-enabled level. Running after boss routing means a targetable boss consumes the draw exactly as it does today, and clue combat governs only the non-boss population.


- `IsClueCombatActive == false` falls through to today's behaviour, byte for byte.
- `IsClueCombatActive == true` requires the traced character to equal the active clue's `Character.characterID`. Resolution targets the active clue only. A non-match raises `RaiseDrawingMissed()`, plays corrective feedback, and grants no progress. The AOE burst path is bypassed entirely while clue combat is armed, which resolves the open question recorded at `docs/salin-166-spec.md:236`.

### 3.4 The pause trap

`Enemy` carries no identity field and enemies are pooled. `GameManager.PausedEnemySnapshot` captures only `(EnemyData, Character, Position, CurrentHealth)` — **no identity** — and `WaveManager` respawns them as new pooled objects (`Assets/Scripts/Gameplay/Wave/WaveManager.cs:301`). A resumed run therefore holds different `Enemy` instances than the paused run did.

Consequences, both required for "deterministic across pause":

1. The active clue must **never be persisted as an object reference across a resume**; it is re-derived from `(DistanceToBase, SpawnSequence)` after restore.
2. `SpawnSequence` is assigned from a monotonic counter in `Enemy.Initialize`, and the pause snapshot is **captured ordered by distance to base** so restore re-derives an identical mark.

Without item 2 this criterion fails silently, and only on a resumed run.

### 3.5 Presentation — ActiveCluePresenter

Subscribes to `OnActiveClueChanged` and drives channels from `ClueChannelResolver.Resolve(...)`:

- **Glyph** — active enemy's `EnemyGlyphBadge.Show()`; every other badge `Hide()`. In non-glyph modes all badges hide, which is what makes a sound-only or image-only clue actually read as such on screen.
- **LatinText / IncompleteWord / ContextImage** — HUD clue panel.
- **SpokenAudio** — `EventBus.RaisePronunciationRequested` plus a replay affordance.
- **The mark itself** — a marker treatment on the active enemy, driven independently of channel, satisfying "one visibly marked active enemy or clue".

Paglimot identities are read from `FocusWordDefinition.media` and `BaybayinCharacterSO`. This ticket builds the presentation seam; SALIN-184 authors the content; SALIN-188 gates acceptance.

### 3.6 Objective crediting

On a correct trace of the active clue:

```csharp
ProgressManager.Instance.LevelEvidence.RecordAttempt(
    contentId, contentKind, dimension,
    success: true,
    answerWasVisible: (resolved & ClueChannels.Glyph) != 0);
```

Clue mode feeds retrieval strength with no new progress plumbing, and `answerWasVisible` gains its first combat caller.

**Exactly once.** `CombatResolver.ResolveMatchedEnemyAfterPronunciationLead` waits `_pronunciationLeadSeconds` (0.06s) before applying damage (`Assets/Scripts/Gameplay/Combat/CombatResolver.cs:236`). Recognition can fire twice inside that window and double-credit. The director therefore marks the clue **consumed at trace time, before the coroutine starts** — not when damage lands. A consumed clue is immediately ineligible, so re-selection proceeds normally.

On an incorrect trace: `EventBus.RaiseDrawingMissed()`, `EnemyGlyphBadge.PlayFailFlash()`, and `RecordAttempt(success: false)` against the active clue's content id.

## 4. Determinism matrix

| Acceptance criterion | Mechanism | Test level |
| --- | --- | --- |
| Paired enemies | Equal `DistanceToBase` resolves on lowest `SpawnSequence` | EditMode |
| Multiple lanes | Distance is Y-derived only; X never participates | EditMode |
| Armor | Multi-hit enemy stays eligible until `IsDying`; mark holds | EditMode |
| Target removal | Removal clears eligibility; next evaluation re-selects | EditMode + PlayMode |
| Phase transitions | Director re-runs selection; no state carried across phases | PlayMode |
| Pause | Mark re-derived after restore; snapshot ordered by distance | PlayMode |
| Advances objective exactly once | Clue consumed at trace time, ahead of the pronunciation-lead coroutine | PlayMode |
| Incorrect trace misses, no progress | Strict gate in `CombatResolver`; miss plus corrective feedback | PlayMode |
| Sound clue has readable visual | `ClueChannelResolver.Resolve` invariant plus validator rule | EditMode |

## 5. File impact

**New**

- `Assets/Scripts/Data/Campaign/ClueChannels.cs`
- `Assets/Scripts/Data/Campaign/ClueChannelResolver.cs`
- `Assets/Scripts/Gameplay/Combat/ActiveClueSelector.cs`
- `Assets/Scripts/Gameplay/Combat/ActiveClueDirector.cs`
- `Assets/Scripts/Gameplay/Combat/IClueObjectiveSource.cs`
- `Assets/Scripts/UI/HUD/ActiveCluePresenter.cs`
- `Assets/Tests/Editor/Gameplay/ActiveClueSelectorTests.cs`
- `Assets/Tests/Editor/Data/ClueChannelResolverTests.cs`
- `Assets/Tests/PlayMode/Gameplay/ActiveClueDirectorTests.cs`

**Modified**

- `Assets/Scripts/Data/LevelConfigSO.cs` — replace `clueMode` with the channel fields
- `Assets/Scripts/Data/Campaign/FocusWordDefinition.cs` — remove the `ClueMode` enum
- `Assets/Scripts/Data/Validation/CampaignConfigValidator.cs` — add the clue-channel rule
- `Assets/Scripts/Gameplay/Combat/CombatResolver.cs` — one early gate
- `Assets/Scripts/Gameplay/Enemy/Enemy.cs` — assign `SpawnSequence` in `Initialize`
- `Assets/Scripts/Core/GameManager.cs` — order the pause snapshot by distance to base

## 6. Verification plan

Per `AGENTS.md`, no repository-owned command-line Unity test command is verified; none is invented here.

- **Compilation.** Unity `6000.3.9f1` via the live Unity MCP connection, followed by Console inspection. Verified available at design time: Editor connected, 0 errors.
- **EditMode tests.** `Assets/Tests/Editor/` through the Unity Test Framework.
- **PlayMode tests.** `Assets/Tests/PlayMode/` through the Unity Test Framework.
- **Test Runner access.** The Test Runner is not exposed as a direct MCP tool; it would be driven through `TestRunnerApi` via `Unity_RunCommand`. This route must be **proven before it is relied upon**. If it does not work, results are reported `NOT RUN` rather than claimed as passing.
- **Manual regression.** Relevant cases from `docs/system/09_Test_Strategy_and_Acceptance_Criteria.md`, specifically RC-01, RC-02, EN-08.

## 7. Criteria this ticket cannot close alone

Recorded explicitly so review does not read them as satisfied:

- **"Active campaign presentation uses approved Paglimot identities"** — the presentation seam ships here, but no approved Paglimot assets exist yet. Content is SALIN-184, which is itself blocked by this ticket.
- **Final acceptance** — SALIN-188 language and cultural review is a stated acceptance gate, not an implementation gate.

## 8. Decision log

| # | Decision | Rationale |
| --- | --- | --- |
| 1 | Build standalone behind an adapter seam rather than wait for SALIN-178 | SALIN-178 has no code in the repository and belongs to another engineer; the seam costs one interface |
| 2 | `[Flags] ClueChannels` replaces `ClueMode` | The four required modalities do not fit a single-select ladder, and the audio-fallback rule needs composition |
| 3 | Strict targeting: only the active clue is drawable | Literal reading of "an incorrect trace misses"; makes the objective unambiguous. Retires the AOE burst while clue combat is armed |
| 4 | Threat-first selection, tie-broken on spawn sequence | Cannot deadlock, reuses established closest-to-base ordering, gives paired and multi-lane spawns a defined winner |
| 5 | Director plus Unity-free selector, not a tracker extension or a `CombatResolver` refactor | Keeps registry separate from policy; avoids a merge conflict with SALIN-182, which is queued to touch `CombatResolver` |
| 6 | The mark latches and freezes during a trace | Prevents a faster enemy stealing the mark mid-trace and turning a correct trace into a miss |
| 7 | Clue consumed at trace time, not at damage time | The 0.06s pronunciation lead is a double-credit window |
| 8 | Decoys are never eligible as the active clue | A decoy's glyph is deliberately wrong; marking it would teach the wrong symbol |
