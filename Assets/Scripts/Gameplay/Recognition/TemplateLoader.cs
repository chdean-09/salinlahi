using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
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
    // TEMP: variant suffix made required to exclude the BA/KA/GA canonicals while their
    // geometric shapes (circle/triangle/zigzag) outrank real-character variants.
    private static readonly Regex s_variantPattern = new Regex(@"^(?<id>[A-Z][A-Z-]*)_TEMPLATE_\d+$", RegexOptions.Compiled);

    public Dictionary<string, List<List<Vector2>>> LoadAll()
    {
        var result = new Dictionary<string, List<List<Vector2>>>();
        TextAsset[] assets = Resources.LoadAll<TextAsset>(RESOURCES_PATH);
        if (assets.Length == 0)
            DebugLogger.LogWarning($"TemplateLoader: No templates found in Resources/{RESOURCES_PATH}/");

        foreach (TextAsset asset in assets)
        {
            if (!TryExtractCharacterID(asset.name, out string id))
            {
                DebugLogger.LogWarning($"TemplateLoader: Skipping template '{asset.name}' due to invalid naming. Expected ID_template_NN (e.g. BA_template_01).");
                continue;
            }

            List<List<Vector2>> parsedStrokes = ParseStrokes(asset.text);
            List<Vector2> pts = FlattenStrokes(parsedStrokes);
            if (pts.Count > 0)
            {
                if (!result.TryGetValue(id, out List<List<Vector2>> variants))
                {
                    variants = new List<List<Vector2>>();
                    result[id] = variants;
                }

                variants.Add(pts);
                DebugLogger.Log($"TemplateLoader: Loaded '{asset.name}' -> '{id}' with {parsedStrokes.Count} strokes and {pts.Count} points.");
            }
            else
            {
                DebugLogger.LogWarning($"TemplateLoader: Template '{id}' had no valid points after parsing strokes.");
            }
        }

        return result;
    }

    private bool TryExtractCharacterID(string assetName, out string id)
    {
        id = string.Empty;
        Match match = s_variantPattern.Match(assetName.ToUpperInvariant().Trim());
        if (!match.Success)
            return false;

        id = BaybayinIdCanonicalizer.Canonicalize(match.Groups["id"].Value);
        return !string.IsNullOrEmpty(id);
    }

    private List<List<Vector2>> ParseStrokes(string text)
    {
        var strokes = new List<List<Vector2>>();
        var current = new List<Vector2>();
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line))
            {
                if (current.Count > 0)
                {
                    strokes.Add(current);
                    current = new List<Vector2>();
                }
                continue;
            }

            string[] parts = line.Split(',');
            if (parts.Length != 2) continue;
            if (float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                current.Add(new Vector2(x, y));
            }
        }

        if (current.Count > 0)
            strokes.Add(current);

        return strokes;
    }

    private List<Vector2> FlattenStrokes(List<List<Vector2>> strokes)
    {
        var points = new List<Vector2>();
        if (strokes == null) return points;

        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            if (stroke == null || stroke.Count == 0) continue;
            points.AddRange(stroke);
        }

        return points;
    }
}
