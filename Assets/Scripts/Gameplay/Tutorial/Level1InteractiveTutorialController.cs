using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Level1InteractiveTutorialController : MonoBehaviour
{
    public const string RequiredSceneName = "Level_01_Tutorial";

    [Header("Guards")]
    [SerializeField] private string _requiredSceneName = RequiredSceneName;
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
    [SerializeField] private Transform _protagonist;
    [SerializeField] private Transform _protagonistWalkStart;
    [SerializeField] private Transform _protagonistWalkEnd;
    [SerializeField] private GameObject[] _hideDuringTutorial;

    [Header("Animation Timing")]
    [SerializeField] private float _protagonistWalkSeconds = 1.75f;

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
    private bool _uiHiddenForTutorial;

    public static bool IsCombatOverrideActive { get; private set; }
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
        string activeScene = SceneManager.GetActiveScene().name;
        DebugLogger.Log($"Level1InteractiveTutorialController.Awake: Scene='{activeScene}', Required='{_requiredSceneName}'");
        
        if (activeScene != _requiredSceneName)
        {
            DebugLogger.Log($"Level1InteractiveTutorialController: Scene mismatch. Disabling.");
            enabled = false;
            return;
        }

        // NOTE: Don't check CurrentLevel here — it may not be set yet.
        // The full validation happens in ShouldRunFor() which is called later.

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
        StrokeCapture.OnStrokesSubmitted += HandleStrokesSubmitted;
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
        EventBus.OnBaseHit -= HandleBaseHit;
        StrokeCapture.OnStrokesSubmitted -= HandleStrokesSubmitted;
        IsCombatOverrideActive = false;
        RestoreHiddenTutorialUI();
    }

    public static bool ShouldRunForContext(string sceneName, int levelNumber)
    {
        return sceneName == RequiredSceneName
            && levelNumber == LevelTutorialProgress.TutorialLevelNumber;
    }

    public bool ShouldRunFor(LevelConfigSO levelConfig)
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        bool hasSeen = LevelTutorialProgress.HasSeenLevel1Tutorial();
        
        DebugLogger.Log($"Level1InteractiveTutorialController.ShouldRunFor: " +
            $"levelConfig={(levelConfig != null ? levelConfig.name : "null")}, " +
            $"scene='{activeSceneName}', required='{_requiredSceneName}', " +
            $"levelNumber={(levelConfig?.levelNumber.ToString() ?? "N/A")}, required={_requiredLevelNumber}, " +
            $"hasSeen={hasSeen}");
        
        bool result = levelConfig != null
            && activeSceneName == _requiredSceneName
            && levelConfig.levelNumber == _requiredLevelNumber;
        
        DebugLogger.Log($"Level1InteractiveTutorialController.ShouldRunFor: returning {result}");
        return result;
    }

    public IEnumerator PlayIfNeeded(LevelConfigSO levelConfig)
    {
        if (!ShouldRunFor(levelConfig))
            yield break;

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
            sceneName != _requiredSceneName ||
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
        IsCombatOverrideActive = true;
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
        if (_protagonist == null || _protagonistWalkStart == null || _protagonistWalkEnd == null)
            yield break;

        _state = Level1TutorialState.WalkIn;
        _protagonist.position = _protagonistWalkStart.position;

        float duration = Mathf.Max(0.01f, GetProtagonistWalkSeconds());
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-out during final 20%
            float eased = t < 0.8f ? t : Mathf.Lerp(0.8f, 1f, 1f - Mathf.Pow(1f - ((t - 0.8f) / 0.2f), 2f));
            _protagonist.position = Vector3.Lerp(
                _protagonistWalkStart.position,
                _protagonistWalkEnd.position,
                eased);
            yield return null;
        }

        _protagonist.position = _protagonistWalkEnd.position;
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
        if (SceneManager.GetActiveScene().name != _requiredSceneName)
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

    private void RequestSkip()
    {
        if (_firstManualSuccess)
            _skipRequested = true;
    }

    private void CompleteTutorial()
    {
        IsCombatOverrideActive = false;
        _activeEnemy = null;
        _guideUI?.Hide();
        RestoreHiddenTutorialUI();

        if (_assistedCompletion)
        {
            DebugLogger.Log("Level1InteractiveTutorialController: Tutorial completed with assist.");
            // TODO: Log analytics with assisted=true
        }

        // Interactive Level 1 tutorial is embedded in Level_01_Tutorial and should run
        // every time that scene opens. The older overlay tutorial still owns the
        // one-time FTUE seen flag.
    }

    private void HideTutorialBlockedUI()
    {
        if (_uiHiddenForTutorial || _hideDuringTutorial == null || _hideDuringTutorial.Length == 0)
            return;

        _hiddenOriginalStates = new bool[_hideDuringTutorial.Length];
        for (int i = 0; i < _hideDuringTutorial.Length; i++)
        {
            GameObject target = _hideDuringTutorial[i];
            if (target == null)
                continue;

            _hiddenOriginalStates[i] = target.activeSelf;
            target.SetActive(false);
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
            if (target != null)
                target.SetActive(_hiddenOriginalStates[i]);
        }

        _uiHiddenForTutorial = false;
        _hiddenOriginalStates = null;
    }
}
