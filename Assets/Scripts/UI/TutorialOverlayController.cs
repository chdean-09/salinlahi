using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialOverlayController : MonoBehaviour
{
    private const int RequiredStepCount = 3;

    // Serialized defaults keep this player-facing copy ready for future localization/data wiring.
    [SerializeField] private string[] _stepBodyText =
    {
        "An enemy approaches — it shows a Baybayin character",
        "Draw the character on your screen with your finger",
        "The enemy is defeated!"
    };
    [SerializeField] private string _nextButtonLabel = "Next";
    [SerializeField] private string _doneButtonLabel = "Done";

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

        bool enteredTutorialPause = TryEnterTutorialPause();

        try
        {
            ShowFirstStep();
            yield return new WaitUntil(() => !_isShowing);
        }
        finally
        {
            if (enteredTutorialPause && GameManager.Instance != null)
                GameManager.Instance.ExitDialoguePause();
        }
    }

    private void ShowFirstStep()
    {
        _stepIndex = 0;
        _isShowing = true;
        // Persist on first show so partial views still count as "seen" after backgrounding or force-quit.
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
        if (_stepIndex >= _stepBodyText.Length)
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
            && _buttonText != null
            && _stepBodyText != null
            && _stepBodyText.Length == RequiredStepCount
            && _dismissButton != null;
    }

    private void RenderCurrentStep()
    {
        if (_bodyText != null)
            _bodyText.text = _stepBodyText[_stepIndex];

        if (_buttonText != null)
            _buttonText.text = _stepIndex == _stepBodyText.Length - 1 ? _doneButtonLabel : _nextButtonLabel;
    }

    private bool TryEnterTutorialPause()
    {
        if (GameManager.Instance == null)
            return false;

        GameState currentState = GameManager.Instance.CurrentState;
        if (currentState != GameState.Playing && currentState != GameState.LevelComplete)
            return false;

        GameManager.Instance.EnterDialoguePause();
        return GameManager.Instance.CurrentState == GameState.Paused;
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
