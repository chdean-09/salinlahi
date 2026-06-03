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

        [Test]
        public void Awake_WhenSceneSerializedRevealFieldsAsZero_AppliesRuntimeDefaults()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(
                out _,
                out _,
                out _,
                out _,
                revealViewportYFromBottom: 0f,
                revealTimeoutSeconds: 0f);

            Assert.AreEqual(0.72f, GetPrivateField<float>(controller, "_revealViewportYFromBottom"), 0.001f);
            Assert.AreEqual(4f, GetPrivateField<float>(controller, "_revealTimeoutSeconds"), 0.001f);
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
        public IEnumerator EnemyDiscovered_WhenPhaserIsInvisible_WaitsUntilVisible()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out CanvasGroup group, out _, out _, out _);
            SetPrivateField(controller, "_revealViewportYFromBottom", 0.72f);
            SetPrivateField(controller, "_revealTimeoutSeconds", 2f);
            EnemyDataSO data = CreateEnemyData("fraile");
            data.isPhaser = true;
            Enemy enemy = CreateEnemy(data);
            PhaserEnemy phaser = enemy.gameObject.AddComponent<PhaserEnemy>();
            enemy.transform.position = new Vector3(0f, 1f, 0f);
            SpriteRenderer spriteRenderer = enemy.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateSprite(16, 16);
            spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            SetPrivateField(phaser, "_isVisible", false);

            EventBus.RaiseEnemyDiscovered(data, enemy);
            yield return null;
            yield return null;

            Assert.AreEqual(0f, group.alpha);

            SetPrivateField(phaser, "_isVisible", true);
            spriteRenderer.color = Color.white;
            yield return WaitFrames(6);

            Assert.AreEqual(1f, group.alpha);
            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void IsEnemyVisibleForDiscovery_WhenPhaserSpriteIsPartiallyTransparent_ReturnsFalse()
        {
            EnemyDataSO data = CreateEnemyData("fraile");
            data.isPhaser = true;
            Enemy enemy = CreateEnemy(data);
            enemy.gameObject.AddComponent<PhaserEnemy>();
            SpriteRenderer spriteRenderer = enemy.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateSprite(16, 16);
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.5f);

            bool result = InvokePrivateStatic<bool>(
                typeof(EnemyDiscoveryOnboardingController),
                "IsEnemyVisibleForDiscovery",
                enemy);

            Assert.IsFalse(result);
        }

        [Test]
        public void ResolveEnemyBounds_IncludesBadgeAndLargerChildRenderers()
        {
            EnemyDataSO data = CreateEnemyData("soldado");
            Enemy enemy = CreateEnemy(data);
            SpriteRenderer body = enemy.GetComponent<SpriteRenderer>();
            body.sprite = CreateSprite(16, 16);
            body.transform.localPosition = Vector3.zero;

            GameObject badgeGo = new GameObject("GlyphBadge");
            _objectsToDestroy.Add(badgeGo);
            badgeGo.transform.SetParent(enemy.transform, false);
            badgeGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            SpriteRenderer badge = badgeGo.AddComponent<SpriteRenderer>();
            badge.sprite = CreateSprite(16, 16);

            Bounds bounds = InvokePrivateStatic<Bounds>(
                typeof(EnemyDiscoveryOnboardingController),
                "ResolveEnemyBounds",
                enemy);

            Assert.Greater(bounds.max.y, body.bounds.max.y);
            Assert.Less(bounds.min.y, badge.bounds.min.y);
        }

        [Test]
        public void ResolveEnemyBounds_IgnoresEnemyDebugLabels()
        {
            EnemyDataSO data = CreateEnemyData("fraile");
            Enemy enemy = CreateEnemy(data);
            SpriteRenderer body = enemy.GetComponent<SpriteRenderer>();
            body.sprite = CreateSprite(16, 16);
            body.transform.localPosition = Vector3.zero;

            TextMeshPro drawLabel = CreateEnemyDebugLabel(enemy, "BaybayinLabel", "Draw: da (DA)", new Vector3(0f, -3f, 0f));
            TextMeshPro typeLabel = CreateEnemyDebugLabel(enemy, "EnemyTypeLabel", "Type: fraile", new Vector3(0f, -3.5f, 0f));

            Bounds bounds = InvokePrivateStatic<Bounds>(
                typeof(EnemyDiscoveryOnboardingController),
                "ResolveEnemyBounds",
                enemy);

            Assert.AreEqual(body.bounds.min.y, bounds.min.y, 0.001f);
            Assert.AreEqual(body.bounds.max.y, bounds.max.y, 0.001f);
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

        [Test]
        public void Awake_WhenTargetFrameUsesFilledImageOutline_MakesFrameNonBlocking()
        {
            EnemyDiscoveryOnboardingController controller = CreateController(out _, out RectTransform frame, out _, out _);
            Image frameImage = frame.GetComponent<Image>();
            Outline outline = frame.GetComponent<Outline>();

            Assert.NotNull(controller);
            Assert.AreEqual(0f, frameImage.color.a);
            Assert.IsFalse(frameImage.raycastTarget);
            Assert.IsTrue(outline.useGraphicAlpha);
        }

        private EnemyDiscoveryOnboardingController CreateController(
            out CanvasGroup group,
            out RectTransform frame,
            out TextMeshProUGUI text,
            out Button button,
            float revealViewportYFromBottom = 0.72f,
            float revealTimeoutSeconds = 2f)
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
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(1f, 0.86f, 0.38f, 1f);
            frameImage.raycastTarget = true;
            Outline frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = new Color(1f, 0.86f, 0.38f, 0.95f);
            frameOutline.useGraphicAlpha = false;
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
            SetPrivateField(controller, "_revealViewportYFromBottom", revealViewportYFromBottom);
            SetPrivateField(controller, "_revealTimeoutSeconds", revealTimeoutSeconds);
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

        private TextMeshPro CreateEnemyDebugLabel(Enemy enemy, string labelName, string text, Vector3 localPosition)
        {
            GameObject labelGo = new GameObject(labelName);
            _objectsToDestroy.Add(labelGo);
            labelGo.transform.SetParent(enemy.transform, false);
            labelGo.transform.localPosition = localPosition;
            TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.ForceMeshUpdate();
            return label;
        }

        private Sprite CreateSprite(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height);
            _objectsToDestroy.Add(texture);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;

            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 16f);
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            return (T)field.GetValue(target);
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

        private static T InvokePrivateStatic<T>(System.Type targetType, string methodName, params object[] args)
        {
            MethodInfo method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{targetType.Name}.{methodName} method not found.");
            return (T)method.Invoke(null, args);
        }
    }
}
