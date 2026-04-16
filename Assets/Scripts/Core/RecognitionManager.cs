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
        _recognizer.SetTemplateVariants(templates);

        int variantCount = 0;
        foreach (var kvp in templates)
            variantCount += kvp.Value.Count;

        DebugLogger.Log($"RecognitionManager: {templates.Count} characters loaded across {variantCount} template variants.");
    }

    public void PreviewRecognize(List<Vector2> points)
    {
        if (points == null || points.Count < _config.minimumPointCount)
        {
            EventBus.RaiseRecognitionResolved(
                new RecognitionResult("NONE", 0f, -1, "NONE", float.MinValue),
                false,
                _config.minimumConfidence);
            return;
        }

        RecognitionResult result = _recognizer.Recognize(points);
        bool passedThreshold = result.score >= _config.minimumConfidence;
        EventBus.RaiseRecognitionResolved(
            result,
            passedThreshold,
            _config.minimumConfidence);
    }

    public void Recognize(List<Vector2> points)
    {
        if (points == null || points.Count < _config.minimumPointCount)
        {
            DebugLogger.Log("RecognitionManager: Too few points -- ignoring.");
            EventBus.RaiseDrawingFailed();
            return;
        }
        RecognitionResult result = _recognizer.Recognize(points);
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
}
