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

    private List<List<Vector2>> _strokes = new List<List<Vector2>>();
    private List<Vector2> _currentPoints = new List<Vector2>();
    private Coroutine _timerRoutine;

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
        // Only capture strokes when the game is actively playing
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        // A new finger down cancels the pending multi-stroke timer
        if (_timerRoutine != null) StopCoroutine(_timerRoutine);
        _currentPoints.Clear();
        EventBus.RaiseDrawingStarted();
        _canvas.BeginStroke();
    }

    private void OnFingerMove(Finger finger)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing) return;
        _currentPoints.Add(finger.screenPosition);
        _canvas.AddPoint(finger.screenPosition);
    }

    private void OnFingerUp(Finger finger)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing) return;

        // Only accept strokes with enough points to be meaningful
        if (_currentPoints.Count >= _config.minimumPointCount)
            _strokes.Add(new List<Vector2>(_currentPoints));

        _canvas.EndStroke();
        _currentPoints.Clear();

        // Begin the window timer. If no new stroke starts before it expires,
        // submit everything collected so far for recognition.
        _timerRoutine = StartCoroutine(MultiStrokeTimerRoutine());
    }

    private IEnumerator MultiStrokeTimerRoutine()
    {
        yield return new WaitForSeconds(_config.multiStrokeWindowSeconds);
        SubmitForRecognition();
    }

    private void SubmitForRecognition()
    {
        if (_strokes.Count == 0) return;
        List<Vector2> allPoints = new List<Vector2>();
        foreach (var stroke in _strokes) allPoints.AddRange(stroke);
        _strokes.Clear();
        _canvas.ClearCanvas();
        RecognitionManager.Instance.Recognize(allPoints);
    }
}