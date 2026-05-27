using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CutscenePlayer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _panelImage;
    [SerializeField] private RectTransform _imageRectTransform;
    [SerializeField] private Image _bottomGradientOverlay;
    [SerializeField] private Color _bottomGradientColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField, Range(0.05f, 0.5f)] private float _bottomGradientHeightPercent = 0.30f;
    [SerializeField] private Image _exitTransitionImage;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private TMP_FontAsset _bodyFont;
    [SerializeField] private float _bodyFontSize = 92f;
    [SerializeField] private Button _tapCatcher;
    [SerializeField] private Button _skipButton;
    [SerializeField] private GameObject _skipButtonRoot;

    [Header("Slide Transition")]
    [SerializeField] private float _slideDistance = 400f;

    [Header("Exit Transition")]
    [SerializeField] private float _exitFadeToBlackDuration = 0.45f;
    [SerializeField] private float _exitBlackHoldDuration = 0.10f;
    [SerializeField] private float _exitFadeFromBlackDuration = 0.35f;

    public bool IsPlaying { get; private set; }

    private CutsceneSO _currentCutscene;
    private int _panelIndex;
    private bool _isTypewriting;
    private bool _skipRequested;
    private bool _waitingForTap;
    private bool _playExitTransition;
    private bool _isCompleting;
    private Coroutine _playRoutine;

    private void Awake()
    {
        transform.localScale = Vector3.one;
        ConfigureBottomGradientOverlay();
        ConfigureExitTransitionImage();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.AddListener(OnTap);
        if (_skipButton != null)
            _skipButton.onClick.AddListener(SkipCutscene);

        if (_bodyFont != null && _bodyText != null)
            _bodyText.font = _bodyFont;

        if (_bodyText != null)
            _bodyText.fontSize = _bodyFontSize;

        if (_bodyText != null && _bodyText.fontMaterial != null)
        {
            _bodyText.fontMaterial.EnableKeyword("OUTLINE_ON");
            _bodyText.fontMaterial.SetFloat(Shader.PropertyToID("_OutlineWidth"), 0.35f);
            _bodyText.fontMaterial.SetColor(Shader.PropertyToID("_OutlineColor"), Color.black);
            _bodyText.fontMaterial.SetFloat(Shader.PropertyToID("_FaceDilate"), 0.15f);
        }
    }

    private void OnDisable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.RemoveListener(OnTap);
        if (_skipButton != null)
            _skipButton.onClick.RemoveListener(SkipCutscene);
    }

    private void ConfigureBottomGradientOverlay()
    {
        if (_bottomGradientOverlay == null)
        {
            Transform existing = transform.Find("BottomGradientOverlay");
            if (existing != null)
                _bottomGradientOverlay = existing.GetComponent<Image>();
        }

        if (_bottomGradientOverlay == null)
            _bottomGradientOverlay = CreateBottomGradientOverlay();

        if (_bottomGradientOverlay == null)
            return;

        RectTransform rect = _bottomGradientOverlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, Mathf.Clamp01(_bottomGradientHeightPercent));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _bottomGradientOverlay.color = _bottomGradientColor;
        _bottomGradientOverlay.raycastTarget = false;

        EdgeGradient gradient = _bottomGradientOverlay.GetComponent<EdgeGradient>();
        if (gradient == null)
            gradient = _bottomGradientOverlay.gameObject.AddComponent<EdgeGradient>();
        gradient.EdgeType = EdgeGradient.Edge.Bottom;

        EnsureCutsceneSiblingOrder();
    }

    private Image CreateBottomGradientOverlay()
    {
        GameObject go = new("BottomGradientOverlay");
        go.transform.SetParent(transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, Mathf.Clamp01(_bottomGradientHeightPercent));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go.AddComponent<Image>();
    }

    private void ConfigureExitTransitionImage()
    {
        if (_exitTransitionImage == null)
        {
            Transform existing = transform.Find("ExitTransitionImage");
            if (existing != null)
                _exitTransitionImage = existing.GetComponent<Image>();
        }

        if (_exitTransitionImage == null)
            _exitTransitionImage = CreateExitTransitionImage();

        if (_exitTransitionImage == null)
            return;

        RectTransform rect = _exitTransitionImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Color color = Color.black;
        color.a = 0f;
        _exitTransitionImage.color = color;
        _exitTransitionImage.raycastTarget = false;
        _exitTransitionImage.gameObject.SetActive(false);

        EnsureCutsceneSiblingOrder();
    }

    private Image CreateExitTransitionImage()
    {
        GameObject go = new("ExitTransitionImage");
        go.transform.SetParent(transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go.AddComponent<Image>();
    }

    private void EnsureCutsceneSiblingOrder()
    {
        if (_panelImage != null)
            _panelImage.transform.SetSiblingIndex(0);

        if (_bottomGradientOverlay != null)
            _bottomGradientOverlay.transform.SetSiblingIndex(1);

        if (_bodyText != null)
            _bodyText.transform.SetAsLastSibling();
        if (_tapCatcher != null)
            _tapCatcher.transform.SetAsLastSibling();
        if (_skipButtonRoot != null)
            _skipButtonRoot.transform.SetAsLastSibling();
        if (_exitTransitionImage != null)
            _exitTransitionImage.transform.SetAsLastSibling();
    }

    public void Play(CutsceneSO cutscene, bool playExitTransition = false)
    {
        if (IsPlaying)
            return;
        if (cutscene == null || cutscene.panels == null || cutscene.panels.Length == 0)
            return;

        _currentCutscene = cutscene;
        _panelIndex = 0;
        _playExitTransition = playExitTransition;
        _isCompleting = false;
        IsPlaying = true;
        SetCutsceneContentVisible(true);
        SetExitTransitionAlpha(0f);
        if (_exitTransitionImage != null)
            _exitTransitionImage.gameObject.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.EnterDialoguePause();

        EventBus.RaiseCutsceneStarted();

        if (_skipButtonRoot != null)
            _skipButtonRoot.SetActive(true);

        if (_canvasGroup != null)
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        _playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        while (_panelIndex < _currentCutscene.panels.Length)
        {
            CutscenePanel panel = _currentCutscene.panels[_panelIndex];
            _waitingForTap = false;
            _skipRequested = false;

            TransitionType transition = panel.transitionIn;
            if (transition == TransitionType.None && _panelIndex == 0)
                transition = TransitionType.Fade;

            float duration = panel.transitionDuration > 0f
                ? panel.transitionDuration
                : _currentCutscene.defaultTransitionDuration;

            float speed = panel.typewriterSpeed > 0f
                ? panel.typewriterSpeed
                : _currentCutscene.defaultTypewriterSpeed;

            yield return TransitionIn(panel.image, transition, duration);

            yield return TypewriterRoutine(panel.text ?? "", speed);
            _isTypewriting = false;

            _waitingForTap = true;
            yield return new WaitUntil(() => _currentCutscene == null || !_waitingForTap);

            _panelIndex++;
        }

        yield return CompleteCutsceneRoutine();
    }

    private IEnumerator TransitionIn(Sprite sprite, TransitionType type, float duration)
    {
        if (type == TransitionType.None)
        {
            if (_panelImage != null)
                _panelImage.sprite = sprite;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
            yield break;
        }

        if (type == TransitionType.Fade)
        {
            if (_panelImage != null)
                _panelImage.sprite = sprite;
            yield return FadeCanvasGroup(0f, 1f, duration);
        }
        else
        {
            if (_panelImage != null)
                _panelImage.sprite = sprite;
            yield return SlideIn(type, duration);
        }
    }

    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        if (_canvasGroup == null) yield break;

        _canvasGroup.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        _canvasGroup.alpha = to;
    }

    private IEnumerator SlideIn(TransitionType direction, float duration)
    {
        if (_imageRectTransform == null)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
            yield break;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        Vector2 startOffset = direction switch
        {
            TransitionType.SlideLeft  => new Vector2(_slideDistance, 0f),
            TransitionType.SlideRight => new Vector2(-_slideDistance, 0f),
            TransitionType.SlideUp    => new Vector2(0f, _slideDistance),
            TransitionType.SlideDown  => new Vector2(0f, -_slideDistance),
            _                         => Vector2.zero
        };

        _imageRectTransform.anchoredPosition = startOffset;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _imageRectTransform.anchoredPosition = Vector2.Lerp(startOffset, Vector2.zero, t / duration);
            yield return null;
        }
        _imageRectTransform.anchoredPosition = Vector2.zero;
    }

    private IEnumerator TypewriterRoutine(string fullText, float charsPerSecond)
    {
        _isTypewriting = true;

        if (_bodyText == null)
        {
            _isTypewriting = false;
            yield break;
        }

        _bodyText.text = "";

        if (string.IsNullOrEmpty(fullText))
        {
            _isTypewriting = false;
            yield break;
        }

        float delay = 1f / Mathf.Max(charsPerSecond, 0.1f);

        for (int i = 0; i < fullText.Length; i++)
        {
            if (_skipRequested)
            {
                _bodyText.text = fullText;
                break;
            }
            _bodyText.text = fullText.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(delay);
        }

        _isTypewriting = false;
    }

    private void OnTap()
    {
        if (_currentCutscene == null) return;

        if (_isTypewriting)
        {
            SkipTypewriter();
            return;
        }

        if (_waitingForTap)
            _waitingForTap = false;
    }

    private void SkipTypewriter()
    {
        _skipRequested = true;
        _isTypewriting = false;

        if (_bodyText != null && _currentCutscene != null
            && _panelIndex < _currentCutscene.panels.Length)
        {
            _bodyText.text = _currentCutscene.panels[_panelIndex].text ?? "";
        }
    }

    public void SkipCutscene()
    {
        if (_currentCutscene == null) return;
        CompleteCutscene();
    }

    private IEnumerator CompleteCutsceneRoutine()
    {
        if (_isCompleting)
            yield break;

        _isCompleting = true;
        bool playExitTransition = _playExitTransition;

        if (playExitTransition)
        {
            yield return FadeExitTransition(0f, 1f, _exitFadeToBlackDuration);
            yield return new WaitForSecondsRealtime(_exitBlackHoldDuration);
        }

        CompleteCutscene(keepCanvasVisibleForExitTransition: playExitTransition, stopRoutine: false);

        if (playExitTransition)
        {
            yield return FadeExitTransition(1f, 0f, _exitFadeFromBlackDuration);
            if (_exitTransitionImage != null)
                _exitTransitionImage.gameObject.SetActive(false);
            HideCutsceneCanvas();
        }

        _isCompleting = false;
        _playRoutine = null;
    }

    private IEnumerator FadeExitTransition(float from, float to, float duration)
    {
        if (_exitTransitionImage == null)
            yield break;

        _exitTransitionImage.gameObject.SetActive(true);
        _exitTransitionImage.transform.SetAsLastSibling();

        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        SetExitTransitionAlpha(from);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetExitTransitionAlpha(Mathf.Lerp(from, to, elapsed / safeDuration));
            yield return null;
        }

        SetExitTransitionAlpha(to);
    }

    private void SetExitTransitionAlpha(float alpha)
    {
        if (_exitTransitionImage == null)
            return;

        Color color = Color.black;
        color.a = Mathf.Clamp01(alpha);
        _exitTransitionImage.color = color;
    }

    private void CompleteCutscene(bool keepCanvasVisibleForExitTransition = false, bool stopRoutine = true)
    {
        if (stopRoutine && _playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        else if (!stopRoutine)
        {
            _playRoutine = null;
        }

        _isTypewriting = false;
        _waitingForTap = false;
        _currentCutscene = null;
        _panelIndex = 0;
        _playExitTransition = false;
        if (!keepCanvasVisibleForExitTransition)
            _isCompleting = false;
        IsPlaying = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        if (keepCanvasVisibleForExitTransition)
            SetCutsceneContentVisible(false);
        else
            HideCutsceneCanvas();

        if (GameManager.Instance != null)
            GameManager.Instance.ExitDialoguePause();

        EventBus.RaiseCutsceneComplete();
    }

    private void HideCutsceneCanvas()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        SetCutsceneContentVisible(false);
    }

    private void SetCutsceneContentVisible(bool visible)
    {
        if (_panelImage != null)
            _panelImage.gameObject.SetActive(visible);
        if (_bottomGradientOverlay != null)
            _bottomGradientOverlay.gameObject.SetActive(visible);
        if (_bodyText != null)
            _bodyText.gameObject.SetActive(visible);
        if (_tapCatcher != null)
            _tapCatcher.gameObject.SetActive(visible);
        if (_skipButtonRoot != null)
            _skipButtonRoot.SetActive(visible);
    }
}
