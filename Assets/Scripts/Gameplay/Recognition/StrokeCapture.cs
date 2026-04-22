using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class StrokeCapture : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private RecognitionConfigSO _config;
    [SerializeField] private DrawingCanvas _canvas;

    [Header("Edge Case Settings")]
    [Tooltip("Minimum stroke duration in seconds. "
    + "Below this = accidental tap.")]
    [SerializeField] private float _minStrokeDuration = 0.1f;

    [Tooltip("Seconds of no input mid-stroke before "
        + "auto-completing.")]
    [SerializeField] private float _strokeTimeoutSeconds = 2f;

    private List<List<Vector2>> _strokes = new List<List<Vector2>>();
    private List<Vector2> _currentPoints = new List<Vector2>();
    private float _strokeStartTime;
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

        if (finger.index != 0)
        {
            if (_isDrawing)
            {
                DebugLogger.Log(
                    "StrokeCapture: Multitouch detected, "
                    + "rejecting stroke");
                _isDrawing = false;
                CancelCurrentStroke();
            }
            return;
        }

        _isDrawing = true;

        _multiStrokeTimerEndTime = -1f;
        _strokeTimeoutEndTime = -1f;

        _currentPoints.Clear();
        _strokeStartTime = Time.time;
        EventBus.RaiseDrawingStarted();
        _canvas.BeginStroke();

        _strokeTimeoutEndTime = Time.time + _strokeTimeoutSeconds;
    }

    private void OnFingerMove(Finger finger)
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.AcceptsDrawingInput) return;

        if (finger.index != 0 || !_isDrawing) return;

        Vector2 pos = finger.screenPosition;

        float marginX = Screen.width * 0.05f;
        float marginY = Screen.height * 0.05f;
        if (pos.x < marginX || pos.x > Screen.width - marginX ||
            pos.y < marginY || pos.y > Screen.height - marginY)
            return;

        _currentPoints.Add(pos);
        _canvas.AddPoint(pos);

        _strokeTimeoutEndTime = Time.time + _strokeTimeoutSeconds;
    }

    private void OnFingerUp(Finger finger)
    {
        if (finger.index != 0 || !_isDrawing) return;

        _isDrawing = false;
        _strokeTimeoutEndTime = -1f;

        CompleteCurrentStroke();
    }

    private void CompleteCurrentStroke()
    {
        float duration = Time.time - _strokeStartTime;

        if (duration < _minStrokeDuration)
        {
            DebugLogger.Log(
                $"StrokeCapture: Stroke too short "
                + $"({duration:F3}s), discarding");
            _canvas.EndStroke();
            _canvas.ClearCanvas();
            _currentPoints.Clear();
            return;
        }

        if (_currentPoints.Count < _config.minimumPointCount)
        {
            DebugLogger.Log(
                $"StrokeCapture: Only {_currentPoints.Count} "
                + $"points (min: {_config.minimumPointCount}), "
                + $"discarding");
            _canvas.EndStroke();
            _canvas.ClearCanvas();
            _currentPoints.Clear();
            return;
        }

        _strokes.Add(new List<Vector2>(_currentPoints));
        _canvas.EndStroke();
        _currentPoints.Clear();

        StartMultiStrokeTimer(_config.multiStrokeWindowSeconds);
    }

    private void CancelCurrentStroke()
    {
        _currentPoints.Clear();
        _canvas.EndStroke();
        _canvas.ClearCanvas();
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

        List<Vector2> allPoints = new List<Vector2>();
        foreach (var stroke in _strokes)
            allPoints.AddRange(stroke);

        _strokes.Clear();
        _pendingRecognitionSubmit = false;
        _canvas.ClearCanvas();

        RecognitionManager.Instance.Recognize(allPoints);
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

            if (_currentPoints.Count >= _config.minimumPointCount)
            {
                CompleteCurrentStroke();
            }
            else
            {
                _currentPoints.Clear();
                _canvas.EndStroke();
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
        return results.Count > 0;
    }
}
