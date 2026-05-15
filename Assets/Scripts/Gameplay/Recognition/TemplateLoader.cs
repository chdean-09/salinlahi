using System.Collections.Generic;
using UnityEngine;
// Loads Baybayin character template point clouds from Resources / Templates /.
// Template files are plain text: one x,y pair per line.
// Example file: BA_template.txt
// -0.123, 0.456
// -0.120, 0.450
// ... (32+ points)
public class TemplateLoader
{
    private const string RESOURCES_PATH = "Templates";

    public Dictionary<string, List<List<List<Vector2>>>> LoadAll()
    {
        var result = new Dictionary<string, List<List<List<Vector2>>>>();
        TextAsset[] assets = Resources.LoadAll<TextAsset>(RESOURCES_PATH);
        if (assets.Length == 0)
            DebugLogger.LogWarning($"TemplateLoader: No templates found in Resources/{RESOURCES_PATH}/");

        foreach (TextAsset asset in assets)
        {
            if (!TemplateVariantNameUtility.TryExtractCharacterID(asset.name, out string id))
            {
                DebugLogger.LogWarning($"TemplateLoader: Skipping template '{asset.name}' due to invalid naming. Expected ID_template_NN (e.g. BA_template_01).");
                continue;
            }

            List<List<Vector2>> parsedStrokes = StrokeTextParser.ParseStrokes(asset.text);
            int pointCount = StrokeTextParser.FlattenStrokes(parsedStrokes).Count;
            if (pointCount > 0)
            {
                if (!result.TryGetValue(id, out List<List<List<Vector2>>> variants))
                {
                    variants = new List<List<List<Vector2>>>();
                    result[id] = variants;
                }

                variants.Add(parsedStrokes);
                DebugLogger.Log($"TemplateLoader: Loaded '{asset.name}' -> '{id}' with {parsedStrokes.Count} strokes and {pointCount} points.");
            }
            else
            {
                DebugLogger.LogWarning($"TemplateLoader: Template '{id}' had no valid points after parsing strokes.");
            }
        }

        return result;
    }
}
