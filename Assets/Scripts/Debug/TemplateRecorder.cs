// TemplateRecorder.cs -- temporary debug tool, DELETE before Sprint 2
// Desktop-friendly version using mouse input
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TemplateRecorder : MonoBehaviour
{
    private enum SaveMode
    {
        Template,
        TestDraw
    }

    [Header("Save Output")]
    [SerializeField] private SaveMode _saveMode = SaveMode.Template;
    [SerializeField] private string _saveAsCharacterID = "BA";
    [SerializeField] private bool _useNumberedFileNames = true;
    [SerializeField] private int _templateNumber = 1;
    [SerializeField] private bool _autoIncrementTemplateNumber = true;
    [SerializeField] private int _drawNumber = 1;
    [SerializeField] private bool _autoIncrementDrawNumber = true;
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private float _lineWidth = 0.02f;
    [SerializeField] private int _minimumStrokePointCount = 3;
    [SerializeField] private bool _clearAfterSave = true;
    [SerializeField] private bool _showOverlayButtons = true;

    [Header("Guide Overlay")]
    [SerializeField] private Sprite _guideSprite;
    [SerializeField] private bool _guideVisible = true;
    [SerializeField, Range(0f, 1f)] private float _guideAlpha = 0.35f;
    [SerializeField] private Vector3 _guideLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 _guideLocalScale = Vector3.one;
    [SerializeField] private int _guideSortingOrder = -1;
    [SerializeField] private string _guideResourcesPath = "YA";
    [SerializeField] private bool _loadGuideFromResourcesOnStart = true;

    private List<List<Vector2>> _strokes = new List<List<Vector2>>();
    private List<Vector2> _activeStrokePoints;
    private bool _drawing = false;
    private LineRenderer _lr;
    private readonly List<LineRenderer> _strokeRenderers = new List<LineRenderer>();
    private Camera _mainCamera;
    private SpriteRenderer _guideRenderer;
    private const string GuideObjectName = "GuideImage";

    // Optional convenience for external visualisers / debug tools
    public List<Vector2> PreviewPoints => FlattenPoints();

    private void Start()
    {
        _mainCamera = Camera.main;
        _lr = GetComponent<LineRenderer>();
        if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
        ConfigureLineRenderer(_lr);
        _strokeRenderers.Add(_lr);
        EnsureGuideRenderer();
        TryLoadGuideOnStart();
        RefreshGuideVisual();
    }

    private void OnValidate()
    {
        // Keep inspector edits reflected while in play mode without restarting.
        if (!Application.isPlaying) return;
        EnsureGuideRenderer();
        RefreshGuideVisual();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            BeginStroke();
        }

        if (_drawing && Input.GetMouseButton(0))
        {
            _activeStrokePoints.Add((Vector2)Input.mousePosition);
            UpdateLineRenderer(_activeStrokePoints, GetRendererForStrokeIndex(_strokes.Count - 1));
        }

        if (_drawing && Input.GetMouseButtonUp(0))
        {
            EndStroke();
        }
    }

    public void SaveCurrentCharacter()
    {
        SaveTemplate();
    }

    public void SetGuideSprite(Sprite sprite)
    {
        _guideSprite = sprite;
        EnsureGuideRenderer();
        RefreshGuideVisual();
    }

    public void SetGuideVisible(bool isVisible)
    {
        _guideVisible = isVisible;
        RefreshGuideVisual();
    }

    public void SetGuideAlpha(float alpha)
    {
        _guideAlpha = Mathf.Clamp01(alpha);
        RefreshGuideVisual();
    }

    public bool SetGuideSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath)) return false;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite == null)
        {
            Debug.LogWarning($"TemplateRecorder: No guide sprite found at Resources/{resourcesPath}.");
            return false;
        }

        SetGuideSprite(sprite);
        return true;
    }

    public bool SetGuideSpriteFromCharacterID(string characterID)
    {
        if (string.IsNullOrWhiteSpace(characterID)) return false;

        string canonical = BaybayinIdCanonicalizer.Canonicalize(characterID);
        if (string.IsNullOrEmpty(canonical)) return false;

        _guideResourcesPath = canonical;

        List<string> candidates = BaybayinIdCanonicalizer.GetSpriteResourceCandidates(characterID);
        for (int i = 0; i < candidates.Count; i++)
        {
            Sprite sprite = Resources.Load<Sprite>(candidates[i]);
            if (sprite == null) continue;

            SetGuideSprite(sprite);
            return true;
        }

        Debug.LogWarning($"TemplateRecorder: No guide sprite found for character ID '{characterID}'. Tried canonical '{canonical}'.");
        return false;
    }

    public void ClearCurrentDrawing()
    {
        _drawing = false;
        _activeStrokePoints = null;
        _strokes.Clear();

        for (int i = 0; i < _strokeRenderers.Count; i++)
        {
            if (_strokeRenderers[i] != null)
                _strokeRenderers[i].positionCount = 0;
        }
    }

    private void BeginStroke()
    {
        _drawing = true;
        _activeStrokePoints = new List<Vector2>();
        _strokes.Add(_activeStrokePoints);

        LineRenderer renderer = GetRendererForStrokeIndex(_strokes.Count - 1);
        if (renderer != null) renderer.positionCount = 0;
    }

    private void EndStroke()
    {
        _drawing = false;
        if (_activeStrokePoints == null) return;

        if (_activeStrokePoints.Count < _minimumStrokePointCount)
        {
            int lastIndex = _strokes.Count - 1;
            if (lastIndex >= 0)
            {
                _strokes.RemoveAt(lastIndex);
                LineRenderer renderer = GetRendererForStrokeIndex(lastIndex);
                if (renderer != null) renderer.positionCount = 0;
            }
        }

        _activeStrokePoints = null;
    }

    private void UpdateLineRenderer(List<Vector2> strokePoints, LineRenderer targetRenderer)
    {
        if (targetRenderer == null || _mainCamera == null || strokePoints == null || strokePoints.Count == 0) return;
        // Compute a safe positive distance from the camera for ScreenToWorldPoint.
        // Use the absolute camera Z (common case: camera at z = -10) + small offset,
        // otherwise fall back to a near-plane based distance.
        float camZ = _mainCamera.transform.position.z;
        float z = (Mathf.Abs(camZ) > 0.01f) ? Mathf.Abs(camZ) + 0.1f : (_mainCamera.nearClipPlane + 0.1f);
        // Densify points: if consecutive screen points are far apart, interpolate extra points
        var worldPoints = new List<Vector3>();
        for (int i = 0; i < strokePoints.Count; i++)
        {
            if (i == 0)
            {
                Vector3 wp0 = _mainCamera.ScreenToWorldPoint(new Vector3(strokePoints[i].x, strokePoints[i].y, z));
                worldPoints.Add(wp0);
                continue;
            }

            Vector2 prev = strokePoints[i - 1];
            Vector2 cur = strokePoints[i];
            float dist = Vector2.Distance(prev, cur);
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / 6f), 1, 20); // one point per ~6 screen pixels
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                Vector2 inter = Vector2.Lerp(prev, cur, t);
                Vector3 wp = _mainCamera.ScreenToWorldPoint(new Vector3(inter.x, inter.y, z));
                worldPoints.Add(wp);
            }
        }

        targetRenderer.positionCount = worldPoints.Count;
        for (int i = 0; i < worldPoints.Count; i++)
            targetRenderer.SetPosition(i, worldPoints[i]);
    }

    private void SaveTemplate()
    {
        List<Vector2> allPoints = FlattenPoints();
        if (allPoints.Count < 5)
        {
            Debug.LogWarning("TemplateRecorder: Not enough points to save.");
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var p in allPoints)
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
        foreach (var p in allPoints)
            sb.AppendLine($"{(p.x - minX) / w:F4}, {(p.y - minY) / h:F4}");

        string dir = BuildOutputDirectory();
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, BuildFileName());
        File.WriteAllText(path, sb.ToString());

        Debug.Log($"Saved template: {path} ({_strokes.Count} strokes, {allPoints.Count} points)");

        if (_saveMode == SaveMode.Template)
        {
            if (_useNumberedFileNames && _autoIncrementTemplateNumber)
                _templateNumber++;
        }
        else
        {
            if (_autoIncrementDrawNumber)
                _drawNumber++;
        }

        if (_clearAfterSave)
            ClearCurrentDrawing();
    }

    private string BuildFileName()
    {
        string id = BaybayinIdCanonicalizer.Canonicalize(_saveAsCharacterID);
        if (string.IsNullOrEmpty(id))
            id = "UNSET";

        if (_saveMode == SaveMode.TestDraw)
            return $"{id}_draw_{Mathf.Max(1, _drawNumber):00}.txt";

        if (_useNumberedFileNames)
            return $"{id}_template_{Mathf.Max(1, _templateNumber):00}.txt";

        return id + "_template.txt";
    }

    private string BuildOutputDirectory()
    {
        string resourcesDir = Path.Combine(Application.dataPath, "Resources");
        if (_saveMode == SaveMode.TestDraw)
            return Path.Combine(resourcesDir, "TestDraws");

        return Path.Combine(resourcesDir, "Templates");
    }

    private void ConfigureLineRenderer(LineRenderer renderer)
    {
        if (renderer == null) return;
        renderer.material = _lineMaterial ?? new Material(Shader.Find("Sprites/Default"));
        renderer.widthMultiplier = _lineWidth;
        renderer.positionCount = 0;
        renderer.loop = false;
        renderer.useWorldSpace = true;
        // Make the line render smoothly and fill gaps
        renderer.numCapVertices = 8;
        renderer.numCornerVertices = 8;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.alignment = LineAlignment.View;
    }

    private LineRenderer GetRendererForStrokeIndex(int strokeIndex)
    {
        if (strokeIndex < 0) return null;

        while (strokeIndex >= _strokeRenderers.Count)
        {
            GameObject strokeObj = new GameObject($"StrokeLine_{_strokeRenderers.Count + 1}");
            strokeObj.transform.SetParent(transform, false);
            LineRenderer extraRenderer = strokeObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(extraRenderer);
            _strokeRenderers.Add(extraRenderer);
        }

        return _strokeRenderers[strokeIndex];
    }

    private void EnsureGuideRenderer()
    {
        if (_guideRenderer != null) return;

        Transform guideTransform = transform.Find(GuideObjectName);
        if (guideTransform == null)
        {
            GameObject guideObject = new GameObject(GuideObjectName);
            guideObject.transform.SetParent(transform, false);
            guideTransform = guideObject.transform;
        }

        _guideRenderer = guideTransform.GetComponent<SpriteRenderer>();
        if (_guideRenderer == null)
            _guideRenderer = guideTransform.gameObject.AddComponent<SpriteRenderer>();
    }

    private void RefreshGuideVisual()
    {
        if (_guideRenderer == null) return;

        Transform guideTransform = _guideRenderer.transform;
        guideTransform.localPosition = _guideLocalPosition;
        guideTransform.localScale = _guideLocalScale;

        _guideRenderer.sprite = _guideSprite;
        _guideRenderer.sortingOrder = _guideSortingOrder;

        Color color = _guideRenderer.color;
        color.a = _guideAlpha;
        _guideRenderer.color = color;

        bool shouldShow = _guideVisible && _guideSprite != null;
        _guideRenderer.enabled = shouldShow;
    }

    private void TryLoadGuideOnStart()
    {
        if (!_loadGuideFromResourcesOnStart) return;
        if (_guideSprite != null) return;
        if (string.IsNullOrWhiteSpace(_guideResourcesPath)) return;

        SetGuideSpriteFromCharacterID(_guideResourcesPath);
    }

    private List<Vector2> FlattenPoints()
    {
        var allPoints = new List<Vector2>();
        for (int i = 0; i < _strokes.Count; i++)
        {
            if (_strokes[i] != null)
                allPoints.AddRange(_strokes[i]);
        }

        return allPoints;
    }

    private void OnGUI()
    {
        if (!_showOverlayButtons) return;

        GUILayout.BeginArea(new Rect(12, 12, 220, 120), GUI.skin.box);
        GUILayout.Label("Template Recorder");
        if (GUILayout.Button("Save Character")) SaveCurrentCharacter();
        if (GUILayout.Button("Clear Drawing")) ClearCurrentDrawing();
        if (GUILayout.Button(_guideVisible ? "Hide Guide" : "Show Guide")) SetGuideVisible(!_guideVisible);
        GUILayout.EndArea();
    }
}