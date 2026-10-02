using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small per-enemy adapter for retention-oriented abilities that are not already represented by a
/// signature component. It deliberately owns no level flow or objective state: it only reads the
/// current restoration cursor, applies its own resolution blocks, and lets the existing recognizer and
/// combat resolver do the actual work.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class EnemyLearningAbilityController : MonoBehaviour
{
    private static readonly List<Enemy> SnapshotBuffer = new List<Enemy>();

    private Enemy _enemy;
    private ActiveClueDirector _director;
    private ActiveCluePresenter _presenter;
    private Camera _worldCamera;
    private readonly List<Enemy> _boundPair = new List<Enemy>(2);
    private readonly List<Enemy> _visualPair = new List<Enemy>(2);
    private bool _suppressedForIntroductionSpawn;
    private long _spawnSequence = -1;
    private int _reviewIndex;

    public EnemyLearningAbility Ability => _enemy?.Data?.learningAbility ?? EnemyLearningAbility.None;
    public bool IsSuppressedForIntroductionSpawn => _suppressedForIntroductionSpawn;
    public IReadOnlyList<Enemy> BoundPair => _boundPair;
    public IReadOnlyList<Enemy> VisualPair => _visualPair;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        if (_enemy != null)
            _enemy.HealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (_enemy != null)
            _enemy.HealthChanged -= HandleHealthChanged;

        ResetForPool();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            return;

        Tick(Time.deltaTime);
    }

    /// <summary>Drives the ability without requiring a frame, matching the existing enemy controllers.</summary>
    public void Tick(float deltaTime)
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        if (_enemy == null || _enemy.Data == null || Ability == EnemyLearningAbility.None)
        {
            ReleaseBoundPair();
            UnsubscribeFromDirector();
            return;
        }

        if (_spawnSequence != _enemy.SpawnSequence)
        {
            ReleaseBoundPair();
            _reviewIndex = 0;
            _spawnSequence = _enemy.SpawnSequence;
        }

        if (Ability == EnemyLearningAbility.InkAbsorption)
            EnsureDirectorSubscription();
        else
            UnsubscribeFromDirector();

        string next = !_suppressedForIntroductionSpawn && Ability == EnemyLearningAbility.BoundPair
            ? ResolveNextTargetSymbol() : null;
        if (_suppressedForIntroductionSpawn || Ability != EnemyLearningAbility.BoundPair
            || string.IsNullOrEmpty(next) || !IsInsideGameplayView())
        {
            ReleaseBoundPair();
            return;
        }

        RebuildBoundPair(next);
    }

    private bool IsInsideGameplayView()
    {
        if (_worldCamera == null || !_worldCamera.isActiveAndEnabled)
            _worldCamera = Camera.main;
        if (_worldCamera == null || !_worldCamera.isActiveAndEnabled
            || (_worldCamera.cullingMask & (1 << gameObject.layer)) == 0)
            return false;

        Vector3 viewport = _worldCamera.WorldToViewportPoint(transform.position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
    }

    /// <summary>
    /// Makes the ability inert for the introduction spawn and releases any blocks it already owns.
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        _suppressedForIntroductionSpawn = suppressed;
        if (suppressed)
            ReleaseBoundPair();
    }

    /// <summary>Resets attempt-local state when a pooled shell is reused for a new spawn.</summary>
    public void ResetForSpawn()
    {
        ReleaseBoundPair();
        _reviewIndex = 0;
    }

    /// <summary>Ends ability effects before a death animation keeps the shell on screen.</summary>
    public void NotifyDefeated()
    {
        _suppressedForIntroductionSpawn = true;
        ReleaseBoundPair();
        UnsubscribeFromDirector();
    }

    /// <summary>Releases all targets before a pooled enemy shell is reused.</summary>
    public void ResetForPool()
    {
        ReleaseBoundPair();
        UnsubscribeFromDirector();
        _spawnSequence = -1;
        _reviewIndex = 0;
        _suppressedForIntroductionSpawn = false;
    }

    /// <summary>
    /// Selects another learned character for a multi-layer enemy. Stable list order makes the
    /// review deterministic in tests and on replay while still using the current wave roster.
    /// </summary>
    public static BaybayinCharacterSO SelectNextReviewCharacter(
        BaybayinCharacterSO current,
        int reviewIndex,
        IReadOnlyList<BaybayinCharacterSO> allowedCharacters)
    {
        if (current == null || allowedCharacters == null || allowedCharacters.Count == 0)
            return current;

        var alternatives = new List<BaybayinCharacterSO>();
        for (int i = 0; i < allowedCharacters.Count; i++)
        {
            BaybayinCharacterSO candidate = allowedCharacters[i];
            if (candidate == null || candidate == current)
                continue;
            if (string.Equals(candidate.characterID, current.characterID,
                    StringComparison.OrdinalIgnoreCase))
                continue;
            if (!alternatives.Contains(candidate))
                alternatives.Add(candidate);
        }

        if (alternatives.Count == 0)
            return current;

        int index = Mathf.Abs(reviewIndex) % alternatives.Count;
        return alternatives[index];
    }

    private void HandleHealthChanged(Enemy enemy, int previousHealth, int currentHealth)
    {
        if (enemy != _enemy || _suppressedForIntroductionSpawn || currentHealth >= previousHealth || currentHealth <= 0)
            return;

        switch (Ability)
        {
            case EnemyLearningAbility.ChangingArmor:
            case EnemyLearningAbility.FinalWordSeal:
            case EnemyLearningAbility.ChainSequence:
                BaybayinCharacterSO next = SelectNextReviewCharacter(
                    _enemy.Character,
                    _reviewIndex++,
                    WaveManager.CurrentAllowedCharacters);
                if (next != null)
                    _enemy.AssignCharacter(next);
                break;
        }
    }

    private string ResolveNextTargetSymbol()
    {
        RestorationObjectiveController objective = RestorationObjectiveController.Active;
        if (objective == null)
            return null;

        if (objective.UsesLegacyFallback)
        {
            if (_presenter == null)
                _presenter = FindFirstObjectByType<ActiveCluePresenter>();
            if (_presenter != null)
                return _presenter.RestorationState.NextTargetSymbolStableId;
        }

        return objective.State.NextTargetSymbolStableId;
    }

    private void RebuildBoundPair(string next)
    {
        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
        {
            ReleaseBoundPair();
            return;
        }

        tracker.FillActiveEnemiesSnapshot(SnapshotBuffer);
        var candidates = new List<Enemy>(2);
        for (int i = 0; i < SnapshotBuffer.Count; i++)
        {
            Enemy candidate = SnapshotBuffer[i];
            if (!IsPairCandidate(candidate))
                continue;

            candidates.Add(candidate);
            if (candidates.Count == 2)
                break;
        }

        _visualPair.Clear();
        _visualPair.AddRange(candidates);

        for (int i = _boundPair.Count - 1; i >= 0; i--)
        {
            Enemy held = _boundPair[i];
            if (held == null || !candidates.Contains(held))
            {
                held?.RemoveResolutionBlock(this);
                _boundPair.RemoveAt(i);
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            Enemy candidate = candidates[i];
            if (IsContextuallyOpen(candidate, next))
            {
                candidate.RemoveResolutionBlock(this);
                _boundPair.Remove(candidate);
                continue;
            }

            candidate.AddResolutionBlock(this);
            if (!_boundPair.Contains(candidate))
                _boundPair.Add(candidate);
        }
    }

    private bool IsPairCandidate(Enemy candidate)
    {
        return candidate != null
            && candidate != _enemy
            && candidate.Data != null
            // A binder must remain a route out of its own ability, including overlapping Gapos.
            && candidate.Data.learningAbility != EnemyLearningAbility.BoundPair
            && candidate.gameObject.activeInHierarchy
            && !candidate.IsBoss
            && !candidate.IsDying;
    }

    private static bool IsContextuallyOpen(Enemy candidate, string next)
    {
        return !string.IsNullOrEmpty(next)
            && candidate.Character != null
            && string.Equals(candidate.Character.stableId, next, StringComparison.Ordinal);
    }

    private void ReleaseBoundPair()
    {
        for (int i = 0; i < _boundPair.Count; i++)
            _boundPair[i]?.RemoveResolutionBlock(this);
        _boundPair.Clear();
        _visualPair.Clear();
    }

    private void EnsureDirectorSubscription()
    {
        ActiveClueDirector director = ActiveClueDirector.Instance;
        if (director == null || director == _director)
            return;

        UnsubscribeFromDirector();
        _director = director;
        _director.OnActiveClueResolved += HandleActiveClueResolved;
    }

    private void UnsubscribeFromDirector()
    {
        if (_director == null)
            return;

        _director.OnActiveClueResolved -= HandleActiveClueResolved;
        _director = null;
    }

    private void HandleActiveClueResolved(Enemy clue)
    {
        if (Ability != EnemyLearningAbility.InkAbsorption || clue == null || clue == _enemy)
            return;

        BaybayinCharacterSO restored = clue.Character;
        if (restored != null)
            _enemy.ApplyVisualCharacterOverride(this, restored);
    }
}
