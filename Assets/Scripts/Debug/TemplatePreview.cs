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

    private readonly List<LineRenderer> _strokeRenderers = new List<LineRenderer>();

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
        GetRendererForStrokeIndex(0);
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
            ClearAllRenderers();
            return;
        }

        List<List<Vector2>> strokes = StrokeTextParser.ParseStrokes(asset.text);
        int strokeCount = strokes.Count;
        int pointCount = 0;
        for (int i = 0; i < strokeCount; i++)
            pointCount += strokes[i].Count;

        if (pointCount < 2)
        {
            Debug.LogWarning($"TemplatePreview: '{resourcePath}' had {strokeCount} strokes and {pointCount} points.");
            ClearAllRenderers();
            return;
        }

        int renderedStrokeCount = 0;
        for (int i = 0; i < strokeCount; i++)
        {
            List<Vector2> stroke = strokes[i];
            if (stroke.Count < 2) continue;

            LineRenderer renderer = GetRendererForStrokeIndex(renderedStrokeCount);
            renderer.enabled = true;
            renderer.widthMultiplier = _lineWidth;
            renderer.startColor = _lineColor;
            renderer.endColor = _lineColor;
            renderer.positionCount = stroke.Count;

            // Template is bbox-normalized to [0,1]. Map to a square centered at _displayCenter.
            for (int j = 0; j < stroke.Count; j++)
            {
                float x = _displayCenter.x + (stroke[j].x - 0.5f) * _displaySize;
                float y = _displayCenter.y + (stroke[j].y - 0.5f) * _displaySize;
                renderer.SetPosition(j, new Vector3(x, y, 0f));
            }

            renderedStrokeCount++;
        }

        for (int i = renderedStrokeCount; i < _strokeRenderers.Count; i++)
        {
            if (_strokeRenderers[i] == null) continue;
            _strokeRenderers[i].positionCount = 0;
            _strokeRenderers[i].enabled = false;
        }

        Debug.Log($"TemplatePreview: rendered {resourcePath} with {strokeCount} strokes and {pointCount} points (drawn strokes: {renderedStrokeCount}).");
    }

    private LineRenderer GetRendererForStrokeIndex(int strokeIndex)
    {
        while (strokeIndex >= _strokeRenderers.Count)
        {
            LineRenderer renderer;
            if (_strokeRenderers.Count == 0)
            {
                renderer = GetComponent<LineRenderer>();
                if (renderer == null) renderer = gameObject.AddComponent<LineRenderer>();
            }
            else
            {
                GameObject strokeObj = new GameObject($"PreviewStroke_{_strokeRenderers.Count + 1}");
                strokeObj.transform.SetParent(transform, false);
                renderer = strokeObj.AddComponent<LineRenderer>();
            }

            renderer.material = _lineMaterial ?? new Material(Shader.Find("Sprites/Default"));
            renderer.useWorldSpace = false;
            renderer.loop = false;
            renderer.numCapVertices = 8;
            renderer.numCornerVertices = 8;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.alignment = LineAlignment.View;
            renderer.positionCount = 0;
            renderer.enabled = false;
            _strokeRenderers.Add(renderer);
        }

        return _strokeRenderers[strokeIndex];
    }

    private void ClearAllRenderers()
    {
        for (int i = 0; i < _strokeRenderers.Count; i++)
        {
            if (_strokeRenderers[i] == null) continue;
            _strokeRenderers[i].positionCount = 0;
            _strokeRenderers[i].enabled = false;
        }
    }
}
