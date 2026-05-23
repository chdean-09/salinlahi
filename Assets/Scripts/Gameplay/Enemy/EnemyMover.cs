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
    private bool _externallyMoving;
    private float _focusSpeedMultiplier = 1f;
    public bool IsMoving => _externallyMoving || (_active && GetFinalSpeed() > Mathf.Epsilon);

    // For movers that drive the transform externally (e.g. PhaseBasedMovement
    // on the boss). Lets IsMoving report true so visuals coupled to it — like
    // Enemy.AdvanceWalkAnimation — run while the external system is active,
    // even when this component's own speed is zero.
    public void SetExternallyMoving(bool moving) => _externallyMoving = moving;

    public virtual void SetSpeed(float speed)
    {
        _speed = speed * GetCorridorNormalizationScale();
        _active = true;
    }

    // Updates the stored speed without changing the active state. Used for
    // recalculations that must not unintentionally resume a paused mover —
    // e.g. GeneralAura applying its buff every tick while the enemy is in a
    // hurt-pause window.
    public virtual void UpdateSpeedValue(float speed)
    {
        _speed = speed * GetCorridorNormalizationScale();
    }

    private static float GetCorridorNormalizationScale()
    {
        var cam = AspectLockedCamera.Instance;
        return cam != null ? cam.CorridorSpeedNormalizationScale : 1f;
    }

    protected virtual void OnEnable()
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

    protected virtual void Update()
    {
        if (!_active) return;
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

        EnemyDataSO data = enemy.Data;
        pool.Return(enemy);
        bool dealsContactDamage = data == null || data.dealsContactDamage;
        if (dealsContactDamage)
        {
            EventBus.RaiseBaseHit(1);
        }
        else if (data != null && data.isDecoy)
        {
            RecognitionLogger.LogOutcome(
                outcome: "decoy_ignored",
                recognizedCharacterID: data.assignedCharacter != null ? data.assignedCharacter.characterID : "");
        }
    }

    protected virtual void OnDisable()
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

    protected float GetFinalSpeed()
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
