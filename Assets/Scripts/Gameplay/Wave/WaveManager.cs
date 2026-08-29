using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#if UNITY_EDITOR
using UnityEditor;
#endif
#endif
using UnityEngine;
using UnityEngine.Serialization;

public class WaveManager : MonoBehaviour
{
    public static IReadOnlyList<BaybayinCharacterSO> CurrentAllowedCharacters { get; private set; }
    private static WaveManager _currentAllowedCharactersOwner;

    [Header("Configuration")]
    [Tooltip("If true, WaveManager waits for an external call to StartLevel() instead of auto-starting in Start(). Set to true when LevelFlowController is present.")]
    [SerializeField] private bool _waitForExternalStart;
    [SerializeField] private LevelConfigSO _levelConfig;
    [SerializeField] private WaveSpawner _spawner;
    [FormerlySerializedAs("_legacyDefaultEnemyData")]
    [FormerlySerializedAs("_defaultEnemyData")]
    [SerializeField] private EnemyDataSO _fallbackEnemyData;

    [Header("Level Registry")]
    [Tooltip("All level configs that can be loaded at runtime. Index 0 = Level 1, etc.")]
    [SerializeField] private LevelConfigSO[] _levelConfigs;

#if UNITY_EDITOR || SALINLAHI_SANDBOX
    [Header("Sandbox Registry")]
    [Tooltip("Runtime-safe enemy data catalog for sandbox builds where AssetDatabase is unavailable.")]
    [SerializeField] private List<EnemyDataSO> _sandboxEnemyData = new();
    [Tooltip("Full character catalog used only by sandbox spawning and sandbox visual scramble checks.")]
    [SerializeField] private CharacterRegistrySO _sandboxCharacterRegistry;
#endif

    private int _currentWaveIndex;
    private int _currentWaveSpawnedCount;
    private bool _running;
    private Coroutine _waveRoutine;

    public int CurrentWaveIndex => _currentWaveIndex;
    public int CurrentWaveSpawnedCount => _currentWaveSpawnedCount;

    private void OnEnable()
    {
        EventBus.OnGameOver += HandleGameOver;
        EventBus.OnLevelAttemptAborted += HandleLevelAttemptAborted;

        if (_currentAllowedCharactersOwner != null && _currentAllowedCharactersOwner != this)
        {
            DebugLogger.LogWarning(
                $"WaveManager: Multiple active WaveManager instances detected. "
                + $"'{name}' is taking ownership of CurrentAllowedCharacters.");
        }

        _currentAllowedCharactersOwner = this;
    }

    private void OnDisable()
    {
        EventBus.OnGameOver -= HandleGameOver;
        EventBus.OnLevelAttemptAborted -= HandleLevelAttemptAborted;

        if (_currentAllowedCharactersOwner == this)
        {
            CurrentAllowedCharacters = null;
            _currentAllowedCharactersOwner = null;
        }
    }

    private void Awake()
    {
        // Resolve the level config and propagate to GameManager.CurrentLevel
        // here (not in Start) so that other scene components — most notably
        // EnvironmentThemeSwapper.Start — read the correct level. MainMenu's
        // Play button intentionally clears CurrentLevel before loading the
        // scene; this is the recovery path that reads SelectedLevel from
        // PlayerPrefs and re-hydrates GameManager before any Start runs.
        EnsureLevelConfigResolvedAndPropagated();
    }

    private void Start()
    {
        // Safety re-resolve in case SelectedLevel changed between Awake and
        // Start (rare, but cheap).
        EnsureLevelConfigResolvedAndPropagated();

        if (!_waitForExternalStart
            && LevelFlowController.TryStartRuntimeTutorialFlow(_levelConfig, this, _spawner, _fallbackEnemyData))
        {
            return;
        }

        if (!_waitForExternalStart)
        {
            int selectedLevel = ProgressManager.Instance != null
                ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
            StartLevel(selectedLevel);
        }
    }

