using System.Collections;
using UnityEngine;

public class BootstrapLoader : MonoBehaviour
{
    private IEnumerator Start()
    {
#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = 60;
#endif

        // Wait one frame so all Singleton Awake() calls finish first
        yield return null;
        SaveManager.Instance?.Initialize();
        DebugLogger.Log("Bootstrap complete. Loading MainMenu.");
        SceneLoader.Instance.LoadMainMenu();
    }
}
