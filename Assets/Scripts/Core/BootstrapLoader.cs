using System.Collections;
using UnityEngine;

public class BootstrapLoader : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Wait one frame so all Singleton Awake() calls finish first
        yield return null;
        DebugLogger.Log("Bootstrap complete. Loading MainMenu.");
        SceneLoader.Instance.LoadMainMenu();
    }
}
