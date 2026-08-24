using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the single marked active clue: which enemy carries it, when it may move, and
/// whether it has already been credited.
///
/// The mark latches until its enemy becomes ineligible and freezes during a trace so a faster
/// enemy cannot steal it mid-draw.
/// </summary>
[DisallowMultipleComponent]
public sealed class ActiveClueDirector : MonoBehaviour
{
    private readonly List<Enemy> _enemyBuffer = new List<Enemy>();
    private readonly List<ClueCandidate> _candidateBuffer = new List<ClueCandidate>();

    /// <summary>
    /// Longest the mark may stay frozen after a stroke begins, refreshed on every stroke.
    /// StrokeCapture discards a tap-like stroke without ever reaching RecognitionManager, so
    /// that path raises neither RecognitionResolved nor DrawingFailed and nothing would
    /// otherwise release the freeze. Comfortably exceeds the multi-stroke window.
    /// </summary>
    public const float MaxFreezeSeconds = 3f;

    private IClueObjectiveSource _objectiveSource;
    private Enemy _currentClue;
    private bool _frozen;
    private float _freezeDeadline;
    private bool _currentClueConsumed;

    public static ActiveClueDirector Instance { get; private set; }

    public Enemy CurrentClue => _currentClue;
    public bool IsFrozen => _frozen;

    /// <summary>Fires as (previous, current). Either value may be null.</summary>
    public event Action<Enemy, Enemy> OnActiveClueChanged;

    /// <summary>
    /// Fires once per clue instance, at the moment an accepted draw claims its credit
    /// (SALIN-135). This is the at-accept "the word just got this symbol back" signal that the
    /// HUD hangs its word-restoration cue on; once-ness comes from <see cref="TryConsumeClue"/>
    /// rather than from any timing guard on the listener side.
    ///
    /// Deliberately a director-scoped event rather than an EventBus one: the presenter already
    /// tracks this director, and an instance event cannot survive a scene reload the way a
    /// static subscription can.
    /// </summary>
    public event Action<Enemy> OnActiveClueResolved;

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

        _candidateBuffer.Clear();
        for (int i = 0; i < _enemyBuffer.Count; i++)
        {
            Enemy enemy = _enemyBuffer[i];
            bool isEligible = IsEligibleClue(enemy);
            _candidateBuffer.Add(new ClueCandidate(
                enemy != null && enemy.Character != null ? enemy.Character.characterID : null,
                enemy != null ? enemy.transform.position.y : float.MaxValue,
                enemy != null ? enemy.SpawnSequence : long.MaxValue,
                isEligible));
        }

        int index = ActiveClueSelector.SelectIndex(_candidateBuffer);
        SetClue(index >= 0 ? _enemyBuffer[index] : null);
    }

    /// <summary>
    /// Claims the credit for this clue. The first call wins for the current clue instance;
    /// later calls are rejected during the pronunciation-lead window and do not reset it.
    /// </summary>
    public bool TryConsumeClue(Enemy enemy)
    {
        if (enemy == null || enemy != _currentClue || _currentClueConsumed)
            return false;

        _currentClueConsumed = true;

        // Raised from the single winning consume so the at-accept cue inherits the same
        // once-per-clue guarantee the objective credit has (SALIN-135).
        OnActiveClueResolved?.Invoke(enemy);
        return true;
    }

    private static bool IsGamePaused()
    {
        return GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameState.Paused;
    }

    /// <summary>
    /// Mirrors CombatResolver's combat eligibility and adds clue-only exclusions: decoys carry
    /// deliberately wrong glyphs, and bosses are routed through BossController instead.
    /// </summary>
    private static bool IsEligibleClue(Enemy enemy)
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
        if (enemy.IsDecoy)
            return false;
        if (enemy.Data.isPhaser && !enemy.IsPhaserVisible)
            return false;
        return true;
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
