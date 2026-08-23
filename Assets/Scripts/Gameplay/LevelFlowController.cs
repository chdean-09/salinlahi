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
    [SerializeField] private CampaignOutcomeSaveFailurePanel _saveFailurePanel;

    [Header("Cutscene")]
    [SerializeField] private CutscenePlayer _cutscenePlayer;
    [Tooltip("Maps level numbers to cutscenes. Null = no cutscenes for any level.")]
    [SerializeField] private LevelCutsceneMappingSO _levelCutsceneMapping;

    [Header("Level Config")]
    [Tooltip("Resolved at runtime from GameManager.CurrentLevel or Inspector fallback.")]
    [SerializeField] private LevelConfigSO _levelConfig;
    [Tooltip("Legacy scene override for the generalized challenge prototype. Prefer LevelConfigSO.challengePrototypeEnabled for data-driven opt-in.")]
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
    private LevelFlowMachine _machine;

    // The controller currently driving a live LF-CONTRACT-v2 machine, if any.
    // WaveManager/BossController consult this to decide whether their completion
    // raises OnDefenseComplete (machine flow) or the legacy OnLevelComplete
    // (sandbox scenes and bare controllers with no running flow).
    private static LevelFlowController s_activeFlow;

    public static bool RoutesDefenseCompletion =>
        s_activeFlow != null
        && s_activeFlow._machine != null
        && !s_activeFlow._machine.IsTerminal;
    private CampaignOutcomeCommitResult _completionCommitResult;
    private ActiveClueDirector _activeClueDirector;
    private ActiveCluePresenter _activeCluePresenter;

    public static bool TryStartRuntimeTutorialFlow(
        LevelConfigSO levelConfig,
        WaveManager waveManager,
        WaveSpawner waveSpawner,
        EnemyDataSO fallbackEnemyData)
    {
        bool hasLegacyTutorial = levelConfig != null
            && (levelConfig.tutorialSequence != null || levelConfig.onboardingSequence != null);
        bool hasChallengePrototype = levelConfig != null
            && levelConfig.challengePrototypeEnabled
            && levelConfig.challengeSequence != null;
        if (levelConfig == null
            || !LevelTutorialProgress.ShouldShowForLevelNumber(levelConfig.levelNumber)
            || (!hasLegacyTutorial && !hasChallengePrototype)
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
        _challengePrototypeEnabled = _challengePrototypeEnabled
            || (levelConfig != null && levelConfig.challengePrototypeEnabled);
        EnsureRuntimeReferences(waveSpawner, fallbackEnemyData);
        StartCoroutine(RunLevelFlow());
    }

    private void OnEnable()
    {
        EventBus.OnLevelComplete += HandleLevelComplete;
        EventBus.OnDefenseComplete += HandleDefenseComplete;
        EventBus.OnGameOver += HandleGameOver;
        EventBus.OnBossDefeated += HandleBossDefeated;
        EventBus.OnDialogueComplete += HandleDialogueComplete;
        EventBus.OnCutsceneComplete += HandleCutsceneComplete;
        EventBus.OnGamePaused += HandleGamePaused;
        EventBus.OnGameResumed += HandleGameResumed;
    }

    private void OnDisable()
    {
        EventBus.OnLevelComplete -= HandleLevelComplete;
        EventBus.OnDefenseComplete -= HandleDefenseComplete;
        EventBus.OnGameOver -= HandleGameOver;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        EventBus.OnDialogueComplete -= HandleDialogueComplete;
        EventBus.OnCutsceneComplete -= HandleCutsceneComplete;
        EventBus.OnGamePaused -= HandleGamePaused;
        EventBus.OnGameResumed -= HandleGameResumed;

        if (s_activeFlow == this)
            s_activeFlow = null;
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

    /// <summary>
    /// Drives the LF-CONTRACT-v2 machine: one executor coroutine per planned phase,
    /// in machine order. Executors either report their own completion (Defense via
    /// OnDefenseComplete, AtomicSave via ReportSaveResult, Results explicitly) or
    /// fall through and let the driver auto-complete the phase (content stubs until
    /// SALIN-138/181/202 land their surfaces). One machine per controller instance;
    /// retry/restart/relaunch reload the scene and construct a fresh machine.
    /// </summary>
    private IEnumerator RunLevelFlow()
    {
        if (_levelConfig == null)
        {
            DebugLogger.LogError("LevelFlowController: No LevelConfigSO resolved. Aborting flow.");
            yield break;
        }

        ConfigureActiveClueSystems();

        _machine = new LevelFlowMachine(LevelPhasePlan.FromConfig(_levelConfig));
        _machine.PhaseChanged += HandleMachinePhaseChanged;
        s_activeFlow = this;
        _machine.Begin();

        while (!_machine.IsTerminal && !_flowAborted)
        {
            LevelPhase phase = _machine.Phase;
            yield return ExecutePhase(phase);

            if (_flowAborted)
                yield break;

            // A stub executor finished without reporting: advance so the flow
            // cannot deadlock on a phase that has no surface yet.
            if (!_machine.IsTerminal && _machine.Phase == phase)
                _machine.ReportPhaseComplete(phase);
        }
    }

    private IEnumerator ExecutePhase(LevelPhase phase)
    {
        switch (phase)
        {
            case LevelPhase.Story: return ExecuteStory();
            case LevelPhase.Defense: return ExecuteDefense();
            case LevelPhase.AtomicSave: return ExecuteAtomicSave();
            case LevelPhase.Results: return ExecuteResults();
            // Content phases whose surfaces land with SALIN-138 (FocusWords),
            // SALIN-181 (ContextChallenge), and SALIN-202 (MemoryReward);
            // SymbolLearning/RequiredPractice route through learning surfaces
            // in the same tickets. The driver auto-completes them until then.
            default: return ExecuteStubPhase();
        }
    }

    private static IEnumerator ExecuteStubPhase()
    {
        yield break;
    }

    private IEnumerator ExecuteStory()
    {
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
            yield return new WaitUntil(() => !_waitingForCutscene || _machine.IsTerminal);

            if (_machine.IsTerminal)
                yield break;
        }

        // AC-1: Play intro dialogue before combat begins
        if (_levelConfig.introDialogue != null && _dialogueController != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                GameManager.Instance.StartGame();

            _waitingForDialogue = true;
            _dialogueController.Play(_levelConfig.introDialogue);
            yield return new WaitUntil(() => !_waitingForDialogue || _machine.IsTerminal);
        }
    }

    private IEnumerator ExecuteDefense()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        // Legacy pre-wave beats stay inside the Defense executor so unauthored
        // levels behave exactly as before the phase machine existed.
        if (_revealTiming == RevealTiming.BeforeTutorial)
        {
            yield return PlayRevealsIfAny();
            if (_flowAborted || _machine.IsTerminal) yield break;
        }

        if (ShouldRunChallengePrototype())
            yield return PlayChallengeIfConfigured();
        else
            yield return PlayLevelTutorialIfNeeded();

        if (_flowAborted || _machine.IsTerminal)
            yield break;

        if (_revealTiming == RevealTiming.AfterTutorial)
        {
            yield return PlayRevealsIfAny();
            if (_flowAborted || _machine.IsTerminal) yield break;
        }

        yield return PlayBossTutorialIfNeeded();

        if (_flowAborted || _machine.IsTerminal)
            yield break;

        // AC-2: Start BGM from level config
        if (_levelConfig.bgmClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(_levelConfig.bgmClip);

        if (_machine.IsTerminal)
            yield break;

        // AC-3: Start waves — no isBossLevel branching; WaveManager handles it internally
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        if (_waveManager != null)
            _waveManager.StartLevel();
        else
            DebugLogger.LogError("LevelFlowController: WaveManager reference missing.");

        // Defense systems report defense completion only (OnDefenseComplete →
        // ReportDefenseComplete). They can never mark the level complete.
        yield return new WaitUntil(() => _machine.Phase != LevelPhase.Defense || _machine.IsTerminal);
    }

    private IEnumerator ExecuteAtomicSave()
    {
        _levelEnded = true;

        // The flow, not the defense layer, owns "level complete": GameManager
        // clears the pause snapshot and enters LevelComplete, the legacy
        // ProgressManager path writes stars, ComboManager resets. Our own
        // HandleLevelComplete ignores this raise because a machine is running.
        EventBus.RaiseLevelComplete();

        _completionCommitResult = CommitCompletion();
        if (_completionCommitResult != null && _completionCommitResult.IsAccepted)
        {
            _machine.ReportSaveResult(accepted: true);
            yield break;
        }

        _machine.ReportSaveResult(accepted: false);
        ShowSaveFailurePanel(_completionCommitResult);
        yield return new WaitUntil(() => _machine.Phase != LevelPhase.AtomicSave || _machine.IsTerminal);
    }

    private IEnumerator ExecuteResults()
    {
        yield return PlayOutroSequence();
        ShowVictoryScreen();
        _machine.ReportPhaseComplete(LevelPhase.Results);
    }

    private void HandleMachinePhaseChanged(LevelPhase from, LevelPhase to)
    {
        // Terminal cleanup: no stale waits may survive a defeat or exit. Deeper
        // per-phase surfaces (prompts, timers) clean up inside their own tickets'
        // executors as they land.
        if (_machine != null && _machine.IsTerminal)
        {
            _waitingForDialogue = false;
            _waitingForCutscene = false;
        }
    }

    private void EnsureRuntimeReferences(WaveSpawner waveSpawner, EnemyDataSO fallbackEnemyData)
    {
        _waveManager ??= FindFirstObjectByType<WaveManager>();
        _dialogueController ??= FindActiveDialogueController();
        _cutscenePlayer ??= FindFirstObjectByType<CutscenePlayer>();
        _victoryScreen ??= FindFirstObjectByType<VictoryScreenUI>(FindObjectsInactive.Include);
        _defeatScreen ??= FindFirstObjectByType<DefeatScreenUI>(FindObjectsInactive.Include);
        _saveFailurePanel ??= FindFirstObjectByType<CampaignOutcomeSaveFailurePanel>(FindObjectsInactive.Include);
        _revealController ??= FindFirstObjectByType<CharacterUnlockRevealController>(FindObjectsInactive.Include);
        _bossTutorialController ??= FindFirstObjectByType<BossTutorialController>(FindObjectsInactive.Include);
        _activeClueDirector ??= FindFirstObjectByType<ActiveClueDirector>(FindObjectsInactive.Include);
        _activeCluePresenter ??= FindFirstObjectByType<ActiveCluePresenter>(FindObjectsInactive.Include);

        if (_activeClueDirector == null)
        {
            GameObject directorObject = new GameObject("[Runtime] ActiveClueDirector");
            directorObject.transform.SetParent(transform, false);
            _activeClueDirector = directorObject.AddComponent<ActiveClueDirector>();
        }

        if (_activeCluePresenter == null)
        {
            GameObject presenterObject = new GameObject("[Runtime] ActiveCluePresenter");
            presenterObject.transform.SetParent(transform, false);
            _activeCluePresenter = presenterObject.AddComponent<ActiveCluePresenter>();
        }

        if (ShouldRunChallengePrototype() && _challengeFlowController == null)
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

    private void ConfigureActiveClueSystems()
    {
        if (_levelConfig == null)
            return;

        if (_activeClueDirector != null)
        {
            // Clue combat is active exactly when the player can draw. Probing only for
            // GameState.Playing would silently revert a Practicing-state level to legacy
            // targeting, and would keep the mark live while drawing is suppressed for a
            // cutscene or tutorial beat.
            _activeClueDirector.SetObjectiveSource(
                new LevelConfigClueObjectiveSource(
                    _levelConfig,
                    () => GameManager.Instance != null
                        && GameManager.Instance.AcceptsDrawingInput));
        }

        _activeCluePresenter?.ApplyLevel(_levelConfig);
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
            yield return PlayLevelTutorialIfNeeded();
            yield break;
        }

        yield return _challengeFlowController.Play(_levelConfig.challengeSequence, _levelConfig.levelNumber);
        if (_challengeFlowController.LastPlayResult == ChallengePlayResult.InvalidSequence)
        {
            DebugLogger.LogWarning("LevelFlowController: Invalid challenge sequence. Falling back to legacy onboarding.");
            yield return PlayLevelTutorialIfNeeded();
            yield break;
        }

        if (_challengeFlowController.Session == null || _challengeFlowController.Session.State != ChallengeSessionState.Completed)
            _flowAborted = true;
    }

    private bool ShouldRunChallengePrototype()
    {
        return _levelConfig != null
            && (_levelConfig.challengePrototypeEnabled || _challengePrototypeEnabled)
            && _levelConfig.challengeSequence != null;
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

    // AC-5: Level complete → outro dialogue → [cutscene (after)] → victory screen.
    // With a running machine this event is raised BY the flow itself at AtomicSave
    // and is otherwise ignored — an external completion event can never commit or
    // open Results. The machine-less path preserves the legacy synchronous routing
    // for bare controllers (existing EditMode tests, sandbox scenes).
    private void HandleLevelComplete()
    {
        if (_machine != null)
            return;

        if (_levelEnded) return;
        _levelEnded = true;
        _completionCommitResult = CommitCompletion();

        StartCoroutine(PlayOutroThenVictory());
    }

    private void HandleDefenseComplete()
    {
        _machine?.ReportDefenseComplete();
    }

    private void HandleGamePaused()
    {
        _machine?.NotifyPaused();
    }

    private void HandleGameResumed()
    {
        _machine?.NotifyResumed();
    }

    protected virtual CampaignOutcomeCommitResult CommitCompletion()
    {
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
            ProgressManager.Instance != null)
            return ProgressManager.Instance.CommitCurrentLevelOutcome();

        if (SaveManager.Instance == null || SaveManager.Instance.Mode == SaveManagerMode.Legacy)
            return CampaignOutcomeCommitResult.Committed(null);

        if (SaveManager.Instance.Mode == SaveManagerMode.RevisedBlocked)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "revised-save-blocked");

        return CampaignOutcomeCommitResult.Blocked(
            null, CampaignSaveFailureCode.InvalidStructure, "progress-manager-missing");
    }

    private IEnumerator PlayOutroThenVictory()
    {
        yield return PlayOutroSequence();

        if (_completionCommitResult != null && _completionCommitResult.IsAccepted)
            ShowVictoryScreen();
        else
            ShowSaveFailurePanel(_completionCommitResult);
    }

    private IEnumerator PlayOutroSequence()
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
    }

    // AC-4: Game over → defeat screen directly (no outro)
    private void HandleGameOver()
    {
        // Once the outcome is owned — AtomicSave entered on the machine path, or
        // the legacy path already routed — a late game over can never reopen defeat
        // on top of a saved level.
        if (_levelEnded) return;

        if (_machine != null)
        {
            // Legal from every non-terminal phase; the driver loop unwinds on the
            // terminal transition and terminal cleanup clears stale waits.
            if (_machine.ReportDefeat())
                ShowDefeatScreen();
            return;
        }

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

    private void ShowSaveFailurePanel(CampaignOutcomeCommitResult result)
    {
        if (_saveFailurePanel == null)
            _saveFailurePanel = FindFirstObjectByType<CampaignOutcomeSaveFailurePanel>(FindObjectsInactive.Include);
        if (_saveFailurePanel == null)
            return;

        _saveFailurePanel.Present(
            result,
            RetryCompletion,
            OnSaveRetryAccepted,
            () =>
            {
                _machine?.RequestExit();
                if (SceneLoader.Instance != null)
                    SceneLoader.Instance.LoadMainMenu();
            });
    }

    private void OnSaveRetryAccepted()
    {
        // Machine flow: an accepted retry releases the AtomicSave gate and the
        // Results executor opens the victory screen. Legacy flow shows it directly.
        if (_machine != null)
            _machine.ReportSaveResult(accepted: true);
        else
            ShowVictoryScreen();
    }

    protected virtual CampaignOutcomeCommitResult RetryCompletion()
    {
        return ProgressManager.Instance != null
            ? ProgressManager.Instance.RetryPendingLevelOutcome()
            : CampaignOutcomeCommitResult.Blocked(
                _completionCommitResult?.Outcome,
                CampaignSaveFailureCode.InvalidStructure,
                "progress-manager-missing");
    }

    private void ShowDefeatScreen()
    {
        if (_defeatScreen != null)
            _defeatScreen.Show();
    }
}
