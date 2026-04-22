using System.Collections;
using TMPro;
using UnityEngine;

public class FeedbackToast : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TMP_Text _verdictLabel;
    [SerializeField] private TMP_Text _confidenceLabel;
    [SerializeField] private float _holdSeconds = 1.5f;
    [SerializeField] private float _fadeSeconds = 0.2f;

    private static readonly Color PassColor = new(0.20f, 0.55f, 0.25f);
    private static readonly Color FailColor = new(0.70f, 0.20f, 0.20f);

    private Coroutine _running;

    private void Awake()
    {
        _group.alpha = 0f;
    }

    public void Show(string characterID, float score, bool pass)
    {
        _verdictLabel.text = characterID;
        _verdictLabel.color = pass ? PassColor : FailColor;
        _confidenceLabel.text = $"{score * 100f:F0}%";

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
