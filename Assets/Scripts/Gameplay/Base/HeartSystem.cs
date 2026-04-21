#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
using UnityEngine;

public class HeartSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _maxHearts = 3;

    private int _currentHearts;

    private void Awake()
    {
        _currentHearts = _maxHearts;

        int selectedLevel = PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1);
        if (GameManager.Instance != null
            && GameManager.Instance.TryConsumePausedRunHearts(selectedLevel, _maxHearts, out int restoredHearts))
        {
            _currentHearts = restoredHearts;
            DebugLogger.Log($"HeartSystem: Restored paused run hearts to {_currentHearts}/{_maxHearts}.");
        }
    }

    private void Start()
    {
        // Ensure HUD and other listeners sync to the actual starting hearts
        // every time Gameplay loads (including restored paused runs).
        EventBus.RaiseHeartsChanged(_currentHearts);
    }

    // Called by PlayerBase when the base is hit
    public void LoseHeart(int amount = 1)
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        if (SandboxMode.ShouldBypassLifeLoss)
        {
            DebugLogger.Log($"HeartSystem: Sandbox mode ignored heart loss. Hearts remain {_currentHearts}/{_maxHearts}.");
            return;
        }
#endif

        int safeAmount = Mathf.Max(0, amount);
        _currentHearts = Mathf.Max(0, _currentHearts - safeAmount);
        EventBus.RaiseHeartsChanged(_currentHearts);
        DebugLogger.Log($"Hearts remaining: {_currentHearts}/{_maxHearts}");

        if (_currentHearts <= 0)
        {
            DebugLogger.Log("Hearts at zero. Raising GameOver.");
            EventBus.RaiseGameOver();
        }
    }

    public int GetCurrentHearts() => _currentHearts;
    public int GetMaxHearts() => _maxHearts;
}
