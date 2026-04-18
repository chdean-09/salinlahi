using UnityEngine;
#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif

// Handles enemy movement. Fires RaiseBaseHit when colliding with PlayerBase trigger.
[RequireComponent(typeof(Collider2D))]
public class EnemyMover : MonoBehaviour
{
    private float _speed;
    private bool _active;
    private float _focusSpeedMultiplier = 1f;

    public void SetSpeed(float speed)
    {
        _speed = speed;
        _active = true;
    }

    private void OnEnable()
    {
        EventBus.OnFocusModeActivated += HandleFocusOn;
        EventBus.OnFocusModeDeactivated += HandleFocusOff;

        // If Focus Mode is already active when this enemy spawns,
        // apply the slowdown immediately.
        if (ComboManager.Instance != null
            && ComboManager.Instance.IsFocusModeActive)
        {
            HandleFocusOn();
        }
    }

    public void Stop() => _active = false;

    private void Update()
    {
        if (!_active) return;
        // Portrait orientation: enemies move from top to bottom (negative Y direction)
        float finalSpeed = GetFinalSpeed();
        transform.Translate(Vector2.down * finalSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerBase"))
            return;

        Enemy enemy = GetComponent<Enemy>();
        if (enemy == null)
        {
            DebugLogger.LogError($"EnemyMover: Missing Enemy component on '{name}'. Base hit ignored.");
            return;
        }

        EnemyPool pool = EnemyPool.Instance;
        if (pool == null)
        {
            DebugLogger.LogError("EnemyMover: EnemyPool is missing in this scene. Base hit ignored.");
            return;
        }

        if (!pool.IsCheckedOut(enemy))
            return;

        pool.Return(enemy);
        EventBus.RaiseBaseHit();
    }

    private void OnDisable()
    {
        EventBus.OnFocusModeActivated -= HandleFocusOn;
        EventBus.OnFocusModeDeactivated -= HandleFocusOff;
        _focusSpeedMultiplier = 1f;
    }

    private void HandleFocusOn()
    {
        if (ComboManager.Instance != null)
            _focusSpeedMultiplier = ComboManager.Instance.FocusSpeedMultiplier;
    }

    private void HandleFocusOff()
    {
        _focusSpeedMultiplier = 1f;
    }

    private float GetFinalSpeed()
    {
        if (IsSandboxMovementPaused())
            return 0f;

        return _speed * _focusSpeedMultiplier * GetSandboxMovementSpeedScale();
    }

    private static bool IsSandboxMovementPaused()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        return SandboxMode.IsMovementPaused;
#else
        return false;
#endif
    }

    private static float GetSandboxMovementSpeedScale()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        return SandboxMode.MovementSpeedScale;
#else
        return 1f;
#endif
    }

#if UNITY_INCLUDE_TESTS
    public float GetFinalSpeedForTests()
    {
        return GetFinalSpeed();
    }
#endif
}
