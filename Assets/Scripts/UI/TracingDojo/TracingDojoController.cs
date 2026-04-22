using UnityEngine;

public class TracingDojoController : MonoBehaviour
{
    [SerializeField] private GhostStrokeRenderer _ghost;
    [SerializeField] private FeedbackToast _toast;
    [SerializeField] private CharacterDropdown _dropdown;
    [SerializeField] private CharacterRegistrySO _registry;

    private BaybayinCharacterSO _selected;
    private bool _previousLoggingEnabled;

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += OnResolved;
        _previousLoggingEnabled = RecognitionLogger.LoggingEnabled;
        RecognitionLogger.LoggingEnabled = false;
        if (GameManager.Instance != null) GameManager.Instance.EnterPractice();
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= OnResolved;
        RecognitionLogger.LoggingEnabled = _previousLoggingEnabled;
        if (GameManager.Instance != null) GameManager.Instance.ExitPractice();
    }

    public void SelectCharacter(BaybayinCharacterSO character)
    {
        _selected = character;
        _ghost.Render(character);
        _dropdown.SetCurrentCharacter(character);
        _dropdown.Close();
    }

    private void OnResolved(RecognitionResult result, bool passedThreshold, float threshold)
    {
        bool matchesSelected = _selected == null || result.characterID == _selected.characterID;
        bool pass = passedThreshold && matchesSelected;

        _toast.Show(result.characterID, result.score, pass);

        if (pass && _selected != null && _selected.pronunciationClip != null)
        {
            AudioManager.Instance.PlaySFX(_selected.pronunciationClip);
        }
    }
}
