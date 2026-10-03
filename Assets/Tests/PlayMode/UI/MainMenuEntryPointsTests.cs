using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.UI
{
    /// <summary>
    /// SALIN-256 — proves the runtime-built Exit button is ACTUALLY CREATED.
    ///
    /// WHY THIS FIXTURE EXISTS. The Exit button is built by cloning under a
    /// transform.Find lookup, and that lookup FAILS SILENTLY: a wrong name
    /// or a reparented template compiles, throws nothing, renders nothing, and passes every
    /// other test in the project. MemoryArchiveSceneWiringTests.cs:12-19 documents the same
    /// failure class for the archive button, and guards the SCENE half of it — that
    /// SettingsButton is still a direct child of the MainMenuUI carrying a Button. This fixture
    /// guards the CONSTRUCTION half: given that precondition, the objects get built and wired.
    /// The two are deliberately not merged; a second scene fixture would only re-assert what
    /// MemoryArchiveSceneWiringTests already covers.
    ///
    /// Construction tests use a stub hierarchy to exercise MainMenuUI.Start(). The authored
    /// scene regression also loads MainMenu.unity, because stub buttons cannot reproduce
    /// the persistent Settings callback saved on the real Exit button.
    /// </summary>
    [TestFixture]
    public sealed class MainMenuEntryPointsTests
    {
        private readonly List<GameObject> _objectsToDestroy = new();
        private Scene _loadedMainMenu;

        [UnityTearDown]
        public IEnumerator UnloadMainMenu()
        {
            if (_loadedMainMenu.IsValid() && _loadedMainMenu.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_loadedMainMenu);
            _loadedMainMenu = default;
        }

        [SetUp]
        public void SetUp()
        {
            // Released first because an earlier PlayMode fixture can leave a live manager behind:
            // with Instance still occupied, Singleton<T>.Awake treats this one as a duplicate and
            // schedules its destruction. Precedent and full reasoning: Level1EndToEndTests.cs:33-48.
            ReleaseSingleton<ProgressManager>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _objectsToDestroy)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
            _objectsToDestroy.Clear();

            // The manager is a static singleton; leaving it set would hand a destroyed object to
            // the next fixture, which is the exact leak Level1EndToEndTests guards against.
            ClearSingletonInstance<ProgressManager>();
        }

        [UnityTest]
        public IEnumerator AuthoredExitButton_ShowsOnlyConfirmation_AndSettingsStillWorks()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
            _loadedMainMenu = SceneManager.GetSceneByName("MainMenu");
            yield return null;

            MainMenuUI menu = null;
            foreach (GameObject root in _loadedMainMenu.GetRootGameObjects())
            {
                menu = root.GetComponentInChildren<MainMenuUI>(true);
                if (menu != null)
                    break;
            }
            Assert.IsNotNull(menu);
            SettingsPanel settings = (SettingsPanel)typeof(MainMenuUI)
                .GetField("_settingsPanel", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(menu);
            Assert.IsNotNull(settings);
            Assert.IsFalse(settings.gameObject.activeSelf);

            Button exit = menu.transform.Find(MainMenuUI.ExitButtonName).GetComponent<Button>();
            exit.onClick.Invoke();
            ExitConfirmationPanel panel = Object.FindFirstObjectByType<ExitConfirmationPanel>(
                FindObjectsInactive.Include);
            Assert.IsNotNull(panel);
            _objectsToDestroy.Add(panel.gameObject);
            Assert.IsTrue(panel.IsShowing);
            Assert.IsFalse(settings.gameObject.activeSelf,
                "Exit must not invoke the Settings callback saved on the authored button.");

            FindChildButton(panel.transform, "CancelButton").onClick.Invoke();
            Assert.IsFalse(panel.IsShowing);
            Assert.IsFalse(settings.gameObject.activeSelf);
            menu.transform.Find(MainMenuUI.ArchiveButtonTemplateName)
                .GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(settings.gameObject.activeSelf, "Settings must remain reachable.");
            settings.Hide();
        }

        [UnityTest]
        public IEnumerator Dialog_CentersCopy_AndCancelActionsRestoreSelection()
        {
            EventSystem previousEventSystem = EventSystem.current;
            GameObject eventHost = new GameObject("EventSystem", typeof(EventSystem));
            _objectsToDestroy.Add(eventHost);
            EventSystem events = eventHost.GetComponent<EventSystem>();
            GameObject previousSelection = new GameObject("ExitSelection");
            _objectsToDestroy.Add(previousSelection);
            EventSystem.current = events;
            try
            {
                events.SetSelectedGameObject(previousSelection);
                GameObject host = new GameObject("ExitConfirmationPanel");
                _objectsToDestroy.Add(host);
                ExitConfirmationPanel panel = host.AddComponent<ExitConfirmationPanel>();
                yield return null;
                Assert.IsTrue(panel.Present());

                TMP_Text body = host.transform.Find("Overlay/Card/Body").GetComponent<TMP_Text>();
                Assert.AreEqual("Are you sure you want to close the game?", body.text);
                Assert.AreEqual(TextAlignmentOptions.Center, body.alignment);
                Button cancel = FindChildButton(host.transform, "CancelButton");
                Button confirm = FindChildButton(host.transform, "ConfirmButton");
                Assert.AreEqual(cancel.gameObject, events.currentSelectedGameObject);
                Assert.AreEqual(confirm, cancel.navigation.selectOnRight);
                Assert.AreEqual(cancel, confirm.navigation.selectOnLeft);
                Assert.IsTrue(panel.Present(), "Repeated Exit presses must not replace return focus.");
                cancel.onClick.Invoke();
                Assert.IsFalse(panel.IsShowing);
                Assert.AreEqual(previousSelection, events.currentSelectedGameObject);

                // InputSystemUIInputModule delivers controller B/Circle to the selected
                // object's cancel handler, so both dialog buttons must receive it.
                foreach (Button selected in new[] { cancel, confirm })
                {
                    int cancelCalls = 0;
                    int confirmCalls = 0;
                    Assert.IsTrue(panel.Present(() => confirmCalls++, () => cancelCalls++));
                    events.SetSelectedGameObject(selected.gameObject);
                    BaseEventData cancelEvent = new BaseEventData(events);
                    Assert.IsTrue(ExecuteEvents.Execute(selected.gameObject, cancelEvent,
                        ExecuteEvents.cancelHandler));
                    Assert.IsTrue(cancelEvent.used);
                    Assert.IsFalse(panel.IsShowing);
                    Assert.AreEqual(1, cancelCalls);
                    Assert.AreEqual(0, confirmCalls, "Controller Cancel must never confirm Exit.");
                    Assert.AreEqual(previousSelection, events.currentSelectedGameObject);
                }
            }
            finally
            {
                if (previousEventSystem != null && previousEventSystem.isActiveAndEnabled)
                    EventSystem.current = previousEventSystem;
            }
        }

        [UnityTest]
        public IEnumerator Start_ClonesTheExitButtonFromTheSettingsTemplateAndWiresIt()
        {
            MainMenuUI menu = CreateMainMenu();
            yield return null;

            Transform exitButton = menu.transform.Find(MainMenuUI.ExitButtonName);
            Assert.IsNotNull(
                exitButton,
                "MainMenuUI.Start did not create '" + MainMenuUI.ExitButtonName + "'. The clone "
                + "is built from a transform.Find of '" + MainMenuUI.ArchiveButtonTemplateName
                + "' that fails silently, so without this assertion the menu would ship with no "
                + "way to quit and nothing would report it.");

            Button button = exitButton.GetComponent<Button>();
            Assert.IsNotNull(button, "The Exit clone must carry a Button.");
            Assert.IsTrue(button.interactable, "The Exit clone must be interactable.");

            // Pressing it is the only honest way to prove the listener was attached: a clone
            // that is created but never wired looks perfect on screen and does nothing.
            // Clicking is safe here — OnExitPressed only presents the modal, and the quit
            // itself sits behind ExitConfirmationPanel.QuitAction.
            System.Action original = ExitConfirmationPanel.QuitAction;
            int quitCalls = 0;
            ExitConfirmationPanel.QuitAction = () => quitCalls++;
            try
            {
                button.onClick.Invoke();
                yield return null;

                ExitConfirmationPanel panel = Object.FindFirstObjectByType<ExitConfirmationPanel>(
                    FindObjectsInactive.Include);
                Assert.IsNotNull(
                    panel,
                    "Pressing Exit did not present a confirmation. The button exists but its "
                    + "click listener was never wired.");
                if (panel != null)
                    _objectsToDestroy.Add(panel.gameObject);

                Assert.IsTrue(panel.IsShowing, "Pressing Exit must show the confirmation modal.");
                Assert.AreEqual(
                    0, quitCalls, "Pressing Exit must CONFIRM first, never quit immediately.");
            }
            finally
            {
                ExitConfirmationPanel.QuitAction = original;
            }
        }

        [UnityTest]
        public IEnumerator ConfirmingTheExitDialog_ReachesTheQuitSeam_AndCancellingDoesNot()
        {
            // Application.Quit() is a NO-OP IN THE EDITOR and would take the runner down in a
            // player, so the seam is the ONLY way this path can be asserted at all. The actual
            // quit still requires a manual check on an Android device.
            System.Action original = ExitConfirmationPanel.QuitAction;
            int quitCalls = 0;
            ExitConfirmationPanel.QuitAction = () => quitCalls++;

            try
            {
                GameObject host = new GameObject("ExitConfirmationPanel");
                _objectsToDestroy.Add(host);
                ExitConfirmationPanel panel = host.AddComponent<ExitConfirmationPanel>();
                yield return null;

                Assert.IsTrue(panel.Present(), "The Exit confirmation must build its surface.");
                Assert.IsTrue(panel.IsShowing, "Present must show the modal.");

                FindChildButton(host.transform, "CancelButton").onClick.Invoke();
                Assert.AreEqual(
                    0, quitCalls, "Cancelling the Exit dialog must never quit the application.");
                Assert.IsFalse(panel.IsShowing, "Cancelling must dismiss the modal.");

                Assert.IsTrue(panel.Present(), "The modal must be re-presentable after a cancel.");
                FindChildButton(host.transform, "ConfirmButton").onClick.Invoke();
                FindChildButton(host.transform, "ConfirmButton").onClick.Invoke();
                Assert.AreEqual(
                    1, quitCalls, "Confirming the Exit dialog must reach the quit call exactly once.");
            }
            finally
            {
                ExitConfirmationPanel.QuitAction = original;
            }
        }

        /// <summary>
        /// A stub menu carrying the one child the runtime clones depend on: a SettingsButton
        /// that is a DIRECT child and carries a Button. That precondition is what
        /// MemoryArchiveSceneWiringTests asserts against the real MainMenu.unity.
        /// </summary>
        private MainMenuUI CreateMainMenu()
        {
            GameObject host = new GameObject("MainMenuUI", typeof(RectTransform));
            _objectsToDestroy.Add(host);
            // Inactive until the hierarchy is complete, so Start() sees the template.
            host.SetActive(false);

            GameObject template = new GameObject(
                MainMenuUI.ArchiveButtonTemplateName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            template.transform.SetParent(host.transform, worldPositionStays: false);

            MainMenuUI menu = host.AddComponent<MainMenuUI>();
            host.SetActive(true);
            return menu;
        }

        private static Button FindChildButton(Transform root, string name)
        {
            foreach (Button candidate in root.GetComponentsInChildren<Button>(true))
            {
                if (candidate.gameObject.name == name)
                    return candidate;
            }

            Assert.Fail("The Exit confirmation did not build a '" + name + "'.");
            return null;
        }

        private static void ReleaseSingleton<T>() where T : MonoBehaviour
        {
            foreach (T existing in Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                    Object.DestroyImmediate(existing);
            }

            ClearSingletonInstance<T>();
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { null });
        }
    }
}
