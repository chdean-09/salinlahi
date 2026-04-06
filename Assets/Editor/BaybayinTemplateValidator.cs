using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BaybayinTemplateValidator
{
    private static readonly string[] ExpectedCharacterIDs =
    {
        "BA", "KA", "DA", "RA", "GA", "HA", "LA", "MA", "NA", "NGA", "PA", "SA", "TA", "WA", "YA", "A", "EI", "OU"
    };

    private const string TemplatesFolder = "Assets/Resources/Templates";

    [MenuItem("Salinlahi/Validation/Validate Baybayin Templates")]
    public static void ValidateTemplates()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!Directory.Exists(TemplatesFolder))
        {
            errors.Add($"Missing templates folder: {TemplatesFolder}");
            PrintResults(errors, warnings);
            return;
        }

        string[] files = Directory.GetFiles(TemplatesFolder, "*.txt", SearchOption.TopDirectoryOnly);
        ValidateTemplateFiles(files, errors, warnings);
        ValidateCharacterCoverage(files, errors);
        ValidateCharacterAssets(errors, warnings);

        PrintResults(errors, warnings);
    }

    private static void ValidateTemplateFiles(string[] files, List<string> errors, List<string> warnings)
    {
        if (files.Length < 54 || files.Length > 90)
            warnings.Add($"Template count is {files.Length}. AC-1 expects between 54 and 90 for 3-5 variants across 18 characters.");

        foreach (string filePath in files)
        {
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);

            if (!fileNameNoExt.Contains("_template_", StringComparison.OrdinalIgnoreCase))
                warnings.Add($"File '{fileNameNoExt}' does not match numbered variant naming (expected *_template_01 style).");

            string resourcePath = $"Templates/{fileNameNoExt}";
            TextAsset loaded = Resources.Load<TextAsset>(resourcePath);
            if (loaded == null)
                errors.Add($"Resources.Load failed for '{resourcePath}'.");

            int validPairs = CountValidCoordinatePairs(File.ReadAllText(filePath));
            if (validPairs < 32)
                errors.Add($"Template '{fileNameNoExt}' has {validPairs} valid coordinate pairs. AC-2 requires at least 32.");
        }
    }

    private static void ValidateCharacterCoverage(string[] files, List<string> errors)
    {
        var foundIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in files)
        {
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();
            int marker = fileNameNoExt.IndexOf("_TEMPLATE", StringComparison.Ordinal);
            if (marker <= 0) continue;

            string id = fileNameNoExt.Substring(0, marker);
            id = CanonicalizeID(id);
            foundIDs.Add(id);
        }

        foreach (string expected in ExpectedCharacterIDs)
        {
            if (!foundIDs.Contains(expected))
                errors.Add($"Missing template coverage for character '{expected}'.");
        }
    }

    private static void ValidateCharacterAssets(List<string> errors, List<string> warnings)
    {
        string[] assetGuids = AssetDatabase.FindAssets("t:BaybayinCharacterSO");
        if (assetGuids.Length < ExpectedCharacterIDs.Length)
            warnings.Add($"Found {assetGuids.Length} BaybayinCharacterSO assets; expected at least {ExpectedCharacterIDs.Length}.");

        var seenIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in assetGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BaybayinCharacterSO so = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(path);
            if (so == null) continue;

            if (string.IsNullOrWhiteSpace(so.characterID))
                errors.Add($"{path}: characterID is empty.");
            else
                seenIDs.Add(CanonicalizeID(so.characterID.Trim().ToUpperInvariant()));

            if (string.IsNullOrWhiteSpace(so.templateFileName))
            {
                errors.Add($"{path}: templateFileName is empty.");
            }
            else
            {
                string normalizedTemplatePath = so.templateFileName.Trim().Replace('\\', '/');
                string templatePathWithoutExtension = Path.ChangeExtension(normalizedTemplatePath, null)?.TrimStart('/');
                string resourcePath = templatePathWithoutExtension.StartsWith("Templates/", StringComparison.OrdinalIgnoreCase)
                    ? templatePathWithoutExtension
                    : $"Templates/{templatePathWithoutExtension}";

                TextAsset loaded = Resources.Load<TextAsset>(resourcePath);
                if (loaded == null)
                    errors.Add($"{path}: Resources.Load failed for templateFileName '{so.templateFileName}' (resolved '{resourcePath}').");
            }

            if (so.displaySprite == null)
                warnings.Add($"{path}: displaySprite is unassigned.");
            if (so.pronunciationClip == null)
                warnings.Add($"{path}: pronunciationClip is unassigned.");
        }

        foreach (string expected in ExpectedCharacterIDs)
        {
            if (!seenIDs.Contains(expected))
                warnings.Add($"No BaybayinCharacterSO found for expected characterID '{expected}'.");
        }
    }

    private static int CountValidCoordinatePairs(string text)
    {
        int count = 0;
        string[] lines = text.Split('\n');
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] parts = line.Split(',');
            if (parts.Length != 2) continue;

            bool parsedX = float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            bool parsedY = float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            if (parsedX && parsedY)
                count++;
        }

        return count;
    }

    private static string CanonicalizeID(string id)
    {
        return BaybayinIdCanonicalizer.Canonicalize(id);
    }

    private static void PrintResults(List<string> errors, List<string> warnings)
    {
        foreach (string warning in warnings)
            Debug.LogWarning($"[BaybayinValidator] {warning}");

        foreach (string error in errors)
            Debug.LogError($"[BaybayinValidator] {error}");

        if (errors.Count == 0)
            Debug.Log($"[BaybayinValidator] Validation passed with {warnings.Count} warning(s).");
        else
            Debug.LogError($"[BaybayinValidator] Validation failed with {errors.Count} error(s) and {warnings.Count} warning(s).");
    }
}
