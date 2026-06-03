using UnityEngine;

/// <summary>
/// Renders a stylised guide path for the tutorial assist animation.
/// Accepts world-space points from Level1TutorialStepSO.templatePoints and
/// displays them via a LineRenderer with an alpha fade toward the end.
/// Paired with TutorialAssistAnimator on the TutorialAssistAnimation prefab.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public sealed class TutorialPathTrail : MonoBehaviour
{
    [SerializeField] private LineRenderer _line;
    [SerializeField] private Color _startColor = new Color(1f, 0.9f, 0.3f, 0.85f);
    [SerializeField] private Color _endColor = new Color(1f, 0.9f, 0.3f, 0.15f);
    [SerializeField] private float _lineWidth = 0.07f;

    private void Reset()
    {
        _line = GetComponent<LineRenderer>();
    }

    public int PointCount => _line != null ? _line.positionCount : 0;

    /// <summary>
    /// Loads the given world-space points into the LineRenderer and makes the trail visible.
    /// </summary>
    public void Setup(Vector2[] worldPoints)
    {
        if (_line == null) _line = GetComponent<LineRenderer>();
        if (worldPoints == null || worldPoints.Length == 0)
        {
            _line.positionCount = 0;
            return;
        }

        _line.useWorldSpace = true;
        _line.positionCount = worldPoints.Length;
        _line.startColor = _startColor;
        _line.endColor = _endColor;
        _line.startWidth = _lineWidth;
        _line.endWidth = _lineWidth * 0.4f;

        for (int i = 0; i < worldPoints.Length; i++)
            _line.SetPosition(i, new Vector3(worldPoints[i].x, worldPoints[i].y, 0f));

        _line.enabled = true;
    }

    /// <returns>The world-space position of point at <paramref name="index"/>.</returns>
    public Vector3 GetPoint(int index)
    {
        if (_line == null || index < 0 || index >= _line.positionCount) return Vector3.zero;
        return _line.GetPosition(index);
    }

    public void Show()
    {
        if (_line != null) _line.enabled = true;
    }

    public void Hide()
    {
        if (_line != null) _line.enabled = false;
    }
}
