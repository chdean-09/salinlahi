using UnityEngine;

/// <summary>
/// Adjusts the attached RectTransform to fit within the device safe area.
/// Attach to the root panel of any Canvas that needs notch/cutout support.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    [SerializeField] private bool _includeAspectLockedPlayColumn;

    private RectTransform _rect;
    private Rect _lastSafeArea;
    private Rect _lastPlayColumnRect;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        Rect playColumnRect = ResolvePlayColumnRect();
        if (Screen.safeArea != _lastSafeArea
            || Screen.width != _lastScreenWidth
            || Screen.height != _lastScreenHeight
            || (_includeAspectLockedPlayColumn && playColumnRect != _lastPlayColumnRect))
        {
            ApplySafeArea();
        }
    }

    public void SetIncludeAspectLockedPlayColumn(bool include)
    {
        if (_includeAspectLockedPlayColumn == include)
            return;

        _includeAspectLockedPlayColumn = include;
        Refresh();
    }

    public void Refresh()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        Rect playColumnRect = ResolvePlayColumnRect();
        _lastSafeArea = safeArea;
        _lastPlayColumnRect = playColumnRect;
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        CalculateAnchors(
            safeArea,
            Screen.width,
            Screen.height,
            _includeAspectLockedPlayColumn,
            playColumnRect,
            out Vector2 anchorMin,
            out Vector2 anchorMax);

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }

    public static void CalculateAnchors(
        Rect safeArea,
        int screenWidth,
        int screenHeight,
        bool includePlayColumn,
        Rect playColumnRect,
        out Vector2 anchorMin,
        out Vector2 anchorMax)
    {
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            anchorMin = Vector2.zero;
            anchorMax = Vector2.one;
            return;
        }

        Rect screenRect = new(0f, 0f, screenWidth, screenHeight);
        Rect targetArea = NormalizeArea(safeArea, screenRect);

        if (includePlayColumn)
            targetArea = Intersect(targetArea, NormalizeArea(playColumnRect, screenRect), targetArea);

        anchorMin = targetArea.position;
        anchorMax = targetArea.position + targetArea.size;

        anchorMin.x /= screenWidth;
        anchorMin.y /= screenHeight;
        anchorMax.x /= screenWidth;
        anchorMax.y /= screenHeight;

        anchorMin.x = Mathf.Clamp01(anchorMin.x);
        anchorMin.y = Mathf.Clamp01(anchorMin.y);
        anchorMax.x = Mathf.Clamp01(anchorMax.x);
        anchorMax.y = Mathf.Clamp01(anchorMax.y);
    }

    private Rect ResolvePlayColumnRect()
    {
        AspectLockedCamera camera = AspectLockedCamera.Instance;
        if (camera != null)
            return camera.PlayColumnScreenRect;

        return new Rect(0f, 0f, Screen.width, Screen.height);
    }

    private static Rect NormalizeArea(Rect area, Rect fallback)
    {
        if (area.width <= 0f || area.height <= 0f)
            return fallback;

        float xMin = Mathf.Clamp(area.xMin, fallback.xMin, fallback.xMax);
        float yMin = Mathf.Clamp(area.yMin, fallback.yMin, fallback.yMax);
        float xMax = Mathf.Clamp(area.xMax, fallback.xMin, fallback.xMax);
        float yMax = Mathf.Clamp(area.yMax, fallback.yMin, fallback.yMax);

        if (xMax <= xMin || yMax <= yMin)
            return fallback;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static Rect Intersect(Rect a, Rect b, Rect fallback)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        if (xMax <= xMin || yMax <= yMin)
            return fallback;

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
