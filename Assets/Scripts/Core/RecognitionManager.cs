using System.Collections.Generic;
using UnityEngine;

public class RecognitionManager : Singleton<RecognitionManager>
{
    [Header("Configuration")]
    [SerializeField] private RecognitionConfigSO _config;

    private DollarPRecognizer _recognizer;

    /// <summary>
    /// The campaign-wide accuracy floor, before any level override. Also the value a forgiven
    /// drawing is measured against for the silent correction.
    /// </summary>
    public float GlobalThreshold => _config != null ? _config.minimumConfidence : 0f;

    /// <summary>
    /// The accuracy floor in force right now: the current level's override when it authored one,
    /// otherwise <see cref="GlobalThreshold"/> unchanged.
    /// </summary>
    /// <remarks>
    /// Read per recognition rather than cached at level load. The level config is reachable only
    /// through GameManager, which is populated after this singleton's Awake on a fresh scene load, so
    /// anything cached here would be the previous level's value — or none — for the first draw of
    /// every level. A property read is two null checks on a path that already runs a full point-cloud
    /// match, so there is nothing to win by caching it.
    /// </remarks>
    public float ActiveThreshold =>
        LevelConfigSO.ResolveDrawingAccuracyThreshold(GameManager.CurrentLevelConfig, GlobalThreshold);

    protected override void Awake()
    {
        base.Awake();

        // base.Awake() only returns out of ITSELF when this is a duplicate, so without this
        // guard the doomed copy still ran LoadTemplates() before its deferred Destroy landed --
        // re-parsing all 121 stroke templates on every single level load. Same guard the other
        // singletons here already use (SceneLoader, AudioManager, EnemyPool).
        if (Instance != this) return;

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
        float threshold = ActiveThreshold;

        if (StrokeValidation.IsRecognitionDegenerate(strokes))
        {
            EventBus.RaiseRecognitionResolved(
                new RecognitionResult("NONE", 0f, -1, "NONE", float.MinValue),
                false,
                threshold);
            return;
        }

        RecognitionResult result = _recognizer.Recognize(strokes);
        bool passedThreshold = result.score >= threshold;
        EventBus.RaiseRecognitionResolved(
            result,
            passedThreshold,
            threshold);
    }

    public void Recognize(List<Vector2> points)
    {
        Recognize(new List<List<Vector2>> { points });
    }

    public void Recognize(List<List<Vector2>> strokes)
    {
        if (StrokeValidation.IsRecognitionDegenerate(strokes))
        {
            // Deliberately raises no accuracy report. A degenerate stroke was never scored against a
            // threshold, so it is not the "sloppy drawing" the two-tier response is about -- treating
            // a stray tap as a rejected attempt would replay the correct form at a player who has not
            // attempted anything yet. DrawingFeedback's existing OnDrawingFailed cue still covers it.
            DebugLogger.Log("RecognitionManager: Degenerate stroke input -- ignoring.");
            EventBus.RaiseDrawingFailed();
            return;
        }

        float globalThreshold = GlobalThreshold;
        float levelThreshold = ActiveThreshold;

        RecognitionResult result = _recognizer.Recognize(strokes);
        DebugLogger.Log(
            $"Recognized: {result.characterID} "
            + $"Score: {result.score:F3} "
            + $"Second: {result.secondBestID} "
            + $"({result.secondBestScore:F3}) "
            + $"Gap: {result.scoreGap:F3} "
            + $"Threshold: {levelThreshold:F2} "
            + $"(global {globalThreshold:F2})");
        LogCandidateShape(strokes);

        RecognitionLogger.LogAttempt(
            result,
            TestSessionController.IntendedCharacterID);

        bool passedThreshold = result.score >= levelThreshold;
        DrawAccuracyVerdict verdict = ClassifyAccuracy(result.score, levelThreshold, globalThreshold);

        // Raised before the recognition event on purpose. The presenter has to know a forgiven
        // drawing is forgiven BEFORE the kill it belongs to resolves, because the silent correction
        // is owed to that specific kill and there is no later signal that ties the two together.
        DrawFeedbackSignals.RaiseAccuracyResolved(new DrawAccuracyReport
        {
            Verdict = verdict,
            CharacterId = result.characterID,
            Score = result.score,
            LevelThreshold = levelThreshold,
            GlobalThreshold = globalThreshold,
        });

        EventBus.RaiseRecognitionResolved(
            result,
            passedThreshold,
            levelThreshold);

        if (passedThreshold)
            EventBus.RaiseCharacterRecognized(result.characterID);
        else
            EventBus.RaiseDrawingFailed();
    }

    /// <summary>
    /// Sorts a score into the two-tier response: refused, forgiven-with-a-silent-correction, or
    /// clean.
    /// </summary>
    /// <remarks>
    /// The middle tier only exists when a level has lowered its floor below the global default. A
    /// level with no override collapses the two thresholds onto each other, so every accepted drawing
    /// comes out <see cref="DrawAccuracyVerdict.Accepted"/> and no correction is ever owed — which is
    /// exactly the behaviour those levels have today.
    ///
    /// Static and threshold-parameterised rather than reading the config itself, so the tier boundary
    /// is assertable without a scene or a GameManager.
    /// </remarks>
    public static DrawAccuracyVerdict ClassifyAccuracy(float score, float levelThreshold, float globalThreshold)
    {
        if (score < levelThreshold)
            return DrawAccuracyVerdict.Rejected;

        return score < globalThreshold
            ? DrawAccuracyVerdict.AcceptedWithSilentCorrection
            : DrawAccuracyVerdict.Accepted;
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
