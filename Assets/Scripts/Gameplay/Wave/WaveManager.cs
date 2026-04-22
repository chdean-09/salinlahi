using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
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
    private int _currentWaveSpawnedCount;
    private bool _running;
    private Coroutine _waveRoutine;

    public int CurrentWaveIndex => _currentWaveIndex;
    public int CurrentWaveSpawnedCount => _currentWaveSpawnedCount;

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
        // Fall back to PlayerPrefs if not set or stale (e.g. Play after a previous level select).
        int selectedLevel = PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1);
        LevelConfigSO selectedGameManagerLevel = GameManager.Instance != null
            ? GameManager.Instance.CurrentLevel
            : null;

        if (selectedGameManagerLevel != null && selectedGameManagerLevel.levelNumber == selectedLevel)
        {
            _levelConfig = selectedGameManagerLevel;
        }
        else
        {
            LoadLevelConfig(selectedLevel);
        }

        StartLevel(selectedLevel);
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
        int selectedLevel = PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1);
        StartLevel(selectedLevel);
    }

    private void StartLevel(int selectedLevel)
    {
        if (_spawner != null)
            _spawner.SetFallbackEnemyDataIfMissing(_legacyDefaultEnemyData);

        if (TryHandleSandboxMode())
            return;

        if (_levelConfig == null)
        {
            DebugLogger.LogError("WaveManager.StartLevel: No LevelConfigSO assigned.");
            return;
        }

        if (TryRestorePausedRun(selectedLevel))
            return;

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
        _currentWaveSpawnedCount = 0;
        _waveRoutine = StartCoroutine(RunAllWavesRoutine(0, 0));
    }

    private bool TryHandleSandboxMode()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        if (!SandboxMode.IsActive)
            return false;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        PauseWaves();
        SandboxController.EnsureExists(this, _spawner);
        DebugLogger.Log("WaveManager: Sandbox mode active. Normal waves are disabled.");
        return true;
#else
        return false;
