using UnityEngine;

// Placed in Gameplay scene. Assign spawn point Transforms in the Inspector.
// WaveManager calls SpawnEnemy() with the data it wants.

public class WaveSpawner : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Top-of-screen positions where enemies appear. Add 3-5 evenly spaced.")]
    [SerializeField] private Transform[] _spawnPoints;

    // Spawn points define horizontal bounds (left/right edges).
    // X position is randomized between bounds for natural spawn spread.
    public Enemy SpawnEnemy(EnemyDataSO data, BaybayinCharacterSO characterOverride = null)
    {
        if (data == null)
        {
            DebugLogger.LogError("WaveSpawner: Cannot spawn with null EnemyDataSO.");
            return null;
        }

        if (EnemyPool.Instance == null)
        {
            DebugLogger.LogError("WaveSpawner: EnemyPool.Instance is missing.");
            return null;
        }

        if (_spawnPoints == null || _spawnPoints.Length < 2)
        {
            DebugLogger.LogError("WaveSpawner: Need at least 2 spawn points for min/max X!");
            return null;
        }

        Enemy enemy = EnemyPool.Instance.Get(data, characterOverride);

        // Use first and last spawn points as left/right bounds
        float minX = _spawnPoints[0].position.x;
        float maxX = _spawnPoints[_spawnPoints.Length - 1].position.x;
        float spawnY = _spawnPoints[0].position.y;

        float randomX = Random.Range(minX, maxX);
        enemy.transform.position = new Vector3(randomX, spawnY, 0f);

        return enemy;
    }
}
