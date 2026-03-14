using UnityEngine;

// Handles enemy movement. Fires RaiseBaseHit when colliding with PlayerBase trigger.
[RequireComponent(typeof(Collider2D))]
public class EnemyMover : MonoBehaviour
{
    private float _speed;
    private bool _active;

    public void SetSpeed(float speed)
    {
        _speed = speed;
        _active = true;
    }

    public void Stop() => _active = false;

    private void Update()
    {
        if (!_active) return;
        // Portrait orientation: enemies move from top to bottom (negative Y direction)
        transform.Translate(Vector2.down * _speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerBase")) return;
        // Enemy reached the base. Fire event and return to pool.
        EventBus.RaiseBaseHit();
        GetComponent<Enemy>()?.ReturnToPool();
    }
}
