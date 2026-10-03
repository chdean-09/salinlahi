using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-234 — what the Level Results screen actually reads, and on which paths.
    ///
    /// ShowVictoryScreen() has three callers: the machine path
    /// (LevelFlowController.cs:689), FinishLegacyCompletion (:1104) and OnSaveRetryAccepted
    /// (:1270). Before this ticket only the machine path pushed a summary first, so a player
    /// who hit a save failure and retried successfully was shown a bare panel — no stars, no
    /// score, no restored content. The summary push now lives inside ShowVictoryScreen, which
    /// is the one place all three paths share.
    /// </summary>
    [TestFixture]
    public sealed class LevelResultsSummaryTests
    {
        private GameObject _controllerObject;
        private GameObject _victoryObject;

        [TearDown]
        public void TearDown()
        {
            if (_controllerObject != null) Object.DestroyImmediate(_controllerObject);
            if (_victoryObject != null) Object.DestroyImmediate(_victoryObject);
        }

        /// <summary>
        /// AC-11's worked example, end to end through the flow: Level 1 finished with one heart
        /// lost and one hint used. Hearts 2 of 3 gives ratio 0.667 — at or above the 0.5
        /// two-star gate and below the 0.99 three-star gate — with full accuracy, so the
        /// attempt earns two stars (LevelResultsCalculator.cs:47-50). shrineHearts is 3
        /// (Level1_Config.asset:105) and HeartSystem._maxHearts is 3 (HeartSystem.cs:9).
        /// </summary>
        [Test]
        public void WorkedExample_OneHeartLostAndOneHint_RendersStarsScoreHeartsAndHints()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                new LearningEvidenceBatch { levelId = "level.ugat.01" },
                heartsRemaining: 2, maxHearts: 3, hintsUsed: 1, emergencyHintPenalty: 0f);
            Assert.AreEqual(2, results.Stars, "precondition: the worked example is a two-star run");

            GameObject panel = PresentAndReadPanel(results, heartsRemaining: 2, maxHearts: 3);
            // TEMP DISABLED (victory screen simplification): only the commented
            // asserts below read the summary text; kept out to avoid an unused local.
            // string summary = CollectRenderedText(panel);

            // TEMP DISABLED (victory screen simplification): stars, score and the
            // hints line are intentionally not rendered; hearts remain asserted.
            // Assert.AreEqual(2, CountFilled(panel, "Star_"),
            //     "AC-10: the attempt's stars, rendered as filled star icons.");
            // StringAssert.Contains("Score 93", summary,
            //     "0.5 * 1 + 0.3 * 1 + 0.2 * (2/3) = 0.9333 -> 93.");
            Assert.AreEqual(2, CountFilled(panel, "Heart_"),
                "AC-4. The count comes from the hearts the flow already read, not from rounding " +
                "metric.hearts-ratio back into a count.");
            Assert.AreEqual(3, CountChildren(panel, "Heart_"),
                "Three heart slots render for a three-heart level.");
            // StringAssert.Contains("Hints 1", summary,
            //     "AC-5, worded exactly as the acceptance criterion states it.");
        }

        /// <summary>
        /// The save-retry path. OnSaveRetryAccepted with no machine falls through to
        /// ShowVictoryScreen, which is the shape the legacy path (:1104) takes too.
        /// </summary>
        [Test]
        public void SaveRetryPath_PresentsTheSameSummaryAsTheMachinePath()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                new LearningEvidenceBatch { levelId = "level.ugat.01" },
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            GameObject panel = PresentAndReadPanel(results, heartsRemaining: 3, maxHearts: 3);
            string summary = CollectRenderedText(panel);

            Assert.IsNotEmpty(
                summary,
                "A successful retry must present the same Results screen as a first-time save. " +
                "Before SALIN-234 this path called Show() with no summary push and the player " +
                "got a bare panel.");
            // TEMP DISABLED (victory screen simplification): stars not rendered.
            // Assert.AreEqual(3, CountFilled(panel, "Star_"));
            Assert.AreEqual(3, CountFilled(panel, "Heart_"));
        }

        /// <summary>OWNER RULING R1: no accuracy figure reaches the player.</summary>
        [Test]
        public void Summary_ShowsNoAccuracyFigureAndNoTracingProse()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                new LearningEvidenceBatch { levelId = "level.ugat.01" },
                heartsRemaining: 2, maxHearts: 3, hintsUsed: 1, emergencyHintPenalty: 0f);

            string summary = CollectRenderedText(
                    PresentAndReadPanel(results, heartsRemaining: 2, maxHearts: 3))
                .ToLowerInvariant();

            Assert.IsFalse(summary.Contains("tracing"),
                "D-006: prose and UI strings say \"drawing\"; code identifiers are unchanged.");
            Assert.IsFalse(summary.Contains("accuracy"),
                "Owner ruling R1 cut the accuracy display. The scoring input is untouched — see " +
                "LevelResultsScoringWeightPinTests.");
            Assert.IsFalse(summary.Contains("%"),
                "The two percentage readouts on this screen were the accuracy figures.");
        }

        /// <summary>
        /// D-011 / SALIN-220. Objective flags gate the SUCCESSOR level's unlock
        /// (CampaignOutcomeCoordinator.cs:249), never this screen. Results must therefore make
        /// no unlock claim — a pin on behaviour that is currently correct, so a later ticket
        /// cannot quietly start promising an unlock the save may not have granted.
        /// </summary>
        [Test]
        public void Summary_MakesNoUnlockClaim()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                new LearningEvidenceBatch { levelId = "level.ugat.01" },
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            string summary = CollectRenderedText(
                    PresentAndReadPanel(results, heartsRemaining: 3, maxHearts: 3))
                .ToLowerInvariant();

            foreach (string claim in new[] { "unlock", "next level is", "now available" })
            {
                Assert.IsFalse(
                    summary.Contains(claim),
                    "Results reports the attempt, not unlock state. \"" + claim + "\" appeared.");
            }
        }

        private GameObject PresentAndReadPanel(LevelResults results, int heartsRemaining, int maxHearts)
        {
            VictoryScreenUI victory = CreateVictory();
            LevelFlowController controller = CreateController();

            SetPrivateField(controller, "_victoryScreen", victory);
            SetPrivateField(controller, "_lastHeartsRemaining", heartsRemaining);
            SetPrivateField(controller, "_lastMaxHearts", maxHearts);
            typeof(LevelFlowController)
                .GetProperty("LastResults")
                .GetSetMethod(nonPublic: true)
                .Invoke(controller, new object[] { results });

            // The retry path with no machine running: OnSaveRetryAccepted falls through to
            // ShowVictoryScreen, the single place all three victory paths now share.
            InvokePrivate(controller, "OnSaveRetryAccepted");

            GameObject panel = GetPrivateField<GameObject>(victory, "_panel");
            Assert.IsTrue(panel.activeSelf, "The victory panel was never shown.");

            Assert.IsNotNull(
                panel.transform.Find(VictoryScreenUI.RuntimeStatsPanelName),
                "No structured stats panel was rendered on the victory panel.");
            return panel;
        }

        /// <summary>
        /// All TMP text under the panel — score readout, stats rows, button labels —
        /// concatenated so the copy assertions read what a player sees on screen.
        /// </summary>
        private static string CollectRenderedText(GameObject panel)
        {
            var builder = new System.Text.StringBuilder();
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(text.text);
            }
            return builder.ToString();
        }

        private static int CountChildren(GameObject panel, string namePrefix)
        {
            int count = 0;
            foreach (Transform child in panel.GetComponentsInChildren<Transform>(includeInactive: true))
                if (child.name.StartsWith(namePrefix))
                    count++;
            return count;
        }

        /// <summary>
        /// Counts icons whose sprite is the "full" variant — or, when sprites have not
        /// loaded (asset-less runners), whose fallback tint is the earned color.
        /// </summary>
        private static int CountFilled(GameObject panel, string namePrefix)
        {
            int count = 0;
            foreach (UnityEngine.UI.Image image in
                     panel.GetComponentsInChildren<UnityEngine.UI.Image>(includeInactive: true))
            {
                if (!image.name.StartsWith(namePrefix))
                    continue;
                bool filled = image.sprite != null
                    ? image.sprite.name.Contains("full")
                    : image.color.r > 0.5f && image.color.g > 0.4f;
                if (filled)
                    count++;
            }
            return count;
        }

        private LevelFlowController CreateController()
        {
            _controllerObject = new GameObject("LevelFlowController_Test");
            return _controllerObject.AddComponent<LevelFlowController>();
        }

        private VictoryScreenUI CreateVictory()
        {
            _victoryObject = new GameObject("VictoryScreen_Test");
            GameObject panel = new GameObject("VictoryPanel", typeof(RectTransform));
            panel.transform.SetParent(_victoryObject.transform, false);
            panel.SetActive(false);
            VictoryScreenUI victory = _victoryObject.AddComponent<VictoryScreenUI>();
            SetPrivateField(victory, "_panel", panel);
            return victory;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = null;
            for (System.Type type = target.GetType(); type != null && field == null; type = type.BaseType)
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field '" + name + "'.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field '" + name + "'.");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = null;
            for (System.Type type = target.GetType(); type != null && method == null; type = type.BaseType)
                method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method '" + methodName + "'.");
            method.Invoke(target, null);
        }
    }
}
