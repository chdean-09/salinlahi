using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Level1TutorialGuideUI : MonoBehaviour
{
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

    public void Initialize(System.Action skipRequested)
    {
        _skipRequested = skipRequested;
        if (_skipButton != null)
            _skipButton.onClick.AddListener(HandleSkipClicked);
    }

    private void OnDestroy()
    {
        if (_skipButton != null)
            _skipButton.onClick.RemoveListener(HandleSkipClicked);
    }

    public void ShowPrompt(Level1TutorialStepSO step, bool canSkip)
    {
        if (_root != null)
            _root.SetActive(true);

        if (_promptText != null)
            _promptText.text = step != null ? step.promptText : string.Empty;

        if (_feedbackText != null)
            _feedbackText.text = string.Empty;

        if (_skipButton != null)
            _skipButton.gameObject.SetActive(canSkip);

        ShowGuideSprite(step);

        // Set up guide visuals if available
        if (_guidePathRenderer != null && step != null && step.templatePoints != null && step.templatePoints.Length > 1)
        {
            _guidePathRenderer.positionCount = step.templatePoints.Length;
            for (int i = 0; i < step.templatePoints.Length; i++)
                _guidePathRenderer.SetPosition(i, step.templatePoints[i]);
            _guidePathRenderer.gameObject.SetActive(true);
        }

        if (_startDot != null)
        {
            if (step != null && step.templatePoints != null && step.templatePoints.Length > 0)
            {
                _startDot.position = step.templatePoints[0];
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
                _directionArrow.position = first + dir * 0.5f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                _directionArrow.rotation = Quaternion.Euler(0, 0, angle);
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
        if (_feedbackText != null)
            _feedbackText.text = message ?? string.Empty;
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);

        if (_guidePathRenderer != null)
            _guidePathRenderer.gameObject.SetActive(false);

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
        rect.sizeDelta = new Vector2(180f, 180f);

        _guideSpriteImage = imageObject.GetComponent<Image>();
        _guideSpriteImage.raycastTarget = false;
        _guideSpriteImage.color = new Color(1f, 1f, 1f, 0.9f);
        imageObject.SetActive(false);
        return _guideSpriteImage;
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
        }
    }

    private System.Collections.IEnumerator PulseCoroutine()
    {
        if (_startDot == null)
            yield break;

        Vector3 baseScale = _startDot.localScale;
        float duration = 0.6f;
        while (true)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
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

        _guidePathRenderer.positionCount = totalPoints;
    }

    private void HandleSkipClicked()
    {
        _skipRequested?.Invoke();
    }
}
