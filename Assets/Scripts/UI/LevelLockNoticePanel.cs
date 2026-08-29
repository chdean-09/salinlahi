using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ============================================================================
/// SALIN-137 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the Level Select lock notice can show lives here, and nowhere
/// else, so product/content can rewrite the wording without touching flow logic.
///
/// WHY IT IS PLACEHOLDER: the authored <see cref="LevelConfigSO.levelName"/>
/// values are developer-facing ("Level1", "Chapter2-Level1", "Gauntlet",
/// "Kadiliman") and would read badly to a player, so the copy is built from the
/// level number and the era's display name instead. The final wording, tone, and
/// language (English vs Filipino) are a design call that has NOT been made.
///
/// ACTION REQUIRED: product/content review before release.
/// ============================================================================
/// </summary>
public static class LevelLockNoticeCopy
{
    /// <summary>Dismiss-button label.</summary>
    public const string DismissLabel = "OK";

    /// <summary>
    /// Names the single immediately preceding requirement. SALIN-137 AC2 asks for one
    /// requirement only, so this never chains further back than one step.
    /// </summary>
    /// <param name="requiredLevelNumber">1-based number of the level that must be completed.</param>
    /// <param name="crossesEra">True when the requirement finishes the previous era.</param>
    /// <param name="requiredEraName">
    /// Display name of the era owning the requirement. May be null or empty — the copy
    /// degrades to the plain level-number form, which the legacy progress path always uses.
    /// </param>
    public static string Prerequisite(int requiredLevelNumber, bool crossesEra, string requiredEraName)
    {
        if (requiredLevelNumber < 1)
            return string.Empty;

        if (crossesEra && !string.IsNullOrEmpty(requiredEraName))
            return $"Locked. Finish {requiredEraName} by completing Level {requiredLevelNumber} to open this era.";

        return $"Locked. Complete Level {requiredLevelNumber} first.";
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
    /// when there is nothing to explain (<paramref name="requiredLevelNumber"/> below 1),
    /// which covers the reachable, first-level, and unknown/blocked-save cases.
    /// </summary>
    public void PresentPrerequisite(int requiredLevelNumber, bool crossesEra, string requiredEraName)
    {
        string message = LevelLockNoticeCopy.Prerequisite(requiredLevelNumber, crossesEra, requiredEraName);
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

        GameObject bodyObject = new GameObject("[Runtime] LevelLockNoticeBody", typeof(RectTransform));
        bodyObject.transform.SetParent(card.transform, false);
        Text body = bodyObject.AddComponent<Text>();
        body.font = font;
        body.fontSize = 30;
        body.alignment = TextAnchor.MiddleCenter;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        body.color = Color.white;
        body.raycastTarget = false;
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
        label.fontSize = 26;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = LevelLockNoticeCopy.DismissLabel;
        Stretch(labelObject.GetComponent<RectTransform>());

        _overlayRoot = root;
        _bodyText = body;
        _dismissButton = dismiss;
        _runtimeOverlayRoot = root;
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
