using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Salinlahi.Runtime.Gameplay;

public sealed class Level1InteractiveTutorialController : MonoBehaviour
{
    [Header("Guards")]
    [SerializeField] private int _requiredLevelNumber = LevelTutorialProgress.TutorialLevelNumber;

    [Header("Flow Data")]
    [Tooltip("Optional: use a ScriptableObject sequence asset. If null, falls back to inline _steps.")]
    [SerializeField] private Level1TutorialSequenceSO _sequence;

    [SerializeField] private Level1TutorialStepSO[] _steps;

    // Inline copy (used only when _sequence is null)
    [SerializeField] private string _baseIntroText = "This is the base.";
    [SerializeField] private string _baseDefenseText = "Keep enemies away from it.";
    [SerializeField] private string _drawPurposeText = "Draw its syllable to defeat it.";
    [SerializeField] private string _baseDamageText = "The base took damage. Draw before enemies reach it.";
    [SerializeField] private string _finalReleaseText = "You are ready. Defend the base.";
    [SerializeField] private float _messageSeconds = 1.25f;
    [SerializeField] private float _idleHintSeconds = 5f;
    [SerializeField] private float _strongHintSeconds = 12f;
    [SerializeField] private int _failuresBeforeAssist = 3;

    [Header("Scene References")]
    [SerializeField] private WaveSpawner _waveSpawner;
    [SerializeField] private EnemyDataSO _fallbackTutorialEnemyData;
    [SerializeField] private Level1TutorialGuideUI _guideUI;
    [SerializeField] private GameObject[] _hideDuringTutorial;

    [Header("Animation Timing")]
    [SerializeField] private float _protagonistWalkSeconds = 1.75f;
    
    private Vector3 _protagonistEndPosition;

    private readonly Level1TutorialGlyphValidator _validator = new();
    private Level1TutorialState _state = Level1TutorialState.Gate;
    private Level1TutorialStepSO _activeStep;
    private Level1TutorialEnemyController _activeEnemy;
    private List<List<Vector2>> _lastSubmittedStrokes;
    private RecognitionResult _lastRecognitionResult;
    private bool _lastRecognitionPassed;
    private bool _hasRecognitionForPrompt;
    private int _failureCount;
    private bool _firstManualSuccess;
    private bool _skipRequested;
    private bool _assistedCompletion;
    private bool _baseDamagePauseRequested;
    private bool _baseDamagePauseShown;
    private bool _baseDamagePauseRunning;
    private bool[] _hiddenOriginalStates;
    private CanvasGroup[] _hiddenCanvasGroups;
    private bool[] _hiddenCanvasGroupExisted;
    private float[] _hiddenOriginalAlpha;
    private bool[] _hiddenOriginalInteractable;
    private bool[] _hiddenOriginalBlocksRaycasts;
    private bool _uiHiddenForTutorial;

    public static bool IsCombatOverrideActive => TutorialRuntimeState.IsCombatOverrideActive;
    public Level1TutorialState State => _state;
    public bool IsConfigured 
    { 
        get 
        { 
            var steps = GetSteps();
            bool configured = steps != null && steps.Length > 0;
            DebugLogger.Log($"Level1InteractiveTutorialController.IsConfigured: steps={(steps?.Length.ToString() ?? "null")}, configured={configured}");
            return configured;
        } 
    }

