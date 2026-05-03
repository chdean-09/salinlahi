using UnityEngine;

// applies a horizontal sine offset around the spawn-X every frame.
[RequireComponent(typeof(Enemy))]
public class PensionadoMover : MonoBehaviour
{
    [Tooltip("Multiplier for side-to-side movement speed. Higher = faster zigzag.")]
    [SerializeField] private float speedMultiplier = 1f;

    private Enemy _enemy;
    private float _baseX;
    private float _spawnTime;
    private bool _baseXInitialized;

    private void OnEnable()
    {
        _enemy = GetComponent<Enemy>();
        _spawnTime = Time.time;
        _baseXInitialized = false;
    }

    private void Update()
    {
        // Capture spawn X on the first Update, after WaveSpawner has positioned the enemy.
        // OnEnable fires before WaveSpawner sets the position, so capturing there picks up
        // the off-screen pool position (X ≈ -9.2) instead of the actual spawn X.
        if (!_baseXInitialized)
        {
            _baseX = transform.position.x;
            _baseXInitialized = true;
        }

        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        // Freeze horizontal oscillation while the death animation plays so the
        // sprite does not slide sideways after defeat.
        if (_enemy == null || _enemy.IsDying) return;

        EnemyDataSO data = _enemy.Data;
        if (data == null || data.zigzagAmplitude <= 0f) return;

        float t = Time.time - _spawnTime;
        float offset = Mathf.Sin(t * Mathf.PI * 2f * data.zigzagFrequency * speedMultiplier)
                       * data.zigzagAmplitude;

        Vector3 pos = transform.position;
        pos.x = _baseX + offset;
        transform.position = pos;
    }
}