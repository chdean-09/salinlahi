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
    public Dictionary<string, List<Vector2>> LoadAll()
    {
        var result = new Dictionary<string, List<Vector2>>();
        TextAsset[] assets = Resources.LoadAll<TextAsset>(RESOURCES_PATH);
        if (assets.Length == 0)
            DebugLogger.LogWarning($"TemplateLoader: No templates found in Resources/{RESOURCES_PATH}/");
        foreach (TextAsset asset in assets)
        {
            // Asset name format: BA_template (without .txt extension)
            string id = asset.name.Replace("_template", "").ToUpper().Trim();
            List<Vector2> pts = ParsePoints(asset.text);
            if (pts.Count > 0)
            {
                result[id] = pts;
                DebugLogger.Log($"TemplateLoader: Loaded '{id}' with {pts.Count} points.");
            }
            else
            {
                DebugLogger.LogWarning($"TemplateLoader: Template '{id}' had no valid points.");
            }
        }
        return result;
    }
    private List<Vector2> ParsePoints(string text)
    {
        var points = new List<Vector2>();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            string[] parts = line.Split(',');
            if (parts.Length != 2) continue;
            if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                points.Add(new Vector2(x, y));
            }
        }
        return points;
    }
}