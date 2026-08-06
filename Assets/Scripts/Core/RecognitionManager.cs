using System.Collections.Generic;
using UnityEngine;

public class RecognitionManager : Singleton<RecognitionManager>
{
    [Header("Configuration")]
    [SerializeField] private RecognitionConfigSO _config;

    private DollarPRecognizer _recognizer;

    // Re-entrancy guard: ensures a single Recognize call produces exactly one
    // feedback cycle. Without this, a re-entrant call (e.g. input system edge
    // case) could raise OnCharacterRecognized / OnDrawingFailed twice for the
    // same stroke, duplicating combat and word-restoration feedback.
    private bool _isRecognizing;

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
        if (_isRecognizing)
        {
            DebugLogger.LogWarning("RecognitionManager: Recognize called re-entrantly -- ignoring duplicate.");
            return;
        }

        _isRecognizing = true;
        try
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
        finally
        {
            _isRecognizing = false;
        }
    }
}
