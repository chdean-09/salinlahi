using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button _endlessModeButton;

    private void Start()
    {
        if (_endlessModeButton != null)
            _endlessModeButton.interactable = IsStoryComplete();
    }

    public void OnPlayButtonPressed()
    {
        DebugLogger.Log("MainMenuUI: Play button pressed");
        // Default to Level 1 when pressing Play
        PlayerPrefs.SetInt("SelectedLevel", 1);
        PlayerPrefs.Save();
        SceneLoader.Instance.LoadGameplay();
    }

    public void OnEndlessModePressed()
    {
        if (!IsStoryComplete())
        {
            DebugLogger.LogWarning("MainMenuUI: Endless Mode is locked until story is complete.");
            return;
        }

        SceneLoader.Instance.LoadGameplay();
    }

    public void OnTracingDojoPressed()
    {
        DebugLogger.Log("MainMenuUI: Tracing Dojo pressed (not yet implemented)");
    }

    public void OnSettingsPressed()
    {
        DebugLogger.Log("MainMenuUI: Settings pressed (not yet implemented)");
    }

    // TODO: Replace with actual story progression check when save system is implemented
    private bool IsStoryComplete()
    {
        return false;
    }
}
