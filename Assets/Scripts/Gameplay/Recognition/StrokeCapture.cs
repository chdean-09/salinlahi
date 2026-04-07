using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    private int _activeTouchCount;
    private Coroutine _strokeTimeoutRoutine;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        Touch.onFingerDown += OnFingerDown;
        Touch.onFingerMove += OnFingerMove;
        Touch.onFingerUp += OnFingerUp;
    }

    private void OnDisable()
    {
        Touch.onFingerDown -= OnFingerDown;
        Touch.onFingerMove -= OnFingerMove;
        Touch.onFingerUp -= OnFingerUp;
        EnhancedTouchSupport.Disable();
    }

    private void OnFingerDown(Finger finger)
    {
        // Block all input during GameOver or Paused
        if (GameManager.Instance.CurrentState
            != GameState.Playing) return;

        _activeTouchCount++;

        // Reject multitouch: only process single-finger strokes
        if (_activeTouchCount > 1)
        {
            DebugLogger.Log(
                "StrokeCapture: Multitouch detected, "
                + "rejecting stroke");
            CancelCurrentStroke();
            return;
        }

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
        if (GameManager.Instance.CurrentState
            != GameState.Playing) return;

        // Ignore if multitouch is active
        if (_activeTouchCount > 1) return;

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
        if (GameManager.Instance.CurrentState
            != GameState.Playing) return;

        _activeTouchCount = Mathf.Max(0, _activeTouchCount - 1);

        // If other fingers are still down, ignore this lift
        if (_activeTouchCount > 0) return;

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
            _currentPoints.Clear();
            return;
        }

        // Stroke is valid. Add to multi-stroke list.
        _strokes.Add(new List<Vector2>(_currentPoints));
        _canvas.EndStroke();
        _currentPoints.Clear();

        // Start the multi-stroke window timer
        _timerRoutine = StartCoroutine(
            MultiStrokeTimerRoutine());
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

    private IEnumerator MultiStrokeTimerRoutine()
    {
        yield return new WaitForSeconds(_config.multiStrokeWindowSeconds);
        SubmitForRecognition();
    }

    private void SubmitForRecognition()
    {
        // Guard: do not submit during non-playing states
        if (GameManager.Instance.CurrentState
            != GameState.Playing)
        {
            _strokes.Clear();
            return;
        }

        if (_strokes.Count == 0) return;

        List<Vector2> allPoints = new List<Vector2>();
        foreach (var stroke in _strokes)
            allPoints.AddRange(stroke);

        _strokes.Clear();
        _canvas.ClearCanvas();

        RecognitionManager.Instance.Recognize(allPoints);
    }
}