using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    // Sprint 1: wire up Play button to GameManager.StartGame() + SceneLoader.LoadGameplay()
    public void OnPlayButtonPressed()
    {
        DebugLogger.Log("MainMenuUI: Play button pressed (stub)");
        GameManager.Instance.StartGame();
        SceneLoader.Instance.LoadGameplay();
    }
}