    private void Awake()
    {
        if (_guideUI != null)
        {
            DebugLogger.Log("Level1InteractiveTutorialController: Initializing GuideUI.");
            _guideUI.Initialize(RequestSkip);
        }
        else
        {
            DebugLogger.LogWarning("Level1InteractiveTutorialController: _guideUI is null in Awake.");
        }
    }

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += HandleRecognitionResolved;
        EventBus.OnBaseHit += HandleBaseHit;
        EventBus.OnGameOver += HandleGameOver;
        StrokeCapture.OnStrokesSubmitted += HandleStrokesSubmitted;
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
        EventBus.OnBaseHit -= HandleBaseHit;
        EventBus.OnGameOver -= HandleGameOver;
        StrokeCapture.OnStrokesSubmitted -= HandleStrokesSubmitted;
        TutorialRuntimeState.Clear();
        RestoreHiddenTutorialUI();
    }

    public static bool ShouldRunForContext(string sceneName, int levelNumber)
    {
        return levelNumber == LevelTutorialProgress.TutorialLevelNumber;
    }

    public bool ShouldRunFor(LevelConfigSO levelConfig)
    {
        DebugLogger.Log($"Level1InteractiveTutorialController.ShouldRunFor: " +
            $"levelConfig={(levelConfig != null ? levelConfig.name : "null")}, " +
            $"levelNumber={(levelConfig?.levelNumber.ToString() ?? "N/A")}, required={_requiredLevelNumber}, " +
            $"hasTutorialSequence={levelConfig?.tutorialSequence != null}");
        
        bool result = levelConfig != null
            && levelConfig.levelNumber == _requiredLevelNumber
            && levelConfig.tutorialSequence != null;
        
        DebugLogger.Log($"Level1InteractiveTutorialController.ShouldRunFor: returning {result}");
        return result;
    }

    public IEnumerator PlayIfNeeded(LevelConfigSO levelConfig)
    {
        if (!ShouldRunFor(levelConfig))
            yield break;

        ConfigureForLevel(levelConfig, _waveSpawner, _fallbackTutorialEnemyData);
        Begin();
        HideTutorialBlockedUI();
        yield return ShowMessage(GetBaseIntroText());
        yield return RunProtagonistWalkIn();
        yield return ShowMessage(GetBaseDefenseText());

        Level1TutorialStepSO[] steps = GetSteps();
        for (int i = 0; i < steps.Length; i++)
        {
            if (_skipRequested && _firstManualSuccess)
                break;

            yield return RunStep(steps[i], i);
        }

        if (_skipRequested && _firstManualSuccess)
            _state = Level1TutorialState.Skipped;
        else
            _state = Level1TutorialState.Release;

        yield return ShowMessage(GetFinalReleaseText());
        CompleteTutorial();
    }

#if UNITY_INCLUDE_TESTS
    public void BeginForTests(LevelConfigSO levelConfig, string sceneName)
    {
        if (levelConfig == null ||
            levelConfig.levelNumber != _requiredLevelNumber)
        {
            return;
        }

        Begin();
    }

    public void CompleteForTests()
    {
        CompleteTutorial();
    }
