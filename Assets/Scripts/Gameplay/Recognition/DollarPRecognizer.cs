using System.Collections.Generic;
using UnityEngine;
public struct RecognitionResult
{
    public string characterID;
    public float score; // 0..1, higher = better match
    public int templateVariantIndex; // 1-based index among the matched character variants

    public RecognitionResult(string id, float s)
    {
        characterID = id;
        score = s;
        templateVariantIndex = -1;
    }

    public RecognitionResult(string id, float s, int variantIndex)
    {
        characterID = id;
        score = s;
        templateVariantIndex = variantIndex;
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
        _templates.Clear();
        foreach (var kvp in raw)
        {
            var variants = new List<List<Vector2>>();
            foreach (List<Vector2> variant in kvp.Value)
            {
                if (variant == null || variant.Count == 0) continue;
                variants.Add(Preprocess(new List<Vector2>(variant)));
            }

            if (variants.Count > 0)
                _templates[kvp.Key] = variants;
        }
    }
    public RecognitionResult Recognize(List<Vector2> points)
    {
        if (_templates.Count == 0)
            return new RecognitionResult("NONE", 0f, -1);

        List<Vector2> candidate = Preprocess(new List<Vector2>(points));
        string bestID = "NONE";
        float bestScore = float.MinValue;
        int bestVariantIndex = -1;
        foreach (var kvp in _templates)
        {
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                List<Vector2> template = kvp.Value[i];
                float d = GreedyCloudMatch(candidate, template);
                float score = 1f - d / (0.5f * Mathf.Sqrt(2f));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestID = kvp.Key;
                    bestVariantIndex = i + 1;
                }
            }
        }
        return new RecognitionResult(bestID, bestScore, bestVariantIndex);
    }
    // ── PREPROCESSING ────────────────────────────────────────────────
    private List<Vector2> Preprocess(List<Vector2> pts)
    {
        pts = Resample(pts, _n);
        pts = ScaleToSquare(pts, 1f);
        pts = TranslateToOrigin(pts);
        return pts;
    }
    private List<Vector2> Resample(List<Vector2> pts, int n)
    {
        float totalLen = PathLength(pts);
        if (totalLen == 0f) return pts;
        float interval = totalLen / (n - 1);
        float D = 0f;
        var result = new List<Vector2> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            float d = Vector2.Distance(pts[i - 1], pts[i]);
            if (D + d >= interval)
            {
                float t = (interval - D) / d;
                Vector2 q = Vector2.Lerp(pts[i - 1], pts[i], t);
                result.Add(q);
                pts.Insert(i, q);
                D = 0f;
            }
            else D += d;
        }
        // Pad or trim to exactly n points
        while (result.Count < n) result.Add(pts[pts.Count - 1]);
        if (result.Count > n) result.RemoveRange(n, result.Count - n);
        return result;
    }
    private float PathLength(List<Vector2> pts)
    {
        float d = 0f;
        for (int i = 1; i < pts.Count; i++) d += Vector2.Distance(pts[i - 1], pts[i]);
        return d;
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