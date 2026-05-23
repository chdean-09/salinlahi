using UnityEngine;

/// <summary>
/// Sizes the base zone (fence) SpriteRenderer to span the play column width.
/// Subscribes to AspectLockedCamera.OnPlayAreaChanged so it re-runs whenever
/// the play-area extents change (device rotation, editor Game-view aspect switch).
/// Y is left alone so pixel-art proportions and the ground anchor are preserved.
/// For Sliced/Tiled SpriteRenderers, drives SpriteRenderer.size; for Simple
/// sprites, drives transform.localScale.x.
/// Attach to the same GameObject that has the base-zone SpriteRenderer.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BaseZoneScaler : MonoBehaviour
{
    [Tooltip("Optional explicit reference. Falls back to the first AspectLockedCamera in scene.")]
    [SerializeField] private AspectLockedCamera _playColumn;

    [Tooltip("World-unit overflow added to each side (total added width = value * 2). " +
             "Prevents visible seams at the pillar/wall boundary.")]
    [SerializeField] private float _overflowPerSide = 0.5f;

    private SpriteRenderer _sr;
    private bool _warnedNoPlayColumn;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (_playColumn == null) _playColumn = FindFirstObjectByType<AspectLockedCamera>();
        if (_playColumn != null)
            _playColumn.OnPlayAreaChanged += Rescale;
        Rescale();
    }

    private void OnDisable()
    {
        if (_playColumn != null)
            _playColumn.OnPlayAreaChanged -= Rescale;
    }

    /// <summary>
    /// Recomputes and applies the fence width. Safe to call externally
    /// after the SpriteRenderer's sprite has been swapped (e.g. by EnvironmentThemeSwapper).
    /// </summary>
    public void Rescale()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        if (_sr == null || _sr.sprite == null) return;

        if (_playColumn == null)
        {
            if (!_warnedNoPlayColumn)
            {
                Debug.LogWarning("BaseZoneScaler: no AspectLockedCamera found. Sprite renders at native width.", this);
                _warnedNoPlayColumn = true;
            }
            return;
        }

        float spriteWorldWidth = _sr.sprite.bounds.size.x;
        if (spriteWorldWidth <= 0f) return;

        float desiredWidth = _playColumn.WorldHalfWidth * 2f + _overflowPerSide * 2f;

        if (_sr.drawMode == SpriteDrawMode.Simple)
        {
            float requiredScaleX = desiredWidth / spriteWorldWidth;
            Vector3 s = transform.localScale;
            s.x = requiredScaleX;
            transform.localScale = s;
        }
        else
        {
            Vector2 size = _sr.size;
            size.x = desiredWidth;
            _sr.size = size;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Rescale Now")]
    private void EditorRescale() => Rescale();

    private void OnValidate()
    {
        if (!Application.isPlaying) Rescale();
    }
#endif
}