    private void EnsureLevelConfigResolvedAndPropagated()
    {
        int selectedLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
        LevelConfigSO existing = GameManager.Instance != null
            ? GameManager.Instance.CurrentLevel
            : null;

        if (existing != null && existing.levelNumber == selectedLevel)
        {
            _levelConfig = existing;
            return;
        }

        LoadLevelConfig(selectedLevel);

        if (_levelConfig != null
            && GameManager.Instance != null
            && GameManager.Instance.CurrentLevel != _levelConfig)
        {
            GameManager.Instance.SetLevel(_levelConfig);
        }
    }

    /// <summary>
    /// Starts a level with the specified config.
    /// </summary>
    public void StartLevel(LevelConfigSO levelConfigSO)
    {
        _levelConfig = levelConfigSO;

        // BossController and BossSummonTicker sample allowed characters from
        // GameManager.CurrentLevel. Keep it in sync so boss encounters started
        // by passing a LevelConfigSO directly here can resolve glyphs.
        if (levelConfigSO != null
            && GameManager.Instance != null
            && GameManager.Instance.CurrentLevel != levelConfigSO)
        {
            GameManager.Instance.SetLevel(levelConfigSO);
        }

        StartLevel();
    }

    /// <summary>
    /// Starts waves using the currently resolved level config.
    /// </summary>
    public void StartLevel()
    {
        int selectedLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
        StartLevel(selectedLevel);
    }

    private void StartLevel(int selectedLevel)
    {
        SetCurrentAllowedCharacters(null);

        if (_spawner != null)
            _spawner.SetFallbackEnemyDataIfMissing(_fallbackEnemyData);

        // Sandbox must be handled before the level-config guard: sandbox mode
        // explicitly starts without a LevelConfigSO (see SandboxModeTests).
        if (TryHandleSandboxMode(selectedLevel))
            return;

        if (_levelConfig == null)
        {
            DebugLogger.LogError("WaveManager.StartLevel: No LevelConfigSO assigned.");
            return;
        }

        SetCurrentAllowedCharacters(_levelConfig.allowedCharacters);

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

    private bool TryHandleSandboxMode(int selectedLevel)
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        if (!SandboxMode.IsActive)
            return false;

        // Sandbox runs without a config, but when the registry can supply one
        // use it so the sandbox catalog gets the level's allowed characters.
        // Quiet resolution only — no LoadLevelConfig, whose no-match path logs
        // an error that sandbox starts must not produce.
        if (_levelConfig == null && _levelConfigs != null)
        {
            int index = selectedLevel - 1;
            if (index >= 0 && index < _levelConfigs.Length && _levelConfigs[index] != null)
                _levelConfig = _levelConfigs[index];
        }

        SetCurrentAllowedCharacters(_levelConfig != null
            ? _levelConfig.allowedCharacters
            : null);

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

    /// <summary>
    /// SALIN-141. Same teardown as a defeat: stop spawning and return every live enemy
    /// to the pool before the scene unloads, so a restarted level cannot start with the
    /// aborted attempt's enemies still checked out.
    /// </summary>
    private void HandleLevelAttemptAborted() => HandleGameOver();

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

        if (_levelConfig.bossConfig != null)
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

            WaveDefinition wave = _levelConfig.waves[waveIndex];
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
        if (bossConfig.bossEnemyData == null
            || bossConfig.phases == null
            || bossConfig.phases.Count == 0)
        {
            DebugLogger.LogError("WaveManager: BossConfig is incomplete (missing bossEnemyData or phases). Aborting boss encounter.");
            AbortRun();
            yield break;
        }

        // Spawn the boss as a regular Enemy. No character assigned —
        // BossController.TryRouteDraw replaces character matching.
        // Boss spawns at the horizontal center of the spawn bounds rather
        // than a random X, so it visually anchors the encounter.
        Enemy bossEnemy = _spawner.SpawnBossEnemy(bossConfig.bossEnemyData);
        if (bossEnemy == null)
        {
            DebugLogger.LogError("WaveManager: Failed to spawn boss. Aborting boss encounter.");
            AbortRun();
            yield break;
        }

        BossController boss = bossEnemy.GetComponent<BossController>();
        if (boss == null)
        {
            DebugLogger.LogError("WaveManager: Boss prefab is missing BossController. Aborting boss encounter.");
            AbortRun();
            yield break;
        }

        boss.StartBoss(bossConfig, _spawner);

        // Wait for the boss to be defeated (Outro complete) — boss raises
        // OnLevelComplete itself.
        yield return new WaitUntil(() => !CanContinueRun() || boss.IsDefeated);

        if (!CanContinueRun())
        {
            AbortRun();
            yield break;
        }

        // BossController is the source of OnLevelComplete during boss
        // encounters. CompleteRun is intentionally NOT called here.
        _running = false;
        _waveRoutine = null;
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

        WaveDefinition savedWave = _levelConfig.waves[safeWaveIndex];
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
        // SALIN-178: defense systems report defense completion only. The level-flow
        // machine converts an accepted atomic save into OnLevelComplete. Scenes with
        // no running flow machine (sandbox, legacy tests) keep the direct raise.
        if (LevelFlowController.RoutesDefenseCompletion)
            EventBus.RaiseDefenseComplete();
        else
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
        AddEnemyForSandbox(enemies, _fallbackEnemyData);
        AddRuntimeSandboxEnemyData(enemies);
        AddEnemiesFromLevelForSandbox(enemies, _levelConfig);

        if (_levelConfigs != null)
        {
            foreach (LevelConfigSO levelConfig in _levelConfigs)
                AddEnemiesFromLevelForSandbox(enemies, levelConfig);
        }

        AddAllEnemyDataAssetsForSandbox(enemies);

        return enemies;
    }

