using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _overlayPanel;
    [SerializeField] private TMP_Text _speakerText;
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _tapCatcher;

    [Header("Typewriter Settings")]
    [SerializeField] private float _charsPerSecond = 30f;

    // Render the dialogue above the tutorial spotlight dim (sortingOrder 100) and the
    // Level 1 guide canvas (110) so instruction text / buttons are never darkened by the
    // overlay. A nested Canvas with overrideSorting affects only this subtree, so the
    // rest of the HUD is still dimmed normally by the spotlight.
    private const int OverlaySortingOrder = 200;

    private DialogueSO _currentDialogue;
    private int _lineIndex;
    private bool _isTypewriting;
    private Coroutine _typewriterRoutine;

    public static DialogueController CreateRuntime()
    {
        Canvas canvas = FindTutorialCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
        }

        GameObject controllerObject = new("RuntimeDialogueController", typeof(RectTransform));
        controllerObject.transform.SetParent(canvas.transform, false);
        RectTransform controllerRect = controllerObject.GetComponent<RectTransform>();
        Stretch(controllerRect);

        DialogueController controller = controllerObject.AddComponent<DialogueController>();
        controller._overlayPanel = CreateOverlay(controllerObject.transform);
        controller._speakerText = CreateText(controller._overlayPanel.transform, "SpeakerText", new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.84f), 26f, TextAlignmentOptions.Center);
        controller._bodyText = CreateText(controller._overlayPanel.transform, "BodyText", new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.62f), 32f, TextAlignmentOptions.Center);
        controller._portraitImage = CreatePortrait(controller._overlayPanel.transform);
        controller._tapCatcher = CreateTapCatcher(controllerObject.transform);
        controller._tapCatcher.onClick.AddListener(controller.OnTapCatcherPressed);

        controller._overlayPanel.SetActive(false);
        controller.SetTapCatcherActive(false);
        return controller;
    }

    private static Canvas FindTutorialCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == "TutorialCanvas")
                return canvas;
        }

        return null;
    }

    private static GameObject CreateOverlay(Transform parent)
    {
        GameObject panel = new("DialogueOverlay", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.145f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.04f, 0.035f, 0.03f, 0.92f);
        image.raycastTarget = false;
        return panel;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        TutorialFontProvider.ApplyTo(text);
        return text;
    }

    private static Image CreatePortrait(Transform parent)
    {
        GameObject portraitObject = new("PortraitImage", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(parent, false);
        RectTransform rect = portraitObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.03f, 0.16f);
        rect.anchorMax = new Vector2(0.13f, 0.86f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = portraitObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        portraitObject.SetActive(false);
        return image;
    }

    private static Button CreateTapCatcher(Transform parent)
    {
        GameObject buttonObject = new("DialogueTapCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Stretch(buttonObject.GetComponent<RectTransform>());

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        return buttonObject.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Awake()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);

        // The full-screen tap-catcher is a SIBLING of _overlayPanel, so toggling the panel
        // never turns it off. Manage it explicitly here so it can't sit active (and block
        // drawing input via IsScreenPositionOverUI) while no dialogue is showing.
        SetTapCatcherActive(false);
    }

    private void SetTapCatcherActive(bool active)
    {
        if (_tapCatcher != null)
            _tapCatcher.gameObject.SetActive(active);
    }

    private void OnEnable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.AddListener(OnTapCatcherPressed);
    }

    private void OnDisable()
    {
        if (_tapCatcher != null)
            _tapCatcher.onClick.RemoveListener(OnTapCatcherPressed);
    }

    public void Play(DialogueSO dialogue)
    {
        if (!gameObject.activeInHierarchy)
        {
            DialogueController runtime = CreateRuntime();
            runtime.Play(dialogue);
            return;
        }

        if (_currentDialogue != null) return;

        if (GameManager.Instance == null ||
            (GameManager.Instance.CurrentState != GameState.Playing
                && GameManager.Instance.CurrentState != GameState.LevelComplete)) return;

        if (dialogue == null || dialogue.lines == null || dialogue.lines.Length == 0) return;

        _currentDialogue = dialogue;
        _lineIndex = 0;

        if (_overlayPanel != null)
        {
            _overlayPanel.SetActive(true);
            EnsureOverlayOnTop();
        }
        SetTapCatcherActive(true);

        GameManager.Instance.EnterDialoguePause();

        EventBus.RaiseDialogueStarted();

        ShowLine(_currentDialogue.lines[0]);
    }

    // Promote the dialogue overlay's subtree to its own Canvas layered above the spotlight
    // dim so the panel, text, and tap/Next button stay fully visible during enemy highlights.
    private void EnsureOverlayOnTop()
    {
        if (_overlayPanel == null) return;

        Canvas canvas = _overlayPanel.GetComponent<Canvas>();
        if (canvas == null)
            canvas = _overlayPanel.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        // A nested overrideSorting canvas needs its own raycaster for the tap-catcher/buttons.
        if (_overlayPanel.GetComponent<GraphicRaycaster>() == null)
            _overlayPanel.AddComponent<GraphicRaycaster>();
    }

    private void ShowLine(DialogueLine line)
    {
        if (_speakerText != null)
            _speakerText.text = line.speakerName ?? "";

        if (_portraitImage != null)
        {
            if (line.portrait != null)
            {
                _portraitImage.sprite = line.portrait;
                _portraitImage.gameObject.SetActive(true);
            }
            else
            {
                _portraitImage.gameObject.SetActive(false);
            }
        }

        if (_typewriterRoutine != null)
            StopCoroutine(_typewriterRoutine);

        _typewriterRoutine = StartCoroutine(TypewriterRoutine(line.text ?? ""));
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        _isTypewriting = true;

        if (_bodyText == null)
        {
            _isTypewriting = false;
            yield break;
        }

        _bodyText.text = "";

        if (fullText.Length == 0)
        {
            _isTypewriting = false;
            yield break;
        }

        float delay = 1f / Mathf.Max(_charsPerSecond, 0.1f);

        for (int i = 0; i < fullText.Length; i++)
        {
            _bodyText.text = fullText.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(delay);
        }

        _isTypewriting = false;
    }

    private void OnTapCatcherPressed()
    {
        if (_currentDialogue == null) return;

        if (_isTypewriting)
        {
            SkipTypewriter();
            return;
        }

        _lineIndex++;

        if (_lineIndex < _currentDialogue.lines.Length)
        {
            ShowLine(_currentDialogue.lines[_lineIndex]);
        }
        else
        {
            EndDialogue();
        }
    }

    private void SkipTypewriter()
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        _isTypewriting = false;

        if (_bodyText != null && _currentDialogue != null &&
            _lineIndex < _currentDialogue.lines.Length)
        {
            _bodyText.text = _currentDialogue.lines[_lineIndex].text ?? "";
        }
    }

    private void EndDialogue()
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }

        _isTypewriting = false;
        _currentDialogue = null;
        _lineIndex = 0;

        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
        SetTapCatcherActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.ExitDialoguePause();

        EventBus.RaiseDialogueComplete();
    }
}
