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
    [SerializeField] private TutorialOverlayController _tutorialOverlayController;
    [SerializeField] private Level1WorldIntroController _level1WorldIntroController;
    [SerializeField] private VictoryScreenUI _victoryScreen;
    [SerializeField] private DefeatScreenUI _defeatScreen;

    [Header("Level Config")]
    [Tooltip("Resolved at runtime from GameManager.CurrentLevel or Inspector fallback.")]
    [SerializeField] private LevelConfigSO _levelConfig;

    private bool _levelEnded;
    private bool _waitingForDialogue;
    private bool _flowAborted;

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
        ResolveLevelConfig();

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

        if (_waveManager != null)
            _waveManager.StartLevel();
        else
            DebugLogger.LogError("LevelFlowController: WaveManager reference missing.");
    }

    private void ResolveLevelConfig()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentLevel != null)
        {
            _levelConfig = GameManager.Instance.CurrentLevel;
            return;
        }

        if (_levelConfig != null)
            return;

        DebugLogger.LogWarning("LevelFlowController: No level config found via GameManager or Inspector.");
    }

    private IEnumerator PlayLevelTutorialIfNeeded()
    {
        if (!LevelTutorialProgress.ShouldShowForLevel(_levelConfig))
            yield break;

        if (_level1WorldIntroController != null && _level1WorldIntroController.IsConfigured)
        {
            yield return _level1WorldIntroController.PlayIfNeeded(_levelConfig);
            yield break;
        }

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
