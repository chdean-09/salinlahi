using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.UI
{
    /// <summary>
    /// SALIN-256 — proves the two runtime-built main-menu surfaces are ACTUALLY CREATED.
    ///
    /// WHY THIS FIXTURE EXISTS. Both the Exit button and the progress line are built by cloning
    /// or parenting under a transform.Find lookup, and that lookup FAILS SILENTLY: a wrong name
    /// or a reparented template compiles, throws nothing, renders nothing, and passes every
    /// other test in the project. MemoryArchiveSceneWiringTests.cs:12-19 documents the same
    /// failure class for the archive button, and guards the SCENE half of it — that
    /// SettingsButton is still a direct child of the MainMenuUI carrying a Button. This fixture
    /// guards the CONSTRUCTION half: given that precondition, the objects get built and wired.
    /// The two are deliberately not merged; a second scene fixture would only re-assert what
    /// MemoryArchiveSceneWiringTests already covers.
    ///
    /// The hierarchy below is a STUB, not MainMenu.unity — the precedent is
    /// PauseLifecycleTests.cs:377-403, which builds a host with named Button children the same
    /// way. Using a stub keeps this test independent of scene authoring while still exercising
    /// the real MainMenuUI.Start().
    /// </summary>
    [TestFixture]
    public sealed class MainMenuEntryPointsTests
    {
        private readonly List<GameObject> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            // MainMenuUI.EnsureProgressLine RETURNS WITHOUT BUILDING ANYTHING when
            // ProgressManager.Instance is null, and that refusal is correct: a progress readout
            // with no progress behind it would be untruthful, so the menu stays silent instead.
            // In the real game the manager is never absent — ProgressManager lives in
            // Bootstrap.unity (the only scene carrying one) and reaches MainMenu as a
            // DontDestroyOnLoad singleton. A stub scene without one therefore does not reproduce
            // the menu the player sees; it reproduces the degraded Editor case, and asserting the
            // line exists there would be asserting against a state the game never ships.
            //
            // So the fixture supplies the precondition rather than relaxing the assertion. The
            // assertion below still fails loudly if Start() stops building the line.
            //
            // Released first because an earlier PlayMode fixture can leave a live manager behind:
            // with Instance still occupied, Singleton<T>.Awake treats this one as a duplicate and
            // schedules its destruction. Precedent and full reasoning: Level1EndToEndTests.cs:33-48.
            ReleaseSingleton<ProgressManager>();
            GameObject progressHost = new GameObject("ProgressManager");
            _objectsToDestroy.Add(progressHost);
            SetSingletonInstance(progressHost.AddComponent<ProgressManager>());
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
        public IEnumerator Start_BuildsTheProgressLineLabelWithVisibleText()
        {
            MainMenuUI menu = CreateMainMenu();
            yield return null;

            Transform progressLine = menu.transform.Find(MainMenuUI.ProgressLineName);
            Assert.IsNotNull(
                progressLine,
                "MainMenuUI.Start did not create '" + MainMenuUI.ProgressLineName + "', so the "
                + "menu shows no era/level/completion readout at all.");

            TMPro.TextMeshProUGUI label = progressLine.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.IsNotNull(label, "The progress line must carry a TextMeshProUGUI.");

            // The exact string depends on saved progress, which this fixture does not control.
            // What must hold is that the label was POPULATED — an empty label is precisely the
            // silent failure this fixture exists to catch.
            Assert.IsNotEmpty(
                label.text,
                "The progress line was created but left blank, which renders as an invisible "
                + "label and is indistinguishable on screen from the line never being built.");
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
        ///
        /// The menu's OTHER precondition — a live ProgressManager — is a singleton rather than a
        /// child, so SetUp supplies it; see the note there.
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

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { instance });
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
