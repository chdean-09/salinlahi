using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class StrokeCapture : MonoBehaviour
{
    public static event System.Action<IReadOnlyList<List<Vector2>>> OnStrokesSubmitted;

    [Header("Configuration")]
    [SerializeField] private RecognitionConfigSO _config;
    [SerializeField] private DrawingCanvas _canvas;

    [Header("Edge Case Settings")]
    [Tooltip("Seconds of no input mid-stroke before "
        + "auto-completing.")]
    [SerializeField] private float _strokeTimeoutSeconds = 2f;

    [Tooltip("Fraction of screen width/height to ignore at each edge (0 = full screen, 0.05 = 5% margin).")]
    [SerializeField] [Range(0f, 0.25f)] private float _edgeMarginPercent = 0f;

    private List<List<Vector2>> _strokes = new List<List<Vector2>>();
    private CapturedStroke _currentStroke;
    private Finger _activeFinger;
    private double _lastProcessedTouchTime = double.MinValue;
    private bool _isDrawing;
    private bool _pendingRecognitionSubmit;

    private double _strokeTimeoutEndTime = -1d;
    private double _multiStrokeTimerEndTime = -1d;
    private double _pausedMultiStrokeRemainingSeconds = -1d;

    private void Awake()
    {
        if (_config == null)
            DebugLogger.LogError("StrokeCapture: RecognitionConfigSO is not assigned. Drawing input will be disabled.");

        if (_canvas == null)
            DebugLogger.LogError("StrokeCapture: DrawingCanvas is not assigned. Drawing input will be disabled.");
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        EnableMouseAsTouchOnDesktop();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerMove += OnFingerMove;
        Touch.onFingerUp += OnFingerUp;
        EventBus.OnGamePaused += HandleGamePaused;
        EventBus.OnGameResumed += HandleGameResumed;
        EventBus.OnLevelAttemptAborted += HandleLevelAttemptAborted;
    }

    /// <summary>
    /// Drawing is the only combat input and it is captured exclusively from EnhancedTouch
    /// fingers, so on a machine with no touchscreen the recognizer never receives a stroke:
    /// menus respond to the mouse but the player can never attack. That makes the Editor and
    /// any desktop build unplayable past the first tutorial prompt, which also blocks manual
    /// playtesting of the core loop. TouchSimulation republishes mouse events as touches, so
    /// the same finger code path runs unchanged.
    ///
    /// Device builds are untouched: the guard compiles this out everywhere except the Editor
    /// and desktop players, and even there it defers to a real touchscreen when one exists.
    /// </summary>
    private static void EnableMouseAsTouchOnDesktop()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (UnityEngine.InputSystem.Touchscreen.current == null && TouchSimulation.instance == null)
            TouchSimulation.Enable();
#endif
    }

    private void OnDisable()
    {
        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerMove -= OnFingerMove;
        Touch.onFingerUp -= OnFingerUp;
        EventBus.OnGamePaused -= HandleGamePaused;
        EventBus.OnGameResumed -= HandleGameResumed;
        EventBus.OnLevelAttemptAborted -= HandleLevelAttemptAborted;
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        if (_config == null || _canvas == null)
            return;

        // Both timers below run on unscaled time, so Time.timeScale = 0 does not stop
        // them. A stroke or multi-stroke window armed just before the pause — or
        // re-armed during it — would otherwise expire and submit behind the pause menu.
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
            return;

        if (_isDrawing && _activeFinger != null)
            ProcessTouchHistory(_activeFinger);

        if (_strokeTimeoutEndTime > 0d && Time.unscaledTimeAsDouble >= _strokeTimeoutEndTime)
        {
            _strokeTimeoutEndTime = -1d;
            DebugLogger.Log("StrokeCapture: Stroke timeout, auto-completing");
            CompleteCurrentStroke();
        }

        if (_multiStrokeTimerEndTime > 0d && Time.unscaledTimeAsDouble >= _multiStrokeTimerEndTime)
        {
            _multiStrokeTimerEndTime = -1d;
            _pausedMultiStrokeRemainingSeconds = -1d;
            SubmitForRecognition();
        }
    }

    private void OnFingerDown(Finger finger)
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.AcceptsDrawingInput) return;

        if (_config == null || _canvas == null)
            return;

        if (TutorialRuntimeState.IsDrawingInputLocked || ChallengeRuntimeState.IsDrawingInputLocked)
            return;

        if (!TutorialRuntimeState.IsCombatOverrideActive && IsScreenPositionOverUI(finger.screenPosition))
            return;

        if (_pendingRecognitionSubmit && !_isDrawing)
            SubmitForRecognition();

        if (_isDrawing)
        {
            DebugLogger.Log("StrokeCapture: Ignoring additional finger while active stroke is in progress.");
            return;
        }

        _isDrawing = true;
        _activeFinger = finger;

        Touch currentTouch = finger.currentTouch;
        int touchId = currentTouch.valid ? currentTouch.touchId : -1;
        double startTime = currentTouch.valid ? currentTouch.time : Time.realtimeSinceStartupAsDouble;
        Vector2 startPosition = currentTouch.valid ? currentTouch.screenPosition : finger.screenPosition;

        _currentStroke = new CapturedStroke(finger.index, touchId, startTime);
        _currentStroke.Begin(startPosition);
        _lastProcessedTouchTime = startTime;

        _multiStrokeTimerEndTime = -1d;
        _strokeTimeoutEndTime = -1d;

        EventBus.RaiseDrawingStarted();
        _canvas.BeginStroke();
        _canvas.AddPoint(startPosition);

        _strokeTimeoutEndTime = Time.unscaledTimeAsDouble + _strokeTimeoutSeconds;
    }

    private void OnFingerMove(Finger finger)
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.AcceptsDrawingInput) return;

        if (TutorialRuntimeState.IsDrawingInputLocked || ChallengeRuntimeState.IsDrawingInputLocked) return;

        ProcessTouchHistory(finger);
    }

    private void OnFingerUp(Finger finger)
    {
        if (!_isDrawing || finger != _activeFinger)
            return;

        ProcessTouchHistory(finger);

        Touch currentTouch = finger.currentTouch;
        if (currentTouch.valid && currentTouch.time > _lastProcessedTouchTime)
            ProcessTouchSample(currentTouch.screenPosition, currentTouch.time);

        _isDrawing = false;
        _activeFinger = null;
        _strokeTimeoutEndTime = -1d;

        CompleteCurrentStroke();
    }

    private void CompleteCurrentStroke()
    {
        if (_currentStroke == null)
            return;

        List<Vector2> rawPoints = _currentStroke.CloneRawPoints();

        if (StrokeValidation.IsTapLikeStroke(
            rawPoints,
            _config.minimumStrokePathLengthPixels,
            _config.minimumStrokeBoundsPixels))
        {
            DebugLogger.Log("StrokeCapture: Tap-like stroke discarded.");
            _isDrawing = false;
            _activeFinger = null;
            _canvas.DiscardCurrentStroke();
            _currentStroke.Clear();
            _currentStroke = null;
            return;
        }

        _strokes.Add(rawPoints);
        _isDrawing = false;
        _activeFinger = null;
        _canvas.EndStroke();
        _currentStroke.Clear();
        _currentStroke = null;

        StartMultiStrokeTimer(_config.multiStrokeWindowSeconds);
    }

    private void SubmitForRecognition()
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.AcceptsDrawingInput)
        {
            _pendingRecognitionSubmit = _strokes.Count > 0;
            return;
        }

        if (_strokes.Count == 0) return;

        List<List<Vector2>> strokesForRecognition = new List<List<Vector2>>();
        for (int i = 0; i < _strokes.Count; i++)
            strokesForRecognition.Add(new List<Vector2>(_strokes[i]));

        _strokes.Clear();
        _pendingRecognitionSubmit = false;
        _canvas.ClearCanvas();

        OnStrokesSubmitted?.Invoke(strokesForRecognition);
        RecognitionManager.Instance.Recognize(strokesForRecognition);
    }

    private void HandleGameResumed()
    {
        if (_pausedMultiStrokeRemainingSeconds > 0f
            && _strokes.Count > 0
            && !_isDrawing)
        {
            StartMultiStrokeTimer(_pausedMultiStrokeRemainingSeconds);
            _pausedMultiStrokeRemainingSeconds = -1d;
            return;
        }

        if (!_pendingRecognitionSubmit || _isDrawing)
            return;

        SubmitForRecognition();
    }

    private void HandleGamePaused()
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            _strokeTimeoutEndTime = -1d;

            if (_currentStroke != null)
            {
                CompleteCurrentStroke();
            }
            else
            {
                _activeFinger = null;
                _canvas.DiscardCurrentStroke();
            }
        }

        if (_multiStrokeTimerEndTime > 0d)
        {
            double remaining = _multiStrokeTimerEndTime - Time.unscaledTimeAsDouble;
            _pausedMultiStrokeRemainingSeconds = System.Math.Max(0d, remaining);
            _multiStrokeTimerEndTime = -1d;
        }

        if (_strokeTimeoutEndTime > 0d)
            _strokeTimeoutEndTime = -1d;
    }

    /// <summary>
    /// SALIN-141. Drops every scrap of the aborted attempt's drawing state so a
    /// restarted level cannot inherit a half-drawn stroke, a parked multi-stroke
    /// window, or a queued recognition submit.
    /// </summary>
    private void HandleLevelAttemptAborted()
    {
        _isDrawing = false;
        _activeFinger = null;
        _pendingRecognitionSubmit = false;
        _strokeTimeoutEndTime = -1d;
        _multiStrokeTimerEndTime = -1d;
        _pausedMultiStrokeRemainingSeconds = -1d;
        _lastProcessedTouchTime = double.MinValue;

        if (_currentStroke != null)
        {
            _currentStroke.Clear();
            _currentStroke = null;
        }

        _strokes.Clear();

        if (_canvas != null)
            _canvas.ClearCanvas();
    }

    private void ProcessTouchHistory(Finger finger)
    {
        if (!_isDrawing || _currentStroke == null || finger != _activeFinger)
            return;

        foreach (Touch touch in finger.touchHistory)
        {
            if (!touch.valid || touch.time <= _lastProcessedTouchTime)
                continue;

            ProcessTouchSample(touch.screenPosition, touch.time);
        }
    }

    private void ProcessTouchSample(Vector2 screenPosition, double sampleTime)
    {
        if (!IsInsideDrawableScreenArea(screenPosition))
        {
            _lastProcessedTouchTime = sampleTime;
            return;
        }

        bool addedRaw = _currentStroke.AddRawSample(
            screenPosition,
            _config.rawSampleMinDistancePixels);

        if (addedRaw)
        {
            _currentStroke.RebuildVisualCurve(
                _config.visualSampleSpacingPixels,
                _config.maxVisualSamplesPerSegment);

            _canvas.SetPoints(_currentStroke.VisualPoints);
        }

        _lastProcessedTouchTime = sampleTime;
        _strokeTimeoutEndTime = Time.unscaledTimeAsDouble + _strokeTimeoutSeconds;
    }

    private bool IsInsideDrawableScreenArea(Vector2 pos)
    {
        float marginX = Screen.width * _edgeMarginPercent;
        float marginY = Screen.height * _edgeMarginPercent;
        return pos.x >= marginX
            && pos.x <= Screen.width - marginX
            && pos.y >= marginY
            && pos.y <= Screen.height - marginY;
    }

    private void StartMultiStrokeTimer(double seconds)
    {
        double waitSeconds = System.Math.Max(0d, seconds);
        _multiStrokeTimerEndTime = Time.unscaledTimeAsDouble + waitSeconds;
        _pausedMultiStrokeRemainingSeconds = -1d;
    }

    private bool IsScreenPositionOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
            if (result.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null)
                return true;

        return false;
    }
}