    private void AddRuntimeSandboxEnemyData(List<EnemyDataSO> enemies)
    {
        if (_sandboxEnemyData == null)
            return;

        foreach (EnemyDataSO enemy in _sandboxEnemyData)
            AddEnemyForSandbox(enemies, enemy);
    }

    public IReadOnlyList<BaybayinCharacterSO> GetConfiguredCharactersForSandbox()
    {
        var characters = new List<BaybayinCharacterSO>();
        AddCharactersFromRegistryForSandbox(characters, _sandboxCharacterRegistry);
        AddCharactersFromLevelForSandbox(characters, _levelConfig);

        if (_levelConfigs != null)
        {
            foreach (LevelConfigSO levelConfig in _levelConfigs)
                AddCharactersFromLevelForSandbox(characters, levelConfig);
        }

        AddAllCharacterAssetsForSandbox(characters);

        if (SandboxMode.IsActive && characters.Count > 0)
            SetCurrentAllowedCharacters(characters);

        return characters;
    }

    private static void AddCharactersFromRegistryForSandbox(
        List<BaybayinCharacterSO> characters,
        CharacterRegistrySO registry)
    {
        if (registry?.All == null)
            return;

        foreach (BaybayinCharacterSO character in registry.All)
            AddCharacterForSandbox(characters, character);
    }

    private static void AddEnemiesFromLevelForSandbox(List<EnemyDataSO> enemies, LevelConfigSO levelConfig)
    {
        if (levelConfig == null)
            return;

        if (levelConfig.allowedEnemyTypes != null)
        {
            foreach (EnemyDataSO enemy in levelConfig.allowedEnemyTypes)
                AddEnemyForSandbox(enemies, enemy);
        }

        if (levelConfig.waves == null)
            return;

        foreach (WaveDefinition wave in levelConfig.waves)
        {
            if (wave?.enemyTypes == null)
                continue;

            foreach (EnemyDataSO enemy in wave.enemyTypes)
                AddEnemyForSandbox(enemies, enemy);
        }
    }

    private static void AddEnemyForSandbox(List<EnemyDataSO> enemies, EnemyDataSO enemy)
    {
        if (enemy != null && !enemies.Contains(enemy))
            enemies.Add(enemy);
    }

    private static void AddAllEnemyDataAssetsForSandbox(List<EnemyDataSO> enemies)
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:EnemyDataSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EnemyDataSO enemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            AddEnemyForSandbox(enemies, enemy);
        }
