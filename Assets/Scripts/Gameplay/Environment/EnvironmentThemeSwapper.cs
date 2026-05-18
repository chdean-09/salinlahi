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

    private void Start()
    {
        LevelConfigSO level = GameManager.Instance.CurrentLevel;
        if (level == null || level.eraTheme == null)
        {
            DebugLogger.LogWarning("EnvironmentThemeSwapper: No level or era theme assigned.");
            return;
        }

        ApplyTheme(level.eraTheme);
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
            _baseZoneRenderer.sprite = theme.baseZoneSprite;

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
    }
}
