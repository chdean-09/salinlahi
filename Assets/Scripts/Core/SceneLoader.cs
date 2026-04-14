using System.Collections;
using Salinlahi.Debug.Sandbox;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : Singleton<SceneLoader>
{
    // Keep scene names here. If a scene is renamed, fix it in one place.
    private const string SCENE_BOOTSTRAP = "Bootstrap";
    private const string SCENE_MAIN_MENU = "MainMenu";
    private const string SCENE_GAMEPLAY = "Gameplay";
    private const string SCENE_LEVEL_SELECT = "LevelSelect";
    private const string SCENE_GAME_OVER = "GameOver";

    [Header("Fade Stub (placeholder for SALIN-44 TransitionManager)")]
    [SerializeField] private CanvasGroup _fadeCanvasGroup;
    [SerializeField] private float _fadeDuration = 0.25f;

    private bool _isLoading;
    private string _loadingSceneName;

    protected override void Awake()
    {
        base.Awake();

        // Only the surviving singleton instance should build the fade canvas.
        if (Instance != this) return;

        _fadeCanvasGroup ??= CreateFadeCanvas();
    }

    // Unified internal entry point. Convenience wrappers below all funnel here
    // so the in-progress guard lives in exactly one place.
    public void LoadScene(string sceneName)
    {
        if (_isLoading)
        {
            DebugLogger.LogWarning(
                $"SceneLoader: Ignored LoadScene({sceneName}) — already loading '{_loadingSceneName}'.");
            return;
        }
        StartCoroutine(LoadRoutine(sceneName));
    }

    public void LoadMainMenu()
    {
        SandboxMode.Deactivate();
        LoadScene(SCENE_MAIN_MENU);
    }

    public void LoadGameplay()
    {
        SandboxMode.Deactivate();
        LoadScene(SCENE_GAMEPLAY);
    }

    public void LoadSandboxGameplay()
    {
        if (!SandboxMode.TryActivate())
        {
            DebugLogger.LogWarning("SceneLoader: Sandbox gameplay is not available in this build.");
            return;
        }

        LoadScene(SCENE_GAMEPLAY);
    }

    public void LoadLevelSelect()
    {
        SandboxMode.Deactivate();
        LoadScene(SCENE_LEVEL_SELECT);
    }

    public void LoadGameOver()
    {
        if (SandboxMode.IsActive)
        {
            DebugLogger.Log("SceneLoader: Ignored GameOver scene load while sandbox mode is active.");
            return;
        }

        LoadScene(SCENE_GAME_OVER);
    }

    public void ReloadCurrentScene() => LoadScene(SceneManager.GetActiveScene().name);

    private IEnumerator LoadRoutine(string sceneName)
    {
        _isLoading = true;
        _loadingSceneName = sceneName;
        Time.timeScale = 1f; // Always reset before scene change

        // try/finally: yield IS valid inside try. finally runs even if the
        // coroutine is stopped externally (StopAllCoroutines, object destroy, etc.)
        // so _isLoading never gets stuck true and the screen never stays black.
        try
        {
            // Fade in (to black). Stub — replaced by TransitionManager in SALIN-44.
            yield return Fade(0f, 1f);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                DebugLogger.LogError($"SceneLoader: Scene '{sceneName}' not found in Build Profiles. Add it via File → Build Profiles.");
                yield return Fade(1f, 0f);
                yield break;
            }

            op.allowSceneActivation = true; // Fade stub must NOT gate scene activation.

            while (!op.isDone)
            {
                // Progress stops at 0.9 (90%) until scene activation.
                float progress = Mathf.Clamp01(op.progress / 0.9f);
                DebugLogger.Log($"Loading {sceneName}: {progress * 100f:F0}%");
                yield return null;
            }

            DebugLogger.Log($"Loading {sceneName}: Complete");

            // Fade out (from black).
            yield return Fade(1f, 0f);
        }
        finally
        {
            // Cannot yield here, so snap alpha to 0 instantly as a safety net.
            // Under normal flow Fade(1f, 0f) already animated it; this only
            // matters if the coroutine was interrupted before the fade-back ran.
            // ReSharper disable once Unity.NoNullPropagation — != null intentional (Unity overrides ==)
            if (_fadeCanvasGroup != null)
                _fadeCanvasGroup.alpha = 0f;

            _isLoading = false;
            _loadingSceneName = null;
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        if (_fadeCanvasGroup == null) yield break;

        _fadeCanvasGroup.alpha = from;
        float t = 0f;
        while (t < _fadeDuration)
        {
            // unscaledDeltaTime: still fades if gameplay paused Time.timeScale.
            t += Time.unscaledDeltaTime;
            _fadeCanvasGroup.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
            yield return null;
        }
        _fadeCanvasGroup.alpha = to;
    }

    // Builds a full-screen black overlay CanvasGroup at runtime so any scene
    // entered directly in the Editor still has a working fade target.
    private CanvasGroup CreateFadeCanvas()
    {
        var go = new GameObject("SceneLoaderFadeCanvas");
        go.transform.SetParent(transform, worldPositionStays: false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Render above all gameplay UI.

        go.AddComponent<CanvasScaler>();
        // GraphicRaycaster intentionally omitted — the stub must not eat input.

        var imageGo = new GameObject("FadeImage");
        imageGo.transform.SetParent(go.transform, worldPositionStays: false);
        var rect = imageGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = imageGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }
}
