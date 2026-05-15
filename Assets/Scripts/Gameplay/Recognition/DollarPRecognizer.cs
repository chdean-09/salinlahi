using System.Collections.Generic;
using UnityEngine;
public struct RecognitionResult
{
    public string characterID;
    public float score; // 0..1, higher = better match
    public int templateVariantIndex; // 1-based index among the matched character variants
    public string secondBestID;
    public float secondBestScore;
    public float scoreGap;

    public RecognitionResult(
        string id, float s, int variantIndex,
        string secondId, float secondS)
    {
        characterID = id;
        score = s;
        templateVariantIndex = variantIndex;
        secondBestID = secondId;
        secondBestScore = secondS;
        scoreGap = s - secondS;
    }
}

public class DollarPRecognizer
{
    private readonly int _n; // resample point count
    private Dictionary<string, List<List<Vector2>>> _templates;
    public DollarPRecognizer(int resampleCount = 32)
    {
        _n = resampleCount;
        _templates = new Dictionary<string, List<List<Vector2>>>();
    }

    // Backward-compatible entry point for single-template-per-character callers.
    public void SetTemplates(Dictionary<string, List<Vector2>> raw)
    {
        var wrapped = new Dictionary<string, List<List<Vector2>>>();
        foreach (var kvp in raw)
            wrapped[kvp.Key] = new List<List<Vector2>> { kvp.Value };

        SetTemplateVariants(wrapped);
    }

    public void SetTemplateVariants(Dictionary<string, List<List<Vector2>>> raw)
    {
        var wrapped = new Dictionary<string, List<List<List<Vector2>>>>();
        foreach (var kvp in raw)
        {
            var variants = new List<List<List<Vector2>>>();
            foreach (List<Vector2> variant in kvp.Value)
                variants.Add(new List<List<Vector2>> { variant });
            wrapped[kvp.Key] = variants;
        }

        SetTemplateStrokeVariants(wrapped);
    }

    public void SetTemplateStrokeVariants(Dictionary<string, List<List<List<Vector2>>>> raw)
    {
        _templates.Clear();

        foreach (var kvp in raw)
        {
            var variants = new List<List<Vector2>>();

            foreach (List<List<Vector2>> variantStrokes in kvp.Value)
            {
                if (variantStrokes == null || variantStrokes.Count == 0) continue;
                List<Vector2> preprocessed = PreprocessStrokes(CloneStrokes(variantStrokes));
                if (preprocessed.Count == 0) continue;

                variants.Add(preprocessed);
            }

            if (variants.Count > 0)
            {
                _templates[kvp.Key] = variants;
            }
        }
    }

    public RecognitionResult Recognize(List<Vector2> points)
    {
        return Recognize(new List<List<Vector2>> { points });
    }

    public RecognitionResult Recognize(List<List<Vector2>> strokes)
    {
        if (_templates.Count == 0)
            return new RecognitionResult("NONE", 0f, -1, "NONE", float.MinValue);

        List<Vector2> candidate = PreprocessStrokes(CloneStrokes(strokes));
        if (candidate.Count == 0)
            return new RecognitionResult("NONE", 0f, -1, "NONE", float.MinValue);

        string bestID = "NONE";
        float bestScore = float.MinValue;
        int bestVariantIndex = -1;
        string secondID = "NONE";
        float secondScore = float.MinValue;

        foreach (var kvp in _templates)
        {
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                List<Vector2> template = kvp.Value[i];
                float d = GreedyCloudMatch(candidate, template);
                float score = 1f - d / (0.5f * Mathf.Sqrt(2f));

                if (score > bestScore)
                {
                    // Current best becomes second-best
                    secondScore = bestScore;
                    secondID = bestID;

                    bestScore = score;
                    bestID = kvp.Key;
                    bestVariantIndex = i + 1;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                    secondID = kvp.Key;
                }
            }
        }

