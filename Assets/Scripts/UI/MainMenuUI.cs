#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
using TMPro;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private const string SceneGameplay = "Gameplay";
    private const string SceneLevelSelect = "LevelSelect";
    private const string SceneTracingDojo = "TracingDojo";
    private const string SceneAlmanac = "Almanac";

    // SALIN-240. The archive button is cloned from SettingsButton at runtime rather than
    // authored into MainMenu.unity. Both names are consts so the scene-wiring guard asserts
    // against the same strings the clone actually uses.
    public const string MemoryArchiveButtonName = "MemoryArchiveButton";
    public const string ArchiveButtonTemplateName = "SettingsButton";

    // SALIN-256. The Exit button is cloned from the same SettingsButton template, for the same
    // reason: MainMenu.unity is not edited by this ticket either.
    public const string ExitButtonName = "ExitButton";

    // SALIN-256. The progress readout is a runtime-built label, not an authored scene object.
    public const string ProgressLineName = "ProgressLine";

    private static readonly string[] MainMenuButtonNames =
    {
        "PlayButton",
        "LevelSelectButton",
        "TracingDojoButton",
        "AlmanacButton",
        // The const, not a literal: if the two drifted, the archive button would silently
        // stop receiving the shadow treatment its siblings get.
        MemoryArchiveButtonName,
        // Same reasoning for Exit (SALIN-256).
        ExitButtonName,
        "SettingsButton"
    };

    private static readonly Color ActiveTextColor = new(0.7019608f, 0.5019608f, 0.07450981f, 1f);
    private static readonly Color LockedTextColor = new(0.38f, 0.34f, 0.26f, 1f);
    private static readonly Color ActiveButtonColor = Color.white;
    private static readonly Color LockedButtonColor = new(0.42f, 0.39f, 0.32f, 0.75f);
    private static readonly Color TextShadowColor = new(0.06f, 0.035f, 0.01f, 1f);
    private static readonly Vector2 TextShadowOffset = new(5f, -5f);

    [Header("Overlay Panels")]
    [SerializeField] private SettingsPanel _settingsPanel;
    [SerializeField] private CreditsPanel _creditsPanel;
    [SerializeField] private CampaignSaveNoticePanel _campaignSaveNoticePanel;

    // SALIN-240. Not a [SerializeField]: the archive builds itself and is created on first
    // press, so nothing has to be authored into MainMenu.unity.
    private MemoryArchiveController _memoryArchive;

    // SALIN-256. Not a [SerializeField] for the same reason as _memoryArchive: the panel
    // builds itself and is created on first press, so nothing is authored into MainMenu.unity.
    private ExitConfirmationPanel _exitConfirmation;

    private void Start()
    {
        EnsureExitEntryPoint();
        ApplyMainMenuTextEffects();
        EnsureSandboxEntryPoint();
        UpdatePlayButtonLabel();
        EnsureProgressLine();
        if (SaveManager.Instance != null && _campaignSaveNoticePanel != null)
            _campaignSaveNoticePanel.Present(SaveManager.Instance.PendingNotice);
    }

    /// <summary>
    /// SALIN-136: reflects the journey entry point on the Play button without new
    /// serialized fields — a fresh save reads as a new journey, an in-progress one as
    /// continue, and a finished one as review. Leaves the label untouched when the
    /// entry point is blocked or the label object cannot be found.
    /// </summary>
    private void UpdatePlayButtonLabel()
    {
        if (ProgressManager.Instance == null)
            return;

        string label;
        if (GameManager.Instance != null && GameManager.Instance.TryGetPausedRunLevelId(out _))
        {
            label = "Continue";
        }
        else
        {
            switch (ProgressManager.Instance.GetJourneyEntryPoint(out _))
            {
                case JourneyEntryKind.NewJourney:
                    label = "Start Journey";
                    break;
                case JourneyEntryKind.ContinueLevel:
                    label = "Continue";
                    break;
                case JourneyEntryKind.CompletedJourney:
                    label = "Review Journey";
                    break;
                default:
                    return;
            }
        }

        Text playLabel = FindPlayButtonLabel();
        if (playLabel != null)
            playLabel.text = label;
    }

    private Text FindPlayButtonLabel()
    {
        Text[] labels = GetComponentsInChildren<Text>(true);
        foreach (Text label in labels)
        {
            if (label != null && label.transform.parent != null
                && label.transform.parent.name == "PlayButton")
                return label;
        }
        return null;
    }

    public void OnPlayButtonPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("MainMenuUI: Play button pressed");

        int selectedLevel = 1;
        if (GameManager.Instance != null
            && GameManager.Instance.TryGetPausedRunLevelId(out int pausedLevelId))
        {
            // SALIN-143: an in-session paused run keeps highest precedence.
            selectedLevel = pausedLevelId;
            DebugLogger.Log($"MainMenuUI: Resuming paused run on level {selectedLevel}.");
        }
        else if (ProgressManager.Instance != null)
        {
            // SALIN-136: continue from the next meaningful point in the journey.
            JourneyEntryKind entryKind = ProgressManager.Instance.GetJourneyEntryPoint(out int entryLevel);
            if (entryKind == JourneyEntryKind.Blocked)
            {
                DebugLogger.LogWarning("MainMenuUI: Journey routing is blocked; save notice pending.");
                return;
            }
            if (entryKind == JourneyEntryKind.CompletedJourney)
            {
                DebugLogger.Log("MainMenuUI: Journey complete — opening Level Select for review/replay.");
                LoadLevelSelect();
                return;
            }

            selectedLevel = entryLevel;
            DebugLogger.Log($"MainMenuUI: Journey entry '{entryKind}' routes to level {selectedLevel}.");
        }

        if (ProgressManager.Instance != null && !ProgressManager.Instance.TrySetSelectedLevelNumber(selectedLevel))
        {
            DebugLogger.LogWarning("MainMenuUI: Selected level could not be persisted.");
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.SetLevel(null);

        LoadGameplay();
    }

    public void OnLevelSelectPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("MainMenuUI: Level Select pressed");
        LoadLevelSelect();
    }

    public void OnTracingDojoPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        LoadTracingDojo();
    }

    public void OnAlmanacPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("MainMenuUI: Almanac pressed");
        LoadAlmanac();
    }

    /// <summary>
    /// SALIN-240 (spec BTN-ARCHIVE). Opens the Memory Archive.
    ///
    /// Deliberately NOT routed through SceneLoader: there is no archive scene and none is
    /// being added. The archive is a self-building overlay, so it opens over the menu the
    /// player is already looking at.
    /// </summary>
    public void OnMemoryArchivePressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("MainMenuUI: Memory Archive pressed");

        if (_memoryArchive == null)
        {
            GameObject archiveObject = new GameObject("[Runtime] MemoryArchiveController");
            _memoryArchive = archiveObject.AddComponent<MemoryArchiveController>();
        }

        if (!_memoryArchive.Present(null))
            DebugLogger.LogWarning("MainMenuUI: the Memory Archive could not build a surface.");
    }

    /// <summary>
    /// SALIN-240. Creates the archive button by cloning SettingsButton, exactly as
    /// <see cref="CreateSandboxButton"/> already does. Zero edits to MainMenu.unity: a
    /// serialized button would mean hand-authoring the scene under a merge=unityyamlmerge
    /// attribute whose driver is not configured here.
    ///
    /// The Find below is this ticket's ONE genuine scene dependency, and it fails silently —
    /// if SettingsButton is renamed, reparented away from this transform or loses its Button,
    /// the clone is never created, the archive becomes unreachable, and nothing in the test
    /// suite notices. MemoryArchiveSceneWiringTests exists solely to make that failure loud.
    /// </summary>
    private void EnsureMemoryArchiveEntryPoint()
    {
        Transform parent = transform;

        if (parent.Find(MemoryArchiveButtonName) is Transform existing
            && existing.GetComponent<Button>() != null)
        {
            return;
        }

        Button template = parent.Find(ArchiveButtonTemplateName)?.GetComponent<Button>();
        if (template == null)
        {
            DebugLogger.LogWarning(
                $"MainMenuUI: '{ArchiveButtonTemplateName}' was not found under the main menu, so "
                + "the Memory Archive button could not be created and the archive is unreachable.");
            return;
        }

        GameObject buttonObject = Instantiate(template.gameObject, parent, false);
        buttonObject.name = MemoryArchiveButtonName;
        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            return;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        // Clear of SandboxModeButton, which sits at y = 16 in editor/sandbox builds.
        rect.anchoredPosition = new Vector2(0f, 124f);

        // Fully qualified on purpose: `using TMPro;` at the top of this file sits inside a
        // #if UNITY_EDITOR || SALINLAHI_SANDBOX block, so the short name does not resolve in a
        // plain player build, and adding a second using directive would raise CS0105.
        TMPro.TextMeshProUGUI tmpLabel = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (tmpLabel != null)
            tmpLabel.text = MemoryCardCopy.ArchiveTitle;

        Text legacyLabel = button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
            legacyLabel.text = MemoryCardCopy.ArchiveTitle;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnMemoryArchivePressed);
        button.interactable = true;
        button.gameObject.SetActive(true);
    }

    /// <summary>
    /// SALIN-256 (spec BTN-EXIT). Confirms before leaving; the quit itself lives in
    /// ExitConfirmationPanel, which is the only place in the project that calls
    /// Application.Quit.
    ///
    /// NOTE FOR REVIEW: Application.Quit is a NO-OP IN THE EDITOR, so no automated test here
    /// or anywhere else can prove this button actually exits the app. The confirm path is
    /// covered through ExitConfirmationPanel.QuitAction; the quit needs a manual Android check.
    /// </summary>
    public void OnExitPressed()
    {
        AudioManager.Instance?.PlayMenuExitButtonClick();
        DebugLogger.Log("MainMenuUI: Exit pressed");

        if (_exitConfirmation == null)
        {
            GameObject panelObject = new GameObject("[Runtime] ExitConfirmationPanel");
            _exitConfirmation = panelObject.AddComponent<ExitConfirmationPanel>();
        }

        if (!_exitConfirmation.Present())
            DebugLogger.LogWarning("MainMenuUI: the Exit confirmation could not build a surface.");
    }

    /// <summary>
    /// SALIN-256. Creates the Exit button by cloning SettingsButton, exactly as
    /// <see cref="EnsureMemoryArchiveEntryPoint"/> already does.
    ///
    /// The Find below FAILS SILENTLY in the same way its sibling does — if SettingsButton is
    /// renamed or reparented away from this transform, no Exit button is ever created and
    /// nothing throws. That precondition is already guarded by
    /// MemoryArchiveSceneWiringTests.SettingsButtonTemplate_StillExistsUnderTheMainMenuUIAndCarriesAButton,
    /// which asserts the identical template contract this clone depends on; a second scene
    /// fixture re-asserting it would add no coverage. The construction risk that IS new here
    /// — that the clone is built, named and wired — is covered by MainMenuEntryPointsTests.
    /// </summary>
    private void EnsureExitEntryPoint()
    {
        Transform parent = transform;

        Button button = parent.Find(ExitButtonName)?.GetComponent<Button>();
        if (button == null)
        {
            Button template = parent.Find(ArchiveButtonTemplateName)?.GetComponent<Button>();
            if (template == null)
            {
                DebugLogger.LogWarning(
                    $"MainMenuUI: '{ArchiveButtonTemplateName}' was not found under the main menu, so "
                    + "the Exit button could not be created and the player cannot quit from the menu.");
                return;
            }

            GameObject buttonObject = Instantiate(template.gameObject, parent, false);
            buttonObject.name = ExitButtonName;
            button = buttonObject.GetComponent<Button>();
            if (button == null)
                return;
        }

        // Fully qualified on purpose — see the note in EnsureMemoryArchiveEntryPoint.
        TMPro.TextMeshProUGUI tmpLabel = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (tmpLabel != null)
            tmpLabel.text = MainMenuProgressCopy.ExitConfirmButtonLabel;

        Text legacyLabel = button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
            legacyLabel.text = MainMenuProgressCopy.ExitConfirmButtonLabel;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnExitPressed);
        button.interactable = true;
        button.gameObject.SetActive(true);
    }

    /// <summary>
    /// SALIN-256 (spec UF-03/UF-06). Builds the "Ugat Level 3  ·  13%" readout.
    ///
    /// Built at runtime rather than authored, for the same MainMenu.unity reason as the two
    /// cloned buttons. Refreshed in Start() only: the menu has no other mutation point, and
    /// progress cannot change while the player is looking at this screen.
    ///
    /// Renders nothing when the line would be untruthful — no ProgressManager, or a level the
    /// campaign cannot name. MainMenuProgressLine.Format owns that decision.
    /// </summary>
    private void EnsureProgressLine()
    {
        if (ProgressManager.Instance == null)
            return;

        ProgressManager progress = ProgressManager.Instance;
        progress.GetJourneyEntryPoint(out int currentLevelNumber);

        int completed = 0;
        for (int levelNumber = 1; levelNumber <= ProgressManager.TotalLevels; levelNumber++)
        {
            if (progress.IsLevelCompleted(levelNumber))
                completed++;
        }

        string line = MainMenuProgressLine.Format(
            SaveManager.Instance?.Campaign,
            currentLevelNumber,
            completed,
            ProgressManager.TotalLevels);

        if (string.IsNullOrEmpty(line))
            return;

        // Fully qualified on purpose — see the note in EnsureMemoryArchiveEntryPoint.
        TMPro.TextMeshProUGUI label = FindOrCreateProgressLabel();
        if (label != null)
            label.text = line;
    }

    private TMPro.TextMeshProUGUI FindOrCreateProgressLabel()
    {
        if (transform.Find(ProgressLineName) is Transform existing)
            return existing.GetComponent<TMPro.TextMeshProUGUI>();

        GameObject labelObject = new GameObject(
            ProgressLineName, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(transform, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        // Directly above the Exit button at y = 124, continuing the same bottom-anchored stack.
        rect.anchoredPosition = new Vector2(0f, 212f);
        rect.sizeDelta = new Vector2(720f, 56f);

        TMPro.TextMeshProUGUI label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        label.fontSize = 32f;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = ActiveTextColor;
        label.raycastTarget = false;
        return label;
    }

    public void OnSettingsPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("MainMenuUI: Settings pressed");
        if (_settingsPanel != null)
        {
            _settingsPanel.EnableJourneyReset();
            _settingsPanel.Show();
        }
    }

    public void OnCreditsPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("MainMenuUI: Credits pressed");
        if (_creditsPanel != null)
            _creditsPanel.Show();
    }

