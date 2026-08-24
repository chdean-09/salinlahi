using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// SALIN-141 PlayMode coverage for the pause / restart / leave lifecycle.
    ///
    /// PlayMode, not EditMode: every behavior here depends on Awake/OnEnable actually
    /// running so the EventBus subscriptions exist, and on frames elapsing so the
    /// unscaled stroke timers get a chance to misbehave.
    ///
    /// Scene loading is neutralised by parking SceneLoader's own in-progress guard, so
    /// the transition can be asserted up to the abort without tearing down the test
    /// runner's scene.
    /// </summary>
    [TestFixture]
    public sealed class PauseLifecycleTests
    {
        // Long enough that LoadRoutine's fade never finishes inside a test, short of
        // float.PositiveInfinity so the Lerp inside Fade stays well defined.
        private const float ParkedFadeSeconds = 600f;

        private readonly List<Object> _objectsToDestroy = new();
        private int _abortRaises;
        private int _resumeRaises;
        private int _pauseRaises;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<SceneLoader>();
            ClearSingletonInstance<ComboManager>();
            _abortRaises = 0;
            _resumeRaises = 0;
            _pauseRaises = 0;
            EventBus.OnLevelAttemptAborted += CountAbort;
            EventBus.OnGameResumed += CountResume;
            EventBus.OnGamePaused += CountPause;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnLevelAttemptAborted -= CountAbort;
            EventBus.OnGameResumed -= CountResume;
            EventBus.OnGamePaused -= CountPause;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<SceneLoader>();
            ClearSingletonInstance<ComboManager>();
            Time.timeScale = 1f;
        }

        private void CountAbort() => _abortRaises++;
        private void CountResume() => _resumeRaises++;
        private void CountPause() => _pauseRaises++;

        // ------------------------------------------------------------------
        // AC-1 — combat, prompts, drawing input and gameplay timers stop together
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Pause_StopsTheClockAndDrawingInputTogether()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();

            gameManager.PauseGame();
            yield return null;

            Assert.AreEqual(1, _pauseRaises, "Exactly one pause signal must reach subscribers.");
            Assert.AreEqual(0f, Time.timeScale, "Combat and gameplay timers ride Time.timeScale.");
            Assert.IsFalse(gameManager.AcceptsDrawingInput,
                "Drawing input must close in the same beat as the clock.");
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTheUnscaledStrokeTimers()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            StrokeCapture capture = CreateStrokeCapture();

            gameManager.PauseGame();

            // Arm the multi-stroke window AFTER the pause: HandleGamePaused already parks
            // a window armed before it, so only Update's own guard can stop this one.
            // Both timers run on unscaledTime, which Time.timeScale = 0 does not touch.
            //
            // The deadline has to sit in the PAST, or the assertion proves nothing: an
            // end time still ahead of the clock would survive whether the guard exists or
            // not. Clamped above zero because Update treats <= 0 as "disarmed".
            double armedEndTime = System.Math.Max(0.0001d, Time.unscaledTimeAsDouble - 1d);
            Assert.Less(armedEndTime, Time.unscaledTimeAsDouble,
                "Setup: both timers must already be overdue for the guard to be the only thing "
                + "holding them.");
            SetPrivateField(capture, "_multiStrokeTimerEndTime", armedEndTime);
            SetPrivateField(capture, "_strokeTimeoutEndTime", armedEndTime);

            yield return WaitFrames(5);

            Assert.AreEqual(armedEndTime, GetPrivateField<double>(capture, "_multiStrokeTimerEndTime"),
                "A multi-stroke window must not expire behind the pause menu.");
            Assert.AreEqual(armedEndTime, GetPrivateField<double>(capture, "_strokeTimeoutEndTime"),
                "A stroke timeout must not fire behind the pause menu.");
        }

        // ------------------------------------------------------------------
        // AC-2 — Resume continues the same attempt exactly once
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Resume_ContinuesTheSameAttemptExactlyOnce()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            gameManager.PauseGame();
            yield return null;

            gameManager.ResumeGame();
            gameManager.ResumeGame();
            yield return null;

            Assert.AreEqual(GameState.Playing, gameManager.CurrentState);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(1, _resumeRaises,
                "A duplicated resume re-arms every subscriber: enemies, audio and input.");
            Assert.IsTrue(gameManager.AcceptsDrawingInput);
        }

        [UnityTest]
        public IEnumerator Resume_UnderADialoguePause_DoesNotRestartCombat()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            gameManager.EnterDialoguePause();
            yield return null;

            gameManager.ResumeGame();
            yield return null;

            Assert.AreEqual(GameState.Paused, gameManager.CurrentState);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(0, _resumeRaises,
                "Resuming under an open dialogue box would restart combat behind it.");
        }

        // ------------------------------------------------------------------
        // AC-3 / AC-4 — confirmed restart and leave
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RestartTapped_OpensConfirmationAndAbortsNothingYet()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            CreateParkedSceneLoader();
            PauseMenuUI menu = CreatePauseMenu(out GameObject panel, out Button restart, out _);

            gameManager.PauseGame();
            yield return null;
            Assert.IsTrue(panel.activeSelf, "Setup: the pause panel must be open.");

            restart.onClick.Invoke();
            yield return null;

            Assert.AreEqual(0, _abortRaises,
                "AC-3: a restart is destructive and must be confirmed first.");
            GameObject confirmation = GetPrivateField<GameObject>(menu, "_confirmationPanel");
            Assert.IsNotNull(confirmation,
                "An unwired scene must still get a confirmation overlay, built at runtime.");
            Assert.IsTrue(confirmation.activeSelf);
        }

        [UnityTest]
        public IEnumerator ConfirmationCancelled_ReturnsToThePauseMenu()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            CreateParkedSceneLoader();
            PauseMenuUI menu = CreatePauseMenu(out GameObject panel, out Button restart, out _);

            // The pause has to come from GameManager, not a bare EventBus.RaiseGamePaused:
            // the event only drives the UI, and the state assertion below is about the
            // attempt still being paused when the player backs out of the overlay.
            gameManager.PauseGame();
            yield return null;
            Assert.AreEqual(GameState.Paused, gameManager.CurrentState,
                "Setup: the attempt must actually be paused before the overlay opens.");

            restart.onClick.Invoke();
            yield return null;

            ConfirmationButton(menu, "_confirmationCancelButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(0, _abortRaises, "Cancel must abort nothing.");
            Assert.IsFalse(GetPrivateField<GameObject>(menu, "_confirmationPanel").activeSelf);
            Assert.IsTrue(panel.activeSelf, "Cancel returns the player to the pause menu.");
            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState,
                "Cancel must not resume the attempt either.");
            Assert.AreEqual(0f, Time.timeScale, "A cancelled confirmation leaves the clock stopped.");
            Assert.AreEqual(0, _resumeRaises,
                "Cancel must not fire a resume: every subscriber would re-arm behind the menu.");
        }

        [UnityTest]
        public IEnumerator RestartConfirmed_AbortsOnceEvenOnADoubleTap()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            gameManager.CachePausedRunSnapshot(levelId: 1, currentHearts: 2);
            CreateParkedSceneLoader();
            PauseMenuUI menu = CreatePauseMenu(out GameObject panel, out Button restart, out _);

            gameManager.PauseGame();
            yield return null;
            restart.onClick.Invoke();
            yield return null;

            Button confirm = ConfirmationButton(menu, "_confirmationConfirmButton");
            confirm.onClick.Invoke();
            confirm.onClick.Invoke();
            yield return WaitFrames(3);

            Assert.AreEqual(1, _abortRaises,
                "AC-3: a double-tap must not tear the attempt down twice or load twice.");
            Assert.IsTrue(gameManager.IsAttemptAbortInProgress);
            Assert.IsFalse(gameManager.TryGetPausedRunLevelId(out _),
                "A restart must discard the snapshot or the old attempt's enemies come back.");
            Assert.IsFalse(panel.activeSelf, "The pause menu closes behind the transition.");
            Assert.AreEqual(1f, Time.timeScale, "The abort hands the next attempt an unpaused clock.");
        }

        [UnityTest]
        public IEnumerator LeaveConfirmed_AbortsTheAttemptAndKeepsTheResumeSnapshot()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            gameManager.CachePausedRunSnapshot(levelId: 3, currentHearts: 1);
            CreateParkedSceneLoader();
            PauseMenuUI menu = CreatePauseMenu(out _, out _, out Button quit);

            gameManager.PauseGame();
            yield return null;
            quit.onClick.Invoke();
            yield return null;

            ConfirmationButton(menu, "_confirmationConfirmButton").onClick.Invoke();
            yield return WaitFrames(3);

            Assert.AreEqual(1, _abortRaises,
                "AC-4: leaving must tear the attempt down exactly once.");
            Assert.IsTrue(gameManager.IsAttemptAbortInProgress);
            Assert.IsTrue(gameManager.TryGetPausedRunLevelId(out int levelId),
                "Unlike a restart, leaving keeps the level resumable.");
            Assert.AreEqual(3, levelId);
        }

        // The confirmation latch and SceneLoader's in-progress guard are separate pieces of
        // state. If the guard can decline AFTER the attempt has been aborted and the panel
        // hidden, the player is left in a torn-down level with no transition coming and every
        // button latched off. The two must be checked together, before anything destructive.
        [UnityTest]
        public IEnumerator ConfirmedWhileASceneLoadIsAlreadyInFlight_AbortsNothing()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            SceneLoader loader = CreateParkedSceneLoader();
            PauseMenuUI menu = CreatePauseMenu(out GameObject panel, out Button restart, out _);

            gameManager.PauseGame();
            yield return null;
            restart.onClick.Invoke();
            yield return null;

            // Something else got to the loader first — a deep link, or a transition already
            // running when the pause menu opened.
            SetPrivateField(loader, "_isLoading", true);
            SetPrivateField(loader, "_loadingSceneName", "AlreadyLoadingScene");

            ConfirmationButton(menu, "_confirmationConfirmButton").onClick.Invoke();
            yield return WaitFrames(3);

            Assert.AreEqual(0, _abortRaises,
                "A confirmation the load guard will refuse must not tear the attempt down.");
            Assert.IsFalse(GetPrivateField<bool>(menu, "_transitionRequested"),
                "The latch must not go down for a transition that never starts.");
            Assert.IsTrue(panel.activeSelf, "The pause menu must stay open behind the overlay.");
            Assert.AreEqual(GameState.Paused, gameManager.CurrentState);
        }

        [UnityTest]
        public IEnumerator Abort_ResetsPerAttemptCombatState()
        {
            GameManager gameManager = CreateGameManager();
            gameManager.StartGame();
            ComboManager combo = CreateComponent<ComboManager>("ComboManager");
            SetPrivateField(combo, "_currentStreak", 7);
            StrokeCapture capture = CreateStrokeCapture();
            SetPrivateField(capture, "_pendingRecognitionSubmit", true);
            SetPrivateField(capture, "_multiStrokeTimerEndTime", 5d);
            yield return null;

            gameManager.AbortCurrentLevelAttempt();
            yield return null;

            Assert.AreEqual(0, combo.CurrentStreak,
                "A combo earned in a discarded attempt must not carry into the next one.");
            Assert.IsFalse(GetPrivateField<bool>(capture, "_pendingRecognitionSubmit"),
                "A queued recognition submit must not survive the abort.");
            Assert.AreEqual(-1d, GetPrivateField<double>(capture, "_multiStrokeTimerEndTime"));
        }

        // ------------------------------------------------------------------
        // Fixture helpers
        // ------------------------------------------------------------------

        private GameManager CreateGameManager()
        {
            GameManager gameManager = CreateComponent<GameManager>("GameManager");
            SetSingletonInstance(gameManager);
            return gameManager;
        }

        // A SceneLoader that ACCEPTS LoadScene — the in-progress guard, the abort and every
        // pre-load step run exactly as they do in the game — but whose fade stub is stretched
        // far past the lifetime of a test, so LoadRoutine parks in the fade and never reaches
        // SceneManager.LoadSceneAsync. Latching _isLoading instead would make LoadScene
        // decline, which is now a refusal case in its own right (see
        // ConfirmedWhileASceneLoadIsAlreadyInFlight_AbortsNothing) rather than a neutral stub.
        private SceneLoader CreateParkedSceneLoader()
        {
            SceneLoader loader = CreateComponent<SceneLoader>("SceneLoader");
            SetSingletonInstance(loader);
            SetPrivateField(loader, "_fadeDuration", ParkedFadeSeconds);
            SetPrivateField(loader, "_loadingFadeInDuration", ParkedFadeSeconds);
            RegisterSceneLoaderRuntimeCanvases(loader);
            return loader;
        }

        // SceneLoader.Awake builds a fade canvas and a loading canvas, and Singleton.Awake
        // moves the loader hierarchy into the DontDestroyOnLoad scene. Both canvases are
        // children of the loader object the fixture already tracks; registering them too
        // keeps the teardown independent of that parenting. Added after the loader so the
        // reverse-order teardown destroys them before their parent.
        private void RegisterSceneLoaderRuntimeCanvases(SceneLoader loader)
        {
            CanvasGroup fade = GetPrivateField<CanvasGroup>(loader, "_fadeCanvasGroup");
            if (fade != null)
                _objectsToDestroy.Add(fade.gameObject);

            CanvasGroup loading = GetPrivateField<CanvasGroup>(loader, "_loadingCanvasGroup");
            if (loading != null)
                _objectsToDestroy.Add(loading.gameObject);
        }

        private PauseMenuUI CreatePauseMenu(
            out GameObject panel, out Button restartButton, out Button quitButton)
        {
            GameObject host = new GameObject("PauseMenuUI");
            _objectsToDestroy.Add(host);
            // Fields must be injected before Awake/OnEnable so the button listeners bind.
            host.SetActive(false);

            PauseMenuUI menu = host.AddComponent<PauseMenuUI>();

            panel = new GameObject("PausePanel");
            panel.transform.SetParent(host.transform, worldPositionStays: false);

            restartButton = CreateButton(host.transform, "RestartButton");
            quitButton = CreateButton(host.transform, "QuitButton");

            SetPrivateField(menu, "_panel", panel);
            SetPrivateField(menu, "_restartButton", restartButton);
            SetPrivateField(menu, "_quitButton", quitButton);

            host.SetActive(true);
            return menu;
        }

        private static Button ConfirmationButton(PauseMenuUI menu, string fieldName)
        {
            Button button = GetPrivateField<Button>(menu, fieldName);
            Assert.IsNotNull(button,
                $"The runtime confirmation overlay must supply {fieldName}.");
            return button;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(parent, worldPositionStays: false);
            return buttonObject.GetComponent<Button>();
        }

        private StrokeCapture CreateStrokeCapture()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            _objectsToDestroy.Add(cameraObject);

            GameObject canvasObject = new GameObject("DrawingCanvas");
            _objectsToDestroy.Add(canvasObject);
            DrawingCanvas canvas = canvasObject.AddComponent<DrawingCanvas>();

            RecognitionConfigSO config = ScriptableObject.CreateInstance<RecognitionConfigSO>();
            _objectsToDestroy.Add(config);

            GameObject host = new GameObject("StrokeCapture");
            _objectsToDestroy.Add(host);
            // Both references must exist before Awake, which logs an error otherwise.
            host.SetActive(false);
            StrokeCapture capture = host.AddComponent<StrokeCapture>();
            SetPrivateField(capture, "_config", config);
            SetPrivateField(capture, "_canvas", canvas);
            host.SetActive(true);
            return capture;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = new GameObject(name);
            T component = gameObject.AddComponent<T>();
            _objectsToDestroy.Add(gameObject);
            return component;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            return (T)field.GetValue(target);
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return field;
                type = type.BaseType;
            }

            return null;
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            MethodInfo setter = typeof(Singleton<T>)
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            MethodInfo setter = typeof(Singleton<T>)
                .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { null });
        }
    }
}
