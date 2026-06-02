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
