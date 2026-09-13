using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryScreenUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI _starCountText;
    [SerializeField] private GameObject[] _starIcons;

    [Header("Buttons")]
    [SerializeField] private Button _nextLevelButton;
    [SerializeField] private Button _levelSelectButton;
    [SerializeField] private Button _replayButton;

    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    /// <summary>Names of the objects <see cref="EnsureRuntimeControls"/> builds. Read by tests.</summary>
    public const string RuntimeStarCountName = "[Runtime] StarCount";
    public const string RuntimeStarIconsName = "[Runtime] StarIcons";
    public const string RuntimeReplayButtonName = "[Runtime] ReplayButton";

    private const int StarIconCount = 3;

    /// <summary>
    /// SALIN-234 (AC-10): this attempt's results, pushed by the flow before the screen opens.
    /// Null on the legacy path, where <see cref="Show"/> keeps reading ProgressManager.
    /// </summary>
    private LevelResults _attemptResults;

    private bool _replayListenerBound;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (_nextLevelButton != null)
            _nextLevelButton.onClick.AddListener(OnNextLevelPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.AddListener(OnLevelSelectPressed);
        BindReplayListener();
    }

    private void OnDisable()
    {
        if (_nextLevelButton != null)
            _nextLevelButton.onClick.RemoveListener(OnNextLevelPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.RemoveListener(OnLevelSelectPressed);
        UnbindReplayListener();
    }

    /// <summary>
    /// SALIN-234 (AC-10). Opens the screen showing THIS attempt's stars.
    ///
    /// <see cref="Show"/> reads ProgressManager.GetStars, which on the revised path returns
    /// Repository.GetBestStars — the all-time best, not what the player just earned
    /// (ProgressManager.cs:619-620, CampaignProgressRepository.cs:50). A replay that scored
    /// worse than a previous run therefore congratulated the player on the earlier run's
    /// stars. Passing the attempt's <see cref="LevelResults"/> in fixes that without widening
    /// the ProgressManager contract, which 17 branch sites read.
    ///
    /// The save deliberately still keeps the BEST (CampaignOutcomeCoordinator.cs:237,
    /// Math.Max) — AC-12. Displayed stars and saved stars are different things.
    /// </summary>
    public void PresentResults(LevelResults results)
    {
        _attemptResults = results;
        Show();
    }

    public void Show()
    {
        if (_panel != null)
            _panel.SetActive(true);

        EnsureRuntimeControls();

        int currentLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;

        // The attempt's stars win when the flow computed results for this completion. The
        // GetStars read stays as the legacy fallback: UsesRevisedProgress is false on legacy
        // saves and no LevelResults is computed there (ProgressManager.cs:489-490).
        int stars = _attemptResults != null
            ? _attemptResults.Stars
            : (ProgressManager.Instance != null ? ProgressManager.Instance.GetStars(currentLevel) : 0);

        if (_starCountText != null)
            _starCountText.text = LevelResultsCopy.StarCount(stars);

        if (_starIcons != null)
        {
            for (int i = 0; i < _starIcons.Length; i++)
            {
                if (_starIcons[i] != null)
                    _starIcons[i].SetActive(i < stars);
            }
        }

        bool isLastLevel = currentLevel >= 15;
        if (_nextLevelButton != null)
            _nextLevelButton.gameObject.SetActive(!isLastLevel);

        DebugLogger.Log($"VictoryScreenUI: Level {currentLevel} complete with {stars} stars.");
    }

    /// <summary>
    /// SALIN-202: renders the learning-outcome summary on the victory panel. The
    /// summary object is created at runtime when the scene does not author one,
    /// mirroring the other no-Inspector-wiring fallbacks.
    /// </summary>
    public void ShowResultsSummary(string summaryText)
    {
        if (_panel == null || string.IsNullOrWhiteSpace(summaryText))
            return;

        Transform existing = _panel.transform.Find("[Runtime] ResultsSummary");
        GameObject summaryObject;
        if (existing != null)
        {
            summaryObject = existing.gameObject;
        }
        else
        {
            summaryObject = new GameObject("[Runtime] ResultsSummary", typeof(RectTransform));
            summaryObject.transform.SetParent(_panel.transform, false);
            RectTransform rect = summaryObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(520f, 200f);
        }

        TextMeshProUGUI text = summaryObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = summaryObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = summaryText;
    }

    /// <summary>
    /// SALIN-234. Builds the controls this screen needs and the scene does not author,
    /// under <c>_panel</c>, once.
    ///
    /// WHY AT RUNTIME RATHER THAN IN THE SCENES. Gameplay.unity — the scene the game
    /// actually plays, and the only one whose LevelFlowController._victoryScreen is wired
    /// (Gameplay.unity:4459) — serializes `_starCountText: {fileID: 0}` and `_starIcons: []`
    /// (Gameplay.unity:6347-6348), so every line of the star rendering above no-opped there
    /// while 961 EditMode and 172 PlayMode tests stayed green. Level_01_Tutorial.unity is
    /// wired the mirror image: its star fields ARE authored (:4787-4788) but its
    /// LevelFlowController._victoryScreen is null (:5392), so that scene reaches this
    /// component only through the FindFirstObjectByType fallback at
    /// LevelFlowController.cs:807.
    ///
    /// Authoring the missing fields in the Inspector would edit
    /// Assets/_Scenes/Gameplay.unity and Assets/_Scenes/Level_01_Tutorial.unity, the two
    /// highest-collision serialized assets in the project, while .gitattributes:11 declares
    /// merge=unityyamlmerge and the driver is NOT configured — every scene conflict here is
    /// an unassisted hand-merge. This project has already chosen the other way twice:
    /// ShowResultsSummary above, and LevelContentMissingPanel, whose class comment
    /// (LevelContentMissingPanel.cs:11-14) gives exactly this reasoning. Net effect: zero
    /// serialized-asset edits and zero scene-merge conflict surface.
    ///
    /// Authored fields always win — a populated field is never replaced, so
    /// Level_01_Tutorial.unity keeps rendering through its own objects.
    /// </summary>
    private void EnsureRuntimeControls()
    {
        if (_panel == null)
            return;

        if (_starCountText == null)
        {
            GameObject starCountObject = FindOrCreateChild(_panel.transform, RuntimeStarCountName);
            RectTransform rect = starCountObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -30f);
            rect.sizeDelta = new Vector2(240f, 70f);
            _starCountText = CreateOrGetLabel(starCountObject, 44f);
        }

        if (_starIcons == null || _starIcons.Length == 0)
        {
            GameObject container = FindOrCreateChild(_panel.transform, RuntimeStarIconsName);
            RectTransform containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);
            containerRect.anchoredPosition = new Vector2(0f, -110f);
            containerRect.sizeDelta = new Vector2(330f, 96f);

            var icons = new GameObject[StarIconCount];
            for (int i = 0; i < StarIconCount; i++)
            {
                GameObject icon = FindOrCreateChild(container.transform, "Star" + (i + 1));
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2((i - 1) * 110f, 0f);
                iconRect.sizeDelta = new Vector2(96f, 96f);

                // Unity hands back a "missing component" stub rather than a plain null
                // reference, so `??` does not fire and the next member access throws
                // MissingComponentException. Only the overloaded == null comparison is safe.
                Image iconImage = icon.GetComponent<Image>();
                if (iconImage == null)
                    iconImage = icon.AddComponent<Image>();
                iconImage.color = new Color32(209, 168, 82, 255);
                iconImage.raycastTarget = false;
                icons[i] = icon;
            }
            _starIcons = icons;
        }

        if (_replayButton == null)
        {
            GameObject buttonObject = FindOrCreateChild(_panel.transform, RuntimeReplayButtonName);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 250f);
            rect.sizeDelta = new Vector2(320f, 80f);

            Image image = buttonObject.GetComponent<Image>();
            if (image == null)
                image = buttonObject.AddComponent<Image>();
            image.color = new Color32(209, 168, 82, 255);
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
                button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = FindOrCreateChild(buttonObject.transform, "Label");
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = CreateOrGetLabel(labelObject, 30f);
            label.text = LevelResultsCopy.ReplayLevelLabel;
            label.color = Color.black;

            _replayButton = button;
            BindReplayListener();
        }
    }

    /// <summary>
    /// Every object this screen builds is created WITH a RectTransform. uGUI silently repairs
    /// a missing RectTransform on an object that carries a Graphic, so a control that lost only
    /// its transform would still render and no guard could see it — see the note on
    /// CampaignSaveNoticeSceneWiringTests.cs:63-70.
    /// </summary>
    private static GameObject FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing.gameObject;

        GameObject created = new GameObject(childName, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static TextMeshProUGUI CreateOrGetLabel(GameObject host, float fontSize)
    {
        TextMeshProUGUI label = host.GetComponent<TextMeshProUGUI>();
        if (label == null)
            label = host.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private void BindReplayListener()
    {
        if (_replayButton == null || _replayListenerBound)
            return;
        _replayButton.onClick.AddListener(OnReplayPressed);
        _replayListenerBound = true;
    }

    private void UnbindReplayListener()
    {
        if (_replayButton == null || !_replayListenerBound)
            return;
        _replayButton.onClick.RemoveListener(OnReplayPressed);
        _replayListenerBound = false;
    }

    private void OnNextLevelPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        int currentLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
        int nextLevel = currentLevel + 1;

        if (nextLevel > 15)
        {
            DebugLogger.LogWarning("VictoryScreenUI: No next level. Navigating to Level Select.");
            OnLevelSelectPressed();
            return;
        }

        if (ProgressManager.Instance == null || !ProgressManager.Instance.TrySetSelectedLevelNumber(nextLevel))
        {
            DebugLogger.LogWarning("VictoryScreenUI: Next level could not be persisted.");
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.SetLevel(null);

        DebugLogger.Log($"VictoryScreenUI: Advancing to Level {nextLevel}");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("VictoryScreenUI: SceneLoader not available.");
    }

    /// <summary>
    /// SALIN-234 (AC-8, BTN-REPLAY). Reloads the level the player just finished.
    ///
    /// Deliberately does NOT call TrySetSelectedLevelNumber: the selected level is already the
    /// one being replayed, and writing it again is the one mistake that would turn Replay into
    /// a second Next Level. The saved best stars are untouched — AC-12 is satisfied at
    /// CampaignOutcomeCoordinator.cs:237, where the commit takes Math.Max of old and new.
    /// </summary>
    private void OnReplayPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();

        int currentLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;
        DebugLogger.Log($"VictoryScreenUI: Replaying Level {currentLevel}");

        if (GameManager.Instance != null)
            GameManager.Instance.SetLevel(null);

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("VictoryScreenUI: SceneLoader not available.");
    }

    private void OnLevelSelectPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("VictoryScreenUI: Level Select pressed");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("VictoryScreenUI: SceneLoader not available.");
    }
}
