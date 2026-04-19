using System.Collections;
using TMPro;
using UnityEngine;

public class ComboDisplay : MonoBehaviour
{
    [Header("Combo Display")]
    [SerializeField] private TMP_Text _streakText;
    [SerializeField] private float _comboPopScale = 1.4f;
    [SerializeField] private float _comboPopDuration = 0.15f;

    private Vector3 _streakBaseScale;

    private void Awake()
    {
        if (_streakText != null)
        {
            _streakBaseScale = _streakText.transform.localScale;
            _streakText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EventBus.OnComboChanged += UpdateStreakDisplay;
    }

    private void OnDisable()
    {
        EventBus.OnComboChanged -= UpdateStreakDisplay;
    }

    private void UpdateStreakDisplay(int streak)
    {
        if (_streakText == null) return;

        if (streak <= 0)
        {
            _streakText.gameObject.SetActive(false);
            return;
        }

        _streakText.gameObject.SetActive(true);
        _streakText.text = $"x{streak}";
        StartCoroutine(ComboPopAnimation());
    }

    private IEnumerator ComboPopAnimation()
    {
        Vector3 targetScale = _streakBaseScale * _comboPopScale;
        float half = _comboPopDuration / 2f;

        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            _streakText.transform.localScale = Vector3.Lerp(_streakBaseScale, targetScale, elapsed / half);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            _streakText.transform.localScale = Vector3.Lerp(targetScale, _streakBaseScale, elapsed / half);
            yield return null;
        }

        _streakText.transform.localScale = _streakBaseScale;
    }
}