using UnityEngine;

/// <summary>
/// Renders the pillar area outside the play column on wider-than-target devices.
/// Reads fill settings from the active EraThemeSO via EventBus.OnThemeApplied, or
/// from local override fields when _useSceneOverride is true.
/// </summary>
public sealed class PillarFill : MonoBehaviour
{
    [Tooltip("Optional reference. Falls back to FindFirstObjectByType<AspectLockedCamera>().")]
    [SerializeField] private AspectLockedCamera _playColumn;

    [Tooltip("When true, use the local Mode/Color/Sprite fields instead of the active EraThemeSO.")]
    [SerializeField] private bool _useSceneOverride = false;

    [SerializeField] private PillarFillMode _mode = PillarFillMode.None;
    [SerializeField] private Color _color = Color.black;
    [SerializeField] private Sprite _sprite;

    private Camera _cam;
    private SpriteRenderer _leftPillar;
    private SpriteRenderer _rightPillar;
    private Sprite _whitePixelSprite;
    private bool _warnedNoSprite;
    private bool _warnedNoPlayColumn;

    /// <summary>
    /// Returns the half-width of one pillar, in world units. Zero when the device
    /// aspect is at or below the target (no pillar to render). Pure function.
    /// </summary>
    public static float ComputePillarWidth(float refWidth, float orthoSize, float deviceAspect)
    {
        float viewportWorldWidth = 2f * orthoSize * deviceAspect;
        float pillarWidth = (viewportWorldWidth - refWidth) * 0.5f;
        return pillarWidth > 0f ? pillarWidth : 0f;
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (_playColumn == null) _playColumn = FindFirstObjectByType<AspectLockedCamera>();
        if (_cam == null) _cam = Camera.main;
        EnsurePillarRenderers();

        if (_playColumn != null)
            _playColumn.OnPlayAreaChanged += Apply;
        EventBus.OnThemeApplied += OnThemeApplied;

        // Fallback: if EnvironmentThemeSwapper.Start already raised the event before
        // our OnEnable, recover the theme directly from GameManager.
        if (GameManager.Instance != null && GameManager.Instance.CurrentLevel != null)
        {
            Apply();
        }
    }

    private void OnDisable()
    {
        if (_playColumn != null)
            _playColumn.OnPlayAreaChanged -= Apply;
        EventBus.OnThemeApplied -= OnThemeApplied;
    }

    private void OnThemeApplied(EraThemeSO _) => Apply();

    private void Apply()
    {
        if (!Application.isPlaying) return;

        ResolveSettings(out PillarFillMode mode, out Color color, out Sprite sprite);
        ApplyInternal(mode, color, sprite);
    }

    private void ResolveSettings(out PillarFillMode mode, out Color color, out Sprite sprite)
    {
        if (_useSceneOverride)
        {
            mode = _mode;
            color = _color;
            sprite = _sprite;
            return;
        }

        EraThemeSO theme = GameManager.Instance != null && GameManager.Instance.CurrentLevel != null
            ? GameManager.Instance.CurrentLevel.eraTheme
            : null;
        if (theme != null)
        {
            mode = theme.pillarMode;
            color = theme.pillarColor;
            sprite = theme.pillarSprite;
        }
        else
        {
            mode = PillarFillMode.None;
            color = Color.black;
            sprite = null;
        }
    }

    private void ApplyInternal(PillarFillMode mode, Color color, Sprite sprite)
    {
        switch (mode)
        {
            case PillarFillMode.None:
                SetRenderersEnabled(false);
                break;
            case PillarFillMode.Color:
                AssignSprite(GetOrCreateWhitePixelSprite());
                if (_leftPillar != null) _leftPillar.color = color;
                if (_rightPillar != null) _rightPillar.color = color;
                ResizeRenderers();
                break;
            case PillarFillMode.Sprite:
                if (sprite == null)
                {
                    if (!_warnedNoSprite)
                    {
                        Debug.LogWarning("PillarFill: Mode is Sprite but no sprite assigned. Falling back to None.", this);
                        _warnedNoSprite = true;
                    }
                    SetRenderersEnabled(false);
                    return;
                }
                _warnedNoSprite = false;
                AssignSprite(sprite);
                if (_leftPillar != null) _leftPillar.color = Color.white;
                if (_rightPillar != null) _rightPillar.color = Color.white;
                ResizeRenderers();
                break;
        }
    }

