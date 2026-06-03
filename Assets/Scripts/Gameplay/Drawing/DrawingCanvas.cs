using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Renders stroke points as a visible line using LineRenderer.
// Sprint 4: replace with a render texture or GPU line for better visual quality.
public class DrawingCanvas : MonoBehaviour
{
    [Header("Line Appearance")]
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private float _lineWidth = 0.15f;
    [SerializeField] private Color _strokeColor = Color.white;

    [Header("Clear")]
    [SerializeField] private float _clearDelaySeconds = 0.3f;
    [SerializeField] private bool _stabilizeProjectionAgainstCameraShake = true;

    private LineRenderer _currentLine;
    private List<LineRenderer> _activeLines = new List<LineRenderer>();
    private readonly List<Vector3> _worldPointBuffer = new List<Vector3>(256);
    private Camera _cam;
    private Vector3 _cameraRestWorldPosition;

    private void Awake()
    {
        _cam = Camera.main;
        if (_cam == null)
            Debug.LogError("DrawingCanvas: No Camera tagged 'MainCamera' found. Strokes will be disabled.", this);
        else
            _cameraRestWorldPosition = _cam.transform.position;
    }

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
        _currentLine.numCapVertices = 4;
        _currentLine.numCornerVertices = 4;
        _currentLine.sortingOrder = RenderOrder.DrawingStroke;
        _activeLines.Add(_currentLine);
    }

    public void AddPoint(Vector2 screenPos)
    {
        if (_currentLine == null || _cam == null) return;
        if (!StrokeGeometry.IsFinite(screenPos)) return;

        Vector3 world = ScreenToStrokeWorld(screenPos);
        int index = _currentLine.positionCount;
        _currentLine.positionCount = index + 1;
        _currentLine.SetPosition(index, world);
    }

    public void SetPoints(IReadOnlyList<Vector2> screenPositions)
    {
        if (_currentLine == null || _cam == null || screenPositions == null)
            return;

        _worldPointBuffer.Clear();
        for (int i = 0; i < screenPositions.Count; i++)
        {
            Vector2 screenPos = screenPositions[i];
            if (!StrokeGeometry.IsFinite(screenPos))
                continue;

            _worldPointBuffer.Add(ScreenToStrokeWorld(screenPos));
        }

        _currentLine.positionCount = _worldPointBuffer.Count;
        for (int i = 0; i < _worldPointBuffer.Count; i++)
            _currentLine.SetPosition(i, _worldPointBuffer[i]);
    }

    private Vector3 ScreenToStrokeWorld(Vector2 screenPos)
    {
        Vector3 world = _cam.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(_cam.transform.position.z)));

        if (_stabilizeProjectionAgainstCameraShake)
        {
            Vector3 cameraOffset = _cam.transform.position - _cameraRestWorldPosition;
            world -= new Vector3(cameraOffset.x, cameraOffset.y, 0f);
        }

        world.z = 0f;
        return world;
    }

    public void EndStroke() => _currentLine = null;

    public void DiscardCurrentStroke()
    {
        if (_currentLine == null)
            return;

        LineRenderer line = _currentLine;
        _currentLine = null;
        _activeLines.Remove(line);

        if (line != null)
            Destroy(line.gameObject);
    }

    public void ClearCanvas()
    {
        StartCoroutine(ClearAfterDelayRoutine(new List<LineRenderer>(_activeLines)));
    }

    private IEnumerator ClearAfterDelayRoutine(List<LineRenderer> linesToClear)
    {
        yield return new WaitForSeconds(_clearDelaySeconds);

        foreach (var line in linesToClear)
        {
            _activeLines.Remove(line);

            if (line == null) continue;

            if (line == _currentLine)
                _currentLine = null;

            Destroy(line.gameObject);
        }
    }
}
