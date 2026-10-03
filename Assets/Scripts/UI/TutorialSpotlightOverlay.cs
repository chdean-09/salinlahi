using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen UI dim that frames a rectangular "spotlight" around a world-space target.
/// The dim is composed of four side panels (top/bottom/left/right) that tile around the
/// cut-out rect, leaving the target fully visible.
///
/// Used by the Level 1 onboarding beats (base intro, enemy intros, combo intro, heart-loss demo).
/// </summary>
public sealed class TutorialSpotlightOverlay : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Image _topPanel;
    [SerializeField] private Image _bottomPanel;
    [SerializeField] private Image _leftPanel;
    [SerializeField] private Image _rightPanel;

    [Header("Animation")]
    [SerializeField] private float _fadeSeconds = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] private float _maxDimAlpha = 0.78f;

    [Header("Camera")]
    [Tooltip("Camera used for world-to-screen conversion. Falls back to Camera.main when null.")]
    [SerializeField] private Camera _worldCamera;

    private RectTransform _rootRect;
    private Canvas _canvas;
    private Camera ActiveCamera => _worldCamera != null ? _worldCamera : Camera.main;

    private Coroutine _fadeRoutine;
    private bool _trackingActive;
    private Transform _trackTarget;
    private Bounds _staticTargetBounds;
    private bool _useStaticTarget;
    private float _paddingWorld;

    public bool IsVisible => _trackingActive;

    public static TutorialSpotlightOverlay CreateRuntime()
    {
        GameObject canvasObject = new("TutorialSpotlightOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RenderOrder.SpotlightDim;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        TutorialSpotlightOverlay overlay = canvasObject.AddComponent<TutorialSpotlightOverlay>();
        overlay._topPanel = CreateDimPanel(root, "TopPanel");
        overlay._bottomPanel = CreateDimPanel(root, "BottomPanel");
        overlay._leftPanel = CreateDimPanel(root, "LeftPanel");
        overlay._rightPanel = CreateDimPanel(root, "RightPanel");
        overlay.SetPanelsAlpha(0f);
        overlay.SetPanelsActive(false);
        return overlay;
    }

    private static Image CreateDimPanel(Transform parent, string name)
    {
        GameObject panelObject = new(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        return image;
    }

    private void Awake()
    {
        _rootRect = transform as RectTransform;
        _canvas = GetComponent<Canvas>();
        SetPanelsAlpha(0f);
        SetPanelsActive(false);
    }

    public void SetCamera(Camera camera)
    {
        _worldCamera = camera;
        ConfigureCanvasRenderMode(camera);
    }

    // Render the dim through the camera pipeline (Screen Space - Camera) instead of Overlay.
    // Overlay canvases draw on top of ALL world geometry, so they would always cover the
    // world-space drawing strokes. As a camera-space canvas on the Default sorting layer at
    // RenderOrder.SpotlightDim (900), the dim sits above gameplay sprites but BELOW the
    // strokes (1000), so strokes stay fully visible while the background is dimmed. UI
    // canvases (dialogue/guide) are Screen Space - Overlay and still render above this.
    private void ConfigureCanvasRenderMode(Camera camera)
    {
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (_canvas == null || camera == null) return;

        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = camera;
        _canvas.planeDistance = camera.nearClipPlane + 0.5f;
        _canvas.sortingOrder = RenderOrder.SpotlightDim;
    }

    /// <summary>
    /// Activate the spotlight around the live world bounds of <paramref name="target"/>.
    /// The cut-out follows the target each frame (useful when the target moves).
    /// </summary>
    public void Show(Transform target, float paddingWorld = 0.5f)
    {
        if (target == null) return;
        _trackTarget = target;
        _useStaticTarget = false;
        _paddingWorld = paddingWorld;
        Activate();
    }

    /// <summary>
    /// Activate the spotlight around a fixed world-space bounds. Useful for static targets
    /// (e.g., the base) or composite targets (e.g., three enemies framed as a single rect).
    /// </summary>
    public void Show(Bounds worldBounds, float paddingWorld = 0.5f)
    {
        _staticTargetBounds = worldBounds;
        _useStaticTarget = true;
        _trackTarget = null;
        _paddingWorld = paddingWorld;
        Activate();
    }

    public void Hide()
    {
        // Always run the fade-out, even if tracking was already stopped. A defensive
        // double-Hide() must still guarantee the dim panels end up deactivated — never
        // early-return and leave a stale overlay on screen.
        _trackingActive = false;
        _trackTarget = null;
        StartFade(0f, () => SetPanelsActive(false));
    }

    private void Activate()
    {
        SetPanelsActive(true);
        _trackingActive = true;
        StartFade(_maxDimAlpha, null);
        UpdatePanelRects();
    }

    private void LateUpdate()
    {
        if (!_trackingActive) return;
        UpdatePanelRects();
    }

    private void UpdatePanelRects()
    {
        Bounds worldBounds;
        if (_useStaticTarget)
        {
            worldBounds = _staticTargetBounds;
        }
        else if (_trackTarget != null)
        {
            worldBounds = ResolveBoundsFromTransform(_trackTarget);
        }
        else
        {
            return;
        }

        Camera cam = ActiveCamera;
        if (cam == null || _rootRect == null) return;

        Rect screenRect = ComputeScreenRect(worldBounds, cam, _paddingWorld);
        ApplyCutoutRect(screenRect);
    }

    private static Bounds ResolveBoundsFromTransform(Transform target)
    {
        if (target == null)
            return new Bounds(Vector3.zero, Vector3.one);

        // Only union SpriteRenderers (enemy body + glyph badge). Tutorial/debug TextMeshPro
        // labels ("TUTORIAL", "Draw: ha", "Type: soldado") hang below the enemy and would
        // otherwise inflate the cut-out far past the character — keep the spotlight tight.
        SpriteRenderer[] sprites = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
        Bounds? union = null;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer r = sprites[i];
            if (r == null || !r.enabled) continue;
            if (union.HasValue)
            {
                Bounds b = union.Value;
                b.Encapsulate(r.bounds);
                union = b;
            }
            else
            {
                union = r.bounds;
            }
        }
        if (union.HasValue) return union.Value;

        Collider2D col = target.GetComponent<Collider2D>();
        if (col != null && col.enabled) return col.bounds;

        return new Bounds(target.position, Vector3.one);
    }

    /// <summary>
    /// Compute the screen-space cut-out rectangle from world bounds + camera + padding.
    /// Public/static so it can be unit-tested without instantiating the MonoBehaviour.
    /// </summary>
    public static Rect ComputeScreenRect(Bounds worldBounds, Camera camera, float paddingWorld)
    {
        Vector3 min = worldBounds.min - new Vector3(paddingWorld, paddingWorld, 0f);
        Vector3 max = worldBounds.max + new Vector3(paddingWorld, paddingWorld, 0f);
        Vector3 screenMin = camera.WorldToScreenPoint(min);
        Vector3 screenMax = camera.WorldToScreenPoint(max);

        float x = Mathf.Min(screenMin.x, screenMax.x);
        float y = Mathf.Min(screenMin.y, screenMax.y);
        float width = Mathf.Abs(screenMax.x - screenMin.x);
        float height = Mathf.Abs(screenMax.y - screenMin.y);
        return new Rect(x, y, width, height);
    }

    private void ApplyCutoutRect(Rect screenRect)
    {
        if (_topPanel == null || _bottomPanel == null || _leftPanel == null || _rightPanel == null)
            return;

        float screenW = Screen.width;
        float screenH = Screen.height;
        if (screenW <= 0f || screenH <= 0f) return;

        // Work in NORMALIZED screen space (0..1). Anchors are fractions of the parent
        // rect, so positioning panels via anchorMin/anchorMax is independent of the
        // CanvasScaler reference resolution and the device's actual pixel resolution.
        // (WorldToScreenPoint returns pixels, but absolute pixel offsets break under a
        // ScaleWithScreenSize canvas — normalized anchors do not.)
        float xMin = Mathf.Clamp01(screenRect.xMin / screenW);
        float xMax = Mathf.Clamp01(screenRect.xMax / screenW);
        float yMin = Mathf.Clamp01(screenRect.yMin / screenH);
        float yMax = Mathf.Clamp01(screenRect.yMax / screenH);

        // Top band: spans full width, from the cutout's top edge to the screen top.
        ApplyNormalizedRect((RectTransform)_topPanel.transform, 0f, yMax, 1f, 1f);
        // Bottom band: full width, from screen bottom to the cutout's bottom edge.
        ApplyNormalizedRect((RectTransform)_bottomPanel.transform, 0f, 0f, 1f, yMin);
        // Left band: between the top/bottom bands, from screen left to cutout left.
        ApplyNormalizedRect((RectTransform)_leftPanel.transform, 0f, yMin, xMin, yMax);
        // Right band: between the top/bottom bands, from cutout right to screen right.
        ApplyNormalizedRect((RectTransform)_rightPanel.transform, xMax, yMin, 1f, yMax);
    }

    private static void ApplyNormalizedRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(Mathf.Min(xMin, xMax), Mathf.Min(yMin, yMax));
        rt.anchorMax = new Vector2(Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void SetPanelsActive(bool active)
    {
        if (_topPanel != null) _topPanel.gameObject.SetActive(active);
        if (_bottomPanel != null) _bottomPanel.gameObject.SetActive(active);
        if (_leftPanel != null) _leftPanel.gameObject.SetActive(active);
        if (_rightPanel != null) _rightPanel.gameObject.SetActive(active);
    }

    private void SetPanelsAlpha(float alpha)
    {
        SetImageAlpha(_topPanel, alpha);
        SetImageAlpha(_bottomPanel, alpha);
        SetImageAlpha(_leftPanel, alpha);
        SetImageAlpha(_rightPanel, alpha);
    }

    private static void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    private void StartFade(float targetAlpha, System.Action onComplete)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, onComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, System.Action onComplete)
    {
        float startAlpha = _topPanel != null ? _topPanel.color.a : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, _fadeSeconds);
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            SetPanelsAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        SetPanelsAlpha(targetAlpha);
        onComplete?.Invoke();
    }
}