    private void ResizeRenderers()
    {
        if (_playColumn == null || _cam == null)
        {
            if (!_warnedNoPlayColumn)
            {
                Debug.LogWarning("PillarFill: AspectLockedCamera or Camera missing. Pillars disabled.", this);
                _warnedNoPlayColumn = true;
            }
            SetRenderersEnabled(false);
            return;
        }

        float refWidth = _playColumn.WorldHalfWidth * 2f;
        float orthoSize = _cam.orthographicSize;
        float deviceAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float pillarWidth = ComputePillarWidth(refWidth, orthoSize, deviceAspect);

        if (pillarWidth <= 0f)
        {
            SetRenderersEnabled(false);
            return;
        }

        SetRenderersEnabled(true);

        float halfCol = refWidth * 0.5f;
        float halfPillar = pillarWidth * 0.5f;
        float height = orthoSize * 2f;

        _leftPillar.transform.position = new Vector3(-(halfCol + halfPillar), 0f, 0f);
        _rightPillar.transform.position = new Vector3(+(halfCol + halfPillar), 0f, 0f);

        _leftPillar.size = new Vector2(pillarWidth, height);
        _rightPillar.size = new Vector2(pillarWidth, height);
    }

    private void AssignSprite(Sprite sprite)
    {
        if (_leftPillar != null)
        {
            _leftPillar.sprite = sprite;
            _leftPillar.drawMode = SpriteDrawMode.Tiled;
            _leftPillar.sortingOrder = RenderOrder.PillarFill;
        }
        if (_rightPillar != null)
        {
            _rightPillar.sprite = sprite;
            _rightPillar.drawMode = SpriteDrawMode.Tiled;
            _rightPillar.sortingOrder = RenderOrder.PillarFill;
        }
    }

    private void SetRenderersEnabled(bool on)
    {
        if (_leftPillar != null) _leftPillar.enabled = on;
        if (_rightPillar != null) _rightPillar.enabled = on;
    }

    private void EnsurePillarRenderers()
    {
        // Renderers are created in Simple drawMode (default) and switched to Tiled
        // only after a Sprite is assigned in AssignSprite() — setting drawMode = Tiled
        // before a sprite is assigned causes Unity to emit a SpriteRenderer warning.
        if (_leftPillar == null)
        {
            GameObject go = new GameObject("LeftPillar");
            go.transform.SetParent(transform);
            _leftPillar = go.AddComponent<SpriteRenderer>();
            _leftPillar.sortingOrder = RenderOrder.PillarFill;
            _leftPillar.enabled = false;
        }
        if (_rightPillar == null)
        {
            GameObject go = new GameObject("RightPillar");
            go.transform.SetParent(transform);
            _rightPillar = go.AddComponent<SpriteRenderer>();
            _rightPillar.sortingOrder = RenderOrder.PillarFill;
            _rightPillar.enabled = false;
        }
    }

    private Sprite GetOrCreateWhitePixelSprite()
    {
        if (_whitePixelSprite != null) return _whitePixelSprite;
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _whitePixelSprite;
    }

    // ---- Test-only seams ----

    internal void InjectDependenciesForTests(Camera cam, SpriteRenderer left, SpriteRenderer right)
    {
        _cam = cam;
        _leftPillar = left;
        _rightPillar = right;
    }

    internal void ApplyForTests(PillarFillMode mode, Color color, Sprite sprite)
    {
        ApplyInternal(mode, color, sprite);
    }
}
