using UnityEngine;

/// <summary>
/// Swaps environment visuals (background, ground, decorations) based on
/// the current level's EraThemeSO. Place on a root-level GameObject in
/// the Gameplay scene and assign references in the Inspector.
/// </summary>
public class EnvironmentThemeSwapper : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private SpriteRenderer _backgroundRenderer;
    [SerializeField] private SpriteRenderer _groundRenderer;
    [SerializeField] private SpriteRenderer _baseZoneRenderer;
    [SerializeField] private SpriteRenderer _shrineRenderer;
    [SerializeField] private SpriteRenderer _topFoliageRenderer;
    [SerializeField] private SpriteRenderer[] _bushRenderers;
    [SerializeField] private SpriteRenderer[] _torchRenderers;

    private SpriteRenderer _stageBackgroundRenderer;
    private StageBackgroundSO _stageBackground;
    private string _stageSeedKey;
    private AspectLockedCamera _playColumn;

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            DebugLogger.LogWarning("EnvironmentThemeSwapper: GameManager is missing. Theme swap skipped.");
            return;
        }

        LevelConfigSO level = GameManager.Instance.CurrentLevel;
        if (level == null || level.eraTheme == null)
        {
            DebugLogger.LogWarning("EnvironmentThemeSwapper: No level or era theme assigned.");
            return;
        }

        ApplyTheme(level.eraTheme);

        if (level.eraTheme.stageBackground != null)
            ApplyStageBackground(level.eraTheme.stageBackground, level.name);
    }

    private void OnDestroy()
    {
        if (_playColumn != null) _playColumn.OnPlayAreaChanged -= RebakeStageBackground;
        ReleaseBakedSprite();
    }

    /// <summary>
    /// Bakes the stage's tiles, scatter and margins into one sprite covering the play
    /// column and shows it in place of the legacy full-screen background. Re-bakes if
    /// the column changes size (device rotation, editor resize).
    /// </summary>
    private void ApplyStageBackground(StageBackgroundSO stageBackground, string seedKey)
    {
        _stageBackground = stageBackground;
        _stageSeedKey = seedKey;
        _playColumn = AspectLockedCamera.Instance;
        if (_playColumn != null) _playColumn.OnPlayAreaChanged += RebakeStageBackground;
        RebakeStageBackground();
    }

    private void RebakeStageBackground()
    {
        if (_stageBackground == null) return;
        Rect column = ResolvePlayColumn();
        if (column.width <= 0f || column.height <= 0f) return;

        ReleaseBakedSprite();
        Sprite baked = StageBackgroundBaker.Bake(_stageBackground, column, StageBackgroundBaker.StableHash(_stageSeedKey));
        if (baked == null)
        {
            DebugLogger.LogWarning($"EnvironmentThemeSwapper: stage background '{_stageBackground.name}' is incomplete; keeping the legacy background.");
            return;
        }

        if (_stageBackgroundRenderer == null)
        {
            var go = new GameObject("StageBackground");
            go.transform.SetParent(transform, false);
            _stageBackgroundRenderer = go.AddComponent<SpriteRenderer>();
            _stageBackgroundRenderer.sortingOrder = RenderOrder.StageBackground;
            if (_backgroundRenderer != null)
                _stageBackgroundRenderer.sortingLayerID = _backgroundRenderer.sortingLayerID;
        }
        _stageBackgroundRenderer.sprite = baked;
        _stageBackgroundRenderer.transform.position = new Vector3(column.xMin, column.yMin, 0f);
        if (_backgroundRenderer != null) _backgroundRenderer.enabled = false;

        // The margins now frame the lane, so the fence spans the lane, not the screen.
        BaseZoneScaler fence = _baseZoneRenderer != null ? _baseZoneRenderer.GetComponent<BaseZoneScaler>() : null;
        if (fence == null) fence = FindFirstObjectByType<BaseZoneScaler>();
        if (fence != null)
            fence.SetLaneInset(_stageBackground.MarginWidthPx / (float)StageBackgroundBaker.PixelsPerUnit);
        DebugLogger.Log($"EnvironmentThemeSwapper: baked stage background '{_stageBackground.name}' {baked.texture.width}x{baked.texture.height} at {column}");
    }

    private Rect ResolvePlayColumn()
    {
        if (_playColumn != null && _playColumn.PlayColumnWorldRect.width > 0f)
            return _playColumn.PlayColumnWorldRect;
        Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
        if (cam == null) return Rect.zero;
        float halfW = _playColumn != null ? _playColumn.WorldHalfWidth : cam.orthographicSize * cam.aspect;
        float halfH = cam.orthographicSize;
        Vector3 c = cam.transform.position;
        return new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);
    }

    private void ReleaseBakedSprite()
    {
        if (_stageBackgroundRenderer == null || _stageBackgroundRenderer.sprite == null) return;
        Sprite old = _stageBackgroundRenderer.sprite;
        _stageBackgroundRenderer.sprite = null;
        Texture2D tex = old.texture;
        Destroy(old);
        Destroy(tex);
    }

    private void ApplyTheme(EraThemeSO theme)
    {
        // Background
        if (_mainCamera != null)
            _mainCamera.backgroundColor = theme.backgroundColor;

        if (_backgroundRenderer != null && theme.backgroundSprite != null)
            _backgroundRenderer.sprite = theme.backgroundSprite;

        // Ground
        if (_groundRenderer != null && theme.groundSprite != null)
            _groundRenderer.sprite = theme.groundSprite;

        // Base zone (fence)
        if (_baseZoneRenderer != null && theme.baseZoneSprite != null)
        {
            _baseZoneRenderer.sprite = theme.baseZoneSprite;

            // Sprite bounds/PPU can differ between era themes, so the
            // scaler must recompute after the swap rather than relying on
            // Start() execution order.
            if (_baseZoneRenderer.TryGetComponent(out BaseZoneScaler baseZoneScaler))
                baseZoneScaler.Rescale();
        }

        // Shrine
        if (_shrineRenderer != null && theme.shrineSprite != null)
            _shrineRenderer.sprite = theme.shrineSprite;

        // Top foliage
        if (_topFoliageRenderer != null && theme.topFoliageSprite != null)
            _topFoliageRenderer.sprite = theme.topFoliageSprite;

        // Bushes
        if (_bushRenderers != null && theme.bushSprite != null)
        {
            foreach (var bush in _bushRenderers)
            {
                if (bush != null)
                    bush.sprite = theme.bushSprite;
            }
        }

        // Torches
        if (_torchRenderers != null && theme.torchSprite != null)
        {
            foreach (var torch in _torchRenderers)
            {
                if (torch != null)
                    torch.sprite = theme.torchSprite;
            }
        }

        DebugLogger.Log($"EnvironmentThemeSwapper: Applied theme '{theme.eraName}'");

        EventBus.RaiseThemeApplied(theme);
    }
}
