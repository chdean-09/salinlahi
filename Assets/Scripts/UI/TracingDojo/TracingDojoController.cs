using TMPro;
using UnityEngine;

public class TracingDojoController : MonoBehaviour
{
    [SerializeField] private GhostStrokeRenderer _ghost;
    [SerializeField] private TMP_Text _verdictLabel;
    [SerializeField] private TMP_Text _confidenceLabel;
    [SerializeField] private CharacterRegistrySO _registry;

    private BaybayinCharacterSO _selected;

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += OnResolved;
        RecognitionLogger.LoggingEnabled = false; // practice attempts are not logged
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= OnResolved;
        RecognitionLogger.LoggingEnabled = true;
    }

    public void SelectCharacter(BaybayinCharacterSO character)
    {
        _selected = character;
        _ghost.Render(character);
        _verdictLabel.text = string.Empty;
        _confidenceLabel.text = string.Empty;
    }

    private void OnResolved(RecognitionResult result, bool passedThreshold, float threshold)
    {
        RenderFeedback(result.characterID, result.score, passedThreshold);

        if (passedThreshold
            && _selected != null
            && result.characterID == _selected.characterID
            && _selected.pronunciationClip != null)
        {
            AudioManager.Instance.PlaySFX(_selected.pronunciationClip);
        }
    }

    private void RenderFeedback(string characterID, float score, bool pass)
    {
        _confidenceLabel.text = $"{score * 100f:F0}%";
        _verdictLabel.text = pass
            ? $"✓  {characterID}"
            : $"✗  Try again ({characterID}?)";
        _verdictLabel.color = pass
            ? new Color(0.20f, 0.55f, 0.25f)
            : new Color(0.70f, 0.20f, 0.20f);
    }
}
