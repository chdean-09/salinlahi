using UnityEngine;
using UnityEngine.SceneManagement;

public class DojoNavigator : MonoBehaviour
{
    private const string SceneMainMenu = "MainMenu";

    public void GoToMainMenu()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
        {
            DebugLogger.LogWarning(
                $"DojoNavigator: SceneLoader not available. Loading '{SceneMainMenu}' directly. "
                + "Open from Bootstrap for normal transitions.");
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneMainMenu);
        }
    }
}
