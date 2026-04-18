using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Pause")]
    [SerializeField] private Button _pauseButton;

    private void OnEnable()
    {
        if (_pauseButton != null)
            _pauseButton.onClick.AddListener(OnPausePressed);
    }

    private void OnDisable()
    {
        if (_pauseButton != null)
            _pauseButton.onClick.RemoveListener(OnPausePressed);
    }

    private void OnPausePressed()
    {
        GameManager.Instance.PauseGame();
    }
}