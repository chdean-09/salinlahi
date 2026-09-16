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

    [Header("Instant Win")]
    [Tooltip("Optional. The instant-win beat played when the last slot of the target text "
             + "fills. Left empty one is built at runtime with its own defaults, exactly as "
             + "the Wave Cleared screen and the content-missing panel are, so no scene has to "
             + "be re-authored to ship the beat.")]
    [SerializeField] private InstantWinPresenter _instantWinPresenter;

    // Restoration overflow: how many enemies per batch. The batch budget before giving up is a
    // per-level policy field (SpawnAssignmentPolicy.maxOverflowBatches), not a constant here.
    private const int OverflowBatchSize = 3;

    private int _currentWaveIndex;
    private int _currentWaveSpawnedCount;
    private bool _running;
    private Coroutine _waveRoutine;

    // The instant-win short-circuit. Once per RUN, not once per segment: a segmented level
    // re-enters Defense with the target text already whole, and re-firing the beat would
    // replay the freeze and the banner on a level the player has already been told they won.
    private bool _instantWinTaken;
    private Coroutine _instantWinRoutine;

    // Resolved lazily and cached: the presenter that owns focus-word restoration state. Read
    // rather than subscribed to because the moment a slot fills lives inside
    // ActiveCluePresenter.HandleActiveClueResolved, which raises no per-slot signal.
    private ActiveCluePresenter _restorationSource;

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

    /// <summary>
    /// SALIN-226. Runs one alternating segment's wave range, <c>[startWaveIndex,
    /// endWaveIndexExclusive)</c>, and completes the run at that bound so the flow machine
    /// advances out of Defense through the usual OnDefenseComplete.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT consult <see cref="TryRestorePausedRun"/>. That path exists for
    /// resuming a level the player left, and it rewinds to the saved wave index; consulting
    /// it here would send segment 2 back to a wave saved in a previous session. The
    /// leave-and-return restore still owns the segment-0 entry through StartLevel.
    ///
    /// CurrentWaveIndex / CurrentWaveSpawnedCount keep their absolute meaning into the flat
    /// wave list, so PauseMenuUI's snapshot keeps working untouched.
    /// </remarks>
    public void StartSegment(int startWaveIndex, int endWaveIndexExclusive)
    {
        if (_spawner != null)
            _spawner.SetFallbackEnemyDataIfMissing(_fallbackEnemyData);

        if (_levelConfig == null)
        {
            DebugLogger.LogError("WaveManager.StartSegment: No LevelConfigSO assigned.");
            return;
        }

        SetCurrentAllowedCharacters(_levelConfig.allowedCharacters);

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        if (_running || _waveRoutine != null)
        {
            if (_waveRoutine != null)
                StopCoroutine(_waveRoutine);

            ReturnAllActiveEnemies();
            ResetRunState();
        }

        _running = true;
        _currentWaveIndex = Mathf.Max(0, startWaveIndex);
        _currentWaveSpawnedCount = 0;
        _waveRoutine = StartCoroutine(
            RunAllWavesRoutine(startWaveIndex, 0, endWaveIndexExclusive));
    }

    private void StartLevel(int selectedLevel)
    {
        SetCurrentAllowedCharacters(null);

        // A fresh attempt re-arms the instant win. Deliberately NOT done in StartSegment: the
        // beat is once per run, and a segmented level re-entering Defense with the text already
        // whole must not replay it. Re-resolving the restoration source with it keeps a
        // reloaded scene from holding the previous attempt's presenter.
        _instantWinTaken = false;
        _restorationSource = null;

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

        // A defeat or an abort underneath the instant-win beat has to take the beat down with
        // it, or the dipped timeScale outlives the level and the defeat screen crawls.
        if (_instantWinRoutine != null)
        {
            StopCoroutine(_instantWinRoutine);
            _instantWinRoutine = null;
        }
        _instantWinPresenter?.Cancel();

        ReturnAllActiveEnemies();
        _waveRoutine = null;
    }

    /// <summary>
    /// SALIN-141. Same teardown as a defeat: stop spawning and return every live enemy
    /// to the pool before the scene unloads, so a restarted level cannot start with the
    /// aborted attempt's enemies still checked out.
    /// </summary>
    private void HandleLevelAttemptAborted() => HandleGameOver();

    /// <summary>
    /// THE WIN RULE. Filling every slot in the target text wins the level, and it ends the
    /// instant the last slot fills — with enemies still alive on screen. Surviving a wave is
    /// not a win condition.
    ///
    /// WHY THIS IS A PER-FRAME READ AND NOT AN EVENT SUBSCRIPTION. The moment a slot fills is
    /// ActiveCluePresenter.HandleActiveClueResolved, which mutates its ActiveClueRestorationState
    /// and raises nothing per slot. Reading the state every frame reaches the same conclusion in
    /// the same frame as an event would, and it needs no change to the presenter — which is what
    /// keeps the win rule out of the HUD layer.
    ///
    /// WHY IT IS IN Update AND NOT IN THE WAVE COROUTINE. RunAllWavesRoutine is a sequential
    /// chain of waits: a check inside it can only run when whatever it is waiting on releases,
    /// so a slot that fills during a spawn interval, an inter-wave delay or a WaitUntil for the
    /// board to clear would not be noticed until that wait ended — which is precisely the gap
    /// this exists to close. Update sees it wherever in the run it happens.
    /// </summary>
    private void Update()
    {
        if (!_running || _instantWinTaken || _instantWinRoutine != null)
            return;

        if (!IsTargetTextRestored())
            return;

        BeginInstantWin();
    }

    /// <summary>
    /// True once every authored slot of every focus word has been restored.
    ///
    /// Levels that do not opt into combat restoration never reach the state at all —
    /// ActiveCluePresenter only applies a restored symbol when activeClueRestorationEnabled is
    /// set — so the check is gated on the config rather than relying on an empty state reading
    /// as incomplete. That keeps legacy levels, boss levels and the sandbox on exactly the
    /// behaviour they have today.
    /// </summary>
    private bool IsTargetTextRestored()
    {
        if (_levelConfig == null || !_levelConfig.activeClueRestorationEnabled)
            return false;

        // Boss levels are excluded, for the same reason LevelFlowController.HandleDefenseComplete
        // excludes them from the Wave Cleared gate: BossController, not this component, owns
        // when a boss encounter ends, and completing the run out from under it would finish the
        // level through a path that never reports the boss defeated.
        if (_levelConfig.bossConfig != null)
            return false;

        if (_restorationSource == null)
        {
            _restorationSource = FindFirstObjectByType<ActiveCluePresenter>(
                FindObjectsInactive.Include);
            if (_restorationSource == null)
                return false;
        }

        // IsComplete is false for a level with no focus words, so an unauthored level cannot
        // win itself on an empty target text.
        return _restorationSource.RestorationState.IsComplete;
    }

    /// <summary>
    /// Ends the defense where it stands and hands the moment to the instant-win beat.
    ///
    /// StopAllCoroutines, not StopCoroutine(_waveRoutine): the per-wave spawn loop runs as
    /// StartCoroutine(_spawner.SpawnWave(...)) and is therefore a SEPARATE coroutine owned by
    /// this component, not a child of the wave routine. Stopping only the wave routine leaves
    /// it spawning, so enemies would keep walking on during the frozen hold of a level that
    /// is already over. Nothing else on this component runs a coroutine that must survive
    /// this point — the run is finished either way.
    /// </summary>
    private void BeginInstantWin()
    {
        _instantWinTaken = true;
        StopAllCoroutines();
        _waveRoutine = null;

        // The one signal the rest of the game gets for "the target text is whole". Raised
        // before the beat plays, so a listener that wants to change what it presents during
        // the beat is told in time.
        EventBus.RaiseFocusWordRestorationComplete();

        _instantWinRoutine = StartCoroutine(RunInstantWinRoutine());
    }

    private IEnumerator RunInstantWinRoutine()
    {
        InstantWinPresenter presenter = ResolveInstantWinPresenter();
        if (presenter != null)
        {
            int levelNumber = _levelConfig != null ? _levelConfig.levelNumber : 1;
            yield return presenter.Play(_restorationSource, levelNumber);
        }
        else
        {
            DebugLogger.LogWarning(
                "WaveManager: no instant-win presenter could be resolved. Completing the run "
                + "without the beat rather than stranding a level the player has won.");
        }

        _instantWinRoutine = null;

        if (!CanContinueRun())
        {
            AbortRun();
            yield break;
        }

        // The SAME completion path a full clear uses: CompleteRun -> RaiseLevelCompleted ->
        // OnDefenseComplete. The flow machine, the atomic save and the victory screen are
        // reached exactly as they are on a wave clear, and none of them need to know that
        // this run ended early.
        CompleteRun();
    }

    /// <summary>
    /// Finds the scene-authored instant-win presenter, or builds one. Mirrors
    /// LevelFlowController.ShowWaveClearedScreen / ShowContentMissingPanel: the surface is
    /// unwired in every scene, and a [SerializeField] requirement would force an edit to
    /// Assets/_Scenes/*.unity, the project's highest-collision serialized assets.
    /// </summary>
    private InstantWinPresenter ResolveInstantWinPresenter()
    {
        if (_instantWinPresenter == null)
        {
            _instantWinPresenter = FindFirstObjectByType<InstantWinPresenter>(
                FindObjectsInactive.Include);
        }

        if (_instantWinPresenter == null)
        {
            GameObject presenterObject = new GameObject("[Runtime] InstantWinPresenter");
            _instantWinPresenter = presenterObject.AddComponent<InstantWinPresenter>();
        }

        return _instantWinPresenter;
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

    /// <param name="endWaveIndexExclusive">
    /// SALIN-226. Upper bound of the half-open wave range to run. Negative (the default)
    /// means "to the end of the list", which is what every pre-existing caller passes, so
    /// the full-level and snapshot-restore paths are behaviourally unchanged.
    /// </param>
    private IEnumerator RunAllWavesRoutine(
        int startWaveIndex, int firstWaveSpawnOffset, int endWaveIndexExclusive = -1)
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

        // SALIN-226. Reaching this bound ends the run exactly as list exhaustion does, so
        // CompleteRun -> RaiseLevelCompleted raises the SAME OnDefenseComplete the flow
        // machine already listens for. A segment deliberately adds no second signal.
        int lastWaveIndexExclusive = endWaveIndexExclusive < 0
            ? _levelConfig.waves.Count
            : Mathf.Clamp(endWaveIndexExclusive, 0, _levelConfig.waves.Count);

        for (int waveIndex = firstWaveIndex; waveIndex < lastWaveIndexExclusive; waveIndex++)
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

            // The finale gate opens as the LAST wave starts, so a level that withheld its final
            // slot becomes completable exactly here and not before. Resolved against the same
            // exclusive bound the overflow pass uses, so a segmented run gates per segment rather
            // than once per level.
            if (IsFinalWaveIndex(waveIndex, lastWaveIndexExclusive))
            {
                SpawnAssignmentCoordinator gateCoordinator =
                    FindFirstObjectByType<SpawnAssignmentCoordinator>(FindObjectsInactive.Include);
                if (gateCoordinator != null)
                    gateCoordinator.OpenGate(SpawnGateRegistry.FinalWaveReached);
            }

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

        // The words, not the wave list, decide when the defense is over.
        yield return RunRestorationOverflow(lastWaveIndexExclusive);

        if (!CanContinueRun())
        {
            AbortRun();
            yield break;
        }

        CompleteRun();
    }

    /// <summary>
    /// True when this wave index is the last of the run. Pure and static so the finale gate's timing
    /// is an EditMode test rather than something only a full play session can exercise: an inline
    /// comparison here could be silently wrong and no suite would notice.
    /// </summary>
    internal static bool IsFinalWaveIndex(int waveIndex, int endWaveIndexExclusive) =>
        endWaveIndexExclusive > 0 && waveIndex == endWaveIndexExclusive - 1;

    /// <summary>
    /// True while <see cref="RunRestorationOverflow"/>'s loop should spawn another batch. Pure and
    /// static for the same reason as <see cref="IsFinalWaveIndex"/>: not because the expression is
    /// complex, but so an off-by-one on the bound (<c>&lt;=</c> instead of <c>&lt;</c>) or a flipped
    /// operator (<c>&amp;&amp;</c> instead of <c>||</c>) is an EditMode test failure instead of a
    /// silent behaviour change only a full play session would surface.
    /// </summary>
    internal static bool ShouldContinueOverflow(int batch, bool unbounded, int maxBatches) =>
        unbounded || batch < maxBatches;

    /// <summary>
    /// Keeps the defense running past the authored wave budget until the level's focus words are
    /// finished.
    ///
    /// The authored enemyCount is a pacing target, not a supply guarantee: a player who misses one
    /// needed carrier exhausts it with a slot still empty. Before this, that run hit
    /// LevelFlowController.RefuseCompletionForMissingContent ("combat ended before restoring focus
    /// word slots") and dead-ended - the wave budget ran out, not the player's skill.
    ///
    /// Reuses the final wave's roster and cadence through the ordinary SpawnWave path, so spawn
    /// spread, the level speed multiplier and the assignment schedule all still apply. The
    /// schedule's starvation timers are what actually terminate this loop: they force the needed
    /// symbol once the player has been without it long enough.
    /// </summary>
    private IEnumerator RunRestorationOverflow(int lastWaveIndexExclusive)
    {
        SpawnAssignmentCoordinator coordinator = FindFirstObjectByType<SpawnAssignmentCoordinator>(
            FindObjectsInactive.Include);

        if (coordinator == null || !coordinator.WantsOverflow)
            yield break;

        WaveDefinition template = FindOverflowTemplate(lastWaveIndexExclusive);
        if (template == null)
        {
            DebugLogger.LogWarning(
                "WaveManager: restoration overflow needed but no wave is available to draw a "
                + "roster from. The level will refuse completion instead.");
            yield break;
        }

        SpawnAssignmentPolicy overflowPolicy =
            _levelConfig != null ? _levelConfig.spawnAssignmentPolicy : null;
        bool unbounded = overflowPolicy != null && overflowPolicy.OverflowIsUnbounded;
        int maxBatches = overflowPolicy != null ? overflowPolicy.maxOverflowBatches : 12;

        // Bounded by default so a broken gate or an unrestorable target cannot spin forever;
        // unbounded when a level's policy asks for escorts to keep coming. Both still exit on
        // CanContinueRun() and on WantsOverflow above, so an unbounded run still ends when the run
        // is won or lost - it just never gives up on its own.
        for (int batch = 0; ShouldContinueOverflow(batch, unbounded, maxBatches); batch++)
        {
            if (!CanContinueRun() || !coordinator.WantsOverflow)
                yield break;

            if (!ValidateRunDependencies())
                yield break;

            WaveDefinition overflowWave = BuildOverflowWave(template);
            yield return StartCoroutine(_spawner.SpawnWave(overflowWave, HandleEnemySpawned));

            if (!CanContinueRun() || !coordinator.WantsOverflow)
                yield break;

            yield return WaitForActiveEnemiesCleared();
        }

        // Only reachable for a bounded level whose budget ran out - an unbounded level's loop
        // condition never goes false, so it only ever leaves through a yield break above.
        DebugLogger.LogWarning(
            $"WaveManager: restoration overflow ran {maxBatches} batches (this level's "
            + "spawnAssignmentPolicy.maxOverflowBatches budget) without finishing the focus words. "
            + "Check that every gated slot has something calling OpenGate.");
    }

    /// <summary>Last non-intermission wave, whose roster and cadence the overflow reuses.</summary>
    private WaveDefinition FindOverflowTemplate(int lastWaveIndexExclusive)
    {
        if (_levelConfig?.waves == null)
            return null;

        int last = Mathf.Min(lastWaveIndexExclusive, _levelConfig.waves.Count) - 1;
        for (int i = last; i >= 0; i--)
        {
            WaveDefinition wave = _levelConfig.waves[i];
            if (wave != null && !wave.isIntermissionWave && wave.enemyCount > 0)
                return wave;
        }

        return null;
    }

    private static WaveDefinition BuildOverflowWave(WaveDefinition template)
    {
        return new WaveDefinition
        {
            isIntermissionWave = false,
            characters = template.characters,
            enemyTypes = template.enemyTypes,
            enemyCount = OverflowBatchSize,
            spawnInterval = template.spawnInterval,
            waveStartDelay = 0f,
        };
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
