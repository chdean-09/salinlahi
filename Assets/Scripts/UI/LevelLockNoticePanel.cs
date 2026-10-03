using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================================
/// SALIN-137 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the Level Select lock notice can show lives here, and nowhere
/// else, so product/content can rewrite the wording without touching flow logic.
///
/// WHY IT IS BUILT RATHER THAN AUTHORED: the <see cref="LevelConfigSO.levelName"/>
/// values are developer-facing ("Level1", "Chapter2-Level1", "Gauntlet") and would
/// read badly to a player, so the copy is composed from the level number and the
/// era display name instead.
///
/// SALIN-258: the level is now named by an ERA-RELATIVE LABEL ("Ugat Level 5"), never by a
/// global 1-15 id (docs/design/spec-rulings-2026-09.md, "How are levels numbered for the
/// player?"). Both builders below take that label as an already-rendered string rather than
/// an int, because composing it needs the era, which only the caller can resolve -- see
/// CampaignLevelLabel and LevelSelectUI.FindEraForLevel. An EMPTY label means "there is
/// nothing to name", which is what preserves the old "requiredLevelNumber &lt; 1 stays
/// silent" behaviour: the guard moved from the number to the label, it did not disappear.
///
/// LANGUAGE: English. The project splits by role -- UI chrome is English (this class,
/// CampaignSaveNoticePanel, LevelContentMissingPanel), narrative content is Filipino
/// (the dialogue assets). That split was already in force; it is followed here rather
/// than re-decided.
///
/// ACTION REQUIRED: product/content review of the wording before release. The language
/// split above is settled; the sentences themselves are not sacred.
/// ============================================================================
/// </summary>
public static class LevelLockNoticeCopy
{
    /// <summary>Dismiss-button label.</summary>
    public const string DismissLabel = "OK";

    /// <summary>
    /// SALIN-220 AC6. One sentence per completion objective, for the case where the
    /// predecessor was finished but still owes something. Keyed by the
    /// <see cref="LevelObjectives"/> identifier carried on
    /// <see cref="LevelLockStatus.MissingObjectiveId"/>.
    /// </summary>
    /// <remarks>
    /// English, matching <see cref="Prerequisite"/> and <see cref="DismissLabel"/>. The project
    /// splits languages by role: UI chrome is English, narrative content is Filipino (see the
    /// dialogue assets). That split was already in force here and is followed rather than
    /// re-decided. An unrecognised identifier falls back to the plain prerequisite wording, so a
    /// new objective added without copy degrades instead of showing a blank or an identifier.
    /// </remarks>
    /// <param name="objectiveId">A <see cref="LevelObjectives"/> constant.</param>
    /// <param name="requiredLevelLabel">
    /// SALIN-258. The era-relative label of the level that owes the objective ("Ugat Level 4"),
    /// from <see cref="CampaignLevelLabel"/>. Empty when there is nothing to name.
    /// </param>
    public static string MissingObjective(string objectiveId, string requiredLevelLabel)
    {
        if (string.IsNullOrEmpty(requiredLevelLabel))
            return string.Empty;

        switch (objectiveId)
        {
            case LevelObjectives.StoryViewed:
                return $"Locked. Watch the story in {requiredLevelLabel} to open this one.";
            case LevelObjectives.SymbolsPracticed:
                return $"Locked. Practice every symbol in {requiredLevelLabel} to open this one.";
            case LevelObjectives.WordsRestored:
                return $"Locked. Restore every word in {requiredLevelLabel} to open this one.";
            case LevelObjectives.ContextPassed:
                return $"Locked. Finish the challenge in {requiredLevelLabel} to open this one.";
            case LevelObjectives.FinalSyllableRestored:
                return $"Locked. Restore the final syllable in {requiredLevelLabel} to open this one.";
            default:
                return Prerequisite(requiredLevelLabel, crossesEra: false, requiredEraName: null);
        }
    }

    /// <summary>
    /// Names the single immediately preceding requirement. SALIN-137 AC2 asks for one
    /// requirement only, so this never chains further back than one step.
    /// </summary>
    /// <param name="requiredLevelLabel">
    /// SALIN-258. The era-relative label of the level that must be completed ("Ugat Level 5"),
    /// from <see cref="CampaignLevelLabel"/>. Empty when there is nothing to name, which is
    /// the reachable / first-level / blocked-save case.
    /// </param>
    /// <param name="crossesEra">True when the requirement finishes the previous era.</param>
    /// <param name="requiredEraName">
    /// Display name of the era owning the requirement. May be null or empty — the copy
    /// degrades to the plain form, which the legacy progress path always uses. It is still a
    /// separate parameter from the label even though the label usually contains it: it is the
    /// signal that the era was RESOLVED, and the era-crossing sentence is only honest when it
    /// was. A label alone cannot carry that distinction.
    /// </param>
    public static string Prerequisite(string requiredLevelLabel, bool crossesEra, string requiredEraName)
    {
        if (string.IsNullOrEmpty(requiredLevelLabel))
            return string.Empty;

        // SALIN-258 / AC-2, frozen I56 verbatim: "Finish Ugat Level 5". The previous wording
        // was "Finish {era} by completing Level {n}"; the era-relative label folds the era and
        // the level into the one sanctioned form, so repeating the era would read "Finish Ugat
        // by completing Ugat Level 5".
        if (crossesEra && !string.IsNullOrEmpty(requiredEraName))
            return $"Locked. Finish {requiredLevelLabel} to open this era.";

        return $"Locked. Complete {requiredLevelLabel} first.";
    }
}

