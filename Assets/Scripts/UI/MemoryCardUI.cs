using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-240. The memory card: a front carrying the collectible number, the level's title,
/// its target words and their Baybayin forms, and a back carrying the lore. A Flip control
/// swaps the two faces.
///
/// NEVER WIRED IN A SCENE. It builds its own canvas and card at runtime, exactly as
/// <see cref="LevelContentMissingPanel"/> does and for the same stated reason: a
/// [SerializeField] reference would have to be authored into every scene that can show a
/// card, and .gitattributes declares merge=unityyamlmerge for .unity while the driver is not
/// configured locally, so every scene edit is an unassisted hand-merge. This ticket edits
/// zero serialized assets.
///
/// DELIBERATE DIFFERENCE FROM LevelContentMissingPanel: that panel gates its build on
/// Application.isPlaying. This one does not. It has no scene dependency at all, so building
/// in an EditMode host is safe -- and it is what lets MemoryCardRuntimeControlTests assert
/// that the controls genuinely exist. Gating on isPlaying would make that guard assert
/// nothing and report a false green, which is the exact failure mode this ticket is
/// guarding against: Gameplay.unity once shipped _starCountText: {fileID: 0} and rendered no
/// stars at all while the whole suite stayed green.
///
/// ART IS CONTENT-BLOCKED (AC-10). The memory illustration does not exist -- every cutscene
/// panel is image: {fileID: 0} and every Level 1-5 contextImage is unassigned, blocked on
/// SALIN-206. No placeholder sprite is stubbed and no filename is invented. What the card
/// shows instead is authored and real: the words, their meanings, the Baybayin glyph
/// outlines, and the lore. The art drops into the existing layout later without a redesign.
///
/// <see cref="Present"/> returns false when no surface could be built. The caller MUST treat
/// false as "there is nothing to show" and never hold on it.
/// </summary>
public sealed class MemoryCardUI : MonoBehaviour
{
    [SerializeField] private GameObject _overlayRoot;
    [SerializeField] private GameObject _frontRoot;
    [SerializeField] private GameObject _backRoot;
    [SerializeField] private TMP_Text _numberText;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _wordsText;
    [SerializeField] private TMP_Text _loreText;
    [SerializeField] private Transform _glyphRow;
    [SerializeField] private Button _flipButton;
    [SerializeField] private Button _closeButton;

    private Action _closeAction;
    private bool _listenersBound;

    public bool HasRequiredReferences =>
        _overlayRoot != null && _frontRoot != null && _backRoot != null
        && _numberText != null && _titleText != null && _wordsText != null
        && _loreText != null && _glyphRow != null
        && _flipButton != null && _closeButton != null;

    /// <summary>True while the card is on screen.</summary>
    public bool IsPresented { get; private set; }

    /// <summary>True while the lore face is showing. AC-2's flip state.</summary>
    public bool IsShowingBack { get; private set; }

    /// <summary>The entry currently rendered, valid while <see cref="IsPresented"/>.</summary>
    public MemoryArchiveEntry PresentedEntry { get; private set; }

    private void Awake()
    {
        BindListeners();
        if (!IsPresented)
            Hide();
    }

    /// <summary>
    /// Shows the card for <paramref name="entry"/>. <paramref name="totalInEra"/> is the
    /// denominator of the collectible number (AC-6).
    ///
    /// Returns false when there is nothing to present -- a null entry, or one whose content
    /// is not authored (Levels 6-15 under D-015). The caller must not wait on a false.
    /// </summary>
    public bool Present(MemoryArchiveEntry entry, int totalInEra, Action closeAction)
    {
        if (entry == null || !entry.HasAuthoredContent)
        {
            Hide();
            return false;
        }

        BuildCardUi();
        if (!HasRequiredReferences)
        {
            Hide();
            return false;
        }

        BindListeners();
        _closeAction = closeAction;
        PresentedEntry = entry;
        IsPresented = true;
        Render(entry, totalInEra);
        ShowFront();
        _overlayRoot.SetActive(true);
        _flipButton.interactable = true;
        _closeButton.interactable = true;
        return true;
    }

