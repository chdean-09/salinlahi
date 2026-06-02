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

    public static void RebuildVisualCurve(
        IReadOnlyList<Vector2> rawPoints,
        List<Vector2> visualPoints,
        float spacingPixels,
        int maxInsertedPointsPerSegment)
    {
        if (visualPoints == null)
            return;

        visualPoints.Clear();
        if (rawPoints == null || rawPoints.Count == 0)
            return;

        for (int i = 0; i < rawPoints.Count; i++)
        {
            if (!IsFinite(rawPoints[i]))
                return;
        }

        visualPoints.Add(rawPoints[0]);
        if (rawPoints.Count == 1)
            return;

        for (int i = 0; i < rawPoints.Count - 1; i++)
        {
            Vector2 p1 = rawPoints[i];
            Vector2 p2 = rawPoints[i + 1];
            Vector2 p0 = i > 0
                ? rawPoints[i - 1]
                : p1 + (p1 - p2);
            Vector2 p3 = i + 2 < rawPoints.Count
                ? rawPoints[i + 2]
                : p2 + (p2 - p1);

            AppendCurvedSegment(
                visualPoints,
                p0,
                p1,
                p2,
                p3,
                spacingPixels,
                maxInsertedPointsPerSegment);
        }
    }

    private static void AppendCurvedSegment(
        List<Vector2> visualPoints,
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        float spacingPixels,
        int maxInsertedPoints)
    {
        float distance = Vector2.Distance(p1, p2);
        float spacing = Mathf.Max(1f, spacingPixels);
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
        segmentCount = Mathf.Min(segmentCount, Mathf.Max(1, maxInsertedPoints + 1));

        Vector2 tangent1 = LimitTangent((p2 - p0) * 0.5f, distance);
        Vector2 tangent2 = LimitTangent((p3 - p1) * 0.5f, distance);

        for (int i = 1; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            Vector2 point = CubicHermite(p1, tangent1, p2, tangent2, t);

            if (visualPoints.Count == 0 || Vector2.SqrMagnitude(visualPoints[visualPoints.Count - 1] - point) > 0.0001f)
                visualPoints.Add(point);
        }
    }

    private static Vector2 LimitTangent(Vector2 tangent, float segmentDistance)
    {
        float maxMagnitude = Mathf.Max(1f, segmentDistance * 1.25f);
        if (tangent.sqrMagnitude <= maxMagnitude * maxMagnitude)
            return tangent;

        return tangent.normalized * maxMagnitude;
    }

    private static Vector2 CubicHermite(Vector2 p0, Vector2 m0, Vector2 p1, Vector2 m1, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;

        return h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
    }

    public static bool IsFinite(Vector2 point)
    {
        return !float.IsNaN(point.x)
            && !float.IsNaN(point.y)
            && !float.IsInfinity(point.x)
            && !float.IsInfinity(point.y);
    }
}