/// <summary>
/// SALIN-137 AC2: keeps the player on Level Select and explains the immediately
/// preceding requirement when a locked level is pressed.
///
/// Follows the project's established explanation-overlay shape
/// (<see cref="CampaignSaveNoticePanel"/>): serialized root + body + dismiss button,
/// a <see cref="HasRequiredReferences"/> gate, hidden in <c>Awake</c>, and all copy
/// behind static builders (<see cref="LevelLockNoticeCopy"/>).
///
/// UNLIKE that panel, this one BUILDS ITS OWN SURFACE AT RUNTIME when the serialized
/// references are unwired, because <c>Assets/_Scenes/LevelSelect.unity</c> currently
/// has no message surface at all (exactly one legacy <c>Text</c>, zero TMP). That
/// follows the same no-Inspector-wiring fallback precedent as
/// <see cref="SceneLoader"/>'s fade canvas and <c>ActiveCluePresenter</c>'s runtime
/// HUD panel. Authored art replacing this fallback is owed scene work — assign the
/// serialized fields and the runtime build is skipped entirely.
///
/// Legacy <see cref="Text"/> is used deliberately, not TextMeshPro: the Level Select
/// scene contains zero TMP components, so there is no TMP font asset to inherit there.
/// </summary>
public sealed class LevelLockNoticePanel : MonoBehaviour
{
    [Header("Authored Surface (optional — built at runtime when unwired)")]
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private Text _bodyText;
    [SerializeField] private Button _dismissButton;

    private bool _surfaceBuildAttempted;

    /// <summary>
    /// SALIN-137: the runtime-built overlay, when there is one. It is parented to a canvas
    /// this panel does not own, so it would outlive the panel unless the panel destroys it
    /// itself in <see cref="OnDestroy"/>. Null whenever the surface is authored.
    /// </summary>
    private GameObject _runtimeOverlayRoot;

    /// <summary>True once a usable root and body text exist, authored or runtime-built.</summary>
    public bool HasRequiredReferences => _overlayRoot != null && _bodyText != null;

    /// <summary>True while the notice is visible. Drives AC2 verification.</summary>
    public bool IsShowing => _overlayRoot != null && _overlayRoot.activeSelf;

    /// <summary>The message currently on screen, or empty. Drives AC2 verification.</summary>
    public string VisibleMessage => _bodyText != null ? _bodyText.text : string.Empty;

    private void Awake()
    {
        EnsureSurface();
        Hide();
    }

    /// <summary>
    /// Shows the single prerequisite that would unlock the pressed level. Hides instead
    /// when there is nothing to explain (an empty <paramref name="requiredLevelLabel"/>),
    /// which covers the reachable, first-level, and unknown/blocked-save cases.
    /// </summary>
    /// <param name="requiredLevelLabel">
    /// SALIN-258: the era-relative label from <see cref="CampaignLevelLabel"/>, not a global
    /// 1-15 id.
    /// </param>
    public void PresentPrerequisite(string requiredLevelLabel, bool crossesEra, string requiredEraName)
    {
        string message = LevelLockNoticeCopy.Prerequisite(requiredLevelLabel, crossesEra, requiredEraName);
        Present(message);
    }

    /// <summary>
    /// SALIN-220 AC6. Shows which objective the finished predecessor still owes, rather than
    /// the generic "complete Level N" wording, which reads as a bug to a player who has
    /// already completed it.
    /// </summary>
    /// <param name="objectiveId">
    /// A <see cref="LevelObjectives"/> identifier from <see cref="LevelLockStatus.MissingObjectiveId"/>.
    /// An unrecognised value degrades to the prerequisite copy rather than showing nothing.
    /// </param>
    /// <param name="requiredLevelLabel">
    /// SALIN-258: the era-relative label of the level that owes the objective, from
    /// <see cref="CampaignLevelLabel"/>.
    /// </param>
    public void PresentMissingObjective(string objectiveId, string requiredLevelLabel)
    {
        string message = LevelLockNoticeCopy.MissingObjective(objectiveId, requiredLevelLabel);
        Present(message);
    }

    /// <summary>Shows an arbitrary message, or hides when it is empty.</summary>
    public void Present(string message)
    {
        EnsureSurface();

        if (!HasRequiredReferences || string.IsNullOrEmpty(message))
        {
            Hide();
            return;
        }

        _bodyText.text = message;
        _overlayRoot.SetActive(true);
        _overlayRoot.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_dismissButton != null)
            _dismissButton.onClick.RemoveListener(Hide);

