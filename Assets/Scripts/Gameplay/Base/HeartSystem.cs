using Salinlahi.Debug.Sandbox;
using UnityEngine;

public class HeartSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int _maxHearts = 3;

    private int _currentHearts;

    private void Awake()
    {
        _currentHearts = _maxHearts;
    }

    // Called by PlayerBase when the base is hit
    public void LoseHeart()
    {
        if (SandboxMode.ShouldBypassLifeLoss)
        {
            DebugLogger.Log($"HeartSystem: Sandbox mode ignored heart loss. Hearts remain {_currentHearts}/{_maxHearts}");
            return;
        }

        _currentHearts = Mathf.Max(0, _currentHearts - 1);
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
