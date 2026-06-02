using System.Collections.Generic;
using UnityEngine;

public static class StrokeGeometry
{
    public static bool TryAppendPoint(List<Vector2> points, Vector2 point, float minDistancePixels)
    {
        if (points == null)
            return false;

        if (!IsFinite(point))
            return false;

        if (points.Count == 0)
        {
            points.Add(point);
            return true;
        }

        float minDistance = Mathf.Max(0f, minDistancePixels);
        if (Vector2.Distance(points[points.Count - 1], point) < minDistance)
            return false;

        points.Add(point);
        return true;
    }

    public static float ComputePathLength(IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count < 2)
            return 0f;

        float total = 0f;
        for (int i = 1; i < points.Count; i++)
            total += Vector2.Distance(points[i - 1], points[i]);

        return total;
    }

    public static Vector2 ComputeBoundsSize(IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count == 0)
            return Vector2.zero;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 point = points[i];
            if (point.x < minX) minX = point.x;
            if (point.x > maxX) maxX = point.x;
            if (point.y < minY) minY = point.y;
            if (point.y > maxY) maxY = point.y;
        }

        return new Vector2(maxX - minX, maxY - minY);
    }

    public static void AppendVisualSegment(
        List<Vector2> visualPoints,
        Vector2 from,
        Vector2 to,
        float spacingPixels,
        int maxInsertedPoints)
    {
        if (visualPoints == null)
            return;

        if (!IsFinite(from) || !IsFinite(to))
            return;

        float distance = Vector2.Distance(from, to);
        float spacing = Mathf.Max(1f, spacingPixels);
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
        segmentCount = Mathf.Min(segmentCount, Mathf.Max(1, maxInsertedPoints + 1));

        for (int i = 1; i <= segmentCount; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, i / (float)segmentCount);
            if (visualPoints.Count == 0 || Vector2.SqrMagnitude(visualPoints[visualPoints.Count - 1] - point) > 0.0001f)
                visualPoints.Add(point);
        }
    }

    public static bool IsFinite(Vector2 point)
    {
        return !float.IsNaN(point.x)
            && !float.IsNaN(point.y)
            && !float.IsInfinity(point.x)
            && !float.IsInfinity(point.y);
    }
}
