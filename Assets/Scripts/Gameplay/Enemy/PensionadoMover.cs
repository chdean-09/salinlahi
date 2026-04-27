using UnityEngine;

// applies a horizontal sine offset around the spawn-X every frame.
[RequireComponent(typeof(Enemy))]
public class PensionadoMover : MonoBehaviour
{
    private Enemy _enemy;
    private float _baseX;
    private float _spawnTime;

    private void OnEnable()
    {
        _enemy = GetComponent<Enemy>();
        _baseX = transform.position.x;
        _spawnTime = Time.time;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        if (data == null || data.zigzagAmplitude <= 0f) return;

        float t = Time.time - _spawnTime;
        float offset = Mathf.Sin(t * Mathf.PI * 2f * data.zigzagFrequency)
                       * data.zigzagAmplitude;

        Vector3 pos = transform.position;
        pos.x = _baseX + offset;
        transform.position = pos;
    }
}