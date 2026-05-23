using System.Collections.Generic;
using UnityEngine;

public class RecognitionManager : Singleton<RecognitionManager>
{
    [Header("Configuration")]
    [SerializeField] private RecognitionConfigSO _config;

    [Header("Debug")]
    [SerializeField] private bool _enableBossTestCheat;

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
        int pointCount = StrokeTextParser.FlattenStrokes(strokes).Count;
        if (pointCount < _config.minimumPointCount)
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
        int pointCount = StrokeTextParser.FlattenStrokes(strokes).Count;
        if (pointCount < _config.minimumPointCount)
        {
            DebugLogger.Log("RecognitionManager: Too few points -- ignoring.");
            EventBus.RaiseDrawingFailed();
            return;
        }

        if (_enableBossTestCheat)
        {
            string cheatCharacterID = ResolveCheatCharacterID();
            if (!string.IsNullOrEmpty(cheatCharacterID))
            {
                var cheatResult = new RecognitionResult(cheatCharacterID, 1f, 0, "NONE", 1f);
                DebugLogger.Log($"RecognitionManager: test cheat forcing recognized character to {cheatCharacterID}.");
                EventBus.RaiseRecognitionResolved(cheatResult, true, _config.minimumConfidence);
                EventBus.RaiseCharacterRecognized(cheatCharacterID);
                return;
            }
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

    private static string ResolveCheatCharacterID()
    {
        BossController boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (boss != null && boss.IsTargetable && !string.IsNullOrEmpty(boss.CurrentExpectedCharacterID))
            return boss.CurrentExpectedCharacterID;

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker != null)
        {
            Enemy nearest = tracker.FindClosestToBase();
            if (nearest != null && nearest.Character != null)
                return nearest.Character.characterID;
        }

        return null;
    }
}
