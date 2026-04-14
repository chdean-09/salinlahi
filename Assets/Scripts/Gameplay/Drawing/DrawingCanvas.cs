using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Renders stroke points as a visible line using LineRenderer.
// Sprint 4: replace with a render texture or GPU line for better visual quality.

public class DrawingCanvas : MonoBehaviour
{
    [Header("Line Appearance")]
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private float _lineWidth = 0.05f;
    [SerializeField] private Color _strokeColor = Color.white;

    [Header("Clear")]
    [SerializeField] private float _clearDelaySeconds = 0.3f;

    private LineRenderer _currentLine;
    private List<LineRenderer> _activeLines = new List<LineRenderer>();
    private Camera _cam;

    private void Awake() => _cam = Camera.main;

    public void BeginStroke()
    {
        GameObject go = new GameObject("Stroke");
        go.transform.SetParent(transform);
        _currentLine = go.AddComponent<LineRenderer>();
        _currentLine.material = _lineMaterial;
        _currentLine.startWidth = _lineWidth;
        _currentLine.endWidth = _lineWidth;
        _currentLine.startColor = _strokeColor;
        _currentLine.endColor = _strokeColor;
        _currentLine.positionCount = 0;
        _currentLine.useWorldSpace = true;
        _activeLines.Add(_currentLine);
    }

    public void AddPoint(Vector2 screenPos)
    {
        if (_currentLine == null) return;
        if (float.IsInfinity(screenPos.x) || float.IsInfinity(screenPos.y) ||
            float.IsNaN(screenPos.x) || float.IsNaN(screenPos.y)) return;
        Vector3 world = _cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(_cam.transform.position.z)));
        world.z = 0f;
        _currentLine.positionCount++;
        _currentLine.SetPosition(_currentLine.positionCount - 1, world);
    }

    public void EndStroke() => _currentLine = null;

    public void ClearCanvas()
    {
        StartCoroutine(ClearAfterDelayRoutine());
    }

    private IEnumerator ClearAfterDelayRoutine()
    {
        yield return new WaitForSeconds(_clearDelaySeconds);
        foreach (var line in _activeLines)
            if (line != null) Destroy(line.gameObject);
        _activeLines.Clear();
    }
}