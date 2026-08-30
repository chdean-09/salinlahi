using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.UI
{
    /// <summary>
    /// The timed half of the discovery-overlay coverage: the reveal-timeout
    /// fallback measures real elapsed Time.unscaledTime, which stands still
    /// across batch-EditMode frames. The frame-driven reveal tests stay in the
    /// EditMode EnemyDiscoveryOnboardingControllerTests.
    /// </summary>
    [TestFixture]
    public class EnemyDiscoveryTimeoutPlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            TutorialRuntimeState.Clear();
            EnemyDiscoveryProgress.ResetForTests();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            TutorialRuntimeState.Clear();
            EnemyDiscoveryProgress.ResetForTests();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.Destroy(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator EnemyDiscovered_TimesOutWhileOnScreen_ShowsOverlayAtCurrentPosition()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 0.05f);
            EnemyDataSO data = CreateEnemyData("soldado");
            Enemy enemy = CreateEnemy(data);
            // On-screen (viewport y ~0.95) but above the 0.72 reveal band, so it
            // never reaches the ideal position — only the timeout fallback can
            // reveal it. Regression guard: this used to be abandoned (skipped),
            // which is why the first enemy of each type never made the almanac.
            enemy.transform.position = new Vector3(0f, 4.5f, 0f);

            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;
            yield return null;

            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(EnemyDiscoveryProgress.HasDiscovered(data));
            Object.Destroy(controller.gameObject);
        }

        private EnemyDiscoveryOnboardingController CreateController(out CanvasGroup group)
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
            RectTransform frame = new GameObject("Frame").AddComponent<RectTransform>();
            frame.SetParent(controllerGo.transform, false);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(1f, 0.86f, 0.38f, 1f);
            frameImage.raycastTarget = true;
            Outline frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = new Color(1f, 0.86f, 0.38f, 0.95f);
            frameOutline.useGraphicAlpha = false;
            TextMeshProUGUI text = new GameObject("BodyText").AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(controllerGo.transform, false);
            Button button = new GameObject("DismissButton").AddComponent<Button>();
            button.transform.SetParent(controllerGo.transform, false);

            EnemyDiscoveryOnboardingController controller = controllerGo.AddComponent<EnemyDiscoveryOnboardingController>();
            SetPrivateField(controller, "_canvasGroup", group);
            SetPrivateField(controller, "_targetFrame", frame);
            SetPrivateField(controller, "_bodyText", text);
            SetPrivateField(controller, "_dismissButton", button);
            SetPrivateField(controller, "_gameplayCamera", _camera);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 2f);
            SetPrivateField(controller, "_safeAreaViewportPadding", 0.02f);
            SetPrivateField(controller, "_spotlightPadding", new Vector2(36f, 36f));
            SetPrivateField(controller, "_dimOverlayColor", new Color(0f, 0f, 0f, 0.78f));
            // PlayMode runs Awake/OnEnable on activation, so no manual lifecycle
            // driving (unlike the EditMode fixture this test came from).
            controllerGo.SetActive(true);

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

        private Enemy CreateEnemy(EnemyDataSO data)
        {
            GameObject go = new GameObject("Enemy_Discovery_Timeout_Test");
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
            data.displayName = "Soldado";
            data.discoverySubtitle = "The Conscripted Shadows";
            data.description = "During the Spanish occupation, many natives were forced into military service under colonial command. They became symbols of obedience to foreign rule.\n\nPower: Marches forward.";
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
    }
}
