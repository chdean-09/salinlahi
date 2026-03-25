using TMPro;
using UnityEngine;
// Sprint 1 stub: displays heart count as a number.
// Sprint 2: replace with actual heart icons and wave counter.
public class HUD : MonoBehaviour
{
    [Header("Heart Display")]
    [SerializeField] private TextMeshProUGUI _heartText;
    [Header("Wave Display")]
    [SerializeField] private TextMeshProUGUI _waveText;
    private void OnEnable()
    {
        EventBus.OnHeartsChanged += UpdateHearts;
        EventBus.OnWaveStarted += UpdateWave;
    }
    private void OnDisable()
    {
        EventBus.OnHeartsChanged -= UpdateHearts;
        EventBus.OnWaveStarted -= UpdateWave;
    }
    private void UpdateHearts(int current)
    {
        if (_heartText != null) _heartText.text = $"Hearts: {current}";
    }
    private void UpdateWave(int waveIndex)
    {
        if (_waveText != null) _waveText.text = $"Wave {waveIndex + 1}";
    }
}