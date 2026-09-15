using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-253. The Era Completion screen (UF-33 / UF-34 / BTN-NEXT-ERA): after the Results
/// screen of an era's final level, the era's name, its five memory tiles, and the control
/// that opens the next era on the Level Select map.
///
/// THERE IS NO EraCompletion.unity, DELIBERATELY. This is a self-building overlay on its own
/// canvas — the shape LevelContentMissingPanel.cs:11-14 established and MemoryCardUI,
/// MemoryArchiveController, MemoryClaimPanel and VictoryScreenUI's fallback controls all
/// followed. A new scene would force an edit to ProjectSettings/EditorBuildSettings.asset's
/// scene list plus hand-authored YAML under a merge=unityyamlmerge attribute (.gitattributes:11)
/// whose driver is not configured locally, so every such edit is an unassisted hand-merge.
/// SALIN-240 was explicitly told not to create one. This ticket edits ZERO serialized assets.
///
/// IT IS AN OVERLAY, NOT A NEW LevelPhase. Appending a phase after Results would need
/// coordinated edits to LevelPhase.cs, LevelPhasePlan.PhaseOrder and LevelPhasePlan.Has —
/// whose `default: return false` means an unhandled new phase is silently never planned —
/// and LevelFlowController.ExecutePhase, whose `default: return ExecuteStubPhase()` silently
/// auto-completes it. Two silent-failure defaults for no benefit.
/// LevelFlowController.ShowMemoryClaimPanel() is the shipped precedent for stacking a surface
/// on Results, and this sits directly beside it.
///
/// FIVE TILES, NOT FIVE CARDS. MemoryCardUI.Present is a single-card FULL-SCREEN modal with a
/// dimming overlay (MemoryCardUI.cs:81, 279, 289), so five instances would stack rather than
/// tile. This renders five rows in the shape of MemoryArchiveController.BuildEntryRow and
/// opens the existing full card on tap, exactly as MemoryArchiveController.OpenCard does.
///
/// LOCKED SILHOUETTES ARE A FIRST-CLASS STATE, NOT A DEFECT. For Ugnayan and Pamana every
/// entry has HasAuthoredContent == false (Levels 6-15 carry rewardIds: []), which is D-015 —
/// Ugat complete and polished, Levels 6-15 present but flagged incomplete — and is exactly
/// what the shipped archive already renders (MemoryArchiveController.cs:21-24). Those eras'
/// completion screens show five locked tiles, not an empty panel.
///
/// ⚠️ AC-2 (THE ERA ENDING LINE) IS CONTENT-BLOCKED AND SHIPS UNBUILT.
/// The screen builds a labelled slot for it and renders nothing, because there is nothing to
/// render. docs/audit/BACKLOG.md:563 cites "Era sheets 'Era Ending Line'"; the frozen workbook
/// has ten sheets and none of them is an Era sheet. docs/audit/AUDIT.md:115: "No era scene, no
/// paragraph, no ending line." EraConfigSO.cs:10-31 has no field to hold one, and adding one
/// here would rewrite Era_01/02/03.asset on the next batch run, because
/// RevisedCampaignBootstrap.Run() ends in AssetDatabase.SaveAssets() (:67) — three serialized
/// asset edits acquired by side effect, for a field with nothing authored to put in it. The
/// field belongs to the authoring ticket, together with its content. The line is Filipino
/// narrative; it is NOT drafted here. See EraCompletionCopy for the full escalation.
///
/// ART IS CONTENT-BLOCKED TOO. Every cutscene panel is image: {fileID: 0} and every Level 1-5
/// contextImage is unassigned, blocked on SALIN-206 (MemoryCardUI.cs:27-31). Tiles render
/// text-only, as the shipped card already does.
///
/// NO MASTERY SUMMARY AND NO ACCURACY/STREAK/SCORE READOUT — D-021 and owner ruling R1. See
/// EraCompletionCopy for why the Jira prose's "mastery summary" is out of scope.
///
/// <see cref="Present"/> returns false when no surface could be built. Callers MUST treat
/// false as "there is nothing to show" and never wait on it.
/// </summary>
public sealed class EraCompletionScreenUI : MonoBehaviour
{
    /// <summary>No era has been requested; Level Select opens wherever it normally would.</summary>
    public const int NoPendingEra = -1;

    private static int _pendingEraIndex = NoPendingEra;

