using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class BaseZoneScalerTests
    {
        private GameObject _camGO;
        private AspectLockedCamera _alc;
        private GameObject _zoneGO;
        private BaseZoneScaler _scaler;
        private SpriteRenderer _sr;

        [SetUp]
        public void SetUp()
        {
            _camGO = new GameObject("AspectLockedCamera_Test");
            var cam = _camGO.AddComponent<Camera>();
            cam.orthographic = true;
            _alc = _camGO.AddComponent<AspectLockedCamera>();

            _zoneGO = new GameObject("BaseZone_Test");
            _sr = _zoneGO.AddComponent<SpriteRenderer>();
            // 32x32 white texture → 1u x 1u sprite at PPU 32.
            var tex = new Texture2D(32, 32);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            _sr.sprite = sprite;
            _sr.drawMode = SpriteDrawMode.Tiled;
            _sr.size = new Vector2(1f, 1f);

            _scaler = _zoneGO.AddComponent<BaseZoneScaler>();
            // Wire _playColumn via reflection so the scaler does not depend on FindFirstObjectByType ordering.
            FieldInfo f = typeof(BaseZoneScaler).GetField("_playColumn", BindingFlags.Instance | BindingFlags.NonPublic);
            f.SetValue(_scaler, _alc);
            // Re-invoke OnEnable now that _playColumn is set, so the subscription is established.
            typeof(BaseZoneScaler).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(_scaler, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_camGO != null) Object.DestroyImmediate(_camGO);
            if (_zoneGO != null) Object.DestroyImmediate(_zoneGO);
        }

        [Test]
        public void Rescale_UsesPlayColumnWidthPlusOverflow()
        {
            // Default _overflowPerSide = 0.5; _referenceWorldWidth = 11.25.
            // Expected width = 11.25 + 1.0 = 12.25.
            _scaler.Rescale();
            Assert.AreEqual(12.25f, _sr.size.x, 0.001f);
        }

        [Test]
        public void Rescale_FiresWhenAspectLockedCameraRaisesEvent()
        {
            _sr.size = new Vector2(99f, 1f); // poison the value
            MethodInfo recompute = typeof(AspectLockedCamera).GetMethod(
                "Recompute", BindingFlags.Instance | BindingFlags.NonPublic);
            recompute.Invoke(_alc, null);
            Assert.AreEqual(12.25f, _sr.size.x, 0.001f,
                "OnPlayAreaChanged should have re-driven the scaler's size.");
        }
    }
}
