using UnityEngine;

// Placeholder for SALIN-23: satisfies AC-1/AC-2 by providing a valid LevelSelect scene destination.
// Full level grid with lock/unlock/complete states is implemented in SALIN-43.
public class LevelSelectUI : MonoBehaviour
{
    private void Start()                                                   
    {                                                               
        SceneLoader.Instance.LoadGameplay();                               
    }      

    public void OnBackButtonPressed()
    {
        SceneLoader.Instance.LoadMainMenu();
    }
}
