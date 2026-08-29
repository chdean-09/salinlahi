using UnityEngine;

/// Tracks consecutive correct combat hits and activates Focus Mode.
/// Resets on any miss or heart loss.
public class ComboManager : Singleton<ComboManager>
{
    [Header("Configuration")]
    [SerializeField] private GameConfigSO _config;

    private int _currentStreak;
    private bool _focusModeActive;
    private Coroutine _focusRoutine;
    private int _suppressedHeartResetCount;
    private bool _powerGrantedThisStreak;
    private int _pendingRapidShotHits;

    public int CurrentStreak => _currentStreak;
    public bool IsFocusModeActive => _focusModeActive;
    public float FocusSpeedMultiplier => _config.focusModeSpeedMultiplier;

    /// <summary>The power the current streak granted (SALIN-182). None until the threshold.</summary>
    public ComboPower ActivePower { get; private set; }

    /// <summary>
    /// Unspent Tier 5 shield charges, capped at <c>GameConfigSO.shieldCharges</c> (1 by default).
    /// That cap is what makes the shield nonstacking: reaching the threshold again while a charge is
    /// held adds nothing.
    /// </summary>
    /// <remarks>
    /// Shields deliberately outlive a streak reset, unlike <see cref="ActivePower"/> and the Rapid
    /// Shot hits. The criterion grants a shield that blocks "the next" heart loss, so revoking it on
    /// the next miss would mean it almost never survived to do its job.
    /// </remarks>
    public int ShieldCharges { get; private set; }

    /// <summary>Unspent Tier 2 bonus hits.</summary>
    public int PendingRapidShotHits => _pendingRapidShotHits;

    private void OnEnable()
    {
        EventBus.OnEnemyTargeted += HandleEnemyTargeted;
        EventBus.OnDrawingFailed += HandleMiss;
        EventBus.OnDrawingMissed += HandleMiss;
        EventBus.OnHeartsChanged += HandleHeartsChanged;
        EventBus.OnGameOver += HandleGameOver;
        EventBus.OnLevelComplete += HandleLevelEnd;
        // SALIN-141: ComboManager is a persistent singleton, so an abandoned attempt
        // would otherwise carry its streak and Focus Mode into the next one.
        EventBus.OnLevelAttemptAborted += HandleLevelEnd;
    }

    private void OnDisable()
    {
        EventBus.OnEnemyTargeted -= HandleEnemyTargeted;
        EventBus.OnDrawingFailed -= HandleMiss;
        EventBus.OnDrawingMissed -= HandleMiss;
        EventBus.OnHeartsChanged -= HandleHeartsChanged;
        EventBus.OnGameOver -= HandleGameOver;
        EventBus.OnLevelComplete -= HandleLevelEnd;
        EventBus.OnLevelAttemptAborted -= HandleLevelEnd;
    }

    private void HandleEnemyTargeted(Enemy _)
    {
        _currentStreak++;
        EventBus.RaiseComboChanged(_currentStreak);
        DebugLogger.Log($"ComboManager: Streak = {_currentStreak}");

        if (_currentStreak >= _config.focusModeThreshold)
            GrantComboPowerOnce();

        if (IsFocusModeEnabledForCurrentLevel()
            && _currentStreak >= _config.focusModeThreshold)
        {
            if (!_focusModeActive)
            {
                // First time hitting threshold.
                ActivateFocusMode();
            }
            else
            {
                // Already in Focus Mode, restart the timer.
                if (_focusRoutine != null)
                    StopCoroutine(_focusRoutine);
                _focusRoutine = StartCoroutine(FocusModeTimerRoutine());
                DebugLogger.Log("ComboManager: Focus Mode timer reset");
            }
        }
    }

    private void HandleMiss()
    {
        ResetStreak();
    }

    private void HandleHeartsChanged(int currentHearts)
    {
        if (_suppressedHeartResetCount > 0)
        {
            _suppressedHeartResetCount--;
            return;
        }

        // Any heart loss resets the streak.
        // We reset on every change because hearts only go down.
        ResetStreak();
    }

    public void SuppressNextHeartLossResets(int count)
    {
        if (count <= 0)
            return;

        _suppressedHeartResetCount += count;
    }

