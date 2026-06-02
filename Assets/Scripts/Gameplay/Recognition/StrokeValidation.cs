using System.Collections.Generic;
using UnityEngine;

public static class StrokeValidation
{
    public static bool IsTapLikeStroke(
        IReadOnlyList<Vector2> points,
        float minimumPathLengthPixels,
        float minimumBoundsPixels)
    {
        if (points == null || points.Count < 2)
            return true;

        float pathLength = StrokeGeometry.ComputePathLength(points);
        Vector2 boundsSize = StrokeGeometry.ComputeBoundsSize(points);
        float largestBoundsSide = Mathf.Max(boundsSize.x, boundsSize.y);

        return pathLength < Mathf.Max(0f, minimumPathLengthPixels)
            || largestBoundsSide < Mathf.Max(0f, minimumBoundsPixels);
    }

    public static bool IsRecognitionDegenerate(List<List<Vector2>> strokes)
    {
        if (strokes == null)
            return true;

        int pointCount = 0;
        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            if (stroke == null)
                continue;

            pointCount += stroke.Count;
            if (pointCount >= 2)
                return false;
        }

        return true;
    }
}
