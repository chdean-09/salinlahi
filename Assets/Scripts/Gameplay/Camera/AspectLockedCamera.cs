using System;
using UnityEngine;

/// <summary>
/// Enforces a fixed-aspect (default 9:16) play column in world space, regardless
/// of device aspect ratio. Sits on the Main Camera in the Gameplay scene.
/// All gameplay systems (BaseZoneScaler, DrawingCanvas, HUD elements)
/// read play-area extents from this component instead of Camera.aspect.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public sealed class AspectLockedCamera : MonoBehaviour
{
    [Tooltip("Target aspect (width / height). Default 9:16 = 0.5625.")]
    [SerializeField] private float _targetAspect = 9f / 16f;

    [Tooltip("World-space height of the reference resolution at PPU 32: 640 / 32 = 20.")]
    [SerializeField] private float _referenceWorldHeight = 20f;

    [Tooltip("World-space width of the reference resolution at PPU 32: 360 / 32 = 11.25.")]
    [SerializeField] private float _referenceWorldWidth = 11.25f;

    public static AspectLockedCamera Instance { get; private set; }

    private Camera _cam;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private bool _warnedNotOrthographic;
    private bool _warnedInvalidSettings;
    private Rect _playColumnScreenRect;
    private Rect _playColumnWorldRect;
    private float _authoredCameraY;
    private bool _authoredCameraYCaptured;
    private float _bottomBandPixels;
    private float _bottomBandWorldHeight;

    /// <summary>Raised after orthographicSize / play-column rects recompute.</summary>
    public event Action OnPlayAreaChanged;

    /// <summary>Half the play column's world-space width. Constant across devices.</summary>
    public float WorldHalfWidth => _referenceWorldWidth * 0.5f;

    /// <summary>
    /// Half the play column's world-space height. Equals orthographicSize.
    /// Tablet (height-locked): _referenceWorldHeight / 2.
    /// Tall phone (width-locked): larger (column extends vertically).
    /// </summary>
    public float WorldHalfHeight => _cam != null ? _cam.orthographicSize : _referenceWorldHeight * 0.5f;

    /// <summary>
    /// Multiply raw SO moveSpeed by this to make travel time device-independent.
    /// 1.0 on the 9:16 reference device; less than 1.0 on taller phones (wider corridor
    /// means enemies would otherwise take proportionally longer to reach the base).
    /// </summary>
    public float CorridorSpeedNormalizationScale => (_referenceWorldHeight * 0.5f) / WorldHalfHeight;

    /// <summary>Cached world-space rect of the play column centered on the camera. Updated by Recompute().</summary>
    public Rect PlayColumnWorldRect => _playColumnWorldRect;

    /// <summary>
    /// Screen pixels reserved at the foot of the screen for HUD that must not be drawn over the
    /// play field. Zero until something asks for a band.
    /// </summary>
    public float BottomBandPixels => _bottomBandPixels;

    /// <summary>World units the reserved bottom band is worth at the current resolution.</summary>
    public float BottomBandWorldHeight => _bottomBandWorldHeight;

    /// <summary>
    /// Reserves <paramref name="pixels"/> of screen at the bottom for HUD, by lowering the camera
    /// until everything the play column used to show sits entirely above that band.
    ///
    /// <para>
    /// The camera is TRANSLATED rather than zoomed or given a viewport rect, and the world is not
    /// touched at all. That matters more than it looks: the base's hit line, the enemy path and the
    /// spawn heights are all authored world positions, so moving the camera changes where the fence
    /// is DRAWN and changes nothing about where an enemy actually reaches the shrine. A smaller
    /// orthographic size would have reserved the same band by shrinking the whole field, and a
    /// viewport rect would have cropped Juan — who stands BELOW the fence — off the bottom.
    /// </para>
    ///
    /// <para>
    /// Asked for by the HUD element that needs the room, in that element's own measured screen
    /// pixels, so a level whose rail is taller reserves more and a level with no rail at all
    /// reserves nothing and frames exactly as it did before.
    /// </para>
    /// </summary>
    public void SetBottomBandPixels(float pixels)
    {
        float requested = Mathf.Max(0f, pixels);
        if (Mathf.Approximately(_bottomBandPixels, requested))
            return;

        _bottomBandPixels = requested;
        Recompute();
    }

    /// <summary>
    /// World units a band of <paramref name="bandPixels"/> screen pixels covers, for a camera of
    /// <paramref name="orthographicSize"/> rendering into <paramref name="screenHeightPixels"/>.
    /// Pure, so the reservation can be checked at a resolution the test run is not running at.
    /// </summary>
    public static float BandWorldHeight(
        float bandPixels, float orthographicSize, float screenHeightPixels)
    {
        if (bandPixels <= 0f || orthographicSize <= 0f || screenHeightPixels <= 0f)
            return 0f;

        return bandPixels * (2f * orthographicSize) / screenHeightPixels;
    }

    /// <summary>
    /// Cached pixel rect inside Screen.width x Screen.height covering the play column. Updated by Recompute().
    /// Tablet (wider than target): width fraction &lt; 1.0, height fraction = 1.0.
    /// Tall phone / 9:16 device: full screen (column extends vertically to cover).
    /// Consumed by DrawingCanvas and PillarFill.
    /// </summary>
    public Rect PlayColumnScreenRect => _playColumnScreenRect;

    private void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        _cam = GetComponent<Camera>();
        // The authored height, captured before any band is applied, so repeated reservations are
        // absolute rather than cumulative and a band of zero restores the authored framing exactly.
        if (!_authoredCameraYCaptured)
        {
            _authoredCameraY = transform.position.y;
            _authoredCameraYCaptured = true;
        }
        _warnedNotOrthographic = false;
        _warnedInvalidSettings = false;
        _playColumnScreenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        _playColumnWorldRect = new Rect(-_referenceWorldWidth * 0.5f, -_referenceWorldHeight * 0.5f, _referenceWorldWidth, _referenceWorldHeight);
        Recompute();
    }

    private void Update()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            Recompute();
    }

    private void Recompute()
    {
        if (_cam == null) _cam = GetComponent<Camera>();
        if (_cam == null) return;

        if (!_cam.orthographic)
        {
            if (!_warnedNotOrthographic)
            {
                Debug.LogError("AspectLockedCamera requires an orthographic camera.", this);
                _warnedNotOrthographic = true;
            }
            return;
        }

        if (_targetAspect <= 0f || _referenceWorldWidth <= 0f || _referenceWorldHeight <= 0f)
        {
            if (!_warnedInvalidSettings)
            {
                Debug.LogWarning("AspectLockedCamera: invalid inspector settings (must all be > 0). Skipping recompute.", this);
                _warnedInvalidSettings = true;
            }
            return;
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        float deviceAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float orthoSize = deviceAspect >= _targetAspect
            ? _referenceWorldHeight * 0.5f                          // height-locked
            : _referenceWorldWidth / (2f * deviceAspect);           // width-locked

        if (!Mathf.Approximately(_cam.orthographicSize, orthoSize))
            _cam.orthographicSize = orthoSize;

        // Lower the camera by the reserved band, so the band at the foot of the screen shows only
        // what used to be below the play column and the fence, the shrine and Juan all clear it.
        _bottomBandWorldHeight = BandWorldHeight(
            _bottomBandPixels, _cam.orthographicSize, Screen.height);
        Vector3 camPosition = transform.position;
        float bandedY = _authoredCameraY - _bottomBandWorldHeight;
        if (!Mathf.Approximately(camPosition.y, bandedY))
        {
            camPosition.y = bandedY;
            transform.position = camPosition;
        }

        // Cache play-column rects so getters are allocation-free.
        float worldHalfH = _cam.orthographicSize;
        float w = _referenceWorldWidth;
        float h = worldHalfH * 2f;
        // Centred on the camera rather than on the origin: with a band reserved the two are no
        // longer the same point, and a consumer asking what the column covers wants what is on
        // screen, not what would have been on screen with the camera at zero.
        _playColumnWorldRect = new Rect(-w * 0.5f, bandedY - h * 0.5f, w, h);

        if (deviceAspect >= _targetAspect)
        {
            // Height-locked. Pillars on the sides.
            float viewportWorldWidth = h * deviceAspect;
            float widthFraction = w / viewportWorldWidth;
            float pillarFraction = (1f - widthFraction) * 0.5f;
            _playColumnScreenRect = new Rect(
                pillarFraction * Screen.width,
                0f,
                widthFraction * Screen.width,
                Screen.height);
        }
        else
        {
            // Width-locked. Full screen — corridor extends vertically.
            _playColumnScreenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        OnPlayAreaChanged?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("Recompute Now")]
    private void EditorRecompute() => Recompute();

    private void OnValidate()
    {
        if (!Application.isPlaying) Recompute();
    }
#endif
}
