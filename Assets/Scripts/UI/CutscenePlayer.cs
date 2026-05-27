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
    private bool _waitingForTap;
    private Coroutine _typewriterRoutine;
    private Coroutine _playRoutine;

    private void Awake()
    {
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

            _typewriterRoutine = StartCoroutine(TypewriterRoutine(panel.text ?? "", speed));
            yield return _typewriterRoutine;
            _typewriterRoutine = null;
            _isTypewriting = false;

            Debug.Log($"[CutscenePlayer] Panel {_panelIndex} done. Waiting for tap...");
            _waitingForTap = true;
            yield return new WaitUntil(() => _currentCutscene == null || !_waitingForTap);
            Debug.Log($"[CutscenePlayer] Tap received. Advancing from panel {_panelIndex}.");

            _panelIndex++;
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
        Debug.Log($"[CutscenePlayer] OnTap: isTypewriting={_isTypewriting}, waitingForTap={_waitingForTap}, panelIndex={_panelIndex}, totalPanels={_currentCutscene?.panels?.Length}");

        if (_currentCutscene == null) return;

        if (_isTypewriting)
        {
            Debug.Log("[CutscenePlayer] OnTap -> SkipTypewriter");
            SkipTypewriter();
            return;
        }

        if (_waitingForTap)
        {
            Debug.Log("[CutscenePlayer] OnTap -> Advance Panel");
            _waitingForTap = false;
        }
        else
        {
            Debug.Log("[CutscenePlayer] OnTap -> Ignored (not waiting)");
        }
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
        _waitingForTap = false;
        _currentCutscene = null;
        _panelIndex = 0;
        IsPlaying = false;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        if (_skipButtonRoot != null)
            _skipButtonRoot.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ExitDialoguePause();

        EventBus.RaiseCutsceneComplete();
    }
}
