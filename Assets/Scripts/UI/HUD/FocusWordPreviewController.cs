using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-138: the Focus Words phase surface — presents both restoration goals
/// (word and readable decomposition) after the story intro and before
/// any drawing is possible. Drawing input stays suppressed for the whole preview;
/// the Defense executor releases it exactly once as it opens. Glyph badge
/// art attaches when SALIN-199's assets land; until then the decomposition reads
/// as Latin syllables, the manifest-approved fallback.
/// </summary>
public class FocusWordPreviewController : MonoBehaviour
{
    [Header("Authored wiring (optional — a runtime panel is built when absent)")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private TextMeshProUGUI _previewText;
    [SerializeField] private Button _continueButton;

    private GameObject _overlayRoot;
    private GameObject _wordBlocksRoot;
    private bool _continueRequested;
    private bool _runtimePanelBuilt;

    /// <summary>True while the preview panel is up and waiting for Continue.</summary>
    public bool IsPresenting { get; private set; }

    /// <summary>The composed preview copy, sourced entirely from the level config.</summary>
    public string RenderedText { get; private set; }

    public IEnumerator Present(LevelConfigSO config)
    {
        if (config == null)
            yield break;

        bool hasAuthoredObjective = config.restorationObjective?.HasTargets == true;
        bool hasLegacyFocusWords = config.focusWords != null && config.focusWords.Count > 0;
        if (!hasAuthoredObjective && !hasLegacyFocusWords)
            yield break;

        RenderedText = hasAuthoredObjective
            ? RestorationObjectiveTextFormatter.Render(config.restorationObjective)
            : BuildPreviewText(config);
        EnsurePanel();
        EnsureModalChrome();
        if (_previewText != null)
            _previewText.text = RenderedText;
        BuildWordBlocks(config);
        SetVisible(true);

        _continueRequested = false;
        IsPresenting = true;
        yield return new WaitUntil(() => _continueRequested);

        IsPresenting = false;
        SetVisible(false);
    }

    /// <summary>Tap-to-continue: closes the preview and lets the flow advance.</summary>
    public void Continue()
    {
        _continueRequested = true;
    }

    private void OnDestroy()
    {
        // The overlay is parented to the shared modal canvas, not this controller —
        // a scene teardown while presenting would leave the modal floating over
        // whatever loads next (observed live: an orphaned preview over the victory
        // screen). Scene-authored panels are reparented into the overlay by
        // EnsureModalChrome, so destroying it takes them along.
        if (_overlayRoot != null)
            Destroy(_overlayRoot);
        else if (_panelRoot != null && IsPresenting)
            _panelRoot.SetActive(false);
        IsPresenting = false;
    }

    private static string BuildPreviewText(LevelConfigSO config)
    {
        var builder = new StringBuilder();
        for (int wordIndex = 0; wordIndex < config.focusWords.Count; wordIndex++)
        {
            FocusWordDefinition focus = config.focusWords[wordIndex];
            if (focus == null)
                continue;
            if (builder.Length > 0)
                builder.Append("\n\n");

            builder.Append(string.IsNullOrEmpty(focus.displayLabel)
                ? focus.latinSpelling
                : focus.displayLabel);
            // Ugat QA 2026-09-16: the meaning is English ("IBA - different", "MANA - inheritance")
            // and this card is player-facing, which Q16 does not allow: English is UI copy only,
            // and story text, cutscenes and focus-word explanations stay Filipino. The meaning is
            // still content — it is what the Meaning mastery dimension matches on — it simply is
            // not a gloss to print beside the word. The Filipino explanation the player is meant to
            // read already arrives as focus-word dialogue (Dialogue_Ugat01_Ina / _Ama and their
            // siblings), so removing it here drops a duplicate, not the only copy.

            if (focus.decomposition != null && focus.decomposition.Count > 0)
            {
                builder.Append('\n');
                for (int index = 0; index < focus.decomposition.Count; index++)
                {
                    if (index > 0)
                        builder.Append(" · ");
                    builder.Append(SyllableLabel(focus.decomposition[index]));
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// SALIN-221: the preview reads the word-context spoken value (e.g. "i" for INA, "o" for OO),
    /// not the symbol's combined syllable. <see cref="SpokenValueResolver.ResolveLabel"/> keeps the
    /// same fallback chain this method used to inline — syllable, then the id suffix, then "?".
    /// </summary>
    private static string SyllableLabel(SymbolValueReference reference)
    {
        return SpokenValueResolver.ResolveLabel(reference?.symbol, reference?.spokenValueId);
    }

    private void EnsurePanel()
    {
        if (_panelRoot != null || _runtimePanelBuilt)
            return;

        _runtimePanelBuilt = true;

        // Prefer the HUD canvas so the authored scene renders the preview; a bare
        // test scene parents under this controller (text data still observable).
        Canvas canvas = ScrollPanelArt.ResolveModalCanvas(this);
        Transform parent = canvas != null ? canvas.transform : transform;

        _overlayRoot = ScrollPanelArt.CreateDimOverlay(
            parent, "[Runtime] FocusWordPreviewOverlay");
        RectTransform panelRect = ScrollPanelArt.CreateScrollPanel(
            _overlayRoot.transform, "[Runtime] FocusWordPreview");
        _panelRoot = panelRect.gameObject;

        Image background = panelRect.GetComponent<Image>();
        bool onParchment = ScrollPanelArt.ApplyFull(background);

        GameObject textObject = new GameObject("[Runtime] FocusWordPreviewText", typeof(RectTransform));
        textObject.transform.SetParent(panelRect.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.25f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -16f);
        _previewText = textObject.AddComponent<TextMeshProUGUI>();
        _previewText.fontSize = UITextScale.Title;
        _previewText.alignment = TextAlignmentOptions.Center;
        _previewText.raycastTarget = false;
        TutorialFontProvider.ApplyTo(_previewText);

        GameObject buttonObject = new GameObject("[Runtime] FocusWordContinue", typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(panelRect.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 24f);
        buttonRect.sizeDelta = new Vector2(240f, 56f);
        buttonObject.GetComponent<Image>().color = new Color(0.85f, 0.72f, 0.35f, 1f);
        _continueButton = buttonObject.AddComponent<Button>();

        GameObject buttonLabel = new GameObject("[Runtime] ContinueLabel", typeof(RectTransform));
        buttonLabel.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI label = buttonLabel.AddComponent<TextMeshProUGUI>();
        label.text = "Continue";
        label.fontSize = UITextScale.Body;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        TutorialFontProvider.ApplyTo(label);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        ApplyParchmentLayout(panelRect, _previewText, _continueButton);
        _continueButton.onClick.AddListener(Continue);
        if (onParchment)
        {
            ScrollPanelArt.InkifyRecursive(panelRect.transform);
            ScrollPanelArt.Inkify(label);
        }
        _overlayRoot.SetActive(false);
    }

    /// <summary>
    /// Wraps a scene-authored panel in the same dim overlay the runtime path
    /// builds, then seats it at the shared scroll rect so an authored
    /// <c>FocusWordPreview</c> panel presents identically.
    /// </summary>
    private void EnsureModalChrome()
    {
        if (_panelRoot == null || _overlayRoot != null)
            return;

        Canvas canvas = ScrollPanelArt.ResolveModalCanvas(this);
        Transform parent = canvas != null ? canvas.transform : _panelRoot.transform.parent;

        _overlayRoot = ScrollPanelArt.CreateDimOverlay(
            parent, "[Runtime] FocusWordPreviewOverlay");
        RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
        panelRect.SetParent(_overlayRoot.transform, false);
        panelRect.SetAsLastSibling();

        ApplyParchmentLayout(panelRect, _previewText, _continueButton);
        if (ScrollPanelArt.ApplyFull(_panelRoot.GetComponent<Image>()))
        {
            ScrollPanelArt.InkifyRecursive(panelRect.transform);
            if (_continueButton != null)
                ScrollPanelArt.Inkify(_continueButton.GetComponentInChildren<TMP_Text>(true));
        }
    }

    /// <summary>
    /// SALIN-199 follow-through: each focus word renders its decomposition as
    /// inked glyph outlines (the same bare sprites the memory card uses) above
    /// the word's caption, so the preview teaches the shapes, not just the
    /// spelling. Modes that hide targets until restored (ClueOnlyWords,
    /// MarkedContext, HiddenContext) keep the text-only presentation — the
    /// glyph row would leak what the mode withholds.
    /// </summary>
    private void BuildWordBlocks(LevelConfigSO config)
    {
        if (_wordBlocksRoot != null)
        {
            Destroy(_wordBlocksRoot);
            _wordBlocksRoot = null;
        }

        var entries = CollectWordEntries(config);
        bool anyGlyphs = false;
        for (int i = 0; i < entries.Count; i++)
            anyGlyphs |= entries[i].glyphs.Count > 0;

        if (_previewText != null)
            _previewText.gameObject.SetActive(!anyGlyphs);
        if (!anyGlyphs || _panelRoot == null)
            return;

        _wordBlocksRoot = new GameObject("[Runtime] FocusWordBlocks", typeof(RectTransform));
        _wordBlocksRoot.transform.SetParent(_panelRoot.transform, false);
        RectTransform blocksRect = _wordBlocksRoot.GetComponent<RectTransform>();
        // The blocks band reaches toward both rods — the scroll sprite's own frame
        // supplies the visual margin, so a tighter band left a dead paper tail
        // below the Continue button on portrait.
        ScrollPanelArt.SetAnchors(
            blocksRect, Rect.MinMaxRect(0.18f, BlocksBandBottom, 0.82f, BlocksBandTop));

        float blockHeight = 1f / entries.Count;
        for (int i = 0; i < entries.Count; i++)
            BuildWordBlock(blocksRect, entries[i], i, entries.Count, blockHeight);
    }

    /// <summary>
    /// A resolved glyph plus how its art behaves: the almanac PNG is self-coloured and inks
    /// only part of its frame (the share differs per glyph — see GlyphInkMetrics), while the
    /// outline fallback is a white silhouette that needs inking.
    /// </summary>
    private struct GlyphVisual
    {
        public Sprite sprite;
        public float inkFraction;
        public bool selfColoured;
    }

    /// <summary>
    /// Prefers the almanac's finished glyph over the bare outline — same chain the
    /// restoration rail resolves, so every surface shows the player the same mark.
    /// </summary>
    private static GlyphVisual? ResolveGlyphVisual(BaybayinCharacterSO symbol)
    {
        if (symbol == null)
            return null;

        if (symbol.almanacSprite != null)
        {
            return new GlyphVisual
            {
                sprite = symbol.almanacSprite,
                inkFraction = GlyphInkMetrics.ForAlmanac(symbol),
                selfColoured = true,
            };
        }

        if (symbol.glyphOutlineSprite != null)
        {
            return new GlyphVisual
            {
                sprite = symbol.glyphOutlineSprite,
                inkFraction = GlyphInkMetrics.OutlineFraction,
                selfColoured = false,
            };
        }

        return null;
    }

    // Panel-space layout, shared by BuildWordBlocks and BuildGlyphRow so the band
    // math cannot drift: the blocks band spans BlocksBandBottom..BlocksBandTop of
    // the panel, each block gives its top GlyphRowShare to glyphs and the rest to
    // the caption.
    private const float BlocksBandTop = 0.86f;
    private const float BlocksBandBottom = 0.27f;
    private const float GlyphRowShare = 0.57f;
    private const float ReferencePanelHeight = 998f;

    private struct WordEntry
    {
        public List<GlyphVisual> glyphs;
        public string caption;
    }

    private static List<WordEntry> CollectWordEntries(LevelConfigSO config)
    {
        var entries = new List<WordEntry>();
        RestorationObjectiveDefinition objective = config.restorationObjective;
        if (objective?.HasTargets == true
            && objective.displayMode == RestorationDisplayMode.GuidedWords
            && objective.units != null)
        {
            foreach (RestorationObjectiveUnit unit in objective.units)
            {
                if (unit == null)
                    continue;
                var entry = new WordEntry { glyphs = new List<GlyphVisual>() };
                if (unit.tokens != null)
                    foreach (RestorationObjectiveToken token in unit.tokens)
                        if (token?.IsTarget == true)
                        {
                            GlyphVisual? glyph = ResolveGlyphVisual(token.target?.symbol);
                            if (glyph.HasValue)
                                entry.glyphs.Add(glyph.Value);
                        }
                entry.caption = RestorationObjectiveTextFormatter.RenderUnit(
                    unit, objective.displayMode);
                entries.Add(entry);
            }
        }
        else if (config.focusWords != null)
        {
            foreach (FocusWordDefinition focus in config.focusWords)
            {
                if (focus == null)
                    continue;
                var entry = new WordEntry
                {
                    glyphs = new List<GlyphVisual>(),
                    caption = string.IsNullOrEmpty(focus.displayLabel)
                        ? focus.latinSpelling : focus.displayLabel,
                };
                if (focus.decomposition != null)
                {
                    var syllables = new StringBuilder();
                    foreach (SymbolValueReference reference in focus.decomposition)
                    {
                        GlyphVisual? glyph = ResolveGlyphVisual(reference?.symbol);
                        if (glyph.HasValue)
                            entry.glyphs.Add(glyph.Value);
                        if (syllables.Length > 0)
                            syllables.Append(" · ");
                        syllables.Append(SyllableLabel(reference));
                    }
                    if (syllables.Length > 0)
                        entry.caption += "\n" + syllables;
                }
                entries.Add(entry);
            }
        }
        return entries;
    }

    private void BuildWordBlock(
        RectTransform blocksRect, WordEntry entry, int index, int count, float blockHeight)
    {
        GameObject block = new GameObject("[Runtime] FocusWordBlock" + index, typeof(RectTransform));
        block.transform.SetParent(blocksRect, false);
        RectTransform blockRect = block.GetComponent<RectTransform>();
        float top = 1f - index * blockHeight;
        blockRect.anchorMin = new Vector2(0f, top - blockHeight);
        blockRect.anchorMax = new Vector2(1f, top);
        blockRect.offsetMin = blockRect.offsetMax = Vector2.zero;

        if (entry.glyphs.Count > 0)
            BuildGlyphRow(blockRect, entry.glyphs, count);

        GameObject captionObject = new GameObject("Caption", typeof(RectTransform));
        captionObject.transform.SetParent(block.transform, false);
        RectTransform captionRect = captionObject.GetComponent<RectTransform>();
        captionRect.anchorMin = new Vector2(0f, entry.glyphs.Count > 0 ? 0.02f : 0f);
        captionRect.anchorMax = new Vector2(1f, entry.glyphs.Count > 0 ? 1f - GlyphRowShare - 0.02f : 1f);
        captionRect.offsetMin = captionRect.offsetMax = Vector2.zero;
        TMP_Text caption = captionObject.AddComponent<TextMeshProUGUI>();
        caption.text = entry.caption;
        caption.alignment = TextAlignmentOptions.Center;
        caption.fontSize = UITextScale.Title;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = UITextScale.Caption;
        caption.fontSizeMax = UITextScale.Title;
        caption.raycastTarget = false;
        TutorialFontProvider.ApplyTo(caption);
        ScrollPanelArt.Inkify(caption);
    }

    private static void BuildGlyphRow(RectTransform blockRect, List<GlyphVisual> glyphs, int count)
    {
        GameObject row = new GameObject("GlyphRow", typeof(RectTransform));
        row.transform.SetParent(blockRect, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f - GlyphRowShare);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = rowRect.offsetMax = Vector2.zero;

        // The scroll panel is ~907x998 at reference resolution and the block's
        // glyph band is GlyphRowShare of the block height. Rows cap at 560px so a
        // long word (4+ symbols) shrinks to fit the paper instead of overflowing
        // it, and glyphs stay under the band height so they never hit the caption.
        // Sizes and spacing are ink-to-ink: each sprite's frame is mostly transparent
        // padding (the share differs per glyph — GlyphInkMetrics), so its rect is
        // enlarged by 1/inkFraction while the stride still walks the ink —
        // neighbouring transparent regions overlap harmlessly.
        const float maxRowWidth = 560f;
        const float gap = 22f;
        float bandHeight =
            GlyphRowShare * ((BlocksBandTop - BlocksBandBottom) / count) * ReferencePanelHeight;
        float inkSize = Mathf.Min(170f, (maxRowWidth + gap) / glyphs.Count - gap, bandHeight * 0.92f);
        float stride = inkSize + gap;
        float rowWidth = stride * glyphs.Count - gap;
        for (int i = 0; i < glyphs.Count; i++)
        {
            if (glyphs[i].sprite == null)
                continue;
            GameObject glyphObject = new GameObject("Glyph" + i, typeof(RectTransform), typeof(Image));
            glyphObject.transform.SetParent(row.transform, false);
            RectTransform glyphRect = glyphObject.GetComponent<RectTransform>();
            glyphRect.anchorMin = glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRect.pivot = new Vector2(0.5f, 0.5f);
            glyphRect.anchoredPosition = new Vector2(
                -rowWidth * 0.5f + stride * i + inkSize * 0.5f, 0f);
            float rectSize = inkSize / Mathf.Max(0.01f, glyphs[i].inkFraction);
            glyphRect.sizeDelta = new Vector2(rectSize, rectSize);
            Image image = glyphObject.GetComponent<Image>();
            image.sprite = glyphs[i].sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = glyphs[i].selfColoured ? Color.white : (Color)ScrollPanelArt.InkColor;
        }
    }

    private void SetVisible(bool visible)
    {
        // Authored panels can be serialized inactive, so the panel itself is
        // toggled alongside the overlay rather than relying on the hierarchy.
        if (_overlayRoot != null)
            _overlayRoot.SetActive(visible);
        if (_panelRoot != null)
            _panelRoot.SetActive(visible);
    }

    public static void ApplyParchmentLayout(
        RectTransform panel,
        TMP_Text preview,
        Button continueButton)
    {
        if (panel == null)
            return;

        ScrollPanelArt.SetAnchors(panel, ScrollPanelArt.ScrollArea);
        if (preview != null)
        {
            ScrollPanelArt.SetAnchors(
                preview.rectTransform,
                Rect.MinMaxRect(0.20f, 0.26f, 0.80f, 0.84f));
            preview.enableAutoSizing = true;
            preview.fontSizeMin = UITextScale.Caption;
            preview.fontSizeMax = UITextScale.Title;
            preview.textWrappingMode = TextWrappingModes.Normal;
        }

        if (continueButton == null)
            return;

        ScrollPanelArt.PlaceButton(
            continueButton,
            Rect.MinMaxRect(0.30f, 0.13f, 0.70f, 0.22f));
    }
}
