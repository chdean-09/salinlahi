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
    private const float BorrowedGlyphOverrideSeconds = 1.5f;

    private static readonly List<Enemy> SnapshotBuffer = new List<Enemy>();

    private Enemy _enemy;
    private ActiveClueDirector _director;
    private ActiveCluePresenter _presenter;
    private Camera _worldCamera;
    private readonly List<Enemy> _boundPair = new List<Enemy>(1);
    private readonly List<Enemy> _visualPair = new List<Enemy>(1);
    private bool _suppressedForIntroductionSpawn;
    private long _spawnSequence = -1;
    private int _reviewIndex;
    private float _borrowedGlyphOverrideRemainingSeconds;
    private bool _hasBorrowedGlyphOverride;

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

        if (_hasBorrowedGlyphOverride)
        {
            if (Ability == EnemyLearningAbility.ForkedGlyph)
                TickBorrowedGlyphOverride(deltaTime);
            else
                ClearBorrowedGlyphOverride();
        }

        if (_enemy == null || _enemy.Data == null || Ability == EnemyLearningAbility.None)
        {
            ReleaseBoundPair();
            UnsubscribeFromDirector();
            return;
        }

        if (_spawnSequence != _enemy.SpawnSequence)
        {
            ReleaseBoundPair();
            ClearBorrowedGlyphOverride();
            _reviewIndex = 0;
            _spawnSequence = _enemy.SpawnSequence;
        }

        if (ListensForResolvedGlyph())
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
        {
            ReleaseBoundPair();
            ClearBorrowedGlyphOverride();
        }
    }

    /// <summary>Resets attempt-local state when a pooled shell is reused for a new spawn.</summary>
    public void ResetForSpawn()
    {
        ReleaseBoundPair();
        ClearBorrowedGlyphOverride();
        _reviewIndex = 0;
    }

    /// <summary>Ends ability effects before a death animation keeps the shell on screen.</summary>
    public void NotifyDefeated()
    {
        _suppressedForIntroductionSpawn = true;
        ReleaseBoundPair();
        UnsubscribeFromDirector();
        ClearBorrowedGlyphOverride();
    }

    /// <summary>Releases all targets before a pooled enemy shell is reused.</summary>
    public void ResetForPool()
    {
        ReleaseBoundPair();
        UnsubscribeFromDirector();
        ClearBorrowedGlyphOverride();
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
        if (enemy != _enemy)
            return;

        if (currentHealth <= 0)
            ClearBorrowedGlyphOverride();

        if (_suppressedForIntroductionSpawn || currentHealth >= previousHealth || currentHealth <= 0)
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
        Enemy victim = _boundPair.Count > 0 ? _boundPair[0] : null;
        if (!SnapshotBuffer.Contains(victim) || !IsAvailableBindingTarget(victim, next))
        {
            ReleaseBoundPair();
            victim = null;
            for (int i = 0; i < SnapshotBuffer.Count; i++)
            {
                Enemy candidate = SnapshotBuffer[i];
                if (!IsAvailableBindingTarget(candidate, next))
                    continue;

                victim = candidate;
                break;
            }
        }

        if (victim == null)
            return;

        victim.AddResolutionBlock(this);
        if (_boundPair.Count == 0)
        {
            _boundPair.Add(victim);
            _visualPair.Add(victim);
        }
    }

    private bool IsAvailableBindingTarget(Enemy candidate, string next)
    {
        if (!IsPairCandidate(candidate) || IsContextuallyOpen(candidate, next))
            return false;

        // Gapos owns one victim exclusively; other ability blocks remain independently owned.
        for (int i = 0; i < SnapshotBuffer.Count; i++)
        {
            Enemy other = SnapshotBuffer[i];
            if (other == null || other == _enemy || other.Data == null
                || other.Data.learningAbility != EnemyLearningAbility.BoundPair)
                continue;

            var binder = other.GetComponent<EnemyLearningAbilityController>();
            if (binder != null && binder.BoundPair.Count > 0 && binder.BoundPair[0] == candidate)
                return false;
        }
        return true;
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
        if (!ListensForResolvedGlyph() || _suppressedForIntroductionSpawn
            || clue == null || clue == _enemy)
            return;

        BaybayinCharacterSO restored = clue.Character;
        if (restored == null)
            return;

        _enemy.ApplyVisualCharacterOverride(this, restored);
        if (Ability == EnemyLearningAbility.ForkedGlyph)
        {
            // Uhaw borrows the restored enemy glyph for a short badge tell; the HUD's captured
            // rail proxy has its own lifetime and remains owned by ActiveCluePresenter.
            _borrowedGlyphOverrideRemainingSeconds = BorrowedGlyphOverrideSeconds;
            _hasBorrowedGlyphOverride = true;
        }
    }

    private bool ListensForResolvedGlyph()
    {
        return Ability == EnemyLearningAbility.InkAbsorption
            || Ability == EnemyLearningAbility.ForkedGlyph;
    }

    private void TickBorrowedGlyphOverride(float deltaTime)
    {
        if (!_hasBorrowedGlyphOverride)
            return;

        if (_enemy == null || _enemy.Data == null || _enemy.IsDying || _enemy.CurrentHealth <= 0)
        {
            ClearBorrowedGlyphOverride();
            return;
        }

        _borrowedGlyphOverrideRemainingSeconds -= Mathf.Max(0f, deltaTime);
        if (_borrowedGlyphOverrideRemainingSeconds <= 0f)
            ClearBorrowedGlyphOverride();
    }

    private void ClearBorrowedGlyphOverride()
    {
        if (_hasBorrowedGlyphOverride)
            _enemy?.ClearVisualCharacterOverride(this);

        _borrowedGlyphOverrideRemainingSeconds = 0f;
        _hasBorrowedGlyphOverride = false;
    }
}