    /// <summary>
    /// SALIN-253 (AC-4). The cross-scene handoff that tells Level Select which era to open on.
    ///
    /// WHY A CONSUMED-ONCE STATIC AND NOT A PlayerPref. A PlayerPrefs key would have to be
    /// registered in ProgressManager.ClearAllProgress or it would survive a journey reset, and
    /// it would persist across a crash so the NEXT session would open on an era the player
    /// never asked for. A static is not MonoBehaviour state, so it survives
    /// SceneLoader.LoadLevelSelect() exactly as long as it needs to and no longer, and it
    /// touches no save schema. That matters this sprint: the save schema has already moved
    /// twice (SALIN-227 most recently).
    ///
    /// It MUST be consumed exactly once — see <see cref="ConsumePendingEraIndex"/>. A value
    /// that is set and never cleared would pin Level Select to Era 2 for the rest of the
    /// session, and nothing else in the project would notice.
    ///
    /// The value is an index into the campaign's compacted era list, which is what
    /// LevelSelectUI.ResolveEras() produces and LevelSelectUI.ShowEra(int) consumes. See
    /// EraBoundary.IndexOfEra.
    /// </summary>
    public static int PendingEraIndex
    {
        get => _pendingEraIndex;
        set => _pendingEraIndex = value;
    }

    /// <summary>
    /// Reads <see cref="PendingEraIndex"/> and resets it to <see cref="NoPendingEra"/> in one
    /// step, so a request can never be honoured twice. Returns <see cref="NoPendingEra"/> when
    /// nothing was pending, which callers must treat as "do not steer the screen".
    /// </summary>
    public static int ConsumePendingEraIndex()
    {
        int pending = _pendingEraIndex;
        _pendingEraIndex = NoPendingEra;
        return pending;
    }

    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _headingText;
    [SerializeField] private TMP_Text _endingLineText;
    [SerializeField] private TMP_Text _memoriesHeadingText;
    [SerializeField] private Transform _contentRoot;
    [SerializeField] private Button _enterNextEraButton;
    [SerializeField] private Button _closeButton;

    private Action _enterNextEraAction;
    private Action _closeAction;
    private bool _listenersBound;
    private MemoryCardUI _cardUI;
    private readonly List<MemoryArchiveEntry> _entries = new();

    public bool HasRequiredReferences =>
        _overlayRoot != null && _headingText != null && _endingLineText != null
        && _memoriesHeadingText != null && _contentRoot != null
        && _enterNextEraButton != null && _closeButton != null;

    /// <summary>True while the screen is on top of the Results screen.</summary>
    public bool IsPresented { get; private set; }

    /// <summary>Tiles rendered — one per level in the completed era.</summary>
    public int TileCount => _entries.Count;

    /// <summary>
    /// True while the ending-line slot is showing text. Always false today: AC-2 is
    /// content-blocked and there is no authored line to show. A reviewer seeing this false is
    /// seeing the recorded state of the content, not a rendering bug.
    /// </summary>
    public bool IsEndingLineVisible =>
        _endingLineText != null && _endingLineText.gameObject.activeSelf;

    private void Awake()
    {
        BindListeners();
        if (!IsPresented)
            Hide();
    }

    /// <summary>
    /// Opens the era completion screen over the Results screen.
    ///
    /// Returns false only when no surface could be built at all — a null era, or a build that
    /// failed to produce every control. An era whose memories are ALL locked still returns
    /// true: that is Ugnayan and Pamana under D-015, a state to render rather than a failure.
    /// </summary>
    /// <param name="entries">
    /// The completed era's archive entries, from MemoryArchiveModel.BuildForEra. Null or empty
    /// renders a heading with no tiles rather than throwing.
    /// </param>
    /// <param name="hasNextEra">
    /// False on the final era, which hides "Enter Next Era". Pushed in rather than recomputed
    /// here so this class never needs the campaign.
    /// </param>
    public bool Present(
        EraConfigSO era,
        IReadOnlyList<MemoryArchiveEntry> entries,
        bool hasNextEra,
        Action enterNextEraAction,
        Action closeAction)
    {
        if (era == null)
        {
            Hide();
            return false;
        }

        BuildScreenUi();
        if (!HasRequiredReferences)
        {
            Hide();
            return false;
        }

        BindListeners();
        _enterNextEraAction = enterNextEraAction;
        _closeAction = closeAction;
        IsPresented = true;

        _entries.Clear();
        if (entries != null)
        {
            foreach (MemoryArchiveEntry entry in entries)
                if (entry != null)
                    _entries.Add(entry);
        }

        _headingText.text = EraCompletionCopy.EraCompleteHeading(era.eraName);
        _memoriesHeadingText.text = EraCompletionCopy.MemoriesHeading;

        // ⚠️ AC-2, CONTENT-BLOCKED. The slot exists and stays empty because no era ending line
        // is authored anywhere in the repo and EraConfigSO has no field to hold one. When the
        // authoring ticket adds both, this is the single line that changes. Do NOT draft a
        // placeholder: this is the last thing a demo player reads.
        _endingLineText.text = string.Empty;
        _endingLineText.gameObject.SetActive(false);

        RenderTiles();

        _enterNextEraButton.gameObject.SetActive(hasNextEra);
        _enterNextEraButton.interactable = hasNextEra;
        _closeButton.interactable = true;

        _overlayRoot.SetActive(true);
        return true;
    }

