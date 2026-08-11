using System.Collections;
using System.Collections.Generic;
using Salinlahi.Runtime.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the full level lifecycle in the Gameplay scene:
/// [cutscene (before)] → intro dialogue → BGM → WaveManager → outro dialogue → [cutscene (after)] → Victory/Defeat routing.
/// EventBus signals drive lifecycle changes; this controller owns terminal screen routing.
/// </summary>
public class LevelFlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private DialogueController _dialogueController;
    [SerializeField] private Level1OnboardingController _level1OnboardingController;
    [SerializeField] private ChallengeFlowController _challengeFlowController;
    [SerializeField] private CharacterUnlockRevealController _revealController;
    [SerializeField] private BossTutorialController _bossTutorialController;
    [SerializeField] private VictoryScreenUI _victoryScreen;
    [SerializeField] private DefeatScreenUI _defeatScreen;

    [Header("Cutscene")]
    [SerializeField] private CutscenePlayer _cutscenePlayer;
    [Tooltip("Maps level numbers to cutscenes. Null = no cutscenes for any level.")]
    [SerializeField] private LevelCutsceneMappingSO _levelCutsceneMapping;

    [Header("Level Config")]
    [Tooltip("Resolved at runtime from GameManager.CurrentLevel or Inspector fallback.")]
    [SerializeField] private LevelConfigSO _levelConfig;
    [Tooltip("Opt-in switch for the generalized challenge prototype. Legacy onboarding remains the fallback when disabled.")]
    [SerializeField] private bool _challengePrototypeEnabled;

    private enum RevealTiming { BeforeTutorial, AfterTutorial }

    [Header("Character Unlock Reveal")]
    [Tooltip("Whether the 'New Character Unlocked!' reveal plays before or after the tutorial. " +
             "Global; non-tutorial levels play it at level start regardless.")]
    [SerializeField] private RevealTiming _revealTiming = RevealTiming.AfterTutorial;

    private bool _levelEnded;
    private bool _waitingForDialogue;
    private bool _waitingForCutscene;
    private bool _flowAborted;
    private bool _runtimeBootstrapped;

    public static bool TryStartRuntimeTutorialFlow(
        LevelConfigSO levelConfig,
        WaveManager waveManager,
        WaveSpawner waveSpawner,
        EnemyDataSO fallbackEnemyData)
    {
        if (levelConfig == null
            || !LevelTutorialProgress.ShouldShowForLevelNumber(levelConfig.levelNumber)
            || (levelConfig.tutorialSequence == null && levelConfig.onboardingSequence == null && levelConfig.challengeSequence == null)
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
        EventBus.OnCutsceneComplete += HandleCutsceneComplete;
    }

    private void OnDisable()
    {
        EventBus.OnLevelComplete -= HandleLevelComplete;
        EventBus.OnGameOver -= HandleGameOver;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        EventBus.OnDialogueComplete -= HandleDialogueComplete;
        EventBus.OnCutsceneComplete -= HandleCutsceneComplete;
    }

    private IEnumerator Start()
    {
        if (_runtimeBootstrapped)
            yield break;
        if (!IsGameplayScene())
            yield break;

        ResolveLevelConfig();
        EnsureRuntimeReferences(null, null);
        yield return RunLevelFlow();
    }

    private static bool IsGameplayScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == "Gameplay" || sceneName == "Level_01_Tutorial";
    }

    private IEnumerator RunLevelFlow()
    {
        if (_levelConfig == null)
        {
            DebugLogger.LogError("LevelFlowController: No LevelConfigSO resolved. Aborting flow.");
            yield break;
        }

        // Spawn protagonist if level has one configured
        if (_levelConfig.hasProtagonist)
        {
            ProtagonistManager protagonistManager = EnsureProtagonistManager();
            if (protagonistManager != null)
            {
                Vector3 protagonistPos = protagonistManager.CalculateProtagonistPosition();
                protagonistManager.EnsureProtagonist(protagonistPos, spawnBelowScreen: _levelConfig.protagonistWalksIn);
            }
            else
            {
                DebugLogger.LogError("[LevelFlowController] ProtagonistManager.Instance is NULL! Is the ProtagonistManager prefab in the scene?");
            }
        }

        // AC-0: Play "before level" cutscene if mapped
        CutsceneSO beforeCutscene = ResolveCutscene(CutsceneTriggerType.BeforeLevel);
        if (beforeCutscene != null && _cutscenePlayer != null)
        {
            _waitingForCutscene = true;
            bool playExitTransition = _levelConfig.levelNumber == 1;
            _cutscenePlayer.Play(beforeCutscene, playExitTransition);
            yield return new WaitUntil(() => !_waitingForCutscene);

            if (_levelEnded)
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

        if (_revealTiming == RevealTiming.BeforeTutorial)
        {
            yield return PlayRevealsIfAny();
            if (_flowAborted || _levelEnded) yield break;
        }

        if (_challengePrototypeEnabled && _levelConfig.challengeSequence != null)
            yield return PlayChallengeIfConfigured();
        else
            yield return PlayLevelTutorialIfNeeded();

        if (_flowAborted || _levelEnded)
            yield break;

        if (_revealTiming == RevealTiming.AfterTutorial)
        {
            yield return PlayRevealsIfAny();
            if (_flowAborted || _levelEnded) yield break;
        }

        yield return PlayBossTutorialIfNeeded();

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

    private void EnsureRuntimeReferences(WaveSpawner waveSpawner, EnemyDataSO fallbackEnemyData)
    {
        _waveManager ??= FindFirstObjectByType<WaveManager>();
        _dialogueController ??= FindActiveDialogueController();
        _cutscenePlayer ??= FindFirstObjectByType<CutscenePlayer>();
        _victoryScreen ??= FindFirstObjectByType<VictoryScreenUI>(FindObjectsInactive.Include);
        _defeatScreen ??= FindFirstObjectByType<DefeatScreenUI>(FindObjectsInactive.Include);
        _revealController ??= FindFirstObjectByType<CharacterUnlockRevealController>(FindObjectsInactive.Include);
        _bossTutorialController ??= FindFirstObjectByType<BossTutorialController>(FindObjectsInactive.Include);

        if (_challengePrototypeEnabled && _challengeFlowController == null)
        {
            _challengeFlowController = FindFirstObjectByType<ChallengeFlowController>(FindObjectsInactive.Include);
            if (_challengeFlowController == null)
            {
                GameObject challengeObject = new GameObject("[Runtime] ChallengeFlowController");
                challengeObject.transform.SetParent(transform, false);
                _challengeFlowController = challengeObject.AddComponent<ChallengeFlowController>();
            }
        }

        if (_level1OnboardingController == null
            && _levelConfig != null
            && IsTutorialLevelWithSequence(_levelConfig))
        {
            _level1OnboardingController = FindFirstObjectByType<Level1OnboardingController>(FindObjectsInactive.Include);
            if (_level1OnboardingController == null && ShouldCreateRuntimeOnboardingController())
                _level1OnboardingController = CreateRuntimeOnboardingController();
        }
    }

    private bool ShouldCreateRuntimeOnboardingController()
    {
        return _levelConfig != null
            && LevelTutorialProgress.ShouldShowForLevelNumber(_levelConfig.levelNumber)
            && (_levelConfig.onboardingSequence != null || _levelConfig.tutorialSequence != null);
    }

    private Level1OnboardingController CreateRuntimeOnboardingController()
    {
        GameObject go = new("[Runtime] Level1OnboardingController");
        go.transform.SetParent(transform, false);

        if (_levelConfig != null && _levelConfig.levelNumber == LevelTutorialProgress.Level2TutorialLevelNumber)
        {
            go.AddComponent<ComboTeachBeat>();
            go.AddComponent<FocusModeTeachBeat>();
        }
        else
        {
            go.AddComponent<ProtagonistIntroBeat>();
            go.AddComponent<BaseIntroBeat>();
            go.AddComponent<SoloTeachBeat>();
            go.AddComponent<HeartLossDemoBeat>();
        }

        go.AddComponent<ReleaseBeat>();
        return go.AddComponent<Level1OnboardingController>();
    }

    private static bool IsTutorialLevelWithSequence(LevelConfigSO levelConfig)
    {
        return levelConfig != null
            && LevelTutorialProgress.ShouldShowForLevelNumber(levelConfig.levelNumber)
            && (levelConfig.onboardingSequence != null || levelConfig.tutorialSequence != null);
    }

    private static DialogueController FindActiveDialogueController()
    {
        DialogueController[] controllers = FindObjectsByType<DialogueController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            DialogueController controller = controllers[i];
            if (controller != null && controller.gameObject.activeInHierarchy)
                return controller;
        }

        return null;
    }

    private static ProtagonistManager EnsureProtagonistManager()
    {
        if (ProtagonistManager.Instance != null)
            return ProtagonistManager.Instance;

        ProtagonistManager existing = FindFirstObjectByType<ProtagonistManager>();
        if (existing != null)
            return existing;

        GameObject managerObject = new("[Manager] ProtagonistManager");
        ProtagonistManager manager = managerObject.AddComponent<ProtagonistManager>();

        if (managerObject.GetComponent<ProtagonistAttackController>() == null)
            managerObject.AddComponent<ProtagonistAttackController>();

        return manager;
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
        if (_levelConfig == null)
        {
            DebugLogger.LogError("LevelFlowController: _levelConfig is null. Cannot determine if tutorial is needed.");
            yield break;
        }

        bool isTutorialLevel = LevelTutorialProgress.ShouldShowForLevelNumber(_levelConfig.levelNumber);

        if (!isTutorialLevel)
            yield break;

        // Tutorial is due from this point on.
        if (_level1OnboardingController == null)
        {
            DebugLogger.LogError($"LevelFlowController: Level {_levelConfig.levelNumber} tutorial is due, but Level1OnboardingController is not in the scene. Run Salinlahi → Tutorial → 5. Wire Level Scene.");
            yield break;
        }

        if (!_level1OnboardingController.IsSequenceResolvable(_levelConfig))
        {
            DebugLogger.LogError($"LevelFlowController: Level {_levelConfig.levelNumber} tutorial is due, but no OnboardingSequenceSO is assigned. Set LevelConfig.onboardingSequence or the controller's Fallback Sequence field.");
            yield break;
        }

        yield return _level1OnboardingController.PlayIfNeeded(_levelConfig);
    }

    private IEnumerator PlayChallengeIfConfigured()
    {
        if (_levelConfig == null || _levelConfig.challengeSequence == null)
            yield break;
        if (_challengeFlowController == null)
        {
            DebugLogger.LogError("LevelFlowController: Challenge sequence is assigned but ChallengeFlowController is missing.");
            _flowAborted = true;
            yield break;
        }

        yield return _challengeFlowController.Play(_levelConfig.challengeSequence, _levelConfig.levelNumber);
        if (_challengeFlowController.Session == null || _challengeFlowController.Session.State != ChallengeSessionState.Completed)
            _flowAborted = true;
    }

    private IEnumerator PlayBossTutorialIfNeeded()
    {
        if (_levelConfig == null
            || _levelConfig.bossConfig == null
            || _levelConfig.bossConfig.tutorial == null)
            yield break;

        if (_bossTutorialController == null)
        {
            DebugLogger.LogWarning("LevelFlowController: Boss tutorial is assigned, but no BossTutorialController is in the scene — skipping.");
            yield break;
        }

        yield return _bossTutorialController.Play(_levelConfig.bossConfig);
    }

    private IEnumerator PlayRevealsIfAny()
    {
        if (_levelConfig == null || _revealController == null)
            yield break;

        List<BaybayinCharacterSO> queue = CharacterUnlockRevealController.BuildRevealQueue(
            _levelConfig.allowedCharacters, CharacterUnlockProgress.HasUnlocked);

        if (queue.Count == 0)
            yield break;

        yield return _revealController.Play(queue);
    }

    // AC-5: Level complete → outro dialogue → [cutscene (after)] → victory screen
    private void HandleLevelComplete()
    {
        if (_levelEnded) return;
        _levelEnded = true;

        StartCoroutine(PlayOutroThenVictory());
    }

    private IEnumerator PlayOutroThenVictory()
    {
        if (_levelConfig != null && _levelConfig.outroDialogue != null && _dialogueController != null)
        {
            _waitingForDialogue = true;
            _dialogueController.Play(_levelConfig.outroDialogue);
            yield return new WaitUntil(() => !_waitingForDialogue);
        }

        CutsceneSO afterCutscene = ResolveCutscene(CutsceneTriggerType.AfterLevel);
        if (afterCutscene != null && _cutscenePlayer != null)
        {
            _waitingForCutscene = true;
            _cutscenePlayer.Play(afterCutscene);
            yield return new WaitUntil(() => !_waitingForCutscene);
        }

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

    private void HandleCutsceneComplete()
    {
        _waitingForCutscene = false;
    }

    private CutsceneSO ResolveCutscene(CutsceneTriggerType trigger)
    {
        if (_levelCutsceneMapping == null || _levelConfig == null) return null;

        foreach (LevelCutsceneEntry entry in _levelCutsceneMapping.entries)
        {
            if (entry.levelNumber == _levelConfig.levelNumber && entry.triggerType == trigger)
                return entry.cutscene;
        }
        return null;
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