#endif

    private void Begin()
    {
        _state = Level1TutorialState.BaseIntro;
        TutorialRuntimeState.Begin(_requiredLevelNumber);
        TutorialRuntimeState.SetCombatOverrideActive(true);
        _failureCount = 0;
        _firstManualSuccess = false;
        _skipRequested = false;
        _assistedCompletion = false;
        _baseDamagePauseRequested = false;
        _baseDamagePauseShown = false;
        _baseDamagePauseRunning = false;
        _activeStep = null;
        _activeEnemy = null;
        _lastSubmittedStrokes = null;
        _hasRecognitionForPrompt = false;
        ClearLeakedEnemiesBeforeTutorial();
    }

    public void ConfigureForLevel(
        LevelConfigSO levelConfig,
        WaveSpawner waveSpawner,
        EnemyDataSO fallbackTutorialEnemyData)
    {
        if (levelConfig != null && levelConfig.tutorialSequence != null)
            _sequence = levelConfig.tutorialSequence;

        if (waveSpawner != null)
            _waveSpawner = waveSpawner;

        if (fallbackTutorialEnemyData != null)
            _fallbackTutorialEnemyData = fallbackTutorialEnemyData;

        EnsureRuntimeReferences();

        if (_guideUI != null)
            _guideUI.Initialize(RequestSkip);
    }

    private void EnsureRuntimeReferences()
    {
        _waveSpawner ??= FindFirstObjectByType<WaveSpawner>();
        _guideUI ??= Level1TutorialGuideUI.CreateRuntime();
        EnsureTutorialTransforms();
        EnsureHiddenTutorialUI();
    }

    private void EnsureTutorialTransforms()
    {
        // Calculate end position: center behind the base
        PlayerBase playerBase = FindFirstObjectByType<PlayerBase>();
        if (playerBase != null)
        {
            Bounds bounds = ResolveBounds(playerBase.gameObject);
            _protagonistEndPosition = new Vector3(bounds.center.x, bounds.min.y + 1.5f, 0f);
        }
        else
        {
            _protagonistEndPosition = Vector3.zero;
        }

        // Ensure protagonist exists via ProtagonistManager
        if (ProtagonistManager.Instance != null)
        {
            ProtagonistManager.Instance.EnsureProtagonist(_protagonistEndPosition);
        }
    }

    private static Bounds ResolveBounds(GameObject target)
    {
        Collider2D collider = target.GetComponent<Collider2D>();
        if (collider != null)
            return collider.bounds;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(target.transform.position, Vector3.one);
    }

    private static Transform CreateMarker(string name, Vector3 position)
    {
        GameObject marker = new(name);
        marker.transform.position = position;
        return marker.transform;
    }

    private void EnsureHiddenTutorialUI()
    {
        GameObject hudLayer = GameObject.Find("HUDLayer");
        if (hudLayer != null)
        {
            _hideDuringTutorial = new[] { hudLayer };
            return;
        }

        _hideDuringTutorial = new[]
        {
            GameObject.Find("HeartsPanel"),
            GameObject.Find("WaveText"),
            GameObject.Find("ComboText"),
            GameObject.Find("GlyphCounter")
        };
    }

    private static void ClearLeakedEnemiesBeforeTutorial()
    {
        WaveManager[] waveManagers = FindObjectsByType<WaveManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < waveManagers.Length; i++)
        {
            if (waveManagers[i] != null)
                waveManagers[i].PauseWaves();
        }

        EnemyPool pool = EnemyPool.Instance;
        if (pool != null)
        {
            pool.ReturnAllCheckedOut();
            return;
        }

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
            return;

        List<Enemy> activeEnemies = tracker.GetActiveEnemiesSnapshot();
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy enemy = activeEnemies[i];
            if (enemy != null)
                enemy.gameObject.SetActive(false);
        }
    }

    private Level1TutorialStepSO[] GetSteps()
    {
        if (_sequence != null && _sequence.steps != null && _sequence.steps.Length > 0)
            return _sequence.steps;
        return _steps;
    }

    private string GetBaseIntroText() => _sequence != null ? _sequence.baseIntroText : _baseIntroText;
    private string GetBaseDefenseText() => _sequence != null ? _sequence.baseDefenseText : _baseDefenseText;
    private string GetDrawPurposeText() => _sequence != null ? _sequence.drawPurposeText : _drawPurposeText;
    private string GetBaseDamageText() => _sequence != null ? _sequence.baseDamageText : _baseDamageText;
    private string GetFinalReleaseText() => _sequence != null ? _sequence.finalReleaseText : _finalReleaseText;
    private float GetMessageSeconds() => _sequence != null ? _sequence.messageSeconds : _messageSeconds;
    private float GetIdleHintSeconds() => _sequence != null ? _sequence.idleHintSeconds : _idleHintSeconds;
    private float GetStrongHintSeconds() => _sequence != null ? _sequence.strongHintSeconds : _strongHintSeconds;
    private int GetFailuresBeforeAssist() => _sequence != null ? _sequence.failuresBeforeAssist : _failuresBeforeAssist;
    private float GetProtagonistWalkSeconds() => _sequence != null ? _sequence.protagonistWalkSeconds : _protagonistWalkSeconds;

    private IEnumerator RunStep(Level1TutorialStepSO step, int stepIndex)
    {
        if (step == null)
            yield break;

        _state = stepIndex == 0 ? Level1TutorialState.EnemyIntro : Level1TutorialState.PracticeChain;
        _activeStep = step;
        _failureCount = 0;
        _assistedCompletion = false;
        _hasRecognitionForPrompt = false;
        _lastSubmittedStrokes = null;
        _guideUI?.Hide();

        yield return SpawnTutorialEnemy(step);

        if (stepIndex == 0)
            yield return ShowMessage(GetDrawPurposeText());

        _state = Level1TutorialState.DrawPrompt;
        _guideUI?.ShowPrompt(step, _firstManualSuccess);

        float promptStartTime = Time.unscaledTime;
        bool showedIdleHint = false;
        bool showedStrongHint = false;

        while (true)
        {
            if (_skipRequested && _firstManualSuccess)
            {
                _guideUI?.Hide();
                DefeatActiveTutorialEnemy();
                yield break;
            }

            if (_hasRecognitionForPrompt)
            {
                _hasRecognitionForPrompt = false;
                Level1TutorialValidationResult validation = ValidateActivePrompt();
                if (validation.IsCorrect)
                {
                    _guideUI?.Hide();
                    DefeatActiveTutorialEnemy();
                    _firstManualSuccess = true;
                    if (!string.IsNullOrWhiteSpace(step.successText))
                        yield return ShowMessage(step.successText);
                    yield break;
                }

                _failureCount++;
                _guideUI?.ShowFeedback(GetFeedbackText(step, validation.Failure));

                // Widen tolerance on 2nd failure
                if (_failureCount == 2 && _activeStep != null)
                {
                    _activeStep.tolerancePixels = Mathf.Max(_activeStep.tolerancePixels, _activeStep.widenedTolerancePixels);
                }

                if (_failureCount >= GetFailuresBeforeAssist())
                {
                    _assistedCompletion = true;
                    _guideUI?.ShowFeedback(step.assistText);
                    _guideUI?.PlayAssistAnimation(step.assistAnimationPrefab);
                    _guideUI?.Hide();
                    DefeatActiveTutorialEnemy();
                    yield break;
                }

                promptStartTime = Time.unscaledTime;
                showedIdleHint = false;
                showedStrongHint = false;
            }

            if (_baseDamagePauseRequested)
                yield return ShowBaseDamagePause(restorePrompt: true);

            float idleTime = Time.unscaledTime - promptStartTime;
            if (!showedIdleHint && idleTime >= GetIdleHintSeconds())
            {
                showedIdleHint = true;
                _guideUI?.ShowFeedback(step.idleHint);
                _guideUI?.PulseStartDot();
            }

            if (!showedStrongHint && idleTime >= GetStrongHintSeconds())
            {
                showedStrongHint = true;
                _guideUI?.ShowFeedback(step.strongHint);
                _guideUI?.AnimateGuidePath();
            }

            yield return null;
        }
    }

    private IEnumerator RunProtagonistWalkIn()
    {
        if (ProtagonistManager.Instance == null)
        {
            DebugLogger.LogWarning("[Level1Tutorial] ProtagonistManager not found, skipping walk-in");
            yield break;
        }

        _state = Level1TutorialState.WalkIn;
        
        // Ensure protagonist is created and walk it in
        ProtagonistManager.Instance.EnsureProtagonist(_protagonistEndPosition);
        ProtagonistManager.Instance.WalkInProtagonist(_protagonistEndPosition);

        // Wait for walk-in to complete
        float duration = Mathf.Max(0.01f, GetProtagonistWalkSeconds());
        yield return new WaitForSeconds(duration);
    }

    private IEnumerator ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            yield break;

        _guideUI?.ShowMessage(message, _firstManualSuccess);
        float duration = Mathf.Max(0.05f, GetMessageSeconds());
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator ShowBaseDamagePause(bool restorePrompt)
    {
        if (_baseDamagePauseShown || _baseDamagePauseRunning)
            yield break;

        _baseDamagePauseRequested = false;
        _baseDamagePauseShown = true;
        _baseDamagePauseRunning = true;

        bool enteredPause = TryEnterDialoguePause();
        if (restorePrompt)
            _activeEnemy?.FreezeThreat();

        yield return ShowMessage(GetBaseDamageText());

        if (restorePrompt && _state == Level1TutorialState.DrawPrompt && _activeStep != null)
            _guideUI?.ShowPrompt(_activeStep, _firstManualSuccess);
        else
            _guideUI?.Hide();

        if (enteredPause && GameManager.Instance != null)
            GameManager.Instance.ExitDialoguePause();

        _baseDamagePauseRunning = false;
    }

    private void DefeatActiveTutorialEnemy()
    {
        _activeEnemy?.Defeat();
        _activeEnemy = null;
    }

    private static bool TryEnterDialoguePause()
    {
        if (GameManager.Instance == null)
            return false;

        GameManager.Instance.EnterDialoguePause();
        return GameManager.Instance.CurrentState == GameState.Paused;
    }

    private IEnumerator SpawnTutorialEnemy(Level1TutorialStepSO step)
    {
        if (_waveSpawner == null)
        {
            DebugLogger.LogError("Level1InteractiveTutorialController: WaveSpawner is missing. Tutorial enemy cannot spawn.");
            yield break;
        }

        EnemyDataSO enemyData = step.enemyData != null ? step.enemyData : _fallbackTutorialEnemyData;
        if (enemyData == null)
        {
            DebugLogger.LogError($"Level1InteractiveTutorialController: Step '{step.promptId}' has no enemy data and no fallback enemy data.");
            yield break;
        }

        Enemy enemy = _waveSpawner.SpawnEnemy(enemyData, step.targetCharacter);
        if (enemy == null)
        {
            DebugLogger.LogError($"Level1InteractiveTutorialController: Failed to spawn tutorial enemy for step '{step.promptId}'.");
            yield break;
        }

        _activeEnemy = new Level1TutorialEnemyController(enemy);
        _activeEnemy.DisableContactDamage();
        _activeEnemy.MarkAsTutorialTarget(step.targetCharacter != null
            ? $"Draw {step.targetCharacter.characterID}"
            : "Draw");

        Vector3 end = step.stopPosition;
        if (end == Vector3.zero && _waveSpawner.transform.Find("TutorialEnemyStopPoint") != null)
            end = _waveSpawner.transform.Find("TutorialEnemyStopPoint").position;

        yield return WaitUntilEnemyIsVisible(enemy, end);

        float readableDelay = Mathf.Max(0f, step.promptFreezeDelaySeconds);
        float elapsed = 0f;
        while (enemy != null && !enemy.IsDying && elapsed < readableDelay)
        {
            if (end != Vector3.zero && enemy.transform.position.y <= end.y)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _activeEnemy.FreezeThreat();
    }

    private static IEnumerator WaitUntilEnemyIsVisible(Enemy enemy, Vector3 fallbackStopPosition)
    {
        Camera camera = Camera.main;
        if (camera == null)
            yield break;

        while (enemy != null && !enemy.IsDying)
        {
            Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position);
            bool visible = viewport.z > 0f
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0.82f && viewport.y <= 1f;

            if (visible)
                yield break;

            if (fallbackStopPosition != Vector3.zero && enemy.transform.position.y <= fallbackStopPosition.y)
                yield break;

            yield return null;
        }
    }

    private Level1TutorialValidationResult ValidateActivePrompt()
    {
        if (_activeStep == null || _activeStep.targetCharacter == null)
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.NoPrompt);

        List<List<Vector2>> templateStrokes = new()
        {
            new List<Vector2>(_activeStep.templatePoints ?? System.Array.Empty<Vector2>())
        };

        return _validator.Validate(
            _activeStep.targetCharacter.characterID,
            _lastRecognitionResult,
            _lastRecognitionPassed,
            _lastSubmittedStrokes,
            templateStrokes,
            _activeStep.tolerancePixels);
    }

    private static string GetFeedbackText(Level1TutorialStepSO step, Level1TutorialValidationFailure failure)
    {
        switch (failure)
        {
            case Level1TutorialValidationFailure.WrongCharacter:
                return step?.wrongCharacterFeedback ?? "Draw the shown syllable.";
            case Level1TutorialValidationFailure.DirectionMismatch:
                return step?.directionMismatchFeedback ?? "Follow the arrow direction.";
            case Level1TutorialValidationFailure.TooFewPoints:
            case Level1TutorialValidationFailure.PathMismatch:
                return step?.tooShortFeedback ?? "Draw the full shape.";
            case Level1TutorialValidationFailure.RecognitionFailed:
                return step?.recognitionFailedFeedback ?? "Try that shape again.";
            default:
                return string.Empty;
        }
    }

    private void HandleStrokesSubmitted(IReadOnlyList<List<Vector2>> strokes)
    {
        _lastSubmittedStrokes = new List<List<Vector2>>();
        if (strokes == null)
            return;

        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            _lastSubmittedStrokes.Add(stroke != null ? new List<Vector2>(stroke) : new List<Vector2>());
        }
    }

    private void HandleRecognitionResolved(
        RecognitionResult result,
        bool passedThreshold,
        float threshold)
    {
        if (_state != Level1TutorialState.DrawPrompt)
            return;

        _lastRecognitionResult = result;
        _lastRecognitionPassed = passedThreshold;
        _hasRecognitionForPrompt = true;
    }

    private void HandleBaseHit(int damage)
    {
        if (!IsLevelOneContext())
            return;

        if (_baseDamagePauseShown || _baseDamagePauseRunning)
            return;

        if (damage <= 0)
            return;

        if (_state == Level1TutorialState.DrawPrompt)
        {
            _baseDamagePauseRequested = true;
            return;
        }

        StartCoroutine(ShowBaseDamagePause(restorePrompt: false));
    }

    private void HandleGameOver()
    {
        TutorialRuntimeState.Clear();
        _activeEnemy = null;
        _state = Level1TutorialState.Gate;
        _guideUI?.Hide();
        RestoreHiddenTutorialUI();
    }

    private bool IsLevelOneContext()
    {
        if (TutorialRuntimeState.IsActiveForLevel(_requiredLevelNumber))
            return true;

        LevelConfigSO currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
        if (currentLevel != null)
            return currentLevel.levelNumber == _requiredLevelNumber;

        return PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1) == _requiredLevelNumber;
    }

    private void RequestSkip()
    {
        if (_firstManualSuccess)
            _skipRequested = true;
    }

    private void CompleteTutorial()
    {
        TutorialRuntimeState.Clear();
        _activeEnemy = null;
        _guideUI?.Hide();
        RestoreHiddenTutorialUI();
        ForceGameplayHudVisible();

        if (_assistedCompletion)
        {
            DebugLogger.Log("Level1InteractiveTutorialController: Tutorial completed with assist.");
            // TODO: Log analytics with assisted=true
        }

        // Interactive Level 1 tutorial is embedded in Level 1 gameplay and should
        // run every time Level 1 opens. The older overlay tutorial still owns the
        // one-time FTUE seen flag.
    }

    private void HideTutorialBlockedUI()
    {
        if (_uiHiddenForTutorial || _hideDuringTutorial == null || _hideDuringTutorial.Length == 0)
            return;

        _hiddenOriginalStates = new bool[_hideDuringTutorial.Length];
        _hiddenCanvasGroups = new CanvasGroup[_hideDuringTutorial.Length];
        _hiddenCanvasGroupExisted = new bool[_hideDuringTutorial.Length];
        _hiddenOriginalAlpha = new float[_hideDuringTutorial.Length];
        _hiddenOriginalInteractable = new bool[_hideDuringTutorial.Length];
        _hiddenOriginalBlocksRaycasts = new bool[_hideDuringTutorial.Length];

        for (int i = 0; i < _hideDuringTutorial.Length; i++)
        {
            GameObject target = _hideDuringTutorial[i];
            if (target == null)
                continue;

            _hiddenOriginalStates[i] = target.activeSelf;

            if (!target.activeInHierarchy)
                continue;

            _hiddenCanvasGroupExisted[i] = target.TryGetComponent(out CanvasGroup canvasGroup);
            if (!_hiddenCanvasGroupExisted[i])
                canvasGroup = target.AddComponent<CanvasGroup>();

            _hiddenCanvasGroups[i] = canvasGroup;
            _hiddenOriginalAlpha[i] = canvasGroup.alpha;
            _hiddenOriginalInteractable[i] = canvasGroup.interactable;
            _hiddenOriginalBlocksRaycasts[i] = canvasGroup.blocksRaycasts;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        _uiHiddenForTutorial = true;
    }

    private void RestoreHiddenTutorialUI()
    {
        if (!_uiHiddenForTutorial || _hideDuringTutorial == null || _hiddenOriginalStates == null)
            return;

        int count = Mathf.Min(_hideDuringTutorial.Length, _hiddenOriginalStates.Length);
        for (int i = 0; i < count; i++)
        {
            GameObject target = _hideDuringTutorial[i];
            if (target == null)
                continue;

            if (_hiddenCanvasGroups != null
                && i < _hiddenCanvasGroups.Length
                && _hiddenCanvasGroups[i] != null)
            {
                CanvasGroup canvasGroup = _hiddenCanvasGroups[i];
                canvasGroup.alpha = i < _hiddenOriginalAlpha.Length ? _hiddenOriginalAlpha[i] : 1f;
                canvasGroup.interactable = i < _hiddenOriginalInteractable.Length && _hiddenOriginalInteractable[i];
                canvasGroup.blocksRaycasts = i < _hiddenOriginalBlocksRaycasts.Length && _hiddenOriginalBlocksRaycasts[i];

                if (_hiddenCanvasGroupExisted != null
                    && i < _hiddenCanvasGroupExisted.Length
                    && !_hiddenCanvasGroupExisted[i])
                {
                    Destroy(canvasGroup);
                }
            }

            if (target.activeSelf != _hiddenOriginalStates[i])
                target.SetActive(_hiddenOriginalStates[i]);
        }

        _uiHiddenForTutorial = false;
        _hiddenOriginalStates = null;
        _hiddenCanvasGroups = null;
        _hiddenCanvasGroupExisted = null;
        _hiddenOriginalAlpha = null;
        _hiddenOriginalInteractable = null;
        _hiddenOriginalBlocksRaycasts = null;
    }

    public static void ForceGameplayHudVisible()
    {
        string[] names =
        {
            "HUDCanvas",
            "HUDRoot",
            "HUDLayer",
            "HeartsPanel",
            "WaveText",
            "GlyphCounter"
        };

        for (int i = 0; i < names.Length; i++)
        {
            GameObject target = FindSceneObjectByName(names[i]);
            if (target == null)
                continue;

            ActivateHierarchy(target);

            if (target.name == "HUDCanvas")
            {
                if (target.transform.localScale == Vector3.zero)
                    target.transform.localScale = Vector3.one;

                continue;
            }

            if (target.name == "HUDRoot" || target.name == "HUDLayer")
                continue;

            RestoreCanvasGroups(target);
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                return candidate.gameObject;
        }

        return null;
    }

    private static void ActivateHierarchy(GameObject target)
    {
        if (target == null)
            return;

        Transform current = target.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            current = current.parent;
        }
    }

    private static void RestoreCanvasGroups(GameObject target)
    {
        CanvasGroup[] groups = target.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            CanvasGroup group = groups[i];
            if (group == null)
                continue;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }
}
