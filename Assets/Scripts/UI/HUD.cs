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
    [Header("Combo Display")]
    [SerializeField] private TMP_Text _streakText;
    [SerializeField] private GameObject _focusModeIndicator;

    private void OnEnable()
    {
        EventBus.OnHeartsChanged += UpdateHearts;
        EventBus.OnWaveStarted += UpdateWave;
        EventBus.OnComboChanged += UpdateStreakDisplay;
        EventBus.OnFocusModeActivated += ShowFocusIndicator;
        EventBus.OnFocusModeDeactivated += HideFocusIndicator;

    }
    private void OnDisable()
    {
        EventBus.OnHeartsChanged -= UpdateHearts;
        EventBus.OnWaveStarted -= UpdateWave;
        EventBus.OnComboChanged -= UpdateStreakDisplay;
        EventBus.OnFocusModeActivated -= ShowFocusIndicator;
        EventBus.OnFocusModeDeactivated -= HideFocusIndicator;

    }
    private void UpdateHearts(int current)
    {
        if (_heartText != null) _heartText.text = $"Hearts: {current}";
    }
    private void UpdateWave(int waveIndex)
    {
        if (_waveText != null) _waveText.text = $"Wave {waveIndex + 1}";
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
    }

    private void ShowFocusIndicator()
    {
        if (_focusModeIndicator != null)
            _focusModeIndicator.SetActive(true);
    }

    private void HideFocusIndicator()
    {
        if (_focusModeIndicator != null)
            _focusModeIndicator.SetActive(false);
    }

}