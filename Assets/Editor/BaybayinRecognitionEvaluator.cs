using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BaybayinRecognitionEvaluator
{
    private const string TestDrawsFolder = "Assets/Resources/TestDraws";
    private static readonly string[] ExpectedCharacterIDs =
    {
        "A", "BA", "DA", "EI", "GA", "HA", "KA", "LA", "MA", "NA", "NGA", "OU", "PA", "RA", "SA", "TA", "WA", "YA"
    };

    private class AccuracyStats
    {
        public int Total;
        public int Correct;

        public float AccuracyPercent => Total > 0 ? (float)Correct / Total * 100f : 0f;
    }

    [MenuItem("Salinlahi/Validation/Evaluate Baybayin Recognition")]
    public static void EvaluateRecognition()
    {
        if (!Directory.Exists(TestDrawsFolder))
        {
            Debug.LogError($"[BaybayinEval] Missing test draws folder: {TestDrawsFolder}");
            Debug.Log("[BaybayinEval] Add test draw files named like BA_draw_01.txt under Assets/Resources/TestDraws/");
            return;
        }

        RecognitionConfigSO config = FindRecognitionConfig();
        int resampleCount = config != null ? config.resamplePointCount : 32;

        var loader = new TemplateLoader();
        Dictionary<string, List<List<Vector2>>> templates = loader.LoadAll();

        var recognizer = new DollarPRecognizer(resampleCount);
        recognizer.SetTemplateVariants(templates);

        string[] drawFiles = Directory.GetFiles(TestDrawsFolder, "*.txt", SearchOption.TopDirectoryOnly);
        if (drawFiles.Length == 0)
        {
            Debug.LogError("[BaybayinEval] No draw sample files found.");
            return;
        }

        int total = 0;
        int correct = 0;
        var perCharacter = CreateCharacterStatsDictionary();
        var confusion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var perCharacterPredictedVariant = new Dictionary<string, Dictionary<int, AccuracyStats>>(StringComparer.OrdinalIgnoreCase);
        var expectedVariantStats = new Dictionary<string, Dictionary<int, AccuracyStats>>(StringComparer.OrdinalIgnoreCase);
        int drawsWithExpectedVariant = 0;

        foreach (string drawFile in drawFiles)
        {
            string fileNameNoExt = Path.GetFileNameWithoutExtension(drawFile).ToUpperInvariant();
            string expectedID = ExtractExpectedID(fileNameNoExt);
            if (string.IsNullOrEmpty(expectedID))
            {
                Debug.LogWarning($"[BaybayinEval] Skipping sample '{fileNameNoExt}' due to invalid name. Expected ID_draw_01 format.");
                continue;
            }

            List<Vector2> points = ParsePoints(File.ReadAllText(drawFile));
            if (points.Count < 8)
            {
                Debug.LogWarning($"[BaybayinEval] Skipping sample '{fileNameNoExt}' with too few points ({points.Count}).");
                continue;
            }

            RecognitionResult result = recognizer.Recognize(points);
            total++;

            bool isCorrect = string.Equals(expectedID, result.characterID, StringComparison.OrdinalIgnoreCase);
            if (isCorrect) correct++;

            AccuracyStats charStats = GetOrCreateStats(perCharacter, expectedID);
            charStats.Total++;
            if (isCorrect) charStats.Correct++;

            var perVariantForCharacter = GetOrCreateNestedStats(perCharacterPredictedVariant, expectedID);
            AccuracyStats predictedVariantStats = GetOrCreateStats(perVariantForCharacter, result.templateVariantIndex);
            predictedVariantStats.Total++;
            if (isCorrect) predictedVariantStats.Correct++;

            if (TryExtractExpectedTemplateVariant(fileNameNoExt, out int expectedTemplateVariant))
            {
                drawsWithExpectedVariant++;
                var expectedVariantForCharacter = GetOrCreateNestedStats(expectedVariantStats, expectedID);
                AccuracyStats expectedStats = GetOrCreateStats(expectedVariantForCharacter, expectedTemplateVariant);
                expectedStats.Total++;
                if (isCorrect) expectedStats.Correct++;
            }

            string predictedID = CanonicalizeID(result.characterID);
            if (!string.Equals(expectedID, predictedID, StringComparison.OrdinalIgnoreCase))
            {
                string confusionKey = $"{expectedID}->{predictedID}";
                confusion[confusionKey] = confusion.TryGetValue(confusionKey, out int existing) ? existing + 1 : 1;
            }

            if (!isCorrect)
            {
                Debug.LogWarning(
                    $"[BaybayinEval] Miss: expected={expectedID} predicted={result.characterID} predictedVariant={result.templateVariantIndex} score={result.score:F3} sample={fileNameNoExt}");
            }
        }

        if (total == 0)
        {
            Debug.LogError("[BaybayinEval] No valid samples were evaluated.");
            return;
        }

        float accuracy = (float)correct / total * 100f;
        Debug.Log($"[BaybayinEval] Top-1 accuracy: {accuracy:F2}% ({correct}/{total})");

        PrintPerCharacterReport(perCharacter, templates);
        PrintPerCharacterTemplateVariantReport(perCharacterPredictedVariant, templates);

        if (drawsWithExpectedVariant > 0)
            PrintExpectedTemplateVariantReport(expectedVariantStats, templates, drawsWithExpectedVariant);
        else
            Debug.Log("[BaybayinEval] Expected-template variant report skipped. Add sample names like BA_template_03_draw_01.txt to enable it.");

        PrintTopConfusions(confusion, 10);

        if (total != 90)
            Debug.LogWarning($"[BaybayinEval] Evaluated {total} samples. AC-4 expects 90 (5 draws x 18 characters).");

        if (accuracy >= 80f)
            Debug.Log("[BaybayinEval] PASS: Accuracy meets AC-4 (>= 80%).");
        else
            Debug.LogError("[BaybayinEval] FAIL: Accuracy below AC-4 target (>= 80%).");
    }

    private static RecognitionConfigSO FindRecognitionConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:RecognitionConfigSO");
        if (guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<RecognitionConfigSO>(path);
    }

    private static string ExtractExpectedID(string fileNameNoExt)
    {
        int marker = fileNameNoExt.IndexOf("_DRAW_", StringComparison.Ordinal);
        if (marker <= 0) return string.Empty;

        string raw = fileNameNoExt.Substring(0, marker);

        // Supports optional expected-template marker in test draw names:
        // BA_TEMPLATE_03_DRAW_01.txt => expected character ID BA
        int templateMarker = raw.IndexOf("_TEMPLATE_", StringComparison.Ordinal);
        if (templateMarker > 0)
            raw = raw.Substring(0, templateMarker);

        return CanonicalizeID(raw);
    }

    private static bool TryExtractExpectedTemplateVariant(string fileNameNoExt, out int variantIndex)
    {
        variantIndex = -1;

        int templateMarker = fileNameNoExt.IndexOf("_TEMPLATE_", StringComparison.Ordinal);
        if (templateMarker < 0)
            return false;

        int numberStart = templateMarker + "_TEMPLATE_".Length;
        if (numberStart >= fileNameNoExt.Length)
            return false;

        int drawMarker = fileNameNoExt.IndexOf("_DRAW_", numberStart, StringComparison.Ordinal);
        string numberPart = drawMarker > numberStart
            ? fileNameNoExt.Substring(numberStart, drawMarker - numberStart)
            : fileNameNoExt.Substring(numberStart);

        if (!int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
            return false;

        variantIndex = parsed;
        return true;
    }

    private static string CanonicalizeID(string id)
    {
        return BaybayinIdCanonicalizer.Canonicalize(id);
    }

    private static Dictionary<string, AccuracyStats> CreateCharacterStatsDictionary()
    {
        var result = new Dictionary<string, AccuracyStats>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ExpectedCharacterIDs)
            result[id] = new AccuracyStats();
        return result;
    }

    private static AccuracyStats GetOrCreateStats<TKey>(Dictionary<TKey, AccuracyStats> stats, TKey key)
    {
        if (!stats.TryGetValue(key, out AccuracyStats value))
        {
            value = new AccuracyStats();
            stats[key] = value;
        }

        return value;
    }

    private static Dictionary<int, AccuracyStats> GetOrCreateNestedStats(
        Dictionary<string, Dictionary<int, AccuracyStats>> source,
        string characterID)
    {
        if (!source.TryGetValue(characterID, out Dictionary<int, AccuracyStats> nested))
        {
            nested = new Dictionary<int, AccuracyStats>();
            source[characterID] = nested;
        }

        return nested;
    }

    private static void PrintPerCharacterReport(
        Dictionary<string, AccuracyStats> perCharacter,
        Dictionary<string, List<List<Vector2>>> templates)
    {
        Debug.Log("[BaybayinEval] Per-character accuracy (expectedID -> accuracy, evaluated draws, loaded template count):");

        foreach (string id in ExpectedCharacterIDs)
        {
            AccuracyStats stats = GetOrCreateStats(perCharacter, id);
            int templateCount = templates.TryGetValue(id, out List<List<Vector2>> variants) ? variants.Count : 0;
            Debug.Log($"[BaybayinEval] {id}: {stats.AccuracyPercent:F2}% ({stats.Correct}/{stats.Total}) templates={templateCount}");
        }
    }

    private static void PrintPerCharacterTemplateVariantReport(
        Dictionary<string, Dictionary<int, AccuracyStats>> perCharacterPredictedVariant,
        Dictionary<string, List<List<Vector2>>> templates)
    {
        Debug.Log("[BaybayinEval] Per-character predicted template-variant accuracy (winner variant index):");

        foreach (string id in ExpectedCharacterIDs)
        {
            int templateCount = templates.TryGetValue(id, out List<List<Vector2>> variants) ? variants.Count : 0;
            Debug.Log($"[BaybayinEval] {id}: loadedVariants={templateCount}");

            if (!perCharacterPredictedVariant.TryGetValue(id, out Dictionary<int, AccuracyStats> byVariant) || byVariant.Count == 0)
            {
                Debug.Log($"[BaybayinEval]   (no evaluated draws)");
                continue;
            }

            foreach (KeyValuePair<int, AccuracyStats> kvp in byVariant.OrderBy(k => k.Key))
            {
                AccuracyStats stats = kvp.Value;
                Debug.Log($"[BaybayinEval]   predictedVariant={kvp.Key}: {stats.AccuracyPercent:F2}% ({stats.Correct}/{stats.Total})");
            }
        }
    }

    private static void PrintExpectedTemplateVariantReport(
        Dictionary<string, Dictionary<int, AccuracyStats>> expectedVariantStats,
        Dictionary<string, List<List<Vector2>>> templates,
        int drawsWithExpectedVariant)
    {
        Debug.Log($"[BaybayinEval] Expected-template variant accuracy for {drawsWithExpectedVariant} labeled draw(s):");

        foreach (string id in ExpectedCharacterIDs)
        {
            int templateCount = templates.TryGetValue(id, out List<List<Vector2>> variants) ? variants.Count : 0;
            Debug.Log($"[BaybayinEval] {id}: loadedVariants={templateCount}");

            if (!expectedVariantStats.TryGetValue(id, out Dictionary<int, AccuracyStats> byVariant) || byVariant.Count == 0)
            {
                Debug.Log($"[BaybayinEval]   (no expected-template labels in draws)");
                continue;
            }

            foreach (KeyValuePair<int, AccuracyStats> kvp in byVariant.OrderBy(k => k.Key))
            {
                AccuracyStats stats = kvp.Value;
                Debug.Log($"[BaybayinEval]   expectedTemplate={kvp.Key}: {stats.AccuracyPercent:F2}% ({stats.Correct}/{stats.Total})");
            }
        }
    }

    private static void PrintTopConfusions(Dictionary<string, int> confusion, int topN)
    {
        if (confusion.Count == 0)
        {
            Debug.Log("[BaybayinEval] No confusions observed in evaluated samples.");
            return;
        }

        Debug.Log($"[BaybayinEval] Top {topN} confusions (expected->predicted):");
        foreach (KeyValuePair<string, int> kvp in confusion.OrderByDescending(k => k.Value).ThenBy(k => k.Key).Take(topN))
            Debug.Log($"[BaybayinEval]   {kvp.Key}: {kvp.Value}");
    }

    private static List<Vector2> ParsePoints(string text)
    {
        var points = new List<Vector2>();
        string[] lines = text.Split('\n');
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] parts = line.Split(',');
            if (parts.Length != 2) continue;

            bool parsedX = float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            bool parsedY = float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            if (parsedX && parsedY)
                points.Add(new Vector2(x, y));
        }

        return points;
    }
}
