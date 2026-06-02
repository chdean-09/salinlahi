using System.Collections.Generic;
using UnityEngine;

public sealed class CapturedStroke
{
    private readonly List<Vector2> _rawPoints = new List<Vector2>(128);
    private readonly List<Vector2> _visualPoints = new List<Vector2>(256);

    public CapturedStroke(int fingerIndex, int touchId, double startTime)
    {
        FingerIndex = fingerIndex;
        TouchId = touchId;
        StartTime = startTime;
    }

    public int FingerIndex { get; }
    public int TouchId { get; }
    public double StartTime { get; }
    public IReadOnlyList<Vector2> RawPoints => _rawPoints;
    public IReadOnlyList<Vector2> VisualPoints => _visualPoints;

    public void Begin(Vector2 point)
    {
        _rawPoints.Clear();
        _visualPoints.Clear();
        if (!StrokeGeometry.IsFinite(point))
            return;

        _rawPoints.Add(point);
        _visualPoints.Add(point);
    }

    public bool AddRawSample(Vector2 point, float rawMinDistancePixels)
    {
        return StrokeGeometry.TryAppendPoint(_rawPoints, point, rawMinDistancePixels);
    }

    public void AddVisualSegment(Vector2 from, Vector2 to, float spacingPixels, int maxInsertedPoints)
    {
        StrokeGeometry.AppendVisualSegment(_visualPoints, from, to, spacingPixels, maxInsertedPoints);
    }

    public void RebuildVisualCurve(float spacingPixels, int maxInsertedPointsPerSegment)
    {
        StrokeGeometry.RebuildVisualCurve(
            _rawPoints,
            _visualPoints,
            spacingPixels,
            maxInsertedPointsPerSegment);
    }

    public List<Vector2> CloneRawPoints()
    {
        return new List<Vector2>(_rawPoints);
    }

    public void Clear()
    {
        _rawPoints.Clear();
        _visualPoints.Clear();
    }
}
