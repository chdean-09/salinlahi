using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

public class SettingsPanel : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("Labels")]
    [SerializeField] private TMP_Text _masterLabel;
    [SerializeField] private TMP_Text _bgmLabel;
    [SerializeField] private TMP_Text _sfxLabel;

    [Header("Navigation")]
    [SerializeField] private Button _closeButton;

    [Header("Modal")]
    [SerializeField] private bool _hideSiblingUiWhileOpen = false;
    [SerializeField] private Color _modalBackdropColor = new(0.02f, 0.03f, 0.06f, 0.94f);
    [SerializeField] private Sprite _sliderFallbackSprite;

    [Header("Journey Reset")]
    [SerializeField] private bool _allowJourneyReset = false;
    [SerializeField] private Button _journeyResetButton;
    [SerializeField] private ResetJourneyConfirmationPanel _resetConfirmationPanel;

    private static readonly Color TrackColor = new(0.2f, 0.23f, 0.3f, 1f);
    private static readonly Color FillColor = new(0.08f, 0.56f, 1f, 1f);
    private static readonly Color HandleColor = new(0.95f, 0.98f, 1f, 1f);
    private static readonly Color LabelColor = new(1f, 1f, 1f, 1f);
    private static readonly Color CardColor = new(0.07f, 0.1f, 0.17f, 1f);
    private static readonly Color JourneyResetButtonColor = new(0.72f, 0.18f, 0.15f, 1f);
    private static readonly Color MainMenuButtonTextColor = new(0.7019608f, 0.5019608f, 0.07450981f, 1f);
    private static readonly Color MainMenuTextShadowColor = new(0.06f, 0.035f, 0.01f, 1f);
    private static readonly Vector2 MainMenuTextShadowOffset = new(5f, -5f);
    private static readonly Vector2 CloseButtonMinSize = new(280f, 88f);
    private const float CloseButtonMinFontSize = 34f;
    private static readonly string[] MainMenuButtonTemplateNames =
    {
        "SettingsButton",
        "PlayButton",
        "LevelSelectButton",
        "EndlessModeButton",
        "TracingDojoButton"
    };

    private static Sprite s_runtimeWhiteSprite;
    private GameObject _modalBackdrop;
    private RectTransform _settingsCardRect;
    private readonly System.Collections.Generic.List<GameObject> _hiddenSiblingObjects = new();
    private readonly System.Collections.Generic.List<Graphic> _disabledSiblingRaycastGraphics = new();
    private Canvas _rootCanvas;

    public event Action Opened;
    public event Action Closed;

    private void OnEnable()
    {
        EnsureTopInputLayer();
        EnsureSafeArea();
        EnsureModalBackdrop();
        EnsureCloseButtonVisible();
        SetSiblingUiInputEnabled(false);
        ResolveLabelReferencesIfMissing();
        EnsureSliderVisuals();
        EnsureCardLayout();
        EnsureJourneyResetButton();
        SyncSlidersToAudioManager();
        UpdateVolumeLabels();
        SetSlidersInteractable(true);

        if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (_bgmSlider != null) _bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        if (_journeyResetButton != null) _journeyResetButton.onClick.AddListener(OnJourneyResetPressed);
        Opened?.Invoke();
    }

    private void OnDisable()
    {
        SetSlidersInteractable(false);
        SetSiblingUiInputEnabled(true);
        if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (_bgmSlider != null) _bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        if (_journeyResetButton != null) _journeyResetButton.onClick.RemoveListener(OnJourneyResetPressed);
        Closed?.Invoke();
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        AudioManager.Instance?.PlayMenuExitButtonClick();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Called by the main-menu context before Show(). The pause-menu instance never
    /// calls this, so Reset Journey stays unavailable mid-level (SALIN-142).
    /// </summary>
    public void EnableJourneyReset()
    {
        _allowJourneyReset = true;
    }

    private bool IsJourneyResetAvailable()
    {
        return _allowJourneyReset && SaveManager.Instance != null &&
            ProgressManager.Instance != null && SceneLoader.Instance != null &&
            ResetJourneyFlow.CanOfferReset(SaveManager.Instance.Mode);
    }

    private void EnsureJourneyResetButton()
    {
        if (!IsJourneyResetAvailable())
        {
            if (_journeyResetButton != null)
                _journeyResetButton.gameObject.SetActive(false);
            return;
        }

        if (_journeyResetButton == null)
            _journeyResetButton = BuildJourneyResetButton();
        _journeyResetButton.gameObject.SetActive(true);
    }

    private Button BuildJourneyResetButton()
    {
        GameObject buttonObject = new("JourneyResetButton_Runtime",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = JourneyResetButtonColor;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.2f, 0.17f);
        rect.anchorMax = new Vector2(0.8f, 0.25f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = ResetJourneyFlow.ConfirmButtonLabel;
        label.fontSize = CloseButtonMinFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        EnsureTextShadow(labelObject);

        return buttonObject.GetComponent<Button>();
    }

    private void OnJourneyResetPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        EnsureResetConfirmationPanel();
        _resetConfirmationPanel.Present(
            ResetJourneyFlow.Execute,
            () => SceneLoader.Instance?.LoadMainMenu());
    }

    private void EnsureResetConfirmationPanel()
    {
        if (_resetConfirmationPanel != null)
            return;
        GameObject panelObject = new("ResetJourneyConfirmationPanel", typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        _resetConfirmationPanel = panelObject.AddComponent<ResetJourneyConfirmationPanel>();
    }

    private void SyncSlidersToAudioManager()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            DebugLogger.LogWarning("SettingsPanel: AudioManager.Instance not available.");
            return;
        }

        if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(audio.MasterVolume);
        if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(audio.BgmVolume);
        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
    }

    private void OnMasterChanged(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
        UpdateVolumeLabels();
    }

    private void OnBgmChanged(float value)
    {
        AudioManager.Instance?.SetBgmVolume(value);
        UpdateVolumeLabels();
    }

    private void OnSfxChanged(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
        UpdateVolumeLabels();
    }

    private void UpdateVolumeLabels()
    {
        UpdateLabel(_masterLabel, "Master Volume", _masterSlider);
        UpdateLabel(_bgmLabel, "BGM Volume", _bgmSlider);
        UpdateLabel(_sfxLabel, "SFX Volume", _sfxSlider);
    }

    private static void UpdateLabel(TMP_Text label, string prefix, Slider slider)
    {
        if (label == null || slider == null)
            return;

        label.color = LabelColor;
        label.fontSize = Mathf.Max(label.fontSize, 26f);
        int percent = Mathf.RoundToInt(slider.value * 100f);
        label.text = $"{prefix}: {percent}%";
    }

    private void SetSlidersInteractable(bool isInteractable)
    {
        if (_masterSlider != null) _masterSlider.interactable = isInteractable;
        if (_bgmSlider != null) _bgmSlider.interactable = isInteractable;
        if (_sfxSlider != null) _sfxSlider.interactable = isInteractable;
    }

    private void EnsureSliderVisuals()
    {
        EnsureSliderVisual(_masterSlider);
        EnsureSliderVisual(_bgmSlider);
        EnsureSliderVisual(_sfxSlider);
    }

    private void EnsureSliderVisual(Slider slider)
    {
        if (slider == null)
            return;

        EnsureSliderStructure(slider);
        Image background = slider.transform.Find("Background")?.GetComponent<Image>();
        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        Image handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;

        Sprite fallback = ResolveSliderFallbackSprite();
        EnsureImageVisible(background, TrackColor, fallback);
        EnsureImageVisible(fill, FillColor, fallback);
        EnsureImageVisible(handle, HandleColor, fallback);

        if (handle != null)
        {
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            if (handleRect != null)
                handleRect.sizeDelta = new Vector2(28f, 28f);
        }

        slider.direction = Slider.Direction.LeftToRight;
    }

    private static void EnsureImageVisible(Image image, Color color, Sprite fallback)
    {
        if (image == null)
            return;

        if (image.sprite == null && fallback != null)
            image.sprite = fallback;

        image.enabled = true;
        image.color = color;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    private Sprite ResolveSliderFallbackSprite()
    {
        if (_sliderFallbackSprite != null)
            return _sliderFallbackSprite;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.sprite != null)
            {
                _sliderFallbackSprite = image.sprite;
                return _sliderFallbackSprite;
            }
        }

        if (s_runtimeWhiteSprite == null)
        {
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            s_runtimeWhiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        _sliderFallbackSprite = s_runtimeWhiteSprite;
        return _sliderFallbackSprite;
    }

    private void ResolveLabelReferencesIfMissing()
    {
        _masterLabel ??= transform.Find("MasterLabel")?.GetComponent<TMP_Text>();
        _bgmLabel ??= transform.Find("BGMLabel")?.GetComponent<TMP_Text>();
        _sfxLabel ??= transform.Find("SFXLabel")?.GetComponent<TMP_Text>();
    }

    private void EnsureSafeArea()
    {
        if (GetComponent<RectTransform>() == null)
            return;

        if (GetComponent<SafeAreaHandler>() == null)
            gameObject.AddComponent<SafeAreaHandler>();
    }

    private void EnsureModalBackdrop()
    {
        if (_modalBackdrop == null)
        {
            Transform existing = transform.Find("ModalBackdrop");
            if (existing == null)
                existing = transform.Find("Background");
            if (existing != null)
                _modalBackdrop = existing.gameObject;
        }

        if (_modalBackdrop == null)
        {
            _modalBackdrop = new GameObject("ModalBackdrop");
            _modalBackdrop.transform.SetParent(transform, false);
            RectTransform rt = _modalBackdrop.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _modalBackdrop.transform.SetSiblingIndex(0);
            _modalBackdrop.AddComponent<CanvasRenderer>();
            Image image = _modalBackdrop.AddComponent<Image>();
            image.raycastTarget = true;
        }

        _modalBackdrop.transform.SetSiblingIndex(0);

        Image backdrop = _modalBackdrop.GetComponent<Image>();
        if (backdrop != null)
        {
            backdrop.color = _modalBackdropColor;
            // Eat clicks so underlying menu can't steal pointer input.
            backdrop.raycastTarget = true;
        }
    }

    private void SetSiblingUiInputEnabled(bool isEnabled)
    {
        if (!_hideSiblingUiWhileOpen || transform.parent == null)
            return;

        if (isEnabled)
        {
            for (int i = 0; i < _disabledSiblingRaycastGraphics.Count; i++)
            {
                Graphic graphic = _disabledSiblingRaycastGraphics[i];
                if (graphic != null)
                    graphic.raycastTarget = true;
            }

            _disabledSiblingRaycastGraphics.Clear();

            return;
        }

        _disabledSiblingRaycastGraphics.Clear();
        for (int i = 0; i < transform.parent.childCount; i++)
        {
            Transform sibling = transform.parent.GetChild(i);
            if (sibling == null || sibling == transform || !sibling.gameObject.activeSelf)
                continue;

            if (ShouldKeepActiveWhileModalIsOpen(sibling.gameObject))
                continue;

            Graphic[] graphics = sibling.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
            {
                Graphic graphic = graphics[g];
                if (graphic == null || !graphic.raycastTarget)
                    continue;

                _disabledSiblingRaycastGraphics.Add(graphic);
                graphic.raycastTarget = false;
            }
        }
    }

    private void EnsureCloseButtonVisible()
    {
        if (_closeButton == null)
            _closeButton = BuildRuntimeCloseButton();

        if (_closeButton == null)
            return;

        StyleCloseButtonLikeMainMenu(_closeButton);

        RectTransform closeRect = _closeButton.GetComponent<RectTransform>();
        if (closeRect == null)
            return;

        closeRect.anchorMin = new Vector2(0.5f, 1f);
        closeRect.anchorMax = new Vector2(0.5f, 1f);
        closeRect.pivot = new Vector2(0f, 1f);
        closeRect.anchorMin = new Vector2(0f, 1f);
        closeRect.anchorMax = new Vector2(0f, 1f);
        closeRect.anchoredPosition = new Vector2(24f, -24f);
        closeRect.sizeDelta = new Vector2(
            Mathf.Max(closeRect.sizeDelta.x, CloseButtonMinSize.x),
            Mathf.Max(closeRect.sizeDelta.y, CloseButtonMinSize.y));

        TMP_Text closeLabel = _closeButton.GetComponentInChildren<TMP_Text>(true);
        if (closeLabel != null)
        {
            closeLabel.text = "Back";
            closeLabel.fontSize = Mathf.Max(closeLabel.fontSize, CloseButtonMinFontSize);
        }
    }

    private void StyleCloseButtonLikeMainMenu(Button button)
    {
        if (button == null)
            return;

        Image buttonImage = button.targetGraphic as Image;
        if (buttonImage == null)
            buttonImage = button.GetComponent<Image>();

        Image templateImage = FindMainMenuButtonTemplateImage();
        if (buttonImage != null)
        {
            if (templateImage != null)
            {
                buttonImage.sprite = templateImage.sprite;
                buttonImage.type = templateImage.type;
                buttonImage.pixelsPerUnitMultiplier = templateImage.pixelsPerUnitMultiplier;
            }

            buttonImage.enabled = true;
            buttonImage.color = Color.white;
            buttonImage.raycastTarget = true;
            button.targetGraphic = buttonImage;
        }

        TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.color = MainMenuButtonTextColor;
            tmpLabel.alignment = TextAlignmentOptions.Center;
            tmpLabel.raycastTarget = false;
            EnsureTextShadow(tmpLabel.gameObject);
        }

        Text legacyLabel = button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.color = MainMenuButtonTextColor;
            legacyLabel.alignment = TextAnchor.MiddleCenter;
            legacyLabel.fontSize = Mathf.Max(legacyLabel.fontSize, Mathf.RoundToInt(CloseButtonMinFontSize));
            legacyLabel.raycastTarget = false;
            EnsureTextShadow(legacyLabel.gameObject);
        }
    }

    private Image FindMainMenuButtonTemplateImage()
    {
        if (transform.parent == null)
            return null;

        for (int i = 0; i < MainMenuButtonTemplateNames.Length; i++)
        {
            Transform template = transform.parent.Find(MainMenuButtonTemplateNames[i]);
            if (template == null)
                continue;

            Image image = template.GetComponent<Image>();
            if (image != null)
                return image;
        }

        return null;
    }

    private static void EnsureTextShadow(GameObject labelObject)
    {
        if (labelObject == null)
            return;

        Shadow shadow = labelObject.GetComponent<Shadow>();
        if (shadow == null)
            shadow = labelObject.AddComponent<Shadow>();

        shadow.effectColor = MainMenuTextShadowColor;
        shadow.effectDistance = MainMenuTextShadowOffset;
        shadow.useGraphicAlpha = true;
    }

    private static void EnsureSliderStructure(Slider slider)
    {
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (sliderRect == null)
            return;

        Transform backgroundTransform = slider.transform.Find("Background");
        if (backgroundTransform == null)
        {
            GameObject backgroundGo = new("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundGo.transform.SetParent(slider.transform, false);
            backgroundTransform = backgroundGo.transform;
        }

        RectTransform backgroundRect = backgroundTransform.GetComponent<RectTransform>();
        if (backgroundRect != null)
        {
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.offsetMin = new Vector2(0f, -6f);
            backgroundRect.offsetMax = new Vector2(0f, 6f);
        }

        Transform fillAreaTransform = slider.transform.Find("Fill Area");
        if (fillAreaTransform == null)
        {
            GameObject fillAreaGo = new("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(slider.transform, false);
            fillAreaTransform = fillAreaGo.transform;
        }

        RectTransform fillAreaRect = fillAreaTransform.GetComponent<RectTransform>();
        if (fillAreaRect != null)
        {
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRect.offsetMin = new Vector2(0f, -6f);
            fillAreaRect.offsetMax = new Vector2(0f, 6f);
        }

        Transform fillTransform = fillAreaTransform.Find("Fill");
        if (fillTransform == null)
        {
            GameObject fillGo = new("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(fillAreaTransform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;
        }
        else if (slider.fillRect == null)
        {
            slider.fillRect = fillTransform.GetComponent<RectTransform>();
        }

        Transform handleAreaTransform = slider.transform.Find("Handle Slide Area");
        if (handleAreaTransform == null)
        {
            GameObject handleAreaGo = new("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(slider.transform, false);
            handleAreaTransform = handleAreaGo.transform;
        }

        RectTransform handleAreaRect = handleAreaTransform.GetComponent<RectTransform>();
        if (handleAreaRect != null)
        {
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(14f, 0f);
            handleAreaRect.offsetMax = new Vector2(-14f, 0f);
        }

        Transform handleTransform = handleAreaTransform.Find("Handle");
        if (handleTransform == null)
        {
            GameObject handleGo = new("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGo.transform.SetParent(handleAreaTransform, false);
            RectTransform handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(28f, 28f);
            slider.handleRect = handleRect;
            slider.targetGraphic = handleGo.GetComponent<Image>();
        }
        else
        {
            if (slider.handleRect == null)
                slider.handleRect = handleTransform.GetComponent<RectTransform>();
            if (slider.targetGraphic == null)
                slider.targetGraphic = handleTransform.GetComponent<Image>();
        }

        if (sliderRect.sizeDelta.y < 32f)
            sliderRect.sizeDelta = new Vector2(sliderRect.sizeDelta.x, 40f);
    }

    private Button BuildRuntimeCloseButton()
    {
        GameObject buttonObject = new("CloseButton_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0.65f, 1f, 0.95f);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = CloseButtonMinSize;

        GameObject labelObj = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        label.text = "Back";
        label.fontSize = CloseButtonMinFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        return buttonObject.GetComponent<Button>();
    }

    private void EnsureCardLayout()
    {
        if (_settingsCardRect == null)
        {
            Transform existing = transform.Find("SettingsCard");
            if (existing != null)
                _settingsCardRect = existing as RectTransform;
        }

        if (_settingsCardRect == null)
        {
            GameObject card = new("SettingsCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(transform, false);
            _settingsCardRect = card.GetComponent<RectTransform>();
            Image cardImage = card.GetComponent<Image>();
            cardImage.color = CardColor;
            cardImage.raycastTarget = false;
        }

        _settingsCardRect.SetSiblingIndex(1);
        _settingsCardRect.anchorMin = new Vector2(0.07f, 0.28f);
        _settingsCardRect.anchorMax = new Vector2(0.93f, 0.76f);
        _settingsCardRect.offsetMin = Vector2.zero;
        _settingsCardRect.offsetMax = Vector2.zero;

        LayoutVolumeRow(_masterLabel, _masterSlider, 0.72f);
        LayoutVolumeRow(_bgmLabel, _bgmSlider, 0.46f);
        LayoutVolumeRow(_sfxLabel, _sfxSlider, 0.2f);
        DisableNonInteractiveRaycastTargets();
    }

    private void LayoutVolumeRow(TMP_Text label, Slider slider, float rowYNormalized)
    {
        if (label == null || slider == null || _settingsCardRect == null)
            return;

        RectTransform labelRect = label.GetComponent<RectTransform>();
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (labelRect == null || sliderRect == null)
            return;

        labelRect.SetParent(_settingsCardRect, false);
        labelRect.anchorMin = new Vector2(0.08f, rowYNormalized + 0.06f);
        labelRect.anchorMax = new Vector2(0.92f, rowYNormalized + 0.06f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(0f, 44f);
        labelRect.anchoredPosition = Vector2.zero;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;

        sliderRect.SetParent(_settingsCardRect, false);
        sliderRect.anchorMin = new Vector2(0.08f, rowYNormalized - 0.06f);
        sliderRect.anchorMax = new Vector2(0.92f, rowYNormalized - 0.06f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(0f, 44f);
        sliderRect.anchoredPosition = Vector2.zero;
    }

    private void DisableNonInteractiveRaycastTargets()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            Button parentButton = text.GetComponentInParent<Button>();
            if (parentButton == _closeButton)
                continue;

            text.raycastTarget = false;
        }
    }

    private static bool ShouldKeepActiveWhileModalIsOpen(GameObject go)
    {
        if (go == null)
            return true;

        if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
            return true;

        if (go.GetComponent<Canvas>() != null)
            return true;

        if (go.GetComponent<GraphicRaycaster>() != null)
            return true;

        if (go.GetComponent<CanvasGroup>() != null)
            return true;

        if (go.name.IndexOf("EventSystem", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private void EnsureTopInputLayer()
    {
        _rootCanvas ??= GetComponent<Canvas>();
        if (_rootCanvas == null)
            _rootCanvas = gameObject.AddComponent<Canvas>();

        _rootCanvas.overrideSorting = true;
        _rootCanvas.sortingOrder = 200;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemGo = new("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }
    }
}
