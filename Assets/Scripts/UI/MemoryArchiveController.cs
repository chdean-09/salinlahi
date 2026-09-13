using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-240. The Memory Archive (spec BTN-ARCHIVE): every memory in the campaign, grouped
/// by era, with the claimed ones openable as cards and the rest shown as locked silhouettes
/// labelled "Earn in Level n".
///
/// THERE IS NO MemoryArchive.unity, DELIBERATELY. The archive is a self-building full-screen
/// overlay on its own canvas, the shape LevelContentMissingPanel established. A new scene
/// would force an edit to ProjectSettings/EditorBuildSettings.asset's scene list plus a
/// hand-authored .unity under a merge=unityyamlmerge attribute whose driver is not
/// configured locally -- the exact combination behind several invisible UI defects on this
/// project. Every acceptance criterion here is about behaviour, not about where the surface
/// is hosted, and the overlay satisfies all of them with a conflict surface of zero
/// serialized assets.
///
/// LEVELS 6-15 ARE SILHOUETTES AND THAT IS CORRECT. They carry rewardIds: [] because D-015
/// scopes Ugat (Levels 1-5) as complete and polished while Levels 6-15 stay present but
/// flagged incomplete. Authoring their memories is SALIN-248/SALIN-251. A reviewer seeing
/// ten locked slots is seeing the specified behaviour, not missing work.
///
/// THE EMPTY ARCHIVE IS A FIRST-CLASS STATE. On a fresh save nothing is unlocked, every slot
/// is a silhouette, and the empty-state line shows. That must render, not throw -- it is the
/// state the archive is in the first time most players open it.
/// </summary>
public sealed class MemoryArchiveController : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _emptyStateText;
    [SerializeField] private Transform _contentRoot;
    [SerializeField] private Button _closeButton;

    private Action _closeAction;
    private bool _listenersBound;
    private MemoryCardUI _cardUI;
    private readonly List<MemoryArchiveEntry> _entries = new();

    public bool HasRequiredReferences =>
        _overlayRoot != null && _titleText != null && _emptyStateText != null
        && _contentRoot != null && _closeButton != null;

    public bool IsPresented { get; private set; }

    /// <summary>Total slots rendered — one per level in the campaign.</summary>
    public int EntryCount => _entries.Count;

    /// <summary>Slots rendered as unlocked memories.</summary>
    public int UnlockedCount
    {
        get
        {
            int count = 0;
            foreach (MemoryArchiveEntry entry in _entries)
                if (entry.IsUnlocked && entry.HasAuthoredContent)
                    count++;
            return count;
        }
    }

    /// <summary>True when the archive has nothing unlocked and is showing the empty line.</summary>
    public bool IsEmptyStateVisible => _emptyStateText != null && _emptyStateText.gameObject.activeSelf;

    private void Awake()
    {
        BindListeners();
        if (!IsPresented)
            Hide();
    }

    /// <summary>
    /// Opens the archive over the current scene, reading unlock state from the live save.
    ///
    /// A null SaveManager, a null Repository or a null campaign are all valid inputs, not
    /// errors: an uninitialised save and an EditMode host both hit them, and the archive
    /// must still open with everything locked rather than throwing on a player's screen.
    /// </summary>
    public bool Present(Action closeAction)
    {
        SaveManager saveManager = SaveManager.Instance;
        CampaignConfigSO campaign = saveManager != null ? saveManager.Campaign : null;
        IReadOnlyCollection<string> unlocked =
            saveManager != null && saveManager.Repository != null
                ? saveManager.Repository.UnlockedMemoryIds
                : null;

        return Present(campaign, unlocked, closeAction);
    }

    /// <summary>
    /// The testable overload: takes the campaign and the unlocked ids directly, so EditMode
    /// tests exercise the whole surface with in-memory fixtures and no SaveManager.
    ///
    /// Returns false only when no surface could be built at all. An empty or null
    /// <paramref name="unlockedMemoryIds"/> still returns true -- that is the fresh-save
    /// archive, which is a state to render, not a failure.
    /// </summary>
    public bool Present(
        CampaignConfigSO campaign,
        IReadOnlyCollection<string> unlockedMemoryIds,
        Action closeAction)
    {
        BuildArchiveUi();
        if (!HasRequiredReferences)
        {
            Hide();
            return false;
        }

        BindListeners();
        _closeAction = closeAction;
        IsPresented = true;

        _entries.Clear();
        foreach (MemoryArchiveEntry entry in MemoryArchiveModel.Build(campaign, unlockedMemoryIds))
            _entries.Add(entry);

        _titleText.text = MemoryCardCopy.ArchiveTitle;
        RenderRows();
        _emptyStateText.gameObject.SetActive(UnlockedCount == 0);
        _emptyStateText.text = MemoryCardCopy.EmptyArchiveBody;

        _overlayRoot.SetActive(true);
        _closeButton.interactable = true;
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

    private void BindListeners()
    {
        if (_listenersBound || _closeButton == null)
            return;
        _closeButton.onClick.AddListener(HandleClose);
        _listenersBound = true;
    }

    private void HandleClose()
    {
        Hide();
        _closeAction?.Invoke();
    }

    /// <summary>
    /// AC-7: one section per era, headed by the authored eraName, then one row per level.
    /// </summary>
    private void RenderRows()
    {
        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            DestroyChild(_contentRoot.GetChild(i).gameObject);

        string currentEra = null;
        var eraTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (MemoryArchiveEntry entry in _entries)
        {
            string key = entry.EraName ?? string.Empty;
            eraTotals.TryGetValue(key, out int total);
            eraTotals[key] = total + 1;
        }

        foreach (MemoryArchiveEntry entry in _entries)
        {
            if (!string.Equals(currentEra, entry.EraName, StringComparison.Ordinal))
            {
                currentEra = entry.EraName;
                CreateRowLabel(
                    "EraHeader_" + currentEra,
                    currentEra,
                    38f,
                    new Color32(224, 196, 120, 255));
            }

            eraTotals.TryGetValue(entry.EraName ?? string.Empty, out int eraTotal);
            BuildEntryRow(entry, eraTotal);
        }
    }

    /// <summary>
    /// One archive slot.
    ///
    /// The locked label is the SALIN-258 boundary call site named in MemoryCardCopy: it
    /// passes the GLOBAL level number because the acceptance criterion's literal wording is
    /// "Earn in Level n". SALIN-258's ruling is "never show a global 1-15"; when that ticket
    /// lands it changes MemoryCardCopy.EarnInLevelFormat and the argument on the next line,
    /// and nothing else on this screen.
    /// </summary>
    private void BuildEntryRow(MemoryArchiveEntry entry, int eraTotal)
    {
        bool isOpenable = entry.IsUnlocked && entry.HasAuthoredContent;

        if (!isOpenable)
        {
            CreateRowLabel(
                "LockedRow_" + entry.LevelNumber,
                MemoryCardCopy.LockedLabel + "  ·  " + MemoryCardCopy.EarnInLevel(entry.LevelNumber),
                28f,
                new Color32(122, 110, 92, 255));
            return;
        }

        GameObject rowObject = new GameObject(
            "MemoryRow_" + entry.LevelNumber,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        rowObject.transform.SetParent(_contentRoot, false);
        rowObject.GetComponent<LayoutElement>().minHeight = 76f;

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color32(70, 52, 38, 255);
        rowImage.raycastTarget = true;

        Button rowButton = rowObject.GetComponent<Button>();
        rowButton.targetGraphic = rowImage;

        TMP_Text label = CreateText(
            rowObject.transform,
            "Label",
            MemoryCardCopy.CollectibleNumber(entry.EraLocalOrder, eraTotal) + "  " + entry.Title,
            30f);
        label.color = new Color32(240, 226, 198, 255);
        RectTransform labelRect = ((Component)label).GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(24f, 0f);
        labelRect.offsetMax = new Vector2(-24f, 0f);

        MemoryArchiveEntry captured = entry;
        int capturedTotal = eraTotal;
        rowButton.onClick.AddListener(() => OpenCard(captured, capturedTotal));
    }

    /// <summary>AC-7: an unlocked entry opens its card.</summary>
    public void OpenCard(MemoryArchiveEntry entry, int eraTotal)
    {
        if (_cardUI == null)
        {
            GameObject cardObject = new GameObject("[Runtime] MemoryCardUI");
            cardObject.transform.SetParent(transform.parent, false);
            _cardUI = cardObject.AddComponent<MemoryCardUI>();
        }

        // Present returns false when there is nothing to show. Nothing waits on it; the
        // archive simply stays open, which is the only sensible outcome for a row the player
        // could not have opened in the first place.
        _cardUI.Present(entry, eraTotal, null);
    }

    private TMP_Text CreateRowLabel(string name, string text, float fontSize, Color32 color)
    {
        GameObject rowObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        rowObject.transform.SetParent(_contentRoot, false);
        rowObject.GetComponent<LayoutElement>().minHeight = fontSize + 26f;

        TMP_Text label = CreateText(rowObject.transform, "Label", text, fontSize);
        label.color = color;
        RectTransform labelRect = ((Component)label).GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(24f, 0f);
        labelRect.offsetMax = new Vector2(-24f, 0f);
        return label;
    }

    private static void DestroyChild(GameObject child)
    {
        if (Application.isPlaying)
            Destroy(child);
        else
            DestroyImmediate(child);
    }

    /// <summary>
    /// Builds the overlay. Every object is created with an explicit RectTransform in the
    /// GameObject constructor: uGUI silently repairs a missing RectTransform on anything
    /// carrying a Graphic, so relying on that repair would let a broken control pass its
    /// guard and be reported as passing.
    /// </summary>
    private void BuildArchiveUi()
    {
        if (HasRequiredReferences)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] MemoryArchiveCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transform.SetParent(canvas.transform, false);
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 310);

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

        _titleText = CreateText(transform, "ArchiveTitleText", MemoryCardCopy.ArchiveTitle, 52f);
        RectTransform titleRect = ((Component)_titleText).GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(60f, -110f);
        titleRect.offsetMax = new Vector2(-60f, -40f);
        _titleText.color = new Color32(240, 226, 198, 255);

        _emptyStateText = CreateText(transform, "EmptyStateText", MemoryCardCopy.EmptyArchiveBody, 28f);
        RectTransform emptyRect = ((Component)_emptyStateText).GetComponent<RectTransform>();
        emptyRect.anchorMin = new Vector2(0f, 1f);
        emptyRect.anchorMax = new Vector2(1f, 1f);
        emptyRect.pivot = new Vector2(0.5f, 1f);
        emptyRect.offsetMin = new Vector2(80f, -196f);
        emptyRect.offsetMax = new Vector2(-80f, -118f);
        _emptyStateText.color = new Color32(196, 180, 156, 255);

        GameObject viewport = new GameObject(
            "Viewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewport.transform.SetParent(transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(60f, 120f);
        viewportRect.offsetMax = new Vector2(-60f, -206f);
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
        contentRect.offsetMin = new Vector2(0f, 0f);
        contentRect.offsetMax = new Vector2(0f, 0f);

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

        _closeButton = CreateButton(transform, "CloseButton", MemoryCardCopy.CloseLabel, 0f, 28f);
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

        TMP_Text label = CreateText(buttonObject.transform, "Label", labelText, 30f);
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
