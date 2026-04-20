using UnityEngine;

public class DojoNavigator : MonoBehaviour
{
    public void GoToMainMenu()
    {
        SceneLoader.Instance.LoadScene("MainMenu");
    }
}
