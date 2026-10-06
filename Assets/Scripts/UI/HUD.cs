using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUD : MonoBehaviour
{
    public const float ControlSize = 100f;
    private const float PauseGlyphSize = 45f;
    [Header("Pause")]
    [SerializeField] private Button _pauseButton;

    [Header("Responsive Layout")]
    [SerializeField] private SafeAreaHandler _safeAreaHandler;
    [SerializeField] private RectTransform _hudLayer;

    private void Awake()
    {
        ConfigureResponsiveLayout();
    }

    private void OnEnable()
    {
        ConfigureResponsiveLayout();
        ScrollPanelArt.ApplyButtonSkin(_pauseButton);
        ConfigurePauseControl();

        if (_pauseButton != null)
            _pauseButton.onClick.AddListener(OnPausePressed);
    }

    private void OnDisable()
    {
        if (_pauseButton != null)
            _pauseButton.onClick.RemoveListener(OnPausePressed);
    }

    private void OnPausePressed()
    {
        GameManager.Instance.PauseGame();
    }

    private void ConfigurePauseControl()
    {
        if (_pauseButton == null)
            return;

        RectTransform rect = _pauseButton.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ControlSize);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ControlSize);
        foreach (TMP_Text glyph in _pauseButton.GetComponentsInChildren<TMP_Text>(true))
        {
            glyph.enableAutoSizing = false;
            glyph.fontSize = PauseGlyphSize;
            glyph.raycastTarget = false;
        }
    }

    private void ConfigureResponsiveLayout()
    {
        RectTransform root = transform as RectTransform;
        Stretch(root);

        if (_hudLayer == null)
            _hudLayer = transform.Find("HUDLayer") as RectTransform;
        Stretch(_hudLayer);

        if (_safeAreaHandler == null)
            _safeAreaHandler = GetComponent<SafeAreaHandler>();
        if (_safeAreaHandler == null)
            _safeAreaHandler = gameObject.AddComponent<SafeAreaHandler>();

        _safeAreaHandler.SetIncludeAspectLockedPlayColumn(true);
        _safeAreaHandler.Refresh();

        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
