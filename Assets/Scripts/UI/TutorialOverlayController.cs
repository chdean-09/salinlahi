using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialOverlayController : MonoBehaviour
{
    private const int RequiredStepCount = 3;

    // Serialized defaults keep this player-facing copy ready for future localization/data wiring.
    [SerializeField] private string[] _stepBodyText =
    {
        "Enemies carry Baybayin characters.",
        "Draw the shown character anywhere on the screen.",
        "Protect the Shrine and clear each wave."
    };
    [SerializeField] private string _nextButtonLabel = "Next";
    [SerializeField] private string _doneButtonLabel = "Done";

    [Header("UI References")]
    [SerializeField] private GameObject _overlayPanel;
    [SerializeField] private TextMeshProUGUI _bodyText;
    [SerializeField] private TextMeshProUGUI _buttonText;
    [SerializeField] private Button _dismissButton;

    [Header("Guided Draw References")]
    [SerializeField] private CanvasGroup _visualOverlayGroup;
    [SerializeField] private GameObject _guidedDrawPanel;
    [SerializeField] private TextMeshProUGUI _guidedPromptText;
    [SerializeField] private Image _guidedGlyphImage;
    [SerializeField] private RectTransform _enemyHighlight;
    [SerializeField] private Camera _worldCamera;
    [SerializeField] private Canvas _targetCanvas;

    [Header("Toast References")]
    [SerializeField] private GameObject _toastPanel;
    [SerializeField] private TextMeshProUGUI _toastText;
    [SerializeField] private float _toastDurationSeconds = 3f;

    private int _stepIndex;
    private bool _isShowing;
    private Enemy _highlightedEnemy;
    private Coroutine _toastRoutine;
    private readonly Queue<string> _toastQueue = new();

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

        HideOverlay();
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

    public void ShowGuidedDraw(Enemy enemy)
    {
        if (enemy == null)
            return;

        _highlightedEnemy = enemy;

        if (_visualOverlayGroup != null)
        {
            _visualOverlayGroup.alpha = 1f;
            _visualOverlayGroup.interactable = false;
            _visualOverlayGroup.blocksRaycasts = false;
        }

        if (_guidedDrawPanel != null)
            _guidedDrawPanel.SetActive(true);

        BaybayinCharacterSO character = enemy.Character;
        if (_guidedPromptText != null)
            _guidedPromptText.text = "Trace this Baybayin character to stop the enemy.";

        if (_guidedGlyphImage != null)
        {
            _guidedGlyphImage.sprite = character != null ? character.displaySprite : null;
            _guidedGlyphImage.enabled = _guidedGlyphImage.sprite != null;
            _guidedGlyphImage.raycastTarget = false;
        }

        if (_enemyHighlight != null)
            _enemyHighlight.gameObject.SetActive(true);
    }

    public void HideGuidedDraw()
    {
        _highlightedEnemy = null;

        if (_guidedDrawPanel != null)
            _guidedDrawPanel.SetActive(false);

        if (_enemyHighlight != null)
            _enemyHighlight.gameObject.SetActive(false);

        if (_visualOverlayGroup != null)
        {
            _visualOverlayGroup.alpha = 0f;
            _visualOverlayGroup.interactable = false;
            _visualOverlayGroup.blocksRaycasts = false;
        }
    }

    public void ShowToast(string message)
    {
        if (_toastPanel == null || _toastText == null)
            return;

        _toastQueue.Enqueue(message);

        if (_toastRoutine == null)
            _toastRoutine = StartCoroutine(ShowToastRoutine());
    }

    private void LateUpdate()
    {
        if (_highlightedEnemy == null || _enemyHighlight == null)
            return;

        UpdateEnemyHighlight(_highlightedEnemy.transform.position);
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

    private void UpdateEnemyHighlight(Vector3 worldPosition)
    {
        if (_targetCanvas == null)
            return;

        Camera cameraForWorld = _worldCamera != null ? _worldCamera : Camera.main;
        Vector3 screenPosition = cameraForWorld != null
            ? cameraForWorld.WorldToScreenPoint(worldPosition)
            : worldPosition;

        RectTransform canvasRect = _targetCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            _targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _targetCanvas.worldCamera,
            out Vector2 localPoint);

        _enemyHighlight.anchoredPosition = localPoint;
    }

    private IEnumerator ShowToastRoutine()
    {
        while (_toastQueue.Count > 0)
        {
            _toastText.text = _toastQueue.Dequeue();
            _toastPanel.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _toastDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        _toastPanel.SetActive(false);
        _toastRoutine = null;
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
        HideGuidedDraw();

        if (_toastRoutine != null)
        {
            StopCoroutine(_toastRoutine);
            _toastRoutine = null;
        }

        _toastQueue.Clear();

        if (_toastPanel != null)
            _toastPanel.SetActive(false);

        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
    }
}
