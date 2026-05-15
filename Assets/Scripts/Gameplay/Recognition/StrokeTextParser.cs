using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class StrokeTextParser
{
    public static List<List<Vector2>> ParseStrokes(string text)
    {
        var strokes = new List<List<Vector2>>();
        var current = new List<Vector2>();
        string normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalized.Split('\n');

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
            if (parts.Length != 2)
                continue;

            bool parsedX = float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            bool parsedY = float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            if (parsedX && parsedY)
                current.Add(new Vector2(x, y));
        }

        if (current.Count > 0)
            strokes.Add(current);

        return strokes;
    }

    public static List<Vector2> FlattenStrokes(List<List<Vector2>> strokes)
    {
        var points = new List<Vector2>();
        if (strokes == null)
            return points;

        for (int i = 0; i < strokes.Count; i++)
        {
            List<Vector2> stroke = strokes[i];
            if (stroke == null || stroke.Count == 0)
                continue;
            points.AddRange(stroke);
        }

        return points;
    }
}
