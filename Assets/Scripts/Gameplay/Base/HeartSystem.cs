#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
using UnityEngine;

public class HeartSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _maxHearts = 3;

    private int _currentHearts;

    private void OnEnable()
    {
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.RegisterHeartSystem(this);
    }

    private void OnDisable()
    {
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.DeregisterHeartSystem(this);
    }

    private void Awake()
    {
        _currentHearts = _maxHearts;

        int selectedLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
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

        // SALIN-182 Tier 5. The shield blocks the heart loss itself, not merely its side effects, so
        // it returns before any heart is deducted. One consequence is deliberate: because no heart
        // is lost, OnHeartsChanged never fires, so the combo streak survives too -- a blocked hit
        // should not silently cost the player their streak.
        if (Mathf.Max(0, amount) > 0
            && ComboManager.Instance != null
            && ComboManager.Instance.TryConsumeShield())
        {
            DebugLogger.Log(
                $"HeartSystem: Shield blocked the heart loss. Hearts remain {_currentHearts}/{_maxHearts}.");
            return;
        }

        int safeAmount = Mathf.Max(0, amount);
        int previousHearts = _currentHearts;
        _currentHearts = Mathf.Max(0, _currentHearts - safeAmount);
        int appliedDamage = previousHearts - _currentHearts;

        if (appliedDamage > 0)
            EventBus.RaiseBaseDamageApplied(appliedDamage);

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
