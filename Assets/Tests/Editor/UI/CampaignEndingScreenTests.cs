#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor
{
    public sealed class CampaignEndingScreenTests
    {
        private CampaignEndingScreenUI _screen;
        private RectTransform Credits => _screen.transform.Find(
            "EndingBackdrop/SafeArea/EndingScroll/CreditsViewport/Credits").GetComponent<RectTransform>();

        [SetUp]
        public void SetUp()
        {
            _screen = new GameObject("EndingTest", typeof(RectTransform)).AddComponent<CampaignEndingScreenUI>();
            _screen.Present();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_screen.gameObject);

        [Test]
        public void Present_ShowsEveryContributorAndOnlyMainMenuNavigation()
        {
            string credits = Credits.GetComponent<TMP_Text>().text;
            foreach (string name in new[] { "Chad Andrada", "Ian Clyde", "Jeff Andre Millan", "Jon Wayne Cabusbusan" })
                Assert.AreEqual(1, credits.Split(new[] { name }, System.StringSplitOptions.None).Length - 1);
            Assert.AreEqual(1, _screen.GetComponentsInChildren<Button>().Length);
            Assert.AreEqual(Navigation.Mode.None, _screen.GetComponentInChildren<Button>().navigation.mode);
            Assert.AreEqual("Menu", _screen.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text);
            RectTransform paper = _screen.transform.Find("EndingBackdrop/SafeArea/EndingScroll").GetComponent<RectTransform>();
            Assert.AreEqual(ScrollPanelArt.ScrollArea.min, paper.anchorMin);
            Assert.AreEqual(ScrollPanelArt.ScrollArea.max, paper.anchorMax);
            Assert.IsNotNull(paper.GetComponent<Image>().sprite);
            Assert.IsNotNull(_screen.GetComponentInChildren<SafeAreaHandler>());
            Assert.IsNull(_screen.GetComponentInChildren<ScrollRect>());
        }

        [Test]
        public void Credits_AutomaticallyMoveAtASlowReadingPace()
        {
            float before = Credits.anchoredPosition.y;
            for (int i = 0; i < 10; i++) _screen.AdvanceCredits(0.1f);
            Assert.That(Credits.anchoredPosition.y - before, Is.EqualTo(24f).Within(0.1f));
        }

        [Test]
        public void Credits_LoopContinuouslyWithoutStoppingOrDuplicatingControls()
        {
            RectTransform continuation = Credits.parent.Find("CreditsContinuation").GetComponent<RectTransform>();
            float cycle = Credits.anchoredPosition.y - continuation.anchoredPosition.y;
            Assert.Greater(cycle, Credits.rect.height);
            for (int i = 0; i < Mathf.CeilToInt(cycle / 2.4f) + 5; i++) _screen.AdvanceCredits(0.1f);
            float before = Credits.anchoredPosition.y;
            _screen.AdvanceCredits(0.1f);
            Assert.That(Credits.anchoredPosition.y - before, Is.EqualTo(2.4f).Within(0.1f));
            Assert.That(Credits.anchoredPosition.y - continuation.anchoredPosition.y, Is.EqualTo(cycle).Within(0.1f));
            Assert.IsTrue(_screen.gameObject.activeSelf);
            Assert.AreEqual(1, _screen.GetComponentsInChildren<Button>().Length);
        }

        [Test]
        public void PresentAgain_ResetsPositionWithoutDuplicatingControls()
        {
            _screen.AdvanceCredits(0.1f);
            _screen.Present();
            Assert.That(Credits.anchoredPosition.y, Is.Zero);
            Assert.AreEqual(1, _screen.GetComponentsInChildren<Button>().Length);
        }

        [Test]
        public void MissingSceneLoader_KeepsMenuAvailableAndCreditsMoving()
        {
            Assert.IsNull(SceneLoader.Instance);
            LogAssert.Expect(LogType.Error, "[Salinlahi] CampaignEndingScreenUI: SceneLoader not available.");
            _screen.ReturnToMainMenu();
            Assert.IsTrue(_screen.GetComponentInChildren<Button>().interactable);
            StringAssert.Contains("Pakisubukan muli", _screen.transform.Find("EndingBackdrop/SafeArea/EndingScroll/Status").GetComponent<TMP_Text>().text);
            _screen.AdvanceCredits(0.1f);
            Assert.Greater(Credits.anchoredPosition.y, 0f);
        }

        [TestCase(5)]
        [TestCase(10)]
        [TestCase(15)]
        public void FinalEraAction_OpensCreditsOnlyOnExplicitPress(int levelNumber)
        {
            var saveHost = new GameObject("EndingSaveTest");
            var flowHost = new GameObject("EndingFlowTest");
            var victoryHost = new GameObject("EndingVictoryTest", typeof(RectTransform));
            LevelFlowController flow = flowHost.AddComponent<LevelFlowController>();
            try
            {
                SaveManager save = saveHost.AddComponent<SaveManager>();
                typeof(Singleton<SaveManager>).GetProperty("Instance").GetSetMethod(true)
                    .Invoke(null, new object[] { save });
                save.SetCampaignForTests(AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
                    "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset"));
                VictoryScreenUI victory = victoryHost.AddComponent<VictoryScreenUI>();
                SetField(victory, "_panel", victoryHost);
                var nextHost = new GameObject("Next", typeof(RectTransform), typeof(Image), typeof(Button));
                nextHost.transform.SetParent(victoryHost.transform, false);
                var labelHost = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelHost.transform.SetParent(nextHost.transform, false);
                Button next = nextHost.GetComponent<Button>();
                SetField(victory, "_nextLevelButton", next);
                SetField(flow, "_victoryScreen", victory);
                SetField(flow, "_levelConfig", AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                    $"Assets/ScriptableObjects/Levels/Level{levelNumber}_Config.asset"));
                typeof(LevelFlowController).GetMethod("ShowVictoryScreen", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(flow, null);

                Assert.IsNull(GetField(flow, "_campaignEndingScreen"), "Credits must wait for the player's action.");
                Assert.AreEqual(levelNumber == 15, GetField(flow, "_eraCompletionScreen") == null);
                Assert.AreEqual(levelNumber == 15 ? "Magpatuloy" : "Susunod na Panahon",
                    labelHost.GetComponent<TMP_Text>().text);
                if (levelNumber == 15)
                {
                    typeof(VictoryScreenUI).GetMethod("OnNextLevelPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(victory, null);
                    var ending = (CampaignEndingScreenUI)GetField(flow, "_campaignEndingScreen");
                    Assert.IsNotNull(ending);
                    Assert.IsTrue(ending.gameObject.activeSelf);
                }
            }
            finally
            {
                var ending = GetField(flow, "_campaignEndingScreen") as CampaignEndingScreenUI;
                if (ending != null) Object.DestroyImmediate(ending.gameObject);
                var era = GetField(flow, "_eraCompletionScreen") as EraCompletionScreenUI;
                if (era != null) Object.DestroyImmediate(era.transform.root.gameObject);
                Object.DestroyImmediate(flowHost);
                Object.DestroyImmediate(victoryHost);
                Object.DestroyImmediate(saveHost);
            }
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static object GetField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
#endif