        return new RecognitionResult(
            bestID, bestScore, bestVariantIndex,
            secondID, secondScore);
    }

    // ── PREPROCESSING ────────────────────────────────────────────────
    private List<Vector2> PreprocessStrokes(List<List<Vector2>> strokes)
    {
        List<Vector2> pts = ResampleStrokes(strokes, _n);
        if (pts.Count == 0)
            return pts;
        pts = ScaleToSquare(pts, 1f);
        pts = TranslateToOrigin(pts);
        return pts;
    }

    private List<Vector2> ResampleStrokes(List<List<Vector2>> strokes, int n)
    {
        List<Vector2> flattened = StrokeTextParser.FlattenStrokes(strokes);
        if (flattened.Count == 0 || n <= 0)
            return flattened;
        if (n == 1)
            return new List<Vector2> { flattened[0] };

        float totalLen = TotalStrokePathLength(strokes);
        if (totalLen <= 1e-6f)
        {
            var degenerate = new List<Vector2> { flattened[0] };
            while (degenerate.Count < n)
                degenerate.Add(flattened[flattened.Count - 1]);
            return degenerate;
        }

        float interval = totalLen / (n - 1);
        float accumulated = 0f;
        var result = new List<Vector2> { flattened[0] };
        for (int strokeIndex = 0; strokeIndex < strokes.Count; strokeIndex++)
        {
            List<Vector2> stroke = strokes[strokeIndex];
            if (stroke == null || stroke.Count < 2)
                continue;

            Vector2 segmentStart = stroke[0];
            for (int pointIndex = 1; pointIndex < stroke.Count; pointIndex++)
            {
                Vector2 segmentEnd = stroke[pointIndex];
                float segmentLength = Vector2.Distance(segmentStart, segmentEnd);
                if (segmentLength <= 1e-6f)
                {
                    segmentStart = segmentEnd;
                    continue;
                }

                while (accumulated + segmentLength >= interval)
                {
                    float t = (interval - accumulated) / segmentLength;
                    Vector2 q = Vector2.Lerp(segmentStart, segmentEnd, t);
                    result.Add(q);
                    segmentStart = q;
                    segmentLength = Vector2.Distance(segmentStart, segmentEnd);
                    accumulated = 0f;
                }

                accumulated += segmentLength;
                segmentStart = segmentEnd;
            }
        }

        while (result.Count < n)
            result.Add(flattened[flattened.Count - 1]);
        if (result.Count > n)
            result.RemoveRange(n, result.Count - n);
        return result;
    }

    private float TotalStrokePathLength(List<List<Vector2>> strokes)
    {
        float total = 0f;
        if (strokes == null)
            return total;

        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            if (stroke == null || stroke.Count < 2)
                continue;
            for (int j = 1; j < stroke.Count; j++)
                total += Vector2.Distance(stroke[j - 1], stroke[j]);
        }

        return total;
    }

    private List<List<Vector2>> CloneStrokes(List<List<Vector2>> strokes)
    {
        var clone = new List<List<Vector2>>();
        if (strokes == null)
            return clone;

        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            if (stroke == null || stroke.Count == 0)
                continue;
            clone.Add(new List<Vector2>(stroke));
        }

        return clone;
    }
    private List<Vector2> ScaleToSquare(List<Vector2> pts, float size)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts)
        {
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
        }
        float sx = (maxX - minX) > 1e-6f ? size / (maxX - minX) : 1f;
        float sy = (maxY - minY) > 1e-6f ? size / (maxY - minY) : 1f;
        var result = new List<Vector2>(pts.Count);
        foreach (var p in pts) result.Add(new Vector2(p.x * sx, p.y * sy));
        return result;
    }
    private List<Vector2> TranslateToOrigin(List<Vector2> pts)
    {
        Vector2 centroid = Vector2.zero;
        foreach (var p in pts) centroid += p;
        centroid /= pts.Count;
        var result = new List<Vector2>(pts.Count);
        foreach (var p in pts) result.Add(p - centroid);
        return result;
    }
    // ── MATCHING ─────────────────────────────────────────────────────
    // Greedy nearest-neighbor point cloud matching from Vatavu et al. 2012.
    // Returns average nearest-neighbor distance (lower = better match).
    private float GreedyCloudMatch(List<Vector2> a, List<Vector2> b)
    {
        int n = a.Count;
        bool[] used = new bool[n];
        float sum = 0f;
        foreach (var tp in b)
        {
            int best = -1;
            float minDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                float d = Vector2.SqrMagnitude(a[i] - tp);
                if (d < minDist) { minDist = d; best = i; }
            }
            if (best >= 0) { used[best] = true; sum += Mathf.Sqrt(minDist); }
        }
        return sum / b.Count;
    }
}
