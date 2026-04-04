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

    private bool _isLoading = false;
    [SerializeField] private CanvasGroup _fadeCanvasGroup;
    [SerializeField] private float _fadeDuration = 0.5f;

    protected override void Awake()
    {
        base.Awake();
        
        // Create fade CanvasGroup if not assigned
        if (_fadeCanvasGroup == null)
        {
            GameObject fadeObj = new GameObject("FadeCanvasGroup");
            fadeObj.transform.SetParent(transform);
            _fadeCanvasGroup = fadeObj.AddComponent<CanvasGroup>();
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    // Internal method - all convenience wrappers route through here
    public void LoadScene(string sceneName)
    {
        if (_isLoading)
        {
            DebugLogger.LogWarning($"Scene load already in progress. Ignoring request to load: {sceneName}");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName));
    }

    // Convenience wrappers
    public void LoadMainMenu() => LoadScene(SCENE_MAIN_MENU);
    public void LoadGameplay() => LoadScene(SCENE_GAMEPLAY);
    public void LoadLevelSelect() => LoadScene(SCENE_LEVEL_SELECT);
    public void LoadGameOver() => LoadScene(SCENE_GAME_OVER);

    public void ReloadCurrentScene()
    {
        string current = SceneManager.GetActiveScene().name;
        LoadScene(current);
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        _isLoading = true;
        Time.timeScale = 1f; // Always reset before scene change

        // Fade in (alpha 0 -> 1)
        yield return StartCoroutine(FadeIn());

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            DebugLogger.Log($"Loading {sceneName}: {(op.progress * 100f):F0}%");
            yield return null;
        }

        // Fade out (alpha 1 -> 0)
        yield return StartCoroutine(FadeOut());

        _isLoading = false;
    }

    // Fade stub - alpha 0 to 1
    private IEnumerator FadeIn()
    {
        if (_fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        _fadeCanvasGroup.alpha = 0f;
        _fadeCanvasGroup.blocksRaycasts = true;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeDuration);
            yield return null;
        }

        _fadeCanvasGroup.alpha = 1f;
    }

    // Fade stub - alpha 1 to 0
    private IEnumerator FadeOut()
    {
        if (_fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        _fadeCanvasGroup.alpha = 1f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _fadeCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeDuration);
            yield return null;
        }

        _fadeCanvasGroup.alpha = 0f;
        _fadeCanvasGroup.blocksRaycasts = false;
    }
}
