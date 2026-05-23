using System.Collections;
using TMPro;
using UnityEngine;

public class WaveDisplay : MonoBehaviour
{
    [Header("Wave Display")]
    [SerializeField] private TMP_Text _waveText;
    [SerializeField] private float _waveSlideDistance = 60f;
    [SerializeField] private float _waveSlideDuration = 0.35f;

    private Vector3 _waveBasePos;

    private void Awake()
    {
        if (_waveText != null)
        {
            _waveBasePos = _waveText.transform.localPosition;
            _waveText.text = string.Empty;
        }
    }

    private void OnEnable()
    {
        EventBus.OnWaveStarted += UpdateWave;
        EventBus.OnBossStarted += HandleBossStarted;
    }

    private void OnDisable()
    {
        EventBus.OnWaveStarted -= UpdateWave;
        EventBus.OnBossStarted -= HandleBossStarted;
    }

    private void UpdateWave(int waveIndex)
    {
        if (_waveText == null) return;
        _waveText.gameObject.SetActive(true);
        _waveText.text = $"Wave {waveIndex + 1}";
        StartCoroutine(WaveSlideAnimation());
    }

    private void HandleBossStarted(BossConfigSO _)
    {
        if (_waveText == null) return;
        _waveText.gameObject.SetActive(false);
    }

    private IEnumerator WaveSlideAnimation()
    {
        Vector3 startPos = _waveBasePos + Vector3.right * _waveSlideDistance;
        float elapsed = 0f;

        while (elapsed < _waveSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / _waveSlideDuration;
            t = 1f - (1f - t) * (1f - t);
            _waveText.transform.localPosition = Vector3.Lerp(startPos, _waveBasePos, t);
            yield return null;
        }

        _waveText.transform.localPosition = _waveBasePos;
    }
}