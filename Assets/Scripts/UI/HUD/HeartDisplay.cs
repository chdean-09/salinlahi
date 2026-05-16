using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HeartDisplay : MonoBehaviour
{
    [Header("Heart Display")]
    [SerializeField] private Image[] _heartIcons;
    [SerializeField] private Sprite _heartFull;
    [SerializeField] private Sprite _heartEmpty;
    [SerializeField] private float _heartShakeDuration = 0.2f;
    [SerializeField] private float _heartPunchScale = 1.2f;

    private Coroutine[] _heartAnimRoutines;
    private Vector3[] _heartBaseScales;
    private int _lastHeartCount;

    private void OnEnable()
    {
        EnsureRuntimeBuffers();
        EventBus.OnHeartsChanged += UpdateHearts;

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        if (heartSystem != null)
            UpdateHearts(heartSystem.GetCurrentHearts());
    }

    private void OnDisable()
    {
        EventBus.OnHeartsChanged -= UpdateHearts;

        if (_heartIcons == null || _heartAnimRoutines == null || _heartBaseScales == null)
            return;

        for (int i = 0; i < _heartIcons.Length && i < _heartAnimRoutines.Length; i++)
        {
            if (_heartAnimRoutines[i] != null)
            {
                StopCoroutine(_heartAnimRoutines[i]);
                _heartAnimRoutines[i] = null;
            }

            if (_heartIcons[i] != null && i < _heartBaseScales.Length && _heartBaseScales[i] != Vector3.zero)
                _heartIcons[i].transform.localScale = _heartBaseScales[i];
        }
    }

    private void UpdateHearts(int current)
    {
        EnsureRuntimeBuffers();
        bool lost = current < _lastHeartCount;
        _lastHeartCount = current;

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            if (_heartIcons[i] == null) continue;

            CacheBaseScale(i);

            bool filled = i < current;
            if (_heartFull != null && _heartEmpty != null)
                _heartIcons[i].sprite = filled ? _heartFull : _heartEmpty;
            else
                _heartIcons[i].color = filled ? Color.red : new Color(1f, 1f, 1f, 0.25f);

            if (lost && i == current)
            {
                if (i < _heartAnimRoutines.Length && _heartAnimRoutines[i] != null)
                    StopCoroutine(_heartAnimRoutines[i]);

                if (i < _heartAnimRoutines.Length)
                    _heartAnimRoutines[i] = StartCoroutine(HeartLossAnimation(i, _heartIcons[i]));
            }
        }
    }

    private void CacheBaseScale(int index)
    {
        if (_heartIcons == null || index >= _heartIcons.Length)
            return;

        if (index >= _heartBaseScales.Length)
            return;

        if (_heartBaseScales[index] == Vector3.zero && _heartIcons[index] != null)
            _heartBaseScales[index] = _heartIcons[index].transform.localScale;
    }

    private IEnumerator HeartLossAnimation(int index, Image heart)
    {
        if (heart == null) yield break;

        float duration = Mathf.Max(0.05f, _heartShakeDuration);
        float elapsed = 0f;
        Vector3 baseScale = index < _heartBaseScales.Length && _heartBaseScales[index] != Vector3.zero
            ? _heartBaseScales[index]
            : heart.transform.localScale;
        Vector3 punchScale = baseScale * Mathf.Max(1f, _heartPunchScale);
        Vector3 dipScale = baseScale * 0.95f;

        while (elapsed < duration)
        {
            if (heart == null) yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (t < 0.45f)
            {
                float phase = t / 0.45f;
                heart.transform.localScale = Vector3.LerpUnclamped(baseScale, punchScale, phase);
            }
            else if (t < 0.75f)
            {
                float phase = (t - 0.45f) / 0.30f;
                heart.transform.localScale = Vector3.LerpUnclamped(punchScale, dipScale, phase);
            }
            else
            {
                float phase = (t - 0.75f) / 0.25f;
                heart.transform.localScale = Vector3.LerpUnclamped(dipScale, baseScale, phase);
            }

            yield return null;
        }

        if (heart != null)
            heart.transform.localScale = baseScale;

        if (index < _heartAnimRoutines.Length)
            _heartAnimRoutines[index] = null;
    }

    private void EnsureRuntimeBuffers()
    {
        int heartCount = _heartIcons != null ? _heartIcons.Length : 0;

        if (_heartAnimRoutines == null || _heartAnimRoutines.Length != heartCount)
            _heartAnimRoutines = new Coroutine[heartCount];

        if (_heartBaseScales == null || _heartBaseScales.Length != heartCount)
            _heartBaseScales = new Vector3[heartCount];
    }
}
