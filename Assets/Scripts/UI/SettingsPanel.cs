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

    private static readonly Color TrackColor = new(0.24f, 0.20f, 0.16f, 1f);
    private static readonly Color FillColor = new(0.68f, 0.44f, 0.12f, 1f);
    private static readonly Color HandleColor = new(0.95f, 0.83f, 0.52f, 1f);
    private static readonly Color LabelColor = new(1f, 1f, 1f, 1f);
    private static readonly Color CardColor = new(0.07f, 0.1f, 0.17f, 1f);
    private static readonly Vector2 CloseButtonMinSize = new(420f, 152f);
    private const float CloseButtonMinFontSize = UITextScale.Title;

    private static Sprite s_runtimeWhiteSprite;
    private GameObject _modalBackdrop;
    private RectTransform _settingsCardRect;
    private RectTransform _settingsScrollRect;
    private RectTransform _volumeContent;
    private bool _onParchment;
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
        SetSiblingUiInputEnabled(false);
        ResolveLabelReferencesIfMissing();
        EnsureSliderVisuals();
        EnsureCardLayout();
        EnsureCloseButtonVisible();
        SyncSlidersToAudioManager();
        UpdateVolumeLabels();
        SetSlidersInteractable(AudioManager.Instance != null);

        if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (_bgmSlider != null) _bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
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
        UpdateLabel(_masterLabel, "Lahat", _masterSlider);
        UpdateLabel(_bgmLabel, "Musika", _bgmSlider);
        UpdateLabel(_sfxLabel, "Epekto", _sfxSlider);
    }

    private void UpdateLabel(TMP_Text label, string prefix, Slider slider)
    {
        if (label == null || slider == null)
            return;

        label.color = _onParchment ? ScrollPanelArt.InkColor : LabelColor;
        label.fontSize = UITextScale.Title;
        int percent = Mathf.RoundToInt(slider.value * 100f);
        label.text = prefix;
        TMP_Text valueLabel = _volumeContent != null
            ? _volumeContent.Find(slider.name + "Value")?.GetComponent<TMP_Text>() : null;
        if (valueLabel != null)
            valueLabel.text = slider.value <= slider.minValue ? "Mute" : $"{percent}%";
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

        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
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
            {
                handleRect.anchorMin = new Vector2(handleRect.anchorMin.x, 0.5f);
                handleRect.anchorMax = new Vector2(handleRect.anchorMax.x, 0.5f);
                handleRect.sizeDelta = new Vector2(48f, 64f);
            }
        }

        if (handle != null)
        {
            handle.raycastTarget = true;
            slider.targetGraphic = handle;
            Outline outline = handle.GetComponent<Outline>();
            if (outline == null)
                outline = handle.gameObject.AddComponent<Outline>();
            outline.effectColor = ScrollPanelArt.InkColor;
            outline.effectDistance = new Vector2(3f, -3f);
        }
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(1f, 0.94f, 0.78f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.80f, 0.67f, 0.42f);
        slider.colors = colors;
        slider.transition = Selectable.Transition.ColorTint;
        slider.direction = Slider.Direction.LeftToRight;
    }

    private static void EnsureImageVisible(Image image, Color color, Sprite fallback)
    {
        if (image == null)
            return;

        image.overrideSprite = null;
        image.sprite = fallback;
        image.raycastTarget = false;

        image.enabled = true;
        image.color = color;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    private Sprite ResolveSliderFallbackSprite()
    {
        if (_sliderFallbackSprite != null)
            return _sliderFallbackSprite;

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
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null)
            return;

        // The page background covers the display; only its controls need notch padding.
        SafeAreaHandler safeArea = GetComponent<SafeAreaHandler>();
        if (safeArea != null)
            safeArea.enabled = false;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image pageFill = GetComponent<Image>();
        if (pageFill == null)
            pageFill = gameObject.AddComponent<Image>();
        pageFill.sprite = null;
        pageFill.overrideSprite = null;
        pageFill.color = Color.clear;
        pageFill.raycastTarget = false;
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
        ScrollPanelArt.SetAnchors(_modalBackdrop.GetComponent<RectTransform>(),
            Rect.MinMaxRect(0f, 0f, 1f, 1f));

        Image backdrop = _modalBackdrop.GetComponent<Image>();
        if (backdrop != null)
        {
            backdrop.sprite = null;
            backdrop.overrideSprite = null;
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

        StyleCloseButton(_closeButton);

        RectTransform closeRect = _closeButton.GetComponent<RectTransform>();
        if (closeRect == null)
            return;

        closeRect.SetParent(_settingsCardRect, false);
        closeRect.anchorMin = new Vector2(0.08f, 0.96f);
        closeRect.anchorMax = closeRect.anchorMin;
        closeRect.pivot = new Vector2(0f, 1f);
        closeRect.anchoredPosition = Vector2.zero;
        closeRect.sizeDelta = new Vector2(
            Mathf.Max(closeRect.sizeDelta.x, CloseButtonMinSize.x),
            Mathf.Max(closeRect.sizeDelta.y, CloseButtonMinSize.y));

        TMP_Text closeLabel = _closeButton.GetComponentInChildren<TMP_Text>(true);
        if (closeLabel != null)
        {
            closeLabel.text = "Balik";
            TutorialFontProvider.ApplyTo(closeLabel);
            TutorialFontProvider.ClearLegibilityEffects(closeLabel);
            closeLabel.enableAutoSizing = true;
            closeLabel.fontSizeMin = UITextScale.Body;
            closeLabel.fontSizeMax = CloseButtonMinFontSize;
            closeLabel.fontSize = CloseButtonMinFontSize;
        }
    }

    private void StyleCloseButton(Button button)
    {
        if (button == null)
            return;

        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        Image menuImage = transform.parent?.Find("SettingsButton")?.GetComponent<Image>();
        if (image != null)
        {
            Sprite sprite = menuImage != null ? menuImage.sprite : null;
            if (sprite == null)
            {
                foreach (Sprite candidate in Resources.LoadAll<Sprite>("Art/UI/Buttons/ui_button_generic"))
                {
                    if (candidate.name == "ui_button_generic_0")
                    {
                        sprite = candidate;
                        break;
                    }
                }
            }
            image.overrideSprite = null;
            image.sprite = sprite;
            image.type = menuImage != null ? menuImage.type : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = menuImage != null ? menuImage.pixelsPerUnitMultiplier : 1f;
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;
        }
        button.transition = Selectable.Transition.ColorTint;
        button.colors = ColorBlock.defaultColorBlock;
        foreach (Graphic label in button.GetComponentsInChildren<Graphic>(true))
        {
            if (label is TMP_Text tmp)
                tmp.alignment = TextAlignmentOptions.Center;
            else if (label is Text text)
            {
                text.alignment = TextAnchor.MiddleCenter;
                text.fontSize = Mathf.RoundToInt(CloseButtonMinFontSize);
            }
            else
                continue;

            label.color = new Color(0.7019608f, 0.5019608f, 0.07450981f, 1f);
            label.raycastTarget = false;
            Shadow shadow = label.GetComponent<Shadow>();
            if (shadow == null)
                shadow = label.gameObject.AddComponent<Shadow>();
            shadow.enabled = true;
            shadow.effectColor = new Color(0.06f, 0.035f, 0.01f, 1f);
            shadow.effectDistance = new Vector2(5f, -5f);
            shadow.useGraphicAlpha = true;
        }
    }

    private static void EnsureSliderStructure(Slider slider)
    {
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (sliderRect == null)
            return;

        Image hitArea = slider.GetComponent<Image>();
        if (hitArea == null)
            hitArea = slider.gameObject.AddComponent<Image>();
        hitArea.sprite = null;
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;

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
            backgroundRect.offsetMin = new Vector2(24f, -8f);
            backgroundRect.offsetMax = new Vector2(-24f, 8f);
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
            fillAreaRect.offsetMin = new Vector2(24f, -8f);
            fillAreaRect.offsetMax = new Vector2(-24f, 8f);
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

        if (slider.fillRect != null)
        {
            slider.fillRect.offsetMin = Vector2.zero;
            slider.fillRect.offsetMax = Vector2.zero;
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
            // Slider drives both handle anchors; a zero-height travel area keeps
            // the thumb's height fixed while its horizontal anchor follows the value.
            handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRect.offsetMin = new Vector2(24f, 0f);
            handleAreaRect.offsetMax = new Vector2(-24f, 0f);
        }

        Transform handleTransform = handleAreaTransform.Find("Handle");
        if (handleTransform == null)
        {
            GameObject handleGo = new("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGo.transform.SetParent(handleAreaTransform, false);
            RectTransform handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.sizeDelta = new Vector2(48f, 64f);
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
        label.text = "Balik";
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

        Image contentImage = _settingsCardRect.GetComponent<Image>();
        contentImage.sprite = null;
        contentImage.overrideSprite = null;
        contentImage.color = Color.clear;
        contentImage.raycastTarget = false;
        _settingsCardRect.SetSiblingIndex(1);
        ScrollPanelArt.SetAnchors(_settingsCardRect, Rect.MinMaxRect(0f, 0f, 1f, 1f));
        SafeAreaHandler safeArea = _settingsCardRect.GetComponent<SafeAreaHandler>();
        if (safeArea == null)
            safeArea = _settingsCardRect.gameObject.AddComponent<SafeAreaHandler>();
        safeArea.Refresh();

        if (_settingsScrollRect == null)
        {
            Transform existingScroll = _settingsCardRect.Find("SettingsScroll");
            if (existingScroll != null)
                _settingsScrollRect = existingScroll as RectTransform;
        }
        if (_settingsScrollRect == null)
        {
            GameObject scroll = new("SettingsScroll", typeof(RectTransform), typeof(Image));
            scroll.transform.SetParent(_settingsCardRect, false);
            _settingsScrollRect = scroll.GetComponent<RectTransform>();
        }
        Image scrollImage = _settingsScrollRect.GetComponent<Image>();
        scrollImage.color = CardColor;
        scrollImage.raycastTarget = false;
        _onParchment = ScrollPanelArt.ApplyFull(scrollImage);
        ScrollPanelArt.SetAnchors(_settingsScrollRect, ScrollPanelArt.ScrollArea);

        TMP_Text title = transform.Find("Title")?.GetComponent<TMP_Text>();
        title ??= _settingsScrollRect.Find("Title")?.GetComponent<TMP_Text>();
        title ??= _settingsCardRect.Find("Title")?.GetComponent<TMP_Text>();
        title ??= EnsureCardText("Title", "Mga Setting");
        title.rectTransform.SetParent(_settingsScrollRect, false);
        StyleCardText(title, Rect.MinMaxRect(0.16f, 0.81f, 0.84f, 0.87f), UITextScale.Display);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;

        EnsureVolumeViewport();
        LayoutVolumeRow(_masterLabel, _masterSlider, 0.85f, "Lahat ng tunog ng laro");
        LayoutVolumeRow(_bgmLabel, _bgmSlider, 0.52f, "Musika sa laro");
        LayoutVolumeRow(_sfxLabel, _sfxSlider, 0.19f, "Epekto at pagbigkas");
        TMP_Text status = EnsureCardText("AudioStatus", !Application.isPlaying || AudioManager.Instance != null
            ? "Awtomatikong nase-save ang mga pagbabago" : "Hindi magagamit ang mga kontrol sa tunog");
        StyleCardText(status, Rect.MinMaxRect(0.16f, 0.16f, 0.84f, 0.25f), UITextScale.Secondary);
        status.alignment = TextAlignmentOptions.Center;
        DisableNonInteractiveRaycastTargets();
    }

    private void EnsureVolumeViewport()
    {
        if (_volumeContent != null)
            return;
        GameObject host = new("VolumeViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        host.transform.SetParent(_settingsScrollRect, false);
        RectTransform viewport = host.GetComponent<RectTransform>();
        ScrollPanelArt.SetAnchors(viewport, Rect.MinMaxRect(0.16f, 0.28f, 0.84f, 0.79f));
        host.GetComponent<Image>().color = Color.clear;
        GameObject content = new("VolumeContent", typeof(RectTransform));
        content.transform.SetParent(viewport, false);
        _volumeContent = content.GetComponent<RectTransform>();
        _volumeContent.anchorMin = new Vector2(0f, 1f);
        _volumeContent.anchorMax = Vector2.one;
        _volumeContent.pivot = new Vector2(0.5f, 1f);
        _volumeContent.sizeDelta = new Vector2(0f, 1440f);
        ScrollRect scroll = host.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _volumeContent;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        ScrollPanelArt.EnsureVerticalScrollbar(scroll);
    }

    private void LayoutVolumeRow(TMP_Text label, Slider slider, float rowY, string description)
    {
        if (label == null || slider == null || _settingsCardRect == null)
            return;

        label.rectTransform.SetParent(_volumeContent, false);
        StyleCardText(label, Rect.MinMaxRect(0f, rowY + 0.045f, 0.70f, rowY + 0.105f), UITextScale.Title);
        TMP_Text value = EnsureCardText(slider.name + "Value", "");
        value.rectTransform.SetParent(_volumeContent, false);
        StyleCardText(value, Rect.MinMaxRect(0.70f, rowY + 0.045f, 1f, rowY + 0.105f), UITextScale.Title);
        value.alignment = TextAlignmentOptions.MidlineRight;
        TMP_Text hint = EnsureCardText(slider.name + "Hint", description);
        hint.rectTransform.SetParent(_volumeContent, false);
        StyleCardText(hint, Rect.MinMaxRect(0f, rowY - 0.055f, 1f, rowY + 0.045f), UITextScale.Body);
        hint.alignment = TextAlignmentOptions.TopLeft;
        hint.textWrappingMode = TextWrappingModes.Normal;

        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.SetParent(_volumeContent, false);
        sliderRect.anchorMin = new Vector2(0f, rowY - 0.12f);
        sliderRect.anchorMax = new Vector2(1f, rowY - 0.12f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(0f, 152f);
        sliderRect.anchoredPosition = Vector2.zero;
    }

    private TMP_Text EnsureCardText(string name, string text)
    {
        TMP_Text label = _volumeContent == null ? null : _volumeContent.Find(name)?.GetComponent<TMP_Text>();
        label ??= _settingsScrollRect.Find(name)?.GetComponent<TMP_Text>();
        label ??= _settingsCardRect.Find(name)?.GetComponent<TMP_Text>();
        if (label == null)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_settingsScrollRect, false);
            label = go.GetComponent<TMP_Text>();
        }
        label.rectTransform.SetParent(_settingsScrollRect, false);
        label.text = text;
        return label;
    }

    private void StyleCardText(TMP_Text label, Rect area, float fontSize)
    {
        TutorialFontProvider.ApplyTo(label);
        if (_onParchment)
            ScrollPanelArt.Inkify(label);
        else
            label.color = LabelColor;
        ScrollPanelArt.SetAnchors(label.rectTransform, area);
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(fontSize, UITextScale.Body);
        label.fontSizeMax = fontSize;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
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
