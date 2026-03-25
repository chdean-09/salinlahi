// TemplateRecorder.cs -- temporary debug tool, DELETE before Sprint 2
// Desktop-friendly version using mouse input
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TemplateRecorder : MonoBehaviour
{
    [SerializeField] private string _saveAsCharacterID = "BA";
    private List<Vector2> _points = new List<Vector2>();
    private bool _drawing = false;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _points.Clear();
            _drawing = true;
        }

        if (_drawing && Input.GetMouseButton(0))
        {
            _points.Add((Vector2)Input.mousePosition);
        }

        if (_drawing && Input.GetMouseButtonUp(0))
        {
            _drawing = false;
            SaveTemplate();
        }
    }

    private void SaveTemplate()
    {
        if (_points.Count < 5) return; // ignore accidental clicks

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var p in _points)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        float w = maxX - minX;
        float h = maxY - minY;

        if (w < 1f || h < 1f) return; // drawing too small

        var sb = new System.Text.StringBuilder();
        foreach (var p in _points)
            sb.AppendLine($"{(p.x - minX) / w:F4}, {(p.y - minY) / h:F4}");

        string dir = Path.Combine(Application.dataPath, "Resources", "Templates");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, _saveAsCharacterID + "_template.txt");
        File.WriteAllText(path, sb.ToString());

        Debug.Log($"Saved template: {path} ({_points.Count} points)");
    }
}