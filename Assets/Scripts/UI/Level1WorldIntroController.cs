using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Level1WorldIntroController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup _introGroup;
    [SerializeField] private TextMeshProUGUI _objectiveText;
    [SerializeField] private Transform _protagonist;
    [SerializeField] private Transform _shrineFocus;
    [SerializeField] private Animator _optionalAnimator;

    [Header("Objective")]
    [SerializeField] private string _objectiveLine = "Defend the Shrine.";
    [SerializeField] private string _threatCueLine = "Enemies incoming.";
    [SerializeField] private bool _hidePlaceholderLabels = true;

    [Header("Timing")]
    [SerializeField] private float _fadeInSeconds = 0.35f;
    [SerializeField] private float _protagonistWalkSeconds = 1.5f;
    [SerializeField] private float _shrineHoldSeconds = 1.25f;
    [SerializeField] private float _objectiveHoldSeconds = 1.75f;
    [SerializeField] private float _threatCueHoldSeconds = 0.75f;
    [SerializeField] private float _fadeOutSeconds = 0.35f;

    [Header("Protagonist Motion")]
    [SerializeField] private bool _animateProtagonistPosition = true;
    [SerializeField] private Vector2 _protagonistStartAnchoredOffset = new(0f, -240f);
    [SerializeField] private Vector3 _protagonistStartOffset = new(0f, -1.5f, 0f);

    [Header("Animator Triggers")]
    [SerializeField] private string _introStartTrigger = "IntroStart";
    [SerializeField] private string _introEndTrigger = "IntroEnd";

    public bool IsConfigured => _introGroup != null
        && _objectiveText != null
        && _protagonist != null
        && _shrineFocus != null;

    private void Awake()
    {
        if (_hidePlaceholderLabels)
            HidePlaceholderLabels();

        HideImmediate();
    }

    public IEnumerator PlayIfNeeded(LevelConfigSO levelConfig)
    {
        if (!ShouldPlay(levelConfig))
        {
            DebugLogger.Log("Level1WorldIntroController: Level 1 world intro already seen or not applicable.");
            yield break;
        }

        if (!IsConfigured)
        {
            DebugLogger.LogWarning("Level1WorldIntroController: Missing intro UI references. Skipping world intro.");
            yield break;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        DebugLogger.Log("Level1WorldIntroController: Playing Level 1 world intro.");

        yield return PlayIntroRoutine();

        LevelTutorialProgress.MarkLevel1WorldIntroSeen();
        LevelTutorialProgress.MarkLevel1TutorialSeen();
        DebugLogger.Log("Level1WorldIntroController: Level 1 world intro complete.");
    }

    private static bool ShouldPlay(LevelConfigSO levelConfig)
    {
        if (levelConfig == null || levelConfig.levelNumber != LevelTutorialProgress.TutorialLevelNumber)
            return false;

        return !LevelTutorialProgress.HasSeenLevel1WorldIntro();
    }

    private IEnumerator PlayIntroRoutine()
    {
        RectTransform protagonistRect = _protagonist as RectTransform;
        Vector2 protagonistEndAnchoredPosition = protagonistRect != null ? protagonistRect.anchoredPosition : Vector2.zero;
        Vector2 protagonistStartAnchoredPosition = protagonistEndAnchoredPosition + _protagonistStartAnchoredOffset;
        Vector3 protagonistEndPosition = _protagonist != null ? _protagonist.position : Vector3.zero;
        Vector3 protagonistStartPosition = protagonistEndPosition + _protagonistStartOffset;

        if (protagonistRect != null && _animateProtagonistPosition)
            protagonistRect.anchoredPosition = protagonistStartAnchoredPosition;
        else if (_protagonist != null && _animateProtagonistPosition)
            _protagonist.position = protagonistStartPosition;

        _objectiveText.text = _objectiveLine;
        _introGroup.alpha = 0f;
        _introGroup.interactable = false;
        _introGroup.blocksRaycasts = true;
        _introGroup.gameObject.SetActive(true);

        TrySetTrigger(_introStartTrigger);

        yield return FadeTo(1f, _fadeInSeconds);
        yield return MoveProtagonist(
            protagonistRect,
            protagonistStartAnchoredPosition,
            protagonistEndAnchoredPosition,
            protagonistStartPosition,
            protagonistEndPosition);

        if (_shrineFocus != null)
            yield return new WaitForSeconds(_shrineHoldSeconds);

        yield return new WaitForSeconds(_objectiveHoldSeconds);

        if (!string.IsNullOrWhiteSpace(_threatCueLine) && _objectiveText != null)
        {
            _objectiveText.text = _threatCueLine;
            yield return new WaitForSeconds(_threatCueHoldSeconds);
        }

        TrySetTrigger(_introEndTrigger);
        yield return FadeTo(0f, _fadeOutSeconds);

        HideImmediate();
    }

    private IEnumerator MoveProtagonist(
        RectTransform protagonistRect,
        Vector2 startAnchoredPosition,
        Vector2 endAnchoredPosition,
        Vector3 startPosition,
        Vector3 endPosition)
    {
        if ((protagonistRect == null && _protagonist == null) || !_animateProtagonistPosition || _protagonistWalkSeconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < _protagonistWalkSeconds)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / _protagonistWalkSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            if (protagonistRect != null)
                protagonistRect.anchoredPosition = Vector2.LerpUnclamped(startAnchoredPosition, endAnchoredPosition, eased);
            else
                _protagonist.position = Vector3.LerpUnclamped(startPosition, endPosition, eased);

            yield return null;
        }

        if (protagonistRect != null)
            protagonistRect.anchoredPosition = endAnchoredPosition;
        else
            _protagonist.position = endPosition;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (_introGroup == null)
            yield break;

        if (duration <= 0f)
        {
            _introGroup.alpha = targetAlpha;
            yield break;
        }

        float startAlpha = _introGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            _introGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        _introGroup.alpha = targetAlpha;
    }

    private void TrySetTrigger(string triggerName)
    {
        if (_optionalAnimator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        _optionalAnimator.SetTrigger(triggerName);
    }

    private void HideImmediate()
    {
        if (_introGroup == null)
            return;

        _introGroup.alpha = 0f;
        _introGroup.interactable = false;
        _introGroup.blocksRaycasts = false;
        _introGroup.gameObject.SetActive(false);
    }

    private static void HidePlaceholderLabels()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] roots = activeScene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            TextMeshProUGUI[] labels = roots[i].GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int j = 0; j < labels.Length; j++)
            {
                TextMeshProUGUI label = labels[j];
                if (label == null || !IsPlaceholderLabel(label.text))
                    continue;

                label.gameObject.SetActive(false);
            }
        }
    }

    private static bool IsPlaceholderLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().ToUpperInvariant();
        return normalized == "PROTAGONIST"
            || normalized == "SHRINE"
            || normalized == "ENEMY CROSSING LINE";
    }
}
