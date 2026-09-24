using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-234 — the Level Results screen.
    ///
    /// The defect these tests exist to keep fixed: Gameplay.unity — the scene the game
    /// actually plays — does not author a Replay Level control, so the fix builds it at
    /// runtime rather than editing the scene; these tests are what make that observable.
    /// </summary>
    [TestFixture]
    public sealed class VictoryScreenResultsTests
    {
        private GameObject _screenObject;
        private GameObject _progressManagerObject;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_screenObject != null) Object.DestroyImmediate(_screenObject);
            if (_progressManagerObject != null) Object.DestroyImmediate(_progressManagerObject);
            ClearSingletonInstance<ProgressManager>();

            PlayerPrefs.DeleteKey(ProgressManager.SelectedLevelKey);
            for (int i = 1; i <= 15; i++)
            {
                PlayerPrefs.DeleteKey($"salinlahi.progress.unlocked.{i}");
                PlayerPrefs.DeleteKey($"salinlahi.progress.stars.{i}");
            }
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------
        // The runtime-built controls. See EnsureRuntimeControls for why these are built
        // rather than authored into the two scenes.
        // ------------------------------------------------------------------

        /// <summary>
        /// NOTE on falsifying this guard: uGUI silently repairs a missing RectTransform on an
        /// object that carries a Graphic, so removing only the RectTransform from a built
        /// control does NOT make this fail. The negative control has to remove the Graphic as
        /// well — the same trap documented at CampaignSaveNoticeSceneWiringTests.cs:63-70.
        /// </summary>
        [Test]
        public void RuntimeControls_AreBuiltUnderThePanel_AndEachCarriesARectTransform()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 1));

            GameObject panel = Panel(screen);
            Transform control = panel.transform.Find(VictoryScreenUI.RuntimeReplayButtonName);
            Assert.IsNotNull(
                control,
                "'" + VictoryScreenUI.RuntimeReplayButtonName + "' was not built under the " +
                "victory panel. Gameplay.unity authors none of these, so nothing else creates " +
                "them and the control is invisible at runtime with no error.");
            Assert.IsNotNull(
                control.GetComponent<RectTransform>(),
                "'" + VictoryScreenUI.RuntimeReplayButtonName + "' has a plain Transform, not " +
                "a RectTransform, so uGUI cannot lay it out or draw it.");
        }

        [Test]
        public void RuntimeControls_AreNotRebuiltOnASecondShow()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 1));
            screen.PresentResults(Results(stars: 3));

            GameObject panel = Panel(screen);
            int replayButtons = 0;
            foreach (Transform child in panel.transform)
            {
                if (child.name == VictoryScreenUI.RuntimeReplayButtonName)
                    replayButtons++;
            }

            Assert.AreEqual(
                1,
                replayButtons,
                "Show() runs on every completion, so the fallback construction must be " +
                "idempotent. Duplicated buttons stack invisibly and multiply the click.");
        }

        // ------------------------------------------------------------------
        // AC-8 / BTN-REPLAY.
        // ------------------------------------------------------------------

        [Test]
        public void ReplayButton_UsesTheApprovedLabelAndPresentsARaycastSurface()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResults(Results(stars: 1));

            Transform button = Panel(screen).transform.Find(VictoryScreenUI.RuntimeReplayButtonName);
            Assert.IsNotNull(button, "The Replay Level control was not built.");

            var replay = button.GetComponent<Button>();
            Assert.IsNotNull(replay, "The Replay Level control carries no Button.");
            Assert.IsNotNull(
                replay.targetGraphic,
                "The Replay Level button has no target Graphic, so it presents no raycast " +
                "surface and can never be clicked.");
            Assert.IsTrue(
                replay.targetGraphic.raycastTarget,
                "The Replay Level button's Graphic has raycastTarget disabled, so clicks pass " +
                "through it.");

            var label = button.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(label, "The Replay Level button has no label.");
            Assert.AreEqual(LevelResultsCopy.ReplayLevelLabel, label.text);
        }

        [Test]
        public void ReplayPressed_DoesNotAdvanceTheSelectedLevel()
        {
            ProgressManager progress = CreateProgressManager();
            progress.MarkLevelComplete(1, 2);
            progress.TrySetSelectedLevelNumber(1);
            VictoryScreenUI screen = CreateScreen();

            // No SceneLoader singleton exists in EditMode and creating one would really load a
            // scene, so the handler's "SceneLoader not available" LogError is expected noise.
            LogAssert.ignoreFailingMessages = true;
            InvokePrivate(screen, "OnReplayPressed");

            Assert.AreEqual(
                1,
                progress.GetSelectedLevelNumber(),
                "Replay reloads the level just finished. Calling TrySetSelectedLevelNumber " +
                "here would turn Replay into a second Next Level.");
        }

        [Test]
        public void NextLevelPressed_StillAdvances_ProvingTheReplayGuardDiscriminates()
        {
            ProgressManager progress = CreateProgressManager();
            progress.MarkLevelComplete(1, 2);
            progress.TrySetSelectedLevelNumber(1);
            VictoryScreenUI screen = CreateScreen();

            LogAssert.ignoreFailingMessages = true;
            InvokePrivate(screen, "OnNextLevelPressed");

            Assert.AreEqual(
                2,
                progress.GetSelectedLevelNumber(),
                "If this ever reads 1, the previous test proves nothing — both handlers would " +
                "be leaving the selected level alone for an unrelated reason.");
        }

        // ------------------------------------------------------------------
        // SALIN-253 AC-5 — era-aware Next Level suppression.
        // (This is SALIN-258's deferred AC-3, recorded at docs/audit/BACKLOG.md:719.)
        //
        // ⚠️ WHY THESE LEVELS AND NOT LEVEL 15.
        // The rule this replaced was `currentLevel >= 15`. That rule and a correct era-aware
        // rule AGREE ON EVERY LEVEL IN THE CAMPAIGN EXCEPT GLOBAL 5 AND GLOBAL 10 — the ends
        // of Ugat and Ugnayan. A Next-Level test written at level 15, which is the natural
        // first instinct, therefore passes whether or not AC-5 works and proves nothing.
        //
        // The pair below is global 5 (era-final: the button must be HIDDEN, where the old rule
        // SHOWED it) and global 4 (mid-era: still SHOWN). The second is the control — without
        // it, a change that simply hid the button always would satisfy the first.
        // ------------------------------------------------------------------

        [Test]
        public void NextLevelButton_IsHiddenAfterUgatLevel5()
        {
            ProgressManager progress = CreateProgressManager();
            progress.TrySetSelectedLevelNumber(5);
            VictoryScreenUI screen = CreateScreen();
            Button nextLevel = AttachNextLevelButton(screen);

            screen.PresentResults(Results(stars: 3), isEraFinalLevel: true);

            Assert.IsFalse(
                nextLevel.gameObject.activeSelf,
                "Ugat Level 5 is the end of an era. The old `currentLevel >= 15` rule left " +
                "Next Level showing here and advanced the player straight into Ugnayan " +
                "Level 1, skipping the era completion flow entirely.");
        }

        [Test]
        public void NextLevelButton_IsShownAfterUgatLevel4()
        {
            ProgressManager progress = CreateProgressManager();
            progress.TrySetSelectedLevelNumber(4);
            VictoryScreenUI screen = CreateScreen();
            Button nextLevel = AttachNextLevelButton(screen);

            screen.PresentResults(Results(stars: 3), isEraFinalLevel: false);

            Assert.IsTrue(
                nextLevel.gameObject.activeSelf,
                "Level 4 is mid-era and must still offer Next Level. Without this control, a " +
                "change that hid the button unconditionally would satisfy the test above.");
        }

        /// <summary>
        /// REGRESSION GUARD, NOT AC-5 COVERAGE. The campaign's final level hides Next Level
        /// under the old rule and the new one alike, so this case cannot discriminate between
        /// them. It exists only to prove the global fallback was not deleted along the way.
        /// </summary>
        [Test]
        public void NextLevelButton_IsStillHiddenAtTheFinalLevel_RegressionGuard()
        {
            ProgressManager progress = CreateProgressManager();
            progress.TrySetSelectedLevelNumber(15);
            VictoryScreenUI screen = CreateScreen();
            Button nextLevel = AttachNextLevelButton(screen);

            // No flag pushed — the legacy path. The global check must still hold on its own.
            screen.PresentResults(Results(stars: 3));

            Assert.IsFalse(
                nextLevel.gameObject.activeSelf,
                "The legacy progress path pushes no era flag, so `currentLevel >= 15` must " +
                "survive as the fallback. The degraded path must never GAIN a Next Level " +
                "button it did not have before.");
        }

        // ------------------------------------------------------------------
        // Structured results readout — star row, score, framed stats panel.
        // Built by PresentResultsSummary from LevelResultsViewData; runtime-built
        // under the panel for the same zero-scene-edit reason as the replay button.
        // ------------------------------------------------------------------

        [Test]
        public void PresentResultsSummary_BuildsStarRowScoreAndStatsPanel_ExactlyOnce()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResultsSummary(Data(stars: 2, heartsRemaining: 2, heartsMax: 3));
            screen.PresentResultsSummary(Data(stars: 3, heartsRemaining: 3, heartsMax: 3));

            GameObject panel = Panel(screen);
            // TEMP DISABLED (victory screen simplification): star row and score
            // readout are intentionally not rendered; their names are absent.
            foreach (string name in new[]
                     {
                         // VictoryScreenUI.RuntimeStarRowName,
                         // VictoryScreenUI.RuntimeScoreTextName,
                         VictoryScreenUI.RuntimeStatsPanelName,
                     })
            {
                int count = 0;
                foreach (Transform child in panel.GetComponentsInChildren<Transform>(true))
                    if (child.name == name)
                        count++;
                Assert.AreEqual(1, count,
                    "'" + name + "' must be built exactly once. ShowVictoryScreen runs on " +
                    "every completion path, so a duplicate build would stack invisibly.");
            }
        }

        // TEMP DISABLED (victory screen simplification): the star row is not
        // rendered and every assertion below is star-specific. Restore with the
        // star row.
        // [Test]
        public void PresentResultsSummary_RendersTheAttemptsStarsAsIcons()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResultsSummary(Data(stars: 2, heartsRemaining: 2, heartsMax: 3));

            Transform row = Panel(screen).transform.Find(VictoryScreenUI.RuntimeStarRowName);
            Assert.IsNotNull(row, "The star row was not built under the victory panel.");
            Assert.AreEqual(3, row.childCount, "The star row must always show all three slots.");

            int filled = 0;
            foreach (Transform star in row)
            {
                var image = star.GetComponent<Image>();
                Assert.IsNotNull(image, star.name + " carries no Image.");
                bool earned = image.sprite != null
                    ? image.sprite.name.Contains("full")
                    : image.color.r > 0.5f && image.color.g > 0.4f;
                if (earned)
                    filled++;
            }
            Assert.AreEqual(2, filled, "A two-star run must fill exactly two star icons.");
        }

        [Test]
        public void PresentResultsSummary_RendersHeartsAsIconsAndStatsAsText()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResultsSummary(Data(stars: 3, heartsRemaining: 2, heartsMax: 3,
                hintsUsed: 1, hintPenalty: 25));

            Transform stats = Panel(screen).transform.Find(VictoryScreenUI.RuntimeStatsPanelName);
            Assert.IsNotNull(stats, "The stats panel was not built.");

            // The hearts row moved out of the stats frame to the vacated star band.
            Transform hearts = Panel(screen).transform.Find("HeartsRow");
            Assert.IsNotNull(hearts, "The hearts row was not built under the victory panel.");
            Assert.AreEqual(3, hearts.childCount,
                "Three heart slots render for a three-heart level.");

            // TEMP DISABLED (victory screen simplification): hint lines hidden —
            // the stats text is only read by the commented asserts below.
            // var statsText = stats.Find("StatsText").GetComponent<TMP_Text>();
            // StringAssert.Contains("Hints 1", statsText.text);
            // StringAssert.Contains("Hint cost -25", statsText.text,
            //     "The emergency-hint penalty must render when it removed score points.");
        }

        [Test]
        public void PresentResultsSummary_OffersNoAccuracyReadout()
        {
            CreateProgressManager();
            VictoryScreenUI screen = CreateScreen();

            screen.PresentResultsSummary(Data(stars: 3, heartsRemaining: 3, heartsMax: 3));

            foreach (TMP_Text text in
                     Panel(screen).GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                string lower = text.text.ToLowerInvariant();
                Assert.IsFalse(lower.Contains("accuracy"),
                    "R1: no accuracy figure reaches the player. Found in: " + text.text);
                Assert.IsFalse(lower.Contains("%"),
                    "R1: the percentage readouts were the accuracy figures. Found in: " + text.text);
            }
        }

        // ------------------------------------------------------------------
        // OWNER RULING R1 (2026-09-13) + D-006.
        // ------------------------------------------------------------------

        [Test]
        public void ResultsCopy_OffersNoAccuracyReadoutAndNoTracingProse()
        {
            foreach (FieldInfo field in typeof(LevelResultsCopy).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                    continue;

                var value = (string)field.GetValue(null);
                Assert.IsFalse(
                    value.ToLowerInvariant().Contains("tracing"),
                    "LevelResultsCopy." + field.Name + " = \"" + value + "\" carries player-facing " +
                    "\"tracing\" prose. D-006 rules prose and UI strings say \"drawing\"; code " +
                    "identifiers are explicitly unchanged.");
                Assert.IsFalse(
                    value.ToLowerInvariant().Contains("accuracy"),
                    "LevelResultsCopy." + field.Name + " = \"" + value + "\" shows an accuracy " +
                    "figure. Owner ruling R1 cut the accuracy DISPLAY from this screen. It did " +
                    "NOT cut the scoring input — see LevelResultsScoringWeightPinTests.");
            }
        }

        // ------------------------------------------------------------------
        // Helpers.
        // ------------------------------------------------------------------

        private static LevelResults Results(int stars)
        {
            return new LevelResults(new Dictionary<string, float>(), stars);
        }

        private static LevelResultsViewData Data(
            int stars, int heartsRemaining, int heartsMax,
            int hintsUsed = 0, int hintPenalty = 0)
        {
            return new LevelResultsViewData
            {
                Stars = stars,
                HeartsRemaining = heartsRemaining,
                HeartsMax = heartsMax,
                HintsUsed = hintsUsed,
                HintPenaltyScorePoints = hintPenalty,
            };
        }

        private VictoryScreenUI CreateScreen()
        {
            _screenObject = new GameObject("VictoryScreen_Test");
            GameObject panel = new GameObject("VictoryPanel", typeof(RectTransform));
            panel.transform.SetParent(_screenObject.transform, false);
            panel.SetActive(false);
            VictoryScreenUI screen = _screenObject.AddComponent<VictoryScreenUI>();
            SetPrivateField(screen, "_panel", panel);
            return screen;
        }

        /// <summary>
        /// SALIN-253. Gives the screen a real _nextLevelButton to toggle.
        ///
        /// Gameplay.unity and Level_01_Tutorial.unity both author this field — it is covered by
        /// VictoryScreenSceneWiringTests:53 — but this fixture builds a bare screen, so without
        /// this the visibility branch would run against a null and every assertion about the
        /// button would silently pass on an object that never existed.
        /// </summary>
        private static Button AttachNextLevelButton(VictoryScreenUI screen)
        {
            GameObject buttonObject = new GameObject(
                "NextLevelButton_Test", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(Panel(screen).transform, false);

            var button = buttonObject.GetComponent<Button>();
            SetPrivateField(screen, "_nextLevelButton", button);
            return button;
        }

        private ProgressManager CreateProgressManager()
        {
            _progressManagerObject = new GameObject("ProgressManager_Test");
            ProgressManager progress = _progressManagerObject.AddComponent<ProgressManager>();
            // EditMode never runs Awake on AddComponent, and Awake is what assigns Instance.
            InvokePrivate(progress, "Awake");
            progress.ClearAllProgress();
            return progress;
        }

        private static GameObject Panel(VictoryScreenUI screen) =>
            GetPrivateField<GameObject>(screen, "_panel");

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing serialized field '" + name + "'.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing serialized field '" + name + "'.");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = null;
            for (System.Type type = target.GetType(); type != null && method == null; type = type.BaseType)
            {
                method = type.GetMethod(
                    methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            Assert.IsNotNull(method, "Missing method '" + methodName + "'.");
            method.Invoke(target, null);
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")
                .GetSetMethod(true)
                .Invoke(null, new object[] { null });
        }
    }
}
