using System.Collections.Generic;
using UnityEngine;

public sealed class Level1TutorialGlyphValidator
{
    private const int MinimumPointCount = 2;

    public Level1TutorialValidationResult Validate(
        string targetCharacterId,
        RecognitionResult recognitionResult,
        bool passedRecognitionThreshold,
        IReadOnlyList<List<Vector2>> submittedStrokes,
        IReadOnlyList<List<Vector2>> templateStrokes,
        float tolerancePixels)
    {
        if (string.IsNullOrWhiteSpace(targetCharacterId))
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.NoPrompt);

        if (!passedRecognitionThreshold)
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.RecognitionFailed);

        if (!string.Equals(
                recognitionResult.characterID,
                targetCharacterId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.WrongCharacter);
        }

        List<Vector2> submitted = Flatten(submittedStrokes);
        List<Vector2> template = Flatten(templateStrokes);

        if (submitted.Count < MinimumPointCount || template.Count < MinimumPointCount)
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.TooFewPoints);

        if (!DirectionMatches(submitted, template))
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.DirectionMismatch);

        if (!PathMatches(submitted, template, Mathf.Max(1f, tolerancePixels)))
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.PathMismatch);

        return Level1TutorialValidationResult.Correct();
    }

    private static List<Vector2> Flatten(IReadOnlyList<List<Vector2>> strokes)
    {
        List<Vector2> result = new();
        if (strokes == null)
            return result;

        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            if (stroke == null)
                continue;

            for (int j = 0; j < stroke.Count; j++)
                result.Add(stroke[j]);
        }

        return result;
    }

    private static bool DirectionMatches(IReadOnlyList<Vector2> submitted, IReadOnlyList<Vector2> template)
    {
        Vector2 submittedDirection = submitted[submitted.Count - 1] - submitted[0];
        Vector2 templateDirection = template[template.Count - 1] - template[0];

        if (submittedDirection.sqrMagnitude <= Mathf.Epsilon ||
            templateDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return true;
        }

        return Vector2.Dot(submittedDirection.normalized, templateDirection.normalized) >= 0f;
    }

    private static bool PathMatches(IReadOnlyList<Vector2> submitted, IReadOnlyList<Vector2> template, float tolerancePixels)
    {
        int sampleCount = Mathf.Max(submitted.Count, template.Count, 2);
        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            Vector2 a = SamplePolyline(submitted, t);
            Vector2 b = SamplePolyline(template, t);
            if (Vector2.Distance(a, b) > tolerancePixels)
                return false;
        }

        return true;
    }

    private static Vector2 SamplePolyline(IReadOnlyList<Vector2> points, float t)
    {
        if (points == null || points.Count == 0)
            return Vector2.zero;

        if (points.Count == 1)
            return points[0];

        float scaled = Mathf.Clamp01(t) * (points.Count - 1);
        int index = Mathf.FloorToInt(scaled);
        if (index >= points.Count - 1)
            return points[points.Count - 1];

        float localT = scaled - index;
        return Vector2.Lerp(points[index], points[index + 1], localT);
    }
}
