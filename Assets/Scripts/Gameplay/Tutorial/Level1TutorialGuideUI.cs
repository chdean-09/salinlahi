using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Level1TutorialGuideUI : MonoBehaviour
{
    private const float PromptMinY = 0.895f;
    private const float PromptMaxY = 0.965f;
    private const float FeedbackMinY = 0.805f;
    private const float FeedbackMaxY = 0.875f;
    private const float TextMinX = 0.06f;
    private const float TextMaxX = 0.94f;

    [Header("Panel")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _promptText;
    [SerializeField] private TMP_Text _feedbackText;
    [SerializeField] private Button _skipButton;

    [Header("Guide Visuals")]
    [Tooltip("LineRenderer or similar to draw the guide path.")]
    [SerializeField] private LineRenderer _guidePathRenderer;
    [Tooltip("Optional UI image that shows the syllable glyph being copied.")]
    [SerializeField] private Image _guideSpriteImage;
    [Tooltip("Transform of the start dot (will be pulsed).")]
    [SerializeField] private Transform _startDot;
    [Tooltip("Transform of the direction arrow.")]
    [SerializeField] private Transform _directionArrow;
    [Tooltip("Parent for assist animation instances.")]
    [SerializeField] private Transform _assistAnimationParent;

    private System.Action _skipRequested;
    private Coroutine _pulseCoroutine;
    private Coroutine _animatePathCoroutine;
    private Coroutine _heartbeatCoroutine;
    private Vector3[] _originalGuidePathPoints;

    private void Awake()
    {
        TutorialFontProvider.ApplyTo(_promptText);
        TutorialFontProvider.ApplyTo(_feedbackText);
        ApplyResponsiveTextLayout(_promptText, _feedbackText);
        EnsureGuideVisuals();

        if (_skipButton != null)
        {
            TMP_Text skipLabel = _skipButton.GetComponentInChildren<TMP_Text>(true);
            TutorialFontProvider.ApplyTo(skipLabel);
        }
    }

    public static Level1TutorialGuideUI CreateRuntime()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject root = new("Level1TutorialGuideUI", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Level1TutorialGuideUI guide = root.AddComponent<Level1TutorialGuideUI>();
        guide._root = root;
        guide._promptText = CreateText(root.transform, "PromptText", new Vector2(0.5f, 0.88f), 42, TextAlignmentOptions.Center);
        guide._feedbackText = CreateText(root.transform, "FeedbackText", new Vector2(0.5f, 0.76f), 28, TextAlignmentOptions.Center);
        ApplyResponsiveTextLayout(guide._promptText, guide._feedbackText);
        guide._skipButton = CreateSkipButton(root.transform);
        guide.EnsureGuideVisuals();
        root.SetActive(false);
        return guide;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        Vector2 anchor,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(900f, 120f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
        return text;
    }

    public static void ApplyResponsiveTextLayout(TMP_Text promptText, TMP_Text feedbackText)
    {
        ConfigureTextBand(promptText, PromptMinY, PromptMaxY, 38f, 56f);
        ConfigureTextBand(feedbackText, FeedbackMinY, FeedbackMaxY, 32f, 46f);
    }

    private static void ConfigureTextBand(TMP_Text text, float minY, float maxY, float minFontSize, float maxFontSize)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(TextMinX, minY);
        rect.anchorMax = new Vector2(TextMaxX, maxY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMin = minFontSize;
        text.fontSizeMax = maxFontSize;
        text.fontSize = maxFontSize;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
    }

    private static Button CreateSkipButton(Transform parent)
    {
        GameObject buttonObject = new("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-48f, -96f);
        rect.sizeDelta = new Vector2(120f, 56f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.45f);

        TextMeshProUGUI label = CreateText(buttonObject.transform, "Label", new Vector2(0.5f, 0.5f), 24, TextAlignmentOptions.Center);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.sizeDelta = rect.sizeDelta;
        label.text = "Skip";

        buttonObject.SetActive(false);
        return buttonObject.GetComponent<Button>();
    }

    public void Initialize(System.Action skipRequested)
    {
        EnsureRuntimeCanvas();
        ApplyResponsiveTextLayout(_promptText, _feedbackText);
        _skipRequested = skipRequested;
        if (_skipButton != null)
            _skipButton.onClick.AddListener(HandleSkipClicked);
    }

    private void EnsureRuntimeCanvas()
    {
        Canvas currentCanvas = GetComponentInParent<Canvas>();
        if (currentCanvas != null
            && currentCanvas.transform.localScale != Vector3.zero
            && currentCanvas.name == "TutorialCanvas")
        {
            return;
        }

        Canvas tutorialCanvas = FindTutorialCanvas();
        if (tutorialCanvas == null)
        {
            GameObject canvasObject = new("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            tutorialCanvas = canvasObject.GetComponent<Canvas>();
            tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        tutorialCanvas.sortingOrder = 110;
        tutorialCanvas.transform.localScale = Vector3.one;
        transform.SetParent(tutorialCanvas.transform, false);
        transform.SetAsLastSibling();
    }

    private static Canvas FindTutorialCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == "TutorialCanvas")
                return canvas;
        }

        return null;
    }

    public void PrepareForChallenge()
    {
        EnsureRuntimeCanvas();
        EnsureGuideVisuals();
    }

    private void OnDestroy()
    {
        if (_skipButton != null)
            _skipButton.onClick.RemoveListener(HandleSkipClicked);
    }

    public void ShowPrompt(Level1TutorialStepSO step, bool canSkip)
    {
        EnsureGuideVisuals();
        if (_root != null)
            _root.SetActive(true);

        ApplyResponsiveTextLayout(_promptText, _feedbackText);
        transform.SetAsLastSibling();

        if (_promptText != null)
            _promptText.text = step != null ? step.promptText : string.Empty;

        if (_feedbackText != null)
            _feedbackText.text = string.Empty;

        if (_skipButton != null)
            _skipButton.gameObject.SetActive(canSkip);

        ShowGuideSprite(step);

        if (_heartbeatCoroutine != null)
            StopCoroutine(_heartbeatCoroutine);
        _heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());

        // Set up guide visuals if available
        if (_guidePathRenderer != null && step != null && step.templatePoints != null && step.templatePoints.Length > 1)
        {
            _guidePathRenderer.positionCount = step.templatePoints.Length;
            _originalGuidePathPoints = new Vector3[step.templatePoints.Length];
            for (int i = 0; i < step.templatePoints.Length; i++)
            {
                Vector3 pos = step.templatePoints[i];
                _guidePathRenderer.SetPosition(i, pos);
                _originalGuidePathPoints[i] = pos;
            }
            _guidePathRenderer.enabled = true;
            _guidePathRenderer.gameObject.SetActive(true);
        }

        if (_startDot != null)
        {
            if (step != null && step.templatePoints != null && step.templatePoints.Length > 0)
            {
                SetGuidePosition(_startDot, step.templatePoints[0]);
                _startDot.gameObject.SetActive(true);
            }
            else
            {
                _startDot.gameObject.SetActive(false);
            }
        }

        if (_directionArrow != null)
        {
            if (step != null && step.templatePoints != null && step.templatePoints.Length > 1)
            {
                Vector2 first = step.templatePoints[0];
                Vector2 second = step.templatePoints[1];
                Vector2 dir = (second - first).normalized;
                SetGuidePosition(_directionArrow, first + dir * 0.5f);
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _directionArrow.localRotation = Quaternion.Euler(0, 0, angle);
                _directionArrow.gameObject.SetActive(true);
            }
            else
            {
                _directionArrow.gameObject.SetActive(false);
            }
        }
    }

    public void ShowMessage(string message, bool canSkip)
    {
        if (_root != null)
            _root.SetActive(true);

        ApplyResponsiveTextLayout(_promptText, _feedbackText);
        transform.SetAsLastSibling();

        if (_promptText != null)
            _promptText.text = message ?? string.Empty;

        if (_feedbackText != null)
            _feedbackText.text = string.Empty;

        if (_skipButton != null)
            _skipButton.gameObject.SetActive(canSkip);

        if (_guideSpriteImage != null)
            _guideSpriteImage.gameObject.SetActive(false);
    }

    public void ShowFeedback(string message)
    {
        ApplyResponsiveTextLayout(_promptText, _feedbackText);
        transform.SetAsLastSibling();

        if (_feedbackText != null)
            _feedbackText.text = message ?? string.Empty;
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);

        if (_guidePathRenderer != null)
        {
            _guidePathRenderer.enabled = false;
            _guidePathRenderer.gameObject.SetActive(false);
        }

        if (_startDot != null)
            _startDot.gameObject.SetActive(false);

        if (_directionArrow != null)
            _directionArrow.gameObject.SetActive(false);

        if (_guideSpriteImage != null)
            _guideSpriteImage.gameObject.SetActive(false);

        StopEffects();
    }

    public void PulseStartDot()
    {
        if (_startDot == null)
            return;

        if (_pulseCoroutine != null)
            StopCoroutine(_pulseCoroutine);

        _pulseCoroutine = StartCoroutine(PulseCoroutine());
    }

    public void AnimateGuidePath()
    {
        if (_guidePathRenderer == null || _guidePathRenderer.positionCount < 2)
            return;

        if (_animatePathCoroutine != null)
            StopCoroutine(_animatePathCoroutine);

        _animatePathCoroutine = StartCoroutine(AnimatePathCoroutine());
    }

    public void PlayAssistAnimation(GameObject prefab)
    {
        if (prefab == null || _assistAnimationParent == null)
            return;

        GameObject instance = Instantiate(prefab, _assistAnimationParent);
        Destroy(instance, 3f); // Clean up after animation
    }

    private void ShowGuideSprite(Level1TutorialStepSO step)
    {
        if (step == null || step.guideSprite == null)
        {
            if (_guideSpriteImage != null)
                _guideSpriteImage.gameObject.SetActive(false);
            return;
        }

        Image image = EnsureGuideSpriteImage();
        if (image == null)
            return;

        image.sprite = step.guideSprite;
        image.preserveAspect = true;
        image.gameObject.SetActive(true);
    }

    private Image EnsureGuideSpriteImage()
    {
        if (_guideSpriteImage != null)
            return _guideSpriteImage;

        Transform parent = _root != null ? _root.transform : transform;
        Transform existing = parent.Find("GuideSpriteImage");
        if (existing != null)
        {
            _guideSpriteImage = existing.GetComponent<Image>();
            if (_guideSpriteImage != null)
                return _guideSpriteImage;
        }

        GameObject imageObject = new("GuideSpriteImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 120f);
        rect.sizeDelta = new Vector2(260f, 260f);

        _guideSpriteImage = imageObject.GetComponent<Image>();
        _guideSpriteImage.raycastTarget = false;
        _guideSpriteImage.color = new Color(1f, 1f, 1f, 0.55f);
        imageObject.SetActive(false);
        return _guideSpriteImage;
    }

    private void EnsureGuideVisuals()
    {
        Transform parent = _root != null ? _root.transform : transform;

        if (_guidePathRenderer == null)
        {
            Transform existing = parent.Find("GuidePathRenderer");
            if (existing != null)
                _guidePathRenderer = existing.GetComponent<LineRenderer>();
        }
        if (_guidePathRenderer == null)
        {
            GameObject pathObject = new GameObject("GuidePathRenderer");
            pathObject.transform.SetParent(parent, false);
            _guidePathRenderer = pathObject.AddComponent<LineRenderer>();
            _guidePathRenderer.useWorldSpace = false;
            _guidePathRenderer.widthMultiplier = 5f;
            _guidePathRenderer.startColor = new Color(0.1f, 0.95f, 0.45f, 0.9f);
            _guidePathRenderer.endColor = new Color(0.1f, 0.95f, 0.45f, 0.9f);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                _guidePathRenderer.material = new Material(shader);
        }
        _guidePathRenderer.gameObject.SetActive(false);

        if (_startDot == null)
        {
            Transform existing = parent.Find("StartDot");
            if (existing != null)
                _startDot = existing;
        }
        if (_startDot == null)
        {
            GameObject dotObject = new GameObject("StartDot", typeof(RectTransform), typeof(Image));
            dotObject.transform.SetParent(parent, false);
            RectTransform dotRect = dotObject.GetComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(26f, 26f);
            Image dotImage = dotObject.GetComponent<Image>();
            dotImage.color = new Color(0.1f, 1f, 0.2f, 1f);
            dotImage.raycastTarget = false;
            _startDot = dotObject.transform;
        }
        _startDot.gameObject.SetActive(false);

        if (_directionArrow == null)
        {
            Transform existing = parent.Find("DirectionArrow");
            if (existing != null)
                _directionArrow = existing;
        }
        if (_directionArrow == null)
        {
            GameObject arrowObject = new GameObject("DirectionArrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            arrowObject.transform.SetParent(parent, false);
            RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(72f, 48f);
            TextMeshProUGUI arrowText = arrowObject.GetComponent<TextMeshProUGUI>();
            arrowText.text = "➜";
            arrowText.fontSize = 38f;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.color = new Color(0.1f, 1f, 0.2f, 1f);
            arrowText.raycastTarget = false;
            TutorialFontProvider.ApplyTo(arrowText);
            _directionArrow = arrowObject.transform;
        }
        _directionArrow.gameObject.SetActive(false);

        if (_assistAnimationParent == null)
            _assistAnimationParent = parent;
    }

    private static void SetGuidePosition(Transform target, Vector3 position)
    {
        if (target == null)
            return;

        RectTransform rect = target as RectTransform;
        if (rect != null)
            rect.anchoredPosition = position;
        else
            target.localPosition = position;
    }

    private void StopEffects()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
        if (_animatePathCoroutine != null)
        {
            StopCoroutine(_animatePathCoroutine);
            _animatePathCoroutine = null;
            RestoreGuidePath();
        }
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }
    }

    private System.Collections.IEnumerator PulseCoroutine()
    {
        if (_startDot == null)
            yield break;

        Vector3 baseScale = _startDot.localScale;
        float duration = 0.6f;
        while (_startDot != null)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_startDot == null)
                    yield break;
                float t = Mathf.PingPong(elapsed / duration, 1f);
                float scale = Mathf.Lerp(1f, 1.3f, t);
                _startDot.localScale = baseScale * scale;
                yield return null;
            }
        }
    }

    private System.Collections.IEnumerator AnimatePathCoroutine()
    {
        if (_guidePathRenderer == null)
            yield break;

        int totalPoints = _guidePathRenderer.positionCount;
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int visiblePoints = Mathf.Max(2, Mathf.CeilToInt(t * totalPoints));
            _guidePathRenderer.positionCount = visiblePoints;
            yield return null;
        }

        RestoreGuidePath();
    }

    private void RestoreGuidePath()
    {
        if (_guidePathRenderer == null || _originalGuidePathPoints == null)
            return;

        _guidePathRenderer.positionCount = _originalGuidePathPoints.Length;
        for (int i = 0; i < _originalGuidePathPoints.Length; i++)
            _guidePathRenderer.SetPosition(i, _originalGuidePathPoints[i]);
    }

    private System.Collections.IEnumerator HeartbeatCoroutine()
    {
        float cycleDuration = 1.2f;
        while (true)
        {
            float elapsed = 0f;
            while (elapsed < cycleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.PingPong(elapsed / cycleDuration, 1f);
                float alpha = Mathf.Lerp(0.35f, 0.75f, t);
                if (_guideSpriteImage != null)
                    _guideSpriteImage.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
        }
    }

    private void HandleSkipClicked()
    {
        _skipRequested?.Invoke();
    }
}
