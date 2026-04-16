using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class WaveManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private LevelConfigSO _levelConfig;
    [SerializeField] private WaveSpawner _spawner;
    [FormerlySerializedAs("_defaultEnemyData")]
    [SerializeField] private EnemyDataSO _legacyDefaultEnemyData;

    [Header("Level Registry")]
    [Tooltip("All level configs that can be loaded at runtime. Index 0 = Level 1, etc.")]
    [SerializeField] private LevelConfigSO[] _levelConfigs;

    private int _currentWaveIndex;
    private bool _running;
    private Coroutine _waveRoutine;

    private void OnEnable()
    {
        EventBus.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        EventBus.OnGameOver -= HandleGameOver;
    }

    private void Start()
    {
        // GameManager.CurrentLevel is set by LevelSelectUI before scene load.
        // Fall back to PlayerPrefs if not set (e.g. direct scene entry or Play button).
        if (GameManager.Instance != null && GameManager.Instance.CurrentLevel != null)
        {
            _levelConfig = GameManager.Instance.CurrentLevel;
        }
        else
        {
            int selectedLevel = PlayerPrefs.GetInt("SelectedLevel", 1);
            LoadLevelConfig(selectedLevel);
        }

        StartLevel();
    }

    /// <summary>
    /// Starts a level with the specified config.
    /// </summary>
    public void StartLevel(LevelConfigSO levelConfigSO)
    {
        _levelConfig = levelConfigSO;
        StartLevel();
    }

    /// <summary>
    /// Starts waves using the currently resolved level config.
    /// </summary>
    public void StartLevel()
    {
        if (_levelConfig == null)
        {
            DebugLogger.LogError("WaveManager.StartLevel: No LevelConfigSO assigned.");
            return;
        }

        if (_spawner != null)
            _spawner.SetFallbackEnemyDataIfMissing(_legacyDefaultEnemyData);

        // Ensure GameManager is in Playing state so input is not blocked.
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
        {
            GameManager.Instance.StartGame();
            DebugLogger.Log("WaveManager: Auto-started GameManager.");
        }

        if (_running || _waveRoutine != null)
        {
            if (_waveRoutine != null)
                StopCoroutine(_waveRoutine);

            ReturnAllActiveEnemies();
            ResetRunState();
        }

        _running = true;
        _currentWaveIndex = 0;
        _waveRoutine = StartCoroutine(RunAllWavesRoutine());
    }

    public void PauseWaves()
    {
        _running = false;

        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }
    }

    private void HandleGameOver()
    {
        _running = false;

        if (_waveRoutine != null)
            StopCoroutine(_waveRoutine);

        ReturnAllActiveEnemies();
        _waveRoutine = null;
    }

    private IEnumerator RunAllWavesRoutine()
    {
        if (!ValidateRunDependencies())
        {
            AbortRun();
            yield break;
        }

        if (_levelConfig.waves == null || _levelConfig.waves.Count == 0)
        {
            DebugLogger.LogWarning("WaveManager: Level has no waves.");
            if (CanContinueRun())
                CompleteRun();
            else
                AbortRun();
            yield break;
        }

        for (int waveIndex = 0; waveIndex < _levelConfig.waves.Count; waveIndex++)
        {
            if (!CanContinueRun())
            {
                AbortRun();
                yield break;
            }

            WaveConfigSO wave = _levelConfig.waves[waveIndex];
            if (wave == null)
            {
                DebugLogger.LogWarning($"WaveManager: Wave at index {waveIndex} is null. Skipping.");
                continue;
            }

            if (!ValidateRunDependencies())
            {
                AbortRun();
                yield break;
            }

            _currentWaveIndex = waveIndex;
            EventBus.RaiseWaveStarted(waveIndex);

            float startDelay = ClampWaveStartDelay(wave.waveStartDelay, waveIndex);
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            if (!CanContinueRun())
            {
                AbortRun();
                yield break;
            }

            yield return StartCoroutine(_spawner.SpawnWave(wave));

            if (!CanContinueRun())
            {
                AbortRun();
                yield break;
            }

            bool trackerMissingDuringWait = false;
            yield return new WaitUntil(() =>
            {
                if (!CanContinueRun())
                    return true;

                ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
                if (tracker == null)
                {
                    trackerMissingDuringWait = true;
                    return true;
                }

                return tracker.IsClear;
            });

            if (trackerMissingDuringWait)
            {
                DebugLogger.LogError("WaveManager: ActiveEnemyTracker.Instance became null while waiting for wave clear.");
                AbortRun();
                yield break;
            }

            if (!CanContinueRun())
            {
                AbortRun();
                yield break;
            }

            EventBus.RaiseWaveCleared(waveIndex);
        }

        if (!CanContinueRun())
        {
            AbortRun();
            yield break;
        }

        CompleteRun();
    }

    private void ReturnAllActiveEnemies()
    {
        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        EnemyPool pool = EnemyPool.Instance;

        if (tracker == null || pool == null)
            return;

        var activeEnemies = tracker.GetActiveEnemiesSnapshot();
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            pool.Return(activeEnemies[i]);
        }
    }

    private bool ValidateRunDependencies()
    {
        if (_spawner == null)
        {
            DebugLogger.LogError("WaveManager: WaveSpawner reference is missing.");
            return false;
        }

        if (EnemyPool.Instance == null)
        {
            DebugLogger.LogError("WaveManager: EnemyPool.Instance is missing.");
            return false;
        }

        if (ActiveEnemyTracker.Instance == null)
        {
            DebugLogger.LogError("WaveManager: ActiveEnemyTracker.Instance is missing.");
            return false;
        }

        return true;
    }

    private bool CanContinueRun()
    {
        if (!_running)
            return false;

        if (GameManager.Instance == null)
            return true;

        return !IsTerminalState(GameManager.Instance.CurrentState);
    }

    private bool IsTerminalState(GameState state)
    {
        return state == GameState.Idle
            || state == GameState.GameOver
            || state == GameState.LevelComplete;
    }

    private float ClampWaveStartDelay(float delay, int waveIndex)
    {
        if (delay < 0f)
        {
            DebugLogger.LogWarning($"WaveManager: waveStartDelay < 0 at index {waveIndex}. Clamping to 0.");
            return 0f;
        }

        return delay;
    }

    private void CompleteRun()
    {
        _running = false;
        _waveRoutine = null;
        RaiseLevelCompleted();
    }

    private void RaiseLevelCompleted()
    {
        EventBus.RaiseLevelComplete();
    }

    private void AbortRun()
    {
        _running = false;
        _waveRoutine = null;
    }

    private void ResetRunState()
    {
        _running = false;
        _waveRoutine = null;
        _currentWaveIndex = 0;
    }

    private void LoadLevelConfig(int levelNumber)
    {
        // Try to find config in the registry array first.
        if (_levelConfigs != null && _levelConfigs.Length > 0)
        {
            int index = levelNumber - 1; // Level 1 is at index 0.
            if (index >= 0 && index < _levelConfigs.Length && _levelConfigs[index] != null)
            {
                _levelConfig = _levelConfigs[index];
                DebugLogger.Log($"WaveManager: Loaded Level {levelNumber} from registry.");
                return;
            }
        }

        // Fallback: try to load from Resources.
        LevelConfigSO loadedConfig = Resources.Load<LevelConfigSO>($"LevelConfigs/Level{levelNumber}_Config");
        if (loadedConfig != null)
        {
            _levelConfig = loadedConfig;
            DebugLogger.Log($"WaveManager: Loaded Level {levelNumber} from Resources.");
            return;
        }

        // If we already have a config assigned in inspector, use that.
        if (_levelConfig != null)
        {
            DebugLogger.LogWarning($"WaveManager: Could not find Level {levelNumber} config. Using inspector-assigned config: {_levelConfig.name}");
            return;
        }

        DebugLogger.LogError($"WaveManager: Could not load Level {levelNumber} config and no fallback assigned.");
    }
}
