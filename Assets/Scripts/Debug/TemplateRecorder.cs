// TemplateRecorder.cs -- temporary debug tool, DELETE before Sprint 2
// Desktop-friendly version using mouse input
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private float _minimumPointDistancePixels = 1f;
    [SerializeField] private int _minimumStrokePointCount = 3;
    [SerializeField] private float _minimumStrokePathLength = 12f;
    [SerializeField] private bool _preserveAspectRatioOnSave = true;
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
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (IsMouseOverOverlayButtons(mouse.position.ReadValue()))
                return;

            BeginStroke();
        }

        if (_drawing && mouse.leftButton.isPressed)
        {
            Vector2 point = mouse.position.ReadValue();
            if (TryAppendPoint(_activeStrokePoints, point))
                UpdateLineRenderer(_activeStrokePoints, GetRendererForStrokeIndex(_strokes.Count - 1));
        }

        if (_drawing && mouse.leftButton.wasReleasedThisFrame)
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

        float strokeLength = ComputeStrokePathLength(_activeStrokePoints);
        if (_activeStrokePoints.Count < _minimumStrokePointCount || strokeLength < _minimumStrokePathLength)
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

    private bool TryAppendPoint(List<Vector2> strokePoints, Vector2 point)
    {
        if (strokePoints == null) return false;
        if (strokePoints.Count == 0)
        {
            strokePoints.Add(point);
            return true;
        }

        float minDistance = Mathf.Max(0f, _minimumPointDistancePixels);
        Vector2 lastPoint = strokePoints[strokePoints.Count - 1];
        if (Vector2.Distance(lastPoint, point) < minDistance)
            return false;

        strokePoints.Add(point);
        return true;
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
        List<List<Vector2>> validStrokes = new List<List<Vector2>>();
        for (int i = 0; i < _strokes.Count; i++)
        {
            List<Vector2> stroke = _strokes[i];
            if (stroke == null || stroke.Count == 0) continue;
            validStrokes.Add(stroke);
        }

        int totalPointCount = 0;
        for (int i = 0; i < validStrokes.Count; i++)
            totalPointCount += validStrokes[i].Count;

        if (totalPointCount < 5)
        {
            Debug.LogWarning("TemplateRecorder: Not enough points to save."
                + $" Strokes={validStrokes.Count}, Points={totalPointCount}");
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int i = 0; i < validStrokes.Count; i++)
        {
            List<Vector2> stroke = validStrokes[i];
            for (int j = 0; j < stroke.Count; j++)
            {
                Vector2 p = stroke[j];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
        }

        float w = maxX - minX;
        float h = maxY - minY;

        if (w < 1f || h < 1f)
        {
            Debug.LogWarning("TemplateRecorder: Drawing too small to save."
                + $" Width={w:F4}, Height={h:F4}");
            return;
        }

        var sb = new System.Text.StringBuilder();
        float scale = _preserveAspectRatioOnSave ? Mathf.Max(w, h) : 0f;
        float xOffset = _preserveAspectRatioOnSave ? (scale - w) * 0.5f : 0f;
        float yOffset = _preserveAspectRatioOnSave ? (scale - h) * 0.5f : 0f;
        for (int i = 0; i < validStrokes.Count; i++)
        {
            List<Vector2> stroke = validStrokes[i];
            for (int j = 0; j < stroke.Count; j++)
            {
                Vector2 p = stroke[j];
                float nx;
                float ny;
                if (_preserveAspectRatioOnSave)
                {
                    nx = ((p.x - minX) + xOffset) / scale;
                    ny = ((p.y - minY) + yOffset) / scale;
                }
                else
                {
                    nx = (p.x - minX) / w;
                    ny = (p.y - minY) / h;
                }
                sb.AppendLine($"{nx.ToString("F4", CultureInfo.InvariantCulture)}, {ny.ToString("F4", CultureInfo.InvariantCulture)}");
            }

            if (i < validStrokes.Count - 1)
                sb.AppendLine();
        }

        string dir = BuildOutputDirectory();
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, BuildFileName());
        File.WriteAllText(path, sb.ToString());

        Debug.Log($"TemplateRecorder: Saved '{path}' with {validStrokes.Count} strokes and {totalPointCount} points."
            + $" PreserveAspect={_preserveAspectRatioOnSave}");

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

    private bool IsMouseOverOverlayButtons(Vector2 mouseScreenPos)
    {
        if (!_showOverlayButtons) return false;

        Vector2 guiPoint = new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y);
        Rect overlayRect = new Rect(12f, 12f, 220f, 120f);
        return overlayRect.Contains(guiPoint);
    }

    private float ComputeStrokePathLength(List<Vector2> points)
    {
        if (points == null || points.Count < 2) return 0f;

        float total = 0f;
        for (int i = 1; i < points.Count; i++)
            total += Vector2.Distance(points[i - 1], points[i]);

        return total;
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
#endif
