using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the single marked active clue: which enemy carries it, when it may move, and
/// whether it has already been credited.
///
/// The mark latches until its enemy becomes ineligible and freezes during a drawing so a faster
/// enemy cannot steal it mid-draw.
///
/// It also owns the distinction between the two questions the clue system gets asked about any one
/// enemy — may a draw strike it (<see cref="IsClueTargetable"/>), and may it be the objective the
/// HUD marks (IsEligibleClue) — which differ for exactly one kind of body: Iligaw's false copy.
/// </summary>
[DisallowMultipleComponent]
public sealed class ActiveClueDirector : MonoBehaviour
{
    private readonly List<Enemy> _enemyBuffer = new List<Enemy>();
    private readonly List<ClueCandidate> _candidateBuffer = new List<ClueCandidate>();

    // Separate buffers for the credit gate in TryConsumeClue. It runs from CombatResolver during
    // recognition handling rather than from LateUpdate, so it can land between a Reevaluate fill
    // and the read that follows it. Sharing the selection buffers would make that interleaving a
    // silent mis-selection instead of a compile error; two more lists cost nothing.
    private readonly List<Enemy> _drawEnemyBuffer = new List<Enemy>();
    private readonly List<ClueCandidate> _drawCandidateBuffer = new List<ClueCandidate>();

    // Cached so LateUpdate's per-frame Reevaluate does not allocate a delegate every frame.
    private static readonly Func<Enemy, bool> ObjectiveEligibility = IsEligibleClue;
    private static readonly Func<Enemy, bool> DrawEligibility = IsClueTargetable;

    /// <summary>
    /// Longest the mark may stay frozen after a stroke begins, refreshed on every stroke.
    /// StrokeCapture discards a tap-like stroke without ever reaching RecognitionManager, so
    /// that path raises neither RecognitionResolved nor DrawingFailed and nothing would
    /// otherwise release the freeze. Comfortably exceeds the multi-stroke window.
    /// </summary>
    public const float MaxFreezeSeconds = 3f;

    /// <summary>
    /// What <see cref="FalseCopiesAreDrawTargets"/> answers when no director instance exists — the
    /// EditMode case, where <see cref="IsClueTargetable"/> is called as a plain predicate. Matches
    /// the authored default so a test and a scene agree about the rule.
    /// </summary>
    private const bool FalseCopiesAreDrawTargetsDefault = true;

    [Header("False copies")]
    [SerializeField]
    [Tooltip("When on, Iligaw's mirror copy is a legal draw target: drawing the glyph the copy "
        + "visibly carries shatters that copy, leaves its source walking, and restores no part of "
        + "the text. Turn this off only to restore the older behaviour where the copy's glyph "
        + "resolved as a miss, which tells the player that nothing carries a glyph they can plainly "
        + "read on a body on screen.")]
    private bool _falseCopiesAreDrawTargets = true;

    private IClueObjectiveSource _objectiveSource;
    private Enemy _currentClue;
    private bool _frozen;
    private float _freezeDeadline;
    private bool _currentClueConsumed;
    private readonly HashSet<long> _creditedSpawnSequences = new HashSet<long>();

    public static ActiveClueDirector Instance { get; private set; }

    public Enemy CurrentClue => _currentClue;
    public bool IsFrozen => _frozen;

    /// <summary>Fires as (previous, current). Either value may be null.</summary>
    public event Action<Enemy, Enemy> OnActiveClueChanged;

    /// <summary>
    /// Fires once per real enemy spawn, at the moment an accepted draw claims its credit
    /// (SALIN-135). This is the at-accept "the word just got this symbol back" signal that the
    /// HUD hangs its word-restoration cue on; both consume paths share the same per-spawn
    /// credit guard rather than relying on timing in the listener.
    ///
    /// Deliberately a director-scoped event rather than an EventBus one: the presenter already
    /// tracks this director, and an instance event cannot survive a scene reload the way a
    /// static subscription can.
    /// </summary>
    public event Action<Enemy> OnActiveClueResolved;

    /// <summary>
    /// Fires when an accepted draw was refused objective credit because the carrier it actually
    /// resolved against was a false copy. The argument is that copy.
    ///
    /// This is the counterpart of <see cref="OnActiveClueResolved"/> and the two are mutually
    /// exclusive per draw: one says the word just got a symbol back, this one says the player
    /// struck a body that was never part of the word. A feedback listener needs both, because the
    /// only other thing it could infer from silence is a miss — and a shattered copy is the
    /// opposite of a miss. The player read the glyph correctly; the glyph was a lie.
    ///
    /// <para>This event covers the marked clue's glyph. Other false-copy draws are still denied
    /// credit by <see cref="TryConsumeUnmarkedCarrier"/>; CombatResolver reports their outcome
    /// through the draw-feedback relation so the HUD can say a copy shattered.</para>
    /// </summary>
    public event Action<Enemy> OnFalseCopyShattered;

