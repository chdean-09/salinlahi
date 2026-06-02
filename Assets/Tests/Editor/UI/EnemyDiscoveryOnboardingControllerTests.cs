using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class EnemyDiscoveryOnboardingControllerTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private Camera _camera;

        [TearDown]
        public void TearDown()
        {
            TutorialRuntimeState.Clear();
            EnemyDiscoveryProgress.ResetForTests();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_WhenConfigured_ShowsOverlayForEnemy()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out RectTransform frame, out TextMeshProUGUI text, out Button button);
            EnemyDataSO data = CreateEnemyData("soldado");
            Enemy enemy = CreateEnemy(data);
            enemy.transform.position = new Vector3(0f, 4.5f, 0f);

            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return WaitFrames(2);
            enemy.transform.position = new Vector3(0f, 1f, 0f);
            yield return WaitFrames(6);

            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(group.blocksRaycasts);
            Assert.IsTrue(frame.gameObject.activeSelf);
            StringAssert.Contains("Soldado - The Conscripted Shadows", text.text);
            StringAssert.Contains("Power: Marches forward.", text.text);

            button.onClick.Invoke();
            Assert.AreEqual(0f, group.alpha);
            Object.DestroyImmediate(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_AfterDismiss_AllowsAnotherDiscoveryOverlay()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out _, out TextMeshProUGUI text, out Button button);
            EnemyDataSO soldadoData = CreateEnemyData("soldado");
            Enemy soldado = CreateEnemy(soldadoData);
            soldado.transform.position = new Vector3(0f, 4.5f, 0f);

            EventBus.RaiseEnemyDiscovered(soldadoData, soldado);
            yield return WaitFrames(2);
            soldado.transform.position = new Vector3(0f, 1f, 0f);
            yield return WaitFrames(6);

            Assert.AreEqual(1f, group.alpha);
            StringAssert.Contains("Soldado - The Conscripted Shadows", text.text);

            button.onClick.Invoke();
            Assert.AreEqual(0f, group.alpha);
            Object.DestroyImmediate(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_DuringInteractiveTutorial_DoesNotShowOverlay()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out _, out _, out _);
            EnemyDataSO data = CreateEnemyData("soldado");

            TutorialRuntimeState.Begin(LevelTutorialProgress.TutorialLevelNumber);
            Enemy enemy = CreateEnemy(data);
            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return null;
            yield return null;

            Assert.AreEqual(0f, group.alpha);
            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void ResolveRevealViewportY_UsesConfiguredThresholdWhenItIsInsideSafeArea()
        {
            float result = EnemyDiscoveryOnboardingController.ResolveRevealViewportY(
                0.72f,
                new Rect(0f, 40f, 1080f, 2200f),
                new Vector2Int(1080, 2400));

            Assert.AreEqual(0.72f, result, 0.001f);
        }

        [Test]
        public void ResolveRevealViewportY_ClampsToSafeAreaTopWhenConfiguredThresholdIsTooHigh()
        {
            float result = EnemyDiscoveryOnboardingController.ResolveRevealViewportY(
                0.92f,
                new Rect(0f, 0f, 1080f, 1800f),
                new Vector2Int(1080, 2400));

            Assert.AreEqual(0.73f, result, 0.001f);
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_AboveRevealThreshold_DoesNotShowImmediately()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out _, out _, out _);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 2f);
            EnemyDataSO data = CreateEnemyData("soldado");
            Enemy enemy = CreateEnemy(data);
            enemy.transform.position = new Vector3(0f, 4.5f, 0f);

            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(0f, group.alpha);
            Object.DestroyImmediate(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_ShowsAfterEnemyReachesRevealThreshold()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out RectTransform frame, out TextMeshProUGUI text, out Button button);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 2f);
            EnemyDataSO data = CreateEnemyData("soldado");
            Enemy enemy = CreateEnemy(data);
            enemy.transform.position = new Vector3(0f, 4.5f, 0f);

            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return null;
            yield return null;

            Assert.AreEqual(0f, group.alpha);

            enemy.transform.position = new Vector3(0f, 1f, 0f);
            yield return null;
            yield return null;

            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(frame.gameObject.activeSelf);
            StringAssert.Contains("Soldado - The Conscripted Shadows", text.text);
            StringAssert.Contains("Power: Marches forward.", text.text);

            button.onClick.Invoke();
            Object.DestroyImmediate(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_TimesOutBeforeThreshold_SkipsOverlay()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out _, out _, out _);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 0.05f);
            EnemyDataSO data = CreateEnemyData("soldado");
            Enemy enemy = CreateEnemy(data);
            enemy.transform.position = new Vector3(0f, 4.5f, 0f);

            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;

            Assert.AreEqual(0f, group.alpha);
            Object.DestroyImmediate(controller.gameObject);
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_BecomesInactiveBeforeThreshold_SkipsOverlay()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out _, out _, out _);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 2f);
            EnemyDataSO data = CreateEnemyData("soldado");
            Enemy enemy = CreateEnemy(data);
            enemy.transform.position = new Vector3(0f, 4.5f, 0f);

            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return null;
            enemy.gameObject.SetActive(false);
            yield return null;
            yield return null;

            Assert.AreEqual(0f, group.alpha);
            Object.DestroyImmediate(controller.gameObject);
        }

        private EnemyDiscoveryOnboardingController CreateController(
            out CanvasGroup group,
            out RectTransform frame,
            out TextMeshProUGUI text,
            out Button button)
        {
            GameObject canvasGo = new GameObject("EnemyDiscoveryCanvas");
            _objectsToDestroy.Add(canvasGo);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _camera = CreateCamera();

            GameObject controllerGo = new GameObject("EnemyDiscoveryOnboardingController");
            controllerGo.SetActive(false);
            controllerGo.transform.SetParent(canvasGo.transform, false);
            _objectsToDestroy.Add(controllerGo);

            group = controllerGo.AddComponent<CanvasGroup>();
            frame = new GameObject("Frame").AddComponent<RectTransform>();
            frame.SetParent(controllerGo.transform, false);
            text = new GameObject("BodyText").AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(controllerGo.transform, false);
            button = new GameObject("DismissButton").AddComponent<Button>();
            button.transform.SetParent(controllerGo.transform, false);

            EnemyDiscoveryOnboardingController controller = controllerGo.AddComponent<EnemyDiscoveryOnboardingController>();
            SetPrivateField(controller, "_canvasGroup", group);
            SetPrivateField(controller, "_targetFrame", frame);
            SetPrivateField(controller, "_bodyText", text);
            SetPrivateField(controller, "_dismissButton", button);
            SetPrivateField(controller, "_messageTemplate", "New enemy: {0}");
            SetPrivateField(controller, "_gameplayCamera", _camera);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 2f);
            SetPrivateField(controller, "_safeAreaViewportPadding", 0.02f);
            SetPrivateField(controller, "_spotlightPadding", new Vector2(36f, 36f));
            SetPrivateField(controller, "_dimOverlayColor", new Color(0f, 0f, 0f, 0.78f));
            controllerGo.SetActive(true);
            InvokePrivateMethod(controller, "OnDisable");
            InvokePrivateMethod(controller, "Awake");
            InvokePrivateMethod(controller, "OnEnable");

            return controller;
        }

        private Camera CreateCamera()
        {
            GameObject cameraGo = new GameObject("Main Camera");
            _objectsToDestroy.Add(cameraGo);
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static IEnumerator WaitFrames(int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
                yield return null;
        }

        private Enemy CreateEnemy(EnemyDataSO data)
        {
            GameObject go = new GameObject("Enemy_Discovery_UI_Test");
            _objectsToDestroy.Add(go);
            go.transform.position = Vector3.zero;
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_data", data);
            return enemy;
        }

        private EnemyDataSO CreateEnemyData(string enemyID)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            _objectsToDestroy.Add(data);
            return data;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            method.Invoke(target, null);
        }
    }
}
