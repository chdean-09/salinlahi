using UnityEngine;

// Placed in Gameplay scene. Assign spawn point Transforms in the Inspector.
// WaveManager calls SpawnEnemy() with the data it wants.

public class WaveSpawner : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Top-of-screen positions where enemies appear. Add 3-5 evenly spaced.")]
    [SerializeField] private Transform[] _spawnPoints;

    private int _spawnIndex = 0;

    public Enemy SpawnEnemy(EnemyDataSO data)
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            DebugLogger.LogError("WaveSpawner: No spawn points assigned!");
            return null;
        }

        Enemy enemy = EnemyPool.Instance.Get(data);
        Transform spawnPoint = _spawnPoints[_spawnIndex % _spawnPoints.Length];
        enemy.transform.position = spawnPoint.position;

        // Cycle through spawn points so enemies stagger horizontally
        _spawnIndex++;
        return enemy;
    }
}