        // The runtime overlay hangs off a canvas this panel does not own, so nothing else
        // would ever reap it. Authored surfaces are left alone — the scene owns those.
        if (_runtimeOverlayRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(_runtimeOverlayRoot);
        else
            DestroyImmediate(_runtimeOverlayRoot);

        _runtimeOverlayRoot = null;
    }

    // ---------------------------------------------------------------
    // Runtime fallback surface
    // ---------------------------------------------------------------

    /// <summary>
    /// Builds a minimal overlay when the serialized references are unwired. Attempted at
    /// most once per instance so a failed build (no canvas, no font) never retries every
    /// frame. Called from both <c>Awake</c> and <see cref="Present"/> so the panel also
    /// self-heals when it is created after its own Awake would have run.
    /// </summary>
    private void EnsureSurface()
    {
        if (HasRequiredReferences)
        {
            BindDismissButton();
            return;
        }

        if (_surfaceBuildAttempted)
            return;
        _surfaceBuildAttempted = true;

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            DebugLogger.LogWarning("LevelLockNoticePanel: no canvas available; the lock notice cannot be shown.");
            return;
        }

        // No builtin-sprite lookup: UISprite.psd lives in unity_builtin_extra, which
        // Resources.GetBuiltinResource cannot serve, so the call only asserts (and fails
        // headless tests). Null renders flat tinted quads — acceptable for a
        // no-Inspector-wiring fallback. Authored art must replace this before release.
        Sprite uiSprite = null;
        Font font = ResolveFont();

        GameObject root = new GameObject("[Runtime] LevelLockNotice", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        Image scrim = root.GetComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.6f);
        // Blocks input to the level scrolls behind the notice while it is up.
        scrim.raycastTarget = true;

        GameObject card = new GameObject("[Runtime] LevelLockNoticeCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(720f, 300f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.sprite = uiSprite;
        cardImage.color = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        bool onParchment = ScrollPanelArt.ApplyFull(cardImage);

        GameObject bodyObject = new GameObject("[Runtime] LevelLockNoticeBody", typeof(RectTransform));
        bodyObject.transform.SetParent(card.transform, false);
        Text body = bodyObject.AddComponent<Text>();
        body.font = font;
        body.fontSize = Mathf.RoundToInt(UITextScale.Body);
        body.alignment = TextAnchor.MiddleCenter;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        body.color = Color.white;
        body.raycastTarget = false;
        TutorialFontProvider.ApplyLegibilityEffects(body);
        Stretch(bodyObject.GetComponent<RectTransform>(), new Vector2(36f, 96f), new Vector2(-36f, -36f));

        GameObject buttonObject = new GameObject(
            "[Runtime] LevelLockNoticeDismiss", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(card.transform, false);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = uiSprite;
        buttonImage.color = new Color(0.55f, 0.36f, 0.15f, 1f);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 28f);
        buttonRect.sizeDelta = new Vector2(220f, 56f);
        Button dismiss = buttonObject.GetComponent<Button>();
        dismiss.targetGraphic = buttonImage;

        GameObject labelObject = new GameObject("[Runtime] LevelLockNoticeDismissLabel", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Text label = labelObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = Mathf.RoundToInt(UITextScale.Secondary);
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        TutorialFontProvider.ApplyLegibilityEffects(label);
        label.text = LevelLockNoticeCopy.DismissLabel;
        Stretch(labelObject.GetComponent<RectTransform>());

        _overlayRoot = root;
        _bodyText = body;
        _dismissButton = dismiss;
        _runtimeOverlayRoot = root;
        if (onParchment)
            ScrollPanelArt.InkifyRecursive(card.transform);
        root.SetActive(false);

        BindDismissButton();
    }

    private void BindDismissButton()
    {
        if (_dismissButton == null)
            return;
        _dismissButton.onClick.RemoveListener(Hide);
        _dismissButton.onClick.AddListener(Hide);
    }

    private Canvas ResolveCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

        canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            return canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

        // Last resort: no canvas in the scene at all. Mirrors SceneLoader's fade-canvas
        // stub. Ordered just below the scene-transition canvas so a load still covers it.
        GameObject canvasObject = new GameObject(
            "[Runtime] LevelLockNoticeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, worldPositionStays: false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RenderOrder.LoadingCanvas - 1;
        return canvas;
    }

    /// <summary>
    /// Inherits the font from any legacy <see cref="Text"/> already in the scene — Level
    /// Select has exactly one — so the notice matches the screen it appears on. Falls
    /// back to Unity's builtin legacy font, which is Editor-only; a null font renders
    /// nothing but never throws.
    /// </summary>
    private static Font ResolveFont()
    {
        Text template = FindFirstObjectByType<Text>();
        if (template != null && template.font != null)
            return template.font;

        if (Application.isBatchMode)
            return null;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.zero);

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
