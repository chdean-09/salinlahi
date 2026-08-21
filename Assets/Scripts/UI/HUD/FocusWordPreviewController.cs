using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-138: the Focus Words phase surface — presents both restoration goals
/// (word, meaning, and readable decomposition) after the story intro and before
/// any drawing is possible. Drawing input stays suppressed for the whole preview;
/// the Defense executor releases it exactly once when waves start. Glyph badge
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
            if (!string.IsNullOrEmpty(focus.meaning))
                builder.Append(" — ").Append(focus.meaning);

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

    private static string SyllableLabel(SymbolValueReference reference)
    {
        if (reference?.symbol != null && !string.IsNullOrEmpty(reference.symbol.syllable))
            return reference.symbol.syllable;
        if (!string.IsNullOrEmpty(reference?.spokenValueId)
            && reference.spokenValueId.StartsWith("value.", System.StringComparison.Ordinal))
            return reference.spokenValueId.Substring("value.".Length);
        return "?";
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
        label.text = "Magpatuloy";
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        _continueButton.onClick.AddListener(Continue);
        _panelRoot.SetActive(false);
    }
}
