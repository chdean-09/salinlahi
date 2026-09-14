using System.Collections;
using System.Collections.Generic;
using Salinlahi.Runtime.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif

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
    private bool _skipLessonForCombatRetry;
    private bool _drawingSuppressedByFlow;
    private bool _runtimeBootstrapped;
    private LevelFlowMachine _machine;

    // SALIN-223: the plan the running machine was built from. The executors consult it
    // for the content-missing predicates rather than re-deriving the content rule from
    // raw config fields, so plan and runtime can never disagree about what is missing.
    private LevelPhasePlan _phasePlan;

    // Built on demand; never scene-wired. See ShowContentMissingPanel.
    private LevelContentMissingPanel _contentMissingPanel;

    // SALIN-240. Same shape and same reason as _contentMissingPanel: unwired in every scene,
    // built on demand, so no scene edit is forced. See ShowMemoryClaimPanel.
    private MemoryClaimPanel _memoryClaimPanel;

    // SALIN-253. Same shape and same reason again. See ShowEraCompletionScreen.
    private EraCompletionScreenUI _eraCompletionScreen;

    // SALIN-232. Built on demand; never scene-wired. See ShowWaveClearedScreen.
    private WaveClearedScreenUI _waveClearedScreen;

    // SALIN-232. True while the Wave Cleared screen is holding a defense completion that
    // has NOT yet been reported to the machine. Re-entrancy guard: OnDefenseComplete can
    // be raised more than once for one clear, and a second raise while the screen is up
    // must be inert rather than re-present it or register a second continue callback.
    // Cleared by the continue tap and by terminal cleanup, so a segmented level
    // (SALIN-226) can engage the gate again for its next segment.
    private bool _waveClearedGateHeld;

    // The controller currently driving a live LF-CONTRACT-v2 machine, if any.
    // WaveManager/BossController consult this to decide whether their completion
    // raises OnDefenseComplete (machine flow) or the legacy OnLevelComplete
    // (sandbox scenes and bare controllers with no running flow).
    private static LevelFlowController s_activeFlow;

    /// <summary>Metrics computed for the current completion (SALIN-202); null until AtomicSave runs.</summary>
    public LevelResults LastResults { get; private set; }

    /// <summary>Rewards resolved for the current completion (SALIN-202); null until AtomicSave runs.</summary>
    public RewardGrant LastRewardGrant { get; private set; }

    // SALIN-234 (AC-4): hearts as a COUNT for the Results readout. metric.hearts-ratio carries
    // heartsRemaining / maxHearts, and recovering "2 of 3" from 0.666... by rounding the float
    // back is the kind of edit that fails silently. ComputeCompletionResults already reads both
    // values, so they are simply kept rather than reconstructed.
    private int _lastHeartsRemaining;
    private int _lastMaxHearts;

    /// <summary>
    /// SALIN-226. The alternating Defense/ContextChallenge segment currently running,
    /// 0-based; 0 for the whole run on an unsegmented level.
    ///
    /// This is the segment boundary SALIN-235 (T20) / SALIN-236 (T21) will use as their
    /// checkpoint. It is EXPOSED here and deliberately not consumed: nothing in this ticket
    /// persists it, restores from it, or reads it on a defeat path. Retry today is still a
    /// full scene reload that builds a fresh machine, so the value resets with the run.
    /// </summary>
    public int CurrentSegmentIndex => _machine != null ? _machine.CurrentSegmentIndex : 0;

    /// <summary>
    /// SALIN-226. How many segments this level's plan runs; 1 when unsegmented.
    /// </summary>
    public int SegmentCount => _phasePlan != null ? _phasePlan.SegmentCount : 1;

    public static bool RoutesDefenseCompletion =>
        s_activeFlow != null
        && s_activeFlow._machine != null
        && !s_activeFlow._machine.IsTerminal;
    private CampaignOutcomeCommitResult _completionCommitResult;
    private ActiveClueDirector _activeClueDirector;
    private ActiveCluePresenter _activeCluePresenter;
    private FocusWordPreviewController _focusWordPreview;
    private SymbolLearningCardController _symbolLearningCards;
    private LevelReadyScreenController _levelReadyScreen;

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
        EventBus.OnLevelAttemptAborted += HandleLevelAttemptAborted;
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
        EventBus.OnLevelAttemptAborted -= HandleLevelAttemptAborted;

        if (s_activeFlow == this)
            s_activeFlow = null;
    }

    private void OnDestroy()
    {
        // A destroyed host never runs its coroutines' finally blocks, so a scene
        // unload during the preview would strand the suppression on the persistent
        // GameManager.
        ReleaseDrawingSuppression();
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

#if UNITY_INCLUDE_TESTS
    // EditMode tests pump Start() by hand inside the test runner's untitled
    // scene, which the gameplay-scene guard below would reject before any flow
    // logic runs. Set via reflection by LevelFlowControllerTests and reset in
    // its teardown; compiled out of player builds. Mirrors the
    // SandboxMode._availabilityOverride test-seam precedent.
    private static bool s_forceGameplaySceneForTests;
    private static bool s_skipReadyScreenForTests;

    public static void SetSkipReadyScreenForTests(bool skip)
    {
        s_skipReadyScreenForTests = skip;
    }
#endif

    private static bool IsGameplayScene()
    {
#if UNITY_INCLUDE_TESTS
        if (s_forceGameplaySceneForTests)
            return true;
#endif
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

        _skipLessonForCombatRetry = LevelRetryIntent.ConsumeCombatOnly();

        _phasePlan = LevelPhasePlan.FromConfig(_levelConfig);
        _machine = new LevelFlowMachine(_phasePlan);
        _machine.PhaseChanged += HandleMachinePhaseChanged;
        s_activeFlow = this;
        _machine.Begin();

        while (!_machine.IsTerminal && !_flowAborted)
        {
            LevelPhase phase = _machine.Phase;
            yield return ExecutePhase(phase);

            if (_flowAborted)
            {
                // An aborted flow leaves the machine non-terminal, so the terminal
                // cleanup never runs: no exit from here may hold suppression.
                ReleaseDrawingSuppression();
                yield break;
            }

            // A stub executor finished without reporting: advance so the flow
            // cannot deadlock on a phase that has no surface yet.
            if (!_machine.IsTerminal && _machine.Phase == phase)
                _machine.ReportPhaseComplete(phase);
        }
    }

    // Sandbox is a developer spawning surface: only the Defense phase means
    // anything there, and the content phases (story, learning cards, challenge,
    // save, results) would gate manual spawn testing behind the full level-one
    // onboarding. The driver auto-completes every other phase in sandbox.
    private static bool IsSandboxRun()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        return SandboxMode.IsActive;
#else
        return false;
#endif
    }

    private IEnumerator ExecutePhase(LevelPhase phase)
    {
        if (IsSandboxRun() && phase != LevelPhase.Defense)
            return ExecuteStubPhase();

        switch (phase)
        {
            case LevelPhase.Story: return ExecuteStory();
            case LevelPhase.FocusWords: return ExecuteFocusWords();
            case LevelPhase.SymbolLearning: return ExecuteSymbolLearning();
            case LevelPhase.Defense: return ExecuteDefense();
            case LevelPhase.ContextChallenge: return ExecuteContextChallenge();
            case LevelPhase.MemoryReward: return ExecuteMemoryReward();
            case LevelPhase.AtomicSave: return ExecuteAtomicSave();
            case LevelPhase.Results: return ExecuteResults();
            // RequiredPractice routes through its practice surface when its
            // campaign gate lands (SALIN-172 scope). The driver auto-completes
            // it until then.
            default: return ExecuteStubPhase();
        }
    }

    private static IEnumerator ExecuteStubPhase()
    {
        yield break;
    }

    private IEnumerator ExecuteStory()
    {
        bool shouldPresentReady = !IsSandboxRun() && !_skipLessonForCombatRetry;
#if UNITY_INCLUDE_TESTS
        shouldPresentReady &= !s_skipReadyScreenForTests;
#endif
        if (shouldPresentReady)
        {
            if (_levelReadyScreen == null)
            {
                GameObject readyObject = new GameObject("[Runtime] LevelReadyScreenController");
                readyObject.transform.SetParent(transform, false);
                _levelReadyScreen = readyObject.AddComponent<LevelReadyScreenController>();
            }

            yield return _levelReadyScreen.Present(
                _levelConfig,
                () => _machine == null || _machine.IsTerminal || _flowAborted,
                ExitToLevelSelectFromReady);

            if (_machine == null || _machine.IsTerminal || _flowAborted)
                yield break;
        }

        // Spawn protagonist if level has one configured
        if (_levelConfig.hasProtagonist)
        {
            ProtagonistManager protagonistManager = EnsureProtagonistManager();
            if (protagonistManager != null)
            {
                Vector3 protagonistPos = protagonistManager.CalculateProtagonistPosition();

                // spawnBelowScreen parks him 5 units under the camera and leaves him there;
                // ProtagonistIntroBeat is the only thing that walks him up. That beat lives in the
                // onboarding sequence, which IsTutorialLevelWithSequence stops running once the
                // tutorial has been seen. Level 1 is the only level with protagonistWalksIn set, so
                // replaying it used to spawn him off-screen with nothing left to walk him in and no
                // protagonist visible for the whole level. Only duck below the screen when the beat
                // that recovers him will actually run.
                bool onboardingWillWalkHimIn = _levelConfig.protagonistWalksIn
                    && IsTutorialLevelWithSequence(_levelConfig);
                protagonistManager.EnsureProtagonist(protagonistPos, spawnBelowScreen: onboardingWillWalkHimIn);
            }
            else
            {
                DebugLogger.LogError("[LevelFlowController] ProtagonistManager.Instance is NULL! Is the ProtagonistManager prefab in the scene?");
            }
        }

        if (_skipLessonForCombatRetry)
            yield break;

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

    private IEnumerator ExecuteFocusWords()
    {
        if (_skipLessonForCombatRetry)
            yield break;

        if (_levelConfig.focusWords == null || _levelConfig.focusWords.Count == 0)
            yield break;

        // Both words and their decompositions must be readable BEFORE drawing
        // begins; the Defense executor releases this exactly once as it opens.
        SuppressDrawingForPreview();

        if (_focusWordPreview == null)
        {
            _focusWordPreview = FindFirstObjectByType<FocusWordPreviewController>(FindObjectsInactive.Include);
            if (_focusWordPreview == null)
            {
                GameObject previewObject = new GameObject("[Runtime] FocusWordPreviewController");
                previewObject.transform.SetParent(transform, false);
                _focusWordPreview = previewObject.AddComponent<FocusWordPreviewController>();
            }
        }

        yield return _focusWordPreview.Present(_levelConfig);
    }

    private IEnumerator ExecuteSymbolLearning()
    {
        if (_skipLessonForCombatRetry)
            yield break;

        // SALIN-157: one learning card per Instruction-kind requirement. A level
        // with none presentable skips without touching drawing suppression and
        // the driver auto-completes the phase.
        if (!SymbolLearningCardController.HasPresentableRequirement(_levelConfig))
            yield break;

        // Every card must be readable before drawing begins. FocusWords already
        // holds suppression through this phase when it ran; a level authored
        // with learning requirements but no focus words takes it here. Either
        // way the Defense executor releases it exactly once as it opens.
        SuppressDrawingForPreview();

        if (_symbolLearningCards == null)
        {
            _symbolLearningCards = FindFirstObjectByType<SymbolLearningCardController>(FindObjectsInactive.Include);
            if (_symbolLearningCards == null)
            {
                GameObject cardObject = new GameObject("[Runtime] SymbolLearningCardController");
                cardObject.transform.SetParent(transform, false);
                _symbolLearningCards = cardObject.AddComponent<SymbolLearningCardController>();
            }
        }

        yield return _symbolLearningCards.Present(_levelConfig);
    }

    private void ExitToLevelSelectFromReady()
    {
        _flowAborted = true;
        ReleaseDrawingSuppression();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("LevelFlowController: SceneLoader not available for Ready Back.");
    }

    private IEnumerator ExecuteDefense()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        // Drawing input opens as the defense sequence begins (SALIN-138), ahead of
        // the pre-wave beats: those beats own their own suppression via try/finally
        // and their StartGame() remedy cannot clear the preview's flag.
        ReleaseDrawingSuppression();

        // SALIN-226. A segmented level re-enters Defense once per segment. The pre-wave
        // beats below are ONCE PER LEVEL: re-running them would replay the Level 1
        // onboarding, the reveals and the prototype sequence in the middle of the level.
        // The gate reads the machine's own segment index rather than a local bool so it
        // cannot drift out of step with the transition that caused the re-entry.
        if (_machine.CurrentSegmentIndex == 0)
        {
            yield return PlayOncePerLevelBeats();

            if (_flowAborted || _machine.IsTerminal)
                yield break;
        }

        if (_machine.IsTerminal)
            yield break;

        // AC-3: Start waves — no isBossLevel branching; WaveManager handles it internally
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            GameManager.Instance.StartGame();

        if (_waveManager == null)
        {
            DebugLogger.LogError("LevelFlowController: WaveManager reference missing.");
        }
        else if (_phasePlan != null
            && _phasePlan.TryGetSegmentWaveRange(
                _machine.CurrentSegmentIndex, out int startWave, out int endWaveExclusive))
        {
            // Segmented: run only this segment's slice of the flat wave list. Reaching the
            // bound completes the run through the same OnDefenseComplete a full clear uses.
            _waveManager.StartSegment(startWave, endWaveExclusive);
        }
        else
        {
            // Unsegmented levels take the identical path they take today.
            _waveManager.StartLevel();
        }

        // Defense systems report defense completion only (OnDefenseComplete →
        // ReportDefenseComplete). They can never mark the level complete.
        yield return new WaitUntil(() => _machine.Phase != LevelPhase.Defense || _machine.IsTerminal);
    }

    /// <summary>
    /// SALIN-226. The pre-wave beats that run ONCE PER LEVEL, not once per segment: the
    /// reveals, the tutorial or prototype sequence, the boss tutorial and the BGM start.
    /// A segmented level re-enters Defense for every segment, and replaying these would
    /// repeat the onboarding in the middle of the level.
    ///
    /// Virtual so a test can observe how many times it ran; nothing else overrides it.
    /// </summary>
    protected virtual IEnumerator PlayOncePerLevelBeats()
    {
        // Level-wide challenge metrics cover exactly one playthrough, and a segmented
        // level plays one challenge session per segment.
        if (_challengeFlowController != null)
            _challengeFlowController.BeginLevelChallengeMetrics();

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
    }

    private IEnumerator ExecuteContextChallenge()
    {
        // D-003. Levels that opt into the shared combat-restoration path fill their focus-word
        // slots as clues are accepted during Defense. They do not open the retired, separate
        // post-wave challenge board; Level 1 remains on that authored path until its final-
        // syllable gate is migrated in a later slice.
        if (UsesCombatRestorationPath())
        {
            yield return ExecuteCombatRestoration();
            yield break;
        }

        // SALIN-223: phase 6 is planned on every level now, so this executor is the
        // thing that has to notice missing content. Returning early would let the
        // driver auto-complete the phase (RunLevelFlow), which is the defect: the
        // level would finish on wave clear with no challenge ever played.
        if (_levelConfig.challengeSequence == null)
        {
            yield return RefuseCompletionForMissingContent(
                LevelPhase.ContextChallenge,
                $"level {_levelConfig.levelNumber} has no authored challengeSequence");
            yield break;
        }

        if (_challengeFlowController == null)
        {
            // Authored content with no surface to play it on is a wiring defect, and
            // completing it silently is the exact defect class this ticket closes.
            // EnsureRuntimeReferences builds one whenever challengeSequence != null,
            // so this should be unreachable.
            yield return RefuseCompletionForMissingContent(
                LevelPhase.ContextChallenge,
                "the level has an authored challengeSequence but no ChallengeFlowController");
            yield break;
        }

        // SALIN-226. On a segmented level this phase plays only the current segment's
        // restoration leg. The two refusals above are unchanged and still run first, so a
        // level with no authored sequence refuses exactly as SALIN-223 made it refuse.
        IReadOnlyList<string> segmentUnitIds = _phasePlan != null
            ? _phasePlan.SegmentChallengeUnitIds(_machine.CurrentSegmentIndex)
            : System.Array.Empty<string>();
        bool isSegmented = _phasePlan != null && _phasePlan.SegmentCount > 1;

        if (isSegmented && segmentUnitIds.Count == 0)
        {
            // This segment has no restoration leg — a trailing wave group, authored as
            // such. Completing here is authored intent, NOT the silent auto-completion
            // SALIN-223 removed: the level does have a challenge sequence, and the plan
            // rejects a segment list in which no segment plays any unit at all.
            _machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            yield break;
        }

        // SALIN-231. The hint modal explains the focus word this unit evidences, and the
        // meaning lives here on the level config, not on the challenge sequence. Handed
        // over immediately before every Play so no sequence is ever played against another
        // level's words.
        _challengeFlowController.SetLevelFocusWords(_levelConfig.focusWords);

        yield return _challengeFlowController.Play(
            _levelConfig.challengeSequence,
            _levelConfig.levelNumber,
            _levelConfig.challengePolicy,
            new ProgressManagerEvidenceSink(),
            segmentUnitIds);

        switch (_challengeFlowController.LastPlayResult)
        {
            case ChallengePlayResult.Completed:
                _machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
                break;
            case ChallengePlayResult.Exited:
                // Evidence stays uncommitted in the level recorder; only the
                // AtomicSave phase can commit campaign progress.
                _machine.RequestExit();
                break;
            case ChallengePlayResult.Failed:
                // ChallengeFlowController normally raises GameOver itself when
                // hearts reach zero; this is the fallback when it could not.
                if (!_machine.IsTerminal && _machine.ReportDefeat())
                    ShowDefeatScreen();
                break;
            // NotStarted is unreachable today: ChallengeFlowController.Play always reassigns
            // LastPlayResult before it returns. Refusing is still strictly safer than falling
            // through, because falling through means the driver auto-completes the phase — the
            // exact behaviour this ticket exists to remove.
            default:
                // SALIN-223 inverts the old policy here. Falling through let the driver
                // auto-complete the phase so "a bad asset cannot deadlock the level" —
                // but the level then completed and unlocked the next one on the strength
                // of a challenge that never ran. A bad asset must block, not pass.
                yield return RefuseCompletionForMissingContent(
                    LevelPhase.ContextChallenge,
                    $"the challenge sequence could not be played ({_challengeFlowController.LastPlayResult})");
                break;
        }
    }

    private bool UsesCombatRestorationPath()
    {
        return _levelConfig != null
            && _levelConfig.activeClueCombatEnabled
            && _levelConfig.activeClueRestorationEnabled;
    }

    private IEnumerator ExecuteCombatRestoration()
    {
        if (_activeCluePresenter == null || !_activeCluePresenter.HasRestorationWords)
        {
            yield return RefuseCompletionForMissingContent(
                LevelPhase.ContextChallenge,
                $"level {_levelConfig.levelNumber} has no focus words for combat restoration");
            yield break;
        }

        if (!TryResolveCombatRestorationTargets(
                out List<ActiveClueRestorationTarget> requiredTargets,
                out bool segmentHasNoRestoration,
                out string mappingFailure))
        {
            yield return RefuseCompletionForMissingContent(
                LevelPhase.ContextChallenge,
                mappingFailure);
            yield break;
        }

        // A segment with no named restoration unit is authored as a wave-only segment. It is
        // valid to advance without asking for a second board, just as the legacy segmented path
        // does for an empty challengeUnitIds list.
        if (segmentHasNoRestoration)
        {
            _machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
            yield break;
        }

        if (!_activeCluePresenter.AreRestorationTargetsComplete(requiredTargets))
        {
            string required = DescribeRestorationTargets(requiredTargets);
            yield return RefuseCompletionForMissingContent(
                LevelPhase.ContextChallenge,
                $"combat ended before restoring focus word slots ({required})");
            yield break;
        }

        _machine.ReportPhaseComplete(LevelPhase.ContextChallenge);
    }

    private bool TryResolveCombatRestorationTargets(
        out List<ActiveClueRestorationTarget> requiredTargets,
        out bool segmentHasNoRestoration,
        out string mappingFailure)
    {
        requiredTargets = new List<ActiveClueRestorationTarget>();
        segmentHasNoRestoration = false;
        mappingFailure = null;

        bool isSegmented = _phasePlan != null && _phasePlan.SegmentCount > 1;
        if (_levelConfig.challengeSequence == null || _levelConfig.challengeSequence.units == null)
        {
            mappingFailure = $"level {_levelConfig.levelNumber} has combat restoration but no "
                + "challenge sequence to map its targets";
            return false;
        }

        IReadOnlyList<string> unitIds = isSegmented
            ? _phasePlan.SegmentChallengeUnitIds(_machine.CurrentSegmentIndex)
            : null;
        if (isSegmented && (unitIds == null || unitIds.Count == 0))
        {
            segmentHasNoRestoration = true;
            return true;
        }

        if (isSegmented)
        {
            for (int idIndex = 0; idIndex < unitIds.Count; idIndex++)
            {
                bool found = false;
                for (int unitIndex = 0; unitIndex < _levelConfig.challengeSequence.units.Length; unitIndex++)
                {
                    ChallengeUnitDefinition unit = _levelConfig.challengeSequence.units[unitIndex];
                    if (unit != null && unit.unitId == unitIds[idIndex])
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    mappingFailure = $"level {_levelConfig.levelNumber} segment {_machine.CurrentSegmentIndex} "
                        + $"references unknown challenge unit '{unitIds[idIndex]}'";
                    return false;
                }
            }
        }

        var seen = new HashSet<string>();
        for (int unitIndex = 0; unitIndex < _levelConfig.challengeSequence.units.Length; unitIndex++)
        {
            ChallengeUnitDefinition unit = _levelConfig.challengeSequence.units[unitIndex];
            if (isSegmented)
            {
                bool isInSegment = false;
                for (int idIndex = 0; idIndex < unitIds.Count; idIndex++)
                {
                    if (unit != null && unit.unitId == unitIds[idIndex])
                    {
                        isInSegment = true;
                        break;
                    }
                }

                if (!isInSegment)
                    continue;
            }

            if (!TryBuildRestorationTarget(
                    unit,
                    out ActiveClueRestorationTarget target,
                    out string unitFailure))
            {
                mappingFailure = isSegmented
                    ? $"level {_levelConfig.levelNumber} segment {_machine.CurrentSegmentIndex} {unitFailure}"
                    : $"level {_levelConfig.levelNumber} {unitFailure}";
                return false;
            }

            string targetKey = target.WordStableId + "|" + (target.SymbolStableId ?? "*");
            if (!seen.Add(targetKey))
                continue;

            requiredTargets.Add(target);
        }

        if (requiredTargets.Count == 0)
        {
            mappingFailure = isSegmented
                ? $"level {_levelConfig.levelNumber} segment {_machine.CurrentSegmentIndex} has no mapped focus words"
                : $"level {_levelConfig.levelNumber} has no mapped focus words";
            return false;
        }

        return true;
    }

    private bool TryBuildRestorationTarget(
        ChallengeUnitDefinition unit,
        out ActiveClueRestorationTarget target,
        out string failure)
    {
        target = null;
        failure = null;

        if (unit == null || string.IsNullOrEmpty(unit.unitId))
        {
            failure = "contains a null or unnamed challenge unit";
            return false;
        }

        if (string.IsNullOrEmpty(unit.evidenceContentId))
        {
            failure = $"challenge unit '{unit.unitId}' has no focus-word evidence id";
            return false;
        }

        FocusWordDefinition focusWord = FindFocusWord(unit.evidenceContentId);
        if (focusWord == null)
        {
            failure = $"challenge unit '{unit.unitId}' references unknown focus word '{unit.evidenceContentId}'";
            return false;
        }

        string tokenText = ResolveFocusTokenText(unit);
        string symbolStableId = ResolveRestorationSymbolStableId(focusWord, tokenText);
        target = new ActiveClueRestorationTarget(focusWord.stableId, symbolStableId);
        return true;
    }

    private FocusWordDefinition FindFocusWord(string stableId)
    {
        if (string.IsNullOrEmpty(stableId) || _levelConfig.focusWords == null)
            return null;

        for (int i = 0; i < _levelConfig.focusWords.Count; i++)
        {
            FocusWordDefinition word = _levelConfig.focusWords[i];
            if (word != null && string.Equals(word.stableId, stableId, System.StringComparison.Ordinal))
                return word;
        }

        return null;
    }

    private static string ResolveFocusTokenText(ChallengeUnitDefinition unit)
    {
        if (unit?.tokens == null)
            return null;

        for (int i = 0; i < unit.tokens.Length; i++)
        {
            ChallengeTokenDefinition token = unit.tokens[i];
            if (token != null
                && token.role == ChallengeTokenRole.Focus
                && !string.IsNullOrEmpty(token.displayText))
            {
                return token.displayText;
            }
        }

        for (int i = 0; i < unit.tokens.Length; i++)
        {
            ChallengeTokenDefinition token = unit.tokens[i];
            if (token != null && !string.IsNullOrEmpty(token.displayText))
                return token.displayText;
        }

        return null;
    }

    private static string ResolveRestorationSymbolStableId(
        FocusWordDefinition word,
        string tokenText)
    {
        if (word?.decomposition == null || string.IsNullOrEmpty(tokenText))
            return null;

        for (int i = 0; i < word.decomposition.Count; i++)
        {
            SymbolValueReference reference = word.decomposition[i];
            BaybayinCharacterSO symbol = reference?.symbol;
            if (symbol == null)
                continue;

            string spokenLabel = SpokenValueResolver.ResolveLabel(symbol, reference.spokenValueId);
            if (string.Equals(symbol.characterID, tokenText, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(spokenLabel, tokenText, System.StringComparison.OrdinalIgnoreCase))
            {
                return symbol.stableId;
            }
        }

        // A sentence/word token such as TAMA or AMA represents the whole focus word rather
        // than one slot. The null symbol id intentionally makes the gate require every slot.
        return null;
    }

    private static string DescribeRestorationTargets(
        IReadOnlyList<ActiveClueRestorationTarget> targets)
    {
        if (targets == null || targets.Count == 0)
            return "none";

        var labels = new List<string>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            ActiveClueRestorationTarget target = targets[i];
            if (target == null)
                continue;

            labels.Add(string.IsNullOrEmpty(target.SymbolStableId)
                ? target.WordStableId
                : target.WordStableId + "/" + target.SymbolStableId);
        }

        return string.Join(", ", labels);
    }

    private IEnumerator ExecuteMemoryReward()
    {
        // The restored-memory cutscene (SALIN-200 content). SALIN-223: phase 7 is planned
        // on every level, so unauthored memory content blocks the level instead of
        // letting the driver auto-complete it.
        if (_phasePlan == null || _phasePlan.MemoryRewardContentMissing)
        {
            yield return RefuseCompletionForMissingContent(
                LevelPhase.MemoryReward,
                $"level {_levelConfig.levelNumber} has no authored memory reward "
                + "(rewardIds and contextMedia.cutscene are both required)");
            yield break;
        }

        CutsceneSO memory = _levelConfig.contextMedia.cutscene;
        if (_cutscenePlayer == null)
        {
            // Deliberately NOT symmetric with ExecuteContextChallenge's missing-surface
            // branch. A missing ChallengeFlowController self-heals in
            // EnsureRuntimeReferences, so its absence really is a defect; a CutscenePlayer
            // may legitimately be absent in a bare host (PlayMode fixtures, sandbox),
            // and blocking on scene wiring rather than on authored content would brick
            // every such host. The content exists, so the phase is satisfied.
            yield break;
        }

        _waitingForCutscene = true;
        _cutscenePlayer.Play(memory);
        yield return new WaitUntil(() => !_waitingForCutscene || _machine.IsTerminal);
    }

    /// <summary>
    /// SALIN-223. Presents the content-missing panel and then refuses to complete the
    /// phase: the flow holds until something drives the machine terminal, so it never
    /// reaches AtomicSave, never raises LevelComplete, and never unlocks the next level.
    ///
    /// The hold is what defeats the driver's auto-advance in <see cref="RunLevelFlow"/>.
    /// When no panel could be presented there is nothing that could ever satisfy the
    /// hold, so the flow exits through the machine instead — a WaitUntil nothing can
    /// satisfy hangs a test runner rather than failing it.
    /// </summary>
    private IEnumerator RefuseCompletionForMissingContent(LevelPhase phase, string reason)
    {
        DebugLogger.LogError(
            $"LevelFlowController: {phase} cannot run because {reason}. "
            + "Refusing to complete the level; progress will not be saved (SALIN-223).");

        if (!ShowContentMissingPanel(phase))
        {
            DebugLogger.LogError(
                "LevelFlowController: no content-missing surface could be presented. "
                + "Exiting the level rather than holding a wait nothing can satisfy.");
            _machine.RequestExit();
            yield break;
        }

        yield return new WaitUntil(() => _machine.IsTerminal);
    }

    /// <summary>
    /// Resolves (and, when absent, builds) the content-missing panel. Returns whether it
    /// actually presented. The panel is intentionally not a [SerializeField]: it is
    /// unwired in every scene, and adding one would force a scene edit this ticket does
    /// not make.
    /// </summary>
    private bool ShowContentMissingPanel(LevelPhase phase)
    {
        if (_contentMissingPanel == null)
            _contentMissingPanel = FindFirstObjectByType<LevelContentMissingPanel>(FindObjectsInactive.Include);

        if (_contentMissingPanel == null)
        {
            GameObject panelObject = new GameObject("[Runtime] LevelContentMissingPanel");
            _contentMissingPanel = panelObject.AddComponent<LevelContentMissingPanel>();
        }

        return _contentMissingPanel.Present(
            phase,
            () =>
            {
                _machine?.RequestExit();
                if (SceneLoader.Instance != null)
                    SceneLoader.Instance.LoadMainMenu();
            });
    }

    private IEnumerator ExecuteAtomicSave()
    {
        _levelEnded = true;

        // SALIN-202: compute the documented metrics and reward grant BEFORE the
        // commit, from the same evidence batch the outcome will carry.
        ComputeCompletionResults();

        // The flow, not the defense layer, owns "level complete": GameManager
        // clears the pause snapshot and enters LevelComplete and the legacy
        // ProgressManager path writes stars. Our own HandleLevelComplete
        // ignores this raise because a machine is running.
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
        // SALIN-234: the summary push moved INTO ShowVictoryScreen, the one place all three
        // paths to the victory screen share. See the note there.
        ShowVictoryScreen();
        _machine.ReportPhaseComplete(LevelPhase.Results);
    }

    private void ComputeCompletionResults()
    {
        LearningEvidenceBatch evidence = ProgressManager.Instance != null
            ? ProgressManager.Instance.LevelEvidence.Build()
            : new LearningEvidenceBatch();

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        int hearts = heartSystem != null ? heartSystem.GetCurrentHearts() : 1;
        int maxHearts = heartSystem != null ? heartSystem.GetMaxHearts() : 1;
        _lastHeartsRemaining = hearts;
        _lastMaxHearts = maxHearts;

        // SALIN-226. Read the LEVEL-WIDE accumulators, not the last session. A segmented
        // level plays one ChallengeSession per segment and each replaces the previous, so
        // reading Session here would feed only the final segment's hints into the star and
        // score calculation — a wrong result that throws nothing and fails no test. On an
        // unsegmented level these are exactly the single session's values.
        int hintsUsed = _challengeFlowController != null
            ? _challengeFlowController.LevelHintsUsed
            : 0;
        float emergencyHintScorePenalty = _challengeFlowController != null
            ? _challengeFlowController.LevelEmergencyHintScorePenalty
            : 0f;

        LastResults = LevelResultsCalculator.Compute(
            evidence,
            hearts,
            maxHearts,
            hintsUsed,
            emergencyHintScorePenalty);
        LastRewardGrant = LevelRewardResolver.Resolve(_levelConfig);
        ProgressManager.Instance?.SetPendingLevelResults(LastResults);

        // SALIN-220. Derived here, before CommitCompletion, from the phases this run actually
        // finished. An objective the level never authored counts as satisfied -- the rule lives
        // in LevelObjectiveFlagResolver, not here.
        ProgressManager.Instance?.SetPendingObjectiveFlags(
            LevelObjectiveFlagResolver.Resolve(_machine?.Plan, _machine?.CompletedPhases));
    }

    /// <summary>
    /// SALIN-234. Composes the Results readout. Every player-facing string lives in
    /// <see cref="LevelResultsCopy"/> — this method holds none — following the
    /// CampaignSaveNoticeCopy (SALIN-272) and LevelLockNoticeCopy (SALIN-137) precedent.
    ///
    /// NO ACCURACY LINE, by owner ruling R1 (2026-09-13): D-021 cut the DISPLAYED
    /// accuracy/streak statistic. The shipped "Tracing N%   Context N%" line is gone, which
    /// also removes the only player-facing "Tracing" prose in the project and settles D-006
    /// here by deletion rather than rewording. The SCORING is deliberately untouched — tracing
    /// accuracy is still 0.5 and context accuracy 0.3 of metric.score and both still gate the
    /// star thresholds (LevelResultsCalculator.cs:43-50), pinned by
    /// LevelResultsScoringWeightPinTests so no level's star rating moves because of a UI ticket.
    /// </summary>
    private string BuildResultsSummary()
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(LevelResultsCopy.Stars(LastResults.Stars));
        if (LastResults.Metrics.TryGetValue(LevelResultsCalculator.ScoreMetricId, out float score))
        {
            builder.Append(LevelResultsCopy.InlineSeparator)
                .Append(LevelResultsCopy.Score(Mathf.RoundToInt(score)));
        }

        // AC-4 / AC-5. Both values are already computed; neither was ever rendered.
        builder.Append(LevelResultsCopy.LineSeparator)
            .Append(LevelResultsCopy.Hearts(_lastHeartsRemaining, _lastMaxHearts));
        if (LastResults.Metrics.TryGetValue(LevelResultsCalculator.HintsUsedMetricId, out float hints))
        {
            builder.Append(LevelResultsCopy.InlineSeparator)
                .Append(LevelResultsCopy.Hints(Mathf.RoundToInt(hints)));
        }

        // SALIN-231 AC-3. The penalty itself needed no building: it has been accrued by
        // ChallengeSession, accumulated level-wide by ChallengeFlowController, and carried
        // as metric.emergency-hint-penalty since SALIN-181/226/202. It was simply never
        // rendered. The metric is a 0-1 fraction of the score; the screen shows points.
        // Guarded on > 0 so tiers 1-4 — where ForTier leaves the budget disabled and the
        // metric is always 0 — gain no dead "Hint cost -0" readout.
        if (LastResults.Metrics.TryGetValue(
                LevelResultsCalculator.EmergencyHintPenaltyMetricId, out float hintPenalty)
            && hintPenalty > 0f)
        {
            builder.Append(LevelResultsCopy.InlineSeparator)
                .Append(LevelResultsCopy.HintPenalty(Mathf.RoundToInt(hintPenalty * 100f)));
        }

        // D-003: these are the level's focus words auto-filled during combat, not a
        // restoration board. The null _levelConfig guard matters now that this runs on the
        // legacy and save-retry paths too.
        if (_levelConfig != null && _levelConfig.focusWords != null && _levelConfig.focusWords.Count > 0)
        {
            var restored = new List<string>(_levelConfig.focusWords.Count);
            for (int i = 0; i < _levelConfig.focusWords.Count; i++)
                restored.Add(_levelConfig.focusWords[i].displayLabel);
            builder.Append(LevelResultsCopy.LineSeparator).Append(LevelResultsCopy.Restored(restored));
        }

        if (LastRewardGrant != null && LastRewardGrant.UnlockedSymbolIds.Count > 0)
        {
            builder.Append(LevelResultsCopy.LineSeparator)
                .Append(LevelResultsCopy.NewSymbols(LastRewardGrant.UnlockedSymbolIds.Count));
        }

        return builder.ToString();
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
            _levelReadyScreen?.Hide();
            // SALIN-232: this is that landing for the Wave Cleared screen. A defeat or an
            // abort (HandleLevelAttemptAborted -> RequestExit) can arrive while the banner
            // is up, and the modal overlay claims sortingOrder 300 — it would sit over the
            // defeat and exit screens with a continue button whose deferred report the
            // terminal machine will refuse. Hiding here also releases the gate flag, so a
            // retried attempt starts with the gate open.
            _waveClearedGateHeld = false;
            if (_waveClearedScreen != null)
                _waveClearedScreen.Hide();
            // A defeat or exit must never leave drawing suppressed for the
            // terminal screens (GameManager also clears it on its own terminal
            // states; this covers Exited).
            _drawingSuppressedByFlow = false;
            GameManager.Instance?.SuppressDrawingInput(false);
            // SALIN-135: the tutorial statics are static, so a defeat or exit landing
            // mid-beat would otherwise carry a combat override or an input lock into the
            // retried attempt. Clearing is idempotent and, once IsActive is false, a beat
            // still unwinding can no longer re-latch either flag.
            TutorialRuntimeState.Clear();
            // The tutorial guide uses its own overlay canvas, so it is not necessarily covered
            // by the gameplay HUD root hidden by DefeatScreenUI. Close it explicitly to keep its
            // current prompt and feedback (for example, "Draw MA" and "Nice — that's the one.")
            // from remaining above the terminal screen.
            FindFirstObjectByType<Level1TutorialGuideUI>(FindObjectsInactive.Include)?.Hide();
        }
    }

    private void SuppressDrawingForPreview()
    {
        _drawingSuppressedByFlow = true;
        GameManager.Instance?.SuppressDrawingInput(true);
    }

    /// <summary>
    /// Releases the preview's drawing suppression, and only that: a flow that never
    /// suppressed cannot stomp a beat or cutscene that currently owns the flag.
    /// </summary>
    private void ReleaseDrawingSuppression()
    {
        if (!_drawingSuppressedByFlow)
            return;

        _drawingSuppressedByFlow = false;
        GameManager.Instance?.SuppressDrawingInput(false);
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

        if (_levelConfig != null && _levelConfig.challengeSequence != null && _challengeFlowController == null)
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

        // SALIN-225 deleted ComboTeachBeat and FocusModeTeachBeat with the mechanics they taught.
        // SALIN-241 replaced them with MassClearTeachBeat, which teaches the AOE mass-clear that
        // Level 2 switches on, so the level-2 arm attaches that beat plus ReleaseBeat and still
        // skips Level 1's four basics rather than re-teaching them.
        //
        // This split is cosmetic: Level1OnboardingController.Awake calls EnsureDefaultBeatComponents,
        // which attaches every beat regardless of level. It is kept in step with that method so the
        // two sites do not drift and read as disagreeing about what Level 2 runs.
        bool isLevel2Onboarding = _levelConfig != null
            && _levelConfig.levelNumber == LevelTutorialProgress.Level2TutorialLevelNumber;
        if (isLevel2Onboarding)
        {
            go.AddComponent<MassClearTeachBeat>();
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

        DialogueController inactiveFallback = null;

        for (int i = 0; i < controllers.Length; i++)
        {
            DialogueController controller = controllers[i];
            if (controller == null)
                continue;

            if (controller.gameObject.activeInHierarchy)
                return controller;

            inactiveFallback ??= controller;
        }

        return inactiveFallback;
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
        // Sandbox skips the onboarding gates so manual spawn testing is reachable.
        if (IsSandboxRun())
            yield break;

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
        if (IsSandboxRun())
            yield break;
        if (_levelConfig == null || _levelConfig.challengeSequence == null)
            yield break;
        if (_challengeFlowController == null)
        {
            DebugLogger.LogError("LevelFlowController: Challenge sequence is assigned but ChallengeFlowController is missing.");
            yield return PlayLevelTutorialIfNeeded();
            yield break;
        }

        // SALIN-231. See ExecuteContextChallenge: the legacy path sets the words too, so
        // there is no Play call that could inherit a previous level's list.
        _challengeFlowController.SetLevelFocusWords(_levelConfig.focusWords);

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
        if (IsSandboxRun())
            yield break;
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
        if (IsSandboxRun())
            yield break;
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

        // With no outro content there is nothing to wait for, and StartCoroutine
        // would defer the victory/save-failure routing to a later player-loop
        // frame — a frame bare EditMode fixtures never get, and a needless one
        // at runtime.
        if (!HasOutroContent())
        {
            FinishLegacyCompletion();
            return;
        }

        StartCoroutine(PlayOutroThenVictory());
    }

    private bool HasOutroContent()
    {
        return (_levelConfig != null && _levelConfig.outroDialogue != null && _dialogueController != null)
            || (ResolveCutscene(CutsceneTriggerType.AfterLevel) != null && _cutscenePlayer != null);
    }

    private void FinishLegacyCompletion()
    {
        if (_completionCommitResult != null && _completionCommitResult.IsAccepted)
            ShowVictoryScreen();
        else
            ShowSaveFailurePanel(_completionCommitResult);
    }

    /// <summary>
    /// SALIN-232 (AC-5, AC-7). The Wave Cleared gate.
    ///
    /// WHY THE GATE IS HERE AND NOT IN ExecuteDefense. ExecuteDefense ends on
    /// <c>WaitUntil(_machine.Phase != LevelPhase.Defense || _machine.IsTerminal)</c>, which
    /// only releases AFTER this method has already reported completion and the machine has
    /// already left Defense. Presenting the screen after that wait compiles, throws nothing
    /// and passes every existing test while showing the banner one phase too late — AC-7
    /// violated invisibly. The hold therefore lives at the report site: the report is
    /// DEFERRED into the continue callback, the machine stays in Defense, and the
    /// ExecuteDefense wait keeps holding on its own terms. No new LevelPhase, no change to
    /// LevelFlowMachine.
    /// </summary>
    private void HandleDefenseComplete()
    {
        // A second raise while the screen is up is inert: it must not re-present the
        // screen, and it must not register a second continue callback.
        if (_waveClearedGateHeld)
            return;

        // Anything that is not a live Defense-phase completion takes the pre-SALIN-232
        // path unchanged. A terminal machine rejects the report on its own (that is what
        // makes a late raise after a defeat or an abort inert), and routing it through the
        // screen instead would put a celebration banner over the defeat screen.
        if (_machine == null || _machine.IsTerminal || _machine.Phase != LevelPhase.Defense)
        {
            _machine?.ReportDefenseComplete();
            return;
        }

        // BOSS PATH — a design decision, recorded rather than buried. BossController also
        // raises OnDefenseComplete (BossController.cs:322-323), so a boss level would show
        // a "Wave Cleared" banner after a boss phase, which is visibly wrong copy. Levels
        // 10 and 15 keep their bossConfig under D-026 until SALIN-247 authors waves, so the
        // path is live even though Level 15 is outside the D-015 demo scope. Skipped here
        // rather than shipping wrong wording; if product wants a boss-phase acknowledgement
        // that is a separate ticket with its own copy.
        if (_levelConfig != null && _levelConfig.bossConfig != null)
        {
            _machine.ReportDefenseComplete();
            return;
        }

        _waveClearedGateHeld = true;
        if (!ShowWaveClearedScreen())
        {
            // The screen could not be built (an EditMode host, or a stripped scene).
            // Proceed immediately — NEVER hold. A missing celebration surface must not
            // strand a level the player has legitimately cleared. This is the opposite
            // remedy from LevelContentMissingPanel, whose false means "leave the level".
            _waveClearedGateHeld = false;
            _machine.ReportDefenseComplete();
        }
    }

    /// <summary>
    /// SALIN-232. Finds or builds the Wave Cleared screen and presents it, mirroring
    /// <see cref="ShowContentMissingPanel"/>. Returns false when no surface could be
    /// presented; the caller must then advance immediately rather than waiting.
    /// The screen is intentionally not a [SerializeField]: it is unwired in every scene,
    /// and adding one would force a scene edit this ticket does not make.
    /// </summary>
    private bool ShowWaveClearedScreen()
    {
        if (_waveClearedScreen == null)
            _waveClearedScreen = FindFirstObjectByType<WaveClearedScreenUI>(FindObjectsInactive.Include);

        if (_waveClearedScreen == null)
        {
            GameObject screenObject = new GameObject("[Runtime] WaveClearedScreen");
            _waveClearedScreen = screenObject.AddComponent<WaveClearedScreenUI>();
        }

        // Read hearts live, mid-level, exactly as ComputeCompletionResults does at the end
        // of the run — the same source and the same fallbacks, so the two readouts cannot
        // disagree about where the number comes from.
        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        int hearts = heartSystem != null ? heartSystem.GetCurrentHearts() : 1;
        int maxHearts = heartSystem != null ? heartSystem.GetMaxHearts() : 1;

        return _waveClearedScreen.Present(
            hearts,
            maxHearts,
            () =>
            {
                if (!_waveClearedGateHeld)
                    return;

                _waveClearedGateHeld = false;
                _waveClearedScreen.Hide();
                _machine?.ReportDefenseComplete();
            });
    }

    private void HandleGamePaused()
    {
        _machine?.NotifyPaused();
    }

    private void HandleGameResumed()
    {
        _machine?.NotifyResumed();
    }

    /// <summary>
    /// SALIN-141. The player restarted or left the level. The abort is routed THROUGH
    /// the machine — never around it: RequestExit is the only thing that makes the
    /// transition terminal, and only a terminal transition runs
    /// <see cref="HandleMachinePhaseChanged"/>, which clears the stale dialogue and
    /// cutscene waits, releases drawing suppression, and closes the SALIN-135 tutorial
    /// statics. Stopping the coroutines instead would leave the machine live, keep
    /// RoutesDefenseCompletion true, and carry that state into the next attempt.
    /// Never commits: an exited attempt can no longer reach AtomicSave.
    /// </summary>
    private void HandleLevelAttemptAborted()
    {
        _machine?.RequestExit();

        // Also latches the machine-less legacy path (bare controllers, sandbox
        // scenes, EditMode fixtures) so a late OnLevelComplete cannot commit there.
        _levelEnded = true;
        _flowAborted = true;
    }

    protected virtual CampaignOutcomeCommitResult CommitCompletion()
    {
        if (SaveManager.Instance != null &&
            SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
            ProgressManager.Instance != null)
            return ProgressManager.Instance.CommitCurrentLevelOutcome(
                LastRewardGrant?.UnlockedSymbolIds,
                LastRewardGrant?.UnlockedMemoryIds,
                LastRewardGrant?.ClaimedRewardIds);

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
        FinishLegacyCompletion();
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

    /// <summary>
    /// SALIN-234. The single entry point to the Results screen for all three callers — the
    /// machine path (ExecuteResults), FinishLegacyCompletion and OnSaveRetryAccepted.
    ///
    /// The summary push used to sit beside the machine-path call only, so the other two showed
    /// a bare panel: a player who hit a save failure and retried successfully got no stars, no
    /// score and no restored content. LastResults is assigned in ComputeCompletionResults,
    /// which runs at the top of ExecuteAtomicSave — before any of the three callers can fire —
    /// so the guard is satisfied on the retry path and correctly skipped on a machine-less
    /// legacy run, where no LevelResults is ever computed.
    /// </summary>
    private void ShowVictoryScreen()
    {
        if (_victoryScreen == null)
            return;

        if (LastResults != null)
            _victoryScreen.ShowResultsSummary(BuildResultsSummary());

        // SALIN-253 (AC-5). The era boundary is resolved HERE, where the campaign and the
        // level config are both in hand, and pushed into the Results screen — which reads
        // ProgressManager only and cannot work it out for itself. On an era's final level this
        // suppresses "Next Level", so the player is handed to the era completion flow instead
        // of being advanced straight into the next era's Level 1.
        CampaignConfigSO campaign = SaveManager.Instance != null ? SaveManager.Instance.Campaign : null;
        EraConfigSO completedEra = FindEraForLevel(campaign, _levelConfig);
        bool isEraFinalLevel = EraBoundary.IsEraFinalLevel(completedEra, _levelConfig);

        // Null on the legacy path; VictoryScreenUI falls back to ProgressManager.GetStars there.
        _victoryScreen.PresentResults(LastResults, isEraFinalLevel);

        ShowMemoryClaimPanel();
        ShowEraCompletionScreen(campaign, completedEra, isEraFinalLevel);
    }

    /// <summary>
    /// SALIN-253 (AC-1, AC-3, AC-4). Stacks the era completion screen on the Results screen
    /// when the player has just finished the last level of an era.
    ///
    /// It is an overlay rather than a new LevelPhase for the reasons recorded on
    /// <see cref="EraCompletionScreenUI"/>, and it sits directly beside
    /// <see cref="ShowMemoryClaimPanel"/> because that is the shipped precedent for stacking a
    /// surface on Results.
    ///
    /// NOTHING AWAITS IT — the same contract as ShowMemoryClaimPanel. A false from Present
    /// means there is nothing to show and the Results screen simply stands alone, which is
    /// exactly the behaviour that shipped before this ticket.
    ///
    /// THE NEXT ERA IS ALREADY UNLOCKED before this screen ever appears:
    /// CampaignOutcomeCoordinator.cs:244-252 advances a flat 15-entry list by index + 1 and so
    /// crosses the era boundary implicitly. This routes to that unlock; it must never
    /// re-implement it.
    /// </summary>
    private void ShowEraCompletionScreen(
        CampaignConfigSO campaign, EraConfigSO completedEra, bool isEraFinalLevel)
    {
        if (!isEraFinalLevel || completedEra == null)
            return;

        // The same read MemoryArchiveController.Present performs. A null repository is the
        // uninitialised-save case and is valid: every tile comes back locked rather than
        // throwing on a player's screen.
        IReadOnlyCollection<string> unlockedMemoryIds =
            SaveManager.Instance != null && SaveManager.Instance.Repository != null
                ? SaveManager.Instance.Repository.UnlockedMemoryIds
                : null;

        IReadOnlyList<MemoryArchiveEntry> entries =
            MemoryArchiveModel.BuildForEra(completedEra, unlockedMemoryIds);

        EraConfigSO nextEra = EraBoundary.NextEra(campaign, completedEra);
        int nextEraIndex = EraBoundary.IndexOfEra(campaign, nextEra);

        if (_eraCompletionScreen == null)
            _eraCompletionScreen = FindFirstObjectByType<EraCompletionScreenUI>(FindObjectsInactive.Include);

        if (_eraCompletionScreen == null)
        {
            GameObject screenObject = new GameObject("[Runtime] EraCompletionScreen");
            _eraCompletionScreen = screenObject.AddComponent<EraCompletionScreenUI>();
        }

        _eraCompletionScreen.Present(
            completedEra,
            entries,
            nextEra != null,
            () => EnterNextEra(nextEraIndex),
            null);
    }

    /// <summary>
    /// SALIN-253 (AC-4, BTN-NEXT-ERA). Leaves the target era behind for Level Select, then
    /// loads it — the shape of VictoryScreenUI.OnLevelSelectPressed, including the click sound.
    ///
    /// The index is left in a consumed-once static rather than a PlayerPref: a pref would have
    /// to be registered in ProgressManager.ClearAllProgress or survive a journey reset, and
    /// would persist across a crash into an era the player never asked for. See
    /// EraCompletionScreenUI.PendingEraIndex.
    /// </summary>
    private static void EnterNextEra(int nextEraIndex)
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        EraCompletionScreenUI.PendingEraIndex = nextEraIndex;

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("LevelFlowController: SceneLoader not available.");
    }

    /// <summary>
    /// SALIN-240. Offers the Claim Memory control over the Results screen when this
    /// completion granted a memory. Before this, unlockedMemoryIds was written to the save
    /// and never read back, so the reward was unreachable.
    ///
    /// It is a separate overlay rather than a button on the Results panel because
    /// VictoryScreenUI.cs belongs to SALIN-258 this sprint; see MemoryClaimPanel's summary.
    ///
    /// Nothing waits on the result. A false from Present means there is nothing to show —
    /// no memory granted, or the level's memory content is not authored (Levels 6-15 under
    /// D-015, which carry rewardIds: []) — and the Results screen simply stands alone, which
    /// is exactly the behaviour that shipped before this ticket.
    /// </summary>
    private void ShowMemoryClaimPanel()
    {
        if (LastRewardGrant == null
            || LastRewardGrant.UnlockedMemoryIds == null
            || LastRewardGrant.UnlockedMemoryIds.Count == 0)
        {
            return;
        }

        CampaignConfigSO campaign = SaveManager.Instance != null ? SaveManager.Instance.Campaign : null;
        EraConfigSO era = FindEraForLevel(campaign, _levelConfig);
        MemoryArchiveEntry entry =
            MemoryArchiveModel.BuildEntry(era, _levelConfig, LastRewardGrant.UnlockedMemoryIds);
        if (entry == null || !entry.HasAuthoredContent)
            return;

        if (_memoryClaimPanel == null)
            _memoryClaimPanel = FindFirstObjectByType<MemoryClaimPanel>(FindObjectsInactive.Include);

        if (_memoryClaimPanel == null)
        {
            GameObject panelObject = new GameObject("[Runtime] MemoryClaimPanel");
            _memoryClaimPanel = panelObject.AddComponent<MemoryClaimPanel>();
        }

        int eraTotal = era != null && era.levels != null ? era.levels.Count : entry.EraLocalOrder;
        _memoryClaimPanel.Present(entry, eraTotal, null);
    }

    private static EraConfigSO FindEraForLevel(CampaignConfigSO campaign, LevelConfigSO level)
    {
        if (campaign == null || campaign.eras == null || level == null)
            return null;

        foreach (EraConfigSO era in campaign.eras)
        {
            if (era == null || era.levels == null)
                continue;
            foreach (LevelConfigSO candidate in era.levels)
                if (candidate == level)
                    return era;
        }

        return null;
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
