using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("Navigation")]
    [SerializeField] private Button _closeButton;

    private void OnEnable()
    {
        SyncSlidersToAudioManager();

        if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (_bgmSlider != null) _bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (_bgmSlider != null) _bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void SyncSlidersToAudioManager()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            DebugLogger.LogWarning("SettingsPanel: AudioManager.Instance not available.");
            return;
        }

        if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(audio.MasterVolume);
        if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(audio.BgmVolume);
        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
    }

    private void OnMasterChanged(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
    }

    private void OnBgmChanged(float value)
    {
        AudioManager.Instance?.SetBgmVolume(value);
    }

    private void OnSfxChanged(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
    }
}