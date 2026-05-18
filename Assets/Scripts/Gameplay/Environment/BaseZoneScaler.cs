using UnityEngine;

/// <summary>
/// Scales the base zone (fence) SpriteRenderer to always span the full
/// camera width, regardless of device aspect ratio.
/// Attach to the same GameObject that has the base-zone SpriteRenderer
/// (e.g. '[Base] PlayerShrine').
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BaseZoneScaler : MonoBehaviour
{
    [Tooltip("Reference camera. Falls back to Camera.main if not set.")]
    [SerializeField] private Camera _camera;

    [Tooltip("Extra world-unit padding added to each side so the fence " +
             "extends beyond the screen edges and avoids visible seams.")]
    [SerializeField] private float _overflowPadding = 0.5f;

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        if (_camera == null)
            _camera = Camera.main;
    }

    private void Start()
    {
        ScaleToFitWidth();
    }

    /// <summary>
    /// Calculates the visible world width of the orthographic camera and
    /// scales this sprite's X so it covers the full width (plus padding).
    /// Y and Z scale remain unchanged.
    /// </summary>
    private void ScaleToFitWidth()
    {
        if (_sr == null || _sr.sprite == null || _camera == null) return;

        // The sprite's native width in world units at scale 1.
        float spriteWorldWidth = _sr.sprite.bounds.size.x;

        if (spriteWorldWidth <= 0f) return;

        // Visible world width for an orthographic camera.
        float cameraWorldWidth = 2f * _camera.orthographicSize * _camera.aspect;

        float desiredWidth = cameraWorldWidth + _overflowPadding * 2f;
        float requiredScaleX = desiredWidth / spriteWorldWidth;

        Vector3 s = transform.localScale;
        s.x = requiredScaleX;
        transform.localScale = s;
    }
}
