using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Placed in Gameplay scene. Assign spawn point Transforms in the Inspector.
// WaveManager controls wave sequencing and calls SpawnWave()/SpawnEnemy().
public class WaveSpawner : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Top-of-screen positions where enemies appear. Add 3-5 evenly spaced.")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("Fallback")]
    [Tooltip("Used when a wave spawn chooses no valid enemy type.")]
    [SerializeField] private EnemyDataSO _fallbackEnemyData;

    public void SetFallbackEnemyDataIfMissing(EnemyDataSO fallbackData)
    {
        if (_fallbackEnemyData != null || fallbackData == null)
            return;

        _fallbackEnemyData = fallbackData;
        DebugLogger.Log($"WaveSpawner: Applied migrated fallback enemy data '{fallbackData.name}'.");
    }

    // Spawn points define horizontal bounds (left/right edges).
    // X position is randomized between bounds for natural spawn spread.
    public Enemy SpawnEnemy(EnemyDataSO data)
    {
        EnemyDataSO finalData = ResolveEnemyData(data);
        if (finalData == null)
        {
            DebugLogger.LogError("WaveSpawner.SpawnEnemy: No enemy data resolved (input and fallback are null).");
            return null;
        }

        EnemyPool pool = EnemyPool.Instance;
        if (pool == null)
        {
            DebugLogger.LogError("WaveSpawner.SpawnEnemy: EnemyPool.Instance is missing.");
            return null;
        }

        if (!TryGetSpawnBounds(out float minX, out float maxX, out float spawnY))
        {
            DebugLogger.LogError("WaveSpawner.SpawnEnemy: Invalid spawn points. Need valid first/last entries.");
            return null;
        }

        Enemy enemy = pool.Get(finalData);
        if (enemy == null)
            return null;

        float randomX = UnityEngine.Random.Range(minX, maxX);
        enemy.transform.position = new Vector3(randomX, spawnY, 0f);
        return enemy;
    }

    public Enemy SpawnEnemy(EnemyDataSO data, BaybayinCharacterSO character)
    {
        Enemy enemy = SpawnEnemy(data);
        if (enemy != null)
            enemy.AssignCharacter(character);

        return enemy;
    }

    public Enemy RestoreEnemy(
        EnemyDataSO data,
        BaybayinCharacterSO character,
        Vector3 position,
        int currentHealth)
    {
        EnemyDataSO finalData = ResolveEnemyData(data);
        if (finalData == null)
        {
            DebugLogger.LogError("WaveSpawner.RestoreEnemy: No enemy data resolved.");
            return null;
        }

        EnemyPool pool = EnemyPool.Instance;
        if (pool == null)
        {
            DebugLogger.LogError("WaveSpawner.RestoreEnemy: EnemyPool.Instance is missing.");
            return null;
        }

        Enemy enemy = pool.Get(finalData);
        if (enemy == null)
            return null;

        enemy.transform.position = position;
        enemy.AssignCharacter(character);
        enemy.RestoreCurrentHealth(currentHealth);
        return enemy;
    }

    public IEnumerator SpawnWave(WaveConfigSO wave, Action onEnemySpawned = null, int spawnOffset = 0)
    {
        if (wave == null)
        {
            DebugLogger.LogWarning("WaveSpawner.SpawnWave: Wave is null. Skipping.");
            yield break;
        }

        if (!TryGetSpawnBounds(out _, out _, out _))
        {
            DebugLogger.LogError("WaveSpawner.SpawnWave: Invalid spawn points. Wave spawn aborted.");
            yield break;
        }

        int enemyCount = wave.enemyCount;
        if (enemyCount <= 0)
        {
            DebugLogger.LogWarning($"WaveSpawner.SpawnWave: enemyCount <= 0 for wave '{wave.name}'. Spawning zero enemies.");
            yield break;
        }

        int firstSpawnIndex = Mathf.Clamp(spawnOffset, 0, enemyCount);
        if (firstSpawnIndex >= enemyCount)
            yield break;

        float interval = GetClampedSpawnInterval(wave);

        for (int i = firstSpawnIndex; i < enemyCount; i++)
        {
            EnemyDataSO data = SelectEnemyDataForSpawn(wave);
            BaybayinCharacterSO character = SelectCharacterForSpawn(wave, data);

            Enemy enemy = SpawnEnemy(data);
            if (enemy != null)
            {
                enemy.AssignCharacter(character);
                onEnemySpawned?.Invoke();
            }

            if (i < enemyCount - 1)
                yield return new WaitForSeconds(interval);
        }
    }

    private EnemyDataSO ResolveEnemyData(EnemyDataSO candidate)
    {
        if (candidate != null)
            return candidate;

        return _fallbackEnemyData;
    }

    private EnemyDataSO SelectEnemyDataForSpawn(WaveConfigSO wave)
    {
        EnemyDataSO selected = null;

        if (wave.enemyTypesInWave != null && wave.enemyTypesInWave.Count > 0)
        {
            List<EnemyDataSO> validTypes = new List<EnemyDataSO>();
            for (int i = 0; i < wave.enemyTypesInWave.Count; i++)
            {
                if (wave.enemyTypesInWave[i] != null)
                    validTypes.Add(wave.enemyTypesInWave[i]);
            }

            if (validTypes.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, validTypes.Count);
                selected = validTypes[index];
            }
        }

        return ResolveEnemyData(selected);
    }

    private BaybayinCharacterSO SelectCharacterForSpawn(WaveConfigSO wave, EnemyDataSO selectedEnemyData)
    {
        if (wave.charactersInWave != null && wave.charactersInWave.Count > 0)
        {
            List<BaybayinCharacterSO> validCharacters = new List<BaybayinCharacterSO>();
            for (int i = 0; i < wave.charactersInWave.Count; i++)
            {
                if (wave.charactersInWave[i] != null)
                    validCharacters.Add(wave.charactersInWave[i]);
            }

            if (validCharacters.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, validCharacters.Count);
                return validCharacters[index];
            }
        }

        BaybayinCharacterSO fallbackCharacter = selectedEnemyData != null
            ? selectedEnemyData.assignedCharacter
            : null;

        if (fallbackCharacter == null)
            DebugLogger.LogWarning("WaveSpawner.SpawnWave: Spawned enemy with null character assignment.");

        return fallbackCharacter;
    }

    private float GetClampedSpawnInterval(WaveConfigSO wave)
    {
        float interval = wave.spawnInterval;
        if (interval <= 0f)
        {
            DebugLogger.LogWarning($"WaveSpawner.SpawnWave: spawnInterval <= 0 for wave '{wave.name}'. Using 0.");
            return 0f;
        }

        return interval;
    }

    private bool TryGetSpawnBounds(out float minX, out float maxX, out float spawnY)
    {
        minX = 0f;
        maxX = 0f;
        spawnY = 0f;

        if (_spawnPoints == null || _spawnPoints.Length < 2)
            return false;

        Transform first = _spawnPoints[0];
        Transform last = _spawnPoints[_spawnPoints.Length - 1];
        if (first == null || last == null)
            return false;

        minX = Mathf.Min(first.position.x, last.position.x);
        maxX = Mathf.Max(first.position.x, last.position.x);
        spawnY = first.position.y;
        return true;
    }
}
