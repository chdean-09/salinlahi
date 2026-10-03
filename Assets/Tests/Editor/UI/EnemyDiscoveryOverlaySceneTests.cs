using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class EnemyDiscoveryOverlaySceneTests
    {
        private const string OverlayPrefabPath = "Assets/Prefabs/UI/EnemyDiscoveryOverlay.prefab";
        private static readonly string[] GameplayScenePaths =
        {
            "Assets/_Scenes/Gameplay.unity",
            "Assets/_Scenes/Level_01_Tutorial.unity"
        };

        [Test]
        public void EnemyDiscoveryOverlayPrefab_IsPresentAndConfigured()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);

            Assert.NotNull(prefab, "Enemy discovery overlay prefab must exist so scene merges cannot silently drop the UI object.");
            EnemyDiscoveryOnboardingController controller = prefab.GetComponent<EnemyDiscoveryOnboardingController>();
            Assert.NotNull(controller, "Enemy discovery overlay prefab must include EnemyDiscoveryOnboardingController.");
            AssertRequiredReferences(controller, OverlayPrefabPath);
        }

        [Test]
        public void GameplayScenes_ContainConfiguredEnemyDiscoveryOverlay()
        {
            foreach (string scenePath in GameplayScenePaths)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                EnemyDiscoveryOnboardingController controller = Object.FindFirstObjectByType<EnemyDiscoveryOnboardingController>(FindObjectsInactive.Include);

                Assert.NotNull(controller, $"{scenePath} must contain an EnemyDiscoveryOnboardingController instance under the gameplay Canvas.");
                AssertRequiredReferences(controller, scenePath);
            }
        }

        private static void AssertRequiredReferences(EnemyDiscoveryOnboardingController controller, string context)
        {
            SerializedObject serializedController = new SerializedObject(controller);

            AssertAssigned(serializedController, "_canvasGroup", context);
            AssertAssigned(serializedController, "_targetFrame", context);
            AssertAssigned(serializedController, "_bodyText", context);
            AssertAssigned(serializedController, "_dismissButton", context);
        }

        private static void AssertAssigned(SerializedObject serializedObject, string propertyName, string context)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            Assert.NotNull(property, $"{propertyName} must exist on EnemyDiscoveryOnboardingController.");
            Assert.NotNull(property.objectReferenceValue, $"{context} must assign EnemyDiscoveryOnboardingController.{propertyName}.");
        }
    }
}