    /// <summary>
    /// Grants the tier's power the first time a streak reaches the threshold (SALIN-182).
    /// </summary>
    /// <remarks>
    /// Deliberately not gated on <c>focusModeEnabled</c>. That flag governs the slow-time effect;
    /// the tier power is a separate reward, and a level that turns off Focus Mode has not thereby
    /// said its tier grants nothing. Levels at tier 0 or 1 resolve to None, so this changes nothing
    /// for them — and <c>comboPowersEnabled</c> turns the whole mechanic off if that is wanted.
    /// </remarks>
    private void GrantComboPowerOnce()
    {
        if (_powerGrantedThisStreak || _config == null || !_config.comboPowersEnabled)
            return;

        _powerGrantedThisStreak = true;
        ActivePower = ComboPowerResolver.ForTier(ComboPowerResolver.CurrentTier());

        switch (ActivePower)
        {
            case ComboPower.RapidShot:
                _pendingRapidShotHits = Mathf.Max(0, _config.rapidShotBonusHits);
                break;

            case ComboPower.Shield:
                // Nonstacking: clamped to the cap, so a second grant while a charge is held is a
                // no-op rather than a second shield.
                ShieldCharges = Mathf.Min(ShieldCharges + 1, Mathf.Max(0, _config.shieldCharges));
                break;
        }

        DebugLogger.Log($"ComboManager: Tier power granted = {ActivePower}");
    }

    /// <summary>
    /// Spends one Rapid Shot hit if any remain. Returns false once exhausted, which is what keeps
    /// "one bonus hit" from becoming one per recognition.
    /// </summary>
    public bool TryConsumeRapidShotHit()
    {
        if (_pendingRapidShotHits <= 0)
            return false;

        _pendingRapidShotHits--;
        return true;
    }

    /// <summary>Spends one shield charge if one is held. See <see cref="ShieldCharges"/>.</summary>
    public bool TryConsumeShield()
    {
        if (ShieldCharges <= 0)
            return false;

        ShieldCharges--;
        DebugLogger.Log("ComboManager: Shield absorbed a heart loss");
        return true;
    }

    /// <summary>True when the level's tier grants Piercing Arrow and it is enabled.</summary>
    public bool PiercingArrowActive =>
        ActivePower == ComboPower.PiercingArrow
        && _config != null
        && _config.comboPowersEnabled
        && _config.piercingArrowEnabled;

    public void ResetStreakForTutorial()
    {
        ResetStreak();
    }

    private void HandleGameOver()
    {
        ResetStreak();
        ClearComboPowers();
        DeactivateFocusMode();
    }

    private void HandleLevelEnd()
    {
        ResetStreak();
        ClearComboPowers();
        DeactivateFocusMode();
    }

    private void ResetStreak()
    {
        // Streak-scoped power state clears even when the streak is already 0, because the early
        // return below would otherwise strand an unspent Rapid Shot hit past the miss that ended
        // the streak that earned it. Shields are not streak-scoped -- see ShieldCharges.
        _powerGrantedThisStreak = false;
        _pendingRapidShotHits = 0;
        ActivePower = ComboPower.None;

        if (_currentStreak == 0) return;
        _currentStreak = 0;
        EventBus.RaiseComboChanged(_currentStreak);
        DebugLogger.Log("ComboManager: Streak reset to 0");
    }

    /// <summary>
    /// Clears everything a level attempt may have accrued, shields included. A banked shield must
    /// not cross into the next attempt: SALIN-141 established that an abandoned attempt leaves no
    /// residue, and a free heart carried over would be exactly that.
    /// </summary>
    private void ClearComboPowers()
    {
        _powerGrantedThisStreak = false;
        _pendingRapidShotHits = 0;
        ShieldCharges = 0;
        ActivePower = ComboPower.None;
    }

    private void ActivateFocusMode()
    {
        _focusModeActive = true;
        EventBus.RaiseFocusModeActivated();
        DebugLogger.Log($"ComboManager: FOCUS MODE ON for {_config.focusModeDuration}s");

        // Cancel any existing timer and start fresh.
        if (_focusRoutine != null)
            StopCoroutine(_focusRoutine);
        _focusRoutine = StartCoroutine(FocusModeTimerRoutine());
    }

    private void DeactivateFocusMode()
    {
        if (!_focusModeActive) return;
        _focusModeActive = false;
        if (_focusRoutine != null)
        {
            StopCoroutine(_focusRoutine);
            _focusRoutine = null;
        }
        EventBus.RaiseFocusModeDeactivated();
        DebugLogger.Log("ComboManager: FOCUS MODE OFF");
    }

    private System.Collections.IEnumerator FocusModeTimerRoutine()
    {
        yield return new WaitForSeconds(_config.focusModeDuration);
        _focusModeActive = false;
        _focusRoutine = null;
        EventBus.RaiseFocusModeDeactivated();
        DebugLogger.Log("ComboManager: Focus Mode expired");
    }

    private static bool IsFocusModeEnabledForCurrentLevel()
        => GameManager.CurrentLevelConfig?.focusModeEnabled ?? true;
}
