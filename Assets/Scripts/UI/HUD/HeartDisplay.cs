using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HeartDisplay : MonoBehaviour
{
    [Header("Heart Display")]
    [SerializeField] private Image[] _heartIcons;
    [SerializeField] private Sprite _heartFull;
    [SerializeField] private Sprite _heartEmpty;
    [SerializeField] private float _heartShakeDuration = 0.3f;
    [SerializeField] private float _heartShakeMagnitude = 5f;

    private int _lastHeartCount;

    private void OnEnable()
    {
        EventBus.OnHeartsChanged += UpdateHearts;

        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        if (heartSystem != null)
            UpdateHearts(heartSystem.GetCurrentHearts());
    }

    private void OnDisable()
    {
        EventBus.OnHeartsChanged -= UpdateHearts;
    }

    private void UpdateHearts(int current)
    {
        bool lost = current < _lastHeartCount;
        _lastHeartCount = current;

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            if (_heartIcons[i] == null) continue;

            bool filled = i < current;
            if (_heartFull != null && _heartEmpty != null)
                _heartIcons[i].sprite = filled ? _heartFull : _heartEmpty;
            else
                _heartIcons[i].color = filled ? Color.red : new Color(1f, 1f, 1f, 0.25f);

            if (lost && i == current)
                StartCoroutine(HeartLossAnimation(_heartIcons[i]));
        }
    }

    private IEnumerator HeartLossAnimation(Image heart)
    {
        if (heart == null) yield break;
        Vector3 originalPos = heart.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < _heartShakeDuration)
        {
            if (heart == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float x = Random.Range(-1f, 1f) * _heartShakeMagnitude;
            float y = Random.Range(-1f, 1f) * _heartShakeMagnitude;
            heart.transform.localPosition = originalPos + new Vector3(x, y, 0f);
            yield return null;
        }

        if (heart != null)
            heart.transform.localPosition = originalPos;
    }
}