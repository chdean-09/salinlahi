using System.Collections.Generic;
using UnityEngine;

// Renders a saved template file from Resources/Templates/ as a LineRenderer
// so recorded point clouds can be eyeballed for shape correctness.
public class TemplatePreview : MonoBehaviour
{
    [Header("Template")]
    [SerializeField] private string _characterID = "KA";
    [SerializeField, Min(1)] private int _variantNumber = 1;
    [SerializeField] private bool _useNumberedFileName = true;

    [Header("Display")]
    [SerializeField] private Vector2 _displayCenter = Vector2.zero;
    [SerializeField, Min(0.1f)] private float _displaySize = 4f;
    [SerializeField] private Color _lineColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private float _lineWidth = 0.05f;
    [SerializeField] private Material _lineMaterial;

    [Header("Auto-reload")]
    [SerializeField] private bool _reloadOnValidate = true;

    private LineRenderer _lr;

    private void Start()
    {
        EnsureLineRenderer();
        LoadAndRender();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (!_reloadOnValidate) return;
        EnsureLineRenderer();
        LoadAndRender();
    }

    private void EnsureLineRenderer()
    {
        if (_lr != null) return;
        _lr = GetComponent<LineRenderer>();
        if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
        _lr.material = _lineMaterial ?? new Material(Shader.Find("Sprites/Default"));
        _lr.useWorldSpace = false;
        _lr.loop = false;
        _lr.numCapVertices = 8;
        _lr.numCornerVertices = 8;
        _lr.textureMode = LineTextureMode.Stretch;
        _lr.alignment = LineAlignment.View;
    }

    private void LoadAndRender()
    {
        string id = BaybayinIdCanonicalizer.Canonicalize(_characterID);
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"TemplatePreview: invalid character ID '{_characterID}'.");
            return;
        }

        string fileName = _useNumberedFileName
            ? $"{id}_template_{Mathf.Max(1, _variantNumber):00}"
            : $"{id}_template";
        string resourcePath = $"Templates/{fileName}";

        TextAsset asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"TemplatePreview: could not load Resources/{resourcePath}.txt");
            _lr.positionCount = 0;
            return;
        }

        List<Vector2> points = ParsePoints(asset.text);
        if (points.Count < 2)
        {
            Debug.LogWarning($"TemplatePreview: '{resourcePath}' had {points.Count} points.");
            _lr.positionCount = 0;
            return;
        }

        // Template is bbox-normalized to [0,1]. Map to a square centered at _displayCenter.
        _lr.widthMultiplier = _lineWidth;
        _lr.startColor = _lineColor;
        _lr.endColor = _lineColor;
        _lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            float x = _displayCenter.x + (points[i].x - 0.5f) * _displaySize;
            float y = _displayCenter.y + (points[i].y - 0.5f) * _displaySize;
            _lr.SetPosition(i, new Vector3(x, y, 0f));
        }

        Debug.Log($"TemplatePreview: rendered {resourcePath} ({points.Count} points).");
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
            if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                points.Add(new Vector2(x, y));
            }
        }
        return points;
    }
}
