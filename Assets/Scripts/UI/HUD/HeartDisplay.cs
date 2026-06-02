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
    private bool[] _heartScaleCached;
    private int _lastHeartCount;

    private int _tutorialDemoEmptiedIndex = -1;

    private void OnEnable()
    {
        EnsureRuntimeBuffers();
        EventBus.OnHeartsChanged += UpdateHearts;
        EventBus.OnTutorialBaseHitDemo += HandleTutorialDemoHit;
        EventBus.OnTutorialBaseRestoreDemo += HandleTutorialDemoRestore;

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        if (heartSystem != null)
            UpdateHearts(heartSystem.GetCurrentHearts());
    }

    private void OnDisable()
    {
        EventBus.OnHeartsChanged -= UpdateHearts;
        EventBus.OnTutorialBaseHitDemo -= HandleTutorialDemoHit;
        EventBus.OnTutorialBaseRestoreDemo -= HandleTutorialDemoRestore;

        if (_heartIcons == null || _heartAnimRoutines == null || _heartBaseScales == null)
            return;

        for (int i = 0; i < _heartIcons.Length && i < _heartAnimRoutines.Length; i++)
        {
            if (_heartAnimRoutines[i] != null)
            {
                StopCoroutine(_heartAnimRoutines[i]);
                _heartAnimRoutines[i] = null;
            }

            if (_heartIcons[i] != null && i < _heartBaseScales.Length && i < _heartScaleCached.Length && _heartScaleCached[i])
                _heartIcons[i].transform.localScale = _heartBaseScales[i];
        }
    }

    private void UpdateHearts(int current)
    {
        EnsureRuntimeBuffers();
        int previous = _lastHeartCount;
        bool lost = current < previous;
        _lastHeartCount = current;

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            if (_heartIcons[i] == null) continue;

            CacheBaseScale(i);

            bool filled = i < current;
            ApplyHeartVisual(_heartIcons[i], filled);

            if (lost && i >= current && i < previous)
            {
                if (i < _heartAnimRoutines.Length && _heartAnimRoutines[i] != null)
                    StopCoroutine(_heartAnimRoutines[i]);

                if (i < _heartAnimRoutines.Length)
                    _heartAnimRoutines[i] = StartCoroutine(HeartLossAnimation(i, _heartIcons[i]));
            }
        }
    }

    private void HandleTutorialDemoHit(int _)
    {
        EnsureRuntimeBuffers();
        if (_heartIcons == null) return;

        int targetIndex = -1;
        for (int i = _heartIcons.Length - 1; i >= 0; i--)
        {
            if (_heartIcons[i] != null && i < _lastHeartCount)
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex < 0) return;

        CacheBaseScale(targetIndex);
        _tutorialDemoEmptiedIndex = targetIndex;

        // Empty the heart and keep it empty — the restore is now an explicit, explained
        // step (HandleTutorialDemoRestore), not a silent timed snap-back.
        ApplyHeartVisual(_heartIcons[targetIndex], filled: false);

        if (targetIndex < _heartAnimRoutines.Length && _heartAnimRoutines[targetIndex] != null)
            StopCoroutine(_heartAnimRoutines[targetIndex]);
        if (targetIndex < _heartAnimRoutines.Length)
            _heartAnimRoutines[targetIndex] = StartCoroutine(HeartLossAnimation(targetIndex, _heartIcons[targetIndex]));
    }

    // Tutorial-only: visibly refill the heart the demo hit emptied, with a pulse so the
    // restoration reads as intentional rather than a sudden snap-back.
    private void HandleTutorialDemoRestore()
    {
        EnsureRuntimeBuffers();
        int index = _tutorialDemoEmptiedIndex;
        _tutorialDemoEmptiedIndex = -1;
        if (_heartIcons == null || index < 0 || index >= _heartIcons.Length || _heartIcons[index] == null)
            return;

        ApplyHeartVisual(_heartIcons[index], filled: true);

        CacheBaseScale(index);
        if (index < _heartAnimRoutines.Length && _heartAnimRoutines[index] != null)
            StopCoroutine(_heartAnimRoutines[index]);
        if (index < _heartAnimRoutines.Length)
            _heartAnimRoutines[index] = StartCoroutine(HeartRestorePulse(index, _heartIcons[index]));
    }

    private IEnumerator HeartRestorePulse(int index, Image heart)
    {
        if (heart == null) yield break;
        Vector3 baseScale = index < _heartBaseScales.Length && _heartScaleCached[index]
            ? _heartBaseScales[index] : heart.transform.localScale;

        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (heart == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.LerpUnclamped(1.45f, 1f, t);
            heart.transform.localScale = baseScale * scale;
            yield return null;
        }
        if (heart != null) heart.transform.localScale = baseScale;
        if (index < _heartAnimRoutines.Length) _heartAnimRoutines[index] = null;
    }

    private void CacheBaseScale(int index)
    {
        if (_heartIcons == null || index >= _heartIcons.Length)
            return;

        if (index >= _heartBaseScales.Length)
            return;

        if (!_heartScaleCached[index] && _heartIcons[index] != null)
        {
            _heartBaseScales[index] = _heartIcons[index].transform.localScale;
            _heartScaleCached[index] = true;
        }
    }

    private void ApplyHeartVisual(Image heart, bool filled)
    {
        if (heart == null)
            return;

        if (_heartFull != null && _heartEmpty != null)
        {
            heart.sprite = filled ? _heartFull : _heartEmpty;
            return;
        }

        heart.color = filled ? Color.red : new Color(1f, 1f, 1f, 0.25f);
    }

    private IEnumerator HeartLossAnimation(int index, Image heart)
    {
        if (heart == null) yield break;

        float duration = Mathf.Max(0.05f, _heartShakeDuration);
        float elapsed = 0f;
        Vector3 baseScale = index < _heartBaseScales.Length && index < _heartScaleCached.Length && _heartScaleCached[index]
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

        if (_heartScaleCached == null || _heartScaleCached.Length != heartCount)
            _heartScaleCached = new bool[heartCount];
    }
}
