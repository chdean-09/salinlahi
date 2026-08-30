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
    private struct CandidateMatch
    {
        public string CharacterID;
        public float Score;
        public int VariantIndex;
    }

    // Recognition runs in two stages:
    //   1. Pure $P shape scoring picks a leader. If the leader beats the runner-up
    //      by CLEAR_WIN_GAP or more, return it untaxed.
    //   2. Otherwise disambiguate among the top DISAMBIGUATION_TOP_K candidates by
    //      multiplying their shape scores by stroke-count and aspect-ratio penalties
    //      that $P itself ignores.
    // Applying penalties only to close-call candidates avoids regressing characters
    // that are already unambiguous by shape alone.
    private const int DISAMBIGUATION_TOP_K = 3;
    private const float CLEAR_WIN_GAP = 0.08f;

    // Stroke-count mismatch penalty: shape score multiplied by
    // max(MIN_PENALTY, 1 - PER_STROKE_PENALTY * |userStrokes - templateStrokes|).
    // Applied only during disambiguation.
    private const float STROKE_COUNT_PER_STROKE_PENALTY = 0.15f;
    private const float STROKE_COUNT_MIN_PENALTY = 0.6f;

    // Aspect-ratio mismatch penalty: shape score multiplied by
    // max(MIN_PENALTY, 1 - STRENGTH * |log10(userRatio / templateRatio)|).
    // Aspect ratio = longer/shorter of the raw bounding box. Applied only during
    // disambiguation.
    private const float ASPECT_RATIO_PENALTY_STRENGTH = 0.4f;
    private const float ASPECT_RATIO_MIN_PENALTY = 0.6f;

    private readonly int _n; // resample point count
    private Dictionary<string, List<List<Vector2>>> _templates;
    private Dictionary<string, List<int>> _templateStrokeCounts;
    private Dictionary<string, List<float>> _templateAspectRatios;
    public DollarPRecognizer(int resampleCount = 32)
    {
        _n = resampleCount;
        _templates = new Dictionary<string, List<List<Vector2>>>();
        _templateStrokeCounts = new Dictionary<string, List<int>>();
        _templateAspectRatios = new Dictionary<string, List<float>>();
    }

    // Backward-compatible entry point for single-template-per-character callers.
    public void SetTemplates(Dictionary<string, List<Vector2>> raw)
    {
        var wrapped = new Dictionary<string, List<List<Vector2>>>();
        foreach (var kvp in raw)
            wrapped[kvp.Key] = new List<List<Vector2>> { kvp.Value };

        SetTemplateVariants(wrapped);
    }

    // Backward-compatible wrapper for legacy callers with flattened multi-variant templates.
    // Prefer SetTemplateStrokeVariants for new stroke-aware template loading.
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
        _templateStrokeCounts.Clear();
        _templateAspectRatios.Clear();

        foreach (var kvp in raw)
        {
            var variants = new List<List<Vector2>>();
            var strokeCounts = new List<int>();
            var aspectRatios = new List<float>();

            foreach (List<List<Vector2>> variantStrokes in kvp.Value)
            {
                if (variantStrokes == null || variantStrokes.Count == 0) continue;
                List<Vector2> preprocessed = PreprocessStrokes(CloneStrokes(variantStrokes));
                if (preprocessed.Count == 0) continue;

                variants.Add(preprocessed);
                strokeCounts.Add(CountNonEmptyStrokes(variantStrokes));
                aspectRatios.Add(ComputeAspectRatio(variantStrokes));
            }

            if (variants.Count > 0)
            {
                _templates[kvp.Key] = variants;
                _templateStrokeCounts[kvp.Key] = strokeCounts;
                _templateAspectRatios[kvp.Key] = aspectRatios;
            }
        }
    }

    private static int CountNonEmptyStrokes(List<List<Vector2>> strokes)
    {
        if (strokes == null) return 0;
        int count = 0;
        for (int i = 0; i < strokes.Count; i++)
        {
            if (strokes[i] != null && strokes[i].Count > 0)
                count++;
        }
        return count;
    }

    private static float StrokeCountPenalty(int userStrokeCount, int templateStrokeCount)
    {
        int diff = Mathf.Abs(userStrokeCount - templateStrokeCount);
        if (diff == 0) return 1f;
        return Mathf.Max(STROKE_COUNT_MIN_PENALTY, 1f - STROKE_COUNT_PER_STROKE_PENALTY * diff);
    }

    // Returns longer-side / shorter-side from the strokes' bounding box. Result is
    // always >= 1 (rotation-invariant) and is 1 for a square or degenerate bbox.
    private static float ComputeAspectRatio(List<List<Vector2>> strokes)
    {
        if (strokes == null) return 1f;
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        bool any = false;
        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> s = strokes[i];
            if (s == null) continue;
            for (int j = 0; j < s.Count; j++)
            {
                Vector2 p = s[j];
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
                any = true;
            }
        }
        if (!any) return 1f;
        float w = maxX - minX, h = maxY - minY;
        float longer = Mathf.Max(w, h);
        float shorter = Mathf.Min(w, h);
        if (longer < 1e-6f) return 1f;
        if (shorter < 1e-6f) shorter = 1e-6f;
        return longer / shorter;
    }

    private static float AspectRatioPenalty(float userRatio, float templateRatio)
    {
        if (userRatio < 1e-6f || templateRatio < 1e-6f) return 1f;
        float logDistance = Mathf.Abs(Mathf.Log10(userRatio / templateRatio));
        if (logDistance < 1e-6f) return 1f;
        return Mathf.Max(ASPECT_RATIO_MIN_PENALTY, 1f - ASPECT_RATIO_PENALTY_STRENGTH * logDistance);
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

        // Stage 1: pure shape scoring. For each character, keep its best-matching variant.
        var shortlist = new List<CandidateMatch>(_templates.Count);
        foreach (var kvp in _templates)
        {
            float bestShape = float.MinValue;
            int bestVariant = -1;
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                float d = GreedyCloudMatch(candidate, kvp.Value[i]);
                float shape = 1f - d / (0.5f * Mathf.Sqrt(2f));
                if (shape > bestShape)
                {
                    bestShape = shape;
                    bestVariant = i + 1;
                }
            }

            if (bestVariant > 0)
            {
                shortlist.Add(new CandidateMatch
                {
                    CharacterID = kvp.Key,
                    Score = bestShape,
                    VariantIndex = bestVariant
                });
            }
        }

        if (shortlist.Count == 0)
            return new RecognitionResult("NONE", 0f, -1, "NONE", float.MinValue);

        shortlist.Sort((a, b) => b.Score.CompareTo(a.Score));

        CandidateMatch leader = shortlist[0];
        CandidateMatch runnerUp = shortlist.Count > 1
            ? shortlist[1]
            : new CandidateMatch { CharacterID = "NONE", Score = float.MinValue, VariantIndex = -1 };

        // Stage 2: if shape alone gives the leader a clear margin, trust it untaxed.
        // This is the core Option-A property: unambiguous characters never pay a penalty.
        float shapeGap = shortlist.Count > 1 ? leader.Score - runnerUp.Score : leader.Score;
        if (shortlist.Count == 1 || shapeGap >= CLEAR_WIN_GAP)
        {
            return new RecognitionResult(
                leader.CharacterID, leader.Score, leader.VariantIndex,
                runnerUp.CharacterID, runnerUp.Score);
        }

        // Stage 3: close call — re-rank the top K by composite score (shape × stroke-count × aspect-ratio).
        int userStrokeCount = CountNonEmptyStrokes(strokes);
        float userAspectRatio = ComputeAspectRatio(strokes);
        int k = Mathf.Min(DISAMBIGUATION_TOP_K, shortlist.Count);

        CandidateMatch disambiguatedBest = new CandidateMatch
        {
            CharacterID = "NONE",
            Score = float.MinValue,
            VariantIndex = -1
        };
        CandidateMatch disambiguatedSecond = new CandidateMatch
        {
            CharacterID = "NONE",
            Score = float.MinValue,
            VariantIndex = -1
        };

        for (int i = 0; i < k; i++)
        {
            CandidateMatch candidateMatch = shortlist[i];
            int templateStrokeCount = LookupTemplateStrokeCount(
                candidateMatch.CharacterID, candidateMatch.VariantIndex, userStrokeCount);
            float templateAspectRatio = LookupTemplateAspectRatio(
                candidateMatch.CharacterID, candidateMatch.VariantIndex, userAspectRatio);

            float composite = candidateMatch.Score
                * StrokeCountPenalty(userStrokeCount, templateStrokeCount)
                * AspectRatioPenalty(userAspectRatio, templateAspectRatio);

            CandidateMatch ranked = new CandidateMatch
            {
                CharacterID = candidateMatch.CharacterID,
                Score = composite,
                VariantIndex = candidateMatch.VariantIndex
            };

            if (composite > disambiguatedBest.Score)
            {
                disambiguatedSecond = disambiguatedBest;
                disambiguatedBest = ranked;
            }
            else if (composite > disambiguatedSecond.Score)
            {
                disambiguatedSecond = ranked;
            }
        }

        return new RecognitionResult(
            disambiguatedBest.CharacterID, disambiguatedBest.Score, disambiguatedBest.VariantIndex,
            disambiguatedSecond.CharacterID, disambiguatedSecond.Score);
    }

    private int LookupTemplateStrokeCount(string characterID, int variantOneBased, int fallback)
    {
        if (_templateStrokeCounts.TryGetValue(characterID, out List<int> list))
        {
            int idx = variantOneBased - 1;
            if (idx >= 0 && idx < list.Count) return list[idx];
        }
        return fallback;
    }

    private float LookupTemplateAspectRatio(string characterID, int variantOneBased, float fallback)
    {
        if (_templateAspectRatios.TryGetValue(characterID, out List<float> list))
        {
            int idx = variantOneBased - 1;
            if (idx >= 0 && idx < list.Count) return list[idx];
        }
        return fallback;
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
    // Per-axis scaling erases aspect ratio, which is most of the signal a near-1D
    // glyph has: HA's flat wave stretched to a square becomes amplified capture
    // noise, so real HA draws scored below WA/SA/LA/MA. Wobbrock et al.'s $1 paper
    // (UIST 2007, "Limitations") prescribes uniform scaling for 1D gestures via a
    // bounding-box ratio test; template aspect ratios cluster at <= 3.94 for 2D
    // glyphs vs 5.53-12.77 for HA, so 4.5 separates the classes. Verified against
    // the full template bank + Assets/Tests/Fixtures/TestDraws: leave-one-out goes
    // 119/121 -> 121/121 and all 20 recorded draws stay correct, where always-uniform
    // scaling regresses every RA draw to KA.
    private const float ONE_D_ASPECT_THRESHOLD = 4.5f;

    private List<Vector2> ScaleToSquare(List<Vector2> pts, float size)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts)
        {
            if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
        }
        float width = maxX - minX;
        float height = maxY - minY;
        float longer = Mathf.Max(width, height);
        float shorter = Mathf.Max(Mathf.Min(width, height), 1e-6f);

        float sx, sy;
        if (longer > 1e-6f && longer / shorter >= ONE_D_ASPECT_THRESHOLD)
        {
            sx = sy = size / longer;
        }
        else
        {
            sx = width > 1e-6f ? size / width : 1f;
            sy = height > 1e-6f ? size / height : 1f;
        }
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
