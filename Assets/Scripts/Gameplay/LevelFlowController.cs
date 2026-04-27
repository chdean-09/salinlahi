using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the full level lifecycle in the Gameplay scene:
/// intro dialogue → BGM → WaveManager → outro dialogue → Victory/Defeat routing.
/// All transitions driven by EventBus events (SALIN-46 AC-6).
/// </summary>
public class LevelFlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private DialogueController _dialogueController;
    [SerializeField] private VictoryScreenUI _victoryScreen;

    [Header("Level Config")]
    [Tooltip("Resolved at runtime from GameManager.CurrentLevel or Inspector fallback.")]
    [SerializeField] private LevelConfigSO _levelConfig;

    private bool _levelEnded;
    private bool _waitingForDialogue;

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
        }

        // AC-2: Start BGM from level config
        if (_levelConfig.bgmClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(_levelConfig.bgmClip);

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

    // AC-5: Level complete → outro dialogue → victory screen
    private void HandleLevelComplete()
    {
        if (_levelEnded) return;
        _levelEnded = true;

        if (_levelConfig != null && _levelConfig.outroDialogue != null && _dialogueController != null)
            StartCoroutine(PlayOutroThenVictory());
        // No outro: VictoryScreenUI handles display via its own OnLevelComplete subscription
    }

    private IEnumerator PlayOutroThenVictory()
    {
        // DialogueController.Play() requires GameState.Playing
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();

        _waitingForDialogue = true;
        _dialogueController.Play(_levelConfig.outroDialogue);
        yield return new WaitUntil(() => !_waitingForDialogue);

        // Show victory screen directly to avoid re-triggering OnLevelComplete handlers
        if (_victoryScreen != null)
            _victoryScreen.Show();
    }

    // AC-4: Game over → defeat screen directly (no outro)
    private void HandleGameOver()
    {
        if (_levelEnded) return;
        _levelEnded = true;
        // DefeatScreenUI handles display via its own OnGameOver subscription
    }

    // AC-7: Boss-specific hooks (chapter-complete dialogue can be added here)
    private void HandleBossDefeated()
    {
        DebugLogger.Log("LevelFlowController: Boss defeated. Chapter hooks can be added here.");
    }

    private void HandleDialogueComplete()
    {
        _waitingForDialogue = false;
    }
}
