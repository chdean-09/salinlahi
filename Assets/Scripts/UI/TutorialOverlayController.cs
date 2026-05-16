using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialOverlayController : MonoBehaviour
{
    private static readonly string[] TutorialSteps =
    {
        "An enemy approaches — it shows a Baybayin character",
        "Draw the character on your screen with your finger",
        "The enemy is defeated!"
    };

    [Header("UI References")]
    [SerializeField] private GameObject _overlayPanel;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private TextMeshProUGUI _buttonText;
    [SerializeField] private Button _dismissButton;

    private int _stepIndex;
    private bool _isShowing;

    public bool IsShowing => _isShowing;
    public int CurrentStepIndex => _stepIndex;
    public bool IsConfigured => CanShowOverlay();

    private void Awake()
    {
        HideOverlay();
    }

    private void OnEnable()
    {
        if (_dismissButton != null)
            _dismissButton.onClick.AddListener(AdvanceStep);
    }

    private void OnDisable()
    {
        if (_dismissButton != null)
            _dismissButton.onClick.RemoveListener(AdvanceStep);
    }

    public IEnumerator PlayIfNeeded(LevelConfigSO levelConfig)
    {
        if (!LevelTutorialProgress.ShouldShowForLevel(levelConfig))
            yield break;

        if (!CanShowOverlay())
        {
            DebugLogger.LogError("TutorialOverlayController: Missing UI references. Tutorial cannot be shown.");
            yield break;
        }

        ShowFirstStep();
        yield return new WaitUntil(() => !_isShowing);
    }

    public void ShowFirstStep()
    {
        _stepIndex = 0;
        _isShowing = true;
        LevelTutorialProgress.MarkLevel1TutorialSeen();

        if (_overlayPanel != null)
            _overlayPanel.SetActive(true);

        RenderCurrentStep();
    }

    public void AdvanceStep()
    {
        if (!_isShowing)
            return;

        _stepIndex++;
        if (_stepIndex >= TutorialSteps.Length)
        {
            CompleteTutorial();
            return;
        }

        RenderCurrentStep();
    }

    private bool CanShowOverlay()
    {
        return _overlayPanel != null
            && _bodyText != null
            && _dismissButton != null;
    }

    private void RenderCurrentStep()
    {
        if (_bodyText != null)
            _bodyText.text = TutorialSteps[_stepIndex];

        if (_buttonText != null)
            _buttonText.text = _stepIndex == TutorialSteps.Length - 1 ? "Done" : "Next";
    }

    private void CompleteTutorial()
    {
        HideOverlay();
    }

    private void HideOverlay()
    {
        _isShowing = false;
        _stepIndex = 0;

        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
    }
}
