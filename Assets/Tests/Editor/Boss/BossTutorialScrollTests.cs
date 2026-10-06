using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Boss
{
    [TestFixture]
    public class BossTutorialScrollTests
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
        public void Show_WhenFirstFrameNull_UsesFirstNonNullFrame()
        {
            BossTutorialScroll scroll = CreateScroll(out Image art);
            Sprite firstValid = CreateSprite(Color.green);

            scroll.Show(new[]
            {
                new BossTutorialPage
                {
                    title = "Frame fallback",
                    frames = new Sprite[] { null, firstValid },
                    animationFps = 0f,
                    effect = BossTutorialArtEffect.None,
                }
            });

            Assert.IsTrue(art.enabled);
            Assert.AreSame(firstValid, art.sprite);
        }

        [Test]
        public void RunArtEffects_FirstMoveNext_AppliesStaticTeleportScaleWithoutMovingPosition()
        {
            BossTutorialScroll scroll = CreateScroll(out Image art);
            Sprite sprite = CreateSprite(Color.red);
            RectTransform rt = art.rectTransform;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            SetPrivateField(scroll, "_teleportScale", 0.5f);
            SetPrivateField(scroll, "_teleportBounds", new Vector2(100f, 100f));
            SetPrivateField(scroll, "_artBaseAnchoredPos", Vector2.zero);
            SetPrivateField(scroll, "_artBaseScale", Vector3.one);
            SetPrivateField(scroll, "_artBaseStateCaptured", true);

            var page = new BossTutorialPage
            {
                title = "Teleport",
                frames = new[] { sprite },
                animationFps = 0f,
                effect = BossTutorialArtEffect.Teleporting,
            };

            IEnumerator routine = InvokePrivate<IEnumerator>(scroll, "RunArtEffects", page, 0);

            Assert.IsTrue(routine.MoveNext(), "Coroutine should yield once after applying static setup.");
            Assert.AreEqual(Vector2.zero, rt.anchoredPosition, "First tick must not randomize position before the first render.");
            Assert.AreEqual(Vector3.one * 0.5f, rt.localScale);
        }

        [TestCase(320f, 568f)]
        [TestCase(360f, 800f)]
        [TestCase(430f, 932f)]
        public void Show_LongBoldHeadingsKeepTheirInkInsideThePaper(float width, float height)
        {
            GameObject canvas = new("Boss heading test", typeof(RectTransform), typeof(Canvas));
            _objectsToDestroy.Add(canvas);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
            GameObject host = new("Boss scroll", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            ScrollPanelArt.SetAnchors(host.GetComponent<RectTransform>(), Rect.MinMaxRect(0.02f, 0.05f, 0.98f, 0.95f));
            BossTutorialScroll scroll = host.AddComponent<BossTutorialScroll>();
            TMP_Text title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            title.transform.SetParent(host.transform, false);
            title.font = TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(title.font);
            TMP_FontAsset authoredFont = title.font;
            title.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            SetPrivateField(scroll, "_title", title);
            foreach (string heading in new[] { "Pagpapatawag", "Pagteleport" })
            {
                scroll.Show(new[] { new BossTutorialPage { title = heading } });
                Canvas.ForceUpdateCanvases();
                title.ForceMeshUpdate();
                Assert.AreSame(authoredFont, title.font);
                Assert.GreaterOrEqual(title.fontSize, UITextScale.Body);
                Assert.IsFalse(title.isTextOverflowing, heading);
                Assert.AreEqual(1, title.textInfo.lineCount, heading + " must remain intact.");
                Assert.LessOrEqual(title.textBounds.size.x, title.rectTransform.rect.width, heading);
            }
        }

        private BossTutorialScroll CreateScroll(out Image art)
        {
            GameObject host = new("BossTutorialScrollHost");
            _objectsToDestroy.Add(host);
            BossTutorialScroll scroll = host.AddComponent<BossTutorialScroll>();

            GameObject artGo = new("Art");
            artGo.transform.SetParent(host.transform, false);
            _objectsToDestroy.Add(artGo);
            art = artGo.AddComponent<Image>();

            SetPrivateField(scroll, "_art", art);
            SetPrivateField(scroll, "_artBaseAnchoredPos", art.rectTransform.anchoredPosition);
            SetPrivateField(scroll, "_artBaseScale", art.rectTransform.localScale);
            SetPrivateField(scroll, "_artBaseStateCaptured", true);

            return scroll;
        }

        private Sprite CreateSprite(Color color)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            _objectsToDestroy.Add(tex);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            return (T)method.Invoke(target, args);
        }
    }
}
