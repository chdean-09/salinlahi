using System.Collections;
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
    private Coroutine _timerRoutine;
    private float _strokeStartTime;
    private Coroutine _strokeTimeoutRoutine;
    private bool _isDrawing;
    private bool _pendingRecognitionSubmit;
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

    private void OnFingerDown(Finger finger)
    {
        // Block all input during GameOver or Paused
        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameState.Playing) return;

        // Ignore touches that hit UI (pause button, menus, etc.).
        // Prevents UI taps from being treated as drawing strokes.
        if (IsScreenPositionOverUI(finger.screenPosition))
            return;

        // If a recognition submit was deferred during pause, flush it before
        // beginning a new stroke so the previous drawing is not lost.
        if (_pendingRecognitionSubmit && !_isDrawing)
            SubmitForRecognition();

        // Only accept the primary finger (index 0).
        // On desktop, mouse buttons beyond left-click create
        // higher-index fingers via touch simulation.
        // On mobile, index > 0 means genuine multitouch.
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

        // Cancel the pending multi-stroke timer
        if (_timerRoutine != null)
            StopCoroutine(_timerRoutine);

        // Cancel the stroke timeout timer
        if (_strokeTimeoutRoutine != null)
            StopCoroutine(_strokeTimeoutRoutine);

        _currentPoints.Clear();
        _strokeStartTime = Time.time;
        EventBus.RaiseDrawingStarted();
        _canvas.BeginStroke();

        // Start the no-input timeout
        _strokeTimeoutRoutine = StartCoroutine(
            StrokeTimeoutRoutine());
    }

    private System.Collections.IEnumerator
    StrokeTimeoutRoutine()
    {
        yield return new WaitForSeconds(_strokeTimeoutSeconds);

        DebugLogger.Log(
            "StrokeCapture: Stroke timeout, auto-completing");

        // Treat as if the finger was lifted
        CompleteCurrentStroke();
    }


    private void OnFingerMove(Finger finger)
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameState.Playing) return;

        if (finger.index != 0 || !_isDrawing) return;

        Vector2 pos = finger.screenPosition;

        // Safe area clamping: ignore edges
        float marginX = Screen.width * 0.05f;
        float marginY = Screen.height * 0.05f;
        if (pos.x < marginX || pos.x > Screen.width - marginX ||
            pos.y < marginY || pos.y > Screen.height - marginY)
            return;

        _currentPoints.Add(pos);
        _canvas.AddPoint(pos);

        // Reset the stroke timeout timer on every move
        if (_strokeTimeoutRoutine != null)
            StopCoroutine(_strokeTimeoutRoutine);
        _strokeTimeoutRoutine = StartCoroutine(
            StrokeTimeoutRoutine());
    }


    private void OnFingerUp(Finger finger)
    {
        if (finger.index != 0 || !_isDrawing) return;

        _isDrawing = false;

        // Stop the stroke timeout timer
        if (_strokeTimeoutRoutine != null)
        {
            StopCoroutine(_strokeTimeoutRoutine);
            _strokeTimeoutRoutine = null;
        }

        CompleteCurrentStroke();
    }

    private void CompleteCurrentStroke()
    {
        float duration = Time.time - _strokeStartTime;

        // Reject strokes shorter than 100ms (accidental tap)
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

        // Reject strokes with too few points
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

        // Stroke is valid. Add to multi-stroke list.
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

        if (_strokeTimeoutRoutine != null)
        {
            StopCoroutine(_strokeTimeoutRoutine);
            _strokeTimeoutRoutine = null;
        }
    }

    private IEnumerator MultiStrokeTimerRoutine(float waitSeconds)
    {
        yield return new WaitForSeconds(waitSeconds);
        _timerRoutine = null;
        _multiStrokeTimerEndScaledTime = -1f;
        _pausedMultiStrokeRemainingSeconds = -1f;
        SubmitForRecognition();
    }

    private void SubmitForRecognition()
    {
        // Guard: do not submit during non-playing states
        if (GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameState.Playing)
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
        // If pause landed before finger-up dispatch, end the active stroke so it
        // can still participate in recognition after resume.
        if (_isDrawing)
        {
            _isDrawing = false;

            if (_strokeTimeoutRoutine != null)
            {
                StopCoroutine(_strokeTimeoutRoutine);
                _strokeTimeoutRoutine = null;
            }

            if (_currentPoints.Count >= _config.minimumPointCount)
            {
                CompleteCurrentStroke();
            }
            else
            {
                // Pause can interrupt a new tap before any real stroke data exists.
                // Only end that in-progress line; do not clear previously completed
                // strokes waiting for recognition.
                _currentPoints.Clear();
                _canvas.EndStroke();
            }
        }

        // Freeze active multi-stroke countdown and resume it later.
        if (_timerRoutine != null)
        {
            float remaining = _multiStrokeTimerEndScaledTime - Time.time;
            _pausedMultiStrokeRemainingSeconds = Mathf.Max(0f, remaining);
            StopCoroutine(_timerRoutine);
            _timerRoutine = null;
            _multiStrokeTimerEndScaledTime = -1f;
        }
    }

    private void StartMultiStrokeTimer(float seconds)
    {
        if (_timerRoutine != null)
            StopCoroutine(_timerRoutine);

        float waitSeconds = Mathf.Max(0f, seconds);
        _multiStrokeTimerEndScaledTime = Time.time + waitSeconds;
        _pausedMultiStrokeRemainingSeconds = -1f;
        _timerRoutine = StartCoroutine(MultiStrokeTimerRoutine(waitSeconds));
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