#if UNITY_EDITOR || SALINLAHI_SANDBOX
    public void OnSandboxModePressed()
    {
        if (!SandboxMode.IsAvailable)
        {
            DebugLogger.LogWarning("MainMenuUI: Sandbox mode is not available in this build.");
            return;
        }

        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("MainMenuUI: Sandbox mode pressed");
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadSandboxGameplay();
        else
            LoadSandboxGameplayDirect();
    }

    private void EnsureSandboxEntryPoint()
    {
        if (!SandboxMode.IsAvailable)
            return;

        Button sandboxButton = CreateSandboxButton();
        if (sandboxButton == null)
            return;

        sandboxButton.onClick.RemoveAllListeners();
        sandboxButton.onClick.AddListener(OnSandboxModePressed);
        sandboxButton.interactable = true;
        sandboxButton.gameObject.SetActive(true);
    }

    private Button CreateSandboxButton()
    {
        // SALIN-225 removed the Endless Mode button this used to hang off and clone.
        // Both lookups collapse to the fallbacks they already carried.
        Transform parent = transform;

        if (parent.Find("SandboxModeButton") is Transform existing && existing.GetComponent<Button>() != null)
            return existing.GetComponent<Button>();

        Button template = parent.Find("SettingsButton")?.GetComponent<Button>();

        if (template == null)
            return null;

        GameObject buttonObject = Instantiate(template.gameObject, parent, false);
        buttonObject.name = "SandboxModeButton";
        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            return null;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 16f);

        TextMeshProUGUI tmpLabel = buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null)
            tmpLabel.text = "SANDBOX";

        Text legacyLabel = buttonObject.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
            legacyLabel.text = "SANDBOX";

        return button;
    }

    private static void LoadSandboxGameplayDirect()
    {
        if (!SandboxMode.TryActivate())
        {
            DebugLogger.LogWarning("MainMenuUI: Sandbox mode is not available in this build.");
            return;
        }

        GameManager.Instance?.DiscardPausedRunSnapshot();
        EnemyPool.Instance?.ReturnAllCheckedOut();
        LoadSceneDirect(SceneGameplay);
    }
