using UnityEngine;

public class TracingDojoController : MonoBehaviour
{
    [SerializeField] private GhostStrokeRenderer _ghost;
    [SerializeField] private FeedbackToast _toast;
    [SerializeField] private CharacterDropdown _dropdown;
    [SerializeField] private CharacterRegistrySO _registry;

    private BaybayinCharacterSO _selected;

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += OnResolved;
        RecognitionLogger.LoggingEnabled = false;
        if (GameManager.Instance != null) GameManager.Instance.EnterPractice();
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= OnResolved;
        RecognitionLogger.LoggingEnabled = true;
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
        _toast.Show(result.characterID, result.score, passedThreshold);

        if (passedThreshold
            && _selected != null
            && result.characterID == _selected.characterID
            && _selected.pronunciationClip != null)
        {
            AudioManager.Instance.PlaySFX(_selected.pronunciationClip);
        }
    }
}