    public void Hide()
    {
        IsPresented = false;
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    /// <summary>AC-2. Swaps the two faces. Exactly one root is active at any time.</summary>
    public void Flip()
    {
        if (!HasRequiredReferences)
            return;

        if (IsShowingBack)
            ShowFront();
        else
            ShowBack();
    }

    private void ShowFront()
    {
        IsShowingBack = false;
        _frontRoot.SetActive(true);
        _backRoot.SetActive(false);
        SetButtonLabel(_flipButton, MemoryCardCopy.FlipLabel);
    }

    private void ShowBack()
    {
        IsShowingBack = true;
        _frontRoot.SetActive(false);
        _backRoot.SetActive(true);
        SetButtonLabel(_flipButton, MemoryCardCopy.BackLabel);
    }

    private void BindListeners()
    {
        if (_listenersBound)
            return;
        if (_flipButton == null || _closeButton == null)
            return;

        _flipButton.onClick.AddListener(Flip);
        _closeButton.onClick.AddListener(HandleClose);
        _listenersBound = true;
    }

    private void HandleClose()
    {
        Hide();
        _closeAction?.Invoke();
    }

    /// <summary>
    /// Writes the authored content onto the built controls.
    ///
    /// The title, the word meanings and the lore are reproduced VERBATIM from the assets --
    /// they are Filipino narrative content and this ticket drafts none of it. Only the
    /// headings and the number format come from MemoryCardCopy.
    /// </summary>
    private void Render(MemoryArchiveEntry entry, int totalInEra)
    {
        _numberText.text = MemoryCardCopy.CollectibleNumber(
            entry.EraLocalOrder,
            Mathf.Max(totalInEra, entry.EraLocalOrder));
        _titleText.text = entry.Title;
        _wordsText.text = BuildWordsBlock(entry);
        _loreText.text = MemoryCardCopy.LoreHeading + "\n\n" + entry.Lore;
        BuildGlyphs(entry);
    }

    /// <summary>
    /// "INA — mother" per word, from focusWords[*].displayLabel and .meaning. The meaning is
    /// the approved authored string; nothing is paraphrased.
    /// </summary>
    private static string BuildWordsBlock(MemoryArchiveEntry entry)
    {
        var lines = new List<string> { MemoryCardCopy.WordsHeading };
        if (entry.Words != null)
        {
            foreach (MemoryArchiveWord word in entry.Words)
            {
                if (word == null || string.IsNullOrWhiteSpace(word.Label))
                    continue;
                lines.Add(string.IsNullOrWhiteSpace(word.Meaning)
                    ? word.Label
                    : word.Label + " — " + word.Meaning);
            }
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// AC-4. One Image per decomposition symbol, drawn from glyphOutlineSprite -- the bare
    /// glyph on a transparent background, the only sprite with no card, frame or
    /// romanisation. A symbol whose outline sprite is missing contributes no Image; the
    /// Latin label on the front still names the word, so the card degrades to text rather
    /// than showing an empty box.
    /// </summary>
    private void BuildGlyphs(MemoryArchiveEntry entry)
    {
        for (int i = _glyphRow.childCount - 1; i >= 0; i--)
            DestroyChild(_glyphRow.GetChild(i).gameObject);

        if (entry.Words == null)
            return;

        int index = 0;
        foreach (MemoryArchiveWord word in entry.Words)
        {
            if (word == null || word.Symbols == null)
                continue;

            foreach (BaybayinCharacterSO symbol in word.Symbols)
            {
                if (symbol == null || symbol.glyphOutlineSprite == null)
                    continue;

                GameObject glyphObject = new GameObject(
                    "Glyph" + index, typeof(RectTransform), typeof(Image));
                glyphObject.transform.SetParent(_glyphRow, false);
                RectTransform glyphRect = glyphObject.GetComponent<RectTransform>();
                glyphRect.anchorMin = new Vector2(0f, 0.5f);
                glyphRect.anchorMax = new Vector2(0f, 0.5f);
                glyphRect.pivot = new Vector2(0f, 0.5f);
                glyphRect.sizeDelta = new Vector2(GlyphSize, GlyphSize);
                glyphRect.anchoredPosition = new Vector2(index * (GlyphSize + GlyphGap), 0f);

                Image glyphImage = glyphObject.GetComponent<Image>();
                glyphImage.sprite = symbol.glyphOutlineSprite;
                glyphImage.preserveAspect = true;
                glyphImage.raycastTarget = false;
                // The outline sprite ships white so consumers tint it.
                glyphImage.color = new Color32(240, 226, 198, 255);
                index++;
            }
        }
    }

    private static void DestroyChild(GameObject child)
    {
        if (Application.isPlaying)
            Destroy(child);
        else
            DestroyImmediate(child);
    }

    private const float GlyphSize = 96f;
    private const float GlyphGap = 16f;

    /// <summary>
    /// Builds the whole surface. Every object is created with an explicit RectTransform in
    /// the GameObject constructor -- uGUI silently adds a missing RectTransform to anything
    /// carrying a Graphic, so a control that relied on that repair would pass its guard and
    /// be reported as passing while being unlayoutable.
    /// </summary>
    private void BuildCardUi()
    {
        if (HasRequiredReferences)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] MemoryCardCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transform.SetParent(canvas.transform, false);
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 320);

        _overlayRoot = gameObject;

        // Unity's GetComponent hands back a "missing component" stub rather than a plain
        // null, so `??` does not fire and the next member access throws. Only the
        // overloaded == null comparison is safe here.
        Image overlayImage = GetComponent<Image>();
        if (overlayImage == null)
            overlayImage = gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 205f / 255f);
        overlayImage.raycastTarget = true;

        RectTransform overlayRect = gameObject.GetComponent<RectTransform>();
        if (overlayRect == null)
            overlayRect = gameObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        GameObject card = new GameObject("MemoryCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(880f, 620f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color32(45, 32, 25, 255);
        cardImage.raycastTarget = true;
        bool onParchment = ScrollPanelArt.ApplyFull(cardImage);

        _frontRoot = CreateFace(card.transform, "FrontFace");
        _backRoot = CreateFace(card.transform, "BackFace");

        _numberText = CreateText(_frontRoot.transform, "NumberText", string.Empty, 24f, 56f, 34f);
        _titleText = CreateText(_frontRoot.transform, "TitleText", string.Empty, 78f, 84f, 46f);
        _wordsText = CreateText(_frontRoot.transform, "WordsText", string.Empty, 168f, 180f, 30f);

        GameObject glyphRowObject = new GameObject("GlyphRow", typeof(RectTransform));
        glyphRowObject.transform.SetParent(_frontRoot.transform, false);
        RectTransform glyphRowRect = glyphRowObject.GetComponent<RectTransform>();
        glyphRowRect.anchorMin = new Vector2(0f, 1f);
        glyphRowRect.anchorMax = new Vector2(1f, 1f);
        glyphRowRect.pivot = new Vector2(0.5f, 1f);
        glyphRowRect.offsetMin = new Vector2(60f, -470f);
        glyphRowRect.offsetMax = new Vector2(-60f, -360f);
        _glyphRow = glyphRowObject.transform;

        _loreText = CreateText(_backRoot.transform, "LoreText", string.Empty, 60f, 400f, 30f);

        _flipButton = CreateButton(card.transform, "FlipButton", MemoryCardCopy.FlipLabel, -250f, 24f);
        _closeButton = CreateButton(card.transform, "CloseButton", MemoryCardCopy.CloseLabel, 250f, 24f);

        if (onParchment)
            ScrollPanelArt.InkifyRecursive(card.transform);
    }

    private static GameObject CreateFace(Transform parent, string name)
    {
        GameObject face = new GameObject(name, typeof(RectTransform));
        face.transform.SetParent(parent, false);
        RectTransform rect = face.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(0f, 120f);
        rect.offsetMax = Vector2.zero;
        return face;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null)
            return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;
    }

    // Visual constants deliberately mirror LevelContentMissingPanel so the overlays read as
    // one family.
    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float top,
        float height,
        float fontSize)
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

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(50f, -top - height);
        rect.offsetMax = new Vector2(-50f, -top);
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
        rect.sizeDelta = new Vector2(380f, 92f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color32(209, 168, 82, 255);
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text label = CreateText(buttonObject.transform, "Label", labelText, 0f, 92f, 30f);
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
