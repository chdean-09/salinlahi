using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Placed in Gameplay scene. Assign spawn point Transforms in the Inspector.
// WaveManager controls wave sequencing and calls SpawnWave()/SpawnEnemy().
public class WaveSpawner : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Top-of-screen positions where enemies appear. Add 3-5 evenly spaced.")]
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("Where the boss appears at encounter start. Y is used instead of the enemy spawn-point Y so the boss enters within the visible play area even when enemy spawn points are above the screen.")]
    [SerializeField] private Transform _bossSpawnPoint;

    [Header("Fallback")]
    [Tooltip("Used when a wave spawn chooses no valid enemy type.")]
    [SerializeField] private EnemyDataSO _fallbackEnemyData;

    [Header("Spawn Spread")]
    [Tooltip("Minimum world-X distance a spawn tries to keep from the previous one. A wave mixes " +
             "moveSpeeds (Level 6 spans 0.85-1.9), so a fast enemy catches a slow one and the pair " +
             "stacks; keeping them apart horizontally keeps both readable. 0 disables. Raising this " +
             "past roughly half the spawn band is counter-productive: spawns ping-pong between the " +
             "two edges and every second pair lines up again.")]
    [SerializeField] private float _minLateralSpawnSeparation = 1.8f;

    [Tooltip("How many times a spawn re-rolls its X looking for one that clears the separation. " +
             "Bounded so a band narrower than the separation cannot stall the spawn.")]
    [SerializeField] private int _lateralSeparationAttempts = 8;

    [Tooltip("Order a wave's enemies fastest-first instead of spawning them in the order they were " +
             "rolled. A later spawn is then never faster than the one ahead of it, so the gap " +
             "between them only grows and no enemy can catch and stack on another. Trade-off: every " +
             "wave's speed profile becomes fast-to-slow, a consistent rhythm rather than a random " +
             "one. Turn off to restore the rolled order.")]
    [SerializeField] private bool _spawnFastestFirst = true;

    // X of the previous spawn, or null before the first one this session.
    private float? _lastSpawnX;

    public void SetFallbackEnemyDataIfMissing(EnemyDataSO fallbackData)
    {
        if (_fallbackEnemyData != null || fallbackData == null)
            return;

        _fallbackEnemyData = fallbackData;
        DebugLogger.Log($"WaveSpawner: Applied migrated fallback enemy data '{fallbackData.name}'.");
    }

    // Spawn points define horizontal bounds (left/right edges).
    // X position is randomized between bounds for natural spawn spread.
    public virtual Enemy SpawnEnemy(EnemyDataSO data)
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

        float spawnX = PickSpawnX(minX, maxX);
        _lastSpawnX = spawnX;
        enemy.transform.position = new Vector3(spawnX, spawnY, 0f);
        return enemy;
    }

    // Random X across the band, re-rolled a bounded number of times to land clear of the previous
    // spawn. Falls back to the last roll rather than looping, so a band narrower than the
    // separation still spawns.
    private float PickSpawnX(float minX, float maxX)
    {
        float x = UnityEngine.Random.Range(minX, maxX);
        if (_minLateralSpawnSeparation <= 0f || !_lastSpawnX.HasValue)
            return x;

        for (int attempt = 0; attempt < _lateralSeparationAttempts; attempt++)
        {
            if (Mathf.Abs(x - _lastSpawnX.Value) >= _minLateralSpawnSeparation)
                break;

            x = UnityEngine.Random.Range(minX, maxX);
        }

        return x;
    }

    public Enemy SpawnEnemy(EnemyDataSO data, BaybayinCharacterSO character)
    {
        Enemy enemy = SpawnEnemy(data);
        if (enemy != null)
            enemy.AssignCharacter(character);

        return enemy;
    }

    // Boss-specific entry point: spawns the enemy at the horizontal center
    // of the spawn bounds rather than a random X. Uses _bossSpawnPoint.y
    // when assigned so the boss appears within the visible play area even
    // when enemy _spawnPoints are positioned above the screen.
    public Enemy SpawnBossEnemy(EnemyDataSO data)
    {
        Enemy enemy = SpawnEnemy(data);
        if (enemy == null)
            return null;

        if (TryGetSpawnBounds(out float minX, out float maxX, out float spawnY))
        {
            float centerX = (minX + maxX) * 0.5f;
            float bossY = _bossSpawnPoint != null ? _bossSpawnPoint.position.y : spawnY;
            enemy.transform.position = new Vector3(centerX, bossY, 0f);
            // SpawnEnemy recorded the random X it rolled; the boss overrides it, so correct the
            // separation anchor to where the boss actually is.
            _lastSpawnX = centerX;
        }

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

    public virtual IEnumerator SpawnWave(WaveDefinition wave, Action onEnemySpawned = null, int spawnOffset = 0)
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
            DebugLogger.LogWarning("WaveSpawner.SpawnWave: enemyCount <= 0 for a wave. Spawning zero enemies.");
            yield break;
        }

        int firstSpawnIndex = Mathf.Clamp(spawnOffset, 0, enemyCount);
        if (firstSpawnIndex >= enemyCount)
            yield break;

        float interval = GetClampedSpawnInterval(wave);
        List<EnemyDataSO> spawnOrder = BuildSpawnOrder(wave, enemyCount);

        for (int i = firstSpawnIndex; i < enemyCount; i++)
        {
            EnemyDataSO data = spawnOrder[i];
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

    // Rolls the whole wave's types up front so they can be ordered before the first spawn.
    //
    // A wave rolls each type independently and its roster mixes moveSpeed (Level 6 spans 0.85-1.9),
    // so a fast enemy rolled late catches the slow one ahead and the two stack into one unreadable
    // silhouette. Spawning fastest-first removes that: a later spawn is never faster than the one
    // ahead, so their gap only grows, and by the time the follower descends into view it has already
    // separated.
    //
    // The sort is stable (LINQ OrderByDescending), so equal-speed enemies keep the order they were
    // rolled in and waves do not collapse into a fixed sequence.
    private List<EnemyDataSO> BuildSpawnOrder(WaveDefinition wave, int enemyCount)
    {
        List<EnemyDataSO> order = new(enemyCount);
        for (int i = 0; i < enemyCount; i++)
            order.Add(SelectEnemyDataForSpawn(wave));

        if (!_spawnFastestFirst)
            return order;

        // Null data can only come from an unresolvable roll; sort it last so SpawnEnemy's existing
        // error path is reached at the end of the wave rather than displacing a real enemy.
        return order.OrderByDescending(d => d != null ? d.moveSpeed : float.NegativeInfinity).ToList();
    }

    private EnemyDataSO ResolveEnemyData(EnemyDataSO candidate)
    {
        if (candidate != null)
            return candidate;

        return _fallbackEnemyData;
    }

    private EnemyDataSO SelectEnemyDataForSpawn(WaveDefinition wave)
    {
        EnemyDataSO selected = null;

        if (wave.enemyTypes != null && wave.enemyTypes.Count > 0)
        {
            List<EnemyDataSO> validTypes = new List<EnemyDataSO>();
            for (int i = 0; i < wave.enemyTypes.Count; i++)
            {
                if (wave.enemyTypes[i] != null)
                    validTypes.Add(wave.enemyTypes[i]);
            }

            if (validTypes.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, validTypes.Count);
                selected = validTypes[index];
            }
        }

        return ResolveEnemyData(selected);
    }

    private BaybayinCharacterSO SelectCharacterForSpawn(WaveDefinition wave, EnemyDataSO selectedEnemyData)
    {
        if (wave.characters != null && wave.characters.Count > 0)
        {
            List<BaybayinCharacterSO> validCharacters = new List<BaybayinCharacterSO>();
            for (int i = 0; i < wave.characters.Count; i++)
            {
                if (wave.characters[i] != null)
                    validCharacters.Add(wave.characters[i]);
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

    private float GetClampedSpawnInterval(WaveDefinition wave)
    {
        float interval = wave.spawnInterval;
        if (interval <= 0f)
        {
            DebugLogger.LogWarning("WaveSpawner.SpawnWave: spawnInterval <= 0 for a wave. Using 0.");
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
