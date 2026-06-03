using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnemyDiscoveryOnboardingController : MonoBehaviour
{
    private const float DefaultRevealViewportYFromBottom = 0.72f;
    private const float DefaultRevealTimeoutSeconds = 4f;
    private const float DefaultSafeAreaViewportPadding = 0.02f;
    private const float FullyVisibleAlphaThreshold = 0.99f;
    private const float ReadableBodyFontSize = 24f;
    private const float DismissButtonFontSize = 24f;
    private const float MinimumPanelWidth = 460f;
    private const float MinimumPanelHeight = 220f;

    private static readonly Color MenuButtonTextColor = new(0.7019608f, 0.5019608f, 0.07450981f, 1f);
    private static readonly Color MenuButtonShadowColor = new(0.06f, 0.035f, 0.01f, 1f);
    private static readonly Vector2 MenuButtonShadowOffset = new(5f, -5f);

    [Header("UI References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _targetFrame;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private Button _dismissButton;

    [Header("Copy")]
#pragma warning disable 0414
    [SerializeField] private string _messageTemplate = "New enemy: {0}";
#pragma warning restore 0414

    [Header("Positioning")]
    [SerializeField] private Camera _gameplayCamera;
    [SerializeField] private Vector2 _framePadding = new Vector2(28f, 28f);
    [SerializeField] private Vector2 _fallbackFrameSize = new Vector2(140f, 140f);

    [Header("Reveal Timing")]
    [SerializeField, Range(0.05f, 0.95f)] private float _revealViewportYFromBottom = DefaultRevealViewportYFromBottom;
    [SerializeField] private float _revealTimeoutSeconds = DefaultRevealTimeoutSeconds;
    [SerializeField, Range(0f, 0.2f)] private float _safeAreaViewportPadding = DefaultSafeAreaViewportPadding;

    [Header("Spotlight")]
    [SerializeField] private SpotlightOverlayGraphic _spotlightOverlay;
    [SerializeField] private Vector2 _spotlightPadding = new Vector2(36f, 36f);
    [SerializeField] private Color _dimOverlayColor = new Color(0f, 0f, 0f, 0.78f);

    [Header("Text Animation")]
    [SerializeField] private bool _useTypewriter = true;
    [SerializeField] private float _typewriterCharactersPerSecond = 42f;

    private Enemy _targetEnemy;
    private EnemyDataSO _targetData;
    private Coroutine _queueRoutine;
    private bool _isTypewriterRunning;
    private int _typewriterCharacterCount;
    private float _typewriterVisibleCharacters;
    private float _typewriterLastUpdateTime;
    private bool _enteredPause;
    private readonly Queue<PendingDiscovery> _pendingDiscoveries = new Queue<PendingDiscovery>();

    public bool IsShowing => _canvasGroup != null && _canvasGroup.alpha > 0f;

    private readonly struct PendingDiscovery
    {
        public PendingDiscovery(EnemyDataSO data, Enemy enemy)
        {
            Data = data;
            Enemy = enemy;
        }

        public EnemyDataSO Data { get; }
        public Enemy Enemy { get; }
    }

    private void Awake()
    {
        NormalizeRuntimeConfiguration();

        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (_gameplayCamera == null)
            _gameplayCamera = Camera.main;

        ConfigureDismissButtonVisuals();
        ConfigureTextAndButtonLayout();
        EnsureSpotlightOverlay();
        ConfigureTargetFrame();
        HideImmediate();
    }

    private void OnValidate()
    {
        NormalizeRuntimeConfiguration();
    }

    private void OnEnable()
    {
        EventBus.OnEnemyDiscovered += HandleEnemyDiscovered;

        if (_dismissButton != null)
            _dismissButton.onClick.AddListener(Dismiss);
    }

    private void OnDisable()
    {
        EventBus.OnEnemyDiscovered -= HandleEnemyDiscovered;

        if (_dismissButton != null)
            _dismissButton.onClick.RemoveListener(Dismiss);

        if (_queueRoutine != null)
        {
            StopCoroutine(_queueRoutine);
            _queueRoutine = null;
        }

        StopTypewriter(revealAll: true);
        _pendingDiscoveries.Clear();
        ExitPauseIfNeeded();
        HideImmediate();
        _targetEnemy = null;
        _targetData = null;
    }

    private void HandleEnemyDiscovered(EnemyDataSO data, Enemy enemy)
    {
        if (data == null || enemy == null)
            return;

        if (TutorialRuntimeState.IsActive)
            return;

        if (!CanShow())
        {
            DebugLogger.LogWarning("EnemyDiscoveryOnboardingController: Missing UI references. Discovery overlay skipped.");
            return;
        }

        _pendingDiscoveries.Enqueue(new PendingDiscovery(data, enemy));

        if (_queueRoutine == null)
            _queueRoutine = StartCoroutine(ProcessDiscoveryQueue());
    }

    private IEnumerator ProcessDiscoveryQueue()
    {
        yield return null;

        while (_pendingDiscoveries.Count > 0)
        {
            PendingDiscovery pending = _pendingDiscoveries.Dequeue();
            yield return WaitForRevealReady(pending);

            Enemy enemy = pending.Enemy;
            EnemyDataSO data = pending.Data;
            if (!IsPendingDiscoveryValid(data, enemy)
                || !IsEnemyPastRevealThreshold(enemy)
                || !IsEnemyVisibleForDiscovery(enemy))
            {
                continue;
            }

            _targetEnemy = enemy;
            _targetData = data;
            EnterPauseIfPossible();
            ShowImmediate();
            RenderCopy(data);
            PositionFrameAndSpotlight();

            yield return new WaitUntil(() => !IsShowing);
        }

        _queueRoutine = null;
        if (_pendingDiscoveries.Count > 0 && isActiveAndEnabled)
            _queueRoutine = StartCoroutine(ProcessDiscoveryQueue());
    }

    private void Update()
    {
        if (!IsShowing || _targetEnemy == null)
            return;

        if (!_targetEnemy.gameObject.activeInHierarchy || _targetEnemy.Data != _targetData)
        {
            Dismiss();
            return;
        }

        AdvanceTypewriter();
        PositionFrameAndSpotlight();
    }

    private void RenderCopy(EnemyDataSO data)
    {
        if (_bodyText == null)
            return;

        EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);
        _bodyText.text = BuildFormattedCopy(copy);
        StartTypewriter();
    }

    private void PositionFrameAndSpotlight()
    {
        if (_targetFrame == null || _targetEnemy == null)
            return;

        Camera camera = _gameplayCamera != null ? _gameplayCamera : Camera.main;
        if (camera == null)
            return;

        Bounds bounds = ResolveEnemyBounds(_targetEnemy);
        Vector3 screenCenter = camera.WorldToScreenPoint(bounds.center);
        Vector3 screenMin = camera.WorldToScreenPoint(bounds.min);
        Vector3 screenMax = camera.WorldToScreenPoint(bounds.max);

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas?.worldCamera;

        RectTransform parentRect = _targetFrame.parent as RectTransform;
        Vector2 localCenter = Vector2.zero;
        if (parentRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenCenter, uiCamera, out localCenter))
        {
            _targetFrame.anchoredPosition = localCenter;
        }
        else
        {
            _targetFrame.position = screenCenter;
        }

        Vector2 size = new Vector2(
            Mathf.Abs(screenMax.x - screenMin.x),
            Mathf.Abs(screenMax.y - screenMin.y));
        if (size.x <= 1f || size.y <= 1f)
            size = _fallbackFrameSize;

        Vector2 paddedFrameSize = size + _framePadding;
        _targetFrame.sizeDelta = paddedFrameSize;

        UpdateSpotlightCutout(parentRect, _targetFrame.anchoredPosition, paddedFrameSize);
    }

    private IEnumerator WaitForRevealReady(PendingDiscovery pending)
    {
        float startTime = Time.unscaledTime;

        while (true)
        {
            if (!IsPendingDiscoveryValid(pending.Data, pending.Enemy))
                yield break;

            bool isPastRevealThreshold = IsEnemyPastRevealThreshold(pending.Enemy);
            if (isPastRevealThreshold && IsEnemyVisibleForDiscovery(pending.Enemy))
                yield break;

            if (!isPastRevealThreshold && _revealTimeoutSeconds > 0f && Time.unscaledTime - startTime >= _revealTimeoutSeconds)
                yield break;

            yield return null;
        }
    }

    private bool IsPendingDiscoveryValid(EnemyDataSO data, Enemy enemy)
    {
        return !TutorialRuntimeState.IsActive
            && enemy != null
            && data != null
            && enemy.gameObject.activeInHierarchy
            && enemy.Data == data;
    }

    private static bool IsEnemyVisibleForDiscovery(Enemy enemy)
    {
        if (enemy == null)
            return false;

        PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
        if (phaser != null && !phaser.IsVisible)
            return false;

        bool requiresFullAlpha = phaser != null;
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (renderer is SpriteRenderer spriteRenderer)
            {
                if (requiresFullAlpha && spriteRenderer.color.a < FullyVisibleAlphaThreshold)
                    return false;

                if (!requiresFullAlpha && spriteRenderer.color.a <= 0.01f)
                    continue;
            }
        }

        TextMeshPro[] labels = enemy.GetComponentsInChildren<TextMeshPro>();
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshPro label = labels[i];
            if (label == null || !label.enabled || !label.gameObject.activeInHierarchy)
                continue;

            if (requiresFullAlpha && label.color.a < FullyVisibleAlphaThreshold)
                return false;
        }

        return true;
    }

    private bool IsEnemyPastRevealThreshold(Enemy enemy)
    {
        Camera camera = _gameplayCamera != null ? _gameplayCamera : Camera.main;
        if (camera == null || enemy == null)
            return true;

        Bounds bounds = ResolveEnemyBounds(enemy);
        Vector3 viewportCenter = camera.WorldToViewportPoint(bounds.center);
        return viewportCenter.z >= 0f && viewportCenter.y <= CurrentRevealViewportY();
    }

    public static float ResolveRevealViewportY(float configuredViewportY, Rect safeArea, Vector2Int screenSize, float safeAreaViewportPadding)
    {
        float configured = Mathf.Clamp(configuredViewportY, 0.05f, 0.95f);
        if (screenSize.y <= 0 || safeArea.height <= 0f)
            return configured;

        float safeAreaTop = Mathf.Clamp01(safeArea.yMax / screenSize.y);
        float padding = Mathf.Clamp(safeAreaViewportPadding, 0f, 0.2f);
        return Mathf.Min(configured, Mathf.Max(0.05f, safeAreaTop - padding));
    }

    public static float ResolveRevealViewportY(float configuredViewportY, Rect safeArea, Vector2Int screenSize)
    {
        return ResolveRevealViewportY(configuredViewportY, safeArea, screenSize, 0.02f);
    }

    private float CurrentRevealViewportY()
    {
        return ResolveRevealViewportY(
            _revealViewportYFromBottom,
            Screen.safeArea,
            new Vector2Int(Screen.width, Screen.height),
            _safeAreaViewportPadding);
    }

    private void NormalizeRuntimeConfiguration()
    {
        if (_revealViewportYFromBottom <= 0f)
            _revealViewportYFromBottom = DefaultRevealViewportYFromBottom;

        if (_revealTimeoutSeconds <= 0f)
            _revealTimeoutSeconds = DefaultRevealTimeoutSeconds;

        if (_safeAreaViewportPadding < 0f)
            _safeAreaViewportPadding = DefaultSafeAreaViewportPadding;
    }

    private void EnsureSpotlightOverlay()
    {
        if (_spotlightOverlay == null)
            _spotlightOverlay = GetComponentInChildren<SpotlightOverlayGraphic>(includeInactive: true);

        if (_spotlightOverlay == null)
        {
            GameObject overlayGo = new GameObject("SpotlightOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpotlightOverlayGraphic));
            overlayGo.transform.SetParent(transform, false);
            RectTransform overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            _spotlightOverlay = overlayGo.GetComponent<SpotlightOverlayGraphic>();
        }

        _spotlightOverlay.color = _dimOverlayColor;
        _spotlightOverlay.raycastTarget = false;
        _spotlightOverlay.transform.SetSiblingIndex(0);
    }

    private void ConfigureTargetFrame()
    {
        if (_targetFrame == null)
            return;

        Image frameImage = _targetFrame.GetComponent<Image>();
        if (frameImage != null)
        {
            Color color = frameImage.color;
            color.a = 0f;
            frameImage.color = color;
            frameImage.raycastTarget = false;
        }

        Shadow[] frameEffects = _targetFrame.GetComponents<Shadow>();
        foreach (Shadow frameEffect in frameEffects)
            frameEffect.useGraphicAlpha = true;
    }

    private void ConfigureTextAndButtonLayout()
    {
        if (_bodyText != null)
        {
            TutorialFontProvider.ApplyTo(_bodyText);
            _bodyText.enableAutoSizing = false;
            _bodyText.fontSize = ReadableBodyFontSize;
            _bodyText.fontSizeMin = 20f;
            _bodyText.fontSizeMax = 28f;
            _bodyText.alignment = TextAlignmentOptions.TopLeft;
            _bodyText.textWrappingMode = TextWrappingModes.Normal;
            _bodyText.overflowMode = TextOverflowModes.Truncate;
            _bodyText.richText = true;
            _bodyText.raycastTarget = false;

            RectTransform textRect = _bodyText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0.34f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 10f);
            textRect.offsetMax = new Vector2(-24f, -18f);

            if (textRect.parent is RectTransform panelRect)
            {
                Vector2 panelSize = panelRect.sizeDelta;
                panelSize.x = Mathf.Max(panelSize.x, MinimumPanelWidth);
                panelSize.y = Mathf.Max(panelSize.y, MinimumPanelHeight);
                panelRect.sizeDelta = panelSize;
            }
        }

        if (_dismissButton == null)
            return;

        RectTransform buttonRect = _dismissButton.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 16f);
            buttonRect.sizeDelta = new Vector2(190f, 56f);
        }
    }

    private void ConfigureDismissButtonVisuals()
    {
        if (_dismissButton == null)
            return;

        Image buttonImage = _dismissButton.targetGraphic as Image;
        if (buttonImage == null)
        {
            buttonImage = _dismissButton.GetComponent<Image>();
            if (buttonImage == null)
                buttonImage = _dismissButton.gameObject.AddComponent<Image>();

            _dismissButton.targetGraphic = buttonImage;
        }

        buttonImage.color = Color.white;
        buttonImage.raycastTarget = true;

        ColorBlock colors = _dismissButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.72f, 1f);
        colors.pressedColor = new Color(0.88f, 0.72f, 0.32f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.42f, 0.39f, 0.32f, 0.75f);
        _dismissButton.colors = colors;

        TextMeshProUGUI label = ResolveDismissButtonLabel(_dismissButton);
        if (label == null)
            return;

        TutorialFontProvider.ApplyTo(label);
        label.text = "Got it";
        label.fontSize = DismissButtonFontSize;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.color = MenuButtonTextColor;
        label.raycastTarget = false;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null)
            shadow = label.gameObject.AddComponent<Shadow>();

        shadow.effectColor = MenuButtonShadowColor;
        shadow.effectDistance = MenuButtonShadowOffset;
        shadow.useGraphicAlpha = true;
    }

    private static TextMeshProUGUI ResolveDismissButtonLabel(Button button)
    {
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        if (label != null)
            return label;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(button.transform, false);
        return labelGo.AddComponent<TextMeshProUGUI>();
    }

    private static string BuildFormattedCopy(EnemyDiscoveryCopy copy)
    {
        return $"<size=28><b>{copy.Title}</b></size>\n<size=21>{copy.Description}</size>\n<size=22>Power: {copy.Power}</size>";
    }

    private static int CountVisibleCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int visibleCharacters = 0;
        bool insideRichTextTag = false;
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '<')
            {
                insideRichTextTag = true;
                continue;
            }

            if (insideRichTextTag)
            {
                if (character == '>')
                    insideRichTextTag = false;

                continue;
            }

            visibleCharacters++;
        }

        return visibleCharacters;
    }

    private void StartTypewriter()
    {
        StopTypewriter(revealAll: false);
        if (_bodyText == null)
            return;

        int characterCount = CountVisibleCharacters(_bodyText.text);
        if (!_useTypewriter || _typewriterCharactersPerSecond <= 0f || characterCount <= 0)
        {
            _bodyText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        _typewriterCharacterCount = characterCount;
        _typewriterVisibleCharacters = 0f;
        _typewriterLastUpdateTime = Time.realtimeSinceStartup;
        _bodyText.maxVisibleCharacters = 0;
        _isTypewriterRunning = true;
    }

    private void AdvanceTypewriter()
    {
        if (!_isTypewriterRunning || _bodyText == null)
            return;

        float currentTime = Time.realtimeSinceStartup;
        float elapsedSeconds = Mathf.Max(Time.unscaledDeltaTime, currentTime - _typewriterLastUpdateTime);
        _typewriterLastUpdateTime = currentTime;
        if (elapsedSeconds <= 0f)
            elapsedSeconds = 1f / 60f;

        _typewriterVisibleCharacters += _typewriterCharactersPerSecond * elapsedSeconds;
        int visibleCharacters = Mathf.Clamp(Mathf.FloorToInt(_typewriterVisibleCharacters), 0, _typewriterCharacterCount);
        if (visibleCharacters >= _typewriterCharacterCount)
        {
            _bodyText.maxVisibleCharacters = int.MaxValue;
            _isTypewriterRunning = false;
            return;
        }

        _bodyText.maxVisibleCharacters = visibleCharacters;
    }

    private void StopTypewriter(bool revealAll)
    {
        _isTypewriterRunning = false;
        _typewriterCharacterCount = 0;
        _typewriterVisibleCharacters = 0f;

        if (revealAll && _bodyText != null)
            _bodyText.maxVisibleCharacters = int.MaxValue;
    }

    private void UpdateSpotlightCutout(RectTransform parentRect, Vector2 localCenter, Vector2 paddedFrameSize)
    {
        if (_spotlightOverlay == null)
            return;

        RectTransform overlayRect = _spotlightOverlay.rectTransform;
        Vector2 cutoutSize = paddedFrameSize + _spotlightPadding;
        Vector2 overlayLocalCenter = localCenter;

        if (parentRect != null && overlayRect != parentRect)
        {
            Vector3 worldCenter = parentRect.TransformPoint(localCenter);
            overlayLocalCenter = overlayRect.InverseTransformPoint(worldCenter);
        }

        Rect cutout = new Rect(
            overlayLocalCenter.x - cutoutSize.x * 0.5f,
            overlayLocalCenter.y - cutoutSize.y * 0.5f,
            cutoutSize.x,
            cutoutSize.y);

        _spotlightOverlay.SetCutout(cutout);
    }

    private static Bounds ResolveEnemyBounds(Enemy enemy)
    {
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        Bounds combinedBounds = default;
        bool hasRendererBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (IsEnemyDebugLabelRenderer(renderer))
                continue;

            if (!hasRendererBounds)
            {
                combinedBounds = renderer.bounds;
                hasRendererBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasRendererBounds)
            return combinedBounds;

        Collider2D collider = enemy.GetComponentInChildren<Collider2D>();
        if (collider != null)
            return collider.bounds;

        return new Bounds(enemy.transform.position, Vector3.one);
    }

    private static bool IsEnemyDebugLabelRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        if (renderer.GetComponent<TextMeshPro>() == null)
            return false;

        return renderer.gameObject.name == "BaybayinLabel"
            || renderer.gameObject.name == "EnemyTypeLabel";
    }

    private bool CanShow()
    {
        return _canvasGroup != null
            && _targetFrame != null
            && _bodyText != null
            && _dismissButton != null;
    }

    private void EnterPauseIfPossible()
    {
        if (GameManager.Instance == null)
            return;

        if (GameManager.Instance.CurrentState != GameState.Playing)
            return;

        GameManager.Instance.EnterDialoguePause();
        _enteredPause = GameManager.Instance.CurrentState == GameState.Paused;
    }

    private void ExitPauseIfNeeded()
    {
        if (!_enteredPause || GameManager.Instance == null)
        {
            _enteredPause = false;
            return;
        }

        GameManager.Instance.ExitDialoguePause();
        _enteredPause = false;
    }

    private void ShowImmediate()
    {
        _targetFrame.gameObject.SetActive(true);
        if (_spotlightOverlay != null)
            _spotlightOverlay.gameObject.SetActive(true);

        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
    }

    private void HideImmediate()
    {
        StopTypewriter(revealAll: true);

        if (_targetFrame != null)
            _targetFrame.gameObject.SetActive(false);

        if (_spotlightOverlay != null)
        {
            _spotlightOverlay.ClearCutout();
            _spotlightOverlay.gameObject.SetActive(false);
        }

        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    public void Dismiss()
    {
        ExitPauseIfNeeded();
        HideImmediate();
        _targetEnemy = null;
        _targetData = null;
    }
}