    /// <summary>
    /// Whether a false copy may be struck by a draw at all. Read statically because
    /// <see cref="IsClueTargetable"/> is CombatResolver's static hook, and falls back to the
    /// authored default when there is no instance to ask.
    /// </summary>
    private static bool FalseCopiesAreDrawTargets =>
        Instance != null ? Instance._falseCopiesAreDrawTargets : FalseCopiesAreDrawTargetsDefault;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destroys the component, not the GameObject. CombatResolver destroys its whole
            // GameObject, but this director may be authored onto a shared object (a HUD root
            // or the level flow controller), so removing the object could take unrelated
            // components with it. A stray empty GameObject is the cheaper failure.
            if (Application.isPlaying)
                Destroy(this);
            else
                DestroyImmediate(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnDrawingStarted += HandleDrawingStarted;
        EventBus.OnRecognitionResolved += HandleRecognitionResolved;
        EventBus.OnDrawingFailed += HandleDrawingFailed;
    }

    private void OnDisable()
    {
        EventBus.OnDrawingStarted -= HandleDrawingStarted;
        EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
        EventBus.OnDrawingFailed -= HandleDrawingFailed;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetObjectiveSource(IClueObjectiveSource source)
    {
        _objectiveSource = source;
        _creditedSpawnSequences.Clear();
    }

    public bool IsClueCombatActive =>
        _objectiveSource != null && _objectiveSource.IsClueCombatActive;

    private void LateUpdate()
    {
        if (IsGamePaused())
        {
            // The freeze must not time out while the game is paused: StrokeCapture preserves
            // an in-flight multi-stroke draw across pause, so push the deadline forward.
            if (_frozen)
                _freezeDeadline = Time.unscaledTime + MaxFreezeSeconds;
            return;
        }

        if (_frozen && Time.unscaledTime >= _freezeDeadline)
        {
            _frozen = false;
            DebugLogger.Log(
                "ActiveClueDirector: freeze expired without a recognition result; releasing the mark.");
        }

        if (_frozen)
            return;

        Reevaluate();
    }

    /// <summary>
    /// Re-runs selection unless frozen or paused. The mark latches while the current clue
    /// remains eligible, even if another enemy becomes closer.
    /// </summary>
    public void Reevaluate()
    {
        if (_frozen || IsGamePaused())
            return;

        if (!IsClueCombatActive)
        {
            SetClue(null);
            return;
        }

        // The mark latches while its enemy is alive, INCLUDING after consumption. Consumption
        // guards objective credit only (see TryConsumeClue); it must not affect eligibility,
        // or a multi-hit clue would lose the mark after one hit and become undrawable under
        // the strict gate. CombatResolver still applies damage when credit is refused.
        if (IsEligibleClue(_currentClue))
            return;

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
        {
            SetClue(null);
            return;
        }

        tracker.FillActiveEnemiesSnapshot(_enemyBuffer);

        // ObjectiveEligibility, never DrawEligibility: the mark is the HUD's statement of what the
        // player is supposed to answer next, and a false copy carries a glyph the level never asked
        // for. Marking one would point the player at a body that restores nothing.
        FillCandidates(_enemyBuffer, _candidateBuffer, ObjectiveEligibility);

        int index = ActiveClueSelector.SelectIndex(_candidateBuffer);
        SetClue(index >= 0 ? _enemyBuffer[index] : null);
    }

    /// <summary>
    /// Claims the credit for this clue. The first call wins for the current clue instance;
    /// later calls are rejected during the pronunciation-lead window and do not reset it.
    ///
    /// Also refuses — and raises <see cref="OnFalseCopyShattered"/> instead — when the body this
    /// draw actually struck was one of Iligaw's false copies. The caller knows only that the clue's
    /// glyph was drawn; whether a real carrier of it fell is a separate fact, and this is where the
    /// two are told apart.
    /// </summary>
    public bool TryConsumeClue(Enemy enemy)
    {
        if (enemy == null || enemy != _currentClue || _currentClueConsumed
            || _creditedSpawnSequences.Contains(enemy.SpawnSequence))
            return false;

        Enemy falseCopy = FindFalseCopyHoldingTheDraw(enemy);
        if (falseCopy != null)
        {
            // Deliberately leaves _currentClueConsumed false. The slot is still owed: the copy fell
            // and the real carrier is still walking, so the very next draw of the same glyph must be
            // able to claim the credit this one could not.
            OnFalseCopyShattered?.Invoke(falseCopy);
            return false;
        }

        _currentClueConsumed = true;
        _creditedSpawnSequences.Add(enemy.SpawnSequence);

        // Raised from the single winning consume so the at-accept cue inherits the same
        // once-per-clue guarantee the objective credit has (SALIN-135).
        OnActiveClueResolved?.Invoke(enemy);
        return true;
    }

    /// <summary>
    /// Credits a real carrier drawn while another glyph holds the mark. CombatResolver has
    /// already selected this enemy as the draw's target; false copies never restore text.
    /// </summary>
    public bool TryConsumeUnmarkedCarrier(Enemy enemy)
    {
        if (enemy == null || enemy == _currentClue || enemy.IsDecoy
            || !IsClueTargetable(enemy)
            || !_creditedSpawnSequences.Add(enemy.SpawnSequence))
            return false;

        OnActiveClueResolved?.Invoke(enemy);
        return true;
    }

    private static bool IsGamePaused()
    {
        return GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameState.Paused;
    }

    /// <summary>
    /// Whether a draw may resolve against this enemy — CombatResolver's targetability hook.
    ///
    /// <para><b>Targetable for a draw is not the same question as eligible to be the objective,
    /// and a false copy is the one body where the two answers differ.</b> Everything else about
    /// being on screen and resolvable is shared, which is why both questions are layered over the
    /// same <see cref="IsResolvableOnScreen"/> core rather than restated — the two sets can drift
    /// apart only where a comment here says they are meant to.</para>
    ///
    /// <para>A copy is targetable because its glyph is <i>visible on a body on screen</i>. Refusing
    /// it made drawing that glyph resolve as a miss, and the miss copy states what is on the board
    /// — so the game told the player nothing out there carried a symbol they could plainly read on
    /// a walking enemy. That is not a hard lesson, it is a lie, and it makes the whole "falls for
    /// it" branch of the deception beat unreachable: the copy is supposed to shatter while the real
    /// one keeps walking, and the player is supposed to learn that from the board rather than from
    /// a prompt.</para>
    ///
    /// <para>Targetability is all this grants. Which carrier dies is still
    /// <see cref="ActiveClueSelector"/>'s single rule — closest to the base, ties broken by spawn
    /// sequence — so a copy competes for the kill on exactly the terms every other body does, with
    /// no branch anywhere that reads "if decoy". Whether the word advances is decided separately,
    /// in <see cref="TryConsumeClue"/>.</para>
    /// </summary>
    public static bool IsClueTargetable(Enemy enemy)
    {
        if (!IsResolvableOnScreen(enemy))
            return false;

        return !enemy.IsDecoy || FalseCopiesAreDrawTargets;
    }

    /// <summary>
    /// Whether this enemy may carry the mark — the authored objective the HUD points the player at.
    ///
    /// False copies are excluded here and must stay excluded. A copy carries a deliberately wrong
    /// glyph, so marking one would not merely be unhelpful, it would instruct the player to draw a
    /// symbol that restores nothing and then show them the text failing to advance. The exclusion is
    /// the reason this predicate exists apart from <see cref="IsClueTargetable"/>: struck by a draw,
    /// yes; held up as the thing to draw, never.
    /// </summary>
    private static bool IsEligibleClue(Enemy enemy)
    {
        if (!IsResolvableOnScreen(enemy))
            return false;

        return !enemy.IsDecoy;
    }

    /// <summary>
    /// The part both questions agree on: this enemy is really out there and a draw could resolve
    /// against it at all. Mirrors CombatResolver's combat eligibility, plus the clue-system
    /// requirement of a readable character and the boss exclusion — bosses route through
    /// BossController instead.
    /// </summary>
    private static bool IsResolvableOnScreen(Enemy enemy)
    {
        if (enemy == null)
            return false;
        if (!enemy.gameObject.activeInHierarchy)
            return false;
        if (enemy.IsDying)
            return false;
        if (enemy.Data == null)
            return false;
        if (enemy.Character == null)
            return false;
        if (enemy.IsBoss)
            return false;
        if (enemy.Data.isPhaser && !enemy.IsPhaserVisible)
            return false;
        // SALIN-286: keeps the mirror above honest. Without it a Bakod-shielded enemy could be
        // marked as the active clue and then refused by CombatResolver — the player would be
        // handed a target that cannot be resolved, with no way to move on. AUDIT.md:466 names both
        // this hook and CombatResolver.IsEligibleCombatTarget for the ability.
        if (enemy.IsResolutionBlocked)
            return false;
        return true;
    }

    /// <summary>
    /// The false copy this draw actually struck, or null when the draw struck a real carrier.
    ///
    /// <para><b>Why the director has to work this out for itself.</b> CombatResolver hands this
    /// method the <i>clue</i>, not the body it killed — credit follows the glyph, so the enemy that
    /// died is often not the marked one. Once a copy can be that body, "the clue's glyph was drawn"
    /// stops implying "a real carrier of it fell", and the gap between those two facts is exactly
    /// where the deception beat lives. Credit is this director's guarantee to keep, so it resolves
    /// the question rather than trusting the caller's conclusion.</para>
    ///
    /// <para>It asks <see cref="DrawTargetResolver"/> — the same rule CombatResolver used a moment
    /// earlier in the same frame, over the same tracker snapshot, with nothing moving in between —
    /// so the two cannot disagree about which body the draw took. Recomputing is what keeps this
    /// from being a second targeting policy; restating "closest wins" here is what would.</para>
    /// </summary>
    private Enemy FindFalseCopyHoldingTheDraw(Enemy clue)
    {
        // Nothing to check when copies cannot be struck at all: the winning carrier is a real enemy
        // by construction, and skipping the work keeps the old behaviour exactly as it was.
        if (!FalseCopiesAreDrawTargets)
            return null;

        string drawnCharacterId = clue.Character != null ? clue.Character.characterID : null;
        if (string.IsNullOrEmpty(drawnCharacterId))
            return null;

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
            return null;

        tracker.FillActiveEnemiesSnapshot(_drawEnemyBuffer);
        FillCandidates(_drawEnemyBuffer, _drawCandidateBuffer, DrawEligibility);

        int index = DrawTargetResolver.SelectSingleIndex(_drawCandidateBuffer, drawnCharacterId);
        if (index < 0)
            return null;

        // A null winner, or a real one, both mean "do not withhold credit". Refusal has to be
        // positively established: guessing wrong in this direction silently drops a slot the player
        // earned, which is a far worse failure than crediting a draw that happened to be muddled.
        Enemy winner = _drawEnemyBuffer[index];
        return winner != null && winner.IsDecoy ? winner : null;
    }

    /// <summary>
    /// Flattens a snapshot of enemies into selector candidates. The eligibility predicate is a
    /// parameter because the two callers ask different questions of the same board — the mark asks
    /// what may be the objective, a draw asks what may be struck — and everything else about
    /// building a candidate is identical between them.
    /// </summary>
    private static void FillCandidates(
        List<Enemy> enemies, List<ClueCandidate> candidates, Func<Enemy, bool> isEligible)
    {
        candidates.Clear();
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            candidates.Add(new ClueCandidate(
                enemy != null && enemy.Character != null ? enemy.Character.characterID : null,
                enemy != null ? enemy.transform.position.y : float.MaxValue,
                enemy != null ? enemy.SpawnSequence : long.MaxValue,
                isEligible(enemy)));
        }
    }

    private void SetClue(Enemy next)
    {
        if (_currentClue == next)
            return;

        Enemy previous = _currentClue;
        _currentClue = next;
        _currentClueConsumed = false;
        OnActiveClueChanged?.Invoke(previous, next);
    }

    private void HandleDrawingStarted()
    {
        _frozen = true;

        // Refreshed per stroke, so a deliberate multi-stroke character never times out: the
        // gap between strokes is bounded by the multi-stroke window.
        _freezeDeadline = Time.unscaledTime + MaxFreezeSeconds;
    }

    private void HandleRecognitionResolved(
        RecognitionResult result,
        bool passedThreshold,
        float threshold)
    {
        Unfreeze();
    }

    /// <summary>
    /// RecognitionManager raises DrawingFailed instead of RecognitionResolved for a
    /// degenerate stroke, so this path must release the mark too.
    /// </summary>
    private void HandleDrawingFailed()
    {
        Unfreeze();
    }

    private void Unfreeze()
    {
        _frozen = false;
        Reevaluate();
    }
}
