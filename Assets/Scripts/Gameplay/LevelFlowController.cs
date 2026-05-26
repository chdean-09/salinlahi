using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the full level lifecycle in the Gameplay scene:
/// intro dialogue → BGM → WaveManager → outro dialogue → Victory/Defeat routing.
/// EventBus signals drive lifecycle changes; this controller owns terminal screen routing.
/// </summary>
public class LevelFlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private DialogueController _dialogueController;
    [SerializeField] private Level1InteractiveTutorialController _level1InteractiveTutorialController;
    [SerializeField] private TutorialOverlayController _tutorialOverlayController;
    [SerializeField] private VictoryScreenUI _victoryScreen;
    [SerializeField] private DefeatScreenUI _defeatScreen;

    [Header("Level Config")]
    [Tooltip("Resolved at runtime from GameManager.CurrentLevel or Inspector fallback.")]
    [SerializeField] private LevelConfigSO _levelConfig;

    private bool _levelEnded;
    private bool _waitingForDialogue;
    private bool _flowAborted;
    private bool _runtimeBootstrapped;

    public static bool TryStartRuntimeTutorialFlow(
        LevelConfigSO levelConfig,
        WaveManager waveManager,
        WaveSpawner waveSpawner,
        EnemyDataSO fallbackEnemyData)
    {
        if (levelConfig == null
            || levelConfig.levelNumber != LevelTutorialProgress.TutorialLevelNumber
            || levelConfig.tutorialSequence == null
            || waveManager == null)
        {
            return false;
        }

        LevelFlowController existing = FindFirstObjectByType<LevelFlowController>();
        if (existing != null)
            return true;

        GameObject go = new("[Runtime] LevelFlowController");
        LevelFlowController controller = go.AddComponent<LevelFlowController>();
        controller.BootstrapRuntimeFlow(levelConfig, waveManager, waveSpawner, fallbackEnemyData);
        return true;
    }

    private void BootstrapRuntimeFlow(
        LevelConfigSO levelConfig,
        WaveManager waveManager,
        WaveSpawner waveSpawner,
        EnemyDataSO fallbackEnemyData)
    {
        _runtimeBootstrapped = true;
        _levelConfig = levelConfig;
        _waveManager = waveManager;
        EnsureRuntimeReferences(waveSpawner, fallbackEnemyData);
        StartCoroutine(RunLevelFlow());
    }

    private void OnEnable()
    {
        EventBus.OnLevelComplete += HandleLevelComplete;
        EventBus.OnGameOver += HandleGameOver;
        EventBus.OnBossDefeated += HandleBossDefeated;
        EventBus.OnDialogueComplete += HandleDialogueComplete;
    }

    private void OnDisable()
    {
        EventBus.OnLevelComplete -= HandleLevelComplete;
        EventBus.OnGameOver -= HandleGameOver;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        EventBus.OnDialogueComplete -= HandleDialogueComplete;
    }

    private IEnumerator Start()
    {
        if (_runtimeBootstrapped)
            yield break;

        ResolveLevelConfig();
        EnsureRuntimeReferences(null, null);
        yield return RunLevelFlow();
    }

    private IEnumerator RunLevelFlow()
    {
        if (_levelConfig == null)
        {
            DebugLogger.LogError("LevelFlowController: No LevelConfigSO resolved. Aborting flow.");
            yield break;
        }

        // AC-1: Play intro dialogue before starting waves
        if (_levelConfig.introDialogue != null && _dialogueController != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                GameManager.Instance.StartGame();

            _waitingForDialogue = true;
            _dialogueController.Play(_levelConfig.introDialogue);
            yield return new WaitUntil(() => !_waitingForDialogue);

            if (_levelEnded)
                yield break;
        }

        if (_levelEnded)
            yield break;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        yield return PlayLevelTutorialIfNeeded();

        if (_flowAborted || _levelEnded)
            yield break;

        // AC-2: Start BGM from level config
        if (_levelConfig.bgmClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(_levelConfig.bgmClip);

        if (_levelEnded)
            yield break;

        // AC-3: Start waves — no isBossLevel branching; WaveManager handles it internally
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        if (_levelConfig.levelNumber == LevelTutorialProgress.TutorialLevelNumber)
            Level1InteractiveTutorialController.ForceGameplayHudVisible();

        if (_waveManager != null)
            _waveManager.StartLevel();
        else
            DebugLogger.LogError("LevelFlowController: WaveManager reference missing.");
    }

    private void EnsureRuntimeReferences(WaveSpawner waveSpawner, EnemyDataSO fallbackEnemyData)
    {
        _waveManager ??= FindFirstObjectByType<WaveManager>();
        _dialogueController ??= FindFirstObjectByType<DialogueController>();
        _victoryScreen ??= FindFirstObjectByType<VictoryScreenUI>();
        _defeatScreen ??= FindFirstObjectByType<DefeatScreenUI>();
        _tutorialOverlayController ??= FindFirstObjectByType<TutorialOverlayController>();

        if (_level1InteractiveTutorialController == null
            && _levelConfig != null
            && _levelConfig.tutorialSequence != null
            && _levelConfig.levelNumber == LevelTutorialProgress.TutorialLevelNumber)
        {
            _level1InteractiveTutorialController = FindFirstObjectByType<Level1InteractiveTutorialController>();
            if (_level1InteractiveTutorialController == null)
            {
                GameObject tutorialObject = new("Level1InteractiveTutorialController");
                _level1InteractiveTutorialController = tutorialObject.AddComponent<Level1InteractiveTutorialController>();
            }
        }

        if (_level1InteractiveTutorialController != null && _levelConfig != null)
            _level1InteractiveTutorialController.ConfigureForLevel(_levelConfig, waveSpawner, fallbackEnemyData);
    }

    private void ResolveLevelConfig()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentLevel != null)
        {
            _levelConfig = GameManager.Instance.CurrentLevel;
            return;
        }

        if (_levelConfig != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentLevel != _levelConfig)
                GameManager.Instance.SetLevel(_levelConfig);

            return;
        }

        DebugLogger.LogWarning("LevelFlowController: No level config found via GameManager or Inspector.");
    }

    private IEnumerator PlayLevelTutorialIfNeeded()
    {
        DebugLogger.Log("LevelFlowController: PlayLevelTutorialIfNeeded() started.");
        
        if (_levelConfig == null)
        {
            DebugLogger.LogError("LevelFlowController: _levelConfig is null. Cannot determine if tutorial is needed.");
            yield break;
        }
        
        DebugLogger.Log($"LevelFlowController: _levelConfig.levelNumber = {_levelConfig.levelNumber}");

        if (_level1InteractiveTutorialController != null
            && _level1InteractiveTutorialController.ShouldRunFor(_levelConfig))
        {
            DebugLogger.Log("LevelFlowController: Interactive tutorial controller.ShouldRunFor returned true.");
            
            if (!_level1InteractiveTutorialController.IsConfigured)
            {
                _flowAborted = true;
                DebugLogger.LogError("LevelFlowController: Level 1 interactive tutorial is due, but it is not configured (no steps).");
                yield break;
            }

            DebugLogger.Log("LevelFlowController: Starting interactive tutorial...");
            yield return _level1InteractiveTutorialController.PlayIfNeeded(_levelConfig);
            DebugLogger.Log("LevelFlowController: Interactive tutorial completed.");
            yield break;
        }

        if (!LevelTutorialProgress.ShouldShowForLevel(_levelConfig))
        {
            DebugLogger.Log($"LevelFlowController: ShouldShowForLevel returned false. levelNumber={_levelConfig.levelNumber}, HasSeen={LevelTutorialProgress.HasSeenLevel1Tutorial()}");
            yield break;
        }
        
        if (_level1InteractiveTutorialController == null)
            DebugLogger.Log("LevelFlowController: _level1InteractiveTutorialController is null. Falling back to overlay.");
        else
            DebugLogger.Log("LevelFlowController: _level1InteractiveTutorialController.ShouldRunFor returned false. Falling back to overlay.");

        if (_tutorialOverlayController == null)
        {
            _flowAborted = true;
            DebugLogger.LogError("LevelFlowController: Level 1 FTUE is due, but TutorialOverlayController is not assigned.");
            yield break;
        }

        if (!_tutorialOverlayController.IsConfigured)
        {
            _flowAborted = true;
            DebugLogger.LogError("LevelFlowController: Level 1 FTUE is due, but TutorialOverlayController is not fully configured.");
            yield break;
        }

        DebugLogger.Log("LevelFlowController: Starting overlay tutorial...");
        yield return _tutorialOverlayController.PlayIfNeeded(_levelConfig);
    }

    // AC-5: Level complete → outro dialogue → victory screen
    private void HandleLevelComplete()
    {
        if (_levelEnded) return;
        _levelEnded = true;

        if (_levelConfig != null && _levelConfig.outroDialogue != null && _dialogueController != null)
            StartCoroutine(PlayOutroThenVictory());
        else
            ShowVictoryScreen();
    }

    private IEnumerator PlayOutroThenVictory()
    {
        _waitingForDialogue = true;
        _dialogueController.Play(_levelConfig.outroDialogue);
        yield return new WaitUntil(() => !_waitingForDialogue);

        ShowVictoryScreen();
    }

    // AC-4: Game over → defeat screen directly (no outro)
    private void HandleGameOver()
    {
        if (_levelEnded) return;
        _levelEnded = true;
        ShowDefeatScreen();
    }

    // AC-7: Boss-specific hooks (chapter-complete dialogue can be added here)
    private void HandleBossDefeated()
    {
        // Reserved for future boss-specific chapter hooks. Current boss flow completes via OnLevelComplete.
    }

    private void HandleDialogueComplete()
    {
        _waitingForDialogue = false;
    }

    private void ShowVictoryScreen()
    {
        if (_victoryScreen != null)
            _victoryScreen.Show();
    }

    private void ShowDefeatScreen()
    {
        if (_defeatScreen != null)
            _defeatScreen.Show();
    }
}
