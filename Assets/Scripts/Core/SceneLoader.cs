using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : Singleton<SceneLoader>
{
    // Keep scene names here. If a scene is renamed, fix it in one place.
    private const string SCENE_BOOTSTRAP = "Bootstrap";
    private const string SCENE_MAIN_MENU = "MainMenu";
    private const string SCENE_GAMEPLAY = "Gameplay";
    private const string SCENE_LEVEL_SELECT = "LevelSelect";
    private const string SCENE_GAME_OVER = "GameOver";

    protected override void Awake() => base.Awake();

    public void LoadMainMenu() => StartCoroutine(LoadRoutine(SCENE_MAIN_MENU));
    public void LoadGameplay() => StartCoroutine(LoadRoutine(SCENE_GAMEPLAY));
    public void LoadLevelSelect() => StartCoroutine(LoadRoutine(SCENE_LEVEL_SELECT));
    public void LoadGameOver() => StartCoroutine(LoadRoutine(SCENE_GAME_OVER));

    public void ReloadCurrentScene()
    {
        string current = SceneManager.GetActiveScene().name;
        StartCoroutine(LoadRoutine(current));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        Time.timeScale = 1f; // Always reset before scene change
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            DebugLogger.Log($"Loading {sceneName}: {(op.progress * 100f):F0}%");
            yield return null;
        }
    }
}
