using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryScreenUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button _nextLevelButton;
    [SerializeField] private Button _levelSelectButton;
    [SerializeField] private Button _replayButton;

    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    /// <summary>Name of the object <see cref="EnsureRuntimeControls"/> builds. Read by tests.</summary>
    public const string RuntimeReplayButtonName = "[Runtime] ReplayButton";
    /// <summary>Star rating row built by <see cref="PresentResultsSummary"/>. Read by tests.</summary>
    public const string RuntimeStarRowName = "[Runtime] StarRow";
    /// <summary>Score readout built by <see cref="PresentResultsSummary"/>. Read by tests.</summary>
    public const string RuntimeScoreTextName = "[Runtime] ScoreText";
    /// <summary>Stats block built by <see cref="PresentResultsSummary"/>. Read by tests.</summary>
    public const string RuntimeStatsPanelName = "[Runtime] StatsPanel";

    private const int StarCount = 3;
    private const string StarFullPath = "Art/UI/Results/ui_star_full";
    private const string StarEmptyPath = "Art/UI/Results/ui_star_empty";
    private const string HeartFullPath = "Art/UI/Heart/ui_heart_full";
    private const string HeartEmptyPath = "Art/UI/Heart/ui_heart_empty";
    private const string FramePath = "Art/UI/Frames/border";

    private static Sprite _starFull, _starEmpty, _heartFull, _heartEmpty;
    private static Sprite _frame;
    private static bool _spritesLoaded;

    private Coroutine _starAnimation;
    private RectTransform _starRowRef;
    private bool _starPopPending;

    /// <summary>
    /// SALIN-234 (AC-10): this attempt's results, pushed by the flow before the screen opens.
    /// Null on the legacy path, where <see cref="Show"/> keeps reading ProgressManager.
    /// </summary>
    private LevelResults _attemptResults;

    /// <summary>
    /// SALIN-253 (AC-5). True when the completion just shown was an era's FINAL level, which
    /// suppresses "Next Level" so the era completion flow can take over.
    ///
    /// PUSHED IN BY THE FLOW, NEVER RECOMPUTED HERE. This class has no LevelConfigSO and no
    /// EraConfigSO — it reads ProgressManager only — so the era boundary is resolved by
    /// LevelFlowController (which holds both) and handed over with the results. Reaching for
    /// the campaign from here would duplicate EraBoundary's rule on a screen that cannot see
    /// the data it needs.
    /// </summary>
    private bool _isEraFinalLevel;

    private bool _replayListenerBound;
    private bool _showRequested;

    /// <summary>Gameplay HUD root, found by name at Show time — left unwired to keep
    /// the scene diffs out; null is a safe no-op in test scenes.</summary>
    private GameObject _hudRoot;

    private void Awake()
    {
        // In Gameplay.unity this component and _panel are the same object, authored inactive.
        // The first Show() activates it and runs Awake re-entrantly, so preserve that request.
        if (_panel != null && !_showRequested)
            _panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (_nextLevelButton != null)
            _nextLevelButton.onClick.AddListener(OnNextLevelPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.AddListener(OnLevelSelectPressed);
        BindReplayListener();

        // This component sits ON the victory panel in Gameplay.unity, so it is
        // inactive when PresentResultsSummary stages the stars — StartCoroutine
        // would throw there. Show() activates the panel, which lands here.
        if (_starPopPending)
            StartStarPop();
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
    public void PresentResults(LevelResults results) => PresentResults(results, false);

    /// <summary>
    /// SALIN-253 (AC-5). As <see cref="PresentResults(LevelResults)"/>, plus the era-boundary
    /// flag that suppresses "Next Level" at the end of an era.
    ///
    /// The one-argument overload is kept and delegates with <c>false</c>, so the legacy
    /// completion path and the save-retry caller are unchanged — this is an additive
    /// signature, not a contract change.
    /// </summary>
    /// <param name="isEraFinalLevel">
    /// EraBoundary.IsEraFinalLevel for the level just completed, resolved by
    /// LevelFlowController.ShowVictoryScreen.
    /// </param>
    public void PresentResults(LevelResults results, bool isEraFinalLevel)
    {
        _attemptResults = results;
        _isEraFinalLevel = isEraFinalLevel;
        Show();
    }

    public void Show()
    {
        // Set before activation because activating an inactive panel can run Awake immediately.
        _showRequested = true;
        if (_panel != null)
        {
            _panel.SetActive(true);
            // Translucent dim over the live frame — the world stays visible behind the
            // results instead of the authored opaque scrim. Runtime-only: the scenes
            // keep their opaque backdrop and EndScreenBackdropSceneTests keeps passing.
            EndScreenDim.Apply(_panel, EndScreenDim.VictoryTint);
        }

        // The HUD would otherwise read through the dim and float over the results —
        // the defeat screen already hides it for the same reason. All three buttons
        // leave the scene, so nothing has to put it back: the reload does.
        if (_hudRoot == null)
            _hudRoot = GameObject.Find("HUDRoot");
        if (_hudRoot != null)
            _hudRoot.SetActive(false);

        // ActiveCluePresenter sits beside HUDRoot on HUDCanvas, not under it, so the
        // instruction, rail and restored-word cue keep rendering through the dim.
        // Same reload-owns-restore reasoning as the HUD takedown above.
        ActiveCluePresenter cluePresenter = FindFirstObjectByType<ActiveCluePresenter>();
        if (cluePresenter != null)
            cluePresenter.gameObject.SetActive(false);

        EnsureRuntimeControls();

        int currentLevel = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetSelectedLevelNumber() : 1;

        // The attempt's stars win when the flow computed results for this completion. The
        // GetStars read stays as the legacy fallback: UsesRevisedProgress is false on legacy
        // saves and no LevelResults is computed there (ProgressManager.cs:489-490).
        int stars = _attemptResults != null
            ? _attemptResults.Stars
            : (ProgressManager.Instance != null ? ProgressManager.Instance.GetStars(currentLevel) : 0);

        // SALIN-253 (AC-5), closing SALIN-258's deferred AC-3.
        //
        // This read `currentLevel >= 15` alone, which is wrong at exactly two levels in the
        // whole campaign: global 5 and global 10, the ends of Ugat and Ugnayan. There it
        // offered "Next Level" straight into the next era's Level 1, skipping the era moment
        // entirely and stranding the era completion flow.
        //
        // ⚠️ A TEST WRITTEN AT LEVEL 15 CANNOT SEE THIS. The old rule and the era-aware one
        // agree on every level except 5 and 10, so a level-15 case passes under both and
        // proves nothing. VictoryScreenResultsTests uses globals 5 and 4 deliberately.
        //
        // The global check is KEPT as an OR rather than replaced: the legacy progress path
        // pushes no flag, and this way the degraded path can only ever hide the button, never
        // gain one it did not have before.
        bool isLastLevel = _isEraFinalLevel || currentLevel >= 15;
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
        text.fontSize = UITextScale.Body;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = summaryText;
    }

    /// <summary>
    /// Structured results readout: a star rating row, the score, and a framed
    /// stats block (hearts as icons, hints, restored words, new symbols).
    /// Built at runtime under <c>_panel</c> for the same reason as
    /// <see cref="EnsureRuntimeControls"/> — zero scene edits on the two
    /// highest-collision serialized assets. Idempotent: a second call rebuilds
    /// in place rather than stacking duplicates.
    /// </summary>
    public void PresentResultsSummary(LevelResultsViewData data)
    {
        if (_panel == null)
            return;

        EnsureRuntimeControls();
        EnsureSpritesLoaded();

        // TEMP DISABLED (victory screen simplification): star row and score readout
        // intentionally not rendered. Uncomment to restore.
        // RectTransform starRow = EnsureStarRow();
        // ApplyStars(starRow, data.Stars);
        // EnsureScoreText(data.Score);
        EnsureHeartsRow(_panel.transform, data.HeartsRemaining, data.HeartsMax);
        EnsureHeartsCaption();
        EnsureStatsPanel(data);
        PositionButtons();
    }

    private RectTransform EnsureStarRow()
    {
        GameObject row = FindOrCreateChild(_panel.transform, RuntimeStarRowName);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 170f);
        rect.sizeDelta = new Vector2(560f, 150f);

        for (int i = 0; i < StarCount; i++)
        {
            GameObject starObject = FindOrCreateChild(row.transform, "Star_" + i);
            RectTransform starRect = starObject.GetComponent<RectTransform>();
            starRect.anchorMin = starRect.anchorMax = new Vector2(0.5f, 0.5f);
            starRect.pivot = new Vector2(0.5f, 0.5f);
            starRect.anchoredPosition = new Vector2((i - 1) * 190f, 0f);
            starRect.sizeDelta = new Vector2(150f, 150f);
            Image image = starObject.GetComponent<Image>();
            if (image == null)
                image = starObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        return rect;
    }

    private void ApplyStars(RectTransform starRow, int stars)
    {
        int filled = Mathf.Clamp(stars, 0, StarCount);
        for (int i = 0; i < StarCount; i++)
        {
            Transform child = starRow.Find("Star_" + i);
            if (child == null)
                continue;
            Image image = child.GetComponent<Image>();
            if (image == null)
                continue;

            bool earned = i < filled;
            image.sprite = earned ? _starFull : _starEmpty;
            // Fallback tint when the sprites have not loaded (batch/edit-mode):
            // gold for earned, dimmed for missed — never an invisible blank.
            image.color = image.sprite != null
                ? Color.white
                : (earned ? new Color32(209, 168, 82, 255) : new Color32(60, 50, 35, 200));
            child.localScale = Vector3.one;
        }

        if (_starAnimation != null)
        {
            StopCoroutine(_starAnimation);
            _starAnimation = null;
        }
        _starRowRef = starRow;
        _starPopPending = Application.isPlaying;
        if (_starPopPending && isActiveAndEnabled)
            StartStarPop();
    }

    private void StartStarPop()
    {
        _starPopPending = false;
        if (_starRowRef != null)
            _starAnimation = StartCoroutine(PopStars(_starRowRef));
    }

    /// <summary>Staggered pop per star: 0 → 1.25 → 1, ~0.12s apart, unscaled time.</summary>
    private IEnumerator PopStars(RectTransform starRow)
    {
        const float popDuration = 0.28f;
        const float stagger = 0.12f;

        var stars = new List<RectTransform>(StarCount);
        for (int i = 0; i < StarCount; i++)
        {
            Transform child = starRow.Find("Star_" + i);
            if (child != null)
            {
                child.localScale = Vector3.zero;
                stars.Add((RectTransform)child);
            }
        }

        float elapsed = 0f;
        bool anyScaling = true;
        while (anyScaling)
        {
            anyScaling = false;
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < stars.Count; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * stagger) / popDuration);
                if (t < 1f)
                    anyScaling = true;
                // Ease-out cubic with a slight overshoot peak mid-way.
                float scale = t < 0.5f
                    ? Mathf.Lerp(0f, 1.25f, 1f - Mathf.Pow(1f - t * 2f, 3f))
                    : Mathf.Lerp(1.25f, 1f, (t - 0.5f) * 2f);
                stars[i].localScale = Vector3.one * scale;
            }
            yield return null;
        }

        foreach (RectTransform star in stars)
            star.localScale = Vector3.one;
        _starAnimation = null;
    }

    private void EnsureScoreText(int score)
    {
        GameObject scoreObject = FindOrCreateChild(_panel.transform, RuntimeScoreTextName);
        RectTransform rect = scoreObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 30f);
        rect.sizeDelta = new Vector2(560f, 90f);

        TextMeshProUGUI text = scoreObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = scoreObject.AddComponent<TextMeshProUGUI>();
        text.text = LevelResultsCopy.Score(score);
        text.fontSize = UITextScale.Display;
        text.color = new Color32(209, 168, 82, 255);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
    }

    private void EnsureStatsPanel(LevelResultsViewData data)
    {
        GameObject panel = FindOrCreateChild(_panel.transform, RuntimeStatsPanelName);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        // Text-only frame now — the hearts row moved up to the old star position.
        // Slimmer box centred between the hearts caption and Next Level.
        rect.anchoredPosition = new Vector2(0f, -110f);
        rect.sizeDelta = new Vector2(620f, 170f);

        Image frame = panel.GetComponent<Image>();
        if (frame == null)
            frame = panel.AddComponent<Image>();
        if (_frame != null)
        {
            frame.sprite = _frame;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
        }
        else
        {
            frame.sprite = null;
            frame.color = new Color(0.05f, 0.06f, 0.12f, 0.85f);
        }
        frame.raycastTarget = false;

        EnsureStatsText(panel.transform, data);

        // With the hearts row moved out, an empty frame would read as a defect —
        // hide the border when the run restored nothing and unlocked nothing.
        bool hasStatsText =
            !string.IsNullOrEmpty(LevelResultsCopy.Restored(data.RestoredLabels))
            || data.NewSymbolCount > 0;
        frame.enabled = hasStatsText;
    }

    /// <summary>
    /// Hearts moved out of the framed stats box to the vacated star-row band —
    /// larger icons, centre-anchored on the panel, captioned by
    /// <see cref="EnsureHeartsCaption"/>.
    /// </summary>
    private void EnsureHeartsRow(Transform parent, int remaining, int max)
    {
        GameObject row = FindOrCreateChild(parent, "HeartsRow");
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 160f);
        rect.sizeDelta = new Vector2(Mathf.Max(1, max) * 96f, 84f);

        // Transform.Find skips inactive children, so match by iteration: icons
        // beyond the current max get retired below and must be reusable, not
        // duplicated, if a later run needs them again.
        for (int i = 0; i < max; i++)
        {
            Transform heartTransform = null;
            foreach (Transform child in row.transform)
                if (child.name == "Heart_" + i)
                    heartTransform = child;
            GameObject heartObject;
            if (heartTransform != null)
            {
                heartObject = heartTransform.gameObject;
            }
            else
            {
                heartObject = new GameObject("Heart_" + i, typeof(RectTransform));
                heartObject.transform.SetParent(row.transform, false);
            }
            heartObject.SetActive(true);

            RectTransform heartRect = heartObject.GetComponent<RectTransform>();
            heartRect.anchorMin = heartRect.anchorMax = new Vector2(0f, 0.5f);
            heartRect.pivot = new Vector2(0f, 0.5f);
            heartRect.anchoredPosition = new Vector2(i * 96f + 6f, 0f);
            heartRect.sizeDelta = new Vector2(84f, 84f);
            Image image = heartObject.GetComponent<Image>();
            if (image == null)
                image = heartObject.AddComponent<Image>();
            bool filled = i < remaining;
            image.sprite = filled ? _heartFull : _heartEmpty;
            image.color = image.sprite != null
                ? Color.white
                : (filled ? new Color32(190, 60, 60, 255) : new Color32(70, 60, 60, 160));
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        // Retire icons beyond the current max so a run with fewer hearts cannot
        // leave stale hearts on the row.
        foreach (Transform child in row.transform)
        {
            if (!child.name.StartsWith("Heart_"))
                continue;
            if (int.TryParse(child.name.Substring(6), out int index) && index >= max)
                child.gameObject.SetActive(false);
        }
    }

    /// <summary>Caption under the hearts row naming what the icons count.</summary>
    private void EnsureHeartsCaption()
    {
        GameObject captionObject = FindOrCreateChild(_panel.transform, "HeartsCaption");
        RectTransform rect = captionObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 90f);
        rect.sizeDelta = new Vector2(560f, 56f);

        TextMeshProUGUI text = captionObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = captionObject.AddComponent<TextMeshProUGUI>();
        text.text = LevelResultsCopy.HeartsLeftLabel;
        text.fontSize = UITextScale.Body;
        text.color = new Color(0.93f, 0.89f, 0.78f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
    }

    private void EnsureStatsText(Transform parent, LevelResultsViewData data)
    {
        GameObject textObject = FindOrCreateChild(parent, "StatsText");
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0f);
        rect.anchorMax = new Vector2(0.92f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        // Full-height band now that the hearts row no longer reserves the top.
        rect.offsetMin = new Vector2(0f, 24f);
        rect.offsetMax = new Vector2(0f, -24f);

        var builder = new System.Text.StringBuilder();
        // TEMP DISABLED (victory screen simplification): hint count and hint-cost
        // lines hidden; restored words and new symbols remain. Uncomment to restore.
        // builder.Append(LevelResultsCopy.Hints(data.HintsUsed));
        // if (data.HintPenaltyScorePoints > 0)
        // {
        //     builder.Append(LevelResultsCopy.InlineSeparator)
        //         .Append(LevelResultsCopy.HintPenalty(data.HintPenaltyScorePoints));
        // }
        string restored = LevelResultsCopy.Restored(data.RestoredLabels);
        if (!string.IsNullOrEmpty(restored))
            builder.Append(restored);
        if (data.NewSymbolCount > 0)
        {
            if (builder.Length > 0)
                builder.Append(LevelResultsCopy.LineSeparator);
            builder.Append(LevelResultsCopy.NewSymbols(data.NewSymbolCount));
        }

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = builder.ToString();
        text.fontSize = UITextScale.Body;
        text.color = new Color(0.93f, 0.89f, 0.78f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
    }

    /// <summary>
    /// Next Level stays the full-width primary action; Replay and Level Select
    /// become a balanced secondary pair beneath it.
    /// </summary>
    private void PositionButtons()
    {
        PositionButton(_nextLevelButton, new Vector2(0f, -430f), new Vector2(446f, 150f));
        PositionButton(_levelSelectButton, new Vector2(-185f, -645f), new Vector2(330f, 130f));
        PositionButton(_replayButton, new Vector2(185f, -645f), new Vector2(330f, 130f));
    }

    private static void PositionButton(Button button, Vector2 position, Vector2 size)
    {
        if (button == null)
            return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void EnsureSpritesLoaded()
    {
        if (_spritesLoaded)
            return;
        _spritesLoaded = true;
        _starFull = LoadSprite(StarFullPath);
        _starEmpty = LoadSprite(StarEmptyPath);
        _heartFull = LoadSprite(HeartFullPath);
        _heartEmpty = LoadSprite(HeartEmptyPath);
        _frame = LoadSprite(FramePath);
    }

    /// <summary>
    /// Loads the whole-texture sprite, falling back to the first sub-sprite for
    /// textures imported in Multiple mode. Null-safe: missing art leaves the
    /// tinted fallback colors in place.
    /// </summary>
    private static Sprite LoadSprite(string resourcePath)
    {
        Sprite single = Resources.Load<Sprite>(resourcePath);
        if (single != null)
            return single;
        Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpriteCache()
    {
        _spritesLoaded = false;
        _starFull = _starEmpty = _heartFull = _heartEmpty = null;
        _frame = null;
    }

    /// <summary>
    /// SALIN-234. Builds the controls this screen needs and the scene does not author,
    /// under <c>_panel</c>, once.
    ///
    /// WHY AT RUNTIME RATHER THAN IN THE SCENES. Authoring these fields in the Inspector
    /// would edit Assets/_Scenes/Gameplay.unity and Assets/_Scenes/Level_01_Tutorial.unity,
    /// the two highest-collision serialized assets in the project, while .gitattributes:11
    /// declares merge=unityyamlmerge and the driver is NOT configured — every scene conflict
    /// here is an unassisted hand-merge. This project has already chosen the other way
    /// twice: ShowResultsSummary above, and LevelContentMissingPanel, whose class comment
    /// (LevelContentMissingPanel.cs:11-14) gives exactly this reasoning. Net effect: zero
    /// serialized-asset edits and zero scene-merge conflict surface.
    ///
    /// Authored fields always win — a populated field is never replaced.
    /// </summary>
    private void EnsureRuntimeControls()
    {
        if (_panel == null)
            return;

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
            TextMeshProUGUI label = CreateOrGetLabel(labelObject, 42f);
            label.text = LevelResultsCopy.ReplayLevelLabel;
            // Font, fill and label colour land in the shared convention pass at the
            // bottom of this method — nothing else to author here.

            _replayButton = button;
            BindReplayListener();
        }

        // The shared convention, applied to authored and runtime buttons alike on
        // every Show: Next Level carries the gold primary fill; Level Select and
        // Replay take slate — hierarchy by colour, not mismatched plaque sizes.
        // Positions stay authored on the legacy path; PresentResultsSummary's
        // PositionButtons reflows them into the primary/secondary arrangement.
        ScrollPanelArt.StylePrimaryButton(_nextLevelButton);
        ScrollPanelArt.StyleSecondaryButton(_levelSelectButton);
        ScrollPanelArt.StyleSecondaryButton(_replayButton);
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
        TutorialFontProvider.ApplyTo(label);
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