    public void Hide()
    {
        IsPresented = false;
        if (_cardUI != null)
            _cardUI.Hide();
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    /// <summary>An unlocked tile opens the existing memory card, as the archive does.</summary>
    public void OpenCard(MemoryArchiveEntry entry, int eraTotal)
    {
        if (_cardUI == null)
        {
            GameObject cardObject = new GameObject("[Runtime] EraCompletionMemoryCard");
            cardObject.transform.SetParent(transform.parent, false);
            _cardUI = cardObject.AddComponent<MemoryCardUI>();
        }

        // Present returns false when there is nothing to show. Nothing waits on it; the era
        // screen simply stays open, which is the only sensible outcome for a tile the player
        // could not have opened in the first place.
        _cardUI.Present(entry, eraTotal, null);
    }

    private void BindListeners()
    {
        if (_listenersBound || _enterNextEraButton == null || _closeButton == null)
            return;
        _enterNextEraButton.onClick.AddListener(HandleEnterNextEra);
        _closeButton.onClick.AddListener(HandleClose);
        _listenersBound = true;
    }

    private void HandleEnterNextEra()
    {
        Action action = _enterNextEraAction;
        Hide();
        action?.Invoke();
    }

    private void HandleClose()
    {
        Hide();
        _closeAction?.Invoke();
    }

    private void RenderTiles()
    {
        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            DestroyChild(_contentRoot.GetChild(i).gameObject);

        int eraTotal = _entries.Count;
        foreach (MemoryArchiveEntry entry in _entries)
            BuildTile(entry, eraTotal);
    }

    /// <summary>
    /// One memory tile.
    ///
    /// The LOCKED label is delegated to MemoryArchiveController.LockedRowLabel rather than
    /// rebuilt here, so the archive and this screen cannot drift apart: that method is the
    /// single call site SALIN-258 re-pointed onto the era-relative label, and re-deriving the
    /// string here would be exactly how a global 1-15 number leaks back onto a screen after
    /// that ticket closed.
    ///
    /// The tile's GameObject name keeps the global entry.LevelNumber deliberately — it is
    /// identity for debugging and is never shown to the player.
    /// </summary>
    private void BuildTile(MemoryArchiveEntry entry, int eraTotal)
    {
        bool isOpenable = entry.IsUnlocked && entry.HasAuthoredContent;

        if (!isOpenable)
        {
            CreateTileLabel(
                "LockedTile_" + entry.LevelNumber,
                MemoryArchiveController.LockedRowLabel(entry),
                UITextScale.Secondary,
                new Color32(122, 110, 92, 255));
            return;
        }

        GameObject tileObject = new GameObject(
            "MemoryTile_" + entry.LevelNumber,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        tileObject.transform.SetParent(_contentRoot, false);
        tileObject.GetComponent<LayoutElement>().minHeight = 76f;

        Image tileImage = tileObject.GetComponent<Image>();
        tileImage.color = new Color32(70, 52, 38, 255);
        tileImage.raycastTarget = true;

        Button tileButton = tileObject.GetComponent<Button>();
        tileButton.targetGraphic = tileImage;

        TMP_Text label = CreateText(
            tileObject.transform,
            "Label",
            MemoryCardCopy.CollectibleNumber(entry.EraLocalOrder, eraTotal) + "  " + entry.Title,
            UITextScale.Body);
        label.color = new Color32(240, 226, 198, 255);
        StretchLabel(label);

        MemoryArchiveEntry captured = entry;
        int capturedTotal = eraTotal;
        tileButton.onClick.AddListener(() => OpenCard(captured, capturedTotal));
    }

    private TMP_Text CreateTileLabel(string name, string text, float fontSize, Color32 color)
    {
        GameObject tileObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        tileObject.transform.SetParent(_contentRoot, false);
        tileObject.GetComponent<LayoutElement>().minHeight = fontSize + 26f;

        TMP_Text label = CreateText(tileObject.transform, "Label", text, fontSize);
        label.color = color;
        StretchLabel(label);
        return label;
    }

    private static void StretchLabel(TMP_Text label)
    {
        RectTransform labelRect = ((Component)label).GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(24f, 0f);
        labelRect.offsetMax = new Vector2(-24f, 0f);
    }

    private static void DestroyChild(GameObject child)
    {
        if (Application.isPlaying)
            Destroy(child);
        else
            DestroyImmediate(child);
    }

    /// <summary>
    /// Builds the overlay.
    ///
    /// EVERY OBJECT IS CREATED WITH AN EXPLICIT RectTransform IN THE GameObject CONSTRUCTOR.
    /// uGUI silently repairs a missing RectTransform on anything carrying a Graphic, so
    /// relying on that repair would let a control that lost only its transform still render
    /// and no guard could see the difference (VictoryScreenUI.cs:263-268;
    /// CampaignSaveNoticeSceneWiringTests.cs:63-70).
    ///
    /// DELIBERATELY NOT GATED ON Application.isPlaying, unlike LevelContentMissingPanel. This
    /// surface has no scene dependency, so building it in an EditMode host is safe — and it is
    /// what lets EraCompletionScreenTests assert the controls genuinely exist. Gating on
    /// isPlaying would make that guard assert nothing and report a false green, which is the
    /// exact failure mode it exists to catch: Gameplay.unity once shipped
    /// _starCountText: {fileID: 0} and rendered no stars while the whole suite stayed green.
    /// </summary>
    private void BuildScreenUi()
    {
        if (HasRequiredReferences)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] EraCompletionCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transform.SetParent(canvas.transform, false);
        }

        // Above the Results screen and the memory-claim panel, below the full memory card
        // (MemoryCardUI uses >= 320), which this screen opens on top of itself.
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 315);

