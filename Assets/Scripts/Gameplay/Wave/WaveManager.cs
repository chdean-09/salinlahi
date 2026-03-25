using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private LevelConfigSO _levelConfig;
    [SerializeField] private EnemyDataSO _defaultEnemyData; // Sprint 1 only
    [SerializeField] private WaveSpawner _spawner;

    private int _currentWaveIndex = 0;
    private bool _running = false;
    private int _activeEnemyCount = 0;

    private void OnEnable()
    {
        EventBus.OnEnemyDefeated += OnEnemyRemoved;
        EventBus.OnBaseHit += OnEnemyRemoved;
    }

    private void OnDisable()
    {
        EventBus.OnEnemyDefeated -= OnEnemyRemoved;
        EventBus.OnBaseHit -= OnEnemyRemoved;
    }

    private void Start()
    {
        StartWaves();
    }

    public void StartWaves()
    {
        if (_levelConfig == null)
        {
            DebugLogger.LogError("WaveManager: No LevelConfigSO assigned!");
            return;
        }
        _running = true;
        _currentWaveIndex = 0;
        StartCoroutine(RunAllWavesRoutine());
    }

    public void PauseWaves()
    {
        _running = false;
        StopAllCoroutines();
    }

    private IEnumerator RunAllWavesRoutine()
    {
        foreach (WaveConfigSO wave in _levelConfig.waves)
        {
            yield return new WaitForSeconds(wave.waveStartDelay);
            EventBus.RaiseWaveStarted(_currentWaveIndex);
            DebugLogger.Log($"Starting wave {_currentWaveIndex + 1}");
            yield return StartCoroutine(SpawnWaveRoutine(wave));

            // Wait until all enemies from this wave are gone
            yield return new WaitUntil(() => _activeEnemyCount <= 0);
            DebugLogger.Log($"Wave {_currentWaveIndex + 1} cleared.");
            _currentWaveIndex++;
        }
        DebugLogger.Log("All waves cleared. Level complete.");
        EventBus.RaiseLevelComplete();
    }

    private IEnumerator SpawnWaveRoutine(WaveConfigSO wave)
    {
        for (int i = 0; i < wave.enemyCount; i++)
        {
            if (!_running) yield break;

            // Sprint 1: use default enemy data for all spawns.
            // Sprint 2: map wave.charactersInWave to specific EnemyDataSO assets.
            Enemy enemy = _spawner.SpawnEnemy(_defaultEnemyData);
            if (enemy != null) _activeEnemyCount++;
            yield return new WaitForSeconds(wave.spawnInterval);
        }
    }

    // One handler for both defeat and base-hit -- both remove from active count
    private void OnEnemyRemoved(BaybayinCharacterSO _) => _activeEnemyCount = Mathf.Max(0, _activeEnemyCount - 1);
    private void OnEnemyRemoved() => _activeEnemyCount = Mathf.Max(0, _activeEnemyCount - 1);
}