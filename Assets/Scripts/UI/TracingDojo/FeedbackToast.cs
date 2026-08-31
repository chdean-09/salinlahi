using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class FeedbackToast : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _verdictLabel;

    // Was _confidenceLabel, which printed the recognizer score as a percentage (SALIN-163 AC1).
    // FormerlySerializedAs keeps the existing binding in TracingDojo.unity attached through the
    // rename; dropping it would silently leave the label unassigned in the scene.
    [FormerlySerializedAs("_confidenceLabel")]
    [SerializeField] private TMP_Text _encouragementLabel;
    [SerializeField] private float _holdSeconds = 1.5f;
    [SerializeField] private float _fadeSeconds = 0.2f;

    private static readonly Color PassColor = new(0.20f, 0.55f, 0.25f);
    private static readonly Color FailColor = new(0.70f, 0.20f, 0.20f);

    private Coroutine _running;

    private void Awake()
    {
        _group.alpha = 0f;
    }

    /// <summary>
    /// SALIN-163 AC1. The recognizer score is not a parameter: the toast used to render it as
    /// "83%", grading the player against an internal threshold instead of telling them what to
    /// change. Taking it out of the signature is what keeps it out of the UI for good.
    /// </summary>
    public void Show(string characterID, bool pass)
    {
        _verdictLabel.text = characterID;
        _verdictLabel.color = pass ? PassColor : FailColor;
        _encouragementLabel.text = pass
            ? DrawingFeedbackVocabulary.Accepted
            : DrawingFeedbackVocabulary.RejectedFirstAttempt;

        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(FadeCycle());
    }

    private IEnumerator FadeCycle()
    {
        yield return Fade(0f, 1f, _fadeSeconds);
        yield return new WaitForSeconds(_holdSeconds);
        yield return Fade(1f, 0f, _fadeSeconds);
        _running = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        _group.alpha = to;
    }
}