#endif
    }

    private static void AddCharactersFromLevelForSandbox(List<BaybayinCharacterSO> characters, LevelConfigSO levelConfig)
    {
        if (levelConfig == null)
            return;

        if (levelConfig.allowedCharacters != null)
        {
            foreach (BaybayinCharacterSO character in levelConfig.allowedCharacters)
                AddCharacterForSandbox(characters, character);
        }

        if (levelConfig.waves == null)
            return;

        foreach (WaveDefinition wave in levelConfig.waves)
        {
            if (wave?.characters == null)
                continue;

            foreach (BaybayinCharacterSO character in wave.characters)
                AddCharacterForSandbox(characters, character);
        }
    }

    private static void AddCharacterForSandbox(List<BaybayinCharacterSO> characters, BaybayinCharacterSO character)
    {
        if (character != null && !characters.Contains(character))
            characters.Add(character);
    }

    private static void AddAllCharacterAssetsForSandbox(List<BaybayinCharacterSO> characters)
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:BaybayinCharacterSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            BaybayinCharacterSO character = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(path);
            AddCharacterForSandbox(characters, character);
        }
#endif
    }
#endif

    private void LoadLevelConfig(int levelNumber)
    {
        if (SaveManager.Instance != null && SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
            ProgressManager.Instance != null && ProgressManager.Instance.TryGetSelectedLevel(out LevelConfigSO revisedLevel))
        {
            _levelConfig = revisedLevel;
            DebugLogger.Log($"WaveManager: Loaded revised level {_levelConfig.stableId}.");
            return;
        }

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

    private void SetCurrentAllowedCharacters(IReadOnlyList<BaybayinCharacterSO> source)
    {
        if (_currentAllowedCharactersOwner != this)
            _currentAllowedCharactersOwner = this;

        CurrentAllowedCharacters = CloneCharacters(source);
    }

    private static IReadOnlyList<BaybayinCharacterSO> CloneCharacters(IReadOnlyList<BaybayinCharacterSO> source)
    {
        if (source == null || source.Count == 0)
            return null;

        var clone = new List<BaybayinCharacterSO>(source.Count);
        for (int i = 0; i < source.Count; i++)
            clone.Add(source[i]);

        return new ReadOnlyCollection<BaybayinCharacterSO>(clone);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_fallbackEnemyData == null)
            Debug.LogWarning("WaveManager is missing _fallbackEnemyData.", this);

        if (_levelConfigs == null)
            return;

        var seenLevelNumbers = new HashSet<int>();
        for (int i = 0; i < _levelConfigs.Length; i++)
        {
            LevelConfigSO level = _levelConfigs[i];
            if (level == null)
            {
                Debug.LogError($"WaveManager has a missing LevelConfigSO reference at _levelConfigs[{i}].", this);
                continue;
            }

            if (!seenLevelNumbers.Add(level.levelNumber))
                Debug.LogError($"WaveManager has duplicate levelNumber {level.levelNumber} in _levelConfigs.", this);

            if (level.waves == null)
                continue;

            for (int waveIndex = 0; waveIndex < level.waves.Count; waveIndex++)
            {
                WaveDefinition wave = level.waves[waveIndex];
                if (wave == null)
                {
                    Debug.LogError(
                        $"WaveManager level '{level.name}' has a missing WaveDefinition at waves[{waveIndex}].",
                        level);
                    continue;
                }

                ValidateWaveRefs(level, wave, waveIndex);
            }
        }
    }

    private static void ValidateWaveRefs(LevelConfigSO level, WaveDefinition wave, int waveIndex)
    {
        if (wave.enemyTypes != null)
        {
            for (int i = 0; i < wave.enemyTypes.Count; i++)
            {
                if (wave.enemyTypes[i] == null)
                {
                    Debug.LogError(
                        $"Level '{level.name}' waves[{waveIndex}] has a missing enemyTypes[{i}] reference.",
                        level);
                }
            }
        }

        if (wave.characters != null)
        {
            for (int i = 0; i < wave.characters.Count; i++)
            {
                if (wave.characters[i] == null)
                {
                    Debug.LogError(
                        $"Level '{level.name}' waves[{waveIndex}] has a missing characters[{i}] reference.",
                        level);
                }
            }
        }
    }
#endif
}
