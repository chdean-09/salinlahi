using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    public void OnRetryPressed()
    {
        DebugLogger.Log("GameOverUI: Retry pressed (stub)");
        SceneLoader.Instance.LoadGameplay();
    }

    public void OnMainMenuPressed()
    {
        SceneLoader.Instance.LoadMainMenu();
    }
}
