using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeartDisplay : MonoBehaviour
{
    private const string TutorialDamageOverlayName = "TutorialHeartDamageOverlay";
    private const string TutorialDamageContainerName = "TutorialHeartDamageContainer";
    private const string TutorialDamageIconName = "TutorialHeartDamageIcon";
    private const string TutorialDamageIndicatorName = "TutorialHeartDamageIndicator";

    [Header("Heart Display")]
    [SerializeField] private Image[] _heartIcons;
    [SerializeField] private Sprite _heartFull;
    [SerializeField] private Sprite _heartEmpty;
    [SerializeField] private float _heartShakeDuration = 0.2f;
    [SerializeField] private float _heartPunchScale = 1.2f;

    [Header("Tutorial Demo")]
    [SerializeField] private float _tutorialDamageIndicatorSeconds = 0.95f;
    [SerializeField] private Color _tutorialDamageIndicatorColor = new Color(1f, 0.1f, 0.1f, 1f);

    private Coroutine[] _heartAnimRoutines;
    private Vector3[] _heartBaseScales;
    private bool[] _heartScaleCached;
    private int _lastHeartCount;

    private int _tutorialDemoEmptiedIndex = -1;
    private Coroutine _tutorialDamageIndicatorRoutine;
    private Coroutine _tutorialDamageOverlayRoutine;
    private Canvas _tutorialDamageCanvas;
    private RectTransform _tutorialDamageCanvasRect;
    private RectTransform _tutorialDamageContainerRect;
    private Image _tutorialDamageIconImage;
    private CanvasGroup _tutorialDamageGroup;

    private void OnEnable()
    {
        EnsureRuntimeBuffers();
        EventBus.OnHeartsChanged += UpdateHearts;
        EventBus.OnTutorialBaseHitDemo += HandleTutorialDemoHit;
        EventBus.OnTutorialBaseRestoreDemo += HandleTutorialDemoRestore;

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        if (heartSystem != null)
            UpdateHearts(heartSystem.GetCurrentHearts());
    }

    private void OnDisable()
    {
        EventBus.OnHeartsChanged -= UpdateHearts;
        EventBus.OnTutorialBaseHitDemo -= HandleTutorialDemoHit;
        EventBus.OnTutorialBaseRestoreDemo -= HandleTutorialDemoRestore;

        if (_tutorialDamageIndicatorRoutine != null)
        {
            StopCoroutine(_tutorialDamageIndicatorRoutine);
            _tutorialDamageIndicatorRoutine = null;
        }

        if (_tutorialDamageOverlayRoutine != null)
        {
            StopCoroutine(_tutorialDamageOverlayRoutine);
            _tutorialDamageOverlayRoutine = null;
        }

        if (_tutorialDamageContainerRect != null)
            _tutorialDamageContainerRect.gameObject.SetActive(false);

        if (_heartIcons == null || _heartAnimRoutines == null || _heartBaseScales == null)
            return;

        for (int i = 0; i < _heartIcons.Length && i < _heartAnimRoutines.Length; i++)
        {
            if (_heartAnimRoutines[i] != null)
            {
                StopCoroutine(_heartAnimRoutines[i]);
                _heartAnimRoutines[i] = null;
            }

            if (_heartIcons[i] != null && i < _heartBaseScales.Length && i < _heartScaleCached.Length && _heartScaleCached[i])
                _heartIcons[i].transform.localScale = _heartBaseScales[i];

            Transform marker = _heartIcons[i] != null ? _heartIcons[i].transform.Find(TutorialDamageIndicatorName) : null;
            if (marker != null)
                marker.gameObject.SetActive(false);
        }
    }

    private void UpdateHearts(int current)
    {
        EnsureRuntimeBuffers();
        int previous = _lastHeartCount;
        bool lost = current < previous;
        _lastHeartCount = current;

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            if (_heartIcons[i] == null) continue;

            CacheBaseScale(i);

            bool filled = i < current;
            ApplyHeartVisual(_heartIcons[i], filled);

            if (lost && i >= current && i < previous)
            {
                if (i < _heartAnimRoutines.Length && _heartAnimRoutines[i] != null)
                    StopCoroutine(_heartAnimRoutines[i]);

                if (i < _heartAnimRoutines.Length)
                    _heartAnimRoutines[i] = StartCoroutine(HeartLossAnimation(i, _heartIcons[i]));
            }
        }
    }

    private void HandleTutorialDemoHit(int _)
    {
        EnsureRuntimeBuffers();
        if (_heartIcons == null) return;

        int targetIndex = ResolveTutorialDemoTargetIndex();
        if (targetIndex < 0) return;

        CacheBaseScale(targetIndex);
        _tutorialDemoEmptiedIndex = targetIndex;

        // Empty the heart and keep it empty — the restore is now an explicit, explained
        // step (HandleTutorialDemoRestore), not a silent timed snap-back.
        ApplyHeartVisual(_heartIcons[targetIndex], filled: false);

        if (targetIndex < _heartAnimRoutines.Length && _heartAnimRoutines[targetIndex] != null)
            StopCoroutine(_heartAnimRoutines[targetIndex]);
        if (targetIndex < _heartAnimRoutines.Length)
            _heartAnimRoutines[targetIndex] = StartCoroutine(HeartLossAnimation(targetIndex, _heartIcons[targetIndex]));

        ShowTutorialDamageIndicator(targetIndex);
    }

    // Tutorial-only: visibly refill the heart the demo hit emptied, with a pulse so the
    // restoration reads as intentional rather than a sudden snap-back.
    private void HandleTutorialDemoRestore()
    {
        EnsureRuntimeBuffers();
        int index = _tutorialDemoEmptiedIndex;
        _tutorialDemoEmptiedIndex = -1;
        if (_heartIcons == null || index < 0 || index >= _heartIcons.Length || _heartIcons[index] == null)
            return;

        ApplyHeartVisual(_heartIcons[index], filled: true);
        PlayTutorialOverlayRestore(_heartIcons[index]);

        CacheBaseScale(index);
        if (index < _heartAnimRoutines.Length && _heartAnimRoutines[index] != null)
            StopCoroutine(_heartAnimRoutines[index]);
        if (index < _heartAnimRoutines.Length)
            _heartAnimRoutines[index] = StartCoroutine(HeartRestorePulse(index, _heartIcons[index]));
    }

    private IEnumerator HeartRestorePulse(int index, Image heart)
    {
        if (heart == null) yield break;
        Vector3 baseScale = index < _heartBaseScales.Length && _heartScaleCached[index]
            ? _heartBaseScales[index] : heart.transform.localScale;

        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (heart == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.LerpUnclamped(1.45f, 1f, t);
            heart.transform.localScale = baseScale * scale;
            yield return null;
        }
        if (heart != null) heart.transform.localScale = baseScale;
        if (index < _heartAnimRoutines.Length) _heartAnimRoutines[index] = null;
    }

    private void CacheBaseScale(int index)
    {
        if (_heartIcons == null || index >= _heartIcons.Length)
            return;

        if (index >= _heartBaseScales.Length)
            return;

        if (!_heartScaleCached[index] && _heartIcons[index] != null)
        {
            _heartBaseScales[index] = _heartIcons[index].transform.localScale;
            _heartScaleCached[index] = true;
        }
    }

    private void ApplyHeartVisual(Image heart, bool filled)
    {
        if (heart == null)
            return;

        if (_heartFull != null && _heartEmpty != null)
        {
            heart.sprite = filled ? _heartFull : _heartEmpty;
            return;
        }

        heart.color = filled ? Color.red : new Color(1f, 1f, 1f, 0.25f);
    }

    private int ResolveTutorialDemoTargetIndex()
    {
        if (_heartIcons == null || _heartIcons.Length == 0)
            return -1;

        int visibleHeartCount = _lastHeartCount;
        if (visibleHeartCount <= 0)
        {
            HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
            if (heartSystem != null)
            {
                visibleHeartCount = heartSystem.GetCurrentHearts();
                _lastHeartCount = visibleHeartCount;
            }
        }

        if (visibleHeartCount <= 0)
            visibleHeartCount = _heartIcons.Length;

        for (int i = _heartIcons.Length - 1; i >= 0; i--)
        {
            if (_heartIcons[i] != null && i < visibleHeartCount)
                return i;
        }

        return -1;
    }

    private void ShowTutorialDamageIndicator(int heartIndex)
    {
        if (_heartIcons == null
            || heartIndex < 0
            || heartIndex >= _heartIcons.Length
            || _heartIcons[heartIndex] == null)
        {
            return;
        }

        TextMeshProUGUI indicator = EnsureTutorialDamageIndicator();
        if (indicator == null)
            return;

        PositionTutorialDamageOverlay(_heartIcons[heartIndex].rectTransform);
        CopyHeartVisualToTutorialOverlay(_heartIcons[heartIndex]);

        if (_tutorialDamageIndicatorRoutine != null)
            StopCoroutine(_tutorialDamageIndicatorRoutine);

        _tutorialDamageIndicatorRoutine = StartCoroutine(TutorialDamageIndicatorRoutine(indicator));
    }

    private TextMeshProUGUI EnsureTutorialDamageIndicator()
    {
        EnsureTutorialDamageOverlay();
        if (_tutorialDamageContainerRect == null)
            return null;

        Transform existing = _tutorialDamageContainerRect.Find(TutorialDamageIndicatorName);
        TextMeshProUGUI indicator = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (indicator != null)
            return indicator;

        GameObject marker = new(TutorialDamageIndicatorName, typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI), typeof(Outline));
        marker.transform.SetParent(_tutorialDamageContainerRect, false);

        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(96f, 48f);

        indicator = marker.GetComponent<TextMeshProUGUI>();
        indicator.text = "-1";
        indicator.fontSize = 32f;
        indicator.alignment = TextAlignmentOptions.Center;
        indicator.color = _tutorialDamageIndicatorColor;
        indicator.raycastTarget = false;
        TutorialFontProvider.ApplyTo(indicator);

        Outline outline = marker.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        marker.SetActive(false);
        return indicator;
    }

    private void EnsureTutorialDamageOverlay()
    {
        if (_tutorialDamageCanvas != null
            && _tutorialDamageCanvasRect != null
            && _tutorialDamageContainerRect != null
            && _tutorialDamageIconImage != null
            && _tutorialDamageGroup != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find(TutorialDamageOverlayName);
        if (canvasObject == null)
            canvasObject = new GameObject(TutorialDamageOverlayName, typeof(RectTransform), typeof(Canvas));

        _tutorialDamageCanvas = canvasObject.GetComponent<Canvas>();
        _tutorialDamageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _tutorialDamageCanvas.sortingOrder = RenderOrder.LoadingCanvas + 100;
        _tutorialDamageCanvas.pixelPerfect = false;

        _tutorialDamageCanvasRect = canvasObject.GetComponent<RectTransform>();
        _tutorialDamageCanvasRect.anchorMin = Vector2.zero;
        _tutorialDamageCanvasRect.anchorMax = Vector2.one;
        _tutorialDamageCanvasRect.offsetMin = Vector2.zero;
        _tutorialDamageCanvasRect.offsetMax = Vector2.zero;

        Transform containerTransform = canvasObject.transform.Find(TutorialDamageContainerName);
        if (containerTransform == null)
        {
            GameObject containerObject = new(TutorialDamageContainerName, typeof(RectTransform), typeof(CanvasGroup));
            containerObject.transform.SetParent(canvasObject.transform, false);
            containerTransform = containerObject.transform;
        }

        _tutorialDamageContainerRect = containerTransform as RectTransform;
        _tutorialDamageGroup = containerTransform.GetComponent<CanvasGroup>();
        if (_tutorialDamageGroup == null)
            _tutorialDamageGroup = containerTransform.gameObject.AddComponent<CanvasGroup>();
        _tutorialDamageGroup.blocksRaycasts = false;
        _tutorialDamageGroup.interactable = false;

        Transform iconTransform = containerTransform.Find(TutorialDamageIconName);
        if (iconTransform == null)
        {
            GameObject iconObject = new(TutorialDamageIconName, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(containerTransform, false);
            iconTransform = iconObject.transform;
        }

        RectTransform iconRect = iconTransform as RectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        _tutorialDamageIconImage = iconTransform.GetComponent<Image>();
        if (_tutorialDamageIconImage == null)
            _tutorialDamageIconImage = iconTransform.gameObject.AddComponent<Image>();
        _tutorialDamageIconImage.raycastTarget = false;
        _tutorialDamageIconImage.preserveAspect = true;

        containerTransform.gameObject.SetActive(false);
    }

    private void PositionTutorialDamageOverlay(RectTransform sourceHeart)
    {
        EnsureTutorialDamageOverlay();
        if (sourceHeart == null || _tutorialDamageCanvasRect == null || _tutorialDamageContainerRect == null)
            return;

        Rect screenRect = GetScreenRect(sourceHeart);
        Vector2 screenCenter = screenRect.center;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _tutorialDamageCanvasRect,
            screenCenter,
            null,
            out Vector2 localCenter);

        _tutorialDamageContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
        _tutorialDamageContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
        _tutorialDamageContainerRect.pivot = new Vector2(0.5f, 0.5f);
        _tutorialDamageContainerRect.anchoredPosition = localCenter;
        _tutorialDamageContainerRect.sizeDelta = new Vector2(
            Mathf.Max(64f, screenRect.width),
            Mathf.Max(64f, screenRect.height));
        _tutorialDamageContainerRect.localScale = Vector3.one;
    }

    private Rect GetScreenRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Canvas sourceCanvas = rectTransform.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.worldCamera
            : null;

        Vector2 min = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[0]);
        Vector2 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void CopyHeartVisualToTutorialOverlay(Image sourceHeart)
    {
        EnsureTutorialDamageOverlay();
        if (_tutorialDamageContainerRect == null || _tutorialDamageIconImage == null || _tutorialDamageGroup == null)
            return;

        _tutorialDamageIconImage.sprite = sourceHeart != null ? sourceHeart.sprite : null;
        _tutorialDamageIconImage.color = sourceHeart != null ? sourceHeart.color : new Color(1f, 1f, 1f, 0.25f);
        _tutorialDamageIconImage.enabled = true;

        _tutorialDamageGroup.alpha = 1f;
        _tutorialDamageContainerRect.localScale = Vector3.one;
        _tutorialDamageContainerRect.gameObject.SetActive(true);
        _tutorialDamageContainerRect.SetAsLastSibling();
    }

    private void PlayTutorialOverlayRestore(Image restoredHeart)
    {
        if (_tutorialDamageContainerRect == null
            || !_tutorialDamageContainerRect.gameObject.activeSelf
            || _tutorialDamageIconImage == null)
        {
            return;
        }

        CopyHeartVisualToTutorialOverlay(restoredHeart);

        if (_tutorialDamageOverlayRoutine != null)
            StopCoroutine(_tutorialDamageOverlayRoutine);

        _tutorialDamageOverlayRoutine = StartCoroutine(TutorialOverlayRestoreRoutine());
    }

    private IEnumerator TutorialOverlayRestoreRoutine()
    {
        if (_tutorialDamageContainerRect == null || _tutorialDamageGroup == null)
            yield break;

        float duration = 0.55f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_tutorialDamageContainerRect == null || _tutorialDamageGroup == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.35f
                ? Mathf.LerpUnclamped(1f, 1.35f, t / 0.35f)
                : Mathf.LerpUnclamped(1.35f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));

            _tutorialDamageContainerRect.localScale = Vector3.one * scale;
            _tutorialDamageGroup.alpha = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
            yield return null;
        }

        if (_tutorialDamageContainerRect != null)
            _tutorialDamageContainerRect.gameObject.SetActive(false);

        _tutorialDamageOverlayRoutine = null;
    }

    private IEnumerator TutorialDamageIndicatorRoutine(TextMeshProUGUI indicator)
    {
        if (indicator == null)
            yield break;

        GameObject marker = indicator.gameObject;
        RectTransform rect = marker.transform as RectTransform;
        CanvasGroup group = marker.GetComponent<CanvasGroup>();
        if (rect == null || group == null)
            yield break;

        marker.SetActive(true);
        marker.transform.SetAsLastSibling();
        indicator.text = "-1";
        indicator.color = _tutorialDamageIndicatorColor;

        Vector2 start = new Vector2(22f, 8f);
        Vector2 end = new Vector2(22f, 56f);
        float duration = Mathf.Max(0.15f, _tutorialDamageIndicatorSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (indicator == null || rect == null || group == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = Mathf.SmoothStep(0f, 1f, t);

            rect.anchoredPosition = Vector2.LerpUnclamped(start, end, ease);
            float pop = t < 0.22f
                ? Mathf.LerpUnclamped(0.7f, 1.35f, t / 0.22f)
                : Mathf.LerpUnclamped(1.35f, 1f, Mathf.Clamp01((t - 0.22f) / 0.28f));
            rect.localScale = Vector3.one * pop;
            group.alpha = t < 0.58f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.58f) / 0.42f);

            yield return null;
        }

        if (marker != null)
            marker.SetActive(false);

        _tutorialDamageIndicatorRoutine = null;
    }

    private IEnumerator HeartLossAnimation(int index, Image heart)
    {
        if (heart == null) yield break;

        float duration = Mathf.Max(0.05f, _heartShakeDuration);
        float elapsed = 0f;
        Vector3 baseScale = index < _heartBaseScales.Length && index < _heartScaleCached.Length && _heartScaleCached[index]
            ? _heartBaseScales[index]
            : heart.transform.localScale;
        Vector3 punchScale = baseScale * Mathf.Max(1f, _heartPunchScale);
        Vector3 dipScale = baseScale * 0.95f;

        while (elapsed < duration)
        {
            if (heart == null) yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (t < 0.45f)
            {
                float phase = t / 0.45f;
                heart.transform.localScale = Vector3.LerpUnclamped(baseScale, punchScale, phase);
            }
            else if (t < 0.75f)
            {
                float phase = (t - 0.45f) / 0.30f;
                heart.transform.localScale = Vector3.LerpUnclamped(punchScale, dipScale, phase);
            }
            else
            {
                float phase = (t - 0.75f) / 0.25f;
                heart.transform.localScale = Vector3.LerpUnclamped(dipScale, baseScale, phase);
            }

            yield return null;
        }

        if (heart != null)
            heart.transform.localScale = baseScale;

        if (index < _heartAnimRoutines.Length)
            _heartAnimRoutines[index] = null;
    }

    private void EnsureRuntimeBuffers()
    {
        int heartCount = _heartIcons != null ? _heartIcons.Length : 0;

        if (_heartAnimRoutines == null || _heartAnimRoutines.Length != heartCount)
            _heartAnimRoutines = new Coroutine[heartCount];

        if (_heartBaseScales == null || _heartBaseScales.Length != heartCount)
            _heartBaseScales = new Vector3[heartCount];

        if (_heartScaleCached == null || _heartScaleCached.Length != heartCount)
            _heartScaleCached = new bool[heartCount];
    }
}