        _overlayRoot = gameObject;

        // GetComponent returns a stub rather than a plain null, so only the overloaded
        // == null comparison is reliable here.
        Image overlayImage = GetComponent<Image>();
        if (overlayImage == null)
            overlayImage = gameObject.AddComponent<Image>();
        overlayImage.color = new Color32(28, 20, 14, 250);
        overlayImage.raycastTarget = true;

        RectTransform overlayRect = gameObject.GetComponent<RectTransform>();
        if (overlayRect == null)
            overlayRect = gameObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        _headingText = CreateText(transform, "EraCompleteHeadingText", string.Empty, UITextScale.Title);
        RectTransform headingRect = ((Component)_headingText).GetComponent<RectTransform>();
        headingRect.anchorMin = new Vector2(0f, 1f);
        headingRect.anchorMax = new Vector2(1f, 1f);
        headingRect.pivot = new Vector2(0.5f, 1f);
        headingRect.offsetMin = new Vector2(60f, -110f);
        headingRect.offsetMax = new Vector2(-60f, -40f);
        _headingText.color = new Color32(240, 226, 198, 255);

        // ⚠️ The AC-2 slot. Built so the layout is finished and the guard can see it; left
        // inactive because no era ending line is authored. See the class summary.
        _endingLineText = CreateText(transform, "EraEndingLineText", string.Empty, UITextScale.Body);
        RectTransform endingRect = ((Component)_endingLineText).GetComponent<RectTransform>();
        endingRect.anchorMin = new Vector2(0f, 1f);
        endingRect.anchorMax = new Vector2(1f, 1f);
        endingRect.pivot = new Vector2(0.5f, 1f);
        endingRect.offsetMin = new Vector2(80f, -200f);
        endingRect.offsetMax = new Vector2(-80f, -118f);
        _endingLineText.color = new Color32(214, 198, 170, 255);
        _endingLineText.gameObject.SetActive(false);

        _memoriesHeadingText =
            CreateText(transform, "MemoriesHeadingText", EraCompletionCopy.MemoriesHeading, UITextScale.Body);
        RectTransform memoriesRect = ((Component)_memoriesHeadingText).GetComponent<RectTransform>();
        memoriesRect.anchorMin = new Vector2(0f, 1f);
        memoriesRect.anchorMax = new Vector2(1f, 1f);
        memoriesRect.pivot = new Vector2(0.5f, 1f);
        memoriesRect.offsetMin = new Vector2(60f, -252f);
        memoriesRect.offsetMax = new Vector2(-60f, -206f);
        _memoriesHeadingText.color = new Color32(224, 196, 120, 255);

        GameObject viewport = new GameObject(
            "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewport.transform.SetParent(transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(60f, 220f);
        viewportRect.offsetMax = new Vector2(-60f, -262f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        content.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = viewport.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        _contentRoot = content.transform;

        _enterNextEraButton = CreateButton(
            transform, "EnterNextEraButton", EraCompletionCopy.EnterNextEraLabel, 0f, 116f);
        _closeButton = CreateButton(
            transform, "CloseButton", EraCompletionCopy.CloseLabel, 0f, 24f);
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        TutorialFontProvider.ApplyLegibilityEffects(label);
        return label;
    }

    private static Button CreateButton(
        Transform parent, string name, string labelText, float x, float y)
    {
        GameObject buttonObject = new GameObject(
            name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(420f, 84f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(209, 168, 82, 255);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText(buttonObject.transform, "Label", labelText, UITextScale.Body);
        RectTransform labelRect = ((Component)label).GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.color = Color.black;
        return button;
    }
}
