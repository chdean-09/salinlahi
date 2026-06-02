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

    private float _strokeTimeoutEndTime = -1f;
    private float _multiStrokeTimerEndTime = -1f;
    private float _multiStrokeTimerEndScaledTime = -1f;
    private float _pausedMultiStrokeRemainingSeconds = -1f;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerMove += OnFingerMove;
        Touch.onFingerUp += OnFingerUp;
        EventBus.OnGamePaused += HandleGamePaused;
        EventBus.OnGameResumed += HandleGameResumed;
    }

    private void OnDisable()
    {
        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerMove -= OnFingerMove;
        Touch.onFingerUp -= OnFingerUp;
        EventBus.OnGamePaused -= HandleGamePaused;
        EventBus.OnGameResumed -= HandleGameResumed;
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        if (_isDrawing && _activeFinger != null)
            ProcessTouchHistory(_activeFinger);

        if (_strokeTimeoutEndTime > 0f && Time.time >= _strokeTimeoutEndTime)
        {
            _strokeTimeoutEndTime = -1f;
            DebugLogger.Log("StrokeCapture: Stroke timeout, auto-completing");
            CompleteCurrentStroke();
        }

        if (_multiStrokeTimerEndTime > 0f && Time.time >= _multiStrokeTimerEndTime)
        {
            _multiStrokeTimerEndTime = -1f;
            _multiStrokeTimerEndScaledTime = -1f;
            _pausedMultiStrokeRemainingSeconds = -1f;
            SubmitForRecognition();
        }
    }

    private void OnFingerDown(Finger finger)
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.AcceptsDrawingInput) return;

        if (IsScreenPositionOverUI(finger.screenPosition))
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

        _multiStrokeTimerEndTime = -1f;
        _strokeTimeoutEndTime = -1f;

        EventBus.RaiseDrawingStarted();
        _canvas.BeginStroke();
        _canvas.AddPoint(startPosition);

        _strokeTimeoutEndTime = Time.time + _strokeTimeoutSeconds;
    }

    private void OnFingerMove(Finger finger)
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.AcceptsDrawingInput) return;

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
        _strokeTimeoutEndTime = -1f;

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

    private void CancelCurrentStroke()
    {
        _currentStroke?.Clear();
        _currentStroke = null;
        _activeFinger = null;
        _isDrawing = false;
        _canvas.DiscardCurrentStroke();
        _strokeTimeoutEndTime = -1f;
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
            _pausedMultiStrokeRemainingSeconds = -1f;
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
            _strokeTimeoutEndTime = -1f;

            if (_currentStroke != null && !StrokeValidation.IsTapLikeStroke(
                _currentStroke.RawPoints,
                _config.minimumStrokePathLengthPixels,
                _config.minimumStrokeBoundsPixels))
            {
                CompleteCurrentStroke();
            }
            else
            {
                _currentStroke?.Clear();
                _currentStroke = null;
                _activeFinger = null;
                _canvas.DiscardCurrentStroke();
            }
        }

        if (_multiStrokeTimerEndTime > 0f)
        {
            float remaining = _multiStrokeTimerEndScaledTime - Time.time;
            _pausedMultiStrokeRemainingSeconds = Mathf.Max(0f, remaining);
            _multiStrokeTimerEndTime = -1f;
            _multiStrokeTimerEndScaledTime = -1f;
        }

        if (_strokeTimeoutEndTime > 0f)
            _strokeTimeoutEndTime = -1f;
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

        IReadOnlyList<Vector2> rawPoints = _currentStroke.RawPoints;
        Vector2 previousRaw = rawPoints.Count > 0
            ? rawPoints[rawPoints.Count - 1]
            : screenPosition;

        bool addedRaw = _currentStroke.AddRawSample(
            screenPosition,
            _config.rawSampleMinDistancePixels);

        if (addedRaw)
        {
            int visualStart = _currentStroke.VisualPoints.Count;
            _currentStroke.AddVisualSegment(
                previousRaw,
                screenPosition,
                _config.visualSampleSpacingPixels,
                _config.maxVisualSamplesPerSegment);

            AddNewVisualPointsToCanvas(visualStart);
        }

        _lastProcessedTouchTime = sampleTime;
        _strokeTimeoutEndTime = Time.time + _strokeTimeoutSeconds;
    }

    private void AddNewVisualPointsToCanvas(int visualStart)
    {
        if (_currentStroke == null)
            return;

        IReadOnlyList<Vector2> visualPoints = _currentStroke.VisualPoints;
        if (visualStart >= visualPoints.Count)
            return;

        var pointsToRender = new List<Vector2>(visualPoints.Count - visualStart);
        for (int i = visualStart; i < visualPoints.Count; i++)
            pointsToRender.Add(visualPoints[i]);

        _canvas.AddPoints(pointsToRender);
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

    private void StartMultiStrokeTimer(float seconds)
    {
        float waitSeconds = Mathf.Max(0f, seconds);
        _multiStrokeTimerEndTime = Time.time + waitSeconds;
        _multiStrokeTimerEndScaledTime = Time.time + waitSeconds;
        _pausedMultiStrokeRemainingSeconds = -1f;
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
