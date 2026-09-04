#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private SettingsPanel _settingsPanel;

    [Header("Level Lifecycle (SALIN-141)")]
    [Tooltip("Optional. Restarts the current level after confirmation. "
        + "Leave unwired until the button is authored in the Pause canvas.")]
    [SerializeField] private Button _restartButton;
    [Tooltip("Optional. Confirmation overlay root. When any of the four confirmation "
        + "references is unwired, PauseMenuUI builds a working overlay at runtime.")]
    [SerializeField] private GameObject _confirmationPanel;
    [SerializeField] private Button _confirmationConfirmButton;
    [SerializeField] private Button _confirmationCancelButton;
    [SerializeField] private TMP_Text _confirmationPromptLabel;

    private enum PendingAction { None, Restart, Leave }

    private const string RestartPrompt =
        "Restart this level?\nYour progress in this attempt will be lost.";
    private const string LeavePrompt =
        "Leave this level?\nYour progress in this attempt will not be saved.";

    private PendingAction _pendingAction = PendingAction.None;

    // Latched the moment a destination is confirmed. A second tap on Confirm — or a
    // Restart tap landing while the Leave load is already starting — must never issue
    // a second scene load or a second attempt abort.
    private bool _transitionRequested;

    private GameObject _runtimeConfirmationRoot;
    private bool _confirmationListenersBound;

    private bool HasConfirmationReferences =>
        _confirmationPanel != null
        && _confirmationConfirmButton != null
        && _confirmationCancelButton != null;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
        if (_confirmationPanel != null) _confirmationPanel.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnGamePaused += Show;
        EventBus.OnGameResumed += Hide;
        EventBus.OnLevelAttemptAborted += HandleLevelAttemptAborted;

        if (_resumeButton != null)
            _resumeButton.onClick.AddListener(OnResumePressed);
        if (_quitButton != null)
            _quitButton.onClick.AddListener(OnQuitPressed);
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OnSettingsPressed);
        if (_restartButton != null)
            _restartButton.onClick.AddListener(OnRestartPressed);

        BindConfirmationListeners();
    }

    private void OnDisable()
    {
        EventBus.OnGamePaused -= Show;
        EventBus.OnGameResumed -= Hide;
        EventBus.OnLevelAttemptAborted -= HandleLevelAttemptAborted;

        if (_resumeButton != null)
            _resumeButton.onClick.RemoveListener(OnResumePressed);
        if (_quitButton != null)
            _quitButton.onClick.RemoveListener(OnQuitPressed);
        if (_settingsButton != null)
            _settingsButton.onClick.RemoveListener(OnSettingsPressed);
        if (_restartButton != null)
            _restartButton.onClick.RemoveListener(OnRestartPressed);

        UnbindConfirmationListeners();
    }

    private void OnDestroy()
    {
        if (_runtimeConfirmationRoot != null)
            Destroy(_runtimeConfirmationRoot);
    }

    private void Show()
    {
        if (_panel != null)
        {
            _panel.SetActive(true);

            // Pausing on top of a character-unlock reveal used to interleave the two panels:
            // "PAUSED" rendered behind the unlock card while Resume/Restart/Quit rendered in
            // front of it, leaving both unreadable. The player asked for the pause menu, so it
            // takes the top of the stack and its own dim covers whatever was underneath.
            _panel.transform.SetAsLastSibling();
        }
    }

    private void Hide()
    {
        HideConfirmation();
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnResumePressed()
    {
        if (_transitionRequested) return;

        AudioManager.Instance?.PlayMenuButtonClick();
        GameManager.Instance.ResumeGame();
    }

    private void OnRestartPressed()
    {
        if (_transitionRequested) return;

        AudioManager.Instance?.PlayMenuButtonClick();
        RequestConfirmation(PendingAction.Restart);
    }

    private void OnQuitPressed()
    {
        if (_transitionRequested) return;

        AudioManager.Instance?.PlayMenuButtonClick();
        RequestConfirmation(PendingAction.Leave);
    }

    private void OnSettingsPressed()
    {
        if (_transitionRequested) return;

        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("PauseMenuUI: Settings pressed");
        if (_settingsPanel != null)
            _settingsPanel.Show();
    }

    // ------------------------------------------------------------------
    // Confirmation flow
    // ------------------------------------------------------------------

    private void RequestConfirmation(PendingAction action)
    {
        _pendingAction = action;

        if (Application.isPlaying && !HasConfirmationReferences)
            BuildRuntimeConfirmationOverlay();

        if (!HasConfirmationReferences)
        {
            // Only reachable outside play mode, where no overlay can be built. Restart
            // and Leave are destructive, so the safe default is to refuse rather than
            // act on an unconfirmed tap.
            DebugLogger.LogWarning(
                "PauseMenuUI: No confirmation overlay available; ignoring the request.");
            _pendingAction = PendingAction.None;
            return;
        }

        BindConfirmationListeners();

        if (_confirmationPromptLabel != null)
        {
            _confirmationPromptLabel.text =
                action == PendingAction.Restart ? RestartPrompt : LeavePrompt;
        }

        _confirmationPanel.SetActive(true);
    }

    private void OnConfirmationCancelled()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        HideConfirmation();
    }

    private void OnConfirmationConfirmed()
    {
        // Idempotent: the latch is the only thing standing between a double-tap and two
        // scene loads, because SceneLoader's own guard is per-load, not per-intent.
        if (_transitionRequested) return;

        PendingAction action = _pendingAction;
        if (action == PendingAction.None) return;

        // The latch and SceneLoader's in-progress guard have to agree or they are not
        // transactional: a LoadScene that declines at the end of the sequence would leave
        // the attempt already aborted, the pause panel hidden by that abort, and every
        // button latched off with no transition on the way. Refuse before the destructive
        // half starts instead of discovering the decline after it.
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
        {
            DebugLogger.LogWarning(
                "PauseMenuUI: A scene load is already in progress; ignoring the confirmation.");
            return;
        }

        _transitionRequested = true;
        SetConfirmationInteractable(false);
        AudioManager.Instance?.PlayMenuExitButtonClick();

        if (action == PendingAction.Restart)
            ExecuteRestart();
        else
            ExecuteLeave();
    }

    private void ExecuteRestart()
    {
        // A restarted level must never restore the previous attempt's enemies or hearts.
        // SceneLoader.RestartCurrentLevel discards the snapshot too; doing it here keeps
        // the direct-load fallback below honest.
        GameManager.Instance?.DiscardPausedRunSnapshot();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.RestartCurrentLevel();
            return;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        LoadWithoutSceneLoader(activeScene);
    }

    private void ExecuteLeave()
    {
        CachePausedRunSnapshotIfAllowed();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LeaveToLevelSelect();
            return;
        }

        LoadWithoutSceneLoader("LevelSelect");
    }

    // Mirrors the pre-SALIN-141 quit fallback so a gameplay scene opened directly in the
    // Editor (no Bootstrap, therefore no SceneLoader singleton) still transitions.
    private void LoadWithoutSceneLoader(string sceneName)
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        SandboxMode.Deactivate();
#endif
        GameManager.Instance?.AbortCurrentLevelAttempt();
        EnemyPool.Instance?.ReturnAllCheckedOut();
        DebugLogger.LogWarning(
            $"PauseMenuUI: SceneLoader not available. Loading {sceneName} directly. "
            + "Open from Bootstrap for normal transitions.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void CachePausedRunSnapshotIfAllowed()
    {
        if (GameManager.Instance == null)
            return;

        if (!ShouldCachePausedRunSnapshot())
        {
            GameManager.Instance.DiscardPausedRunSnapshot();
            return;
        }

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        if (heartSystem == null)
            return;

        int selectedLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
        var activeEnemies = ActiveEnemyTracker.Instance != null
            ? ActiveEnemyTracker.Instance.GetActiveEnemiesSnapshot()
            : null;
        WaveManager waveManager = FindFirstObjectByType<WaveManager>();
        int currentWaveIndex = waveManager != null ? waveManager.CurrentWaveIndex : -1;
        int currentWaveSpawnedCount = waveManager != null ? waveManager.CurrentWaveSpawnedCount : 0;

        GameManager.Instance.CachePausedRunSnapshot(
            selectedLevel,
            heartSystem.GetCurrentHearts(),
            currentWaveIndex,
            currentWaveSpawnedCount,
            activeEnemies);
    }

    private void HandleLevelAttemptAborted()
    {
        HideConfirmation();
        if (_panel != null) _panel.SetActive(false);
    }

    private void HideConfirmation()
    {
        _pendingAction = PendingAction.None;
        if (_confirmationPanel != null)
            _confirmationPanel.SetActive(false);
    }

    private void SetConfirmationInteractable(bool interactable)
    {
        if (_confirmationConfirmButton != null)
            _confirmationConfirmButton.interactable = interactable;
        if (_confirmationCancelButton != null)
            _confirmationCancelButton.interactable = interactable;
    }

    private void BindConfirmationListeners()
    {
        if (_confirmationListenersBound || !HasConfirmationReferences)
            return;

        _confirmationConfirmButton.onClick.AddListener(OnConfirmationConfirmed);
        _confirmationCancelButton.onClick.AddListener(OnConfirmationCancelled);
        _confirmationListenersBound = true;
    }

    private void UnbindConfirmationListeners()
    {
        if (!_confirmationListenersBound)
            return;

        if (_confirmationConfirmButton != null)
            _confirmationConfirmButton.onClick.RemoveListener(OnConfirmationConfirmed);
        if (_confirmationCancelButton != null)
            _confirmationCancelButton.onClick.RemoveListener(OnConfirmationCancelled);
        _confirmationListenersBound = false;
    }

    /// <summary>
    /// Builds a working confirmation overlay in code when the scene has not authored one.
    /// Direct precedent: SceneLoader.CreateFadeCanvas / CreateLoadingCanvas and
    /// CampaignOutcomeSaveFailurePanel.BuildFallbackUi. Unlike the fade canvas this one
    /// DOES carry a GraphicRaycaster — it is a modal that must receive taps and block the
    /// pause buttons underneath. A designer can later author real art into the same
    /// serialized slots with no code change.
    /// </summary>
    private void BuildRuntimeConfirmationOverlay()
    {
        if (HasConfirmationReferences)
            return;

        if (_runtimeConfirmationRoot != null)
        {
            _confirmationPanel = _runtimeConfirmationRoot;
            return;
        }

        // Resolved before the overlay's own Canvas exists, so it can only ever find an
        // ancestor. overrideSorting is meaningful on a nested Canvas only.
        Canvas parentCanvas = GetComponentInParent<Canvas>(includeInactive: true);

        var root = new GameObject("[Runtime] PauseConfirmation");
        root.transform.SetParent(transform, worldPositionStays: false);
        _runtimeConfirmationRoot = root;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (parentCanvas != null)
        {
            // Nested under the pause canvas: sortingOrder needs the override to apply.
            canvas.overrideSorting = true;
        }
        canvas.sortingOrder = RenderOrder.PauseConfirmation;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        // Full-screen scrim: dims the pause panel and swallows taps meant for it.
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(root.transform, worldPositionStays: false);
        RectTransform scrimRect = scrim.GetComponent<RectTransform>();
        scrimRect.anchorMin = Vector2.zero;
        scrimRect.anchorMax = Vector2.one;
        scrimRect.offsetMin = Vector2.zero;
        scrimRect.offsetMax = Vector2.zero;
        Image scrimImage = scrim.GetComponent<Image>();
        scrimImage.color = new Color(0f, 0f, 0f, 190f / 255f);
        scrimImage.raycastTarget = true;

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, worldPositionStays: false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(820f, 460f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color32(45, 32, 25, 255);
        cardImage.raycastTarget = true;

        _confirmationPromptLabel = CreateOverlayText(card.transform, "PromptLabel", string.Empty);
        _confirmationConfirmButton = CreateOverlayButton(card.transform, "ConfirmButton", "Confirm", 150f);
        _confirmationCancelButton = CreateOverlayButton(card.transform, "CancelButton", "Cancel", 30f);
        _confirmationPanel = root;

        root.SetActive(false);
    }

    private static TMP_Text CreateOverlayText(Transform parent, string name, string text)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, worldPositionStays: false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 40f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(50f, -230f);
        rect.offsetMax = new Vector2(-50f, -45f);
        return label;
    }

    private static Button CreateOverlayButton(Transform parent, string name, string labelText, float y)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(520f, 100f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(209, 168, 82, 255);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateOverlayText(buttonObject.transform, "Label", labelText);
        label.fontSize = 32f;
        label.color = Color.black;
        RectTransform labelRect = ((Component)label).GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    public static bool ShouldCachePausedRunSnapshot()
    {
        if (ChallengeRuntimeState.IsActive)
            return false;

        // Boss encounters cannot be safely resumed from a generic enemy snapshot:
        // the restored boss would lack a running BossController state machine and
        // soft-lock the run. Quitting mid-boss discards the snapshot instead.
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss != null)
            return false;

#if UNITY_EDITOR || SALINLAHI_SANDBOX
        return !SandboxMode.IsActive;
#else
        return true;
#endif
    }
}
