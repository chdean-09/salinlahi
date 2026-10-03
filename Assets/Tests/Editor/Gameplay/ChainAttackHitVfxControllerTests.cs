using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class ChainAttackHitVfxControllerTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();
        }

        [Test]
        public void SpawnAtTarget_AlignsVfxBottomToEnemyFeet()
        {
            GameObject controllerGo = new GameObject("ChainVfxController_Test");
            ChainAttackHitVfxController controller = controllerGo.AddComponent<ChainAttackHitVfxController>();
            _objectsToDestroy.Add(controllerGo);

            SingleAttackHitSpriteVfx vfxPrefab = CreateVfxPrefabWithCenterPivotSprite();
            SetPrivateField(controller, "_chainVfxPrefab", vfxPrefab);
            // _detachFromEnemy defaults true, so SpawnAtTarget parents the
            // instance under _vfxRoot; point it at the controller so the
            // GetComponentsInChildren assertion below can see the spawn.
            SetPrivateField(controller, "_vfxRoot", controllerGo.transform);
            SetPrivateField(controller, "_useSpriteBoundsCenter", true);
            SetPrivateField(controller, "_anchorAtFeet", true);
            SetPrivateField(controller, "_anchorAtWaist", false);
            SetPrivateField(controller, "_worldOffset", Vector3.zero);

            Enemy enemy = CreateEnemyWithSprite(height: 4f, position: new Vector3(3f, 5f, 0f));

            InvokePrivateMethod(controller, "SpawnAtTarget", enemy);

            SingleAttackHitSpriteVfx[] spawned = controllerGo.GetComponentsInChildren<SingleAttackHitSpriteVfx>(true);
            Assert.AreEqual(1, spawned.Length, "Expected one spawned chain VFX instance.");

            SpriteRenderer enemyRenderer = enemy.GetComponent<SpriteRenderer>();
            SpriteRenderer vfxRenderer = spawned[0].GetComponent<SpriteRenderer>();
            float expectedFeetY = enemyRenderer.bounds.min.y;
            float actualVfxBottomY = vfxRenderer.bounds.min.y;

            Assert.AreEqual(expectedFeetY, actualVfxBottomY, 0.001f,
                "Chain lightning VFX bottom should align to enemy feet.");
        }

        private SingleAttackHitSpriteVfx CreateVfxPrefabWithCenterPivotSprite()
        {
            GameObject go = new GameObject("VfxPrefab");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            SingleAttackHitSpriteVfx vfx = go.AddComponent<SingleAttackHitSpriteVfx>();

            Sprite sprite = CreateSprite(width: 1f, height: 2f, pivot: new Vector2(0.5f, 0.5f));
            sr.sprite = sprite;

            SetPrivateField(vfx, "_spriteRenderer", sr);
            SetPrivateField(vfx, "_frames", new[] { sprite });
            SetPrivateField(vfx, "_framesPerSecond", 15f);

            _objectsToDestroy.Add(go);
            return vfx;
        }

        private Enemy CreateEnemyWithSprite(float height, Vector3 position)
        {
            GameObject go = new GameObject("Enemy_Test");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.transform.position = position;
            sr.sprite = CreateSprite(width: 1f, height: height, pivot: new Vector2(0.5f, 0.5f));
            _objectsToDestroy.Add(go);
            return enemy;
        }

        private Sprite CreateSprite(float width, float height, Vector2 pivot)
        {
            int pixelsPerUnit = 100;
            int w = Mathf.Max(2, Mathf.RoundToInt(width * pixelsPerUnit));
            int h = Mathf.Max(2, Mathf.RoundToInt(height * pixelsPerUnit));
            Texture2D tex = new Texture2D(w, h);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0, 0, w, h),
                pivot,
                pixelsPerUnit);

            _objectsToDestroy.Add(tex);
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
