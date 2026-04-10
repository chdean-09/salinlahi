using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private LevelConfigSO _levelConfig;
    [SerializeField] private EnemyDataSO _defaultEnemyData;
    [SerializeField] private WaveSpawner _spawner;

    [Header("Level Registry")]
    [Tooltip("All level configs that can be loaded at runtime. Index 0 = Level 1, etc.")]
    [SerializeField] private LevelConfigSO[] _levelConfigs;

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
        // Try to load level from PlayerPrefs (set by LevelSelectUI)
        int selectedLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
        LoadLevelConfig(selectedLevel);
        StartWaves();
    }

    /// <summary>
    /// Starts a level with the specified config. This is the runtime API required by AC-4.
    /// </summary>
    /// <param name="levelConfigSO">The level configuration to use</param>
    public void StartLevel(LevelConfigSO levelConfigSO)
    {
        if (levelConfigSO == null)
        {
            DebugLogger.LogError("WaveManager.StartLevel: levelConfigSO is null!");
            return;
        }

        // Stop any current waves
        if (_running)
        {
            StopAllCoroutines();
            _running = false;
        }

        _levelConfig = levelConfigSO;
        StartWaves();
    }

    private void LoadLevelConfig(int levelNumber)
    {
        // Try to find config in the registry array first
        if (_levelConfigs != null && _levelConfigs.Length > 0)
        {
            int index = levelNumber - 1; // Level 1 is at index 0
            if (index >= 0 && index < _levelConfigs.Length && _levelConfigs[index] != null)
            {
                _levelConfig = _levelConfigs[index];
                DebugLogger.Log($"WaveManager: Loaded Level {levelNumber} from registry.");
                return;
            }
        }

        // Fallback: Try to load from Resources
        LevelConfigSO loadedConfig = Resources.Load<LevelConfigSO>($"LevelConfigs/Level{levelNumber}_Config");
        if (loadedConfig != null)
        {
            _levelConfig = loadedConfig;
            DebugLogger.Log($"WaveManager: Loaded Level {levelNumber} from Resources.");
            return;
        }

        // If we already have a config assigned in inspector, use that
        if (_levelConfig != null)
        {
            DebugLogger.LogWarning($"WaveManager: Could not find Level {levelNumber} config. Using inspector-assigned config: {_levelConfig.name}");
            return;
        }

        DebugLogger.LogError($"WaveManager: Could not load Level {levelNumber} config and no fallback assigned!");
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

            EnemyDataSO enemyData = SelectEnemyDataForWave(wave);
            Enemy enemy = _spawner.SpawnEnemy(enemyData);
            if (enemy != null) _activeEnemyCount++;
            yield return new WaitForSeconds(wave.spawnInterval);
        }
    }

    private EnemyDataSO SelectEnemyDataForWave(WaveConfigSO wave)
    {
        if (wave != null && wave.enemyTypesInWave != null && wave.enemyTypesInWave.Count > 0)
        {
            int index = Random.Range(0, wave.enemyTypesInWave.Count);
            EnemyDataSO selected = wave.enemyTypesInWave[index];
            if (selected != null)
                return selected;
        }

        return _defaultEnemyData;
    }

    // One handler for both defeat and base-hit -- both remove from active count
    private void OnEnemyRemoved(BaybayinCharacterSO _) => _activeEnemyCount = Mathf.Max(0, _activeEnemyCount - 1);
    private void OnEnemyRemoved() => _activeEnemyCount = Mathf.Max(0, _activeEnemyCount - 1);
}