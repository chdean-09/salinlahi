using System.Collections.Generic;
using UnityEngine;

public class RecognitionManager : Singleton<RecognitionManager>
{
    [Header("Configuration")]
    [SerializeField] private RecognitionConfigSO _config;

    private DollarPRecognizer _recognizer;

    protected override void Awake()
    {
        base.Awake();
        _recognizer = new DollarPRecognizer(_config.resamplePointCount);
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var loader = new TemplateLoader();
        var templates = loader.LoadAll();
        _recognizer.SetTemplateStrokeVariants(templates);

        int variantCount = 0;
        foreach (var kvp in templates)
            variantCount += kvp.Value.Count;

        DebugLogger.Log($"RecognitionManager: {templates.Count} characters loaded across {variantCount} template variants.");
    }

    public void PreviewRecognize(List<Vector2> points)
    {
        PreviewRecognize(new List<List<Vector2>> { points });
    }

    public void PreviewRecognize(List<List<Vector2>> strokes)
    {
        if (StrokeValidation.IsRecognitionDegenerate(strokes))
        {
            EventBus.RaiseRecognitionResolved(
                new RecognitionResult("NONE", 0f, -1, "NONE", float.MinValue),
                false,
                _config.minimumConfidence);
            return;
        }

        RecognitionResult result = _recognizer.Recognize(strokes);
        bool passedThreshold = result.score >= _config.minimumConfidence;
        EventBus.RaiseRecognitionResolved(
            result,
            passedThreshold,
            _config.minimumConfidence);
    }

    public void Recognize(List<Vector2> points)
    {
        Recognize(new List<List<Vector2>> { points });
    }

    public void Recognize(List<List<Vector2>> strokes)
    {
        if (StrokeValidation.IsRecognitionDegenerate(strokes))
        {
            DebugLogger.Log("RecognitionManager: Degenerate stroke input -- ignoring.");
            EventBus.RaiseDrawingFailed();
            return;
        }

        RecognitionResult result = _recognizer.Recognize(strokes);
        DebugLogger.Log(
            $"Recognized: {result.characterID} "
            + $"Score: {result.score:F3} "
            + $"Second: {result.secondBestID} "
            + $"({result.secondBestScore:F3}) "
            + $"Gap: {result.scoreGap:F3} "
            + $"Threshold: {_config.minimumConfidence:F2}");
        LogCandidateShape(strokes);

        RecognitionLogger.LogAttempt(
            result,
            TestSessionController.IntendedCharacterID);

        bool passedThreshold = result.score >= _config.minimumConfidence;
        EventBus.RaiseRecognitionResolved(
            result,
            passedThreshold,
            _config.minimumConfidence);

        if (passedThreshold)
            EventBus.RaiseCharacterRecognized(result.characterID);
        else
            EventBus.RaiseDrawingFailed();
    }

    // Shape of the submitted candidate: stroke/point counts and bounding box.
    // Keeps recognition mismatches diagnosable from a player log alone.
    private static void LogCandidateShape(List<List<Vector2>> strokes)
    {
        int strokeCount = 0, pointCount = 0;
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (List<Vector2> stroke in strokes)
        {
            if (stroke == null || stroke.Count == 0) continue;
            strokeCount++;
            pointCount += stroke.Count;
            foreach (Vector2 p in stroke)
            {
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            }
        }
        DebugLogger.Log(
            $"Candidate: strokes={strokeCount} points={pointCount} "
            + $"bbox=({minX:F0},{minY:F0})-({maxX:F0},{maxY:F0}) "
            + $"size={maxX - minX:F0}x{maxY - minY:F0}");
    }
}
