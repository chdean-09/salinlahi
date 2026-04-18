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

    public int CurrentStreak => _currentStreak;
    public bool IsFocusModeActive => _focusModeActive;
    public float FocusSpeedMultiplier => _config.focusModeSpeedMultiplier;

    private void OnEnable()
    {
        EventBus.OnEnemyTargeted += HandleEnemyTargeted;
        EventBus.OnDrawingFailed += HandleMiss;
        EventBus.OnDrawingMissed += HandleMiss;
        EventBus.OnHeartsChanged += HandleHeartsChanged;
        EventBus.OnGameOver += HandleGameOver;
        EventBus.OnLevelComplete += HandleLevelEnd;
    }

    private void OnDisable()
    {
        EventBus.OnEnemyTargeted -= HandleEnemyTargeted;
        EventBus.OnDrawingFailed -= HandleMiss;
        EventBus.OnDrawingMissed -= HandleMiss;
        EventBus.OnHeartsChanged -= HandleHeartsChanged;
        EventBus.OnGameOver -= HandleGameOver;
        EventBus.OnLevelComplete -= HandleLevelEnd;
    }

    private void HandleEnemyTargeted(Enemy _)
    {
        _currentStreak++;
        EventBus.RaiseComboChanged(_currentStreak);
        DebugLogger.Log($"ComboManager: Streak = {_currentStreak}");

        if (_currentStreak >= _config.focusModeThreshold)
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
        // Any heart loss resets the streak.
        // We reset on every change because hearts only go down.
        ResetStreak();
    }

    private void HandleGameOver()
    {
        ResetStreak();
        DeactivateFocusMode();
    }

    private void HandleLevelEnd()
    {
        ResetStreak();
        DeactivateFocusMode();
    }

    private void ResetStreak()
    {
        if (_currentStreak == 0) return;
        _currentStreak = 0;
        EventBus.RaiseComboChanged(_currentStreak);
        DebugLogger.Log("ComboManager: Streak reset to 0");
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
}
