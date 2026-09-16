using System.Collections;
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

    private bool _continueRequested;
    private bool _runtimePanelBuilt;

    /// <summary>True while the preview panel is up and waiting for Continue.</summary>
    public bool IsPresenting { get; private set; }

    /// <summary>The composed preview copy, sourced entirely from the level config.</summary>
    public string RenderedText { get; private set; }

    public IEnumerator Present(LevelConfigSO config)
    {
        if (config == null || config.focusWords == null || config.focusWords.Count == 0)
            yield break;

        RenderedText = BuildPreviewText(config);
        EnsurePanel();
        if (_previewText != null)
            _previewText.text = RenderedText;
        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        _continueRequested = false;
        IsPresenting = true;
        yield return new WaitUntil(() => _continueRequested);

        IsPresenting = false;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    /// <summary>Tap-to-continue: closes the preview and lets the flow advance.</summary>
    public void Continue()
    {
        _continueRequested = true;
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
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        _panelRoot = new GameObject("[Runtime] FocusWordPreview", typeof(RectTransform), typeof(Image));
        _panelRoot.transform.SetParent(parent, false);
        RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 360f);

        // No builtin-sprite lookup: it logs an assert in batch mode; a flat tinted
        // quad is the approved unstyled fallback (see ActiveCluePresenter).
        Image background = _panelRoot.GetComponent<Image>();
        background.color = new Color(0.04f, 0.06f, 0.12f, 0.94f);
        bool onParchment = ScrollPanelArt.ApplyFull(background);

        GameObject textObject = new GameObject("[Runtime] FocusWordPreviewText", typeof(RectTransform));
        textObject.transform.SetParent(_panelRoot.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.25f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(24f, 8f);
        textRect.offsetMax = new Vector2(-24f, -16f);
        _previewText = textObject.AddComponent<TextMeshProUGUI>();
        _previewText.fontSize = 30f;
        _previewText.alignment = TextAlignmentOptions.Center;
        _previewText.raycastTarget = false;

        GameObject buttonObject = new GameObject("[Runtime] FocusWordContinue", typeof(RectTransform), typeof(Image));
        buttonObject.transform.SetParent(_panelRoot.transform, false);
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
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        ApplyParchmentLayout(panelRect, _previewText, _continueButton);
        _continueButton.onClick.AddListener(Continue);
        if (onParchment)
        {
            ScrollPanelArt.InkifyRecursive(_panelRoot.transform);
            ScrollPanelArt.Inkify(label);
        }
        _panelRoot.SetActive(false);
    }

    public static void ApplyParchmentLayout(
        RectTransform panel,
        TMP_Text preview,
        Button continueButton)
    {
        if (panel == null)
            return;

        panel.sizeDelta = new Vector2(560f, 520f);
        if (preview != null)
        {
            ScrollPanelArt.SetAnchors(
                preview.rectTransform,
                Rect.MinMaxRect(0.17f, 0.31f, 0.83f, 0.76f));
            preview.enableAutoSizing = true;
            preview.fontSizeMin = 26f;
            preview.fontSizeMax = 34f;
            preview.textWrappingMode = TextWrappingModes.Normal;
        }

        if (continueButton == null)
            return;

        ScrollPanelArt.SetAnchors(
            continueButton.GetComponent<RectTransform>(),
            Rect.MinMaxRect(0.26f, 0.17f, 0.74f, 0.28f));
    }
}
