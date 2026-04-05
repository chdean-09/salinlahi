using UnityEngine;

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
        float finalSpeed = _speed * _focusSpeedMultiplier;
        transform.Translate(Vector2.down * finalSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerBase")) return;
        // Enemy reached the base. Fire event and return to pool.
        EventBus.RaiseBaseHit();
        GetComponent<Enemy>()?.ReturnToPool();
    }

    private void OnDisable()
    {
        EventBus.OnFocusModeActivated -= HandleFocusOn;
        EventBus.OnFocusModeDeactivated -= HandleFocusOff;
        _focusSpeedMultiplier = 1f;
    }

    private void HandleFocusOn()
    {
        _focusSpeedMultiplier = ComboManager.Instance.FocusSpeedMultiplier;
    }

    private void HandleFocusOff()
    {
        _focusSpeedMultiplier = 1f;
    }
}