#endif
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

    private bool TryRestorePausedRun(int selectedLevel)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return false;

        if (!gameManager.TryGetPausedRunEnemies(
                selectedLevel,
                out IReadOnlyList<GameManager.PausedEnemySnapshot> pausedEnemies))
        {
            return false;
        }

        int currentWaveIndex = 0;
        int currentWaveSpawnedCount = 0;
        bool hasSavedWaveProgress = gameManager.TryGetPausedRunWaveProgress(
            selectedLevel,
            out currentWaveIndex,
            out currentWaveSpawnedCount);

        if (GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        _running = true;
        _currentWaveIndex = Mathf.Max(0, currentWaveIndex);
        _currentWaveSpawnedCount = Mathf.Max(0, currentWaveSpawnedCount);
        _waveRoutine = StartCoroutine(
            pausedEnemies.Count > 0
                ? RestorePausedRunRoutine(
                    selectedLevel,
                    pausedEnemies,
                    hasSavedWaveProgress,
                    currentWaveIndex,
                    currentWaveSpawnedCount)
                : RestorePausedRunWithoutActiveEnemiesRoutine(
                    selectedLevel,
                    hasSavedWaveProgress,
                    currentWaveIndex,
                    currentWaveSpawnedCount));
        return true;
    }

    private IEnumerator RestorePausedRunRoutine(
        int selectedLevel,
        IReadOnlyList<GameManager.PausedEnemySnapshot> pausedEnemies,
        bool hasSavedWaveProgress,
        int savedWaveIndex,
        int savedWaveSpawnedCount)
    {
        if (!ValidateRunDependencies())
        {
            AbortRun();
            yield break;
        }

        for (int i = 0; i < pausedEnemies.Count; i++)
        {
            GameManager.PausedEnemySnapshot snapshot = pausedEnemies[i];
            _spawner.RestoreEnemy(
                snapshot.EnemyData,
                snapshot.Character,
                snapshot.Position,
                snapshot.CurrentHealth);
        }

        GameManager.Instance?.ClearPausedRunSnapshotForLevel(selectedLevel);

        int startWaveIndex = ResolveResumeWaveIndex(
            hasSavedWaveProgress,
            savedWaveIndex,
            savedWaveSpawnedCount,
            out int spawnOffset);

        if (spawnOffset <= 0)
            yield return WaitForActiveEnemiesCleared();

        if (!CanContinueRun())
        {
            AbortRun();
            yield break;
        }

        yield return RunAllWavesRoutine(startWaveIndex, spawnOffset);
    }

    private IEnumerator RestorePausedRunWithoutActiveEnemiesRoutine(
        int selectedLevel,
        bool hasSavedWaveProgress,
        int savedWaveIndex,
        int savedWaveSpawnedCount)
    {
        GameManager.Instance?.ClearPausedRunSnapshotForLevel(selectedLevel);

        if (!CanContinueRun())
        {
            AbortRun();
            yield break;
        }

        int startWaveIndex = ResolveResumeWaveIndex(
            hasSavedWaveProgress,
            savedWaveIndex,
            savedWaveSpawnedCount,
            out int spawnOffset);
        yield return RunAllWavesRoutine(startWaveIndex, spawnOffset);
    }

    private IEnumerator RunAllWavesRoutine(int startWaveIndex, int firstWaveSpawnOffset)
    {
        if (!ValidateRunDependencies())
        {
            AbortRun();
            yield break;
        }

        if (_levelConfig.isBossLevel && _levelConfig.bossConfig != null)
        {
            yield return StartCoroutine(RunBossEncounter(_levelConfig.bossConfig));
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

        int firstWaveIndex = Mathf.Clamp(startWaveIndex, 0, _levelConfig.waves.Count);
        for (int waveIndex = firstWaveIndex; waveIndex < _levelConfig.waves.Count; waveIndex++)
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
            _currentWaveSpawnedCount = 0;
            EventBus.RaiseWaveStarted(waveIndex);

            float startDelay = ClampWaveStartDelay(wave.waveStartDelay, waveIndex);
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            if (!CanContinueRun())
            {
                AbortRun();
                yield break;
            }

            int spawnOffset = waveIndex == firstWaveIndex
                ? Mathf.Clamp(firstWaveSpawnOffset, 0, Mathf.Max(0, wave.enemyCount))
                : 0;
            _currentWaveSpawnedCount = spawnOffset;
            yield return StartCoroutine(_spawner.SpawnWave(wave, HandleEnemySpawned, spawnOffset));

            if (!CanContinueRun())
            {
                AbortRun();
                yield break;
            }

            yield return WaitForActiveEnemiesCleared();

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

    private void HandleEnemySpawned()
    {
        _currentWaveSpawnedCount++;
    }

    private IEnumerator RunBossEncounter(BossConfigSO bossConfig)
    {
        if (bossConfig.bossEnemyData == null)
        {
            DebugLogger.LogError("WaveManager: BossConfig has no bossEnemyData assigned. Aborting boss encounter.");
            AbortRun();
            yield break;
        }

        if (_levelConfig.allowedCharacters == null || _levelConfig.allowedCharacters.Count == 0)
        {
            DebugLogger.LogError("WaveManager: Boss level has no allowedCharacters. Aborting boss encounter.");
            AbortRun();
            yield break;
        }

        EventBus.RaiseWaveStarted(0);
        yield return new WaitForSeconds(1f);

        BaybayinCharacterSO character = _levelConfig.allowedCharacters[
            Random.Range(0, _levelConfig.allowedCharacters.Count)];
        Enemy bossEnemy = _spawner.SpawnEnemy(bossConfig.bossEnemyData, character);

        if (bossEnemy == null)
        {
            DebugLogger.LogError("WaveManager: Failed to spawn boss enemy. Aborting boss encounter.");
            AbortRun();
            yield break;
        }

        yield return new WaitUntil(() =>
        {
            if (!CanContinueRun())
                return true;

            ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
            if (tracker == null)
            {
                DebugLogger.LogError("WaveManager: ActiveEnemyTracker.Instance is null during boss encounter.");
                return true;
            }

            return tracker.IsClear;
        });

        if (!CanContinueRun())
        {
            AbortRun();
            yield break;
        }

        EventBus.RaiseBossDefeated();
        EventBus.RaiseWaveCleared(0);
        CompleteRun();
    }

    private int ResolveResumeWaveIndex(
        bool hasSavedWaveProgress,
        int savedWaveIndex,
        int savedWaveSpawnedCount,
        out int spawnOffset)
    {
        spawnOffset = 0;

        if (!hasSavedWaveProgress || _levelConfig?.waves == null || _levelConfig.waves.Count == 0)
            return 0;

        int safeWaveIndex = Mathf.Clamp(savedWaveIndex, 0, _levelConfig.waves.Count);
        if (safeWaveIndex >= _levelConfig.waves.Count)
            return _levelConfig.waves.Count;

        WaveConfigSO savedWave = _levelConfig.waves[safeWaveIndex];
        int enemyCount = savedWave != null ? Mathf.Max(0, savedWave.enemyCount) : 0;
        int safeSpawnedCount = Mathf.Clamp(savedWaveSpawnedCount, 0, enemyCount);

        if (safeSpawnedCount < enemyCount)
        {
            spawnOffset = safeSpawnedCount;
            return safeWaveIndex;
        }

        return Mathf.Min(safeWaveIndex + 1, _levelConfig.waves.Count);
    }

    private IEnumerator WaitForActiveEnemiesCleared()
    {
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
        }
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
        _currentWaveSpawnedCount = 0;
    }

#if UNITY_EDITOR || SALINLAHI_SANDBOX
    public IReadOnlyList<EnemyDataSO> GetConfiguredEnemyTypesForSandbox()
    {
        var enemies = new List<EnemyDataSO>();
        AddEnemyForSandbox(enemies, _legacyDefaultEnemyData);
        AddEnemiesFromLevelForSandbox(enemies, _levelConfig);

        if (_levelConfigs != null)
        {
            foreach (LevelConfigSO levelConfig in _levelConfigs)
                AddEnemiesFromLevelForSandbox(enemies, levelConfig);
        }

        return enemies;
    }

    public IReadOnlyList<BaybayinCharacterSO> GetConfiguredCharactersForSandbox()
    {
        var characters = new List<BaybayinCharacterSO>();
        AddCharactersFromLevelForSandbox(characters, _levelConfig);

        if (_levelConfigs != null)
        {
            foreach (LevelConfigSO levelConfig in _levelConfigs)
                AddCharactersFromLevelForSandbox(characters, levelConfig);
        }

        return characters;
    }

    private static void AddEnemiesFromLevelForSandbox(List<EnemyDataSO> enemies, LevelConfigSO levelConfig)
    {
        if (levelConfig?.waves == null)
            return;

        foreach (WaveConfigSO wave in levelConfig.waves)
        {
            if (wave?.enemyTypesInWave == null)
                continue;

            foreach (EnemyDataSO enemy in wave.enemyTypesInWave)
                AddEnemyForSandbox(enemies, enemy);
        }
    }

    private static void AddEnemyForSandbox(List<EnemyDataSO> enemies, EnemyDataSO enemy)
    {
        if (enemy != null && !enemies.Contains(enemy))
            enemies.Add(enemy);
    }

    private static void AddCharactersFromLevelForSandbox(List<BaybayinCharacterSO> characters, LevelConfigSO levelConfig)
    {
        if (levelConfig?.waves == null)
            return;

        foreach (WaveConfigSO wave in levelConfig.waves)
        {
            if (wave?.charactersInWave == null)
                continue;

            foreach (BaybayinCharacterSO character in wave.charactersInWave)
                AddCharacterForSandbox(characters, character);
        }
    }

    private static void AddCharacterForSandbox(List<BaybayinCharacterSO> characters, BaybayinCharacterSO character)
    {
        if (character != null && !characters.Contains(character))
            characters.Add(character);
    }
#endif

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