#else
    private void EnsureSandboxEntryPoint() { }
#endif

    private static void LoadGameplay()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
        {
            CleanupDirectGameplayState();
            LoadSceneDirect(SceneGameplay);
        }
    }

    private static void LoadLevelSelect()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            LoadSceneDirect(SceneLevelSelect);
    }

    private static void LoadTracingDojo()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(SceneTracingDojo);
        else
            LoadSceneDirect(SceneTracingDojo);
    }

    private static void LoadAlmanac()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadAlmanac();
        else
            LoadSceneDirect(SceneAlmanac);
    }

    private static void LoadSceneDirect(string sceneName)
    {
        DebugLogger.LogWarning(
            $"MainMenuUI: SceneLoader not available. Loading '{sceneName}' directly. "
            + "Open from Bootstrap for normal transitions.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private static void CleanupDirectGameplayState()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        SandboxMode.Deactivate();
#endif
        EnemyPool.Instance?.ReturnAllCheckedOut();
    }

    private static void ApplyButtonVisualState(Button button, bool isUnlocked)
    {
        if (button.targetGraphic != null)
            button.targetGraphic.color = isUnlocked ? ActiveButtonColor : LockedButtonColor;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = isUnlocked ? ActiveTextColor : LockedTextColor;
            EnsureTextShadow(label);
        }
    }

    private void ApplyMainMenuTextEffects()
    {
        Text[] labels = GetComponentsInChildren<Text>(true);
        foreach (Text label in labels)
        {
            if (label == null)
                continue;

            if (IsPrimaryMenuLabel(label))
                EnsureTextShadow(label);
        }
    }

    private static bool IsPrimaryMenuLabel(Text label)
    {
        if (label.name == "TitleText" || label.text == "Salinlahi")
            return true;

        return label.transform.parent != null && IsMainMenuButton(label.transform.parent.name);
    }

    private static bool IsMainMenuButton(string objectName)
    {
        foreach (string buttonName in MainMenuButtonNames)
        {
            if (objectName == buttonName)
                return true;
        }

        return false;
    }

    private static void EnsureTextShadow(Text label)
    {
        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null)
            shadow = label.gameObject.AddComponent<Shadow>();

        shadow.effectColor = TextShadowColor;
        shadow.effectDistance = TextShadowOffset;
        shadow.useGraphicAlpha = true;
    }
}
