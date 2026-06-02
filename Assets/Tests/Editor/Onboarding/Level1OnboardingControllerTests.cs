using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class Level1OnboardingControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyRuntimeObject("TutorialCanvas");
            DestroyRuntimeObject("TutorialSpotlightOverlay");
        }

        [Test]
        public void Awake_WhenSceneObjectHasNoBeatComponents_AddsDefaultBeatComponents()
        {
            GameObject host = new("Level1OnboardingControllerHost");
            try
            {
                host.AddComponent<Level1OnboardingController>();

                Assert.NotNull(host.GetComponent<ProtagonistIntroBeat>());
                Assert.NotNull(host.GetComponent<BaseIntroBeat>());
                Assert.NotNull(host.GetComponent<SoloTeachBeat>());
                Assert.NotNull(host.GetComponent<ComboTeachBeat>());
                Assert.NotNull(host.GetComponent<HeartLossDemoBeat>());
                Assert.NotNull(host.GetComponent<ReleaseBeat>());
                Assert.AreEqual(6, host.GetComponents<OnboardingBeat>().Length);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void DestroyRuntimeObject(string objectName)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < objects.Length; i++)
            {
                GameObject obj = objects[i];
                if (obj != null && obj.name == objectName)
                    Object.DestroyImmediate(obj);
            }
        }
    }
}
