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
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private TMP_FontAsset _bodyFont;
    [SerializeField] private Button _tapCatcher;
    [SerializeField] private Button _skipButton;
    [SerializeField] private GameObject _skipButtonRoot;

    [Header("Slide Transition")]
    [SerializeField] private float _slideDistance = 400f;

    public bool IsPlaying { get; private set; }

    private CutsceneSO _currentCutscene;
    private int _panelIndex;
    private bool _isTypewriting;
    private Coroutine _typewriterRoutine;
    private Coroutine _playRoutine;

    private void Awake()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_bodyFont != null && _bodyText != null)
            _bodyText.font = _bodyFont;
    }

    private void OnEnable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.AddListener(OnTap);
        if (_skipButton != null)
            _skipButton.onClick.AddListener(SkipCutscene);
    }

    private void OnDisable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.RemoveListener(OnTap);
        if (_skipButton != null)
            _skipButton.onClick.RemoveListener(SkipCutscene);
    }

    public void Play(CutsceneSO cutscene)
    {
        if (IsPlaying)
            return;
        if (cutscene == null || cutscene.panels == null || cutscene.panels.Length == 0)
            return;

        _currentCutscene = cutscene;
        _panelIndex = 0;
        IsPlaying = true;

        if (GameManager.Instance != null)
            GameManager.Instance.EnterDialoguePause();

        EventBus.RaiseCutsceneStarted();

        if (_skipButtonRoot != null)
            _skipButtonRoot.SetActive(true);

        _playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        while (_panelIndex < _currentCutscene.panels.Length)
        {
            CutscenePanel panel = _currentCutscene.panels[_panelIndex];
            int currentIndex = _panelIndex;

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

            yield return new WaitUntil(() => _currentCutscene == null || _panelIndex != currentIndex);
        }

        EndCutscene();
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

        _panelIndex++;
    }

    private void SkipTypewriter()
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

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
        EndCutscene();
    }

    private void EndCutscene()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        _isTypewriting = false;
        _currentCutscene = null;
        _panelIndex = 0;
        IsPlaying = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_skipButtonRoot != null)
            _skipButtonRoot.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ExitDialoguePause();

        EventBus.RaiseCutsceneComplete();
    }
}
